using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TbotUltra.Desktop.Models;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Views;

public partial class TroopEvasionPanel : UserControl
{
    private bool _loading;
    public ObservableCollection<TroopEvasionVillageItem> Villages { get; } = [];
    public event Action? SettingsChanged;
    public event Action<TroopEvasionVillageItem>? ValidateRequested;
    public event Func<TroopEvasionVillageItem, string?>? EnableValidationRequested;

    public TroopEvasionPanel()
    {
        InitializeComponent();
        VillageItemsControl.ItemsSource = Villages;
        LeadTimeComboBox.ItemsSource = new[] { 1, 2, 5, 10 };
        ProtectionWindowComboBox.ItemsSource = new[] { 1, 2, 5, 10 };
        MovementTypeComboBox.ItemsSource = Enum.GetValues<TroopEvasionMovementType>();
    }

    public int LeadTimeMinutes => LeadTimeComboBox.SelectedItem as int? ?? 5;
    public int ProtectionWindowMinutes => ProtectionWindowComboBox.SelectedItem as int? ?? 5;
    public int? TargetX => int.TryParse(TargetXTextBox.Text, out var value) ? value : null;
    public int? TargetY => int.TryParse(TargetYTextBox.Text, out var value) ? value : null;
    public TroopEvasionMovementType MovementType => MovementTypeComboBox.SelectedItem is TroopEvasionMovementType value
        ? value
        : TroopEvasionMovementType.Reinforcement;
    public bool EvadeRaids => EvadeRaidsCheckBox.IsChecked == true;
    public bool EvadeAttacks => EvadeAttacksCheckBox.IsChecked == true;

    public void SetGlobalSettings(
        int lead,
        int protection,
        int? targetX,
        int? targetY,
        TroopEvasionMovementType movementType,
        bool evadeRaids,
        bool evadeAttacks)
    {
        _loading = true;
        LeadTimeComboBox.SelectedItem = lead;
        ProtectionWindowComboBox.SelectedItem = protection;
        TargetXTextBox.Text = targetX?.ToString() ?? string.Empty;
        TargetYTextBox.Text = targetY?.ToString() ?? string.Empty;
        MovementTypeComboBox.SelectedItem = movementType;
        EvadeRaidsCheckBox.IsChecked = evadeRaids;
        EvadeAttacksCheckBox.IsChecked = evadeAttacks;
        _loading = false;
        ApplyGlobalDispatchSettingsToVillages();
    }

    private void GlobalSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) SettingsChanged?.Invoke();
    }

    private void GlobalDispatchSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ApplyGlobalDispatchSettingsToVillages();
        SettingsChanged?.Invoke();
    }

    private void GlobalEvadeForSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loading) SettingsChanged?.Invoke();
    }

    private void ApplyGlobalDispatchSettingsToVillages()
    {
        foreach (var village in Villages)
        {
            village.TargetX = TargetXTextBox.Text;
            village.TargetY = TargetYTextBox.Text;
            village.MovementType = MovementType;
        }
    }

    private void VillageSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if ((sender as FrameworkElement)?.Tag is TroopEvasionVillageItem village)
        {
            village.RefreshDerived();
            if (sender is ToggleButton { IsChecked: true })
            {
                ValidateEnabledVillage(village);
            }
        }
        SettingsChanged?.Invoke();
    }

    internal bool SetVillageEnabled(string villageKey, bool enabled)
    {
        var village = Villages.FirstOrDefault(item => string.Equals(
            item.VillageKey,
            villageKey,
            StringComparison.OrdinalIgnoreCase));
        if (village is null)
        {
            return false;
        }

        village.Enabled = enabled;
        village.RefreshDerived();
        if (enabled)
        {
            ValidateEnabledVillage(village);
        }

        SettingsChanged?.Invoke();
        return village.Enabled;
    }

    private bool ValidateEnabledVillage(TroopEvasionVillageItem village)
    {
        var error = EnableValidationRequested?.Invoke(village);
        if (string.IsNullOrWhiteSpace(error))
        {
            return true;
        }

        _loading = true;
        village.Enabled = false;
        village.RuntimeStatus = error;
        _loading = false;
        AppDialog.ShowCustom(
            Window.GetWindow(this),
            $"Troop evasion cannot be enabled for {village.VillageName}.\n\n{error}",
            "Complete troop evasion settings",
            [("OK", MessageBoxResult.OK)],
            MessageBoxImage.Warning,
            MessageBoxResult.OK,
            MessageBoxResult.OK,
            successResult: MessageBoxResult.OK);
        return false;
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is TroopEvasionVillageItem village) ValidateRequested?.Invoke(village);
    }

    private void SyncSettings_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not TroopEvasionVillageItem source) return;
        var targets = Villages.Where(village => !ReferenceEquals(village, source)).ToList();
        if (targets.Count == 0)
        {
            AppDialog.Show(Window.GetWindow(this), "There are no other villages to sync.", "Sync evasion settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var choices = new StackPanel();
        choices.Children.Add(new TextBlock
        {
            Text = $"Copy troop selections from {source.VillageName} to:",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10),
        });
        foreach (var target in targets)
        {
            choices.Children.Add(new CheckBox
            {
                Content = target.VillageName,
                IsChecked = true,
                Tag = target,
                Margin = new Thickness(0, 0, 0, 7),
            });
        }

        var result = AppDialog.ShowCustomContent(
            Window.GetWindow(this), choices, $"Sync settings from {source.VillageName}",
            [("Sync", MessageBoxResult.Yes), ("Close", MessageBoxResult.Cancel)],
            MessageBoxImage.None, MessageBoxResult.Yes, MessageBoxResult.Cancel,
            successResult: MessageBoxResult.Yes, width: 430, hideIcon: true,
            dangerResult: MessageBoxResult.Cancel);
        if (result != MessageBoxResult.Yes) return;

        foreach (var selected in choices.Children.OfType<CheckBox>().Where(choice => choice.IsChecked == true))
        {
            if (selected.Tag is TroopEvasionVillageItem target) target.CopyTroopSelectionFrom(source);
        }
        SettingsChanged?.Invoke();
    }
}
