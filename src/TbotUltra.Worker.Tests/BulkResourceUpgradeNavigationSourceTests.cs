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
