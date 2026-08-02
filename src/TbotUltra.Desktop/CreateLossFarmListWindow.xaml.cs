using System.Windows;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop;

public partial class CreateLossFarmListWindow : Window
{
    public CreateLossFarmListWindow(string suggestedName, IReadOnlyList<FarmLossDestinationOption> existingLists)
    {
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);
        ExistingListsComboBox.ItemsSource = existingLists;
        ListNameTextBox.Text = suggestedName;
        UseExistingRadioButton.IsChecked = existingLists.Count > 0;
        CreateNewRadioButton.IsChecked = existingLists.Count == 0;
        if (existingLists.Count > 0)
        {
            ExistingListsComboBox.SelectedIndex = 0;
        }

        Loaded += (_, _) =>
        {
            if (UseExistingRadioButton.IsChecked == true)
            {
                ExistingListsComboBox.Focus();
            }
            else
            {
                ListNameTextBox.Focus();
                ListNameTextBox.SelectAll();
            }
        };
        RefreshState();
    }

    public string ListName => ListNameTextBox.Text.Trim();
    public FarmLossDestinationOption? SelectedExistingDestination
        => UseExistingRadioButton.IsChecked == true
            ? ExistingListsComboBox.SelectedItem as FarmLossDestinationOption
            : null;

    private void ListNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => RefreshState();

    private void DestinationMode_Changed(object sender, RoutedEventArgs e) => RefreshState();

    private void ExistingListsComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ExistingListsComboBox.SelectedItem is not null)
        {
            UseExistingRadioButton.IsChecked = true;
        }

        RefreshState();
    }

    private void RefreshState()
    {
        if (ConfirmButton is null
            || ValidationTextBlock is null
            || UseExistingRadioButton is null
            || ExistingListsComboBox is null
            || ListNameTextBox is null)
        {
            return;
        }

        var name = ListNameTextBox.Text.Trim();
        var useExisting = UseExistingRadioButton.IsChecked == true;
        ExistingListsComboBox.IsEnabled = useExisting;
        ListNameTextBox.IsEnabled = !useExisting;
        ConfirmButton.Content = useExisting ? "Use selected list" : "Create farmlist";
        ConfirmButton.IsEnabled = useExisting
            ? ExistingListsComboBox.SelectedItem is FarmLossDestinationOption
            : name.Length is > 0 and <= 30;
        ValidationTextBlock.Text = useExisting
            ? ExistingListsComboBox.SelectedItem is null ? "Select an existing farmlist." : string.Empty
            : name.Length == 0
                ? "Enter a farmlist name."
                : name.Length > 30 ? "Farm list names can contain at most 30 characters." : string.Empty;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmButton.IsEnabled)
        {
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
