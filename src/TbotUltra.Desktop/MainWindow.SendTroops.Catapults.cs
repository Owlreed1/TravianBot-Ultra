using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Models;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private void StartCatapultWavesButton_Click(object sender, RoutedEventArgs e)
    {
        if (BlockIfSessionSleeping("Catapult waves"))
        {
            return;
        }

        if (_farmingOperationBusy)
        {
            return;
        }

        _backgroundTasks.Track(StartCatapultWavesAsync());
    }

    private async Task StartCatapultWavesAsync()
    {
        var operationId = BeginOperation("Catapult Waves");
        var operationSw = Stopwatch.StartNew();
        var operationToken = _loopController.StartOperation("operation");
        SetFarmingFunctionRunning(true);
        BusyOverlay.ShowCancel = true;
        ShowBusyOverlay("Catapult waves", "Reading troops from Rally Point...");
        try
        {
            await EnsureChromiumInstalledAsync();
            SetCatapultWavesStatus("Reading troops from Rally Point...");

            var villages = (VillageComboBox.ItemsSource as IEnumerable<VillageSelectionItem> ?? [])
                .Where(village => !string.IsNullOrWhiteSpace(village.Name) && village.Name != "-")
                .ToList();
            var activeVillage = VillageComboBox.SelectedItem as VillageSelectionItem
                ?? villages.FirstOrDefault(village => string.Equals(
                    GetVillageKey(village), _activeWorkingVillageKey, StringComparison.OrdinalIgnoreCase));

            async Task<CatapultWaveSetupInfo> ReadSetupAsync(
                BotOptions options,
                bool forceRefresh,
                Action<string> status,
                CancellationToken token)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(operationToken, token);
                return await _botService.ReadCatapultWaveSetupInfoAsync(
                    options,
                    message =>
                    {
                        AppendLog(message);
                        status(message);
                    },
                    forceRefresh,
                    linkedCts.Token);
            }

            var initialSetupInfo = await ReadSetupAsync(
                ApplySelectedVillageToOptions(LoadBotOptions()),
                forceRefresh: false,
                status => BusyOverlay.Text = status,
                operationToken);
            SetCatapultWavesStatus("Troops loaded from Rally Point.");
            HideBusyOverlay();

            var dialog = new CatapultWaveWindow(
                ResolveCurrentTribeForFarming(),
                availableTroops: initialSetupInfo.AvailableTroops,
                rallyPointLevel: initialSetupInfo.RallyPointLevel,
                villages: villages,
                activeVillage: activeVillage)
            {
                Owner = this,
                RefreshRequested = async (status, token) =>
                {
                    status("Refreshing troops from Rally Point...");
                    SetCatapultWavesStatus("Refreshing troops from Rally Point...");
                    var refreshedSetupInfo = await ReadSetupAsync(
                        ApplySelectedVillageToOptions(LoadBotOptions()),
                        forceRefresh: true,
                        status,
                        token);
                    SetCatapultWavesStatus("Troops refreshed from Rally Point.");
                    return refreshedSetupInfo;
                },
                SwitchVillageRequested = async (selected, status, token) =>
                {
                    VillageComboBox.SelectedItem = selected;
                    status($"Switching to {selected.DisplayName}…");
                    var options = BotOptionsPayloadApplier.Apply(LoadBotOptions(), BuildVillageRuntimePayload(selected));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(operationToken, token);
                    var villageStatus = await ReadVillageStatusWithRetryAsync(
                        options,
                        linkedCts.Token,
                        resourceOnly: false,
                        forceCurrentVillage: true);
                    var expectedVillageKey = GetVillageKey(selected);
                    var actualVillageKey = ResolveStatusVillageKey(villageStatus);
                    if (!string.Equals(actualVillageKey, expectedVillageKey, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Browser stayed on '{villageStatus.ActiveVillage}' instead of '{selected.DisplayName}'.");
                    }

                    SetActiveWorkingVillageFromStatus(villageStatus);
                    CacheVillageStatus(villageStatus);
                    status("Opening Send Troops and reading troops…");
                    return await ReadSetupAsync(options, forceRefresh: true, status, token);
                },
                StartRequested = async (request, status, token) =>
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(operationToken, token);
                    var options = ApplySelectedVillageToOptions(LoadBotOptions());
                    status("Preparing catapult waves...");
                    SetCatapultWavesStatus("Preparing catapult waves...");
                    var result = await _botService.StartCatapultWavesAsync(
                        options,
                        request,
                        message =>
                        {
                            AppendLog(message);
                            status(message);
                        },
                        linkedCts.Token);

                    var attackMode = request.RaidAttack ? "raid" : "normal attack";
                    var message = $"Sent {result.SentCount}/{result.TotalAttacks} {attackMode}(s) to ({result.X}|{result.Y}).";
                    SetCatapultWavesStatus(message);
                    CompleteOperation(operationId, operationSw, message);
                    return result;
                },
            };

            if (dialog.ShowDialog() != true)
            {
                AppendLog("Catapult waves canceled.");
                CompleteOperation(operationId, operationSw, "Catapult waves canceled before sending.");
                return;
            }
        }
        catch (OperationCanceledException)
        {
            SetCatapultWavesStatus("Catapult waves canceled.");
            AppendLog("Catapult waves canceled.");
            CompleteOperation(operationId, operationSw, "Catapult waves stopped by user.");
        }
        catch (Exception ex)
        {
            SetCatapultWavesStatus($"Catapult waves failed: {ex.Message}");
            FailOperation(operationId, operationSw, ex);
        }
        finally
        {
            HideBusyOverlay();
            BusyOverlay.ShowCancel = false;
            SetFarmingFunctionRunning(false);
            DisposeOperationCts();
        }
    }

    private void SetCatapultWavesStatus(string status)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => SetCatapultWavesStatus(status));
            return;
        }

        if (CatapultWavesStatusTextBlock is not null)
        {
            CatapultWavesStatusTextBlock.Text = status;
        }
    }
}
