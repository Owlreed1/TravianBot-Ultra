using TbotUltra.Worker.Services;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>
/// Executes one building mutation through the existing browser-client seam.
/// Task handlers keep their current task-name, logging, and result contracts;
/// this operation owns only the choice of browser mutation.
/// </summary>
internal sealed class BuildingAutomationOperation(IBuildingClient client)
{
    public Task<BreweryCelebrationStatus> ReadBreweryCelebrationStatusAsync(
        IReadOnlyList<Building>? knownBuildings,
        CancellationToken cancellationToken)
        => client.ReadBreweryCelebrationStatusAsync(knownBuildings, cancellationToken);

    public Task<SmithyUpgradeStatus> ReadSmithyUpgradeStatusAsync(
        IReadOnlyList<Building>? knownBuildings,
        CancellationToken cancellationToken)
        => client.ReadSmithyUpgradeStatusAsync(knownBuildings, cancellationToken);

    public Task<string> ReadSmithyQueueFromCurrentPageAsync(CancellationToken cancellationToken)
        => client.ReadSmithyQueueFromCurrentPageTestAsync(cancellationToken);

    public Task<string> RunBreweryCelebrationAsync(
        bool restartDelayEnabled,
        double restartDelayMinMinutes,
        double restartDelayMaxMinutes,
        CancellationToken cancellationToken)
        => client.RunBreweryCelebrationAsync(
            restartDelayEnabled,
            restartDelayMinMinutes,
            restartDelayMaxMinutes,
            cancellationToken);

    public Task<string> RunTownHallCelebrationAsync(
        string mode,
        int count,
        bool restartDelayEnabled,
        double restartDelayMinMinutes,
        double restartDelayMaxMinutes,
        CancellationToken cancellationToken)
        => client.RunTownHallCelebrationAsync(
            mode,
            count,
            restartDelayEnabled,
            restartDelayMinMinutes,
            restartDelayMaxMinutes,
            cancellationToken);

    public Task<string> ExecuteAsync(BuildingAutomationRequest request, CancellationToken cancellationToken)
    {
        return request.Action switch
        {
            BuildingAutomationAction.Demolish => client.DemolishBuildingToLevelAsync(
                request.TargetBuildingSlotOrName!,
                request.TargetLevel!.Value,
                cancellationToken),
            BuildingAutomationAction.UpgradeToLevel => client.UpgradeBuildingToLevelAsync(
                request.SlotId!.Value,
                request.TargetLevel!.Value,
                cancellationToken),
            BuildingAutomationAction.UpgradeToMax => client.UpgradeBuildingToMaxAsync(
                request.SlotId!.Value,
                request.MaxAttempts,
                cancellationToken),
            BuildingAutomationAction.Construct => client.ConstructBuildingAsync(
                request.SlotId!.Value,
                request.Gid!.Value,
                request.Name!,
                cancellationToken,
                request.AllowSlotFallback,
                request.FallbackExcludedSlots),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
    }
}

internal sealed record BuildingAutomationRequest(
    BuildingAutomationAction Action,
    int? SlotId = null,
    int? TargetLevel = null,
    int? Gid = null,
    string? Name = null,
    string? TargetBuildingSlotOrName = null,
    int MaxAttempts = 30,
    bool AllowSlotFallback = false,
    string? FallbackExcludedSlots = null);

internal enum BuildingAutomationAction
{
    Demolish,
    UpgradeToLevel,
    UpgradeToMax,
    Construct,
}
