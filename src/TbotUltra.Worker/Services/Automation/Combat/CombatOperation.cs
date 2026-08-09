using TbotUltra.Core.Travian;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>Owns combat-domain dispatch behind the existing browser client contract.</summary>
internal sealed class CombatOperation(ICombatClient client)
{
    public Task<IReadOnlyDictionary<string, long>> ReadAvailableTroopsForCatapultWavesAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
        => client.ReadAvailableTroopsForCatapultWavesAsync(forceRefresh, cancellationToken);

    public Task<CatapultWaveSetupInfo> ReadCatapultWaveSetupInfoAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
        => client.ReadCatapultWaveSetupInfoAsync(forceRefresh, cancellationToken);

    public Task<CatapultWaveRunResult> StartCatapultWavesAsync(
        CatapultWaveRequest request,
        CancellationToken cancellationToken)
        => client.StartCatapultWavesAsync(request, cancellationToken);

    public Task<string> SendReinforcementsBetweenOwnVillagesAsync(CancellationToken cancellationToken)
        => client.SendReinforcementsBetweenOwnVillagesAsync(cancellationToken);

    public Task<string> SendResourcesBetweenOwnVillagesAsync(CancellationToken cancellationToken)
        => client.SendResourcesBetweenOwnVillagesAsync(cancellationToken);

    public Task<string> TestSendReinforcementsBetweenOwnVillagesAsync(CancellationToken cancellationToken)
        => client.TestSendReinforcementsBetweenOwnVillagesAsync(cancellationToken);
}
