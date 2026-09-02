using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BrowserShutdownVerificationSourceTests
{
    [Fact]
    public void RunnerShutdown_VerifiesTrackedProcessesEvenWithoutASharedSessionAndPropagatesFailure()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "BotTaskRunner.cs"));
        var methodStart = source.IndexOf("public async Task ShutdownAsync(Action<string>? log = null)", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("    public async Task SaveBrowserStateAsync", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd].ReplaceLineEndings("\n");

        Assert.Contains("LaunchedBrowserRegistry.CleanupTrackedBrowsers", methodBody, StringComparison.Ordinal);
        Assert.Contains("processCleanup.RemainingCount > 0", methodBody, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException(\"Browser shutdown could not be verified.\"", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("if (_sharedVisibleSession is null)\n            {\n                return;", methodBody, StringComparison.Ordinal);
    }
}
