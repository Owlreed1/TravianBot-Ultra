using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ResourceSnapshotCalculatorTests
{
    [Fact]
    public void MergeProductionByHour_LiveValuesWinAndCacheFillsMissingValues()
    {
        var live = new Dictionary<string, double?> { ["wood"] = 120, ["clay"] = null };
        var cached = new Dictionary<string, double?> { ["wood"] = 90, ["clay"] = 80, ["iron"] = 70 };

        var result = ResourceSnapshotCalculator.MergeProductionByHour(live, cached);

        Assert.Equal(120, result["wood"]);
        Assert.Equal(80, result["clay"]);
        Assert.Equal(70, result["iron"]);
        Assert.Null(result["crop"]);
    }

    [Fact]
    public void OrderUpgradeCandidates_LowestLevelFirstWithoutStockSnapshot()
    {
        var fields = new[]
        {
            Field(3, "wood", 4),
            Field(2, "clay", 2),
            Field(1, "iron", 2),
            Field(null, "crop", 1),
        };

        var result = ResourceSnapshotCalculator.OrderUpgradeCandidates(fields, stockByType: null);

        Assert.Equal(new int?[] { 1, 2, 3 }, result.Select(field => field.SlotId));
    }

    [Fact]
    public void OrderUpgradeCandidates_SmartModeUsesStockThenLevelThenSlot()
    {
        var fields = new[]
        {
            Field(4, "wood", 1),
            Field(3, "clay", 5),
            Field(2, "iron", 3),
            Field(1, "iron", 3),
        };
        var stocks = new Dictionary<string, long> { ["wood"] = 500, ["clay"] = 100, ["iron"] = 100 };

        var result = ResourceSnapshotCalculator.OrderUpgradeCandidates(fields, stocks);

        Assert.Equal(new int?[] { 1, 2, 3, 4 }, result.Select(field => field.SlotId));
    }

    [Fact]
    public void OrderUpgradeCandidates_QueuedLevelRanksAfterUntouchedFieldAtLowerProjectedLevel()
    {
        var fields = new[]
        {
            Field(13, "crop", 0),
            Field(14, "wood", 0),
            Field(15, "crop", 1),
        };
        var queuedLevelsBySlot = new Dictionary<int, int>
        {
            [13] = 1,
        };

        var result = ResourceSnapshotCalculator.OrderUpgradeCandidates(
            fields,
            stockByType: null,
            queuedLevelsBySlot);

        Assert.Equal(new int?[] { 14, 13, 15 }, result.Select(field => field.SlotId));
    }

    [Fact]
    public void OrderUpgradeCandidates_Dorf1QueuedLevelRanksAfterUntouchedFieldAtLowerLevel()
    {
        var fields = new[]
        {
            new ResourceField(3, "wood", "Woodcutter", 6, "/build.php?id=3", QueuedLevel: 7),
            new ResourceField(4, "wood", "Woodcutter", 6, "/build.php?id=4"),
        };

        var queuedLevels = ResourceSnapshotCalculator.BuildDorf1QueuedLevelProjections(fields);
        var result = ResourceSnapshotCalculator.OrderUpgradeCandidates(fields, null, queuedLevels);

        Assert.Equal(new int?[] { 4, 3 }, result.Select(field => field.SlotId));
    }

    [Fact]
    public void MergeQueuedLevelProjections_ConfirmedUpgradeSurvivesMissingLiveSlotIdentity()
    {
        var fields = new[]
        {
            Field(5, "clay", 4),
            Field(10, "iron", 4),
        };
        var liveQueuedLevels = new Dictionary<int, int>();
        var confirmedDuringOperation = new Dictionary<int, int> { [5] = 5 };

        var merged = ResourceSnapshotCalculator.MergeQueuedLevelProjections(
            liveQueuedLevels,
            confirmedDuringOperation);
        var result = ResourceSnapshotCalculator.OrderUpgradeCandidates(fields, null, merged);

        Assert.Equal(new int?[] { 10, 5 }, result.Select(field => field.SlotId));
    }

    [Fact]
    public void BuildUpgradeOfferIdentity_GroupsOnlySameResourceTypeAndLevel()
    {
        var cropLevelEight = ResourceSnapshotCalculator.BuildUpgradeOfferIdentity("crop", 8);

        Assert.Equal(cropLevelEight, ResourceSnapshotCalculator.BuildUpgradeOfferIdentity(" Crop ", 8));
        Assert.NotEqual(cropLevelEight, ResourceSnapshotCalculator.BuildUpgradeOfferIdentity("crop", 9));
        Assert.NotEqual(cropLevelEight, ResourceSnapshotCalculator.BuildUpgradeOfferIdentity("wood", 8));
    }

    [Fact]
    public void BuildStorageForecasts_UsesGranaryForCropAndWarehouseForOtherResources()
    {
        var resources = new Dictionary<string, string>
        {
            ["wood"] = "500",
            ["clay"] = "0",
            ["iron"] = "1000",
            ["crop"] = "250",
        };
        var production = new Dictionary<string, double?>
        {
            ["wood"] = 100,
            ["clay"] = 0,
            ["iron"] = 100,
            ["crop"] = 250,
        };

        var result = ResourceSnapshotCalculator.BuildStorageForecasts(resources, 1000, 500, production);

        var wood = Assert.Single(result, item => item.ResourceKey == "wood");
        var crop = Assert.Single(result, item => item.ResourceKey == "crop");
        Assert.Equal(50, wood.PercentOfCapacity);
        Assert.Equal(18_000, wood.SecondsToFull);
        Assert.Equal(500, crop.Capacity);
        Assert.Equal(50, crop.PercentOfCapacity);
        Assert.Equal(3_600, crop.SecondsToFull);
    }

    [Fact]
    public void BuildStorageForecasts_ComputesCropTimeToEmptyForNegativeProduction()
    {
        var resources = new Dictionary<string, string> { ["crop"] = "6000" };
        var production = new Dictionary<string, double?> { ["crop"] = -12_000 };

        var result = ResourceSnapshotCalculator.BuildStorageForecasts(resources, 10_000, 20_000, production);

        var crop = Assert.Single(result, item => item.ResourceKey == "crop");
        Assert.Equal(1_800, crop.SecondsToEmpty);
        Assert.Null(crop.SecondsToFull);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(0, 0)]
    [InlineData(10, 11)]
    [InlineData(50_000, 43_200)]
    public void ComputeUpgradeWaitSeconds_AddsBufferAndCapsWait(int? detected, int expected)
    {
        Assert.Equal(expected, ResourceSnapshotCalculator.ComputeUpgradeWaitSeconds(detected));
    }

    [Fact]
    public void EvaluateUpgradeAffordability_UsesLongestResourceWait()
    {
        var resources = new Dictionary<string, string>
        {
            ["wood"] = "100",
            ["clay"] = "100",
            ["iron"] = "100",
            ["crop"] = "100",
        };
        var production = new Dictionary<string, double?>
        {
            ["wood"] = 100,
            ["clay"] = 200,
            ["iron"] = 100,
            ["crop"] = 100,
        };

        var result = ResourceSnapshotCalculator.EvaluateUpgradeAffordability(
            wood: 200, clay: 300, iron: 100, crop: 100, resources, production);

        Assert.False(result.HasUnknownWait);
        Assert.Equal(3_600, result.TimeUntilAffordableSeconds);
        Assert.Equal(700, result.TotalCost);
    }

    [Fact]
    public void EvaluateUpgradeAffordability_MarksMissingProductionAsUnknown()
    {
        var resources = new Dictionary<string, string> { ["wood"] = "0" };
        var production = new Dictionary<string, double?> { ["wood"] = null };

        var result = ResourceSnapshotCalculator.EvaluateUpgradeAffordability(
            wood: 1, clay: 0, iron: 0, crop: 0, resources, production);

        Assert.True(result.HasUnknownWait);
        Assert.Equal(long.MaxValue, result.TimeUntilAffordableSeconds);
    }

    [Fact]
    public void BuildBulkUpgradePlan_SkipsUnaffordableFieldAndSelectsAffordableFieldWithoutNavigation()
    {
        var expensiveIron = BuildingCatalogService.CostFor(3, 10)!;
        var affordableClay = BuildingCatalogService.CostFor(2, 1)!;
        var fields = new[]
        {
            Field(1, "iron", 9),
            Field(2, "clay", 0),
        };
        var resources = ResourcesFor(affordableClay);
        Assert.True(expensiveIron.Wood > affordableClay.Wood
            || expensiveIron.Clay > affordableClay.Clay
            || expensiveIron.Iron > affordableClay.Iron
            || expensiveIron.Crop > affordableClay.Crop);

        var plan = ResourceSnapshotCalculator.BuildBulkUpgradePlan(
            fields,
            targetLevel: 10,
            fallbackMax: 40,
            queuedLevelsBySlot: new Dictionary<int, int>(),
            resources,
            CompleteProduction(),
            warehouseCapacity: 100_000,
            granaryCapacity: 100_000);

        Assert.True(plan.IsComplete);
        Assert.Equal(2, plan.CandidateToInspect?.Field.SlotId);
        Assert.Equal(1, Assert.Single(plan.BlockedByResources).Field.SlotId);
    }

    [Fact]
    public void BuildBulkUpgradePlan_UsesProjectedQueuedLevelForTheNextCatalogCost()
    {
        var levelThreeCost = BuildingCatalogService.CostFor(1, 3)!;
        var fields = new[] { Field(7, "wood", 1) };

        var plan = ResourceSnapshotCalculator.BuildBulkUpgradePlan(
            fields,
            targetLevel: 4,
            fallbackMax: 40,
            queuedLevelsBySlot: new Dictionary<int, int> { [7] = 2 },
            ResourcesFor(levelThreeCost),
            CompleteProduction(),
            warehouseCapacity: 100_000,
            granaryCapacity: 100_000);

        var candidate = Assert.IsType<ResourceBulkUpgradeCandidate>(plan.CandidateToInspect);
        Assert.Equal(2, candidate.ProjectedLevel);
        Assert.Equal(3, candidate.OfferLevel);
        Assert.Equal(levelThreeCost, candidate.Cost);
    }

    [Fact]
    public void BuildBulkUpgradePlan_WhenNoneAreAffordable_ProvidesOneRecoveryCandidateAndEarliestWait()
    {
        var fields = new[]
        {
            Field(1, "iron", 2),
            Field(2, "clay", 2),
        };
        var production = CompleteProduction(100);

        var plan = ResourceSnapshotCalculator.BuildBulkUpgradePlan(
            fields,
            targetLevel: 10,
            fallbackMax: 40,
            queuedLevelsBySlot: new Dictionary<int, int>(),
            EmptyResources(),
            production,
            warehouseCapacity: 100_000,
            granaryCapacity: 100_000);

        Assert.True(plan.IsComplete);
        Assert.Null(plan.CandidateToInspect);
        Assert.Equal(1, plan.RecoveryCandidate?.Field.SlotId);
        Assert.Equal(2, plan.BlockedByResources.Count);
        Assert.NotNull(plan.EarliestBlockedCandidate);
        Assert.True(plan.EarliestBlockedCandidate!.Affordability.TimeUntilAffordableSeconds > 0);
    }

    [Fact]
    public void BuildBulkUpgradePlan_IncompleteDorf1SnapshotRequestsLegacyFallback()
    {
        var plan = ResourceSnapshotCalculator.BuildBulkUpgradePlan(
            new[] { Field(1, "wood", 1) },
            targetLevel: 10,
            fallbackMax: 40,
            queuedLevelsBySlot: new Dictionary<int, int>(),
            resources: new Dictionary<string, string> { ["wood"] = "100" },
            CompleteProduction(),
            warehouseCapacity: null,
            granaryCapacity: null);

        Assert.False(plan.IsComplete);
        Assert.Null(plan.CandidateToInspect);
    }

    [Fact]
    public void BuildBulkUpgradePlan_ReportsQueuedFieldAlreadyReachingTarget()
    {
        var plan = ResourceSnapshotCalculator.BuildBulkUpgradePlan(
            new[] { Field(7, "wood", 1) },
            targetLevel: 2,
            fallbackMax: 40,
            queuedLevelsBySlot: new Dictionary<int, int> { [7] = 2 },
            EmptyResources(),
            CompleteProduction(),
            warehouseCapacity: 100_000,
            granaryCapacity: 100_000);

        Assert.True(plan.IsComplete);
        Assert.True(plan.AnyQueuedTowardTarget);
        Assert.Null(plan.CandidateToInspect);
        Assert.Empty(plan.BlockedByResources);
    }

    private static Dictionary<string, string> ResourcesFor(BuildingLevelStats cost) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["wood"] = cost.Wood.ToString(),
        ["clay"] = cost.Clay.ToString(),
        ["iron"] = cost.Iron.ToString(),
        ["crop"] = cost.Crop.ToString(),
    };

    private static Dictionary<string, string> EmptyResources() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["wood"] = "0",
        ["clay"] = "0",
        ["iron"] = "0",
        ["crop"] = "0",
    };

    private static Dictionary<string, double?> CompleteProduction(double value = 1_000) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["wood"] = value,
        ["clay"] = value,
        ["iron"] = value,
        ["crop"] = value,
    };

    private static ResourceField Field(int? slotId, string type, int? level)
        => new(slotId, type, type, level, null);
}
