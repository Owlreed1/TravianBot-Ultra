using System.ComponentModel;
using System.Runtime.CompilerServices;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Models;

public sealed class IncomingAttackRowItem : INotifyPropertyChanged
{
    public required string Id { get; init; }
    public required string VillageKey { get; init; }
    public required string TargetVillageName { get; init; }
    public IncomingAttackMovementType MovementType { get; init; }
    public string TypeText => IsReading ? "Reading…" : MovementType.ToString();
    public string SourcePlayerName { get; init; } = string.Empty;
    public string SourceVillageName { get; init; } = string.Empty;
    public string SourceCoordinatesText { get; init; } = string.Empty;
    public DateTimeOffset? ArrivalAtUtc { get; init; }
    public bool IsReading { get; init; }
    public string ArrivalText { get; set; } = string.Empty;

    private string _countdownText = string.Empty;
    public string CountdownText
    {
        get => _countdownText;
        set
        {
            if (string.Equals(_countdownText, value, StringComparison.Ordinal)) return;
            _countdownText = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
