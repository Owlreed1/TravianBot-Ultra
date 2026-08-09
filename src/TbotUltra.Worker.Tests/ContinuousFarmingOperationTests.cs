using TbotUltra.Core.Configuration;
using TbotUltra.Core.Travian;
using TbotUltra.Worker.Configuration;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using TbotUltra.Worker.Services.Automation;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ContinuousFarmingOperationTests
{
    [Fact]
    public async Task ExecuteAsync_SelectedReadyLists_HandlesLossesThenSendsAndReturnsSnapshot()
    {
        var client = new FakeFarmingClient(
            [new FarmListOverview("Mercs", 3, 3, 0, "42")],
            [new FarmListOverview("Mercs", 3, 3, 90, "42")]);
        var operation = new ContinuousFarmingOperation(client);
        var logs = new List<string>();

        var result = await operation.ExecuteAsync(
            new ContinuousFarmingDispatchRequest(
                FarmingDefaults.SendModeListPerList,
                ["Mercs"],
                ["42"],
                600,
                true,
                new FarmListLossHandlingRequest(false, false, "", "", "")),
            logs.Add,
            CancellationToken.None);

        Assert.Equal(["read", "loss", "send-selected", "read"], client.Calls);
        Assert.True(result.ScheduleNextRound);
        Assert.Equal(600, result.WaitSeconds);
        Assert.Equal(TaskWaitReasons.WorkQueued, result.WaitReasonCode);
        Assert.Equal("Mercs", Assert.Single(result.Snapshot!).Name);
        Assert.NotNull(result.LossHandlingResult);
        Assert.Contains(logs, line => line.Contains("1 list(s) dispatched", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_SelectedListsMissing_DefersWithoutBrowserMutation()
    {
        var client = new FakeFarmingClient([new FarmListOverview("Different", 1, 1, 0, "7")]);
        var operation = new ContinuousFarmingOperation(client);

        var result = await operation.ExecuteAsync(
            new ContinuousFarmingDispatchRequest(
                FarmingDefaults.SendModeListPerList,
                ["Mercs"],
                [],
                600,
                false,
                null),
            _ => { },
            CancellationToken.None);

        Assert.Equal(["read"], client.Calls);
        Assert.False(result.ScheduleNextRound);
        Assert.Equal("Selected farm lists were not found on the farm page.", result.WaitMessage);
        Assert.Equal(600, result.WaitSeconds);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task ExecuteAsync_AllAtOnce_PreservesLossHandlingThenSendOrder()
    {
        var client = new FakeFarmingClient(
            [new FarmListOverview("Mercs", 3, 3, 0, "42")]);
        var operation = new ContinuousFarmingOperation(client);

        var result = await operation.ExecuteAsync(
            new ContinuousFarmingDispatchRequest(
                FarmingDefaults.SendModeAllAtOnce,
                [],
                [],
                600,
                true,
                new FarmListLossHandlingRequest(false, false, "", "", "")),
            _ => { },
            CancellationToken.None);

        Assert.Equal(["loss", "send-all", "read"], client.Calls);
        Assert.True(result.ScheduleNextRound);
        Assert.Equal("Continuous farming cooldown active.", result.WaitMessage);
    }

    private sealed class FakeFarmingClient(
        IReadOnlyList<FarmListOverview> initialOverview,
        IReadOnlyList<FarmListOverview>? refreshedOverview = null) : IFarmingClient
    {
        private readonly Queue<IReadOnlyList<FarmListOverview>> _overviews = new([initialOverview, refreshedOverview ?? initialOverview]);

        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<FarmListOverview>> ReadFarmListsOverviewAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("read");
            return Task.FromResult(_overviews.Count > 1 ? _overviews.Dequeue() : _overviews.Peek());
        }

        public Task<int?> SendFarmListNowAsync(string farmListName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> SendAllFarmListsNowAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("send-all");
            return Task.FromResult(1);
        }

        public Task<int> SendSelectedFarmListsNowAsync(IReadOnlyCollection<string> selectedNames, IReadOnlyCollection<string> selectedIds, CancellationToken cancellationToken = default)
        {
            Calls.Add("send-selected");
            Assert.Contains("Mercs", selectedNames);
            Assert.Contains("42", selectedIds);
            return Task.FromResult(1);
        }

        public Task<int> SendAllFarmListsViaStartAllButtonAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FarmListLossDeactivationResult> DeactivateFarmListLossTargetsAsync(bool includeUnoccupiedOasis, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FarmListLossDeactivationResult> HandleFarmListLossTargetsAsync(FarmListLossHandlingRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add("loss");
            return Task.FromResult(new FarmListLossDeactivationResult(2, 1, 0));
        }

        public Task<FarmListCreateBatchResult> CreateFarmListsAsync(FarmListCreateRequest request, IProgress<FarmListCreateProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FarmAddBatchResult> AddFarmsFromCoordinatesAsync(string farmListName, string troopType, int troopCount, int requestedCount, IReadOnlyList<FarmCoordinate> coordinates, bool useDefaultTroops = false, IProgress<FarmAddProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
