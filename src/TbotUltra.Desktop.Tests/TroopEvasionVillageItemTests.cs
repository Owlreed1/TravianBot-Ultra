using TbotUltra.Desktop.Models;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TroopEvasionVillageItemTests
{
    [Fact]
    public void CopyTroopSelectionFrom_CopiesEverySlotAndHeroButNotEnabledState()
    {
        var source = Create("source", enabled: true, selectedSlots: [2, 7], includeHero: false);
        var target = Create("target", enabled: false, selectedSlots: [1, 3, 10], includeHero: true);

        target.CopyTroopSelectionFrom(source);

        Assert.Equal([2, 7], target.Units.Where(unit => unit.IsSelected).Select(unit => unit.Slot));
        Assert.False(target.IncludeHero);
        Assert.False(target.Enabled);
    }

    private static TroopEvasionVillageItem Create(string key, bool enabled, int[] selectedSlots, bool includeHero)
    {
        var item = new TroopEvasionVillageItem { VillageKey = key, VillageName = key, Enabled = enabled, IncludeHero = includeHero };
        for (var slot = 1; slot <= 10; slot++)
        {
            item.Units.Add(new TroopEvasionUnitItem(slot, $"Unit {slot}", selectedSlots.Contains(slot)));
        }
        return item;
    }
}
