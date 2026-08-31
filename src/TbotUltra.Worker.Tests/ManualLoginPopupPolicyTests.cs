using TbotUltra.Worker.Infrastructure;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ManualLoginPopupPolicyTests
{
    [Fact]
    public void PostLoginContext_IsOnTheGamePageBeforeTheLobbyContextCloses()
    {
        var source = ReadBrowserSessionSource();
        var rotationStart = source.IndexOf("RotateMainContextFromSavedStateAsync", StringComparison.Ordinal);
        var rotationEnd = source.IndexOf("CreateChromiumLaunchOptions", rotationStart, StringComparison.Ordinal);
        var rotation = source[rotationStart..rotationEnd];
        var gameNavigation = rotation.IndexOf("cleanPage.GotoAsync", StringComparison.Ordinal);
        var lobbyClose = rotation.IndexOf("previousContext.CloseAsync", StringComparison.Ordinal);

        Assert.True(gameNavigation >= 0, "The clean context must preload the game page.");
        Assert.True(lobbyClose > gameNavigation, "The lobby context must remain visible until the clean game page is ready.");
    }

    [Theory]
    [InlineData("about:blank")]
    [InlineData("chrome://newtab/")]
    [InlineData("https://appleid.apple.com/auth/authorize")]
    [InlineData("https://idmsa.apple.com/appleauth/auth/signin")]
    [InlineData("https://auth.travian.com/callback")]
    [InlineData("https://delivery.consentmanager.net/delivery/cookie-consent.php")]
    [InlineData("https://accounts.google.com/signin")]
    public void ManualLoginWait_KeepsEveryUserOpenedPageAvailable(string popupUrl)
    {
        Assert.True(BrowserSession.ShouldKeepExtraPageOpen(manualLoginWaitActive: true, popupUrl));
    }

    [Fact]
    public void OutsideManualLoginWait_ExtraPagesRemainBlocked()
    {
        Assert.False(BrowserSession.ShouldKeepExtraPageOpen(manualLoginWaitActive: false, "about:blank"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void NativePopupBlocker_IsDisabledOnlyForManualLoginAccounts(
        bool manualLoginAccount,
        bool expectedKeepBlocker)
    {
        Assert.Equal(expectedKeepBlocker, BrowserSession.ShouldKeepNativePopupBlocker(manualLoginAccount));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void NewContext_AllowsPopupSourcesOnlyDuringManualLoginWait(
        bool manualLoginAccount,
        bool manualLoginWaitActive,
        bool expectedAllowed)
    {
        Assert.Equal(
            expectedAllowed,
            BrowserSession.ShouldAllowPopupSourcesInNewContext(manualLoginAccount, manualLoginWaitActive));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    public void ManualLoginWait_NeverUnblocksTheConsentAdStack(
        bool isAdDomain,
        bool bonusVideoConsentAllowed,
        bool manualLoginWaitActive,
        bool expectedBlocked)
    {
        Assert.Equal(
            expectedBlocked,
            BrowserSession.ShouldBlockMainContextRequest(
                isAdDomain,
                bonusVideoConsentAllowed,
                manualLoginWaitActive));
    }

    [Fact]
    public void MainContext_SuppressesConsentUiBeforeCreatingTheVisiblePage()
    {
        var source = ReadBrowserSessionSource();
        var contextStart = source.IndexOf("OpenMainContextPageAsync", StringComparison.Ordinal);
        var contextEnd = source.IndexOf("RotateMainContextFromSavedStateAsync", contextStart, StringComparison.Ordinal);
        var context = source[contextStart..contextEnd];
        var suppression = context.IndexOf("MainContextConsentUiSuppressionScript", StringComparison.Ordinal);
        var visiblePage = context.IndexOf("NewPageAsync", StringComparison.Ordinal);

        Assert.Contains("#cmpwrapper", BrowserSession.MainContextConsentUiSuppressionScript, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", BrowserSession.MainContextConsentUiSuppressionScript, StringComparison.Ordinal);
        Assert.True(suppression >= 0, "The main context must install consent suppression.");
        Assert.True(visiblePage > suppression, "Consent suppression must be installed before the visible page is created.");
    }

    [Fact]
    public void PopupGuard_AllowsTheVisibleTravianLoginPageToOpenAuthenticationTabs()
    {
        var source = ReadBrowserSessionSource();

        Assert.Contains("host === 'www.travian.com'", source, StringComparison.Ordinal);
    }

    private static string ReadBrowserSessionSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "TbotUltra.Worker", "Infrastructure", "BrowserSession.cs");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new DirectoryNotFoundException("Could not locate BrowserSession.cs.");
    }
}
