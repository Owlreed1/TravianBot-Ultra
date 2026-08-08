using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class PriorityBrowserWorkCoordinatorTests
{
    [Fact]
    public void EnterPriorityRequest_ReportsPendingUntilDisposed()
    {
        var coordinator = new PriorityBrowserWorkCoordinator();

        Assert.False(coordinator.HasPendingRequest);

        using (coordinator.EnterPriorityRequest())
        {
            Assert.True(coordinator.HasPendingRequest);
        }

        Assert.False(coordinator.HasPendingRequest);
    }

    [Fact]
    public void EnterPriorityRequest_KeepsPendingWhileAnotherRequestRemains()
    {
        var coordinator = new PriorityBrowserWorkCoordinator();
        using var first = coordinator.EnterPriorityRequest();
        using var second = coordinator.EnterPriorityRequest();

        first.Dispose();

        Assert.True(coordinator.HasPendingRequest);

        second.Dispose();

        Assert.False(coordinator.HasPendingRequest);
    }
}
