using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TbotUltra.Desktop.Common;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>
/// View model backing the farm-lists panel: owns the farm-list status rows,
/// the placeholder-row invariant, and panel commands. The service-bound
/// send/refresh/scan flows stay hosted by MainWindow during the transition.
/// </summary>
public sealed class FarmListsViewModel : BaseViewModel
{
    private readonly RelayCommand _analyzeCommand;
    private readonly RelayCommand _addFarmsCommand;
    private readonly RelayCommand _createFarmListCommand;
    private readonly RelayCommand _sendAllNowCommand;
    private readonly RelayCommand<FarmListStatusRow> _sendNowCommand;
    private readonly RelayCommand _travcoInactiveSearchCommand;
    private bool _canAnalyze = true;
    private bool _canManageLists;
    private bool _canCreate = true;
    private bool _canSendAll;
    private int _settingsNotificationSuppressionCount;
    private bool _sendAllLists;
    private string _dispatchDelayMinMinutes = "15";
    private string _dispatchDelayMaxMinutes = "30";
    private bool _deactivateLosses;
    private bool _deactivateOasisLosses;
    private bool _moveLosses;
    private FarmLossDestinationOption? _selectedLossDestination;

    public FarmListsViewModel()
    {
        _analyzeCommand = new RelayCommand(() => AnalyzeRequested?.Invoke(), () => _canAnalyze);
        _addFarmsCommand = new RelayCommand(() => AddFarmsRequested?.Invoke(), () => _canManageLists);
        _createFarmListCommand = new RelayCommand(() => CreateFarmListRequested?.Invoke(), () => _canCreate);
        _sendAllNowCommand = new RelayCommand(() => SendAllNowRequested?.Invoke(), () => _canSendAll);
        _sendNowCommand = new RelayCommand<FarmListStatusRow>(row => SendNowRequested?.Invoke(row), row => _canManageLists && row.CanSendNow);
        _travcoInactiveSearchCommand = new RelayCommand(() => TravcoInactiveSearchRequested?.Invoke());
    }

    /// <summary>
    /// Farm-list status rows shown on the farming tab. Created once and mutated
    /// in place so the panel's ItemsSource assignment stays stable.
    /// </summary>
    public ObservableCollection<FarmListStatusRow> FarmLists { get; } = [];

    public ICommand AnalyzeCommand => _analyzeCommand;
    public ICommand AddFarmsCommand => _addFarmsCommand;
    public ICommand CreateFarmListCommand => _createFarmListCommand;
    public ICommand SendAllNowCommand => _sendAllNowCommand;
    public ICommand SendNowCommand => _sendNowCommand;
    public ICommand TravcoInactiveSearchCommand => _travcoInactiveSearchCommand;

    public event Action? AnalyzeRequested;
    public event Action? AddFarmsRequested;
    public event Action? CreateFarmListRequested;
    public event Action? SendAllNowRequested;
    public event Action<FarmListStatusRow>? SendNowRequested;
    public event Action? TravcoInactiveSearchRequested;
    public event Action? SettingsChanged;
    public event Action? MoveLossesEnabledRequested;

    /// <summary>Applies MainWindow's global session/busy gate to panel commands.</summary>
    public void UpdateCommandAvailability(bool canAnalyze, bool canManageLists, bool canCreate, bool canSendAll)
    {
        _canAnalyze = canAnalyze;
        _canManageLists = canManageLists;
        _canCreate = canCreate;
        _canSendAll = canSendAll;
        _analyzeCommand.RaiseCanExecuteChanged();
        _addFarmsCommand.RaiseCanExecuteChanged();
        _createFarmListCommand.RaiseCanExecuteChanged();
        _sendAllNowCommand.RaiseCanExecuteChanged();
        _sendNowCommand.RaiseCanExecuteChanged();
    }

    public bool SendAllLists
    {
        get => _sendAllLists;
        set
        {
            if (SetProperty(ref _sendAllLists, value))
            {
                OnPropertyChanged(nameof(SendToggledLists));
                OnSettingsChanged();
            }
        }
    }

    public bool SendToggledLists
    {
        get => !_sendAllLists;
        set
        {
            if (value)
            {
                SendAllLists = false;
            }
        }
    }

    public string DispatchDelayMinMinutes
    {
        get => _dispatchDelayMinMinutes;
        set
        {
            if (SetProperty(ref _dispatchDelayMinMinutes, value))
            {
                OnSettingsChanged();
            }
        }
    }

    public string DispatchDelayMaxMinutes
    {
        get => _dispatchDelayMaxMinutes;
        set
        {
            if (SetProperty(ref _dispatchDelayMaxMinutes, value))
            {
                OnSettingsChanged();
            }
        }
    }

    public bool DeactivateLosses
    {
        get => _deactivateLosses;
        set
        {
            if (!SetProperty(ref _deactivateLosses, value))
            {
                return;
            }

            if (!value && MoveLosses)
            {
                using (SuppressSettingsNotifications())
                {
                    MoveLosses = false;
                }
            }

            OnPropertyChanged(nameof(CanMoveLosses));
            OnSettingsChanged();
        }
    }

    public bool DeactivateOasisLosses
    {
        get => _deactivateOasisLosses;
        set
        {
            if (SetProperty(ref _deactivateOasisLosses, value))
            {
                OnSettingsChanged();
            }
        }
    }

    public bool MoveLosses
    {
        get => _moveLosses;
        set
        {
            if (!SetProperty(ref _moveLosses, value))
            {
                return;
            }

            OnSettingsChanged();
            if (value && _settingsNotificationSuppressionCount == 0)
            {
                MoveLossesEnabledRequested?.Invoke();
            }
        }
    }

    public bool CanMoveLosses => DeactivateLosses;

    public ObservableCollection<FarmLossDestinationOption> LossDestinations { get; } = [];

    public FarmLossDestinationOption? SelectedLossDestination
    {
        get => _selectedLossDestination;
        set
        {
            if (SetProperty(ref _selectedLossDestination, value))
            {
                OnSettingsChanged();
            }
        }
    }

    /// <summary>
    /// Replaces server-derived destination options without treating the refresh as a user setting change.
    /// The collection instance remains stable for the panel binding.
    /// </summary>
    public void ReplaceLossDestinations(
        IEnumerable<FarmLossDestinationOption> destinations,
        FarmLossDestinationOption? selectedDestination)
    {
        using var suppress = SuppressSettingsNotifications();
        LossDestinations.Clear();
        foreach (var destination in destinations)
        {
            LossDestinations.Add(destination);
        }

        SelectedLossDestination = selectedDestination;
    }

    public void LoadSettings(
        bool sendAllLists,
        int dispatchDelayMinMinutes,
        int dispatchDelayMaxMinutes,
        bool deactivateLosses,
        bool deactivateOasisLosses,
        bool moveLosses)
    {
        using var suppress = SuppressSettingsNotifications();
        SendAllLists = sendAllLists;
        DispatchDelayMinMinutes = dispatchDelayMinMinutes.ToString();
        DispatchDelayMaxMinutes = dispatchDelayMaxMinutes.ToString();
        DeactivateLosses = deactivateLosses;
        DeactivateOasisLosses = deactivateOasisLosses;
        MoveLosses = deactivateLosses && moveLosses;
    }

    private IDisposable SuppressSettingsNotifications()
    {
        _settingsNotificationSuppressionCount++;
        return new SettingsNotificationSuppression(this);
    }

    private void OnSettingsChanged()
    {
        if (_settingsNotificationSuppressionCount == 0)
        {
            SettingsChanged?.Invoke();
        }
    }

    private sealed class SettingsNotificationSuppression(FarmListsViewModel owner) : IDisposable
    {
        public void Dispose()
        {
            owner._settingsNotificationSuppressionCount = Math.Max(0, owner._settingsNotificationSuppressionCount - 1);
        }
    }

    public static bool IsRealRow(FarmListStatusRow row) => !row.IsPlaceholder;

    /// <summary>
    /// Keeps exactly one placeholder row while no real farm lists are loaded,
    /// and none once real rows exist.
    /// </summary>
    public void EnsurePlaceholderRow()
    {
        if (FarmLists.Any(IsRealRow))
        {
            foreach (var row in FarmLists.Where(row => row.IsPlaceholder).ToList())
            {
                FarmLists.Remove(row);
            }

            return;
        }

        if (!FarmLists.Any(row => row.IsPlaceholder))
        {
            FarmLists.Add(new FarmListStatusRow
            {
                IsPlaceholder = true,
                IsEnabled = false,
            });
        }
    }

    /// <summary>Status line for the farming tab under the current rows.</summary>
    public string DescribeStatus()
    {
        var realFarmLists = FarmLists.Where(IsRealRow).ToList();
        if (realFarmLists.Count <= 0)
        {
            return "No farm lists loaded. Click Analyze Farmlists.";
        }

        var readyCount = realFarmLists.Count(item => item.IsReady && !item.IsEmpty);
        return $"Loaded {realFarmLists.Count} farm list(s). Ready: {readyCount}.";
    }
}
