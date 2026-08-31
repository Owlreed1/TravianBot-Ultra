using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class HeroInventoryOintmentSourceTests
{
    [Fact]
    public void ResourceInventoryRead_CapturesOintmentsInTheSameBrowserEvaluation()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Hero",
            "TravianClient.Hero.Attributes.cs"));
        var start = source.IndexOf("public async Task<HeroInventoryResources> ReadHeroInventoryResourcesAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task<HeroAttributeSnapshot> ReadHeroInventorySnapshotAsync", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        var method = source[start..end];
        Assert.Contains(".heroItems .item.item106", method, StringComparison.Ordinal);
        Assert.Contains("ointmentFound: !!ointmentItem", method, StringComparison.Ordinal);
        Assert.Contains("pageRead.OintmentCount", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadHeroOintmentInventoryInfoAsync", method, StringComparison.Ordinal);
    }
}
