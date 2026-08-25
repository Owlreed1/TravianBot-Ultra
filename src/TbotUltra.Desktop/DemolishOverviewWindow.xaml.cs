using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TbotUltra.Desktop;

public partial class DemolishOverviewWindow : Window
{
    private readonly Func<Guid, bool> _removeTask;
    private readonly ObservableCollection<DemolishOverviewRow> _rows;

    internal DemolishOverviewWindow(IReadOnlyList<DemolishOverviewRow> rows, Func<Guid, bool> removeTask)
    {
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);
        _removeTask = removeTask;
        _rows = new ObservableCollection<DemolishOverviewRow>(rows);
        DataContext = _rows;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DemolishOverviewRow { QueueItemId: Guid itemId } row })
        {
            return;
        }

        var choice = AppDialog.ShowCustom(
            this,
            $"Remove the demolition task for {row.VillageName}? A demolition already accepted by Travian will finish normally.",
            "Remove demolition",
            [("Cancel", MessageBoxResult.No), ("Remove", MessageBoxResult.Yes)],
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            MessageBoxResult.No,
            dangerResult: MessageBoxResult.Yes);
        if (choice != MessageBoxResult.Yes || !_removeTask(itemId))
        {
            return;
        }

        var removedIndex = _rows.IndexOf(row);
        _rows.Remove(row);
        if (!_rows.Any(candidate => candidate.CanRemove
                && string.Equals(candidate.VillageKey, row.VillageKey, StringComparison.OrdinalIgnoreCase)))
        {
            _rows.Insert(Math.Min(removedIndex, _rows.Count), DemolishOverviewRow.Empty(row.VillageKey, row.VillageName));
        }
    }
}

internal sealed record DemolishOverviewRow(
    string VillageKey,
    string VillageName,
    string DemolishStatus,
    bool DemolishStatusHasTimer,
    Guid? QueueItemId)
{
    public bool CanRemove => QueueItemId.HasValue;

    public static DemolishOverviewRow Empty(string villageKey, string villageName) =>
        new(villageKey, villageName, "No active demolition", false, null);
}
