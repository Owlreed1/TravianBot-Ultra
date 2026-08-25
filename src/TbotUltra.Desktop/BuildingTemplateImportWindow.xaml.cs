using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;

namespace TbotUltra.Desktop;

public sealed record BuildingTemplateConflictChoice(
    string Label,
    BuildingTemplateImportConflictAction Action);

public sealed class BuildingTemplateImportRowView : INotifyPropertyChanged
{
    private bool _isSelected;
    private BuildingTemplateConflictChoice _selectedConflictChoice;

    public BuildingTemplateImportRowView(
        BuildingTemplateImportCandidate candidate,
        bool hasConflict)
    {
        Candidate = candidate;
        HasConflict = hasConflict;
        _isSelected = candidate.IsValid;
        ConflictChoices =
        [
            new BuildingTemplateConflictChoice("Import as copy", BuildingTemplateImportConflictAction.ImportAsCopy),
            new BuildingTemplateConflictChoice("Overwrite", BuildingTemplateImportConflictAction.Overwrite),
        ];
        _selectedConflictChoice = ConflictChoices[0];
    }

    public BuildingTemplateImportCandidate Candidate { get; }
    public string Name => Candidate.Template.Name;
    public string Tribe => Candidate.Template.CreatedByTribe;
    public int StepCount => Candidate.Template.Rows.Count;
    public bool IsValid => Candidate.IsValid;
    public bool HasConflict { get; }
    public bool IsActionableConflict => IsValid && HasConflict;
    public bool TribeMismatch => Candidate.TribeMismatch;
    public IReadOnlyList<BuildingTemplateConflictChoice> ConflictChoices { get; }
    public string ActionText => IsValid ? "Import" : "Blocked";
    public string DetailText => Candidate.Errors.Count > 0
        ? string.Join("\n", Candidate.Errors)
        : TribeMismatch
            ? $"Created for {Tribe}; review tribe-specific buildings before queueing."
            : HasConflict
                ? "A local template has the same ID."
                : "Ready to import.";
    public string StatusText => Candidate.Errors.Count > 0
        ? $"Invalid · {Candidate.Errors[0]}"
        : HasConflict && TribeMismatch
            ? "Conflict · Different tribe"
            : HasConflict
                ? "Conflict"
                : TribeMismatch
                    ? "Different tribe"
                    : "New";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!IsValid || _isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public BuildingTemplateConflictChoice SelectedConflictChoice
    {
        get => _selectedConflictChoice;
        set
        {
            if (value is null || Equals(_selectedConflictChoice, value)) return;
            _selectedConflictChoice = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedConflictChoice)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class BuildingTemplateImportWindow : Window, INotifyPropertyChanged
{
    private string _summaryText = string.Empty;

    public ObservableCollection<BuildingTemplateImportRowView> Rows { get; }
    public IReadOnlyList<BuildingTemplateImportSelection> Selections { get; private set; } = [];

    public string SummaryText
    {
        get => _summaryText;
        private set
        {
            if (_summaryText == value) return;
            _summaryText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SummaryText)));
        }
    }

    public BuildingTemplateImportWindow(
        IReadOnlyList<BuildingTemplateImportCandidate> candidates,
        IReadOnlyCollection<Guid> existingTemplateIds)
    {
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);
        Rows = new ObservableCollection<BuildingTemplateImportRowView>(
            candidates.Select(candidate => new BuildingTemplateImportRowView(
                candidate,
                candidate.Template.Id != Guid.Empty && existingTemplateIds.Contains(candidate.Template.Id))));
        foreach (var row in Rows)
        {
            row.PropertyChanged += (_, _) => RefreshSummary();
        }

        DataContext = this;
        RefreshSummary();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RefreshSummary()
    {
        var selected = Rows.Count(row => row.IsSelected);
        var invalid = Rows.Count(row => !row.IsValid);
        var conflicts = Rows.Count(row => row.IsValid && row.HasConflict);
        SummaryText = $"{selected} selected · {conflicts} conflict(s) · {invalid} invalid";
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedRows = Rows.Where(row => row.IsSelected).ToList();
        if (selectedRows.Count == 0)
        {
            AppDialog.Show(
                this,
                "Select at least one valid template to import.",
                "Import building templates",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Selections = selectedRows.Select(row => new BuildingTemplateImportSelection(
            row.Candidate.Template,
            true,
            row.SelectedConflictChoice.Action)).ToList();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
