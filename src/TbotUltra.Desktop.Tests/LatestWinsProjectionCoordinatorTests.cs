using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class LatestWinsProjectionCoordinatorTests
{
    [Fact]
    public async Task RequestAsync_DiscardsAnOlderProjectionThatFinishesLast()
    {
        using var coordinator = new LatestWinsProjectionCoordinator<string>();
        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var applied = new List<string>();

        var firstRequest = coordinator.RequestAsync(_ => first.Task, applied.Add);
        var secondRequest = coordinator.RequestAsync(_ => second.Task, applied.Add);
        second.SetResult("new");
        await secondRequest;
        first.SetResult("old");
        await firstRequest;

        Assert.Equal(["new"], applied);
    }
}
