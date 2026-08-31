using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.ViewModels;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class HeroViewModelTests
{
    [Fact]
    public void ApplyObservedInventory_OnlyReportsAnIncreaseForTheSameAccountAndServer()
    {
        var vm = new HeroViewModel();
        vm.SeedObservedInventory("main", "https://ts1.example", new HeroInventoryResources(1, 2, 3, 4));

        Assert.True(vm.ApplyObservedInventory("main", "https://ts1.example", new HeroInventoryResources(2, 2, 3, 4)));
        Assert.False(vm.ApplyObservedInventory("other", "https://ts1.example", new HeroInventoryResources(3, 2, 3, 4)));
    }

    [Fact]
    public void LoadSettingsFromConfig_UsesResourcesFirstDefaultPriority()
    {
        var vm = new HeroViewModel();

        vm.LoadSettingsFromConfig(new BotOptions());

        Assert.Equal(
            ["resources", "fighting_strength", "offence_bonus", "defence_bonus"],
            vm.AttributePriorityItems.Select(item => item.Key));
        Assert.Equal(
            "resources,fighting_strength,offence_bonus,defence_bonus",
            vm.BuildPriorityPayload());
    }

    [Fact]
    public void BuildPriorityPayload_PreservesUiOrder()
    {
        var vm = new HeroViewModel();
        vm.LoadPriorityFromConfig("resources,fighting_strength,offence_bonus,defence_bonus");

        vm.AttributePriorityItems.Move(1, 0);
        vm.UpdateOrders();

        Assert.Equal(
            "fighting_strength,resources,offence_bonus,defence_bonus",
            vm.BuildPriorityPayload());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadSettingsFromConfig_LoadsAutoUseOintments(bool enabled)
    {
        var vm = new HeroViewModel();
        var options = new BotOptions
        {
            HeroAutoUseOintments = enabled,
        };

        vm.LoadSettingsFromConfig(options);

        Assert.Equal(enabled, vm.AutoUseOintments);
    }

    [Theory]
    [InlineData(50, 50)]
    [InlineData(90, 90)]
    [InlineData(100, 100)]
    [InlineData(75, 100)]
    public void OintmentTargetHpPercent_AllowsOnlyUiChoices(int requested, int expected)
    {
        var vm = new HeroViewModel { OintmentTargetHpPercent = requested };

        Assert.Equal(expected, vm.OintmentTargetHpPercent);
    }

    [Fact]
    public void AdventureVideoChancePercent_DefaultsLoadsAndClamps()
    {
        var vm = new HeroViewModel();

        Assert.Equal(70, vm.AdventureVideoChancePercent);

        vm.LoadSettingsFromConfig(new BotOptions { HeroAdventureVideoChancePercent = 35 });
        Assert.Equal(35, vm.AdventureVideoChancePercent);

        vm.AdventureVideoChancePercent = -1;
        Assert.Equal(0, vm.AdventureVideoChancePercent);

        vm.AdventureVideoChancePercent = 101;
        Assert.Equal(100, vm.AdventureVideoChancePercent);
    }

    [Fact]
    public void ResetRuntimeState_ClearsPreviousAccountValues()
    {
        var vm = new HeroViewModel();
        vm.LoadPriorityFromConfig(null);
        vm.ApplyAttributeSnapshot(new HeroAttributeSnapshot(
            FreePoints: 5,
            FightingStrength: 10,
            OffenceBonus: 20,
            DefenceBonus: 30,
            Resources: 40));
        vm.ApplyInventory(new HeroInventoryResources(1, 2, 3, 4));
        vm.AdventureCountText = "7";
        vm.HeroHpText = "95%";

        vm.ResetRuntimeState();

        Assert.Equal("?", vm.AdventureCountText);
        Assert.Equal("?", vm.HeroHpText);
        Assert.Equal("-", vm.HeroInventoryWood);
        Assert.Equal("Hero stats not loaded.", vm.AttributesStatusText);
        Assert.All(vm.AttributePriorityItems, item => Assert.Equal("-", item.PointsText));
    }

    [Fact]
    public void ApplyAttributeSnapshot_DisplaysResourceAndFreePointsFromSnapshot()
    {
        var vm = new HeroViewModel();
        vm.LoadPriorityFromConfig(null);

        vm.ApplyAttributeSnapshot(new HeroAttributeSnapshot(FreePoints: 4, Resources: 28));

        var resources = Assert.Single(vm.AttributePriorityItems, item => item.Key == "resources");
        Assert.Equal("28", resources.PointsText);
        Assert.Equal("Free points: 4", vm.AttributesStatusText);
    }

    [Fact]
    public void HeroReadyText_SaysNoAdventures_WhenHomeAndReadyWithoutAdventures()
    {
        var vm = new HeroViewModel();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName ?? string.Empty);

        // Hero home/idle = "Ready". No adventures available -> "No adventures".
        vm.HeroStatusText = "Ready";
        vm.AdventureCountText = "0";
        Assert.Equal("No adventures", vm.HeroReadyText);
        Assert.Contains(nameof(HeroViewModel.HeroReadyText), changes);

        // A newly discovered adventure flips it back to "Ready".
        vm.AdventureCountText = "1";
        Assert.Equal("Ready", vm.HeroReadyText);

        // Away/other states never read "No adventures" (the timer shows instead).
        vm.HeroStatusText = "Away";
        vm.AdventureCountText = "0";
        Assert.Equal("Ready", vm.HeroReadyText);
    }

    [Fact]
    public void HeroLoopTask_UsesSharedAutomationLoopCountdown()
    {
        var vm = new HeroViewModel();
        var loopTask = new LoopTaskOption
        {
            IsEnabled = false,
            RemainingSeconds = 65,
        };

        vm.HeroLoopTask = loopTask;
        loopTask.TickOneSecond();

        Assert.Same(loopTask, vm.HeroLoopTask);
        Assert.False(vm.HeroLoopTask.HasTimer);
        Assert.True(vm.HeroLoopTask.HasCountdown);
        Assert.Equal("01:04", vm.HeroLoopTask.TimerText);
    }

    [Fact]
    public void ManualCommands_DisableTogetherWhileAnOperationRuns()
    {
        var vm = new HeroViewModel();
        var refreshRequested = false;
        vm.RefreshStatsRequested += () => refreshRequested = true;

        vm.RefreshStatsCommand.Execute(null);
        vm.SetManualOperationRunning(true);

        Assert.True(refreshRequested);
        Assert.False(vm.RefreshStatsCommand.CanExecute(null));
        Assert.False(vm.RefreshInventoryCommand.CanExecute(null));

        vm.SetManualOperationRunning(false);

        Assert.True(vm.RefreshStatsCommand.CanExecute(null));
    }
}
