using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ProductionBonusBatchSourceTests
{
    [Fact]
    public void ActivationBatch_AttemptsEveryInitiallyActivatableResourceWithoutInternalCooldownStop()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Features",
            "TravianClient.ProductionBonus.cs"));
        var methodStart = source.IndexOf("public async Task<string> ActivateProductionBonusVideosAsync", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("public async Task<string> ScanProductionBonusTimersAsync", methodStart, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        Assert.Contains("bypassExistingCooldown: resourceIndex > 0", method, StringComparison.Ordinal);
        Assert.DoesNotContain("stopping remaining video attempts", method, StringComparison.Ordinal);
    }
}
