using System.Windows;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private Task<bool> ConfirmManualLoginAsync(
        ManualLoginConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.CheckAccess())
        {
            return Task.FromResult(ShowManualLoginConfirmation(request, cancellationToken));
        }

        return Dispatcher
            .InvokeAsync(() => ShowManualLoginConfirmation(request, cancellationToken))
            .Task
            .WaitAsync(cancellationToken);
    }

    private bool ShowManualLoginConfirmation(
        ManualLoginConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var window = new ManualLoginWindow(request.ValidationMessage)
        {
            Owner = this,
        };
        using var registration = cancellationToken.Register(() =>
            window.Dispatcher.BeginInvoke(() => window.Close()));
        var result = window.ShowDialog() == true;
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
