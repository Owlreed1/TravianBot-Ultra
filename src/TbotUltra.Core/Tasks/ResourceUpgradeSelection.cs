using System;
using System.Collections.Generic;
using System.Linq;

namespace TbotUltra.Core.Tasks;

/// <summary>Canonical resource categories selected for a bulk field-upgrade task.</summary>
public static class ResourceUpgradeSelection
{
    public static IReadOnlyList<string> AllTypes { get; } = ["wood", "clay", "iron", "crop"];

    public static string Serialize(IEnumerable<string>? types)
    {
        var selected = Normalize(types);
        return string.Join(',', AllTypes.Where(selected.Contains));
    }

    /// <summary>Normalizes an explicit user selection; an empty sequence remains empty.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? types)
    {
        var selected = NormalizeKnownTypes(types);
        return AllTypes.Where(selected.Contains).ToList();
    }

    /// <summary>Missing payload data is a legacy bulk task and therefore selects every resource type.</summary>
    public static IReadOnlySet<string> Parse(string? rawTypes)
    {
        if (string.IsNullOrWhiteSpace(rawTypes))
        {
            return new HashSet<string>(AllTypes, StringComparer.OrdinalIgnoreCase);
        }

        return NormalizeKnownTypes(rawTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static bool Matches(string? fieldType, string? name, IReadOnlySet<string> selectedTypes)
    {
        var category = ResolveCategory(fieldType) ?? ResolveCategory(name);
        return category is not null && selectedTypes.Contains(category);
    }

    private static HashSet<string> NormalizeKnownTypes(IEnumerable<string>? types)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (types is null)
        {
            return selected;
        }

        foreach (var type in types)
        {
            var category = ResolveCategory(type);
            if (category is not null)
            {
                selected.Add(category);
            }
        }

        return selected;
    }

    private static string? ResolveCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Contains("wood", StringComparison.OrdinalIgnoreCase)) return "wood";
        if (value.Contains("clay", StringComparison.OrdinalIgnoreCase)) return "clay";
        if (value.Contains("iron", StringComparison.OrdinalIgnoreCase)) return "iron";
        if (value.Contains("crop", StringComparison.OrdinalIgnoreCase)) return "crop";
        return null;
    }
}
