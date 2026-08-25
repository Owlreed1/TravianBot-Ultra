using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ServerSpeedAlarmSourceTests
{
    [Theory]
    [InlineData("COMMUNITY WEEK X5", "https://lobby.legends.travian.com", 5)]
    [InlineData("Community Week 5x", "https://lobby.legends.travian.com", 5)]
    [InlineData("Community Week", "https://ts1.x10.europe.travian.com", 10)]
    public void ResolveConfiguredServerSpeed_AcceptsOfficialNameAndUrlFormats(
        string serverName,
        string serverUrl,
        double expected)
    {
        Assert.Equal(expected, MainWindow.ResolveConfiguredServerSpeed(serverName, serverUrl));
    }

    [Fact]
    public void ResolveServerSpeed_DoesNotAlarmBeforeVerifiedLogin()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.ServerAccount.cs"));
        var methodStart = source.IndexOf("private double ResolveServerSpeed()", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private string? GetActiveAccountServerUrl()", methodStart, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        var loggedOutGuard = method.IndexOf("if (!_isLoggedIn)", StringComparison.Ordinal);
        var alarm = method.IndexOf("ALARM: could not detect server speed", StringComparison.Ordinal);

        Assert.True(loggedOutGuard >= 0 && loggedOutGuard < alarm);
    }
}
