using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using TbotUltra.Desktop.ViewModels;

namespace TbotUltra.Desktop;

public partial class TroopTrainingSettingsSyncWindow : Window
{
    public ObservableCollection<TroopTrainingQuickVillageRow> Rows { get; }
    public ObservableCollection<TroopTrainingSyncTargetOption> Targets { get; }

    public TroopTrainingQuickVillageRow? SourceRow { get; private set; }
    public IReadOnlyList<TroopTrainingQuickVillageRow> TargetRows { get; private set; } = [];

    public TroopTrainingSettingsSyncWindow(IReadOnlyList<TroopTrainingQuickVillageRow> rows)
    {
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);

        Rows = new ObservableCollection<TroopTrainingQuickVillageRow>(rows);
        Targets = new ObservableCollection<TroopTrainingSyncTargetOption>(
            rows.Select(row => new TroopTrainingSyncTargetOption(row)));
        DataContext = this;
        SourceVillageComboBox.ItemsSource = Rows;

        if (Rows.Count > 0)
        {
            SourceVillageComboBox.SelectedIndex = 0;
        }
    }

    private void SourceVillageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var source = SourceVillageComboBox.SelectedItem as TroopTrainingQuickVillageRow;
        foreach (var target in Targets)
        {
            target.IsSource = ReferenceEquals(target.Row, source);
        }
    }

    private void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        SourceRow = SourceVillageComboBox.SelectedItem as TroopTrainingQuickVillageRow;
        if (SourceRow is null)
        {
            AppDialog.Show(this, "Select a source village.", "Sync troop settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TargetRows = Targets
            .Where(item => item.IsSelected && !ReferenceEquals(item.Row, SourceRow))
            .Select(item => item.Row)
            .ToList();
        if (TargetRows.Count == 0)
        {
            AppDialog.Show(this, "Select at least one target village.", "Sync troop settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}

public sealed class TroopTrainingSyncTargetOption : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private bool _isSource;

    public TroopTrainingSyncTargetOption(TroopTrainingQuickVillageRow row)
    {
        Row = row;
    }

    public TroopTrainingQuickVillageRow Row { get; }
    public string VillageName => Row.VillageName;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsSource
    {
        get => _isSource;
        set
        {
            if (_isSource == value)
            {
                return;
            }

            _isSource = value;
            if (value)
            {
                IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSelect));
            OnPropertyChanged(nameof(SourceVisibility));
        }
    }

    public bool CanSelect => !IsSource;
    public Visibility SourceVisibility => IsSource ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
