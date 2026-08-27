using TbotUltra.Core.Configuration;
using TbotUltra.Core.Accounts;
using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Configuration;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Infrastructure;
using TbotUltra.Worker.Services.Automation;
using Microsoft.Playwright;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TbotUltra.Worker.Services;

public sealed partial class BotTaskRunner
{
    public async Task<CapitalProfileCheckResult> CheckCapitalFromProfileAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        using var priorityRequest = _priorityBrowserWork.EnterPriorityRequest();
        CapitalProfileCheckResult? result = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                result = await new CapitalProfileOperation(client).CheckAsync(cancellationToken);
            });

        return result ?? throw new InvalidOperationException("The player profile returned no capital village.");
    }

    public async Task SetVerifiedCapitalStateAsync(
        BotOptions options,
        CapitalProfileCheckResult capital,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        using var priorityRequest = _priorityBrowserWork.EnterPriorityRequest();
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            client => new CapitalProfileOperation(client).SetVerifiedStateAsync(capital, cancellationToken));
    }

    private static FarmListLossHandlingRequest CreateFarmListLossHandlingRequest(
        BotOptions options,
        int? maxTargets = null,
        bool yellowLossesOnly = false)
    {
        var moveEnabled = options.ContinuousFarmDeactivateLosses
            && options.ContinuousFarmMoveLosses
            && !string.IsNullOrWhiteSpace(options.ContinuousFarmLossDestinationListName);
        var villageIdMatch = Regex.Match(
            options.TargetVillageUrl ?? string.Empty,
            @"[?&]newdid=(\d+)",
            RegexOptions.IgnoreCase);
        var createTemplate = string.IsNullOrWhiteSpace(options.TargetVillageName)
            ? null
            : new FarmListCreateRequest(
                [options.ContinuousFarmLossDestinationListName],
                options.TargetVillageName,
                villageIdMatch.Success ? villageIdMatch.Groups[1].Value : null,
                "First available troop",
                1,
                TroopIndexOverride: 1,
                OnlyCreateReportsWithLosses: options.FarmListOnlyCreateReportsWithLosses);

        return new FarmListLossHandlingRequest(
            options.ContinuousFarmDeactivateOasisLosses,
            moveEnabled,
            options.ContinuousFarmLossDestinationListId,
            options.ContinuousFarmLossDestinationListName,
            string.IsNullOrWhiteSpace(options.ContinuousFarmLossDestinationBaseName)
                ? options.ContinuousFarmLossDestinationListName
                : options.ContinuousFarmLossDestinationBaseName,
            createTemplate,
            maxTargets,
            yellowLossesOnly);
    }

    // Manual farming actions still use this path. The queued continuous-farming
    // task owns the same decision through ContinuousFarmingOperation.
    private static async Task RunFarmListLossDeactivationIfEnabledAsync(TaskExecutionContext context)
    {
        if (!context.Options.ContinuousFarmDeactivateLosses)
        {
            context.Log("Continuous farming loss deactivation disabled.");
            return;
        }

        var result = await new ManualFarmingOperation(context.Client).HandleLossTargetsAsync(
            CreateFarmListLossHandlingRequest(context.Options),
            context.CancellationToken);
        context.Log($"Continuous farming loss handling result: found={result.RowsFound}, deactivated={result.RowsDeactivated}, moved={result.RowsMoved}, moveFailures={result.MoveFailures}, skippedOasis={result.SkippedOasisRows}.");
        context.Runner.PublishFarmLossDestinationChange(context.Options, result);
    }

    private void PublishFarmLossDestinationChange(BotOptions options, FarmListLossDeactivationResult result)
    {
        if (!result.DestinationChanged
            || string.IsNullOrWhiteSpace(result.DestinationListId)
            || string.IsNullOrWhiteSpace(result.DestinationListName))
        {
            return;
        }

        RaiseFarmLossDestinationChanged(new FarmLossDestinationChange(
            _accountProvider.LoadAccount().Name,
            result.DestinationListId,
            result.DestinationListName,
            string.IsNullOrWhiteSpace(options.ContinuousFarmLossDestinationBaseName)
                ? options.ContinuousFarmLossDestinationListName
                : options.ContinuousFarmLossDestinationBaseName,
            options.TargetVillageName));
    }

    public async Task<FarmListLossDeactivationResult> RunFarmLossMoveDebugAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        if (!options.ContinuousFarmDeactivateLosses
            || !options.ContinuousFarmMoveLosses
            || string.IsNullOrWhiteSpace(options.ContinuousFarmLossDestinationListName))
        {
            throw new InvalidOperationException(
                "Enable 'Deactivate red/yellow attacks' and 'Move red/yellow farms to list', then select a destination list first.");
        }

        FarmListLossDeactivationResult? result = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                await TrySwitchToTargetVillageAsync(client, options, log, cancellationToken);
                var request = CreateFarmListLossHandlingRequest(options)
                    with { IncludeUnoccupiedOasis = false };
                log($"[farm-list:debug] running red/yellow farm move/deactivate to '{request.DestinationListName}'.");
                result = await new ManualFarmingOperation(client).HandleLossTargetsAsync(request, cancellationToken);
            });

        var completed = result ?? throw new InvalidOperationException("The red/yellow farm move returned no result.");
        PublishFarmLossDestinationChange(options, completed);
        return completed;
    }

    public async Task<IReadOnlyList<FarmListOverview>> ReadFarmListsOverviewAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FarmListOverview> overview = [];
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                overview = await new ManualFarmingOperation(client).ReadOverviewAsync(cancellationToken);
            });

        return overview;
    }

    public async Task<int?> SendFarmListNowAsync(
        BotOptions options,
        string farmListName,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        int? remainingSeconds = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                remainingSeconds = await new ManualFarmingOperation(client).SendOneAsync(farmListName, cancellationToken);
            });

        return remainingSeconds;
    }

    public async Task<int> SendAllFarmListsNowAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        var listCount = 0;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                await RunFarmListLossDeactivationIfEnabledAsync(new TaskExecutionContext(this, options, client, log, cancellationToken, _ => { }));
                listCount = await new ManualFarmingOperation(client).SendAllAsync(cancellationToken);
            });

        return listCount;
    }

    public async Task<int> SendSelectedFarmListsNowAsync(
        BotOptions options,
        IReadOnlyCollection<string> selectedNames,
        IReadOnlyCollection<string> selectedIds,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        var sent = 0;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                await RunFarmListLossDeactivationIfEnabledAsync(new TaskExecutionContext(this, options, client, log, cancellationToken, _ => { }));
                sent = await new ManualFarmingOperation(client).SendSelectedAsync(selectedNames, selectedIds, cancellationToken);
            });

        return sent;
    }

    public async Task<int> SendAllFarmListsViaStartAllButtonAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        var listCount = 0;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                await RunFarmListLossDeactivationIfEnabledAsync(new TaskExecutionContext(this, options, client, log, cancellationToken, _ => { }));
                listCount = await new ManualFarmingOperation(client).SendAllViaStartAllButtonAsync(cancellationToken);
            });

        return listCount;
    }

    public async Task<FarmAddBatchResult> AddFarmsFromCoordinatesAsync(
        BotOptions options,
        string farmListName,
        string troopType,
        int troopCount,
        int requestedCount,
        IReadOnlyList<FarmCoordinate> coordinates,
        bool useDefaultTroops,
        Action<string> log,
        string? accountName = null,
        IProgress<FarmAddProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        FarmAddBatchResult? result = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                result = await new ManualFarmingOperation(client).AddFarmsAsync(
                    farmListName,
                    troopType,
                    troopCount,
                    requestedCount,
                    coordinates,
                    useDefaultTroops,
                    progress,
                    cancellationToken);
            });

        return result ?? throw new InvalidOperationException("Could not add farms from Travco list.");
    }

    public async Task<FarmListCreateBatchResult> CreateFarmListsAsync(
        BotOptions options,
        FarmListCreateRequest request,
        Action<string> log,
        string? accountName = null,
        IProgress<FarmListCreateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var priorityRequest = _priorityBrowserWork.EnterPriorityRequest();
        FarmListCreateBatchResult? result = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                await client.LoginAsync(cancellationToken);
                result = await new ManualFarmingOperation(client).CreateListsAsync(request, progress, cancellationToken);
            });

        return result ?? throw new InvalidOperationException("Could not create farm lists.");
    }

}
