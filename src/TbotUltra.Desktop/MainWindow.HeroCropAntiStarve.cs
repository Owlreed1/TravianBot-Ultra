using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private sealed record HeroCropAntiStarveObservationState(
        DateTimeOffset ActionAt,
        string VillageName,
        bool Queued);

    private readonly Dictionary<string, HeroCropAntiStarveObservationState> _heroCropAntiStarveObservations =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<HeroCropAntiStarveVillageRow> BuildHeroCropAntiStarveVillageRows()
    {
        var account = _accountStore.ActiveAccountName();
        var serverUrl = LoadBotOptions().BaseUrl;
        return GetAllVillageKeyInfos()
            .Select(village => new HeroCropAntiStarveVillageRow(
                village.Key,
                village.Name,
                HeroCropAntiStarveSettingsStore.IsEnabled(
                    _projectRoot,
                    account,
                    serverUrl,
                    village.Key,
                    defaultIfMissing: true)))
            .ToList();
    }

    private void PersistHeroCropAntiStarveVillages(IEnumerable<HeroCropAntiStarveVillageRow> rows)
    {
        var options = LoadBotOptions();
        HeroCropAntiStarveSettingsStore.Save(
            _projectRoot,
            _accountStore.ActiveAccountName(),
            options.BaseUrl,
            rows.Select(row => (row.VillageKey, row.IsEnabled)));

        RemoveDisabledHeroCropAntiStarveTasks(options);
        SeedHeroCropAntiStarveObservations(options);
        ActivateDueHeroCropAntiStarveObservations(options);
        if (options.HeroCropAntiStarveEnabled && IsContinuousLoopRunning())
        {
            RequestContinuousAutomationWake();
        }
    }

    private void ObserveHeroCropAntiStarveStatus(
        VillageStatus status,
        string villageName,
        DateTimeOffset? observedAt = null)
    {
        var options = LoadBotOptions();
        var villageKey = VillageStatusCache.TryResolveCoordinateKey(villageName, status)
            ?? (_villageStatusCache.TryGetUniqueKeyByName(villageName, out var cachedKey) ? cachedKey : null);
        if (string.IsNullOrWhiteSpace(villageKey))
        {
            return;
        }

        var forecast = status.ResourceStorageForecasts?
            .FirstOrDefault(candidate => string.Equals(candidate.ResourceKey, "crop", StringComparison.OrdinalIgnoreCase));
        var decision = HeroCropAntiStarveObservationPlanner.Evaluate(
            forecast?.ProductionPerHour,
            forecast?.SecondsToEmpty,
            options.HeroCropAntiStarveTriggerMinutes);

        if (!IsHeroCropAntiStarveEnabled(options, villageKey))
        {
            _heroCropAntiStarveObservations.Remove(villageKey);
            RemovePendingHeroCropAntiStarveTask(villageKey);
            return;
        }

        switch (decision.Action)
        {
            case HeroCropAntiStarveObservationAction.NoObservation:
                return;
            case HeroCropAntiStarveObservationAction.Cancel:
                if (_heroCropAntiStarveObservations.Remove(villageKey))
                {
                    AppendLog($"[anti-starve:verbose] village='{villageName}' monitoring cleared; crop production is not negative.");
                }
                RemovePendingHeroCropAntiStarveTask(villageKey);
                return;
            case HeroCropAntiStarveObservationAction.Schedule:
                var snapshotTime = observedAt is { } timestamp && timestamp <= DateTimeOffset.UtcNow
                    ? timestamp
                    : DateTimeOffset.UtcNow;
                _heroCropAntiStarveObservations[villageKey] = new(
                    snapshotTime + decision.Delay,
                    villageName,
                    Queued: false);
                RemovePendingHeroCropAntiStarveTask(villageKey);
                AppendLog(
                    $"[anti-starve:verbose] village='{villageName}' negative crop observed; local action scheduled "
                    + $"in {Math.Ceiling(decision.Delay.TotalMinutes):0}m without browser navigation.");
                return;
            case HeroCropAntiStarveObservationAction.QueueNow:
                _heroCropAntiStarveObservations[villageKey] = new(DateTimeOffset.UtcNow, villageName, Queued: false);
                ActivateDueHeroCropAntiStarveObservations(options);
                return;
        }
    }

    private void SeedHeroCropAntiStarveObservations(BotOptions options)
    {
        if (!options.HeroCropAntiStarveEnabled)
        {
            _heroCropAntiStarveObservations.Clear();
            return;
        }

        foreach (var pair in _villageStatusCache.Snapshot)
        {
            if (!_heroCropAntiStarveObservations.ContainsKey(pair.Key))
            {
                ObserveHeroCropAntiStarveStatus(
                    pair.Value,
                    pair.Value.ActiveVillage,
                    pair.Value.ServerTimeUtc);
            }
        }
    }

    private void ActivateDueHeroCropAntiStarveObservations(BotOptions? options = null)
    {
        if (!_isLoggedIn)
        {
            return;
        }

        options ??= LoadBotOptions();
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _heroCropAntiStarveObservations.ToList())
        {
            var state = pair.Value;
            if (state.Queued || state.ActionAt > now || !IsHeroCropAntiStarveEnabled(options, pair.Key))
            {
                continue;
            }

            var village = GetAllKnownVillages().FirstOrDefault(candidate =>
                string.Equals(GetVillageKey(candidate), pair.Key, StringComparison.OrdinalIgnoreCase));
            if (village is null)
            {
                AppendLog($"[anti-starve] skipped action for unresolved village key='{pair.Key}'.");
                _heroCropAntiStarveObservations[pair.Key] = state with { Queued = true };
                continue;
            }

            var activeItem = _botService.GetQueueItemsForDisplay().Any(item =>
                string.Equals(item.TaskName, "anti_starve_hero_crop", StringComparison.OrdinalIgnoreCase)
                && item.Status is QueueStatus.Pending or QueueStatus.Running or QueueStatus.Paused
                && string.Equals(GetQueueItemVillageKey(item), pair.Key, StringComparison.OrdinalIgnoreCase));
            if (!activeItem)
            {
                _botService.EnqueueRuntime(
                    "anti_starve_hero_crop",
                    "Anti-starve hero crop",
                    BuildVillageRuntimePayload(village),
                    priority: 100,
                    maxRetries: 0);
                AppendLog($"[anti-starve] negative crop reached trigger in village='{state.VillageName}'; queued one live confirmation.");
                TriggerQueueAutoRunFromEnqueue();
            }

            _heroCropAntiStarveObservations[pair.Key] = state with { Queued = true };
        }
    }

    private bool IsHeroCropAntiStarveEnabled(BotOptions options, string villageKey)
        => options.HeroCropAntiStarveEnabled
            && HeroCropAntiStarveSettingsStore.IsEnabled(
                _projectRoot,
                _accountStore.ActiveAccountName(),
                options.BaseUrl,
                villageKey,
                defaultIfMissing: true);

    private void ResetHeroCropAntiStarveObservations()
        => _heroCropAntiStarveObservations.Clear();

    private void RemovePendingHeroCropAntiStarveTask(string villageKey)
    {
        foreach (var item in _botService.GetQueueItemsForDisplay().Where(item =>
                     string.Equals(item.TaskName, "anti_starve_hero_crop", StringComparison.OrdinalIgnoreCase)
                     && item.Status is QueueStatus.Pending or QueueStatus.Paused
                     && string.Equals(GetQueueItemVillageKey(item), villageKey, StringComparison.OrdinalIgnoreCase)))
        {
            _botService.RemoveQueueItem(item.Id);
            AppendLog($"[anti-starve:verbose] removed stale queued confirmation for village key='{villageKey}'.");
        }
    }

    private void RemoveDisabledHeroCropAntiStarveTasks(BotOptions options)
    {
        var account = _accountStore.ActiveAccountName();
        foreach (var item in _botService.GetQueueItemsForDisplay().Where(item =>
                     string.Equals(item.TaskName, "anti_starve_hero_crop", StringComparison.OrdinalIgnoreCase)))
        {
            var villageKey = GetQueueItemVillageKey(item);
            var enabled = options.HeroCropAntiStarveEnabled
                && HeroCropAntiStarveSettingsStore.IsEnabled(
                    _projectRoot,
                    account,
                    options.BaseUrl,
                    villageKey,
                    defaultIfMissing: true);
            if (!enabled)
            {
                _botService.RemoveQueueItem(item.Id);
                if (!string.IsNullOrWhiteSpace(villageKey))
                {
                    _heroCropAntiStarveObservations.Remove(villageKey);
                }
            }
        }

        if (!options.HeroCropAntiStarveEnabled)
        {
            _heroCropAntiStarveObservations.Clear();
        }
    }
}
