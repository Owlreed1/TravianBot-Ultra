namespace TbotUltra.Desktop.Services;

public sealed class LatestWinsProjectionCoordinator<T> : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _activeRequest;
    private long _generation;
    private bool _disposed;

    public async Task RequestAsync(
        Func<CancellationToken, Task<T>> createProjection,
        Action<T> applyProjection)
    {
        ArgumentNullException.ThrowIfNull(createProjection);
        ArgumentNullException.ThrowIfNull(applyProjection);

        CancellationTokenSource request;
        long generation;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            generation = ++_generation;
            _activeRequest?.Cancel();
            _activeRequest?.Dispose();
            request = new CancellationTokenSource();
            _activeRequest = request;
        }

        try
        {
            var projection = await createProjection(request.Token);
            lock (_sync)
            {
                if (_disposed || request.IsCancellationRequested || generation != _generation)
                {
                    return;
                }
            }

            applyProjection(projection);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _activeRequest?.Cancel();
            _activeRequest?.Dispose();
            _activeRequest = null;
        }
    }
}
