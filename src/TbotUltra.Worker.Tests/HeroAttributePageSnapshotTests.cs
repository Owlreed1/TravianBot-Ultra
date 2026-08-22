using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class HeroAttributePageSnapshotTests
{
    [Fact]
    public void ExplicitAttributeRead_NavigatesToLivePageWithoutCacheShortcut()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectRootLocator.FindProjectRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Hero",
            "TravianClient.Hero.Status.cs"));
        var methodStart = source.IndexOf(
            "public async Task<HeroAttributeSnapshot> ReadHeroAttributeSnapshotAsync",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "private async Task<(string? Name, bool Away, int? X, int? Y)> ReadHeroHomeVillageInfoAsync",
            methodStart,
            StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        var navigation = method.IndexOf("GotoAsync(Paths.HeroAttributes", StringComparison.Ordinal);
        var liveRead = method.IndexOf("ReadHeroInventorySnapshotAsync", StringComparison.Ordinal);

        Assert.True(navigation >= 0 && liveRead > navigation);
        Assert.DoesNotContain("TryGetCachedHeroAttributeSnapshot", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSnapshot_PreservesVerifiedLiveValues()
    {
        var page = new HeroAttributePageSnapshot(
            Ok: true,
            AttributeCount: 4,
            FreePoints: 4,
            FightingStrength: 12,
            OffenceBonus: 3,
            DefenceBonus: 5,
            Resources: 28);

        var snapshot = page.ToSnapshot();

        Assert.Equal(4, snapshot.FreePoints);
        Assert.Equal(28, snapshot.Resources);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 3)]
    public void ToSnapshot_RejectsFailedOrIncompletePageReads(bool ok, int attributeCount)
    {
        var page = new HeroAttributePageSnapshot(Ok: ok, AttributeCount: attributeCount);

        Assert.Throws<InvalidOperationException>(() => page.ToSnapshot());
    }
}
