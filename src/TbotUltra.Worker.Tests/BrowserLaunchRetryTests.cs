using Microsoft.Playwright;
using TbotUltra.Worker.Infrastructure;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BrowserLaunchRetryTests
{
    [Fact]
    public async Task RetriesOnceWhenSystemBrowserClosesDuringLaunch()
    {
        var attempts = 0;
        var messages = new List<string>();

        var result = await BrowserLaunchRetry.RunAsync(
            () => ++attempts == 1
                ? Task.FromException<string>(new PlaywrightException("Target page, context or browser has been closed"))
                : Task.FromResult("opened"),
            messages.Add,
            (_, _) => Task.CompletedTask);

        Assert.Equal("opened", result);
        Assert.Equal(2, attempts);
        Assert.Contains(messages, message => message.Contains("retrying", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DoesNotRetryAnUnrelatedPlaywrightFailure()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<PlaywrightException>(() => BrowserLaunchRetry.RunAsync<string>(
            () =>
            {
                attempts++;
                return Task.FromException<string>(new PlaywrightException("Executable does not exist"));
            },
            delay: (_, _) => Task.CompletedTask));

        Assert.Equal("Executable does not exist", exception.Message);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task CancellationDuringBackoffPreventsTheSecondLaunch()
    {
        var attempts = 0;
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BrowserLaunchRetry.RunAsync<string>(
            () =>
            {
                attempts++;
                return Task.FromException<string>(new PlaywrightException("Target page, context or browser has been closed"));
            },
            delay: (_, token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(token);
            },
            cancellationToken: cancellation.Token));

        Assert.Equal(1, attempts);
    }
}
