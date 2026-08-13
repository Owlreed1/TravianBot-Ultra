namespace TbotUltra.Core.Travian;

public sealed record TroopTrainingCost(int Wood, int Clay, int Iron, int Crop);

public static class TroopCatalog
{
    private static readonly IReadOnlyList<string> RomanTroops = ["Legionnaire", "Praetorian", "Imperian", "Equites Legati", "Equites Imperatoris", "Equites Caesaris", "Ram", "Fire Catapult", "Senator", "Settler"];
    private static readonly IReadOnlyList<string> GaulTroops = ["Phalanx", "Swordsman", "Pathfinder", "Theutates Thunder", "Druidrider", "Haeduan", "Ram", "Trebuchet", "Chieftain", "Settler"];
    private static readonly IReadOnlyList<string> TeutonTroops = ["Clubswinger", "Spearman", "Axeman", "Scout", "Paladin", "Teutonic Knight", "Ram", "Catapult", "Chief", "Settler"];
    private static readonly IReadOnlyList<string> HunTroops = ["Mercenary", "Bowman", "Spotter", "Steppe Rider", "Marksman", "Marauder", "Ram", "Catapult", "Logades", "Settler"];
    private static readonly IReadOnlyList<string> EgyptianTroops = ["Slave Militia", "Ash Warden", "Khopesh Warrior", "Sopdu Explorer", "Anhur Guard", "Resheph Chariot", "Ram", "Stone Catapult", "Nomarch", "Settler"];
    private static readonly IReadOnlyList<string> SpartanTroops = ["Hoplite", "Sentinel", "Shieldsman", "Twinsteel Therion", "Elpida Rider", "Corinthian Crusher", "Ram", "Ballista", "Ephor", "Settler"];
    private static readonly IReadOnlyList<string> FallbackTroops = ["Infantry 1", "Infantry 2", "Scout", "Cavalry 1", "Cavalry 2", "Ram", "Catapult"];

    private static readonly IReadOnlyList<IReadOnlyList<string>> AllTribeTroops =
    [
        RomanTroops,
        GaulTroops,
        TeutonTroops,
        HunTroops,
        EgyptianTroops,
        SpartanTroops,
        FallbackTroops,
    ];

    // Standard Official Travian: Legends recruitment costs. The live building page is checked
    // against these values before submit so special-world balance changes fail visibly instead
    // of making the pre-navigation resource gate silently inaccurate.
    private static readonly IReadOnlyDictionary<int, TroopTrainingCost> TrainingCostsByUnitId =
        new Dictionary<int, TroopTrainingCost>
        {
            [1] = new(120, 100, 150, 30), [2] = new(100, 130, 160, 70),
            [3] = new(150, 160, 210, 80), [4] = new(140, 160, 20, 40),
            [5] = new(550, 440, 320, 100), [6] = new(550, 640, 800, 180),
            [7] = new(900, 360, 500, 70), [8] = new(950, 1350, 600, 90),
            [11] = new(95, 75, 40, 40), [12] = new(145, 70, 85, 40),
            [13] = new(130, 120, 170, 70), [14] = new(160, 100, 50, 50),
            [15] = new(370, 270, 290, 75), [16] = new(450, 515, 480, 80),
            [17] = new(1000, 300, 350, 70), [18] = new(900, 1200, 600, 60),
            [21] = new(100, 130, 55, 30), [22] = new(140, 150, 185, 60),
            [23] = new(170, 150, 20, 40), [24] = new(350, 450, 230, 60),
            [25] = new(360, 330, 280, 120), [26] = new(500, 620, 675, 170),
            [27] = new(950, 555, 330, 75), [28] = new(960, 1450, 630, 90),
            [51] = new(45, 60, 30, 15), [52] = new(115, 100, 145, 60),
            [53] = new(170, 180, 220, 80), [54] = new(170, 150, 20, 40),
            [55] = new(360, 330, 280, 120), [56] = new(450, 560, 610, 180),
            [57] = new(995, 575, 340, 80), [58] = new(980, 1510, 660, 100),
            [61] = new(130, 80, 40, 40), [62] = new(140, 110, 60, 60),
            [63] = new(170, 150, 20, 40), [64] = new(290, 370, 190, 45),
            [65] = new(320, 350, 330, 50), [66] = new(450, 560, 610, 140),
            [67] = new(1060, 330, 360, 70), [68] = new(950, 1280, 620, 60),
            [71] = new(110, 185, 110, 45), [72] = new(185, 150, 35, 80),
            [73] = new(145, 95, 245, 55), [74] = new(130, 200, 400, 70),
            [75] = new(555, 445, 330, 120), [76] = new(660, 495, 995, 175),
            [77] = new(1040, 350, 400, 80), [78] = new(950, 1350, 600, 100),
        };

    /// <summary>
    /// True when the value maps to a specific tribe's troop list. Unknown/empty values fall back
    /// to the generic list in <see cref="ResolveTroopTypesForTribe(string?)"/> — callers that would
    /// overwrite real troop data should check this first instead of trusting the fallback.
    /// </summary>
    public static bool IsKnownTribe(string? tribe)
    {
        var value = (tribe ?? string.Empty).Trim().ToLowerInvariant();
        return value.Contains("roman")
            || value.Contains("gaul")
            || value.Contains("teuton")
            || value.Contains("hun")
            || value.Contains("egypt")
            || value.Contains("spartan");
    }

    /// <summary>
    /// Maps Travian's numeric tribe id (used in DOM classes like "tribe8_medium" and on the profile
    /// villages table) to the tribe name this codebase uses elsewhere. Returns null for ids we do not
    /// play — 4 (Nature) and 5 (Natars) are NPC tribes and never belong to a player village.
    /// </summary>
    public static string? ResolveTribeFromTravianId(int tribeId)
    {
        return tribeId switch
        {
            1 => "Romans",
            2 => "Teutons",
            3 => "Gauls",
            6 => "Egyptians",
            7 => "Huns",
            8 => "Spartans",
            _ => null,
        };
    }

    public static IReadOnlyList<string> ResolveTroopTypesForTribe(string? tribe)
    {
        var value = (tribe ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("roman"))
        {
            return RomanTroops;
        }

        if (value.Contains("gaul"))
        {
            return GaulTroops;
        }

        if (value.Contains("teuton"))
        {
            return TeutonTroops;
        }

        if (value.Contains("hun"))
        {
            return HunTroops;
        }

        if (value.Contains("egypt"))
        {
            return EgyptianTroops;
        }

        if (value.Contains("spartan"))
        {
            return SpartanTroops;
        }

        return FallbackTroops;
    }

    public static IReadOnlyList<string> ResolveTroopTypesForTribe(string? tribe, TroopTrainingBuildingType buildingType)
    {
        var allTroops = ResolveTroopTypesForTribe(tribe);
        var value = (tribe ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsKnownTribe(tribe))
        {
            // The 7-item fallback list has its own layout (no chief/settler tail), so the generic
            // 3/3/2 split below would put Ram in the Stable and leave the Workshop with only Catapult.
            return buildingType switch
            {
                TroopTrainingBuildingType.Barracks => allTroops.Take(3).ToList(),
                TroopTrainingBuildingType.Stable => allTroops.Skip(3).Take(2).ToList(),
                TroopTrainingBuildingType.Workshop => allTroops.Skip(5).Take(2).ToList(),
                _ => [],
            };
        }

        if (value.Contains("teuton"))
        {
            return buildingType switch
            {
                TroopTrainingBuildingType.Barracks => allTroops.Take(4).ToList(),
                TroopTrainingBuildingType.Stable => allTroops.Skip(4).Take(2).ToList(),
                TroopTrainingBuildingType.Workshop => allTroops.Skip(6).Take(2).ToList(),
                _ => [],
            };
        }

        if (value.Contains("gaul"))
        {
            return buildingType switch
            {
                TroopTrainingBuildingType.Barracks => allTroops.Take(2).ToList(),
                TroopTrainingBuildingType.Stable => allTroops.Skip(2).Take(4).ToList(),
                TroopTrainingBuildingType.Workshop => allTroops.Skip(6).Take(2).ToList(),
                _ => [],
            };
        }

        return buildingType switch
        {
            TroopTrainingBuildingType.Barracks => allTroops.Take(3).ToList(),
            TroopTrainingBuildingType.Stable => allTroops.Skip(3).Take(3).ToList(),
            TroopTrainingBuildingType.Workshop => allTroops.Skip(6).Take(2).ToList(),
            _ => [],
        };
    }

    public static bool IsTroopTypeAllowedForBuilding(string? tribe, string? troopType, TroopTrainingBuildingType buildingType)
    {
        var normalized = Normalize(troopType);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return ResolveTroopTypesForTribe(tribe, buildingType)
            .Any(item => string.Equals(Normalize(item), normalized, StringComparison.Ordinal));
    }

    public static int? ResolveTroopIndex(string? troopType)
    {
        var normalized = Normalize(troopType);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        foreach (var troopSet in AllTribeTroops)
        {
            for (var index = 0; index < troopSet.Count; index++)
            {
                if (string.Equals(Normalize(troopSet[index]), normalized, StringComparison.Ordinal))
                {
                    return index + 1;
                }
            }
        }

        return null;
    }

    public static int? ResolveTravianUnitId(string? tribe, string? troopType)
    {
        var normalizedTroop = Normalize(troopType);
        if (string.IsNullOrWhiteSpace(normalizedTroop))
        {
            return null;
        }

        var baseId = ResolveTribeUnitBaseId(tribe);
        if (baseId is null)
        {
            return null;
        }

        var troopSet = ResolveTroopTypesForTribe(tribe);
        for (var index = 0; index < troopSet.Count; index++)
        {
            if (string.Equals(Normalize(troopSet[index]), normalizedTroop, StringComparison.Ordinal))
            {
                return baseId.Value + index;
            }
        }

        return null;
    }

    public static bool TryResolveTrainingCost(string? tribe, string? troopType, out TroopTrainingCost cost)
    {
        var unitId = ResolveTravianUnitId(tribe, troopType);
        if (unitId.HasValue && TrainingCostsByUnitId.TryGetValue(unitId.Value, out var resolved))
        {
            cost = resolved;
            return true;
        }

        cost = default!;
        return false;
    }

    private static int? ResolveTribeUnitBaseId(string? tribe)
    {
        var value = (tribe ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("roman"))
        {
            return 1;
        }

        if (value.Contains("teuton"))
        {
            return 11;
        }

        if (value.Contains("gaul"))
        {
            return 21;
        }

        if (value.Contains("egypt"))
        {
            return 51;
        }

        if (value.Contains("hun"))
        {
            return 61;
        }

        if (value.Contains("spartan"))
        {
            return 71;
        }

        return null;
    }

    private static string Normalize(string? value) =>
        string.Concat((value ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch))).Trim().ToLowerInvariant();
}
