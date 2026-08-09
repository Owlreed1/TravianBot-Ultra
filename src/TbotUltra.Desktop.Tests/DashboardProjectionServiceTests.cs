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
        var preview = new QueueItem { TaskName = "send_farmlists" };

        var result = new DashboardProjectionService().ProjectNextTask(new DashboardNextTaskRequest(
            IsLoggedIn: true,
            QueueItems: [running],
            EligibleDeferredItems: [],
            PreviewNextTask: preview,
            NowUtc: DateTimeOffset.UtcNow));

        Assert.Equal(DashboardNextTaskState.Running, result.State);
        Assert.Same(running, result.QueueItem);
    }

    [Fact]
    public void ProjectNextTask_UsesEarliestEligibleDeferredItem()
    {
        var now = DateTimeOffset.UtcNow;
        var later = new QueueItem { TaskName = "later", NextAttemptAt = now.AddMinutes(2) };
        var sooner = new QueueItem { TaskName = "sooner", NextAttemptAt = now.AddSeconds(30) };

        var result = new DashboardProjectionService().ProjectNextTask(new DashboardNextTaskRequest(
            IsLoggedIn: true,
            QueueItems: [later, sooner],
            EligibleDeferredItems: [later, sooner],
            PreviewNextTask: null,
            NowUtc: now));

        Assert.Equal(DashboardNextTaskState.Waiting, result.State);
        Assert.Same(sooner, result.QueueItem);
        Assert.Equal(TimeSpan.FromSeconds(30), result.Remaining);
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
