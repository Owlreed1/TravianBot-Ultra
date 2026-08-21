using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class MapSqlOwnVillageFilterSourceTests
{
    [Fact]
    public void ImportAllVillages_UsesCurrentSidebarPlayerAsAnIgnoredOwner()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var runner = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Worker", "Services", "BotTaskRunner.BulkMessages.cs"));
        var identity = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Worker", "Services", "Automation", "Core", "TravianClient.PlayerIdentity.cs"));
        var selectors = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Worker", "Services", "Automation", "Core", "TravianClient.Selectors.cs"));

        Assert.Contains("request.SkipOwnVillages && request.IncludePlayers", runner, StringComparison.Ordinal);
        Assert.Contains("client.ReadCurrentPlayerNameAsync", runner, StringComparison.Ordinal);
        Assert.Contains("effectiveIgnoredPlayers.Add(ownPlayerName);", runner, StringComparison.Ordinal);
        Assert.Contains("Selectors.CurrentPlayerName", identity, StringComparison.Ordinal);
        Assert.Contains("public const string CurrentPlayerName = \".content > .playerName\"", selectors, StringComparison.Ordinal);
    }
}
