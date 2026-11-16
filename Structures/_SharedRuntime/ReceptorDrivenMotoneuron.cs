using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;

internal sealed record MotorReceptorBatchReceipt(Guid SessionId, Guid ContactId, long Sequence,
    double CompleteThroughSourceMs, int Events, string Sha256);
internal sealed record MotorReceptorLaneState(Guid ContactId, int Batches, double? CompleteThroughSourceMs,
    int AdmittedEvents, int DeliveredEvents);
internal sealed record ReceptorDrivenMotorState(Guid SessionId, double TimeMs, string IntegrationVersion,
    double[] MembraneState, double[] Crossings, MotorReceptorState[] Contacts,
    MotorReceptorLaneState[] InputLanes, long AdmittedEvents, long DeliveredEvents, long IntegrationSteps,
    double MaximumChargeLedgerErrorFc, double MaximumCalciumLedgerErrorMm, bool Faulted);

// Isolated neuronal owner, not a live StructureEngine selection. Completeness
// is explicitly supplied by the producing source; this class validates its
// chronology and accounting, not the biological origin of that declaration.
// No soma-current injection, firing-rate conversion, or prescribed crossings.
internal sealed class ReceptorDrivenMotoneuron
{
    private sealed record Pending(SpikeMessage Original, MotorReceptorReceipt Receipt);
    private sealed class Lane(MotorReceptorDefinition definition, MotorReceptorContact receptor)
    {
        internal readonly MotorReceptorDefinition Definition = definition;
        internal readonly MotorReceptorContact Receptor = receptor;
        internal readonly List<Pending> Events = [];
        internal readonly HashSet<Guid> EventIds = [];
        internal readonly Dictionary<long, MotorReceptorBatchReceipt> Batches = [];
        internal double CompleteThroughSourceMs;
        internal int Delivered;
    }

    private readonly Guid _session;
    private readonly int _owner = Environment.CurrentManagedThreadId, _maximumEpochEvents, _maximumEpochBatches;
    private readonly MotoneuronReferenceCell _cell;
    private readonly Lane[] _lanes;
    private readonly MotorReceptorContact[] _contacts;
    private readonly Dictionary<Guid, string> _originalHashes = [];
    private bool _faulted;
    private long _integrationSteps;

    internal ReceptorDrivenMotoneuron(MotoneuronReferenceParameters parameters,
        IReadOnlyList<MotorReceptorDefinition> contacts, Guid sessionId,
        double initialVoltageMv = -65, MotoneuronDiagnosticBlock block = MotoneuronDiagnosticBlock.None,
        MotoneuronAccumulation accumulation = MotoneuronAccumulation.Compensated,
        int maximumEpochEventsPerContact = 1_000_000, int maximumEpochBatchesPerContact = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        if (sessionId == Guid.Empty || contacts.Count == 0 || maximumEpochEventsPerContact <= 0 || maximumEpochBatchesPerContact <= 0)
            throw new ArgumentException("Declare a fresh neural session, contacts and finite epoch capacities.");
        foreach (var d in contacts) { ArgumentNullException.ThrowIfNull(d); d.Validate(); }
        var target = contacts[0];
        if (contacts.Any(d => d.TargetInstance != target.TargetInstance || d.TargetNeuron != target.TargetNeuron ||
                d.TargetNeuronIndex != target.TargetNeuronIndex) || contacts.Select(d => d.ContactId).Distinct().Count() != contacts.Count)
            throw new ArgumentException("Distinct physical contacts must belong to one target motor neuron.");
        _session = sessionId; _maximumEpochEvents = maximumEpochEventsPerContact; _maximumEpochBatches = maximumEpochBatchesPerContact;
        _cell = new(parameters, initialVoltageMv, block, accumulation);
        _lanes = contacts.OrderBy(d => d.ContactId).Select(d => new Lane(d,
            new MotorReceptorContact(d, sessionId, maximumEpochEvents: maximumEpochEventsPerContact))).ToArray();
        _contacts = _lanes.Select(l => l.Receptor).ToArray();
    }

    internal ReceptorDrivenMotorState Capture()
    {
        RequireOwner();
        return new(_session, _cell.TimeMs, _cell.IntegrationVersion, _cell.CopyState(), _cell.CopyCrossings(),
            _contacts.Select(c => c.Capture()).ToArray(),
            _lanes.Select(l => new MotorReceptorLaneState(l.Definition.ContactId, l.Batches.Count,
                l.Batches.Count == 0 ? null : l.CompleteThroughSourceMs, l.Events.Count, l.Delivered)).ToArray(),
            _lanes.Sum(l => (long)l.Events.Count), _lanes.Sum(l => (long)l.Delivered), _integrationSteps,
            _cell.MaximumChargeLedgerErrorFc, _cell.MaximumCalciumLedgerErrorMm, _faulted);
    }

    // Each contact has an explicit delivery lane. The first batch includes
    // firing time zero; subsequent batches cover (previous, completeThrough].
    // Exact batch replay is inert, including after its spikes were delivered.
    // Admission stores deep snapshots: later caller mutation cannot change input.
    internal MotorReceptorBatchReceipt Admit(Guid sessionId, Guid contactId, long sequence,
        double completeThroughSourceMs, IReadOnlyList<SpikeMessage> originals)
    {
        RequireOperational(); ArgumentNullException.ThrowIfNull(originals);
        if (sessionId != _session || sequence <= 0 || !double.IsFinite(completeThroughSourceMs) || completeThroughSourceMs < 0)
            throw new ArgumentException("Invalid source session, sequence or completeness time.");
        var lane = _lanes.SingleOrDefault(l => l.Definition.ContactId == contactId)
            ?? throw new ArgumentException("Unknown receptor contact.");
        double arrivalHorizon = completeThroughSourceMs + lane.Definition.AxonalDelayMs;
        // Validate the supported clock before a completeness declaration commits.
        if (!double.IsFinite(arrivalHorizon) || arrivalHorizon + 1 <= arrivalHorizon ||
            (lane.Definition.AxonalDelayMs > 0 && arrivalHorizon <= completeThroughSourceMs))
            throw new ArgumentException("Completeness/delay cannot be represented on the neural clock.");
        if (originals.Count > _maximumEpochEvents)
            throw new InvalidOperationException("Batch exceeds this declared epoch's capacity.");
        var incoming = new Pending[originals.Count];
        for (int i = 0; i < originals.Count; ++i)
        {
            ArgumentNullException.ThrowIfNull(originals[i]);
            var frozen = JsonSerializer.Deserialize<SpikeMessage>(JsonSerializer.SerializeToUtf8Bytes(originals[i]))
                ?? throw new ArgumentException("Cannot snapshot original presynaptic event.");
            incoming[i] = new(frozen, lane.Receptor.DescribeOriginal(sessionId, frozen));
        }
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            SessionId = sessionId, ContactId = contactId, Sequence = sequence, CompleteThroughSourceMs = completeThroughSourceMs,
            OriginalEventHashes = incoming.Select(p => p.Receipt.OriginalEventSha256).ToArray()
        })));
        var receipt = new MotorReceptorBatchReceipt(sessionId, contactId, sequence, completeThroughSourceMs, incoming.Length, hash);
        if (lane.Batches.TryGetValue(sequence, out var old))
        {
            if (old != receipt) throw new ArgumentException("Conflicting retained source batch.");
            return old;
        }
        if (sequence != lane.Batches.Count + 1L || completeThroughSourceMs < lane.CompleteThroughSourceMs)
            throw new ArgumentException("Source batches must be contiguous and completeness cannot retreat.");
        if (lane.Batches.Count >= _maximumEpochBatches || incoming.Length > _maximumEpochEvents - lane.Events.Count)
            throw new InvalidOperationException("Epoch capacity exhausted; retain input, do not discard replay history.");
        var ids = new HashSet<Guid>();
        double priorTime = lane.CompleteThroughSourceMs;
        double? previousArrival = lane.Events.Count == 0 ? null : lane.Events[^1].Receipt.ArrivedAtMs;
        foreach (var item in incoming)
        {
            var e = item.Receipt;
            if (_originalHashes.TryGetValue(e.EventId, out var existingHash) && existingHash != e.OriginalEventSha256)
                throw new ArgumentException("One original event cannot change identity or payload between contact lanes.");
            if (e.FiredAtMs > completeThroughSourceMs || e.FiredAtMs < priorTime ||
                (lane.Batches.Count != 0 && e.FiredAtMs <= lane.CompleteThroughSourceMs) ||
                e.ArrivedAtMs < _cell.TimeMs || lane.EventIds.Contains(e.EventId) || !ids.Add(e.EventId))
                throw new ArgumentException("New events must be original, ordered and inside the newly complete interval.");
            if (previousArrival.HasValue && e.ArrivedAtMs < previousArrival.Value + MotorReceptorContact.PulseDurationMs)
                throw new ArgumentException("Overlapping release is unsupported; preserve and reject the whole batch.");
            priorTime = e.FiredAtMs; previousArrival = e.ArrivedAtMs;
        }
        try
        {
            lane.Events.AddRange(incoming); lane.EventIds.UnionWith(ids);
            foreach (var item in incoming) _originalHashes.TryAdd(item.Receipt.EventId, item.Receipt.OriginalEventSha256);
            lane.Batches.Add(sequence, receipt); lane.CompleteThroughSourceMs = completeThroughSourceMs;
            return receipt;
        }
        catch { _faulted = true; throw; }
    }

    // Advances only when EVERY declared lane accounts for input up to the
    // requested time. A silent but accounted interval is different from missing
    // input. The public target must not exceed this causal completeness boundary.
    internal void AdvanceTo(double targetNeuralMs, double maximumStepMs = .003125)
    {
        RequireOperational();
        if (!double.IsFinite(targetNeuralMs) || targetNeuralMs < _cell.TimeMs ||
            !double.IsFinite(maximumStepMs) || maximumStepMs <= 0 || maximumStepMs > MotoneuronReferenceCell.MaximumStepMs)
            throw new ArgumentException("Require a forward neural target and bounded physical solver step.");
        if (_lanes.Any(l => l.Batches.Count == 0 || targetNeuralMs > l.CompleteThroughSourceMs + l.Definition.AxonalDelayMs))
            throw new InvalidOperationException("Input is incomplete; do not advance the cell or infer silence.");
        try
        {
            DeliverAtCurrentTime();
            while (_cell.TimeMs < targetNeuralMs)
            {
                double boundary = targetNeuralMs;
                foreach (var lane in _lanes)
                {
                    if (lane.Delivered < lane.Events.Count) boundary = Math.Min(boundary, lane.Events[lane.Delivered].Receipt.ArrivedAtMs);
                    if (lane.Receptor.Capture().PulseEndMs is { } end && end > _cell.TimeMs) boundary = Math.Min(boundary, end);
                }
                double dt = BoundaryDuration(_cell.TimeMs, boundary, maximumStepMs), next = _cell.TimeMs + dt;
                if (dt <= 0 || next <= _cell.TimeMs || next > boundary)
                    throw new InvalidOperationException("Cannot represent the next event/solver boundary; no timing repair.");
                _cell.AdvanceWithReceptors(dt, _contacts);
                if (_cell.TimeMs != next) throw new InvalidOperationException("Membrane clock differs from its owner.");
                foreach (var receptor in _contacts) receptor.AdvanceTo(next);
                _integrationSteps = checked(_integrationSteps + 1);
                DeliverAtCurrentTime();
            }
        }
        catch { _faulted = true; throw; }
    }

    // Endpoint-difference scheduling follows the qualified ClockAudit method.
    // A very early fractional event can leave from < maximum. Limit growth to
    // from in that case, so 0 < from <= end <= 2*from: Sterbenz subtraction then
    // makes end-from exact. The initial zero interval is exact directly. No
    // supplied biological duration is lost in a rounded clock addition.
    internal static double BoundaryDuration(double from, double until, double maximum)
    {
        if (!double.IsFinite(from) || from < 0 || !double.IsFinite(until) || until <= from ||
            !double.IsFinite(maximum) || maximum <= 0)
            throw new ArgumentException("Declare a finite forward interval and positive step bound.");
        double bound = from == 0 ? maximum : Math.Min(maximum, from);
        double end = Math.Min(until, from + bound);
        if (end - from > bound) end = Math.BitDecrement(end);
        double duration = end - from;
        if (!double.IsFinite(end) || end <= from || duration <= 0 || duration > maximum ||
            from + duration != end || (from > 0 && end - from > from))
            throw new InvalidOperationException("Cannot represent a duration-preserving neural interval.");
        return duration;
    }

    private void DeliverAtCurrentTime()
    {
        foreach (var lane in _lanes)
            while (lane.Delivered < lane.Events.Count && lane.Events[lane.Delivered].Receipt.ArrivedAtMs <= _cell.TimeMs)
            {
                var pending = lane.Events[lane.Delivered];
                if (pending.Receipt.ArrivedAtMs != _cell.TimeMs)
                    throw new InvalidOperationException("Owner passed an original release; no late delivery.");
                var actual = lane.Receptor.Release(_session, pending.Original);
                if (actual != pending.Receipt) throw new InvalidOperationException("Original event differs from its admission snapshot.");
                lane.Delivered++;
            }
    }

    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("One owner advances this neuronal circuit.");
    }
    private void RequireOperational()
    {
        RequireOwner();
        if (_faulted) throw new InvalidOperationException("Faulted synaptic motor circuit cannot resume.");
    }
}
