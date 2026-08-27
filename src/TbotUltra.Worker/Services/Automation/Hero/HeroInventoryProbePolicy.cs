using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

internal static class HeroInventoryProbePolicy
{
    internal static bool IsEmpty(HeroInventoryResources resources)
        => resources.Wood == 0
            && resources.Clay == 0
            && resources.Iron == 0
            && resources.Crop == 0;

    internal static bool ShouldProbe(HeroInventorySnapshot snapshot, DateTimeOffset now)
        => IsEmpty(snapshot.Resources)
            && (snapshot.NextProbeAtUtc is null || snapshot.NextProbeAtUtc <= now);

    internal static TimeSpan GetEmptyProbeDelay(int consecutiveEmptyObservations, double sample)
    {
        var (minimumMinutes, maximumMinutes) = consecutiveEmptyObservations switch
        {
            <= 1 => (15d, 30d),
            2 => (30d, 45d),
            _ => (45d, 60d),
        };
        var normalizedSample = Math.Clamp(sample, 0d, 1d);
        return TimeSpan.FromMinutes(
            minimumMinutes + ((maximumMinutes - minimumMinutes) * normalizedSample));
    }
}
