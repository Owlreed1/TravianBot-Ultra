using TbotUltra.Core.Configuration;
using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Services.Automation;

namespace TbotUltra.Worker.Services;

public sealed partial class BotTaskRunner
{
    private static async Task ExecuteUpgradeTroopsAtSmithyAsync(TaskExecutionContext context)
    {
        var operation = new TroopTrainingOperation(context.Client);
        var result = await operation.UpgradeSelectedAtSmithyAsync(
            context.Options.SmithyUpgradeTargets,
            context.CancellationToken);
        context.Log(result.Message);
        if (!result.ShouldRefreshSnapshot)
        {
            return;
        }

        await RefreshBuildingsSnapshotAfterTaskAsync(context);
        ThrowIfTroopsGroupBlocked(result.Message);
        ThrowIfTaskBlocked("upgrade_troops_at_smithy", result.Message);
    }

    private static async Task ExecuteBuildTroopsAsync(TaskExecutionContext context)
    {
        context.Log("[troops] build_troops starting");
        var result = await new TroopTrainingOperation(context.Client).BuildAsync(context.CancellationToken);
        context.Log(result);
        ThrowIfTaskBlocked("build_troops", result);
    }
}
