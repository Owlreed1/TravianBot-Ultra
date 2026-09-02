using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

public static class QueueGroupCatalog
{
    private static readonly IReadOnlyDictionary<QueueGroup, (string Key, string Title, string Description)> Metadata =
        new Dictionary<QueueGroup, (string Key, string Title, string Description)>
        {
            [QueueGroup.Construction] = ("construction", "Construction", "Resources and buildings."),
            [QueueGroup.Troops] = ("troops", "Upgrade Troops", "Smithy and troop tasks."),
            [QueueGroup.Hero] = ("hero", "Hero", "Hero actions and adventures."),
            [QueueGroup.Farming] = ("farming", "Farming", "Selected farmlists."),
            [QueueGroup.TroopTraining] = ("troop_training", "Build Troops", "Barracks, Stable, and Workshop."),
            [QueueGroup.BreweryCelebration] = ("brewery_celebration", "Brewery Celebration", "Teutons brewery celebration."),
            [QueueGroup.NpcTrade] = ("npc_trade", "NPC Trade", "NPC resource exchange while building troops, buildings, or resource fields."),
            [QueueGroup.ResourceTransfer] = ("resource_transfer", "Resource Transfer", "Send resources between own villages."),
            [QueueGroup.Reinforcements] = ("reinforcements", "Reinforcements", "Send troops between own villages."),
            [QueueGroup.TownHallCelebration] = ("town_hall_celebration", "Town Hall celebration", "Small/big Town Hall celebrations."),
            [QueueGroup.Account] = ("account", "Account", "Account and read-only status tasks."),
            [QueueGroup.Demolish] = ("demolish", "Demolish", "Queued building demolitions."),
        };

    public static IReadOnlyList<QueueGroup> AllGroups => Metadata.Keys.ToList();

    public static QueueGroup ResolveGroup(string? taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return QueueGroup.Construction;
        }

        const string runtimeManualPrefix = "desktop_runtime_manual:";
        if (taskName.StartsWith(runtimeManualPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var runtimeTask = taskName[runtimeManualPrefix.Length..];
            if (runtimeTask.StartsWith("upgradeallresources", StringComparison.OrdinalIgnoreCase))
            {
                return QueueGroup.Construction;
            }

            if (runtimeTask.StartsWith("farm", StringComparison.OrdinalIgnoreCase)
                || runtimeTask.StartsWith("add_farms", StringComparison.OrdinalIgnoreCase)
                || runtimeTask.StartsWith("analyze_farmlists", StringComparison.OrdinalIgnoreCase)
                || runtimeTask.StartsWith("create_farmlists", StringComparison.OrdinalIgnoreCase)
                || runtimeTask.StartsWith("catapult_waves", StringComparison.OrdinalIgnoreCase))
            {
                return QueueGroup.Farming;
            }

            if (runtimeTask.StartsWith("hero", StringComparison.OrdinalIgnoreCase)
                || runtimeTask.StartsWith("refresh_hero", StringComparison.OrdinalIgnoreCase)
                || runtimeTask.StartsWith("refresh_adventures", StringComparison.OrdinalIgnoreCase))
            {
                return QueueGroup.Hero;
            }

            if (runtimeTask.StartsWith("refresh_reinforcement", StringComparison.OrdinalIgnoreCase))
            {
                return QueueGroup.Reinforcements;
            }

            if (runtimeTask.StartsWith("refresh_troop_queues", StringComparison.OrdinalIgnoreCase))
            {
                return QueueGroup.TroopTraining;
            }

            if (runtimeTask.StartsWith("check_celebration", StringComparison.OrdinalIgnoreCase))
            {
                return QueueGroup.BreweryCelebration;
            }

            if (runtimeTask.StartsWith("scan_resource_villages", StringComparison.OrdinalIgnoreCase))
            {
                return QueueGroup.ResourceTransfer;
            }

            // Manual runtime rows are account-level history unless their domain is explicitly known above.
            // This prevents new read-only/manual operations from silently appearing as Construction work.
            return QueueGroup.Account;
        }

        if (TbotUltra.Core.Tasks.TaskCatalog.TryGetDescriptor(taskName, out var descriptor))
        {
            return ToQueueGroup(descriptor.Group);
        }

        return QueueGroup.Construction;
    }

    private static QueueGroup ToQueueGroup(TaskGroup group)
    {
        return group switch
        {
            TaskGroup.Troops => QueueGroup.Troops,
            TaskGroup.Hero => QueueGroup.Hero,
            TaskGroup.Farming => QueueGroup.Farming,
            TaskGroup.TroopTraining => QueueGroup.TroopTraining,
            TaskGroup.BreweryCelebration => QueueGroup.BreweryCelebration,
            TaskGroup.NpcTrade => QueueGroup.NpcTrade,
            TaskGroup.ResourceTransfer => QueueGroup.ResourceTransfer,
            TaskGroup.Reinforcements => QueueGroup.Reinforcements,
            TaskGroup.TownHallCelebration => QueueGroup.TownHallCelebration,
            TaskGroup.Account => QueueGroup.Account,
            TaskGroup.Demolish => QueueGroup.Demolish,
            _ => QueueGroup.Construction,
        };
    }

    public static string GetKey(QueueGroup group) => Metadata[group].Key;

    public static string GetTitle(QueueGroup group) => Metadata[group].Title;

    public static string GetDescription(QueueGroup group) => Metadata[group].Description;

    public static bool TryParse(string? value, out QueueGroup group)
    {
        foreach (var pair in Metadata)
        {
            if (string.Equals(pair.Value.Key, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pair.Key.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                group = pair.Key;
                return true;
            }
        }

        group = QueueGroup.Construction;
        return false;
    }
}
