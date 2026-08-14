using System.Collections.ObjectModel;
using System.Windows;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop;

public partial class HeroCropAntiStarveVillagesWindow : Window
{
    private readonly ObservableCollection<HeroCropAntiStarveVillageRow> _rows;

    public HeroCropAntiStarveVillagesWindow(IEnumerable<HeroCropAntiStarveVillageRow> rows)
    {
        InitializeComponent();
        _rows = new ObservableCollection<HeroCropAntiStarveVillageRow>(rows.Select(row =>
            new HeroCropAntiStarveVillageRow(row.VillageKey, row.VillageName, row.IsEnabled)));
        DataContext = _rows;
    }

    public IReadOnlyList<HeroCropAntiStarveVillageRow> Results => _rows;

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.IsEnabled = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.IsEnabled = false;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
