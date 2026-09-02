using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class FarmLossDestinationLoginGuardSourceTests
{
    [Fact]
    public void DestinationSetup_RequiresAnExistingLoginBeforeStartingBrowserWork()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Farming.FarmLists.cs"));
        var methodStart = source.IndexOf(
            "private async Task EnsureFarmLossDestinationSelectedAsync",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    private async Task PauseAutomationForFarmLossDestinationAsync",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];
        var loginGuard = methodBody.IndexOf("if (!_isLoggedIn)", StringComparison.Ordinal);
        var browserWork = methodBody.IndexOf("await EnsureChromiumInstalledAsync();", StringComparison.Ordinal);

        Assert.True(loginGuard >= 0 && loginGuard < browserWork);
        Assert.Contains("SetMoveLosses(isRed, false);", methodBody[loginGuard..], StringComparison.Ordinal);
        Assert.Contains("\"You must log in first.\"", methodBody[loginGuard..], StringComparison.Ordinal);
        Assert.Contains("MessageBoxButton.OK", methodBody[loginGuard..], StringComparison.Ordinal);
    }
}
