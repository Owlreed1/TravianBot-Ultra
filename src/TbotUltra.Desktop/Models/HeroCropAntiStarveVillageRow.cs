using TbotUltra.Desktop.Common;

namespace TbotUltra.Desktop.Models;

public sealed class HeroCropAntiStarveVillageRow : BaseViewModel
{
    private bool _isEnabled;

    public HeroCropAntiStarveVillageRow(string villageKey, string villageName, bool isEnabled)
    {
        VillageKey = villageKey;
        VillageName = villageName;
        _isEnabled = isEnabled;
    }

    public string VillageKey { get; }
    public string VillageName { get; }
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
