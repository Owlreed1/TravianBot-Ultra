using TbotUltra.Desktop.Common;

namespace TbotUltra.Desktop.ViewModels;

/// <summary>One selectable hour in the session-pacing schedule.</summary>
public sealed class PacingHourOptionViewModel(int hour) : BaseViewModel
{
    private bool _isSelected = true;

    public int Hour { get; } = hour;
    public string Label => $"{Hour:00}:00";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
