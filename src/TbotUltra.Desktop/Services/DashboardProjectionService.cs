using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>
/// Produces the read-only state for the Dashboard's next-task indicator.
/// It never reads storage, navigates, or changes queue state.
/// </summary>
internal sealed class DashboardProjectionService
{
    public DashboardNextTaskProjection ProjectNextTask(DashboardNextTaskRequest request)
    {
        if (!request.IsLoggedIn)
        {
            return new DashboardNextTaskProjection(DashboardNextTaskState.Idle, null, null);
        }

        if (request.Forecast.State == ContinuousLoopForecastState.Running)
        {
            return new DashboardNextTaskProjection(
                DashboardNextTaskState.Running,
                request.Forecast.Item,
                null);
        }

        if (!string.IsNullOrWhiteSpace(request.ActiveOperationName))
        {
            return new DashboardNextTaskProjection(
                DashboardNextTaskState.RunningOperation,
                null,
                null,
                request.ActiveOperationName.Trim());
        }

        return request.Forecast.State switch
        {
            ContinuousLoopForecastState.Ready => new DashboardNextTaskProjection(
                DashboardNextTaskState.Next,
                request.Forecast.Item,
                null),
            ContinuousLoopForecastState.Waiting => new DashboardNextTaskProjection(
                DashboardNextTaskState.Waiting,
                request.Forecast.Item,
                request.Forecast.ReadyAtUtc - request.NowUtc),
            ContinuousLoopForecastState.WaitingForRefresh => new DashboardNextTaskProjection(
                DashboardNextTaskState.WaitingForRefresh,
                null,
                null),
            _ => new DashboardNextTaskProjection(DashboardNextTaskState.NothingQueued, null, null),
        };
    }
}

internal sealed record DashboardNextTaskRequest(
    bool IsLoggedIn,
    ContinuousLoopForecast Forecast,
    DateTimeOffset NowUtc,
    string? ActiveOperationName = null);

internal sealed record DashboardNextTaskProjection(
    DashboardNextTaskState State,
    QueueItem? QueueItem,
    TimeSpan? Remaining,
    string? OperationName = null);

internal enum DashboardNextTaskState
{
    Idle,
    Running,
    RunningOperation,
    Next,
    Waiting,
    WaitingForRefresh,
    NothingQueued,
}
