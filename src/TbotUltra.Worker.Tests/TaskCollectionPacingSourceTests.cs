using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class TaskCollectionPacingSourceTests
{
    [Fact]
    public void TaskTabSwitch_AppliesClickPacingBeforeDomClick()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Core",
            "TravianClient.Tasks.cs"));
        var methodStart = source.IndexOf(
            "private async Task<bool> SwitchTasksTabAsync",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "private async Task<bool> HasVisibleCollectButton",
            methodStart,
            StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.NotEmpty(method);
        var delayIndex = method.IndexOf("DelayBeforeClickAsync", StringComparison.Ordinal);
        var clickIndex = method.IndexOf("target.click()", StringComparison.Ordinal);
        Assert.True(delayIndex >= 0, "Task tab switching must use the configured click pacing.");
        Assert.True(delayIndex < clickIndex, "Click pacing must finish before the General/Village tab click.");
        Assert.DoesNotContain("ApplyActionDelayAsync", method, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate TbotUltra.sln from the test output directory.");
    }
}
