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
}
