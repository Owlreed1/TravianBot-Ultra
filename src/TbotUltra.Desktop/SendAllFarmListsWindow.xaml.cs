using System.Windows;

namespace TbotUltra.Desktop;

/// <summary>
/// Small chooser shown by the farming tab's "Send all Now" button: send only the toggled farm lists
/// (paced, like continuous farming) or send every list at once via Travian's "Start all farm lists" button.
/// </summary>
public partial class SendAllFarmListsWindow : Window
{
    public enum SendAllChoice
    {
        Cancel,
        Toggled,
        All,
    }

    public SendAllChoice Choice { get; private set; } = SendAllChoice.Cancel;

    public SendAllFarmListsWindow(Window? owner)
    {
        InitializeComponent();
        if (owner is not null)
        {
            Owner = owner;
        }
    }

    private void SendToggledButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = SendAllChoice.Toggled;
        DialogResult = true;
        Close();
    }

    private void SendAllButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = SendAllChoice.All;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = SendAllChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
