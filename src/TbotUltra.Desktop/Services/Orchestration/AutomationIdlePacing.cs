using TbotUltra.Core.Configuration;

namespace TbotUltra.Desktop.Services.Orchestration;

internal readonly record struct AutomationIdleBreakPlan(bool ShouldTakeBreak, int DurationSeconds);

internal readonly record struct AutomationIdleBrowsePlan(string? Page, bool NoPageSelected)
{
    internal bool ShouldBrowse => !string.IsNullOrWhiteSpace(Page);
}

internal sealed class AutomationIdlePacing(
    TimeProvider? timeProvider = null,
    Func<double>? nextDouble = null,
    Func<int, int, int>? nextInt = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Func<double> _nextDouble = nextDouble ?? Random.Shared.NextDouble;
    private readonly Func<int, int, int> _nextInt = nextInt ?? Random.Shared.Next;
    private DateTimeOffset _nextBreakDueUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _nextBrowseDueUtc = DateTimeOffset.MinValue;

    internal void Reset()
    {
        _nextBreakDueUtc = DateTimeOffset.MinValue;
        _nextBrowseDueUtc = DateTimeOffset.MinValue;
    }

    internal AutomationIdleBreakPlan PlanBreak(BotOptions options, bool sessionAvailable)
    {
        if (!options.ActionPacingIdleBreakEnabled)
        {
            return default;
        }

        var now = _timeProvider.GetUtcNow();
        if (_nextBreakDueUtc == DateTimeOffset.MinValue)
        {
            ScheduleNextBreak(now, options);
            return default;
        }
        if (now < _nextBreakDueUtc)
        {
            return default;
        }
        if (!sessionAvailable)
        {
            ScheduleNextBreak(now, options);
            return default;
        }

        var durationMinutes = RandomMinutes(
            options.ActionPacingIdleBreakDurationMinMinutes,
            options.ActionPacingIdleBreakDurationMaxMinutes);
        return new AutomationIdleBreakPlan(
            ShouldTakeBreak: true,
            Math.Max(1, (int)Math.Round(durationMinutes * 60)));
    }

    internal void CompleteBreak(BotOptions options) =>
        ScheduleNextBreak(_timeProvider.GetUtcNow(), options);

    internal AutomationIdleBrowsePlan PlanBrowse(BotOptions options, bool sessionAvailable)
    {
        if (!options.ActionPacingIdleBrowseEnabled)
        {
            return default;
        }

        var now = _timeProvider.GetUtcNow();
        if (_nextBrowseDueUtc == DateTimeOffset.MinValue)
        {
            ScheduleNextBrowse(now, options);
            return default;
        }
        if (now < _nextBrowseDueUtc)
        {
            return default;
        }
        if (!sessionAvailable)
        {
            ScheduleNextBrowse(now, options);
            return default;
        }

        var pages = GetEnabledBrowsePages(options);
        if (pages.Count == 0)
        {
            ScheduleNextBrowse(now, options);
            return new AutomationIdleBrowsePlan(null, NoPageSelected: true);
        }

        return new AutomationIdleBrowsePlan(
            pages[_nextInt(0, pages.Count)],
            NoPageSelected: false);
    }

    internal void CompleteBrowse(BotOptions options) =>
        ScheduleNextBrowse(_timeProvider.GetUtcNow(), options);

    internal static IReadOnlyList<string> GetEnabledBrowsePages(BotOptions options)
    {
        var pages = new List<string>(8);
        if (options.ActionPacingIdleBrowsePageMap) pages.Add("karte.php");
        if (options.ActionPacingIdleBrowsePageStatistics) pages.Add("/statistics/general");
        if (options.ActionPacingIdleBrowsePageStatisticsHero) pages.Add("/statistics/hero");
        if (options.ActionPacingIdleBrowsePageStatisticsTop10) pages.Add("/statistics/player/top10");
        if (options.ActionPacingIdleBrowsePageStatisticsDefenders) pages.Add("/statistics/player/defenders");
        if (options.ActionPacingIdleBrowsePageStatisticsAttackers) pages.Add("/statistics/player/attackers");
        if (options.ActionPacingIdleBrowsePageReports) pages.Add("berichte.php");
        if (options.ActionPacingIdleBrowsePageMessages) pages.Add("nachrichten.php");
        return pages;
    }

    internal static bool RequiresStatisticsLandingPage(string page) =>
        page.StartsWith("/statistics/", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(page, "/statistics", StringComparison.OrdinalIgnoreCase);

    private void ScheduleNextBreak(DateTimeOffset now, BotOptions options)
    {
        var minutes = RandomMinutes(
            options.ActionPacingIdleBreakIntervalMinMinutes,
            options.ActionPacingIdleBreakIntervalMaxMinutes);
        _nextBreakDueUtc = now.AddSeconds(Math.Max(5.0, minutes * 60.0));
    }

    private void ScheduleNextBrowse(DateTimeOffset now, BotOptions options)
    {
        var minutes = RandomMinutes(
            options.ActionPacingIdleBrowseIntervalMinMinutes,
            options.ActionPacingIdleBrowseIntervalMaxMinutes);
        _nextBrowseDueUtc = now.AddSeconds(Math.Max(5.0, minutes * 60.0));
    }

    private double RandomMinutes(double min, double max)
    {
        var lo = Math.Max(0, min);
        var hi = Math.Max(lo, max);
        return lo + (_nextDouble() * (hi - lo));
    }
}
