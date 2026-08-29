using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ResourceRefreshCancellationSourceTests
{
    [Fact]
    public void ExpectedSessionCancellation_IsHandledBeforeGenericFailureLogging()
    {
        var root = FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Resources.Snapshot.cs"));
        var start = source.IndexOf("private async Task<VillageStatus?> RefreshResourceSnapshotForUiAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task EnsureTravianLanguageForCurrentPageAsync", start, StringComparison.Ordinal);
        var method = source[start..end];

        var cancellationCatch = method.IndexOf("catch (OperationCanceledException)", StringComparison.Ordinal);
        var genericCatch = method.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
        Assert.True(cancellationCatch >= 0 && cancellationCatch < genericCatch);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
