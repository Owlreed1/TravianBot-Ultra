using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

public sealed record TroopEvasionDueWork(
    string VillageKey,
    IncomingAttack Attack,
    DateTimeOffset DueAtUtc,
    string Milestone);

public static class TroopEvasionScheduler
{
    public static TroopEvasionDueWork? SelectMostUrgent(
        IEnumerable<(string VillageKey, IncomingAttack Attack)> attacks,
        IReadOnlyDictionary<string, TroopEvasionVillageSettings> settings,
        IReadOnlyDictionary<string, TroopEvasionProtectionState> protections,
        IReadOnlySet<string> completedMilestones,
        DateTimeOffset nowUtc,
        int leadTimeMinutes)
    {
        return attacks
            .Where(item => item.Attack.ArrivalAtUtc > nowUtc)
            .Where(item => settings.TryGetValue(item.VillageKey, out var config) && config.Enabled)
            .Where(item => !protections.TryGetValue(item.VillageKey, out var protection)
                           || item.Attack.ArrivalAtUtc > protection.ProtectedThroughUtc)
            .Select(item => CreateNextDue(item.VillageKey, item.Attack, completedMilestones, nowUtc, leadTimeMinutes))
            .Where(item => item is not null && item.DueAtUtc <= nowUtc)
            .OrderBy(item => item!.Attack.ArrivalAtUtc)
            .ThenBy(item => item!.VillageKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static TroopEvasionProtectionState CreateProtection(
        string villageKey,
        DateTimeOffset triggeringArrivalUtc,
        DateTimeOffset confirmedAtUtc,
        int protectionWindowMinutes)
        => new(villageKey, triggeringArrivalUtc, triggeringArrivalUtc.AddMinutes(protectionWindowMinutes), confirmedAtUtc);

    public static string MilestoneKey(string villageKey, IncomingAttack attack, string milestone)
        => $"{villageKey}|{attack.Id}|{attack.ArrivalAtUtc.UtcTicks}|{milestone}";

    private static TroopEvasionDueWork? CreateNextDue(
        string villageKey,
        IncomingAttack attack,
        IReadOnlySet<string> completed,
        DateTimeOffset nowUtc,
        int leadMinutes)
    {
        var candidates = new[]
        {
            (Name: "lead", At: attack.ArrivalAtUtc.AddMinutes(-leadMinutes)),
            (Name: "retry-1m", At: attack.ArrivalAtUtc.AddMinutes(-1)),
            (Name: "retry-30s", At: attack.ArrivalAtUtc.AddSeconds(-30)),
        };
        var completedIndex = -1;
        for (var index = 0; index < candidates.Length; index++)
            if (completed.Contains(MilestoneKey(villageKey, attack, candidates[index].Name))) completedIndex = index;
        return candidates
            .Select((candidate, index) => (candidate, index))
            .Where(item => item.index > completedIndex && item.candidate.At <= nowUtc)
            .OrderByDescending(item => item.index)
            .Select(item => new TroopEvasionDueWork(villageKey, attack, item.candidate.At, item.candidate.Name))
            .FirstOrDefault();
    }
}
