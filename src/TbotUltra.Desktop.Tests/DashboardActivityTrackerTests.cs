using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class DashboardActivityTrackerTests
{
    [Fact]
    public void NestedScopes_RestoreOuterActivity()
    {
        var tracker = new DashboardActivityTracker();

        using (tracker.Begin("Village scan"))
        {
            Assert.Equal("Village scan", tracker.Current);
            using (tracker.Begin("Village scan (3/7): H03"))
            {
                Assert.Equal("Village scan (3/7): H03", tracker.Current);
            }

            Assert.Equal("Village scan", tracker.Current);
        }

        Assert.Null(tracker.Current);
    }

    [Fact]
    public void OlderScopeDisposedOutOfOrder_DoesNotClearNewerActivity()
    {
        var tracker = new DashboardActivityTracker();
        var older = tracker.Begin("Resource refresh");
        using var newer = tracker.Begin("Village scan");

        older.Dispose();

        Assert.Equal("Village scan", tracker.Current);
    }
}
