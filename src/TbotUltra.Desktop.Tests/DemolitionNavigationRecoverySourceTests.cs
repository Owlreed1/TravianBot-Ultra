using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class DemolitionNavigationRecoverySourceTests
{
    [Fact]
    public void QueueFailure_DemolitionNavigationRaceDefersWithoutConsumingRetries()
    {
        var source = ReadSource("MainWindow.QueueExecution.cs");
        var failureHandler = source[source.IndexOf("private async Task<bool> HandleQueueItemFailureAsync", StringComparison.Ordinal)..];

        Assert.Contains("IsDemolishQueueItem(item)", failureHandler, StringComparison.Ordinal);
        Assert.Contains("BrowserFailureClassifier.IsTransientNavigation(ex)", failureHandler, StringComparison.Ordinal);
        Assert.Contains("without consuming retries", failureHandler, StringComparison.Ordinal);

        var recovery = failureHandler.IndexOf("BrowserFailureClassifier.IsTransientNavigation(ex)", StringComparison.Ordinal);
        var terminalFailure = failureHandler.IndexOf("MarkQueueItemExecutionFailed(item.Id)", StringComparison.Ordinal);
        Assert.True(recovery >= 0 && recovery < terminalFailure,
            "The demolition navigation recovery must run before terminal retry accounting.");
    }

    private static string ReadSource(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "TbotUltra.Desktop", fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new DirectoryNotFoundException($"Could not locate {fileName}.");
    }
}
