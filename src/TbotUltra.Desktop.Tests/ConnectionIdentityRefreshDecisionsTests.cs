using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ConnectionIdentityRefreshDecisionsTests
{
    [Fact]
    public void ShouldStartLookup_DirectLookupAlreadyInFlight_ReturnsFalse()
    {
        var shouldStart = ConnectionIdentityRefreshDecisions.ShouldStartLookup(
            "direct",
            "direct",
            string.Empty,
            "direct");

        Assert.False(shouldStart);
    }

    [Fact]
    public void ShouldStartLookup_DirectIpIsCached_ReturnsFalse()
    {
        var shouldStart = ConnectionIdentityRefreshDecisions.ShouldStartLookup(
            "direct",
            "direct",
            "203.0.113.42",
            string.Empty);

        Assert.False(shouldStart);
    }

    [Fact]
    public void ShouldStartLookup_ConnectionChanged_ReturnsTrue()
    {
        var shouldStart = ConnectionIdentityRefreshDecisions.ShouldStartLookup(
            "socks5://proxy.example:1080",
            "direct",
            "203.0.113.42",
            string.Empty);

        Assert.True(shouldStart);
    }
}
