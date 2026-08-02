using System.Text.Json;
using System.IO;
using TbotUltra.Core.Accounts;

namespace TbotUltra.Desktop.Services;

public sealed record FarmListDispatchState(DateTimeOffset? LastSentAtUtc, bool Failed);

public static class FarmListDispatchStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static string CreateKey(string? listId, string? listName)
    {
        if (!string.IsNullOrWhiteSpace(listId))
        {
            return $"lid:{listId.Trim()}";
        }

        return $"name:{(listName ?? string.Empty).Trim()}";
    }

    public static IReadOnlyDictionary<string, FarmListDispatchState> Load(string projectRoot, string accountName)
    {
        var path = AccountStoragePaths.FarmListDispatchStatePath(projectRoot, accountName);
        if (!File.Exists(path))
        {
            return new Dictionary<string, FarmListDispatchState>(StringComparer.OrdinalIgnoreCase);
        }

        var file = JsonSerializer.Deserialize<DispatchStateFile>(File.ReadAllText(path));
        return (file?.Lists ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new FarmListDispatchState(group.Last().LastSentAtUtc, group.Last().Failed),
                StringComparer.OrdinalIgnoreCase);
    }

    public static void Save(string projectRoot, string accountName, IReadOnlyDictionary<string, FarmListDispatchState> states)
    {
        var path = AccountStoragePaths.FarmListDispatchStatePath(projectRoot, accountName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var file = new DispatchStateFile(
            states
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new DispatchStateEntry(pair.Key, pair.Value.LastSentAtUtc, pair.Value.Failed))
                .ToList());
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(file, SerializerOptions));
    }

    private sealed record DispatchStateFile(List<DispatchStateEntry> Lists);

    private sealed record DispatchStateEntry(string Key, DateTimeOffset? LastSentAtUtc, bool Failed);
}
