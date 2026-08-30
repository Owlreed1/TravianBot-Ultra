using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BulkResourceUpgradeNavigationSourceTests
{
    [Fact]
    public void CandidateScan_ReusesOverviewConstructionSnapshotInsteadOfNavigatingPerField()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Resources",
            "TravianClient.Resources.Upgrade.cs"));
        var loopStart = source.IndexOf("foreach (var candidate in candidateRows)", StringComparison.Ordinal);
        var loopEnd = source.IndexOf("if (!attemptedAny)", loopStart, StringComparison.Ordinal);

        Assert.True(loopStart >= 0 && loopEnd > loopStart, "Could not locate the bulk resource candidate scan.");
        var candidateScan = source[loopStart..loopEnd];
        Assert.DoesNotContain("ReadHighestKnownQueuedResourceLevelAsync", candidateScan, StringComparison.Ordinal);
        Assert.Contains("ResourceConstructionQueueMatcher.HighestQueuedLevelForSlot", candidateScan, StringComparison.Ordinal);
        Assert.Contains("queuedLevelsBySlot.TryGetValue", candidateScan, StringComparison.Ordinal);
        Assert.Contains("confirmedQueuedLevelsBySlot[slot]", candidateScan, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticDorf1Redirect_AppliesPageLoadPacingBeforeNextCandidate()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Resources",
            "TravianClient.Resources.Upgrade.cs"));
        var methodStart = source.IndexOf("private async Task NavigateToResourceFieldsAfterUpgradeClickAsync", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private async Task EnsureResourceFieldsPageAsync", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var method = source[methodStart..methodEnd];
        Assert.Contains("ApplyPacingDelayAsync", method, StringComparison.Ordinal);
        Assert.Contains("after resource upgrade redirect", method, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleAndBulkResourceUpgrades_RunFinalSafetyCheckBeforeAnyAction()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Resources",
            "TravianClient.Resources.Upgrade.cs"));

        Assert.Equal(3, source.Split("VerifyResourceUpgradePreClickSafetyAsync", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, source.Split("clickSafety.CandidateIndex", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, source.Split("pre-click safety stopped upgrade", StringSplitOptions.None).Length - 1);
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
