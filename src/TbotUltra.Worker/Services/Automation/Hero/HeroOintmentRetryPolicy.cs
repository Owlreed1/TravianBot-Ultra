namespace TbotUltra.Worker.Services;

internal sealed record HeroOintmentAvailabilityState(DateTimeOffset RetryNotBeforeUtc);

internal static class HeroOintmentRetryPolicy
{
    internal static readonly TimeSpan EmptyInventoryCooldown = TimeSpan.FromHours(12);

    internal static bool ShouldLookup(HeroOintmentAvailabilityState? state, DateTimeOffset now)
        => state is null || state.RetryNotBeforeUtc <= now;
}
