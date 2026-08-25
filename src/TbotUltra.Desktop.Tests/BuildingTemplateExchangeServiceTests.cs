using System.Text.Json;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BuildingTemplateExchangeServiceTests
{
    private readonly BuildingTemplateExchangeService _service = new();

    [Fact]
    public void ExportThenImport_PreservesShareableTemplateDataAndMetadataOnly()
    {
        var path = TempFile();
        var template = CreateTemplate("Starter", "Gauls");

        _service.Export(path, [template], "2.4.0", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        var json = File.ReadAllText(path);
        var imported = Assert.Single(_service.Import(path, "Gauls"));

        Assert.True(imported.IsValid);
        Assert.False(imported.TribeMismatch);
        Assert.Equal(template.Id, imported.Template.Id);
        Assert.Equal(template.Rows[0].Id, imported.Template.Rows[0].Id);
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"appVersion\": \"2.4.0\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("account", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("server", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("village", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("player", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseDocument_NewerSchemaRequiresAppUpdate()
    {
        var json = """
        { "schemaVersion": 99, "appVersion": "99.0", "exportedAtUtc": "2026-08-25T12:00:00Z", "templates": [] }
        """;

        var error = Assert.Throws<InvalidDataException>(() => _service.ParseDocument(json, "Romans"));

        Assert.Contains("newer Tbot Ultra version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseDocument_ReportsInvalidTemplateWithoutBlockingValidTemplate()
    {
        var valid = CreateTemplate("Valid", "Romans");
        var path = TempFile();
        _service.Export(path, [valid], "2.4.0", DateTimeOffset.UtcNow);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var validJson = document.RootElement.GetProperty("templates")[0].GetRawText();
        var json = $$"""
        {
          "schemaVersion": 1,
          "appVersion": "2.4.0",
          "exportedAtUtc": "2026-08-25T12:00:00Z",
          "templates": [
            {{validJson}},
            { "id": "00000000-0000-0000-0000-000000000000", "name": "", "createdByTribe": "Gauls", "createdAtUtc": "2026-08-25T12:00:00Z", "updatedAtUtc": "2026-08-25T12:00:00Z", "rows": [] }
          ]
        }
        """;

        var candidates = _service.ParseDocument(json, "Teutons");

        Assert.True(candidates[0].IsValid);
        Assert.True(candidates[0].TribeMismatch);
        Assert.False(candidates[1].IsValid);
        Assert.Contains(candidates[1].Errors, error => error.Contains("ID", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(candidates[1].Errors, error => error.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyImport_OverwritesByIdButKeepsLocalCreationTime()
    {
        var id = Guid.NewGuid();
        var localCreated = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var existing = CreateTemplate("Local", "Romans", id);
        existing.CreatedAtUtc = localCreated;
        var incoming = CreateTemplate("Shared update", "Gauls", id);
        var importedAt = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

        var result = _service.ApplyImport(
            [existing],
            [new BuildingTemplateImportSelection(incoming, true, BuildingTemplateImportConflictAction.Overwrite)],
            importedAt);

        var template = Assert.Single(result.Templates);
        Assert.Equal(id, template.Id);
        Assert.Equal("Shared update", template.Name);
        Assert.Equal("Gauls", template.CreatedByTribe);
        Assert.Equal(localCreated, template.CreatedAtUtc);
        Assert.Equal(importedAt, template.UpdatedAtUtc);
        Assert.Equal(1, result.OverwrittenCount);
    }

    [Fact]
    public void ApplyImport_CopyGetsNewTemplateAndRowIds()
    {
        var existing = CreateTemplate("Starter", "Romans");
        var incoming = CreateTemplate("Starter", "Romans", existing.Id);

        var result = _service.ApplyImport(
            [existing],
            [new BuildingTemplateImportSelection(incoming, true, BuildingTemplateImportConflictAction.ImportAsCopy)],
            DateTimeOffset.UtcNow);

        Assert.Equal(2, result.Templates.Count);
        var copy = result.Templates[1];
        Assert.NotEqual(existing.Id, copy.Id);
        Assert.NotEqual(incoming.Rows[0].Id, copy.Rows[0].Id);
        Assert.Equal("Starter copy", copy.Name);
        Assert.Equal(1, result.CopiedCount);
    }

    [Fact]
    public void ImportPreview_DefaultsConflictsToSafeCopyAndBlocksInvalidRows()
    {
        var template = CreateTemplate("Starter", "Romans");
        var conflict = new BuildingTemplateImportRowView(
            new BuildingTemplateImportCandidate(template, [], false),
            hasConflict: true);
        var invalid = new BuildingTemplateImportRowView(
            new BuildingTemplateImportCandidate(template, ["Broken row."], false),
            hasConflict: false);

        Assert.True(conflict.IsSelected);
        Assert.Equal(BuildingTemplateImportConflictAction.ImportAsCopy, conflict.SelectedConflictChoice.Action);
        Assert.False(invalid.IsSelected);
        Assert.Equal("Blocked", invalid.ActionText);
    }

    private static BuildingTemplate CreateTemplate(string name, string tribe, Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            CreatedByTribe = tribe,
            CreatedAtUtc = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero),
            Rows =
            [
                new BuildingTemplateRow
                {
                    Id = Guid.NewGuid(),
                    Kind = BuildingTemplateRowKind.Building,
                    Gid = 15,
                    BuildingName = "Main Building",
                    PreferredSlotId = 19,
                    TargetLevel = 3,
                    ResourceScope = "all",
                    ResourceStrategy = "lowest",
                },
            ],
        };

    private static string TempFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tbot-ultra-template-exchange-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"templates{BuildingTemplateExchangeService.FileExtension}");
    }
}
