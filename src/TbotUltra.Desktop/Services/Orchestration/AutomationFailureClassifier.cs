using TbotUltra.Worker.Services;

namespace TbotUltra.Desktop.Services.Orchestration;

internal sealed class AutomationContextException(
    AutomationFailureKind failureKind,
    string diagnosticCode,
    string message) : InvalidOperationException(message)
{
    internal AutomationFailureKind FailureKind { get; } = failureKind;

    internal string DiagnosticCode { get; } = diagnosticCode;
}

internal static class AutomationFailureClassifier
{
    internal static AutomationFailure Classify(Exception exception)
    {
        if (exception is AutomationContextException contextException)
        {
            return new AutomationFailure(
                contextException.FailureKind,
                contextException.DiagnosticCode,
                IsRetryable: false);
        }

        if (exception is AccountAccessException)
        {
            return new AutomationFailure(
                AutomationFailureKind.AccountAccess,
                exception.GetType().Name,
                IsRetryable: false);
        }

        if (AutomationNetworkBackoff.IsTransientConnectionFailure(exception))
        {
            return new AutomationFailure(
                AutomationFailureKind.TransientNetwork,
                exception.GetType().Name,
                IsRetryable: true);
        }

        return new AutomationFailure(
            AutomationFailureKind.AdapterContract,
            exception.GetType().Name,
            IsRetryable: false);
    }
}
