using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class IncomingAttackFlowSourceTests
{
    [Fact]
    public void RallyPointRead_OpensIncomingOnlyUrlAndWaitsForFilterControl()
    {
        var source = Read("TbotUltra.Worker", "Services", "Automation", "Combat", "TravianClient.IncomingAttacks.cs");

        Assert.Contains("/build.php?gid=16&tt=1&filter=1&subfilters=1", source, StringComparison.Ordinal);
        Assert.Contains("await incoming.WaitForAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoritativeEmptyDorf1Read_ClearsPendingAndConfirmedVillageState()
    {
        var source = Read("TbotUltra.Desktop", "MainWindow.IncomingAttacks.cs");
        Assert.Contains("resolvedSignals.All(signal => !string.Equals(signal.Key, activeKey", source, StringComparison.Ordinal);
        Assert.Contains("ClearIncomingAttacksAfterAuthoritativeDorf1Read(activeKey", source, StringComparison.Ordinal);
        Assert.Contains("_incomingAttackPendingSignals.Remove(villageKey)", source, StringComparison.Ordinal);
        Assert.Contains("_incomingAttacksByVillage.Remove(villageKey)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("knownAttacks.Count == 0", source, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        var root = ProjectRootLocator.FindProjectRoot();
        return File.ReadAllText(Path.Combine([root, "src", .. parts]));
    }
}
