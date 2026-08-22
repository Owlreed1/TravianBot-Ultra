using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class IncomingAttackStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tbot-incoming-attacks-{Guid.NewGuid():N}");

    [Fact]
    public void SaveLoad_IsolatesWorldsAndDropsExpiredAttacks()
    {
        var store = new IncomingAttackStore(_root);
        var now = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var active = new IncomingAttack("active", "A4", now.AddMinutes(5), TargetVillageKey: "xy:1|2");
        var expired = new IncomingAttack("expired", "A4", now.AddMinutes(-1), TargetVillageKey: "xy:1|2");
        var pending = new IncomingAttackSignal("A5", CoordX: 3, CoordY: 4);

        store.Save("account", "https://ts1.example", [active, expired], [pending]);

        var loaded = store.Load("account", "https://ts1.example", now);
        Assert.Equal("active", Assert.Single(loaded.Attacks).Id);
        Assert.Equal("A5", Assert.Single(loaded.PendingSignals).VillageName);
        Assert.Empty(store.Load("account", "https://ts2.example", now).Attacks);
    }

    [Fact]
    public void Load_CorruptSnapshotReturnsEmptyState()
    {
        var path = TbotUltra.Core.Accounts.AccountStoragePaths.IncomingAttacksSnapshotPath(
            _root, "account", "https://ts1.example");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{broken");

        var loaded = new IncomingAttackStore(_root).Load(
            "account", "https://ts1.example", DateTimeOffset.UtcNow);

        Assert.Empty(loaded.Attacks);
        Assert.Empty(loaded.PendingSignals);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
