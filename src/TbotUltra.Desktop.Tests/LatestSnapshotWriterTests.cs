using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class LatestSnapshotWriterTests
{
    [Fact]
    public async Task Request_WritesSeriallyAndCoalescesPendingSnapshotsToTheLatest()
    {
        var firstMayFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new List<string>();
        var writer = new LatestSnapshotWriter<string>(async value =>
        {
            writes.Add(value);
            if (value == "first")
            {
                firstStarted.SetResult();
                await firstMayFinish.Task;
            }
        });

        writer.Request("first");
        await firstStarted.Task;
        writer.Request("second");
        writer.Request("latest");
        firstMayFinish.SetResult();
        await writer.WhenIdleAsync();

        Assert.Equal(["first", "latest"], writes);
    }
}
