using System;
using System.Collections.Generic;
using System.Linq;
using TbotUltra.Core.Tasks;

namespace TbotUltra.Desktop.Services;

public sealed partial class VillageSettingsStore
{
    /// <summary>Returns the bulk-upgrade resource categories for a village; unknown and legacy villages select all.</summary>
    public IReadOnlyList<string> GetResourceUpgradeTypes(VillageKeyInfo village)
    {
        if (village is null || string.IsNullOrWhiteSpace(village.Key))
        {
            return ResourceUpgradeSelection.AllTypes;
        }

        lock (FileIoLock)
        {
            EnsureCacheLoaded();
            var record = FindRecordByVillage(village);
            return record?.ResourceUpgradeTypes is null
                ? ResourceUpgradeSelection.AllTypes
                : ResourceUpgradeSelection.Normalize(record.ResourceUpgradeTypes);
        }
    }

    /// <summary>Persists the selected bulk-upgrade resource categories for one village.</summary>
    public void SetResourceUpgradeTypes(VillageKeyInfo village, IEnumerable<string> types)
    {
        if (village is null || string.IsNullOrWhiteSpace(village.Key))
        {
            return;
        }

        var normalized = ResourceUpgradeSelection.Normalize(types).ToList();
        lock (FileIoLock)
        {
            EnsureCacheLoaded();
            var record = FindRecordByVillage(village);
            if (record is not null)
            {
                if (record.ResourceUpgradeTypes is not null
                    && record.ResourceUpgradeTypes.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    return;
                }

                record.ResourceUpgradeTypes = normalized;
                record.Name = village.Name;
                record.LastSeenUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                var key = CanonicalKey(village);
                _cache[key] = new VillageSettingRecord
                {
                    Key = key,
                    Name = village.Name,
                    CoordX = village.CoordX,
                    CoordY = village.CoordY,
                    IsCapital = village.IsCapital,
                    IsEnabled = DefaultAutomationEnabled,
                    EnabledGroups = CreateDefaultEnabledGroups(),
                    NpcTrade = false,
                    ConstructFasterEnabled = true,
                    HeroResourcesEnabled = true,
                    ResourceUpgradeTypes = normalized,
                    LastSeenUtc = DateTimeOffset.UtcNow,
                };
            }

            Save();
        }
    }
}
