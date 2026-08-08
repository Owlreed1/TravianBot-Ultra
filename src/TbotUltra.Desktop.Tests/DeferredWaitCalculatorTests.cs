using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class DeferredWaitCalculatorTests
{
    // ---- EvaluateDeferredTroopTrainingWait ----

    [Fact]
    public void TroopTraining_ready_when_checked_resources_meet_the_percent_threshold()
    {
        var requests = new[] { Barracks(minPercent: 50, checkWood: true) };
        var current = Res(wood: 600);

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, NoBuildingFilter, current, NoProduction, warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 30);

        Assert.Equal(new DeferredTroopTrainingEvaluation(true, 0, "ready"), result);
    }

    [Fact]
    public void TroopTraining_waits_when_any_checked_resource_is_below_the_percent_threshold()
    {
        var requests = new[] { Barracks(minPercent: 50, checkWood: true, checkClay: true, checkIron: true) };
        var current = Res(wood: 600, clay: 600, iron: 400);
        var production = Prod(iron: 100); // iron is 100 short of 50%, so wait one hour.

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, NoBuildingFilter, current, production, warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 30);

        Assert.Equal(new DeferredTroopTrainingEvaluation(false, 3600, "estimated_from_status"), result);
    }

    [Fact]
    public void TroopTraining_is_ready_when_all_checked_resources_meet_the_percent_threshold()
    {
        var requests = new[] { Barracks(minPercent: 50, checkWood: true, checkClay: true, checkIron: true, checkCrop: true) };
        var current = Res(wood: 500, clay: 500, iron: 500, crop: 500);

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, NoBuildingFilter, current, NoProduction, warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 30);

        Assert.Equal(new DeferredTroopTrainingEvaluation(true, 0, "ready"), result);
    }

    [Fact]
    public void TroopTraining_checks_all_resources_when_no_resource_checkbox_is_selected()
    {
        var requests = new[] { Barracks(minPercent: 50, checkWood: false) };
        var current = Res(wood: 600, clay: 600, iron: 600, crop: 400);
        var production = Prod(crop: 100); // no selection falls back to all resources; crop is 100 short.

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, NoBuildingFilter, current, production, warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 30);

        Assert.Equal(new DeferredTroopTrainingEvaluation(false, 3600, "estimated_from_status"), result);
    }

    [Fact]
    public void TroopTraining_estimates_wait_from_production_when_short()
    {
        var requests = new[] { Barracks(minPercent: 50, checkWood: true) };
        var current = Res(wood: 100); // threshold 500, missing 400
        var production = Prod(wood: 200); // 400/200h = 2h = 7200s

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, NoBuildingFilter, current, production, warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 30);

        Assert.Equal(new DeferredTroopTrainingEvaluation(false, 7200, "estimated_from_status"), result);
    }

    [Fact]
    public void TroopTraining_falls_back_to_cooldown_when_production_is_unknown()
    {
        var requests = new[] { Barracks(minPercent: 50, checkWood: true) };
        var current = Res(wood: 100); // short, but no production data

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, NoBuildingFilter, current, NoProduction, warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 45);

        Assert.Equal(new DeferredTroopTrainingEvaluation(false, 45, "recheck_needed"), result);
    }

    [Fact]
    public void TroopTraining_skips_refresh_when_no_resource_percent_request_is_active()
    {
        // Enabled but timed mode → no % gate to resume on.
        var requests = new[] { Barracks(minPercent: 50, checkWood: true) with { RunMode = "timed" } };

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, NoBuildingFilter, Res(wood: 0), NoProduction, warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 30);

        Assert.Equal(new DeferredTroopTrainingEvaluation(false, 30, "skip_refresh"), result);
    }

    [Fact]
    public void TroopTraining_ignores_requests_whose_building_is_not_present()
    {
        var requests = new[] { Barracks(minPercent: 50, checkWood: true) };
        var onlyStable = new List<Building> { new(null, "Stable", null, null, 20) };

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, onlyStable, Res(wood: 0), NoProduction, warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 30);

        Assert.Equal("skip_refresh", result.WaitReason);
    }

    [Fact]
    public void TroopTraining_takes_the_shortest_wait_across_requests()
    {
        var requests = new[]
        {
            Barracks(minPercent: 50, checkWood: true),                          // missing 400, prod 200 → 7200s
            Barracks(minPercent: 50, checkWood: true) with { BuildingName = "Stable" }, // missing 400, prod 400 → 3600s
        };

        var result = DeferredWaitCalculator.EvaluateDeferredTroopTrainingWait(
            requests, NoBuildingFilter, Res(wood: 100), new Dictionary<string, double?> { ["wood"] = 400 },
            warehouseCapacity: 1000, granaryCapacity: 1000, fallbackCooldownSeconds: 30);

        // Both requests key on wood with prod 400 → 3600s each; shortest is 3600.
        Assert.Equal(3600, result.WaitSeconds);
        Assert.Equal("estimated_from_status", result.WaitReason);
    }

    // ---- EvaluateDeferredUpgradeWait ----

    [Fact]
    public void Upgrade_ready_when_current_meets_required()
    {
        var result = DeferredWaitCalculator.EvaluateDeferredUpgradeWait(
            EmptyPayload, required: Res(wood: 100, clay: 100), currentResources: Res(wood: 100, clay: 100), liveProductionByHour: NoProduction);

        Assert.Equal(new DeferredUpgradeEvaluation(true, 0, "resources_ready"), result);
    }

    [Fact]
    public void Upgrade_estimates_wait_from_live_production()
    {
        var result = DeferredWaitCalculator.EvaluateDeferredUpgradeWait(
            EmptyPayload, required: Res(wood: 1000), currentResources: Res(wood: 0), liveProductionByHour: Prod(wood: 1000)); // 3600s

        Assert.Equal(new DeferredUpgradeEvaluation(false, 3600, "estimated_from_status"), result);
    }

    [Fact]
    public void Upgrade_clamps_wait_to_60s_when_any_resource_wait_is_unknown()
    {
        // wood is short with no production anywhere → unknown wait; clamps to [30,60].
        var result = DeferredWaitCalculator.EvaluateDeferredUpgradeWait(
            EmptyPayload, required: Res(wood: 1000), currentResources: Res(wood: 0), liveProductionByHour: NoProduction);

        Assert.Equal(new DeferredUpgradeEvaluation(false, 60, "recheck_needed"), result);
    }

    [Fact]
    public void Upgrade_unknown_wait_caps_a_longer_finite_estimate()
    {
        // clay finite (short wait), wood unknown → reason recheck_needed and wait capped into [30,60].
        var production = new Dictionary<string, double?> { ["clay"] = 100000 }; // clay 1000/100000h ≈ 36s
        var result = DeferredWaitCalculator.EvaluateDeferredUpgradeWait(
            EmptyPayload, required: Res(wood: 1000, clay: 1000), currentResources: Res(wood: 0, clay: 0), liveProductionByHour: production);

        Assert.False(result.ResourcesEnough);
        Assert.Equal("recheck_needed", result.WaitReason);
        Assert.InRange(result.WaitSeconds, 30, 60);
    }

    // ---- IsVillageResourcesFull ----

    [Fact]
    public void ResourcesFull_is_false_when_capacities_are_unknown()
    {
        var status = Status(warehouse: null, granary: null);
        Assert.False(DeferredWaitCalculator.IsVillageResourcesFull(status, Res(wood: 1000, clay: 1000, iron: 1000, crop: 1000)));
    }

    [Fact]
    public void ResourcesFull_is_true_when_all_stores_are_within_one_percent_of_cap()
    {
        var status = Status(warehouse: 1000, granary: 1000);
        var current = Res(wood: 1000, clay: 995, iron: 991, crop: 1000); // cap - max(1, cap/100) = 990
        Assert.True(DeferredWaitCalculator.IsVillageResourcesFull(status, current));
    }

    [Fact]
    public void ResourcesFull_is_false_when_one_store_is_below_the_threshold()
    {
        var status = Status(warehouse: 1000, granary: 1000);
        var current = Res(wood: 1000, clay: 1000, iron: 980, crop: 1000); // iron below 990
        Assert.False(DeferredWaitCalculator.IsVillageResourcesFull(status, current));
    }

    // ---- ResolveTroopTrainingFallbackCooldownSeconds ----

    [Theory]
    [InlineData(10, 10)]
    [InlineData(30, 30)]
    [InlineData(600, 600)]
    [InlineData(45, 30)]
    [InlineData(0, 30)]
    public void FallbackCooldown_passes_allowed_values_and_defaults_the_rest(int configured, int expected)
    {
        Assert.Equal(expected, DeferredWaitCalculator.ResolveTroopTrainingFallbackCooldownSeconds(configured));
    }

    // ---- TryParseDesktopResourceValue ----

    [Theory]
    [InlineData("1 234", 1234L)]
    [InlineData("1.234", 1234L)]
    [InlineData("1,234", 1234L)]
    [InlineData("1 234.567", 1234567L)]
    [InlineData("42", 42L)]
    public void ParseResource_strips_grouping_separators(string raw, long expected)
    {
        Assert.Equal(expected, DeferredWaitCalculator.TryParseDesktopResourceValue(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData(null)]
    public void ParseResource_returns_null_for_blank_or_non_numeric(string? raw)
    {
        Assert.Null(DeferredWaitCalculator.TryParseDesktopResourceValue(raw));
    }

    // ---- BuildDeferredTroopTrainingRequests ----

    [Fact]
    public void BuildRequests_maps_barracks_stable_and_workshop_from_options()
    {
        var options = new BotOptions
        {
            TroopTrainingBarracksEnabled = true,
            TroopTrainingBarracksRunMode = "resource_percent",
            TroopTrainingBarracksMinimumResourcesPercent = 40,
            TroopTrainingBarracksCheckWood = true,
        };

        var requests = DeferredWaitCalculator.BuildDeferredTroopTrainingRequests(options);

        Assert.Collection(
            requests,
            barracks =>
            {
                Assert.Equal("Barracks", barracks.BuildingName);
                Assert.True(barracks.Enabled);
                Assert.Equal("resource_percent", barracks.RunMode);
                Assert.Equal(40, barracks.MinimumResourcesPercent);
                Assert.True(barracks.CheckWood);
            },
            stable => Assert.Equal("Stable", stable.BuildingName),
            workshop => Assert.Equal("Workshop", workshop.BuildingName));
    }

    // ---- fixtures ----

    private static readonly IReadOnlyList<Building> NoBuildingFilter = System.Array.Empty<Building>();
    private static readonly IReadOnlyDictionary<string, double?> NoProduction = new Dictionary<string, double?>();
    private static readonly IReadOnlyDictionary<string, string> EmptyPayload = new Dictionary<string, string>();

    private static DeferredTroopTrainingRequest Barracks(
        int minPercent,
        bool checkWood,
        bool checkClay = false,
        bool checkIron = false,
        bool checkCrop = false)
        => new("Barracks", Enabled: true, RunMode: "resource_percent", MinimumResourcesPercent: minPercent,
            CheckWood: checkWood, CheckClay: checkClay, CheckIron: checkIron, CheckCrop: checkCrop);

    private static Dictionary<string, long> Res(long wood = 0, long clay = 0, long iron = 0, long crop = 0)
        => new(StringComparer.OrdinalIgnoreCase) { ["wood"] = wood, ["clay"] = clay, ["iron"] = iron, ["crop"] = crop };

    private static Dictionary<string, double?> Prod(double? wood = null, double? clay = null, double? iron = null, double? crop = null)
        => new(StringComparer.OrdinalIgnoreCase) { ["wood"] = wood, ["clay"] = clay, ["iron"] = iron, ["crop"] = crop };

    private static VillageStatus Status(long? warehouse, long? granary)
        => new(
            ActiveVillage: "v",
            Villages: System.Array.Empty<Village>(),
            Resources: new Dictionary<string, string>(),
            ResourceFields: System.Array.Empty<ResourceField>(),
            Buildings: System.Array.Empty<Building>(),
            BuildQueue: System.Array.Empty<BuildQueueItem>(),
            WarehouseCapacity: warehouse,
            GranaryCapacity: granary);
}
