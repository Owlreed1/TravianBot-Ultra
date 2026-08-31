using TbotUltra.Worker.Services;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>Runs Hero task mutations through the existing Hero client seam.</summary>
internal sealed class HeroAutomationOperation(IHeroClient client)
{
    public Task<HeroAdventureDispatchResult> DispatchAdventureAsync(CancellationToken cancellationToken)
        => client.SendHeroOnAdventureAsync(cancellationToken);

    public Task<bool> ReviveIfNeededAsync(bool autoRevive, CancellationToken cancellationToken)
        => client.CheckAndReviveDeadHeroOnCurrentPageAsync(autoRevive, cancellationToken);

    public Task<int?> RefreshAdventureCountAsync(CancellationToken cancellationToken)
        => client.RefreshAdventureCountAsync(cancellationToken: cancellationToken);

    public Task<bool> HasLevelUpIndicatorAsync(CancellationToken cancellationToken)
        => client.HasHeroLevelUpIndicatorOnCurrentPageAsync(cancellationToken);

    public Task<bool> IsRevivingAsync(CancellationToken cancellationToken)
        => client.IsHeroRevivingOnCurrentPageAsync(cancellationToken);

    public Task<bool> IsHomeAsync(CancellationToken cancellationToken)
        => client.IsHeroHomeOnCurrentPageAsync(cancellationToken);

    public Task<int?> ReadCurrentPageHpAsync(CancellationToken cancellationToken)
        => client.ReadHeroHpFromCurrentPageAsync(cancellationToken);

    public Task<bool> HasClaimableTasksAsync(CancellationToken cancellationToken)
        => client.HasClaimableTasksOnCurrentPageAsync(cancellationToken);

    public Task<bool> HasClaimableDailyQuestsAsync(CancellationToken cancellationToken)
        => client.HasClaimableDailyQuestsOnCurrentPageAsync(cancellationToken);

    public Task<HeroAttributeSnapshot> ReadAttributesAsync(CancellationToken cancellationToken)
        => client.ReadHeroAttributeSnapshotAsync(cancellationToken);

    public Task<HeroInventoryResources> ReadInventoryResourcesAsync(CancellationToken cancellationToken)
        => client.ReadHeroInventoryResourcesAsync(cancellationToken);

    public Task<string> IncreaseAdventuresToHardAsync(CancellationToken cancellationToken)
        => client.IncreaseAdventuresToHardAsync(cancellationToken);

    public Task<string> ReduceAdventuresTimeAsync(CancellationToken cancellationToken)
        => client.ReduceAdventuresTimeAsync(cancellationToken);

    public Task<string> ManageAsync(
        int minHp, bool autoRevive, bool autoAssignPoints, bool autoUseOintments, int ointmentTargetHpPercent,
        string statPriority, string adventurePickOrder, int hpRegenPerDayPercent,
        CancellationToken cancellationToken)
        => client.ManageHeroAsync(minHp, autoRevive, autoAssignPoints, autoUseOintments, ointmentTargetHpPercent,
            statPriority, adventurePickOrder, hpRegenPerDayPercent, cancellationToken);

    public Task<string> SpendAttributePointsAsync(string statPriority, CancellationToken cancellationToken)
        => client.SpendHeroAttributePointsAsync(statPriority, cancellationToken);
}
