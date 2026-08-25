using Microsoft.Playwright;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

public sealed partial class TravianClient
{
    public async Task<string> RunHeroCropAntiStarveAsync(
        int triggerMinutes,
        int targetMinutes,
        int maxCropPerTransfer,
        int minHeroCropRemaining,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        triggerMinutes = Math.Clamp(triggerMinutes, 1, 1440);
        targetMinutes = Math.Max(triggerMinutes + 1, Math.Clamp(targetMinutes, 1, 1440));
        maxCropPerTransfer = Math.Max(1, maxCropPerTransfer);
        minHeroCropRemaining = Math.Max(0, minHeroCropRemaining);
        Notify($"[anti-starve] checking village='{_config.TargetVillageName ?? "-"}' trigger={triggerMinutes}m target={targetMinutes}m max={maxCropPerTransfer} reserve={minHeroCropRemaining}");

        var beforeReadAt = DateTimeOffset.UtcNow;
        var before = await ReadVillageResourceStatusAsync(cancellationToken);
        var cropForecast = before.ResourceStorageForecasts?
            .FirstOrDefault(forecast => string.Equals(forecast.ResourceKey, "crop", StringComparison.OrdinalIgnoreCase));
        var currentCrop = ReadCrop(before);
        if (currentCrop is null || before.GranaryCapacity is not > 0 || cropForecast?.ProductionPerHour is null)
        {
            var wait = HeroCropAntiStarveCalculator.ResolvePassiveCheckSeconds(triggerMinutes);
            return $"Anti-starve blocked: crop, production or granary capacity is unavailable. anti_starve_alarm=true queue_wait_seconds={wait}";
        }

        var production = cropForecast.ProductionPerHour.Value;
        var initial = HeroCropAntiStarveCalculator.Calculate(
            currentCrop.Value,
            before.GranaryCapacity.Value,
            production,
            int.MaxValue,
            triggerMinutes,
            targetMinutes,
            maxCropPerTransfer,
            minHeroCropRemaining: 0);
        if (!initial.IsRequired)
        {
            return $"Anti-starve not required after live confirmation: {initial.Reason} eta_seconds={initial.SecondsToEmpty?.ToString() ?? "unknown"}";
        }

        var inventory = await ReadHeroInventoryResourcesAsync(cancellationToken);
        var projectedCrop = ProjectCurrentCrop(currentCrop.Value, production, beforeReadAt, DateTimeOffset.UtcNow);
        var decision = HeroCropAntiStarveCalculator.Calculate(
            projectedCrop,
            before.GranaryCapacity.Value,
            production,
            inventory.Crop,
            triggerMinutes,
            targetMinutes,
            maxCropPerTransfer,
            minHeroCropRemaining);
        if (decision.TransferAmount <= 0)
        {
            var wait = ResolveBlockedRetrySeconds(decision.SecondsToEmpty);
            return $"Anti-starve blocked: {decision.Reason} hero_crop={inventory.Crop} reserve={minHeroCropRemaining} anti_starve_alarm=true queue_wait_seconds={wait}";
        }

        const string CropInventoryItemSelector =
            ".heroItems .heroItem:has(.item.item148), " +
            ".inventory .heroItem:has(.item.item148), " +
            ".heroItem[data-slot='inventory']:has(.item.item148)";
        try
        {
            var opened = await TryClickFirstVisibleEnabledAsync(
                CropInventoryItemSelector,
                cancellationToken,
                reason: "open anti-starve crop transfer",
                timeoutMs: 5000);
            if (!opened)
            {
                return $"Anti-starve blocked: hero crop item could not be opened. anti_starve_alarm=true queue_wait_seconds={ResolveBlockedRetrySeconds(decision.SecondsToEmpty)}";
            }
        }
        catch (PlaywrightException ex)
        {
            return $"Anti-starve blocked: hero crop item could not be opened ({ex.Message}). anti_starve_alarm=true queue_wait_seconds={ResolveBlockedRetrySeconds(decision.SecondsToEmpty)}";
        }

        try
        {
            await _page.WaitForSelectorAsync(
                "div.resourceTransferDialog, #dialogContent",
                new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        }
        catch (PlaywrightException ex)
        {
            return $"Anti-starve blocked: transfer dialog did not load ({ex.Message}). anti_starve_alarm=true queue_wait_seconds={ResolveBlockedRetrySeconds(decision.SecondsToEmpty)}";
        }

        var dialogInventory = await ReadHeroInventoryFromTransferDialogAsync(cancellationToken) ?? inventory;
        projectedCrop = ProjectCurrentCrop(currentCrop.Value, production, beforeReadAt, DateTimeOffset.UtcNow);
        decision = HeroCropAntiStarveCalculator.Calculate(
            projectedCrop,
            before.GranaryCapacity.Value,
            production,
            dialogInventory.Crop,
            triggerMinutes,
            targetMinutes,
            maxCropPerTransfer,
            minHeroCropRemaining);
        if (decision.TransferAmount <= 0)
        {
            await TryDismissResourceTransferDialogAsync(cancellationToken);
            var wait = ResolveBlockedRetrySeconds(decision.SecondsToEmpty);
            return $"Anti-starve blocked: {decision.Reason} live_hero_crop={dialogInventory.Crop} reserve={minHeroCropRemaining} anti_starve_alarm=true queue_wait_seconds={wait}";
        }

        var requested = new HeroInventoryResources(Crop: decision.TransferAmount);
        var filled = await TryFillHeroResourceTransferDialogAsync(requested, dialogInventory, cancellationToken);
        var verified = await ReadHeroResourceTransferDialogAmountsAsync();
        if (filled is null || verified is null || !HeroTransferAmountsMatch(verified, requested))
        {
            await TryDismissResourceTransferDialogAsync(cancellationToken);
            return $"Anti-starve blocked: crop transfer amount could not be verified. anti_starve_alarm=true queue_wait_seconds={ResolveBlockedRetrySeconds(decision.SecondsToEmpty)}";
        }

        var confirmed = await TryConfirmHeroResourceTransferDialogAsync(
            cancellationToken,
            "confirm anti-starve crop transfer",
            requireExactTransfer: true);
        if (!confirmed)
        {
            await TryDismissResourceTransferDialogAsync(cancellationToken);
            return $"Anti-starve blocked: the Transfer button was unavailable. anti_starve_alarm=true queue_wait_seconds={ResolveBlockedRetrySeconds(decision.SecondsToEmpty)}";
        }

        try
        {
            await _page.WaitForFunctionAsync(
                "() => !document.querySelector('div.resourceTransferDialog') && ((document.querySelector('#dialogContent h3')?.textContent || '').trim().toLowerCase() !== 'transfer resources')",
                null,
                new PageWaitForFunctionOptions { Timeout = 5000 });
        }
        catch (PlaywrightException)
        {
            await TryDismissResourceTransferDialogAsync(cancellationToken);
        }

        DeductFromHeroInventoryCache(requested);
        var after = await ReadVillageResourceStatusAsync(cancellationToken);
        var afterCrop = ReadCrop(after);
        var afterForecast = after.ResourceStorageForecasts?
            .FirstOrDefault(forecast => string.Equals(forecast.ResourceKey, "crop", StringComparison.OrdinalIgnoreCase));
        var tolerance = Math.Max(5L, (long)Math.Ceiling(Math.Abs(production) / 60d));
        var expectedMinimum = Math.Max(0L, projectedCrop + decision.TransferAmount - tolerance);
        var verifiedIncrease = afterCrop is not null && afterCrop.Value >= expectedMinimum;
        var postEta = afterForecast?.SecondsToEmpty;
        var partial = decision.IsPartial
            || HeroCropAntiStarveCalculator.IsPostTransferEtaShortfallActionable(postEta, targetMinutes);
        var alarm = !verifiedIncrease || partial ? " anti_starve_alarm=true" : string.Empty;
        return $"Anti-starve transferred crop={decision.TransferAmount} hero_before={dialogInventory.Crop} hero_after_min={dialogInventory.Crop - decision.TransferAmount} post_crop={afterCrop?.ToString() ?? "unknown"} post_eta_seconds={postEta?.ToString() ?? "unknown"} partial={partial.ToString().ToLowerInvariant()} verified={verifiedIncrease.ToString().ToLowerInvariant()}{alarm}";
    }

    private static long? ReadCrop(VillageStatus status)
    {
        status.Resources.TryGetValue("crop", out var raw);
        return TravianParsing.TryParseResourceValue(raw);
    }

    private static long ProjectCurrentCrop(long currentCrop, double productionPerHour, DateTimeOffset readAt, DateTimeOffset now)
        => Math.Max(0L, (long)Math.Floor(currentCrop + productionPerHour * Math.Max(0, (now - readAt).TotalHours)));

    private static int ResolveBlockedRetrySeconds(int? secondsToEmpty)
    {
        if (secondsToEmpty is not int eta)
        {
            return 5 * 60;
        }

        return Math.Clamp(eta / 4, 30, 5 * 60);
    }
}
