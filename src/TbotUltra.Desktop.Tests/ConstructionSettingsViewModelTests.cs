using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.ViewModels;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ConstructionSettingsViewModelTests
{
    [Fact]
    public void CropShortageRecovery_IsEnabledByDefault()
    {
        var vm = new ConstructionSettingsViewModel();

        Assert.True(vm.CropShortageRecoveryEnabled);
        Assert.True(ConstructionDefaults.CropShortageRecoveryEnabled);
    }

    [Fact]
    public void StorageUpgradeLevelsAhead_NormalizesToConfiguredRange()
    {
        var vm = new ConstructionSettingsViewModel
        {
            StorageUpgradeLevelsAhead = int.MaxValue,
        };

        Assert.Equal(ConstructionDefaults.StorageUpgradeLevelsAheadMax, vm.StorageUpgradeLevelsAhead);
    }
}
