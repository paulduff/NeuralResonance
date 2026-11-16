using NeuralResonanceEngine.Protocol;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed record ReceptorMotorIdentity(string Instance, string Neuron, Guid Projection, Guid Synapse);
internal sealed record ReceptorMotorEmission(long Ordinal, Guid MessageId, double FiredAtNeuralMs,
    string SourceInstanceKey, string SourceNeuronId, Guid ProjectionId, Guid SourceSynapseId)
{
    internal SpikeMessage ToMessage() => new()
    {
        MessageId = MessageId, TimestampMs = FiredAtNeuralMs, SourceInstanceKey = SourceInstanceKey,
        SourceNeuronId = SourceNeuronId, ProjectionId = ProjectionId, SynapseId = SourceSynapseId,
        SourceStructure = StructureId.SpinalCordMotor, Neurotransmitter = NTEnum.ACETYLCHOLINE
    };
}

internal sealed record ReceptorMotorSourceState(Guid SessionId, string ConfigurationFingerprint,
    long CompletedSamples, double SampleMs, double MaximumStepMs,
    MotoneuronDiagnosticBlock DiagnosticBlock, bool Faulted, ReceptorDrivenMotorState Cell,
    IReadOnlyList<ReceptorMotorEmission> Emissions);

// Explicit isolated source. Only the private cell's observed upward crossings
// become motor messages. Input provenance, spinal calibration and active axonal
// propagation are NOT established by this adapter. No live host selects it.
internal sealed class ReceptorMotorSource
{
    internal const string Version = "isolated-receptor-motor-source-v1";
    internal const double DefaultSampleMs = .1;
    private readonly ReceptorDrivenMotoneuron _cell;
    private readonly Guid _session;
    private readonly ReceptorMotorIdentity _identity;
    private readonly double _maximumStepMs, _sampleMs;
    private readonly MotoneuronDiagnosticBlock _block;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly List<ReceptorMotorEmission> _emissions = [];
    private long _samples;
    private bool _claimed, _faulted;
    internal ReceptorMotorIdentity Identity => _identity;
    internal string ConfigurationFingerprint { get; }

    internal ReceptorMotorSource(MotoneuronReferenceParameters parameters,
        IReadOnlyList<MotorReceptorDefinition> contacts, Guid session, ReceptorMotorIdentity identity,
        double maximumStepMs = .003125, double initialVoltageMv = -65,
        MotoneuronDiagnosticBlock block = MotoneuronDiagnosticBlock.None, double sampleMs = DefaultSampleMs)
    {
        ArgumentNullException.ThrowIfNull(contacts); ArgumentNullException.ThrowIfNull(identity);
        static bool Key(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
        if (session == Guid.Empty || !Key(identity.Instance) || !Key(identity.Neuron) ||
            identity.Projection == Guid.Empty || identity.Synapse == Guid.Empty || contacts.Count == 0 ||
            !double.IsFinite(maximumStepMs) || maximumStepMs <= 0 || maximumStepMs > MotoneuronReferenceCell.MaximumStepMs ||
            !double.IsFinite(sampleMs) || sampleMs <= 0 || !Enum.IsDefined(block))
            throw new ArgumentException("Declare one identified motor cell and its bounded integration step.");
        var input = contacts.ToArray();
        foreach (var contact in input)
        {
            ArgumentNullException.ThrowIfNull(contact); contact.Validate();
            if (contact.TargetInstance != identity.Instance || contact.TargetNeuron != identity.Neuron)
                throw new ArgumentException("Motor output must belong to the cell receiving these contacts.");
        }
        _cell = new(parameters, input, session, initialVoltageMv, block, MotoneuronAccumulation.Compensated);
        _session = session; _identity = identity; _maximumStepMs = maximumStepMs; _block = block; _sampleMs = sampleMs;
        ConfigurationFingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version, CellVersion = MotoneuronReferenceCell.CompensatedVersion, ReceptorVersion = MotorReceptorContact.Version,
            Parameters = parameters, Contacts = input.OrderBy(c => c.ContactId).ToArray(), Identity = identity,
            MaximumStepMs = maximumStepMs, SampleMs = sampleMs, InitialVoltageMv = initialVoltageMv, Block = block
        })));
    }

    internal ReceptorMotorSourceState Capture()
    {
        RequireOwner(); var state = _cell.Capture();
        return new(_session, ConfigurationFingerprint, _samples, _sampleMs, _maximumStepMs, _block, _faulted || state.Faulted, state,
            Array.AsReadOnly(_emissions.ToArray()));
    }

    internal MotorReceptorBatchReceipt Admit(Guid session, Guid contact, long sequence,
        double completeThroughSourceMs, IReadOnlyList<SpikeMessage> originals)
    {
        RequireUnclaimed(); return AdmitCore(session, contact, sequence, completeThroughSourceMs, originals);
    }

    internal ReceptorMotorSourceState AdvanceOneSample()
    {
        RequireUnclaimed(); return AdvanceCore();
    }

    internal (Func<ReceptorMotorSourceState> Advance,
        Func<Guid, Guid, long, double, IReadOnlyList<SpikeMessage>, MotorReceptorBatchReceipt> Admit) ClaimForLane()
    {
        RequireUnclaimed();
        if (Capture().Faulted) throw new InvalidOperationException("Faulted neuronal source cannot acquire a motor owner.");
        _claimed = true;
        return (AdvanceCore, AdmitCore);
    }

    private MotorReceptorBatchReceipt AdmitCore(Guid session, Guid contact, long sequence,
        double completeThroughSourceMs, IReadOnlyList<SpikeMessage> originals)
    {
        RequireOwner();
        if (_faulted) throw new InvalidOperationException("Faulted neuronal source cannot resume.");
        return _cell.Admit(session, contact, sequence, completeThroughSourceMs, originals);
    }

    private ReceptorMotorSourceState AdvanceCore()
    {
        RequireOwner();
        if (_faulted) throw new InvalidOperationException("Faulted neuronal source cannot resume.");
        long next = checked(_samples + 1); double until = next * _sampleMs;
        if (!double.IsFinite(until) || until <= _cell.Capture().TimeMs)
            throw new InvalidOperationException("Neuronal sample endpoint is no longer representable.");
        // Missing input is a recoverable, pre-mutation rejection in the cell.
        // An integration failure remains faulted in its retained cell snapshot.
        _cell.AdvanceTo(until, _maximumStepMs);
        try
        {
            var state = _cell.Capture();
            if (state.TimeMs != until || state.Crossings.Length < _emissions.Count)
                throw new InvalidOperationException("Neuronal time or observed crossing history changed.");
            for (int i = 0; i < _emissions.Count; ++i)
                if (state.Crossings[i] != _emissions[i].FiredAtNeuralMs)
                    throw new InvalidOperationException("An earlier observed crossing changed.");
            for (int i = _emissions.Count; i < state.Crossings.Length; ++i)
            {
                double at = state.Crossings[i];
                if (!double.IsFinite(at) || at <= 0 || at > until || (i > 0 && at <= state.Crossings[i - 1]))
                    throw new InvalidOperationException("Require original, strictly ordered observed crossings.");
                Guid id = StableNeuralIdentity.Create(Version, _session, _identity.Instance,
                    _identity.Neuron, _identity.Projection, _identity.Synapse, i + 1);
                _emissions.Add(new(i + 1, id, at, _identity.Instance, _identity.Neuron, _identity.Projection, _identity.Synapse));
            }
            _samples = next;
            return Capture();
        }
        catch { _faulted = true; throw; }
    }

    private void RequireUnclaimed()
    {
        RequireOwner();
        if (_claimed) throw new InvalidOperationException("Bound source advances and admits input only through its motor lane.");
    }

    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("Neuronal source has one simulation owner.");
    }
}

// Existing transport and Avatar receiver retain their wire contract. Publication
// and acknowledgements never call the cell. A bounded packet may publish a
// completed prefix; the unreported suffix stays in the private source history.
internal sealed class ReceptorMotorLane
{
    private readonly ReceptorMotorSource _source;
    private readonly MotorAxonReplayBuffer _sender;
    private readonly Func<ReceptorMotorSourceState> _advance;
    private readonly Func<Guid, Guid, long, double, IReadOnlyList<SpikeMessage>, MotorReceptorBatchReceipt> _admit;
    private readonly int _maximumBatches, _maximumEvents, _maximumEpochEvents;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private int _publishedEmissions;
    private double _publishedThrough;

    internal ReceptorMotorLane(ReceptorMotorSource source, MotorAxonAnatomy anatomy, MotorAxonClock clock,
        int maximumBatches = 32, int maximumEvents = 65_536, int maximumEpochEvents = 1_000_000,
        bool allowDiagnosticSource = false)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(anatomy); ArgumentNullException.ThrowIfNull(clock);
        clock.Validate(); var initial = source.Capture(); var identity = source.Identity;
        if (initial.Faulted || (!allowDiagnosticSource && initial.DiagnosticBlock != MotoneuronDiagnosticBlock.None) ||
            initial.SessionId != clock.SessionId || initial.Cell.TimeMs != clock.NeuralEpochMs ||
            initial.Cell.Crossings.Length != 0 || initial.Emissions.Count != 0 || maximumBatches <= 0 || maximumEvents <= 0 ||
            maximumEpochEvents <= 0 || anatomy.Contacts.Count(c => c.SourceInstanceKey == identity.Instance) != 1)
            throw new ArgumentException("Require one source cell, matching session/epoch and completely retained quiet prehistory.");
        _ = anatomy.Resolve(identity.Instance, identity.Neuron, identity.Projection);
        _sender = new(anatomy, clock, identity.Instance, maximumBatches, maximumEvents, maximumEpochEvents: maximumEpochEvents);
        _source = source; _maximumBatches = maximumBatches; _maximumEvents = maximumEvents; _maximumEpochEvents = maximumEpochEvents;
        _publishedThrough = initial.Cell.TimeMs;
        (_advance, _admit) = source.ClaimForLane();
    }

    internal ReceptorMotorSourceState CaptureSource() { RequireOwner(); return _source.Capture(); }
    internal double PublishedThroughNeuralMs { get { RequireOwner(); return _publishedThrough; } }
    internal int UnpublishedEmissions { get { RequireOwner(); return _source.Capture().Emissions.Count - _publishedEmissions; } }
    internal IReadOnlyList<MotorAxonBatch> Unacknowledged() { RequireOwner(); return _sender.Unacknowledged(); }

    internal MotorReceptorBatchReceipt Admit(Guid session, Guid contact, long sequence,
        double completeThroughSourceMs, IReadOnlyList<SpikeMessage> originals)
    {
        RequireOwner(); return _admit(session, contact, sequence, completeThroughSourceMs, originals);
    }

    internal ReceptorMotorSourceState AdvanceOneSample()
    {
        RequireOwner(); var state = _source.Capture();
        if (_sender.Faulted || state.Faulted || _sender.PendingBatches >= _maximumBatches ||
            (long)_sender.PendingEvents + state.Emissions.Count - _publishedEmissions >= _maximumEvents ||
            state.Emissions.Count >= _maximumEpochEvents)
            throw new InvalidOperationException("Motor output is backpressured or its epoch is full; retain source state and delivery.");
        return _advance();
    }

    internal MotorAxonBatch PublishCompleted()
    {
        RequireOwner(); var state = _source.Capture();
        if (state.Faulted || state.Cell.TimeMs <= _publishedThrough)
            throw new InvalidOperationException("Require a new completed neuronal interval; retry retained batches instead.");
        int remaining = state.Emissions.Count - _publishedEmissions;
        int capacity = Math.Min(_maximumEvents - _sender.PendingEvents, _maximumEpochEvents - _publishedEmissions);
        if (remaining > 0 && capacity <= 0)
            throw new InvalidOperationException("Output capacity exhausted; no observed crossing or completeness discarded.");
        int count = Math.Min(remaining, capacity);
        var original = state.Emissions.Skip(_publishedEmissions).Take(count).Select(e => e.ToMessage()).ToArray();
        // A prefix only claims time through its last actual crossing. It cannot
        // hide the remaining crossings behind the cell's farther watermark.
        double through = count < remaining ? original[^1].TimestampMs : state.Cell.TimeMs;
        var batch = _sender.Publish(original, through);
        _publishedEmissions += count; _publishedThrough = through;
        return batch;
    }

    internal void Acknowledge(MotorAxonAcknowledgement receipt) { RequireOwner(); _sender.Acknowledge(receipt); }

    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("Motor output has one simulation owner.");
    }
}
