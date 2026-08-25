using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BulkMessageSendOutcomeTests
{
    [Fact]
    public void Classify_MissingPlayerTakesPriorityWhileWriterRemainsVisible()
    {
        var outcome = TravianClient.ClassifyBulkMessageSendOutcome(new BulkMessageSendSnapshot(
            IsWritePage: true,
            IsWriteFormVisible: true,
            DialogText: "The name Missing Player does not exist.",
            ValidationError: null));

        Assert.Equal(BulkMessageSendOutcomeKind.MissingPlayer, outcome.Kind);
        Assert.Equal("Missing Player", outcome.Detail);
    }

    [Fact]
    public void Classify_WriterStillVisibleRemainsPending()
    {
        var outcome = TravianClient.ClassifyBulkMessageSendOutcome(new BulkMessageSendSnapshot(
            IsWritePage: true,
            IsWriteFormVisible: true,
            DialogText: null,
            ValidationError: null));

        Assert.Equal(BulkMessageSendOutcomeKind.Pending, outcome.Kind);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Classify_RequiresWriterToDisappearBeforeReportingSent(bool isWritePage, bool isWriteFormVisible)
    {
        var outcome = TravianClient.ClassifyBulkMessageSendOutcome(new BulkMessageSendSnapshot(
            isWritePage,
            isWriteFormVisible,
            DialogText: null,
            ValidationError: null));

        Assert.Equal(BulkMessageSendOutcomeKind.Sent, outcome.Kind);
    }

    [Fact]
    public void Classify_VisibleValidationErrorFailsInsteadOfReportingSent()
    {
        var outcome = TravianClient.ClassifyBulkMessageSendOutcome(new BulkMessageSendSnapshot(
            IsWritePage: true,
            IsWriteFormVisible: true,
            DialogText: null,
            ValidationError: "Recipient is invalid."));

        Assert.Equal(BulkMessageSendOutcomeKind.Error, outcome.Kind);
        Assert.Equal("Recipient is invalid.", outcome.Detail);
    }

    [Fact]
    public void Classify_UnknownDialogFailsInsteadOfReportingSent()
    {
        var outcome = TravianClient.ClassifyBulkMessageSendOutcome(new BulkMessageSendSnapshot(
            IsWritePage: true,
            IsWriteFormVisible: true,
            DialogText: "Unexpected server response.",
            ValidationError: null));

        Assert.Equal(BulkMessageSendOutcomeKind.Error, outcome.Kind);
    }

    [Fact]
    public void EmptyBatch_ReturnsWithoutAbortingLaterBatches()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectRootLocator.FindProjectRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Features",
            "TravianClient.BulkMessages.cs"));

        Assert.Contains("return currentPlayerNames;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Bulk message batch has no valid recipients left", source, StringComparison.Ordinal);
    }
}
