using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class ResourcesHubPanel : UserControl
{
    public ResourcesHubPanel() => InitializeComponent();

    internal ResourcesPanel ResourcesPanel => ResourcesContent;
    internal NpcTradePanel TradingPanel => TradingContent;
}
