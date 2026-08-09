using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BrowserSessionAdmissionTests
{
    [Fact]
    public void Close_BlocksQueuedBrowserSessionCreation_UntilExplicitLoginReopensIt()
    {
        var admission = new BrowserSessionAdmission();

        admission.Close();
        Assert.Throws<OperationCanceledException>(() => admission.ThrowIfClosed());

        admission.Open();

        admission.ThrowIfClosed();
    }
}
