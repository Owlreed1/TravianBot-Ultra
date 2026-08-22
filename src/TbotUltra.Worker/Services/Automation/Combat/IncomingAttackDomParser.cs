using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

internal static class IncomingAttackDomParser
{
    private static readonly Regex VillageRowRegex = new(
        @"<div\b(?<attrs>[^>]*\bclass\s*=\s*[\""'](?=[^\""']*\blistEntry\b)(?=[^\""']*\bvillage\b)(?=[^\""']*\battack\b)[^\""']*[\""'][^>]*)>(?<body>.*?)(?=<div\b[^>]*\bclass\s*=\s*[\""'][^\""']*\blistEntry\b|</div>\s*</div>\s*</div>)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MovementTableRegex = new(
        @"<table\b(?<attrs>[^>]*\bclass\s*=\s*[\""'][^\""']*\btroop_details\b[^\""']*[\""'][^>]*)>(?<body>.*?)</table>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    internal static IReadOnlyList<IncomingAttackSignal> ParseDorf1Signals(
        string? html,
        string activeVillageName,
        string? activeVillageUrl,
        int? activeCoordX,
        int? activeCoordY,
        DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var signals = new List<IncomingAttackSignal>();
        if (Regex.IsMatch(
                html,
                @"<div\b[^>]*\bclass\s*=\s*[\""'][^\""']*\bvillageInfobox\b[^\""']*\bmovements\b[^\""']*[\""'][^>]*>[\s\S]*?<table\b[^>]*\bid\s*=\s*[\""']movements[\""'][^>]*>[\s\S]*?(?:class\s*=\s*[\""']att1[\""']|\bAttack\b|\bRaid\b)",
                RegexOptions.IgnoreCase))
        {
            signals.Add(new IncomingAttackSignal(
                activeVillageName,
                activeVillageUrl,
                TravianUrls.TryParseNewdid(activeVillageUrl),
                activeCoordX,
                activeCoordY,
                observedAtUtc));
        }

        foreach (Match row in VillageRowRegex.Matches(html))
        {
            var attrs = row.Groups["attrs"].Value;
            var body = row.Groups["body"].Value;
            var villageId = ParseIntAttribute(attrs, "data-did");
            var name = ReadFirstGroup(body, @"<span\b[^>]*\bclass\s*=\s*[\""'][^\""']*\bname\b[^\""']*[\""'][^>]*>(?<value>.*?)</span>");
            var coords = ParseCoordinates(body);
            if (string.IsNullOrWhiteSpace(name) && !villageId.HasValue)
            {
                continue;
            }

            signals.Add(new IncomingAttackSignal(
                name ?? activeVillageName,
                villageId is int did ? $"dorf1.php?newdid={did}" : null,
                villageId,
                coords.X,
                coords.Y,
                observedAtUtc));
        }

        return signals
            .GroupBy(signal => signal.VillageId?.ToString(CultureInfo.InvariantCulture)
                               ?? (signal.CoordX.HasValue && signal.CoordY.HasValue
                                   ? $"{signal.CoordX}|{signal.CoordY}"
                                   : signal.VillageName),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    internal static IReadOnlyList<IncomingAttack> ParseIncomingAttacks(
        string? html,
        string targetVillageName,
        string? targetVillageKey,
        int? targetCoordX,
        int? targetCoordY,
        DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var attacks = new List<IncomingAttack>();
        foreach (Match table in MovementTableRegex.Matches(html))
        {
            var attrs = table.Groups["attrs"].Value;
            var body = table.Groups["body"].Value;
            var remaining = ParseRemainingSeconds(body);
            if (!remaining.HasValue)
            {
                continue;
            }

            var arrival = observedAtUtc.AddSeconds(Math.Max(0, remaining.Value));
            var id = ReadFirstGroup(body, @"markSymbol_(?<value>\d+)")
                     ?? ReadFirstGroup(body, @"markAttackSymbol\((?<value>\d+)\)");
            var role = ReadFirstGroup(body, @"<td\b[^>]*\bclass\s*=\s*[\""'][^\""']*\brole\b[^\""']*[\""'][^>]*>[\s\S]*?<a\b[^>]*>(?<value>.*?)</a>");
            var headline = ReadFirstGroup(body, @"<td\b[^>]*\bclass\s*=\s*[\""'][^\""']*\btroopHeadline\b[^\""']*[\""'][^>]*>[\s\S]*?<a\b(?![^>]*\bmarkAttack\b)[^>]*>(?<value>.*?)</a>");
            var sourceCoords = ParseCoordinates(body);
            var movementType = HasCssClass(attrs, "inRaid")
                ? IncomingAttackMovementType.Raid
                : HasCssClass(attrs, "inAttack")
                    ? IncomingAttackMovementType.Attack
                    : IncomingAttackMovementType.Unknown;

            id ??= CreateFallbackId(targetVillageKey, targetVillageName, role, headline, movementType, arrival);
            attacks.Add(new IncomingAttack(
                id,
                targetVillageName,
                arrival,
                movementType,
                targetVillageKey,
                targetCoordX,
                targetCoordY,
                role,
                headline,
                sourceCoords.X,
                sourceCoords.Y,
                observedAtUtc));
        }

        return attacks.OrderBy(attack => attack.ArrivalAtUtc).ToList();
    }

    internal static bool HasOnlyIncomingFilterActive(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var active = Regex.Matches(
                html,
                @"<button\b(?<attrs>[^>]*\bclass\s*=\s*[\""'][^\""']*\biconFilterActive\b[^\""']*[\""'][^>]*)>(?<body>.*?)</button>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .Select(match => match.Groups["body"].Value)
            .ToList();
        return active.Count == 1
               && Regex.IsMatch(active[0], @"\bsubFilterCategory1\b", RegexOptions.IgnoreCase);
    }

    private static int? ParseRemainingSeconds(string html)
    {
        var raw = ReadFirstGroup(html, @"<span\b[^>]*(?:data-value|value)\s*=\s*[\""'](?<value>\d+)[\""'][^>]*\bclass\s*=\s*[\""'][^\""']*\btimer\b")
                  ?? ReadFirstGroup(html, @"<span\b[^>]*\bclass\s*=\s*[\""'][^\""']*\btimer\b[^\""']*[\""'][^>]*(?:data-value|value)\s*=\s*[\""'](?<value>\d+)[\""']");
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static (int? X, int? Y) ParseCoordinates(string html)
    {
        var x = ReadFirstGroup(html, @"coordinateX[^>]*>[^\d-]*(?<value>-?\d+)");
        var y = ReadFirstGroup(html, @"coordinateY[^>]*>[^\d-]*(?<value>-?\d+)");
        return (
            int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedX) ? parsedX : null,
            int.TryParse(y, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedY) ? parsedY : null);
    }

    private static int? ParseIntAttribute(string attrs, string attribute)
    {
        var value = ReadFirstGroup(attrs, $@"\b{Regex.Escape(attribute)}\s*=\s*[\""'](?<value>\d+)[\""']");
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static string? ReadFirstGroup(string input, string pattern)
    {
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        var text = WebUtility.HtmlDecode(Regex.Replace(match.Groups["value"].Value, "<[^>]+>", " "));
        text = Regex.Replace(text, @"[\u202A-\u202E\u2066-\u2069]", string.Empty);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool HasCssClass(string attrs, string className) =>
        Regex.IsMatch(attrs, $@"\bclass\s*=\s*[\""'][^\""']*\b{Regex.Escape(className)}\b", RegexOptions.IgnoreCase);

    private static string CreateFallbackId(
        string? targetVillageKey,
        string targetVillageName,
        string? sourcePlayer,
        string? sourceVillage,
        IncomingAttackMovementType movementType,
        DateTimeOffset arrival)
    {
        var raw = string.Join('|', targetVillageKey ?? targetVillageName, sourcePlayer, sourceVillage, movementType, arrival.UtcTicks);
        return $"fallback:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..20]}";
    }
}
