using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TravcoToolsControlSourceTests
{
    [Fact]
    public void ActiveSession_ExposesGlobalCloseActionAcrossUiNavigation()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.xaml"));
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Farming.Travco.cs"));

        var globalAction = xaml.IndexOf("x:Name=\"TravcoSessionAttentionBorder\"", StringComparison.Ordinal);
        var navigation = xaml.IndexOf("x:Name=\"DashboardNavButton\"", StringComparison.Ordinal);

        Assert.True(globalAction >= 0 && globalAction < navigation);
        Assert.Contains("Style=\"{StaticResource TravcoAttentionRingStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("GlobalCloseTravcoTabButton_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("_travcoSessionActive ? System.Windows.Visibility.Visible", source, StringComparison.Ordinal);
        Assert.Contains("RequestCloseSessionAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAllVillagesPopup_ShowsSavedSkipOwnVillagesBelowIgnoreAlliance()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectRootLocator.FindProjectRoot(),
            "src",
            "TbotUltra.Desktop",
            "TravcoToolsControl.xaml.cs"));
        var ignoredAlliance = source.IndexOf("content.Children.Add(ignoredAlliances);", StringComparison.Ordinal);
        var skipOwn = source.IndexOf("content.Children.Add(skipOwnVillages);", StringComparison.Ordinal);

        Assert.Contains("Content = \"Skip own villages\"", source, StringComparison.Ordinal);
        Assert.Contains("IsChecked = saved.SkipOwnVillages", source, StringComparison.Ordinal);
        Assert.True(ignoredAlliance >= 0 && skipOwn > ignoredAlliance);
    }
}
