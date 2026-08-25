using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class HeroHubPanel : UserControl
{
    public HeroHubPanel() => InitializeComponent();

    private void HeroTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, HeroTabControl)
            && ReferenceEquals(HeroTabControl.SelectedItem, HeroInventoryTabItem))
        {
            HeroInventoryPanel.RefreshHeroResourceSettings();
        }
    }
}
