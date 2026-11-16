using NeuralResonanceEngine.Protocol;

// Isolated, single-cell source adapter for the already checked six-second
// reference protocols. It is not a live StructureEngine selection, active axon
// or physiologically matched human motor unit. No rate becomes a spike here.
internal sealed record ObservedMotorIdentity(string Instance, string Neuron, Guid Projection, Guid Synapse);

internal sealed record ObservedMotorEmission(long Ordinal, Guid MessageId, double FiredAtNeuralMs,
    string SourceInstanceKey, string SourceNeuronId, Guid ProjectionId, Guid SourceSynapseId)
{
    internal SpikeMessage ToMessage() => new()
    {
        MessageId = MessageId, TimestampMs = FiredAtNeuralMs, SourceInstanceKey = SourceInstanceKey,
        SourceNeuronId = SourceNeuronId, ProjectionId = ProjectionId, SynapseId = SourceSynapseId,
        SourceStructure = StructureId.SpinalCordMotor, Neurotransmitter = NTEnum.ACETYLCHOLINE
    };
}

internal sealed record ObservedMotorSnapshot(string Source, double TimeMs, int CompletedSamples,
    long IntegrationCalls, string SuppliedDurationUnits, bool Faulted, IReadOnlyList<double> State,
    IReadOnlyList<double> CrossingsMs, IReadOnlyList<ObservedMotorEmission> Emissions);

internal sealed class ObservedMotorSource
{
    internal const string Version = "isolated-observed-motor-source-v1";
    internal const double SampleMs = .1, MaximumIntegrationStepMs = .003125, DurationMs = 6000;
    internal const int Samples = 60_000;
    private readonly MotoneuronReferenceCell _cell = new(MotoneuronReferenceParameters.CatLowThreshold, -65,
        MotoneuronDiagnosticBlock.None, MotoneuronAccumulation.Compensated);
    private readonly Guid _evidenceRun;
    private readonly ObservedMotorIdentity _identity;
    private readonly bool _stimulated;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly List<ObservedMotorEmission> _emissions = [];
    private int _samples;
    private long _calls;
    private Int128 _supplied;
    private bool _faulted;
    private bool _claimed;
    internal ObservedMotorIdentity Identity => _identity;
    internal string Source => _stimulated ? "soma-6000" : "quiet";

    internal ObservedMotorSource(Guid evidenceRun, ObservedMotorIdentity identity, bool stimulated)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (evidenceRun == Guid.Empty || string.IsNullOrWhiteSpace(identity.Instance) || identity.Instance != identity.Instance.Trim() ||
            string.IsNullOrWhiteSpace(identity.Neuron) || identity.Neuron != identity.Neuron.Trim() ||
            identity.Projection == Guid.Empty || identity.Synapse == Guid.Empty)
            throw new ArgumentException("Require stable source-cell and projection/synapse identities.");
        _evidenceRun = evidenceRun; _identity = identity; _stimulated = stimulated;
    }

    internal ObservedMotorSnapshot Capture()
    {
        RequireOwner();
        return new(Source, _cell.TimeMs, _samples, _calls, _supplied.ToString(System.Globalization.CultureInfo.InvariantCulture), _faulted,
            Array.AsReadOnly(_cell.CopyState()), Array.AsReadOnly(_cell.CopyCrossings()), Array.AsReadOnly(_emissions.ToArray()));
    }

    // Only the fixed source grid can advance the neuron. Native body endpoints,
    // packet cadence, wall time and acknowledgements cannot split its RK4 grid.
    internal ObservedMotorSnapshot AdvanceOneSample()
    {
        RequireOwner();
        if (_claimed) throw new InvalidOperationException("Bound source advances only through its motor lane.");
        return AdvanceCore();
    }

    internal Func<ObservedMotorSnapshot> ClaimForLane()
    {
        RequireOwner();
        if (_claimed || _faulted) throw new InvalidOperationException("Source cannot acquire another motor owner.");
        _claimed = true;
        return AdvanceCore;
    }

    private ObservedMotorSnapshot AdvanceCore()
    {
        RequireOwner();
        if (_faulted) throw new InvalidOperationException("Faulted source cannot resume.");
        if (_samples == Samples) throw new InvalidOperationException("Declared neuronal horizon is complete.");
        try
        {
            double until = (_samples + 1) * SampleMs;
            while (_cell.TimeMs < until)
            {
                double boundary = !_stimulated ? DurationMs : _cell.TimeMs < 1000 ? 1000 : _cell.TimeMs < 3000 ? 3000 : DurationMs;
                double current = _stimulated && _cell.TimeMs >= 1000 && _cell.TimeMs < 3000 ? 6000 : 0;
                double dt = ClockAudit.BoundaryDuration(_cell.TimeMs, Math.Min(until, boundary), MaximumIntegrationStepMs);
                _cell.Advance(dt, current, 0, 0);
                _supplied = checked(_supplied + ClockAudit.Units(dt)); _calls = checked(_calls + 1);
                if (_supplied != ClockAudit.Units(_cell.TimeMs)) throw new InvalidOperationException("Source lost integrated duration.");
            }
            if (_cell.TimeMs != until) throw new InvalidOperationException("Source missed its own sample endpoint.");
            double[] crossings = _cell.CopyCrossings();
            if (crossings.Length < _emissions.Count) throw new InvalidOperationException("Observed source crossing disappeared.");
            for (int i = _emissions.Count; i < crossings.Length; ++i)
            {
                double at = crossings[i];
                if (!double.IsFinite(at) || at <= 0 || at > until || (i > 0 && at <= crossings[i - 1]))
                    throw new InvalidOperationException("Noncausal or repeated source crossing.");
                // Route identity participates so distinct cells cannot acquire
                // the same event identity merely by having the same ordinal.
                Guid id = StableNeuralIdentity.Create(Version, _evidenceRun, Source, _identity.Instance,
                    _identity.Neuron, _identity.Projection, _identity.Synapse, i + 1);
                _emissions.Add(new(i + 1, id, at, _identity.Instance, _identity.Neuron, _identity.Projection, _identity.Synapse));
            }
            ++_samples;
            return Capture();
        }
        catch { _faulted = true; throw; }
    }

    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("Source has one simulation owner.");
    }
}

// The source and publisher have one owner. Publication is transactional: an
// unsuccessful publication retains the same original events and watermark.
// Retries reuse retained batches; they never invoke the neuron again.
internal sealed class ObservedMotorLane
{
    private readonly ObservedMotorSource _source;
    private readonly MotorAxonReplayBuffer _sender;
    private readonly Func<ObservedMotorSnapshot> _advanceSource;
    private readonly int _maximumBatches, _maximumEvents, _owner = Environment.CurrentManagedThreadId;
    private int _publishedEmissions;
    private double _publishedThrough;

    internal ObservedMotorLane(Guid evidenceRun, ObservedMotorIdentity identity, bool stimulated,
        MotorAxonAnatomy anatomy, MotorAxonClock clock, int maximumBatches = 32, int maximumEvents = 65_536)
        : this(new ObservedMotorSource(evidenceRun, identity, stimulated), anatomy, clock, maximumBatches, maximumEvents) { }

    // Explicitly attach a fresh body after a recorded QUIET source prehistory.
    // Never reset the cell, shift its original timestamps or discard crossings.
    internal ObservedMotorLane(ObservedMotorSource source, MotorAxonAnatomy anatomy, MotorAxonClock clock,
        int maximumBatches = 32, int maximumEvents = 65_536)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(anatomy); ArgumentNullException.ThrowIfNull(clock);
        var identity = source.Identity; var initial = source.Capture();
        clock.Validate();
        if (clock.NeuralEpochMs != initial.TimeMs || initial.Faulted || initial.Emissions.Count != 0 ||
            initial.CrossingsMs.Count != 0 || maximumBatches <= 0 || maximumEvents <= 0 ||
            anatomy.Contacts.Count(c => c.SourceInstanceKey == identity.Instance) != 1)
            throw new ArgumentException("Require one source cell, an exactly matching epoch and fully retained quiet prehistory.");
        _ = anatomy.Resolve(identity.Instance, identity.Neuron, identity.Projection);
        _source = source;
        _sender = new(anatomy, clock, identity.Instance, maximumBatches, maximumEvents);
        _maximumBatches = maximumBatches; _maximumEvents = maximumEvents;
        _publishedThrough = initial.TimeMs;
        _advanceSource = source.ClaimForLane();
    }

    internal ObservedMotorSnapshot CaptureSource() { RequireOwner(); return _source.Capture(); }
    internal int UnpublishedEmissions { get { RequireOwner(); return _source.Capture().Emissions.Count - _publishedEmissions; } }
    internal IReadOnlyList<MotorAxonBatch> Unacknowledged() { RequireOwner(); return _sender.Unacknowledged(); }

    internal ObservedMotorSnapshot AdvanceOneSample()
    {
        RequireOwner();
        if (_sender.Faulted || _sender.PendingBatches >= _maximumBatches ||
            _sender.PendingEvents + UnpublishedEmissions >= _maximumEvents)
            throw new InvalidOperationException("Motor output is backpressured; preserve source state and recover retained delivery first.");
        return _advanceSource();
    }

    internal MotorAxonBatch PublishCompleted()
    {
        RequireOwner(); var snapshot = _source.Capture();
        if (snapshot.Faulted || snapshot.TimeMs <= _publishedThrough)
            throw new InvalidOperationException("Require a new completed neuronal interval; retry retained batches instead.");
        var original = snapshot.Emissions.Skip(_publishedEmissions).Select(e => e.ToMessage()).ToArray();
        var batch = _sender.Publish(original, snapshot.TimeMs);
        _publishedEmissions = snapshot.Emissions.Count; _publishedThrough = snapshot.TimeMs;
        return batch;
    }

    internal void Acknowledge(MotorAxonAcknowledgement receipt) { RequireOwner(); _sender.Acknowledge(receipt); }
    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("Motor lane has one simulation owner.");
    }
}
