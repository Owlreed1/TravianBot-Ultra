using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ResourceRefreshNavigationSourceTests
{
    [Fact]
    public void TransientPageReadFailure_IncludesNavigationContextReplacement()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Resources.Snapshot.cs"));
        var methodStart = source.IndexOf(
            "private static bool IsTransientPageReadFailure(Exception ex)",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    internal static bool IsTransientConnectionFailure",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        Assert.Contains(
            "BrowserFailureClassifier.IsTransientNavigation(ex)",
            source[methodStart..methodEnd],
            StringComparison.Ordinal);
    }
}
