using TbotUltra.Desktop.ViewModels;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class FarmingSettingsViewModelTests
{
    [Fact]
    public void LastSentLimit_ExposesEditablePresentationState()
    {
        var vm = new FarmingSettingsViewModel
        {
            FarmListLastSentLimitEnabled = false,
            FarmListLastSentLimitHours = "48",
        };

        Assert.False(vm.FarmListLastSentLimitEnabled);
        Assert.Equal("48", vm.FarmListLastSentLimitHours);
    }
}
