using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TbotUltra.Desktop.Models;

public sealed class IncomingAttackMonitoringVillageItem : INotifyPropertyChanged
{
    private bool _enabled = true;
    private string _villageName = string.Empty;

    public required string VillageKey { get; init; }
    public string VillageName
    {
        get => _villageName;
        set => Set(ref _villageName, value);
    }
    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
