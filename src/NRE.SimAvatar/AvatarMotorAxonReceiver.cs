using NeuralResonanceEngine.Protocol;

namespace NRE.SimAvatar;

public enum MotorAxonBatchDisposition { Accepted, ExactReplay }

public sealed record AvatarMotorAxonArrival(Guid SessionId, string SourceInstanceKey,
    Guid MessageId, long Ordinal, string SourceNeuronId, Guid ProjectionId, Guid SourceSynapseId, Guid ContactId,
    string MotorUnitId, double FiredAtNeuralMs, double ArrivalAtBodyMs);

// Isolated transport candidate. This produces motor-unit arrival events, not
// excitation levels, joint targets, muscle force or a gait. All callers must
// serialize access. No live body/Unity controller currently consumes it.
public sealed class AvatarMotorAxonReceiver
{
    private sealed class SourceState
    {
        public long Sequence, Ordinal;
        public double Watermark;
        public readonly Dictionary<long, string> RetainedBatches = new();
        public SourceState(double watermark) => Watermark = watermark;
    }

    private readonly MotorAxonAnatomy _anatomy;
    private readonly MotorAxonClock _clock;
    private readonly Dictionary<string, SourceState> _sources;
    private readonly HashSet<Guid> _seen = new();
    private readonly int _maximumPending, _maximumEpochEvents, _retainedBatches;
    private readonly PriorityQueue<AvatarMotorAxonArrival, AvatarMotorAxonArrival> _pending = new(
        Comparer<AvatarMotorAxonArrival>.Create((a, b) =>
        {
            int order = a.ArrivalAtBodyMs.CompareTo(b.ArrivalAtBodyMs);
            if (order == 0) order = StringComparer.Ordinal.Compare(a.SourceInstanceKey, b.SourceInstanceKey);
            return order == 0 ? a.Ordinal.CompareTo(b.Ordinal) : order;
        }));

    public double BodyTimeMs { get; private set; }
    public double CompleteThroughBodyMs => _clock.ToBodyMs(_sources.Values.Min(s => s.Watermark));
    public int PendingEvents => _pending.Count;
    public long AcceptedEvents => _seen.Count;
    public long DeliveredEvents { get; private set; }

    public AvatarMotorAxonReceiver(MotorAxonAnatomy anatomy, MotorAxonClock clock,
        int maximumPending = 65_536, int maximumEpochEvents = 1_000_000, int retainedBatches = 64)
    {
        ArgumentNullException.ThrowIfNull(anatomy); ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        if (maximumPending <= 0 || maximumEpochEvents <= 0 || retainedBatches <= 0)
            throw new ArgumentException("Declare positive queue, epoch and replay capacities.");
        _anatomy = anatomy; _clock = clock; _maximumPending = maximumPending;
        _maximumEpochEvents = maximumEpochEvents; _retainedBatches = retainedBatches;
        _sources = anatomy.Contacts.Select(c => c.SourceInstanceKey).Distinct(StringComparer.Ordinal)
            .ToDictionary(k => k, _ => new SourceState(clock.NeuralEpochMs), StringComparer.Ordinal);
        BodyTimeMs = clock.BodyEpochMs;
    }

    public MotorAxonBatchDisposition Accept(MotorAxonBatch batch)=>AcceptCore(batch).Disposition;

    // Receipt is made from the same immutable snapshot that passed acceptance.
    // An exact replay returns the same receipt without applying its events again.
    public MotorAxonAcknowledgement AcceptWithAcknowledgement(MotorAxonBatch batch)=>AcceptCore(batch).Receipt;

    private (MotorAxonBatchDisposition Disposition,MotorAxonAcknowledgement Receipt) AcceptCore(MotorAxonBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Version != MotorAxonAnatomy.ProtocolVersion || batch.SessionId != _clock.SessionId ||
            batch.AnatomyFingerprint != _anatomy.Fingerprint || batch.SourceInstanceKey is null ||
            !_sources.TryGetValue(batch.SourceInstanceKey, out var source) || batch.Sequence <= 0 ||
            batch.Spikes is null || !double.IsFinite(batch.CompleteThroughNeuralMs))
            throw new ArgumentException("Wrong protocol/session/anatomy/source or invalid batch header.");
        // Snapshot caller-owned collections before checking or queuing anything.
        var captured = batch with { Spikes = Array.AsReadOnly(batch.Spikes.ToArray()) };
        string digest = MotorAxonBatchIntegrity.Digest(captured);
        if (batch.Sequence <= source.Sequence)
        {
            if (!source.RetainedBatches.TryGetValue(batch.Sequence, out var previous) || previous != digest)
                throw new ArgumentException("Conflicting or expired replay; no events applied.");
            return (MotorAxonBatchDisposition.ExactReplay,MotorAxonBatchIntegrity.Receipt(captured,digest));
        }
        if (batch.Sequence != checked(source.Sequence + 1))
            throw new ArgumentException("Missing motor batch; recover the gap before advancing completeness.");
        if (batch.CompleteThroughNeuralMs < source.Watermark)
            throw new ArgumentException("Source completeness cannot move backward.");
        _clock.ToBodyMs(batch.CompleteThroughNeuralMs);
        if (captured.Spikes.Count > _maximumPending - _pending.Count ||
            captured.Spikes.Count > _maximumEpochEvents - _seen.Count)
            throw new InvalidOperationException("Motor-event capacity exhausted; batch rejected intact, never truncated.");
        var arrivals = new List<AvatarMotorAxonArrival>(captured.Spikes.Count);
        var ids = new HashSet<Guid>();
        double previousFiring = source.Watermark;
        foreach (var spike in captured.Spikes)
        {
            MotorAxonAnatomy.ValidateSpike(spike);
            var contact = _anatomy.Resolve(batch.SourceInstanceKey, spike.SourceNeuronId, spike.ProjectionId);
            if (spike.Ordinal != checked(source.Ordinal + arrivals.Count + 1) ||
                spike.FiredAtNeuralMs <= source.Watermark || spike.FiredAtNeuralMs < previousFiring ||
                spike.FiredAtNeuralMs > batch.CompleteThroughNeuralMs ||
                _seen.Contains(spike.MessageId) || !ids.Add(spike.MessageId))
                throw new ArgumentException("Missing/repeated event or firing outside its complete source interval.");
            double arrival = _clock.ToBodyMs(spike.FiredAtNeuralMs) + contact.ConductionDelayMs;
            if (!double.IsFinite(arrival) || arrival <= BodyTimeMs)
                throw new ArgumentException("Motor event is late or outside the clock domain; never retime it to now.");
            arrivals.Add(new(_clock.SessionId, batch.SourceInstanceKey, spike.MessageId, spike.Ordinal,
                spike.SourceNeuronId, spike.ProjectionId, spike.SourceSynapseId, contact.ContactId, contact.MotorUnitId,
                spike.FiredAtNeuralMs, arrival));
            previousFiring = spike.FiredAtNeuralMs;
        }
        foreach (var arrival in arrivals) _pending.Enqueue(arrival, arrival);
        _seen.UnionWith(ids); source.Ordinal += arrivals.Count;
        source.Sequence = batch.Sequence; source.Watermark = batch.CompleteThroughNeuralMs;
        source.RetainedBatches.Add(batch.Sequence, digest);
        if (source.RetainedBatches.Count > _retainedBatches)
            source.RetainedBatches.Remove(source.Sequence - _retainedBatches);
        return (MotorAxonBatchDisposition.Accepted,MotorAxonBatchIntegrity.Receipt(captured,digest));
    }

    // The caller advances contraction at each returned event's own arrival time,
    // not at bodyTimeMs for the whole list. No event is synthesized during a gap.
    // Completeness is the minimum across ALL declared source instances; this
    // conservative bound deliberately does not speculate about future silence.
    public IReadOnlyList<AvatarMotorAxonArrival> AdvanceTo(double bodyTimeMs)
    {
        if (!double.IsFinite(bodyTimeMs) || bodyTimeMs < BodyTimeMs || bodyTimeMs > CompleteThroughBodyMs)
            throw new ArgumentOutOfRangeException(nameof(bodyTimeMs), "Wait for complete neuronal input; never outrun or rewind it.");
        var arrivals = new List<AvatarMotorAxonArrival>();
        while (_pending.TryPeek(out var next, out _) && next.ArrivalAtBodyMs <= bodyTimeMs)
            arrivals.Add(_pending.Dequeue());
        BodyTimeMs = bodyTimeMs; DeliveredEvents += arrivals.Count;
        return arrivals.AsReadOnly();
    }
}
