using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

public static class IncomingAttackObservationPolicy
{
    public static readonly TimeSpan UnconfirmedSignalRetryInterval = TimeSpan.FromMinutes(10);

    public static bool ShouldReadDetails(
        IncomingAttackSignal signal,
        int? confirmedMovementCount,
        DateTimeOffset? lastReadUtc,
        DateTimeOffset nowUtc)
    {
        if (confirmedMovementCount.HasValue)
        {
            return signal.Dorf1ArrivalTimesUtc is { } arrivals
                   && arrivals.Count > confirmedMovementCount.Value;
        }

        if (!lastReadUtc.HasValue)
        {
            return true;
        }

        return nowUtc - lastReadUtc.Value >= UnconfirmedSignalRetryInterval;
    }
}
