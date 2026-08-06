using System.Collections.Concurrent;
using System.Diagnostics;

namespace NRE.SimAvatar;

public sealed class AvatarService : IDisposable
{
    private readonly AvatarNervousSystemOptions _options;
    private readonly AvatarServiceClockOptions _clockOptions;
    private readonly AvatarNervousSystem _nervousSystem;
    private readonly BlockingCollection<IAvatarServiceCommand> _commands = new(new ConcurrentQueue<IAvatarServiceCommand>(), MaxPendingCommands);
    private readonly BoundedOutputQueue<AvatarNervousSystemSignal> _publishedSignals = new(MaxPublishedSignals);
    private readonly BoundedOutputQueue<AvatarAuditoryCue> _publishedAuditoryInputs = new(MaxPublishedAuditoryInputs);
    private readonly BoundedOutputQueue<AvatarAudioOutput> _publishedAudioOutputs = new(MaxPublishedAudioOutputs);
    private readonly BoundedOutputQueue<AvatarBodyStateInput> _publishedBodyInputs = new(MaxPublishedBodyInputs);
    private readonly BoundedOutputQueue<AvatarOutcomeTelemetry> _publishedOutcomes = new(MaxPublishedOutcomes);
    private readonly BoundedOutputQueue<AvatarObjectObservation> _publishedObjectObservations = new(MaxPublishedObjectObservations);
    private readonly BoundedOutputQueue<AvatarSightFrame> _publishedSightOutputs = new(MaxPublishedSightOutputs);
    private readonly BoundedOutputQueue<AvatarActionOutput> _publishedActionOutputs = new(MaxPublishedActionOutputs);
    private readonly BoundedOutputQueue<AvatarAttentionOutput> _publishedAttentionOutputs = new(MaxPublishedAttentionOutputs);
    private readonly BoundedOutputQueue<AvatarAudioOutput> _publishedVoiceOutputs = new(MaxPublishedPeripheralOutputs);
    private readonly BoundedOutputQueue<AvatarGestureOutput> _publishedGestureOutputs = new(MaxPublishedPeripheralOutputs);
    private readonly BoundedOutputQueue<AvatarArousalOutput> _publishedArousalOutputs = new(MaxPublishedPeripheralOutputs);
    private readonly BoundedOutputQueue<AvatarBodySoundOutput> _publishedBodySoundOutputs = new(MaxPublishedPeripheralOutputs);
    private readonly BoundedOutputQueue<AvatarNeedsRhythmState> _publishedNeedsRhythmStates = new(MaxPublishedNeedsRhythmStates);
    private readonly BoundedOutputQueue<AvatarReflexOutput> _publishedReflexOutputs = new(MaxPublishedPeripheralOutputs);
    private readonly BoundedOutputQueue<AvatarAffectiveWeather> _publishedAffectiveWeather = new(MaxPublishedPeripheralOutputs);
    private readonly Thread _workerThread;
    private readonly object _signalGate = new();
    private readonly object _memoryGate = new();
    private readonly object _actionOutputGate = new();
    private readonly object _bodyEventGate = new();
    private readonly object _needsRhythmGate = new();
    private readonly object _sightOutputGate = new();
    private readonly object _sightInputGate = new();
    private readonly object _placeMemoryGate = new();
    private readonly object _actionPublicationGate = new();
    private readonly List<AvatarBodyEvent> _bodyEventLedger = new(64);
    private readonly Dictionary<string, AvatarPlaceMemory> _placeMemories = new(StringComparer.OrdinalIgnoreCase);
    private AvatarNervousSystemSignal _latestSignal = new(0.0, 0.0, 0, 0, AvatarToolSignal.None);
    private AvatarSensationMemory _recentSensationMemory = AvatarSensationMemory.Empty;
    private AvatarSightFrame? _latestSightOutput;
    private AvatarSightFrame? _pendingSightInput;
    private bool _sightInputScheduled;
    private AvatarActionOutput _latestActionOutput = new(
        new AvatarMotorOutput(0.0, 0.0),
        AvatarToolSignal.None,
        AvatarAttentionOutput.None(),
        null,
        AvatarGestureOutput.None(),
        AvatarArousalOutput.None(),
        AvatarBodySoundOutput.None(),
        AvatarNeedsRhythmState.Resting(),
        AvatarReflexOutput.None(),
        AvatarAffectiveWeather.Neutral(),
        0);
    private AvatarAttentionOutput _latestAttentionOutput = AvatarAttentionOutput.None();
    private AvatarAudioOutput? _latestVoiceOutput;
    private AvatarGestureOutput _latestGestureOutput = AvatarGestureOutput.None();
    private AvatarArousalOutput _latestArousalOutput = AvatarArousalOutput.None();
    private AvatarBodySoundOutput _latestBodySoundOutput = AvatarBodySoundOutput.None();
    private AvatarNeedsRhythmState _latestNeedsRhythmState = AvatarNeedsRhythmState.Resting();
    private AvatarReflexOutput _latestReflexOutput = AvatarReflexOutput.None();
    private AvatarAffectiveWeather _latestAffectiveWeather = AvatarAffectiveWeather.Neutral();
    private int _disposed;
    private long _enqueuedCommands;
    private long _processedCommands;
    private long _failedCommands;
    private long _clockTicks;
    private long _lastActionConsequenceUnixMs;
    private double _clockDriveDecayOverride = double.NaN;
    private long _droppedCommands;
    private const int MaxPendingCommands = 64;
    private const int MaxPublishedSignals = 64;
    private const int MaxPublishedAuditoryInputs = 32;
    private const int MaxPublishedAudioOutputs = 32;
    private const int MaxPublishedBodyInputs = 32;
    private const int MaxPublishedOutcomes = 32;
    private const int MaxPublishedObjectObservations = 64;
    private const int MaxPublishedSightOutputs = 3;
    private const int MaxPublishedActionOutputs = 16;
    private const int MaxPublishedAttentionOutputs = 16;
    private const int MaxPublishedPeripheralOutputs = 16;
    private const int MaxPublishedNeedsRhythmStates = 16;
    private const int MaxBodyEventLedgerEntries = 96;
    private const int MaxPlaceMemoryEntries = 256;

    public AvatarService(
        AvatarNervousSystemOptions options,
        string name = "NRE.Avatar.Service",
        AvatarServiceClockOptions? clockOptions = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clockOptions = clockOptions ?? new AvatarServiceClockOptions();
        if (_clockOptions.DriveDecayOverride is double configuredDecay)
        {
            _clockDriveDecayOverride = configuredDecay;
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

    public long EnqueuedCommands => Interlocked.Read(ref _enqueuedCommands);

    public long ProcessedCommands => Interlocked.Read(ref _processedCommands);

    public long FailedCommands => Interlocked.Read(ref _failedCommands);

    public long ClockTicks => Interlocked.Read(ref _clockTicks);

    public long DroppedCommands => Interlocked.Read(ref _droppedCommands);

    public int PendingCommandCount => _commands.Count;

    public int PublishedSignalCount => _publishedSignals.Count;

    public int PublishedSightOutputCount => _publishedSightOutputs.Count;

    public AvatarSensationMemory RecentSensationMemory
    {
        get
        {
            lock (_memoryGate)
            {
                return _recentSensationMemory;
            }
        }
    }

    public IReadOnlyList<AvatarBodyEvent> RecentBodyEvents
    {
        get
        {
            lock (_bodyEventGate)
            {
                return _bodyEventLedger.ToArray();
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

    public IReadOnlyList<AvatarPlaceMemory> PlaceMemories
    {
        get
        {
            lock (_placeMemoryGate)
            {
                return _placeMemories.Values
                    .OrderByDescending(static item => item.LastSeenUnixMs)
                    .ThenBy(static item => item.PlaceId)
                    .ToArray();
            }
        }
    }

    public AvatarSelfDiagnostics CurrentSelfDiagnostics
    {
        get
        {
            var memory = RecentSensationMemory;
            var action = LatestActionOutput;
            var events = RecentBodyEvents;
            var recentEvent = events.Count > 0 ? events[^1] : default;
            return new AvatarSelfDiagnostics(
                BodyMood: string.IsNullOrWhiteSpace(memory.BodyMood) ? "unknown" : memory.BodyMood,
                AttentionTarget: string.IsNullOrWhiteSpace(memory.AttentionTarget) ? "none" : memory.AttentionTarget,
                CurrentAction: DescribeAction(action),
                LastSensation: DescribeLastSensation(memory),
                CurrentNeed: string.IsNullOrWhiteSpace(action.Needs.DominantNeed) ? "none" : action.Needs.DominantNeed,
                RecentBodyEvent: string.IsNullOrWhiteSpace(recentEvent.Kind)
                    ? "none"
                    : $"{recentEvent.Kind}: {recentEvent.Description}",
                UpdatedUnixMs: Math.Max(memory.UpdatedUnixMs, action.EmittedUnixMs));
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

    public AvatarAttentionOutput LatestAttentionOutput
    {
        get
        {
            lock (_actionOutputGate)
            {
                return _latestAttentionOutput;
            }
        }
    }

    public AvatarAudioOutput? LatestVoiceOutput
    {
        get
        {
            lock (_actionOutputGate)
            {
                return _latestVoiceOutput;
            }
        }
    }

    public AvatarGestureOutput LatestGestureOutput
    {
        get
        {
            lock (_actionOutputGate)
            {
                return _latestGestureOutput;
            }
        }
    }

    public AvatarArousalOutput LatestArousalOutput
    {
        get
        {
            lock (_actionOutputGate)
            {
                return _latestArousalOutput;
            }
        }
    }

    public AvatarBodySoundOutput LatestBodySoundOutput
    {
        get
        {
            lock (_actionOutputGate)
            {
                return _latestBodySoundOutput;
            }
        }
    }

    public AvatarNeedsRhythmState LatestNeedsRhythmState
    {
        get
        {
            lock (_needsRhythmGate)
            {
                return _latestNeedsRhythmState;
            }
        }
    }

    public AvatarReflexOutput LatestReflexOutput
    {
        get
        {
            lock (_actionOutputGate)
            {
                return _latestReflexOutput;
            }
        }
    }

    public AvatarAffectiveWeather LatestAffectiveWeather
    {
        get
        {
            lock (_actionOutputGate)
            {
                return _latestAffectiveWeather;
            }
        }
    }

    public bool TryDequeueSignal(out AvatarNervousSystemSignal signal)
        => _publishedSignals.TryDequeue(out signal);

    public bool TryDequeueAuditoryInput(out AvatarAuditoryCue cue)
        => _publishedAuditoryInputs.TryDequeue(out cue);

    public bool TryDequeueAudioOutput(out AvatarAudioOutput output)
        => _publishedAudioOutputs.TryDequeue(out output);

    public bool TryDequeueBodyInput(out AvatarBodyStateInput input)
        => _publishedBodyInputs.TryDequeue(out input);

    public bool TryDequeueOutcome(out AvatarOutcomeTelemetry outcome)
        => _publishedOutcomes.TryDequeue(out outcome);

    public bool TryDequeueObjectObservation(out AvatarObjectObservation observation)
        => _publishedObjectObservations.TryDequeue(out observation);

    public bool TryDequeueSightOutput(out AvatarSightFrame frame)
        => _publishedSightOutputs.TryDequeue(out frame);

    public bool TryDequeueActionOutput(out AvatarActionOutput output)
        => _publishedActionOutputs.TryDequeue(out output);

    public bool TryDequeueAttentionOutput(out AvatarAttentionOutput output)
        => _publishedAttentionOutputs.TryDequeue(out output);

    public bool TryDequeueVoiceOutput(out AvatarAudioOutput output)
        => _publishedVoiceOutputs.TryDequeue(out output);

    public bool TryDequeueGestureOutput(out AvatarGestureOutput output)
        => _publishedGestureOutputs.TryDequeue(out output);

    public bool TryDequeueArousalOutput(out AvatarArousalOutput output)
        => _publishedArousalOutputs.TryDequeue(out output);

    public bool TryDequeueBodySoundOutput(out AvatarBodySoundOutput output)
        => _publishedBodySoundOutputs.TryDequeue(out output);

    public bool TryDequeueNeedsRhythmState(out AvatarNeedsRhythmState state)
        => _publishedNeedsRhythmStates.TryDequeue(out state);

    public bool TryDequeueReflexOutput(out AvatarReflexOutput output)
        => _publishedReflexOutputs.TryDequeue(out output);

    public bool TryDequeueAffectiveWeather(out AvatarAffectiveWeather weather)
        => _publishedAffectiveWeather.TryDequeue(out weather);

    public void PostBrainSignals(IReadOnlyList<AvatarDispatchSpike> dispatches, AvatarNervousSystemBodyState body)
    {
        ArgumentNullException.ThrowIfNull(dispatches);
        AvatarDispatchSpike[] snapshot = dispatches.Count == 0 ? [] : dispatches.ToArray();
        Post(new BrainSignalsCommand(snapshot, body));
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

    public void PostAddMotorDrive(double leftDelta, double rightDelta)
        => Post(new AddMotorDriveCommand(leftDelta, rightDelta));

    public void PostSetMotorDrive(double left, double right)
        => Post(new SetMotorDriveCommand(left, right));

    public void PostResetMotor()
        => Post(ResetMotorCommand.Instance);

    public void PostAuditoryInputCandidates(IEnumerable<AvatarAuditoryCue> cues, int maxCues = 1)
    {
        ArgumentNullException.ThrowIfNull(cues);
        Post(new AuditoryInputCandidatesCommand(cues.ToArray(), Math.Clamp(maxCues, 1, 8)));
    }

    public void PostAudioOutput(AvatarAudioOutput output)
        => Post(new AudioOutputCommand(output));

    public void PostBodyInput(AvatarBodyTelemetry telemetry, AvatarBodyStateProfile profile)
        => Post(new BodyInputCommand(new AvatarBodyStateInput(telemetry, profile)));

    public void PostOutcome(AvatarOutcomeTelemetry outcome)
        => Post(new OutcomeCommand(outcome));

    public void PostObjectCandidates(IEnumerable<AvatarObjectObservation> observations, int maxObservations = 1)
    {
        ArgumentNullException.ThrowIfNull(observations);
        Post(new ObjectCandidatesCommand(observations.ToArray(), Math.Clamp(maxObservations, 1, 8)));
    }

    public void PostPlaceObservations(IEnumerable<AvatarPlaceObservation> observations, int maxObservations = 8)
    {
        ArgumentNullException.ThrowIfNull(observations);
        Post(new PlaceObservationsCommand(observations.ToArray(), Math.Clamp(maxObservations, 1, 32)));
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
        double turnGain = 1.0,
        double forwardScale = 1.0)
    {
        var signal = LatestSignal;
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(
            signal.LeftMotorDrive,
            signal.RightMotorDrive,
            _options.Kinematics,
            forwardGain,
            turnGain,
            forwardScale);
        return new AvatarMotorOutput(forwardSpeed, turnRateDeg);
    }

    public AvatarActionOutput PublishActionOutput(
        double forwardGain = 1.0,
        double turnGain = 1.0,
        double forwardScale = 1.0)
    {
        lock (_actionPublicationGate)
        {
            var signal = LatestSignal;
            var output = CreateActionOutput(signal, forwardGain, turnGain, forwardScale);
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
            // Shutdown race: safe to ignore.
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
            var signal = CurrentSignal();
            PublishSignal(signal);
            PublishActionOutput(CreateActionOutput(signal));
        }
    }

    private void ExecuteClockTick()
    {
        try
        {
            if (_clockOptions.ApplyDriveDecay)
            {
                var overrideValue = Volatile.Read(ref _clockDriveDecayOverride);
                double? smoothing = double.IsNaN(overrideValue)
                    ? null
                    : overrideValue;
                _nervousSystem.ApplyDriveDecay(smoothing);
            }

            UpdateNeedsRhythmState();
            Interlocked.Increment(ref _clockTicks);
            var signal = CurrentSignal();
            PublishSignal(signal);
            PublishActionOutput(CreateActionOutput(signal));
        }
        catch
        {
            Interlocked.Increment(ref _failedCommands);
            var signal = CurrentSignal();
            PublishSignal(signal);
            PublishActionOutput(CreateActionOutput(signal));
        }
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
            _latestAttentionOutput = output.Attention;
            _latestVoiceOutput = output.Voice;
            _latestGestureOutput = output.Gesture;
            _latestArousalOutput = output.Arousal;
            _latestBodySoundOutput = output.BodySound;
            _latestReflexOutput = output.Reflex;
            _latestAffectiveWeather = output.Weather;
        }

        AppendActionConsequenceEvents(output);

        _publishedActionOutputs.Enqueue(output);
        _publishedAttentionOutputs.Enqueue(output.Attention);
        if (output.Voice is AvatarAudioOutput voice)
        {
            _publishedVoiceOutputs.Enqueue(voice);
        }

        _publishedGestureOutputs.Enqueue(output.Gesture);
        _publishedArousalOutputs.Enqueue(output.Arousal);
        _publishedBodySoundOutputs.Enqueue(output.BodySound);
        _publishedNeedsRhythmStates.Enqueue(output.Needs);
        _publishedReflexOutputs.Enqueue(output.Reflex);
        _publishedAffectiveWeather.Enqueue(output.Weather);
    }

    private void AppendActionConsequenceEvents(AvatarActionOutput output)
    {
        var nowMs = output.EmittedUnixMs > 0
            ? output.EmittedUnixMs
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var lastMs = Interlocked.Read(ref _lastActionConsequenceUnixMs);
        if ((nowMs - lastMs) < 220)
        {
            return;
        }

        var events = DeriveActionConsequenceEvents(output, nowMs);
        if (events.Count == 0)
        {
            return;
        }

        Interlocked.Exchange(ref _lastActionConsequenceUnixMs, nowMs);
        AppendBodyEvents(events);
    }

    private static IReadOnlyList<AvatarBodyEvent> DeriveActionConsequenceEvents(AvatarActionOutput output, long nowMs)
    {
        var events = new List<AvatarBodyEvent>(3);
        var movement = output.Movement;
        var movementIntensity = Math.Clamp(
            (Math.Abs(movement.ForwardSpeed) * 0.28) +
            (Math.Abs(movement.TurnRateDeg) / 360.0 * 0.42),
            0.0,
            1.0);
        if (movementIntensity >= 0.05)
        {
            events.Add(new AvatarBodyEvent(
                "effort",
                movementIntensity,
                "action used bodily effort",
                nowMs,
                "avatar_action"));
        }

        if (output.Tool.HasAction)
        {
            events.Add(new AvatarBodyEvent(
                "tool",
                Math.Clamp(output.Tool.Strength, 0.0, 1.0),
                $"{output.Tool.Action.ToString().ToLowerInvariant()} toward {output.Tool.Direction.ToString().ToLowerInvariant()}",
                nowMs,
                "avatar_action"));
        }

        if (output.Gesture.Name is not "none" && output.Gesture.Intensity >= 0.10)
        {
            events.Add(new AvatarBodyEvent(
                "expression",
                Math.Clamp(output.Gesture.Intensity, 0.0, 1.0),
                output.Gesture.Name,
                nowMs,
                "avatar_action"));
        }

        return events;
    }

    private static string DescribeAction(AvatarActionOutput action)
    {
        if (action.Tool.HasAction)
        {
            return $"{action.Tool.Action.ToString().ToLowerInvariant()} {action.Tool.Direction.ToString().ToLowerInvariant()}";
        }

        var moving = Math.Abs(action.Movement.ForwardSpeed) >= 0.03;
        var turning = Math.Abs(action.Movement.TurnRateDeg) >= 3.0;
        return (moving, turning) switch
        {
            (true, true) => action.Movement.ForwardSpeed >= 0.0 ? "moving and turning" : "backing and turning",
            (true, false) => action.Movement.ForwardSpeed >= 0.0 ? "moving" : "backing",
            (false, true) => "turning",
            _ when action.Reflex.Name is not "none" => action.Reflex.Name,
            _ when action.Gesture.Name is not "none" => action.Gesture.Name,
            _ => "resting"
        };
    }

    private static string DescribeLastSensation(AvatarSensationMemory memory)
    {
        if (memory.LastSeenObject is AvatarObjectObservation seen)
        {
            return $"saw {seen.Label}";
        }

        if (memory.LastHeardSound is AvatarAuditoryCue heard)
        {
            return $"heard {heard.Pattern}";
        }

        if (memory.LastOutcome is AvatarOutcomeTelemetry outcome)
        {
            if (Math.Max(outcome.PainLevel, outcome.DamageLevel) >= 0.05)
            {
                return "felt impact";
            }

            if (Math.Max(outcome.SafetyRelief, outcome.ShelterComfort) >= 0.05)
            {
                return "felt relief";
            }

            if (outcome.SatietyRelief >= 0.05)
            {
                return "felt fed";
            }
        }

        if (memory.LastBodyState is not null)
        {
            return "felt body state";
        }

        return "none";
    }

    private AvatarActionOutput CreateActionOutput(
        AvatarNervousSystemSignal signal,
        double forwardGain = 1.0,
        double turnGain = 1.0,
        double forwardScale = 1.0)
    {
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(
            signal.LeftMotorDrive,
            signal.RightMotorDrive,
            _options.Kinematics,
            forwardGain,
            turnGain,
            forwardScale);
        var attention = CreateAttentionOutput();
        var emittedUnixMs = attention.EmittedUnixMs > 0
            ? attention.EmittedUnixMs
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var memory = RecentSensationMemory;
        var needs = LatestNeedsRhythmState;
        var voice = CreateVoiceOutput(memory);
        var weather = CreateAffectiveWeather(memory, needs, emittedUnixMs);
        var reflex = CreateReflexOutput(memory, needs, emittedUnixMs);
        var reflexedForwardSpeed = forwardSpeed * Math.Clamp(reflex.ForwardScale, 0.0, 1.25);
        var reflexedTurnRateDeg = turnRateDeg + reflex.TurnBiasDeg;
        return new AvatarActionOutput(
            new AvatarMotorOutput(reflexedForwardSpeed, reflexedTurnRateDeg),
            signal.Tool,
            attention,
            voice,
            CreateGestureOutput(signal.Tool, memory, weather, emittedUnixMs),
            CreateArousalOutput(memory, needs, weather, emittedUnixMs),
            CreateBodySoundOutput(memory, needs, weather, emittedUnixMs),
            needs,
            reflex,
            weather,
            emittedUnixMs);
    }

    private AvatarAttentionOutput CreateAttentionOutput()
    {
        var memory = RecentSensationMemory;
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (memory.LastSeenObject is AvatarObjectObservation seen)
        {
            return new AvatarAttentionOutput(
                Mode: "look",
                Target: string.IsNullOrWhiteSpace(memory.AttentionTarget)
                    ? $"{seen.Label}:{seen.ObjectId}"
                    : memory.AttentionTarget,
                Hemisphere: seen.Hemisphere,
                Confidence: Math.Clamp(seen.Confidence, 0.0, 1.0),
                Salience: Math.Clamp(seen.Salience, 0.0, 1.0),
                EmittedUnixMs: nowMs);
        }

        if (memory.LastHeardSound is AvatarAuditoryCue heard)
        {
            return new AvatarAttentionOutput(
                Mode: "listen",
                Target: string.IsNullOrWhiteSpace(memory.AttentionTarget)
                    ? heard.Pattern
                    : memory.AttentionTarget,
                Hemisphere: heard.Hemisphere,
                Confidence: Math.Clamp(heard.Intensity / 3.0, 0.0, 1.0),
                Salience: Math.Clamp(heard.Intensity / 3.0, 0.0, 1.0),
                EmittedUnixMs: nowMs);
        }

        return AvatarAttentionOutput.None(nowMs);
    }

    private static AvatarAudioOutput? CreateVoiceOutput(AvatarSensationMemory memory)
        => memory.LastAudioOutput;

    private static AvatarAffectiveWeather CreateAffectiveWeather(
        AvatarSensationMemory memory,
        AvatarNeedsRhythmState needs,
        long emittedUnixMs)
    {
        var events = new[]
        {
            ("hurt", RecentEventScore(memory, "pain"), -0.80, "pain or damage"),
            ("tense", Math.Max(needs.Stress, RecentEventScore(memory, "fear")), -0.50, "threat or anxiety"),
            ("hungry", needs.Hunger, -0.35, "hunger"),
            ("tired", Math.Max(needs.Fatigue, needs.SleepPressure), -0.30, "fatigue or sleep pressure"),
            ("curious", needs.Curiosity, 0.35, "novelty"),
            ("sheltered", Math.Max(needs.Recovery, RecentEventScore(memory, "shelter")), 0.55, "shelter or safety"),
            ("relieved", RecentEventScore(memory, "relief"), 0.70, "relief"),
            ("blocked", RecentEventScore(memory, "impact"), -0.40, "impact or obstruction")
        };

        var best = events.OrderByDescending(static item => item.Item2).First();
        if (best.Item2 < 0.20)
        {
            return AvatarAffectiveWeather.Neutral(emittedUnixMs);
        }

        var arousal = Math.Clamp(
            needs.Stress * 0.55 +
            needs.Restlessness * 0.24 +
            needs.Curiosity * 0.18 +
            best.Item2 * 0.28,
            0.0,
            1.0);
        return new AvatarAffectiveWeather(
            State: best.Item1,
            Valence: Math.Clamp(best.Item3, -1.0, 1.0),
            Arousal: arousal,
            Confidence: Math.Clamp(best.Item2, 0.0, 1.0),
            Reason: best.Item4,
            UpdatedUnixMs: emittedUnixMs);
    }

    private static double RecentEventScore(AvatarSensationMemory memory, string eventKind)
    {
        if (memory.LastOutcome is AvatarOutcomeTelemetry outcome)
        {
            return eventKind switch
            {
                "relief" => Math.Clamp(Math.Max(outcome.SafetyRelief, Math.Max(outcome.ShelterComfort, outcome.SatietyRelief)), 0.0, 1.0),
                "pain" => Math.Clamp(Math.Max(outcome.PainLevel, outcome.DamageLevel), 0.0, 1.0),
                _ => 0.0
            };
        }

        if (memory.LastBodyState is AvatarBodyStateInput body)
        {
            var telemetry = body.Telemetry;
            return eventKind switch
            {
                "pain" => Math.Clamp(Math.Max(telemetry.PainLevel, 1.0 - telemetry.Health), 0.0, 1.0),
                "fear" => Math.Clamp(Math.Max(telemetry.PredatorThreat, telemetry.Anxiety), 0.0, 1.0),
                "shelter" => Math.Clamp(Math.Max(telemetry.InShelter, telemetry.ShelterSafety), 0.0, 1.0),
                "impact" => Math.Clamp(telemetry.ContactLevel, 0.0, 1.0),
                _ => 0.0
            };
        }

        return 0.0;
    }

    private static AvatarGestureOutput CreateGestureOutput(
        AvatarToolSignal tool,
        AvatarSensationMemory memory,
        AvatarAffectiveWeather weather,
        long emittedUnixMs)
    {
        if (tool.HasAction)
        {
            return new AvatarGestureOutput(
                Name: tool.Action.ToString().ToLowerInvariant(),
                Intensity: Math.Clamp(tool.Strength, 0.0, 1.0),
                Direction: tool.Direction.ToString().ToLowerInvariant(),
                EmittedUnixMs: emittedUnixMs);
        }

        if (weather.State is "hurt")
        {
            return new AvatarGestureOutput("guard_body", 0.78, null, emittedUnixMs);
        }

        if (weather.State is "tense")
        {
            return new AvatarGestureOutput("brace", 0.72, null, emittedUnixMs);
        }

        return memory.BodyMood switch
        {
            "hurt" => new AvatarGestureOutput("guard_body", 0.75, null, emittedUnixMs),
            "threatened" => new AvatarGestureOutput("brace", 0.70, null, emittedUnixMs),
            "hungry" => new AvatarGestureOutput("seek", 0.45, null, emittedUnixMs),
            "sheltered" => new AvatarGestureOutput("settle", 0.35, null, emittedUnixMs),
            _ => AvatarGestureOutput.None(emittedUnixMs)
        };
    }

    private static AvatarArousalOutput CreateArousalOutput(
        AvatarSensationMemory memory,
        AvatarNeedsRhythmState needs,
        AvatarAffectiveWeather weather,
        long emittedUnixMs)
    {
        var level = Math.Max(weather.Arousal, Math.Max(0.10, (needs.Stress * 0.50) + (needs.Restlessness * 0.22) + (needs.Curiosity * 0.16)));
        var mode = "rest";
        var reason = weather.State is not "calm" ? weather.State : needs.DominantNeed;

        if (memory.LastBodyState is AvatarBodyStateInput body)
        {
            var telemetry = body.Telemetry;
            level = Math.Clamp(
                0.12 +
                (telemetry.Anxiety * 0.34) +
                (telemetry.PredatorThreat * 0.44) +
                (telemetry.PainLevel * 0.34) +
                (telemetry.Urgency * 0.28) +
                (telemetry.Hunger * 0.16) +
                ((1.0 - telemetry.Health) * 0.24),
                0.0,
                1.0);
        }

        if (memory.LastOutcome is AvatarOutcomeTelemetry outcome)
        {
            level = Math.Clamp(
                level +
                (outcome.PainLevel * 0.30) +
                (outcome.DamageLevel * 0.32) +
                (outcome.Novelty * 0.12) -
                (outcome.SafetyRelief * 0.18) -
                (outcome.ShelterComfort * 0.12),
                0.0,
                1.0);
        }

        if (level >= 0.70)
        {
            mode = "alarm";
        }
        else if (level >= 0.42)
        {
            mode = "alert";
        }
        else if (level >= 0.22)
        {
            mode = "engaged";
        }

        if (string.IsNullOrWhiteSpace(reason) || reason is "unknown" or "none")
        {
            reason = string.IsNullOrWhiteSpace(memory.BodyMood) ? mode : memory.BodyMood;
        }

        return new AvatarArousalOutput(level, mode, reason, emittedUnixMs);
    }

    private static AvatarBodySoundOutput CreateBodySoundOutput(
        AvatarSensationMemory memory,
        AvatarNeedsRhythmState needs,
        AvatarAffectiveWeather weather,
        long emittedUnixMs)
    {
        if (memory.LastBodyState is not AvatarBodyStateInput body)
        {
            return weather.State is "tense"
                ? new AvatarBodySoundOutput("breath", 0.35, emittedUnixMs)
                : AvatarBodySoundOutput.None(emittedUnixMs);
        }

        var telemetry = body.Telemetry;
        if (telemetry.PainLevel >= 0.35)
        {
            return new AvatarBodySoundOutput("pain_breath", Math.Clamp(telemetry.PainLevel, 0.0, 1.0), emittedUnixMs);
        }

        if (telemetry.ContactLevel >= 0.35)
        {
            return new AvatarBodySoundOutput("impact", Math.Clamp(telemetry.ContactLevel, 0.0, 1.0), emittedUnixMs);
        }

        var motion = Math.Clamp(Math.Abs(telemetry.ForwardVelocity) / Math.Max(body.Profile.MaxForwardSpeed, 0.001), 0.0, 1.0);
        if (motion >= 0.08)
        {
            return new AvatarBodySoundOutput("footstep", motion, emittedUnixMs);
        }

        if (memory.BodyMood is "threatened" or "hurt")
        {
            return new AvatarBodySoundOutput("breath", 0.35, emittedUnixMs);
        }

        if (needs.Fatigue >= 0.55 || needs.SleepPressure >= 0.55)
        {
            return new AvatarBodySoundOutput("tired_breath", Math.Clamp(Math.Max(needs.Fatigue, needs.SleepPressure), 0.0, 1.0), emittedUnixMs);
        }

        return AvatarBodySoundOutput.None(emittedUnixMs);
    }

    private static AvatarReflexOutput CreateReflexOutput(
        AvatarSensationMemory memory,
        AvatarNeedsRhythmState needs,
        long emittedUnixMs)
    {
        if (memory.LastBodyState is AvatarBodyStateInput body)
        {
            var telemetry = body.Telemetry;
            // Directional tactile channels may report nearby surfaces before
            // impact. ContactLevel is the explicit gate; tactile asymmetry
            // selects the withdrawal direction after contact is established.
            var contact = Math.Clamp(telemetry.ContactLevel, 0.0, 1.0);
            if (contact >= 0.35)
            {
                var intensity = Math.Clamp(Math.Max(contact, telemetry.PainLevel), 0.0, 1.0);
                var sideBias = telemetry.TactileLeft - telemetry.TactileRight;
                var turnBias = Math.Clamp(sideBias * 70.0, -70.0, 70.0);
                if (Math.Abs(turnBias) < 8.0 && telemetry.TactileFront >= 0.35)
                {
                    // A symmetric head-on impact still needs one stable escape
                    // side. Continue an existing turn; otherwise break symmetry
                    // deterministically until fresh tactile input says otherwise.
                    turnBias = telemetry.TurnRateDeg < -4.0 ? -55.0 : 55.0;
                }

                return new AvatarReflexOutput(
                    Name: "withdraw_contact",
                    Intensity: intensity,
                    ForwardScale: Math.Clamp(1.0 - (intensity * 0.90), 0.04, 1.0),
                    TurnBiasDeg: turnBias,
                    Target: "contact",
                    EmittedUnixMs: emittedUnixMs);
            }

            if (telemetry.PainLevel >= 0.35)
            {
                var intensity = Math.Clamp(telemetry.PainLevel, 0.0, 1.0);
                return new AvatarReflexOutput(
                    Name: "flinch",
                    Intensity: intensity,
                    ForwardScale: Math.Clamp(1.0 - (intensity * 0.85), 0.05, 1.0),
                    TurnBiasDeg: 0.0,
                    Target: "pain",
                    EmittedUnixMs: emittedUnixMs);
            }

            if (telemetry.Health <= 0.55)
            {
                var intensity = Math.Clamp(1.0 - telemetry.Health, 0.0, 1.0);
                return new AvatarReflexOutput(
                    Name: "slow_when_damaged",
                    Intensity: intensity,
                    ForwardScale: Math.Clamp(1.0 - (intensity * 0.70), 0.18, 1.0),
                    TurnBiasDeg: 0.0,
                    Target: "damage",
                    EmittedUnixMs: emittedUnixMs);
            }
        }

        if (needs.RestNeed >= 0.62 || needs.SleepPressure >= 0.70 || needs.Fatigue >= 0.72)
        {
            var intensity = Math.Clamp(Math.Max(needs.RestNeed, Math.Max(needs.SleepPressure, needs.Fatigue)), 0.0, 1.0);
            return new AvatarReflexOutput(
                Name: "seek_rest",
                Intensity: intensity,
                ForwardScale: Math.Clamp(1.0 - (intensity * 0.42), 0.32, 1.0),
                TurnBiasDeg: 0.0,
                Target: "rest",
                EmittedUnixMs: emittedUnixMs);
        }

        if (memory.LastHeardSound is AvatarAuditoryCue heard && heard.Intensity >= 0.55f)
        {
            var turnBias = heard.Hemisphere?.Trim().ToUpperInvariant() switch
            {
                "L" => -28.0 * Math.Clamp(heard.Intensity, 0.0, 1.0),
                "R" => 28.0 * Math.Clamp(heard.Intensity, 0.0, 1.0),
                _ => 0.0
            };
            return new AvatarReflexOutput(
                Name: "orient_to_sound",
                Intensity: Math.Clamp(heard.Intensity, 0.0, 1.0),
                ForwardScale: 0.92,
                TurnBiasDeg: turnBias,
                Target: heard.Pattern,
                EmittedUnixMs: emittedUnixMs);
        }

        return AvatarReflexOutput.None(emittedUnixMs);
    }

    private void PublishSightOutput(AvatarSightFrame frame)
    {
        lock (_sightOutputGate)
        {
            _latestSightOutput = frame;
        }

        _publishedSightOutputs.Enqueue(frame);
    }

    private void RememberHeardSound(AvatarAuditoryCue cue)
        => UpdateRecentSensationMemory(memory => memory with
        {
            LastHeardSound = cue,
            AttentionTarget = cue.Pattern
        });

    private void RememberAudioOutput(AvatarAudioOutput output)
        => UpdateRecentSensationMemory(memory => memory with
        {
            LastAudioOutput = output
        });

    private void RememberBodyState(AvatarBodyStateInput input)
        => UpdateRecentSensationMemory(memory => memory with
        {
            LastBodyState = input,
            BodyMood = DescribeBodyMood(input.Telemetry)
        }, DeriveBodyEvents(input));

    private void RememberOutcome(AvatarOutcomeTelemetry outcome)
        => UpdateRecentSensationMemory(memory => memory with
        {
            LastOutcome = outcome,
            BodyMood = DescribeOutcomeMood(outcome, memory.BodyMood)
        }, DeriveOutcomeEvents(outcome));

    private void RememberObject(AvatarObjectObservation observation)
        => UpdateRecentSensationMemory(memory => memory with
        {
            LastSeenObject = observation,
            AttentionTarget = $"{observation.Label}:{observation.ObjectId}"
        });

    private void RememberSightFrame(AvatarSightFrame frame)
        => UpdateRecentSensationMemory(memory => memory with
        {
            LastSightGeneration = frame.Generation,
            LastSightTimestampMs = frame.CaptureTimestampMs
        });

    private void RememberPlaces(IEnumerable<AvatarPlaceObservation> observations)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (_placeMemoryGate)
        {
            foreach (var observation in observations)
            {
                var placeId = observation.PlaceId.Trim();
                if (placeId.Length == 0)
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(observation.Label)
                    ? placeId
                    : observation.Label.Trim();
                var source = string.IsNullOrWhiteSpace(observation.Source)
                    ? "avatar_place"
                    : observation.Source.Trim();

                if (_placeMemories.TryGetValue(placeId, out var existing))
                {
                    var observationWeight = Math.Clamp(observation.Confidence, 0.12, 1.0) * 0.28;
                    var updated = new AvatarPlaceMemory(
                        PlaceId: existing.PlaceId,
                        Label: label,
                        X: BlendUnbounded(existing.X, observation.X, observationWeight),
                        Y: BlendUnbounded(existing.Y, observation.Y, observationWeight),
                        Z: BlendUnbounded(existing.Z, observation.Z, observationWeight),
                        Safety: Blend(existing.Safety, Clamp01(observation.Safety), observationWeight),
                        Danger: Blend(existing.Danger, Clamp01(observation.Danger), observationWeight),
                        Food: Blend(existing.Food, Clamp01(observation.Food), observationWeight),
                        Blockage: Blend(existing.Blockage, Clamp01(observation.Blockage), observationWeight),
                        Interest: Blend(existing.Interest, Clamp01(observation.Interest), observationWeight),
                        Confidence: Math.Max(existing.Confidence * 0.96, Clamp01(observation.Confidence)),
                        FirstSeenUnixMs: existing.FirstSeenUnixMs,
                        LastSeenUnixMs: nowMs,
                        ObservationCount: existing.ObservationCount + 1,
                        DominantKind: "unknown",
                        Source: source);
                    _placeMemories[placeId] = updated with
                    {
                        DominantKind = DescribePlaceKind(updated)
                    };
                }
                else
                {
                    var memory = new AvatarPlaceMemory(
                        PlaceId: placeId,
                        Label: label,
                        X: observation.X,
                        Y: observation.Y,
                        Z: observation.Z,
                        Safety: Clamp01(observation.Safety),
                        Danger: Clamp01(observation.Danger),
                        Food: Clamp01(observation.Food),
                        Blockage: Clamp01(observation.Blockage),
                        Interest: Clamp01(observation.Interest),
                        Confidence: Clamp01(observation.Confidence),
                        FirstSeenUnixMs: nowMs,
                        LastSeenUnixMs: nowMs,
                        ObservationCount: 1,
                        DominantKind: "unknown",
                        Source: source);
                    _placeMemories[placeId] = memory with
                    {
                        DominantKind = DescribePlaceKind(memory)
                    };
                }
            }

            TrimPlaceMemories();
        }
    }

    private void TrimPlaceMemories()
    {
        while (_placeMemories.Count > MaxPlaceMemoryEntries)
        {
            var oldest = _placeMemories.Values
                .OrderBy(static item => item.LastSeenUnixMs)
                .ThenBy(static item => item.PlaceId)
                .FirstOrDefault();
            if (oldest is null || !_placeMemories.Remove(oldest.PlaceId))
            {
                break;
            }
        }
    }

    private static string DescribePlaceKind(AvatarPlaceMemory memory)
    {
        var dominant = new[]
        {
            ("safe", memory.Safety),
            ("danger", memory.Danger),
            ("food", memory.Food),
            ("blocked", memory.Blockage),
            ("interesting", memory.Interest)
        }
        .OrderByDescending(static item => item.Item2)
        .First();

        return dominant.Item2 >= 0.18 ? dominant.Item1 : "unknown";
    }

    private void UpdateRecentSensationMemory(
        Func<AvatarSensationMemory, AvatarSensationMemory> update,
        IEnumerable<AvatarBodyEvent>? bodyEvents = null)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (_memoryGate)
        {
            var updated = update(_recentSensationMemory);
            _recentSensationMemory = updated with
            {
                Revision = _recentSensationMemory.Revision + 1,
                UpdatedUnixMs = nowMs
            };
        }

        if (bodyEvents is not null)
        {
            AppendBodyEvents(bodyEvents);
        }

        UpdateNeedsRhythmState();
    }

    private void UpdateNeedsRhythmState()
    {
        var memory = RecentSensationMemory;
        var events = RecentBodyEvents;
        var previous = LatestNeedsRhythmState;
        var target = ComputeNeedsRhythmTarget(memory, events, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var smoothing = previous.UpdatedUnixMs <= 0 ? 1.0 : 0.24;
        var updated = new AvatarNeedsRhythmState(
            Hunger: Blend(previous.Hunger, target.Hunger, smoothing),
            Fatigue: Blend(previous.Fatigue, target.Fatigue, smoothing),
            SleepPressure: Blend(previous.SleepPressure, target.SleepPressure, smoothing),
            Stress: Blend(previous.Stress, target.Stress, smoothing),
            Curiosity: Blend(previous.Curiosity, target.Curiosity, smoothing),
            Restlessness: Blend(previous.Restlessness, target.Restlessness, smoothing),
            Recovery: Blend(previous.Recovery, target.Recovery, smoothing),
            RestNeed: Blend(previous.RestNeed, target.RestNeed, smoothing),
            UpdatedUnixMs: target.UpdatedUnixMs,
            DominantNeed: ResolveDominantNeed(target));

        lock (_needsRhythmGate)
        {
            _latestNeedsRhythmState = updated;
        }

        _publishedNeedsRhythmStates.Enqueue(updated);
    }

    private static AvatarNeedsRhythmState ComputeNeedsRhythmTarget(
        AvatarSensationMemory memory,
        IReadOnlyList<AvatarBodyEvent> events,
        long nowMs)
    {
        var hunger = 0.0;
        var fatigue = 0.0;
        var sleepPressure = 0.0;
        var stress = 0.0;
        var curiosity = 0.0;
        var recovery = 0.0;
        var restNeed = 0.0;

        if (memory.LastBodyState is AvatarBodyStateInput body)
        {
            var telemetry = body.Telemetry;
            hunger = Math.Clamp(telemetry.Hunger, 0.0, 1.0);
            var threat = Math.Max(telemetry.PredatorThreat, telemetry.Anxiety);
            stress = Math.Clamp(Math.Max(threat, Math.Max(telemetry.PainLevel, 1.0 - telemetry.Health)), 0.0, 1.0);
            var movement = Math.Clamp(Math.Abs(telemetry.ForwardVelocity) / Math.Max(body.Profile.MaxForwardSpeed, 0.001), 0.0, 1.0);
            fatigue = Math.Clamp((movement * 0.28) + (telemetry.ContactLevel * 0.18) + (telemetry.Urgency * 0.16), 0.0, 1.0);
            sleepPressure = Math.Clamp((telemetry.EnvironmentalDarkness * 0.38) + (fatigue * 0.45) + (stress * 0.14), 0.0, 1.0);
            recovery = Math.Clamp((telemetry.InShelter * 0.32) + (telemetry.ShelterSafety * 0.38) + ((1.0 - stress) * 0.12), 0.0, 1.0);
        }

        foreach (var bodyEvent in events.TakeLast(24))
        {
            var intensity = Math.Clamp(bodyEvent.Intensity, 0.0, 1.0);
            switch (bodyEvent.Kind)
            {
                case "fatigue":
                    fatigue = Math.Max(fatigue, intensity);
                    break;
                case "rest":
                    recovery = Math.Max(recovery, intensity);
                    break;
                case "relief":
                    recovery = Math.Max(recovery, intensity);
                    stress = Math.Max(0.0, stress - (intensity * 0.16));
                    break;
                case "curiosity":
                    curiosity = Math.Max(curiosity, intensity);
                    break;
                case "hunger":
                    hunger = Math.Max(hunger, intensity);
                    break;
                case "fear":
                case "pain":
                case "impact":
                    stress = Math.Max(stress, intensity);
                    break;
            }
        }

        fatigue = Math.Clamp(fatigue - (recovery * 0.18), 0.0, 1.0);
        sleepPressure = Math.Clamp(sleepPressure + (fatigue * 0.28) - (recovery * 0.12), 0.0, 1.0);
        restNeed = Math.Clamp((fatigue * 0.42) + (sleepPressure * 0.38) + (stress * 0.20) - (recovery * 0.18), 0.0, 1.0);
        var restlessness = Math.Clamp((hunger * 0.28) + (curiosity * 0.36) + (stress * 0.22) - (recovery * 0.20), 0.0, 1.0);

        return new AvatarNeedsRhythmState(
            Hunger: hunger,
            Fatigue: fatigue,
            SleepPressure: sleepPressure,
            Stress: stress,
            Curiosity: curiosity,
            Restlessness: restlessness,
            Recovery: recovery,
            RestNeed: restNeed,
            UpdatedUnixMs: nowMs,
            DominantNeed: "none");
    }

    private static string ResolveDominantNeed(AvatarNeedsRhythmState state)
    {
        var candidates = new (string Name, double Value)[]
        {
            ("hunger", state.Hunger),
            ("rest", state.RestNeed),
            ("stress", state.Stress),
            ("curiosity", state.Curiosity),
            ("recovery", state.Recovery),
            ("sleep", state.SleepPressure)
        };
        var best = candidates.OrderByDescending(static item => item.Value).First();
        return best.Value >= 0.20 ? best.Name : "none";
    }

    private static double Blend(double previous, double target, double amount)
        => Math.Clamp(previous + ((target - previous) * Math.Clamp(amount, 0.0, 1.0)), 0.0, 1.0);

    private static double BlendUnbounded(double previous, double target, double amount)
        => previous + ((target - previous) * Math.Clamp(amount, 0.0, 1.0));

    private static double Clamp01(double value)
        => Math.Clamp(value, 0.0, 1.0);

    private void AppendBodyEvents(IEnumerable<AvatarBodyEvent> events)
    {
        lock (_bodyEventGate)
        {
            foreach (var bodyEvent in events)
            {
                if (string.IsNullOrWhiteSpace(bodyEvent.Kind))
                {
                    continue;
                }

                _bodyEventLedger.Add(bodyEvent);
            }

            if (_bodyEventLedger.Count > MaxBodyEventLedgerEntries)
            {
                _bodyEventLedger.RemoveRange(0, _bodyEventLedger.Count - MaxBodyEventLedgerEntries);
            }
        }
    }

    private static IEnumerable<AvatarBodyEvent> DeriveBodyEvents(AvatarBodyStateInput input)
    {
        var telemetry = input.Telemetry;
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var movement = Math.Clamp(
            Math.Abs(telemetry.ForwardVelocity) / Math.Max(input.Profile.MaxForwardSpeed, 0.001),
            0.0,
            1.0);
        if (movement >= 0.08 || Math.Abs(telemetry.TurnRateDeg) >= 5.0)
        {
            yield return new AvatarBodyEvent("movement", Math.Max(movement, 0.10), "body moved", nowMs);
        }

        if (telemetry.ContactLevel >= 0.20)
        {
            yield return new AvatarBodyEvent("impact", Math.Clamp(telemetry.ContactLevel, 0.0, 1.0), "body contact", nowMs);
        }

        if (telemetry.PainLevel >= 0.15 || telemetry.Health <= 0.70)
        {
            var pain = Math.Max(telemetry.PainLevel, 1.0 - telemetry.Health);
            yield return new AvatarBodyEvent("pain", Math.Clamp(pain, 0.0, 1.0), "body pain or damage", nowMs);
        }

        if (telemetry.Hunger >= 0.45)
        {
            yield return new AvatarBodyEvent("hunger", Math.Clamp(telemetry.Hunger, 0.0, 1.0), "hunger rising", nowMs);
        }

        var fear = Math.Max(telemetry.PredatorThreat, telemetry.Anxiety);
        if (fear >= 0.35)
        {
            yield return new AvatarBodyEvent("fear", Math.Clamp(fear, 0.0, 1.0), "threat or anxiety", nowMs);
        }

        if (telemetry.InShelter >= 0.5 || telemetry.ShelterSafety >= 0.4)
        {
            yield return new AvatarBodyEvent("shelter", Math.Clamp(Math.Max(telemetry.InShelter, telemetry.ShelterSafety), 0.0, 1.0), "shelter or safety", nowMs);
        }

        if (movement < 0.04 && telemetry.ContactLevel < 0.05 && telemetry.PainLevel < 0.10 && fear < 0.25)
        {
            yield return new AvatarBodyEvent("rest", 0.25, "body resting", nowMs);
        }
    }

    private static IEnumerable<AvatarBodyEvent> DeriveOutcomeEvents(AvatarOutcomeTelemetry outcome)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (outcome.SatietyRelief >= 0.10)
        {
            yield return new AvatarBodyEvent("relief", Math.Clamp(outcome.SatietyRelief, 0.0, 1.0), "satiety relief", nowMs, outcome.InputSource);
        }

        var safetyRelief = Math.Max(outcome.SafetyRelief, outcome.ShelterComfort);
        if (safetyRelief >= 0.10)
        {
            yield return new AvatarBodyEvent("relief", Math.Clamp(safetyRelief, 0.0, 1.0), "safety relief", nowMs, outcome.InputSource);
        }

        var pain = Math.Max(outcome.PainLevel, outcome.DamageLevel);
        if (pain >= 0.10)
        {
            yield return new AvatarBodyEvent("pain", Math.Clamp(pain, 0.0, 1.0), "outcome pain or damage", nowMs, outcome.InputSource);
        }

        if (outcome.Progress >= 0.10)
        {
            yield return new AvatarBodyEvent("progress", Math.Clamp(outcome.Progress, 0.0, 1.0), "progress felt", nowMs, outcome.InputSource);
        }

        if (outcome.EffortCost >= 0.20)
        {
            yield return new AvatarBodyEvent("fatigue", Math.Clamp(outcome.EffortCost, 0.0, 1.0), "effort cost", nowMs, outcome.InputSource);
        }

        if (outcome.Novelty >= 0.20)
        {
            yield return new AvatarBodyEvent("curiosity", Math.Clamp(outcome.Novelty, 0.0, 1.0), "novelty noticed", nowMs, outcome.InputSource);
        }
    }

    private static string DescribeBodyMood(AvatarBodyTelemetry telemetry)
    {
        if (telemetry.PainLevel >= 0.35 || telemetry.Health <= 0.45)
        {
            return "hurt";
        }

        if (telemetry.PredatorThreat >= 0.35 || telemetry.Anxiety >= 0.55)
        {
            return "threatened";
        }

        if (telemetry.Hunger >= 0.60)
        {
            return "hungry";
        }

        if (telemetry.ContactLevel >= 0.35)
        {
            return "blocked";
        }

        if (telemetry.InShelter >= 0.5 && telemetry.ShelterSafety >= 0.4)
        {
            return "sheltered";
        }

        if (Math.Abs(telemetry.ForwardVelocity) >= 0.15 || Math.Abs(telemetry.TurnRateDeg) >= 5.0)
        {
            return "moving";
        }

        return "calm";
    }

    private static string DescribeOutcomeMood(AvatarOutcomeTelemetry outcome, string fallback)
    {
        if (outcome.PainLevel >= 0.25 || outcome.DamageLevel >= 0.20)
        {
            return "hurt";
        }

        if (outcome.SafetyRelief >= 0.35 || outcome.ShelterComfort >= 0.35)
        {
            return "relieved";
        }

        if (outcome.SatietyRelief >= 0.35)
        {
            return "sated";
        }

        if (outcome.Progress >= 0.35)
        {
            return "encouraged";
        }

        if (outcome.EffortCost >= 0.55)
        {
            return "tired";
        }

        return string.IsNullOrWhiteSpace(fallback) ? "unknown" : fallback;
    }

    private AvatarNervousSystemSignal CurrentSignal()
        => new(
            _nervousSystem.LeftMotorDrive,
            _nervousSystem.RightMotorDrive,
            _nervousSystem.LastMotorDispatchCount,
            _nervousSystem.TicksWithoutMotorDispatch,
            AvatarToolSignal.None);

    private interface IAvatarServiceCommand
    {
        AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem);
    }

    private sealed record BrainSignalsCommand(
        IReadOnlyList<AvatarDispatchSpike> Dispatches,
        AvatarNervousSystemBodyState Body) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
            => nervousSystem.InterpretBrainSignals(Dispatches, Body);
    }

    private sealed record ApplyDriveDecayCommand(double? SmoothingOverride) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            nervousSystem.ApplyDriveDecay(SmoothingOverride);
            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
    }

    private sealed record AddMotorDriveCommand(double LeftDelta, double RightDelta) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            nervousSystem.AddMotorDrive(LeftDelta, RightDelta);
            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
    }

    private sealed record SetMotorDriveCommand(double Left, double Right) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            nervousSystem.SetMotorDrive(Left, Right);
            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
    }

    private sealed class ResetMotorCommand : IAvatarServiceCommand
    {
        public static ResetMotorCommand Instance { get; } = new();

        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            nervousSystem.ResetMotor();
            return new AvatarNervousSystemSignal(0.0, 0.0, 0, 0, AvatarToolSignal.None);
        }
    }

    private sealed record AuditoryInputCandidatesCommand(
        IReadOnlyList<AvatarAuditoryCue> Cues,
        int MaxCues) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            foreach (var cue in Cues
                         .Where(static cue => !string.IsNullOrWhiteSpace(cue.Pattern))
                         .OrderByDescending(static cue => cue.Intensity)
                         .Take(MaxCues))
            {
                service._publishedAuditoryInputs.Enqueue(cue);
                service.RememberHeardSound(cue);
            }

            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
    }

    private sealed record AudioOutputCommand(AvatarAudioOutput Output) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            service._publishedAudioOutputs.Enqueue(Output);
            service.RememberAudioOutput(Output);
            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
    }

    private sealed record BodyInputCommand(AvatarBodyStateInput Input) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            service._publishedBodyInputs.Enqueue(Input);
            service.RememberBodyState(Input);
            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
    }

    private sealed record OutcomeCommand(AvatarOutcomeTelemetry Outcome) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            service._publishedOutcomes.Enqueue(Outcome);
            service.RememberOutcome(Outcome);
            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
    }

    private sealed record ObjectCandidatesCommand(
        IReadOnlyList<AvatarObjectObservation> Observations,
        int MaxObservations) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            foreach (var observation in Observations
                         .Where(static item => !string.IsNullOrWhiteSpace(item.ObjectId) && !string.IsNullOrWhiteSpace(item.Label))
                         .OrderByDescending(static item => item.Salience)
                         .ThenByDescending(static item => item.Confidence)
                         .Take(MaxObservations))
            {
                service._publishedObjectObservations.Enqueue(observation);
                service.RememberObject(observation);
            }

            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
    }

    private sealed record PlaceObservationsCommand(
        IReadOnlyList<AvatarPlaceObservation> Observations,
        int MaxObservations) : IAvatarServiceCommand
    {
        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            var selected = Observations
                .Where(static item => !string.IsNullOrWhiteSpace(item.PlaceId))
                .OrderByDescending(static item => item.Confidence)
                .Take(MaxObservations)
                .ToArray();

            service.RememberPlaces(selected);
            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
        }
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

    private sealed class FlushSightInputCommand : IAvatarServiceCommand
    {
        public static FlushSightInputCommand Instance { get; } = new();

        public AvatarNervousSystemSignal Execute(AvatarService service, AvatarNervousSystem nervousSystem)
        {
            var frame = service.TakePendingSightInput();
            if (frame is not null)
            {
                service.PublishSightOutput(frame);
                service.RememberSightFrame(frame);
            }

            return new AvatarNervousSystemSignal(
                nervousSystem.LeftMotorDrive,
                nervousSystem.RightMotorDrive,
                nervousSystem.LastMotorDispatchCount,
                nervousSystem.TicksWithoutMotorDispatch,
                AvatarToolSignal.None);
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
