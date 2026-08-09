using TbotUltra.Core.Accounts;
using TbotUltra.Core.Travian;
using TbotUltra.Worker.Configuration;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using TbotUltra.Worker.Services.Automation;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class SessionOperationTests
{
    [Fact]
    public async Task EverySessionOperation_ForwardsCancellationAndKeepsLanguageLoggingContract()
    {
        var client = new RecordingSessionClient();
        var operation = new SessionOperation(client);
        var logs = new List<string>();
        using var cancellation = new CancellationTokenSource();

        await operation.LoginAsync(cancellation.Token);
        Assert.True(await operation.CheckLoggedInAsync(cancellation.Token));
        await operation.LogoutAsync(cancellation.Token);
        Assert.Equal("sv-SE", await operation.ReadCurrentLanguageAsync(logs.Add, cancellation.Token));
        await operation.EnsureExpectedLanguageAsync(cancellation.Token);
        Assert.Equal("en-US", await operation.SetLanguageToEnglishAsync(logs.Add, cancellation.Token));

        Assert.Equal(["login", "check", "logout", "read-language", "ensure-language", "set-language"], client.Calls);
        Assert.All(client.CancellationTokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Equal(
            ["[language] current Travian language: sv-SE.", "[language] Travian language set to English."],
            logs);
    }

    private sealed class RecordingSessionClient : ISessionClient
    {
        public List<string> Calls { get; } = [];
        public List<CancellationToken> CancellationTokens { get; } = [];
        public Task LoginAsync(CancellationToken cancellationToken = default) => Record("login", cancellationToken);
        public Task LogoutAsync(CancellationToken cancellationToken = default) => Record("logout", cancellationToken);
        public Task<bool> CheckLoggedInAsync(CancellationToken cancellationToken = default) => Record("check", cancellationToken, true);
        public Task<string?> ReadCurrentLanguageAsync(CancellationToken cancellationToken = default) => Record("read-language", cancellationToken, (string?)"sv-SE");
        public Task EnsureExpectedLanguageAsync(CancellationToken cancellationToken = default) => Record("ensure-language", cancellationToken);
        public Task<string?> SetLanguageToEnglishAsync(CancellationToken cancellationToken = default) => Record("set-language", cancellationToken, (string?)"en-US");
        public Task SwitchToVillageAsync(string villageName = "", string? villageUrl = null, CancellationToken cancellationToken = default, bool skipFeatureRefresh = false) => throw new NotSupportedException();
        public Task<IReadOnlyList<VillageStatus>> ReadAllVillageStatusesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VillageStatus> ReadVillageStatusAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VillageStatus> ReadVillageStatusAsync(IReadOnlyList<Village> knownVillages, IReadOnlyList<Building> knownBuildings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AccountSnapshot> ReadAccountSnapshotAsync(bool forceRefreshVillages = false, bool preferCurrentPageVillages = false, bool restorePageAfterProfile = true, bool suppressEnsureUiSync = false, bool skipOverviewNavigation = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AccountAnalysisSnapshot> ReadAccountAnalysisSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RefreshAccountFeatureSignalsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ReadGoldClubStatusAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RefreshCurrentPageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> ReadTribeOnlyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsTravianPlusActiveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private Task Record(string call, CancellationToken token) { Calls.Add(call); CancellationTokens.Add(token); return Task.CompletedTask; }
        private Task<T> Record<T>(string call, CancellationToken token, T result) { Calls.Add(call); CancellationTokens.Add(token); return Task.FromResult(result); }
    }
}
