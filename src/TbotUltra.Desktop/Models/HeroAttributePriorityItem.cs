using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TbotUltra.Desktop.Models;

public sealed class HeroAttributePriorityItem : INotifyPropertyChanged
{
    private int _order;
    private string _pointsText = "-";
    private int _maxPoints = 100;

    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    public int Order
    {
        get => _order;
        set
        {
            if (_order == value)
            {
                return;
            }

            _order = value;
            OnPropertyChanged();
        }
    }

    public string PointsText
    {
        get => _pointsText;
        set
        {
            if (string.Equals(_pointsText, value, StringComparison.Ordinal))
            {
                return;
            }

            _pointsText = value;
            OnPropertyChanged();
        }
    }

    public int MaxPoints
    {
        get => _maxPoints;
        set
        {
            var normalized = value is >= 0 and <= 100 ? value : 100;
            if (_maxPoints == normalized)
            {
                return;
            }

            _maxPoints = normalized;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
