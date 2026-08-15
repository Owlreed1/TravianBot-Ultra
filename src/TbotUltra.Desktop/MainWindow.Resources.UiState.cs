using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TbotUltra.Core.Configuration;
using TbotUltra.Core.Tasks;
using TbotUltra.Desktop.Models;
using TbotUltra.Worker;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private static bool TryReadResourceUpgradePayload(IReadOnlyDictionary<string, string> payload, out int slotId, out int targetLevel)
    {
        slotId = 0;
        targetLevel = 0;
        if (!ResourceUpgradePayload.TryFromDictionary(payload, out var parsed, ResourceFieldMaxLevel)
            || parsed is null)
        {
            return false;
        }

        slotId = parsed.SlotId;
        targetLevel = parsed.TargetLevel;
        return true;
    }

    private QueueItem? EnqueueResourceUpgradeTaskCoalesced(
        Dictionary<string, string> payload,
        int slotId,
        int requestedTargetLevel,
        out int effectiveTargetLevel,
        out bool enqueued,
        out int removedCount)
    {
        var relatedItems = _botService.GetQueueItemsForDisplay()
            .Where(item => string.Equals(item.TaskName, "upgrade_resource_to_level", StringComparison.OrdinalIgnoreCase))
            .Where(item => IsActiveQueueStatus(item.Status))
            // Same-village only: slot ids repeat across villages, so without this filter another
            // village's queued upgrade for the same slot both blocks this enqueue as a "duplicate"
            // and gets removed by the coalescing below. Mirrors the construction coalescing.
            .Where(IsQueueItemForSelectedVillageOrGlobal)
            .Select(item =>
            {
                var parsed = TryReadResourceUpgradePayload(item.Payload, out var parsedSlotId, out var parsedTargetLevel);
                return new
                {
                    Item = item,
                    Parsed = parsed,
                    SlotId = parsedSlotId,
                    TargetLevel = parsedTargetLevel,
                };
            })
            .Where(item => item.Parsed && item.SlotId == slotId)
            .ToList();

        var highestExistingTarget = relatedItems.Count == 0
            ? 0
            : relatedItems.Max(item => item.TargetLevel);
        effectiveTargetLevel = Math.Max(requestedTargetLevel, highestExistingTarget);

        if (highestExistingTarget >= requestedTargetLevel)
        {
            enqueued = false;
            removedCount = 0;
            return relatedItems
                .OrderByDescending(item => item.TargetLevel)
                .ThenBy(item => item.Item.CreatedAt)
                .Select(item => item.Item)
                .FirstOrDefault();
        }

        removedCount = RemoveCoalescedQueueItems(relatedItems.Select(item => item.Item));

        payload[BotOptionPayloadKeys.ResourceUpgradeTargetLevel] = effectiveTargetLevel.ToString();
        ApplySelectedVillageToPayload(payload);
        var created = _botService.Enqueue("upgrade_resource_to_level", payload, priority: 0, maxRetries: 3);
        enqueued = true;
        return created;
    }

    private List<ResourceFieldRow> BuildResourceRows(VillageStatus status, bool includeQueuedTargets)
    {
        var queuedTargetsBySlot = includeQueuedTargets
            ? GetQueuedResourceTargetsBySlot()
            : null;
        var resourceMaxLevel = ResolveResourceMaxLevelFromStatus(status);

        // Resource-field upgrades started outside the program (Travian's own build list) so they show
        // the target level in parentheses just like program-queued upgrades.
        var externalTargetsBySlot = BuildExternalUpgradeTargetsBySlot(
            status.ActiveConstructions,
            ConstructionKind.Resource,
            status.ResourceFields
                .Where(field => field.SlotId is not null)
                .Select(field => (field.SlotId!.Value, (string?)field.Name, field.Level)));

        int? ResolvePendingTarget(int slotId, int currentLevel)
        {
            var programTarget = includeQueuedTargets
                ? ResolveQueuedResourceTarget(slotId, currentLevel, queuedTargetsBySlot!)
                : null;
            if (programTarget is not null)
            {
                return programTarget;
            }

            return externalTargetsBySlot.TryGetValue(slotId, out var externalTarget) ? externalTarget : null;
        }

        return status.ResourceFields
            .Where(item => item.SlotId is not null)
            .OrderBy(item => item.SlotId)
            .Select(item => new ResourceFieldRow
            {
                SlotId = item.SlotId ?? 0,
                FieldType = item.FieldType,
                Name = item.Name,
                Level = item.Level,
                Url = item.Url ?? string.Empty,
                PendingTargetLevel = ResolvePendingTarget(item.SlotId ?? 0, item.Level ?? 0),
                IsMaxLevel = (item.Level ?? 0) >= resourceMaxLevel,
            })
            .ToList();
    }

    private List<ResourceFieldRow> ApplyResourceRowsAndVillageStatus(VillageStatus status, bool includeQueuedTargets)
    {
        if (!IsStatusForSelectedVillage(status))
        {
            AppendLog($"[resource-ui] skipped repaint: status is for '{status.ActiveVillage}', another village is selected.");
            return BuildResourceRows(status, includeQueuedTargets);
        }

        var rows = BuildResourceRows(status, includeQueuedTargets);
        SetResourceRows(rows);
        ApplyVillageStatusToUi(status);
        UpdateResourcesInfoText(status, rows.Count);
        return rows;
    }

    private void UpdateResourcesInfoText(VillageStatus status, int rowCount)
    {
        var capitalText = status.IsCapital == true ? "Yes" : status.IsCapital == false ? "No" : "Unknown";
        _resourcesViewModel.InfoText = $"Loaded {rowCount} resource fields. Capital: {capitalText}. {BuildResourceForecastSummary(status)}";
    }

    private void SetResourceRows(IReadOnlyList<ResourceFieldRow> rows)
    {
        _resourcesViewModel.SetAllFields(rows);
        UpdateCroplandLayout();
    }

    private IReadOnlyDictionary<int, int> GetQueuedResourceTargetsBySlot()
    {
        var targetsBySlot = new Dictionary<int, int>();
        IReadOnlyList<QueueItem> queueItems;
        try
        {
            queueItems = _queueItemsForUiProjection.Count > 0
                ? _queueItemsForUiProjection
                : _botService.GetQueueItemsForDisplay();
        }
        catch
        {
            return targetsBySlot;
        }

        foreach (var item in queueItems)
        {
            if (!string.Equals(item.TaskName, "upgrade_resource_to_level", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.Status is QueueStatus.Succeeded or QueueStatus.Failed)
            {
                continue;
            }

            // Only this village's queued upgrades may mark its resource slots (1-18) as pending. The queue
            // is one-per-account with each item tagged for its target village, so without this filter a
            // resource upgrade queued in another village shows as a pending target on the same slot here.
            if (!IsQueueItemForSelectedVillageOrGlobal(item))
            {
                continue;
            }

            if (!TryReadResourceUpgradePayload(item.Payload, out var slotId, out var targetLevel))
            {
                continue;
            }

            if (!targetsBySlot.TryGetValue(slotId, out var existing) || targetLevel > existing)
            {
                targetsBySlot[slotId] = targetLevel;
            }
        }

        return targetsBySlot;
    }

    private int? ResolveQueuedResourceTarget(int slotId, int currentLevel, IReadOnlyDictionary<int, int> queuedTargetsBySlot)
    {
        return _resourcesViewModel.ResolveQueuedResourceTarget(slotId, currentLevel, queuedTargetsBySlot);
    }

    private void SyncPendingResourceTargetsInUi()
    {
        var sourceRows = _resourcesViewModel.AllFields;
        if (sourceRows.Count == 0)
        {
            return;
        }

        var queuedTargetsBySlot = GetQueuedResourceTargetsBySlot();
        var changed = false;
        var updatedRows = sourceRows
            .Select(row =>
            {
                var currentLevel = row.Level ?? 0;
                var pendingTarget = ResolveQueuedResourceTarget(row.SlotId, currentLevel, queuedTargetsBySlot);
                if (row.PendingTargetLevel == pendingTarget)
                {
                    return row;
                }

                changed = true;
                return new ResourceFieldRow
                {
                    SlotId = row.SlotId,
                    FieldType = row.FieldType,
                    Name = row.Name,
                    Level = row.Level,
                    Url = row.Url,
                    PendingTargetLevel = pendingTarget,
                    IsMaxLevel = row.IsMaxLevel,
                };
            })
            .ToList();

        if (!changed)
        {
            return;
        }

        SetResourceRows(updatedRows);
    }

    private void ClearPendingResourceLevelsFromUi()
    {
        _resourcesViewModel.ClearPendingTargets();
        var sourceRows = _resourcesViewModel.AllFields;
        if (sourceRows.Count == 0)
        {
            return;
        }

        var updatedRows = sourceRows
            .Select(row => new ResourceFieldRow
            {
                SlotId = row.SlotId,
                FieldType = row.FieldType,
                Name = row.Name,
                Level = row.Level,
                Url = row.Url,
                PendingTargetLevel = null,
                IsMaxLevel = row.IsMaxLevel,
            })
            .ToList();

        SetResourceRows(updatedRows);
    }

    private void SetPendingResourceLevel(int slotId, int targetLevel)
    {
        var normalizedTarget = Math.Clamp(targetLevel, 1, _activeVillageResourceMaxLevel);
        if (_resourcesViewModel.TryGetPendingTarget(slotId, out var existingTarget) && existingTarget > normalizedTarget)
        {
            normalizedTarget = existingTarget;
        }

        _resourcesViewModel.RememberPendingTarget(slotId, normalizedTarget);

        var sourceRows = _resourcesViewModel.AllFields;
        if (sourceRows.Count == 0)
        {
            return;
        }

        var updated = sourceRows
            .Select(row => row.SlotId == slotId
                ? new ResourceFieldRow
                {
                    SlotId = row.SlotId,
                    FieldType = row.FieldType,
                    Name = row.Name,
                    Level = row.Level,
                    Url = row.Url,
                    PendingTargetLevel = normalizedTarget > (row.Level ?? 0) ? normalizedTarget : null,
                    IsMaxLevel = row.IsMaxLevel,
                }
                : row)
            .ToList();

        if (updated.FirstOrDefault(row => row.SlotId == slotId)?.PendingTargetLevel is null)
        {
            _resourcesViewModel.ForgetPendingTarget(slotId);
        }

        SetResourceRows(updated);
    }

    private void MarkResourceAsMax(int slotId)
    {
        _resourcesViewModel.ForgetPendingTarget(slotId);
        var sourceRows = _resourcesViewModel.AllFields;
        if (sourceRows.Count == 0)
        {
            return;
        }

        var updated = sourceRows
            .Select(row => row.SlotId == slotId
                ? new ResourceFieldRow
                {
                    SlotId = row.SlotId,
                    FieldType = row.FieldType,
                    Name = row.Name,
                    Level = row.Level,
                    Url = row.Url,
                    PendingTargetLevel = null,
                    IsMaxLevel = true,
                }
                : row)
            .ToList();
        SetResourceRows(updated);
    }

    private void UpdateCroplandLayout()
    {
        if (CroplandItemsControl is null)
        {
            return;
        }

        var isDenseCropland = _resourcesViewModel.UseDenseCroplandLayout;
        var columns = isDenseCropland ? 2 : 1;
        var factory = new FrameworkElementFactory(typeof(UniformGrid));
        factory.SetValue(UniformGrid.ColumnsProperty, columns);
        var template = new ItemsPanelTemplate(factory);
        template.Seal();
        CroplandItemsControl.ItemsPanel = template;

        if (CroplandColumnPanel is not null)
        {
            CroplandColumnPanel.Width = isDenseCropland ? 350 : 190;
        }
    }
    private void QueueResourceLevelBadgeUpgrade(ResourceFieldRow row)
    {
        var liveRow = _resourcesViewModel.AllFields
            .FirstOrDefault(item => item.SlotId == row.SlotId) ?? row;
        var currentLevel = liveRow.Level ?? 0;
        var rowName = string.IsNullOrWhiteSpace(liveRow.Name) ? row.Name : liveRow.Name;

        var now = DateTimeOffset.UtcNow;
        if (!_resourcesViewModel.TryBeginSlotClick(row.SlotId, now))
        {
            return;
        }

        if (liveRow.IsMaxLevel || currentLevel >= _activeVillageResourceMaxLevel)
        {
            MarkResourceAsMax(row.SlotId);
            AppDialog.Show(this, "Max level reached", "Resources", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pendingLevel = liveRow.PendingTargetLevel ?? currentLevel;
        var baseLevel = Math.Max(currentLevel, pendingLevel);
        var target = Math.Clamp(baseLevel + 1, 1, _activeVillageResourceMaxLevel);
        if (_resourcesViewModel.WasTargetQueuedRecently(row.SlotId, target, now))
        {
            return;
        }

        var payload = new ResourceUpgradePayload(row.SlotId, target, rowName).ToDictionary();
        ApplySelectedVillageToPayload(payload);
        if (!TryPrepareConstructionStoragePreflight(
                [new QueueItemCreateRequest("upgrade_resource_to_level", payload, 0, 3)],
                out var plannedRequests,
                out var storageUpgrades))
        {
            return;
        }

        var created = _botService.EnqueueBatch(plannedRequests);
        ApplyStoragePreflightPendingState(storageUpgrades);
        _resourcesViewModel.RememberQueuedTarget(row.SlotId, target, now);
        SetPendingResourceLevel(row.SlotId, target);
        RequestQueueUiRefresh(selectId: created.LastOrDefault()?.Id);
        TriggerQueueAutoRunFromEnqueue();
        _resourcesViewModel.InfoText = $"Queued {rowName} to level {target}.";
        AppendLog($"Queued single resource upgrade: slot {row.SlotId} -> level {target}, with {storageUpgrades.Count} storage prerequisite(s).");
    }

    private static bool IsResourceUpgradeTask(string taskName)
    {
        return string.Equals(taskName, "upgrade_resource_to_level", StringComparison.OrdinalIgnoreCase)
               || string.Equals(taskName, "upgrade_all_resources_to_level", StringComparison.OrdinalIgnoreCase);
    }
}
