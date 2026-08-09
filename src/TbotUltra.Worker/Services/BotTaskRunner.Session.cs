using TbotUltra.Core.Configuration;
using TbotUltra.Core.Accounts;
using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Configuration;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Infrastructure;
using TbotUltra.Worker.Services.Automation;
using Microsoft.Playwright;
using System.Text.Json;

namespace TbotUltra.Worker.Services;

public sealed partial class BotTaskRunner
{
    public async Task<bool> IsLoggedInAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        var isLoggedIn = false;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                isLoggedIn = await new SessionOperation(client).CheckLoggedInAsync(cancellationToken);
            });

        return isLoggedIn;
    }

    public async Task ExecuteLoginAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default,
        bool keepBrowserOpenAfterLogin = false)
    {
        _ = keepBrowserOpenAfterLogin;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                log($"Starting login for server {options.ServerName}.");
                await new SessionOperation(client).LoginAsync(cancellationToken);
                await TrySwitchToTargetVillageAsync(client, options, log, cancellationToken, skipFeatureRefresh: true);
                log("Login completed and browser session saved. Browser stays open.");
            });
    }

    public async Task<string?> ReadCurrentLanguageAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        string? language = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                language = await new SessionOperation(client).ReadCurrentLanguageAsync(log, cancellationToken);
            });

        return language;
    }

    public async Task EnsureExpectedLanguageAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                await new SessionOperation(client).EnsureExpectedLanguageAsync(cancellationToken);
            });
    }

    public async Task<string?> SetLanguageToEnglishAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        string? language = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                language = await new SessionOperation(client).SetLanguageToEnglishAsync(log, cancellationToken);
            });

        return language;
    }

    public async Task<PostLoginSnapshot> ExecuteLoginAndLoadPostLoginSnapshotAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default,
        bool keepBrowserOpenAfterLogin = false)
    {
        _ = keepBrowserOpenAfterLogin;
        PostLoginSnapshot? snapshot = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                log($"Starting login for server {options.ServerName}.");
                await new SessionOperation(client).LoginAsync(cancellationToken);
                await TrySwitchToTargetVillageAsync(client, options, log, cancellationToken, skipFeatureRefresh: true);
                log("Login completed and browser session saved. Browser stays open.");

                snapshot = await LoadPostLoginSnapshotAsync(client, options, log, cancellationToken);
            });

        return snapshot ?? throw new InvalidOperationException("Could not load post-login snapshot.");
    }

    public async Task<PostLoginSnapshot> LoadPostLoginSnapshotAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        PostLoginSnapshot? snapshot = null;
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                await new SessionOperation(client).LoginAsync(cancellationToken);
                await TrySwitchToTargetVillageAsync(client, options, log, cancellationToken, skipFeatureRefresh: true);
                snapshot = await LoadPostLoginSnapshotAsync(client, options, log, cancellationToken);
            });

        return snapshot ?? throw new InvalidOperationException("Could not load post-login snapshot.");
    }

    private async Task<PostLoginSnapshot> LoadPostLoginSnapshotAsync(
        TravianClient client,
        BotOptions options,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        return await new PostLoginSnapshotOperation(client, _accountAnalysisStore)
            .LoadAsync(options, log, cancellationToken);
    }

    private static bool IsKnownTribe(string? tribe)
        => !string.IsNullOrWhiteSpace(tribe)
           && !string.Equals(tribe, "Unknown", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ReadAndPersistGoldClubStatusAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        var account = _accountProvider.LoadAccount(accountName);
        _accountAnalysisStore.TryLoad(account.Name, out var existing, options.BaseUrl);
        if (existing?.GoldClubEnabled == true)
        {
            return true;
        }

        var detectedGoldClubEnabled = false;

        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: true,
            cancellationToken,
            async client =>
            {
                await new SessionOperation(client).LoginAsync(cancellationToken);
                detectedGoldClubEnabled = await new GoldClubStatusOperation(client, _accountAnalysisStore)
                    .ReadAndPersistAsync(account, options, log, cancellationToken);
            });

        return detectedGoldClubEnabled;
    }

    public async Task ExecuteLogoutAsync(
        BotOptions options,
        Action<string> log,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithClientAsync(
            options,
            log,
            accountName,
            interactive: false,
            cancellationToken,
            async client =>
            {
                log($"Starting logout for server {options.ServerName}.");
                await new SessionOperation(client).LogoutAsync(cancellationToken);
                log("Logout completed.");
                // Drop all session-scoped cache (villages, population, plus/gold, logged-in state)
                // so a subsequent login on this shared browser starts from a clean slate and never
                // reuses the logged-out account's data.
                _sharedVisibleSessionCache = new TravianSessionCache();
            });
    }

}
