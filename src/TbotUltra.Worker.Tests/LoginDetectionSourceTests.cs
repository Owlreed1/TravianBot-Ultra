using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class LoginDetectionSourceTests
{
    [Fact]
    public void LoginDetection_WaitsOnCurrentPageWithoutCanonicalRecoveryNavigation()
    {
        var root = FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Core",
            "TravianClient.Login.cs"));
        var ensureMethod = ExtractMethod(source, "private async Task EnsureLoggedInAsync", "private async Task<bool> IsLoggedInAsync");
        var stateMethod = ExtractMethod(source, "private async Task<AccountAccessState> LoginStateAsync", "private async Task<AccountAccessState?> ProbeExplicitAccountAccessStateAsync");

        Assert.DoesNotContain("VerifyUnknownAccessStateAsync", ensureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("GotoAsync", ensureMethod, StringComparison.Ordinal);
        Assert.Contains("cancellationToken", stateMethod, StringComparison.Ordinal);
        Assert.Contains("loginProbeDeadline", stateMethod, StringComparison.Ordinal);
        Assert.Contains("Task.Delay", stateMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticatedLayoutSelectors_IncludeOfficialStableShellMarkers()
    {
        var root = FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Core",
            "TravianClient.Selectors.cs"));
        var indicators = ExtractMethod(source, "public static readonly string[] LoggedInIndicators", "public static readonly string[] LoggedOutIndicators");

        // The hero control is part of the authenticated shell on Dorf1, Dorf2, building,
        // farmlist, profile and Hero pages; login detection must not depend on village pages.
        Assert.Contains("#heroImageButton[href^='/hero']", indicators, StringComparison.Ordinal);
        Assert.Contains("img.heroImage[alt='Hero']", indicators, StringComparison.Ordinal);
        Assert.Contains("#sidebarBoxActiveVillage", indicators, StringComparison.Ordinal);
        Assert.Contains("#sidebarBoxVillageList", indicators, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentPageResourceRead_ForwardsPauseCancellationToLoginCheck()
    {
        var root = FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Resources",
            "TravianClient.Resources.Snapshot.cs"));
        var method = ExtractMethod(source, "public async Task<VillageStatus> ReadVillageResourceStatusAsync", "public async Task<VillageStatus> ReadCurrentPageStorageStatusAsync");

        Assert.Contains("EnsureLoggedInAsync(cancellationToken: cancellationToken)", method, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
