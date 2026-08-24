using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class LobbyLoginRetrySourceTests
{
    [Fact]
    public void LobbyLogin_WaitsForDelayedReactStateAndRetriesBeforeGivingUp()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Core",
            "TravianClient.LobbyLogin.cs"));

        var loginFlow = Slice(source, "private async Task<bool> TryLoginThroughLobbyAsync", "private async Task<LobbyWorldCard?> RequestLobbyWorldSelectionAsync");
        var preparation = Slice(source, "private async Task<bool> PrepareAuthenticatedLobbyAsync", "private async Task<LobbyEntryState> WaitForLobbyEntryStateAsync");

        Assert.Contains("PrepareAuthenticatedLobbyAsync(cancellationToken)", loginFlow, StringComparison.Ordinal);
        Assert.Contains("for (var attempt = 1; attempt <= LobbyLoadAttempts; attempt++)", preparation, StringComparison.Ordinal);
        Assert.Contains("await WaitForLobbyEntryStateAsync(cancellationToken)", preparation, StringComparison.Ordinal);
        Assert.Contains("retrying the lobby load", preparation, StringComparison.OrdinalIgnoreCase);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not locate source slice {startMarker}..{endMarker}.");
        return source[start..end];
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate TbotUltra.sln from the test output directory.");
    }
}
