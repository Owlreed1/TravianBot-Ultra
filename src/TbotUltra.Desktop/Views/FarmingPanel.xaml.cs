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

    public FarmingPanel() => InitializeComponent();

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

}
