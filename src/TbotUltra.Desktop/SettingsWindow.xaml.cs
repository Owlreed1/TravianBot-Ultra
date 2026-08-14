using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Services;
using TbotUltra.Desktop.ViewModels;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop;

public enum SettingsCategory
{
    General,
    Pacing,
    Construction,
    Hero,
    Farming,
    Troops,
    Celebrations,
    NpcTrade,
}

public partial class SettingsWindow : Window
{
    private const int DefaultGoldLimit = 100;
    private const int DefaultDailyGoldSpendingLimit = 20;
    private const int DefaultSilverLimit = 100;
    private const int DefaultDailySilverSpendingLimit = 10000;
    private readonly BotConfigStore _store;
    private readonly SettingsPersistenceService _settingsPersistence;
    private JsonObject _config = [];
    private bool _isClosing;
    private readonly bool _sessionSleeping;
    // Server-local hour the bot auto-detected for the active account (null when not yet detected). Display-only.
    private readonly int? _detectedDailyResetHour;
    private readonly Action? _resetDailyGoldSpending;
    private readonly Action? _resetDailySilverSpending;
    private bool _suppressDetailedBrowserLoggingConfirmation;
    private bool _suppressInitialConfirmationDialogs = true;
    private string _initialTownHallFingerprint = string.Empty;
    private readonly Func<DateTimeOffset>? _villageStatusSweepNextScanProvider;
    private readonly Func<DateTimeOffset>? _continuousKeepAliveNextReloadProvider;
    private readonly Func<Task>? _runVillageStatusSweepNow;
    private readonly bool _newAccountAnalysisCompleted;
    private readonly DispatcherTimer _villageStatusSweepTimer;

    public SettingsDialogViewModel SettingsVm { get; }

    public IReadOnlyList<TownHallOverviewResult> TownHallResults { get; private set; } = [];
    public bool TownHallSettingsChanged { get; private set; }

    // Set when the user confirms "Sleep now"; MainWindow reads it after ShowDialog to trigger the sleep.
    public bool SleepNowRequested { get; private set; }

    public SettingsWindow(
        BotConfigStore store,
        bool sessionSleeping = false,
        int? detectedDailyResetHour = null,
        Func<JsonObject, string?>? validateBeforeSave = null,
        SettingsCategory initialCategory = SettingsCategory.General,
        IReadOnlyList<TownHallOverviewRow>? townHallRows = null,
        Action? resetDailyGoldSpending = null,
        Action? resetDailySilverSpending = null,
        int dailyGoldSpent = 0,
        int dailySilverSpent = 0,
        Func<DateTimeOffset>? villageStatusSweepNextScanProvider = null,
        Func<DateTimeOffset>? continuousKeepAliveNextReloadProvider = null,
        Func<Task>? runVillageStatusSweepNow = null,
        bool newAccountAnalysisCompleted = false,
        IReadOnlyList<HeroCropAntiStarveVillageRow>? heroCropAntiStarveVillages = null)
    {
        InitializeComponent();
        ThemeChrome.EnableEarlyDarkTitleBar(this);
        _store = store;
        _settingsPersistence = new SettingsPersistenceService(_store, validateBeforeSave);
        _sessionSleeping = sessionSleeping;
        _detectedDailyResetHour = detectedDailyResetHour;
        _resetDailyGoldSpending = resetDailyGoldSpending;
        _resetDailySilverSpending = resetDailySilverSpending;
        _villageStatusSweepNextScanProvider = villageStatusSweepNextScanProvider;
        _continuousKeepAliveNextReloadProvider = continuousKeepAliveNextReloadProvider;
        _runVillageStatusSweepNow = runVillageStatusSweepNow;
        _newAccountAnalysisCompleted = newAccountAnalysisCompleted;
        SettingsVm = new SettingsDialogViewModel(
            sleepNowEnabled: !_sessionSleeping,
            villageStatusSweepEnabled: _runVillageStatusSweepNow is not null,
            dailyGoldSpendingResetEnabled: _resetDailyGoldSpending is not null,
            dailySilverSpendingResetEnabled: _resetDailySilverSpending is not null);
        SettingsVm.DailyGoldSpent = dailyGoldSpent;
        SettingsVm.DailySilverSpent = dailySilverSpent;
        SettingsVm.SaveRequested += SaveSettings;
        SettingsVm.CancelRequested += CancelSettings;
        SettingsVm.ResetSettingsRequested += ResetSettings;
        SettingsVm.ResetPacingRequested += ApplyPacingDefaultsToUi;
        SettingsVm.SleepNowRequested += RequestSleepNow;
        SettingsVm.VillageStatusSweepNowRequested += () => _ = RunVillageStatusSweepNowAsync();
        SettingsVm.ResetDailyGoldSpendingRequested += ResetDailyGoldLimit;
        SettingsVm.ResetDailySilverSpendingRequested += ResetDailySilverLimit;
        foreach (var row in townHallRows ?? [])
        {
            SettingsVm.Celebrations.TownHallRows.Add(row);
        }
        foreach (var row in heroCropAntiStarveVillages ?? [])
        {
            SettingsVm.Hero.CropAntiStarveVillages.Add(row);
        }
        DataContext = this;
        AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(SettingsInputChanged));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(SettingsInputChanged));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(SettingsInputChanged));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(SettingsInputChanged));
        InitializeSessionPacingChoices();
        InitializeConstructionChoices();
        PopulateDailyServerResetHours();
        LoadConfig();
        UpdateNewAccountAnalysisStatus();
        SettingsCategoryTabControl.SelectedIndex = (int)initialCategory;
        _initialTownHallFingerprint = BuildTownHallFingerprint();
        ContentRendered += (_, _) => _suppressInitialConfirmationDialogs = false;
        _villageStatusSweepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _villageStatusSweepTimer.Tick += (_, _) =>
        {
            UpdateVillageStatusSweepNextScanDisplay();
            UpdateContinuousKeepAliveNextReloadDisplay();
        };
        Closed += (_, _) => _villageStatusSweepTimer.Stop();
        UpdateVillageStatusSweepNextScanDisplay();
        UpdateContinuousKeepAliveNextReloadDisplay();
        _villageStatusSweepTimer.Start();
    }

    private void UpdateNewAccountAnalysisStatus()
    {
        NewAccountAnalysisStatusTextBlock.Text = _newAccountAnalysisCompleted ? "Analyzed" : "Not analyzed";
        NewAccountAnalysisStatusCard.SetResourceReference(
            Border.BackgroundProperty,
            _newAccountAnalysisCompleted ? "SuccessBrush" : "WarningBrush");
        NewAccountAnalysisStatusCard.SetResourceReference(
            Border.BorderBrushProperty,
            _newAccountAnalysisCompleted ? "SuccessBrush" : "WarningBorderBrush");
    }

    private async Task RunVillageStatusSweepNowAsync()
    {
        if (_runVillageStatusSweepNow is null)
        {
            return;
        }

        SettingsVm.SetVillageStatusSweepRunning(true);
        try
        {
            await _runVillageStatusSweepNow();
            UpdateVillageStatusSweepNextScanDisplay();
        }
        finally
        {
            SettingsVm.SetVillageStatusSweepRunning(false);
        }
    }

    private void UpdateVillageStatusSweepNextScanDisplay()
    {
        UpdateNextScheduledDisplay(
            _villageStatusSweepNextScanProvider,
            VillageStatusSweepNextScanTextBlock,
            VillageStatusSweepNextScanBadge,
            "Ready");
    }

    private void UpdateContinuousKeepAliveNextReloadDisplay()
    {
        if (!SettingsVm.Pacing.ContinuousKeepAliveEnabled)
        {
            ContinuousKeepAliveNextReloadTextBlock.Text = "Off";
            ApplyNextScheduleBadgeTheme(
                ContinuousKeepAliveNextReloadTextBlock,
                ContinuousKeepAliveNextReloadBadge,
                isReady: true);
            return;
        }

        UpdateNextScheduledDisplay(
            _continuousKeepAliveNextReloadProvider,
            ContinuousKeepAliveNextReloadTextBlock,
            ContinuousKeepAliveNextReloadBadge,
            "Ready");
    }

    private static void UpdateNextScheduledDisplay(
        Func<DateTimeOffset>? nextScheduleProvider,
        TextBlock textBlock,
        Border badge,
        string emptyText)
    {
        var nextScheduleUtc = nextScheduleProvider?.Invoke() ?? DateTimeOffset.MinValue;
        var remaining = nextScheduleUtc - DateTimeOffset.UtcNow;
        if (nextScheduleUtc == DateTimeOffset.MinValue || remaining <= TimeSpan.Zero)
        {
            textBlock.Text = emptyText;
            ApplyNextScheduleBadgeTheme(textBlock, badge, isReady: true);
            return;
        }

        textBlock.Text = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes:00}:{remaining.Seconds:00}";
        ApplyNextScheduleBadgeTheme(textBlock, badge, isReady: false);
    }

    private static void ApplyNextScheduleBadgeTheme(TextBlock textBlock, Border badge, bool isReady)
    {
        textBlock.Foreground = (System.Windows.Media.Brush)badge.FindResource(
            isReady ? "SuccessTextBrush" : "WarningTextBrush");
        badge.Background = (System.Windows.Media.Brush)badge.FindResource(
            isReady ? "SuccessBgBrush" : "WarningBgBrush");
        badge.BorderBrush = (System.Windows.Media.Brush)badge.FindResource(
            isReady ? "SuccessBorderBrush" : "WarningBorderBrush");
    }

    private void LoadConfig()
    {
        using var suppressChanges = SettingsVm.SuppressChangeTracking();
        _config = _settingsPersistence.Load();
        SettingsVm.DontNotifyNewVersion = _config[BotOptionPayloadKeys.DontNotifyNewVersion]?.GetValue<bool>() ?? false;
        SettingsVm.QuickReloginEnabled = _config[BotOptionPayloadKeys.PostLoginQuickReloginEnabled]?.GetValue<bool>() ?? true;
        SettingsVm.AutomaticallyCheckLanguage = _config[BotOptionPayloadKeys.AutomaticallyCheckLanguage]?.GetValue<bool>() ?? true;
        _suppressDetailedBrowserLoggingConfirmation = true;
        try
        {
            SettingsVm.DetailedBrowserLoggingEnabled =
                _config[BotOptionPayloadKeys.DetailedBrowserLoggingEnabled]?.GetValue<bool>() ?? false;
        }
        finally
        {
            _suppressDetailedBrowserLoggingConfirmation = false;
        }
        SettingsVm.AllowSilverSpending = _config["allow_silver_spending"]?.GetValue<bool>() ?? false;
        SettingsVm.AllowGoldSpending = _config[BotOptionPayloadKeys.AllowGoldSpending]?.GetValue<bool>() ?? false;
        SettingsVm.GoldLimitText = Math.Max(
            0,
            _config[BotOptionPayloadKeys.GoldLimit]?.GetValue<int>() ?? DefaultGoldLimit).ToString(CultureInfo.InvariantCulture);
        SettingsVm.DailyGoldSpendingLimitText = Math.Max(
            0,
            _config[BotOptionPayloadKeys.DailyGoldSpendingLimit]?.GetValue<int>() ?? DefaultDailyGoldSpendingLimit).ToString(CultureInfo.InvariantCulture);
        LoadDailyServerResetToUi();
        LoadPacingConfigToUi();
        SettingsVm.Construction.StorageUpgradeLevelsAhead = ConstructionDefaults.NormalizeStorageUpgradeLevelsAhead(
            _config[BotOptionPayloadKeys.ConstructionStorageUpgradeLevelsAhead]?.GetValue<int>()
            ?? ConstructionDefaults.StorageUpgradeLevelsAhead);
        SettingsVm.Construction.CropShortageRecoveryEnabled = ReadBool(
            BotOptionPayloadKeys.ConstructionCropShortageRecoveryEnabled,
            ConstructionDefaults.CropShortageRecoveryEnabled);
        LoadConstructionHumanizeConfigToUi();
        SettingsVm.Farming.ShowFarmListLastSentTimer = ReadBool(BotOptionPayloadKeys.ShowFarmListLastSentTimer, FarmingDefaults.ShowLastSentTimer);
        SettingsVm.Farming.FarmListLastSentLimitEnabled = ReadBool(BotOptionPayloadKeys.FarmListLastSentLimitEnabled, FarmingDefaults.LastSentLimitEnabled);
        SettingsVm.Farming.FarmListLastSentLimitHours = FarmingDefaults.NormalizeLastSentLimitHours(ReadInt(
            BotOptionPayloadKeys.FarmListLastSentLimitHours,
            FarmingDefaults.DefaultLastSentLimitHours)).ToString(CultureInfo.InvariantCulture);
        SynchronizeFarmingControls();
        SettingsVm.PostLogin.AnalyzeFarmlists = _config[BotOptionPayloadKeys.PostLoginAnalyzeFarmlists]?.GetValue<bool>() ?? false;
        SettingsVm.PostLogin.AnalyzeHero = _config[BotOptionPayloadKeys.PostLoginAnalyzeHero]?.GetValue<bool>() ?? false;
        SettingsVm.PostLogin.ReadTroopTrainingQueue = _config[BotOptionPayloadKeys.PostLoginReadTroopTrainingQueue]?.GetValue<bool>() ?? false;
        SettingsVm.PostLogin.AnalyzeBrewery = _config[BotOptionPayloadKeys.PostLoginAnalyzeBrewery]?.GetValue<bool>() ?? false;
        SettingsVm.PostLogin.AnalyzeHeroInventory = _config[BotOptionPayloadKeys.PostLoginAnalyzeHeroInventory]?.GetValue<bool>() ?? false;
        SettingsVm.PostLogin.AnalyzeNewVillages = _config[BotOptionPayloadKeys.PostLoginAnalyzeNewVillages]?.GetValue<bool>() ?? true;
        SettingsVm.PostLogin.AnalyzeNewAccount = _config[BotOptionPayloadKeys.PostLoginAnalyzeNewAccount]?.GetValue<bool>() ?? true;
        SynchronizePostLoginControls();
        SettingsVm.SilverLimitText = Math.Max(
            0,
            _config[BotOptionPayloadKeys.SilverLimit]?.GetValue<int>() ?? DefaultSilverLimit).ToString(CultureInfo.InvariantCulture);
        SettingsVm.DailySilverSpendingLimitText = Math.Max(
            0,
            _config[BotOptionPayloadKeys.DailySilverSpendingLimit]?.GetValue<int>() ?? DefaultDailySilverSpendingLimit).ToString(CultureInfo.InvariantCulture);
        if (TownHallCelebrationDefaults.NormalizeCount(
                ReadInt(BotOptionPayloadKeys.TownHallCelebrationCount, TownHallCelebrationDefaults.DefaultCount))
            >= TownHallCelebrationDefaults.MaxCount)
        {
            SettingsVm.Celebrations.TownHallQueue.IsTwo = true;
        }
        else
        {
            SettingsVm.Celebrations.TownHallQueue.IsOne = true;
        }
        SettingsVm.Celebrations.TownHallQueue.DelayMinMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.TownHallCelebrationRestartDelayMinMinutes,
            TownHallCelebrationDefaults.DefaultRestartDelayMinMinutes));
        SettingsVm.Celebrations.TownHallQueue.DelayMaxMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.TownHallCelebrationRestartDelayMaxMinutes,
            TownHallCelebrationDefaults.DefaultRestartDelayMaxMinutes));
        SettingsVm.Celebrations.TownHallQueue.IsRestartDelayEnabled =
            _config[BotOptionPayloadKeys.TownHallCelebrationRestartDelayEnabled]?.GetValue<bool>()
            ?? TownHallCelebrationDefaults.DefaultRestartDelayEnabled;
        SettingsVm.Celebrations.BreweryRestartDelay.IsEnabled =
            _config[BotOptionPayloadKeys.BreweryCelebrationRestartDelayEnabled]?.GetValue<bool>()
            ?? BreweryCelebrationDefaults.DefaultRestartDelayEnabled;
        SettingsVm.Celebrations.BreweryRestartDelay.DelayMinMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.BreweryCelebrationRestartDelayMinMinutes,
            BreweryCelebrationDefaults.DefaultRestartDelayMinMinutes));
        SettingsVm.Celebrations.BreweryRestartDelay.DelayMaxMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.BreweryCelebrationRestartDelayMaxMinutes,
            BreweryCelebrationDefaults.DefaultRestartDelayMaxMinutes));
        SynchronizeCelebrationControls();
        SettingsVm.Hero.AdventureRestartDelay.IsEnabled =
            _config[BotOptionPayloadKeys.HeroAdventureRestartDelayEnabled]?.GetValue<bool>()
            ?? HeroAdventureRestartDelayDefaults.Enabled;
        SettingsVm.Hero.AdventureRestartDelay.DelayMinMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.HeroAdventureRestartDelayMinMinutes,
            HeroAdventureRestartDelayDefaults.MinMinutes));
        SettingsVm.Hero.AdventureRestartDelay.DelayMaxMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.HeroAdventureRestartDelayMaxMinutes,
            HeroAdventureRestartDelayDefaults.MaxMinutes));
        var heroHpRegen = Math.Clamp(ReadInt(BotOptionPayloadKeys.HeroHpRegenPerDayPercent, 40), 20, 100);
        SettingsVm.Hero.HpRegenPerDayPercent = Math.Clamp(((heroHpRegen + 5) / 10) * 10, 20, 100);
        SettingsVm.Hero.CropAntiStarveEnabled = ReadBool(
            BotOptionPayloadKeys.HeroCropAntiStarveEnabled,
            HeroCropAntiStarveDefaults.Enabled);
        SettingsVm.Hero.CropAntiStarveTriggerMinutes = ReadInt(
            BotOptionPayloadKeys.HeroCropAntiStarveTriggerMinutes,
            HeroCropAntiStarveDefaults.TriggerMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Hero.CropAntiStarveTargetMinutes = ReadInt(
            BotOptionPayloadKeys.HeroCropAntiStarveTargetMinutes,
            HeroCropAntiStarveDefaults.TargetMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Hero.CropAntiStarveMaxCropPerTransfer = ReadInt(
            BotOptionPayloadKeys.HeroCropAntiStarveMaxCropPerTransfer,
            HeroCropAntiStarveDefaults.MaxCropPerTransfer).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Hero.CropAntiStarveMinHeroCropRemaining = ReadInt(
            BotOptionPayloadKeys.HeroCropAntiStarveMinHeroCropRemaining,
            HeroCropAntiStarveDefaults.MinHeroCropRemaining).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Hero.SmithyUpgradeRestartDelay.IsEnabled =
            _config[BotOptionPayloadKeys.SmithyUpgradeRestartDelayEnabled]?.GetValue<bool>()
            ?? SmithyUpgradeRestartDelayDefaults.Enabled;
        SettingsVm.Hero.SmithyUpgradeRestartDelay.DelayMinMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.SmithyUpgradeRestartDelayMinMinutes,
            SmithyUpgradeRestartDelayDefaults.MinMinutes));
        SettingsVm.Hero.SmithyUpgradeRestartDelay.DelayMaxMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.SmithyUpgradeRestartDelayMaxMinutes,
            SmithyUpgradeRestartDelayDefaults.MaxMinutes));
        SettingsVm.TroopTrainingFallbackCooldownSeconds = ReadInt(
            BotOptionPayloadKeys.TroopTrainingFallbackCooldownSeconds,
            120);
        SynchronizeHeroControls();
        SynchronizeSpendingLimitControls();
        SettingsVm.ResetChangeTracking();
    }

    private void SettingsInputChanged(object sender, RoutedEventArgs e)
    {
        _ = sender;
        if (e.OriginalSource is TextBox textBox)
        {
            if (ReferenceEquals(textBox, GoldLimitTextBox))
            {
                SettingsVm.GoldLimitText = textBox.Text;
            }
            else if (ReferenceEquals(textBox, DailyGoldSpendingLimitTextBox))
            {
                SettingsVm.DailyGoldSpendingLimitText = textBox.Text;
            }
            else if (ReferenceEquals(textBox, SilverLimitTextBox))
            {
                SettingsVm.SilverLimitText = textBox.Text;
            }
            else if (ReferenceEquals(textBox, DailySilverSpendingLimitTextBox))
            {
                SettingsVm.DailySilverSpendingLimitText = textBox.Text;
            }

            UpdateDailySpendingUsage();
        }

        SettingsVm.MarkChanged();
    }

    // Fills the reset-hour dropdown with 00:00..23:00 (Tag = the whole hour 0..23).
    private void PopulateDailyServerResetHours()
    {
        for (var hour = 0; hour < 24; hour++)
        {
            DailyServerResetHourComboBox.Items.Add(new ComboBoxItem
            {
                Content = $"{hour:00}:00",
                Tag = hour,
            });
        }
    }

    private void InitializeConstructionChoices()
    {
        StorageUpgradeLevelsAheadComboBox.ItemsSource = Enumerable.Range(
            ConstructionDefaults.StorageUpgradeLevelsAheadMin,
            ConstructionDefaults.StorageUpgradeLevelsAheadMax - ConstructionDefaults.StorageUpgradeLevelsAheadMin + 1);
    }

    private void LoadConstructionHumanizeConfigToUi()
    {
        SettingsVm.Construction.HumanizeDelayEnabled = ReadBool(
            BotOptionPayloadKeys.ConstructionHumanizeDelayEnabled,
            PacingDefaults.ConstructionHumanizeDelayEnabled);
        SettingsVm.Construction.QueuePercentMin = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.ConstructionHumanizeQueuePercentMin,
            PacingDefaults.ConstructionHumanizeQueuePercentMin));
        SettingsVm.Construction.QueuePercentMax = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.ConstructionHumanizeQueuePercentMax,
            PacingDefaults.ConstructionHumanizeQueuePercentMax));
        SettingsVm.Construction.MaxDelayMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.ConstructionHumanizeMaxDelayMinutes,
            PacingDefaults.ConstructionHumanizeMaxDelayMinutes));
        SettingsVm.Construction.NoPlusDelayMinMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.ConstructionHumanizeNoPlusMinMinutes,
            PacingDefaults.ConstructionHumanizeNoPlusMinMinutes));
        SettingsVm.Construction.NoPlusDelayMaxMinutes = FormatDelay(ReadDouble(
            BotOptionPayloadKeys.ConstructionHumanizeNoPlusMaxMinutes,
            PacingDefaults.ConstructionHumanizeNoPlusMaxMinutes));
        SettingsVm.Construction.DemolishDelayMinMinutes = ReadInt(
            BotOptionPayloadKeys.DemolishDelayMinMinutes,
            DemolishDefaults.DefaultDelayMinMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Construction.DemolishDelayMaxMinutes = ReadInt(
            BotOptionPayloadKeys.DemolishDelayMaxMinutes,
            DemolishDefaults.DefaultDelayMaxMinutes).ToString(CultureInfo.InvariantCulture);
        SynchronizeConstructionControls();
    }

    private void SaveConstructionHumanizeConfigFromUi()
    {
        var wasEnabled = ReadBool(
            BotOptionPayloadKeys.ConstructionHumanizeDelayEnabled,
            PacingDefaults.ConstructionHumanizeDelayEnabled);
        var enabled = SettingsVm.Construction.HumanizeDelayEnabled;
        var percentMin = Math.Clamp(ReadDoubleText(
            SettingsVm.Construction.QueuePercentMin,
            PacingDefaults.ConstructionHumanizeQueuePercentMin), 0, 99);
        var percentMax = Math.Clamp(Math.Max(percentMin, ReadDoubleText(
            SettingsVm.Construction.QueuePercentMax,
            PacingDefaults.ConstructionHumanizeQueuePercentMax)), 0, 99);
        var maxDelay = Math.Clamp(ReadDoubleText(
            SettingsVm.Construction.MaxDelayMinutes,
            PacingDefaults.ConstructionHumanizeMaxDelayMinutes), 0, 600);
        var noPlusMin = Math.Clamp(ReadDoubleText(
            SettingsVm.Construction.NoPlusDelayMinMinutes,
            PacingDefaults.ConstructionHumanizeNoPlusMinMinutes), 0, 600);
        var noPlusMax = Math.Clamp(Math.Max(noPlusMin, ReadDoubleText(
            SettingsVm.Construction.NoPlusDelayMaxMinutes,
            PacingDefaults.ConstructionHumanizeNoPlusMaxMinutes)), 0, 600);

        _config[BotOptionPayloadKeys.ConstructionHumanizeDelayEnabled] = enabled;
        _config[BotOptionPayloadKeys.ConstructionHumanizeQueuePercentMin] = percentMin;
        _config[BotOptionPayloadKeys.ConstructionHumanizeQueuePercentMax] = percentMax;
        _config[BotOptionPayloadKeys.ConstructionHumanizeMaxDelayMinutes] = maxDelay;
        _config[BotOptionPayloadKeys.ConstructionHumanizeNoPlusMinMinutes] = noPlusMin;
        _config[BotOptionPayloadKeys.ConstructionHumanizeNoPlusMaxMinutes] = noPlusMax;
        _config[BotOptionPayloadKeys.DemolishDelayMinMinutes] = ReadIntText(SettingsVm.Construction.DemolishDelayMinMinutes, DemolishDefaults.DefaultDelayMinMinutes, 0, 1440);
        _config[BotOptionPayloadKeys.DemolishDelayMaxMinutes] = ReadIntText(SettingsVm.Construction.DemolishDelayMaxMinutes, DemolishDefaults.DefaultDelayMaxMinutes, 0, 1440);
        if (wasEnabled != enabled)
        {
            var stateVersion = ReadInt(BotOptionPayloadKeys.ConstructionHumanizeStateVersion, 0);
            _config[BotOptionPayloadKeys.ConstructionHumanizeStateVersion] = stateVersion == int.MaxValue
                ? 1
                : stateVersion + 1;
        }
    }

    private void SettingsCategoryTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == sender && SettingsContentScrollViewer is not null)
        {
            SettingsContentScrollViewer.ScrollToTop();
        }
    }

    private void LoadDailyServerResetToUi()
    {
        var overrideEnabled = _config[BotOptionPayloadKeys.DailyServerResetManualOverrideEnabled]?.GetValue<bool>() ?? false;
        var manualHour = Math.Clamp(_config[BotOptionPayloadKeys.DailyServerResetManualHour]?.GetValue<int>() ?? 0, 0, 23);
        SettingsVm.DailyServerResetOverrideEnabled = overrideEnabled;
        SettingsVm.DailyServerResetHour = manualHour;
        DailyServerResetDetectedTextBlock.Text = _detectedDailyResetHour is int detected
            ? $"detected: {detected:00}:00"
            : "detected: —";
    }

    private void SaveDailyServerResetFromUi()
    {
        _config[BotOptionPayloadKeys.DailyServerResetManualOverrideEnabled] = SettingsVm.DailyServerResetOverrideEnabled;
        _config[BotOptionPayloadKeys.DailyServerResetManualHour] = SettingsVm.DailyServerResetHour;
    }

    private void SaveSettings()
    {
        if (!PersistConfig())
        {
            return;
        }

        _isClosing = true;
        CaptureTownHallResults();
        DialogResult = true;
        Close();
    }

    // Writes the current UI values to the config store. Returns false (and shows the error) on failure so
    // callers can abort closing. Shared by Save and the "Sleep now" button.
    private bool PersistConfig()
    {
        try
        {
            if (!TryValidateNumericInputs())
            {
                return false;
            }

            if (!TryReadSpendingLimits(
                    out var goldLimit,
                    out var dailyGoldSpendingLimit,
                    out var silverLimit,
                    out var dailySilverSpendingLimit))
            {
                return false;
            }

            // The browser always runs visible; headless mode has been removed entirely.
            _config.Remove("headless");
            _config[BotOptionPayloadKeys.DontNotifyNewVersion] = SettingsVm.DontNotifyNewVersion;
            _config[BotOptionPayloadKeys.PostLoginQuickReloginEnabled] = SettingsVm.QuickReloginEnabled;
            _config[BotOptionPayloadKeys.AutomaticallyCheckLanguage] = SettingsVm.AutomaticallyCheckLanguage;
            _config[BotOptionPayloadKeys.DetailedBrowserLoggingEnabled] = SettingsVm.DetailedBrowserLoggingEnabled;
            _config["allow_silver_spending"] = SettingsVm.AllowSilverSpending;
            _config[BotOptionPayloadKeys.AllowGoldSpending] = SettingsVm.AllowGoldSpending;
            _config[BotOptionPayloadKeys.GoldLimit] = goldLimit;
            _config[BotOptionPayloadKeys.DailyGoldSpendingLimit] = dailyGoldSpendingLimit;
            _config[BotOptionPayloadKeys.TownHallCelebrationCount] =
                TownHallCelebrationDefaults.NormalizeCount(SettingsVm.Celebrations.TownHallQueue.Count);
            _config[BotOptionPayloadKeys.TownHallCelebrationRestartDelayMinMinutes] =
                SettingsVm.Celebrations.TownHallQueue.ResolvedDelayMinMinutes;
            _config[BotOptionPayloadKeys.TownHallCelebrationRestartDelayMaxMinutes] =
                SettingsVm.Celebrations.TownHallQueue.ResolvedDelayMaxMinutes;
            _config[BotOptionPayloadKeys.TownHallCelebrationRestartDelayEnabled] =
                SettingsVm.Celebrations.TownHallQueue.IsRestartDelayEnabled;
            _config[BotOptionPayloadKeys.BreweryCelebrationRestartDelayEnabled] =
                SettingsVm.Celebrations.BreweryRestartDelay.IsEnabled;
            _config[BotOptionPayloadKeys.BreweryCelebrationRestartDelayMinMinutes] =
                SettingsVm.Celebrations.BreweryRestartDelay.ResolvedDelayMinMinutes;
            _config[BotOptionPayloadKeys.BreweryCelebrationRestartDelayMaxMinutes] =
                SettingsVm.Celebrations.BreweryRestartDelay.ResolvedDelayMaxMinutes;
            _config[BotOptionPayloadKeys.HeroAdventureRestartDelayEnabled] =
                SettingsVm.Hero.AdventureRestartDelay.IsEnabled;
            _config[BotOptionPayloadKeys.HeroAdventureRestartDelayMinMinutes] =
                SettingsVm.Hero.AdventureRestartDelay.ResolvedDelayMinMinutes;
            _config[BotOptionPayloadKeys.HeroAdventureRestartDelayMaxMinutes] =
                SettingsVm.Hero.AdventureRestartDelay.ResolvedDelayMaxMinutes;
            _config[BotOptionPayloadKeys.HeroHpRegenPerDayPercent] = SettingsVm.Hero.HpRegenPerDayPercent;
            _config[BotOptionPayloadKeys.SmithyUpgradeRestartDelayEnabled] =
                SettingsVm.Hero.SmithyUpgradeRestartDelay.IsEnabled;
            _config[BotOptionPayloadKeys.SmithyUpgradeRestartDelayMinMinutes] =
                SettingsVm.Hero.SmithyUpgradeRestartDelay.ResolvedDelayMinMinutes;
            _config[BotOptionPayloadKeys.SmithyUpgradeRestartDelayMaxMinutes] =
                SettingsVm.Hero.SmithyUpgradeRestartDelay.ResolvedDelayMaxMinutes;
            _config[BotOptionPayloadKeys.TroopTrainingFallbackCooldownSeconds] =
                SettingsVm.TroopTrainingFallbackCooldownSeconds;
            SaveDailyServerResetFromUi();
            SavePacingConfigFromUi();
            _config[BotOptionPayloadKeys.ConstructionStorageUpgradeLevelsAhead] =
                SettingsVm.Construction.StorageUpgradeLevelsAhead;
            _config[BotOptionPayloadKeys.ConstructionCropShortageRecoveryEnabled] =
                SettingsVm.Construction.CropShortageRecoveryEnabled;
            _config[BotOptionPayloadKeys.HeroCropAntiStarveEnabled] = SettingsVm.Hero.CropAntiStarveEnabled;
            _config[BotOptionPayloadKeys.HeroCropAntiStarveTriggerMinutes] = ReadIntText(
                SettingsVm.Hero.CropAntiStarveTriggerMinutes,
                HeroCropAntiStarveDefaults.TriggerMinutes,
                1,
                1440);
            _config[BotOptionPayloadKeys.HeroCropAntiStarveTargetMinutes] = ReadIntText(
                SettingsVm.Hero.CropAntiStarveTargetMinutes,
                HeroCropAntiStarveDefaults.TargetMinutes,
                1,
                1440);
            _config[BotOptionPayloadKeys.HeroCropAntiStarveMaxCropPerTransfer] = ReadIntText(
                SettingsVm.Hero.CropAntiStarveMaxCropPerTransfer,
                HeroCropAntiStarveDefaults.MaxCropPerTransfer,
                1,
                int.MaxValue);
            _config[BotOptionPayloadKeys.HeroCropAntiStarveMinHeroCropRemaining] = ReadIntText(
                SettingsVm.Hero.CropAntiStarveMinHeroCropRemaining,
                HeroCropAntiStarveDefaults.MinHeroCropRemaining,
                0,
                int.MaxValue);
            SaveConstructionHumanizeConfigFromUi();
            _config[BotOptionPayloadKeys.ShowFarmListLastSentTimer] = SettingsVm.Farming.ShowFarmListLastSentTimer;
            _config[BotOptionPayloadKeys.FarmListLastSentLimitEnabled] = SettingsVm.Farming.FarmListLastSentLimitEnabled;
            _config[BotOptionPayloadKeys.FarmListLastSentLimitHours] = ReadIntText(
                SettingsVm.Farming.FarmListLastSentLimitHours,
                FarmingDefaults.DefaultLastSentLimitHours,
                1,
                FarmingDefaults.MaxLastSentLimitHours);
            // Queue-wait handling is always "smart" (defer); drop the removed threshold key.
            _config.Remove("queue_wait_threshold_mode");
            _config[BotOptionPayloadKeys.PostLoginAnalyzeFarmlists] = SettingsVm.PostLogin.AnalyzeFarmlists;
            _config[BotOptionPayloadKeys.PostLoginAnalyzeHero] = SettingsVm.PostLogin.AnalyzeHero;
            _config[BotOptionPayloadKeys.PostLoginReadTroopTrainingQueue] = SettingsVm.PostLogin.ReadTroopTrainingQueue;
            _config[BotOptionPayloadKeys.PostLoginAnalyzeBrewery] = SettingsVm.PostLogin.AnalyzeBrewery;
            _config[BotOptionPayloadKeys.PostLoginAnalyzeHeroInventory] = SettingsVm.PostLogin.AnalyzeHeroInventory;
            _config[BotOptionPayloadKeys.PostLoginAnalyzeNewVillages] = SettingsVm.PostLogin.AnalyzeNewVillages;
            _config[BotOptionPayloadKeys.PostLoginAnalyzeNewAccount] = SettingsVm.PostLogin.AnalyzeNewAccount;
            _config[BotOptionPayloadKeys.SilverLimit] = silverLimit;
            _config[BotOptionPayloadKeys.DailySilverSpendingLimit] = dailySilverSpendingLimit;
            var saveResult = _settingsPersistence.Save(_config);
            if (!string.IsNullOrWhiteSpace(saveResult.ValidationError))
            {
                AppDialog.Show(this, saveResult.ValidationError, "Proxy setup conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (saveResult.Exception is not null)
            {
                AppDialog.Show(this, saveResult.Exception.Message, "Save settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            AppDialog.Show(this, ex.Message, "Save settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private bool TryValidateNumericInputs()
    {
        var fields = new (TextBox TextBox, string Label, bool WholeNumber, double Min, double Max)[]
        {
            (SessionRunMinMinutesTextBox, "Session pacing run minimum", true, 1, 10080),
            (SessionRunMaxMinutesTextBox, "Session pacing run maximum", true, 1, 10080),
            (SessionSleepMinMinutesTextBox, "Session pacing sleep minimum", true, 5, 10080),
            (SessionSleepMaxMinutesTextBox, "Session pacing sleep maximum", true, 5, 10080),
            (ActionTaskMinTextBox, "Task action delay minimum", false, 0, 3600),
            (ActionTaskMaxTextBox, "Task action delay maximum", false, 0, 3600),
            (ActionPageLoadMinTextBox, "Page-load delay minimum", false, 0, 3600),
            (ActionPageLoadMaxTextBox, "Page-load delay maximum", false, 0, 3600),
            (ActionClickMinTextBox, "Click delay minimum", false, 0, 3600),
            (ActionClickMaxTextBox, "Click delay maximum", false, 0, 3600),
            (ActionLoopMinTextBox, "Loop delay minimum", false, 0, 3600),
            (ActionLoopMaxTextBox, "Loop delay maximum", false, 0, 3600),
            (ContinuousKeepAliveMinMinutesTextBox, "Keep Alive minimum", true, 1, 1440),
            (ContinuousKeepAliveMaxMinutesTextBox, "Keep Alive maximum", true, 1, 1440),
            (FarmListStepDelayMinTextBox, "Farm-list step delay minimum", false, 0, 3600),
            (FarmListStepDelayMaxTextBox, "Farm-list step delay maximum", false, 0, 3600),
            (CollectStepDelayMinTextBox, "Collect step delay minimum", false, 0, 3600),
            (CollectStepDelayMaxTextBox, "Collect step delay maximum", false, 0, 3600),
            (IdleBreakIntervalMinTextBox, "Idle-break interval minimum", false, 0, 3600),
            (IdleBreakIntervalMaxTextBox, "Idle-break interval maximum", false, 0, 3600),
            (IdleBreakDurationMinTextBox, "Idle-break duration minimum", false, 0, 3600),
            (IdleBreakDurationMaxTextBox, "Idle-break duration maximum", false, 0, 3600),
            (IdleBrowseIntervalMinTextBox, "Idle-browse interval minimum", false, 0, 3600),
            (IdleBrowseIntervalMaxTextBox, "Idle-browse interval maximum", false, 0, 3600),
            (VillageStatusSweepRoundMinTextBox, "Village scan function delay minimum", true, 1, 1440),
            (VillageStatusSweepRoundMaxTextBox, "Village scan function delay maximum", true, 1, 1440),
            (VillageStatusSweepVillageMinTextBox, "Village scan village delay minimum", false, 0, 3600),
            (VillageStatusSweepVillageMaxTextBox, "Village scan village delay maximum", false, 0, 3600),
            (ConstructionHumanizeQueuePercentMinTextBox, "Construction queue percentage minimum", false, 0, 99),
            (ConstructionHumanizeQueuePercentMaxTextBox, "Construction queue percentage maximum", false, 0, 99),
            (ConstructionHumanizeMaxDelayTextBox, "Construction maximum delay", false, 0, 600),
            (ConstructionHumanizeNoPlusMinTextBox, "Construction no-Plus delay minimum", false, 0, 600),
            (ConstructionHumanizeNoPlusMaxTextBox, "Construction no-Plus delay maximum", false, 0, 600),
            (DemolishDelayMinTextBox, "Demolish delay minimum", true, 0, 1440),
            (DemolishDelayMaxTextBox, "Demolish delay maximum", true, 0, 1440),
            (HeroAdventureRestartDelayMinTextBox, "Hero adventure restart delay minimum", false, 0, double.MaxValue),
            (HeroAdventureRestartDelayMaxTextBox, "Hero adventure restart delay maximum", false, 0, double.MaxValue),
            (HeroCropAntiStarveTriggerTextBox, "Anti-starve trigger", true, 1, 1440),
            (HeroCropAntiStarveTargetTextBox, "Anti-starve target", true, 1, 1440),
            (HeroCropAntiStarveMaxTransferTextBox, "Anti-starve maximum crop per transfer", true, 1, int.MaxValue),
            (HeroCropAntiStarveMinRemainingTextBox, "Anti-starve minimum hero crop remaining", true, 0, int.MaxValue),
            (SmithyUpgradeRestartDelayMinTextBox, "Smithy restart delay minimum", false, 0, double.MaxValue),
            (SmithyUpgradeRestartDelayMaxTextBox, "Smithy restart delay maximum", false, 0, double.MaxValue),
            (TownHallRestartDelayMinTextBox, "Town Hall restart delay minimum", false, 0, double.MaxValue),
            (TownHallRestartDelayMaxTextBox, "Town Hall restart delay maximum", false, 0, double.MaxValue),
            (BreweryRestartDelayMinTextBox, "Brewery restart delay minimum", false, 0, double.MaxValue),
            (BreweryRestartDelayMaxTextBox, "Brewery restart delay maximum", false, 0, double.MaxValue),
            (GoldLimitTextBox, "Minimum gold balance", true, 0, int.MaxValue),
            (DailyGoldSpendingLimitTextBox, "Daily gold spending limit", true, 0, int.MaxValue),
            (SilverLimitTextBox, "Minimum silver balance", true, 0, int.MaxValue),
            (DailySilverSpendingLimitTextBox, "Daily silver spending limit", true, 0, int.MaxValue),
            (FarmListLastSentLimitHoursTextBox, "Farm-list last sent limit", true, 1, FarmingDefaults.MaxLastSentLimitHours),
        };

        foreach (var field in fields)
        {
            if (TryValidateNumericInputText(
                    field.TextBox.Text,
                    field.WholeNumber,
                    field.Min,
                    field.Max,
                    out var error))
            {
                continue;
            }

            ShowInvalidNumericInput(
                field.TextBox,
                field.Label,
                error,
                field.WholeNumber,
                field.Min,
                field.Max);
            return false;
        }

        var ranges = new (TextBox Min, TextBox Max, string Label, bool WholeNumber)[]
        {
            (SessionRunMinMinutesTextBox, SessionRunMaxMinutesTextBox, "Session pacing run", true),
            (SessionSleepMinMinutesTextBox, SessionSleepMaxMinutesTextBox, "Session pacing sleep", true),
            (ActionTaskMinTextBox, ActionTaskMaxTextBox, "Task action delay", false),
            (ActionPageLoadMinTextBox, ActionPageLoadMaxTextBox, "Page-load delay", false),
            (ActionClickMinTextBox, ActionClickMaxTextBox, "Click delay", false),
            (ActionLoopMinTextBox, ActionLoopMaxTextBox, "Loop delay", false),
            (ContinuousKeepAliveMinMinutesTextBox, ContinuousKeepAliveMaxMinutesTextBox, "Keep Alive", true),
            (FarmListStepDelayMinTextBox, FarmListStepDelayMaxTextBox, "Farm-list step delay", false),
            (CollectStepDelayMinTextBox, CollectStepDelayMaxTextBox, "Collect step delay", false),
            (IdleBreakIntervalMinTextBox, IdleBreakIntervalMaxTextBox, "Idle-break interval", false),
            (IdleBreakDurationMinTextBox, IdleBreakDurationMaxTextBox, "Idle-break duration", false),
            (IdleBrowseIntervalMinTextBox, IdleBrowseIntervalMaxTextBox, "Idle-browse interval", false),
            (VillageStatusSweepRoundMinTextBox, VillageStatusSweepRoundMaxTextBox, "Village scan function delay", true),
            (VillageStatusSweepVillageMinTextBox, VillageStatusSweepVillageMaxTextBox, "Village scan village delay", false),
            (ConstructionHumanizeQueuePercentMinTextBox, ConstructionHumanizeQueuePercentMaxTextBox, "Construction queue percentage", false),
            (ConstructionHumanizeNoPlusMinTextBox, ConstructionHumanizeNoPlusMaxTextBox, "Construction no-Plus delay", false),
            (DemolishDelayMinTextBox, DemolishDelayMaxTextBox, "Demolish delay", true),
            (HeroAdventureRestartDelayMinTextBox, HeroAdventureRestartDelayMaxTextBox, "Hero adventure restart delay", false),
            (SmithyUpgradeRestartDelayMinTextBox, SmithyUpgradeRestartDelayMaxTextBox, "Smithy restart delay", false),
            (TownHallRestartDelayMinTextBox, TownHallRestartDelayMaxTextBox, "Town Hall restart delay", false),
            (BreweryRestartDelayMinTextBox, BreweryRestartDelayMaxTextBox, "Brewery restart delay", false),
        };

        foreach (var range in ranges)
        {
            var min = double.Parse(range.Min.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
            var max = double.Parse(range.Max.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (max >= min)
            {
                continue;
            }

            ShowInvalidNumericInput(
                range.Max,
                range.Label,
                "The maximum value must be greater than or equal to the minimum value.",
                range.WholeNumber,
                0,
                double.MaxValue,
                range.Min.Text);
            return false;
        }

        var antiStarveTrigger = int.Parse(HeroCropAntiStarveTriggerTextBox.Text, CultureInfo.InvariantCulture);
        var antiStarveTarget = int.Parse(HeroCropAntiStarveTargetTextBox.Text, CultureInfo.InvariantCulture);
        if (antiStarveTarget <= antiStarveTrigger)
        {
            ShowInvalidNumericInput(
                HeroCropAntiStarveTargetTextBox,
                "Anti-starve target",
                "The target must be greater than the trigger.",
                true,
                antiStarveTrigger + 1,
                1440,
                HeroCropAntiStarveTriggerTextBox.Text);
            return false;
        }

        return true;
    }

    private void HeroCropAntiStarveVillages_Click(object sender, RoutedEventArgs e)
    {
        var window = new HeroCropAntiStarveVillagesWindow(SettingsVm.Hero.CropAntiStarveVillages)
        {
            Owner = this,
        };
        if (window.ShowDialog() != true)
        {
            return;
        }

        SettingsVm.Hero.CropAntiStarveVillages.Clear();
        foreach (var row in window.Results)
        {
            SettingsVm.Hero.CropAntiStarveVillages.Add(row);
        }
    }

    internal static bool TryValidateNumericInputText(
        string? text,
        bool wholeNumber,
        double min,
        double max,
        out string error)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            error = "Enter a value.";
            return false;
        }

        if (trimmed.Contains(','))
        {
            error = wholeNumber
                ? "Enter a whole number without a decimal separator, for example 5."
                : "Use a period as the decimal separator, for example 5.6 instead of 5,6.";
            return false;
        }

        double value;
        if (wholeNumber)
        {
            if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedInteger))
            {
                error = "Enter a whole number.";
                return false;
            }

            value = parsedInteger;
        }
        else if (!double.TryParse(
                     trimmed,
                     NumberStyles.Float,
                     CultureInfo.InvariantCulture,
                     out value)
                 || !double.IsFinite(value))
        {
            error = "Enter a valid number using a period as the decimal separator, for example 5.6.";
            return false;
        }

        if (value < min || value > max)
        {
            error = max == double.MaxValue
                ? $"Enter a value of at least {min.ToString(CultureInfo.InvariantCulture)}."
                : $"Enter a value between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static string GetNumericInputCorrectionExample(
        string? text,
        bool wholeNumber,
        double min,
        double max)
    {
        var normalized = (text ?? string.Empty).Trim().Replace(',', '.');
        var parsed = double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var numericValue)
            && double.IsFinite(numericValue);

        var boundedMin = double.IsFinite(min) ? min : 0;
        var boundedMax = double.IsFinite(max) ? max : double.MaxValue;
        if (boundedMax < boundedMin)
        {
            boundedMax = boundedMin;
        }

        var value = parsed ? numericValue : boundedMin;
        value = Math.Clamp(value, boundedMin, boundedMax);
        if (wholeNumber)
        {
            value = parsed ? Math.Truncate(value) : Math.Ceiling(boundedMin);
            value = Math.Clamp(value, Math.Ceiling(boundedMin), Math.Floor(boundedMax));
        }

        return value.ToString(wholeNumber ? "0" : "0.###############", CultureInfo.InvariantCulture);
    }

    private void ShowInvalidNumericInput(
        TextBox textBox,
        string label,
        string error,
        bool wholeNumber,
        double min,
        double max,
        string? suggestedValue = null)
    {
        var enteredValue = string.IsNullOrWhiteSpace(textBox.Text)
            ? "(empty)"
            : textBox.Text.Trim();
        var correctedValue = string.IsNullOrWhiteSpace(suggestedValue)
            ? GetNumericInputCorrectionExample(textBox.Text, wholeNumber, min, max)
            : suggestedValue.Trim();

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
        });
        content.Children.Add(new TextBlock
        {
            Text = error,
            Margin = new Thickness(0, 4, 0, 12),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
        });

        var valueCards = new Grid();
        valueCards.ColumnDefinitions.Add(new ColumnDefinition());
        valueCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        valueCards.ColumnDefinitions.Add(new ColumnDefinition());

        var enteredCard = CreateNumericValidationValueCard(
            "Incorrect",
            enteredValue,
            "WarningBgBrush",
            "WarningBorderBrush",
            "WarningTextBrush");
        Grid.SetColumn(enteredCard, 0);
        valueCards.Children.Add(enteredCard);

        var correctedCard = CreateNumericValidationValueCard(
            "Correct",
            correctedValue,
            "SuccessBgBrush",
            "SuccessBorderBrush",
            "SuccessTextBrush");
        Grid.SetColumn(correctedCard, 2);
        valueCards.Children.Add(correctedCard);
        content.Children.Add(valueCards);

        content.Children.Add(new TextBlock
        {
            Text = "Settings were not saved.",
            Margin = new Thickness(0, 12, 0, 0),
            FontSize = 12,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSubtleBrush"),
        });

        AppDialog.ShowCustomContent(
            this,
            content,
            "Invalid settings value",
            [("OK", MessageBoxResult.OK)],
            MessageBoxImage.Warning,
            MessageBoxResult.OK,
            MessageBoxResult.OK,
            accentResult: MessageBoxResult.OK,
            width: 540);
        textBox.Focus();
        textBox.SelectAll();
    }

    private Border CreateNumericValidationValueCard(
        string caption,
        string value,
        string backgroundResource,
        string borderResource,
        string foregroundResource)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = caption,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource(foregroundResource),
        });
        text.Children.Add(new TextBlock
        {
            Text = value,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource(foregroundResource),
        });

        return new Border
        {
            Background = (System.Windows.Media.Brush)FindResource(backgroundResource),
            BorderBrush = (System.Windows.Media.Brush)FindResource(borderResource),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(12, 10, 12, 10),
            Child = text,
        };
    }

    private void RequestSleepNow()
    {
        var confirm = AppDialog.Show(
            this,
            "Put the bot to sleep now? It will stop automation, log out, and stay asleep for the configured sleep time before resuming automatically.",
            "Sleep now",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        // Persist first so the sleep uses the current sleep-time/variation values.
        if (!PersistConfig())
        {
            return;
        }

        SleepNowRequested = true;
        _isClosing = true;
        CaptureTownHallResults();
        DialogResult = true;
        Close();
    }

    private void CancelSettings()
    {
        _isClosing = true;
        DialogResult = false;
        Close();
    }

    private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
    }

    private void ResetSettings()
    {
        var confirm = AppDialog.Show(
            this,
            "Reset saved settings to default for the current account? Other accounts and the selected server are kept.",
            "Reset settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _settingsPersistence.ResetToDefaults(_config);
        if (!string.IsNullOrWhiteSpace(result.ValidationError))
        {
            LoadConfig();
            AppDialog.Show(
                this,
                result.ValidationError + "\n\nThe previous Settings values were restored.",
                "Proxy setup conflict",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (result.Exception is null)
        {
            LoadConfig();
            return;
        }

        AppDialog.Show(this, result.Exception.Message, "Reset settings", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ResetDailySilverLimit()
    {
        if (ResetDailySpending(_resetDailySilverSpending))
        {
            SettingsVm.DailySilverSpent = 0;
        }
    }

    private void ResetDailyGoldLimit()
    {
        if (ResetDailySpending(_resetDailyGoldSpending))
        {
            SettingsVm.DailyGoldSpent = 0;
        }
    }

    private bool ResetDailySpending(Action? reset)
    {
        if (reset is null)
        {
            return false;
        }

        try
        {
            reset();
            return true;
        }
        catch (Exception ex)
        {
            AppDialog.Show(this, ex.Message, "Could not reset daily spending", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    // Tab content can remain disconnected until selected. Keep those controls in sync with the ViewModel
    // so validation and the visible usage badges behave the same for every initially selected category.
    private void SynchronizeSpendingLimitControls()
    {
        GoldLimitTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.GoldLimitText);
        DailyGoldSpendingLimitTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.DailyGoldSpendingLimitText);
        SilverLimitTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.SilverLimitText);
        DailySilverSpendingLimitTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.DailySilverSpendingLimitText);
        UpdateDailySpendingUsage();
    }

    private void UpdateDailySpendingUsage()
    {
        DailyGoldSpendingUsageTextBlock.SetCurrentValue(
            TextBlock.TextProperty,
            SettingsVm.DailyGoldSpendingUsageText);
        DailySilverSpendingUsageTextBlock.SetCurrentValue(
            TextBlock.TextProperty,
            SettingsVm.DailySilverSpendingUsageText);
    }

    private void SynchronizeActionPacingControls()
    {
        ActionTaskMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.TaskMinSeconds);
        ActionTaskMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.TaskMaxSeconds);
        ActionPageLoadMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.PageLoadMinSeconds);
        ActionPageLoadMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.PageLoadMaxSeconds);
        ActionClickMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.ClickMinSeconds);
        ActionClickMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.ClickMaxSeconds);
        ActionLoopMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.LoopMinSeconds);
        ActionLoopMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.LoopMaxSeconds);
        FarmListStepDelayMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.FarmListStepDelayMinSeconds);
        FarmListStepDelayMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.FarmListStepDelayMaxSeconds);
        CollectStepDelayMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.CollectStepDelayMinSeconds);
        CollectStepDelayMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.CollectStepDelayMaxSeconds);
        IdleBreakEnabledCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBreakEnabled);
        IdleBreakIntervalMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.IdleBreakIntervalMinMinutes);
        IdleBreakIntervalMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.IdleBreakIntervalMaxMinutes);
        IdleBreakDurationMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.IdleBreakDurationMinMinutes);
        IdleBreakDurationMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.IdleBreakDurationMaxMinutes);
        IdleBrowseEnabledCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowseEnabled);
        IdleBrowseIntervalMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.IdleBrowseIntervalMinMinutes);
        IdleBrowseIntervalMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.IdleBrowseIntervalMaxMinutes);
        IdleBrowsePageMapCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowsePageMap);
        IdleBrowsePageStatisticsCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowsePageStatistics);
        IdleBrowsePageStatisticsHeroCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowsePageStatisticsHero);
        IdleBrowsePageStatisticsTop10CheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowsePageStatisticsTop10);
        IdleBrowsePageStatisticsDefendersCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowsePageStatisticsDefenders);
        IdleBrowsePageStatisticsAttackersCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowsePageStatisticsAttackers);
        IdleBrowsePageReportsCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowsePageReports);
        IdleBrowsePageMessagesCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.IdleBrowsePageMessages);
        ContinuousKeepAliveEnabledCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.ContinuousKeepAliveEnabled);
        ContinuousKeepAliveMinMinutesTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.ContinuousKeepAliveMinMinutes);
        ContinuousKeepAliveMaxMinutesTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.ContinuousKeepAliveMaxMinutes);
        SessionPacingEnabledCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.SessionPacingEnabled);
        SessionRunMinMinutesTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.SessionRunMinMinutes);
        SessionRunMaxMinutesTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.SessionRunMaxMinutes);
        SessionSleepMinMinutesTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.SessionSleepMinMinutes);
        SessionSleepMaxMinutesTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.SessionSleepMaxMinutes);
        VillageStatusSweepEnabledCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepEnabled);
        VillageStatusSweepDorf1CheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepDorf1Enabled);
        VillageStatusSweepDorf2CheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepDorf2Enabled);
        VillageStatusSweepSmithyCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepSmithyEnabled);
        VillageStatusSweepBarracksCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepBarracksEnabled);
        VillageStatusSweepStableCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepStableEnabled);
        VillageStatusSweepWorkshopCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepWorkshopEnabled);
        VillageStatusSweepTownHallCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepTownHallEnabled);
        VillageStatusSweepBreweryCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.Pacing.VillageStatusSweepBreweryEnabled);
        VillageStatusSweepRoundMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.VillageStatusSweepRoundMinMinutes);
        VillageStatusSweepRoundMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.VillageStatusSweepRoundMaxMinutes);
        VillageStatusSweepVillageMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.VillageStatusSweepVillageMinSeconds);
        VillageStatusSweepVillageMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Pacing.VillageStatusSweepVillageMaxSeconds);
    }

    private void SynchronizeConstructionControls()
    {
        StorageUpgradeLevelsAheadComboBox.SetCurrentValue(
            Selector.SelectedItemProperty,
            SettingsVm.Construction.StorageUpgradeLevelsAhead);
        ConstructionHumanizeCheckBox.SetCurrentValue(
            ToggleButton.IsCheckedProperty,
            SettingsVm.Construction.HumanizeDelayEnabled);
        ConstructionHumanizeQueuePercentMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Construction.QueuePercentMin);
        ConstructionHumanizeQueuePercentMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Construction.QueuePercentMax);
        ConstructionHumanizeMaxDelayTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Construction.MaxDelayMinutes);
        ConstructionHumanizeNoPlusMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Construction.NoPlusDelayMinMinutes);
        ConstructionHumanizeNoPlusMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Construction.NoPlusDelayMaxMinutes);
        DemolishDelayMinTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Construction.DemolishDelayMinMinutes);
        DemolishDelayMaxTextBox.SetCurrentValue(TextBox.TextProperty, SettingsVm.Construction.DemolishDelayMaxMinutes);
    }

    private void SynchronizeFarmingControls()
    {
        ShowFarmListLastSentTimerCheckBox.SetCurrentValue(
            ToggleButton.IsCheckedProperty,
            SettingsVm.Farming.ShowFarmListLastSentTimer);
        FarmListLastSentLimitEnabledCheckBox.SetCurrentValue(
            ToggleButton.IsCheckedProperty,
            SettingsVm.Farming.FarmListLastSentLimitEnabled);
        FarmListLastSentLimitHoursTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Farming.FarmListLastSentLimitHours);
    }

    private void SynchronizePostLoginControls()
    {
        PostLoginAnalyzeFarmlistsCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.PostLogin.AnalyzeFarmlists);
        PostLoginAnalyzeHeroCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.PostLogin.AnalyzeHero);
        PostLoginAnalyzeHeroInventoryCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.PostLogin.AnalyzeHeroInventory);
        PostLoginReadTroopTrainingQueueCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.PostLogin.ReadTroopTrainingQueue);
        PostLoginAnalyzeBreweryCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.PostLogin.AnalyzeBrewery);
        PostLoginAnalyzeNewVillagesCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.PostLogin.AnalyzeNewVillages);
        PostLoginAnalyzeNewAccountCheckBox.SetCurrentValue(ToggleButton.IsCheckedProperty, SettingsVm.PostLogin.AnalyzeNewAccount);
    }

    private void SynchronizeHeroControls()
    {
        HeroAdventureRestartDelayMinTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Hero.AdventureRestartDelay.DelayMinMinutes);
        HeroAdventureRestartDelayMaxTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Hero.AdventureRestartDelay.DelayMaxMinutes);
        HeroHpRegenPerDayComboBox.SetCurrentValue(
            Selector.SelectedItemProperty,
            SettingsVm.Hero.HpRegenPerDayPercent);
        SmithyUpgradeRestartDelayMinTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Hero.SmithyUpgradeRestartDelay.DelayMinMinutes);
        SmithyUpgradeRestartDelayMaxTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Hero.SmithyUpgradeRestartDelay.DelayMaxMinutes);
    }

    private void SynchronizeCelebrationControls()
    {
        TownHallRestartDelayMinTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Celebrations.TownHallQueue.DelayMinMinutes);
        TownHallRestartDelayMaxTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Celebrations.TownHallQueue.DelayMaxMinutes);
        BreweryRestartDelayMinTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Celebrations.BreweryRestartDelay.DelayMinMinutes);
        BreweryRestartDelayMaxTextBox.SetCurrentValue(
            TextBox.TextProperty,
            SettingsVm.Celebrations.BreweryRestartDelay.DelayMaxMinutes);
    }

    private void DetailedBrowserLoggingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressInitialConfirmationDialogs
            || _suppressDetailedBrowserLoggingConfirmation
            || DetailedBrowserLoggingCheckBox.IsChecked != true)
        {
            return;
        }

        var result = AppDialog.ShowCustomContent(
            this,
            BuildDetailedBrowserLoggingConfirmContent(),
            "Enable detailed browser logging?",
            [("Toggle ON", MessageBoxResult.Yes), ("Cancel", MessageBoxResult.Cancel)],
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel,
            MessageBoxResult.Cancel,
            warningResult: MessageBoxResult.Yes);
        if (result == MessageBoxResult.Yes)
        {
            return;
        }

        _suppressDetailedBrowserLoggingConfirmation = true;
        try
        {
            DetailedBrowserLoggingCheckBox.IsChecked = false;
        }
        finally
        {
            _suppressDetailedBrowserLoggingConfirmation = false;
        }
    }

    // Structured content for the detailed-browser-logging confirmation dialog (headline, bullet list
    // and a warning note) instead of one long text paragraph. Brushes are set via resource references
    // so the dialog follows the active theme.
    private static StackPanel BuildDetailedBrowserLoggingConfirmContent()
    {
        static TextBlock CreateText(string text, string foregroundResource, double topMargin = 0)
        {
            var block = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Margin = new Thickness(0, topMargin, 0, 0),
            };
            block.SetResourceReference(TextBlock.ForegroundProperty, foregroundResource);
            return block;
        }

        var panel = new StackPanel();

        var headline = CreateText("Development and troubleshooting only", "TextPrimaryBrush");
        headline.FontSize = 14;
        headline.FontWeight = FontWeights.SemiBold;
        panel.Children.Add(headline);

        panel.Children.Add(CreateText(
            "Records high-volume technical details about browser activity:",
            "TextSecondaryBrush",
            topMargin: 8));
        foreach (var line in new[]
                 {
                     "Navigation, reloads and refreshes",
                     "Page reads, waits and retries",
                     "Cache decisions",
                 })
        {
            var bullet = CreateText($"•  {line}", "TextSecondaryBrush", topMargin: 4);
            bullet.Margin = new Thickness(10, 4, 0, 0);
            panel.Children.Add(bullet);
        }

        var noteText = CreateText(
            "Can create large log files and may slightly affect performance. Do not enable during normal use.",
            "TextSecondaryBrush");
        noteText.FontSize = 12;
        var noteBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 12, 0, 0),
            Child = noteText,
        };
        noteBorder.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        noteBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        panel.Children.Add(noteBorder);

        return panel;
    }

    private void LoadPacingConfigToUi()
    {
        SettingsVm.Pacing.SessionPacingEnabled = ReadBool(BotOptionPayloadKeys.SessionPacingEnabled, PacingDefaults.SessionPacingEnabled);
        SettingsVm.Pacing.SessionRunMinMinutes = ReadInt(BotOptionPayloadKeys.SessionPacingRunMinMinutes, PacingDefaults.SessionPacingRunMinMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Pacing.SessionRunMaxMinutes = ReadInt(BotOptionPayloadKeys.SessionPacingRunMaxMinutes, PacingDefaults.SessionPacingRunMaxMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Pacing.SessionSleepMinMinutes = ReadInt(BotOptionPayloadKeys.SessionPacingSleepMinMinutes, PacingDefaults.SessionPacingSleepMinMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Pacing.SessionSleepMaxMinutes = ReadInt(BotOptionPayloadKeys.SessionPacingSleepMaxMinutes, PacingDefaults.SessionPacingSleepMaxMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Pacing.SessionDailyMaxHours = ReadInt(BotOptionPayloadKeys.SessionPacingDailyMaxHours, PacingDefaults.SessionPacingDailyMaxHours);
        SettingsVm.Pacing.SessionDailyMaxVariationPercent = ReadInt(BotOptionPayloadKeys.SessionPacingDailyMaxVariationPercent, PacingDefaults.SessionPacingDailyMaxVariationPercent);
        SettingsVm.Pacing.SetSessionAllowedHours(ReadAllowedHours());

        SettingsVm.Pacing.SessionHoursVariationPercent = ReadInt(BotOptionPayloadKeys.SessionPacingHoursVariationPercent, PacingDefaults.SessionPacingHoursVariationPercent);

        SettingsVm.Pacing.TaskMinSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingTaskMinSeconds, PacingDefaults.ActionPacingTaskMinSeconds));
        SettingsVm.Pacing.TaskMaxSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingTaskMaxSeconds, PacingDefaults.ActionPacingTaskMaxSeconds));
        SettingsVm.Pacing.PageLoadMinSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingPageLoadMinSeconds, PacingDefaults.ActionPacingPageLoadMinSeconds));
        SettingsVm.Pacing.PageLoadMaxSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingPageLoadMaxSeconds, PacingDefaults.ActionPacingPageLoadMaxSeconds));
        SettingsVm.Pacing.ClickMinSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingClickMinSeconds, PacingDefaults.ActionPacingClickMinSeconds));
        SettingsVm.Pacing.ClickMaxSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingClickMaxSeconds, PacingDefaults.ActionPacingClickMaxSeconds));
        SettingsVm.Pacing.LoopMinSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingLoopMinSeconds, PacingDefaults.ActionPacingLoopMinSeconds));
        SettingsVm.Pacing.LoopMaxSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingLoopMaxSeconds, PacingDefaults.ActionPacingLoopMaxSeconds));
        SettingsVm.Pacing.ShortVillageDeferSeconds = PacingDefaults.NormalizeShortVillageDeferSeconds(
            ReadInt(BotOptionPayloadKeys.ShortVillageDeferSeconds, PacingDefaults.ShortVillageDeferSeconds));
        SettingsVm.Pacing.ContinuousKeepAliveEnabled = ReadBool(BotOptionPayloadKeys.ContinuousKeepAliveEnabled, PacingDefaults.ContinuousKeepAliveEnabled);
        SettingsVm.Pacing.ContinuousKeepAliveMinMinutes = ReadInt(BotOptionPayloadKeys.ContinuousKeepAliveMinMinutes, PacingDefaults.ContinuousKeepAliveMinMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Pacing.ContinuousKeepAliveMaxMinutes = ReadInt(BotOptionPayloadKeys.ContinuousKeepAliveMaxMinutes, PacingDefaults.ContinuousKeepAliveMaxMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Pacing.FarmListStepDelayMinSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.FarmListStepDelayMinSeconds, PacingDefaults.FarmListStepDelayMinSeconds));
        SettingsVm.Pacing.FarmListStepDelayMaxSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.FarmListStepDelayMaxSeconds, PacingDefaults.FarmListStepDelayMaxSeconds));
        SettingsVm.Pacing.VillageStatusSweepEnabled = ReadBool(BotOptionPayloadKeys.VillageStatusSweepEnabled, PacingDefaults.VillageStatusSweepEnabled);
        SettingsVm.Pacing.VillageStatusSweepDorf1Enabled = ReadBool(BotOptionPayloadKeys.VillageStatusSweepDorf1Enabled, true);
        var villageStatusSweepDorf2Enabled = ReadBool(BotOptionPayloadKeys.VillageStatusSweepDorf2Enabled, false);
        SettingsVm.Pacing.VillageStatusSweepDorf2Enabled = villageStatusSweepDorf2Enabled;
        SettingsVm.Pacing.VillageStatusSweepSmithyEnabled = villageStatusSweepDorf2Enabled && ReadBool(BotOptionPayloadKeys.VillageStatusSweepSmithyEnabled, false);
        SettingsVm.Pacing.VillageStatusSweepBarracksEnabled = villageStatusSweepDorf2Enabled && ReadBool(BotOptionPayloadKeys.VillageStatusSweepBarracksEnabled, false);
        SettingsVm.Pacing.VillageStatusSweepStableEnabled = villageStatusSweepDorf2Enabled && ReadBool(BotOptionPayloadKeys.VillageStatusSweepStableEnabled, false);
        SettingsVm.Pacing.VillageStatusSweepWorkshopEnabled = villageStatusSweepDorf2Enabled && ReadBool(BotOptionPayloadKeys.VillageStatusSweepWorkshopEnabled, false);
        SettingsVm.Pacing.VillageStatusSweepTownHallEnabled = villageStatusSweepDorf2Enabled && ReadBool(BotOptionPayloadKeys.VillageStatusSweepTownHallEnabled, false);
        SettingsVm.Pacing.VillageStatusSweepBreweryEnabled = villageStatusSweepDorf2Enabled && ReadBool(BotOptionPayloadKeys.VillageStatusSweepBreweryEnabled, false);
        SettingsVm.Pacing.VillageStatusSweepRoundMinMinutes = ReadInt(BotOptionPayloadKeys.VillageStatusSweepRoundMinMinutes, PacingDefaults.VillageStatusSweepRoundMinMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Pacing.VillageStatusSweepRoundMaxMinutes = ReadInt(BotOptionPayloadKeys.VillageStatusSweepRoundMaxMinutes, PacingDefaults.VillageStatusSweepRoundMaxMinutes).ToString(CultureInfo.InvariantCulture);
        SettingsVm.Pacing.VillageStatusSweepVillageMinSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.VillageStatusSweepVillageMinSeconds, PacingDefaults.VillageStatusSweepVillageMinSeconds));
        SettingsVm.Pacing.VillageStatusSweepVillageMaxSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.VillageStatusSweepVillageMaxSeconds, PacingDefaults.VillageStatusSweepVillageMaxSeconds));

        SettingsVm.Pacing.IdleBreakEnabled = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBreakEnabled, PacingDefaults.ActionPacingIdleBreakEnabled);
        SettingsVm.Pacing.IdleBreakIntervalMinMinutes = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingIdleBreakIntervalMinMinutes, PacingDefaults.ActionPacingIdleBreakIntervalMinMinutes));
        SettingsVm.Pacing.IdleBreakIntervalMaxMinutes = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingIdleBreakIntervalMaxMinutes, PacingDefaults.ActionPacingIdleBreakIntervalMaxMinutes));
        SettingsVm.Pacing.IdleBreakDurationMinMinutes = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingIdleBreakDurationMinMinutes, PacingDefaults.ActionPacingIdleBreakDurationMinMinutes));
        SettingsVm.Pacing.IdleBreakDurationMaxMinutes = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingIdleBreakDurationMaxMinutes, PacingDefaults.ActionPacingIdleBreakDurationMaxMinutes));

        SettingsVm.Pacing.IdleBrowseEnabled = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowseEnabled, PacingDefaults.ActionPacingIdleBrowseEnabled);
        SettingsVm.Pacing.IdleBrowseIntervalMinMinutes = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingIdleBrowseIntervalMinMinutes, PacingDefaults.ActionPacingIdleBrowseIntervalMinMinutes));
        SettingsVm.Pacing.IdleBrowseIntervalMaxMinutes = FormatDelay(ReadDouble(BotOptionPayloadKeys.ActionPacingIdleBrowseIntervalMaxMinutes, PacingDefaults.ActionPacingIdleBrowseIntervalMaxMinutes));
        SettingsVm.Pacing.IdleBrowsePageMap = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowsePageMap, PacingDefaults.ActionPacingIdleBrowsePageMap);
        SettingsVm.Pacing.IdleBrowsePageStatistics = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatistics, PacingDefaults.ActionPacingIdleBrowsePageStatistics);
        SettingsVm.Pacing.IdleBrowsePageStatisticsHero = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatisticsHero, PacingDefaults.ActionPacingIdleBrowsePageStatisticsHero);
        SettingsVm.Pacing.IdleBrowsePageStatisticsTop10 = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatisticsTop10, PacingDefaults.ActionPacingIdleBrowsePageStatisticsTop10);
        SettingsVm.Pacing.IdleBrowsePageStatisticsDefenders = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatisticsDefenders, PacingDefaults.ActionPacingIdleBrowsePageStatisticsDefenders);
        SettingsVm.Pacing.IdleBrowsePageStatisticsAttackers = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatisticsAttackers, PacingDefaults.ActionPacingIdleBrowsePageStatisticsAttackers);
        SettingsVm.Pacing.IdleBrowsePageReports = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowsePageReports, PacingDefaults.ActionPacingIdleBrowsePageReports);
        SettingsVm.Pacing.IdleBrowsePageMessages = ReadBool(BotOptionPayloadKeys.ActionPacingIdleBrowsePageMessages, PacingDefaults.ActionPacingIdleBrowsePageMessages);

        SettingsVm.Pacing.CollectStepDelayMinSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.CollectStepDelayMinSeconds, PacingDefaults.CollectStepDelayMinSeconds));
        SettingsVm.Pacing.CollectStepDelayMaxSeconds = FormatDelay(ReadDouble(BotOptionPayloadKeys.CollectStepDelayMaxSeconds, PacingDefaults.CollectStepDelayMaxSeconds));
        SynchronizeActionPacingControls();
    }

    private void SavePacingConfigFromUi()
    {
        _config[BotOptionPayloadKeys.SessionPacingEnabled] = SettingsVm.Pacing.SessionPacingEnabled;
        _config[BotOptionPayloadKeys.SessionPacingRunMinMinutes] = ReadIntText(SettingsVm.Pacing.SessionRunMinMinutes, PacingDefaults.SessionPacingRunMinMinutes, 1, 10080);
        _config[BotOptionPayloadKeys.SessionPacingRunMaxMinutes] = ReadIntText(SettingsVm.Pacing.SessionRunMaxMinutes, PacingDefaults.SessionPacingRunMaxMinutes, 1, 10080);
        _config[BotOptionPayloadKeys.SessionPacingSleepMinMinutes] = ReadIntText(SettingsVm.Pacing.SessionSleepMinMinutes, PacingDefaults.SessionPacingSleepMinMinutes, 5, 10080);
        _config[BotOptionPayloadKeys.SessionPacingSleepMaxMinutes] = ReadIntText(SettingsVm.Pacing.SessionSleepMaxMinutes, PacingDefaults.SessionPacingSleepMaxMinutes, 5, 10080);
        _config[BotOptionPayloadKeys.SessionPacingDailyMaxHours] = SettingsVm.Pacing.SessionDailyMaxHours;
        _config[BotOptionPayloadKeys.SessionPacingDailyMaxVariationPercent] = SettingsVm.Pacing.SessionDailyMaxVariationPercent;
        _config[BotOptionPayloadKeys.SessionPacingAllowedHours] = new JsonArray(
            SettingsVm.Pacing.GetSelectedSessionHours()
                .Select(hour => JsonValue.Create(hour))
                .ToArray());
        _config[BotOptionPayloadKeys.SessionPacingHoursVariationPercent] = SettingsVm.Pacing.SessionHoursVariationPercent;

        _config[BotOptionPayloadKeys.ActionPacingEnabled] = true;
        WriteDelayRange(BotOptionPayloadKeys.ActionPacingTaskMinSeconds, BotOptionPayloadKeys.ActionPacingTaskMaxSeconds, SettingsVm.Pacing.TaskMinSeconds, SettingsVm.Pacing.TaskMaxSeconds, PacingDefaults.ActionPacingTaskMinSeconds, PacingDefaults.ActionPacingTaskMaxSeconds);
        WriteDelayRange(BotOptionPayloadKeys.ActionPacingPageLoadMinSeconds, BotOptionPayloadKeys.ActionPacingPageLoadMaxSeconds, SettingsVm.Pacing.PageLoadMinSeconds, SettingsVm.Pacing.PageLoadMaxSeconds, PacingDefaults.ActionPacingPageLoadMinSeconds, PacingDefaults.ActionPacingPageLoadMaxSeconds);
        WriteDelayRange(BotOptionPayloadKeys.ActionPacingClickMinSeconds, BotOptionPayloadKeys.ActionPacingClickMaxSeconds, SettingsVm.Pacing.ClickMinSeconds, SettingsVm.Pacing.ClickMaxSeconds, PacingDefaults.ActionPacingClickMinSeconds, PacingDefaults.ActionPacingClickMaxSeconds);
        WriteDelayRange(BotOptionPayloadKeys.ActionPacingLoopMinSeconds, BotOptionPayloadKeys.ActionPacingLoopMaxSeconds, SettingsVm.Pacing.LoopMinSeconds, SettingsVm.Pacing.LoopMaxSeconds, PacingDefaults.ActionPacingLoopMinSeconds, PacingDefaults.ActionPacingLoopMaxSeconds);
        _config[BotOptionPayloadKeys.ShortVillageDeferSeconds] = PacingDefaults.NormalizeShortVillageDeferSeconds(
            SettingsVm.Pacing.ShortVillageDeferSeconds);
        _config[BotOptionPayloadKeys.ContinuousKeepAliveEnabled] = SettingsVm.Pacing.ContinuousKeepAliveEnabled;
        _config[BotOptionPayloadKeys.ContinuousKeepAliveMinMinutes] = ReadIntText(SettingsVm.Pacing.ContinuousKeepAliveMinMinutes, PacingDefaults.ContinuousKeepAliveMinMinutes, 1, 1440);
        _config[BotOptionPayloadKeys.ContinuousKeepAliveMaxMinutes] = ReadIntText(SettingsVm.Pacing.ContinuousKeepAliveMaxMinutes, PacingDefaults.ContinuousKeepAliveMaxMinutes, 1, 1440);
        WriteDelayRange(
            BotOptionPayloadKeys.FarmListStepDelayMinSeconds,
            BotOptionPayloadKeys.FarmListStepDelayMaxSeconds,
            SettingsVm.Pacing.FarmListStepDelayMinSeconds,
            SettingsVm.Pacing.FarmListStepDelayMaxSeconds,
            PacingDefaults.FarmListStepDelayMinSeconds,
            PacingDefaults.FarmListStepDelayMaxSeconds);
        _config[BotOptionPayloadKeys.VillageStatusSweepEnabled] = SettingsVm.Pacing.VillageStatusSweepEnabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepDorf1Enabled] = SettingsVm.Pacing.VillageStatusSweepDorf1Enabled;
        var dorf2Enabled = SettingsVm.Pacing.VillageStatusSweepDorf2Enabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepDorf2Enabled] = dorf2Enabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepSmithyEnabled] = dorf2Enabled && SettingsVm.Pacing.VillageStatusSweepSmithyEnabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepBarracksEnabled] = dorf2Enabled && SettingsVm.Pacing.VillageStatusSweepBarracksEnabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepStableEnabled] = dorf2Enabled && SettingsVm.Pacing.VillageStatusSweepStableEnabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepWorkshopEnabled] = dorf2Enabled && SettingsVm.Pacing.VillageStatusSweepWorkshopEnabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepTownHallEnabled] = dorf2Enabled && SettingsVm.Pacing.VillageStatusSweepTownHallEnabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepBreweryEnabled] = dorf2Enabled && SettingsVm.Pacing.VillageStatusSweepBreweryEnabled;
        _config[BotOptionPayloadKeys.VillageStatusSweepRoundMinMinutes] = ReadIntText(SettingsVm.Pacing.VillageStatusSweepRoundMinMinutes, PacingDefaults.VillageStatusSweepRoundMinMinutes, 1, 1440);
        _config[BotOptionPayloadKeys.VillageStatusSweepRoundMaxMinutes] = ReadIntText(SettingsVm.Pacing.VillageStatusSweepRoundMaxMinutes, PacingDefaults.VillageStatusSweepRoundMaxMinutes, 1, 1440);
        WriteDelayRange(BotOptionPayloadKeys.VillageStatusSweepVillageMinSeconds, BotOptionPayloadKeys.VillageStatusSweepVillageMaxSeconds, SettingsVm.Pacing.VillageStatusSweepVillageMinSeconds, SettingsVm.Pacing.VillageStatusSweepVillageMaxSeconds, PacingDefaults.VillageStatusSweepVillageMinSeconds, PacingDefaults.VillageStatusSweepVillageMaxSeconds);

        // Idle "step away" break (minutes). WriteDelayRange clamps and keeps max >= min.
        _config[BotOptionPayloadKeys.ActionPacingIdleBreakEnabled] = SettingsVm.Pacing.IdleBreakEnabled;
        WriteDelayRange(
            BotOptionPayloadKeys.ActionPacingIdleBreakIntervalMinMinutes,
            BotOptionPayloadKeys.ActionPacingIdleBreakIntervalMaxMinutes,
            SettingsVm.Pacing.IdleBreakIntervalMinMinutes,
            SettingsVm.Pacing.IdleBreakIntervalMaxMinutes,
            PacingDefaults.ActionPacingIdleBreakIntervalMinMinutes,
            PacingDefaults.ActionPacingIdleBreakIntervalMaxMinutes);
        WriteDelayRange(
            BotOptionPayloadKeys.ActionPacingIdleBreakDurationMinMinutes,
            BotOptionPayloadKeys.ActionPacingIdleBreakDurationMaxMinutes,
            SettingsVm.Pacing.IdleBreakDurationMinMinutes,
            SettingsVm.Pacing.IdleBreakDurationMaxMinutes,
            PacingDefaults.ActionPacingIdleBreakDurationMinMinutes,
            PacingDefaults.ActionPacingIdleBreakDurationMaxMinutes);

        // Idle browse (interval minutes + per-page toggles). WriteDelayRange clamps and keeps max >= min.
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowseEnabled] = SettingsVm.Pacing.IdleBrowseEnabled;
        WriteDelayRange(
            BotOptionPayloadKeys.ActionPacingIdleBrowseIntervalMinMinutes,
            BotOptionPayloadKeys.ActionPacingIdleBrowseIntervalMaxMinutes,
            SettingsVm.Pacing.IdleBrowseIntervalMinMinutes,
            SettingsVm.Pacing.IdleBrowseIntervalMaxMinutes,
            PacingDefaults.ActionPacingIdleBrowseIntervalMinMinutes,
            PacingDefaults.ActionPacingIdleBrowseIntervalMaxMinutes);
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowsePageMap] = SettingsVm.Pacing.IdleBrowsePageMap;
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatistics] = SettingsVm.Pacing.IdleBrowsePageStatistics;
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatisticsHero] = SettingsVm.Pacing.IdleBrowsePageStatisticsHero;
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatisticsTop10] = SettingsVm.Pacing.IdleBrowsePageStatisticsTop10;
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatisticsDefenders] = SettingsVm.Pacing.IdleBrowsePageStatisticsDefenders;
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowsePageStatisticsAttackers] = SettingsVm.Pacing.IdleBrowsePageStatisticsAttackers;
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowsePageReports] = SettingsVm.Pacing.IdleBrowsePageReports;
        _config[BotOptionPayloadKeys.ActionPacingIdleBrowsePageMessages] = SettingsVm.Pacing.IdleBrowsePageMessages;

        // Collect step delay (seconds). WriteDelayRange clamps and keeps max >= min.
        WriteDelayRange(
            BotOptionPayloadKeys.CollectStepDelayMinSeconds,
            BotOptionPayloadKeys.CollectStepDelayMaxSeconds,
            SettingsVm.Pacing.CollectStepDelayMinSeconds,
            SettingsVm.Pacing.CollectStepDelayMaxSeconds,
            PacingDefaults.CollectStepDelayMinSeconds,
            PacingDefaults.CollectStepDelayMaxSeconds);
    }

    private void ApplyPacingDefaultsToUi()
    {
        SettingsVm.Pacing.ResetDefaults();
        SynchronizeActionPacingControls();
    }

    private bool ReadBool(string key, bool defaultValue) => _config[key]?.GetValue<bool>() ?? defaultValue;

    private int ReadInt(string key, int defaultValue) => _config[key]?.GetValue<int>() ?? defaultValue;

    private double ReadDouble(string key, double defaultValue) => _config[key]?.GetValue<double>() ?? defaultValue;

    private void InitializeSessionPacingChoices()
    {
        SessionDailyMaxHoursComboBox.Items.Add(new ComboBoxItem { Content = "No limit", Tag = "0" });
        for (var hour = 1; hour <= 24; hour++)
        {
            SessionDailyMaxHoursComboBox.Items.Add(new ComboBoxItem
            {
                Content = $"{hour} h",
                Tag = hour.ToString(CultureInfo.InvariantCulture),
            });
        }

        // Daily-max variation: 0..50% in 10% steps. Independent of the run/sleep "Variation" dropdown.
        for (var percent = 0; percent <= 50; percent += 10)
        {
            SessionDailyMaxVariationComboBox.Items.Add(new ComboBoxItem
            {
                Content = percent == 0 ? "No variation" : $"±{percent}%",
                Tag = percent.ToString(CultureInfo.InvariantCulture),
            });
        }

        // Daily hours variation: 0..30% in 10% steps. Jitters the allowed-hours boundaries.
        for (var percent = 0; percent <= 30; percent += 10)
        {
            SessionHoursVariationComboBox.Items.Add(new ComboBoxItem
            {
                Content = $"{percent} %",
                Tag = percent.ToString(CultureInfo.InvariantCulture),
            });
        }

    }

    private HashSet<int> ReadAllowedHours()
    {
        if (_config[BotOptionPayloadKeys.SessionPacingAllowedHours] is not JsonArray array)
        {
            return Enumerable.Range(0, 24).ToHashSet();
        }

        return array
            .Select(node => node?.GetValue<int>() ?? -1)
            .Where(hour => hour is >= 0 and <= 23)
            .ToHashSet();
    }

    private void SessionDailyMaxHoursComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_suppressInitialConfirmationDialogs || !IsLoaded)
        {
            return;
        }

        var dailyMaxHours = SettingsVm.Pacing.SessionDailyMaxHours;
        if (dailyMaxHours is > 0 and <= PacingDefaults.SessionPacingDailyMaxHours)
        {
            return;
        }

        AppDialog.ShowCustom(
            this,
            "Using the bot for more than 12 hours per day increases the risk of being banned.",
            "Daily runtime warning",
            [("OK", MessageBoxResult.OK)],
            MessageBoxImage.Warning,
            MessageBoxResult.OK,
            MessageBoxResult.OK,
            successResult: MessageBoxResult.OK);
    }

    private static int ReadIntText(TextBox textBox, int defaultValue, int min, int max)
    {
        return ReadIntText(textBox.Text, defaultValue, min, max);
    }

    private static int ReadIntText(string? text, int defaultValue, int min, int max)
    {
        return int.TryParse(text, out var value)
            ? Math.Clamp(value, min, max)
            : defaultValue;
    }

    private static double ReadDoubleText(TextBox textBox, double defaultValue)
    {
        return ReadDoubleText(textBox.Text, defaultValue);
    }

    private static double ReadDoubleText(string? text, double defaultValue)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0, 3600)
            : defaultValue;
    }

    private static string FormatDelay(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void WriteDelayRange(string minKey, string maxKey, TextBox minTextBox, TextBox maxTextBox, double defaultMin, double defaultMax)
    {
        WriteDelayRange(minKey, maxKey, minTextBox.Text, maxTextBox.Text, defaultMin, defaultMax);
    }

    private void WriteDelayRange(string minKey, string maxKey, string minText, string maxText, double defaultMin, double defaultMax)
    {
        var min = ReadDoubleText(minText, defaultMin);
        var max = Math.Max(min, ReadDoubleText(maxText, defaultMax));
        _config[minKey] = min;
        _config[maxKey] = max;
    }

    private bool TryReadSpendingLimits(
        out int goldLimit,
        out int dailyGoldSpendingLimit,
        out int silverLimit,
        out int dailySilverSpendingLimit)
    {
        var fields = new[]
        {
            (TextBox: GoldLimitTextBox, Text: SettingsVm.GoldLimitText, Label: "Minimum gold balance"),
            (TextBox: DailyGoldSpendingLimitTextBox, Text: SettingsVm.DailyGoldSpendingLimitText, Label: "Daily gold spending limit"),
            (TextBox: SilverLimitTextBox, Text: SettingsVm.SilverLimitText, Label: "Minimum silver balance"),
            (TextBox: DailySilverSpendingLimitTextBox, Text: SettingsVm.DailySilverSpendingLimitText, Label: "Daily silver spending limit"),
        };
        var values = new int[fields.Length];
        for (var index = 0; index < fields.Length; index++)
        {
            if (!int.TryParse(fields[index].Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out values[index]))
            {
                AppDialog.Show(
                    this,
                    $"{fields[index].Label} must be a whole number between 0 and {int.MaxValue}.",
                    "Invalid spending limit",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                fields[index].TextBox.Focus();
                fields[index].TextBox.SelectAll();
                goldLimit = dailyGoldSpendingLimit = silverLimit = dailySilverSpendingLimit = 0;
                return false;
            }
        }

        goldLimit = values[0];
        dailyGoldSpendingLimit = values[1];
        silverLimit = values[2];
        dailySilverSpendingLimit = values[3];
        return true;
    }

    private void CaptureTownHallResults()
    {
        TownHallResults = SettingsVm.Celebrations.TownHallRows
            .Select(row => new TownHallOverviewResult(
                row.VillageKey,
                row.VillageName,
                row.IsTownHallEnabled,
                row.Mode))
            .ToList();
        TownHallSettingsChanged = !string.Equals(
            _initialTownHallFingerprint,
            BuildTownHallFingerprint(),
            StringComparison.Ordinal);
    }

    private string BuildTownHallFingerprint()
    {
        var villages = string.Join(
            ";",
            SettingsVm.Celebrations.TownHallRows
                .OrderBy(row => row.VillageKey, StringComparer.OrdinalIgnoreCase)
                .Select(row => $"{row.VillageKey}|{row.IsTownHallEnabled}|{row.Mode}"));
        return $"{villages}#{SettingsVm.Celebrations.TownHallQueue.Count}|{SettingsVm.Celebrations.TownHallQueue.ResolvedDelayMinMinutes:0.##}|{SettingsVm.Celebrations.TownHallQueue.ResolvedDelayMaxMinutes:0.##}";
    }

}
