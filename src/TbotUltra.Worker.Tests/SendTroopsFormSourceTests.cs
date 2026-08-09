using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class SendTroopsFormSourceTests
{
    [Fact]
    public void CatapultSendsWithOkBeforeWaitingForTheConfirmationButton()
    {
        var source = ReadWorkerSource("Services", "Automation", "Combat", "TravianClient.Catapults.cs");
        var prepareStart = source.IndexOf("private async Task<PreparedCatapultAttack> PrepareCatapultFirstAttackAsync", StringComparison.Ordinal);
        var prepareEnd = source.IndexOf("private async Task EnsureCatapultConfirmReadyAsync", prepareStart, StringComparison.Ordinal);
        Assert.True(prepareStart >= 0 && prepareEnd > prepareStart);

        var prepareBody = source[prepareStart..prepareEnd];
        var sendClick = prepareBody.IndexOf("TryClickCatapultSendButtonAsync", StringComparison.Ordinal);
        var confirmationWait = prepareBody.IndexOf("WaitForSendTroopsConfirmationPageAsync", StringComparison.Ordinal);

        Assert.Contains("CatapultSendButtonSelectors", source, StringComparison.Ordinal);
        Assert.Contains("button#ok[name='ok'][value='ok'][type='submit']", source, StringComparison.Ordinal);
        Assert.Contains("button#confirmSendTroops.rallyPointConfirm", source, StringComparison.Ordinal);
        Assert.True(sendClick >= 0 && confirmationWait > sendClick,
            "Catapult must click Send (#ok), then wait for the reloaded confirmation page.");
    }

    [Fact]
    public void SendTroopsModesUseTheOfficialEventTypeValues()
    {
        var catapultSource = ReadWorkerSource("Services", "Automation", "Combat", "TravianClient.Catapults.cs");
        var reinforcementSource = ReadWorkerSource("Services", "Automation", "Combat", "TravianClient.Reinforcements.cs");

        Assert.Contains("const eventTypeValue = raidAttack ? '4' : '3';", catapultSource, StringComparison.Ordinal);
        Assert.Contains("input[type=\"radio\"][name=\"eventType\"][value=\"${eventTypeValue}\"]", catapultSource, StringComparison.Ordinal);
        Assert.Contains("input[type=\"radio\"][name=\"eventType\"][value=\"5\"]", reinforcementSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CatapultWaves_KeepControlOpenedTabsUntilEveryWaveIsConfirmed()
    {
        var source = ReadWorkerSource("Services", "Automation", "Combat", "TravianClient.Catapults.cs");
        var runStart = source.IndexOf("public async Task<CatapultWaveRunResult> StartCatapultWavesAsync", StringComparison.Ordinal);
        var cleanupStart = source.IndexOf("finally", runStart, StringComparison.Ordinal);
        Assert.True(runStart >= 0 && cleanupStart > runStart);

        var runBody = source[runStart..cleanupStart];
        Assert.Contains("PrepareCatapultWaveTabsAsync", runBody, StringComparison.Ordinal);
        Assert.Contains("prepared.Insert(0", runBody, StringComparison.Ordinal);
        Assert.Contains("TryClickCatapultConfirmButtonAsync", runBody, StringComparison.Ordinal);

        var cleanupBody = source[cleanupStart..];
        Assert.Contains("foreach (var attack in prepared.Skip(1))", cleanupBody, StringComparison.Ordinal);
        Assert.Contains("await attack.Page.CloseAsync();", cleanupBody, StringComparison.Ordinal);
    }

    [Fact]
    public void CatapultWaves_OpenWaveTabsWithControlSendBeforePreparingTheFirstAttack()
    {
        var source = ReadWorkerSource("Services", "Automation", "Combat", "TravianClient.Catapults.cs");
        var runStart = source.IndexOf("public async Task<CatapultWaveRunResult> StartCatapultWavesAsync", StringComparison.Ordinal);
        var confirmationStart = source.IndexOf("VerifyCatapultArrivalOrder(prepared);", runStart, StringComparison.Ordinal);
        Assert.True(runStart >= 0 && confirmationStart > runStart);

        var preparation = source[runStart..confirmationStart];
        var waves = preparation.IndexOf("PrepareCatapultWaveTabsAsync", StringComparison.Ordinal);
        var firstAttack = preparation.IndexOf("PrepareCatapultFirstAttackAsync", StringComparison.Ordinal);

        Assert.True(waves >= 0 && firstAttack > waves,
            "Catapult must create all Wave tabs before changing the original tab to the First attack.");
        Assert.Contains("KeyboardModifier.Control", source, StringComparison.Ordinal);
        Assert.Contains("TabOpenDelayMilliseconds", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CatapultWaveTabBurst_UsesOnlyItsConfiguredTabDelay()
    {
        var source = ReadWorkerSource("Services", "Automation", "Combat", "TravianClient.Catapults.cs");

        Assert.Contains("DelayBeforeCatapultWaveClickAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DelayBeforeClickAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TypeHumanlyAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GotoOnCatapultPageAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CatapultWaves_DispatchEveryConfirmationBeforeWaitingForAnyResult()
    {
        var source = ReadWorkerSource("Services", "Automation", "Combat", "TravianClient.Catapults.cs");
        var runStart = source.IndexOf("public async Task<CatapultWaveRunResult> StartCatapultWavesAsync", StringComparison.Ordinal);
        var cleanupStart = source.IndexOf("finally", runStart, StringComparison.Ordinal);
        Assert.True(runStart >= 0 && cleanupStart > runStart);

        var runBody = source[runStart..cleanupStart];
        var dispatched = runBody.IndexOf("var dispatched = new List<PreparedCatapultAttack>();", StringComparison.Ordinal);
        var resultWait = runBody.IndexOf("foreach (var attack in dispatched)", StringComparison.Ordinal);

        Assert.True(dispatched >= 0 && resultWait > dispatched,
            "Catapult must dispatch every Confirm before waiting for any server result.");
        Assert.DoesNotContain("WaitForCatapultSendResultAsync", runBody[dispatched..resultWait], StringComparison.Ordinal);
    }

    [Fact]
    public void CatapultFirstAttack_UsesRequestedTargetsAndFallsBackToRandomWhenUnavailable()
    {
        var source = ReadWorkerSource("Services", "Automation", "Combat", "TravianClient.Catapults.cs");
        var prepareStart = source.IndexOf("private async Task<PreparedCatapultAttack> PrepareCatapultConfirmationAsync", StringComparison.Ordinal);
        var prepareEnd = source.IndexOf("private async Task<IPage> OpenCatapultWaveTabAsync", prepareStart, StringComparison.Ordinal);
        Assert.True(prepareStart >= 0 && prepareEnd > prepareStart);

        var prepareBody = source[prepareStart..prepareEnd];
        Assert.Contains("firstAttack ? request.Target1 : null", prepareBody, StringComparison.Ordinal);
        Assert.Contains("firstAttack ? request.Target2 : null", prepareBody, StringComparison.Ordinal);

        var targetSelectStart = source.IndexOf("private async Task<CatapultTargetSelectResult> TrySelectCatapultTargetsAsync", StringComparison.Ordinal);
        var targetSelectEnd = source.IndexOf("private async Task<int?> TryReadAttackDurationSecondsAsync", targetSelectStart, StringComparison.Ordinal);
        Assert.True(targetSelectStart >= 0 && targetSelectEnd > targetSelectStart);
        var targetSelectBody = source[targetSelectStart..targetSelectEnd];

        Assert.Contains("desired !== 'random'", targetSelectBody, StringComparison.Ordinal);
        Assert.Contains("normalize(option.textContent).includes('random')", targetSelectBody, StringComparison.Ordinal);
    }

    private static string ReadWorkerSource(params string[] segments)
        => File.ReadAllText(Path.Combine(ProjectRootLocator.FindProjectRoot(), "src", "TbotUltra.Worker", Path.Combine(segments)));
}
