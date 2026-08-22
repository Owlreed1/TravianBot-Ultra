namespace TbotUltra.Worker.Domain;

public enum TroopEvasionMovementType
{
    Reinforcement = 5,
    Raid = 4,
    Attack = 3,
}

public enum TroopEvasionOutcome
{
    Succeeded,
    Validated,
    NoTroops,
    TooLate,
    Canceled,
    Failed,
}

public enum TroopEvasionProgressState
{
    Preparing,
    FormReady,
    WaitingForSafeReturn,
    Confirming,
    Completed,
}

public sealed record TroopEvasionRequest(
    string VillageName,
    string? VillageUrl,
    string VillageKey,
    int TargetX,
    int TargetY,
    TroopEvasionMovementType MovementType,
    IReadOnlyList<int> SelectedTroopSlots,
    bool IncludeHero,
    DateTimeOffset TriggeringAttackArrivalUtc,
    TimeSpan ReturnSafetyMargin);

public sealed record TroopEvasionProgress(
    TroopEvasionProgressState State,
    string Message,
    DateTimeOffset? SafeConfirmAtUtc = null);

public sealed record TroopEvasionResult(
    TroopEvasionOutcome Outcome,
    string Message,
    IReadOnlyDictionary<int, long>? SentTroops = null,
    bool HeroSent = false,
    TimeSpan? OneWayTravelTime = null,
    DateTimeOffset? ConfirmedAtUtc = null)
{
    public bool Succeeded => Outcome == TroopEvasionOutcome.Succeeded;
}

public sealed record TroopEvasionValidationResult(
    bool IsValid,
    string Message,
    IReadOnlyDictionary<int, long>? AvailableTroops = null,
    bool HeroAvailable = false,
    TimeSpan? OneWayTravelTime = null,
    bool WouldRequireWaiting = false);
