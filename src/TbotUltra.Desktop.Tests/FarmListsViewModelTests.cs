using System.Linq;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.ViewModels;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class FarmListsViewModelTests
{
    private static FarmListStatusRow Real(string name, int? remainingSeconds = null) =>
        new() { Name = name, TotalFarmCount = 1, RemainingSeconds = remainingSeconds };

    [Fact]
    public void EnsurePlaceholderRow_EmptyCollectionGetsOnePlaceholder()
    {
        var vm = new FarmListsViewModel();

        vm.EnsurePlaceholderRow();
        vm.EnsurePlaceholderRow();

        var row = Assert.Single(vm.FarmLists);
        Assert.True(row.IsPlaceholder);
        Assert.False(row.IsEnabled);
    }

    [Fact]
    public void EnsurePlaceholderRow_RealRowsRemovePlaceholder()
    {
        var vm = new FarmListsViewModel();
        vm.EnsurePlaceholderRow();
        vm.FarmLists.Add(Real("List A"));

        vm.EnsurePlaceholderRow();

        Assert.DoesNotContain(vm.FarmLists, row => row.IsPlaceholder);
        Assert.Single(vm.FarmLists);
    }

    [Fact]
    public void DescribeStatus_NoRealListsPromptsAnalyze()
    {
        var vm = new FarmListsViewModel();
        vm.EnsurePlaceholderRow();

        Assert.Equal("No farm lists loaded. Click Analyze Farmlists.", vm.DescribeStatus());
    }

    [Fact]
    public void DescribeStatus_CountsLoadedAndReadyLists()
    {
        var vm = new FarmListsViewModel();
        vm.FarmLists.Add(Real("List A"));
        vm.FarmLists.Add(Real("List B", remainingSeconds: 120));

        Assert.Equal("Loaded 2 farm list(s). Ready: 1.", vm.DescribeStatus());
    }

    [Fact]
    public void EmptyFarmList_IsNotReadyToSend_AndShowsEmptyAction()
    {
        var row = new FarmListStatusRow
        {
            Name = "Empty list",
            IsEnabled = true,
            TotalFarmCount = 0,
        };

        Assert.True(row.IsEmpty);
        Assert.Equal("Empty", row.ReadyText);
        Assert.Equal("Empty", row.ActionText);
        Assert.False(row.CanSendNow);
    }

    [Fact]
    public void LastSentText_UsesElapsedTimeAndKeepsDisabledListsNeutral()
    {
        var row = new FarmListStatusRow
        {
            Name = "Raiders",
            IsEnabled = true,
            LastSentAtUtc = DateTimeOffset.UtcNow.AddHours(-1).AddMinutes(-2),
        };

        Assert.Matches(@"^01:02:\d{2}$", row.LastSentText);

        row.IsEnabled = false;

        Assert.Equal("00:00:00", row.LastSentText);
    }

    [Fact]
    public void LastSentText_AppliesConfiguredLimitOrHardFiveDayCap()
    {
        var row = new FarmListStatusRow
        {
            Name = "Raiders",
            IsEnabled = true,
            LastSentAtUtc = DateTimeOffset.UtcNow.AddHours(-25),
            LastSentLimitEnabled = true,
            LastSentLimitHours = 24,
        };

        Assert.Equal("24h+", row.LastSentText);

        row.LastSentLimitEnabled = false;

        Assert.Matches(@"^25:00:\d{2}$", row.LastSentText);
        row.LastSentAtUtc = DateTimeOffset.UtcNow.AddHours(-121);
        Assert.Equal("120h+", row.LastSentText);
    }

    [Fact]
    public void IsRealRow_PlaceholderIsNotReal()
    {
        Assert.False(FarmListsViewModel.IsRealRow(new FarmListStatusRow { IsPlaceholder = true }));
        Assert.True(FarmListsViewModel.IsRealRow(Real("List A")));
    }

    [Fact]
    public void Commands_FollowGlobalAvailabilityAndForwardSelectedFarmList()
    {
        var vm = new FarmListsViewModel();
        var row = Real("List A");
        FarmListStatusRow? requested = null;
        vm.SendNowRequested += value => requested = value;

        vm.UpdateCommandAvailability(canAnalyze: false, canManageLists: false, canCreate: false, canSendAll: false);

        Assert.False(vm.AnalyzeCommand.CanExecute(null));
        Assert.False(vm.SendNowCommand.CanExecute(row));

        vm.UpdateCommandAvailability(canAnalyze: true, canManageLists: true, canCreate: true, canSendAll: true);
        vm.SendNowCommand.Execute(row);

        Assert.True(vm.AnalyzeCommand.CanExecute(null));
        Assert.Same(row, requested);
    }

    [Fact]
    public void BuildFarmListVillageHeader_UsesKnownCoordinatesImmediately()
    {
        var header = MainWindow.BuildFarmListVillageHeader(
            "Swollster",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Swollster"] = "(120 | 14)",
            });

        Assert.Equal("Swollster (120 | 14)", header);
    }

    [Fact]
    public void FarmingSettings_NotifyForUserChanges_NotInitialLoad()
    {
        var vm = new FarmListsViewModel();
        var changes = 0;
        vm.SettingsChanged += () => changes++;

        vm.LoadSettings(
            sendAllLists: true,
            dispatchDelayMinMinutes: 10,
            dispatchDelayMaxMinutes: 20,
            deactivateLosses: true,
            deactivateOasisLosses: false,
            moveLosses: false);
        vm.DeactivateOasisLosses = true;

        Assert.Equal(1, changes);
        Assert.True(vm.SendAllLists);
        Assert.Equal("10", vm.DispatchDelayMinMinutes);
    }

    [Fact]
    public void MoveLosses_UserEnableRequestsDestinationSetup_InitialLoadDoesNot()
    {
        var vm = new FarmListsViewModel();
        var requests = 0;
        vm.MoveLossesEnabledRequested += () => requests++;

        vm.LoadSettings(
            sendAllLists: false,
            dispatchDelayMinMinutes: 15,
            dispatchDelayMaxMinutes: 30,
            deactivateLosses: true,
            deactivateOasisLosses: false,
            moveLosses: true);

        Assert.Equal(0, requests);

        vm.MoveLosses = false;
        vm.MoveLosses = true;

        Assert.Equal(1, requests);
    }

    [Fact]
    public void ReplaceLossDestinations_KeepsCollectionBindingStable_WithoutSavingSettings()
    {
        var vm = new FarmListsViewModel();
        var collection = vm.LossDestinations;
        var changes = 0;
        vm.SettingsChanged += () => changes++;
        var selected = new FarmLossDestinationOption("42", "yellow", "Capital", 3, 100);

        vm.ReplaceLossDestinations(
            [
                new FarmLossDestinationOption("41", "raiders", "Capital", 10, 100),
                selected,
            ],
            selected);

        Assert.Same(collection, vm.LossDestinations);
        Assert.Equal(2, vm.LossDestinations.Count);
        Assert.Same(selected, vm.SelectedLossDestination);
        Assert.Equal(0, changes);
    }
}
