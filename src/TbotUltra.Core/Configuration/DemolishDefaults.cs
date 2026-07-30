namespace TbotUltra.Core.Configuration;

public static class DemolishDefaults
{
    public const int DefaultDelayMinMinutes = 1;
    public const int DefaultDelayMaxMinutes = 10;

    public static TimeSpan CalculateDelay(
        int minMinutes,
        int maxMinutes,
        Func<int, int, int>? randomSeconds = null)
    {
        var firstSeconds = Math.Max(0, minMinutes) * 60;
        var secondSeconds = Math.Max(0, maxMinutes) * 60;
        var minSeconds = Math.Min(firstSeconds, secondSeconds);
        var maxSeconds = Math.Max(firstSeconds, secondSeconds);
        var seconds = (randomSeconds ?? Random.Shared.Next)(minSeconds, maxSeconds + 1);
        return TimeSpan.FromSeconds(seconds);
    }
}
