using Microsoft.Playwright;
using System.Text.RegularExpressions;
using TbotUltra.Core.Configuration;

namespace TbotUltra.Worker.Services;

internal enum BulkMessageSendOutcomeKind
{
    Pending = 0,
    MissingPlayer = 1,
    Sent = 2,
    Error = 3,
}

internal sealed record BulkMessageSendSnapshot(
    bool IsWritePage,
    bool IsWriteFormVisible,
    string? DialogText,
    string? ValidationError);

internal sealed record BulkMessageSendOutcome(BulkMessageSendOutcomeKind Kind, string? Detail = null);

internal sealed record BulkMessageSendDomState(
    bool IsWriteFormVisible,
    string? DialogText,
    string? ValidationError);

public sealed partial class TravianClient
{
    private static readonly string[] BulkMessageRecipientSelectors =
    [
        "#messageForm input#receiver",
        "#messageForm input[name='an']",
        "input[name='recipients']",
        "textarea[name='recipients']",
        "input[name='recipient']",
        "textarea[name='recipient']",
        "input[name='to']",
        "textarea[name='to']",
        "input[name*='recipient' i]",
        "textarea[name*='recipient' i]",
        "input[name*='receiver' i]",
        "textarea[name*='receiver' i]",
        "input[id*='recipient' i]",
        "textarea[id*='recipient' i]",
        "input[id*='receiver' i]",
        "textarea[id*='receiver' i]",
        "input[placeholder*='Recipient' i]",
        "textarea[placeholder*='Recipient' i]",
        "input[placeholder*='Receiver' i]",
        "textarea[placeholder*='Receiver' i]",
        "input[placeholder*='Player' i]",
        "textarea[placeholder*='Player' i]",
        "input[aria-label*='Recipient' i]",
        "textarea[aria-label*='Recipient' i]",
    ];

    private static readonly string[] BulkMessageSubjectSelectors =
    [
        "#messageForm #subject input[name='be']",
        "#messageForm input[name='be']",
        "input[name='subject']",
        "input[name*='subject' i]",
        "input[id*='subject' i]",
        "input[placeholder*='Subject' i]",
        "input[aria-label*='Subject' i]",
        "input[name*='betreff' i]",
        "input[id*='betreff' i]",
        "input[placeholder*='Betreff' i]",
    ];

    private static readonly string[] BulkMessageBodySelectors =
    [
        "#messageForm textarea#message",
        "#messageForm textarea[name='message']",
        "textarea[name='message']",
        "textarea[name*='message' i]",
        "textarea[name*='body' i]",
        "textarea[name*='text' i]",
        "textarea[id*='message' i]",
        "textarea[id*='body' i]",
        "textarea[placeholder*='Message' i]",
        "textarea[aria-label*='Message' i]",
        "[contenteditable='true'][role='textbox']",
        "[contenteditable='true']",
        "textarea",
    ];

    private static readonly string[] BulkMessageSendButtonSelectors =
    [
        "#messageForm #send button[type='submit']",
        "#messageForm button[value='Send']",
        "form button[type='submit']",
        "form input[type='submit']",
        "button[type='submit']",
        "input[type='submit']",
        "button:has-text('Send')",
        "button:has-text('Send message')",
        "input[value*='Send' i]",
        ".button-content:has(.text:text-is('Send'))",
        ".button-container:has(.text:text-is('Send'))",
    ];

    private static readonly Regex BulkMessageMissingPlayerRegex =
        new(@"^\s*The\s+name\s+(.+?)\s+does\s+not\s+exist\.?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<IReadOnlyList<string>> SendBulkMessageBatchAsync(
        IReadOnlyList<string> playerNames,
        string subject,
        string message,
        CancellationToken cancellationToken = default)
    {
        var safePlayerNames = playerNames
            .Select(name => (name ?? string.Empty).Trim())
            .Where(name => name.Length > 0)
            .Where(name => !MapSqlPlayerParser.IsProtectedPlayerName(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var skippedProtected = playerNames.Count - safePlayerNames.Count;
        if (skippedProtected > 0)
        {
            Notify($"[bulk-messages] skipped {skippedProtected} protected/invalid recipient(s) before writing message.");
        }

        if (safePlayerNames.Count is < 1 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(playerNames), "A message batch must contain 1 to 25 players.");
        }

        Notify($"[bulk-messages] opening message writer for {safePlayerNames.Count} recipient(s).");
        await EnsureLoggedInAsync(cancellationToken: cancellationToken);
        await GotoAsync(Paths.MessagesWrite, cancellationToken);
        await WaitForBulkMessageWriteFormAsync(cancellationToken);

        var recipients = await FindVisibleBulkMessageFieldAsync(BulkMessageRecipientSelectors, "recipients");
        var subjectInput = await FindVisibleBulkMessageFieldAsync(BulkMessageSubjectSelectors, "subject");
        var body = await FindVisibleBulkMessageFieldAsync(BulkMessageBodySelectors, "message body");

        if (recipients is null || subjectInput is null || body is null)
        {
            var missing = string.Join(", ", new[]
            {
                recipients is null ? "recipients" : null,
                subjectInput is null ? "subject" : null,
                body is null ? "message body" : null,
            }.Where(value => value is not null));
            throw new InvalidOperationException($"Could not find message write field(s): {missing}. Save the /messages/write DOM and add selectors.");
        }

        var currentPlayerNames = safePlayerNames.ToList();
        var recipientText = string.Join(';', currentPlayerNames);
        await DelayBeforeBulkFieldAsync(cancellationToken, "recipients");
        await FillBulkMessageFieldAsync(recipients, recipientText, "recipients", cancellationToken);
        await DelayBeforeBulkFieldAsync(cancellationToken, "subject");
        await FillBulkMessageFieldAsync(subjectInput, subject, "subject", cancellationToken);
        await DelayBeforeBulkFieldAsync(cancellationToken, "message");
        await FillBulkMessageFieldAsync(body, message, "message body", cancellationToken);

        var sendButton = await FindVisibleBulkMessageFieldAsync(BulkMessageSendButtonSelectors, "send button");
        if (sendButton is null)
        {
            throw new InvalidOperationException("Could not find the message Send button. Save the /messages/write DOM and add selectors.");
        }

        var retryGuard = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (currentPlayerNames.Count == 0)
            {
                Notify("[bulk-messages] current batch has no valid recipients left; continuing with the next batch.");
                return currentPlayerNames;
            }

            await DelayBeforeClickAsync(cancellationToken, "bulk messages send");
            await sendButton.ClickAsync(new LocatorClickOptions { Timeout = _config.TimeoutMs }).WaitAsync(cancellationToken);
            var outcome = await WaitForBulkMessageSendOutcomeAsync(cancellationToken);
            if (outcome.Kind == BulkMessageSendOutcomeKind.Sent)
            {
                Notify($"[bulk-messages] sent batch to {currentPlayerNames.Count} recipient(s).");
                return currentPlayerNames;
            }

            if (outcome.Kind == BulkMessageSendOutcomeKind.Error)
            {
                throw new InvalidOperationException($"Bulk message send failed: {outcome.Detail}");
            }

            var missingPlayerName = outcome.Detail;
            if (outcome.Kind != BulkMessageSendOutcomeKind.MissingPlayer || string.IsNullOrWhiteSpace(missingPlayerName))
            {
                throw new InvalidOperationException("Bulk message send ended without a verified outcome.");
            }

            await DismissBulkMessageMissingPlayerDialogAsync(cancellationToken);
            var removed = RemoveBulkMessageRecipient(currentPlayerNames, missingPlayerName);
            if (!removed)
            {
                throw new InvalidOperationException($"Bulk message recipient '{missingPlayerName}' does not exist, but it could not be matched to the current batch.");
            }

            retryGuard++;
            if (retryGuard > safePlayerNames.Count)
            {
                throw new InvalidOperationException("Bulk message missing-player retry guard reached.");
            }

            if (currentPlayerNames.Count == 0)
            {
                Notify("[bulk-messages] removed the final missing recipient from the current batch; continuing with the next batch.");
                return currentPlayerNames;
            }

            Notify($"[bulk-messages] removed missing recipient '{missingPlayerName}' and retrying batch with {currentPlayerNames.Count} recipient(s).");
            await FillBulkMessageFieldAsync(recipients, string.Empty, "recipients", cancellationToken);
            await FillBulkMessageFieldAsync(recipients, string.Join(';', currentPlayerNames), "recipients", cancellationToken);
        }
    }

    private async Task WaitForBulkMessageWriteFormAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _page.Locator("#messageForm input#receiver, #messageForm input[name='an']")
                .First
                .WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = Math.Min(_config.TimeoutMs, 15000),
                })
                .WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("Message write form did not load: recipient field was not visible.");
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException($"Message write form did not load: {ex.Message}", ex);
        }
    }

    private async Task<ILocator?> FindVisibleBulkMessageFieldAsync(IReadOnlyList<string> selectors, string label)
    {
        foreach (var selector in selectors)
        {
            ILocator locator;
            try
            {
                locator = _page.Locator(selector);
            }
            catch
            {
                continue;
            }

            int count;
            try
            {
                count = Math.Min(await locator.CountAsync(), 10);
            }
            catch
            {
                continue;
            }

            for (var index = 0; index < count; index++)
            {
                var candidate = locator.Nth(index);
                try
                {
                    if (await candidate.IsVisibleAsync()
                        && await candidate.IsEnabledAsync(new LocatorIsEnabledOptions { Timeout = 1000 }))
                    {
                        Notify($"[bulk-messages:verbose] matched {label} selector '{selector}' index={index}.");
                        return candidate;
                    }
                }
                catch
                {
                    // Try the next candidate.
                }
            }
        }

        return null;
    }

    // Shorter randomized pause used only between the recipient/subject/message fields so filling the
    // message writer is noticeably faster than the standard click pacing, while still not being
    // robot-instant. Kept modest (0.15-0.45s) so the React form stays stable between fields. Respects
    // the global action-pacing on/off switch (no delay when pacing is disabled).
    private Task DelayBeforeBulkFieldAsync(CancellationToken cancellationToken, string reason)
        => ApplyPacingDelayAsync(
            0.15,
            0.45,
            "bulk-field-pacing",
            $"Bulk field: {reason}",
            cancellationToken);

    private async Task FillBulkMessageFieldAsync(ILocator field, string value, string label, CancellationToken cancellationToken)
    {
        await field.ClickAsync(new LocatorClickOptions { Timeout = _config.TimeoutMs }).WaitAsync(cancellationToken);
        await field.FillAsync(value, new LocatorFillOptions { Timeout = _config.TimeoutMs }).WaitAsync(cancellationToken);
        await field.EvaluateAsync(
            """
            element => {
              element.dispatchEvent(new Event('input', { bubbles: true }));
              element.dispatchEvent(new Event('change', { bubbles: true }));
            }
            """).WaitAsync(cancellationToken);

        var actual = await field.EvaluateAsync<string>(
            """
            element => {
              if ('value' in element) return element.value || '';
              return element.textContent || '';
            }
            """).WaitAsync(cancellationToken);
        if (!string.Equals(
            NormalizeBulkMessageFieldValue(actual),
            NormalizeBulkMessageFieldValue(value),
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Could not fill bulk message {label}: field value was not accepted.");
        }
    }

    private static string NormalizeBulkMessageFieldValue(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    internal static string? TryExtractBulkMessageMissingPlayerName(string? dialogText)
    {
        if (string.IsNullOrWhiteSpace(dialogText))
        {
            return null;
        }

        var match = BulkMessageMissingPlayerRegex.Match(dialogText.Trim());
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    internal static BulkMessageSendOutcome ClassifyBulkMessageSendOutcome(BulkMessageSendSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.DialogText))
        {
            var missingPlayerName = TryExtractBulkMessageMissingPlayerName(snapshot.DialogText);
            return missingPlayerName is null
                ? new BulkMessageSendOutcome(BulkMessageSendOutcomeKind.Error, $"Unexpected dialog: {snapshot.DialogText.Trim()}")
                : new BulkMessageSendOutcome(BulkMessageSendOutcomeKind.MissingPlayer, missingPlayerName);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ValidationError))
        {
            return new BulkMessageSendOutcome(BulkMessageSendOutcomeKind.Error, snapshot.ValidationError.Trim());
        }

        if (!snapshot.IsWritePage || !snapshot.IsWriteFormVisible)
        {
            return new BulkMessageSendOutcome(BulkMessageSendOutcomeKind.Sent);
        }

        return new BulkMessageSendOutcome(BulkMessageSendOutcomeKind.Pending);
    }

    private async Task<BulkMessageSendOutcome> WaitForBulkMessageSendOutcomeAsync(CancellationToken cancellationToken)
    {
        var timeoutMs = Math.Clamp(_config.TimeoutMs, 5000, 15000);
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var state = await _page.EvaluateAsync<BulkMessageSendDomState>(
                    """
                    () => {
                      const isVisible = element => {
                        if (!element) return false;
                        const style = window.getComputedStyle(element);
                        const rect = element.getBoundingClientRect();
                        return style.display !== 'none'
                          && style.visibility !== 'hidden'
                          && rect.width > 0
                          && rect.height > 0;
                      };
                      const clean = value => (value || '').replace(/\s+/g, ' ').trim();
                      const dialog = document.querySelector('.dialogOverlay.dialogVisible #dialogContent');
                      const writeForm = document.querySelector('#messageForm');
                      const validationError = Array.from(document.querySelectorAll(
                        '#messageForm .error, #messageForm .errors, #messageForm .alert, #messageForm .warning, ' +
                        '#messageForm [class*="error" i], #messageForm [class*="warning" i]'))
                        .find(node => isVisible(node) && clean(node.textContent).length > 0);
                      return {
                        isWriteFormVisible: isVisible(writeForm),
                        dialogText: isVisible(dialog) ? clean(dialog.textContent) : null,
                        validationError: validationError ? clean(validationError.textContent) : null,
                      };
                    }
                    """).WaitAsync(cancellationToken);
                var outcome = ClassifyBulkMessageSendOutcome(new BulkMessageSendSnapshot(
                    IsCurrentUrlForPath(Paths.MessagesWrite),
                    state.IsWriteFormVisible,
                    state.DialogText,
                    state.ValidationError));
                if (outcome.Kind != BulkMessageSendOutcomeKind.Pending)
                {
                    return outcome;
                }
            }
            catch (PlaywrightException ex) when (IsTransientExecutionContextError(ex))
            {
                // A successful classic form submit replaces the execution context. Re-read the new page.
            }

            await Task.Delay(Random.Shared.Next(100, 180), cancellationToken);
        }

        return new BulkMessageSendOutcome(
            BulkMessageSendOutcomeKind.Error,
            $"No success, missing-player dialog, or validation error appeared within {timeoutMs} ms.");
    }

    private async Task DismissBulkMessageMissingPlayerDialogAsync(CancellationToken cancellationToken)
    {
        var okButton = _page.Locator(".dialogOverlay.dialogVisible button.dialogButtonOk, .dialogOverlay.dialogVisible button.ok").First;
        await DelayBeforeClickAsync(cancellationToken, "bulk messages missing player dialog ok");
        await okButton.ClickAsync(new LocatorClickOptions { Timeout = _config.TimeoutMs }).WaitAsync(cancellationToken);
        try
        {
            await _page.Locator(".dialogOverlay.dialogVisible")
                .First
                .WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Hidden,
                    Timeout = Math.Min(_config.TimeoutMs, 5000),
                })
                .WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The next field fill will fail clearly if the dialog still blocks the page.
        }
    }

    private static bool RemoveBulkMessageRecipient(List<string> playerNames, string missingPlayerName)
    {
        var missingKey = MapSqlPlayerParser.NormalizeNameKey(missingPlayerName);
        var removed = playerNames.RemoveAll(name =>
            string.Equals(MapSqlPlayerParser.NormalizeNameKey(name), missingKey, StringComparison.Ordinal));
        return removed > 0;
    }

}
