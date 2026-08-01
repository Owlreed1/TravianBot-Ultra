using System;
namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private void SetFarmingOperationBusy(bool busy)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => SetFarmingOperationBusy(busy));
            return;
        }

        if (busy)
        {
            EnsureManualExecutionTracking();
        }

        _farmingOperationBusy = busy;
        try
        {
            SyncFarmingControlsEnabledState();
            UpdateExecutionStateIndicator();
        }
        finally
        {
            if (!busy)
            {
                CompleteManualExecutionTrackingIfNeeded();
            }
        }
    }

    private void SetFarmingFunctionRunning(bool running)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => SetFarmingFunctionRunning(running));
            return;
        }

        try
        {
            if (running)
            {
                EnsureManualExecutionTracking();
            }

            UpdateExecutionStateIndicator();
        }
        finally
        {
            if (!running)
            {
                CompleteManualExecutionTrackingIfNeeded();
            }
        }
    }

}
