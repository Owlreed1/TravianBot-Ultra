using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class NextTaskVillageNameSourceTests
{
    [Fact]
    public void NextTaskDescription_UsesCurrentVillageNameResolvedByStableIdentity()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.AutomationLoop.Ui.cs"));
        var method = source[
            source.IndexOf("private string DescribeNextTask", StringComparison.Ordinal)..
            source.IndexOf("private static string FormatNextTaskCountdown", StringComparison.Ordinal)];

        Assert.Contains("GetQueueItemCurrentVillageName(item)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("GetQueueItemVillageName(item)", method, StringComparison.Ordinal);
    }
}
