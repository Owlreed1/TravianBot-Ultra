namespace TbotUltra.Core.Travian;

public static class FarmLossListNaming
{
    public const int MaxNameLength = 30;

    public static string NextAvailable(string? requestedBaseName, IEnumerable<string> existingNames)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedBaseName)
            ? "Yellow farms"
            : requestedBaseName.Trim();
        baseName = baseName[..Math.Min(baseName.Length, MaxNameLength)];
        var existing = existingNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        for (var suffix = 1; suffix < 10000; suffix++)
        {
            var suffixText = suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var prefixLength = Math.Max(0, MaxNameLength - suffixText.Length);
            var candidate = baseName[..Math.Min(baseName.Length, prefixLength)] + suffixText;
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not find an available farm list name.");
    }
}
