using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ConstructionSlotConflictAlarmSourceTests
{
    [Fact]
    public void UnresolvedConstructSlotConflict_RaisesActionableAlarmBeforeDeferring()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.QueueExecution.cs"));
        var methodStart = source.IndexOf("private bool TryHandleOccupiedConstructSlotBeforeGuards", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private async Task<bool> TryHandleConstructQueueFullBeforeRequirementGuardAsync", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var method = source[methodStart..methodEnd];
        var unresolvedStart = method.IndexOf("if (conflict.ReboundSlotId is not int reboundSlotId)", StringComparison.Ordinal);
        var unresolvedEnd = method.IndexOf("if (!_botService.MarkQueueItemDeferred", unresolvedStart, StringComparison.Ordinal);

        Assert.True(unresolvedStart >= 0 && unresolvedEnd > unresolvedStart);
        var unresolvedBranch = method[unresolvedStart..unresolvedEnd];
        Assert.Contains("ALARM:", unresolvedBranch, StringComparison.Ordinal);
        Assert.Contains("unknownSlots", unresolvedBranch, StringComparison.Ordinal);
        Assert.Contains("no construction click was attempted", unresolvedBranch, StringComparison.Ordinal);
    }
}
