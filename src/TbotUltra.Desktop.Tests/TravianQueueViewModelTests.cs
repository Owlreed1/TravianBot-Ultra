using System.Collections.Generic;
using System.Collections.Specialized;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.ViewModels;
using System.Windows.Input;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TravianQueueViewModelTests
{
    private static TravianBuildQueueRow Row(string name) => new() { Name = name };

    [Fact]
    public void ApplyBuildQueueRows_UpdatesSharedRowsInPlace()
    {
        var vm = new TravianQueueViewModel();
        vm.ApplyBuildQueueRows(new List<TravianBuildQueueRow> { Row("Old A"), Row("Old B") });
        var firstInstance = vm.BuildQueueRows[0];

        vm.ApplyBuildQueueRows(new List<TravianBuildQueueRow> { Row("New A"), Row("New B") });

        Assert.Same(firstInstance, vm.BuildQueueRows[0]);
        Assert.Equal("New A", vm.BuildQueueRows[0].Name);
        Assert.Equal("New B", vm.BuildQueueRows[1].Name);
    }

    [Fact]
    public void ApplyBuildQueueRows_TrimsExtraTailRows()
    {
        var vm = new TravianQueueViewModel();
        vm.ApplyBuildQueueRows(new List<TravianBuildQueueRow> { Row("A"), Row("B"), Row("C") });

        vm.ApplyBuildQueueRows(new List<TravianBuildQueueRow> { Row("A") });

        Assert.Single(vm.BuildQueueRows);
    }

    [Fact]
    public void ApplyBuildQueueRows_AppendsNewRowsBeyondShared()
    {
        var vm = new TravianQueueViewModel();
        vm.ApplyBuildQueueRows(new List<TravianBuildQueueRow> { Row("A") });

        vm.ApplyBuildQueueRows(new List<TravianBuildQueueRow> { Row("A"), Row("B"), Row("C") });

        Assert.Equal(3, vm.BuildQueueRows.Count);
        Assert.Equal("C", vm.BuildQueueRows[2].Name);
    }

    [Fact]
    public void ApplyBuildQueueRows_EmptySnapshotClearsRows()
    {
        var vm = new TravianQueueViewModel();
        vm.ApplyBuildQueueRows(new List<TravianBuildQueueRow> { Row("A") });

        vm.ApplyBuildQueueRows(new List<TravianBuildQueueRow>());

        Assert.Empty(vm.BuildQueueRows);
    }

    [Fact]
    public void ApplyActiveQueueRows_PreservesTheBoundCollection()
    {
        var vm = new TravianQueueViewModel();
        var collection = vm.ActiveQueueRows;
        vm.ApplyActiveQueueRows([new QueueItemRow { DisplayName = "Old" }]);

        vm.ApplyActiveQueueRows([new QueueItemRow { DisplayName = "New" }]);

        Assert.Same(collection, vm.ActiveQueueRows);
        Assert.Equal("New", vm.ActiveQueueRows[0].DisplayName);
    }

    [Fact]
    public void ApplyHistoryQueueRows_ClearsObsoleteRows()
    {
        var vm = new TravianQueueViewModel();
        vm.ApplyHistoryQueueRows([new QueueItemRow { DisplayName = "Completed" }]);

        vm.ApplyHistoryQueueRows([]);

        Assert.Empty(vm.HistoryQueueRows);
    }

    [Fact]
    public void ApplyQueueRows_ReusesUnchangedRowsWithoutResettingTheCollection()
    {
        var vm = new TravianQueueViewModel();
        var first = new QueueItemRow { Id = Guid.NewGuid(), DisplayName = "First" };
        var second = new QueueItemRow { Id = Guid.NewGuid(), DisplayName = "Second" };
        vm.ApplyActiveQueueRows([first, second]);
        var actions = new List<NotifyCollectionChangedAction>();
        vm.ActiveQueueRows.CollectionChanged += (_, args) => actions.Add(args.Action);

        vm.ApplyActiveQueueRows([first, second]);

        Assert.Same(first, vm.ActiveQueueRows[0]);
        Assert.Same(second, vm.ActiveQueueRows[1]);
        Assert.Empty(actions);
    }

    [Fact]
    public void QueueCommands_RequireSelectionOnlyForSelectedItemActions()
    {
        var vm = new TravianQueueViewModel();

        Assert.False(vm.RemoveCommand.CanExecute(null));
        Assert.False(vm.MoveUpCommand.CanExecute(null));
        Assert.True(vm.RefreshCommand.CanExecute(null));

        vm.SelectedActiveQueueRow = new QueueItemRow { DisplayName = "Upgrade" };

        Assert.True(vm.RemoveCommand.CanExecute(null));
        Assert.True(vm.MoveUpCommand.CanExecute(null));
    }

    [Fact]
    public void PrimaryQueueCommands_FollowTheHostBusyGate()
    {
        var vm = new TravianQueueViewModel
        {
            SelectedActiveQueueRow = new QueueItemRow { DisplayName = "Upgrade" },
        };

        vm.SetPrimaryCommandAvailability(false);

        Assert.False(vm.RemoveCommand.CanExecute(null));
        Assert.False(vm.MoveDownCommand.CanExecute(null));
        Assert.False(vm.RefreshCommand.CanExecute(null));
        Assert.False(vm.ClearAccountCommand.CanExecute(null));
        Assert.True(vm.PopOutCommand.CanExecute(null));
    }
}
