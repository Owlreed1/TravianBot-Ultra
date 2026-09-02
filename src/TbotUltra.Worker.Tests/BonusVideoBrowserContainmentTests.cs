using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BonusVideoBrowserContainmentTests
{
    [Fact]
    public void IsolatedBonusBrowser_BlocksPopupEscapeBeforeCreatingItsPage()
    {
        var source = ReadBonusVideoSource();
        var contextCreated = source.IndexOf("videoBrowser.NewContextAsync", StringComparison.Ordinal);
        var suppressionInstalled = source.IndexOf(
            "AddInitScriptAsync(IsolatedBonusVideoPopupSuppressionScript)",
            contextCreated,
            StringComparison.Ordinal);
        var pageCreated = source.IndexOf("videoContext.NewPageAsync", contextCreated, StringComparison.Ordinal);

        Assert.Contains("IsolatedBonusVideoPopupSuppressionScript", source, StringComparison.Ordinal);
        Assert.Contains("window.open", ReadBrowserSessionSource(), StringComparison.Ordinal);
        Assert.True(suppressionInstalled > contextCreated, "The isolated context must install popup suppression.");
        Assert.True(pageCreated > suppressionInstalled, "Popup suppression must be active before the page is created.");
    }

    [Fact]
    public void IsolatedBonusBrowser_KeepsChromesNativePopupBlockerEnabled()
    {
        Assert.Contains(
            "CreateChromiumLaunchOptions(keepNativePopupBlocker: true)",
            ReadBonusVideoSource(),
            StringComparison.Ordinal);
    }

    private static string ReadBonusVideoSource()
        => ReadSourceFile("BrowserSession.BonusVideo.cs");

    private static string ReadBrowserSessionSource()
        => ReadSourceFile("BrowserSession.cs");

    private static string ReadSourceFile(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "TbotUltra.Worker",
                "Infrastructure",
                fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new DirectoryNotFoundException($"Could not locate {fileName}.");
    }
}
