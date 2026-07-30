using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class UpdateNotificationPreviewSourceTests
{
    [Fact]
    public void DebugPreview_UsesFixedVersionsWithoutPersistingAnAcknowledgement()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Resources.Actions.cs"));
        var methodStart = source.IndexOf(
            "private void UpdateVersionPreviewButton_Click",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    private void BulkMessagesButton_Click",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];
        Assert.Contains("new UpdateAvailableWindow(\"1.0.0\", \"1.1.0\")", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("AcknowledgeUpdateVersion", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenVersionWindow", methodBody, StringComparison.Ordinal);
    }
}
