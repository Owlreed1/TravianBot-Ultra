using System.Windows;
using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class DashboardHubPanel : UserControl
{
    private MainWindow? _host;

    public DashboardHubPanel() => InitializeComponent();

    private MainWindow? Host => _host ??= Window.GetWindow(this) as MainWindow;

    internal DashboardPanel DashboardPanel => DashboardContent;
    internal bool HasVillagePanels => VillageSettingsContent.Content is not null
        && VillageOverviewContent.Content is not null;
    internal bool IsVillageTabSelected => !ReferenceEquals(DashboardTabControl.SelectedItem, DashboardTabItem);

    internal void SetVillagePanels(VillageSettingsPanel settingsPanel, VillageSettingsPanel overviewPanel)
    {
        (VillageSettingsContent.Content as IDisposable)?.Dispose();
        (VillageOverviewContent.Content as IDisposable)?.Dispose();
        VillageSettingsContent.Content = settingsPanel;
        VillageOverviewContent.Content = overviewPanel;
    }

    internal void ClearVillagePanels()
    {
        (VillageSettingsContent.Content as IDisposable)?.Dispose();
        (VillageOverviewContent.Content as IDisposable)?.Dispose();
        VillageSettingsContent.Content = null;
        VillageOverviewContent.Content = null;
    }

    private void DashboardTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, DashboardTabControl)
            || ReferenceEquals(DashboardTabControl.SelectedItem, DashboardTabItem))
        {
            return;
        }

        Host?.OnDashboardVillageTabSelected();
    }
}
