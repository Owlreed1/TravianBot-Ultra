using TbotUltra.Desktop.Models;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BuildingTemplateDuplicationTests
{
    [Fact]
    public void CreateDuplicateTemplate_CopiesCurrentRowsWithNewIdsAndUniqueName()
    {
        var source = new BuildingTemplate
        {
            Name = "Starter",
            CreatedByTribe = "Romans",
            Rows = [new BuildingTemplateRow { BuildingName = "Old row" }],
        };
        var currentRow = new BuildingTemplateRow
        {
            Kind = BuildingTemplateRowKind.Building,
            Gid = 10,
            BuildingName = "Warehouse",
            PreferredSlotId = 19,
            TargetLevel = 6,
        };
        var existingCopy = new BuildingTemplate { Name = "Starter copy" };

        var duplicate = BuildingTemplatesWindow.CreateDuplicateTemplate(
            source,
            [currentRow],
            [source, existingCopy]);

        Assert.Equal("Starter copy 2", duplicate.Name);
        Assert.Equal("Romans", duplicate.CreatedByTribe);
        Assert.NotEqual(source.Id, duplicate.Id);
        var copiedRow = Assert.Single(duplicate.Rows);
        Assert.Equal("Warehouse", copiedRow.BuildingName);
        Assert.Equal(6, copiedRow.TargetLevel);
        Assert.NotEqual(currentRow.Id, copiedRow.Id);
        Assert.NotSame(currentRow, copiedRow);
    }
}
