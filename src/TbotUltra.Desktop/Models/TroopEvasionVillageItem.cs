using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TbotUltra.Core.Travian;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Models;

public sealed class TroopEvasionUnitItem(int slot, string name, bool selected) : INotifyPropertyChanged
{
    private bool _isSelected = selected;
    public int Slot { get; } = slot;
    public string Name { get; } = name;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TroopEvasionVillageItem : INotifyPropertyChanged
{
    private bool _enabled;
    private string _targetX = string.Empty;
    private string _targetY = string.Empty;
    private TroopEvasionMovementType _movementType = TroopEvasionMovementType.Reinforcement;
    private bool _includeHero = true;
    private string _runtimeStatus = "Disabled";

    public required string VillageKey { get; init; }
    public required string VillageName { get; init; }
    public string? VillageUrl { get; init; }
    public ObservableCollection<TroopEvasionUnitItem> Units { get; } = [];
    public IReadOnlyList<TroopEvasionMovementType> MovementTypes { get; } = Enum.GetValues<TroopEvasionMovementType>();
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public string TargetX { get => _targetX; set => Set(ref _targetX, value); }
    public string TargetY { get => _targetY; set => Set(ref _targetY, value); }
    public TroopEvasionMovementType MovementType { get => _movementType; set { if (Set(ref _movementType, value)) OnPropertyChanged(nameof(HeroRiskWarning)); } }
    public bool IncludeHero { get => _includeHero; set { if (Set(ref _includeHero, value)) OnPropertyChanged(nameof(HeroRiskWarning)); } }
    public string RuntimeStatus { get => _runtimeStatus; set => Set(ref _runtimeStatus, value); }
    public string Destination => int.TryParse(TargetX, out _) && int.TryParse(TargetY, out _) ? $"({TargetX} | {TargetY})" : "Not configured";
    public string TroopSummary => $"{Units.Count(unit => unit.IsSelected)} units" + (IncludeHero ? " + Hero" : string.Empty);
    public string HeroRiskWarning => IncludeHero && MovementType != TroopEvasionMovementType.Reinforcement
        ? "Hero selected for Raid/Attack: the hero may be exposed on return."
        : string.Empty;

    public static TroopEvasionVillageItem Create(VillageSelectionItem village, Services.TroopEvasionVillageSettings settings)
    {
        var result = new TroopEvasionVillageItem
        {
            VillageKey = settings.VillageKey,
            VillageName = village.Name,
            VillageUrl = village.Url,
            _enabled = settings.Enabled,
            _targetX = settings.TargetX?.ToString() ?? string.Empty,
            _targetY = settings.TargetY?.ToString() ?? string.Empty,
            _movementType = settings.MovementType,
            _includeHero = settings.IncludeHero,
        };
        var selected = (settings.SelectedTroopSlots ?? Enumerable.Range(1, 10)).ToHashSet();
        var names = TroopCatalog.ResolveTroopTypesForTribe(village.Tribe);
        for (var slot = 1; slot <= 10; slot++)
        {
            result.Units.Add(new TroopEvasionUnitItem(slot, names.ElementAtOrDefault(slot - 1) ?? $"Unit {slot}", selected.Contains(slot)));
        }
        return result;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void RefreshDerived()
    {
        OnPropertyChanged(nameof(Destination));
        OnPropertyChanged(nameof(TroopSummary));
        OnPropertyChanged(nameof(HeroRiskWarning));
    }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(propertyName); RefreshDerived(); return true;
    }
    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new(name));
}
