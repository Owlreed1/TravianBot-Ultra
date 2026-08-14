using TbotUltra.Desktop.Services.Orchestration;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationPassPortTests
{
    [Theory]
    [InlineData(AutomationRunMode.ContinuousLoop)]
    [InlineData(AutomationRunMode.AutoQueue)]
    public async Task ReadAsync_RoutesToTheRequestedMode(AutomationRunMode mode)
    {
        var continuous = new RecordingModePassPort("continuous");
        var autoQueue = new RecordingModePassPort("auto-queue");
        var port = CreatePort(continuous, autoQueue);

        var result = await port.ReadAsync(mode, CurrentContext(), CancellationToken.None);

        Assert.Equal(mode == AutomationRunMode.ContinuousLoop ? "continuous" : "auto-queue", result.Candidates[0].TaskName);
        Assert.Equal(mode == AutomationRunMode.ContinuousLoop ? 1 : 0, continuous.ReadCount);
        Assert.Equal(mode == AutomationRunMode.AutoQueue ? 1 : 0, autoQueue.ReadCount);
    }

    [Fact]
    public async Task ExecuteAsync_RoutesToTheRequestedMode()
    {
        var continuous = new RecordingModePassPort("continuous");
        var autoQueue = new RecordingModePassPort("auto-queue");
        var port = CreatePort(continuous, autoQueue);
        var action = new AutomationCandidate(Guid.NewGuid(), "status", QueueGroup.Account, null, 0, DateTimeOffset.UtcNow);

        var result = await port.ExecuteAsync(
            AutomationRunMode.AutoQueue,
            CurrentContext(),
            action,
            CancellationToken.None);

        Assert.Equal(AutomationActionOutcome.Completed, result);
        Assert.Equal(0, continuous.ExecuteCount);
        Assert.Equal(1, autoQueue.ExecuteCount);
    }

    [Fact]
    public async Task ReadAsync_RejectsAnAccountChangedDuringTheRun()
    {
        var modePort = new RecordingModePassPort("continuous");
        var port = new AutomationPassPort(() => "account-2", () => 7, modePort, modePort);

        var exception = await Assert.ThrowsAsync<AutomationContextException>(async () =>
            await port.ReadAsync(AutomationRunMode.ContinuousLoop, CurrentContext(), CancellationToken.None));

        Assert.Contains("active account changed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AutomationFailureKind.AccountAccess, exception.FailureKind);
        Assert.Equal("active-account-changed", exception.DiagnosticCode);
        Assert.Equal(0, modePort.ReadCount);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsABrowserGenerationChangedDuringTheRun()
    {
        var modePort = new RecordingModePassPort("continuous");
        var port = new AutomationPassPort(() => "account-1", () => 8, modePort, modePort);
        var action = new AutomationCandidate(Guid.NewGuid(), "status", QueueGroup.Account, null, 0, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<AutomationContextException>(async () =>
            await port.ExecuteAsync(
                AutomationRunMode.ContinuousLoop,
                CurrentContext(),
                action,
                CancellationToken.None));

        Assert.Contains("browser generation changed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AutomationFailureKind.StaleBrowserGeneration, exception.FailureKind);
        Assert.Equal("browser-generation-changed", exception.DiagnosticCode);
        Assert.Equal(0, modePort.ExecuteCount);
    }

    private static AutomationPassPort CreatePort(
        IAutomationModePassPort continuous,
        IAutomationModePassPort autoQueue) => new(
        () => "ACCOUNT-1",
        () => 7,
        continuous,
        autoQueue);

    private static AutomationRunContext CurrentContext() => new(
        "account-1",
        new Uri("https://ts1.x1.example/"),
        7);

    private sealed class RecordingModePassPort(string taskName) : IAutomationModePassPort
    {
        internal int ReadCount { get; private set; }

        internal int ExecuteCount { get; private set; }

        public ValueTask<AutomationStateSnapshot> ReadAsync(
            AutomationRunContext context,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult(new AutomationStateSnapshot(
                [new AutomationCandidate(Guid.NewGuid(), taskName, QueueGroup.Account, null, 0, DateTimeOffset.UtcNow)]));
        }

        public ValueTask<AutomationActionOutcome> ExecuteAsync(
            AutomationRunContext context,
            AutomationCandidate action,
            CancellationToken cancellationToken)
        {
            ExecuteCount++;
            return ValueTask.FromResult(AutomationActionOutcome.Completed);
        }
    }
}
