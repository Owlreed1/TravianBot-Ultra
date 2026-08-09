using TbotUltra.Core.Tasks;

namespace TbotUltra.Worker.Services;

/// <summary>
/// Browser mutations used by troop-training task decisions. Parsing, navigation,
/// retries, and selectors remain on <see cref="TravianClient"/>.
/// </summary>
internal interface ITrainingClient
{
    Task<string> UpgradeSelectedTroopsAtSmithyAsync(
        IReadOnlyList<SmithyTroopTarget> targets,
        CancellationToken cancellationToken = default);

    Task<string> BuildTroopsAsync(CancellationToken cancellationToken = default);
}
