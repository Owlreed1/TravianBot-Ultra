namespace TbotUltra.Desktop.Services.Orchestration;

internal enum AutomationEnqueueAction
{
    None,
    WakeAutoQueue,
    WakeContinuousLoop,
}

internal static class AutomationEnqueuePolicy
{
    internal static AutomationEnqueueAction Resolve(
        AutomationSnapshot snapshot,
        bool hasEligibleWork)
    {
        if (!hasEligibleWork || snapshot.Phase != AutomationPhase.Running)
        {
            return AutomationEnqueueAction.None;
        }

        return snapshot.Mode switch
        {
            AutomationRunMode.AutoQueue => AutomationEnqueueAction.WakeAutoQueue,
            AutomationRunMode.ContinuousLoop => AutomationEnqueueAction.WakeContinuousLoop,
            _ => AutomationEnqueueAction.None,
        };
    }
}
