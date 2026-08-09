using System.Globalization;
using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Common;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>Owns editable Farming presentation settings.</summary>
public sealed class FarmingSettingsViewModel : BaseViewModel
{
    private bool _showFarmListLastSentTimer = FarmingDefaults.ShowLastSentTimer;
    private bool _farmListLastSentLimitEnabled = FarmingDefaults.LastSentLimitEnabled;
    private string _farmListLastSentLimitHours = FarmingDefaults.DefaultLastSentLimitHours.ToString(CultureInfo.InvariantCulture);

    public bool ShowFarmListLastSentTimer { get => _showFarmListLastSentTimer; set => SetProperty(ref _showFarmListLastSentTimer, value); }
    public bool FarmListLastSentLimitEnabled { get => _farmListLastSentLimitEnabled; set => SetProperty(ref _farmListLastSentLimitEnabled, value); }
    public string FarmListLastSentLimitHours { get => _farmListLastSentLimitHours; set => SetProperty(ref _farmListLastSentLimitHours, value); }
}
