using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BanRecoveryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tbot-ban-recovery-{Guid.NewGuid():N}");

    [Fact]
    public void CaptureIfMissing_IsImmutableAndSurvivesReload()
    {
        var first = new BanRecoveryStore(_root);
        Assert.True(first.CaptureIfMissing("one", new Dictionary<string, VillageStatus>
        {
            ["xy:1|2"] = Status("Before", 10),
        }));
        Assert.False(first.CaptureIfMissing("one", new Dictionary<string, VillageStatus>
        {
            ["xy:1|2"] = Status("After", 4),
        }));

        var loaded = new BanRecoveryStore(_root).Load("one");
        Assert.NotNull(loaded);
        Assert.Equal(BanRecoveryStage.Banned, loaded!.Stage);
        Assert.Equal(10, loaded.Baseline["xy:1|2"].ResourceFields.Single().Level);

        first.SetStage("one", BanRecoveryStage.ScanPending);
        Assert.Equal(BanRecoveryStage.ScanPending, new BanRecoveryStore(_root).Load("one")!.Stage);
        first.Clear("one");
        Assert.Null(first.Load("one"));
    }

    private static VillageStatus Status(string name, int fieldLevel) => new(
        name, [new Village(name, "/dorf1.php?newdid=1", CoordX: 1, CoordY: 2)],
        new Dictionary<string, string>(), [new ResourceField(1, "wood", "Woodcutter", fieldLevel, null)],
        [], [], ActiveVillageCoordX: 1, ActiveVillageCoordY: 2);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
