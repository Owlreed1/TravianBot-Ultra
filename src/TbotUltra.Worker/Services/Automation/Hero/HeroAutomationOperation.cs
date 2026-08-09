using TbotUltra.Worker.Services;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>Runs Hero task mutations through the existing Hero client seam.</summary>
internal sealed class HeroAutomationOperation(IHeroClient client)
{
    public Task<string> ManageAsync(
        int minHp, bool autoRevive, bool autoAssignPoints, bool autoUseOintments,
        string statPriority, string adventurePickOrder, int hpRegenPerDayPercent,
        CancellationToken cancellationToken)
        => client.ManageHeroAsync(minHp, autoRevive, autoAssignPoints, autoUseOintments,
            statPriority, adventurePickOrder, hpRegenPerDayPercent, cancellationToken);

    public Task<string> SpendAttributePointsAsync(string statPriority, CancellationToken cancellationToken)
        => client.SpendHeroAttributePointsAsync(statPriority, cancellationToken);
}
