namespace NeuralResonanceEngine.Protocol;

// Snapshot original SpikeMessage fields into an ordered, explicit motor lane.
// No caller currently enables this lane in a live StructureEngine/host. Future
// integration must feed it at the neuronal emission point, before transport caps,
// and retain each returned batch for retransmission. Population telemetry cannot
// supply it. This class validates declarations; it does not authenticate biology.
public sealed class MotorAxonPublisher
{
    private readonly MotorAxonAnatomy _anatomy;
    private readonly MotorAxonClock _clock;
    private readonly string _instance;
    private readonly int _maximumEpochEvents;
    private readonly HashSet<Guid> _seen = new();
    private long _sequence, _ordinal;
    private double _watermark;

    public MotorAxonPublisher(MotorAxonAnatomy anatomy, MotorAxonClock clock, string instance,
        int maximumEpochEvents = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(anatomy); ArgumentNullException.ThrowIfNull(clock);
        clock.Validate();
        if (!anatomy.Contacts.Any(c => c.SourceInstanceKey == instance) || maximumEpochEvents <= 0)
            throw new ArgumentException("Require a declared source and positive epoch event capacity.");
        _anatomy = anatomy; _clock = clock; _instance = instance;
        _maximumEpochEvents = maximumEpochEvents; _watermark = clock.NeuralEpochMs;
    }

    public MotorAxonBatch Publish(IReadOnlyList<SpikeMessage> emitted, double completeThroughNeuralMs)
    {
        ArgumentNullException.ThrowIfNull(emitted);
        if (!double.IsFinite(completeThroughNeuralMs) || completeThroughNeuralMs < _watermark)
            throw new ArgumentException("Source completeness cannot move backward.");
        _clock.ToBodyMs(completeThroughNeuralMs);
        if (emitted.Count > _maximumEpochEvents - _seen.Count)
            throw new InvalidOperationException("Epoch event capacity exhausted; no events discarded.");
        var spikes = new MotorAxonSpike[emitted.Count];
        var ids = new HashSet<Guid>();
        double previous = _watermark;
        for (int i = 0; i < emitted.Count; ++i)
        {
            var source = emitted[i];
            if (source is null || source.SourceStructure != StructureId.SpinalCordMotor || source.SourceInstanceKey != _instance)
                throw new ArgumentException("Require this declared spinal motor instance's original output.");
            var spike = new MotorAxonSpike(source.MessageId, checked(_ordinal + i + 1), source.SourceNeuronId,
                source.ProjectionId, source.SynapseId, source.TimestampMs, source.Neurotransmitter, source.IsSyntheticBackground, source.IsFeedback);
            MotorAxonAnatomy.ValidateSpike(spike);
            _anatomy.Resolve(_instance, spike.SourceNeuronId, spike.ProjectionId);
            if (spike.FiredAtNeuralMs <= _watermark || spike.FiredAtNeuralMs < previous ||
                spike.FiredAtNeuralMs > completeThroughNeuralMs || _seen.Contains(spike.MessageId) || !ids.Add(spike.MessageId))
                throw new ArgumentException("Unordered, repeated, or out-of-interval neuronal emission.");
            previous = spike.FiredAtNeuralMs; spikes[i] = spike;
        }
        var batch = new MotorAxonBatch(MotorAxonAnatomy.ProtocolVersion, _clock.SessionId, _instance,
            _anatomy.Fingerprint, checked(_sequence + 1), completeThroughNeuralMs, Array.AsReadOnly(spikes));
        // Commit only after the complete batch validates. Never repair a bad
        // message by changing its identity, transmitter, firing time or route.
        _seen.UnionWith(ids); _ordinal += spikes.Length;
        _watermark = completeThroughNeuralMs; _sequence = batch.Sequence;
        return batch;
    }
}
