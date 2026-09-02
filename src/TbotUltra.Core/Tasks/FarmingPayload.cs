using TbotUltra.Core.Configuration;

namespace TbotUltra.Core.Tasks;

// Note: the dispatch delay is intentionally NOT carried in this payload. It is a live setting read
// from BotOptions at execution time, so changing it while the continuous loop runs takes effect on
// the next send cycle instead of being frozen at enqueue time.
//
// FarmListIds carries the stable Travian list ids (lid) for the selected lists so matching survives
// a village/list rename. FarmListNames is still carried for display and as a fallback when ids are
// unavailable (e.g. selections saved before lids existed).
public sealed record FarmingPayload(
    IReadOnlyList<string> FarmListNames,
    IReadOnlyList<string>? FarmListIds = null,
    bool MoveLosses = false,
    string? LossDestinationListId = null,
    string? LossDestinationListName = null,
    string? LossDestinationBaseName = null,
    bool MoveRedLosses = false,
    string? RedLossDestinationListId = null,
    string? RedLossDestinationListName = null,
    string? RedLossDestinationBaseName = null,
    bool MoveYellowLosses = false,
    string? YellowLossDestinationListId = null,
    string? YellowLossDestinationListName = null,
    string? YellowLossDestinationBaseName = null)
{
    public static bool TryFromDictionary(IReadOnlyDictionary<string, string> payload, out FarmingPayload? result)
    {
        var names = ParseList(ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmListNames));
        var ids = ParseList(ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmListIds));
        var legacyMove = ReadBool(payload, BotOptionPayloadKeys.ContinuousFarmMoveLosses);
        var legacyListId = ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmLossDestinationListId);
        var legacyListName = ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmLossDestinationListName);
        var legacyBaseName = ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmLossDestinationBaseName);
        result = new FarmingPayload(
            names,
            ids,
            legacyMove,
            legacyListId,
            legacyListName,
            legacyBaseName,
            ReadBool(payload, BotOptionPayloadKeys.ContinuousFarmMoveRedLosses, legacyMove),
            ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationListId) ?? legacyListId,
            ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationListName) ?? legacyListName,
            ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationBaseName) ?? legacyBaseName,
            ReadBool(payload, BotOptionPayloadKeys.ContinuousFarmMoveYellowLosses, legacyMove),
            ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationListId) ?? legacyListId,
            ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationListName) ?? legacyListName,
            ReadTrimmed(payload, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationBaseName) ?? legacyBaseName);
        return true;
    }

    public Dictionary<string, string> ToDictionary()
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BotOptionPayloadKeys.ContinuousFarmListNames] = JoinDistinct(FarmListNames),
        };

        var ids = JoinDistinct(FarmListIds ?? []);
        if (!string.IsNullOrWhiteSpace(ids))
        {
            dictionary[BotOptionPayloadKeys.ContinuousFarmListIds] = ids;
        }

        if (MoveLosses)
        {
            dictionary[BotOptionPayloadKeys.ContinuousFarmMoveLosses] = bool.TrueString;
        }
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmLossDestinationListId, LossDestinationListId);
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmLossDestinationListName, LossDestinationListName);
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmLossDestinationBaseName, LossDestinationBaseName);
        if (MoveRedLosses || RedLossDestinationListId is not null || RedLossDestinationListName is not null || RedLossDestinationBaseName is not null)
            dictionary[BotOptionPayloadKeys.ContinuousFarmMoveRedLosses] = MoveRedLosses.ToString();
        if (MoveYellowLosses || YellowLossDestinationListId is not null || YellowLossDestinationListName is not null || YellowLossDestinationBaseName is not null)
            dictionary[BotOptionPayloadKeys.ContinuousFarmMoveYellowLosses] = MoveYellowLosses.ToString();
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationListId, RedLossDestinationListId);
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationListName, RedLossDestinationListName);
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmRedLossDestinationBaseName, RedLossDestinationBaseName);
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationListId, YellowLossDestinationListId);
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationListName, YellowLossDestinationListName);
        AddIfPresent(dictionary, BotOptionPayloadKeys.ContinuousFarmYellowLossDestinationBaseName, YellowLossDestinationBaseName);

        return dictionary;
    }

    /// <summary>
    /// Replaces only the farm-list selection in an already queued automatic farming payload.
    /// The current village and other execution settings stay intact, while a stale list cursor
    /// is removed so the updated selection is evaluated as one complete round.
    /// </summary>
    public Dictionary<string, string> ApplySelectionTo(IReadOnlyDictionary<string, string> existingPayload)
    {
        ArgumentNullException.ThrowIfNull(existingPayload);

        var payload = new Dictionary<string, string>(existingPayload, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in ToDictionary())
        {
            payload[pair.Key] = pair.Value;
        }

        if (FarmListIds is not { Count: > 0 })
        {
            payload.Remove(BotOptionPayloadKeys.ContinuousFarmListIds);
        }

        payload.Remove(BotOptionPayloadKeys.ContinuousFarmNextListIndex);
        return payload;
    }

    private static void AddIfPresent(Dictionary<string, string> payload, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            payload[key] = value.Trim();
        }
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;

    private static bool ReadBool(IReadOnlyDictionary<string, string> payload, string key, bool fallback)
        => payload.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static string JoinDistinct(IReadOnlyList<string> values)
    {
        return string.Join(",", values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string? ReadTrimmed(IReadOnlyDictionary<string, string> payload, string key)
    {
        return payload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static IReadOnlyList<string> ParseList(string? raw)
    {
        return (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
