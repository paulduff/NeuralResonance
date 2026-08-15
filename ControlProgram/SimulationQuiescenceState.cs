internal sealed record SimulationQuiescenceSnapshot(
    bool PauseRequested,
    bool IsQuiesced,
    int ActiveTicks,
    long Generation);

internal sealed class SimulationQuiescenceState
{
    private readonly object _gate = new();
    private bool _pauseRequested;
    private int _activeTicks;
    private long _generation;
    private TaskCompletionSource _resume = NewSignal();
    private TaskCompletionSource _quiesced = NewSignal();

    public async ValueTask<IDisposable> EnterTickAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task resumeTask;
            lock (_gate)
            {
                if (!_pauseRequested)
                {
                    _activeTicks++;
                    return new TickLease(this);
                }

                resumeTask = _resume.Task;
            }

            await resumeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<SimulationQuiescenceSnapshot> QuiesceAsync(CancellationToken cancellationToken)
    {
        Task quiescedTask;
        lock (_gate)
        {
            if (!_pauseRequested)
            {
                _pauseRequested = true;
                _generation++;
                _resume = NewSignal();
                _quiesced = NewSignal();
            }

            if (_activeTicks == 0)
            {
                _quiesced.TrySetResult();
            }

            quiescedTask = _quiesced.Task;
        }

        await quiescedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        return GetSnapshot();
    }

    public SimulationQuiescenceSnapshot Resume()
    {
        TaskCompletionSource? resume = null;
        lock (_gate)
        {
            if (_pauseRequested)
            {
                _pauseRequested = false;
                _generation++;
                resume = _resume;
            }
        }

        resume?.TrySetResult();
        return GetSnapshot();
    }

    public SimulationQuiescenceSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new SimulationQuiescenceSnapshot(
                _pauseRequested,
                _pauseRequested && _activeTicks == 0,
                _activeTicks,
                _generation);
        }
    }

    private void ExitTick()
    {
        TaskCompletionSource? quiesced = null;
        lock (_gate)
        {
            _activeTicks = Math.Max(0, _activeTicks - 1);
            if (_pauseRequested && _activeTicks == 0)
            {
                quiesced = _quiesced;
            }
        }

        quiesced?.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class TickLease(SimulationQuiescenceState owner) : IDisposable
    {
        private SimulationQuiescenceState? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ExitTick();
    }
}
