namespace TbotUltra.Desktop.Services;

public static class ConnectionIdentityRefreshDecisions
{
    public static bool ShouldStartLookup(
        string lookupKey,
        string currentLookupKey,
        string cachedIp,
        string inFlightLookupKey)
    {
        if (!string.Equals(lookupKey, currentLookupKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(cachedIp)
            && !string.Equals(lookupKey, inFlightLookupKey, StringComparison.OrdinalIgnoreCase);
    }
}
