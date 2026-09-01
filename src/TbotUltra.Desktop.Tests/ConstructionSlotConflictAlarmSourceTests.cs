using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ConstructionSlotConflictAlarmSourceTests
{
    [Fact]
    public void UnresolvedConstructSlotConflict_RaisesAlarmAndMovesTaskToHistory()
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
        var fullVillageStart = unresolvedBranch.IndexOf(
            "if (unknownSlots.Count == 0 && conflict.ConfirmedEmptySlotIds.Count == 0)",
            StringComparison.Ordinal);
        var fullVillageEnd = unresolvedBranch.IndexOf(
            "_botService.MarkQueueItemDeferred",
            fullVillageStart,
            StringComparison.Ordinal);

        Assert.True(fullVillageStart >= 0 && fullVillageEnd > fullVillageStart);
        var fullVillageBranch = unresolvedBranch[fullVillageStart..fullVillageEnd];
        Assert.Contains("ALARM:", unresolvedBranch, StringComparison.Ordinal);
        Assert.Contains("unknownSlots", unresolvedBranch, StringComparison.Ordinal);
        Assert.Contains("no construction click was attempted", unresolvedBranch, StringComparison.Ordinal);
        Assert.Contains("MarkQueueItemPermanentlyFailed", fullVillageBranch, StringComparison.Ordinal);
        Assert.Contains("moved to History", fullVillageBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkQueueItemDeferred", fullVillageBranch, StringComparison.Ordinal);
    }
}
