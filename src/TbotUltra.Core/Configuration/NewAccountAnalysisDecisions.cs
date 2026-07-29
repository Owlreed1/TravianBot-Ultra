namespace TbotUltra.Core.Configuration;

/// <summary>Determines whether the account-and-world first-login analysis still needs to run.</summary>
public static class NewAccountAnalysisDecisions
{
    /// <summary>Only an explicit successful completion suppresses the one-time analysis.</summary>
    public static bool IsPending(bool enabled, bool? completed)
        => enabled && completed != true;
}
