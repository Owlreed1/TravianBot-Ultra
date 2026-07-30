using TbotUltra.Core.Configuration;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class DemolishDefaultsTests
{
    [Fact]
    public void CalculateDelay_UsesInclusiveNormalizedMinuteRange()
    {
        var delay = DemolishDefaults.CalculateDelay(10, 1, (min, max) =>
        {
            Assert.Equal(60, min);
            Assert.Equal(601, max);
            return 420;
        });

        Assert.Equal(TimeSpan.FromSeconds(420), delay);
    }
}
