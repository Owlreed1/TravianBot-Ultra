using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TbotUltra.Desktop.Common;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>
/// View model backing the in-game build/smithy queue display: owns the two
/// ObservableCollections the queue DataGrids render and the in-place reconcile
/// that keeps existing row instances stable (rows are updated via ApplySnapshot
/// instead of being replaced, so DataGrid selection/scroll never jumps).
/// Snapshot production stays with the caller (LiveQueueRowFactory).
/// </summary>
public sealed class TravianQueueViewModel : BaseViewModel
{
    private QueueItemRow? _selectedActiveQueueRow;
    private readonly RelayCommand _removeCommand;
    private readonly RelayCommand _moveUpCommand;
    private readonly RelayCommand _moveDownCommand;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _clearVillageCommand;
    private readonly RelayCommand _clearAccountCommand;
    private readonly RelayCommand _popOutCommand;
    private bool _canUsePrimaryCommands = true;

    public TravianQueueViewModel()
    {
        _removeCommand = new RelayCommand(() => RemoveRequested?.Invoke(), () => _canUsePrimaryCommands && SelectedActiveQueueRow is not null);
        _moveUpCommand = new RelayCommand(() => MoveUpRequested?.Invoke(), () => _canUsePrimaryCommands && SelectedActiveQueueRow is not null);
        _moveDownCommand = new RelayCommand(() => MoveDownRequested?.Invoke(), () => _canUsePrimaryCommands && SelectedActiveQueueRow is not null);
        RestoreCommand = new RelayCommand(() => RestoreRequested?.Invoke());
        _refreshCommand = new RelayCommand(() => RefreshRequested?.Invoke(), () => _canUsePrimaryCommands);
        _clearVillageCommand = new RelayCommand(() => ClearVillageRequested?.Invoke());
        _clearAccountCommand = new RelayCommand(() => ClearAccountRequested?.Invoke(), () => _canUsePrimaryCommands);
        _popOutCommand = new RelayCommand(() => PopOutRequested?.Invoke());
    }

    public event Action? RemoveRequested;
    public event Action? RestoreRequested;
    public event Action? MoveUpRequested;
    public event Action? MoveDownRequested;
    public event Action? RefreshRequested;
    public event Action? ClearVillageRequested;
    public event Action? ClearAccountRequested;
    public event Action? PopOutRequested;

    public ICommand RemoveCommand => _removeCommand;
    public ICommand RestoreCommand { get; }
    public ICommand MoveUpCommand => _moveUpCommand;
    public ICommand MoveDownCommand => _moveDownCommand;
    public ICommand RefreshCommand => _refreshCommand;
    public ICommand ClearVillageCommand => _clearVillageCommand;
    public ICommand ClearAccountCommand => _clearAccountCommand;
    public ICommand PopOutCommand => _popOutCommand;

    public void SetPrimaryCommandAvailability(bool enabled)
    {
        if (_canUsePrimaryCommands == enabled)
        {
            return;
        }

        _canUsePrimaryCommands = enabled;
        _removeCommand.RaiseCanExecuteChanged();
        _moveUpCommand.RaiseCanExecuteChanged();
        _moveDownCommand.RaiseCanExecuteChanged();
        _refreshCommand.RaiseCanExecuteChanged();
        _clearAccountCommand.RaiseCanExecuteChanged();
    }

    public QueueItemRow? SelectedActiveQueueRow
    {
        get => _selectedActiveQueueRow;
        set
        {
            if (!SetProperty(ref _selectedActiveQueueRow, value))
            {
                return;
            }

            _removeCommand.RaiseCanExecuteChanged();
            _moveUpCommand.RaiseCanExecuteChanged();
            _moveDownCommand.RaiseCanExecuteChanged();
        }
    }
    /// <summary>
    /// Active automation-queue rows. Created once so the Queue panel, its
    /// pop-out, and selection logic retain stable collection identity.
    /// </summary>
    public ObservableCollection<QueueItemRow> ActiveQueueRows { get; } = [];

    /// <summary>Completed and terminal automation-queue rows shown on History.</summary>
    public ObservableCollection<QueueItemRow> HistoryQueueRows { get; } = [];

    /// <summary>Rows shown in the construction-queue DataGrid.</summary>
    public ObservableCollection<TravianBuildQueueRow> BuildQueueRows { get; } = [];

    /// <summary>Rows shown in the smithy-queue DataGrid.</summary>
    public ObservableCollection<TravianSmithyQueueRow> SmithyQueueRows { get; } = [];

    public void ApplyBuildQueueRows(IReadOnlyList<TravianBuildQueueRow> rows)
        => Reconcile(BuildQueueRows, rows, (target, source) => target.ApplySnapshot(source));

    public void ApplySmithyQueueRows(IReadOnlyList<TravianSmithyQueueRow> rows)
        => Reconcile(SmithyQueueRows, rows, (target, source) => target.ApplySnapshot(source));

    public void ApplyActiveQueueRows(IReadOnlyList<QueueItemRow> rows)
        => Replace(ActiveQueueRows, rows);

    public void ApplyHistoryQueueRows(IReadOnlyList<QueueItemRow> rows)
        => Replace(HistoryQueueRows, rows);

    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> rows)
    {
        target.Clear();
        foreach (var row in rows)
        {
            target.Add(row);
        }
    }

    private static void Reconcile<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> rows,
        Action<T, T> applySnapshot)
    {
        var sharedCount = Math.Min(target.Count, rows.Count);
        for (var index = 0; index < sharedCount; index++)
        {
            applySnapshot(target[index], rows[index]);
        }

        while (target.Count > rows.Count)
        {
            target.RemoveAt(target.Count - 1);
        }

        for (var index = sharedCount; index < rows.Count; index++)
        {
            target.Add(rows[index]);
        }
    }
}
