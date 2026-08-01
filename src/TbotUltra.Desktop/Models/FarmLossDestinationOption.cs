namespace TbotUltra.Desktop.Models;

public sealed record FarmLossDestinationOption(
    string ListId,
    string Name,
    string VillageName,
    int TotalFarmCount,
    int Capacity)
{
    public string DisplayText => string.IsNullOrWhiteSpace(VillageName)
        ? $"{Name} ({TotalFarmCount}/{Capacity})"
        : $"{VillageName} — {Name} ({TotalFarmCount}/{Capacity})";
}
