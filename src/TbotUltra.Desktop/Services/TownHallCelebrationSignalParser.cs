using System;
using System.Collections.Generic;
using System.Globalization;
using TbotUltra.Core.Configuration;

namespace TbotUltra.Desktop.Services;

internal static class TownHallCelebrationSignalParser
{
    private const string Token = "town_hall_active=";

    internal static IReadOnlyList<TownHallCelebrationTimer> Parse(
        string? message,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return [];
        }

        var tokenIndex = message.IndexOf(Token, StringComparison.OrdinalIgnoreCase);
        if (tokenIndex < 0)
        {
            return [];
        }

        var valueStart = tokenIndex + Token.Length;
        var valueEnd = message.IndexOf(' ', valueStart);
        var value = valueEnd < 0 ? message[valueStart..] : message[valueStart..valueEnd];
        var result = new List<TownHallCelebrationTimer>(2);
        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf(':');
            if (separator <= 0
                || !int.TryParse(entry[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                || seconds <= 0)
            {
                continue;
            }

            result.Add(new TownHallCelebrationTimer(
                TownHallCelebrationDefaults.NormalizeMode(entry[..separator]),
                nowUtc.AddSeconds(seconds)));
        }

        return result;
    }
}
