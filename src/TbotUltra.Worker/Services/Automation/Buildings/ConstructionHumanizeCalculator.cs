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
    internal static double CalculateBoundedQueueDelaySeconds(
        int referenceSeconds,
        double queuePercentMin,
        double queuePercentMax,
        double maxDelayMinutes,
        Func<double, double, double> randomInRange)
    {
        ArgumentNullException.ThrowIfNull(randomInRange);
        if (referenceSeconds <= 1)
        {
            return 0;
        }

        var minimumPercent = Math.Clamp(queuePercentMin, 0, 99);
        var maximumPercent = Math.Clamp(Math.Max(minimumPercent, queuePercentMax), 0, 99);
        var selectedPercent = Math.Clamp(
            randomInRange(minimumPercent, maximumPercent),
            minimumPercent,
            maximumPercent);
        var percent = selectedPercent / 100.0;
        var capSeconds = Math.Max(0, maxDelayMinutes) * 60.0;
        return Math.Min(referenceSeconds - 1, Math.Min(referenceSeconds * percent, capSeconds));
    }

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

        // A full Plus queue has at least two active constructions. Once the first one finishes,
        // another construction is still running and becomes the reference for the configured
        // percentage delay. Persist finish + delay so neither Desktop nor Worker navigates at the
        // exact server timer boundary.
        if (relevantRemainingSeconds.Count(seconds => seconds > 0) > 1)
        {
            var referenceSeconds = relevantRemainingSeconds
                .Where(seconds => seconds > slotFreeWaitSeconds)
                .Select(seconds => seconds - slotFreeWaitSeconds)
                .DefaultIfEmpty(0)
                .Min();
            if (referenceSeconds <= 0)
            {
                var fallbackMinutes = randomInRange(noPlusMinMinutes, noPlusMaxMinutes);
                return new ConstructionHumanizeDecision(
                    slotFreeWaitSeconds + (int)Math.Ceiling(fallbackMinutes * 60.0),
                    fallbackMinutes * 60.0,
                    $"after slot opens, no continuing Plus build; {fallbackMinutes:F1}m");
            }

            var delaySeconds = CalculateBoundedQueueDelaySeconds(
                referenceSeconds,
                queuePercentMin,
                queuePercentMax,
                maxDelayMinutes,
                randomInRange);
            var percent = referenceSeconds > 0 ? delaySeconds / referenceSeconds : 0;
            return new ConstructionHumanizeDecision(
                slotFreeWaitSeconds + (int)Math.Ceiling(delaySeconds),
                delaySeconds,
                $"after slot opens, percent {percent * 100:F0}% of {referenceSeconds}s remaining");
        }

        var minutes = randomInRange(noPlusMinMinutes, noPlusMaxMinutes);
        return new ConstructionHumanizeDecision(
            slotFreeWaitSeconds + (int)Math.Ceiling(minutes * 60.0),
            minutes * 60.0,
            $"after slot opens, no-plus {minutes:F1}m");
    }
}
