using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class CatapultVillageSwitchSourceTests
{
    [Fact]
    public void CatapultVillageSwitch_ReadsTheSelectedVillageBeforeRallyPoint()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.SendTroops.Catapults.cs"));
        var switchStart = source.IndexOf("SwitchVillageRequested = async", StringComparison.Ordinal);
        var switchEnd = source.IndexOf("StartRequested = async", switchStart, StringComparison.Ordinal);
        Assert.True(switchStart >= 0 && switchEnd > switchStart);

        var switchBody = source[switchStart..switchEnd];
        var statusRead = switchBody.IndexOf("ReadVillageStatusWithRetryAsync", StringComparison.Ordinal);
        var rallyPointRead = switchBody.IndexOf("ReadSetupAsync(options, forceRefresh: true", StringComparison.Ordinal);

        Assert.True(statusRead >= 0, "Switch village must navigate and verify the selected village first.");
        Assert.True(rallyPointRead > statusRead, "Rally Point must be read only after the selected village is verified.");
        Assert.Contains("SetActiveWorkingVillageFromStatus(villageStatus);", switchBody, StringComparison.Ordinal);
        Assert.Contains("ResolveStatusVillageKey(villageStatus)", switchBody, StringComparison.Ordinal);
        Assert.Contains("forceCurrentVillage: true", switchBody, StringComparison.Ordinal);
    }
}
