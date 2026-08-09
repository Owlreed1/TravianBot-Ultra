using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>Owns Buildings panel queue access behind the stable Desktop facade.</summary>
public sealed class BuildingsPanelService(IDesktopBotService botService)
{
    public IReadOnlyList<QueueItem> GetQueueItems() => botService.GetQueueItemsForDisplay();

    public IReadOnlyList<QueueItem> EnqueueBatch(IReadOnlyList<QueueItemCreateRequest> requests)
        => botService.EnqueueBatch(requests);

    public QueueItem Enqueue(string taskName, Dictionary<string, string> payload, int priority = 0, int maxRetries = 3)
        => botService.Enqueue(taskName, payload, priority, maxRetries);

    public bool Remove(Guid id) => botService.RemoveQueueItem(id);

    public bool UpdatePending(Guid id, Dictionary<string, string> payload)
        => botService.UpdatePendingQueueItem(id, payload, priority: null);

    public bool ApplyPendingReconciliation(IReadOnlyList<Guid> removals, IReadOnlyList<QueuePayloadUpdate> updates)
        => botService.ApplyPendingQueueReconciliation(removals, updates);
}
