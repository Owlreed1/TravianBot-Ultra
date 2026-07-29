namespace TbotUltra.Worker.Domain;

public sealed record TravcoRow(
    double? Distance,
    string Account,
    string Village,
    long? Pop,
    string Coordinates);

public sealed record TravcoScrapeResult(
    int PageNumber,
    int TotalPages,
    IReadOnlyList<TravcoRow> Rows,
    // Total inactive villages reported by Travco's own header badge (#list-object-count) for the whole
    // search, independent of how many rows are on the current page. Null when the badge was not readable.
    int? TotalInactiveCount = null);

public sealed record TravcoSearchRequest(
    int X,
    int Y,
    int DaysInactive,
    string OrderBy);

public sealed record TravcoSearchProgress(
    int CompletedSteps,
    int TotalSteps,
    string Status);

public sealed record MapSqlVillageImportRequest(
    bool IncludePlayers,
    bool IncludeNatars,
    IReadOnlyList<string> IgnoredPlayers,
    IReadOnlyList<string> IgnoredAlliances);

public sealed record MapSqlVillageImportProgress(string Status);

public sealed record MapSqlVillageImportResult(IReadOnlyList<TravcoRow> Rows);

public sealed record TravcoRawRow(
    IReadOnlyList<string> Cells,
    string? VillageHref);

public sealed record TravcoRawPage(
    int PageNumber,
    int TotalPages,
    IReadOnlyList<string> Headers,
    IReadOnlyList<TravcoRawRow> Rows,
    int? TotalInactiveCount = null);
