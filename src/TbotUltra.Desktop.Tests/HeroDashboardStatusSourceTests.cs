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

    [Fact]
    public void AttributeSnapshot_AppliesReturnTimerToHeroQueueBeforeAutomationStarts()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.Hero.cs"));
        var methodStart = source.IndexOf("private void ApplyHeroReturnTimer", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("internal async Task RefreshHeroStatsCoreAsync", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var method = source[methodStart..methodEnd];
        Assert.Contains("HeroLoopTask.RemainingSeconds = remainingSeconds", method, StringComparison.Ordinal);
        Assert.Contains("UpdateDeferredQueueItem", method, StringComparison.Ordinal);
        Assert.Contains("HeroDeferReasonAway", method, StringComparison.Ordinal);
    }
}
