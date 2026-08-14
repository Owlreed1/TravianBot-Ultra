using TbotUltra.Desktop.Services.Orchestration;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationMailboxTests
{
    [Fact]
    public void ConcurrentWakeCommands_CoalesceUntilThePassConsumesThem()
    {
        using var mailbox = new AutomationMailbox();

        var accepted = Enumerable.Range(0, 20)
            .AsParallel()
            .Count(_ => mailbox.PostWake());

        Assert.Equal(1, accepted);
        mailbox.ConsumeWake();
        Assert.True(mailbox.PostWake());
    }
}
