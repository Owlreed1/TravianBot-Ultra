namespace TbotUltra.Worker.Infrastructure;

internal static class BonusVideoPlaybackPolicy
{
    internal const int MinimumPrePlayObservationSeconds = 20;
    internal const int MinimumPostPlaySeconds = 60;
    internal const int PostPlayTimeoutSeconds = 120;
    internal const int ProviderFailureConfirmations = 2;
    internal const int IsolatedActionTimeoutSeconds = 240;
    internal const int IsolatedPageLoadAttempts = 3;
    internal const int MainPageRecoveryAttempts = 2;

    internal static bool MayComplete(double elapsedPostPlaySeconds)
        => elapsedPostPlaySeconds >= MinimumPostPlaySeconds;

    internal static bool MayGiveUpWaitingForPlaybackStart(double elapsedPrePlaySeconds)
        => elapsedPrePlaySeconds >= MinimumPrePlayObservationSeconds;

    internal static bool MayAcceptProviderFailure(
        double elapsedPostPlaySeconds,
        int consecutiveConfirmations,
        bool playerPresent)
    {
        if (!MayComplete(elapsedPostPlaySeconds))
        {
            return false;
        }

        return !playerPresent || consecutiveConfirmations >= ProviderFailureConfirmations;
    }

    internal static int RemainingGraceSeconds(double elapsedPostPlaySeconds)
        => Math.Max(0, (int)Math.Ceiling(MinimumPostPlaySeconds - elapsedPostPlaySeconds));

    internal static bool ShouldRetryIsolatedPageLoad(int completedAttempt)
        => completedAttempt < IsolatedPageLoadAttempts;

    internal static bool ShouldRetryMainPageRecovery(int completedAttempt)
        => completedAttempt < MainPageRecoveryAttempts;
}
