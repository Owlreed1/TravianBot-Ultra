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

    [Fact]
    public async Task EveryCombatOperation_ForwardsTheExactRequestAndCancellationToken()
    {
        var client = new FakeCombatClient();
        var operation = new CombatOperation(client);
        var request = new CatapultWaveRequest(1, -2, 3, true, new Dictionary<string, int>(), new Dictionary<string, int>(), "Wall", null);
        using var cancellation = new CancellationTokenSource();

        await operation.ReadAvailableTroopsForCatapultWavesAsync(true, cancellation.Token);
        await operation.ReadCatapultWaveSetupInfoAsync(false, cancellation.Token);
        await operation.StartCatapultWavesAsync(request, cancellation.Token);
        await operation.SendReinforcementsBetweenOwnVillagesAsync(cancellation.Token);
        await operation.SendResourcesBetweenOwnVillagesAsync(cancellation.Token);
        await operation.TestSendReinforcementsBetweenOwnVillagesAsync(cancellation.Token);

        Assert.Equal(["available", "setup", "start", "reinforcements", "resources", "test-reinforcements"], client.Calls);
        Assert.True(client.ForceRefresh);
        Assert.Same(request, client.Request);
        Assert.All(client.CancellationTokens, token => Assert.Equal(cancellation.Token, token));
    }

    private sealed class FakeCombatClient : ICombatClient
    {
        public bool SendReinforcementsCalled { get; private set; }
        public List<string> Calls { get; } = [];
        public List<CancellationToken> CancellationTokens { get; } = [];
        public bool ForceRefresh { get; private set; }
        public CatapultWaveRequest? Request { get; private set; }

        public Task<IReadOnlyDictionary<string, long>> ReadAvailableTroopsForCatapultWavesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());

        public Task<IReadOnlyDictionary<string, long>> ReadAvailableTroopsForCatapultWavesAsync(bool forceRefresh, CancellationToken cancellationToken = default)
        {
            ForceRefresh = forceRefresh;
            return Record("available", cancellationToken, (IReadOnlyDictionary<string, long>)new Dictionary<string, long>());
        }

        public Task<CatapultWaveSetupInfo> ReadCatapultWaveSetupInfoAsync(bool forceRefresh, CancellationToken cancellationToken = default)
            => Record("setup", cancellationToken, new CatapultWaveSetupInfo(new Dictionary<string, long>(), 10));

        public Task<CatapultWaveRunResult> StartCatapultWavesAsync(CatapultWaveRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Record("start", cancellationToken, new CatapultWaveRunResult(3, 3, 3, 0, 1, -2));
        }

        public Task<string> SendReinforcementsBetweenOwnVillagesAsync(CancellationToken cancellationToken = default)
        {
            SendReinforcementsCalled = true;
            return Record("reinforcements", cancellationToken, "Reinforcements sent.");
        }

        public Task<string> SendResourcesBetweenOwnVillagesAsync(CancellationToken cancellationToken = default)
            => Record("resources", cancellationToken, "Resources sent.");

        public Task<string> TestSendReinforcementsBetweenOwnVillagesAsync(CancellationToken cancellationToken = default)
            => Record("test-reinforcements", cancellationToken, "Reinforcements test passed.");

        public Task<TroopEvasionResult> SendTroopEvasionAsync(TroopEvasionRequest request, IProgress<TroopEvasionProgress>? progress = null, CancellationToken cancellationToken = default)
            => Record("evasion", cancellationToken, new TroopEvasionResult(TroopEvasionOutcome.Succeeded, "sent"));

        public Task<TroopEvasionValidationResult> ValidateTroopEvasionAsync(TroopEvasionRequest request, CancellationToken cancellationToken = default)
            => Record("validate-evasion", cancellationToken, new TroopEvasionValidationResult(true, "valid"));

        private Task<T> Record<T>(string call, CancellationToken cancellationToken, T result)
        {
            Calls.Add(call);
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(result);
        }
    }
}
