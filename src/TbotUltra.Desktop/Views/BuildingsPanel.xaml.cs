using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.ViewModels;

namespace TbotUltra.Desktop.Views;

/// <summary>
/// Visible Buildings tab content. Service-bound and queue-bound logic stays on
/// MainWindow; this panel only routes the visible interactions back to the host.
/// Hidden compatibility controls remain on MainWindow.
/// </summary>
public partial class BuildingsPanel : UserControl
{
    public BuildingsPanel()
    {
        InitializeComponent();
    }

    private void BuildingTopSlotsView_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = e.Item is BuildingSlotRow row && BuildingsViewModel.IsPinnedBuildingTopSlot(row.SlotId);
    }

    private void BuildingRemainingSlotsView_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = e.Item is BuildingSlotRow row && !BuildingsViewModel.IsPinnedBuildingTopSlot(row.SlotId);
    }

}
