using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class DemolitionDisplayStateTests
{
    [Fact]
    public void IsTracked_KeepsTheMarkerUntilTheDemolitionTaskCompletes()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new QueueItem
        {
            TaskName = "demolish_building_to_level",
            Status = QueueStatus.Pending,
            NextAttemptAt = now.AddMinutes(5),
            Payload = new Dictionary<string, string>
            {
                [BotOptionPayloadKeys.DemolishServerFinishAtUnixSeconds] =
                    now.AddSeconds(-1).ToUnixTimeSeconds().ToString(),
            },
        };

        Assert.True(DemolitionDisplayState.IsTracked(item));

        item.Status = QueueStatus.Succeeded;

        Assert.False(DemolitionDisplayState.IsTracked(item));
    }
}
