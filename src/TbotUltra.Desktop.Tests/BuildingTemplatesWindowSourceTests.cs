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

    [Fact]
    public void TemplateAndRowDeleteActions_UseCompactIconOnlyButtons()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml"));

        Assert.DoesNotContain("<TextBlock Text=\"New\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBlock Text=\"Duplicate\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBlock Text=\"Delete\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"RemoveRowButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"DeleteRowButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Create a new empty template\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Duplicate the selected template\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Delete the selected template\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAllowsDraftsAndQueueValidationFailureIsShownExplicitly()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml.cs"));

        var saveHandlerStart = code.IndexOf("private void SaveButton_Click", StringComparison.Ordinal);
        var queueHandlerStart = code.IndexOf("private void QueueTemplateButton_Click", StringComparison.Ordinal);
        var closeHandlerStart = code.IndexOf("private void CloseButton_Click", StringComparison.Ordinal);
        Assert.True(saveHandlerStart >= 0 && queueHandlerStart > saveHandlerStart && closeHandlerStart > queueHandlerStart);

        var saveHandler = code[saveHandlerStart..queueHandlerStart];
        var queueHandler = code[queueHandlerStart..closeHandlerStart];
        Assert.Contains("SaveAllTemplates(skipValidation: true)", saveHandler, StringComparison.Ordinal);
        Assert.Contains("AppDialog.Show(", queueHandler, StringComparison.Ordinal);
        Assert.Contains("\"Cannot queue template\"", queueHandler, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportAndExportUseCompactIconsAndAConflictPreview()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml"));
        var previewXaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplateImportWindow.xaml"));
        var previewCode = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplateImportWindow.xaml.cs"));
        var code = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml.cs"));

        Assert.Contains("TemplateImportIconGeometry", xaml, StringComparison.Ordinal);
        Assert.Contains("TemplateExportIconGeometry", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Import templates\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Export templates\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"8,0,6,0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Selected template\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"All templates\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Import manifest", previewXaml, StringComparison.Ordinal);
        Assert.Contains("Import as copy", previewCode, StringComparison.Ordinal);
        Assert.Contains("SaveAllTemplates(skipValidation: true)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportMenu_UsesALocalThemeTemplateWithoutSystemCheckGutter()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml"));

        Assert.Contains("x:Key=\"ExportMenuItemStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Property=\"OverridesDefaultStyle\" Value=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource SurfaceBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource ExportMenuItemStyle}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void RowDeleteIcon_PreservesItsTwentyFourPixelDrawingViewport()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "BuildingTemplatesWindow.xaml"));

        var rowDeleteStart = xaml.IndexOf("ToolTip=\"Delete row\"", StringComparison.Ordinal);
        var rowDeleteEnd = xaml.IndexOf("</Button>", rowDeleteStart, StringComparison.Ordinal);
        Assert.True(rowDeleteStart >= 0 && rowDeleteEnd > rowDeleteStart);
        var rowDeleteButton = xaml[rowDeleteStart..rowDeleteEnd];
        Assert.Contains("<Viewbox Width=\"14\" Height=\"14\"", rowDeleteButton, StringComparison.Ordinal);
        Assert.Contains("<Canvas Width=\"24\" Height=\"24\">", rowDeleteButton, StringComparison.Ordinal);
        Assert.Contains("TemplateRowDeleteIconStyle", rowDeleteButton, StringComparison.Ordinal);
    }
}
