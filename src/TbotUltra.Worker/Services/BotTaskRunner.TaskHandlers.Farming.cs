using TbotUltra.Core.Configuration;
using TbotUltra.Core.Accounts;
using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Infrastructure;
using System.Text.Json;

namespace TbotUltra.Worker.Services;

public sealed partial class BotTaskRunner
{
    private static async Task WriteFarmListsSnapshotAsync(TaskExecutionContext context, IReadOnlyList<FarmListOverview> overview)
    {
        try
        {
            var activeAccount = context.Runner._accountProvider.LoadAccount().Name;
            var outputPath = AccountStoragePaths.FarmListsSnapshotPath(context.Runner._projectContext.RootPath, activeAccount);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var payload = new
            {
                account = activeAccount,
                capturedAtUtc = DateTimeOffset.UtcNow,
                lists = overview.Where(item => item is not null).Select(item => new
                {
                    item.Name, item.VillageName, item.VillageIndex, item.ActiveFarmCount, item.TotalFarmCount, item.RemainingSeconds, item.ListId, item.Capacity, item.FarmCoordinates,
                }).ToList(),
            };
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(payload), context.CancellationToken);
        }
        catch (Exception ex)
        {
            context.Log($"Could not write farm list snapshot: {ex.Message}");
        }
    }

    private static async Task ExecuteSendFarmlistsAsync(TaskExecutionContext context)
    {
        var mode = FarmingDefaults.NormalizeSendMode(context.Options.ContinuousFarmSendMode);
        var minDelaySeconds = FarmingDefaults.NormalizeDispatchDelayMinMinutes(context.Options.ContinuousFarmDispatchDelayMinMinutes) * 60;
        var maxDelaySeconds = Math.Max(minDelaySeconds, FarmingDefaults.NormalizeDispatchDelayMaxMinutes(context.Options.ContinuousFarmDispatchDelayMaxMinutes) * 60);
        var dispatchDelaySeconds = FarmingDefaults.CalculateDispatchDelaySeconds(context.Options.ContinuousFarmDispatchDelayMinMinutes, context.Options.ContinuousFarmDispatchDelayMaxMinutes);
        context.Log($"Continuous farming mode={mode}; delayRange={minDelaySeconds}-{maxDelaySeconds}s; selectedDelay={dispatchDelaySeconds}s; targetVillage='{(string.IsNullOrWhiteSpace(context.Options.TargetVillageName) ? "(default)" : context.Options.TargetVillageName)}'; deactivateLosses={context.Options.ContinuousFarmDeactivateLosses}; deactivateOasis={context.Options.ContinuousFarmDeactivateOasisLosses}.");

        if (string.Equals(mode, FarmingDefaults.SendModeAllAtOnce, StringComparison.Ordinal))
        {
            await ExecuteSendAllFarmlistsAsync(context, dispatchDelaySeconds);
            return;
        }

        await ExecuteSendFarmlistsListPerListAsync(context, dispatchDelaySeconds);
    }

    private static async Task ExecuteSendFarmlistsListPerListAsync(TaskExecutionContext context, int dispatchDelaySeconds)
    {
        var selectedNames = (context.Options.ContinuousFarmListNames ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var selectedIds = (context.Options.ContinuousFarmListIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedNames.Count <= 0 && selectedIds.Count <= 0)
        {
            throw new InvalidOperationException("No farm lists selected for continuous farming.");
        }

        bool IsSelected(FarmListOverview item) => (item.ListId is not null && selectedIds.Contains(item.ListId)) || selectedNames.Contains(item.Name, StringComparer.OrdinalIgnoreCase);
        var overview = await context.Client.ReadFarmListsOverviewAsync(context.CancellationToken);
        var matchingLists = overview.Where(item => item is not null && IsSelected(item)).ToList();
        if (matchingLists.Count <= 0)
        {
            var retryWaitSeconds = dispatchDelaySeconds;
            context.Log(overview.Count > 0
                ? $"Continuous farming: none of the selected farm lists ({string.Join(", ", selectedNames)}) were found on the farm page. They may have been renamed — re-analyze and re-select. Retrying in {retryWaitSeconds}s."
                : $"Continuous farming: no farm lists were found on the farm page. Retrying in {retryWaitSeconds}s.");
            throw BuildContinuousFarmDefer("Selected farm lists were not found on the farm page.", retryWaitSeconds, 0);
        }

        // "Send toggled lists" mode: each round sends EVERY toggled list that is ready, one at a time (the
        // small "Send farmlists" pacing sits between each list). The dispatch delay below is the gap between
        // whole rounds, not between individual lists. If none is ready yet, defer until the soonest one is.
        var readyLists = matchingLists.Where(item => item.RemainingSeconds is null or <= 0).ToList();
        if (readyLists.Count <= 0)
        {
            var soonestRemaining = matchingLists.Min(item => item.RemainingSeconds is > 0 ? item.RemainingSeconds.Value : 1);
            var waitSeconds = Math.Max(1, soonestRemaining + Random.Shared.Next(5, 16));
            context.Log($"Continuous farming: none of the {matchingLists.Count} toggled list(s) is ready. Soonest ready in {waitSeconds}s.");
            throw BuildContinuousFarmDefer("No toggled farm list is ready.", waitSeconds, 0);
        }

        await RunFarmListLossDeactivationIfEnabledAsync(context);
        context.Log($"Continuous farming (toggled lists): sending {readyLists.Count}/{matchingLists.Count} ready list(s) this round; delay between rounds={dispatchDelaySeconds}s.");
        var sent = await context.Client.SendSelectedFarmListsNowAsync(selectedNames, selectedIds, context.CancellationToken);
        context.Log($"Continuous farming (toggled lists): {sent} list(s) dispatched this round.");
        var refreshedOverview = await context.Client.ReadFarmListsOverviewAsync(context.CancellationToken);
        await WriteFarmListsSnapshotAsync(context, refreshedOverview);
        LogContinuousFarmNextSchedule(context, dispatchDelaySeconds, 0);
        throw BuildContinuousFarmDefer("Continuous farming cooldown active.", dispatchDelaySeconds, 0, TaskWaitReasons.WorkQueued);
    }

    private static async Task ExecuteSendAllFarmlistsAsync(TaskExecutionContext context, int dispatchDelaySeconds)
    {
        await RunFarmListLossDeactivationIfEnabledAsync(context);
        context.Log("Continuous farming send-all started.");
        var listCount = await context.Client.SendAllFarmListsNowAsync(context.CancellationToken);
        context.Log($"Continuous farming send-all completed. Lists considered={listCount}.");
        var refreshedOverview = await context.Client.ReadFarmListsOverviewAsync(context.CancellationToken);
        await WriteFarmListsSnapshotAsync(context, refreshedOverview);
        LogContinuousFarmNextSchedule(context, dispatchDelaySeconds, 0);
        throw BuildContinuousFarmDefer("Continuous farming cooldown active.", dispatchDelaySeconds, 0, TaskWaitReasons.WorkQueued);
    }

    private static async Task RunFarmListLossDeactivationIfEnabledAsync(TaskExecutionContext context)
    {
        if (!context.Options.ContinuousFarmDeactivateLosses)
        {
            context.Log("Continuous farming loss deactivation disabled.");
            return;
        }

        var result = await context.Client.HandleFarmListLossTargetsAsync(
            CreateFarmListLossHandlingRequest(context.Options),
            context.CancellationToken);
        context.Log($"Continuous farming loss handling result: found={result.RowsFound}, deactivated={result.RowsDeactivated}, moved={result.RowsMoved}, moveFailures={result.MoveFailures}, skippedOasis={result.SkippedOasisRows}.");
        context.Runner.PublishFarmLossDestinationChange(context.Options, result);
    }

    private static void LogContinuousFarmNextSchedule(TaskExecutionContext context, int waitSeconds, int nextIndex)
    {
        var nextTime = DateTimeOffset.Now.AddSeconds(Math.Max(1, waitSeconds));
        context.Log($"Continuous farming next scheduled send time={nextTime:yyyy-MM-dd HH:mm:ss zzz}; nextListIndex={nextIndex}; wait={waitSeconds}s.");
    }

    private static TaskWaitException BuildContinuousFarmDefer(string message, int waitSeconds, int nextIndex, string? reasonCode = null) =>
        new(Math.Max(1, waitSeconds), $"{message} queue_wait_seconds={Math.Max(1, waitSeconds)} {BotOptionPayloadKeys.ContinuousFarmNextListIndex}={Math.Max(0, nextIndex)}", reasonCode);
}
