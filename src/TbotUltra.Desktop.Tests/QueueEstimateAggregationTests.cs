using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class QueueEstimateAggregationTests
{
    [Fact]
    public void SumSeconds_MatchesTheSelectedVillageQueueTotal()
    {
        var rows = new[]
        {
            Row(44 * 60 + 16), Row(1 * 60 * 60 + 44 * 60), Row(4 * 60 * 60 + 24 * 60),
            Row(3 * 60 * 60 + 15 * 60), Row(4 * 60 * 60 + 16 * 60), Row(5 * 60 * 60),
            new QueueItemRow { Group = QueueGroup.Hero, Status = QueueStatus.Pending },
        };

        Assert.Equal(19 * 60 * 60 + 23 * 60 + 16, QueueEstimateAggregation.SumSeconds(rows));
    }

    private static QueueItemRow Row(double seconds) => new()
    {
        Group = QueueGroup.Construction,
        Status = QueueStatus.Pending,
        HasEstimate = true,
        EstimateSeconds = seconds,
    };
}
