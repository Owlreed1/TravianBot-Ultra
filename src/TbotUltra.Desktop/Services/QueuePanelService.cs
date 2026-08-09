using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>Owns Queue panel reads and user-driven persisted queue transitions.</summary>
public sealed class QueuePanelService(IDesktopBotService botService)
{
    public IReadOnlyList<QueueItem> GetItems() => botService.GetQueueItemsForDisplay();

    public bool Remove(Guid id) => botService.RemoveQueueItem(id);

    public bool MoveUp(Guid id) => botService.MoveQueueItemUp(id);

    public bool MoveDown(Guid id) => botService.MoveQueueItemDown(id);

    public bool Pause(Guid id) => botService.PauseQueueItem(id);

    public bool Resume(Guid id) => botService.ResumeQueueItem(id);

    public bool Retry(Guid id) => botService.RetryQueueItem(id);

    public QueueItem Enqueue(string taskName, Dictionary<string, string> payload, int priority, int maxRetries)
        => botService.Enqueue(taskName, payload, priority, maxRetries);
}
