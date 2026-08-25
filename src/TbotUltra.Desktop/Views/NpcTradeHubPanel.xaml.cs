using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class NpcTradeHubPanel : UserControl
{
    public NpcTradeHubPanel() => InitializeComponent();

    internal NpcTradePanel NpcPanel => NpcContent;
}
