using TbotUltra.Core.Configuration;

namespace TbotUltra.Desktop.Services.Orchestration;

internal static class AutomationDeadlinePolicy
{
    internal static TimeSpan? ResolveNextDelay(
        DateTimeOffset now,
        DateTimeOffset? nextQueueDeadline,
        DateTimeOffset? nextConstructionAvailabilityUtc,
        DateTimeOffset? nextVillageStatusRoundUtc)
    {
        var nextDeadline = nextQueueDeadline;
        if (nextConstructionAvailabilityUtc is DateTimeOffset constructionDeadline
            && (nextDeadline is null || constructionDeadline < nextDeadline.Value))
        {
            nextDeadline = constructionDeadline;
        }
        if (nextVillageStatusRoundUtc is DateTimeOffset villageRoundDeadline
            && (villageRoundDeadline == DateTimeOffset.MinValue || villageRoundDeadline <= now))
        {
            return TimeSpan.FromSeconds(1);
        }

        if (nextVillageStatusRoundUtc is DateTimeOffset scheduledVillageRound
            && (nextDeadline is null || scheduledVillageRound < nextDeadline.Value))
        {
            nextDeadline = scheduledVillageRound;
        }

        if (nextDeadline is null)
        {
            return null;
        }

        var delay = nextDeadline.Value - now;
        return delay < TimeSpan.FromSeconds(1)
            ? TimeSpan.FromSeconds(1)
            : delay;
    }

    internal static int ResolveWaitSeconds(
        TimeSpan? authoritativeDelay,
        BotOptions options,
        bool networkBackoff,
        Func<int, int, int>? nextRandom = null)
    {
        var totalSeconds = authoritativeDelay is null
            ? Math.Max(1, options.LoopIntervalSeconds)
            : Math.Max(1, (int)Math.Ceiling(authoritativeDelay.Value.TotalSeconds));
        if (networkBackoff || authoritativeDelay is not null)
        {
            return totalSeconds;
        }

        var minMs = (int)Math.Round(Math.Max(0, options.ActionPacingLoopMinSeconds) * 1000);
        var maxMs = (int)Math.Round(
            Math.Max(options.ActionPacingLoopMinSeconds, options.ActionPacingLoopMaxSeconds) * 1000);
        var pacingMs = (nextRandom ?? Random.Shared.Next)(minMs, maxMs + 1);
        return Math.Max(1, (int)Math.Ceiling(pacingMs / 1000.0));
    }
}
