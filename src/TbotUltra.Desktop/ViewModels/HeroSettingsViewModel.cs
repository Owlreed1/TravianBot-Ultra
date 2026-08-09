using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Common;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>Owns Hero and Smithy restart-delay presentation settings.</summary>
public sealed class HeroSettingsViewModel : BaseViewModel
{
    private int _hpRegenPerDayPercent = 40;
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
