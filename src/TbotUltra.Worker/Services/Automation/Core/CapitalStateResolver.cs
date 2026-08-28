using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

// Travian permits exactly one capital per account. If cached observations disagree, no observation is
// safe enough to present as capital until the next live player-profile scan resolves the conflict.
internal static class CapitalStateResolver
{
    internal static bool RequiresProfileVerification(IReadOnlyList<Village> villages)
        => villages.Count(village => village.IsCapital == true) != 1;

    internal static IReadOnlyDictionary<string, bool?> NormalizeCachedCapitalCandidates(
        IReadOnlyDictionary<string, bool?> candidates)
    {
        if (candidates.Values.Count(isCapital => isCapital == true) <= 1)
        {
            return candidates;
        }

        return candidates.ToDictionary(candidate => candidate.Key, _ => (bool?)null, StringComparer.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<Village> ClearConflictingCapitalFlags(IReadOnlyList<Village> villages)
    {
        var capitalCount = villages.Count(village => village.IsCapital == true);
        if (capitalCount <= 1)
        {
            return villages;
        }

        return villages
            .Select(village => village.IsCapital == true ? village with { IsCapital = null } : village)
            .ToList();
    }

    internal static IReadOnlyList<Village> ApplyVerifiedCapital(
        IReadOnlyList<Village> villages,
        int coordX,
        int coordY)
    {
        var matches = villages.Count(village => village.CoordX == coordX && village.CoordY == coordY);
        if (matches != 1)
        {
            return villages;
        }

        return villages
            .Select(village => village with { IsCapital = village.CoordX == coordX && village.CoordY == coordY })
            .ToList();
    }

    internal static IReadOnlyList<Village> ApplyDefinitiveResourceFieldEvidence(
        IReadOnlyList<Village> villages,
        IReadOnlyList<ResourceField> resourceFields,
        int coordX,
        int coordY)
    {
        return resourceFields.Any(field => field.Level > 10)
            ? ApplyVerifiedCapital(villages, coordX, coordY)
            : villages;
    }
}
