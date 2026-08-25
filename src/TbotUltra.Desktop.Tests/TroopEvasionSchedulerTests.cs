using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TroopEvasionSchedulerTests
{
    [Fact]
    public void SelectMostUrgent_UsesLeadThenFutureRetryMilestones()
    {
        var now = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var attack = new IncomingAttack("a", "A", now.AddMinutes(5), TargetVillageKey: "xy:1|2");
        var settings = new Dictionary<string, TroopEvasionVillageSettings>
        {
            ["xy:1|2"] = new("xy:1|2", "A", null, true, 3, 4, SelectedTroopSlots: [1]),
        };
        var completed = new HashSet<string>();

        var lead = TroopEvasionScheduler.SelectMostUrgent([("xy:1|2", attack)], settings, new Dictionary<string, TroopEvasionProtectionState>(), completed, now, 5, true, true);
        Assert.Equal("lead", lead?.Milestone);
        completed.Add(TroopEvasionScheduler.MilestoneKey("xy:1|2", attack, "lead"));
        Assert.Null(TroopEvasionScheduler.SelectMostUrgent([("xy:1|2", attack)], settings, new Dictionary<string, TroopEvasionProtectionState>(), completed, now, 5, true, true));
        Assert.Equal("retry-1m", TroopEvasionScheduler.SelectMostUrgent([("xy:1|2", attack)], settings, new Dictionary<string, TroopEvasionProtectionState>(), completed, now.AddMinutes(4), 5, true, true)?.Milestone);
    }

    [Fact]
    public void SelectMostUrgent_LateDetectionRunsOnlyLatestDueMilestone()
    {
        var now = DateTimeOffset.UtcNow;
        var attack = new IncomingAttack("a", "A", now.AddSeconds(45), TargetVillageKey: "xy:1|2");
        var settings = new Dictionary<string, TroopEvasionVillageSettings>
        {
            ["xy:1|2"] = new("xy:1|2", "A", null, true, 3, 4, SelectedTroopSlots: [1]),
        };
        var due = TroopEvasionScheduler.SelectMostUrgent([("xy:1|2", attack)], settings, new Dictionary<string, TroopEvasionProtectionState>(), new HashSet<string>(), now, 5, true, true);
        Assert.Equal("retry-1m", due?.Milestone);
    }

    [Fact]
    public void ProtectionSuppressesAttacksInsideWindowButNotLaterAttack()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new IncomingAttack("a", "A", now.AddMinutes(5), TargetVillageKey: "xy:1|2");
        var later = new IncomingAttack("b", "A", now.AddMinutes(11), TargetVillageKey: "xy:1|2");
        var settings = new Dictionary<string, TroopEvasionVillageSettings>
        {
            ["xy:1|2"] = new("xy:1|2", "A", null, true, 3, 4, SelectedTroopSlots: [1]),
        };
        var protection = TroopEvasionScheduler.CreateProtection("xy:1|2", first.ArrivalAtUtc, now, 5);
        var due = TroopEvasionScheduler.SelectMostUrgent(
            [("xy:1|2", first), ("xy:1|2", later)], settings,
            new Dictionary<string, TroopEvasionProtectionState> { ["xy:1|2"] = protection },
            new HashSet<string>(), now.AddMinutes(6), 5, true, true);
        Assert.Equal("b", due?.Attack.Id);
    }

    [Fact]
    public void MovementFilters_SelectOnlyEnabledIncomingType()
    {
        var now = DateTimeOffset.UtcNow;
        var raid = new IncomingAttack("raid", "A", now.AddMinutes(5), IncomingAttackMovementType.Raid, "xy:1|2");
        var attack = new IncomingAttack("attack", "A", now.AddMinutes(5), IncomingAttackMovementType.Attack, "xy:1|2");
        var settings = new Dictionary<string, TroopEvasionVillageSettings>
        {
            ["xy:1|2"] = new("xy:1|2", "A", null, true, 3, 4, SelectedTroopSlots: [1]),
        };

        var attackOnly = TroopEvasionScheduler.SelectMostUrgent(
            [("xy:1|2", raid), ("xy:1|2", attack)], settings, new Dictionary<string, TroopEvasionProtectionState>(),
            new HashSet<string>(), now, 5, evadeRaids: false, evadeAttacks: true);
        var raidOnly = TroopEvasionScheduler.SelectMostUrgent(
            [("xy:1|2", raid), ("xy:1|2", attack)], settings, new Dictionary<string, TroopEvasionProtectionState>(),
            new HashSet<string>(), now, 5, evadeRaids: true, evadeAttacks: false);

        Assert.Equal("attack", attackOnly?.Attack.Id);
        Assert.Equal("raid", raidOnly?.Attack.Id);

        var unknown = new IncomingAttack("unknown", "A", now.AddMinutes(5), IncomingAttackMovementType.Unknown, "xy:1|2");
        Assert.Null(TroopEvasionScheduler.SelectMostUrgent(
            [("xy:1|2", unknown)], settings, new Dictionary<string, TroopEvasionProtectionState>(),
            new HashSet<string>(), now, 5, evadeRaids: false, evadeAttacks: true));
        Assert.Equal("unknown", TroopEvasionScheduler.SelectMostUrgent(
            [("xy:1|2", unknown)], settings, new Dictionary<string, TroopEvasionProtectionState>(),
            new HashSet<string>(), now, 5, evadeRaids: true, evadeAttacks: true)?.Attack.Id);
    }
}
