using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BanRecoverySourceTests
{
    [Fact]
    public void RecoveryScan_DoesNotUseNormalSweepMutationOrExecutionPaths()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(projectRoot, "src", "TbotUltra.Desktop", "MainWindow.BanRecovery.cs"));
        var scanEnd = source.IndexOf("private MessageBoxResult ShowBanRecoveryDecision", StringComparison.Ordinal);
        Assert.True(scanEnd > 0);
        var scan = source[..scanEnd];

        Assert.Contains("requireCompleteStructure: true", scan, StringComparison.Ordinal);
        Assert.Contains("readOnlyObservation: true", scan, StringComparison.Ordinal);
        Assert.DoesNotContain("CollectVillageStatusSweepRewardsAsync", scan, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureContinuousLoopRuntimeItemsAsync", scan, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteReadyVillageStatusSweepTasksAsync", scan, StringComparison.Ordinal);
        Assert.DoesNotContain("ReconcilePendingBuildingQueueWithLiveStatus", scan, StringComparison.Ordinal);
    }

    [Fact]
    public void StartButton_OffersPendingRecoveryBeforeStartingContinuousLoop()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(projectRoot, "src", "TbotUltra.Desktop", "MainWindow.Toolbar.cs"));
        var recoveryIndex = source.IndexOf("await TryRunPendingBanRecoveryAsync()", StringComparison.Ordinal);
        var startIndex = source.IndexOf("StartContinuousLoopRunner();", recoveryIndex, StringComparison.Ordinal);

        Assert.True(recoveryIndex >= 0 && startIndex > recoveryIndex);
    }
}
