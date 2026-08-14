using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Common;
using TbotUltra.Desktop.Models;
using System.Collections.ObjectModel;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>Owns Hero and Smithy restart-delay presentation settings.</summary>
public sealed class HeroSettingsViewModel : BaseViewModel
{
    private int _hpRegenPerDayPercent = 40;
    private bool _cropAntiStarveEnabled = HeroCropAntiStarveDefaults.Enabled;
    private string _cropAntiStarveTriggerMinutes = HeroCropAntiStarveDefaults.TriggerMinutes.ToString();
    private string _cropAntiStarveTargetMinutes = HeroCropAntiStarveDefaults.TargetMinutes.ToString();
    private string _cropAntiStarveMaxCropPerTransfer = HeroCropAntiStarveDefaults.MaxCropPerTransfer.ToString();
    private string _cropAntiStarveMinHeroCropRemaining = HeroCropAntiStarveDefaults.MinHeroCropRemaining.ToString();

    public ObservableCollection<HeroCropAntiStarveVillageRow> CropAntiStarveVillages { get; } = [];
    public bool CropAntiStarveEnabled { get => _cropAntiStarveEnabled; set => SetProperty(ref _cropAntiStarveEnabled, value); }
    public string CropAntiStarveTriggerMinutes { get => _cropAntiStarveTriggerMinutes; set => SetProperty(ref _cropAntiStarveTriggerMinutes, value); }
    public string CropAntiStarveTargetMinutes { get => _cropAntiStarveTargetMinutes; set => SetProperty(ref _cropAntiStarveTargetMinutes, value); }
    public string CropAntiStarveMaxCropPerTransfer { get => _cropAntiStarveMaxCropPerTransfer; set => SetProperty(ref _cropAntiStarveMaxCropPerTransfer, value); }
    public string CropAntiStarveMinHeroCropRemaining { get => _cropAntiStarveMinHeroCropRemaining; set => SetProperty(ref _cropAntiStarveMinHeroCropRemaining, value); }
    public RestartDelaySettings AdventureRestartDelay { get; } = new(
        HeroAdventureRestartDelayDefaults.Enabled,
        HeroAdventureRestartDelayDefaults.MinMinutes,
        HeroAdventureRestartDelayDefaults.MaxMinutes,
        HeroAdventureRestartDelayDefaults.MinMinutes,
        HeroAdventureRestartDelayDefaults.MaxMinutes);

    public IReadOnlyList<int> HpRegenOptions { get; } = [20, 30, 40, 50, 60, 70, 80, 90, 100];
    public int HpRegenPerDayPercent
    {
        get => _hpRegenPerDayPercent;
        set => SetProperty(ref _hpRegenPerDayPercent, Math.Clamp(value, 20, 100));
    }

    public RestartDelaySettings SmithyUpgradeRestartDelay { get; } = new(
        SmithyUpgradeRestartDelayDefaults.Enabled,
        SmithyUpgradeRestartDelayDefaults.MinMinutes,
        SmithyUpgradeRestartDelayDefaults.MaxMinutes,
        SmithyUpgradeRestartDelayDefaults.MinMinutes,
        SmithyUpgradeRestartDelayDefaults.MaxMinutes);
}
