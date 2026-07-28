using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AccountScanSourceTests
{
    [Fact]
    public void AccountScanDialog_UsesTransientDefaultScopeAndSweepReaders()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.AccountScan.cs"));

        Assert.Contains("ShowAccountScanSelectionDialog()", source, StringComparison.Ordinal);
        Assert.Contains("Content = \"Dorf1\"", source, StringComparison.Ordinal);
        Assert.Contains("Content = \"Dorf2\"", source, StringComparison.Ordinal);
        Assert.Contains("CreateAccountScanCheckBox(\"Smithy\")", source, StringComparison.Ordinal);
        Assert.Contains("CreateAccountScanCheckBox(\"Barracks\")", source, StringComparison.Ordinal);
        Assert.Contains("CreateAccountScanCheckBox(\"Stable\")", source, StringComparison.Ordinal);
        Assert.Contains("CreateAccountScanCheckBox(\"Workshop\")", source, StringComparison.Ordinal);
        Assert.Contains("CreateAccountScanCheckBox(\"Town Hall\")", source, StringComparison.Ordinal);
        Assert.Contains("CreateAccountScanCheckBox(\"Brewery\"", source, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(source, "IsChecked = true,"));
        Assert.Contains(".OrderBy(_ => Random.Shared.Next())", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreVillageAfterAccountScanAsync(", source, StringComparison.Ordinal);
        Assert.Contains("ReadVillageStatusSweepBaseStatusAsync(", source, StringComparison.Ordinal);
        Assert.Contains("RefreshVillageStatusSweepOptionalStatusesAsync(", source, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
