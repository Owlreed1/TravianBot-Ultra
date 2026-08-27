using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class FarmListPageReuseTests
{
    [Fact]
    public void ReadFarmListsOverview_RefreshesOnceBeforeReadingCurrentValues()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectRootLocator.FindProjectRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Farming",
            "TravianClient.FarmLists.cs"));

        var methodStart = source.IndexOf("ReadFarmListsOverviewAsync", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private async Task WaitForFarmListsRenderedAsync", methodStart, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        Assert.Matches(
            @"EnsureRallyPointAndOpenFarmListPageAsync\(\s*cancellationToken,\s*refreshCurrentPage: attempt == 1\)",
            method);
    }

    [Theory]
    [InlineData("https://travian.example/build.php?id=39&gid=16&tt=99")]
    [InlineData("https://travian.example/build.php?tt=99&id=39&gid=16&action=showSlot&lid=7")]
    public void IsOfficialFarmListUrl_AcceptsFarmListPages(string url)
    {
        Assert.True(TravianClient.IsOfficialFarmListUrl(url));
    }

    [Theory]
    [InlineData("https://travian.example/build.php?id=39&gid=16&tt=1")]
    [InlineData("https://travian.example/build.php?id=28&gid=24&tt=99")]
    [InlineData("https://travian.example/dorf1.php")]
    public void IsOfficialFarmListUrl_RejectsOtherPages(string url)
    {
        Assert.False(TravianClient.IsOfficialFarmListUrl(url));
    }
}
