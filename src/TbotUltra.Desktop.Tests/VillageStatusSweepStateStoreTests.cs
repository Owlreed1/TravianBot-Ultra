using TbotUltra.Core.Accounts;
using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class VillageStatusSweepStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tbot-ultra-village-sweep-tests", Guid.NewGuid().ToString("N"));
    private const string Account = "alice";

    public VillageStatusSweepStateStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void SaveLoad_RestoresFutureNextScanDeadline()
    {
        var now = new DateTimeOffset(2026, 7, 27, 20, 0, 0, TimeSpan.Zero);
        var deadline = now.AddMinutes(24);

        VillageStatusSweepStateStore.SaveNextScanUtc(_root, Account, deadline);

        var restored = VillageStatusSweepStateStore.LoadNextScanUtc(_root, Account, now);

        Assert.Equal(deadline, restored);
    }

    [Fact]
    public void Load_ReturnsNullForExpiredDeadline()
    {
        var now = new DateTimeOffset(2026, 7, 27, 20, 0, 0, TimeSpan.Zero);
        VillageStatusSweepStateStore.SaveNextScanUtc(_root, Account, now.AddMinutes(-1));

        Assert.Null(VillageStatusSweepStateStore.LoadNextScanUtc(_root, Account, now));
    }

    [Fact]
    public void Load_QuarantinesCorruptStateFile()
    {
        var path = AccountStoragePaths.VillageStatusSweepStatePath(_root, Account);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{not-json");

        Assert.Null(VillageStatusSweepStateStore.LoadNextScanUtc(_root, Account, DateTimeOffset.UtcNow));
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "village_status_sweep.json.corrupt-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
