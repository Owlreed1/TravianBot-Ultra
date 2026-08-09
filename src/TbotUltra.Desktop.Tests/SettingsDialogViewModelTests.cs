using TbotUltra.Desktop.ViewModels;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class SettingsDialogViewModelTests
{
    [Fact]
    public void Commands_ExposeConfiguredAvailabilityAndRaiseRequests()
    {
        var vm = new SettingsDialogViewModel(
            sleepNowEnabled: false,
            villageStatusSweepEnabled: true,
            dailyGoldSpendingResetEnabled: true,
            dailySilverSpendingResetEnabled: false);
        var saveRequested = false;
        var sweepRequested = false;
        vm.SaveRequested += () => saveRequested = true;
        vm.VillageStatusSweepNowRequested += () => sweepRequested = true;

        Assert.False(vm.SleepNowCommand.CanExecute(null));
        Assert.True(vm.RunVillageStatusSweepNowCommand.CanExecute(null));
        Assert.True(vm.ResetDailyGoldSpendingCommand.CanExecute(null));
        Assert.False(vm.ResetDailySilverSpendingCommand.CanExecute(null));

        vm.SaveCommand.Execute(null);
        vm.RunVillageStatusSweepNowCommand.Execute(null);
        vm.SetVillageStatusSweepRunning(true);

        Assert.True(saveRequested);
        Assert.True(sweepRequested);
        Assert.False(vm.RunVillageStatusSweepNowCommand.CanExecute(null));
    }
}
