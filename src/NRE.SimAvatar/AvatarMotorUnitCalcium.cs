using NeuralResonanceEngine.Protocol;

namespace NRE.SimAvatar;

// Independent expression of the slow-unit, optimal-length activation equations
// in Nagamori et al. 2021, doi:10.1371/journal.pcbi.1008707, equations 10-16/20.
// Not selected by a live Avatar/Unity plant. No rate-based spike generator.
internal sealed record MotorUnitCalciumParameters(double StoreSites, double TotalCalcium,
    double ReleaseRatePerSecond, double PumpRatePerSecond, double BindingRatePerSecond,
    double UnbindingRatePerSecond, double ReleaseRiseSeconds, double ReleaseDecaySeconds,
    double HillExponent, double HalfActivationBinding, double ActivationSeconds,
    double Cooperativity, double PeakTetanicForceNewtons)
{
    // Numeric first row/PTi[0] from modelParameter.mat at c317be803f01965a1ca8fc4cbad1ba9156433af8.
    // Human tibialis-anterior model parameterization; not a fit for Avatar's other muscles.
    public static MotorUnitCalciumParameters NagamoriSlowUnit1 { get; } = new(
        14.691776918222324, 1.8, 70.31972850928364, 55.10465471977302,
        53.849235059132326, 41.745357577047464, .0023354595753515782, .025,
        2.2, .038537546789474866, .052000000000000005, .8, .4620037750784163);

    public void Validate()
    {
        double[] positive = [StoreSites, TotalCalcium, ReleaseRatePerSecond, PumpRatePerSecond,
            BindingRatePerSecond, UnbindingRatePerSecond, ReleaseRiseSeconds, ReleaseDecaySeconds,
            HillExponent, HalfActivationBinding, ActivationSeconds, PeakTetanicForceNewtons];
        if (positive.Any(x => !double.IsFinite(x) || x <= 0) || StoreSites < TotalCalcium ||
            HillExponent < 1 || !double.IsFinite(Cooperativity) || Cooperativity < 0)
            throw new ArgumentException("Invalid declared calcium/activation parameters.");
    }
}

internal sealed class AvatarMotorUnitCalcium
{
    public const string Version = "isolated-slow-motor-unit-calcium-rk4-v1";
    public const double MaximumStepMs = .125;
    private readonly MotorUnitCalciumParameters _parameters;
    private readonly MotorAxonContact _contact;
    private readonly MotorAxonClock _clock;
    private double _releaseSlow, _releaseFast;
    private long _lastOrdinal;
    private bool _faulted;
    public double TimeMs { get; private set; }
    public double FreeCalcium { get; private set; }
    public double BoundCalcium { get; private set; }
    public double StoreCalcium => _parameters.TotalCalcium - FreeCalcium - BoundCalcium;
    public double FreeStoreSites => _parameters.StoreSites - StoreCalcium;
    public double Activation { get; private set; }
    public double IsometricForceNewtons => Activation * _parameters.PeakTetanicForceNewtons;
    public double ReleaseDrive => _releaseSlow - _releaseFast;
    public double ReleasedCalcium { get; private set; }
    public double RecoveredCalcium { get; private set; }
    public double NetBoundCalcium { get; private set; }
    public double MaximumCalciumLedgerError { get; private set; }
    public long Arrivals { get; private set; }
    public Guid LastMessageId { get; private set; }

    public AvatarMotorUnitCalcium(MotorUnitCalciumParameters parameters, MotorAxonContact contact, MotorAxonClock clock)
    {
        ArgumentNullException.ThrowIfNull(parameters); parameters.Validate();
        ArgumentNullException.ThrowIfNull(contact); _ = new MotorAxonAnatomy([contact]);
        ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        _parameters = parameters; _contact = contact; _clock = clock; TimeMs = clock.BodyEpochMs;
    }

    // A delivered axonal AP starts a calcium-release waveform. Conduction has
    // already been accounted for by the receiver: never add its delay twice.
    // Endplate transmission succeeds by assumption here; vesicle/receptor and
    // muscle action-potential dynamics are separate, still unqualified modules.
    public void Receive(AvatarMotorAxonArrival arrival)
    {
        if (_faulted) throw new InvalidOperationException("Faulted candidate cannot resume.");
        if (arrival is null || arrival.SessionId != _clock.SessionId ||
            arrival.SourceInstanceKey != _contact.SourceInstanceKey || arrival.SourceNeuronId != _contact.SourceNeuronId ||
            arrival.ProjectionId != _contact.ProjectionId || arrival.ContactId != _contact.ContactId ||
            arrival.MotorUnitId != _contact.MotorUnitId || arrival.MessageId == Guid.Empty ||
            arrival.SourceSynapseId == Guid.Empty || arrival.Ordinal <= _lastOrdinal ||
            arrival.ArrivalAtBodyMs != TimeMs ||
            _clock.ToBodyMs(arrival.FiredAtNeuralMs) + _contact.ConductionDelayMs != arrival.ArrivalAtBodyMs)
            throw new ArgumentException("Require this motor unit's next intact arrival at its actual body time.");
        _releaseSlow += 1; _releaseFast += 1;
        _lastOrdinal = arrival.Ordinal; LastMessageId = arrival.MessageId; Arrivals++;
    }

    private readonly record struct Rates(double Free, double Bound, double Activation, double Release, double Pump, double Binding);

    private Rates Derivative(double free, double bound, double activation, double elapsedSeconds)
    {
        RequirePhysicalState(free, bound, activation);
        var p = _parameters;
        double slow = _releaseSlow * Math.Exp(-elapsedSeconds / p.ReleaseDecaySeconds);
        double fast = _releaseFast * Math.Exp(-elapsedSeconds * (1 / p.ReleaseRiseSeconds + 1 / p.ReleaseDecaySeconds));
        double drive = slow - fast;
        if (!double.IsFinite(drive) || drive < 0) throw new InvalidOperationException("Invalid release waveform; no clipping.");
        double stored = p.TotalCalcium - free - bound;
        double release = p.ReleaseRatePerSecond * stored * drive;
        double pump = p.PumpRatePerSecond * free * (p.StoreSites - stored);
        double off = p.UnbindingRatePerSecond / (1 + p.Cooperativity * activation);
        double binding = (p.BindingRatePerSecond * free - off * bound) * (1 - bound);
        // Slow units have sag=1. At fixed optimal length/zero velocity, yield=1
        // is its resting equilibrium. No dynamic tendon/length law is applied.
        double ratio = bound / p.HalfActivationBinding;
        double powered = Math.Pow(ratio, p.HillExponent);
        double target = powered / (1 + powered);
        return new(release - pump - binding, binding, (target - activation) / p.ActivationSeconds,
            release, pump, binding);
    }

    public void Advance(double elapsedMs)
    {
        if (_faulted) throw new InvalidOperationException("Faulted candidate cannot resume.");
        if (!double.IsFinite(elapsedMs) || elapsedMs <= 0 || elapsedMs > MaximumStepMs ||
            !double.IsFinite(TimeMs + elapsedMs) || TimeMs + elapsedMs == TimeMs)
            throw new ArgumentOutOfRangeException(nameof(elapsedMs));
        try { AdvanceCore(elapsedMs); }
        catch { _faulted = true; throw; }
    }

    private void AdvanceCore(double elapsedMs)
    {
        double h = elapsedMs / 1000, c = FreeCalcium, b = BoundCalcium, a = Activation;
        var k1 = Derivative(c, b, a, 0);
        var k2 = Derivative(c + h / 2 * k1.Free, b + h / 2 * k1.Bound, a + h / 2 * k1.Activation, h / 2);
        var k3 = Derivative(c + h / 2 * k2.Free, b + h / 2 * k2.Bound, a + h / 2 * k2.Activation, h / 2);
        var k4 = Derivative(c + h * k3.Free, b + h * k3.Bound, a + h * k3.Activation, h);
        double free = c + h / 6 * (k1.Free + 2 * k2.Free + 2 * k3.Free + k4.Free);
        double bound = b + h / 6 * (k1.Bound + 2 * k2.Bound + 2 * k3.Bound + k4.Bound);
        double active = a + h / 6 * (k1.Activation + 2 * k2.Activation + 2 * k3.Activation + k4.Activation);
        RequirePhysicalState(free, bound, active);
        ReleasedCalcium += h / 6 * (k1.Release + 2 * k2.Release + 2 * k3.Release + k4.Release);
        RecoveredCalcium += h / 6 * (k1.Pump + 2 * k2.Pump + 2 * k3.Pump + k4.Pump);
        NetBoundCalcium += h / 6 * (k1.Binding + 2 * k2.Binding + 2 * k3.Binding + k4.Binding);
        FreeCalcium = free; BoundCalcium = bound; Activation = active;
        MaximumCalciumLedgerError = Math.Max(MaximumCalciumLedgerError,
            Math.Max(Math.Abs(free - ReleasedCalcium + RecoveredCalcium + NetBoundCalcium),
                Math.Abs(bound - NetBoundCalcium)));
        _releaseSlow *= Math.Exp(-h / _parameters.ReleaseDecaySeconds);
        _releaseFast *= Math.Exp(-h * (1 / _parameters.ReleaseRiseSeconds + 1 / _parameters.ReleaseDecaySeconds));
        TimeMs += elapsedMs;
    }

    private void RequirePhysicalState(double free, double bound, double activation)
    {
        if (!double.IsFinite(free) || !double.IsFinite(bound) || !double.IsFinite(activation) ||
            free < 0 || bound < 0 || bound > 1 || free + bound > _parameters.TotalCalcium || activation < 0 || activation > 1)
            throw new InvalidOperationException("Calcium/activation left its physical domain; no floor, reset or saturation repair.");
    }
}
