using System.Text.Json;
using System.IO;
using TbotUltra.Core.Accounts;
using TbotUltra.Core.Configuration;

namespace TbotUltra.Desktop.Services;

public static class HeroCropAntiStarveSettingsStore
{
    private static readonly object FileIoLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class StateFile
    {
        public Dictionary<string, bool> Villages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsEnabled(
        string projectRoot,
        string? accountName,
        string? serverUrl,
        string? villageKey,
        bool defaultIfMissing = true)
    {
        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(villageKey))
        {
            return defaultIfMissing;
        }

        lock (FileIoLock)
        {
            var state = Read(projectRoot, accountName, serverUrl);
            return state.Villages.TryGetValue(villageKey, out var enabled) ? enabled : defaultIfMissing;
        }
    }

    public static void Save(
        string projectRoot,
        string? accountName,
        string? serverUrl,
        IEnumerable<(string VillageKey, bool IsEnabled)> villages)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        lock (FileIoLock)
        {
            var state = Read(projectRoot, accountName, serverUrl);
            foreach (var village in villages.Where(village => !string.IsNullOrWhiteSpace(village.VillageKey)))
            {
                state.Villages[village.VillageKey] = village.IsEnabled;
            }

            var path = AccountStoragePaths.HeroCropAntiStarveSettingsPath(projectRoot, accountName, serverUrl);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
        }
    }

    private static StateFile Read(string projectRoot, string accountName, string? serverUrl)
    {
        var path = AccountStoragePaths.HeroCropAntiStarveSettingsPath(projectRoot, accountName, serverUrl);
        if (!File.Exists(path))
        {
            return new StateFile();
        }

        try
        {
            return JsonSerializer.Deserialize<StateFile>(File.ReadAllText(path), JsonOptions) ?? new StateFile();
        }
        catch
        {
            return new StateFile();
        }
    }
}
