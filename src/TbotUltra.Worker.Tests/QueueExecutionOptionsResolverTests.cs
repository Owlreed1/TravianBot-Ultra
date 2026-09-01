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
    public void Resolve_HeroManageUsesCurrentAutomationSettingsInsteadOfStaleQueuedSnapshot()
    {
        var currentOptions = new BotOptions
        {
            HeroMinHpForAdventure = 40,
            HeroAutoRevive = true,
            HeroAutoAssignPoints = true,
            HeroAutoUseOintments = true,
            HeroOintmentTargetHpPercent = 100,
            HeroStatPriority = "offence_bonus,resources,fighting_strength,defence_bonus",
            HeroStatMaximums = "resources=40,fighting_strength=80,offence_bonus=20,defence_bonus=100",
            HeroAdventurePickOrder = "top",
            HeroContinuousAdventures = true,
        };
        var item = new QueueItem
        {
            TaskName = "hero_manage",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [BotOptionPayloadKeys.HeroMinHpForAdventure] = "60",
                [BotOptionPayloadKeys.HeroAutoRevive] = "false",
                [BotOptionPayloadKeys.HeroAutoAssignPoints] = "false",
                [BotOptionPayloadKeys.HeroAutoUseOintments] = "false",
                [BotOptionPayloadKeys.HeroOintmentTargetHpPercent] = "50",
                [BotOptionPayloadKeys.HeroStatPriority] =
                    "resources,fighting_strength,offence_bonus,defence_bonus",
                [BotOptionPayloadKeys.HeroStatMaximums] = HeroAttributeMaximums.DefaultSerialized,
                [BotOptionPayloadKeys.HeroAdventurePickOrder] = "shortest",
                [BotOptionPayloadKeys.HeroContinuousAdventures] = "false",
            },
        };

        var result = QueueExecutionOptionsResolver.Resolve(currentOptions, item);

        Assert.Equal(currentOptions.HeroMinHpForAdventure, result.HeroMinHpForAdventure);
        Assert.Equal(currentOptions.HeroAutoRevive, result.HeroAutoRevive);
        Assert.Equal(currentOptions.HeroAutoAssignPoints, result.HeroAutoAssignPoints);
        Assert.Equal(currentOptions.HeroAutoUseOintments, result.HeroAutoUseOintments);
        Assert.Equal(currentOptions.HeroOintmentTargetHpPercent, result.HeroOintmentTargetHpPercent);
        Assert.Equal(currentOptions.HeroStatPriority, result.HeroStatPriority);
        Assert.Equal(currentOptions.HeroStatMaximums, result.HeroStatMaximums);
        Assert.Equal(currentOptions.HeroAdventurePickOrder, result.HeroAdventurePickOrder);
        Assert.Equal(currentOptions.HeroContinuousAdventures, result.HeroContinuousAdventures);
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
