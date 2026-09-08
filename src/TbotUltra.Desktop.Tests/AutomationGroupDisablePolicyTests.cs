using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationGroupDisablePolicyTests
{
    [Fact]
    public void ShouldCancelRunningItem_OnlyMatchesTheDisabledVillageAndGroup()
    {
        var runningFarming = new QueueItem
        {
            TaskName = "send_farmlists",
            Group = QueueGroup.Farming,
            Status = QueueStatus.Running,
        };

        Assert.True(AutomationGroupDisablePolicy.ShouldCancelRunningItem(
            runningFarming, QueueGroup.Farming, "xy:1|2", "xy:1|2"));
        Assert.False(AutomationGroupDisablePolicy.ShouldCancelRunningItem(
            runningFarming, QueueGroup.Farming, "xy:3|4", "xy:1|2"));
        Assert.False(AutomationGroupDisablePolicy.ShouldCancelRunningItem(
            runningFarming, QueueGroup.Construction, "xy:1|2", "xy:1|2"));

        runningFarming.Status = QueueStatus.Pending;
        Assert.False(AutomationGroupDisablePolicy.ShouldCancelRunningItem(
            runningFarming, QueueGroup.Farming, "xy:1|2", "xy:1|2"));
    }
}
