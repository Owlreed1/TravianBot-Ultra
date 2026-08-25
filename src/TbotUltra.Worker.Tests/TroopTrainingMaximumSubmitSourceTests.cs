using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class TroopTrainingMaximumSubmitSourceTests
{
    [Fact]
    public void MaximumMode_ClicksTravianAmountLinkBeforeTrainInsteadOfTyping()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectRootLocator.FindProjectRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Training",
            "TravianClient.TroopTraining.cs"));
        var submitStart = source.IndexOf("private async Task<bool> SubmitTroopTrainingFromCurrentPageAsync", StringComparison.Ordinal);
        var maxStart = source.IndexOf("if (useMaxShortcut)", submitStart, StringComparison.Ordinal);
        var nonMaxStart = source.IndexOf("else", maxStart, StringComparison.Ordinal);
        var trainClick = source.IndexOf("await submitButton.ClickAsync", nonMaxStart, StringComparison.Ordinal);
        Assert.True(submitStart >= 0 && maxStart > submitStart && nonMaxStart > maxStart && trainClick > nonMaxStart);

        var maximumBranch = source[maxStart..nonMaxStart];
        Assert.Contains("ClickTroopTrainingMaxAmountAsync", maximumBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("TypeHumanlyAsync", maximumBranch, StringComparison.Ordinal);
        Assert.True(source.IndexOf("ClickTroopTrainingMaxAmountAsync", maxStart, StringComparison.Ordinal) < trainClick);
        Assert.Contains("' details '", source, StringComparison.Ordinal);
        Assert.Contains(".cta a[href='#']", source, StringComparison.Ordinal);
    }
}
