using System.Windows;
using TbotUltra.Core.Configuration;
using TbotUltra.Core.Tasks;
using TbotUltra.Desktop.Services;
using TbotUltra.Desktop.Views;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private bool TryPrepareConstructionStoragePreflight(
        IReadOnlyList<QueueItemCreateRequest> requestedItems,
        out IReadOnlyList<QueueItemCreateRequest> plannedRequests,
        out IReadOnlyList<StoragePreflightUpgrade> upgrades,
        bool confirmUpgrades = true)
    {
        plannedRequests = [];
        upgrades = [];
        var status = ResolveSelectedVillageBuildingStatus();
        if (status is null
            || status.Buildings.Count == 0
            || status.WarehouseCapacity is not > 0
            || status.GranaryCapacity is not > 0)
        {
            AppDialog.Show(
                this,
                "Refresh the selected village before adding this construction. Current buildings and storage capacity are required for the storage preflight.",
                "Storage capacity check",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        var normalizedRequests = requestedItems.Select(request =>
        {
            var payload = request.Payload is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(request.Payload, StringComparer.OrdinalIgnoreCase);
            ApplySelectedVillageToPayload(payload);
            return request with { Payload = payload };
        }).ToList();
        var villageName = GetSelectedVillageName() ?? status.ActiveVillage;
        var villageKey = GetSelectedVillageKey();
        var precedingItems = _botService.GetQueueItemsForDisplay()
            .Where(item => IsQueueItemForVillage(item, villageName, villageKey))
            .ToList();
        var storageUpgradeLevelsAhead = LoadBotOptions().ConstructionStorageUpgradeLevelsAhead;
        var result = StorageCapacityQueuePreflightPlanner.PlanConstructionRequestsStepwise(
            status,
            precedingItems,
            normalizedRequests,
            storageUpgradeLevelsAhead);
        if (!string.IsNullOrWhiteSpace(result.CannotPlanReason))
        {
            AppDialog.Show(
                this,
                $"The storage requirement could not be planned safely.\n\n{result.CannotPlanReason}\n\nRefresh the village and try again.",
                "Storage capacity check",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        upgrades = result.Upgrades;
        if (upgrades.Count > 0)
        {
            if (confirmUpgrades)
            {
                var stages = StoragePreflightPlanView.CreateStages(upgrades);
                var content = new StoragePreflightPlanView(
                    "The queue reaches one or more storage-capacity limits. Each card identifies the exact " +
                    "building or resource level that requires the actions shown beneath it." +
                    FormatStorageBufferSetting(storageUpgradeLevelsAhead),
                    stages);
                var choice = AppDialog.ShowCustomContent(
                    this,
                    content,
                    "Storage upgrades required",
                    [("Add required storage upgrades", MessageBoxResult.Yes), ("Cancel", MessageBoxResult.Cancel)],
                    MessageBoxImage.Warning,
                    MessageBoxResult.Yes,
                    MessageBoxResult.Cancel,
                    successResult: MessageBoxResult.Yes,
                    width: 600);
                if (choice != MessageBoxResult.Yes)
                {
                    return false;
                }
            }

            var planId = Guid.NewGuid().ToString();
            foreach (var request in result.Requests)
            {
                request.Payload![BotOptionPayloadKeys.StoragePreflightPlanId] = planId;
            }
        }

        plannedRequests = result.Requests;
        return true;
    }

    private bool TryPrepareUpgradeAllStoragePreflight(
        int targetLevel,
        Dictionary<string, string> parentPayload,
        out IReadOnlyList<QueueItemCreateRequest> plannedRequests,
        out IReadOnlyList<StoragePreflightUpgrade> upgrades,
        IReadOnlyList<QueueItemCreateRequest>? precedingTemplateRequests = null,
        bool confirmUpgrades = true)
    {
        plannedRequests = [];
        upgrades = [];
        var status = ResolveSelectedVillageBuildingStatus();
        if (status is null
            || status.ResourceFields.Count == 0
            || status.Buildings.Count == 0
            || status.WarehouseCapacity is not > 0
            || status.GranaryCapacity is not > 0)
        {
            AppDialog.Show(
                this,
                "Refresh the selected village before adding this queue item. Current resource fields, buildings, and storage capacity are required for the storage preflight.",
                "Storage capacity check",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        var villageName = GetSelectedVillageName() ?? status.ActiveVillage;
        var villageKey = GetSelectedVillageKey();
        var precedingItems = _botService.GetQueueItemsForDisplay()
            .Where(item => IsQueueItemForVillage(item, villageName, villageKey))
            .ToList();
        if (precedingTemplateRequests is not null)
        {
            precedingItems.AddRange(precedingTemplateRequests.Select(request => new QueueItem
            {
                TaskName = request.TaskName,
                Payload = request.Payload is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(request.Payload, StringComparer.OrdinalIgnoreCase),
                Status = QueueStatus.Pending,
            }));
        }
        var storageUpgradeLevelsAhead = LoadBotOptions().ConstructionStorageUpgradeLevelsAhead;
        var result = StorageCapacityQueuePreflightPlanner.PlanUpgradeAllResourcesStepwise(
            status,
            precedingItems,
            targetLevel,
            storageUpgradeLevelsAhead,
            ResourceUpgradeSelection.Parse(parentPayload.GetValueOrDefault(BotOptionPayloadKeys.ResourceUpgradeTypes)));
        if (!string.IsNullOrWhiteSpace(result.CannotPlanReason))
        {
            AppDialog.Show(
                this,
                $"The storage requirement could not be planned safely.\n\n{result.CannotPlanReason}\n\nRefresh the village so available building slots and storage capacity can be verified.",
                "Storage capacity check",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        upgrades = result.Upgrades;
        if (upgrades.Count > 0 && confirmUpgrades)
        {
            var stages = result.Stages
                .Where(stage => stage.StorageUpgradesBefore.Count > 0)
                .Select(stage => StoragePreflightPlanView.CreateStage(
                    $"LEVEL {stage.ResourceTargetLevel}",
                    $"Before all resource fields advance to level {stage.ResourceTargetLevel}",
                    stage.StorageUpgradesBefore))
                .ToList();
            var content = new StoragePreflightPlanView(
                "Resource fields are upgraded in stages. Each card shows the storage actions inserted " +
                "immediately before that resource level starts." +
                FormatStorageBufferSetting(storageUpgradeLevelsAhead),
                stages);
            var choice = AppDialog.ShowCustomContent(
                this,
                content,
                "Storage upgrades required",
                [("Add required storage upgrades", MessageBoxResult.Yes), ("Cancel", MessageBoxResult.Cancel)],
                MessageBoxImage.Warning,
                MessageBoxResult.Yes,
                MessageBoxResult.Cancel,
                successResult: MessageBoxResult.Yes,
                width: 600);
            if (choice != MessageBoxResult.Yes)
            {
                return false;
            }
        }

        var requests = new List<QueueItemCreateRequest>();
        var planId = Guid.NewGuid().ToString();
        foreach (var stage in result.Stages)
        {
            var batchId = stage.StorageUpgradesBefore.Count > 0 ? Guid.NewGuid().ToString() : null;
            foreach (var upgrade in stage.StorageUpgradesBefore)
            {
                var name = upgrade.Kind == StorageCapacityKind.Warehouse ? "Warehouse" : "Granary";
                if (upgrade.RequiresConstruction)
                {
                    var gid = upgrade.Kind == StorageCapacityKind.Warehouse ? 10 : 11;
                    var constructPayload = new BuildingConstructPayload(
                        upgrade.SlotId,
                        gid,
                        name,
                        upgrade.TargetLevel).ToDictionary();
                    constructPayload[BotOptionPayloadKeys.BuildingConstructAllowSlotFallback] = bool.TrueString;
                    ApplyStoragePreflightMetadata(constructPayload, planId, batchId!);
                    requests.Add(new QueueItemCreateRequest("construct_building", constructPayload, 0, 3));
                }

                if (!upgrade.RequiresConstruction)
                {
                    var payload = new BuildingUpgradePayload(upgrade.SlotId, upgrade.TargetLevel, name).ToDictionary();
                    ApplyStoragePreflightMetadata(payload, planId, batchId!);
                    requests.Add(new QueueItemCreateRequest("upgrade_building_to_level", payload, 0, 3));
                }
            }

            var resourcePayload = new Dictionary<string, string>(parentPayload, StringComparer.OrdinalIgnoreCase)
            {
                [BotOptionPayloadKeys.ResourceUpgradeTargetLevel] = stage.ResourceTargetLevel.ToString(),
                [BotOptionPayloadKeys.StoragePreflightPlanId] = planId,
            };
            if (batchId is not null)
            {
                resourcePayload[BotOptionPayloadKeys.StoragePreflightBatchId] = batchId;
            }
            requests.Add(new QueueItemCreateRequest("upgrade_all_resources_to_level", resourcePayload, 0, 3));
        }

        plannedRequests = requests;
        return true;
    }

    private bool ConfirmBuildingTemplateStoragePreflight(IReadOnlyList<StoragePreflightUpgrade> upgrades)
    {
        if (upgrades.Count == 0)
        {
            return true;
        }

        var storageUpgradeLevelsAhead = LoadBotOptions().ConstructionStorageUpgradeLevelsAhead;
        var content = new StoragePreflightPlanView(
            "This template reaches storage-capacity limits in resource fields and/or buildings. " +
            "All required Warehouse and Granary actions are shown together below." +
            FormatStorageBufferSetting(storageUpgradeLevelsAhead),
            StoragePreflightPlanView.CreateStages(upgrades));
        return AppDialog.ShowCustomContent(
            this,
            content,
            "Storage upgrades required",
            [("Add required storage upgrades", MessageBoxResult.Yes), ("Cancel", MessageBoxResult.Cancel)],
            MessageBoxImage.Warning,
            MessageBoxResult.Yes,
            MessageBoxResult.Cancel,
            successResult: MessageBoxResult.Yes,
            width: 600) == MessageBoxResult.Yes;
    }

    private static string FormatStorageBufferSetting(int storageUpgradeLevelsAhead) =>
        storageUpgradeLevelsAhead > ConstructionDefaults.StorageUpgradeLevelsAhead
            ? $" Construction setting: {storageUpgradeLevelsAhead} storage levels ahead."
            : string.Empty;

    private void ApplyStoragePreflightMetadata(
        Dictionary<string, string> payload,
        string planId,
        string batchId)
    {
        ApplySelectedVillageToPayload(payload);
        payload[BotOptionPayloadKeys.StoragePreflightPlanId] = planId;
        payload[BotOptionPayloadKeys.StoragePreflightBatchId] = batchId;
        payload[BotOptionPayloadKeys.AutoAddedBy] = BotOptionPayloadKeys.AutoAddedByStorageCapacityPreflight;
    }

    private void ApplyStoragePreflightPendingState(IReadOnlyList<StoragePreflightUpgrade> upgrades)
    {
        foreach (var upgrade in upgrades)
        {
            if (upgrade.RequiresConstruction)
            {
                var gid = upgrade.Kind == StorageCapacityKind.Warehouse ? 10 : 11;
                SetPendingBuildingConstruct(upgrade.SlotId, upgrade.Kind.ToString(), gid);
            }

            if (upgrade.TargetLevel > 1 || !upgrade.RequiresConstruction)
            {
                SetPendingBuildingUpgrade(upgrade.SlotId, upgrade.TargetLevel);
            }
        }
    }

    private static bool HasEarlierStoragePreflightDependency(
        IReadOnlyList<QueueItem> orderedItems,
        int itemIndex)
    {
        var item = orderedItems[itemIndex];
        if (item.Payload.TryGetValue(BotOptionPayloadKeys.StoragePreflightPlanId, out var planId)
            && !string.IsNullOrWhiteSpace(planId)
            && orderedItems.Take(itemIndex).Any(candidate =>
                candidate.Payload.TryGetValue(BotOptionPayloadKeys.StoragePreflightPlanId, out var candidatePlanId)
                && string.Equals(candidatePlanId, planId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!item.Payload.TryGetValue(BotOptionPayloadKeys.StoragePreflightBatchId, out var batchId)
            || string.IsNullOrWhiteSpace(batchId))
        {
            return false;
        }

        return orderedItems.Take(itemIndex).Any(candidate =>
            candidate.Payload.TryGetValue(BotOptionPayloadKeys.StoragePreflightBatchId, out var candidateBatchId)
            && string.Equals(candidateBatchId, batchId, StringComparison.OrdinalIgnoreCase)
            && candidate.Payload.TryGetValue(BotOptionPayloadKeys.AutoAddedBy, out var source)
            && string.Equals(
                source,
                BotOptionPayloadKeys.AutoAddedByStorageCapacityPreflight,
                StringComparison.OrdinalIgnoreCase));
    }
}
