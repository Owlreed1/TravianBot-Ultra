using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class VillageBatchStateTests
{
    [Fact]
    public void SnapshotFor_NewVerifiedVillageStartsFreshBatch()
    {
        var state = new VillageBatchState();
        state.RecordAttempt("a", "a");

        var snapshot = state.SnapshotFor("b");

        Assert.Equal("b", snapshot.VillageKey);
        Assert.Equal(0, snapshot.AttemptCount);
        Assert.Equal(1, state.SnapshotFor("a").AttemptCount);
    }

    [Fact]
    public void RecordAttempt_KeepsTrackingAttemptsWithoutEndingTheVillageBatch()
    {
        var state = new VillageBatchState();

        VillageBatchSnapshot snapshot = default;
        for (var index = 0; index < 100; index++)
        {
            snapshot = state.RecordAttempt("a", "a");
        }

        Assert.Equal(100, snapshot.AttemptCount);
    }

    [Fact]
    public void RecordAttempt_UrgentTargetWaitsForVerifiedVillageBeforeStartingTargetBatch()
    {
        var state = new VillageBatchState();
        state.RecordAttempt("a", "a");

        var beforeVerification = state.RecordAttempt("b", "a");
        state.ObserveVerifiedVillage("b");
        var afterVerification = state.SnapshotFor("b");

        Assert.Equal("a", beforeVerification.VillageKey);
        Assert.Equal(2, beforeVerification.AttemptCount);
        Assert.Equal("b", afterVerification.VillageKey);
        Assert.Equal(1, afterVerification.AttemptCount);
    }

    [Fact]
    public void ObserveVerifiedVillage_PreservesAttemptCountForExpectedTarget()
    {
        var state = new VillageBatchState();
        state.RecordAttempt("b", "a");

        state.ObserveVerifiedVillage("b");

        Assert.Equal(1, state.SnapshotFor("b").AttemptCount);
    }

    [Fact]
    public void UrgentPreemption_PreservesTheInterruptedVillageUntilItCanResume()
    {
        var state = new VillageBatchState();
        state.RecordAttempt("a", "a");

        state.RecordUrgentPreemption("a", "b");
        state.RecordAttempt("b", "a");
        state.ObserveVerifiedVillage("a");
        state.ObserveVerifiedVillage("b");

        var interrupted = state.SnapshotFor("b");
        Assert.Equal("a", interrupted.VillageKey);
        Assert.Equal("b", interrupted.UrgentTargetVillageKey);
        Assert.True(interrupted.HasUrgentPreemption);

        state.RecordAttempt("a", "b");
        state.ObserveVerifiedVillage("a");

        var resumed = state.SnapshotFor("a");
        Assert.Equal("a", resumed.VillageKey);
        Assert.Null(resumed.UrgentTargetVillageKey);
        Assert.False(resumed.HasUrgentPreemption);
    }

    [Fact]
    public void VillageLessUrgentWork_PreservesTheInterruptedVillageAcrossNavigation()
    {
        var state = new VillageBatchState();
        state.RecordAttempt("a", "a");

        state.RecordUrgentPreemption("a", targetVillageKey: null);
        state.RecordAttempt(targetVillageKey: null, verifiedVillageKey: "a");
        state.ObserveVerifiedVillage("b");

        var interrupted = state.SnapshotFor("b");
        Assert.Equal("a", interrupted.VillageKey);
        Assert.True(interrupted.HasUrgentPreemption);
    }

    [Fact]
    public void CompleteUrgentPreemption_ReleasesACompletedInterruptedVillage()
    {
        var state = new VillageBatchState();
        state.RecordAttempt("a", "a");
        state.RecordUrgentPreemption("a", "b");
        state.ObserveVerifiedVillage("b");

        state.CompleteUrgentPreemption("b");

        var completed = state.SnapshotFor("b");
        Assert.Equal("b", completed.VillageKey);
        Assert.False(completed.HasUrgentPreemption);
    }

    [Theory]
    [InlineData(QueueGroup.Account, -50, true)]
    [InlineData(QueueGroup.Farming, 1, true)]
    [InlineData(QueueGroup.Construction, 0, false)]
    [InlineData(QueueGroup.Farming, -50, false)]
    public void IsUrgentItem_UsesAccountOrPositivePriority(
        QueueGroup group,
        int priority,
        bool expected)
    {
        var item = new QueueItem { Group = group, Priority = priority };

        Assert.Equal(expected, ContinuousLoopSelector.IsUrgentItem(item));
    }
}
