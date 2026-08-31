using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

public static class IncomingAttackObservationPolicy
{
    public static readonly TimeSpan UnconfirmedSignalRetryInterval = TimeSpan.FromMinutes(10);

    public static bool ShouldReadDetails(
        IncomingAttackSignal signal,
        int? confirmedMovementCount,
        DateTimeOffset? lastReadUtc,
        DateTimeOffset nowUtc,
        bool isNewPlusMarker = false)
    {
        if (confirmedMovementCount.HasValue)
        {
            return signal.Dorf1ArrivalTimesUtc is { } arrivals
                   && arrivals.Count > confirmedMovementCount.Value;
        }

        if (isNewPlusMarker)
        {
            return true;
        }

        if (!lastReadUtc.HasValue)
        {
            return true;
        }

        return nowUtc - lastReadUtc.Value >= UnconfirmedSignalRetryInterval;
    }

    public static bool ShouldKeepPendingSignal(
        IncomingAttackSignal signal,
        bool hasActiveConfirmedMovements,
        bool hasConfirmedMovementHistory,
        DateTimeOffset nowUtc)
    {
        if (signal.Dorf1ArrivalTimesUtc is { Count: > 0 } arrivals)
        {
            return arrivals.Any(arrival => arrival > nowUtc);
        }

        return hasActiveConfirmedMovements || !hasConfirmedMovementHistory;
    }
}
