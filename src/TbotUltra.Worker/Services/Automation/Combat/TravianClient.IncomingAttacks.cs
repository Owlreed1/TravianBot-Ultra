using Microsoft.Playwright;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

public sealed partial class TravianClient
{
    public async Task<IncomingAttackSnapshot> ReadIncomingAttacksAsync(
        string villageName,
        string? villageUrl,
        string? villageKey,
        CancellationToken cancellationToken = default)
    {
        using var trace = _browserTrace.BeginOperation("READ", "incoming-attacks", $"village={villageName} source=rally-point");
        Notify($"[incoming-attacks] reading Rally Point for '{villageName}'.");
        await SwitchToVillageByIdentityAsync(villageName, villageUrl, villageKey, cancellationToken, skipFeatureRefresh: true);
        if (!IsCurrentUrlForPath(Paths.Resources))
        {
            await GotoAsync(Paths.Resources, cancellationToken);
        }

        await EnsureLoggedInAsync(cancellationToken: cancellationToken);
        var activeVillage = await ReadActiveVillageNameAsync(cancellationToken);
        var coords = await TryReadActiveVillageCoordsFromCurrentPageAsync(cancellationToken);
        var resolvedKey = coords.X.HasValue && coords.Y.HasValue ? $"xy:{coords.X.Value}|{coords.Y.Value}" : villageKey;

        await GotoAsync("/build.php?gid=16&tt=1", cancellationToken);
        await EnsureIncomingAttackFilterAsync(cancellationToken);
        var html = await _page.ContentAsync();
        if (!IncomingAttackDomParser.HasOnlyIncomingFilterActive(html))
        {
            throw new InvalidOperationException("Rally Point incoming filter could not be verified; previous attack data was kept.");
        }

        var observedAtUtc = _serverTimeUtc ?? DateTimeOffset.UtcNow;
        var attacks = IncomingAttackDomParser.ParseIncomingAttacks(
            html,
            activeVillage,
            resolvedKey,
            coords.X,
            coords.Y,
            observedAtUtc);
        Notify($"[incoming-attacks] read {attacks.Count} movement(s) for '{activeVillage}'.");
        trace.Complete("success", $"village={activeVillage} count={attacks.Count}");
        return new IncomingAttackSnapshot(activeVillage, resolvedKey, coords.X, coords.Y, observedAtUtc, attacks);
    }

    private async Task<IReadOnlyList<IncomingAttackSignal>?> ReadIncomingAttackSignalsOnCurrentDorf1Async(
        string activeVillage,
        string? activeVillageUrl,
        int? activeCoordX,
        int? activeCoordY,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentUrlForPath(Paths.Resources))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var html = await _page.ContentAsync();
        var observedAtUtc = _serverTimeUtc ?? DateTimeOffset.UtcNow;
        var signals = IncomingAttackDomParser.ParseDorf1Signals(
            html,
            activeVillage,
            activeVillageUrl,
            activeCoordX,
            activeCoordY,
            observedAtUtc);
        if (signals.Count > 0)
        {
            Notify($"[incoming-attacks] Dorf1 signaled {signals.Count} attacked village(s).");
        }

        return signals;
    }

    private async Task EnsureIncomingAttackFilterAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var html = await _page.ContentAsync();
            if (IncomingAttackDomParser.HasOnlyIncomingFilterActive(html))
            {
                return;
            }

            var activeNonIncoming = _page.Locator("button.iconFilter.iconFilterActive:has(img.filterCategory:not(.subFilterCategory1))").First;
            if (await activeNonIncoming.CountAsync() > 0)
            {
                await DelayBeforeClickAsync(cancellationToken, "incoming attacks: disable extra filter");
                await ClickLocatorAsync(activeNonIncoming, "incoming-attacks-disable-extra-filter", cancellationToken);
                await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                continue;
            }

            var incoming = _page.Locator("button.iconFilter:has(img.subFilterCategory1)").First;
            if (await incoming.CountAsync() == 0)
            {
                throw new InvalidOperationException("Rally Point incoming filter control was not found.");
            }

            await DelayBeforeClickAsync(cancellationToken, "incoming attacks: enable incoming filter");
            await ClickLocatorAsync(incoming, "incoming-attacks-enable-filter", cancellationToken);
            await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        }

        throw new InvalidOperationException("Rally Point incoming filter could not be isolated.");
    }
}
