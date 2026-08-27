using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BuildingTemplateStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var root = TempRoot();
        var store = new BuildingTemplateStore(root);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void SaveThenLoad_PreservesTemplates()
    {
        var root = TempRoot();
        var store = new BuildingTemplateStore(root);
        var template = new BuildingTemplate
        {
            Name = "Starter",
            CreatedByTribe = "Teutons",
            Rows =
            [
                new BuildingTemplateRow
                {
                    Kind = BuildingTemplateRowKind.Building,
                    Gid = 15,
                    BuildingName = "Main Building",
                    PreferredSlotId = 19,
                    TargetLevel = 3,
                },
                new BuildingTemplateRow
                {
                    Kind = BuildingTemplateRowKind.AllResources,
                    BuildingName = "All Woodcutters",
                    ResourceScope = "wood",
                    TargetLevel = 99,
                },
            ],
        };

        store.Save([template]);
        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal("Starter", loaded[0].Name);
        Assert.Equal("Teutons", loaded[0].CreatedByTribe);
        Assert.Equal(2, loaded[0].Rows.Count);
        Assert.Equal(15, loaded[0].Rows[0].Gid);
        Assert.Equal("wood", loaded[0].Rows[1].ResourceScope);
        Assert.Equal(20, loaded[0].Rows[1].TargetLevel);
        Assert.True(File.Exists(Path.Combine(root, "building_templates", "building_templates.json")));
    }

    [Fact]
    public void Save_WritesEachTemplateAsNamedShareableFile()
    {
        var root = TempRoot();
        var store = new BuildingTemplateStore(root);
        var first = new BuildingTemplate { Name = "Starter plan", CreatedByTribe = "Teutons" };
        var second = new BuildingTemplate { Name = "Starter plan", CreatedByTribe = "Gauls" };

        store.Save([first, second]);

        var directory = Path.Combine(root, "building_templates");
        var firstPath = Path.Combine(directory, "Starter plan.tbot-template.json");
        var secondPath = Path.Combine(directory, "Starter plan (2).tbot-template.json");
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
        Assert.Equal(first.Id, Assert.Single(new BuildingTemplateExchangeService().Import(firstPath, "Teutons")).Template.Id);
        Assert.Equal(second.Id, Assert.Single(new BuildingTemplateExchangeService().Import(secondPath, "Gauls")).Template.Id);
    }

    [Fact]
    public void Load_ExistingLibrary_CreatesMissingIndividualFiles()
    {
        var root = TempRoot();
        var store = new BuildingTemplateStore(root);
        store.Save([new BuildingTemplate { Name = "Existing", CreatedByTribe = "Romans" }]);
        var directory = Path.Combine(root, "building_templates");
        File.Delete(Path.Combine(directory, "Existing.tbot-template.json"));
        File.Delete(Path.Combine(directory, "building_templates.manifest.json"));

        var loaded = new BuildingTemplateStore(root).Load();

        Assert.Single(loaded);
        Assert.True(File.Exists(Path.Combine(directory, "Existing.tbot-template.json")));
    }

    [Fact]
    public void Save_RenameAndDelete_CleansOnlyManagedFiles()
    {
        var root = TempRoot();
        var store = new BuildingTemplateStore(root);
        var kept = new BuildingTemplate { Name = "First", CreatedByTribe = "Romans" };
        var removed = new BuildingTemplate { Name = "Second", CreatedByTribe = "Romans" };
        store.Save([kept, removed]);
        var directory = store.DirectoryPath;
        var manualPath = Path.Combine(directory, "Manual backup.tbot-template.json");
        new BuildingTemplateExchangeService().Export(
            manualPath,
            [new BuildingTemplate { Name = "Manual", CreatedByTribe = "Romans" }],
            "test",
            DateTimeOffset.UtcNow);

        kept.Name = "Renamed";
        store.Save([kept]);

        Assert.True(File.Exists(Path.Combine(directory, "Renamed.tbot-template.json")));
        Assert.False(File.Exists(Path.Combine(directory, "First.tbot-template.json")));
        Assert.False(File.Exists(Path.Combine(directory, "Second.tbot-template.json")));
        Assert.True(File.Exists(manualPath));
    }

    [Theory]
    [InlineData("Bad/name", "Bad-name")]
    [InlineData("CON", "_CON")]
    [InlineData("...", "building-template")]
    public void SanitizeTemplateFileName_ProducesSafeReadableName(string name, string expected)
    {
        Assert.Equal(expected, BuildingTemplateStore.SanitizeTemplateFileName(name));
    }

    [Fact]
    public void Load_MigratesLegacyConfigFileWithoutDeletingIt()
    {
        var root = TempRoot();
        var legacyPath = Path.Combine(root, "config", "building_templates.json");
        new BuildingTemplateStore(legacyPath, useExactPath: true).Save(
            [new BuildingTemplate { Name = "Legacy", CreatedByTribe = "Gauls" }]);

        var loaded = new BuildingTemplateStore(root).Load();

        Assert.Equal("Legacy", Assert.Single(loaded).Name);
        Assert.True(File.Exists(legacyPath));
        Assert.True(File.Exists(Path.Combine(root, "building_templates", "building_templates.json")));
    }

    [Fact]
    public async Task LoadAsync_PreservesTemplates()
    {
        var root = TempRoot();
        var store = new BuildingTemplateStore(root);
        store.Save([new BuildingTemplate { Name = "Starter", CreatedByTribe = "Teutons" }]);

        var loaded = await store.LoadAsync();

        var template = Assert.Single(loaded);
        Assert.Equal("Starter", template.Name);
    }

    [Fact]
    public void Load_CorruptJson_QuarantinesFileBeforeReturningEmpty()
    {
        var root = TempRoot();
        var templatesDirectory = Path.Combine(root, "building_templates");
        Directory.CreateDirectory(templatesDirectory);
        File.WriteAllText(Path.Combine(templatesDirectory, "building_templates.json"), "{broken");
        var store = new BuildingTemplateStore(root);

        Assert.Empty(store.Load());
        Assert.NotNull(store.LastLoadWarning);
        Assert.False(File.Exists(Path.Combine(templatesDirectory, "building_templates.json")));
        Assert.Single(Directory.GetFiles(templatesDirectory, "building_templates.json.corrupt-*"));
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "tbot-ultra-building-template-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
