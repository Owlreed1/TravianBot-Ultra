namespace TbotUltra.Worker.Domain;

public sealed record ConstructionQueueObservation(
    string AccountName,
    string VillageName,
    int? CoordX,
    int? CoordY,
    IReadOnlyList<ActiveConstruction> ActiveConstructions);
