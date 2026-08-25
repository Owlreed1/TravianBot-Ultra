using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TravcoToolsWindowSourceTests
{
    [Fact]
    public void AddAllVillagesPopup_ShowsSavedSkipOwnVillagesBelowIgnoreAlliance()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectRootLocator.FindProjectRoot(),
            "src",
            "TbotUltra.Desktop",
            "TravcoToolsWindow.xaml.cs"));
        var ignoredAlliance = source.IndexOf("content.Children.Add(ignoredAlliances);", StringComparison.Ordinal);
        var skipOwn = source.IndexOf("content.Children.Add(skipOwnVillages);", StringComparison.Ordinal);

        Assert.Contains("Content = \"Skip own villages\"", source, StringComparison.Ordinal);
        Assert.Contains("IsChecked = saved.SkipOwnVillages", source, StringComparison.Ordinal);
        Assert.True(ignoredAlliance >= 0 && skipOwn > ignoredAlliance);
    }
}
