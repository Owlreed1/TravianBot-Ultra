using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class IncomingAttackObservationPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnchangedSignal_DoesNotReadAgainAfterOneMinute()
    {
        var arrivals = new[] { Now.AddMinutes(20), Now.AddMinutes(21) };
        var signal = new IncomingAttackSignal("BRE", Dorf1ArrivalTimesUtc: arrivals);
        var attacks = arrivals.Select((arrival, index) => new IncomingAttack($"a{index}", "BRE", arrival)).ToList();

        var shouldRead = IncomingAttackObservationPolicy.ShouldReadDetails(
            signal, null, attacks, Now.AddMinutes(-1), Now);

        Assert.False(shouldRead);
    }

    [Fact]
    public void ChangedArrivalSet_ReadsImmediately()
    {
        var knownArrival = Now.AddMinutes(20);
        var signal = new IncomingAttackSignal(
            "BRE",
            Dorf1ArrivalTimesUtc: [knownArrival, Now.AddMinutes(21)]);
        var attacks = new[] { new IncomingAttack("a", "BRE", knownArrival) };

        var shouldRead = IncomingAttackObservationPolicy.ShouldReadDetails(
            signal, null, attacks, Now.AddMinutes(-1), Now);

        Assert.True(shouldRead);
    }

    [Fact]
    public void UnchangedFallbackSignal_DoesNotRetryOnEveryDorf1Observation()
    {
        var arrivals = new[] { Now.AddMinutes(20), Now.AddMinutes(21) };
        var signal = new IncomingAttackSignal("BRE", Dorf1ArrivalTimesUtc: arrivals);

        var shouldRead = IncomingAttackObservationPolicy.ShouldReadDetails(
            signal, signal, [], Now.AddMinutes(-1), Now);

        Assert.False(shouldRead);
    }

    [Fact]
    public void UnchangedSignal_ReceivesTenMinuteSafetyRefresh()
    {
        var arrival = Now.AddMinutes(20);
        var signal = new IncomingAttackSignal("BRE", Dorf1ArrivalTimesUtc: [arrival]);
        var attacks = new[] { new IncomingAttack("a", "BRE", arrival) };

        var shouldRead = IncomingAttackObservationPolicy.ShouldReadDetails(
            signal, null, attacks, Now.AddMinutes(-10), Now);

        Assert.True(shouldRead);
    }
}
