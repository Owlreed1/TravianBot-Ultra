using TbotUltra.Core.Configuration;
using TbotUltra.Core.Accounts;
using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Infrastructure;
using TbotUltra.Worker.Services.Automation;
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
        var lossRequests = CreateFarmListLossHandlingRequests(context.Options);
        context.Log($"Continuous farming mode={mode}; delayRange={minDelaySeconds}-{maxDelaySeconds}s; selectedDelay={dispatchDelaySeconds}s; targetVillage='{(string.IsNullOrWhiteSpace(context.Options.TargetVillageName) ? "(default)" : context.Options.TargetVillageName)}'; redLosses={context.Options.ContinuousFarmDeactivateRedLosses}; yellowLosses={context.Options.ContinuousFarmDeactivateYellowLosses}; redOasis={context.Options.ContinuousFarmDeactivateRedOasisLosses}; yellowOasis={context.Options.ContinuousFarmDeactivateYellowOasisLosses}.");

        var operation = new ContinuousFarmingOperation(context.Client);
        var result = await operation.ExecuteAsync(
            new ContinuousFarmingDispatchRequest(
                mode,
                context.Options.ContinuousFarmListNames,
                context.Options.ContinuousFarmListIds,
                dispatchDelaySeconds,
                lossRequests.Count > 0,
                null,
                lossRequests),
            context.Log,
            context.CancellationToken);

        foreach (var lossResult in result.LossHandlingResults ?? [])
        {
            context.Runner.PublishFarmLossDestinationChange(context.Options, lossResult);
        }

        if (result.Snapshot is not null)
        {
            await WriteFarmListsSnapshotAsync(context, result.Snapshot);
        }

        if (result.ScheduleNextRound)
        {
            LogContinuousFarmNextSchedule(context, result.WaitSeconds, 0);
        }

        throw BuildContinuousFarmDefer(result.WaitMessage, result.WaitSeconds, 0, result.WaitReasonCode);
    }

    private static void LogContinuousFarmNextSchedule(TaskExecutionContext context, int waitSeconds, int nextIndex)
    {
        var nextTime = DateTimeOffset.Now.AddSeconds(Math.Max(1, waitSeconds));
        context.Log($"Continuous farming next scheduled send time={nextTime:yyyy-MM-dd HH:mm:ss zzz}; nextListIndex={nextIndex}; wait={waitSeconds}s.");
    }

    private static TaskWaitException BuildContinuousFarmDefer(string message, int waitSeconds, int nextIndex, string? reasonCode = null) =>
        new(Math.Max(1, waitSeconds), $"{message} queue_wait_seconds={Math.Max(1, waitSeconds)} {BotOptionPayloadKeys.ContinuousFarmNextListIndex}={Math.Max(0, nextIndex)}", reasonCode);
}
