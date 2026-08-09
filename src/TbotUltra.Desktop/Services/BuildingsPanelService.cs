using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>Owns Buildings panel queue access behind the stable Desktop facade.</summary>
public sealed class BuildingsPanelService(IBuildingsPanelClient client)
{
    public IReadOnlyList<QueueItem> GetQueueItems() => client.GetQueueItems();

    public IReadOnlyList<QueueItem> EnqueueBatch(IReadOnlyList<QueueItemCreateRequest> requests)
        => client.EnqueueBatch(requests);

    public QueueItem Enqueue(string taskName, Dictionary<string, string> payload, int priority = 0, int maxRetries = 3)
        => client.Enqueue(taskName, payload, priority, maxRetries);

    public bool Remove(Guid id) => client.Remove(id);

    public bool UpdatePending(Guid id, Dictionary<string, string> payload)
        => client.UpdatePending(id, payload);

    public bool ApplyPendingReconciliation(IReadOnlyList<Guid> removals, IReadOnlyList<QueuePayloadUpdate> updates)
        => client.ApplyPendingReconciliation(removals, updates);
}
