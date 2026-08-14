using TbotUltra.Desktop.Services.Orchestration;
using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationFailureClassifierTests
{
    [Fact]
    public void Classify_PreservesTypedContextFailures()
    {
        var failure = AutomationFailureClassifier.Classify(new AutomationContextException(
            AutomationFailureKind.StaleBrowserGeneration,
            "browser-generation-changed",
            "changed"));

        Assert.Equal(AutomationFailureKind.StaleBrowserGeneration, failure.Kind);
        Assert.Equal("browser-generation-changed", failure.DiagnosticCode);
        Assert.False(failure.IsRetryable);
    }

    [Fact]
    public void Classify_RecognizesTransientNavigationAsRetryable()
    {
        var failure = AutomationFailureClassifier.Classify(
            new TransientNavigationException("timed out"));

        Assert.Equal(AutomationFailureKind.TransientNetwork, failure.Kind);
        Assert.True(failure.IsRetryable);
    }

    [Fact]
    public void Classify_LeavesUnexpectedAdapterExceptionsTerminal()
    {
        var failure = AutomationFailureClassifier.Classify(new InvalidOperationException("bad adapter"));

        Assert.Equal(AutomationFailureKind.AdapterContract, failure.Kind);
        Assert.False(failure.IsRetryable);
    }
}
