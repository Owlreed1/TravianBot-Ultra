using System.Windows.Input;
using TbotUltra.Desktop.Common;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>Owns command availability for the Settings dialog while the dialog keeps its validation and persistence bridge.</summary>
public sealed class SettingsDialogViewModel
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _resetSettingsCommand;
    private readonly RelayCommand _sleepNowCommand;
    private readonly RelayCommand _runVillageStatusSweepNowCommand;
    private readonly RelayCommand _resetDailyGoldSpendingCommand;
    private readonly RelayCommand _resetDailySilverSpendingCommand;
    private bool _sleepNowEnabled;
    private bool _villageStatusSweepEnabled;
    private bool _dailyGoldSpendingResetEnabled;
    private bool _dailySilverSpendingResetEnabled;

    public SettingsDialogViewModel(
        bool sleepNowEnabled,
        bool villageStatusSweepEnabled,
        bool dailyGoldSpendingResetEnabled,
        bool dailySilverSpendingResetEnabled)
    {
        _sleepNowEnabled = sleepNowEnabled;
        _villageStatusSweepEnabled = villageStatusSweepEnabled;
        _dailyGoldSpendingResetEnabled = dailyGoldSpendingResetEnabled;
        _dailySilverSpendingResetEnabled = dailySilverSpendingResetEnabled;
        _saveCommand = new RelayCommand(() => SaveRequested?.Invoke());
        _cancelCommand = new RelayCommand(() => CancelRequested?.Invoke());
        _resetSettingsCommand = new RelayCommand(() => ResetSettingsRequested?.Invoke());
        _sleepNowCommand = new RelayCommand(() => SleepNowRequested?.Invoke(), () => _sleepNowEnabled);
        _runVillageStatusSweepNowCommand = new RelayCommand(
            () => VillageStatusSweepNowRequested?.Invoke(),
            () => _villageStatusSweepEnabled);
        _resetDailyGoldSpendingCommand = new RelayCommand(
            () => ResetDailyGoldSpendingRequested?.Invoke(),
            () => _dailyGoldSpendingResetEnabled);
        _resetDailySilverSpendingCommand = new RelayCommand(
            () => ResetDailySilverSpendingRequested?.Invoke(),
            () => _dailySilverSpendingResetEnabled);
    }

    public ICommand SaveCommand => _saveCommand;
    public ICommand CancelCommand => _cancelCommand;
    public ICommand ResetSettingsCommand => _resetSettingsCommand;
    public ICommand SleepNowCommand => _sleepNowCommand;
    public ICommand RunVillageStatusSweepNowCommand => _runVillageStatusSweepNowCommand;
    public ICommand ResetDailyGoldSpendingCommand => _resetDailyGoldSpendingCommand;
    public ICommand ResetDailySilverSpendingCommand => _resetDailySilverSpendingCommand;

    public event Action? SaveRequested;
    public event Action? CancelRequested;
    public event Action? ResetSettingsRequested;
    public event Action? SleepNowRequested;
    public event Action? VillageStatusSweepNowRequested;
    public event Action? ResetDailyGoldSpendingRequested;
    public event Action? ResetDailySilverSpendingRequested;

    public void SetVillageStatusSweepRunning(bool running)
    {
        _villageStatusSweepEnabled = !running;
        _runVillageStatusSweepNowCommand.RaiseCanExecuteChanged();
    }
}
