using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>
/// Owns the continuous-farming task decisions while the farming client retains
/// the browser flow, selectors, pacing, and parsing.
/// </summary>
internal sealed class ContinuousFarmingOperation(IFarmingClient client)
{
    public async Task<ContinuousFarmingDispatchResult> ExecuteAsync(
        ContinuousFarmingDispatchRequest request,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.SendMode, FarmingDefaults.SendModeAllAtOnce, StringComparison.Ordinal))
        {
            var lossResults = await HandleLossesIfEnabledAsync(request, log, cancellationToken);
            log("Continuous farming send-all started.");
            var listCount = await client.SendAllFarmListsViaStartAllButtonAsync(cancellationToken);
            log($"Continuous farming send-all completed. Lists considered={listCount}.");
            var snapshot = await client.ReadFarmListsOverviewAsync(cancellationToken);
            return ContinuousFarmingDispatchResult.ForCompletedRound(
                snapshot,
                request.DispatchDelaySeconds,
                lossResults);
        }

        var selectedNames = (request.SelectedNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selectedIds = (request.SelectedIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedNames.Count <= 0 && selectedIds.Count <= 0)
        {
            throw new InvalidOperationException("No farm lists selected for continuous farming.");
        }

        var overview = await client.ReadFarmListsOverviewAsync(cancellationToken);
        var matchingLists = overview
            .Where(item => item is not null
                && ((item.ListId is not null && selectedIds.Contains(item.ListId))
                    || selectedNames.Contains(item.Name, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        if (matchingLists.Count <= 0)
        {
            log(overview.Count > 0
                ? $"Continuous farming: none of the selected farm lists ({string.Join(", ", selectedNames)}) were found on the farm page. They may have been renamed — re-analyze and re-select. Retrying in {request.DispatchDelaySeconds}s."
                : $"Continuous farming: no farm lists were found on the farm page. Retrying in {request.DispatchDelaySeconds}s.");
            return ContinuousFarmingDispatchResult.ForDefer(
                "Selected farm lists were not found on the farm page.",
                request.DispatchDelaySeconds);
        }

        var readyLists = matchingLists.Where(item => item.RemainingSeconds is null or <= 0).ToList();
        if (readyLists.Count <= 0)
        {
            var soonestRemaining = matchingLists.Min(item => item.RemainingSeconds is > 0 ? item.RemainingSeconds.Value : 1);
            var waitSeconds = Math.Max(1, soonestRemaining + Random.Shared.Next(5, 16));
            log($"Continuous farming: none of the {matchingLists.Count} toggled list(s) is ready. Soonest ready in {waitSeconds}s.");
            return ContinuousFarmingDispatchResult.ForDefer("No toggled farm list is ready.", waitSeconds);
        }

        var lossHandlingResults = await HandleLossesIfEnabledAsync(request, log, cancellationToken);
        log($"Continuous farming (toggled lists): sending {readyLists.Count}/{matchingLists.Count} ready list(s) this round; delay between rounds={request.DispatchDelaySeconds}s.");
        var sent = await client.SendSelectedFarmListsNowAsync(selectedNames, selectedIds, cancellationToken);
        log($"Continuous farming (toggled lists): {sent} list(s) dispatched this round.");
        var refreshedOverview = await client.ReadFarmListsOverviewAsync(cancellationToken);
        return ContinuousFarmingDispatchResult.ForCompletedRound(
            refreshedOverview,
            request.DispatchDelaySeconds,
            lossHandlingResults);
    }

    private async Task<IReadOnlyList<FarmListLossDeactivationResult>?> HandleLossesIfEnabledAsync(
        ContinuousFarmingDispatchRequest request,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        if (!request.DeactivateLosses)
        {
            log("Continuous farming loss deactivation disabled.");
            return null;
        }

        var requests = request.LossHandlingRequests
            ?? (request.LossHandlingRequest is null ? null : [request.LossHandlingRequest]);
        if (requests is null || requests.Count == 0)
            throw new InvalidOperationException("Farm loss handling requests are required when loss deactivation is enabled.");

        var results = new List<FarmListLossDeactivationResult>(requests.Count);
        foreach (var lossRequest in requests)
        {
            var result = await client.HandleFarmListLossTargetsAsync(lossRequest, cancellationToken);
            results.Add(result);
            log($"Continuous farming {lossRequest.LossColors.ToString().ToLowerInvariant()} loss handling result: found={result.RowsFound}, deactivated={result.RowsDeactivated}, moved={result.RowsMoved}, moveFailures={result.MoveFailures}, skippedOasis={result.SkippedOasisRows}.");
        }
        return results;
    }
}

internal sealed record ContinuousFarmingDispatchRequest(
    string SendMode,
    IReadOnlyCollection<string>? SelectedNames,
    IReadOnlyCollection<string>? SelectedIds,
    int DispatchDelaySeconds,
    bool DeactivateLosses,
    FarmListLossHandlingRequest? LossHandlingRequest,
    IReadOnlyList<FarmListLossHandlingRequest>? LossHandlingRequests = null);

internal sealed record ContinuousFarmingDispatchResult(
    string WaitMessage,
    int WaitSeconds,
    string? WaitReasonCode,
    IReadOnlyList<FarmListOverview>? Snapshot,
    FarmListLossDeactivationResult? LossHandlingResult,
    bool ScheduleNextRound,
    IReadOnlyList<FarmListLossDeactivationResult>? LossHandlingResults = null)
{
    public static ContinuousFarmingDispatchResult ForDefer(string message, int waitSeconds) =>
        new(message, Math.Max(1, waitSeconds), null, null, null, false);

    public static ContinuousFarmingDispatchResult ForCompletedRound(
        IReadOnlyList<FarmListOverview> snapshot,
        int waitSeconds,
        IReadOnlyList<FarmListLossDeactivationResult>? lossHandlingResults) =>
        new("Continuous farming cooldown active.", Math.Max(1, waitSeconds), TaskWaitReasons.WorkQueued,
            snapshot, CombineLossResults(lossHandlingResults), true, lossHandlingResults);

    private static FarmListLossDeactivationResult? CombineLossResults(IReadOnlyList<FarmListLossDeactivationResult>? results)
    {
        if (results is null || results.Count == 0)
            return null;
        if (results.Count == 1)
            return results[0];
        return new FarmListLossDeactivationResult(
            results.Sum(result => result.RowsFound),
            results.Sum(result => result.RowsDeactivated),
            results.Sum(result => result.SkippedOasisRows),
            results.Sum(result => result.RowsMoved),
            results.Sum(result => result.MoveFailures));
    }
}
