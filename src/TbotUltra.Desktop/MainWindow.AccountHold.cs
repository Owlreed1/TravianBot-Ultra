using System.Windows;
using TbotUltra.Desktop.Services.Orchestration;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Services;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private AccountAutomationHold? ActiveAccountHold()
    {
        var accountName = _accountStore.ActiveAccountName();
        return string.IsNullOrWhiteSpace(accountName)
            ? null
            : _accountAutomationHoldStore.Load(accountName);
    }

    private bool BlockIfActiveAccountOnHold(string operation)
    {
        var hold = ActiveAccountHold();
        if (hold is null)
        {
            return false;
        }

        RefreshAccountHoldUi();
        StatusTextBlock.Text = "Automation is stopped for this account. Manual review is required.";
        AppendLog($"[account-hold] {operation} blocked for account '{hold.AccountName}' ({hold.AccessState}).");
        return true;
    }

    private async Task HoldAccountAutomationAsync(AccountAccessException exception)
    {
        var existingHold = _accountAutomationHoldStore.Load(exception.AccountName);
        var hold = new AccountAutomationHold(
            exception.AccountName,
            exception.State.ToString(),
            exception.Message,
            DateTimeOffset.UtcNow);
        if (existingHold is null)
        {
            _accountAutomationHoldStore.Save(hold);
            if (exception.State == AccountAccessState.Banned)
            {
                _banRecoveryStore.CaptureIfMissing(
                    exception.AccountName,
                    _villageCacheStore.Load(),
                    _villageCacheStore.LoadUpdatedAtUtc());
            }
        }

        RequestAutomationStop(AutomationStopMode.CancelCurrentAction);
        _loopController.CancelVillageSwitch();
        _loopController.CancelSessionScope();

        await Dispatcher.InvokeAsync(() =>
        {
            _isLoggedIn = false;
            StartLoopButton.Content = "Start bot";
            SetLoopIndicator(false);
            UpdateLoginButtonsVisual(false);
            RefreshAccountHoldUi();

            if (existingHold is null)
            {
                var message = exception.State == AccountAccessState.Banned
                    ? "Travian has banned this avatar. All automation for the account has been stopped, and no punishment or support button was clicked. Review the ban manually in the open browser."
                    : $"Travian reported an account access problem ({exception.State}). All automation for the account has been stopped. Manual review is required.";
                AppDialog.Show(
                    this,
                    message,
                    exception.State == AccountAccessState.Banned ? "Account banned" : "Account access stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK);
            }
        });

        AppendLog(
            $"ALARM: Automation stopped for account '{exception.AccountName}'. " +
            $"Access state={exception.State}. Manual review and re-enable are required. Reason: {exception.Message}");
    }

    private void RefreshAccountHoldUi()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke((Action)RefreshAccountHoldUi);
            return;
        }

        var hold = ActiveAccountHold();
        AccountHoldBorder.Visibility = hold is null ? Visibility.Collapsed : Visibility.Visible;
        if (hold is null)
        {
            AccountHoldTextBlock.Text = string.Empty;
            LoginButton.IsEnabled = !_uiBusy;
            return;
        }

        AccountHoldTextBlock.Text =
            $"{hold.AccessState} at {hold.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}. {hold.Reason}";
        LoginButton.IsEnabled = false;
    }

    private void ReenableAccountButton_Click(object sender, RoutedEventArgs e)
    {
        var accountName = _accountStore.ActiveAccountName();
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        var hold = ActiveAccountHold();
        if (hold is not null
            && string.Equals(hold.AccessState, AccountAccessState.Banned.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            _banRecoveryStore.SetStage(accountName, BanRecoveryStage.ScanPending);
        }
        _accountAutomationHoldStore.Clear(accountName);
        _loopController.ClearLoopStopRequest();
        _loopController.ClearQueueStopRequest();
        RefreshAccountHoldUi();
        UpdateLoginButtonsVisual(false);
        const string nextSteps =
            "Account re-enabled. Click Login, then Start bot. A read-only village recovery scan will run before automation starts.";
        StatusTextBlock.Text = nextSteps;
        AppendLog($"Account '{accountName}' manually re-enabled. Queue and settings were kept. Click Login, then Start bot.");
        AppDialog.Show(
            this,
            "The account hold has been removed.\n\n"
            + "1. Make sure the ban has been resolved in the open Travian browser.\n"
            + "2. Click Login in Tbot Ultra to verify the session.\n"
            + "3. Click Start bot when login has completed. Tbot Ultra will scan every village before running any task.\n"
            + "4. Review the recovery choices after the scan.\n\n"
            + "If Travian still reports a ban, all automation will stop again.",
            "Account re-enabled",
            MessageBoxButton.OK,
            MessageBoxImage.Information,
            MessageBoxResult.OK);
    }
}
