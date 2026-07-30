using System;

namespace TbotUltra.Desktop.Services;

public static class UpdateNotificationDecisions
{
    public static bool ShouldShow(
        UpdateChecker.UpdateStatus? status,
        bool notificationsMuted,
        string? lastAcknowledgedVersion,
        bool notificationOpen)
    {
        if (notificationsMuted
            || notificationOpen
            || status?.UpdateAvailable != true
            || status.Release is null)
        {
            return false;
        }

        return !string.Equals(
            lastAcknowledgedVersion?.Trim(),
            status.Release.LatestVersion,
            StringComparison.OrdinalIgnoreCase);
    }
}
