using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class PriorityBrowserWorkCoordinatorTests
{
    [Fact]
    public void CreateFarmLists_RegistersPriorityBeforeWaitingForBrowserSession()
    {
        var source = ReadWorkerSource("BotTaskRunner.Farming.cs");
        var methodStart = source.IndexOf(
            "public async Task<FarmListCreateBatchResult> CreateFarmListsAsync(",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf("return result ??", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("_priorityBrowserWork.EnterPriorityRequest()", method, StringComparison.Ordinal);
        Assert.True(
            method.IndexOf("_priorityBrowserWork.EnterPriorityRequest()", StringComparison.Ordinal)
            < method.IndexOf("ExecuteWithClientAsync(", StringComparison.Ordinal));
    }

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

    private static string ReadWorkerSource(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "TbotUltra.Worker", "Services", fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
