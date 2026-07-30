using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private readonly object _demolishOperationSync = new();
    private readonly HashSet<Guid> _stoppedDemolishItemIds = [];
    private CancellationTokenSource? _activeDemolishOperationCts;
    private Guid? _activeDemolishOperationItemId;

    private static bool IsDemolishQueueItem(QueueItem item) =>
        string.Equals(item.TaskName, "demolish_building_to_level", StringComparison.OrdinalIgnoreCase);

    private bool IsDemolishQueueItemForSelectedVillage(QueueItem item)
    {
        var selectedVillageKey = GetSelectedVillageKey();
        return !string.IsNullOrWhiteSpace(selectedVillageKey)
            && string.Equals(GetQueueItemVillageKey(item), selectedVillageKey, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshDemolishStatusForSelectedVillage()
    {
        var item = _botService.GetQueueItemsForDisplay()
            .Where(IsDemolishQueueItem)
            .Where(IsDemolishQueueItemForSelectedVillage)
            .Where(candidate => candidate.Status is QueueStatus.Pending or QueueStatus.Running)
            .OrderBy(candidate => candidate.CreatedAt)
            .FirstOrDefault();

        _buildingsViewModel.DemolishStatusText = GetDemolishStatusText(item, DateTimeOffset.UtcNow, false);
    }

    private static string GetDemolishStatusText(QueueItem? item, DateTimeOffset now, bool overview)
    {
        if (item is null)
        {
            return overview ? "No active demolition" : "No demolition queued for this village.";
        }

        var target = item.Payload.GetValueOrDefault(BotOptionPayloadKeys.DemolishTargetName)
            ?? item.DisplayName;
        if (string.IsNullOrWhiteSpace(target))
        {
            target = "building";
        }

        if (item.Status == QueueStatus.Running)
        {
            return $"Starting demolition of {target}…";
        }

        if (TryGetDemolishServerFinishAt(item, out var serverFinishAt) && serverFinishAt > now)
        {
            return $"Demolishing {target} — {FormatCountdown((int)Math.Ceiling((serverFinishAt - now).TotalSeconds))} remaining.";
        }

        if (item.NextAttemptAt > now)
        {
            return $"Next demolition for {target} after delay — {FormatCountdown((int)Math.Ceiling((item.NextAttemptAt - now).TotalSeconds))}.";
        }

        return $"Demolition of {target} is ready to start.";
    }

    private static bool TryGetDemolishServerFinishAt(QueueItem item, out DateTimeOffset finishAt)
    {
        finishAt = default;
        if (!item.Payload.TryGetValue(BotOptionPayloadKeys.DemolishServerFinishAtUnixSeconds, out var value)
            || !long.TryParse(value, out var unixSeconds)
            || unixSeconds <= 0)
        {
            return false;
        }

        try
        {
            finishAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    internal void OnDemolishOverviewClicked()
    {
        var now = DateTimeOffset.UtcNow;
        var activeItemsByVillage = _botService.GetQueueItemsForDisplay()
            .Where(IsDemolishQueueItem)
            .Where(item => item.Status is QueueStatus.Pending or QueueStatus.Running)
            .Where(item => !string.IsNullOrWhiteSpace(GetQueueItemVillageKey(item)))
            .GroupBy(item => GetQueueItemVillageKey(item)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.CreatedAt).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var rows = GetAllVillageKeyInfos()
            .OrderBy(village => village.Name, StringComparer.OrdinalIgnoreCase)
            .Select(village =>
            {
                activeItemsByVillage.TryGetValue(village.Key, out var items);
                var status = GetDemolishStatusText(items?.FirstOrDefault(), now, true);
                if (items?.Count > 1)
                {
                    status += $" (+{items.Count - 1} queued)";
                }

                return new DemolishOverviewRow(village.Name, status);
            })
            .ToList();

        var overview = new DemolishOverviewWindow(rows) { Owner = this };
        overview.ShowDialog();
    }

    internal void OnStopDemolitionClicked()
    {
        var items = _botService.GetQueueItemsForDisplay()
            .Where(IsDemolishQueueItem)
            .Where(IsDemolishQueueItemForSelectedVillage)
            .Where(item => item.Status is QueueStatus.Pending or QueueStatus.Running)
            .ToList();
        if (items.Count == 0)
        {
            RefreshDemolishStatusForSelectedVillage();
            return;
        }

        if (MessageBox.Show(this,
                $"Stop and remove {items.Count} demolish task(s) for this village? A demolition already accepted by Travian will finish normally.",
                "Stop demolition", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        var removed = 0;
        foreach (var item in items)
        {
            RequestDemolishOperationStop(item.Id);
            if (item.Status == QueueStatus.Running)
            {
                _botService.MarkQueueItemCanceled(item.Id);
            }
            if (_botService.RemoveQueueItem(item.Id))
            {
                removed++;
            }
        }

        _buildingDemolishingSlots.Clear();
        RefreshDemolishStatusForSelectedVillage();
        RequestQueueUiRefresh();
        AppendLog($"[demolish] stopped {removed} queued task(s) for the selected village.");
    }

    private CancellationToken BeginDemolishOperation(QueueItem item, CancellationToken parentToken)
    {
        lock (_demolishOperationSync)
        {
            if (_stoppedDemolishItemIds.Contains(item.Id))
            {
                return new CancellationToken(canceled: true);
            }

            _activeDemolishOperationCts?.Dispose();
            _activeDemolishOperationCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            _activeDemolishOperationItemId = item.Id;
            return _activeDemolishOperationCts.Token;
        }
    }

    private void CompleteDemolishOperation(Guid itemId)
    {
        lock (_demolishOperationSync)
        {
            if (_activeDemolishOperationItemId != itemId)
            {
                _stoppedDemolishItemIds.Remove(itemId);
                return;
            }

            _activeDemolishOperationCts?.Dispose();
            _activeDemolishOperationCts = null;
            _activeDemolishOperationItemId = null;
            _stoppedDemolishItemIds.Remove(itemId);
        }
    }

    private void RequestDemolishOperationStop(Guid itemId)
    {
        lock (_demolishOperationSync)
        {
            _stoppedDemolishItemIds.Add(itemId);
            if (_activeDemolishOperationItemId == itemId)
            {
                _activeDemolishOperationCts?.Cancel();
            }
        }
    }

    private bool WasDemolishOperationStopped(Guid itemId)
    {
        lock (_demolishOperationSync)
        {
            return _stoppedDemolishItemIds.Contains(itemId);
        }
    }
}
