using System.Windows;

namespace TbotUltra.Desktop;

public partial class ManualLoginWindow : Window
{
    public ManualLoginWindow(string? validationMessage)
    {
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            ValidationTextBlock.Text = validationMessage;
            ValidationTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void LoginDoneButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
