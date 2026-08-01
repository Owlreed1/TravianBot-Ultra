using System.Windows;

namespace TbotUltra.Desktop;

public partial class CreateLossFarmListWindow : Window
{
    public CreateLossFarmListWindow(string suggestedName)
    {
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);
        ListNameTextBox.Text = suggestedName;
        ListNameTextBox.SelectAll();
        Loaded += (_, _) => ListNameTextBox.Focus();
        RefreshState();
    }

    public string ListName => ListNameTextBox.Text.Trim();

    private void ListNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => RefreshState();

    private void RefreshState()
    {
        if (CreateButton is null || ValidationTextBlock is null)
        {
            return;
        }

        var name = ListNameTextBox?.Text?.Trim() ?? string.Empty;
        CreateButton.IsEnabled = name.Length is > 0 and <= 30;
        ValidationTextBlock.Text = name.Length == 0
            ? "Enter a farmlist name."
            : name.Length > 30 ? "Farm list names can contain at most 30 characters." : string.Empty;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (CreateButton.IsEnabled)
        {
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
