using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class TroopsHubPanel : UserControl
{
    public TroopsHubPanel() => InitializeComponent();

    internal ReinforcementsPanel ReinforcementsPanel => ReinforcementsContent;
    internal ReinforcementsPanel CatapultWavesPanel => CatapultWavesContent;
    internal DataGrid IncomingAttacksGrid => IncomingAttacksContent.IncomingAttacksGrid;

    internal void SelectIncomingAttacks()
    {
        TroopsTabControl.SelectedItem = IncomingAttacksTabItem;
    }
}
