using TbotUltra.Core.Configuration;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class NewAccountAnalysisDecisionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void IsPending_EnabledAnalysisWithoutCompletion_RunsOnce(bool? completed)
    {
        Assert.True(NewAccountAnalysisDecisions.IsPending(enabled: true, completed));
    }

    [Fact]
    public void IsPending_CompletedAnalysis_DoesNotRunAgain()
    {
        Assert.False(NewAccountAnalysisDecisions.IsPending(enabled: true, completed: true));
    }

    [Fact]
    public void IsPending_DisabledAnalysis_DoesNotRun()
    {
        Assert.False(NewAccountAnalysisDecisions.IsPending(enabled: false, completed: null));
    }
}
