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

}
