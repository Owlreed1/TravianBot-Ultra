using Microsoft.Playwright;
using System.Text.Json;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

internal sealed record HeroAttributePageSnapshot(
    bool Ok = false,
    string? Error = null,
    int AttributeCount = 0,
    bool LevelUpAvailable = false,
    int FreePoints = 0,
    int FightingStrength = 0,
    int OffenceBonus = 0,
    int DefenceBonus = 0,
    int Resources = 0,
    string HeroState = "Unknown",
    int? ReviveRemainingSeconds = null)
{
    public HeroAttributeSnapshot ToSnapshot()
    {
        if (!Ok || AttributeCount != 4)
        {
            throw new InvalidOperationException(
                $"Hero attributes page returned an incomplete snapshot ({AttributeCount}/4 attributes): {Error ?? "unknown read error"}");
        }

        return new HeroAttributeSnapshot(
            LevelUpAvailable,
            FreePoints,
            FightingStrength,
            OffenceBonus,
            DefenceBonus,
            Resources,
            HeroState: HeroState,
            ReviveRemainingSeconds: ReviveRemainingSeconds);
    }
}

// Hero attributes and inventory snapshot workflow.
public sealed partial class TravianClient
{
    private async Task<bool> HasHeroLevelUpIndicatorAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await _page.EvaluateAsync<bool>(
                """
                // SS: .bigSpeechBubble.levelUp. Official (T4.6): an i.levelUp.show icon shown on
                // almost every page when the hero has unspent attribute points.
                () => !!document.querySelector('.bigSpeechBubble.levelUp, i.levelUp.show, .levelUp.show')
                """);
        }
        catch (PlaywrightException ex) when (IsTransientExecutionContextError(ex))
        {
            return false;
        }
    }

    // Official Travian (T4.6): the attributes page (/hero/attributes) is its own form with
    // inputs name="power"/"offBonus"/"defBonus"/"productionPoints", a .pointsAvailable counter,
    // per-attribute button.plus / button.minus, and a #savePoints submit (enabled once points are
    // assigned). Allocate by clicking the prioritised attribute's + button, then save.
    private async Task<int> AllocateHeroPointsOfficialAsync(string priority, CancellationToken cancellationToken)
    {
        Notify("[hero:verbose] official hero point allocation entered");
        await GotoAsync(Paths.HeroAttributes, cancellationToken);
        await WaitForPageReadyAsync(cancellationToken); // Wait for page to load
        await EnsureLoggedInAsync(cancellationToken: cancellationToken);

        try
        {
            await _page.WaitForFunctionAsync(
                """() => !!document.querySelector('input[name="power"], .pointsAvailable')""",
                null,
                new PageWaitForFunctionOptions { Timeout = 6000 });
        }
        catch (TimeoutException)
        {
        }

        var fieldOrder = HeroCalc.MapHeroStatPriorityToOfficialFields(priority).ToList();
        Notify($"[hero] official allocation priority='{priority}', fieldOrder={string.Join(",", fieldOrder)}");

        var before = await ReadPointsAvailableAsync();
        if (before <= 0)
        {
            Notify("[hero] official allocation: no points available.");
            return 0;
        }

        var remaining = before;
        var used = 0;
        var exhaustedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedFields = new List<string>();
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var activeOrder = fieldOrder.Where(field => !exhaustedFields.Contains(field)).ToList();
            if (activeOrder.Count == 0)
            {
                break;
            }

            var fieldClicked = await ClickOfficialHeroAttributePlusAsync(activeOrder, cancellationToken);
            if (string.IsNullOrWhiteSpace(fieldClicked))
            {
                break;
            }

            await Task.Delay(500, cancellationToken);
            var latest = await ReadPointsAvailableAsync();
            if (latest >= remaining)
            {
                await Task.Delay(500, cancellationToken);
                latest = await ReadPointsAvailableAsync();
            }

            if (latest >= remaining)
            {
                exhaustedFields.Add(fieldClicked);
                Notify($"[hero] official allocation: field={fieldClicked} did not consume a point, trying next priority.");
                continue;
            }

            used += remaining - latest;
            remaining = latest;
            usedFields.Add(fieldClicked);
            Notify($"[hero] official allocation: clicked {fieldClicked}, points remaining={remaining}");
        }

        var after = await ReadPointsAvailableAsync();
        used = Math.Max(0, before - after);
        Notify($"[hero] official allocation: fields={string.Join(",", usedFields.Distinct(StringComparer.OrdinalIgnoreCase))}, points before={before}, after={after}, used={used}");
        if (used <= 0)
        {
            return 0;
        }

        // #savePoints stays disabled until the changes register; wait for it to enable before clicking.
        try
        {
            await _page.WaitForFunctionAsync(
                """() => { const b = document.querySelector('#savePoints'); return !!b && !b.disabled; }""",
                null,
                new PageWaitForFunctionOptions { Timeout = 4000 });
        }
        catch (TimeoutException)
        {
        }

        await DelayBeforeClickAsync(cancellationToken); // Action pacing "Click" delay
        var saved = await _page.EvaluateAsync<bool>(
            """
            () => {
              const save = document.querySelector('#savePoints, button#savePoints');
              if (save && !save.disabled) { save.click(); return true; }
              return false;
            }
            """);
        if (saved)
        {
            InvalidateCachedHeroAttributeSnapshot();
            await WaitForPageReadyAsync(cancellationToken); // Wait for page to load
        }

        Notify($"[hero] official point allocation: assigned {used}, saved={saved}");
        return used;
    }

    private async Task<int> ReadPointsAvailableAsync()
    {
        return await _page.EvaluateAsync<int>(
            """() => parseInt((document.querySelector('.pointsAvailable')?.textContent || '0').replace(/[^\d]/g, ''), 10) || 0""");
    }

    private async Task<string?> ClickOfficialHeroAttributePlusAsync(IReadOnlyList<string> fieldOrder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DelayBeforeClickAsync(cancellationToken); // Action pacing "Click" delay
        return await _page.EvaluateAsync<string?>(
            """
            (order) => {
              const robustClick = (el) => {
                el.scrollIntoView && el.scrollIntoView({ block: 'center' });
                const o = { bubbles: true, cancelable: true, view: window };
                try { el.dispatchEvent(new PointerEvent('pointerdown', o)); } catch (e) {}
                el.dispatchEvent(new MouseEvent('mousedown', o));
                try { el.dispatchEvent(new PointerEvent('pointerup', o)); } catch (e) {}
                el.dispatchEvent(new MouseEvent('mouseup', o));
                el.click();
              };
              const plusFor = (name) => {
                const input = document.querySelector(`input[name="${name}"]`);
                if (!input) return null;

                const selector = 'button.plus, button.textButtonV2.plus';
                const attributeInputSelector = 'input[name="power"], input[name="offBonus"], input[name="defBonus"], input[name="productionPoints"]';
                const inputRatio = input.closest('.inputRatio, .pointsRatio');
                if (inputRatio) {
                  let sibling = inputRatio.previousElementSibling;
                  while (sibling) {
                    const btn = sibling.querySelector(selector);
                    if (btn) return btn;
                    if (sibling.matches?.('.name')) break;
                    sibling = sibling.previousElementSibling;
                  }
                }

                const attributeInputs = Array.from(document.querySelectorAll(attributeInputSelector));
                const previousInput = attributeInputs
                  .filter(other => other !== input && (input.compareDocumentPosition(other) & Node.DOCUMENT_POSITION_PRECEDING))
                  .at(-1);
                return Array.from(document.querySelectorAll(selector))
                  .filter(btn =>
                    (btn.compareDocumentPosition(input) & Node.DOCUMENT_POSITION_FOLLOWING)
                    && (!previousInput || (previousInput.compareDocumentPosition(btn) & Node.DOCUMENT_POSITION_FOLLOWING)))
                  .at(-1)
                  || null;
              };

              const isDisabled = (btn) => {
                const ariaDisabled = (btn?.getAttribute?.('aria-disabled') || '').toLowerCase() === 'true';
                const classDisabled = btn?.classList?.contains('disabled') === true;
                return !btn || btn.disabled || ariaDisabled || classDisabled;
              };

              for (const name of order) {
                const btn = plusFor(name);
                if (isDisabled(btn)) {
                  continue;
                }

                robustClick(btn);
                return name;
              }
              return null;
            }
            """,
            fieldOrder);
    }

    private async Task<int> TryAllocateHeroPointsAsync(string priority, CancellationToken cancellationToken)
    {
        return await AllocateHeroPointsOfficialAsync(priority, cancellationToken);
    }

    private async Task<bool> WaitForAttributesTableAsync(CancellationToken cancellationToken, int timeoutMs)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Travian renders #availablePoints ONLY when free points exist. With 0 free points
            // the span is absent — waiting on it would always burn the full timeout. Accept any
            // of: populated #availablePoints, a row's td.points text, or an attribute input.
            await _page.WaitForFunctionAsync(
                """
                () => {
                  if (document.querySelector('.heroAttributes input[name="power"], input[name="productionPoints"], .heroAttributes .pointsAvailable')) return true;
                  const table = document.querySelector('#attributesOfHero');
                  if (!table) return false;
                  const ap = document.querySelector('#availablePoints');
                  if (ap && (ap.textContent || '').trim().length > 0) return true;
                  if (table.querySelector('td.points')) return true;
                  if (table.querySelector('input[name^="attribute"]')) return true;
                  return false;
                }
                """,
                null,
                new PageWaitForFunctionOptions { Timeout = timeoutMs });
            return true;
        }
        catch (PlaywrightException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    // Reads the four resource consumables the hero is carrying (wood=item145, clay=item146,
    // iron=item147, crop=item148) from the hero inventory grid. Official-only data, but the read
    // itself is harmless on any server: a missing item simply reads as 0. Navigates to the hero
    // inventory page so it can be called on demand from the desktop.
    public async Task<HeroInventoryResources> ReadHeroInventoryResourcesAsync(
        CancellationToken cancellationToken = default,
        bool suppressUiSync = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Notify("[hero-inventory] reading resources from inventory");

        // EnsureLoggedInAsync emits a UI-sync that reads the village list (navigating to the
        // profile on the first read of a session). When called as the very first post-login step
        // this would navigate to the profile BEFORE the inventory is read. Suppress it so the
        // inventory really is read first; the village/profile read still happens right after.
        if (suppressUiSync)
        {
            _suppressEnsureUiSyncDepth++;
        }

        try
        {
            await EnsureLoggedInAsync();
            await GotoAsync(Paths.HeroInventory, cancellationToken);
            await WaitForPageReadyAsync(cancellationToken); // Wait for page to load
            await EnsureLoggedInAsync();

        // Give the React-rendered inventory grid a moment to appear; a timeout just falls through
        // to a best-effort read (missing items read as 0).
        try
        {
            await _page.WaitForSelectorAsync(
                ".heroItems .heroItem .item",
                new PageWaitForSelectorOptions { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
        }
        catch (PlaywrightException)
        {
        }

        if (!await _page.EvaluateAsync<bool>("() => !!document.querySelector('.heroItems')"))
        {
            throw new InvalidOperationException(
                "Hero inventory page loaded without the inventory grid; keeping the previous snapshot.");
        }

        string rawJson;
        try
        {
            rawJson = await _page.EvaluateAsync<string>(
                """
                () => {
                  const readCount = (itemClass) => {
                    const item = document.querySelector('.heroItems .item.' + itemClass);
                    if (!item) return 0;
                    const slot = item.closest('.heroItem');
                    const countEl = slot ? slot.querySelector('.count') : null;
                    const text = (countEl?.textContent || '').replace(/[^0-9]/g, '');
                    return text ? Number(text) || 0 : 0;
                  };
                  const ointmentItem = document.querySelector('.heroItems .item.item106');
                  const ointmentSlot = ointmentItem?.closest('.heroItem');
                  const ointmentText = (ointmentSlot?.querySelector('.count')?.textContent || '').replace(/[^0-9]/g, '');
                  return JSON.stringify({
                    wood: readCount('item145'),
                    clay: readCount('item146'),
                    iron: readCount('item147'),
                    crop: readCount('item148'),
                    ointmentFound: !!ointmentItem,
                    ointmentCount: ointmentItem ? (ointmentText ? Number(ointmentText) || 0 : 1) : 0
                  });
                }
                """);
        }
        catch (Exception ex)
        {
            Notify($"[hero-inventory] read EvaluateAsync threw: {ex.GetType().Name}: {ex.Message}");
            throw new InvalidOperationException(
                "Hero inventory values could not be read; keeping the previous snapshot.",
                ex);
        }

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            Notify("[hero-inventory] read returned empty result");
            throw new InvalidOperationException(
                "Hero inventory returned no values; keeping the previous snapshot.");
        }

        var pageRead = JsonSerializer.Deserialize<HeroInventoryPageRead>(
                rawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new HeroInventoryPageRead();
        var resources = new HeroInventoryResources(pageRead.Wood, pageRead.Clay, pageRead.Iron, pageRead.Crop);

            Notify($"[hero-inventory] wood={resources.Wood} clay={resources.Clay} iron={resources.Iron} crop={resources.Crop}");
            UpdateHeroInventoryCache(resources, HeroInventoryObservationSource.HeroInventoryPage);
            if (pageRead.OintmentFound && pageRead.OintmentCount > 0)
            {
                ClearPersistedHeroOintmentCooldown();
                Notify($"[hero-ointment] inventory read found {pageRead.OintmentCount}; automatic use is available again.");
            }
            else
            {
                var retryNotBeforeUtc = PersistEmptyHeroOintmentCooldown(DateTimeOffset.UtcNow);
                Notify($"[hero-ointment] inventory contains no ointments; next automatic check after {retryNotBeforeUtc:O}.");
            }
            return resources;
        }
        finally
        {
            if (suppressUiSync)
            {
                _suppressEnsureUiSyncDepth--;
            }
        }
    }

    private sealed record HeroInventoryPageRead(
        int Wood = 0,
        int Clay = 0,
        int Iron = 0,
        int Crop = 0,
        bool OintmentFound = false,
        int OintmentCount = 0);

    private async Task<HeroAttributeSnapshot> ReadHeroInventorySnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The reader uses querySelector + textContent, which work on collapsed elements too —
        // we deliberately do NOT expand the panel here. Expansion is a multi-second click+animation
        // wait and only matters for writes (clicking +/- point buttons), not reads.
        var ready = await WaitForAttributesTableAsync(cancellationToken, timeoutMs: 5000);
        if (!ready)
        {
            var url = _page.Url;
            Notify($"Hero attributes table did not appear in time. url='{url}'.");
            throw new InvalidOperationException(
                "Hero attributes page did not expose its values; keeping the previous snapshot.");
        }

        string rawJson;
        try
        {
            rawJson = await _page.EvaluateAsync<string>(
                """
                () => {
                  try {
                    const readDigit = (el) => {
                      if (!el) return 0;
                      // Strip bidi marks/thousands separators before reading the number.
                      const m = (el.textContent || '').replace(/[^\d-]/g, '');
                      return m ? Number(m) || 0 : 0;
                    };
                    // Reads an attribute's allocated points. Prefer hero V2 inputs and keep the
                    // table fallback so read-only snapshots remain tolerant of older markup.
                    const attrPoints = (modernName, tableRowId) => {
                      const modern = document.querySelector('input[name="' + modernName + '"]');
                      if (modern) return Number(modern.value) || 0;
                      const row = document.getElementById(tableRowId);
                      if (!row) return null;
                      const input = row.querySelector('input[type="text"][name^="attribute"]');
                      if (input) return Number(input.value) || 0;
                      const td = row.querySelector('td.points');
                      return td ? readDigit(td) : null;
                    };
                    // Free points: ".pointsAvailable" on hero V2, "#availablePoints" in table markup.
                    const freePointsEl = document.querySelector('.heroAttributes .pointsAvailable, .pointsAvailable, #availablePoints');
                    const parseTimer = (value) => {
                      const text = (value || '').replace(/\s+/g, ' ').trim();
                      if (!text) return null;
                      const parts = text.split(':').map(v => Number(v));
                      if (parts.some(v => !Number.isFinite(v))) return null;
                      if (parts.length === 3) return parts[0] * 3600 + parts[1] * 60 + parts[2];
                      if (parts.length === 2) return parts[0] * 60 + parts[1];
                      return null;
                    };
                    const statusMessage = document.querySelector('.heroState, .heroStatusMessage');
                    const statusText = (statusMessage?.textContent || '').replace(/\s+/g, ' ').trim().toLowerCase();
                    const revivingIcon = !!document.querySelector('.heroStatus i.heroReviving, i.heroReviving, [class*="heroReviving"]');
                    const reviving = revivingIcon
                      || (/being\s+revived|remaining\s+time|reviv/i.test(statusText)
                        && !!statusMessage?.querySelector('.timer, [counting="down"], .heroStatus101Regenerate'));
                    const deadIcon = !!document.querySelector('.heroStatus i.heroDead, i.heroDead, [class*="heroDead"]');
                    const dead = !reviving && (deadIcon || /hero\s+is\s+dead|\bdead\b|\btot\b|\bdeceased\b/i.test(statusText));
                    const reviveTimerNode = statusMessage?.querySelector('.timer[value], [counting="down"][value], .timer')
                      || document.querySelector('.lineWrapper .inlineIcon.duration .value, .lineWrapper .duration .value');
                    const reviveTimer = reviveTimerNode?.getAttribute?.('value')
                      ? Number(reviveTimerNode.getAttribute('value'))
                      : parseTimer(reviveTimerNode?.textContent || '');
                    const fightingStrength = attrPoints('power', 'attributepower');
                    const offenceBonus = attrPoints('offBonus', 'attributeoffBonus');
                    const defenceBonus = attrPoints('defBonus', 'attributedefBonus');
                    const resources = attrPoints('productionPoints', 'attributeproductionPoints');
                    const attributes = [fightingStrength, offenceBonus, defenceBonus, resources];
                    const attributeCount = attributes.filter(value => value !== null).length;
                    return JSON.stringify({
                      ok: attributeCount === 4,
                      error: attributeCount === 4 ? null : 'one or more hero attributes were missing',
                      attributeCount,
                      levelUpAvailable: !!document.querySelector('.bigSpeechBubble.levelUp'),
                      freePoints: readDigit(freePointsEl),
                      fightingStrength: fightingStrength ?? 0,
                      offenceBonus: offenceBonus ?? 0,
                      defenceBonus: defenceBonus ?? 0,
                      resources: resources ?? 0,
                      heroState: reviving ? 'Reviving' : dead ? 'Dead' : 'Alive',
                      reviveRemainingSeconds: Number.isFinite(reviveTimer) ? Math.max(0, Math.trunc(reviveTimer)) : null
                    });
                  } catch (e) {
                    return JSON.stringify({ ok: false, error: String(e && e.message || e) });
                  }
                }
                """);
        }
        catch (Exception ex)
        {
            Notify($"[hero] inventory snapshot EvaluateAsync threw: {ex.GetType().Name}: {ex.Message}");
            throw new InvalidOperationException(
                "Hero attribute values could not be read; keeping the previous snapshot.",
                ex);
        }
        Notify($"[hero:verbose] inventory snapshot raw JSON: {rawJson}");

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new InvalidOperationException(
                "Hero attributes page returned no values; keeping the previous snapshot.");
        }

        // PropertyNameCaseInsensitive is required: JS emits camelCase ("freePoints"), the record is PascalCase ("FreePoints").
        // Without this, every field silently deserializes to its default (0/false).
        var pageSnapshot = JsonSerializer.Deserialize<HeroAttributePageSnapshot>(
                rawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(
                "Hero attributes page returned an invalid snapshot; keeping the previous snapshot.");
        return pageSnapshot.ToSnapshot();
    }

    private string BuildHeroAttributeSnapshotCacheKey()
    {
        return $"{_account.Name}|{_config.BaseUrl.TrimEnd('/')}";
    }

    private HeroAttributeSnapshot? TryGetCachedHeroAttributeSnapshot()
    {
        var key = BuildHeroAttributeSnapshotCacheKey();
        lock (HeroAttributeSnapshotCacheSync)
        {
            if (CachedHeroAttributeSnapshotsByKey.TryGetValue(key, out var snapshot))
            {
                return snapshot;
            }
        }

        if (_heroAttributeSnapshotStore.TryLoad(_account.Name, _config.BaseUrl, out var storedSnapshot)
            && storedSnapshot is not null)
        {
            lock (HeroAttributeSnapshotCacheSync)
            {
                CachedHeroAttributeSnapshotsByKey[key] = storedSnapshot;
            }

            return storedSnapshot;
        }

        return null;
    }

    private void SaveCachedHeroAttributeSnapshot(HeroAttributeSnapshot snapshot)
    {
        var key = BuildHeroAttributeSnapshotCacheKey();
        lock (HeroAttributeSnapshotCacheSync)
        {
            CachedHeroAttributeSnapshotsByKey[key] = snapshot with { };
        }

        _heroAttributeSnapshotStore.Save(_account.Name, _config.BaseUrl, snapshot);
    }

    private void InvalidateCachedHeroAttributeSnapshot()
    {
        var key = BuildHeroAttributeSnapshotCacheKey();
        lock (HeroAttributeSnapshotCacheSync)
        {
            CachedHeroAttributeSnapshotsByKey.Remove(key);
        }

        _heroAttributeSnapshotStore.Delete(_account.Name, _config.BaseUrl);
        Notify("[hero:verbose] invalidated in-memory and persisted hero attribute snapshot");
    }

}
