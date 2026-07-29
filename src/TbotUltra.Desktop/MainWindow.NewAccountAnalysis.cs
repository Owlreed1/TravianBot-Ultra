using System;
using System.Threading.Tasks;
using System.Windows;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private void SetNewAccountAnalysisCompleted(bool completed)
    {
        var accountName = _accountStore.ActiveAccountName();
        var serverUrl = GetActiveAccountServerUrl();
        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(serverUrl))
        {
            return;
        }

        _accountAnalysisStore.Update(accountName, serverUrl, existing => existing is null
            ? null
            : existing with { NewAccountAnalysisCompleted = completed, AnalyzedAtUtc = DateTimeOffset.UtcNow });
        AppendLog($"[new-account-analysis] status={(completed ? "completed" : "pending")}.");
    }

    private void ClearNewAccountAnalysisDebugButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SetNewAccountAnalysisCompleted(false);
        StatusTextBlock.Text = "New account analysis cleared.";
    }

    private async void RunNewAccountAnalysisDebugButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (BlockIfSessionSleeping("Run new account analysis"))
        {
            return;
        }

        SetNewAccountAnalysisCompleted(false);
        if (!_isLoggedIn)
        {
            await ExecuteLoginFlowAsync(forceNewAccountAnalysis: true);
            return;
        }

        await RunGuardedOperationAsync(
            "RunNewAccountAnalysis",
            "New account analysis paused.",
            ToggleUiBusy,
            async (_, cancellationToken) =>
            {
                var options = ApplySelectedVillageToOptions(LoadBotOptions());
                var inventory = await _botService.RefreshHeroInventoryAsync(options, AppendLog, cancellationToken);
                _heroViewModel.ApplyInventory(inventory);
                await RefreshHeroStatsAsync(cancellationToken);

                var accountName = _accountStore.ActiveAccountName();
                _accountAnalysisStore.TryLoad(accountName, out var analysis, GetActiveAccountServerUrl());
                var villages = analysis?.Villages;
                if (villages is null || villages.Count == 0)
                {
                    throw new InvalidOperationException("No saved village list is available. Log out and run this test again.");
                }

                var villageAnalysis = await AnalyzeNewVillagesAfterLoginAsync(options, villages, cancellationToken);
                SetNewAccountAnalysisCompleted(villageAnalysis.Succeeded);
                return villageAnalysis.Succeeded
                    ? "New account analysis completed."
                    : "New account analysis is pending; one or more villages could not be analyzed.";
            });
    }
}
