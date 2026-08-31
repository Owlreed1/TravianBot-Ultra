using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

internal static class AutomationGroupAvailability
{
    public static AutomationGroupToggleState Resolve(
        QueueGroup group,
        bool isCapital,
        bool? goldClubEnabled,
        bool requestedEnabled)
    {
        var canToggle = CanToggle(group, isCapital, goldClubEnabled);
        return new AutomationGroupToggleState(canToggle && requestedEnabled, canToggle);
    }

    public static bool CanToggle(QueueGroup group, bool isCapital, bool? goldClubEnabled)
    {
        return group switch
        {
            QueueGroup.Farming => goldClubEnabled == true,
            QueueGroup.BreweryCelebration => isCapital,
            _ => true,
        };
    }
}

internal readonly record struct AutomationGroupToggleState(bool IsEnabled, bool CanToggle);
