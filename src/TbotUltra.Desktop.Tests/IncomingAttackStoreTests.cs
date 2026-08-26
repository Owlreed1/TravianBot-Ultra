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

        store.Save("account", "https://ts1.example", [active, expired], [pending],
            new Dictionary<string, int> { ["xy:1|2"] = 2 });

        var loaded = store.Load("account", "https://ts1.example", now);
        Assert.Equal("active", Assert.Single(loaded.Attacks).Id);
        Assert.Equal("A5", Assert.Single(loaded.PendingSignals).VillageName);
        Assert.Equal(2, loaded.ConfirmedMovementCounts["xy:1|2"]);
        Assert.Empty(store.Load("account", "https://ts2.example", now).Attacks);
    }

    [Fact]
    public void Load_AfterManualListClearKeepsConfirmedMovementCount()
    {
        new IncomingAttackStore(_root).Save(
            "account",
            "https://ts1.example",
            [],
            [],
            new Dictionary<string, int> { ["xy:25|-196"] = 8 });

        var restored = new IncomingAttackStore(_root).Load(
            "account", "https://ts1.example", DateTimeOffset.UtcNow);

        Assert.Empty(restored.Attacks);
        Assert.Equal(8, restored.ConfirmedMovementCounts["xy:25|-196"]);
    }

    [Fact]
    public void Load_AfterRestartKeepsConfirmedAttackUntilAbsoluteArrival()
    {
        var now = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var arrival = now.AddHours(2);
        new IncomingAttackStore(_root).Save(
            "account",
            "https://ts1.example",
            [new IncomingAttack("confirmed", "BRO", arrival, TargetVillageKey: "xy:25|-196")],
            []);

        var restored = new IncomingAttackStore(_root).Load(
            "account",
            "https://ts1.example",
            now.AddMinutes(30));

        var attack = Assert.Single(restored.Attacks);
        Assert.Equal("confirmed", attack.Id);
        Assert.Equal(arrival, attack.ArrivalAtUtc);
    }

    [Fact]
    public void Load_DropsExpiredPendingWarningAfterConfirmedMovementArrived()
    {
        var now = new DateTimeOffset(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);
        new IncomingAttackStore(_root).Save(
            "account",
            "https://ts1.example",
            [],
            [new IncomingAttackSignal("BRO", CoordX: 25, CoordY: -196)],
            new Dictionary<string, int> { ["xy:25|-196"] = 1 });

        var restored = new IncomingAttackStore(_root).Load(
            "account", "https://ts1.example", now);

        Assert.Empty(restored.PendingSignals);
    }

    [Fact]
    public void Load_VersionOneSnapshotMigratesActiveAttacksAndConfirmedCounts()
    {
        var path = TbotUltra.Core.Accounts.AccountStoragePaths.IncomingAttacksSnapshotPath(
            _root, "account", "https://ts1.example");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            """
            {
              "schemaVersion": 1,
              "capturedAtUtc": "2026-08-22T18:00:00+00:00",
              "attacks": [
                {
                  "id": "legacy",
                  "targetVillageName": "BRO",
                  "arrivalAtUtc": "2026-08-22T20:00:00+00:00",
                  "movementType": 1,
                  "targetVillageKey": "xy:25|-196",
                  "observedAtUtc": "2026-08-22T18:00:00+00:00"
                }
              ],
              "pendingSignals": []
            }
            """);

        var restored = new IncomingAttackStore(_root).Load(
            "account", "https://ts1.example", new DateTimeOffset(2026, 8, 22, 19, 0, 0, TimeSpan.Zero));

        Assert.Equal("legacy", Assert.Single(restored.Attacks).Id);
        Assert.Equal(1, restored.ConfirmedMovementCounts["xy:25|-196"]);
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
        Assert.Empty(loaded.ConfirmedMovementCounts);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
