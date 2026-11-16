using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;

// Independent implementation of the two-state receptor equation in Destexhe,
// Mainen & Sejnowski (1994/1998), ModelDB 18500. These hippocampal reference
// constants are NOT calibrated spinal contacts. No live host selects this type.
internal enum MotorReceptorKind { AmpaReference, GabaAReference }

internal sealed record MotorReceptorDefinition(string SourceInstance, StructureId SourceStructure,
    string SourceNeuron, Guid SourceSynapse, Guid Projection, string TargetInstance,
    string TargetNeuron, int TargetNeuronIndex, string TargetSite, int ElectricalCompartment,
    MotorReceptorKind Kind, double MaximumConductanceNs, double AxonalDelayMs,
    bool IsFeedback = false)
{
    internal NTEnum Transmitter => Kind == MotorReceptorKind.AmpaReference ? NTEnum.GLUTAMATE : NTEnum.GABA;
    internal double BindingPerMsMm => Kind == MotorReceptorKind.AmpaReference ? 1.1 : 5;
    internal double UnbindingPerMs => Kind == MotorReceptorKind.AmpaReference ? .19 : .18;
    internal double ReversalMv => Kind == MotorReceptorKind.AmpaReference ? 0 : -80;
    internal Guid ContactId => StableNeuralIdentity.Create("motor-receptor-contact-v1", StableNeuralIdentity.Contact(new SpikeMessage
    {
        SourceInstanceKey = SourceInstance, SourceStructure = SourceStructure, SourceNeuronId = SourceNeuron,
        TargetStructure = StructureId.SpinalCordMotor, ProjectionId = Projection, SynapseId = SourceSynapse,
        Neurotransmitter = Transmitter, IsFeedback = IsFeedback, IsSyntheticBackground = false
    }, TargetInstance, TargetNeuronIndex), SourceSynapse, TargetNeuron, TargetSite);

    internal void Validate()
    {
        static bool Key(string text) => !string.IsNullOrWhiteSpace(text) && text == text.Trim();
        if (!Key(SourceInstance) || !Key(SourceNeuron) || !Key(TargetInstance) || !Key(TargetNeuron) || !Key(TargetSite) ||
            !Enum.IsDefined(SourceStructure) || !Enum.IsDefined(Kind) || SourceSynapse == Guid.Empty ||
            Projection == Guid.Empty || TargetNeuronIndex < 0 || ElectricalCompartment is < 0 or > 4 ||
            !double.IsFinite(MaximumConductanceNs) || MaximumConductanceNs <= 0 ||
            !double.IsFinite(AxonalDelayMs) || AxonalDelayMs < 0)
            throw new ArgumentException("Declare an individual source/contact/target and finite physical receptor parameters.");
    }
}

internal sealed record MotorReceptorReceipt(Guid SessionId, Guid ContactId, Guid EventId, string OriginalEventSha256,
    double FiredAtMs, double ArrivedAtMs);

internal sealed record MotorReceptorValue(double TimeMs, double OpenFraction, double ConductanceNs,
    double ReversalMv, double TransmitterMm, int ElectricalCompartment)
{
    internal double InwardCurrentPa(double voltageMv)
    {
        double current = ConductanceNs * (ReversalMv - voltageMv); // nS * mV = pA
        if (!double.IsFinite(voltageMv) || !double.IsFinite(current))
            throw new ArgumentException("Nonfinite voltage or receptor current.");
        return current;
    }
}

internal sealed record MotorReceptorState(Guid SessionId, Guid ContactId, string Fingerprint, MotorReceptorValue Value,
    int AcceptedEvents, double? LastReleaseMs, double? PulseEndMs, bool Faulted);

// A single-owner, in-memory receptor. The owner must deliver events at their
// actual arrival boundary and must split membrane steps at release transitions.
// This is a conductance component, not a motor cell, input queue or authority.
internal sealed class MotorReceptorContact
{
    internal const string Version = "isolated-motor-receptor-two-state-v1";
    internal const double PulseConcentrationMm = 1, PulseDurationMs = 1;
    private readonly MotorReceptorDefinition _definition;
    private readonly Guid _session;
    private readonly int _owner = Environment.CurrentManagedThreadId, _maximumEvents;
    private readonly Dictionary<Guid, MotorReceptorReceipt> _receipts = new();
    private double _time, _open, _anchorTime, _anchorOpen;
    private double? _pulseEnd;
    private bool _faulted;
    internal string Fingerprint { get; }

    internal MotorReceptorContact(MotorReceptorDefinition definition, Guid sessionId, double initialNeuralTimeMs = 0,
        int maximumEpochEvents = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(definition); definition.Validate();
        if (sessionId == Guid.Empty) throw new ArgumentException("Declare the neural session; never infer it from arrival time.");
        RequireClock(initialNeuralTimeMs);
        if (maximumEpochEvents <= 0) throw new ArgumentOutOfRangeException(nameof(maximumEpochEvents));
        _definition = definition; _session = sessionId; _maximumEvents = maximumEpochEvents;
        _time = _anchorTime = initialNeuralTimeMs;
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version, Definition = definition, InitialNeuralTimeMs = initialNeuralTimeMs,
            PulseConcentrationMm, PulseDurationMs, definition.BindingPerMsMm, definition.UnbindingPerMs,
            definition.ReversalMv, ReleaseConvention = "observed-events-nonoverlapping-fixed-pulses"
        })));
    }

    internal MotorReceptorState Capture()
    {
        RequireOwner();
        return new(_session, _definition.ContactId, Fingerprint, Value(_time, _open), _receipts.Count,
            _pulseEnd.HasValue ? _anchorTime : null, _pulseEnd, _faulted);
    }

    internal MotorReceptorReceipt Release(Guid sourceSessionId, SpikeMessage original)
    {
        var receipt = DescribeOriginal(sourceSessionId, original);
        double arrival = receipt.ArrivedAtMs;
        if (_receipts.TryGetValue(original.MessageId, out var prior))
        {
            if (prior != receipt) throw new ArgumentException("Conflicting original event replay.");
            return prior; // Exact replay is inert even after receptor time advances.
        }
        if (arrival != _time) throw new ArgumentException("Deliver at the exact arrival boundary; no early or late release.");
        if (_pulseEnd is { } end && arrival < end)
            throw new InvalidOperationException("Overlapping transmitter pulses are outside this candidate; retain the event and stop its owner.");
        if (_receipts.Count >= _maximumEvents)
            throw new InvalidOperationException("Receptor epoch capacity exhausted; do not discard events or replay protection.");
        RequireClock(arrival);
        RequireClock(arrival + PulseDurationMs);
        try
        {
            _receipts.Add(original.MessageId, receipt);
            _anchorTime = arrival; _anchorOpen = _open; _pulseEnd = arrival + PulseDurationMs;
            return receipt;
        }
        catch { _faulted = true; throw; }
    }

    // Pure admission validation for the owning input queue. This is not release
    // or input completeness, and it does not alter occupancy or replay history.
    internal MotorReceptorReceipt DescribeOriginal(Guid sourceSessionId, SpikeMessage original)
    {
        RequireOperational(); ArgumentNullException.ThrowIfNull(original);
        if (sourceSessionId != _session || original.MessageId == Guid.Empty || original.SourceInstanceKey != _definition.SourceInstance ||
            original.SourceStructure != _definition.SourceStructure || original.SourceNeuronId != _definition.SourceNeuron ||
            original.SynapseId != _definition.SourceSynapse || original.ProjectionId != _definition.Projection ||
            original.TargetStructure != StructureId.SpinalCordMotor || original.TargetNeuronId != _definition.TargetNeuron ||
            original.Neurotransmitter != _definition.Transmitter || original.IsSyntheticBackground ||
            original.IsFeedback != _definition.IsFeedback || !double.IsFinite(original.TimestampMs) || original.TimestampMs < 0)
            throw new ArgumentException("Require the original identified event for this declared receptor contact.");
        double arrival = original.TimestampMs + _definition.AxonalDelayMs;
        if (!double.IsFinite(arrival) || (_definition.AxonalDelayMs > 0 && arrival <= original.TimestampMs))
            throw new ArgumentException("Axonal delay is not representable on this source clock.");
        // Hash the original complete message, including metadata not used as a
        // conductance. Do not reinterpret VesicleQuanta or ModulationContext.
        string digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(original)));
        RequireClock(arrival); RequireClock(arrival + PulseDurationMs);
        return new(_session, _definition.ContactId, original.MessageId, digest, original.TimestampMs, arrival);
    }

    // Conditional analytic evaluation of ALREADY ACCEPTED release history.
    // This does not assert input completeness. An external neural owner must
    // stop at the next incoming event and never treat missing input as silence.
    internal MotorReceptorValue EvaluateKnownReleaseAt(double timeMs)
    {
        RequireOperational(); RequireClock(timeMs);
        if (timeMs < _time) throw new ArgumentOutOfRangeException(nameof(timeMs), "No past state reconstruction.");
        double open = OpenAt(timeMs);
        if (!double.IsFinite(open) || open is < 0 or > 1)
            throw new InvalidOperationException("Receptor occupancy left its domain; no clipping.");
        return Value(timeMs, open);
    }

    internal MotorReceptorState AdvanceTo(double timeMs)
    {
        // Pure validation before commit; an invalid caller boundary must not
        // alter the clock, anchor, event ledger or accepted occupancy.
        var value = EvaluateKnownReleaseAt(timeMs);
        _time = timeMs; _open = value.OpenFraction;
        return Capture();
    }

    private double OpenAt(double time)
    {
        if (_pulseEnd is null) return 0;
        double elapsed = time - _anchorTime;
        double opening = _definition.BindingPerMsMm * PulseConcentrationMm;
        double rate = opening + _definition.UnbindingPerMs;
        double onDuration = Math.Min(elapsed, _pulseEnd.Value - _anchorTime);
        double x = rate * onDuration;
        double on = _anchorOpen * Math.Exp(-x) + (opening / rate) * OneMinusExpNegative(x);
        return time <= _pulseEnd.Value ? on : on * Math.Exp(-_definition.UnbindingPerMs * (time - _pulseEnd.Value));
    }

    private MotorReceptorValue Value(double time, double open) => new(time, open,
        _definition.MaximumConductanceNs * open, _definition.ReversalMv,
        _pulseEnd.HasValue && time < _pulseEnd.Value ? PulseConcentrationMm : 0, _definition.ElectricalCompartment);

    // Preserve very small physical intervals instead of cancelling 1-exp(-x).
    private static double OneMinusExpNegative(double x)
    {
        if (x >= 1e-5) return 1 - Math.Exp(-x);
        return x * (1 - x * (.5 - x * (1.0 / 6 - x * (1.0 / 24 - x / 120))));
    }

    private static void RequireClock(double time)
    {
        if (!double.IsFinite(time) || time < 0 || !double.IsFinite(time + PulseDurationMs) || time + PulseDurationMs <= time ||
            Math.BitIncrement(time) - time > PulseDurationMs)
            throw new ArgumentOutOfRangeException(nameof(time), "Require a finite clock that resolves the release duration.");
    }
    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("One owner advances a receptor contact.");
    }
    private void RequireOperational()
    {
        RequireOwner();
        if (_faulted) throw new InvalidOperationException("Faulted receptor epoch cannot resume.");
    }
}
