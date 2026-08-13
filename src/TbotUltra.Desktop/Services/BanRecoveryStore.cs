using System.IO;
using System.Text.Json;
using TbotUltra.Core.Accounts;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

internal enum BanRecoveryStage
{
    Banned,
    ScanPending,
    DecisionPending,
}

internal sealed record BanRecoveryState(
    string AccountName,
    BanRecoveryStage Stage,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyDictionary<string, VillageStatus> Baseline,
    DateTimeOffset? SourceSnapshotAtUtc = null);

internal sealed class BanRecoveryStore
{
    private static readonly object FileIoLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _projectRoot;
    private readonly Action<string>? _log;

    public BanRecoveryStore(string projectRoot, Action<string>? log = null)
    {
        _projectRoot = projectRoot;
        _log = log;
    }

    public BanRecoveryState? Load(string accountName)
    {
        var path = AccountStoragePaths.BanRecoveryPath(_projectRoot, accountName);
        lock (FileIoLock)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return JsonSerializer.Deserialize<BanRecoveryState>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _log?.Invoke($"[ban-recovery] could not read recovery state for '{accountName}': {ex.Message}");
                return null;
            }
        }
    }

    public bool CaptureIfMissing(
        string accountName,
        IReadOnlyDictionary<string, VillageStatus> baseline,
        DateTimeOffset? sourceSnapshotAtUtc = null)
    {
        lock (FileIoLock)
        {
            var path = AccountStoragePaths.BanRecoveryPath(_projectRoot, accountName);
            if (File.Exists(path)) return false;

            SaveLocked(path, new BanRecoveryState(
                accountName,
                BanRecoveryStage.Banned,
                DateTimeOffset.UtcNow,
                new Dictionary<string, VillageStatus>(baseline, StringComparer.OrdinalIgnoreCase),
                sourceSnapshotAtUtc));
            _log?.Invoke($"[ban-recovery] captured immutable pre-ban snapshot with {baseline.Count} village(s) for '{accountName}'.");
            return true;
        }
    }

    public void SetStage(string accountName, BanRecoveryStage stage)
    {
        lock (FileIoLock)
        {
            var path = AccountStoragePaths.BanRecoveryPath(_projectRoot, accountName);
            if (!File.Exists(path)) return;
            var state = JsonSerializer.Deserialize<BanRecoveryState>(File.ReadAllText(path), JsonOptions);
            if (state is null) return;
            SaveLocked(path, state with { Stage = stage });
            _log?.Invoke($"[ban-recovery] state for '{accountName}' changed to {stage}.");
        }
    }

    public void Clear(string accountName)
    {
        lock (FileIoLock)
        {
            var path = AccountStoragePaths.BanRecoveryPath(_projectRoot, accountName);
            if (File.Exists(path)) File.Delete(path);
        }
        _log?.Invoke($"[ban-recovery] cleared recovery snapshot for '{accountName}'.");
    }

    private static void SaveLocked(string path, BanRecoveryState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
    }
}
