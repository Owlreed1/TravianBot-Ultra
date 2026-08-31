using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class HeroOintmentRetryPolicyTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "TbotUltra.HeroOintmentRetryPolicyTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ShouldLookup_BlocksEveryChangedHeroStateUntilThePersistedCooldownExpires()
    {
        var now = new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero);
        var state = new HeroOintmentAvailabilityState(now.AddHours(12));

        Assert.False(HeroOintmentRetryPolicy.ShouldLookup(state, now));
        Assert.False(HeroOintmentRetryPolicy.ShouldLookup(state, now.AddHours(11)));
        Assert.True(HeroOintmentRetryPolicy.ShouldLookup(state, now.AddHours(12)));
    }

    [Fact]
    public void EmptyInventoryCooldown_SurvivesAStoreRestartAndRemainsAccountAndServerScoped()
    {
        var retryAt = new DateTimeOffset(2026, 8, 31, 20, 0, 0, TimeSpan.Zero);
        new HeroOintmentAvailabilityStore(_rootPath).SaveUnavailable(
            "account-one",
            "https://ts100.example.com",
            retryAt);

        var restartedStore = new HeroOintmentAvailabilityStore(_rootPath);

        Assert.Equal(retryAt, restartedStore.TryLoad("account-one", "https://ts100.example.com")?.RetryNotBeforeUtc);
        Assert.Null(restartedStore.TryLoad("account-two", "https://ts100.example.com"));
        Assert.Null(restartedStore.TryLoad("account-one", "https://ts200.example.com"));
    }

    [Fact]
    public void Clear_RearmsAutomaticLookupAfterInventoryFindsOintments()
    {
        var store = new HeroOintmentAvailabilityStore(_rootPath);
        store.SaveUnavailable("account-one", "https://ts100.example.com", DateTimeOffset.UtcNow.AddHours(12));

        store.Clear("account-one", "https://ts100.example.com");

        Assert.Null(store.TryLoad("account-one", "https://ts100.example.com"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
