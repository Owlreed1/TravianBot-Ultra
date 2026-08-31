namespace TbotUltra.Core.Configuration;

public static class HeroAttributeMaximums
{
    public const int DefaultMaximum = 100;
    public const string DefaultSerialized =
        "resources=100,fighting_strength=100,offence_bonus=100,defence_bonus=100";

    public static readonly string[] KnownKeys =
        ["resources", "fighting_strength", "offence_bonus", "defence_bonus"];

    public static IReadOnlyDictionary<string, int> Parse(string? value)
    {
        var parsed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0 || separator == part.Length - 1)
            {
                continue;
            }

            var key = part[..separator].Trim();
            var rawMaximum = part[(separator + 1)..].Trim();
            if (KnownKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
                && int.TryParse(rawMaximum, out var maximum)
                && maximum is >= 0 and <= 100)
            {
                parsed[key] = maximum;
            }
        }

        return KnownKeys.ToDictionary(
            key => key,
            key => parsed.GetValueOrDefault(key, DefaultMaximum),
            StringComparer.OrdinalIgnoreCase);
    }

    public static string Serialize(IEnumerable<KeyValuePair<string, int>> maximums)
    {
        var values = maximums.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is >= 0 and <= 100 ? pair.Value : DefaultMaximum,
            StringComparer.OrdinalIgnoreCase);
        return string.Join(",", KnownKeys.Select(key => $"{key}={values.GetValueOrDefault(key, DefaultMaximum)}"));
    }
}
