using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class LanguageGateWorkflowSourceTests
{
    [Fact]
    public void VerifiedLanguageGate_RestoresTheAutomationModeThatWasRunningBeforeThePause()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var gateSource = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.LanguageGate.cs"));
        var loopSource = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.ContinuousLoop.cs"));

        Assert.Contains("var resumeContinuous = IsContinuousLoopRunning() || _startContinuousLoopAfterQueueStop", gateSource, StringComparison.Ordinal);
        Assert.Contains("var resumeQueue = !resumeContinuous && _autoQueueRunning", gateSource, StringComparison.Ordinal);
        Assert.Contains("ResumeAutomationAfterLanguageGateAsync(resumeContinuous, resumeQueue)", gateSource, StringComparison.Ordinal);
        Assert.Contains("_restartAutoQueueAfterLanguageGate", loopSource, StringComparison.Ordinal);
        Assert.Contains("TriggerQueueAutoRunAsync()", loopSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LanguageGate_UsesTheSameEnglishAcceptanceRuleAsTheWorker()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "TravianLanguageGateWindow.xaml.cs"));

        Assert.Contains("TravianClient.IsExpectedLanguage(language)", source, StringComparison.Ordinal);
    }
}
