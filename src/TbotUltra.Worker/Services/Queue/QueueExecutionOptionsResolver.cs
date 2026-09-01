using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

internal static class QueueExecutionOptionsResolver
{
    internal static BotOptions Resolve(BotOptions currentOptions, QueueItem item)
    {
        var resolved = BotOptionsPayloadApplier.Apply(currentOptions, item.Payload);
        if (IsHeroManageTask(item.TaskName))
        {
            // hero_manage can remain deferred for hours while HP regenerates or the Hero is away.
            // Its payload is only the enqueue-time snapshot; user-facing Hero controls must take
            // effect on the next execution without requiring the old queue item to be recreated.
            return resolved with
            {
                HeroMinHpForAdventure = currentOptions.HeroMinHpForAdventure,
                HeroAutoRevive = currentOptions.HeroAutoRevive,
                HeroAutoAssignPoints = currentOptions.HeroAutoAssignPoints,
                HeroAutoUseOintments = currentOptions.HeroAutoUseOintments,
                HeroOintmentTargetHpPercent = currentOptions.HeroOintmentTargetHpPercent,
                HeroStatPriority = currentOptions.HeroStatPriority,
                HeroStatMaximums = currentOptions.HeroStatMaximums,
                HeroAdventurePickOrder = currentOptions.HeroAdventurePickOrder,
                HeroContinuousAdventures = currentOptions.HeroContinuousAdventures,
            };
        }

        if (!IsHeroAttributeTask(item.TaskName))
        {
            return resolved;
        }

        // Hero tasks can remain queued while the user reorders attributes in the UI. The account
        // configuration loaded for this execution is authoritative; the queued priority is only a snapshot.
        return resolved with
        {
            HeroStatPriority = currentOptions.HeroStatPriority,
            HeroStatMaximums = currentOptions.HeroStatMaximums,
        };
    }

    internal static bool IsHeroManageTask(string taskName)
        => string.Equals(taskName, "hero_manage", StringComparison.OrdinalIgnoreCase);

    internal static bool IsHeroAttributeTask(string taskName)
    {
        return IsHeroManageTask(taskName)
            || string.Equals(taskName, "spend_hero_attribute_points", StringComparison.OrdinalIgnoreCase);
    }
}
