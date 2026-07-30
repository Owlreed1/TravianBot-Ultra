using System.Collections.Generic;
using System.Windows;

namespace TbotUltra.Desktop;

public partial class DemolishOverviewWindow : Window
{
    internal DemolishOverviewWindow(IReadOnlyList<DemolishOverviewRow> rows)
    {
        InitializeComponent();
        DataContext = rows;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

internal sealed record DemolishOverviewRow(string VillageName, string DemolishStatus);
