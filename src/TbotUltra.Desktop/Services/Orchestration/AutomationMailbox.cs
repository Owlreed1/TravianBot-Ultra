namespace TbotUltra.Desktop.Services.Orchestration;

internal sealed class AutomationMailbox : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private int _wakePending;

    internal bool PostWake()
    {
        if (Interlocked.Exchange(ref _wakePending, 1) == 1)
        {
            return false;
        }

        Signal();
        return true;
    }

    internal void Signal() =>
        TryRelease();

    internal void ConsumeWake() =>
        Interlocked.Exchange(ref _wakePending, 0);

    internal void Reset()
    {
        ConsumeWake();
        while (_signal.Wait(0, CancellationToken.None))
        {
        }
    }

    internal Task WaitAsync(CancellationToken cancellationToken) =>
        _signal.WaitAsync(cancellationToken);

    internal Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _signal.WaitAsync(timeout, cancellationToken);

    public void Dispose() => _signal.Dispose();

    private void TryRelease()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // One queued signal represents every command that can wake the current pass.
        }
    }
}
