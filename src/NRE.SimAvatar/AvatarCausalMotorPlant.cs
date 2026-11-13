using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

internal sealed record CausalMuscleDefinition(string MuscleId, DynamicMuscleGeometry Geometry,
    IReadOnlyList<SlowMotorUnitDefinition> Units, double InitialFiberLengthMeters,
    double InitialFiberVelocityMetersPerSecond = 0);

internal sealed record MusculotendonPathMeasurement(string MuscleId, double LengthMeters,
    double VelocityMetersPerSecond);

internal sealed record MotorMechanicalFrame(Guid SessionId, string BodyFingerprint,
    long PhysicsStep, double PhysicalTimeMs, Guid LastAppliedEffortId,
    IReadOnlyList<MusculotendonPathMeasurement> Paths);

internal sealed record TendonTension(string MuscleId, double AxialNewtons);

// Effort is held over this one physical interval. It is the force available at
// its START, never a force computed from a future geometry or later motor event.
internal sealed record MotorMechanicalEffort(Guid EffortId, Guid SessionId, string BodyFingerprint,
    string PlantFingerprint, long StartStep, double StartTimeMs, double EndTimeMs,
    IReadOnlyList<TendonTension> Tensions);

// Isolated mechanical boundary. A real implementation must own one physical
// step and derive paths/torques from the same geometry (virtual work). It must
// apply axial tendon tension directly, without another activation/fatigue filter.
internal interface IAvatarMotorMechanicalBody
{
    string Fingerprint { get; }
    long CompletedPhysicsStep { get; }
    double PhysicsStepMs { get; }
    MotorMechanicalFrame CaptureGeometry();
    void AdvanceOneStep(MotorMechanicalEffort effort);
}

internal enum CausalMotorAdvance { Paused, WaitingForNeuronalCompleteness, Advanced }

internal sealed record CausalMotorStep(MotorMechanicalFrame Before, MotorMechanicalFrame After,
    MotorMechanicalEffort Effort, IReadOnlyList<AvatarMotorAxonArrival> Arrivals,
    IReadOnlyDictionary<string, DynamicMuscleSnapshot> Muscles,
    double HeldTensionWorkOnBodyJoules);

internal sealed record CausalMotorPlantState(bool Initialized, bool Running, bool Faulted, string? Fault,
    long CompletedPhysicsStep, long ObservedPhysicsStep, double BodyTimeMs, double CompleteThroughBodyMs,
    long AcceptedEvents, long DeliveredEvents, int PendingEvents);

// Explicit first-order partition candidate, not a live Unity selection:
// 1. apply START-state tension to the next body step;
// 2. integrate muscle/events over that interval at its known START geometry;
// 3. accept completed body geometry at the same endpoint, without rewriting past output.
// Refinement against coupled mechanical references is required before promotion.
internal sealed class AvatarCausalMotorPlant
{
    public const string Version = "isolated-start-held-motor-body-v1";
    public const int MaximumMuscleSubsteps = 16_384;
    private readonly PhysicalSampleClock _clock;
    private readonly MotorAxonClock _motorClock;
    private readonly IAvatarMotorMechanicalBody _body;
    private readonly AvatarMotorAxonReceiver _receiver;
    private readonly CausalMuscleDefinition[] _definitions;
    private readonly Dictionary<string, string> _owners;
    private readonly Dictionary<string, AvatarDynamicMuscle> _muscles = new(StringComparer.Ordinal);
    private readonly int _substeps, _ownerThread = Environment.CurrentManagedThreadId;
    private readonly string _bodyFingerprint;
    private bool _initialized, _running, _busy, _faulted;
    private string? _fault;
    private long _completed, _observed;
    private MotorMechanicalFrame? _last;
    public string Fingerprint { get; }

    public AvatarCausalMotorPlant(PhysicalSampleClock clock, IAvatarMotorMechanicalBody body,
        IEnumerable<CausalMuscleDefinition> definitions, int muscleSubstepsPerPhysicsStep,
        int maximumPending = 65_536, int maximumEpochEvents = 1_000_000, int retainedBatches = 64)
    {
        ArgumentNullException.ThrowIfNull(clock); clock.Validate(); ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(definitions);
        if (muscleSubstepsPerPhysicsStep <= 0 || muscleSubstepsPerPhysicsStep > MaximumMuscleSubsteps ||
            clock.PhysicsStepMs / muscleSubstepsPerPhysicsStep > AvatarDynamicMuscle.MaximumStepMs)
            throw new ArgumentException("Declare enough muscle substeps for this physical clock.");
        _definitions = definitions.Select(d => d is null || d.Units is null ? throw new ArgumentException("Missing muscle definition.")
            : d with { Units = Array.AsReadOnly(d.Units.ToArray()) }).OrderBy(d => d.MuscleId, StringComparer.Ordinal).ToArray();
        if (_definitions.Length == 0 || _definitions.Any(d => string.IsNullOrWhiteSpace(d.MuscleId) || d.Geometry is null || d.Units.Count == 0) ||
            _definitions.Select(d => d.MuscleId).Distinct(StringComparer.Ordinal).Count() != _definitions.Length)
            throw new ArgumentException("Declare distinct physical muscle paths and their units.");
        if (_definitions.Any(d => d.Units.Any(u => u is null || u.Contact is null || u.Calcium is null)))
            throw new ArgumentException("Missing unit/contact parameters.");
        _definitions = _definitions.Select(d => d with
        { Units = Array.AsReadOnly(d.Units.OrderBy(u => u.Contact.MotorUnitId, StringComparer.Ordinal).ToArray()) }).ToArray();
        var anatomy = new MotorAxonAnatomy(_definitions.SelectMany(d => d.Units).Select(u => u.Contact));
        _owners = new(StringComparer.Ordinal);
        foreach (var d in _definitions)
            foreach (var u in d.Units)
                if (!_owners.TryAdd(u.Contact.MotorUnitId, d.MuscleId))
                    throw new ArgumentException("A motor unit must belong to exactly one declared muscle.");
        _clock = clock; _body = body; _substeps = muscleSubstepsPerPhysicsStep;
        _bodyFingerprint = body.Fingerprint;
        if (string.IsNullOrWhiteSpace(_bodyFingerprint)) throw new ArgumentException("Missing physical geometry fingerprint.");
        _motorClock = new(clock.SessionId, clock.NeuralEpochMs, clock.PhysicalEpochMs);
        _receiver = new(anatomy, _motorClock, maximumPending, maximumEpochEvents, retainedBatches);
        _completed = _observed = clock.InitialPhysicsStep;
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version, MuscleVersion = AvatarDynamicMuscle.Version, Anatomy = anatomy.Fingerprint,
            BodyFingerprint = _bodyFingerprint, clock.PhysicsStepMs, MuscleSubsteps = _substeps, Muscles = _definitions
        })));
    }

    public CausalMotorPlantState State => Exclusive(() => new CausalMotorPlantState(_initialized, _running && !_faulted,
        _faulted, _fault, _completed, _observed, _clock.AtStep(_completed).PhysicalMs,
        _receiver.CompleteThroughBodyMs, _receiver.AcceptedEvents, _receiver.DeliveredEvents, _receiver.PendingEvents), true);

    public MotorAxonAcknowledgement Accept(MotorAxonBatch batch) => Exclusive(() => _receiver.AcceptWithAcknowledgement(batch));

    public MotorMechanicalFrame CaptureInitial() => Exclusive(() =>
    {
        if (_initialized) throw new InvalidOperationException("Initial body already captured.");
        try
        {
            var initial = CaptureAt(_completed, Guid.Empty);
            foreach (var definition in _definitions)
                _muscles.Add(definition.MuscleId, new(definition.Geometry, definition.Units, _motorClock,
                    definition.InitialFiberLengthMeters, initial.Paths.Single(p => p.MuscleId == definition.MuscleId).LengthMeters,
                    definition.InitialFiberVelocityMetersPerSecond));
            _last = initial; _initialized = true;
            return initial;
        }
        catch (Exception e) { Fault("Initial geometry", e); throw; }
    });

    public void Resume() => Exclusive(() =>
    {
        if (!_initialized) throw new InvalidOperationException("Capture the unevolved initial body first.");
        try { CheckCurrent(); _running = true; return true; }
        catch (Exception e) { Fault("Resume", e); throw; }
    });

    public void Pause() => Exclusive(() => { _running = false; return true; }, true);

    public (CausalMotorAdvance Outcome, CausalMotorStep? Step) TryAdvance() => Exclusive(() =>
    {
        if (!_running) return (CausalMotorAdvance.Paused, (CausalMotorStep?)null);
        string phase = "Before physics";
        try
        {
            CheckCurrent();
            long next = checked(_completed + 1); double endpoint = _clock.AtStep(next).PhysicalMs;
            if (_receiver.CompleteThroughBodyMs < endpoint)
                return (CausalMotorAdvance.WaitingForNeuronalCompleteness, (CausalMotorStep?)null);
            var before = _last!;
            // Validate the actual represented intervals BEFORE consuming arrivals
            // or advancing the body. Nominal step/substep arithmetic is insufficient
            // when absolute timestamps have exhausted floating-point resolution.
            var endpoints = MuscleEndpoints(before.PhysicalTimeMs, endpoint);
            var tensions = Array.AsReadOnly(_definitions.Select(d => new TendonTension(d.MuscleId,
                _muscles[d.MuscleId].Capture().Forces.TendonAxialForceNewtons)).ToArray());
            var effort = new MotorMechanicalEffort(StableNeuralIdentity.Create("causal-muscle-effort-v1", _clock.Fingerprint,
                Fingerprint, _completed), _clock.SessionId, _bodyFingerprint, Fingerprint, _completed,
                before.PhysicalTimeMs, endpoint, tensions);
            var arrivals = _receiver.AdvanceTo(endpoint);
            phase = "Physical advance";
            _body.AdvanceOneStep(effort);
            phase = "Completed physical geometry";
            var after = CaptureAt(next, effort.EffortId);
            phase = "Muscle/event advance";
            foreach (var definition in _definitions)
            {
                var muscle = _muscles[definition.MuscleId];
                var local = arrivals.Where(a => _owners[a.MotorUnitId] == definition.MuscleId).ToArray();
                double heldLength = before.Paths.Single(p => p.MuscleId == definition.MuscleId).LengthMeters;
                int eventIndex = 0;
                foreach (double end in endpoints)
                {
                    while (eventIndex < local.Length && local[eventIndex].ArrivalAtBodyMs <= end)
                    {
                        AdvanceHeld(muscle, local[eventIndex].ArrivalAtBodyMs, heldLength);
                        muscle.Receive(local[eventIndex++]);
                    }
                    AdvanceHeld(muscle, end, heldLength);
                }
                if (eventIndex != local.Length) throw new InvalidOperationException("Motor arrival disappeared at a body boundary.");
                muscle.ObserveCompletedBodyLength(endpoint, after.Paths.Single(p => p.MuscleId == definition.MuscleId).LengthMeters);
            }
            double work = tensions.Sum(t => t.AxialNewtons *
                (before.Paths.Single(p => p.MuscleId == t.MuscleId).LengthMeters - after.Paths.Single(p => p.MuscleId == t.MuscleId).LengthMeters));
            if (!double.IsFinite(work)) throw new InvalidOperationException("Nonfinite held-tension boundary work.");
            _last = after; _completed = next;
            return (CausalMotorAdvance.Advanced, (CausalMotorStep?)new CausalMotorStep(before, after, effort, arrivals,
                Snapshots(), work));
        }
        catch (Exception e) { Fault(phase, e); throw; }
    });

    public IReadOnlyDictionary<string, DynamicMuscleSnapshot> CaptureMuscles() => Exclusive(Snapshots, true);

    private IReadOnlyDictionary<string, DynamicMuscleSnapshot> Snapshots() =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, DynamicMuscleSnapshot>(
            _muscles.ToDictionary(p => p.Key, p => p.Value.Capture(), StringComparer.Ordinal));

    private static void AdvanceHeld(AvatarDynamicMuscle muscle, double until, double length)
    {
        if (until < muscle.TimeMs) throw new InvalidOperationException("Motor event arrived behind muscle state.");
        if (until > muscle.TimeMs) muscle.AdvancePrescribed(new(muscle.TimeMs, until, length, length));
    }

    private double[] MuscleEndpoints(double start, double end)
    {
        var endpoints = new double[_substeps]; double previous = start;
        for (int i = 0; i < endpoints.Length; ++i)
        {
            double next = i == endpoints.Length - 1 ? end : start + (end - start) * ((i + 1.0) / _substeps);
            if (!double.IsFinite(next) || next <= previous || next - previous > AvatarDynamicMuscle.MaximumStepMs)
                throw new InvalidOperationException("Muscle clock resolution exhausted or represented step exceeds its qualified maximum.");
            endpoints[i] = next; previous = next;
        }
        return endpoints;
    }

    private void CheckBody(long step)
    {
        _observed = _body.CompletedPhysicsStep;
        if (_observed != step || _body.PhysicsStepMs != _clock.PhysicsStepMs || _body.Fingerprint != _bodyFingerprint)
            throw new InvalidOperationException("Physical ownership, clock or geometry changed outside this coupling epoch.");
    }

    private MotorMechanicalFrame CaptureAt(long step, Guid effort)
    {
        CheckBody(step); var supplied = _body.CaptureGeometry(); CheckBody(step);
        if (supplied is null || supplied.Paths is null || supplied.SessionId != _clock.SessionId ||
            supplied.BodyFingerprint != _bodyFingerprint || supplied.PhysicsStep != step ||
            supplied.PhysicalTimeMs != _clock.AtStep(step).PhysicalMs || supplied.LastAppliedEffortId != effort)
            throw new InvalidOperationException("Physical geometry/effort receipt does not belong to this endpoint.");
        var paths = supplied.Paths.ToArray();
        if (paths.Length != _definitions.Length || paths.Any(p => p is null || !double.IsFinite(p.LengthMeters) ||
            p.LengthMeters <= 0 || !double.IsFinite(p.VelocityMetersPerSecond)) ||
            !paths.Select(p => p.MuscleId).Order(StringComparer.Ordinal).SequenceEqual(_definitions.Select(d => d.MuscleId)))
            throw new InvalidOperationException("Missing, duplicated or invalid measured muscle paths.");
        return supplied with { Paths = Array.AsReadOnly(paths.OrderBy(p => p.MuscleId, StringComparer.Ordinal).ToArray()) };
    }

    private void CheckCurrent()
    {
        var observed = CaptureAt(_completed, _last!.LastAppliedEffortId);
        if (!observed.Paths.SequenceEqual(_last.Paths))
            throw new InvalidOperationException("Body geometry changed without an owned physical step.");
    }

    private void Fault(string phase, Exception error) { _faulted = true; _running = false; _fault = phase + ": " + error; }

    private T Exclusive<T>(Func<T> operation, bool allowFaulted = false)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread || _busy)
            throw new InvalidOperationException("Motor/body coupling belongs to one non-reentrant owner.");
        if (_faulted && !allowFaulted) throw new InvalidOperationException("Faulted motor/body epoch cannot resume or retry.");
        _busy = true;
        try { return operation(); }
        finally { _busy = false; }
    }
}
