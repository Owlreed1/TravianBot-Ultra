using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private IReadOnlyList<HeroCropAntiStarveVillageRow> BuildHeroCropAntiStarveVillageRows()
    {
        var account = _accountStore.ActiveAccountName();
        var serverUrl = LoadBotOptions().BaseUrl;
        return GetAllVillageKeyInfos()
            .Select(village => new HeroCropAntiStarveVillageRow(
                village.Key,
                village.Name,
                HeroCropAntiStarveSettingsStore.IsEnabled(
                    _projectRoot,
                    account,
                    serverUrl,
                    village.Key,
                    defaultIfMissing: true)))
            .ToList();
    }

    private void PersistHeroCropAntiStarveVillages(IEnumerable<HeroCropAntiStarveVillageRow> rows)
    {
        var options = LoadBotOptions();
        HeroCropAntiStarveSettingsStore.Save(
            _projectRoot,
            _accountStore.ActiveAccountName(),
            options.BaseUrl,
            rows.Select(row => (row.VillageKey, row.IsEnabled)));

        RemoveDisabledHeroCropAntiStarveTasks(options);
        if (options.HeroCropAntiStarveEnabled && IsContinuousLoopRunning())
        {
        RequestContinuousAutomationWake();
        }
    }

    private void RemoveDisabledHeroCropAntiStarveTasks(BotOptions options)
    {
        var account = _accountStore.ActiveAccountName();
        foreach (var item in _botService.GetQueueItemsForDisplay().Where(item =>
                     string.Equals(item.TaskName, "anti_starve_hero_crop", StringComparison.OrdinalIgnoreCase)))
        {
            var villageKey = GetQueueItemVillageKey(item);
            var enabled = options.HeroCropAntiStarveEnabled
                && HeroCropAntiStarveSettingsStore.IsEnabled(
                    _projectRoot,
                    account,
                    options.BaseUrl,
                    villageKey,
                    defaultIfMissing: true);
            if (!enabled)
            {
                _botService.RemoveQueueItem(item.Id);
            }
        }
    }
}
