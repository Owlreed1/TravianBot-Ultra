using TbotUltra.Core.Accounts;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TroopEvasionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tbot-evasion-{Guid.NewGuid():N}");

    [Fact]
    public void SaveLoad_IsolatesWorldAndRestoresProtection()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new TroopEvasionStore(_root);
        var state = new TroopEvasionState(2, 10,
            [new("xy:1|2", "A", "dorf1.php?newdid=1", true, 3, 4, TroopEvasionMovementType.Raid, [1, 10], true)],
            [new("xy:1|2", now.AddMinutes(1), now.AddMinutes(11), now)]);
        store.Save("account", "https://one.example", state);

        var loaded = store.Load("account", "https://one.example", now);
        Assert.Equal(2, loaded.LeadTimeMinutes);
        Assert.True(Assert.Single(loaded.Villages).Enabled);
        Assert.Single(loaded.Protections);
        Assert.Empty(store.Load("account", "https://two.example", now).Villages);
    }

    [Fact]
    public void Load_QuarantinesCorruptState()
    {
        var path = AccountStoragePaths.TroopEvasionSettingsPath(_root, "account", "https://one.example");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{broken");

        var loaded = new TroopEvasionStore(_root).Load("account", "https://one.example", DateTimeOffset.UtcNow);

        Assert.Empty(loaded.Villages);
        Assert.False(File.Exists(path));
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
