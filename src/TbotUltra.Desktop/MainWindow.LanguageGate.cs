using System.Windows;
using TbotUltra.Desktop.Services.Orchestration;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private async Task<bool> HandleUnexpectedTravianLanguageAsync(UnexpectedTravianLanguageException ex)
    {
        var resumeContinuous = IsContinuousLoopRunning() || _startContinuousLoopAfterQueueStop;
        var resumeQueue = !resumeContinuous && _autoQueueRunning;
        RequestAutomationStop(AutomationStopMode.AfterCurrentAction);
        AppendLog($"[language] Bot paused: {ex.Message}");
        var verified = await ShowTravianLanguageGateAsync(ex.CurrentLanguage);
        if (verified)
        {
            AcknowledgeLanguageAlarmEntries();
            await ResumeAutomationAfterLanguageGateAsync(resumeContinuous, resumeQueue);
        }

        return verified;
    }

    private async Task ResumeAutomationAfterLanguageGateAsync(bool resumeContinuous, bool resumeQueue)
    {
        if (_loopController.IsClosing || !_isLoggedIn || IsSessionSleeping)
        {
            return;
        }

        if (resumeContinuous)
        {
            if (IsContinuousLoopRunning())
            {
                _restartContinuousLoopAfterStop = true;
                AppendLog("[language] English verified; continuous loop will resume after the current stop completes.");
            }
            else
            {
                AppendLog("[language] English verified; resuming continuous loop.");
                StartContinuousLoopRunner();
            }

            return;
        }

        if (!resumeQueue)
        {
            return;
        }

        if (_autoQueueRunning)
        {
            _restartAutoQueueAfterLanguageGate = true;
            AppendLog("[language] English verified; queue auto-run will resume after the current stop completes.");
            return;
        }

        AppendLog("[language] English verified; resuming queue auto-run.");
        await TriggerQueueAutoRunAsync();
    }

    private async Task<bool> ShowTravianLanguageGateAsync(string? currentLanguage)
    {
        if (_travianLanguageGateActive)
        {
            AppendLog("[language] Language popup is already open.");
            return false;
        }

        _travianLanguageGateActive = true;
        try
        {
            if (Dispatcher.CheckAccess())
            {
                return ShowTravianLanguageGateCore(currentLanguage);
            }

            return await Dispatcher.InvokeAsync(() => ShowTravianLanguageGateCore(currentLanguage));
        }
        finally
        {
            _travianLanguageGateActive = false;
            RunOrPostToUi(() => ToggleUiBusy(_uiBusy));
        }
    }

    private bool ShowTravianLanguageGateCore(string? currentLanguage)
    {
        var options = LoadBotOptions();
        var token = _loopController.AcquireSessionScopeToken();
        var window = new TravianLanguageGateWindow(
            currentLanguage,
            async () =>
            {
                var language = await _botService.SetLanguageToEnglishAsync(options, AppendLog, token);
                if (string.Equals(language?.Trim(), TravianClient.ExpectedLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    AcknowledgeLanguageAlarmEntries();
                }

                return language;
            },
            async () => await _botService.ReadCurrentLanguageAsync(options, AppendLog, token),
            () => _loopController.IsClosing)
        {
            Owner = this,
        };

        var verified = window.ShowDialog() == true;
        if (window.ForceClosed)
        {
            AppendLog("[language] Language popup force-closed. Automation remains stopped; next login/start will check language again.");
        }

        return verified;
    }
}
