using System.Text.Json;
using TbotUltra.Core.Accounts;
using TbotUltra.Core.Infrastructure;

namespace TbotUltra.Worker.Services;

internal sealed class HeroOintmentAvailabilityStore(string projectRoot)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    internal HeroOintmentAvailabilityState? TryLoad(string accountName, string? serverUrl)
    {
        var path = AccountStoragePaths.HeroOintmentAvailabilityPath(projectRoot, accountName, serverUrl);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(path), JsonOptions);
            if (persisted is null
                || !string.Equals(AccountStoragePaths.NormalizeAccountKey(accountName), AccountStoragePaths.NormalizeAccountKey(persisted.AccountName), StringComparison.Ordinal)
                || !string.Equals(AccountStoragePaths.NormalizeServerKey(serverUrl), AccountStoragePaths.NormalizeServerKey(persisted.ServerUrl), StringComparison.Ordinal))
            {
                return null;
            }

            return new HeroOintmentAvailabilityState(persisted.RetryNotBeforeUtc);
        }
        catch
        {
            return null;
        }
    }

    internal void SaveUnavailable(string accountName, string? serverUrl, DateTimeOffset retryNotBeforeUtc)
    {
        var path = AccountStoragePaths.HeroOintmentAvailabilityPath(projectRoot, accountName, serverUrl);
        var persisted = new PersistedState(accountName, serverUrl ?? string.Empty, retryNotBeforeUtc);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(persisted, JsonOptions));
    }

    internal void Clear(string accountName, string? serverUrl)
    {
        var path = AccountStoragePaths.HeroOintmentAvailabilityPath(projectRoot, accountName, serverUrl);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record PersistedState(string AccountName, string ServerUrl, DateTimeOffset RetryNotBeforeUtc);
}
