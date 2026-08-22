using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

public sealed partial class TravianClient
{
    public async Task<TroopEvasionResult> SendTroopEvasionAsync(
        TroopEvasionRequest request,
        IProgress<TroopEvasionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var attempt = await RunTroopEvasionAsync(request, validateOnly: false, progress, cancellationToken);
        return attempt.Result ?? new TroopEvasionResult(TroopEvasionOutcome.Failed, attempt.Validation.Message);
    }

    public async Task<TroopEvasionValidationResult> ValidateTroopEvasionAsync(
        TroopEvasionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await RunTroopEvasionAsync(request, validateOnly: true, progress: null, cancellationToken);
        return result.Validation;
    }

    private async Task<(TroopEvasionValidationResult Validation, TroopEvasionResult? Result)> RunTroopEvasionAsync(
        TroopEvasionRequest request,
        bool validateOnly,
        IProgress<TroopEvasionProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new TroopEvasionProgress(TroopEvasionProgressState.Preparing, "Opening Rally Point."));
        Notify($"[troop-evasion] preparing '{request.VillageName}' for ({request.TargetX}|{request.TargetY}).");
        await SwitchToVillageByIdentityAsync(request.VillageName, request.VillageUrl, request.VillageKey, cancellationToken, skipFeatureRefresh: true);
        await EnsureRallyPointAndOpenSendTroopsPageAsync(cancellationToken, allowReuseCurrentPage: false);
        await EnsureLoggedInAsync(cancellationToken: cancellationToken);

        var activeVillage = await ReadActiveVillageNameAsync(cancellationToken);
        if (!string.Equals(activeVillage, request.VillageName, StringComparison.OrdinalIgnoreCase))
        {
            return FailedValidation($"Source village verification failed: expected '{request.VillageName}', read '{activeVillage}'.");
        }

        var form = await FillAndVerifyTroopEvasionFormAsync(request, cancellationToken);
        if (!form.IsValid)
        {
            return (form, new TroopEvasionResult(
                form.Message.Contains("No selected troops", StringComparison.OrdinalIgnoreCase)
                    ? TroopEvasionOutcome.NoTroops
                    : TroopEvasionOutcome.Failed,
                form.Message));
        }

        progress?.Report(new TroopEvasionProgress(TroopEvasionProgressState.FormReady, "Evasion form is ready."));
        var firstSend = _page.Locator("button#ok[type='submit'][name='ok']").First;
        if (await firstSend.CountAsync() == 0)
        {
            return FailedValidation("The verified first Send button was not found.");
        }

        await DelayBeforeClickAsync(cancellationToken, "troop evasion: first Send");
        await ClickLocatorAsync(firstSend, "troop-evasion-first-send", cancellationToken);
        if (!await WaitForSendTroopsConfirmationPageAsync(cancellationToken))
        {
            var error = await ReadReinforcementFormErrorAsync(cancellationToken);
            return FailedValidation(error ?? "The troop confirmation page did not load.");
        }

        var oneWay = await ReadTroopEvasionTravelTimeAsync(cancellationToken);
        if (oneWay is null)
        {
            return FailedValidation("One-way travel time could not be read from #in or #at.");
        }

        var now = CurrentTravianServerTimeUtc();
        var safeConfirmAt = request.MovementType == TroopEvasionMovementType.Reinforcement
            ? now
            : request.TriggeringAttackArrivalUtc - (oneWay.Value + oneWay.Value) + request.ReturnSafetyMargin;
        var requiresWaiting = safeConfirmAt > now;
        var validation = form with
        {
            Message = requiresWaiting
                ? $"Valid. Confirmation would wait until {safeConfirmAt:O}."
                : "Valid. The movement can be confirmed now.",
            OneWayTravelTime = oneWay,
            WouldRequireWaiting = requiresWaiting,
        };
        if (validateOnly)
        {
            Notify($"[troop-evasion] validation completed for '{request.VillageName}'; final Confirm was not clicked.");
            return (validation, null);
        }

        if (request.MovementType != TroopEvasionMovementType.Reinforcement && requiresWaiting)
        {
            progress?.Report(new TroopEvasionProgress(
                TroopEvasionProgressState.WaitingForSafeReturn,
                "Waiting so attacking or raiding troops cannot return before the incoming attack.",
                safeConfirmAt));
            var wait = safeConfirmAt - now;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }
        }

        now = CurrentTravianServerTimeUtc();
        if (now >= request.TriggeringAttackArrivalUtc)
        {
            return (validation, new TroopEvasionResult(TroopEvasionOutcome.TooLate, "The incoming attack arrived before final confirmation.", form.AvailableTroops, form.HeroAvailable, oneWay));
        }

        progress?.Report(new TroopEvasionProgress(TroopEvasionProgressState.Confirming, "Confirming troop evasion."));
        var finalConfirm = _page.Locator("button#confirmSendTroops.rallyPointConfirm[name='confirmSendTroops']").First;
        if (await finalConfirm.CountAsync() == 0)
        {
            return (validation, new TroopEvasionResult(TroopEvasionOutcome.Failed, "The verified final Confirm button was not found."));
        }

        await DelayBeforeClickAsync(cancellationToken, "troop evasion: final Confirm");
        await ClickLocatorAsync(finalConfirm, "troop-evasion-final-confirm", cancellationToken);
        await WaitForSendTroopsCompletionAsync(cancellationToken);
        if (await _page.Locator("button#confirmSendTroops.rallyPointConfirm[name='confirmSendTroops']").CountAsync() > 0)
        {
            return (validation, new TroopEvasionResult(
                TroopEvasionOutcome.Failed,
                "Final confirmation could not be verified as completed.",
                form.AvailableTroops,
                form.HeroAvailable,
                oneWay));
        }
        var confirmedAt = CurrentTravianServerTimeUtc();
        Notify($"[troop-evasion] sent troops from '{request.VillageName}'.");
        progress?.Report(new TroopEvasionProgress(TroopEvasionProgressState.Completed, "Troop evasion sent."));
        return (validation, new TroopEvasionResult(
            TroopEvasionOutcome.Succeeded,
            "Troop evasion sent.",
            form.AvailableTroops,
            form.HeroAvailable,
            oneWay,
            confirmedAt));
    }

    private async Task<TroopEvasionValidationResult> FillAndVerifyTroopEvasionFormAsync(
        TroopEvasionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TargetX is < -400 or > 400 || request.TargetY is < -400 or > 400)
        {
            return new TroopEvasionValidationResult(false, "Target coordinates must be between -400 and 400.");
        }

        var xText = request.TargetX.ToString(CultureInfo.InvariantCulture);
        var yText = request.TargetY.ToString(CultureInfo.InvariantCulture);
        var mode = ((int)request.MovementType).ToString(CultureInfo.InvariantCulture);
        var target = await TryTypeHumanlyIntoFirstMatchingInputAsync(["input[name='x']"], xText, cancellationToken)
                     && await TryTypeHumanlyIntoFirstMatchingInputAsync(["input[name='y']"], yText, cancellationToken);
        var modeRadio = _page.Locator($"input[type='radio'][name='eventType'][value='{mode}']").First;
        if (target && await modeRadio.CountAsync() > 0)
        {
            await DelayBeforeClickAsync(cancellationToken, "troop evasion: movement type");
            await ClickLocatorAsync(modeRadio, "troop-evasion-movement-type", cancellationToken);
            target = await _page.EvaluateAsync<bool>(
                """(args) => document.querySelector("input[name='x']")?.value.trim() === args.x && document.querySelector("input[name='y']")?.value.trim() === args.y && document.querySelector(`input[type="radio"][name="eventType"][value="${args.mode}"]`)?.checked === true""",
                new { x = xText, y = yText, mode });
        }
        else
        {
            target = false;
        }
        if (!target)
        {
            return new TroopEvasionValidationResult(false, "Target coordinates or movement type could not be set and verified.");
        }

        var selected = request.SelectedTroopSlots.Where(slot => slot is >= 1 and <= 10).Distinct().Order().ToList();
        var available = new Dictionary<int, long>();
        foreach (var slot in selected)
        {
            var count = await ReadAvailableTroopCountAsync($"t{slot}", cancellationToken) ?? 0;
            if (count <= 0)
            {
                continue;
            }
            if (!await TryFillTroopInputAsync($"t{slot}", $"troop slot {slot}", count, cancellationToken))
            {
                return new TroopEvasionValidationResult(false, $"Troop slot {slot} could not be filled.");
            }
            available[slot] = count;
        }

        var heroAvailable = false;
        if (request.IncludeHero)
        {
            var count = await ReadAvailableTroopCountAsync("t11", cancellationToken) ?? 0;
            heroAvailable = count > 0 && await TryFillTroopInputAsync("t11", "Hero", 1, cancellationToken);
        }

        if (available.Count == 0 && !heroAvailable)
        {
            return new TroopEvasionValidationResult(false, "No selected troops are currently at home.", available, false);
        }

        return new TroopEvasionValidationResult(true, "Evasion form is valid.", available, heroAvailable);
    }

    private async Task<TimeSpan?> ReadTroopEvasionTravelTimeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = await _page.EvaluateAsync<string[]>(
            """() => [document.querySelector('#in')?.textContent || '', document.querySelector('#at')?.getAttribute('value') || '']""");
        if (TryParseTravelDuration(data.ElementAtOrDefault(0), out var duration))
        {
            return duration;
        }

        if (long.TryParse(data.ElementAtOrDefault(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            var now = CurrentTravianServerTimeUtc();
            var arrival = DateTimeOffset.FromUnixTimeSeconds(unix);
            return arrival > now ? arrival - now : null;
        }
        return null;
    }

    internal static bool TryParseTravelDuration(string? text, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = Regex.Match(text, @"(?:(?<days>\d+)\s+(?:day|days)\s+)?(?<hours>\d{1,3}):(?<minutes>\d{2}):(?<seconds>\d{2})", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        var days = match.Groups["days"].Success ? int.Parse(match.Groups["days"].Value, CultureInfo.InvariantCulture) : 0;
        duration = TimeSpan.FromDays(days) + new TimeSpan(
            int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture));
        return true;
    }

    private static (TroopEvasionValidationResult Validation, TroopEvasionResult Result) FailedValidation(string message)
    {
        var validation = new TroopEvasionValidationResult(false, message);
        return (validation, new TroopEvasionResult(TroopEvasionOutcome.Failed, message));
    }
}
