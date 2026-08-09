using System.Windows;
using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class FarmingPanel : UserControl
{
    public static readonly DependencyProperty NextSendDisplayProperty = DependencyProperty.Register(
        nameof(NextSendDisplay),
        typeof(string),
        typeof(FarmingPanel),
        new PropertyMetadata("Next send: --"));

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

    public string NextSendDisplay
    {
        get => (string)GetValue(NextSendDisplayProperty);
        set => SetValue(NextSendDisplayProperty, value);
    }

    internal void SetNextSendDisplay(string text) => NextSendDisplay = text;

    private void FarmingSettings_Changed(object sender, RoutedEventArgs e) => Host?.OnFarmingSettingsChanged(sender, e);
    private void MoveFarmLossesCheckBox_Checked(object sender, RoutedEventArgs e) => Host?.OnMoveFarmLossesChecked(sender, e);
    private void TravcoInactiveSearchButton_Click(object sender, RoutedEventArgs e) => Host?.OnTravcoInactiveSearchClicked(sender, e);
}
