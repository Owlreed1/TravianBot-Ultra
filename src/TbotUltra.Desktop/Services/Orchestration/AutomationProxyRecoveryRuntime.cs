namespace TbotUltra.Desktop.Services.Orchestration;

internal readonly record struct AutomationProxyRecoveryRetry(
    int Attempt,
    TimeSpan Delay,
    DateTimeOffset RetryAtUtc);

internal sealed class AutomationProxyRecoveryRuntime(TimeProvider? timeProvider = null)
{
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private bool _scheduled;
    private int _retryAttempt;
    private DateTimeOffset _retryAtUtc = DateTimeOffset.MinValue;

    internal bool TryReserve(int consecutiveFailures, int failureThreshold)
    {
        lock (_sync)
        {
            if (consecutiveFailures < failureThreshold
                || _timeProvider.GetUtcNow() < _retryAtUtc
                || _scheduled)
            {
                return false;
            }

            _scheduled = true;
            return true;
        }
    }

    internal void Release() 
    {
        lock (_sync)
        {
            _scheduled = false;
        }
    }

    internal AutomationProxyRecoveryRetry ScheduleRetry()
    {
        lock (_sync)
        {
            _retryAttempt++;
            var delay = ResolveRetryDelay(_retryAttempt);
            _retryAtUtc = _timeProvider.GetUtcNow() + delay;
            return new AutomationProxyRecoveryRetry(_retryAttempt, delay, _retryAtUtc);
        }
    }

    internal void ResetRetry()
    {
        lock (_sync)
        {
            _retryAttempt = 0;
            _retryAtUtc = DateTimeOffset.MinValue;
        }
    }

    internal static TimeSpan ResolveRetryDelay(int attempt) => attempt switch
    {
        <= 1 => TimeSpan.FromMinutes(2),
        2 => TimeSpan.FromMinutes(5),
        _ => TimeSpan.FromMinutes(10),
    };
}
