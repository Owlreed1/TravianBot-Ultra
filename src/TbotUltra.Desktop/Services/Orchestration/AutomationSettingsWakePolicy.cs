namespace TbotUltra.Desktop.Services.Orchestration;

internal static class AutomationSettingsWakePolicy
{
    internal static bool ShouldWakeForShortVillageWaitChange(
        bool settingsSaved,
        int previousSeconds,
        int currentSeconds,
        bool continuousLoopRunning) =>
        settingsSaved
        && continuousLoopRunning
        && previousSeconds != currentSeconds;
}
