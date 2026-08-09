using TbotUltra.Core.Configuration;

namespace TbotUltra.Desktop.Services;

/// <summary>Owns the Resources panel's account- and village-scoped setting persistence.</summary>
public sealed class ResourcesPanelService(BotConfigStore configStore, VillageSettingsStore villageSettingsStore)
{
    public void SaveBuildStrategy(string strategy)
    {
        var config = configStore.Load();
        config[BotOptionPayloadKeys.ResourceBuildStrategy] = strategy;
        configStore.Save(config);
    }

    public void SaveUpgradeTypes(
        VillageSettingsStore.VillageKeyInfo village,
        IReadOnlyList<string> selectedTypes)
        => villageSettingsStore.SetResourceUpgradeTypes(village, selectedTypes);
}
