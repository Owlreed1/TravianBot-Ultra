using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class QueueExecutionOptionsResolverTests
{
    [Theory]
    [InlineData("hero_manage")]
    [InlineData("spend_hero_attribute_points")]
    public void Resolve_HeroTaskKeepsCurrentConfiguredAttributePriority(string taskName)
    {
        var currentOptions = new BotOptions
        {
            HeroStatPriority = "offence_bonus,resources,fighting_strength,defence_bonus",
            HeroStatMaximums = "resources=40,fighting_strength=80,offence_bonus=20,defence_bonus=100",
        };
        var item = new QueueItem
        {
            TaskName = taskName,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [BotOptionPayloadKeys.HeroStatPriority] =
                    "resources,fighting_strength,offence_bonus,defence_bonus",
                [BotOptionPayloadKeys.HeroStatMaximums] = HeroAttributeMaximums.DefaultSerialized,
                [BotOptionPayloadKeys.HeroAutoAssignPoints] = "false",
            },
        };

        var result = QueueExecutionOptionsResolver.Resolve(currentOptions, item);

        Assert.Equal(currentOptions.HeroStatPriority, result.HeroStatPriority);
        Assert.Equal(currentOptions.HeroStatMaximums, result.HeroStatMaximums);
        Assert.False(result.HeroAutoAssignPoints);
    }

    [Fact]
    public void Resolve_NonHeroTaskRetainsPayloadPrioritySemantics()
    {
        var currentOptions = new BotOptions
        {
            HeroStatPriority = "offence_bonus,resources,fighting_strength,defence_bonus",
        };
        var item = new QueueItem
        {
            TaskName = "collect_tasks",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [BotOptionPayloadKeys.HeroStatPriority] =
                    "resources,fighting_strength,offence_bonus,defence_bonus",
            },
        };

        var result = QueueExecutionOptionsResolver.Resolve(currentOptions, item);

        Assert.Equal(
            "resources,fighting_strength,offence_bonus,defence_bonus",
            result.HeroStatPriority);
    }
}
