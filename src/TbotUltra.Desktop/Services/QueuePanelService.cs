using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>Owns Queue panel reads and user-driven persisted queue transitions.</summary>
public sealed class QueuePanelService(IQueuePanelClient client)
{
    public IReadOnlyList<QueueItem> GetItems() => client.GetItems();

    public bool Remove(Guid id) => client.Remove(id);

    public bool MoveUp(Guid id) => client.MoveUp(id);

    public bool MoveDown(Guid id) => client.MoveDown(id);

    public bool MoveToTop(Guid id) => client.MoveToTop(id);

    public bool MoveToBottom(Guid id) => client.MoveToBottom(id);

    public bool Pause(Guid id) => client.Pause(id);

    public bool Resume(Guid id) => client.Resume(id);

    public bool Retry(Guid id) => client.Retry(id);

    public QueueItem Enqueue(string taskName, Dictionary<string, string> payload, int priority, int maxRetries)
        => client.Enqueue(taskName, payload, priority, maxRetries);
}
