using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private static readonly TimeSpan IncomingAttackRefreshCooldown = TimeSpan.FromMinutes(1);
    private readonly ObservableCollection<IncomingAttackRowItem> _incomingAttackRows = [];
    private readonly Dictionary<string, List<IncomingAttack>> _incomingAttacksByVillage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IncomingAttackSignal> _incomingAttackPendingSignals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _incomingAttackLastReadUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _incomingAttackReadsInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _incomingAttackDorf1ClearVersions = new(StringComparer.OrdinalIgnoreCase);

    private bool IsIncomingAttackMonitoringActive() => IsContinuousLoopRunning() || _autoQueueRunning;

    private void ObserveIncomingAttackSignals(VillageStatus status)
    {
        if (status.IncomingAttackSignals is null || !IsIncomingAttackMonitoringActive())
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => ObserveIncomingAttackSignals(status));
            return;
        }

        var resolvedSignals = new List<(string Key, IncomingAttackSignal Signal)>();
        foreach (var signal in status.IncomingAttackSignals)
        {
            var resolved = ResolveIncomingAttackSignal(signal, status);
            if (resolved is null)
            {
                AppendLog($"[incoming-attacks] skipped ambiguous signal for '{signal.VillageName}'.");
                continue;
            }

            resolvedSignals.Add(resolved.Value);
        }

        var activeKey = VillageStatusCache.TryResolveCoordinateKey(status.ActiveVillage, status);
        if (activeKey is not null
            && resolvedSignals.All(signal => !string.Equals(signal.Key, activeKey, StringComparison.OrdinalIgnoreCase)))
        {
            ClearIncomingAttacksAfterAuthoritativeDorf1Read(activeKey, status.ActiveVillage);
        }

        foreach (var (villageKey, normalizedSignal) in resolvedSignals)
        {
            _incomingAttackPendingSignals[villageKey] = normalizedSignal;
            var shouldRead = !_incomingAttackLastReadUtc.TryGetValue(villageKey, out var lastRead)
                             || DateTimeOffset.UtcNow - lastRead >= IncomingAttackRefreshCooldown;
            if (shouldRead)
            {
                QueueIncomingAttackDetailsRead(villageKey, normalizedSignal);
            }
        }

        RefreshIncomingAttackUi();
        SaveIncomingAttackState();
    }

    private void ClearIncomingAttacksAfterAuthoritativeDorf1Read(string villageKey, string villageName)
    {
        CancelTroopEvasionForClearedVillage(villageKey);
        _incomingAttackDorf1ClearVersions[villageKey] =
            _incomingAttackDorf1ClearVersions.GetValueOrDefault(villageKey) + 1;
        var pendingRemoved = _incomingAttackPendingSignals.Remove(villageKey);
        var attacksRemoved = _incomingAttacksByVillage.Remove(villageKey);
        _incomingAttackLastReadUtc.Remove(villageKey);
        if (pendingRemoved || attacksRemoved)
        {
            AppendLog($"[incoming-attacks] clear Dorf1 read removed the warning for '{villageName}'.");
        }
    }

    private (string Key, IncomingAttackSignal Signal)? ResolveIncomingAttackSignal(
        IncomingAttackSignal signal,
        VillageStatus ownerStatus)
    {
        if (signal.CoordX.HasValue && signal.CoordY.HasValue)
        {
            var coordinateKey = $"xy:{signal.CoordX.Value}|{signal.CoordY.Value}";
            var known = ownerStatus.Villages.FirstOrDefault(village =>
                village.CoordX == signal.CoordX && village.CoordY == signal.CoordY);
            return (coordinateKey, signal with
            {
                VillageName = known?.Name ?? signal.VillageName,
                VillageUrl = known?.Url ?? signal.VillageUrl,
            });
        }

        var villages = ((DashboardVillageList.ItemsSource as IEnumerable<VillageSelectionItem>)
                        ?? (VillageComboBox.ItemsSource as IEnumerable<VillageSelectionItem>)
                        ?? [])
            .Where(village => !string.IsNullOrWhiteSpace(village.Name) && village.Name != "-")
            .ToList();
        var byDid = signal.VillageId.HasValue
            ? villages.Where(village => village.Url.Contains($"newdid={signal.VillageId.Value}", StringComparison.OrdinalIgnoreCase)).ToList()
            : [];
        var matches = byDid.Count == 1
            ? byDid
            : villages.Where(village => string.Equals(village.Name, signal.VillageName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count != 1)
        {
            return null;
        }

        var match = matches[0];
        var key = match.CoordX.HasValue && match.CoordY.HasValue
            ? $"xy:{match.CoordX.Value}|{match.CoordY.Value}"
            : GetVillageKey(match);
        return (key, signal with
        {
            VillageName = match.Name,
            VillageUrl = match.Url,
            CoordX = match.CoordX,
            CoordY = match.CoordY,
        });
    }

    private void QueueIncomingAttackDetailsRead(string villageKey, IncomingAttackSignal signal)
    {
        if (!_incomingAttackReadsInFlight.Add(villageKey))
        {
            return;
        }

        _incomingAttackLastReadUtc[villageKey] = DateTimeOffset.UtcNow;
        var browserGeneration = _botService.BrowserGeneration;
        var dorf1ClearVersion = _incomingAttackDorf1ClearVersions.GetValueOrDefault(villageKey);
        _ = ReadIncomingAttackDetailsAsync(villageKey, signal, browserGeneration, dorf1ClearVersion);
    }

    private async Task ReadIncomingAttackDetailsAsync(
        string villageKey,
        IncomingAttackSignal signal,
        long browserGeneration,
        long dorf1ClearVersion)
    {
        try
        {
            var snapshot = await _botService.ReadIncomingAttacksAsync(
                AutomationExecutionOptions.WithoutImplicitVillageTarget(LoadBotOptions()),
                AppendLog,
                signal.VillageName,
                signal.VillageUrl,
                villageKey,
                _loopController.AcquireSessionScopeToken());
            await Dispatcher.InvokeAsync(() =>
            {
                if (browserGeneration != _botService.BrowserGeneration)
                {
                    AppendLog("[incoming-attacks] discarded result from an old browser generation.");
                    return;
                }
                if (_incomingAttackDorf1ClearVersions.GetValueOrDefault(villageKey) != dorf1ClearVersion)
                {
                    AppendLog($"[incoming-attacks] discarded stale Rally Point result for '{signal.VillageName}' after a clear Dorf1 read.");
                    return;
                }

                var resolvedKey = snapshot.TargetVillageKey ?? villageKey;
                if (!snapshot.RallyPointReadSucceeded)
                {
                    _incomingAttackPendingSignals[resolvedKey] = signal with
                    {
                        Dorf1ArrivalTimesUtc = snapshot.Dorf1FallbackArrivalTimesUtc,
                        ObservedAtUtc = snapshot.ObservedAtUtc,
                    };
                    _incomingAttackLastReadUtc[resolvedKey] = DateTimeOffset.UtcNow;
                    RefreshIncomingAttackUi();
                    SaveIncomingAttackState();
                    return;
                }

                _incomingAttacksByVillage[resolvedKey] = snapshot.Attacks
                    .Where(attack => attack.ArrivalAtUtc > DateTimeOffset.UtcNow)
                    .OrderBy(attack => attack.ArrivalAtUtc)
                    .ToList();
                if (_incomingAttacksByVillage[resolvedKey].Count == 0)
                {
                    CancelTroopEvasionForClearedVillage(resolvedKey);
                }
                _incomingAttackPendingSignals.Remove(villageKey);
                if (!string.Equals(resolvedKey, villageKey, StringComparison.OrdinalIgnoreCase))
                {
                    _incomingAttackPendingSignals.Remove(resolvedKey);
                    _incomingAttacksByVillage.Remove(villageKey);
                }
                _incomingAttackLastReadUtc[resolvedKey] = DateTimeOffset.UtcNow;
                RefreshIncomingAttackUi();
                SaveIncomingAttackState();
            });
        }
        catch (OperationCanceledException)
        {
            // Pause/account switch leaves the pending warning visible for the next active run.
        }
        catch (Exception ex)
        {
            AppendLog($"[incoming-attacks] detail read for '{signal.VillageName}' failed: {ex.Message}");
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => _incomingAttackReadsInFlight.Remove(villageKey));
        }
    }

    private void TickIncomingAttacks(DateTimeOffset serverNow)
    {
        var nowUtc = serverNow.ToUniversalTime();
        var expiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _incomingAttacksByVillage.ToList())
        {
            var expired = pair.Value.Where(attack => attack.ArrivalAtUtc <= nowUtc).ToList();
            var active = pair.Value.Where(attack => attack.ArrivalAtUtc > nowUtc).ToList();
            if (active.Count != pair.Value.Count)
            {
                expiredKeys.Add(pair.Key);
                _incomingAttacksByVillage[pair.Key] = active;
                if (!_incomingAttackPendingSignals.ContainsKey(pair.Key) && expired.FirstOrDefault() is { } arrived)
                {
                    _incomingAttackPendingSignals[pair.Key] = new IncomingAttackSignal(
                        arrived.TargetVillageName,
                        CoordX: arrived.TargetCoordX,
                        CoordY: arrived.TargetCoordY,
                        ObservedAtUtc: nowUtc);
                }
            }
        }

        if (expiredKeys.Count > 0)
        {
            foreach (var key in expiredKeys)
            {
                if (_incomingAttackPendingSignals.TryGetValue(key, out var signal)
                    && IsIncomingAttackMonitoringActive())
                {
                    QueueIncomingAttackDetailsRead(key, signal);
                }
            }
            RefreshIncomingAttackUi();
            SaveIncomingAttackState();
        }

        if (IsIncomingAttackMonitoringActive())
        {
            foreach (var pending in _incomingAttackPendingSignals.ToList())
            {
                if (!_incomingAttackLastReadUtc.TryGetValue(pending.Key, out var lastAttempt)
                    || nowUtc - lastAttempt >= IncomingAttackRefreshCooldown)
                {
                    QueueIncomingAttackDetailsRead(pending.Key, pending.Value);
                }
            }
        }

        foreach (var row in _incomingAttackRows)
        {
            if (row.ArrivalAtUtc is not { } arrival)
            {
                row.CountdownText = "Reading…";
                continue;
            }

            var remaining = arrival - nowUtc;
            row.CountdownText = remaining <= TimeSpan.Zero
                ? "Arrived"
                : $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }
    }

    private void RefreshIncomingAttackUi()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(RefreshIncomingAttackUi);
            return;
        }

        var rows = _incomingAttacksByVillage
            .SelectMany(pair => pair.Value.Select(attack => CreateIncomingAttackRow(pair.Key, attack)))
            .ToList();
        foreach (var pending in _incomingAttackPendingSignals)
        {
            if (rows.Any(row => string.Equals(row.VillageKey, pending.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            rows.Add(new IncomingAttackRowItem
            {
                Id = $"pending:{pending.Key}",
                VillageKey = pending.Key,
                TargetVillageName = pending.Value.VillageName,
                IsReading = true,
                CountdownText = "Reading…",
            });
        }

        rows = rows.OrderBy(row => row.ArrivalAtUtc ?? DateTimeOffset.MinValue).ToList();
        _incomingAttackRows.Clear();
        foreach (var row in rows)
        {
            _incomingAttackRows.Add(row);
        }

        RefreshIncomingAttackVillageIndicators();
        SyncTroopEvasionVillages();
    }

    private IncomingAttackRowItem CreateIncomingAttackRow(string villageKey, IncomingAttack attack)
    {
        var sourceCoordinates = attack.SourceCoordX.HasValue && attack.SourceCoordY.HasValue
            ? $"({attack.SourceCoordX.Value} | {attack.SourceCoordY.Value})"
            : string.Empty;
        return new IncomingAttackRowItem
        {
            Id = attack.Id,
            VillageKey = villageKey,
            TargetVillageName = attack.TargetVillageName,
            MovementType = attack.MovementType,
            SourcePlayerName = attack.SourcePlayerName ?? string.Empty,
            SourceVillageName = attack.SourceVillageName ?? string.Empty,
            SourceCoordinatesText = sourceCoordinates,
            ArrivalAtUtc = attack.ArrivalAtUtc,
            ArrivalText = FormatQueueServerTime(attack.ArrivalAtUtc),
        };
    }

    private void RefreshIncomingAttackVillageIndicators()
    {
        var activeKeys = _incomingAttacksByVillage
            .Where(pair => pair.Value.Count > 0)
            .Select(pair => pair.Key)
            .Concat(_incomingAttackPendingSignals.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var source = (DashboardVillageList.ItemsSource as IEnumerable<VillageSelectionItem>) ?? [];
        foreach (var village in source)
        {
            var key = GetVillageKey(village);
            village.HasIncomingAttack = activeKeys.Contains(key);
            if (!village.HasIncomingAttack)
            {
                village.IncomingAttackTooltip = "No incoming attacks";
                continue;
            }

            var count = _incomingAttacksByVillage.TryGetValue(key, out var attacks) ? attacks.Count : 0;
            village.IncomingAttackTooltip = count > 0
                ? $"{count} incoming attack(s) — click for details"
                : "Incoming attack detected — reading details";
        }
    }

    private void IncomingAttackButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not VillageSelectionItem village)
        {
            return;
        }

        MainTabControl.SelectedItem = TroopsTabItem;
        TroopsHubPanelControl.SelectIncomingAttacks();
        UpdateSidebarSelection(TroopsNavButton);
        var key = GetVillageKey(village);
        var row = _incomingAttackRows
            .Where(candidate => string.Equals(candidate.VillageKey, key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.ArrivalAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
        if (row is null)
        {
            return;
        }

        TroopsHubPanelControl.IncomingAttacksGrid.SelectedItem = row;
        TroopsHubPanelControl.IncomingAttacksGrid.ScrollIntoView(row);
        TroopsHubPanelControl.IncomingAttacksGrid.Focus();
    }

    private void LoadIncomingAttacksForActiveAccount()
    {
        var state = _incomingAttackStore.Load(
            _accountStore.ActiveAccountName(),
            LoadBotOptions().BaseUrl,
            DateTimeOffset.UtcNow);
        _incomingAttacksByVillage.Clear();
        foreach (var attack in state.Attacks)
        {
            var key = attack.TargetVillageKey
                      ?? (attack.TargetCoordX.HasValue && attack.TargetCoordY.HasValue
                          ? $"xy:{attack.TargetCoordX.Value}|{attack.TargetCoordY.Value}"
                          : attack.TargetVillageName);
            if (!_incomingAttacksByVillage.TryGetValue(key, out var attacks))
            {
                attacks = [];
                _incomingAttacksByVillage[key] = attacks;
            }
            attacks.Add(attack);
        }

        _incomingAttackPendingSignals.Clear();
        foreach (var signal in state.PendingSignals)
        {
            var key = signal.CoordX.HasValue && signal.CoordY.HasValue
                ? $"xy:{signal.CoordX.Value}|{signal.CoordY.Value}"
                : signal.VillageName;
            _incomingAttackPendingSignals[key] = signal;
        }
        RefreshIncomingAttackUi();
        LoadTroopEvasionForActiveAccount();
    }

    private void SaveIncomingAttackState()
    {
        _incomingAttackStore.Save(
            _accountStore.ActiveAccountName(),
            LoadBotOptions().BaseUrl,
            _incomingAttacksByVillage.Values.SelectMany(attacks => attacks).ToList(),
            _incomingAttackPendingSignals.Values.ToList());
    }

    private void ClearIncomingAttackUiState()
    {
        _incomingAttacksByVillage.Clear();
        _incomingAttackPendingSignals.Clear();
        _incomingAttackLastReadUtc.Clear();
        _incomingAttackReadsInFlight.Clear();
        _incomingAttackDorf1ClearVersions.Clear();
        _incomingAttackRows.Clear();
        ClearTroopEvasionUiState();
    }
}
