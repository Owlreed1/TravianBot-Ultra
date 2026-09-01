using System.Globalization;

namespace TbotUltra.Worker.Infrastructure;

public sealed record AutomationLogMetadata(
    string? Account,
    string? Task,
    string? Village,
    int? CoordX,
    int? CoordY);

/// <summary>
/// Carries log identity through one asynchronous automation flow. The Desktop sink captures the
/// metadata separately so existing machine-readable log payloads remain unchanged.
/// </summary>
public static class AutomationLogContext
{
    private sealed class State
    {
        public State(AutomationLogMetadata metadata, State? parent)
        {
            Metadata = metadata;
            Parent = parent;
        }

        public AutomationLogMetadata Metadata { get; set; }
        public State? Parent { get; }
    }

    private static readonly AsyncLocal<State?> CurrentState = new();

    public static IDisposable BeginScope(
        string? account = null,
        string? task = null,
        string? village = null,
        string? villageKey = null)
    {
        var parent = CurrentState.Value;
        var inherited = parent?.Metadata;
        var coordinates = ParseCoordinates(villageKey);
        var state = new State(
            new AutomationLogMetadata(
                Normalize(account) ?? inherited?.Account,
                Normalize(task) ?? inherited?.Task,
                Normalize(village) ?? inherited?.Village,
                coordinates.X ?? inherited?.CoordX,
                coordinates.Y ?? inherited?.CoordY),
            parent);
        CurrentState.Value = state;
        return new Scope(state);
    }

    public static AutomationLogMetadata? Capture() => CurrentState.Value?.Metadata;

    public static void UpdateVillage(string? village, int? coordX, int? coordY)
    {
        var normalizedVillage = Normalize(village);
        for (var state = CurrentState.Value; state is not null; state = state.Parent)
        {
            var villageChanged = normalizedVillage is not null
                && !string.Equals(normalizedVillage, state.Metadata.Village, StringComparison.OrdinalIgnoreCase);
            state.Metadata = state.Metadata with
            {
                Village = normalizedVillage ?? state.Metadata.Village,
                CoordX = villageChanged ? coordX : coordX ?? state.Metadata.CoordX,
                CoordY = villageChanged ? coordY : coordY ?? state.Metadata.CoordY,
            };
        }
    }

    public static void UpdateVillageName(string? village)
        => UpdateVillage(village, null, null);

    public static void UpdateVillageCoordinates(int? coordX, int? coordY)
        => UpdateVillage(null, coordX, coordY);

    public static string FormatForHuman(string message, AutomationLogMetadata? metadata)
    {
        message ??= string.Empty;
        if (message.StartsWith("[browser-trace", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[context account=", StringComparison.Ordinal))
        {
            return message;
        }

        metadata ??= new AutomationLogMetadata(null, null, null, null, null);
        var coordinates = metadata.CoordX.HasValue && metadata.CoordY.HasValue
            ? $"{metadata.CoordX.Value.ToString(CultureInfo.InvariantCulture)}|{metadata.CoordY.Value.ToString(CultureInfo.InvariantCulture)}"
            : "-";
        return message
            + $" [context account='{Escape(metadata.Account)}' task='{Escape(metadata.Task)}'"
            + $" village='{Escape(metadata.Village)}' xy='{coordinates}']";
    }

    private static (int? X, int? Y) ParseCoordinates(string? villageKey)
    {
        if (string.IsNullOrWhiteSpace(villageKey)
            || !villageKey.StartsWith("xy:", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var parts = villageKey[3..].Split('|', StringSplitOptions.TrimEntries);
        return parts.Length == 2
               && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
               && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            ? (x, y)
            : (null, null);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Escape(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim()
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace("'", "''", StringComparison.Ordinal);

    private sealed class Scope : IDisposable
    {
        private State? _state;

        public Scope(State state) => _state = state;

        public void Dispose()
        {
            var state = Interlocked.Exchange(ref _state, null);
            if (state is not null && ReferenceEquals(CurrentState.Value, state))
            {
                CurrentState.Value = state.Parent;
            }
        }
    }
}
