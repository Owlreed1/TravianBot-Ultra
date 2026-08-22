using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BonusVideoMuteSourceTests
{
    [Theory]
    [InlineData("Buildings", "TravianClient.ConstructFaster.cs", "WaitForConstructFasterVideoCompletionAsync")]
    [InlineData("Features", "TravianClient.ProductionBonus.cs", "WaitForProductionBonusVideoCompletionAsync")]
    [InlineData("Hero", "TravianClient.AdventureDanger.cs", "WaitForAdventureVideoActiveAsync")]
    public void PlaybackPolling_RetriesBestEffortMuteUntilControlAppears(
        string area,
        string fileName,
        string methodName)
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            area,
            fileName));

        var methodStart = source.IndexOf($"private async Task<bool> {methodName}(", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"Could not find {methodName}.");

        var nextMethod = source.IndexOf("\n    private ", methodStart + methodName.Length, StringComparison.Ordinal);
        var method = nextMethod > methodStart
            ? source[methodStart..nextMethod]
            : source[methodStart..];

        Assert.Contains("MuteBonusVideoAsync(", method, StringComparison.Ordinal);
    }
}
