using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services.Orchestration;

internal sealed record AutomationCandidate(
    Guid Id,
    string TaskName,
    QueueGroup Group,
    string? VillageKey,
    int Priority,
    DateTimeOffset NextAttemptAt,
    AutomationDecisionRequirement? Decision = null);

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

internal sealed class DelegateAutomationStatePort(
    Func<AutomationRunMode, AutomationRunContext, CancellationToken, ValueTask<AutomationStateSnapshot>> readAsync,
    Func<AutomationRunMode, AutomationRunContext, AutomationStateChange, CancellationToken, ValueTask> applyAsync)
    : IAutomationStatePort
{
    public ValueTask<AutomationStateSnapshot> ReadAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        CancellationToken cancellationToken) => readAsync(mode, context, cancellationToken);

    public ValueTask ApplyAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        AutomationStateChange change,
        CancellationToken cancellationToken) => applyAsync(mode, context, change, cancellationToken);
}

internal sealed class DelegateOfficialTravianAutomationPort(
    Func<AutomationRunMode, AutomationRunContext, AutomationCandidate, CancellationToken, ValueTask<AutomationActionOutcome>> executeAsync)
    : IOfficialTravianAutomationPort
{
    public ValueTask<AutomationActionOutcome> ExecuteAsync(
        AutomationRunMode mode,
        AutomationRunContext context,
        AutomationCandidate action,
        CancellationToken cancellationToken) => executeAsync(mode, context, action, cancellationToken);
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
