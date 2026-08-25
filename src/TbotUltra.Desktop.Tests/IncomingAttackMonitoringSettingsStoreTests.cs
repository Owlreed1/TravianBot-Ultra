using TbotUltra.Core.Accounts;
using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class IncomingAttackMonitoringSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tbot-incoming-monitoring-{Guid.NewGuid():N}");

    [Fact]
    public void MissingSettings_DefaultsEveryVillageToEnabled()
    {
        var disabled = new IncomingAttackMonitoringSettingsStore(_root)
            .Load("account", "https://one.example");

        Assert.Empty(disabled);
    }

    [Fact]
    public void SaveLoad_IsolatesDisabledVillageKeysByWorld()
    {
        var store = new IncomingAttackMonitoringSettingsStore(_root);
        store.Save("account", "https://one.example", new[] { "xy:1|2", "xy:3|4" });

        Assert.Equal(2, store.Load("account", "https://one.example").Count);
        Assert.Empty(store.Load("account", "https://two.example"));
    }

    [Fact]
    public void Load_QuarantinesCorruptSettings()
    {
        var path = AccountStoragePaths.IncomingAttackMonitoringSettingsPath(_root, "account", "https://one.example");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{broken");

        var disabled = new IncomingAttackMonitoringSettingsStore(_root).Load("account", "https://one.example");

        Assert.Empty(disabled);
        Assert.False(File.Exists(path));
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
