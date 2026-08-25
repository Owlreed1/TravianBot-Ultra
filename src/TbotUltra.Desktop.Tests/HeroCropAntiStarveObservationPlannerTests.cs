using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class HeroCropAntiStarveObservationPlannerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(125)]
    public void Evaluate_CancelsMonitoringWhenProductionIsNotNegative(double production)
    {
        var result = HeroCropAntiStarveObservationPlanner.Evaluate(production, 60, 30);

        Assert.Equal(HeroCropAntiStarveObservationAction.Cancel, result.Action);
    }

    [Fact]
    public void Evaluate_DoesNotReplaceAValidPlanFromAnIncompleteObservation()
    {
        var result = HeroCropAntiStarveObservationPlanner.Evaluate(null, null, 30);

        Assert.Equal(HeroCropAntiStarveObservationAction.NoObservation, result.Action);
    }

    [Fact]
    public void Evaluate_SchedulesLocallyUntilTheTriggerWindow()
    {
        var result = HeroCropAntiStarveObservationPlanner.Evaluate(-100, 7200, 30);

        Assert.Equal(HeroCropAntiStarveObservationAction.Schedule, result.Action);
        Assert.Equal(TimeSpan.FromMinutes(90), result.Delay);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1800)]
    [InlineData(120)]
    public void Evaluate_QueuesOneConfirmationWhenTheVillageMayBeAtRisk(int? secondsToEmpty)
    {
        var result = HeroCropAntiStarveObservationPlanner.Evaluate(-100, secondsToEmpty, 30);

        Assert.Equal(HeroCropAntiStarveObservationAction.QueueNow, result.Action);
    }
}
