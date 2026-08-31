using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ManualLoginFlowSourceTests
{
    [Fact]
    public void ManualLogin_WaitsForUserAndNeverRunsCredentialRecovery()
    {
        var source = ReadSource("TravianClient.LobbyLogin.cs");
        var manualStart = source.IndexOf("if (_account.ManualLogin)", StringComparison.Ordinal);
        var normalLogin = source.IndexOf("lobby session is not authenticated; submitting credentials", manualStart, StringComparison.Ordinal);
        var manualFlow = source[manualStart..normalLogin];

        Assert.Contains("_manualLoginConfirmationRequested", manualFlow, StringComparison.Ordinal);
        Assert.Contains("allowCredentialRecovery: false", manualFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("FillLoginCredentialsWithPacingAsync", manualFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("ClickLoginButtonAsync", manualFlow, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualLogin_DisablesPopupExceptionBeforePostLoginContextRotation()
    {
        var source = ReadSource("TravianClient.Login.cs");
        var lobbySuccess = source.IndexOf("if (await TryLoginThroughLobbyAsync", StringComparison.Ordinal);
        var popupDisable = source.IndexOf("_setManualAuthenticationPopupsAllowed?.Invoke(false)", lobbySuccess, StringComparison.Ordinal);
        var contextRotation = source.IndexOf("_rotateAfterLobbyLoginAsync(ServerUrl", lobbySuccess, StringComparison.Ordinal);

        Assert.True(lobbySuccess >= 0, "Could not locate the successful lobby-login branch.");
        Assert.True(popupDisable > lobbySuccess, "Could not locate popup-exception shutdown after lobby verification.");
        Assert.True(contextRotation > popupDisable, "Popup exception must close before state save/context rotation can render the game.");
    }

    [Fact]
    public void ManualLogin_DoesNotReloadThePreloadedPostLoginGamePage()
    {
        var source = ReadSource("TravianClient.Login.cs");
        var rotation = source.IndexOf("_rotateAfterLobbyLoginAsync(ServerUrl", StringComparison.Ordinal);
        var nextLoginCheck = source.IndexOf("WaitUntilLoggedInAsync", rotation, StringComparison.Ordinal);
        var postRotation = source[rotation..nextLoginCheck];

        Assert.Contains("if (!IsCurrentUrlForPath(Paths.Resources))", postRotation, StringComparison.Ordinal);
    }

    private static string ReadSource(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "TbotUltra.Worker", "Services", "Automation", "Core", fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new DirectoryNotFoundException($"Could not locate {fileName}.");
    }
}
