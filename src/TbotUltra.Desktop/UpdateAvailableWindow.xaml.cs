using System;
using System.ComponentModel;
using System.Windows;

namespace TbotUltra.Desktop;

public partial class UpdateAvailableWindow : Window
{
    private bool _actionSelected;

    public event EventHandler? UpdateRequested;
    public event EventHandler? DismissRequested;

    public UpdateAvailableWindow(string currentVersion, string latestVersion)
    {
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);
        CurrentVersionText.Text = $"v{currentVersion}";
        LatestVersionText.Text = $"v{latestVersion}";
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        SelectAction(UpdateRequested);
    }

    private void DontUpdateNowButton_Click(object sender, RoutedEventArgs e)
    {
        SelectAction(DismissRequested);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_actionSelected)
        {
            _actionSelected = true;
            DismissRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(e);
    }

    private void SelectAction(EventHandler? action)
    {
        if (_actionSelected)
        {
            return;
        }

        _actionSelected = true;
        action?.Invoke(this, EventArgs.Empty);
        Close();
    }
}
