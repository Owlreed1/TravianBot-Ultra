using System.Windows;
using TbotUltra.Core.Tasks;
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
    private void OnResourcesSettingsChanged(ViewModels.ResourceSettingsChange change)
    {
        if (change == ViewModels.ResourceSettingsChange.BuildStrategy)
        {
            PersistResourceBuildStrategyToConfig();
            return;
        }

        if (_loadingResourceUpgradeTypes)
        {
            return;
        }

        var village = GetSelectedVillageKeyInfoOrNull();
        if (village is null)
        {
            return;
        }

        _resourcesPanelService.SaveUpgradeTypes(village, _resourcesViewModel.SelectedUpgradeTypes);
        AppendLog($"Resource upgrade types for '{village.Name}' set to {ResourceUpgradeSelection.Serialize(_resourcesViewModel.SelectedUpgradeTypes)}.");
    }
}
