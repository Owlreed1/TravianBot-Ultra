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
        Assert.Contains("await category.WaitForAsync", source, StringComparison.Ordinal);
        Assert.Contains("img.filterCategory1", source, StringComparison.Ordinal);
        Assert.Contains("img.subFilterCategory1", source, StringComparison.Ordinal);
        Assert.Contains("img.subFilterCategory2", source, StringComparison.Ordinal);
        Assert.Contains("img.subFilterCategory3", source, StringComparison.Ordinal);
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

    [Fact]
    public void TroopEvasion_ChecksDorf1TroopPresenceBeforeOpeningRallyPoint()
    {
        var source = Read("TbotUltra.Worker", "Services", "Automation", "Combat", "TravianClient.TroopEvasion.cs");
        var presenceCheck = source.IndexOf("ReadTroopPresenceOnCurrentDorf1Async", StringComparison.Ordinal);
        var rallyPointOpen = source.IndexOf("EnsureRallyPointAndOpenSendTroopsPageAsync", StringComparison.Ordinal);

        Assert.True(presenceCheck >= 0);
        Assert.True(rallyPointOpen > presenceCheck);
        Assert.Contains("TroopEvasionOutcome.NoTroops", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TroopEvasion_CachedNoTroopsCanSkipOnlyInitialLeadMilestone()
    {
        var source = Read("TbotUltra.Desktop", "MainWindow.TroopEvasion.cs");

        Assert.Contains("due.Milestone == \"lead\"", source, StringComparison.Ordinal);
        Assert.Contains("cachedStatus.HasTroopsAtHome == false", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IncomingAttackRows_AreCreatedWithAVisibleCountdown()
    {
        var source = Read("TbotUltra.Desktop", "MainWindow.IncomingAttacks.cs");

        Assert.Contains("CountdownText = FormatIncomingAttackCountdown(attack.ArrivalAtUtc", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UnchangedDorf1Signals_DoNotCauseMinuteByMinuteRallyPointReads()
    {
        var source = Read("TbotUltra.Desktop", "MainWindow.IncomingAttacks.cs");

        Assert.Contains("IncomingAttackObservationPolicy.ShouldReadDetails", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeSpan.FromMinutes(1)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledVillage_GatesIncomingReadsAndTroopEvasion()
    {
        var incomingSource = Read("TbotUltra.Desktop", "MainWindow.IncomingAttacks.cs");
        var evasionSource = Read("TbotUltra.Desktop", "MainWindow.TroopEvasion.cs");

        Assert.Contains("if (!IsIncomingAttackMonitoringEnabled(villageKey))", incomingSource, StringComparison.Ordinal);
        Assert.Contains("discarded result for disabled village", incomingSource, StringComparison.Ordinal);
        Assert.Contains("Where(pair => IsIncomingAttackMonitoringEnabled(pair.Key))", evasionSource, StringComparison.Ordinal);
        Assert.Contains("Incoming monitoring disabled", evasionSource, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        var root = ProjectRootLocator.FindProjectRoot();
        return File.ReadAllText(Path.Combine([root, "src", .. parts]));
    }
}
