using TbotUltra.Worker.Services;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>
/// Executes a resource-upgrade task through the narrow resource browser seam.
/// It preserves task-handler validation, logging, and result classification.
/// </summary>
internal sealed class ResourceAutomationOperation(IResourceUpgradeClient client)
{
    public Task<string> UpgradeSingleAsync(int slotId, int targetLevel, CancellationToken cancellationToken)
        => client.UpgradeResourceToLevelAsync(slotId, targetLevel, cancellationToken);

    public Task<string> UpgradeAllAsync(
        int targetLevel,
        string buildStrategy,
        string? resourceTypes,
        string? queuedLevelProjections,
        CancellationToken cancellationToken)
        => client.UpgradeAllResourcesToLevelAsync(
            targetLevel,
            buildStrategy,
            resourceTypes,
            queuedLevelProjections,
            cancellationToken);
}
