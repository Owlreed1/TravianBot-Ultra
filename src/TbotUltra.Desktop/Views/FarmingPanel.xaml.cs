using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TbotUltra.Desktop.Views;

public partial class FarmingPanel : UserControl
{
    private MainWindow? _host;

    public FarmingPanel() => InitializeComponent();

    private MainWindow? Host => _host ??= Window.GetWindow(this) as MainWindow;

    internal Button AnalyzeButton => AnalyzeFarmListsButton;
    internal Button AddFarmsButton => AddFarmsToListButton;
    internal Button CreateListButton => CreateFarmListButton;
    internal TextBlock StatusText => FarmingStatusTextBlock;
    internal Button SendAllButton => FarmListSendAllNowButton;
    internal ItemsControl FarmLists => FarmListsItemsControl;
    internal RadioButton SendListPerListOption => FarmSendListPerListRadioButton;
    internal RadioButton SendAllAtOnceOption => FarmSendAllAtOnceRadioButton;
    internal TextBox DispatchDelayMin => FarmDispatchDelayMinTextBox;
    internal TextBox DispatchDelayMax => FarmDispatchDelayMaxTextBox;
    internal CheckBox DeactivateLossesOption => DeactivateFarmLossesCheckBox;
    internal CheckBox DeactivateOasisLossesOption => DeactivateFarmOasisLossesCheckBox;
    internal CheckBox MoveLossesOption => MoveFarmLossesCheckBox;
    internal ComboBox LossDestinationOption => FarmLossDestinationComboBox;
    internal Button TravcoSearchButton => TravcoInactiveSearchButton;

    internal void SetNextSendDisplay(string text)
    {
        foreach (var element in FindVisualChildren<FrameworkElement>(this))
        {
            if (Equals(element.Tag, "FarmListNextSendText") && element is TextBlock textBlock)
            {
                textBlock.Text = text;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void AnalyzeFarmListsButton_Click(object sender, RoutedEventArgs e) => Host?.OnAnalyzeFarmListsClicked(sender, e);
    private void AddFarmsToListButton_Click(object sender, RoutedEventArgs e) => Host?.OnAddFarmsToListClicked(sender, e);
    private void CreateFarmListButton_Click(object sender, RoutedEventArgs e) => Host?.OnCreateFarmListClicked(sender, e);
    private void FarmListSendAllNowButton_Click(object sender, RoutedEventArgs e) => Host?.OnFarmListSendAllNowClicked(sender, e);
    private void FarmListSendNowButton_Click(object sender, RoutedEventArgs e) => Host?.OnFarmListSendNowClicked(sender, e);
    private void FarmingSettings_Changed(object sender, RoutedEventArgs e) => Host?.OnFarmingSettingsChanged(sender, e);
    private void MoveFarmLossesCheckBox_Checked(object sender, RoutedEventArgs e) => Host?.OnMoveFarmLossesChecked(sender, e);
    private void TravcoInactiveSearchButton_Click(object sender, RoutedEventArgs e) => Host?.OnTravcoInactiveSearchClicked(sender, e);
}
