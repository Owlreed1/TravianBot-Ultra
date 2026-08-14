using TbotUltra.Desktop.Services.Orchestration;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationSettingsWakePolicyTests
{
    [Theory]
    [InlineData(true, 10, 20, true, true)]
    [InlineData(true, 10, 10, true, false)]
    [InlineData(false, 10, 20, true, false)]
    [InlineData(true, 10, 20, false, false)]
    public void ShortVillageWaitChange_WakesOnlyAnActiveSavedRun(
        bool saved,
        int before,
        int after,
        bool running,
        bool expected)
    {
        Assert.Equal(
            expected,
            AutomationSettingsWakePolicy.ShouldWakeForShortVillageWaitChange(
                saved,
                before,
                after,
                running));
    }
}
