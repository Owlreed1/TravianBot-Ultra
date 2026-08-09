namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private System.Windows.Controls.Button AnalyzeFarmListsButton => FarmingPanelControl.AnalyzeButton;
    private System.Windows.Controls.Button AddFarmsToListButton => FarmingPanelControl.AddFarmsButton;
    private System.Windows.Controls.Button CreateFarmListButton => FarmingPanelControl.CreateListButton;
    private System.Windows.Controls.TextBlock FarmingStatusTextBlock => FarmingPanelControl.StatusText;
    private System.Windows.Controls.Button FarmListSendAllNowButton => FarmingPanelControl.SendAllButton;
    private System.Windows.Controls.ItemsControl FarmListsItemsControl => FarmingPanelControl.FarmLists;
    private System.Windows.Controls.RadioButton FarmSendListPerListRadioButton => FarmingPanelControl.SendListPerListOption;
    private System.Windows.Controls.RadioButton FarmSendAllAtOnceRadioButton => FarmingPanelControl.SendAllAtOnceOption;
    private System.Windows.Controls.TextBox FarmDispatchDelayMinTextBox => FarmingPanelControl.DispatchDelayMin;
    private System.Windows.Controls.TextBox FarmDispatchDelayMaxTextBox => FarmingPanelControl.DispatchDelayMax;
    private System.Windows.Controls.CheckBox DeactivateFarmLossesCheckBox => FarmingPanelControl.DeactivateLossesOption;
    private System.Windows.Controls.CheckBox DeactivateFarmOasisLossesCheckBox => FarmingPanelControl.DeactivateOasisLossesOption;
    private System.Windows.Controls.CheckBox MoveFarmLossesCheckBox => FarmingPanelControl.MoveLossesOption;
    private System.Windows.Controls.ComboBox FarmLossDestinationComboBox => FarmingPanelControl.LossDestinationOption;
    private System.Windows.Controls.Button TravcoInactiveSearchButton => FarmingPanelControl.TravcoSearchButton;

}
