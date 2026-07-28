using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TownHallCelebrationSignalParserTests
{
    [Fact]
    public void Parse_ReadsEveryActiveCelebrationTimer()
    {
        var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

        var timers = TownHallCelebrationSignalParser.Parse(
            "Town Hall celebration started. town_hall_active=small:3600,big:7200 queue_wait_seconds=900",
            now);

        Assert.Equal(2, timers.Count);
        Assert.Equal(new TownHallCelebrationTimer("small", now.AddHours(1)), timers[0]);
        Assert.Equal(new TownHallCelebrationTimer("big", now.AddHours(2)), timers[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("town_hall_active=small:not-a-number")]
    [InlineData("town_hall_active=small:-1")]
    public void Parse_InvalidSignalReturnsNoTimers(string message)
    {
        Assert.Empty(TownHallCelebrationSignalParser.Parse(message, DateTimeOffset.UtcNow));
    }
}
