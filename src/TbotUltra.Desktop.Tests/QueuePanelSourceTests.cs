using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class QueuePanelSourceTests
{
    [Fact]
    public void ActiveQueueText_UsesExplicitThemeAwareCellTemplates()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "Views", "QueuePanel.xaml"));

        Assert.Contains("Text=\"{Binding GroupName}\" Foreground=\"{DynamicResource DataGridCellTextBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding VillageName}\" Foreground=\"{DynamicResource DataGridCellTextBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DisplayName}\" Foreground=\"{DynamicResource DataGridCellTextBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding BuildTimeText}\" Foreground=\"{DynamicResource DataGridCellTextBrush}\"", xaml, StringComparison.Ordinal);
    }
}
