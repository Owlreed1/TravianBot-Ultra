using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services.Orchestration;

internal sealed record AutomationCandidate(
    Guid Id,
    string TaskName,
    QueueGroup Group,
    string? VillageKey,
    int Priority,
    DateTimeOffset NextAttemptAt,
    AutomationDecisionRequirement? Decision = null)
{
    internal static AutomationCandidate FromQueueItem(QueueItem item) => new(
        item.Id,
        item.TaskName,
        item.Group,
        item.Payload.GetValueOrDefault(BotOptionPayloadKeys.TargetVillageKey),
        item.Priority,
        item.NextAttemptAt);
}

internal sealed record AutomationDecisionRequirement(string Code);

internal sealed record AutomationStateSnapshot(
    IReadOnlyList<AutomationCandidate> Candidates,
    bool IsComplete = false,
    DateTimeOffset? NextWakeAt = null);

internal abstract record AutomationStateChange
{
    private AutomationStateChange()
    {
    }

    internal sealed record ActionFinished(
        Guid ItemId,
        AutomationActionOutcome Outcome) : AutomationStateChange;
}

internal interface IAutomationStatePort
{
    ValueTask<AutomationStateSnapshot> ReadAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        CancellationToken cancellationToken);

    ValueTask ApplyAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        AutomationStateChange change,
        CancellationToken cancellationToken);
}

internal interface IOfficialTravianAutomationPort
{
    ValueTask<AutomationActionOutcome> ExecuteAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        AutomationCandidate action,
        CancellationToken cancellationToken);
}

internal interface IAutomationModePassPort
{
    ValueTask<AutomationStateSnapshot> ReadAsync(
        AutomationRunContext context,
        CancellationToken cancellationToken);

    ValueTask<AutomationActionOutcome> ExecuteAsync(
        AutomationRunContext context,
        AutomationCandidate action,
        CancellationToken cancellationToken);
}

internal sealed class AutomationPassPort(
    Func<string?> activeAccountKey,
    Func<long> browserGeneration,
    IAutomationModePassPort continuousLoop,
    IAutomationModePassPort autoQueue) : IAutomationStatePort, IOfficialTravianAutomationPort
{
    public ValueTask<AutomationStateSnapshot> ReadAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        CancellationToken cancellationToken)
    {
        EnsureCurrentContext(context);
        return Resolve(mode).ReadAsync(context, cancellationToken);
    }

    public ValueTask ApplyAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        AutomationStateChange change,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask<AutomationActionOutcome> ExecuteAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        AutomationCandidate action,
        CancellationToken cancellationToken)
    {
        EnsureCurrentContext(context);
        return Resolve(mode).ExecuteAsync(context, action, cancellationToken);
    }

    private IAutomationModePassPort Resolve(AutomationRunMode mode) => mode switch
    {
        AutomationRunMode.ContinuousLoop => continuousLoop,
        AutomationRunMode.AutoQueue => autoQueue,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown automation run mode."),
    };

    private void EnsureCurrentContext(AutomationRunContext context)
    {
        if (!string.Equals(
                context.AccountKey,
                activeAccountKey(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new AutomationContextException(
                AutomationFailureKind.AccountAccess,
                "active-account-changed",
                "The active account changed during the automation run.");
        }

        if (context.BrowserGeneration != browserGeneration())
        {
            throw new AutomationContextException(
                AutomationFailureKind.StaleBrowserGeneration,
                "browser-generation-changed",
                "The browser generation changed during the automation run.");
        }
    }
}

internal sealed class DelegateAutomationModePassPort(
    Func<CancellationToken, ValueTask<AutomationStateSnapshot>> readAsync,
    Func<AutomationCandidate, CancellationToken, ValueTask<AutomationActionOutcome>> executeAsync)
    : IAutomationModePassPort
{
    public ValueTask<AutomationStateSnapshot> ReadAsync(
        AutomationRunContext context,
        CancellationToken cancellationToken) => readAsync(cancellationToken);

    public ValueTask<AutomationActionOutcome> ExecuteAsync(
        AutomationRunContext context,
        AutomationCandidate action,
        CancellationToken cancellationToken) => executeAsync(action, cancellationToken);
}

internal sealed class EmptyAutomationStatePort : IAutomationStatePort
{
    internal static EmptyAutomationStatePort Instance { get; } = new();

    public ValueTask<AutomationStateSnapshot> ReadAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AutomationStateSnapshot([]));

    public ValueTask ApplyAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        AutomationStateChange change,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

internal sealed class EmptyOfficialTravianAutomationPort : IOfficialTravianAutomationPort
{
    internal static EmptyOfficialTravianAutomationPort Instance { get; } = new();

    public ValueTask<AutomationActionOutcome> ExecuteAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        AutomationCandidate action,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(AutomationActionOutcome.Skipped);
}
