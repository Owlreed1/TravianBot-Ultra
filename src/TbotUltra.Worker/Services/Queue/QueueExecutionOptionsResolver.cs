using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

internal static class QueueExecutionOptionsResolver
{
    internal static BotOptions Resolve(BotOptions currentOptions, QueueItem item)
    {
        var resolved = BotOptionsPayloadApplier.Apply(currentOptions, item.Payload);
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

    internal static bool IsHeroAttributeTask(string taskName)
    {
        return string.Equals(taskName, "hero_manage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(taskName, "spend_hero_attribute_points", StringComparison.OrdinalIgnoreCase);
    }
}
