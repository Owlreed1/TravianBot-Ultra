namespace TbotUltra.Worker.Services;

/// <summary>
/// Prevents queued work from reopening a browser after an intentional session shutdown.
/// Only an explicit login is allowed to reopen browser admission.
/// </summary>
internal sealed class BrowserSessionAdmission
{
    private int _isOpen = 1;

    internal void Open() => Interlocked.Exchange(ref _isOpen, 1);

    internal void Close() => Interlocked.Exchange(ref _isOpen, 0);

    internal void ThrowIfClosed()
    {
        if (Volatile.Read(ref _isOpen) == 0)
        {
            throw new OperationCanceledException(
                "Browser session creation is paused until an explicit login starts a new session.");
        }
    }
}
