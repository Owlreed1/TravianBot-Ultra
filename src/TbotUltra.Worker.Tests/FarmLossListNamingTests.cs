using TbotUltra.Core.Travian;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class FarmLossListNamingTests
{
    [Fact]
    public void NextAvailable_UsesDefaultNameAndIncrementsCaseInsensitively()
    {
        var result = FarmLossListNaming.NextAvailable(null, ["yellow FARMS", "Yellow farms1"]);

        Assert.Equal("Yellow farms2", result);
    }

    [Fact]
    public void NextAvailable_PreservesCustomBaseAcrossRollover()
    {
        var result = FarmLossListNaming.NextAvailable("Loss holding", ["Loss holding"]);

        Assert.Equal("Loss holding1", result);
    }

    [Fact]
    public void NextAvailable_TruncatesBaseSoSuffixFitsThirtyCharacters()
    {
        var longName = new string('a', FarmLossListNaming.MaxNameLength);

        var result = FarmLossListNaming.NextAvailable(longName, [longName]);

        Assert.Equal(FarmLossListNaming.MaxNameLength, result.Length);
        Assert.EndsWith("1", result, StringComparison.Ordinal);
    }
}
