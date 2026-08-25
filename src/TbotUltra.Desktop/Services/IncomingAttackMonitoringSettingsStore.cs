using System.IO;
using System.Text.Json;
using TbotUltra.Core.Accounts;

namespace TbotUltra.Desktop.Services;

public sealed class IncomingAttackMonitoringSettingsStore(string projectRoot, Action<string>? log = null)
{
    private sealed record FileModel(int SchemaVersion, IReadOnlyList<string> DisabledVillageKeys);
    private const int SchemaVersion = 1;
    private static readonly object IoLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public IReadOnlySet<string> Load(string? accountName, string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(accountName)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = AccountStoragePaths.IncomingAttackMonitoringSettingsPath(projectRoot, accountName, serverUrl);
        if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            FileModel? file;
            lock (IoLock) file = JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), JsonOptions);
            if (file is null || file.SchemaVersion != SchemaVersion) throw new JsonException("Unsupported incoming-attack monitoring schema.");
            return (file.DisabledVillageKeys ?? [])
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Quarantine(path);
            log?.Invoke($"[incoming-attacks] corrupt monitoring settings were quarantined: {ex.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save(string? accountName, string? serverUrl, IReadOnlyCollection<string> disabledVillageKeys)
    {
        if (string.IsNullOrWhiteSpace(accountName)) return;
        var path = AccountStoragePaths.IncomingAttackMonitoringSettingsPath(projectRoot, accountName, serverUrl);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var file = new FileModel(SchemaVersion, disabledVillageKeys.Order(StringComparer.OrdinalIgnoreCase).ToList());
            lock (IoLock)
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(file, JsonOptions));
                File.Move(temporaryPath, path, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"[incoming-attacks] monitoring settings could not be saved: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }

    private static void Quarantine(string path)
    {
        try
        {
            if (File.Exists(path)) File.Move(path, $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", overwrite: false);
        }
        catch { }
    }
}
