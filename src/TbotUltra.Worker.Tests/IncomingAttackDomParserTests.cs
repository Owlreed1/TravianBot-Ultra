using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class IncomingAttackDomParserTests
{
    [Fact]
    public void ParseDorf1Signals_DetectsActiveAndPlusOverviewVillages()
    {
        const string html = """
            <div class="villageInfobox movements"><table id="movements"><tr><td><img class="att1"></td><td>1 Attack</td></tr></table></div>
            <div class="listEntry village attack" data-did="39762">
              <span class="name" data-did="39762">A4</span>
              <span class="coordinateX">(122</span><span class="coordinateY">15)</span>
            </div>
            <div class="listEntry village" data-did="37166"><span class="name">A1</span></div>
            """;
        var observedAt = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);

        var signals = IncomingAttackDomParser.ParseDorf1Signals(
            html, "SWOLL", "dorf1.php?newdid=34875", 120, 14, observedAt);

        Assert.Equal(2, signals.Count);
        Assert.Contains(signals, signal => signal.VillageName == "SWOLL" && signal.VillageId == 34875);
        Assert.Contains(signals, signal => signal.VillageName == "A4" && signal.VillageId == 39762
                                                   && signal.CoordX == 122 && signal.CoordY == 15);
    }

    [Fact]
    public void ParseIncomingAttacks_ReadsRaidIdentitySourceAndArrivalAcrossMidnight()
    {
        const string html = """
            <table class="troop_details inRaid">
              <thead><tr>
                <td class="role"><a href="/karte.php?d=74907">Enemy player</a></td>
                <td class="troopHeadline"><a class="markAttack"><img id="markSymbol_27254877"></a><a href="/karte.php?d=74508">Enemy village</a></td>
              </tr></thead>
              <tbody>
                <tr><th class="coords"><span class="coordinateX">(-12</span>|<span class="coordinateY">34)</span></th></tr>
                <tr><th>Arrival</th><td><div class="in">in <span class="timer" value="120" data-value="120">0:02:00</span></div><div class="at">at 00:01:00</div></td></tr>
              </tbody>
            </table>
            """;
        var observedAt = new DateTimeOffset(2026, 8, 22, 23, 59, 0, TimeSpan.Zero);

        var attacks = IncomingAttackDomParser.ParseIncomingAttacks(
            html, "A4", "xy:122|15", 122, 15, observedAt);

        var attack = Assert.Single(attacks);
        Assert.Equal("27254877", attack.Id);
        Assert.Equal(IncomingAttackMovementType.Raid, attack.MovementType);
        Assert.Equal("Enemy player", attack.SourcePlayerName);
        Assert.Equal("Enemy village", attack.SourceVillageName);
        Assert.Equal(-12, attack.SourceCoordX);
        Assert.Equal(34, attack.SourceCoordY);
        Assert.Equal(new DateTimeOffset(2026, 8, 23, 0, 1, 0, TimeSpan.Zero), attack.ArrivalAtUtc);
    }

    [Fact]
    public void ParseIncomingAttacks_UsesStableFallbackWhenMovementIdIsMissing()
    {
        const string html = """
            <table class="troop_details inAttack"><tbody><tr><td><span class="timer" data-value="60">0:01:00</span></td></tr></tbody></table>
            """;
        var observedAt = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

        var first = Assert.Single(IncomingAttackDomParser.ParseIncomingAttacks(html, "A4", "xy:1|2", 1, 2, observedAt));
        var second = Assert.Single(IncomingAttackDomParser.ParseIncomingAttacks(html, "A4", "xy:1|2", 1, 2, observedAt));

        Assert.StartsWith("fallback:", first.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(IncomingAttackMovementType.Attack, first.MovementType);
    }

    [Fact]
    public void HasOnlyIncomingFilterActive_RejectsMultipleActiveFilters()
    {
        const string correct = """
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory1"></button>
            <button class="iconFilter"><img class="filterCategory subFilterCategory2"></button>
            <button class="iconFilter"><img class="filterCategory subFilterCategory3"></button>
            """;
        const string incorrect = """
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory1"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory2"></button>
            """;

        Assert.True(IncomingAttackDomParser.HasOnlyIncomingFilterActive(correct));
        Assert.False(IncomingAttackDomParser.HasOnlyIncomingFilterActive(incorrect));
    }
}
