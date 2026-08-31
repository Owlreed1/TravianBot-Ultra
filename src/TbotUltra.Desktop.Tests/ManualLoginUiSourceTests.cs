using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ManualLoginUiSourceTests
{
    [Fact]
    public void ManageAndWaitingUi_ExposeTheRequiredManualLoginControls()
    {
        var accountsXaml = ReadSource("AccountsWindow.xaml");
        var waitingXaml = ReadSource("ManualLoginWindow.xaml");

        Assert.Contains("x:Name=\"ManualLoginCheckBox\"", accountsXaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Manual login\"", accountsXaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{DynamicResource InfoTooltipIconStyle}\"", accountsXaml, StringComparison.Ordinal);
        Assert.Contains("Open the Travian lobby and wait while you sign in manually.", accountsXaml, StringComparison.Ordinal);
        Assert.Contains("STOP when the lobby shows your game worlds / servers.", waitingXaml, StringComparison.Ordinal);
        Assert.Contains("Do not click Play now.", waitingXaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Login done\"", waitingXaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Cancel\"", waitingXaml, StringComparison.Ordinal);
    }

    private static string ReadSource(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "TbotUltra.Desktop", fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new DirectoryNotFoundException($"Could not locate {fileName}.");
    }
}
