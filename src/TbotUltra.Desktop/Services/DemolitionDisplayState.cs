using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>
/// Decides whether a demolition queue item still owns its slot's UI marker.
/// </summary>
internal static class DemolitionDisplayState
{
    internal static bool IsTracked(QueueItem item)
        => item.Status is QueueStatus.Pending or QueueStatus.Running;

    internal static bool TryGetServerFinishAt(QueueItem item, out DateTimeOffset finishAt)
    {
        finishAt = default;
        if (!item.Payload.TryGetValue(BotOptionPayloadKeys.DemolishServerFinishAtUnixSeconds, out var value)
            || !long.TryParse(value, out var unixSeconds)
            || unixSeconds <= 0)
        {
            return false;
        }

        try
        {
            finishAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
