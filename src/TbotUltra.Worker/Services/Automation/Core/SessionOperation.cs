using TbotUltra.Worker.Services;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>Owns non-navigation session checks and language decisions behind the browser client seam.</summary>
internal sealed class SessionOperation(ISessionClient client)
{
    public Task LoginAsync(CancellationToken cancellationToken)
        => client.LoginAsync(cancellationToken);

    public Task<bool> CheckLoggedInAsync(CancellationToken cancellationToken)
        => client.CheckLoggedInAsync(cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken)
        => client.LogoutAsync(cancellationToken);

    public async Task<string?> ReadCurrentLanguageAsync(Action<string> log, CancellationToken cancellationToken)
    {
        var language = await client.ReadCurrentLanguageAsync(cancellationToken);
        if (!string.Equals(language?.Trim(), "en-US", StringComparison.OrdinalIgnoreCase))
        {
            log($"[language] current Travian language: {language ?? "unknown"}.");
        }

        return language;
    }

    public Task EnsureExpectedLanguageAsync(CancellationToken cancellationToken)
        => client.EnsureExpectedLanguageAsync(cancellationToken);

    public async Task<string?> SetLanguageToEnglishAsync(Action<string> log, CancellationToken cancellationToken)
    {
        var language = await client.SetLanguageToEnglishAsync(cancellationToken);
        log("[language] Travian language set to English.");
        return language;
    }
}
