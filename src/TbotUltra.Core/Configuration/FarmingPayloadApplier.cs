namespace TbotUltra.Core.Configuration;

internal sealed record FarmingPayloadValues(
    List<string> ListNames,
    List<string> ListIds,
    int DispatchDelayMinMinutes,
    int DispatchDelayMaxMinutes,
    string SendMode,
    string TownHallCelebrationMode,
    bool DeactivateRedLosses,
    bool DeactivateYellowLosses,
    bool DeactivateRedOasisLosses,
    bool DeactivateYellowOasisLosses,
    bool MoveRedLosses,
    bool MoveYellowLosses,
    string RedLossDestinationListId,
    string RedLossDestinationListName,
    string RedLossDestinationBaseName,
    string YellowLossDestinationListId,
    string YellowLossDestinationListName,
    string YellowLossDestinationBaseName,
    int NextListIndex);

internal static class FarmingPayloadApplier
{
    internal static FarmingPayloadValues Apply(BotOptions source, IReadOnlyDictionary<string, string>? payload)
    {
        var result = new FarmingPayloadValues(
            source.ContinuousFarmListNames,
            source.ContinuousFarmListIds,
            source.ContinuousFarmDispatchDelayMinMinutes,
            source.ContinuousFarmDispatchDelayMaxMinutes,
            source.ContinuousFarmSendMode,
            source.TownHallCelebrationMode,
            source.ContinuousFarmDeactivateRedLosses,
            source.ContinuousFarmDeactivateYellowLosses,
            source.ContinuousFarmDeactivateRedOasisLosses,
            source.ContinuousFarmDeactivateYellowOasisLosses,
            source.ContinuousFarmMoveRedLosses,
            source.ContinuousFarmMoveYellowLosses,
            source.ContinuousFarmRedLossDestinationListId,
            source.ContinuousFarmRedLossDestinationListName,
            source.ContinuousFarmRedLossDestinationBaseName,
            source.ContinuousFarmYellowLossDestinationListId,
            source.ContinuousFarmYellowLossDestinationListName,
            source.ContinuousFarmYellowLossDestinationBaseName,
            source.ContinuousFarmNextListIndex);

        if (payload is null)
            return result;

        ApplyLegacyFallbacks(payload, ref result);

        foreach (var pair in payload)
        {
            var key = pair.Key.Trim();
            var value = pair.Value.Trim();
            if (key.Length == 0 || value.Length == 0)
                continue;

            if (key.Equals(BotOptionPayloadKeys.ContinuousFarmListNames, StringComparison.OrdinalIgnoreCase))
                result = result with { ListNames = ParseList(value) };
            else if (key.Equals(BotOptionPayloadKeys.ContinuousFarmListIds, StringComparison.OrdinalIgnoreCase))
                result = result with { ListIds = ParseList(value) };
            else if (TryReadInt(key, value, BotOptionPayloadKeys.ContinuousFarmDispatchDelayMinMinutes, out var delayMin))
                result = result with { DispatchDelayMinMinutes = FarmingDefaults.NormalizeDispatchDelayMinMinutes(delayMin) };
            else if (TryReadInt(key, value, BotOptionPayloadKeys.ContinuousFarmDispatchDelayMaxMinutes, out var delayMax))
                result = result with { DispatchDelayMaxMinutes = FarmingDefaults.NormalizeDispatchDelayMaxMinutes(delayMax) };
            else if (key.Equals(BotOptionPayloadKeys.ContinuousFarmSendMode, StringComparison.OrdinalIgnoreCase))
                result = result with { SendMode = FarmingDefaults.NormalizeSendMode(value) };
            else if (key.Equals(BotOptionPayloadKeys.TownHallCelebrationMode, StringComparison.OrdinalIgnoreCase))
                result = result with { TownHallCelebrationMode = TownHallCelebrationDefaults.NormalizeMode(value) };
            else if (TryReadBool(key, value, BotOptionPayloadKeys.ContinuousFarmDeactivateRedLosses, out var deactivateRed))
                result = result with { DeactivateRedLosses = deactivateRed };
            else if (TryReadBool(key, value, BotOptionPayloadKeys.ContinuousFarmDeactivateYellowLosses, out var deactivateYellow))
                result = result with { DeactivateYellowLosses = deactivateYellow };
            else if (TryReadBool(key, value, BotOptionPayloadKeys.ContinuousFarmDeactivateRedOasisLosses, out var deactivateRedOasis))
                result = result with { DeactivateRedOasisLosses = deactivateRedOasis };
            else if (TryReadBool(key, value, BotOptionPayloadKeys.ContinuousFarmDeactivateYellowOasisLosses, out var deactivateYellowOasis))
                result = result with { DeactivateYellowOasisLosses = deactivateYellowOasis };
            else if (TryReadBool(key, value, BotOptionPayloadKeys.ContinuousFarmMoveRedLosses, out var moveRed))
                result = result with { MoveRedLosses = moveRed };
            else if (TryReadBool(key, value, BotOptionPayloadKeys.ContinuousFarmMoveYellowLosses, out var moveYellow))
                result = result with { MoveYellowLosses = moveYellow };
            else if (key.Equals(BotOptionPayloadKeys.ContinuousFarmRedLossDestinationListId, StringComparison.OrdinalIgnoreCase))
                result = result with { RedLossDestinationListId = value };
            else if (key.Equals(BotOptionPayloadKeys.ContinuousFarmRedLossDestinationListName, StringComparison.OrdinalIgnoreCase))
                result = result with { RedLossDestinationListName = value };
            else if (key.Equals(BotOptionPayloadKeys.ContinuousFarmRedLossDestinationBaseName, StringComparison.OrdinalIgnoreCase))
                result = result with { RedLossDestinationBaseName = value };
            else if (key.Equals(BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationListId, StringComparison.OrdinalIgnoreCase))
                result = result with { YellowLossDestinationListId = value };
            else if (key.Equals(BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationListName, StringComparison.OrdinalIgnoreCase))
                result = result with { YellowLossDestinationListName = value };
            else if (key.Equals(BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationBaseName, StringComparison.OrdinalIgnoreCase))
                result = result with { YellowLossDestinationBaseName = value };
            else if (TryReadInt(key, value, BotOptionPayloadKeys.ContinuousFarmNextListIndex, out var nextIndex))
                result = result with { NextListIndex = Math.Max(0, nextIndex) };
        }

        return result with
        {
            MoveRedLosses = result.DeactivateRedLosses && result.MoveRedLosses,
            MoveYellowLosses = result.DeactivateYellowLosses && result.MoveYellowLosses,
        };
    }

    private static void ApplyLegacyFallbacks(IReadOnlyDictionary<string, string> payload, ref FarmingPayloadValues result)
    {
        if (TryGetBool(payload, BotOptionPayloadKeys.ContinuousFarmDeactivateLosses, out var deactivateLosses))
        {
            if (!ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmDeactivateRedLosses))
                result = result with { DeactivateRedLosses = deactivateLosses };
            if (!ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmDeactivateYellowLosses))
                result = result with { DeactivateYellowLosses = deactivateLosses };
        }

        if (TryGetBool(payload, BotOptionPayloadKeys.ContinuousFarmDeactivateOasisLosses, out var deactivateOasisLosses))
        {
            if (!ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmDeactivateRedOasisLosses))
                result = result with { DeactivateRedOasisLosses = deactivateOasisLosses };
            if (!ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmDeactivateYellowOasisLosses))
                result = result with { DeactivateYellowOasisLosses = deactivateOasisLosses };
        }

        if (TryGetBool(payload, BotOptionPayloadKeys.ContinuousFarmMoveLosses, out var moveLosses))
        {
            if (!ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmMoveRedLosses))
                result = result with { MoveRedLosses = moveLosses };
            if (!ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmMoveYellowLosses))
                result = result with { MoveYellowLosses = moveLosses };
        }

        var legacyListId = ReadLegacyString(payload, BotOptionPayloadKeys.ContinuousFarmLossDestinationListId);
        var legacyListName = ReadLegacyString(payload, BotOptionPayloadKeys.ContinuousFarmLossDestinationListName);
        var legacyBaseName = ReadLegacyString(payload, BotOptionPayloadKeys.ContinuousFarmLossDestinationBaseName);
        result = result with
        {
            RedLossDestinationListId = ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationListId) || legacyListId is null ? result.RedLossDestinationListId : legacyListId,
            YellowLossDestinationListId = ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationListId) || legacyListId is null ? result.YellowLossDestinationListId : legacyListId,
            RedLossDestinationListName = ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationListName) || legacyListName is null ? result.RedLossDestinationListName : legacyListName,
            YellowLossDestinationListName = ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationListName) || legacyListName is null ? result.YellowLossDestinationListName : legacyListName,
            RedLossDestinationBaseName = ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationBaseName) || legacyBaseName is null ? result.RedLossDestinationBaseName : legacyBaseName,
            YellowLossDestinationBaseName = ContainsKey(payload, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationBaseName) || legacyBaseName is null ? result.YellowLossDestinationBaseName : legacyBaseName,
        };
    }

    private static string? ReadLegacyString(IReadOnlyDictionary<string, string> payload, string key)
    {
        var value = payload.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool ContainsKey(IReadOnlyDictionary<string, string> payload, string key)
        => payload.Keys.Any(candidate => candidate.Equals(key, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetBool(IReadOnlyDictionary<string, string> payload, string key, out bool parsed)
    {
        var value = payload.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
        return bool.TryParse(value, out parsed);
    }

    private static List<string> ParseList(string value)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool TryReadInt(string key, string value, string expected, out int parsed)
    {
        parsed = 0;
        return key.Equals(expected, StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out parsed);
    }

    private static bool TryReadBool(string key, string value, string expected, out bool parsed)
    {
        parsed = false;
        return key.Equals(expected, StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out parsed);
    }
}
