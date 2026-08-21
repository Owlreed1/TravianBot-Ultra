using System.Text.Json;
using System.IO;
using TbotUltra.Core.Accounts;

namespace TbotUltra.Desktop.Services;

public sealed class AllVillagesImportSettingsStore
{
    public sealed record Settings(
        bool IncludePlayers,
        bool IncludeNatars,
        string IgnoredPlayers,
        string IgnoredAlliances,
        bool SkipOwnVillages = true)
    {
        public static Settings Default { get; } = new(true, true, string.Empty, string.Empty, true);
    }

    private static readonly object FileIoLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _projectRoot;
    private readonly Func<string> _activeAccountNameProvider;
    private readonly Func<string> _activeServerUrlProvider;
    private readonly Action<string>? _log;

    public AllVillagesImportSettingsStore(
        string projectRoot,
        Func<string> activeAccountNameProvider,
        Func<string> activeServerUrlProvider,
        Action<string>? log = null)
    {
        _projectRoot = projectRoot;
        _activeAccountNameProvider = activeAccountNameProvider;
        _activeServerUrlProvider = activeServerUrlProvider;
        _log = log;
    }

    public Settings Load()
    {
        lock (FileIoLock)
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return Settings.Default;
            }

            try
            {
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions);
                return settings is null
                    ? Settings.Default
                    : settings with
                    {
                        IgnoredPlayers = settings.IgnoredPlayers ?? string.Empty,
                        IgnoredAlliances = settings.IgnoredAlliances ?? string.Empty,
                    };
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[all-villages] could not load import settings: {ex.Message}");
                return Settings.Default;
            }
        }
    }

    public void Save(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (FileIoLock)
        {
            var path = GetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
            _log?.Invoke("[all-villages] saved import settings.");
        }
    }

    private string GetPath()
    {
        var account = _activeAccountNameProvider() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(account))
        {
            throw new InvalidOperationException("No active account is available for All villages settings.");
        }

        return AccountStoragePaths.AllVillagesImportSettingsPath(_projectRoot, account, _activeServerUrlProvider());
    }
}
