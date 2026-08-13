namespace TbotUltra.Desktop.Services;

/// <summary>
/// Tracks nested, user-visible browser workflows without putting orchestration work in the task queue.
/// The newest live scope is displayed; disposing a nested scope restores its parent.
/// </summary>
internal sealed class DashboardActivityTracker
{
    private readonly object _gate = new();
    private readonly List<ActivityEntry> _entries = [];

    public event Action? Changed;

    public string? Current
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count == 0 ? null : _entries[^1].DisplayName;
            }
        }
    }

    public IDisposable Begin(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var entry = new ActivityEntry(Guid.NewGuid(), displayName.Trim());
        lock (_gate)
        {
            _entries.Add(entry);
        }

        Changed?.Invoke();
        return new Scope(this, entry.Id);
    }

    private void End(Guid id)
    {
        var changed = false;
        lock (_gate)
        {
            var index = _entries.FindIndex(entry => entry.Id == id);
            if (index >= 0)
            {
                _entries.RemoveAt(index);
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private sealed record ActivityEntry(Guid Id, string DisplayName);

    private sealed class Scope(DashboardActivityTracker owner, Guid id) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.End(id);
            }
        }
    }
}
