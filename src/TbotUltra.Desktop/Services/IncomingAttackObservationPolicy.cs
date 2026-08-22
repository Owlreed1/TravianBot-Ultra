using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

public static class IncomingAttackObservationPolicy
{
    public static readonly TimeSpan UnchangedSignalRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ArrivalMatchTolerance = TimeSpan.FromSeconds(10);

    public static bool ShouldReadDetails(
        IncomingAttackSignal signal,
        IncomingAttackSignal? previousSignal,
        IReadOnlyCollection<IncomingAttack> knownAttacks,
        DateTimeOffset? lastReadUtc,
        DateTimeOffset nowUtc)
    {
        if (!lastReadUtc.HasValue)
        {
            return true;
        }

        var observedArrivals = signal.Dorf1ArrivalTimesUtc;
        if (observedArrivals is { Count: > 0 })
        {
            var comparisonArrivals = previousSignal?.Dorf1ArrivalTimesUtc is { Count: > 0 } previousArrivals
                ? previousArrivals
                : knownAttacks.Select(attack => attack.ArrivalAtUtc).ToList();
            if (!ArrivalSetsMatch(observedArrivals, comparisonArrivals))
            {
                return true;
            }
        }

        return nowUtc - lastReadUtc.Value >= UnchangedSignalRefreshInterval;
    }

    private static bool ArrivalSetsMatch(
        IReadOnlyList<DateTimeOffset> observed,
        IReadOnlyList<DateTimeOffset> comparison)
    {
        if (observed.Count != comparison.Count)
        {
            return false;
        }

        var observedSorted = observed.OrderBy(value => value).ToList();
        var comparisonSorted = comparison.OrderBy(value => value).ToList();
        return observedSorted.Zip(comparisonSorted)
            .All(pair => (pair.First - pair.Second).Duration() <= ArrivalMatchTolerance);
    }
}
