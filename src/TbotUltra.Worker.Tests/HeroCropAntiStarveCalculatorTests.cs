using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class HeroCropAntiStarveCalculatorTests
{
    [Fact]
    public void Calculate_NeverUsesCropBelowTheAbsoluteHeroReserve()
    {
        var result = HeroCropAntiStarveCalculator.Calculate(
            currentCrop: 100,
            granaryCapacity: 20_000,
            productionPerHour: -10_000,
            heroCrop: 6_000,
            triggerMinutes: 30,
            targetMinutes: 90,
            maxCropPerTransfer: 10_000,
            minHeroCropRemaining: 5_000);

        Assert.True(result.IsRequired);
        Assert.Equal(1_000, result.TransferAmount);
        Assert.True(result.IsPartial);
    }

    [Fact]
    public void Calculate_CapsAtPerTransferMaximumAndGranaryFreeSpace()
    {
        var maxLimited = HeroCropAntiStarveCalculator.Calculate(
            0, 50_000, -20_000, 50_000, 30, 90, 10_000, 5_000);
        var granaryLimited = HeroCropAntiStarveCalculator.Calculate(
            900, 1_000, -2_000, 50_000, 30, 90, 10_000, 5_000);

        Assert.Equal(10_000, maxLimited.TransferAmount);
        Assert.Equal(100, granaryLimited.TransferAmount);
    }

    [Theory]
    [InlineData(100, 100, false)]
    [InlineData(-100, 10_000, false)]
    [InlineData(-100, 10, true)]
    public void Calculate_RequiresNegativeProductionBelowTrigger(
        double production,
        long currentCrop,
        bool expectedRequired)
    {
        var result = HeroCropAntiStarveCalculator.Calculate(
            currentCrop, 50_000, production, 50_000, 30, 90, 10_000, 5_000);

        Assert.Equal(expectedRequired, result.IsRequired);
    }

    [Theory]
    [InlineData(300, 150)]
    [InlineData(599, 299)]
    [InlineData(60, 30)]
    [InlineData(10, 30)]
    public void ResolvePostTransferCheckSeconds_UsesEmergencyHalfEta(int eta, int expected)
    {
        Assert.Equal(expected, HeroCropAntiStarveCalculator.ResolvePostTransferCheckSeconds(eta));
    }

    [Theory]
    [InlineData(5395, 90, false)]
    [InlineData(5340, 90, false)]
    [InlineData(5339, 90, true)]
    public void IsPostTransferEtaShortfallActionable_AllowsOneMinuteObservationTolerance(
        int etaSeconds,
        int targetMinutes,
        bool expected)
    {
        Assert.Equal(
            expected,
            HeroCropAntiStarveCalculator.IsPostTransferEtaShortfallActionable(etaSeconds, targetMinutes));
    }
}
