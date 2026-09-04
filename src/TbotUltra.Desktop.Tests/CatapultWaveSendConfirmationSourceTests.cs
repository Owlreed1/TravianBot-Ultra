using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class CatapultWaveSendConfirmationSourceTests
{
    [Fact]
    public void PreparationAction_ClearlyStatesThatNothingIsSentYet()
    {
        var xaml = ReadSource("CatapultWaveWindow.xaml");
        var codeBehind = ReadSource("CatapultWaveWindow.xaml.cs");

        Assert.Contains("Content=\"Prepare waves\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Prepare catapult waves?", codeBehind, StringComparison.Ordinal);
        Assert.Contains("No attacks will be sent", codeBehind, StringComparison.Ordinal);
        Assert.Contains("BusyOverlay.Show(\"Preparing catapult waves\"", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void PreparedConfirmation_UsesSendNowAndCancelWithSoftActionColors()
    {
        var source = ReadSource("CatapultWaveWindow.xaml.cs");

        Assert.Contains("All catapult attacks are prepared", source, StringComparison.Ordinal);
        Assert.Contains("(\"Send now\", MessageBoxResult.Yes)", source, StringComparison.Ordinal);
        Assert.Contains("(\"Cancel\", MessageBoxResult.Cancel)", source, StringComparison.Ordinal);
        Assert.Contains("successResult: MessageBoxResult.Yes", source, StringComparison.Ordinal);
        Assert.Contains("dangerResult: MessageBoxResult.Cancel", source, StringComparison.Ordinal);
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
