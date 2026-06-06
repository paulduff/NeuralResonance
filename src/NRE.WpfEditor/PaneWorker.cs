using System.Threading;

namespace NRE.WpfEditor;

internal sealed class PaneWorker : IDisposable
{
    private readonly AutoResetEvent _signal = new(false);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    private readonly Thread _thread;
    private Func<CancellationToken, Task>? _pendingWork;
    private bool _disposed;

    public PaneWorker(string name)
    {
        _thread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = name
        };
        _thread.Start();
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
        if (_disposed || _shutdown.IsCancellationRequested)
        {
            return;
        }

        lock (_gate)
        {
            // Keep only the newest pane refresh so slow telemetry cannot build a backlog.
            _pendingWork = work;
        }

        _signal.Set();
    }

    private void WorkerLoop()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            _signal.WaitOne(250);
            if (_shutdown.IsCancellationRequested)
            {
                break;
            }

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
                work(_shutdown.Token).GetAwaiter().GetResult();
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _signal.Set();
        try
        {
            if (_thread.IsAlive)
            {
                _thread.Join(TimeSpan.FromSeconds(1));
            }
        }
        catch
        {
            // Best-effort shutdown.
        }

        _signal.Dispose();
        _shutdown.Dispose();
    }
}
