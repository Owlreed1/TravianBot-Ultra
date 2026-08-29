using TbotUltra.Core.Configuration;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BanDetectionTimingSourceTests
{
    [Fact]
    public void ResourceRetryPaths_CheckAccountAccessBeforeWaitingOrReloading()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Resources",
            "TravianClient.Resources.Snapshot.cs"));

        var readLoop = SliceMethod(source, "ProductionByHour)> ReadResourceSnapshotAsync", "HasAnyProduction");
        var widgetLoop = SliceMethod(source, "private async Task WaitForResourceSnapshotWidgetsAsync", "ReadResourceSnapshotDiagnosticsAsync");

        Assert.Contains("await EnsureAccountAccessAllowedAsync(cancellationToken);", readLoop, StringComparison.Ordinal);
        Assert.Contains("await EnsureAccountAccessAllowedAsync(cancellationToken);", widgetLoop, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceSnapshotRead_RetriesWhenNavigationDestroysExecutionContext()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Resources",
            "TravianClient.Resources.Snapshot.cs"));
        var readLoop = SliceMethod(source, "ProductionByHour)> ReadResourceSnapshotAsync", "HasAnyProduction");

        Assert.Contains("BrowserFailureClassifier.IsTransientNavigation(ex)", readLoop, StringComparison.Ordinal);
        Assert.Contains("await WaitForResourceSnapshotWidgetsAsync(cancellationToken);", readLoop, StringComparison.Ordinal);
    }

    private static string SliceMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not locate source slice {startMarker}..{endMarker}.");
        return source[start..end];
    }
}
