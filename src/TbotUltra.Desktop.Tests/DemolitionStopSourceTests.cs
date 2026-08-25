using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class DemolitionStopSourceTests
{
    [Fact]
    public void StopDemolition_DoesNotDiscardTrackedSlotsBeforeUiReconciliation()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.Demolish.cs"));
        var methodStart = source.IndexOf("internal void OnStopDemolitionClicked()", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private bool RemoveDemolishQueueItem", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var method = source[methodStart..methodEnd];
        Assert.DoesNotContain("_buildingDemolishingSlots.Clear()", method, StringComparison.Ordinal);
        Assert.Contains("RefreshDemolishStatusForSelectedVillage();", method, StringComparison.Ordinal);
    }
}
