namespace TbotUltra.Worker.Services;

internal sealed record ConstructionHumanizeDecision(
    int QueueRetrySeconds,
    double HumanizeDelaySeconds,
    string Reason)
{
    public static ConstructionHumanizeDecision None { get; } = new(0, 0, "no delay");
}

/// <summary>
/// Pure queue-timer decision for construction humanization. Browser/session state stays in
/// <see cref="TravianClient"/>; random selection is injected so production behavior and tests use
/// the same branch logic without coupling this calculator to global RNG state.
/// </summary>
internal static class ConstructionHumanizeCalculator
{
    public static int ResolveExistingWaitSeconds(DateTimeOffset now, DateTimeOffset scheduledUntil)
    {
        var remainingSeconds = (scheduledUntil - now).TotalSeconds;
        return remainingSeconds <= 1 ? 0 : (int)Math.Ceiling(remainingSeconds);
    }

    public static ConstructionHumanizeDecision CalculateAfterFullQueue(
        IReadOnlyList<int> relevantRemainingSeconds,
        int slotFreeWaitSeconds,
        double queuePercentMin,
        double queuePercentMax,
        double maxDelayMinutes,
        double noPlusMinMinutes,
        double noPlusMaxMinutes,
        Func<double, double, double> randomInRange)
    {
        ArgumentNullException.ThrowIfNull(randomInRange);
        if (slotFreeWaitSeconds <= 0)
        {
            return ConstructionHumanizeDecision.None;
        }

        // A full Plus queue has at least two active constructions. The humanized delay belongs
        // while the first construction is still running, not after it has freed a slot. Retrying
        // at that first completion keeps the new slot occupied without changing the configured
        // 5-20% humanization interval.
        if (relevantRemainingSeconds.Count(seconds => seconds > 0) > 1)
        {
            var referenceSeconds = slotFreeWaitSeconds;
            var percent = randomInRange(queuePercentMin, queuePercentMax) / 100.0;
            var delaySeconds = Math.Min(
                referenceSeconds * percent,
                Math.Max(0, maxDelayMinutes) * 60.0);
            return new ConstructionHumanizeDecision(
                slotFreeWaitSeconds,
                delaySeconds,
                $"before slot opens, percent {percent * 100:F0}% of {referenceSeconds}s remaining");
        }

        var minutes = randomInRange(noPlusMinMinutes, noPlusMaxMinutes);
        return new ConstructionHumanizeDecision(
            slotFreeWaitSeconds + (int)Math.Ceiling(minutes * 60.0),
            minutes * 60.0,
            $"after slot opens, no-plus {minutes:F1}m");
    }
}
