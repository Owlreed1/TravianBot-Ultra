using TbotUltra.Worker.Services;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>
/// Executes one building mutation through the existing browser-client seam.
/// Task handlers keep their current task-name, logging, and result contracts;
/// this operation owns only the choice of browser mutation.
/// </summary>
internal sealed class BuildingAutomationOperation(IBuildingClient client)
{
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
