using System;
using System.Threading;

namespace NRE.Core.Engine;

/// <summary>
/// A fixed pool of dedicated worker threads for parallelising simulation work.
/// Unlike Parallel.Invoke / Task.Run, these threads are NOT part of the .NET
/// ThreadPool, so they cannot starve (or be starved by) ASP.NET HTTP handling.
///
/// Usage:
///   pool.Run(action0, action1);            // 2-way
///   pool.Run(action0, action1, action2);   // 3-way
///   pool.Run(actions);                     // N-way
///
/// The calling thread participates as worker 0 (no idle thread).
/// </summary>
public sealed class SimWorkerPool : IDisposable
{
    private readonly int _size;
    private readonly Thread[] _threads;
    private readonly ManualResetEventSlim[] _go;      // signal "work available"
    private readonly ManualResetEventSlim[] _done;     // signal "work finished"
    private readonly Action?[] _work;
    private readonly Exception?[] _errors;             // last exception per worker
    private volatile bool _disposed;

    /// <summary>
    /// Most recent exception observed in any worker since last <see cref="Run(Action,Action)"/>.
    /// Cleared at the start of each Run call. Surfaced by Run via rethrow.
    /// </summary>
    public Exception? LastWorkerException { get; private set; }

    public SimWorkerPool(int workerCount)
    {
        _size = Math.Max(1, workerCount);
        _threads = new Thread[_size];
        _go = new ManualResetEventSlim[_size];
        _done = new ManualResetEventSlim[_size];
        _work = new Action?[_size];
        _errors = new Exception?[_size];

        for (int i = 0; i < _size; i++)
        {
            _go[i] = new ManualResetEventSlim(false);
            _done[i] = new ManualResetEventSlim(true); // starts "done"
            int idx = i;
            _threads[i] = new Thread(() => WorkerLoop(idx))
            {
                Name = $"NRE-Worker-{i}",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _threads[i].Start();
        }
    }

    private void WorkerLoop(int idx)
    {
        while (!_disposed)
        {
            _go[idx].Wait(); // block until signalled
            if (_disposed) return;
            _go[idx].Reset();

            try { _work[idx]?.Invoke(); _errors[idx] = null; }
            catch (Exception ex) { _errors[idx] = ex; }

            _done[idx].Set();
        }
    }

    /// <summary>Run 2 actions in parallel. Caller thread runs action 0.</summary>
    public void Run(Action a0, Action a1)
    {
        LastWorkerException = null;
        _work[0] = a1;
        _done[0].Reset();
        _go[0].Set();

        Exception? callerEx = null;
        try { a0(); }
        catch (Exception ex) { callerEx = ex; }

        _done[0].Wait();
        SurfaceErrors(callerEx, 1);
    }

    /// <summary>Run 3 actions in parallel.</summary>
    public void Run(Action a0, Action a1, Action a2)
    {
        LastWorkerException = null;
        _work[0] = a1;
        _work[1] = a2;
        _done[0].Reset(); _done[1].Reset();
        _go[0].Set(); _go[1].Set();

        Exception? callerEx = null;
        try { a0(); }
        catch (Exception ex) { callerEx = ex; }

        _done[0].Wait(); _done[1].Wait();
        SurfaceErrors(callerEx, 2);
    }

    /// <summary>Run 4 actions in parallel.</summary>
    public void Run(Action a0, Action a1, Action a2, Action a3)
    {
        LastWorkerException = null;
        _work[0] = a1;
        _work[1] = a2;
        _work[2] = a3;
        _done[0].Reset(); _done[1].Reset(); _done[2].Reset();
        _go[0].Set(); _go[1].Set(); _go[2].Set();

        Exception? callerEx = null;
        try { a0(); }
        catch (Exception ex) { callerEx = ex; }

        _done[0].Wait(); _done[1].Wait(); _done[2].Wait();
        SurfaceErrors(callerEx, 3);
    }

    private void SurfaceErrors(Exception? callerEx, int workerCount)
    {
        Exception? first = callerEx;
        for (int i = 0; i < workerCount; i++)
        {
            var e = _errors[i];
            if (e != null)
            {
                first ??= e;
                _errors[i] = null;
            }
        }
        if (first != null)
        {
            LastWorkerException = first;
            throw new AggregateException("Sim worker(s) faulted.", first);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        for (int i = 0; i < _size; i++)
            _go[i].Set(); // wake threads so they exit
        for (int i = 0; i < _size; i++)
            _threads[i].Join(500);
        for (int i = 0; i < _size; i++)
        {
            _go[i].Dispose();
            _done[i].Dispose();
        }
    }
}
