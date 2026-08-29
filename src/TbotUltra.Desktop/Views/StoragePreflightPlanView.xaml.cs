using System.Windows.Controls;
using TbotUltra.Desktop.Services;

namespace TbotUltra.Desktop.Views;

public partial class StoragePreflightPlanView : UserControl
{
    public StoragePreflightPlanView(
        string description,
        IReadOnlyList<StoragePreflightPlanStage> stages)
    {
        InitializeComponent();
        Description = description;
        Stages = stages;
        DataContext = this;
    }

    public string Description { get; }

    public IReadOnlyList<StoragePreflightPlanStage> Stages { get; }

    public static IReadOnlyList<StoragePreflightPlanStage> CreateStages(
        IReadOnlyList<StoragePreflightUpgrade> upgrades)
        => upgrades
            .GroupBy(
                upgrade => upgrade.RequiredBy ?? "the next blocked construction",
                StringComparer.OrdinalIgnoreCase)
            .Select((group, index) => CreateStage(
                $"STEP {index + 1}",
                $"Before {group.Key}",
                group.ToList()))
            .ToList();

    public static StoragePreflightPlanStage CreateStage(
        string badge,
        string heading,
        IReadOnlyList<StoragePreflightUpgrade> upgrades)
    {
        var actions = new List<StoragePreflightPlanAction>();
        foreach (var upgrade in upgrades)
        {
            var capacityBefore = upgrade.ProjectedCapacity;
            if (upgrade.RequiresConstruction)
            {
                var capacityAfterConstruction = capacityBefore
                    + StorageCapacityDependencyPlanner.CapacityAtLevel(1)
                    - StorageCapacityDependencyPlanner.CapacityAtLevel(0);
                actions.Add(new StoragePreflightPlanAction(
                    "Construct",
                    "CONSTRUCT",
                    upgrade.Kind.ToString(),
                    $"Free building slot {upgrade.SlotId} · Level 1",
                    FormatCapacityChange(capacityBefore, capacityAfterConstruction)));
                capacityBefore = capacityAfterConstruction;

                if (upgrade.TargetLevel <= 1)
                {
                    continue;
                }
            }

            var currentLevel = Math.Max(1, upgrade.CurrentLevel);
            var capacityAfterUpgrade = capacityBefore
                + StorageCapacityDependencyPlanner.CapacityAtLevel(upgrade.TargetLevel)
                - StorageCapacityDependencyPlanner.CapacityAtLevel(currentLevel);
            actions.Add(new StoragePreflightPlanAction(
                "Upgrade",
                "UPGRADE",
                upgrade.Kind.ToString(),
                $"Level {currentLevel} → {upgrade.TargetLevel} · Slot {upgrade.SlotId}",
                FormatCapacityChange(capacityBefore, capacityAfterUpgrade)));
        }

        var requirements = upgrades
            .Select(upgrade => $"{upgrade.Kind} needs {upgrade.RequiredCapacity:N0}")
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return new StoragePreflightPlanStage(
            badge,
            heading,
            $"Required capacity: {string.Join("  ·  ", requirements)}",
            actions);
    }

    private static string FormatCapacityChange(long before, long after) =>
        $"{before:N0} → {after:N0}";
}

public sealed record StoragePreflightPlanStage(
    string Badge,
    string Heading,
    string Requirement,
    IReadOnlyList<StoragePreflightPlanAction> Actions);

public sealed record StoragePreflightPlanAction(
    string Kind,
    string Label,
    string Building,
    string Details,
    string Capacity);
