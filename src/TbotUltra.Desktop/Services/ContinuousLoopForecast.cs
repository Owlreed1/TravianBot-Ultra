using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

internal enum ContinuousLoopForecastState
{
    Running,
    Ready,
    Waiting,
    WaitingForRefresh,
    NothingQueued,
}

internal sealed record ContinuousLoopForecast(
    ContinuousLoopForecastState State,
    QueueItem? Item,
    DateTimeOffset? ReadyAtUtc = null);

internal static class ContinuousLoopForecastPlanner
{
    internal static ContinuousLoopForecast Resolve(
        IReadOnlyList<QueueItem> scopedItems,
        DateTimeOffset now,
        IEnumerable<DateTimeOffset> candidateDeadlines,
        Func<DateTimeOffset, QueueItem?> selectAt,
        Func<QueueItem, bool>? hasKnownReadiness = null)
    {
        var running = scopedItems.FirstOrDefault(item => item.Status == QueueStatus.Running);
        if (running is not null)
        {
            return new ContinuousLoopForecast(ContinuousLoopForecastState.Running, running);
        }

        var ready = selectAt(now);
        if (ready is not null)
        {
            if (hasKnownReadiness?.Invoke(ready) == false)
            {
                return new ContinuousLoopForecast(ContinuousLoopForecastState.WaitingForRefresh, null);
            }

            return new ContinuousLoopForecast(ContinuousLoopForecastState.Ready, ready, now);
        }

        if (!scopedItems.Any(item => item.Status == QueueStatus.Pending))
        {
            return new ContinuousLoopForecast(ContinuousLoopForecastState.NothingQueued, null);
        }

        foreach (var deadline in candidateDeadlines
                     .Where(value => value > now)
                     .Distinct()
                     .OrderBy(value => value))
        {
            var selected = selectAt(deadline);
            if (selected is not null)
            {
                if (hasKnownReadiness?.Invoke(selected) == false)
                {
                    return new ContinuousLoopForecast(ContinuousLoopForecastState.WaitingForRefresh, null);
                }

                return new ContinuousLoopForecast(
                    ContinuousLoopForecastState.Waiting,
                    selected,
                    deadline);
            }
        }

        return new ContinuousLoopForecast(ContinuousLoopForecastState.WaitingForRefresh, null);
    }
}
