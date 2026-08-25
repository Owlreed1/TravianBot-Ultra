using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class HeroDashboardStatusSourceTests
{
    [Fact]
    public void WorkerHeroStatusUpdate_UpdatesDashboardHeroStateAsWellAsHeroPageText()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.Hero.cs"));
        var handlerStart = source.IndexOf("private void OnWorkerHeroStatusUpdated", StringComparison.Ordinal);
        var handlerEnd = source.IndexOf("internal async Task RefreshHeroInventoryCoreAsync", handlerStart, StringComparison.Ordinal);

        Assert.True(handlerStart >= 0 && handlerEnd > handlerStart);
        var handler = source[handlerStart..handlerEnd];
        Assert.Contains("HeroStatusText = status.DisplayText", handler, StringComparison.Ordinal);
        Assert.Contains("SetHeroState(", handler, StringComparison.Ordinal);
    }
}
