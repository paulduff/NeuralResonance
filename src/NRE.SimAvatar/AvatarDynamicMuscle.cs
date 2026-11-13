using NeuralResonanceEngine.Protocol;

namespace NRE.SimAvatar;

// Isolated continuous-equation candidate from Nagamori et al. 2021 and the
// pinned c317be8 author parameters. No live Avatar/Unity caller selects this.
internal sealed record SlowMotorUnitDefinition(MotorAxonContact Contact,
    MotorUnitCalciumParameters Calcium, double LengthGainAboveOptimal,
    double LengthGainBelowOptimal)
{
    public static SlowMotorUnitDefinition NagamoriSlowUnit1(MotorAxonContact contact) =>
        new(contact, MotorUnitCalciumParameters.NagamoriSlowUnit1, 1.9, 1.2999999999999998);
}

internal sealed record DynamicMuscleGeometry(double OptimalFiberLengthMeters,
    double ReferenceTendonLengthMeters, double FiberMassKg, double PennationRadians,
    double ReferenceForceNewtons, double PassiveLengthScale)
{
    public static DynamicMuscleGeometry NagamoriTibialisAnterior { get; } =
        new(6.8 / 100, 28.875 / 100, .15, .16755160819145562, 661.7647058823529, 1.129783098945111);

    public void Validate()
    {
        double[] positive = [OptimalFiberLengthMeters, ReferenceTendonLengthMeters,
            FiberMassKg, ReferenceForceNewtons, PassiveLengthScale];
        if (positive.Any(x => !double.IsFinite(x) || x <= 0) ||
            !double.IsFinite(PennationRadians) || PennationRadians < 0 || PennationRadians >= Math.PI / 2)
            throw new ArgumentException("Invalid declared muscle/tendon geometry.");
    }
}

// A known diagnostic trajectory, not interpolation of future live observations.
// Live body coupling needs its own causal stepping/ownership qualification.
internal sealed record PrescribedMusculotendonInterval(double StartTimeMs, double EndTimeMs,
    double StartLengthMeters, double EndLengthMeters);

internal enum FiberBoundary { Free, FixedLengthDiagnostic }

internal sealed record DynamicMotorUnitState(string MotorUnitId, double FreeCalcium,
    double BoundCalcium, double Activation, double ReleaseDrive, double ReleasedCalcium,
    double RecoveredCalcium, double NetBoundCalcium, double MaximumCalciumLedgerError,
    long Arrivals, Guid LastMessageId);

internal sealed record DynamicMuscleForces(double NormalizedFiberLength, double NormalizedFiberVelocity,
    double NormalizedTendonLength, double ActiveFiberForceNewtons, double PassiveFiberForceNewtons,
    double TendonAxialForceNewtons, double ProjectedTendonForceNewtons,
    double ProjectedFiberForceNewtons, double GeometricForceNewtons, double AccelerationMetersPerSecondSquared);

internal sealed record DynamicMuscleSnapshot(double TimeMs, double MusculotendonLengthMeters,
    double FiberLengthMeters, double FiberVelocityMetersPerSecond, double Yield,
    FiberBoundary Boundary, DynamicMuscleForces Forces, double IntegratedNetImpulseNewtonSeconds,
    double IntegratedNetFiberWorkJoules, double MaximumMomentumLedgerError,
    double MaximumKineticLedgerError, bool Faulted, IReadOnlyList<DynamicMotorUnitState> Units);

internal sealed class AvatarDynamicMuscle
{
    public const string Version = "isolated-coupled-slow-muscle-rk4-v1";
    public const double MaximumStepMs = .125;
    private readonly DynamicMuscleGeometry _geometry;
    private readonly MotorAxonClock _clock;
    private readonly SlowMotorUnitDefinition[] _units;
    private readonly Dictionary<string, int> _indices;
    private readonly double[] _releaseSlow, _releaseFast, _released, _recovered, _boundLedger, _maxCalciumError;
    private readonly long[] _ordinals, _arrivals;
    private readonly Guid[] _lastMessages;
    // x (m), v (m/s), yield, then free/bound/activation for each unit.
    private double[] _state;
    private readonly double _initialVelocity;
    private double _length, _impulse, _work, _maxMomentumError, _maxKineticError;
    public double TimeMs { get; private set; }
    public bool Faulted { get; private set; }
    public FiberBoundary Boundary { get; }

    public AvatarDynamicMuscle(DynamicMuscleGeometry geometry, IEnumerable<SlowMotorUnitDefinition> units,
        MotorAxonClock clock, double initialFiberLengthMeters, double initialMusculotendonLengthMeters,
        double initialFiberVelocityMetersPerSecond = 0, FiberBoundary boundary = FiberBoundary.Free)
    {
        ArgumentNullException.ThrowIfNull(geometry); geometry.Validate();
        ArgumentNullException.ThrowIfNull(units); ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        var declared = units.ToArray();
        if (declared.Any(x => x is null || x.Contact is null || x.Calcium is null))
            throw new ArgumentException("Null motor-unit definitions are not valid anatomy.");
        _units = declared.OrderBy(x => x.Contact.MotorUnitId, StringComparer.Ordinal).ToArray();
        if (_units.Length == 0 || !Enum.IsDefined(boundary) ||
            (boundary == FiberBoundary.FixedLengthDiagnostic && initialFiberVelocityMetersPerSecond != 0))
            throw new ArgumentException("Declare motor units and a valid explicit fiber boundary.");
        _ = new MotorAxonAnatomy(_units.Select(x => x.Contact));
        foreach (var unit in _units)
        {
            ArgumentNullException.ThrowIfNull(unit.Calcium); unit.Calcium.Validate();
            if (!double.IsFinite(unit.LengthGainAboveOptimal) || unit.LengthGainAboveOptimal < 0 ||
                !double.IsFinite(unit.LengthGainBelowOptimal) || unit.LengthGainBelowOptimal < 0)
                throw new ArgumentException("Invalid declared length-dependent calcium gains.");
        }
        if (_units.Sum(x => x.Calcium.PeakTetanicForceNewtons) > geometry.ReferenceForceNewtons)
            throw new ArgumentException("Declared unit forces exceed the declared whole-muscle reference force.");
        _geometry = geometry; _clock = clock; Boundary = boundary; TimeMs = clock.BodyEpochMs;
        _length = initialMusculotendonLengthMeters; _initialVelocity = initialFiberVelocityMetersPerSecond;
        _indices = _units.Select((unit, i) => (unit, i)).ToDictionary(x => x.unit.Contact.MotorUnitId, x => x.i, StringComparer.Ordinal);
        _state = new double[3 + 3 * _units.Length];
        _state[0] = initialFiberLengthMeters; _state[1] = initialFiberVelocityMetersPerSecond; _state[2] = 1;
        _releaseSlow = new double[_units.Length]; _releaseFast = new double[_units.Length];
        _released = new double[_units.Length]; _recovered = new double[_units.Length];
        _boundLedger = new double[_units.Length]; _maxCalciumError = new double[_units.Length];
        _ordinals = new long[_units.Length]; _arrivals = new long[_units.Length]; _lastMessages = new Guid[_units.Length];
        RequireState(_state); _ = Forces(_state, _length);
    }

    public void Receive(AvatarMotorAxonArrival arrival)
    {
        RequireOperational();
        if (arrival is null || arrival.MotorUnitId is null || !_indices.TryGetValue(arrival.MotorUnitId, out int index))
            throw new ArgumentException("Arrival addresses an undeclared motor unit.");
        var contact = _units[index].Contact;
        if (arrival.SessionId != _clock.SessionId || arrival.SourceInstanceKey != contact.SourceInstanceKey ||
            arrival.SourceNeuronId != contact.SourceNeuronId || arrival.ProjectionId != contact.ProjectionId ||
            arrival.ContactId != contact.ContactId || arrival.MessageId == Guid.Empty || arrival.SourceSynapseId == Guid.Empty ||
            arrival.Ordinal <= _ordinals[index] || arrival.ArrivalAtBodyMs != TimeMs ||
            _clock.ToBodyMs(arrival.FiredAtNeuralMs) + contact.ConductionDelayMs != arrival.ArrivalAtBodyMs)
            throw new ArgumentException("Require this unit's intact next arrival at its actual body time.");
        _releaseSlow[index] += 1; _releaseFast[index] += 1;
        _ordinals[index] = arrival.Ordinal; _lastMessages[index] = arrival.MessageId; _arrivals[index]++;
    }

    private void RequireOperational()
    {
        if (Faulted) throw new InvalidOperationException("Faulted dynamic muscle cannot resume.");
    }

    private (double Positive, double Negative) LengthFactors(int unit, double length)
    {
        var p = _units[unit];
        double delta = (length - 1) * (length >= 1 ? p.LengthGainAboveOptimal : p.LengthGainBelowOptimal);
        if (!double.IsFinite(delta) || 1 + delta <= 0 || 1 - delta <= 0)
            throw new InvalidOperationException("Length-dependent activation parameters left their valid domain; no clipping.");
        return (1 + delta, 1 - delta);
    }

    private void RequireState(double[] state)
    {
        if (state.Any(x => !double.IsFinite(x)) || state[0] <= 0 || state[2] < .65 || state[2] > 1)
            throw new InvalidOperationException("Fiber/yield state left its physical domain; no reset or clipping.");
        for (int i = 0; i < _units.Length; i++)
        {
            int offset = 3 + 3 * i;
            double c = state[offset], b = state[offset + 1], a = state[offset + 2];
            _ = LengthFactors(i, state[0] / _geometry.OptimalFiberLengthMeters);
            if (c < 0 || b < 0 || b > 1 || c + b > _units[i].Calcium.TotalCalcium || a < 0 || a > 1)
                throw new InvalidOperationException("Calcium/activation left its physical domain; no reset or clipping.");
        }
    }

    // Stable evaluation of log(1+exp(z)); asymptotic forms avoid exp overflow.
    private static double Softplus(double z) => z > 36 ? z + Math.Exp(-z) : z < -36 ? Math.Exp(z) : Math.Log(1 + Math.Exp(z));

    private DynamicMuscleForces Forces(double[] state, double musculotendonLength)
    {
        var g = _geometry;
        double x = state[0], v = state[1], length = x / g.OptimalFiberLengthMeters, velocity = v / g.OptimalFiberLengthMeters;
        double cosine = Math.Cos(g.PennationRadians), tangent = Math.Tan(g.PennationRadians);
        double tendon = (musculotendonLength - x * cosine) / g.ReferenceTendonLengthMeters;
        if (!double.IsFinite(musculotendonLength) || musculotendonLength <= 0 || tendon <= 0 || velocity < -7.88)
            throw new InvalidOperationException("Mechanical input left the declared length/shortening domain.");
        double fl = Math.Exp(-Math.Pow(Math.Abs((Math.Pow(length, 2.3) - 1) / 1.12), 1.62));
        double fv = velocity <= 0 ? (-7.88 - velocity) / (-7.88 + 5.88 * velocity)
            : (.35 - (-4.7 + 8.41 * length - 5.34 * length * length) * velocity) / (.35 + velocity);
        double pe2 = -.02 * Math.Exp(-21 * (length - .70) - 1);
        double active = 0;
        for (int i = 0; i < _units.Length; i++)
            active += state[5 + 3 * i] * _units[i].Calcium.PeakTetanicForceNewtons * (fl * fv + pe2);
        double passive = g.ReferenceForceNewtons * (23 * .046 * Softplus((length / g.PassiveLengthScale - 1.17) / .046) + .01 * velocity);
        double axialTendon = g.ReferenceForceNewtons * 27.8 * .0047 * Softplus((tendon - .964) / .0047);
        double projectedTendon = axialTendon * cosine, projectedFiber = (active + passive) * cosine * cosine;
        double geometric = g.FiberMassKg * v * v * tangent * tangent / x;
        double acceleration = (projectedTendon - projectedFiber + geometric) / g.FiberMassKg;
        double[] values = [active, passive, axialTendon, projectedTendon, projectedFiber, geometric, acceleration];
        if (values.Any(z => !double.IsFinite(z))) throw new InvalidOperationException("Nonfinite force; no numerical repair.");
        return new(length, velocity, tendon, active, passive, axialTendon, projectedTendon, projectedFiber, geometric, acceleration);
    }

    private sealed record Rates(double[] State, double[] Release, double[] Pump, double[] Binding, double Impulse, double Work);

    private Rates Derivative(double[] state, double length, double elapsedSeconds)
    {
        RequireState(state);
        var forces = Forces(state, length);
        var result = new double[state.Length];
        var release = new double[_units.Length]; var pump = new double[_units.Length]; var binding = new double[_units.Length];
        if (Boundary == FiberBoundary.Free) { result[0] = state[1]; result[1] = forces.AccelerationMetersPerSecondSquared; }
        result[2] = (1 - .35 * (1 - Math.Exp(-Math.Abs(forces.NormalizedFiberVelocity) / .1)) - state[2]) / .2;
        for (int i = 0; i < _units.Length; i++)
        {
            var p = _units[i].Calcium; int offset = 3 + 3 * i;
            var factors = LengthFactors(i, forces.NormalizedFiberLength);
            double c = state[offset], b = state[offset + 1], a = state[offset + 2];
            double slow = _releaseSlow[i] * Math.Exp(-elapsedSeconds / p.ReleaseDecaySeconds);
            double fast = _releaseFast[i] * Math.Exp(-elapsedSeconds * (1 / p.ReleaseRiseSeconds + 1 / p.ReleaseDecaySeconds));
            double drive = slow - fast;
            if (drive < 0 || !double.IsFinite(drive)) throw new InvalidOperationException("Invalid release waveform.");
            double stored = p.TotalCalcium - c - b;
            release[i] = p.ReleaseRatePerSecond * stored * drive;
            pump[i] = p.PumpRatePerSecond * c * (p.StoreSites - stored);
            double off = p.UnbindingRatePerSecond / (1 + p.Cooperativity * factors.Positive * a);
            binding[i] = (p.BindingRatePerSecond * factors.Positive * c - off * b) * (1 - b);
            double ratio = b * state[2] / (p.HalfActivationBinding * factors.Negative);
            double powered = Math.Pow(ratio, p.HillExponent * factors.Negative);
            double target = powered / (1 + powered);
            result[offset] = release[i] - pump[i] - binding[i]; result[offset + 1] = binding[i];
            result[offset + 2] = (target - a) / p.ActivationSeconds;
        }
        double impulse = Boundary == FiberBoundary.Free ? _geometry.FiberMassKg * forces.AccelerationMetersPerSecondSquared : 0;
        return new(result, release, pump, binding, impulse, impulse * state[1]);
    }

    public void AdvancePrescribed(PrescribedMusculotendonInterval interval)
    {
        RequireOperational(); ArgumentNullException.ThrowIfNull(interval);
        double elapsedMs = interval.EndTimeMs - interval.StartTimeMs;
        if (interval.StartTimeMs != TimeMs || interval.StartLengthMeters != _length || !double.IsFinite(elapsedMs) ||
            elapsedMs <= 0 || elapsedMs > MaximumStepMs || !double.IsFinite(interval.EndTimeMs) ||
            !double.IsFinite(interval.EndLengthMeters) || interval.EndLengthMeters <= 0)
            throw new ArgumentException("Require a contiguous, finite, explicitly prescribed interval within the solver limit.");
        try { AdvanceCore(interval, elapsedMs / 1000); }
        catch { Faulted = true; throw; }
    }

    // Explicit partition boundary: physics has completed at this SAME time.
    // Change only the observed total path length; retain every fiber/calcium
    // state and ledger. The owner must qualify splitting error and account for
    // physical boundary work. This is not future-frame interpolation or an ODE
    // advancement, and it is never called by the prescribed-reference rig.
    public void ObserveCompletedBodyLength(double physicalTimeMs, double lengthMeters)
    {
        RequireOperational();
        if (physicalTimeMs != TimeMs || !double.IsFinite(lengthMeters) || lengthMeters <= 0)
            throw new ArgumentException("Physical geometry must belong to the muscle's completed time.");
        _ = Forces(_state, lengthMeters); // Validate before changing the boundary.
        _length = lengthMeters;
    }

    private void AdvanceCore(PrescribedMusculotendonInterval interval, double h)
    {
        double halfLength = interval.StartLengthMeters + (interval.EndLengthMeters - interval.StartLengthMeters) / 2;
        var k1 = Derivative(_state, interval.StartLengthMeters, 0);
        double[] Intermediate(double[] derivative, double fraction) =>
            _state.Select((value, i) => value + h * fraction * derivative[i]).ToArray();
        var k2 = Derivative(Intermediate(k1.State, .5), halfLength, h / 2);
        var k3 = Derivative(Intermediate(k2.State, .5), halfLength, h / 2);
        var k4 = Derivative(Intermediate(k3.State, 1), interval.EndLengthMeters, h);
        double Weighted(double a, double b, double c, double d) => h / 6 * (a + 2 * b + 2 * c + d);
        var next = _state.Select((value, i) => value + Weighted(k1.State[i], k2.State[i], k3.State[i], k4.State[i])).ToArray();
        RequireState(next); _ = Forces(next, interval.EndLengthMeters);
        double impulse = _impulse + Weighted(k1.Impulse, k2.Impulse, k3.Impulse, k4.Impulse);
        double work = _work + Weighted(k1.Work, k2.Work, k3.Work, k4.Work);
        var released = new double[_units.Length]; var recovered = new double[_units.Length]; var bound = new double[_units.Length];
        for (int i = 0; i < _units.Length; i++)
        {
            released[i] = _released[i] + Weighted(k1.Release[i], k2.Release[i], k3.Release[i], k4.Release[i]);
            recovered[i] = _recovered[i] + Weighted(k1.Pump[i], k2.Pump[i], k3.Pump[i], k4.Pump[i]);
            bound[i] = _boundLedger[i] + Weighted(k1.Binding[i], k2.Binding[i], k3.Binding[i], k4.Binding[i]);
        }
        if (!double.IsFinite(impulse) || !double.IsFinite(work) || released.Concat(recovered).Concat(bound).Any(x => !double.IsFinite(x)))
            throw new InvalidOperationException("Nonfinite independent ledgers.");
        // Commit only after every stage, endpoint and ledger validates.
        _state = next; _length = interval.EndLengthMeters; TimeMs = interval.EndTimeMs; _impulse = impulse; _work = work;
        for (int i = 0; i < _units.Length; i++)
        {
            var p = _units[i].Calcium;
            _released[i] = released[i]; _recovered[i] = recovered[i]; _boundLedger[i] = bound[i];
            _maxCalciumError[i] = Math.Max(_maxCalciumError[i], Math.Max(
                Math.Abs(next[3 + 3 * i] - released[i] + recovered[i] + bound[i]), Math.Abs(next[4 + 3 * i] - bound[i])));
            _releaseSlow[i] *= Math.Exp(-h / p.ReleaseDecaySeconds);
            _releaseFast[i] *= Math.Exp(-h * (1 / p.ReleaseRiseSeconds + 1 / p.ReleaseDecaySeconds));
        }
        _maxMomentumError = Math.Max(_maxMomentumError, Math.Abs(_geometry.FiberMassKg * (next[1] - _initialVelocity) - _impulse));
        _maxKineticError = Math.Max(_maxKineticError, Math.Abs(.5 * _geometry.FiberMassKg *
            (next[1] * next[1] - _initialVelocity * _initialVelocity) - _work));
    }

    public DynamicMuscleSnapshot Capture() => new(TimeMs, _length, _state[0], _state[1], _state[2], Boundary,
        Forces(_state, _length), _impulse, _work, _maxMomentumError, _maxKineticError, Faulted,
        Array.AsReadOnly(_units.Select((unit, i) => new DynamicMotorUnitState(unit.Contact.MotorUnitId,
            _state[3 + 3 * i], _state[4 + 3 * i], _state[5 + 3 * i], _releaseSlow[i] - _releaseFast[i],
            _released[i], _recovered[i], _boundLedger[i], _maxCalciumError[i], _arrivals[i], _lastMessages[i])).ToArray()));
}
