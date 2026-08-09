using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>
/// Produces the read-only state for the Dashboard's next-task indicator.
/// It never reads storage, navigates, or changes queue state.
/// </summary>
public sealed class DashboardProjectionService
{
    public DashboardNextTaskProjection ProjectNextTask(DashboardNextTaskRequest request)
    {
        if (!request.IsLoggedIn)
        {
            return new DashboardNextTaskProjection(DashboardNextTaskState.Idle, null, null);
        }

        var running = request.QueueItems.FirstOrDefault(item => item.Status == QueueStatus.Running);
        if (running is not null)
        {
            return new DashboardNextTaskProjection(DashboardNextTaskState.Running, running, null);
        }

        if (request.PreviewNextTask is not null)
        {
            return new DashboardNextTaskProjection(DashboardNextTaskState.Next, request.PreviewNextTask, null);
        }

        var waiting = request.EligibleDeferredItems
            .Where(item => item.NextAttemptAt > request.NowUtc)
            .OrderBy(item => item.NextAttemptAt)
            .FirstOrDefault();
        if (waiting is not null)
        {
            return new DashboardNextTaskProjection(
                DashboardNextTaskState.Waiting,
                waiting,
                waiting.NextAttemptAt - request.NowUtc);
        }

        return new DashboardNextTaskProjection(DashboardNextTaskState.NothingQueued, null, null);
    }
}

public sealed record DashboardNextTaskRequest(
    bool IsLoggedIn,
    IReadOnlyList<QueueItem> QueueItems,
    IReadOnlyList<QueueItem> EligibleDeferredItems,
    QueueItem? PreviewNextTask,
    DateTimeOffset NowUtc);

public sealed record DashboardNextTaskProjection(
    DashboardNextTaskState State,
    QueueItem? QueueItem,
    TimeSpan? Remaining);

public enum DashboardNextTaskState
{
    Idle,
    Running,
    Next,
    Waiting,
    NothingQueued,
}
