using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ContinuousLoopForecastPlannerTests
{
    [Fact]
    public void Resolve_PredictsHumanizedConstructionBeforeLaterStoredRetry()
    {
        var now = new DateTimeOffset(2026, 8, 11, 17, 15, 40, TimeSpan.Zero);
        var h02ReadyAt = now.AddMinutes(2).AddSeconds(40);
        var h03ReadyAt = now.AddMinutes(9).AddSeconds(28);
        var h02 = new QueueItem { TaskName = "construct_building", Status = QueueStatus.Pending };
        var h03 = new QueueItem { TaskName = "upgrade_building_to_level", Status = QueueStatus.Pending };

        var result = ContinuousLoopForecastPlanner.Resolve(
            [h02, h03],
            now,
            [h03ReadyAt, h02ReadyAt],
            evaluationTime => evaluationTime >= h02ReadyAt
                ? h02
                : evaluationTime >= h03ReadyAt
                    ? h03
                    : null);

        Assert.Equal(ContinuousLoopForecastState.Waiting, result.State);
        Assert.Same(h02, result.Item);
        Assert.Equal(h02ReadyAt, result.ReadyAtUtc);
    }

    [Fact]
    public void Resolve_UsesRefreshStateWhenNoKnownDeadlineCanProduceASelection()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new QueueItem { TaskName = "construct_building", Status = QueueStatus.Pending };

        var result = ContinuousLoopForecastPlanner.Resolve([item], now, [], _ => null);

        Assert.Equal(ContinuousLoopForecastState.WaitingForRefresh, result.State);
        Assert.Null(result.Item);
    }

    [Fact]
    public void Resolve_UsesRefreshStateWhenSelectedTaskReadinessIsUnknown()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new QueueItem { TaskName = "construct_building", Status = QueueStatus.Pending };

        var result = ContinuousLoopForecastPlanner.Resolve(
            [item],
            now,
            [],
            _ => item,
            _ => false);

        Assert.Equal(ContinuousLoopForecastState.WaitingForRefresh, result.State);
        Assert.Null(result.Item);
    }
}
