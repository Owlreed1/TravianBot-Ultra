using TbotUltra.Desktop.Services;
using TbotUltra.Core.Accounts;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AllVillagesImportSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tbot-ultra-all-villages-tests", Guid.NewGuid().ToString("N"));
    private string _server = "https://ts1.example.travian.com";

    public AllVillagesImportSettingsStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void LoadSave_IsScopedByServerAndDefaultsToBothSources()
    {
        var store = new AllVillagesImportSettingsStore(_root, () => "alice", () => _server);
        var defaults = store.Load();
        Assert.True(defaults.IncludePlayers);
        Assert.True(defaults.IncludeNatars);
        Assert.True(defaults.SkipOwnVillages);

        store.Save(new AllVillagesImportSettingsStore.Settings(false, true, "Alice", "ALLY", false));
        Assert.False(store.Load().IncludePlayers);
        Assert.False(store.Load().SkipOwnVillages);

        _server = "https://ts2.example.travian.com";
        var otherServer = store.Load();
        Assert.True(otherServer.IncludePlayers);
        Assert.True(otherServer.IncludeNatars);
        Assert.True(otherServer.SkipOwnVillages);
    }

    [Fact]
    public void Load_LegacySettingsWithoutSkipOwnVillages_DefaultsToTrue()
    {
        var path = AccountStoragePaths.AllVillagesImportSettingsPath(_root, "alice", _server);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
            {
              "includePlayers": true,
              "includeNatars": true,
              "ignoredPlayers": "",
              "ignoredAlliances": ""
            }
            """);

        var store = new AllVillagesImportSettingsStore(_root, () => "alice", () => _server);

        Assert.True(store.Load().SkipOwnVillages);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
