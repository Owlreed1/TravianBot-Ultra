using System.Windows;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private System.Windows.Controls.Button LoadResourcesButton => ResourcesPanelControl.RefreshButton;
    private System.Windows.Controls.StackPanel CroplandColumnPanel => ResourcesPanelControl.CroplandColumn;
    private System.Windows.Controls.ItemsControl CroplandItemsControl => ResourcesPanelControl.CroplandItems;

    internal void OnLoadResourcesClicked(object sender, RoutedEventArgs e) => _ = GuardUiAsync(LoadResourcesButtonClickAsync);
    internal void OnUpgradeAllResourcesClicked(object sender, RoutedEventArgs e) => UpgradeAllResources();
    internal void OnUpgradeAllResourcesToMaxClicked(object sender, RoutedEventArgs e) => UpgradeAllResourcesToMax();
    internal void OnResourceBuildStrategyChanged(object sender, RoutedEventArgs e) => ResourceBuildStrategyRadio_Click(sender, e);
    internal void OnResourceUpgradeTypesChanged(object sender, RoutedEventArgs e) => ResourceUpgradeTypesCheckBox_Changed(sender, e);
}
