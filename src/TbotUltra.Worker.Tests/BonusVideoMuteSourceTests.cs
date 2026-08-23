using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BonusVideoMuteSourceTests
{
    [Fact]
    public void MuteClicksOnlyTheVerifiedVisibleAudioIcon()
    {
        var method = ReadMethod("Hero", "TravianClient.AdventureDanger.cs", "MuteBonusVideoAsync");

        Assert.Contains(
            "frame.Locator(\".atg-gima-audio-button-enabled:not(.atg-gima-hidden)\")",
            method,
            StringComparison.Ordinal);
        Assert.Contains("IsSafeBonusVideoAudioControlAsync(", method, StringComparison.Ordinal);
        Assert.Contains("if (!_config.TurnOffVideoSound)", method, StringComparison.Ordinal);
        Assert.DoesNotContain(".atg-gima-audio-button:has(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Force = true", method, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Buildings", "TravianClient.ConstructFaster.cs", "WaitForConstructFasterVideoCompletionAsync")]
    [InlineData("Features", "TravianClient.ProductionBonus.cs", "WaitForProductionBonusVideoCompletionAsync")]
    [InlineData("Hero", "TravianClient.AdventureDanger.cs", "WaitForAdventureVideoActiveAsync")]
    public void PlaybackPolling_RetriesBestEffortMuteUntilControlAppears(
        string area,
        string fileName,
        string methodName)
    {
        var method = ReadMethod(area, fileName, methodName);

        Assert.Contains("MuteBonusVideoAsync(", method, StringComparison.Ordinal);
    }

    private static string ReadMethod(string area, string fileName, string methodName)
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
        return nextMethod > methodStart
            ? source[methodStart..nextMethod]
            : source[methodStart..];
    }
}
