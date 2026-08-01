using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class MobileOptimizationsDialogTests
{
    [Fact]
    public void Selectors_AreScopedToCapturedMobileOptimizationsDialog()
    {
        const string capturedDialog = """
            <div id="mobileOptimizationsDialog" class="modal">
              <label class="switch"><input type="checkbox" name="mobileOptimizations"></label>
              <label class="switch"><input type="checkbox" checked="" name="mobileOptimizations"></label>
              <div class="action"><button class="framed green withText" type="button"><div>Play now</div></button></div>
            </div>
            """;

        Assert.Contains("id=\"mobileOptimizationsDialog\"", capturedDialog);
        Assert.Contains("name=\"mobileOptimizations\"", capturedDialog);
        Assert.Contains("<div>Play now</div>", capturedDialog);

        Assert.Equal("#mobileOptimizationsDialog", TravianClient.MobileOptimizationsDialogSelector);
        Assert.Equal(
            "#mobileOptimizationsDialog label.switch:has(input[name='mobileOptimizations'])",
            TravianClient.MobileOptimizationsSwitchSelector);
        Assert.Equal(
            "#mobileOptimizationsDialog .action button.framed.green.withText",
            TravianClient.MobileOptimizationsPlayNowButtonSelector);
    }

    [Fact]
    public void ConfirmingMobileDialog_WaitsForLateGameNavigationBeforeWorldSelection()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Core",
            "TravianClient.LobbyLogin.cs"));
        var methodStart = source.IndexOf("private async Task<bool> TryEnterLobbyWorldAsync", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private async Task<bool> TryHandleMobileOptimizationsDialogAsync", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("WaitForGameOriginAfterMobileConfirmationAsync", method, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate TbotUltra.sln from the test output directory.");
    }
}
