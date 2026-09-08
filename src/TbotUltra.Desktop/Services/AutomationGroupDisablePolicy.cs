using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;

namespace TbotUltra.Desktop.Services;

internal static class AutomationGroupDisablePolicy
{
    internal static bool ShouldCancelRunningItem(
        QueueItem item,
        QueueGroup disabledGroup,
        string? itemVillageKey,
        string? selectedVillageKey)
    {
        return item.Status == QueueStatus.Running
            && item.Group == disabledGroup
            && !string.IsNullOrWhiteSpace(itemVillageKey)
            && string.Equals(itemVillageKey, selectedVillageKey, StringComparison.OrdinalIgnoreCase);
    }
}
