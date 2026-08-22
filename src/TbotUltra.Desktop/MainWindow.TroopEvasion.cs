using System.Globalization;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private TroopEvasionState _troopEvasionState = TroopEvasionState.Default;
    private readonly HashSet<string> _troopEvasionCompletedMilestones = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TroopEvasionProtectionState> _troopEvasionProtections = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _troopEvasionAttemptCts;
    private bool _troopEvasionAttemptInFlight;
    private bool _troopEvasionWaitingForSafeReturn;
    private DateTimeOffset? _troopEvasionCurrentArrivalUtc;
    private string? _troopEvasionCurrentVillageKey;
    private bool _suppressTroopEvasionSave;

    private void LoadTroopEvasionForActiveAccount()
    {
        _suppressTroopEvasionSave = true;
        try
        {
            _troopEvasionState = _troopEvasionStore.Load(
                _accountStore.ActiveAccountName(), LoadBotOptions().BaseUrl, DateTimeOffset.UtcNow);
            _troopEvasionProtections.Clear();
            foreach (var protection in _troopEvasionState.Protections)
                _troopEvasionProtections[protection.VillageKey] = protection;
            TroopsHubPanelControl.EvasionPanel.SetGlobalSettings(
                _troopEvasionState.LeadTimeMinutes,
                _troopEvasionState.ProtectionWindowMinutes,
                _troopEvasionState.TargetX,
                _troopEvasionState.TargetY,
                _troopEvasionState.MovementType,
                _troopEvasionState.EvadeRaids,
                _troopEvasionState.EvadeAttacks);
            SyncTroopEvasionVillages();
        }
        finally { _suppressTroopEvasionSave = false; }
    }

    private void SyncTroopEvasionVillages()
    {
        if (_troopEvasionStore is null || TroopsHubPanelControl?.EvasionPanel is null) return;
        var villages = ((DashboardVillageList.ItemsSource as IEnumerable<VillageSelectionItem>) ?? [])
            .Where(village => village.CoordX.HasValue && village.CoordY.HasValue)
            .ToList();
        var existing = TroopsHubPanelControl.EvasionPanel.Villages.ToDictionary(item => item.VillageKey, StringComparer.OrdinalIgnoreCase);
        var saved = (_troopEvasionState.Villages ?? []).ToDictionary(item => item.VillageKey, StringComparer.OrdinalIgnoreCase);
        foreach (var village in villages)
        {
            var key = GetVillageKey(village);
            if (existing.ContainsKey(key)) continue;
            var settings = saved.GetValueOrDefault(key) ?? new TroopEvasionVillageSettings(
                key, village.Name, village.Url, SelectedTroopSlots: Enumerable.Range(1, 10).ToList());
            var item = TroopEvasionVillageItem.Create(village, settings with
            {
                TargetX = TroopsHubPanelControl.EvasionPanel.TargetX,
                TargetY = TroopsHubPanelControl.EvasionPanel.TargetY,
                MovementType = TroopsHubPanelControl.EvasionPanel.MovementType,
            });
            TroopsHubPanelControl.EvasionPanel.Villages.Add(item);
        }
        foreach (var stale in TroopsHubPanelControl.EvasionPanel.Villages.Where(item => villages.All(v => !string.Equals(GetVillageKey(v), item.VillageKey, StringComparison.OrdinalIgnoreCase))).ToList())
            TroopsHubPanelControl.EvasionPanel.Villages.Remove(stale);
        UpdateTroopEvasionRuntimeStatuses();
    }

    private void TroopEvasionSettingsChanged()
    {
        if (_suppressTroopEvasionSave) return;
        _troopEvasionAttemptCts?.Cancel();
        foreach (var village in TroopsHubPanelControl.EvasionPanel.Villages)
        {
            if (village.Enabled && !TryBuildTroopEvasionSettings(village, out _, out var error))
            {
                _suppressTroopEvasionSave = true;
                village.Enabled = false;
                village.RuntimeStatus = error;
                _suppressTroopEvasionSave = false;
            }
        }
        SaveTroopEvasionState();
        UpdateTroopEvasionRuntimeStatuses();
        SyncVillageProtectionSettingsRows();
    }

    private void SaveTroopEvasionState()
    {
        var settings = TroopsHubPanelControl.EvasionPanel.Villages
            .Select(item => TryBuildTroopEvasionSettings(item, out var value, out _) ? value : BuildUncheckedSettings(item))
            .ToList();
        _troopEvasionState = new TroopEvasionState(
            TroopsHubPanelControl.EvasionPanel.LeadTimeMinutes,
            TroopsHubPanelControl.EvasionPanel.ProtectionWindowMinutes,
            settings,
            _troopEvasionProtections.Values.ToList(),
            TroopsHubPanelControl.EvasionPanel.TargetX,
            TroopsHubPanelControl.EvasionPanel.TargetY,
            TroopsHubPanelControl.EvasionPanel.MovementType,
            TroopsHubPanelControl.EvasionPanel.EvadeRaids,
            TroopsHubPanelControl.EvasionPanel.EvadeAttacks);
        _troopEvasionStore.Save(_accountStore.ActiveAccountName(), LoadBotOptions().BaseUrl, _troopEvasionState);
    }

    private static bool TryBuildTroopEvasionSettings(TroopEvasionVillageItem item, out TroopEvasionVillageSettings settings, out string error)
    {
        settings = BuildUncheckedSettings(item);
        var requirements = GetTroopEvasionSettingsRequirements(item, settings);
        if (requirements.Count > 0)
        {
            error = $"Complete the following:\n• {string.Join("\n• ", requirements)}";
            return false;
        }
        settings = settings with
        {
            TargetX = int.Parse(item.TargetX, NumberStyles.Integer, CultureInfo.InvariantCulture),
            TargetY = int.Parse(item.TargetY, NumberStyles.Integer, CultureInfo.InvariantCulture),
        };
        error = string.Empty;
        return true;
    }

    private static IReadOnlyList<string> GetTroopEvasionSettingsRequirements(
        TroopEvasionVillageItem item,
        TroopEvasionVillageSettings settings)
    {
        var requirements = new List<string>();
        if (!int.TryParse(item.TargetX, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(item.TargetY, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            || x is < -400 or > 400 || y is < -400 or > 400)
        {
            requirements.Add("Enter valid target X and Y coordinates (-400 to 400).");
        }
        if (settings.SelectedTroopSlots!.Count == 0 && !settings.IncludeHero)
        {
            requirements.Add("Select at least one troop or Hero.");
        }
        if (!Enum.IsDefined(settings.MovementType))
        {
            requirements.Add("Select a valid movement type.");
        }
        return requirements;
    }

    private string? ValidateTroopEvasionEnable(TroopEvasionVillageItem item)
    {
        var settings = BuildUncheckedSettings(item);
        var requirements = GetTroopEvasionSettingsRequirements(item, settings).ToList();
        if (!TroopsHubPanelControl.EvasionPanel.EvadeRaids
            && !TroopsHubPanelControl.EvasionPanel.EvadeAttacks)
        {
            requirements.Add("Select Raids, Attacks, or both under Evade for.");
        }

        return requirements.Count == 0
            ? null
            : $"Complete the following:\n• {string.Join("\n• ", requirements)}";
    }

    private static TroopEvasionVillageSettings BuildUncheckedSettings(TroopEvasionVillageItem item) => new(
        item.VillageKey, item.VillageName, item.VillageUrl, item.Enabled,
        int.TryParse(item.TargetX, out var x) ? x : null,
        int.TryParse(item.TargetY, out var y) ? y : null,
        item.MovementType,
        item.Units.Where(unit => unit.IsSelected).Select(unit => unit.Slot).ToList(),
        item.IncludeHero);

    private void TickTroopEvasion(DateTimeOffset serverNow)
    {
        var active = IsIncomingAttackMonitoringActive();
        if (!active)
        {
            _troopEvasionAttemptCts?.Cancel();
            UpdateTroopEvasionRuntimeStatuses();
            return;
        }
        var now = serverNow.ToUniversalTime();
        var settings = _troopEvasionState.Villages.ToDictionary(item => item.VillageKey, StringComparer.OrdinalIgnoreCase);
        var attacks = _incomingAttacksByVillage
            .Where(pair => IsIncomingAttackMonitoringEnabled(pair.Key))
            .SelectMany(pair => pair.Value.Select(attack => (pair.Key, attack)))
            .ToList();
        foreach (var pending in _incomingAttackPendingSignals)
        {
            if (!IsIncomingAttackMonitoringEnabled(pending.Key)) continue;
            foreach (var arrival in pending.Value.Dorf1ArrivalTimesUtc ?? [])
            {
                attacks.Add((pending.Key, new IncomingAttack(
                    $"dorf1:{pending.Key}:{arrival.UtcTicks}", pending.Value.VillageName, arrival,
                    IncomingAttackMovementType.Unknown, pending.Key, pending.Value.CoordX, pending.Value.CoordY,
                    ObservedAtUtc: pending.Value.ObservedAtUtc)));
            }
        }
        var due = TroopEvasionScheduler.SelectMostUrgent(
            attacks, settings, _troopEvasionProtections, _troopEvasionCompletedMilestones, now,
            _troopEvasionState.LeadTimeMinutes,
            _troopEvasionState.EvadeRaids,
            _troopEvasionState.EvadeAttacks);
        if (_troopEvasionAttemptInFlight)
        {
            if (_troopEvasionWaitingForSafeReturn
                && due is not null
                && _troopEvasionCurrentArrivalUtc is { } currentArrival
                && due.Attack.ArrivalAtUtc < currentArrival)
            {
                AppendLog($"[troop-evasion] canceling confirmation wait for a more urgent attack on '{due.Attack.TargetVillageName}'.");
                _troopEvasionAttemptCts?.Cancel();
            }
            return;
        }
        if (due is not null)
        {
            var village = ((DashboardVillageList.ItemsSource as IEnumerable<VillageSelectionItem>) ?? [])
                .FirstOrDefault(candidate => string.Equals(GetVillageKey(candidate), due.VillageKey, StringComparison.OrdinalIgnoreCase));
            if (due.Milestone == "lead"
                && village is not null
                && TryGetCachedVillageStatus(village, out var cachedStatus)
                && cachedStatus.HasTroopsAtHome == false
                && cachedStatus.TroopPresenceObservedAtUtc is { } observedAt
                && now - observedAt.ToUniversalTime() <= TimeSpan.FromMinutes(2))
            {
                _troopEvasionCompletedMilestones.Add(
                    TroopEvasionScheduler.MilestoneKey(due.VillageKey, due.Attack, due.Milestone));
                var row = TroopsHubPanelControl.EvasionPanel.Villages.FirstOrDefault(item =>
                    string.Equals(item.VillageKey, due.VillageKey, StringComparison.OrdinalIgnoreCase));
                if (row is not null) row.RuntimeStatus = "No troops at home — Rally Point skipped";
                return;
            }
            _ = RunTroopEvasionAttemptAsync(due);
        }
    }

    private async Task RunTroopEvasionAttemptAsync(TroopEvasionDueWork due)
    {
        if (_troopEvasionAttemptInFlight) return;
        var item = TroopsHubPanelControl.EvasionPanel.Villages.FirstOrDefault(v => string.Equals(v.VillageKey, due.VillageKey, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;
        if (!TryBuildTroopEvasionSettings(item, out var settings, out var error))
        { item.RuntimeStatus = error; return; }

        _troopEvasionAttemptInFlight = true;
        _troopEvasionCurrentArrivalUtc = due.Attack.ArrivalAtUtc;
        _troopEvasionCurrentVillageKey = due.VillageKey;
        _troopEvasionCompletedMilestones.Add(TroopEvasionScheduler.MilestoneKey(due.VillageKey, due.Attack, due.Milestone));
        _troopEvasionAttemptCts = CancellationTokenSource.CreateLinkedTokenSource(_loopController.AcquireSessionScopeToken());
        var generation = _botService.BrowserGeneration;
        item.RuntimeStatus = "Preparing evasion…";
        try
        {
            var request = CreateTroopEvasionRequest(settings, due.Attack.ArrivalAtUtc);
            var progress = new Progress<TroopEvasionProgress>(value =>
            {
                item.RuntimeStatus = value.Message;
                _troopEvasionWaitingForSafeReturn = value.State == TroopEvasionProgressState.WaitingForSafeReturn;
            });
            var result = await _botService.SendTroopEvasionAsync(
                AutomationExecutionOptions.WithoutImplicitVillageTarget(LoadBotOptions()), request, AppendLog, progress, _troopEvasionAttemptCts.Token);
            if (generation != _botService.BrowserGeneration) return;
            item.RuntimeStatus = result.Message;
            if (result.Succeeded)
            {
                var protection = TroopEvasionScheduler.CreateProtection(
                    due.VillageKey, due.Attack.ArrivalAtUtc, result.ConfirmedAtUtc ?? DateTimeOffset.UtcNow,
                    _troopEvasionState.ProtectionWindowMinutes);
                _troopEvasionProtections[due.VillageKey] = protection;
                SaveTroopEvasionState();
            }
            else if (due.Milestone == "retry-30s")
            {
                AppendLog($"[ALARM] [troop-evasion] final attempt failed for '{item.VillageName}': {result.Message}");
            }
        }
        catch (OperationCanceledException) { item.RuntimeStatus = "Canceled; will recalculate."; }
        catch (Exception ex) { item.RuntimeStatus = ex.Message; AppendLog($"[troop-evasion] '{item.VillageName}' failed: {ex.Message}"); }
        finally
        {
            _troopEvasionAttemptCts?.Dispose();
            _troopEvasionAttemptCts = null;
            _troopEvasionAttemptInFlight = false;
            _troopEvasionWaitingForSafeReturn = false;
            _troopEvasionCurrentArrivalUtc = null;
            _troopEvasionCurrentVillageKey = null;
        }
    }

    private TroopEvasionRequest CreateTroopEvasionRequest(TroopEvasionVillageSettings settings, DateTimeOffset arrival) => new(
        settings.VillageName, settings.VillageUrl, settings.VillageKey, settings.TargetX!.Value, settings.TargetY!.Value,
        settings.MovementType, settings.SelectedTroopSlots ?? [], settings.IncludeHero, arrival, TimeSpan.FromSeconds(15));

    private async void TroopEvasionValidateRequested(TroopEvasionVillageItem item)
    {
        if (!TryBuildTroopEvasionSettings(item, out var settings, out var error)) { item.RuntimeStatus = error; return; }
        await RunGuardedOperationAsync("Validate troop evasion", "Troop evasion validation canceled.", _ => { }, async (_, token) =>
        {
            item.RuntimeStatus = "Validating…";
            var arrival = _incomingAttacksByVillage.GetValueOrDefault(item.VillageKey)?.OrderBy(a => a.ArrivalAtUtc).FirstOrDefault()?.ArrivalAtUtc
                          ?? DateTimeOffset.UtcNow.AddHours(24);
            var result = await _botService.ValidateTroopEvasionAsync(
                AutomationExecutionOptions.WithoutImplicitVillageTarget(LoadBotOptions()),
                CreateTroopEvasionRequest(settings, arrival), AppendLog, token);
            item.RuntimeStatus = result.Message;
            return result.Message;
        });
    }

    private void UpdateTroopEvasionRuntimeStatuses()
    {
        var active = IsIncomingAttackMonitoringActive();
        foreach (var item in TroopsHubPanelControl.EvasionPanel.Villages)
        {
            if (!item.Enabled) item.RuntimeStatus = "Disabled";
            else if (!IsIncomingAttackMonitoringEnabled(item.VillageKey)) item.RuntimeStatus = "Incoming monitoring disabled";
            else if (!active) item.RuntimeStatus = "Paused";
            else if (!_troopEvasionState.EvadeRaids && !_troopEvasionState.EvadeAttacks)
                item.RuntimeStatus = "No incoming types selected";
            else if (_troopEvasionProtections.TryGetValue(item.VillageKey, out var protection))
                item.RuntimeStatus = $"Protected through {FormatQueueServerTime(protection.ProtectedThroughUtc)}";
            else item.RuntimeStatus = "Watching incoming attacks";
        }
    }

    private void ClearTroopEvasionUiState()
    {
        _troopEvasionAttemptCts?.Cancel();
        _troopEvasionCompletedMilestones.Clear();
        _troopEvasionProtections.Clear();
        TroopsHubPanelControl.EvasionPanel.Villages.Clear();
        _troopEvasionState = TroopEvasionState.Default;
    }

    private void CancelTroopEvasionForClearedVillage(string villageKey)
    {
        if (_troopEvasionAttemptInFlight
            && string.Equals(_troopEvasionCurrentVillageKey, villageKey, StringComparison.OrdinalIgnoreCase))
        {
            AppendLog($"[troop-evasion] canceling unconfirmed attempt because '{villageKey}' no longer has an incoming attack.");
            _troopEvasionAttemptCts?.Cancel();
        }
    }
}
