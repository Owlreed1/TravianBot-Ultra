using System.Windows;
using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class ResourcesPanel : UserControl
{
    private MainWindow? _host;

    public ResourcesPanel() => InitializeComponent();

    private MainWindow? Host => _host ??= Window.GetWindow(this) as MainWindow;

    internal Button RefreshButton => LoadResourcesButton;
    internal StackPanel CroplandColumn => CroplandColumnPanel;
    internal ItemsControl CroplandItems => CroplandItemsControl;

    private void ResourceBuildStrategyRadio_Click(object sender, RoutedEventArgs e) => Host?.OnResourceBuildStrategyChanged(sender, e);
    private void ResourceUpgradeTypeCheckBox_Changed(object sender, RoutedEventArgs e) => Host?.OnResourceUpgradeTypesChanged(sender, e);
}
