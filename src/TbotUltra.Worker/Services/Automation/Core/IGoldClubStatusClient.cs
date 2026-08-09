using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

/// <summary>Reads the Gold Club signal and its fallback account tribe.</summary>
internal interface IGoldClubStatusClient
{
    string AccountName { get; }
    string ServerUrl { get; }
    Task<bool> ReadGoldClubStatusAsync(CancellationToken cancellationToken = default);
    Task<AccountSnapshot> ReadAccountSnapshotAsync(bool forceRefreshVillages = false, bool preferCurrentPageVillages = false, bool restorePageAfterProfile = true, bool suppressEnsureUiSync = false, bool skipOverviewNavigation = false, CancellationToken cancellationToken = default);
}

public sealed partial class TravianClient : IGoldClubStatusClient
{
}
