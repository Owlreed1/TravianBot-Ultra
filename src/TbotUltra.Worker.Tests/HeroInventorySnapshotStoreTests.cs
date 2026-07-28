using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class HeroInventorySnapshotStoreTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "TbotUltra.HeroInventorySnapshotStoreTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_PreservesInventoryAcrossStoreInstances()
    {
        var expected = new HeroInventoryResources(65001, 65002, 65003, 65004);
        new HeroInventorySnapshotStore(_rootPath).Save(
            "account-one",
            "https://ts100.example.com",
            expected);

        var restartedStore = new HeroInventorySnapshotStore(_rootPath);

        Assert.True(restartedStore.TryLoad(
            "account-one",
            "https://ts100.example.com",
            out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryLoad_DoesNotReturnAnotherAccountOrServerInventory()
    {
        var store = new HeroInventorySnapshotStore(_rootPath);
        store.Save(
            "account-one",
            "https://ts100.example.com",
            new HeroInventoryResources(1, 2, 3, 4));

        Assert.False(store.TryLoad("account-two", "https://ts100.example.com", out _));
        Assert.False(store.TryLoad("account-one", "https://ts200.example.com", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
