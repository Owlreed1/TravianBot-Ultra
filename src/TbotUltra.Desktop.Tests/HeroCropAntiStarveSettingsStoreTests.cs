using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class HeroCropAntiStarveSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tbot-anti-starve-{Guid.NewGuid():N}");

    [Fact]
    public void MissingVillageDefaultsEnabledAndSavedChoicesAreServerScoped()
    {
        Assert.True(HeroCropAntiStarveSettingsStore.IsEnabled(
            _root, "account", "https://ts1.example", "xy:1|2"));

        HeroCropAntiStarveSettingsStore.Save(
            _root,
            "account",
            "https://ts1.example",
            [("xy:1|2", false)]);

        Assert.False(HeroCropAntiStarveSettingsStore.IsEnabled(
            _root, "account", "https://ts1.example", "xy:1|2"));
        Assert.True(HeroCropAntiStarveSettingsStore.IsEnabled(
            _root, "account", "https://ts2.example", "xy:1|2"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
