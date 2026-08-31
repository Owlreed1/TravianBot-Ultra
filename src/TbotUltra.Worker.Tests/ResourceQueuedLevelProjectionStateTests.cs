using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ResourceQueuedLevelProjectionStateTests
{
    [Fact]
    public void Persisted_projection_survives_defer_and_prevents_reselecting_the_same_slot()
    {
        var now = DateTimeOffset.Parse("2026-08-31T09:40:00Z");
        var state = ResourceQueuedLevelProjectionState.Parse(
            $"10:10:{now.AddMinutes(45).ToUnixTimeSeconds()}");
        var fields = new[]
        {
            new ResourceField(10, "iron", "Iron mine", 9, "/build.php?id=10"),
        };

        state.Reconcile(fields, resourceQueueObservedEmpty: false, now, nextQueueReviewAtUtc: now.AddMinutes(45));
        var plan = ResourceSnapshotCalculator.BuildBulkUpgradePlan(
            fields,
            targetLevel: 10,
            fallbackMax: 40,
            state.Levels,
            CompleteResources(),
            CompleteProduction(),
            warehouseCapacity: 100_000,
            granaryCapacity: 100_000);

        Assert.Null(plan.CandidateToInspect);
        Assert.True(plan.AnyQueuedTowardTarget);
        Assert.Equal(10, state.Levels[10]);
    }

    [Fact]
    public void Completed_live_level_removes_the_projection()
    {
        var now = DateTimeOffset.Parse("2026-08-31T09:40:00Z");
        var state = ResourceQueuedLevelProjectionState.Parse(
            $"10:10:{now.AddMinutes(45).ToUnixTimeSeconds()}");

        state.Reconcile(
            [new ResourceField(10, "iron", "Iron mine", 10, "/build.php?id=10")],
            resourceQueueObservedEmpty: false,
            now,
            nextQueueReviewAtUtc: now.AddMinutes(45));

        Assert.Empty(state.Levels);
    }

    [Fact]
    public void Expired_projection_is_removed_only_after_the_resource_queue_is_confirmed_empty()
    {
        var now = DateTimeOffset.Parse("2026-08-31T09:40:00Z");
        var serialized = $"10:10:{now.AddMinutes(-1).ToUnixTimeSeconds()}";
        var fields = new[]
        {
            new ResourceField(10, "iron", "Iron mine", 9, "/build.php?id=10"),
        };

        var activeState = ResourceQueuedLevelProjectionState.Parse(serialized);
        activeState.Reconcile(fields, resourceQueueObservedEmpty: false, now, now.AddMinutes(10));
        Assert.Equal(10, activeState.Levels[10]);

        var emptyState = ResourceQueuedLevelProjectionState.Parse(serialized);
        emptyState.Reconcile(fields, resourceQueueObservedEmpty: true, now, nextQueueReviewAtUtc: null);
        Assert.Empty(emptyState.Levels);
    }

    private static IReadOnlyDictionary<string, string> CompleteResources() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["wood"] = "100000",
            ["clay"] = "100000",
            ["iron"] = "100000",
            ["crop"] = "100000",
        };

    private static IReadOnlyDictionary<string, double?> CompleteProduction() =>
        new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase)
        {
            ["wood"] = 100,
            ["clay"] = 100,
            ["iron"] = 100,
            ["crop"] = 100,
        };
}
