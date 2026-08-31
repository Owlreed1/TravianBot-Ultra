namespace TbotUltra.Worker.Services;

internal interface IResourceUpgradeClient
{
    Task<string> UpgradeResourceToLevelAsync(int slotId, int targetLevel, CancellationToken cancellationToken = default);

    Task<string> UpgradeAllResourcesToLevelAsync(
        int targetLevel,
        string buildStrategy = "lowest_first",
        string? resourceTypes = null,
        string? queuedLevelProjections = null,
        CancellationToken cancellationToken = default);
}

// Resource automation facade. Implementation is split by snapshot, field scan and upgrade flow.
public sealed partial class TravianClient : IResourceUpgradeClient
{
}
