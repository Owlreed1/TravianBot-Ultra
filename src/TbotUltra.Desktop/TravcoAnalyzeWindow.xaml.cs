using System.Windows;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;
using TbotUltra.Desktop.ViewModels;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

/// <summary>
/// Self-contained "Analyze Travco" workflow popup. The user configures the inactive search here,
/// runs the analysis (which reads the first Travco page and discovers how many pages exist), then
/// saves every result page as one local list. The parent window only opens this dialog and refreshes
/// its saved-list panel afterwards; all search/scrape/save logic lives here.
/// </summary>
public partial class TravcoAnalyzeWindow : Window
{
    private readonly TravcoListStore _store;
    private readonly Func<TravcoSearchRequest, IProgress<TravcoSearchProgress>, CancellationToken, Task<TravcoScrapeResult>> _searchRequested;
    private readonly Func<IProgress<(int CurrentPage, int TotalPages)>, CancellationToken, Task<TravcoScrapeResult>> _scrapeAllPagesRequested;
    private readonly Action<string>? _log;
    private readonly CancellationToken _windowToken;
    private readonly TravcoToolsViewModel _viewModel = new();
    private CancellationTokenSource? _activeOperationCts;
    private TravcoScrapeResult? _lastAnalysis;
    private bool _busy;

    /// <summary>True once at least one list was saved, so the caller can refresh its saved-list panel.</summary>
    public bool ListSaved { get; private set; }

    public TravcoAnalyzeWindow(
        TravcoListStore store,
        IReadOnlyList<VillageSelectionItem> villages,
        VillageSelectionItem? defaultVillage,
        Func<TravcoSearchRequest, IProgress<TravcoSearchProgress>, CancellationToken, Task<TravcoScrapeResult>> searchRequested,
        Func<IProgress<(int CurrentPage, int TotalPages)>, CancellationToken, Task<TravcoScrapeResult>> scrapeAllPagesRequested,
        Action<string>? log,
        CancellationToken windowToken)
    {
        _store = store;
        _searchRequested = searchRequested;
        _scrapeAllPagesRequested = scrapeAllPagesRequested;
        _log = log;
        _windowToken = windowToken;
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);
        DataContext = _viewModel;
        foreach (var village in villages)
        {
            _viewModel.Villages.Add(village);
        }

        _viewModel.SelectedVillage = defaultVillage
            ?? villages.FirstOrDefault(village => village.IsCapital)
            ?? villages.FirstOrDefault();
        // This popup only ever saves all pages, so seed a name that reflects that instead of "page 1".
        _viewModel.ListName = "Travco all pages";
        _viewModel.StatusText = "Configure the search and click Analyze Travco.";
    }

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e) => _ = RunAnalyzeAsync();

    private async Task RunAnalyzeAsync()
    {
        if (_busy)
        {
            return;
        }

        var village = _viewModel.SelectedVillage;
        if (village?.CoordX is null || village.CoordY is null)
        {
            SetStatus("Select a village with coordinates.");
            return;
        }

        if (!int.TryParse(_viewModel.DaysInactiveText, out var daysInactive) || daysInactive is < 1 or > 7)
        {
            SetStatus("Active days must be a whole number between 1 and 7.");
            return;
        }

        SetBusy(true);
        var request = new TravcoSearchRequest(
            village.CoordX.Value,
            village.CoordY.Value,
            daysInactive,
            _viewModel.SelectedOrderBy);
        SetStatus($"Analyzing Travco for {village.NameWithCoords}, {daysInactive} active day(s), order {_viewModel.SelectedOrderBy}.");
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(_windowToken);
        _activeOperationCts = operationCts;
        BusyOverlay.ShowCancel = false;
        BusyOverlay.Show("Analyze Travco", "0% complete\nWaiting for the browser session...");
        BusyOverlay.IsIndeterminate = false;
        BusyOverlay.ProgressValue = 0;
        try
        {
            var progress = new Progress<TravcoSearchProgress>(value =>
            {
                var total = Math.Max(1, value.TotalSteps);
                var completed = Math.Clamp(value.CompletedSteps, 0, total);
                var percent = (double)completed / total * 100;
                BusyOverlay.ProgressValue = percent;
                BusyOverlay.Text = $"{percent:0}% complete\n{value.Status}\nStep {completed}/{total}";
            });
            var result = await _searchRequested(request, progress, operationCts.Token);
            _lastAnalysis = result;
            ShowAnalysisInfo(result);
            SaveAllButton.IsEnabled = true;
            SetStatus(result.Rows.Count == 0
                ? $"Analysis finished: {result.TotalPages} page(s) found, page {result.PageNumber} has no matching villages."
                : $"Analysis finished: {result.TotalPages} page(s) found. Save all pages to store every result.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Travco analysis canceled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Travco analysis failed: {ex.Message}");
        }
        finally
        {
            _activeOperationCts = null;
            BusyOverlay.Hide();
            SetBusy(false);
        }
    }

    // Fills the stat tiles from an analysis result. Villages comes from Travco's own header badge
    // (#list-object-count) — the whole search's inactive total — falling back to the current page's row
    // count only when that badge was not readable.
    private void ShowAnalysisInfo(TravcoScrapeResult result)
    {
        PagesValue.Text = result.TotalPages.ToString();
        VillagesValue.Text = (result.TotalInactiveCount ?? result.Rows.Count).ToString();
        ResultsPanel.Visibility = Visibility.Visible;
        // Save all lives on the bottom bar next to Close (so Close is never pushed off the window); reveal it
        // now that there is an analysis to save.
        SaveAllButton.Visibility = Visibility.Visible;
    }

    private void SaveAllButton_Click(object sender, RoutedEventArgs e) => _ = RunSaveAllAsync();

    private async Task RunSaveAllAsync()
    {
        if (_busy || _lastAnalysis is null)
        {
            return;
        }

        SetBusy(true);
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(_windowToken);
        _activeOperationCts = operationCts;
        BusyOverlay.ShowCancel = true;
        BusyOverlay.Show("Save all Travco pages", "Preparing page collection...");
        BusyOverlay.IsIndeterminate = false;
        BusyOverlay.ProgressValue = 0;
        try
        {
            SetStatus("Reading all Travco result pages.");
            var progress = new Progress<(int CurrentPage, int TotalPages)>(value =>
            {
                var total = Math.Max(1, value.TotalPages);
                var current = Math.Clamp(value.CurrentPage, 0, total);
                var percent = (double)current / total * 100;
                BusyOverlay.ProgressValue = percent;
                BusyOverlay.Text =
                    $"{percent:0}% complete\n" +
                    $"Page {current}/{total} - {total - current} remaining";
            });
            var result = await _scrapeAllPagesRequested(progress, operationCts.Token);
            var name = MakeUniqueListName(string.IsNullOrWhiteSpace(_viewModel.ListName)
                ? "Travco all pages"
                : _viewModel.ListName.Trim());
            SaveList(name, result.Rows);
            ListSaved = true;
            _viewModel.ListName = name;
            SetStatus($"Saved '{name}' with {result.Rows.Count} village(s) from {result.TotalPages} page(s).");
            AppDialog.ShowCustom(
                this,
                $"{result.Rows.Count} village{(result.Rows.Count == 1 ? " was" : "s were")} saved to list '{name}'.",
                "Save all Travco pages complete",
                [("OK", MessageBoxResult.OK)],
                MessageBoxImage.Information,
                defaultResult: MessageBoxResult.OK,
                cancelResult: MessageBoxResult.OK,
                successResult: MessageBoxResult.OK);
            // The user has confirmed the result; close this popup so they return to the Travco tools window,
            // whose saved-list panel refreshes (ListSaved) with the freshly saved list.
            Close();
            return;
        }
        catch (OperationCanceledException)
        {
            SetStatus("Saving all Travco pages was canceled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not save all Travco pages: {ex.Message}");
        }
        finally
        {
            _activeOperationCts = null;
            BusyOverlay.ShowCancel = false;
            BusyOverlay.Hide();
            SetBusy(false);
        }
    }

    private void SaveList(string name, IReadOnlyList<TravcoRow> rows)
    {
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("No Travco result rows were found.");
        }

        var list = new TravcoListStore.TravcoSavedList
        {
            Name = name,
            CreatedUtc = DateTimeOffset.UtcNow,
            Rows = rows.Select(row => new TravcoListStore.TravcoSavedRow
            {
                Distance = row.Distance,
                Account = row.Account,
                Village = row.Village,
                Pop = row.Pop,
                Coordinates = row.Coordinates,
                Selected = true,
            }).ToList(),
        };
        _store.Save(list);
    }

    // Returns baseName unchanged if no saved list already uses it, otherwise the first free
    // "baseName 1", "baseName 2", ... so a saved list never collides with an existing one.
    private string MakeUniqueListName(string baseName)
    {
        var trimmed = baseName.Trim();
        var existing = _store.LoadAll()
            .Select(list => list.Name?.Trim())
            .Where(listName => !string.IsNullOrEmpty(listName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(trimmed))
        {
            return trimmed;
        }

        for (var suffix = 1; ; suffix++)
        {
            var candidate = $"{trimmed} {suffix}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private void BusyOverlay_Cancelled(object sender, EventArgs e)
    {
        if (_activeOperationCts is null || _activeOperationCts.IsCancellationRequested)
        {
            return;
        }

        SetStatus("Cancel requested. Returning Travco to the first page...");
        _activeOperationCts.Cancel();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy)
    {
        _busy = busy;
        AnalyzeButton.IsEnabled = !busy;
        SaveAllButton.IsEnabled = !busy && _lastAnalysis is not null;
    }

    private void SetStatus(string message)
    {
        _viewModel.StatusText = message;
        _log?.Invoke($"[travco-ui] {message}");
    }
}
