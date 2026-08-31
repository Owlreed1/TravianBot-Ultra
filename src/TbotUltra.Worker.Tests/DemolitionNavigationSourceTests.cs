using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class DemolitionNavigationSourceTests
{
    [Fact]
    public void DemolishSubmit_UsesTrustedClickAndWaitsForRedirectBeforeReadingTimer()
    {
        var source = ReadSource();
        var start = source.IndexOf("private async Task<bool> TryStartDemolitionStepAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task<int?> ReadActiveDemolitionSecondsOnCurrentPageAsync", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start, "Could not locate the demolition submit flow.");
        var submitFlow = source[start..end];
        Assert.Contains("Locator(", submitFlow, StringComparison.Ordinal);
        Assert.Contains("ClickAsync", submitFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("button.click()", submitFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("GotoAsync", submitFlow, StringComparison.Ordinal);

        var clickCall = source.IndexOf("TryStartDemolitionStepAsync", StringComparison.Ordinal);
        var timerRead = source.IndexOf("ReadActiveDemolitionSecondsOnCurrentPageAsync", clickCall, StringComparison.Ordinal);
        var afterClick = source[clickCall..timerRead];
        Assert.Contains("WaitForPageReadyAsync", afterClick, StringComparison.Ordinal);
    }

    private static string ReadSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "TbotUltra.Worker",
                "Services",
                "Automation",
                "Buildings",
                "TravianClient.Buildings.Demolition.cs");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new DirectoryNotFoundException("Could not locate the demolition source file.");
    }
}
