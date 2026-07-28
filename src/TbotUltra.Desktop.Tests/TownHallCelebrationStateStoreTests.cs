using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TownHallCelebrationStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "tbot-town-hall-state-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoadActive_PreservesTwoCelebrationTimersAndNextAttempt()
    {
        var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var nextAttempt = now.AddMinutes(15);
        var celebrations = new[]
        {
            new TownHallCelebrationTimer("small", now.AddHours(1)),
            new TownHallCelebrationTimer("big", now.AddHours(2)),
        };

        TownHallCelebrationStateStore.Save(
            _root,
            "account",
            "xy:1|2",
            "small",
            nextAttempt,
            celebrations);

        var state = TownHallCelebrationStateStore.LoadActive(
            _root,
            "account",
            "xy:1|2",
            now);

        Assert.NotNull(state);
        Assert.Equal(nextAttempt, state.EndsAtUtc);
        Assert.Equal(celebrations, state.Celebrations);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
