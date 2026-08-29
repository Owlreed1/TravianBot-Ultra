using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class DailyQuestCollectSourceTests
{
    [Fact]
    public void DailyQuestCollect_ExcludesAlreadyCollectedButtons()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Features",
            "TravianClient.DailyQuests.cs"));

        Assert.Contains("button.textButtonV2.collect.collectable:not(.collected)", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"button.textButtonV2.collect.collectable, button.collect.collectable\"",
            source,
            StringComparison.Ordinal);
    }
}
