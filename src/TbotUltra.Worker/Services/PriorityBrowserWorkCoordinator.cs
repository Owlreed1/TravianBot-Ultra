namespace TbotUltra.Worker.Services;

/// <summary>
/// Signals that a short, user-triggered browser operation is waiting for the shared session.
/// Automatic ticks observe this only at task boundaries so they never interrupt a Travian action.
/// </summary>
internal sealed class PriorityBrowserWorkCoordinator
{
    private int _pendingRequests;

    internal bool HasPendingRequest => Volatile.Read(ref _pendingRequests) > 0;

    internal IDisposable EnterPriorityRequest()
    {
        Interlocked.Increment(ref _pendingRequests);
        return new PriorityRequestLease(this);
    }

    private void LeavePriorityRequest()
    {
        Interlocked.Decrement(ref _pendingRequests);
    }

    private sealed class PriorityRequestLease(PriorityBrowserWorkCoordinator coordinator) : IDisposable
    {
        private PriorityBrowserWorkCoordinator? _coordinator = coordinator;

        public void Dispose()
        {
            Interlocked.Exchange(ref _coordinator, null)?.LeavePriorityRequest();
        }
    }
}
