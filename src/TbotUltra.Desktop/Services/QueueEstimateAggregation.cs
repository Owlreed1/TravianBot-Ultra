using TbotUltra.Desktop.Models;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

/// <summary>
/// Shared aggregation rules for the program queue's construction estimates.
/// </summary>
public static class QueueEstimateAggregation
{
    public static bool CountsTowardTotal(QueueItemRow row) =>
        row.HasEstimate
        && row.Status is QueueStatus.Pending or QueueStatus.Running or QueueStatus.Paused;

    public static double SumSeconds(IEnumerable<QueueItemRow> rows) =>
        rows.Where(CountsTowardTotal).Sum(row => row.EstimateSeconds);
}
