using System.Threading;

namespace NRE.WpfEditor;

internal sealed class PaneWorker : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    private readonly Task _workerTask;
    private Func<CancellationToken, Task>? _pendingWork;
    private int _wakePending;
    private int _disposed;

    public PaneWorker(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _workerTask = Task.Run(WorkerLoopAsync);
    }

    public void Post(Action<CancellationToken> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        Post(token =>
        {
            work(token);
            return Task.CompletedTask;
        });
    }

    public void Post(Func<CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (Volatile.Read(ref _disposed) != 0 || _shutdown.IsCancellationRequested)
        {
            return;
        }

        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) != 0 || _shutdown.IsCancellationRequested)
            {
                return;
            }

            // Keep only the newest pane refresh so slow telemetry cannot build a backlog.
            _pendingWork = work;
            if (Interlocked.Exchange(ref _wakePending, 1) == 0)
            {
                _signal.Release();
            }
        }
    }

    private async Task WorkerLoopAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                await _signal.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                Interlocked.Exchange(ref _wakePending, 0);

                Func<CancellationToken, Task>? work;
                lock (_gate)
                {
                    work = _pendingWork;
                    _pendingWork = null;
                }

                if (work is null)
                {
                    continue;
                }

                try
                {
                    await work(_shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Pane refreshes are opportunistic. The next telemetry pass will retry.
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdown.Cancel();
        if (Interlocked.Exchange(ref _wakePending, 1) == 0)
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // A wake is already pending.
            }
        }

        var stopped = _workerTask.IsCompleted;
        try
        {
            if (!stopped)
            {
                stopped = _workerTask.Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch
        {
            // Best-effort shutdown.
        }

        if (stopped)
        {
            _signal.Dispose();
            _shutdown.Dispose();
        }
    }
}
