using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ContinuousLoopSchedulingSourceTests
{
    [Fact]
    public void ReadyCrossVillageWork_IsSelectedBeforeShortVillageHold()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.ContinuousLoop.cs"));
        var methodStart = source.IndexOf(
            "private QueueItem? SelectNextQueueItemForContinuousLoop",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    private bool IsQueueItemGroupEnabledForItsVillage",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];
        var crossVillageSelection = methodBody.IndexOf(
            "if (otherVillageCandidate is not null)",
            StringComparison.Ordinal);
        var shortHold = methodBody.IndexOf(
            "ContinuousLoopSelector.ResolveShortVillageHoldUntil(",
            StringComparison.Ordinal);

        Assert.True(crossVillageSelection >= 0 && shortHold > crossVillageSelection);
    }

    [Fact]
    public void ContinuousAndAutoQueue_RecordSharedVillageBatchAttempts()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.ContinuousLoop.cs"));

        Assert.Contains(
            "RecordVillageBatchAttempt(item, $\"LOOP {_continuousAutomationTickId}\")",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RecordVillageBatchAttempt(item, $\"AUTOQ {_autoQueueRunLogId}\")",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_continuousGroupRotationVillageKeys", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_continuousConstructionRotationVillageKey", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_autoQueueRotationVillageKey", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VillageScan_StopsReactingAtSharedBatchLimit()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.ContinuousLoop.cs"));
        var methodStart = source.IndexOf(
            "private async Task<bool> ExecuteReadyVillageStatusSweepTasksAsync",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    private QueueItem? SelectNextQueueItemForVillageStatusSweep",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];
        Assert.Contains("attempts < VillageBatchState.MaxAttempts", methodBody, StringComparison.Ordinal);
        Assert.Contains("continuing the scan round", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void SavingChangedShortVillageWait_WakesRunningLoop()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Toolbar.cs"));

        Assert.Contains(
            "optionsAfterSettings.ShortVillageDeferSeconds != optionsBeforeSettings.ShortVillageDeferSeconds",
            source,
            StringComparison.Ordinal);
        Assert.Contains("&& IsContinuousLoopRunning()", source, StringComparison.Ordinal);
        Assert.Contains("RequestContinuousAutomationWake();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FarmListToggle_UpdatesPayloadWithoutChangingCooldownDeadline()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Farming.FarmLists.cs"));
        var methodStart = source.IndexOf(
            "private void RefreshQueuedContinuousFarmListSelections()",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    private IReadOnlySet<string> LoadConfiguredContinuousFarmListNames()",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];
        Assert.Contains("UpdateDeferredQueueItem(item.Id, updatedPayload)", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("PatchDeferredQueueItem", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("NextAttemptAt", methodBody, StringComparison.Ordinal);
    }
}
