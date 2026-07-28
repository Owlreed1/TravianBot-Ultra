using System.Text.Json;
using TbotUltra.Core.Accounts;
using TbotUltra.Core.Infrastructure;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

public sealed class HeroInventorySnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _projectRoot;

    public HeroInventorySnapshotStore(string projectRoot)
    {
        _projectRoot = projectRoot;
    }

    public bool TryLoad(
        string accountName,
        string? serverUrl,
        out HeroInventoryResources? resources)
    {
        resources = null;
        var filePath = AccountStoragePaths.HeroInventorySnapshotPath(_projectRoot, accountName, serverUrl);
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var cached = JsonSerializer.Deserialize<CachedHeroInventorySnapshot>(
                File.ReadAllText(filePath),
                JsonOptions);
            if (cached is null
                || !string.Equals(
                    AccountStoragePaths.NormalizeAccountKey(accountName),
                    AccountStoragePaths.NormalizeAccountKey(cached.AccountName),
                    StringComparison.Ordinal)
                || !string.Equals(
                    AccountStoragePaths.NormalizeServerKey(serverUrl),
                    AccountStoragePaths.NormalizeServerKey(cached.ServerUrl),
                    StringComparison.Ordinal))
            {
                return false;
            }

            resources = cached.Resources;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Save(string accountName, string? serverUrl, HeroInventoryResources resources)
    {
        var filePath = AccountStoragePaths.HeroInventorySnapshotPath(_projectRoot, accountName, serverUrl);
        var cached = new CachedHeroInventorySnapshot(
            accountName,
            serverUrl ?? string.Empty,
            resources,
            DateTimeOffset.UtcNow);
        AtomicFile.WriteAllText(filePath, JsonSerializer.Serialize(cached, JsonOptions));
    }

    private sealed record CachedHeroInventorySnapshot(
        string AccountName,
        string ServerUrl,
        HeroInventoryResources Resources,
        DateTimeOffset UpdatedAtUtc);
}
