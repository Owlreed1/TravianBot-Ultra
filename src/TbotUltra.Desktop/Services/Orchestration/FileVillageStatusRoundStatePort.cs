using TbotUltra.Desktop.Services;

namespace TbotUltra.Desktop.Services.Orchestration;

internal sealed class FileVillageStatusRoundStatePort(string projectRoot) : IVillageStatusRoundStatePort
{
    public DateTimeOffset? Load(string? accountName, DateTimeOffset nowUtc) =>
        VillageStatusSweepStateStore.LoadNextScanUtc(projectRoot, accountName, nowUtc);

    public bool Save(string? accountName, DateTimeOffset nextRoundUtc) =>
        VillageStatusSweepStateStore.SaveNextScanUtc(projectRoot, accountName, nextRoundUtc);

    public bool Clear(string? accountName) =>
        VillageStatusSweepStateStore.Clear(projectRoot, accountName);
}
