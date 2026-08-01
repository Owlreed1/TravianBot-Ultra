using TbotUltra.Desktop.Models;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class LoopTaskOptionCardTests
{
    [Fact]
    public void CardText_PrefixesCountdownWithTheConstructionWaitCause()
    {
        var option = new LoopTaskOption
        {
            IsEnabled = true,
            CardPrefix = "Res",
            CardTone = "Warning",
            RemainingSeconds = 65,
        };

        Assert.Equal("Res: 01:05", option.CardText);
        Assert.Equal("Warning", option.CardTone);
    }

    [Fact]
    public void CardText_ShowsInformationalConstructionStateWithoutCountdown()
    {
        var option = new LoopTaskOption
        {
            IsEnabled = true,
            CardPrefix = "Empty queue",
            CardTone = "Info",
        };

        Assert.Equal("Empty queue", option.CardText);
        Assert.Equal("Info", option.CardTone);
    }
}
