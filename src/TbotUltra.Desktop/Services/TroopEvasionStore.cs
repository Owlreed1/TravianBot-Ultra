using System.IO;
using System.Text.Json;
using TbotUltra.Core.Accounts;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

public sealed record TroopEvasionVillageSettings(
    string VillageKey,
    string VillageName,
    string? VillageUrl,
    bool Enabled = false,
    int? TargetX = null,
    int? TargetY = null,
    TroopEvasionMovementType MovementType = TroopEvasionMovementType.Reinforcement,
    IReadOnlyList<int>? SelectedTroopSlots = null,
    bool IncludeHero = true);

public sealed record TroopEvasionProtectionState(
    string VillageKey,
    DateTimeOffset TriggeringArrivalUtc,
    DateTimeOffset ProtectedThroughUtc,
    DateTimeOffset ConfirmedAtUtc);

public sealed record TroopEvasionState(
    int LeadTimeMinutes,
    int ProtectionWindowMinutes,
    IReadOnlyList<TroopEvasionVillageSettings> Villages,
    IReadOnlyList<TroopEvasionProtectionState> Protections)
{
    public static TroopEvasionState Default { get; } = new(5, 5, [], []);
}

public sealed class TroopEvasionStore(string projectRoot, Action<string>? log = null)
{
    private sealed record FileModel(int SchemaVersion, TroopEvasionState State);
    private const int SchemaVersion = 1;
    private static readonly object IoLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public TroopEvasionState Load(string? accountName, string? serverUrl, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(accountName)) return TroopEvasionState.Default;
        var path = AccountStoragePaths.TroopEvasionSettingsPath(projectRoot, accountName, serverUrl);
        if (!File.Exists(path)) return TroopEvasionState.Default;
        try
        {
            FileModel? file;
            lock (IoLock) file = JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), JsonOptions);
            if (file is null || file.SchemaVersion != SchemaVersion) throw new JsonException("Unsupported troop-evasion schema.");
            var state = file.State ?? TroopEvasionState.Default;
            return state with
            {
                LeadTimeMinutes = NormalizeMinutes(state.LeadTimeMinutes),
                ProtectionWindowMinutes = NormalizeMinutes(state.ProtectionWindowMinutes),
                Villages = (state.Villages ?? []).Select(NormalizeVillage).ToList(),
                Protections = (state.Protections ?? []).Where(item => item.ProtectedThroughUtc > nowUtc).ToList(),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Quarantine(path);
            log?.Invoke($"[troop-evasion] corrupt settings were quarantined: {ex.Message}");
            return TroopEvasionState.Default;
        }
    }

    public void Save(string? accountName, string? serverUrl, TroopEvasionState state)
    {
        if (string.IsNullOrWhiteSpace(accountName)) return;
        var path = AccountStoragePaths.TroopEvasionSettingsPath(projectRoot, accountName, serverUrl);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            lock (IoLock)
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new FileModel(SchemaVersion, state), JsonOptions));
                File.Move(temporaryPath, path, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"[troop-evasion] settings could not be saved: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }

    private static TroopEvasionVillageSettings NormalizeVillage(TroopEvasionVillageSettings value) => value with
    {
        SelectedTroopSlots = (value.SelectedTroopSlots ?? Enumerable.Range(1, 10).ToList())
            .Where(slot => slot is >= 1 and <= 10).Distinct().Order().ToList(),
    };

    private static int NormalizeMinutes(int value) => value is 1 or 2 or 5 or 10 ? value : 5;

    private static void Quarantine(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            File.Move(path, $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", overwrite: false);
        }
        catch { }
    }
}
