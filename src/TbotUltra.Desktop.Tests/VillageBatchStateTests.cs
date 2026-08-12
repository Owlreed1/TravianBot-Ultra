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
    public void RecordAttempt_ReachesFairnessLimitAfterTenAttempts()
    {
        var state = new VillageBatchState();

        VillageBatchSnapshot snapshot = default;
        for (var index = 0; index < VillageBatchState.MaxAttempts; index++)
        {
            snapshot = state.RecordAttempt("a", "a");
        }

        Assert.Equal(VillageBatchState.MaxAttempts, snapshot.AttemptCount);
        Assert.True(snapshot.HasReachedLimit);
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
    public void LogRegression_DeferredH03CannotBlockReadyH02()
    {
        var h03 = new VillageBatchSnapshot("h03", 9);

        var keepH03 = VillageBatchState.ShouldKeepCurrentVillage(
            h03,
            currentVillageHasReadyWork: false,
            anotherVillageHasReadyWork: true);

        Assert.False(keepH03);
    }

    [Fact]
    public void FairnessLimit_RotatesOnlyWhenAnotherVillageIsReady()
    {
        var capped = new VillageBatchSnapshot("a", VillageBatchState.MaxAttempts);

        Assert.False(VillageBatchState.ShouldKeepCurrentVillage(capped, true, true));
        Assert.True(VillageBatchState.ShouldKeepCurrentVillage(capped, true, false));
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
