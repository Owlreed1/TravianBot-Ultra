using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TbotUltra.Desktop.Models;

public sealed class FarmListStatusRow : INotifyPropertyChanged
{
    private bool _isEnabled;
    private string _name = string.Empty;
    private string _villageName = string.Empty;
    private string _villageHeaderText = string.Empty;
    private int _villageOrdinal = -1;
    private string? _listId;
    private int _activeFarmCount;
    private int _totalFarmCount;
    private int? _capacity;
    private int? _remainingSeconds;
    private bool _isProcessing;
    private bool _isPlaceholder;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    // Display name of the owning village. Kept for reference; the UI groups by VillageOrdinal and labels
    // the heading with VillageHeaderText (name + coordinates when resolvable).
    public string VillageName
    {
        get => _villageName;
        set
        {
            var normalized = value ?? string.Empty;
            if (_villageName == normalized)
            {
                return;
            }

            _villageName = normalized;
            OnPropertyChanged();
        }
    }

    // Grouping key: the ordinal of the owning .villageWrapper on the farm page. Two villages that share a
    // display name still get distinct ordinals, so they stay as separate groups. -1 for the placeholder.
    public int VillageOrdinal
    {
        get => _villageOrdinal;
        set
        {
            if (_villageOrdinal == value)
            {
                return;
            }

            _villageOrdinal = value;
            OnPropertyChanged();
        }
    }

    // Heading label shown for the village group: the village name, with coordinates appended when they can
    // be resolved unambiguously ("Name (x | y)"). Empty for the placeholder group, whose header is hidden.
    public string VillageHeaderText
    {
        get => _villageHeaderText;
        set
        {
            var normalized = value ?? string.Empty;
            if (_villageHeaderText == normalized)
            {
                return;
            }

            _villageHeaderText = normalized;
            OnPropertyChanged();
        }
    }

    // Stable Travian farm-list id (lid). Used to keep the selection matched after a village/list
    // rename — the display Name changes but the lid does not. May be null for lists where the lid
    // could not be resolved from the page.
    public string? ListId
    {
        get => _listId;
        set
        {
            if (_listId == value)
            {
                return;
            }

            _listId = value;
            OnPropertyChanged();
        }
    }

    public int ActiveFarmCount
    {
        get => _activeFarmCount;
        set
        {
            if (_activeFarmCount == value)
            {
                return;
            }

            _activeFarmCount = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FarmCountText));
            OnPropertyChanged(nameof(FillPercent));
        }
    }

    public int TotalFarmCount
    {
        get => _totalFarmCount;
        set
        {
            if (_totalFarmCount == value)
            {
                return;
            }

            _totalFarmCount = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FarmCountText));
            OnPropertyChanged(nameof(FillPercent));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ReadyText));
            OnPropertyChanged(nameof(ActionText));
            OnPropertyChanged(nameof(CanSendNow));
        }
    }

    public int? Capacity
    {
        get => _capacity;
        set
        {
            var normalized = value is > 0 ? value : null;
            if (_capacity == normalized)
            {
                return;
            }

            _capacity = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FarmCountText));
            OnPropertyChanged(nameof(FillPercent));
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSendNow));
        }
    }

    public bool IsPlaceholder
    {
        get => _isPlaceholder;
        set
        {
            if (_isPlaceholder == value)
            {
                return;
            }

            _isPlaceholder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFarmList));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(FarmCountText));
            OnPropertyChanged(nameof(ReadyText));
            OnPropertyChanged(nameof(ActionText));
            OnPropertyChanged(nameof(CanSendNow));
        }
    }

    public bool HasFarmList => !IsPlaceholder;

    public bool IsEmpty => HasFarmList && TotalFarmCount == 0;

    public int? RemainingSeconds
    {
        get => _remainingSeconds;
        set
        {
            var normalized = value.HasValue ? Math.Max(0, value.Value) : (int?)null;
            if (_remainingSeconds == normalized)
            {
                return;
            }

            _remainingSeconds = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasTimer));
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(TimerText));
            OnPropertyChanged(nameof(CanSendNow));
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (_isProcessing == value)
            {
                return;
            }

            _isProcessing = value;
            OnPropertyChanged();
        }
    }

    public string FarmCountText => HasFarmList
        ? $"{TotalFarmCount}/{Capacity ?? TotalFarmCount} farms"
        : string.Empty;

    public double FillPercent
    {
        get
        {
            var capacity = Capacity ?? TotalFarmCount;
            if (capacity <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(100, (double)TotalFarmCount / capacity * 100));
        }
    }

    public bool HasTimer => RemainingSeconds is > 0;

    public bool IsReady => !HasTimer;

    public string ReadyText => !HasFarmList ? string.Empty : IsEmpty ? "Empty" : "Ready";

    public string ActionText => IsEmpty ? "Empty" : "Send Now";

    public string TimerText
    {
        get
        {
            if (!HasTimer || RemainingSeconds is null)
            {
                return "00:00";
            }

            var ts = TimeSpan.FromSeconds(RemainingSeconds.Value);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}"
                : $"{ts.Minutes:00}:{ts.Seconds:00}";
        }
    }

    public bool CanSendNow => HasFarmList && !IsEmpty && IsEnabled && IsReady;

    public bool TickOneSecond()
    {
        if (!HasTimer || RemainingSeconds is null)
        {
            return false;
        }

        RemainingSeconds = RemainingSeconds.Value - 1;
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
