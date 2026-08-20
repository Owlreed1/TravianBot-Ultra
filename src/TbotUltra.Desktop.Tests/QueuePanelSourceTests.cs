using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class QueuePanelSourceTests
{
    [Fact]
    public void QueueTables_UseTheSharedWorkingDataGridCellAndHeaderStyles()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "Views", "QueuePanel.xaml"));

        Assert.DoesNotContain("<DataGrid.CellStyle>", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<DataGrid.ColumnHeaderStyle>", xaml, StringComparison.Ordinal);
        Assert.Contains("DataGridTextColumn Header=\"Group\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DataGridTextColumn Header=\"Village\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DataGridTextColumn Header=\"Task\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DataGridTextColumn Header=\"Time\"", xaml, StringComparison.Ordinal);
    }
}
