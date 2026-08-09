using TbotUltra.Core.Tasks;
using TbotUltra.Core.Travian;
using TbotUltra.Worker.Services;
using TbotUltra.Worker.Services.Automation;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class TroopTrainingOperationTests
{
    [Fact]
    public async Task UpgradeSelectedAtSmithyAsync_EmptyPayload_IsNoOp()
    {
        var client = new FakeTrainingClient();
        var operation = new TroopTrainingOperation(client);

        var result = await operation.UpgradeSelectedAtSmithyAsync(null, CancellationToken.None);

        Assert.False(result.ShouldRefreshSnapshot);
        Assert.Empty(client.SmithyTargets);
        Assert.Equal("Smithy: no troops selected for upgrade — configure them via 'Upgrade options'. Nothing to do.", result.Message);
    }

    [Fact]
    public async Task UpgradeSelectedAtSmithyAsync_ParsesPayloadThenUsesTrainingClient()
    {
        var client = new FakeTrainingClient();
        var operation = new TroopTrainingOperation(client);

        var result = await operation.UpgradeSelectedAtSmithyAsync("u21=99;u24=4", CancellationToken.None);

        Assert.True(result.ShouldRefreshSnapshot);
        Assert.Equal("smithy upgraded", result.Message);
        Assert.Equal([new SmithyTroopTarget("u21", 20), new SmithyTroopTarget("u24", 4)], client.SmithyTargets);
    }

    [Fact]
    public async Task BuildAsync_RoutesToTrainingClient()
    {
        var client = new FakeTrainingClient();

        var result = await new TroopTrainingOperation(client).BuildAsync(CancellationToken.None);

        Assert.True(client.BuildRequested);
        Assert.Equal("troops queued", result);
    }

    private sealed class FakeTrainingClient : ITrainingClient
    {
        public IReadOnlyList<SmithyTroopTarget> SmithyTargets { get; private set; } = [];
        public bool BuildRequested { get; private set; }

        public Task<string> UpgradeSelectedTroopsAtSmithyAsync(IReadOnlyList<SmithyTroopTarget> targets, CancellationToken cancellationToken = default)
        {
            SmithyTargets = targets;
            return Task.FromResult("smithy upgraded");
        }

        public Task<string> BuildTroopsAsync(CancellationToken cancellationToken = default)
        {
            BuildRequested = true;
            return Task.FromResult("troops queued");
        }
    }
}
