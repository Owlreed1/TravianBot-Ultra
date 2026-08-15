namespace TbotUltra.Desktop.Services;

public sealed class LatestSnapshotWriter<T>(Func<T, Task> writeAsync)
{
    private readonly object _sync = new();
    private readonly Func<T, Task> _writeAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
    private T? _pending;
    private bool _hasPending;
    private bool _isDraining;
    private Task _drainTask = Task.CompletedTask;

    public void Request(T snapshot)
    {
        lock (_sync)
        {
            _pending = snapshot;
            _hasPending = true;
            if (!_isDraining)
            {
                _isDraining = true;
                _drainTask = Task.Run(DrainAsync);
            }
        }
    }

    public async Task WhenIdleAsync()
    {
        while (true)
        {
            Task current;
            lock (_sync)
            {
                current = _drainTask;
            }

            await current;
            lock (_sync)
            {
                if (ReferenceEquals(current, _drainTask) && !_hasPending && !_isDraining)
                {
                    return;
                }
            }
        }
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            T snapshot;
            lock (_sync)
            {
                if (!_hasPending)
                {
                    _isDraining = false;
                    return;
                }

                snapshot = _pending!;
                _pending = default;
                _hasPending = false;
            }

            await _writeAsync(snapshot);
        }
    }
}
