using System.Collections.ObjectModel;
using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>Owns editable Town Hall and Brewery celebration settings.</summary>
public sealed class CelebrationSettingsViewModel
{
    public ObservableCollection<TownHallOverviewRow> TownHallRows { get; } = [];

    public TownHallQueueSettings TownHallQueue { get; } = new(
        TownHallCelebrationDefaults.DefaultRestartDelayEnabled,
        TownHallCelebrationDefaults.DefaultCount,
        TownHallCelebrationDefaults.DefaultRestartDelayMinMinutes,
        TownHallCelebrationDefaults.DefaultRestartDelayMaxMinutes);

    public RestartDelaySettings BreweryRestartDelay { get; } = new(
        BreweryCelebrationDefaults.DefaultRestartDelayEnabled,
        BreweryCelebrationDefaults.DefaultRestartDelayMinMinutes,
        BreweryCelebrationDefaults.DefaultRestartDelayMaxMinutes,
        BreweryCelebrationDefaults.DefaultRestartDelayMinMinutes,
        BreweryCelebrationDefaults.DefaultRestartDelayMaxMinutes);
}
