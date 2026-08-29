using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TroopTrainingOptionsWindowSourceTests
{
    [Fact]
    public void ExpandedRows_DoNotShowTheMinimumTroopRangeHint()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string? path = null;
        while (directory is not null && path is null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TbotUltra.Desktop", "TroopTrainingOptionsWindow.xaml");
            if (File.Exists(candidate))
            {
                path = candidate;
            }

            directory = directory.Parent;
        }

        Assert.NotNull(path);
        var xaml = File.ReadAllText(path);

        Assert.DoesNotContain("(1–10,000)", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncWindow_UsesNeutralSourcePanelAndNoFooterHelpText()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string? path = null;
        while (directory is not null && path is null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TbotUltra.Desktop", "TroopTrainingSettingsSyncWindow.xaml");
            if (File.Exists(candidate))
            {
                path = candidate;
            }

            directory = directory.Parent;
        }

        Assert.NotNull(path);
        var xaml = File.ReadAllText(path);
        var sourcePanel = xaml[
            xaml.IndexOf("<Border Grid.Row=\"1\"", StringComparison.Ordinal)..
            xaml.IndexOf("<Border Grid.Row=\"2\"", StringComparison.Ordinal)];

        Assert.DoesNotContain("SuccessBgBrush", sourcePanel, StringComparison.Ordinal);
        Assert.DoesNotContain("SuccessBorderBrush", sourcePanel, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(xaml, "Text=\"SYNC FROM\"").Count);
        Assert.DoesNotContain("Text=\"SOURCE\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("The village ON switch is not copied", xaml, StringComparison.Ordinal);
    }
}
