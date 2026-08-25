namespace TbotUltra.Worker.Services;

public sealed partial class TravianClient
{
    public async Task<string?> ReadCurrentPlayerNameAsync(CancellationToken cancellationToken = default)
    {
        LogFunctionStarted();
        await EnsureLoggedInAsync(cancellationToken: cancellationToken);

        var playerNameLocator = _page.Locator(Selectors.CurrentPlayerName).First;
        string? playerName;
        try
        {
            await playerNameLocator.WaitForAsync(new Microsoft.Playwright.LocatorWaitForOptions
            {
                State = Microsoft.Playwright.WaitForSelectorState.Visible,
                Timeout = _config.TimeoutMs,
            }).WaitAsync(cancellationToken);
            playerName = await playerNameLocator
                .TextContentAsync(new Microsoft.Playwright.LocatorTextContentOptions { Timeout = _config.TimeoutMs })
                .WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            Notify("[all-villages] current player name did not render in the active-village sidebar.");
            return null;
        }

        playerName = string.IsNullOrWhiteSpace(playerName)
            ? null
            : System.Text.RegularExpressions.Regex.Replace(playerName, @"\s+", " ").Trim();

        Notify(playerName is null
            ? "[all-villages] current player name was not available in the active-village sidebar."
            : $"[all-villages] current player identified as '{playerName}'.");
        return playerName;
    }
}
