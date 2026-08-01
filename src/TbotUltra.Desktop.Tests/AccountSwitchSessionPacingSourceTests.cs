using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AccountSwitchSessionPacingSourceTests
{
    [Fact]
    public void RefreshAfterActiveAccountChanged_ForceClearsThePreviousAccountsVillageSelection()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Session.cs"));
        var methodStart = source.IndexOf(
            "private void RefreshAfterActiveAccountChanged",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    // ResetVillageSelectionUi()",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];

        Assert.Contains("ForceClearVillageSelectionUi();", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ResetVillageSelectionUi();", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetForAccountSwitch_ClearsThePreviousAccountsSleepState()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Session.cs"));
        var methodStart = source.IndexOf(
            "private async Task ResetForAccountSwitchAsync",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    // bot.json is shared across accounts",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];

        Assert.Contains("ResetSessionPacing();", methodBody, StringComparison.Ordinal);

        var pacingSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.SessionPacing.cs"));
        var resetStart = pacingSource.IndexOf(
            "private void ResetSessionPacing()",
            StringComparison.Ordinal);
        var resetEnd = pacingSource.IndexOf(
            "    // Freeze the pacing run->sleep countdown",
            resetStart,
            StringComparison.Ordinal);

        Assert.True(resetStart >= 0 && resetEnd > resetStart);
        var resetBody = pacingSource[resetStart..resetEnd];
        Assert.Contains("_sleepSnapshot = SleepSnapshot.Idle;", resetBody, StringComparison.Ordinal);
        Assert.Contains("_sessionPacer.Reset();", resetBody, StringComparison.Ordinal);
    }
}
