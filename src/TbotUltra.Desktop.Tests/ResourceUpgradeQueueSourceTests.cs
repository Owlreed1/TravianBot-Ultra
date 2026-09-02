using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ResourceUpgradeQueueSourceTests
{
    [Fact]
    public void QueueUpgradeAllResources_CommitsVisibleCheckboxesBeforeReadingSelection()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Resources.Actions.cs"));

        var methodStart = source.IndexOf("private void QueueUpgradeAllResources", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private int ResolveSelectedVillageResourceMaxLevel", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];
        var commitIndex = method.IndexOf("ResourcesPanelControl.CommitUpgradeTypeSelection()", StringComparison.Ordinal);
        var readIndex = method.IndexOf("_resourcesViewModel.SelectedUpgradeTypes", StringComparison.Ordinal);

        Assert.True(commitIndex >= 0, "The queue action must commit the checkbox values shown in the Resources panel.");
        Assert.True(readIndex > commitIndex, "The committed checkbox values must be read after the UI binding sources are updated.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
