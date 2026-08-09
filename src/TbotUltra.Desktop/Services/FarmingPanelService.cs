using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Models;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>Owns account-scoped Farming panel settings persistence.</summary>
public sealed class FarmingPanelService(IDesktopBotService botService, BotConfigStore configStore)
{
    public Task<bool> ReadAndPersistGoldClubStatusAsync(BotOptions options, Action<string> log, CancellationToken cancellationToken)
        => botService.ReadAndPersistGoldClubStatusAsync(options, log, cancellationToken);

    public Task<IReadOnlyList<FarmListOverview>> ReadOverviewAsync(BotOptions options, Action<string> log, CancellationToken cancellationToken)
        => botService.ReadFarmListsOverviewAsync(options, log, cancellationToken);

    public Task<FarmAddBatchResult> AddFarmsAsync(
        BotOptions options, string farmListName, string troopType, int troopCount, int requestedCount,
        IReadOnlyList<FarmCoordinate> coordinates, bool useDefaultTroops, Action<string> log,
        IProgress<FarmAddProgress>? progress, CancellationToken cancellationToken)
        => botService.AddFarmsFromCoordinatesAsync(options, farmListName, troopType, troopCount, requestedCount,
            coordinates, useDefaultTroops, log, progress, cancellationToken);

    public Task<FarmListCreateBatchResult> CreateListsAsync(
        BotOptions options, FarmListCreateRequest request, Action<string> log,
        IProgress<FarmListCreateProgress>? progress, CancellationToken cancellationToken)
        => botService.CreateFarmListsAsync(options, request, log, progress, cancellationToken);

    public Task<int?> SendOneAsync(BotOptions options, string farmListName, Action<string> log, CancellationToken cancellationToken)
        => botService.SendFarmListNowAsync(options, farmListName, log, cancellationToken);

    public Task<int> SendSelectedAsync(
        BotOptions options, IReadOnlyCollection<string> names, IReadOnlyCollection<string> ids,
        Action<string> log, CancellationToken cancellationToken)
        => botService.SendSelectedFarmListsNowAsync(options, names, ids, log, cancellationToken);

    public Task<int> SendAllAsync(BotOptions options, Action<string> log, CancellationToken cancellationToken)
        => botService.SendAllFarmListsViaStartAllButtonAsync(options, log, cancellationToken);

    public FarmingSettingsSaveResult SaveSettings(FarmingPanelSettings settings)
    {
        var config = configStore.Load();
        var existingDestinationId = config[BotOptionPayloadKeys.ContinuousFarmLossDestinationListId]?.GetValue<string>() ?? string.Empty;
        var priorBaseName = config[BotOptionPayloadKeys.ContinuousFarmLossDestinationBaseName]?.GetValue<string>();
        var destination = settings.SelectedDestination;
        var moveEnabled = settings.DeactivateLosses && settings.MoveLosses && destination is not null;
        var destinationChangedByUser = destination is not null
            && !string.Equals(existingDestinationId, destination.ListId, StringComparison.OrdinalIgnoreCase);

        config[BotOptionPayloadKeys.ContinuousFarmSendMode] = settings.SendAllLists
            ? FarmingDefaults.SendModeAllAtOnce
            : FarmingDefaults.SendModeListPerList;
        config[BotOptionPayloadKeys.ContinuousFarmDispatchDelayMinMinutes] = settings.DispatchDelayMinMinutes;
        config[BotOptionPayloadKeys.ContinuousFarmDispatchDelayMaxMinutes] = settings.DispatchDelayMaxMinutes;
        config[BotOptionPayloadKeys.ContinuousFarmDeactivateLosses] = settings.DeactivateLosses;
        config[BotOptionPayloadKeys.ContinuousFarmDeactivateOasisLosses] = settings.DeactivateOasisLosses;
        config[BotOptionPayloadKeys.ContinuousFarmMoveLosses] = moveEnabled;
        config[BotOptionPayloadKeys.ContinuousFarmLossDestinationListId] = destination?.ListId ?? string.Empty;
        config[BotOptionPayloadKeys.ContinuousFarmLossDestinationListName] = destination?.Name ?? string.Empty;
        config[BotOptionPayloadKeys.ContinuousFarmLossDestinationBaseName] = destinationChangedByUser || string.IsNullOrWhiteSpace(priorBaseName)
            ? destination?.Name ?? string.Empty
            : priorBaseName;
        configStore.Save(config);

        return new FarmingSettingsSaveResult(
            settings.SendAllLists ? FarmingDefaults.SendModeAllAtOnce : FarmingDefaults.SendModeListPerList,
            settings.DispatchDelayMinMinutes,
            settings.DispatchDelayMaxMinutes,
            moveEnabled,
            destination?.Name);
    }

    public void SaveDestinationBaseName(string name)
    {
        var config = configStore.Load();
        config[BotOptionPayloadKeys.ContinuousFarmLossDestinationBaseName] = name;
        configStore.Save(config);
    }
}

public sealed record FarmingPanelSettings(
    bool SendAllLists,
    int DispatchDelayMinMinutes,
    int DispatchDelayMaxMinutes,
    bool DeactivateLosses,
    bool DeactivateOasisLosses,
    bool MoveLosses,
    FarmLossDestinationOption? SelectedDestination);

public sealed record FarmingSettingsSaveResult(
    string SendMode,
    int DelayMinMinutes,
    int DelayMaxMinutes,
    bool MoveLossesEnabled,
    string? DestinationName);
