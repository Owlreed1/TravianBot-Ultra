using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationGroupAvailabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Farming_CannotBeToggledWithoutConfirmedGoldClub(bool? goldClubEnabled)
    {
        var state = AutomationGroupAvailability.Resolve(
            QueueGroup.Farming,
            isCapital: false,
            goldClubEnabled,
            requestedEnabled: true);

        Assert.False(state.CanToggle);
        Assert.False(state.IsEnabled);
    }

    [Fact]
    public void Farming_CanBeToggledWithGoldClub()
    {
        Assert.True(AutomationGroupAvailability.CanToggle(
            QueueGroup.Farming,
            isCapital: false,
            goldClubEnabled: true));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Brewery_RemainsCapitalOnly(bool isCapital, bool expected)
    {
        Assert.Equal(expected, AutomationGroupAvailability.CanToggle(
            QueueGroup.BreweryCelebration,
            isCapital,
            goldClubEnabled: true));
    }
}
