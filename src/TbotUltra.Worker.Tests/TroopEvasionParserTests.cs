using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class TroopEvasionParserTests
{
    [Theory]
    [InlineData("In 2:27:15 hours", 8835)]
    [InlineData("In 1 day 02:03:04", 93784)]
    public void TryParseTravelDuration_ReadsOfficialConfirmationText(string text, int seconds)
    {
        Assert.True(TravianClient.TryParseTravelDuration(text, out var duration));
        Assert.Equal(seconds, duration.TotalSeconds);
    }

    [Fact]
    public void TryParseTravelDuration_RejectsUnreadableText()
        => Assert.False(TravianClient.TryParseTravelDuration("unknown", out _));
}
