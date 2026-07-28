using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class TownHallCelebrationTimerSignalTests
{
    [Fact]
    public void FormatTownHallActiveTimerSignal_IncludesEveryPositiveTimer()
    {
        var signal = TravianClient.FormatTownHallActiveTimerSignal(
            "small",
            [("small", 3600), ("small", 0), ("big", 7200)]);

        Assert.Equal(" town_hall_active=small:3600,big:7200", signal);
    }
}
