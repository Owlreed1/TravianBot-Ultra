using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class UpdateNotificationDecisionsTests
{
    [Fact]
    public void ShouldShow_OnlyAcceptsAnUnacknowledgedAvailableRelease()
    {
        var release = new UpdateChecker.ReleaseInfo("1.1.0", "https://example.test/release", null, null);
        var available = new UpdateChecker.UpdateStatus("1.0.0", release, UpdateAvailable: true);

        Assert.True(UpdateNotificationDecisions.ShouldShow(available, false, null, false));
        Assert.False(UpdateNotificationDecisions.ShouldShow(available, true, null, false));
        Assert.False(UpdateNotificationDecisions.ShouldShow(available, false, "1.1.0", false));
        Assert.False(UpdateNotificationDecisions.ShouldShow(available, false, "1.1.0", true));
        Assert.True(UpdateNotificationDecisions.ShouldShow(available, false, "1.0.9", false));
    }

    [Fact]
    public void ShouldShow_RejectsUnknownOrCurrentRelease()
    {
        var release = new UpdateChecker.ReleaseInfo("1.0.0", "https://example.test/release", null, null);

        Assert.False(UpdateNotificationDecisions.ShouldShow(null, false, null, false));
        Assert.False(UpdateNotificationDecisions.ShouldShow(
            new UpdateChecker.UpdateStatus("1.0.0", release, UpdateAvailable: false),
            false,
            null,
            false));
    }
}
