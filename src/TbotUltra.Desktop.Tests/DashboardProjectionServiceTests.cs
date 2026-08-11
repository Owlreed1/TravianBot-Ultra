using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;
using TbotUltra.Desktop.ViewModels;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class DashboardProjectionServiceTests
{
    [Fact]
    public void ProjectNextTask_PrefersRunningItemOverPreview()
    {
        var running = new QueueItem { TaskName = "build_troops", Status = QueueStatus.Running };
        var result = new DashboardProjectionService().ProjectNextTask(new DashboardNextTaskRequest(
            IsLoggedIn: true,
            Forecast: new ContinuousLoopForecast(ContinuousLoopForecastState.Running, running),
            NowUtc: DateTimeOffset.UtcNow));

        Assert.Equal(DashboardNextTaskState.Running, result.State);
        Assert.Same(running, result.QueueItem);
    }

    [Fact]
    public void ProjectNextTask_UsesForecastedEffectiveDeadline()
    {
        var now = DateTimeOffset.UtcNow;
        var h02 = new QueueItem { TaskName = "construct_building" };

        var result = new DashboardProjectionService().ProjectNextTask(new DashboardNextTaskRequest(
            IsLoggedIn: true,
            Forecast: new ContinuousLoopForecast(
                ContinuousLoopForecastState.Waiting,
                h02,
                now.AddMinutes(2).AddSeconds(40)),
            NowUtc: now));

        Assert.Equal(DashboardNextTaskState.Waiting, result.State);
        Assert.Same(h02, result.QueueItem);
        Assert.Equal(TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(40)), result.Remaining);
    }

    [Fact]
    public void ProjectNextTask_ReportsRefreshInsteadOfMisleadingFallback()
    {
        var result = new DashboardProjectionService().ProjectNextTask(new DashboardNextTaskRequest(
            IsLoggedIn: true,
            Forecast: new ContinuousLoopForecast(ContinuousLoopForecastState.WaitingForRefresh, null),
            NowUtc: DateTimeOffset.UtcNow));

        Assert.Equal(DashboardNextTaskState.WaitingForRefresh, result.State);
        Assert.Null(result.QueueItem);
    }

    [Fact]
    public void AutomationLoopViewModel_UpdatesVisibleOrdersAndReportsCountdownCompletion()
    {
        var viewModel = new AutomationLoopViewModel();
        viewModel.Tasks.Add(new LoopTaskOption { IsVisible = false, RemainingSeconds = 1 });
        viewModel.Tasks.Add(new LoopTaskOption { IsVisible = true, RemainingSeconds = 1 });
        viewModel.Tasks.Add(new LoopTaskOption { IsVisible = true });

        viewModel.UpdateVisibleOrders();

        Assert.Equal(1, viewModel.Tasks[1].Order);
        Assert.Equal(2, viewModel.Tasks[2].Order);
        Assert.True(viewModel.TickCountdowns());
        Assert.Equal(0, viewModel.Tasks[0].RemainingSeconds);
        Assert.Equal(0, viewModel.Tasks[1].RemainingSeconds);
    }
}
