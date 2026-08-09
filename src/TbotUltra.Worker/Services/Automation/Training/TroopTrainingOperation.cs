using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Services;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>Owns task-level troop-training decisions behind the browser client seam.</summary>
internal sealed class TroopTrainingOperation(ITrainingClient client)
{
    public async Task<SmithyUpgradeOperationResult> UpgradeSelectedAtSmithyAsync(
        string? targetPayload,
        CancellationToken cancellationToken)
    {
        var targets = SmithyUpgradePayload.Parse(targetPayload);
        if (targets.Count == 0)
        {
            return SmithyUpgradeOperationResult.NoSelection;
        }

        return new SmithyUpgradeOperationResult(
            await client.UpgradeSelectedTroopsAtSmithyAsync(targets, cancellationToken),
            true);
    }

    public Task<string> BuildAsync(CancellationToken cancellationToken)
        => client.BuildTroopsAsync(cancellationToken);
}

internal sealed record SmithyUpgradeOperationResult(string Message, bool ShouldRefreshSnapshot)
{
    public static SmithyUpgradeOperationResult NoSelection { get; } = new(
        "Smithy: no troops selected for upgrade — configure them via 'Upgrade options'. Nothing to do.",
        false);
}
