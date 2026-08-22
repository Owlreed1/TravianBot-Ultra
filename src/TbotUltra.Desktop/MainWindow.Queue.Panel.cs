using System.Windows.Controls;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private TextBlock BuildQueueStatusTextBlock => QueuePanelControl.BuildQueueStatus;
    private DataGrid TravianBuildQueueDataGrid => QueuePanelControl.TravianBuildQueue;
    private DataGrid TravianSmithyQueueDataGrid => QueuePanelControl.TravianSmithyQueue;
    private TabControl QueueSectionTabControl => QueuePanelControl.QueueSections;
    private Button QueueRemoveButton => QueuePanelControl.RemoveButton;
    private Button QueueMoveUpButton => QueuePanelControl.MoveUpButton;
    private Button QueueMoveDownButton => QueuePanelControl.MoveDownButton;
    private Button QueueMoveToTopButton => QueuePanelControl.MoveToTopButton;
    private Button QueueMoveToBottomButton => QueuePanelControl.MoveToBottomButton;
    private Button QueueRefreshButton => QueuePanelControl.RefreshButton;
    private DataGrid QueueDataGrid => QueuePanelControl.ActiveQueue;
    private TextBlock QueueTotalWoodTextBlock => QueuePanelControl.TotalWood;
    private TextBlock QueueTotalClayTextBlock => QueuePanelControl.TotalClay;
    private TextBlock QueueTotalIronTextBlock => QueuePanelControl.TotalIron;
    private TextBlock QueueTotalCropTextBlock => QueuePanelControl.TotalCrop;
    private TextBlock QueueTotalTimeTextBlock => QueuePanelControl.TotalTime;
    private TextBlock QueueTotalTimeConstructFasterTextBlock => QueuePanelControl.TotalTimeConstructFaster;
    private TabItem HistoryQueueTabItem => QueuePanelControl.HistoryTab;
    private DataGrid QueueHistoryDataGrid => QueuePanelControl.HistoryQueue;
    private Button QueueClearButton => QueuePanelControl.ClearAccountButton;

    internal void OnQueueSectionSelectionChanged(object sender, SelectionChangedEventArgs e) => QueueSectionTabControl_SelectionChanged(sender, e);
}
