using System.Collections.Concurrent;
using System.Diagnostics;

namespace NRE.SimAvatar;

/// <summary>
/// Peripheral transport and actuator boundary for the simulated body.
/// Cognition remains in neuronal circuits; this service only moves receptor
/// packets, integrates neuronal motor spikes, and applies physical kinematics.
/// </summary>
public sealed class AvatarService : IDisposable
{
    private const int MaxPendingCommands = 64;
    private const int MaxPublishedSignals = 64;
    private const int MaxPublishedAudioFrames = 4;
    private const int MaxPublishedSightOutputs = 3;
    private const int MaxPublishedActionOutputs = 16;

    private readonly AvatarNervousSystemOptions _options;
    private readonly AvatarServiceClockOptions _clockOptions;
    private readonly AvatarNervousSystem _nervousSystem;
    private readonly BlockingCollection<IAvatarServiceCommand> _commands =
        new(new ConcurrentQueue<IAvatarServiceCommand>(), MaxPendingCommands);
    private readonly BoundedOutputQueue<AvatarNervousSystemSignal> _publishedSignals = new(MaxPublishedSignals);
    private readonly BoundedOutputQueue<AvatarAudioFrame> _publishedAudioFrames = new(MaxPublishedAudioFrames);
    private readonly BoundedOutputQueue<AvatarSightFrame> _publishedSightOutputs = new(MaxPublishedSightOutputs);
    private readonly BoundedOutputQueue<AvatarActionOutput> _publishedActionOutputs = new(MaxPublishedActionOutputs);
    private readonly Thread _workerThread;
    private readonly object _signalGate = new();
    private readonly object _actionOutputGate = new();
    private readonly object _actionPublicationGate = new();
    private readonly object _sightOutputGate = new();
    private readonly object _sightInputGate = new();
    private AvatarNervousSystemSignal _latestSignal = new(0.0, 0.0, 0.0, 0, 0, 0);
    private AvatarActionOutput _latestActionOutput = new(
        new AvatarMotorOutput(0.0, 0.0),
        new AvatarInteractionOutput(0.0),
        0);
    private AvatarSightFrame? _latestSightOutput;
    private AvatarSightFrame? _pendingSightInput;
    private bool _sightInputScheduled;
    private int _disposed;
    private long _enqueuedCommands;
    private long _processedCommands;
    private long _failedCommands;
    private long _clockTicks;
    private long _droppedCommands;
    private double _clockDriveDecayOverride = double.NaN;

    public AvatarService(
        AvatarNervousSystemOptions options,
        string name = "NRE.Avatar.Service",
        AvatarServiceClockOptions? clockOptions = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clockOptions = clockOptions ?? new AvatarServiceClockOptions();
        if (_clockOptions.DriveDecayOverride is double configuredDecay)
        {
            _clockDriveDecayOverride = Math.Clamp(configuredDecay, 0.0, 1.0);
        }

        _nervousSystem = new AvatarNervousSystem(options);
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = string.IsNullOrWhiteSpace(name) ? "NRE.Avatar.Service" : name,
            Priority = ThreadPriority.AboveNormal
        };
        _workerThread.Start();
    }

    public AvatarNervousSystemSignal LatestSignal
    {
        get
        {
            lock (_signalGate)
            {
                return _latestSignal;
            }
        }
    }

    public AvatarActionOutput LatestActionOutput
    {
        get
        {
            lock (_actionOutputGate)
            {
                return _latestActionOutput;
            }
        }
    }

    public AvatarSightFrame? LatestSightOutput
    {
        get
        {
            lock (_sightOutputGate)
            {
                return _latestSightOutput;
            }
        }
    }

    public long EnqueuedCommands => Interlocked.Read(ref _enqueuedCommands);

    public long ProcessedCommands => Interlocked.Read(ref _processedCommands);

    public long FailedCommands => Interlocked.Read(ref _failedCommands);

    public long ClockTicks => Interlocked.Read(ref _clockTicks);

    public long DroppedCommands => Interlocked.Read(ref _droppedCommands);

    public int PendingCommandCount => _commands.Count;

    public int PublishedSignalCount => _publishedSignals.Count;

    public int PublishedSightOutputCount => _publishedSightOutputs.Count;

    public bool TryDequeueSignal(out AvatarNervousSystemSignal signal)
        => _publishedSignals.TryDequeue(out signal);

    public bool TryDequeueAudioInput(out AvatarAudioFrame frame)
        => _publishedAudioFrames.TryDequeue(out frame);

    public bool TryDequeueSightOutput(out AvatarSightFrame frame)
        => _publishedSightOutputs.TryDequeue(out frame);

    public bool TryDequeueActionOutput(out AvatarActionOutput output)
        => _publishedActionOutputs.TryDequeue(out output);

    public void PostBrainSignals(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        ArgumentNullException.ThrowIfNull(dispatches);
        AvatarDispatchSpike[] snapshot = dispatches.Count == 0 ? [] : dispatches.ToArray();
        Post(new BrainSignalsCommand(snapshot));
    }

    public void PostApplyDriveDecay(double? smoothingOverride = null)
        => Post(new ApplyDriveDecayCommand(smoothingOverride));

    public void SetClockDriveDecayOverride(double? smoothingOverride)
    {
        var value = smoothingOverride is double smoothing
            ? Math.Clamp(smoothing, 0.0, 1.0)
            : double.NaN;
        Volatile.Write(ref _clockDriveDecayOverride, value);
    }

    public void PostResetMotor()
        => Post(ResetMotorCommand.Instance);

    public void PostAudioInputFrame(AvatarAudioFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();
        Post(new AudioInputFrameCommand(frame));
    }

    public void PostSightInputFrame(AvatarSightFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();
        lock (_sightInputGate)
        {
            _pendingSightInput = frame;
            if (_sightInputScheduled)
            {
                return;
            }

            _sightInputScheduled = true;
        }

        if (!Post(FlushSightInputCommand.Instance))
        {
            lock (_sightInputGate)
            {
                _pendingSightInput = null;
                _sightInputScheduled = false;
            }
        }
    }

    public AvatarMotorOutput ComputeMotorOutput(
        double forwardGain = 1.0,
        double turnGain = 1.0)
    {
        var signal = LatestSignal;
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(
            signal.LeftMotorDrive,
            signal.RightMotorDrive,
            _options.Kinematics,
            forwardGain,
            turnGain);
        return new AvatarMotorOutput(forwardSpeed, turnRateDeg);
    }

    public AvatarActionOutput PublishActionOutput(
        double forwardGain = 1.0,
        double turnGain = 1.0)
    {
        lock (_actionPublicationGate)
        {
            var output = CreateActionOutput(LatestSignal, forwardGain, turnGain);
            PublishActionOutputCore(output);
            return output;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _commands.CompleteAdding();
        if (_workerThread.IsAlive)
        {
            _workerThread.Join(TimeSpan.FromSeconds(5));
        }

        if (!_workerThread.IsAlive)
        {
            _commands.Dispose();
        }
    }

    private bool Post(IAvatarServiceCommand command)
    {
        if (Volatile.Read(ref _disposed) != 0 || _commands.IsAddingCompleted)
        {
            return false;
        }

        try
        {
            if (!_commands.TryAdd(command))
            {
                Interlocked.Increment(ref _droppedCommands);
                return false;
            }

            Interlocked.Increment(ref _enqueuedCommands);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }
    }

    private void WorkerLoop()
    {
        var clockEnabled = _clockOptions.Enabled && _clockOptions.TickIntervalMs > 0;
        var tickInterval = TimeSpan.FromMilliseconds(Math.Clamp(_clockOptions.TickIntervalMs, 5, 5000));
        var clock = Stopwatch.StartNew();
        var nextTick = clock.Elapsed + tickInterval;

        while (!_commands.IsCompleted)
        {
            var timeoutMs = Timeout.Infinite;
            if (clockEnabled)
            {
                var wait = nextTick - clock.Elapsed;
                timeoutMs = Math.Max(1, (int)wait.TotalMilliseconds);
            }

            if (_commands.TryTake(out var command, timeoutMs))
            {
                ExecuteCommand(command);
            }

            if (!clockEnabled)
            {
                continue;
            }

            var now = clock.Elapsed;
            if (now < nextTick)
            {
                continue;
            }

            ExecuteClockTick();
            do
            {
                nextTick += tickInterval;
            }
            while (nextTick <= now);
        }
    }

    private void ExecuteCommand(IAvatarServiceCommand command)
    {
        try
        {
            var signal = command.Execute(this, _nervousSystem);
            PublishSignal(signal);
            PublishActionOutput(CreateActionOutput(signal));
            Interlocked.Increment(ref _processedCommands);
        }
        catch
        {
            Interlocked.Increment(ref _failedCommands);
            PublishCurrentState();
        }
    }

    private void ExecuteClockTick()
    {
        try
        {
            if (_clockOptions.ApplyDriveDecay)
            {
                var overrideValue = Volatile.Read(ref _clockDriveDecayOverride);
                _nervousSystem.ApplyDriveDecay(double.IsNaN(overrideValue) ? null : overrideValue);
            }

            Interlocked.Increment(ref _clockTicks);
            PublishCurrentState();
        }
        catch
        {
            Interlocked.Increment(ref _failedCommands);
            PublishCurrentState();
        }
    }

    private void PublishCurrentState()
    {
        var signal = CurrentSignal();
        PublishSignal(signal);
        PublishActionOutput(CreateActionOutput(signal));
    }

    private void PublishSignal(AvatarNervousSystemSignal signal)
    {
        lock (_signalGate)
        {
            _latestSignal = signal;
        }

        _publishedSignals.Enqueue(signal);
    }

    private void PublishActionOutput(AvatarActionOutput output)
    {
        lock (_actionPublicationGate)
        {
            PublishActionOutputCore(output);
        }
    }

    private void PublishActionOutputCore(AvatarActionOutput output)
    {
        lock (_actionOutputGate)
        {
            _latestActionOutput = output;
        }

        _publishedActionOutputs.Enqueue(output);
    }

    private AvatarActionOutput CreateActionOutput(
        AvatarNervousSystemSignal signal,
        double forwardGain = 1.0,
        double turnGain = 1.0)
    {
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(
            signal.LeftMotorDrive,
            signal.RightMotorDrive,
            _options.Kinematics,
            forwardGain,
            turnGain);
        return new AvatarActionOutput(
            new AvatarMotorOutput(forwardSpeed, turnRateDeg),
            new AvatarInteractionOutput(signal.ManipulatorDrive),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private void PublishSightOutput(AvatarSightFrame frame)
    {
        lock (_sightOutputGate)
        {
            _latestSightOutput = frame;
        }

        _publishedSightOutputs.Enqueue(frame);
    }

    private AvatarSightFrame? TakePendingSightInput()
    {
        lock (_sightInputGate)
        {
            var frame = _pendingSightInput;
            _pendingSightInput = null;
            _sightInputScheduled = false;
            return frame;
        }
    }

    private AvatarNervousSystemSignal CurrentSignal()
        => new(
            _nervousSystem.LeftMotorDrive,
            _nervousSystem.RightMotorDrive,
            _nervousSystem.ManipulatorDrive,
            _nervousSystem.LastMotorDispatchCount,
            _nervousSystem.LastManipulatorDispatchCount,
            _nervousSystem.TicksWithoutMotorDispatch);

    private interface IAvatarServiceCommand
    {
        AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem);
    }

    private sealed record BrainSignalsCommand(IReadOnlyList<AvatarDispatchSpike> Dispatches) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
            => nervousSystem.InterpretBrainSignals(Dispatches);
    }

    private sealed record ApplyDriveDecayCommand(double? SmoothingOverride) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            nervousSystem.ApplyDriveDecay(SmoothingOverride);
            return service.CurrentSignal();
        }
    }

    private sealed class ResetMotorCommand : IAvatarServiceCommand
    {
        public static ResetMotorCommand Instance { get; } = new();

        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            nervousSystem.ResetMotor();
            return service.CurrentSignal();
        }
    }

    private sealed record AudioInputFrameCommand(AvatarAudioFrame Frame) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            service._publishedAudioFrames.Enqueue(Frame);
            return service.CurrentSignal();
        }
    }

    private sealed class FlushSightInputCommand : IAvatarServiceCommand
    {
        public static FlushSightInputCommand Instance { get; } = new();

        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            var frame = service.TakePendingSightInput();
            if (frame is not null)
            {
                service.PublishSightOutput(frame);
            }

            return service.CurrentSignal();
        }
    }

    private sealed class BoundedOutputQueue<T>
    {
        private readonly Queue<T> _items = new();
        private readonly object _gate = new();
        private readonly int _capacity;

        public BoundedOutputQueue(int capacity)
        {
            _capacity = Math.Max(1, capacity);
        }

        public void Enqueue(T item)
        {
            lock (_gate)
            {
                _items.Enqueue(item);
                while (_items.Count > _capacity)
                {
                    _items.Dequeue();
                }
            }
        }

        public bool TryDequeue(out T item)
        {
            lock (_gate)
            {
                if (_items.Count == 0)
                {
                    item = default!;
                    return false;
                }

                item = _items.Dequeue();
                return true;
            }
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _items.Count;
                }
            }
        }
    }
}
