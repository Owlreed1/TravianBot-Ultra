using TbotUltra.Desktop.ViewModels;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class HeroSettingsViewModelTests
{
    [Fact]
    public void HpRegenPerDayPercent_IsKeptWithinSupportedOptions()
    {
        var vm = new HeroSettingsViewModel
        {
            HpRegenPerDayPercent = 101,
        };

        Assert.Equal(100, vm.HpRegenPerDayPercent);
    }
}
