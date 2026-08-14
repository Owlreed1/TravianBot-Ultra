using TbotUltra.Desktop.Services;
using TbotUltra.Desktop.Services.Orchestration;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationQueueSelectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Select_UrgentWorkPreemptsTheCurrentVillageBatch()
    {
        var current = Candidate("current", "a", priority: 0);
        var urgent = Candidate("urgent", "b", priority: 100);

        var result = Select([current, urgent], new VillageBatchSnapshot("a", 1));

        Assert.Equal(urgent.Item.Id, result.Selected?.Id);
        Assert.Equal(AutomationQueueSelectionReason.UrgentPreemption, result.Reason);
    }

    [Fact]
    public void Select_KeepsReadyWorkInTheCurrentVillageBeforeTheFairnessLimit()
    {
        var current = Candidate("current", "a");
        var other = Candidate("other", "b");

        var result = Select([current, other], new VillageBatchSnapshot("a", 1));

        Assert.Equal(current.Item.Id, result.Selected?.Id);
        Assert.Equal(AutomationQueueSelectionReason.Selected, result.Reason);
    }

    [Fact]
    public void Select_RotatesToAnotherVillageAtTheFairnessLimit()
    {
        var current = Candidate("current", "a");
        var other = Candidate("other", "b");

        var result = Select(
            [current, other],
            new VillageBatchSnapshot("a", VillageBatchState.MaxAttempts));

        Assert.Equal(other.Item.Id, result.Selected?.Id);
        Assert.Equal(AutomationQueueSelectionReason.VillageRotationFairness, result.Reason);
    }

    [Fact]
    public void Select_ReadyCrossVillageWorkWinsBeforeAShortCurrentVillageHold()
    {
        var deferredCurrent = Candidate("deferred", "a", nextAttemptAt: Now.AddSeconds(10));
        var readyOther = Candidate("other", "b");

        var result = Select([deferredCurrent, readyOther], new VillageBatchSnapshot("a", 1));

        Assert.Equal(readyOther.Item.Id, result.Selected?.Id);
        Assert.Equal(AutomationQueueSelectionReason.VillageRotationNoReadyWork, result.Reason);
    }

    [Fact]
    public void Select_HoldsTheCurrentVillageOnlyWhenNoReadyWorkExistsElsewhere()
    {
        var deferredCurrent = Candidate("deferred", "a", nextAttemptAt: Now.AddSeconds(10));

        var result = Select([deferredCurrent], new VillageBatchSnapshot("a", 1));

        Assert.Null(result.Selected);
        Assert.Equal(AutomationQueueSelectionReason.ShortVillageHold, result.Reason);
        Assert.Equal(Now.AddSeconds(10), result.HoldUntil);
    }

    private static AutomationQueueSelectionResult Select(
        IReadOnlyList<ContinuousLoopSelectionCandidate> candidates,
        VillageBatchSnapshot batch) => AutomationQueueSelector.Select(
        new AutomationQueueSelectionInput(
            candidates,
            [QueueGroup.Construction],
            batch,
            "a",
            Now,
            ShortVillageDeferSeconds: 30,
            Preview: false),
        (items, now, _) => ContinuousLoopSelector.SelectReadyGroupHead(items, now));

    private static ContinuousLoopSelectionCandidate Candidate(
        string taskName,
        string villageKey,
        int priority = 0,
        DateTimeOffset? nextAttemptAt = null)
    {
        var item = new QueueItem
        {
            Id = Guid.NewGuid(),
            TaskName = taskName,
            Group = QueueGroup.Construction,
            Priority = priority,
            Status = QueueStatus.Pending,
            CreatedAt = Now,
            UpdatedAt = Now,
            NextAttemptAt = nextAttemptAt ?? Now,
        };
        return new ContinuousLoopSelectionCandidate(item, villageKey, true, false);
    }
}
