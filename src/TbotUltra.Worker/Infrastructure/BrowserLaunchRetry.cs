using Microsoft.Playwright;

namespace TbotUltra.Worker.Infrastructure;

// Isolated launch seam: browser startup can fail before an IBrowser exists, so TravianClient's page-level
// retry policy cannot handle it. Kept separate so the exact launch failure can be replayed deterministically.
internal static class BrowserLaunchRetry
{
    internal static Task<T> RunAsync<T>(
        Func<Task<T>> launch,
        Action<string>? log = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        CancellationToken cancellationToken = default)
        => RunCoreAsync(launch, log, delay ?? Task.Delay, cancellationToken);

    private static async Task<T> RunCoreAsync<T>(
        Func<Task<T>> launch,
        Action<string>? log,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await launch();
        }
        catch (PlaywrightException ex) when (IsTransientEarlyClosure(ex))
        {
            log?.Invoke($"[browser] system browser closed during startup ({ex.GetType().Name}); retrying once.");
            await delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return await launch();
        }
    }

    private static bool IsTransientEarlyClosure(PlaywrightException exception)
        => string.Equals(exception.GetType().Name, "TargetClosedException", StringComparison.Ordinal)
            || exception.Message.Contains(
                "Target page, context or browser has been closed",
                StringComparison.OrdinalIgnoreCase);
}
