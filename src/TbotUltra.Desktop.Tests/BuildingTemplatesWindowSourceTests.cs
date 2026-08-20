using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BuildingTemplatesWindowSourceTests
{
    [Fact]
    public void LoadingOverlay_CoversTheWholeWindowAndStaysUntilInitialPreviewCompletes()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml.cs"));

        Assert.Contains("Grid.ColumnSpan=\"3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"-14\"", xaml, StringComparison.Ordinal);
        Assert.Contains("LoadingOverlay.Show", code, StringComparison.Ordinal);
        var initialPreview = code.IndexOf("RefreshPlanPreview();", code.IndexOf("SelectedTemplate = Templates[0]", StringComparison.Ordinal), StringComparison.Ordinal);
        var hideOverlay = code.IndexOf("LoadingOverlay.Hide();", StringComparison.Ordinal);
        Assert.True(initialPreview >= 0 && hideOverlay > initialPreview, "The initial preview must finish before the loading overlay is hidden.");
    }

    [Fact]
    public void RowEditing_IsDebouncedAndSupportsMovingToBothEnds()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml"));

        Assert.Contains("RequestPlanPreviewRefresh();", code, StringComparison.Ordinal);
        Assert.Contains("Interval = TimeSpan.FromMilliseconds(120)", code, StringComparison.Ordinal);
        Assert.Contains("DropDownOpened=\"BuildingOptions_DropDownOpened\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshBuildingOptionAvailability();", code, StringComparison.Ordinal);
        Assert.Contains("MoveSelectedRowTo(0)", code, StringComparison.Ordinal);
        Assert.Contains("MoveSelectedRowTo(Rows.Count - 1)", code, StringComparison.Ordinal);
        Assert.Contains("Content=\"Top\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Bottom\"", xaml, StringComparison.Ordinal);
    }
}
