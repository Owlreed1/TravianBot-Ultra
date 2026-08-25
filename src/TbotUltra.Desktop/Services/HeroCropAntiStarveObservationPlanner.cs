namespace TbotUltra.Desktop.Services;

public enum HeroCropAntiStarveObservationAction
{
    NoObservation,
    Cancel,
    Schedule,
    QueueNow,
}

public readonly record struct HeroCropAntiStarveObservationDecision(
    HeroCropAntiStarveObservationAction Action,
    TimeSpan Delay);

public static class HeroCropAntiStarveObservationPlanner
{
    public static HeroCropAntiStarveObservationDecision Evaluate(
        double? productionPerHour,
        int? secondsToEmpty,
        int triggerMinutes)
    {
        if (productionPerHour is null)
        {
            return new(HeroCropAntiStarveObservationAction.NoObservation, TimeSpan.Zero);
        }

        if (productionPerHour.Value >= 0)
        {
            return new(HeroCropAntiStarveObservationAction.Cancel, TimeSpan.Zero);
        }

        if (secondsToEmpty is null)
        {
            return new(HeroCropAntiStarveObservationAction.QueueNow, TimeSpan.Zero);
        }

        var delaySeconds = Math.Max(0, secondsToEmpty.Value - Math.Max(1, triggerMinutes) * 60);
        return delaySeconds == 0
            ? new(HeroCropAntiStarveObservationAction.QueueNow, TimeSpan.Zero)
            : new(HeroCropAntiStarveObservationAction.Schedule, TimeSpan.FromSeconds(delaySeconds));
    }
}
