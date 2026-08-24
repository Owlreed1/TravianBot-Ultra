using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ProxyLibraryStoreTests
{
    [Fact]
    public void Remove_DeletesOnlyTheRequestedPersistedEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tbot-proxy-remove-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "proxies.json");
        try
        {
            var store = new ProxyLibraryStore(path);
            store.Save(
            [
                new ProxyLibraryEntry { Id = "first", Name = "First", Host = "1.1.1.1", Port = 80 },
                new ProxyLibraryEntry { Id = "second", Name = "Second", Host = "2.2.2.2", Port = 80 },
            ]);

            Assert.True(store.Remove("first"));

            var remaining = Assert.Single(store.Load());
            Assert.Equal("second", remaining.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Upsert_DeduplicatesByCanonicalServer()
    {
        var entries = new List<ProxyLibraryEntry>();

        var first = ProxyLibraryStore.Upsert(entries, new ProxyLibraryEntry
        {
            Name = "First",
            Scheme = "socks5",
            Host = "1.2.3.4",
            Port = 1080,
            Country = "Sweden",
            LatencyMs = 120,
        });
        var second = ProxyLibraryStore.Upsert(entries, new ProxyLibraryEntry
        {
            Name = "Updated",
            Scheme = "SOCKS5",
            Host = "1.2.3.4",
            Port = 1080,
            Country = "Norway",
            LatencyMs = 80,
        });

        Assert.Single(entries);
        Assert.Same(first, second);
        Assert.Equal("Updated", first.Name);
        Assert.Equal("Norway", first.Country);
        Assert.Equal(80, first.LatencyMs);
    }

    [Fact]
    public void AddUsage_IsIdempotent()
    {
        var entry = new ProxyLibraryEntry
        {
            Id = "proxy-1",
            Name = "Proxy",
            Scheme = "http",
            Host = "1.2.3.4",
            Port = 8080,
        };
        var entries = new List<ProxyLibraryEntry> { entry };

        ProxyLibraryStore.AddUsage(entries, "proxy-1", "alice");
        ProxyLibraryStore.AddUsage(entries, "proxy-1", "ALICE");
        ProxyLibraryStore.AddUsage(entries, "proxy-1", "bob");

        Assert.Equal(new[] { "alice", "bob" }, entry.UsedByAccounts);
    }

    [Fact]
    public void ClassifyReuse_ReturnsOkForUnknownOrCurrentAccount()
    {
        var entries = new List<ProxyLibraryEntry>
        {
            new()
            {
                Id = "proxy-1",
                Name = "Proxy",
                Scheme = "socks5",
                Host = "1.2.3.4",
                Port = 1080,
                AssignedAccount = "alice",
                UsedByAccounts = new List<string> { "alice" },
            },
        };

        Assert.Equal(ProxyReuse.Ok, ProxyLibraryStore.ClassifyReuse(entries, "socks5://5.6.7.8:1080", "bob").Reuse);
        Assert.Equal(ProxyReuse.Ok, ProxyLibraryStore.ClassifyReuse(entries, "socks5://1.2.3.4:1080", "alice").Reuse);
    }

    [Fact]
    public void ClassifyReuse_ReturnsUsedByOthers()
    {
        var entries = new List<ProxyLibraryEntry>
        {
            new()
            {
                Id = "proxy-1",
                Name = "Proxy",
                Scheme = "http",
                Host = "1.2.3.4",
                Port = 8080,
                UsedByAccounts = new List<string> { "alice", "current" },
            },
        };

        var result = ProxyLibraryStore.ClassifyReuse(entries, "http://1.2.3.4:8080", "current");

        Assert.Equal(ProxyReuse.UsedByOthers, result.Reuse);
        Assert.Equal(new[] { "alice" }, result.Accounts);
    }

    [Fact]
    public void ClassifyReuse_ReturnsLockedToOther()
    {
        var entries = new List<ProxyLibraryEntry>
        {
            new()
            {
                Id = "proxy-1",
                Name = "Proxy",
                Scheme = "socks5",
                Host = "1.2.3.4",
                Port = 1080,
                AssignedAccount = "alice",
                UsedByAccounts = new List<string> { "bob" },
            },
        };

        var result = ProxyLibraryStore.ClassifyReuse(entries, "socks5://1.2.3.4:1080", "bob");

        Assert.Equal(ProxyReuse.LockedToOther, result.Reuse);
        Assert.Equal(new[] { "alice" }, result.Accounts);
    }
}
