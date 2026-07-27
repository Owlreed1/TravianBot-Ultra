using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class FarmListSelectionTests
{
    [Fact]
    public void MatchesFarmListSelection_PrefersStableIdOverDuplicateName()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "merc5" };
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1401" };

        Assert.True(TravianClient.MatchesFarmListSelection("1401", "merc5", names, ids));
        Assert.False(TravianClient.MatchesFarmListSelection("1404", "merc5", names, ids));
    }

    [Fact]
    public void MatchesFarmListSelection_UsesNameWhenLegacySelectionHasNoIds()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "merc5" };

        Assert.True(TravianClient.MatchesFarmListSelection(
            "1404",
            "merc5",
            names,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }
}
