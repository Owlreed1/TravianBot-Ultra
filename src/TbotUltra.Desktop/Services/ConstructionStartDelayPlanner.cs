using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;

namespace TbotUltra.Desktop.Services;

public sealed record ConstructionStartDelayDecision(
    int DelaySeconds,
    DateTimeOffset ReferenceFinishUtc,
    DateTimeOffset ReadyAtUtc,
    string Reason);

/// <summary>
/// Plans the normal construction start pause while the bot is still outside the target village.
/// The caller persists the result; previews deliberately do not invoke this planner.
/// </summary>
public static class ConstructionStartDelayPlanner
{
    public static ConstructionStartDelayDecision? Resolve(
        QueueItem item,
        VillageStatus? status,
        bool? travianPlusActive,
        BotOptions options,
        DateTimeOffset now,
        Func<double, double, double> randomInRange)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(randomInRange);

        if (!options.ConstructionHumanizeDelayEnabled
            || item.Status != QueueStatus.Pending
            || item.NextAttemptAt > now
            || item.Payload.ContainsKey(BotOptionPayloadKeys.ConstructionLoginFill)
            || item.Payload.ContainsKey(BotOptionPayloadKeys.ConstructionPreSleepFill)
            || item.Payload.ContainsKey(BotOptionPayloadKeys.ConstructionHumanizePreNavigationDelaySatisfied)
            || ConstructionQueueState.IsConstructionHumanizeDeferred(item)
            || ConstructionQueueState.ResolveQueueHumanizeExtraSeconds(item) > 0
            || ConstructionQueueState.ResolveAvailabilityForItem(status, travianPlusActive, item, now)
                != ConstructionQueueAvailability.Available)
        {
            return null;
        }

        var active = ConstructionQueueState.ResolveCurrentActiveConstructions(status, now);
        if (string.Equals(status?.Tribe, "Romans", StringComparison.OrdinalIgnoreCase))
        {
            var resourceTask = string.Equals(item.TaskName, "upgrade_resource_to_level", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.TaskName, "upgrade_all_resources_to_level", StringComparison.OrdinalIgnoreCase);
            active = active
                .Where(construction => resourceTask
                    ? construction.Kind == ConstructionKind.Resource
                    : construction.Kind != ConstructionKind.Resource)
                .ToList();
        }

        var referenceSeconds = active
            .Select(construction => construction.Finish?.RemainingSecondsAt(now)
                ?? construction.TimeLeftSeconds
                ?? 0)
            .Where(seconds => seconds > 1)
            .DefaultIfEmpty(0)
            .Min();
        if (referenceSeconds <= 1)
        {
            return null;
        }

        var delay = ConstructionHumanizeCalculator.CalculateBoundedQueueDelaySeconds(
            referenceSeconds,
            options.ConstructionHumanizeQueuePercentMin,
            options.ConstructionHumanizeQueuePercentMax,
            options.ConstructionHumanizeMaxDelayMinutes,
            randomInRange);
        if (delay < 1)
        {
            return null;
        }

        var delaySeconds = (int)Math.Ceiling(delay);
        var selectedPercent = delay / referenceSeconds * 100;
        return new ConstructionStartDelayDecision(
            delaySeconds,
            now.AddSeconds(referenceSeconds),
            now.AddSeconds(delaySeconds),
            $"percent {selectedPercent:F0}% of {referenceSeconds}s remaining");
    }
}
