using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop.Views;

public partial class TroopEvasionPanel : UserControl
{
    private bool _loading;
    public ObservableCollection<TroopEvasionVillageItem> Villages { get; } = [];
    public event Action? SettingsChanged;
    public event Action<TroopEvasionVillageItem>? ValidateRequested;

    public TroopEvasionPanel()
    {
        InitializeComponent();
        VillageItemsControl.ItemsSource = Villages;
        LeadTimeComboBox.ItemsSource = new[] { 1, 2, 5, 10 };
        ProtectionWindowComboBox.ItemsSource = new[] { 1, 2, 5, 10 };
    }

    public int LeadTimeMinutes => LeadTimeComboBox.SelectedItem as int? ?? 5;
    public int ProtectionWindowMinutes => ProtectionWindowComboBox.SelectedItem as int? ?? 5;
    public void SetGlobalSettings(int lead, int protection)
    {
        _loading = true;
        LeadTimeComboBox.SelectedItem = lead;
        ProtectionWindowComboBox.SelectedItem = protection;
        _loading = false;
    }
    public void SetPaused(bool paused) => PausedTextBlock.Text = paused ? "Paused — start Continuous Loop or Auto Queue" : "Active";

    private void GlobalSetting_Changed(object sender, SelectionChangedEventArgs e) { if (!_loading) SettingsChanged?.Invoke(); }
    private void VillageSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if ((sender as FrameworkElement)?.Tag is TroopEvasionVillageItem village) village.RefreshDerived();
        SettingsChanged?.Invoke();
    }
    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is TroopEvasionVillageItem village) ValidateRequested?.Invoke(village);
    }
}
