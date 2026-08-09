using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

/// <summary>
/// Browser mutations used by troop-training task decisions. Parsing, navigation,
/// retries, and selectors remain on <see cref="TravianClient"/>.
/// </summary>
internal interface ITrainingClient
{
    Task<IReadOnlyList<TroopTrainingQueueStatus>> ReadTroopTrainingQueuesAsync(
        IReadOnlyList<Building>? knownBuildings = null,
        CancellationToken cancellationToken = default);

    Task<string> UpgradeSelectedTroopsAtSmithyAsync(
        IReadOnlyList<SmithyTroopTarget> targets,
        CancellationToken cancellationToken = default);

    Task<string> BuildTroopsAsync(CancellationToken cancellationToken = default);
}
