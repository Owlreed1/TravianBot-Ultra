using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class HeroResourceTransferSelectorTests
{
    [Theory]
    [InlineData(1, "contract_building1")]
    [InlineData(17, "contract_building17")]
    [InlineData(23, "contract_building23")]
    [InlineData(46, "contract_building46")]
    public void ConstructScopeId_TargetsExactBuildingRowAcrossCategories(int gid, string expected)
    {
        Assert.Equal(expected, TravianClient.BuildHeroTransferConstructScopeId(gid));
    }

    [Fact]
    public void ConstructScopeId_RejectsMissingOrInvalidGid()
    {
        Assert.Null(TravianClient.BuildHeroTransferConstructScopeId(null));
        Assert.Null(TravianClient.BuildHeroTransferConstructScopeId(0));
    }

    [Fact]
    public void ConstructionFlows_LoadMissingInventoryFromTheExistingTransferDialog()
    {
        var projectRoot = FindRepositoryRoot();
        var automationRoot = Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation");
        var transferSource = File.ReadAllText(Path.Combine(
            automationRoot,
            "Hero",
            "TravianClient.HeroResourceTransfer.cs"));
        var buildingUpgradeSource = File.ReadAllText(Path.Combine(
            automationRoot,
            "Buildings",
            "TravianClient.Buildings.UpgradeFlow.cs"));
        var constructSource = File.ReadAllText(Path.Combine(
            automationRoot,
            "Buildings",
            "TravianClient.Buildings.ConstructFlow.cs"));
        var resourceUpgradeSource = File.ReadAllText(Path.Combine(
            automationRoot,
            "Resources",
            "TravianClient.Resources.Upgrade.cs"));

        Assert.Contains("TryLoadMissingHeroInventoryFromCurrentBuildPageAsync", transferSource);
        Assert.Contains("ReadHeroInventoryFromTransferDialogAsync", transferSource);
        Assert.Contains("TryDismissResourceTransferDialogAsync", transferSource);
        Assert.Contains("TryLoadMissingHeroInventoryFromCurrentBuildPageAsync", buildingUpgradeSource);
        Assert.Contains("TryLoadMissingHeroInventoryFromCurrentBuildPageAsync", constructSource);
        Assert.Contains("TryLoadMissingHeroInventoryFromCurrentBuildPageAsync", resourceUpgradeSource);
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
