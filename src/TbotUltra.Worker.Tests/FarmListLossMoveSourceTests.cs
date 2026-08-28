using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class FarmListLossMoveSourceTests
{
    [Fact]
    public void CombinedMove_VerifiesContextMenuAndHandlesBothTimeoutTypes()
    {
        var source = ReadSource();
        var helper = Slice(
            source,
            "private async Task<ILocator?> TryOpenFarmListEditMenuAsync(",
            "private async Task<bool> WaitForFarmListMoveWithDuplicateOverrideAsync(");

        Assert.Contains(".contextMenu.from.defaultButtons:visible", helper, StringComparison.Ordinal);
        Assert.Contains("Math.Min(_config.TimeoutMs, 3000)", helper, StringComparison.Ordinal);
        Assert.Contains("JsOpenFarmListRowMenuAsync(row)", helper, StringComparison.Ordinal);
        Assert.Contains("catch (TimeoutException ex)", helper, StringComparison.Ordinal);
        Assert.Contains("catch (PlaywrightException ex)", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void CombinedMove_ConfirmsDuplicateOnlyOnceAndRefreshesBeforeMutationRetry()
    {
        var source = ReadSource();
        var confirmation = Slice(
            source,
            "private async Task<bool> WaitForFarmListMoveWithDuplicateOverrideAsync(",
            "private async Task<bool> VerifyFarmListMoveAfterRefreshAsync(");

        Assert.Contains("var duplicateConfirmed = false;", confirmation, StringComparison.Ordinal);
        Assert.Contains("if (duplicateConfirmed)", confirmation, StringComparison.Ordinal);
        Assert.Contains("State = WaitForSelectorState.Hidden", confirmation, StringComparison.Ordinal);
        Assert.Contains("return await VerifyFarmListMoveAfterRefreshAsync", confirmation, StringComparison.Ordinal);
    }

    [Fact]
    public void FarmButtonClick_HandlesSystemTimeoutAsTransient()
    {
        var source = ReadSource();
        var helper = Slice(
            source,
            "private async Task<bool> TryRealClickFarmButtonAsync(",
            "private async Task<IReadOnlyList<FarmListLossRowJs>> ReadFarmListLossRowsFromCurrentPageAsync(");

        Assert.Contains("catch (TimeoutException ex)", helper, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find source marker {startMarker}.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not find source range {startMarker} -> {endMarker}.");
        return source[start..end];
    }

    private static string ReadSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "TbotUltra.Worker",
                "Services",
                "Automation",
                "Farming",
                "TravianClient.FarmLists.cs");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
