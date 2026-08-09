using TbotUltra.Core.Travian;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using TbotUltra.Worker.Services.Automation;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class CombatOperationTests
{
    [Fact]
    public async Task SendReinforcements_UsesExistingCombatClientContract()
    {
        var client = new FakeCombatClient();
        var operation = new CombatOperation(client);

        var result = await operation.SendReinforcementsBetweenOwnVillagesAsync(CancellationToken.None);

        Assert.Equal("Reinforcements sent.", result);
        Assert.True(client.SendReinforcementsCalled);
    }

    private sealed class FakeCombatClient : ICombatClient
    {
        public bool SendReinforcementsCalled { get; private set; }

        public Task<IReadOnlyDictionary<string, long>> ReadAvailableTroopsForCatapultWavesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());

        public Task<IReadOnlyDictionary<string, long>> ReadAvailableTroopsForCatapultWavesAsync(bool forceRefresh, CancellationToken cancellationToken = default)
            => ReadAvailableTroopsForCatapultWavesAsync(cancellationToken);

        public Task<CatapultWaveSetupInfo> ReadCatapultWaveSetupInfoAsync(bool forceRefresh, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CatapultWaveRunResult> StartCatapultWavesAsync(CatapultWaveRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string> SendReinforcementsBetweenOwnVillagesAsync(CancellationToken cancellationToken = default)
        {
            SendReinforcementsCalled = true;
            return Task.FromResult("Reinforcements sent.");
        }

        public Task<string> SendResourcesBetweenOwnVillagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("Resources sent.");

        public Task<string> TestSendReinforcementsBetweenOwnVillagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("Reinforcements test passed.");
    }
}
