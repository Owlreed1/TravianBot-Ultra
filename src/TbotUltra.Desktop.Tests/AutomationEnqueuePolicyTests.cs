using TbotUltra.Desktop.Services.Orchestration;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationEnqueuePolicyTests
{
    [Fact]
    public void Resolve_WakesRunningContinuousLoopForNewEligibleWork()
    {
        var snapshot = RunningSnapshot(AutomationRunMode.ContinuousLoop);

        var action = AutomationEnqueuePolicy.Resolve(snapshot, hasEligibleWork: true);

        Assert.Equal(AutomationEnqueueAction.WakeContinuousLoop, action);
    }

    [Fact]
    public void Resolve_WakesRunningAutoQueueForNewEligibleWork()
    {
        var snapshot = RunningSnapshot(AutomationRunMode.AutoQueue);

        var action = AutomationEnqueuePolicy.Resolve(snapshot, hasEligibleWork: true);

        Assert.Equal(AutomationEnqueueAction.WakeAutoQueue, action);
    }

    [Theory]
    [InlineData(false, AutomationPhase.Running)]
    [InlineData(true, AutomationPhase.Stopped)]
    [InlineData(true, AutomationPhase.Stopping)]
    public void Resolve_DoesNothingWithoutEligibleWorkInARunningRun(
        bool hasEligibleWork,
        AutomationPhase phase)
    {
        var snapshot = RunningSnapshot(AutomationRunMode.ContinuousLoop) with { Phase = phase };

        var action = AutomationEnqueuePolicy.Resolve(snapshot, hasEligibleWork);

        Assert.Equal(AutomationEnqueueAction.None, action);
    }

    private static AutomationSnapshot RunningSnapshot(AutomationRunMode mode) => new(
        new AutomationRunId(1),
        mode,
        new AutomationRunContext("account-1", new Uri("https://ts1.x1.example/"), 7),
        AutomationPhase.Running);
}
