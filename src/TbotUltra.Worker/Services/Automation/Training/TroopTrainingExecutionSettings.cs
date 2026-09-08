using TbotUltra.Core.Tasks;

namespace TbotUltra.Worker.Services;

/// <summary>Captures the target village's saved rules for one execution and guards the submit boundary.</summary>
public static class TroopTrainingExecutionSettings
{
    private sealed record State(Func<TroopTrainingPayload?> Read, TroopTrainingPayload? Snapshot, Action<string> Log);
    private static readonly AsyncLocal<State?> Current = new();

    public static IDisposable BeginScope(Func<TroopTrainingPayload?> read, Action<string> log)
    {
        var previous = Current.Value;
        var snapshot = read();
        Current.Value = new State(read, snapshot, log);
        log("[troops] captured latest saved training rules for the task village.");
        return new Scope(previous);
    }

    public static Dictionary<string, string> MergePayload(
        IReadOnlyDictionary<string, string> queued, TroopTrainingPayload? saved)
    {
        var result = new Dictionary<string, string>(queued, StringComparer.OrdinalIgnoreCase);
        if (saved is not null)
        {
            foreach (var pair in saved.ToDictionary())
                result[pair.Key] = pair.Value;
        }
        return result;
    }

    internal static Dictionary<string, string> ResolvePayload(IReadOnlyDictionary<string, string> queued)
        => MergePayload(queued, Current.Value?.Snapshot);

    public static void VerifyBeforeSubmit()
    {
        var state = Current.Value;
        if (state is null || Equals(state.Snapshot, state.Read()))
            return;

        state.Log("[troops] saved training rules changed during preparation; no Train click made. Retrying with the latest selection.");
        throw new TaskWaitException(1, "Troop training settings changed before submit; retry with latest settings.", "training_settings_changed");
    }

    private sealed class Scope(State? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
