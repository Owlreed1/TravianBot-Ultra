using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BuildingOverviewDomParserTests
{
    [Fact]
    public void Parse_EmptyBuildingLink_IsNotOccupancyEvidence()
    {
        var scan = BuildingOverviewDomParser.Parse(
        [
            new BuildingOverviewSlotSnapshot
            {
                ClassName = "buildingSlot a31 aid31 egyptian",
                OuterHtml = "<div class='buildingSlot a31 aid31 egyptian' data-aid='31'><a href='/build.php?id=31' class='emptyBuildingSlot'></a></div>",
                OccupiedEvidence = true,
            },
        ]);

        var slot = Assert.Single(scan.Buildings).Value;
        Assert.Equal(31, slot.SlotId);
        Assert.Equal("Empty", slot.BuildingName);
        Assert.False(slot.HasOccupancyEvidence);
        Assert.Equal(0, slot.Level);
    }
}
