using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record RubralOutputRoute(string SourceInstance, string SourceNeuron, int SourceIndex,
    StructureId TargetStructure, string TargetNeuron, int TargetIndex, Guid Projection, Guid Synapse, NTEnum Transmitter);
internal sealed record RubralCellObservation(string Neuron, bool ActiveAtObservation, float VoltageMv,
    float NetCurrent, float FiringRateHz, float ActivityTrace);
// The engine sets TickTimestampMs before integration. Only CompletedTick and
// the owner's successfully validated frame establish completion.
internal sealed record RubralPopulationObservation(double? TickTimestampMs, long CompletedTick,
    IReadOnlyList<RubralCellObservation> Cells);
internal sealed record RubralInputDelivery(Guid OriginalMessageId, Guid ReceivingContactId,
    string NamedTarget, string ActualTarget, int ActualTargetIndex, double ArrivalAtMs,
    double AppliedAtMs, string OriginalJson);

public sealed partial class StructureEngine
{
    // Only the isolated owner installs this observer. Capture actual recipient
    // and original message after receptor integration, before learning.
    internal Action<RubralInputDelivery>? RubralInputObserver { get; set; }
    // Observation only. No synapse is created, stabilized or learned here. This
    // restricted descriptor is not a general projection/topology export API.
    internal IReadOnlyList<RubralOutputRoute> DescribeRubralPrimaryOutputs()
    {
        lock (_stateGate)
        {
            if (_profile.StructureId != StructureId.RedNucleus)
                throw new InvalidOperationException("This isolated descriptor is restricted to the existing rubral projection.");
            return Array.AsReadOnly(_neurons.Select(n =>
            {
                if (n.PreferredTarget != StructureId.SpinalCordMotor || n.PreferredNt != NTEnum.GLUTAMATE)
                    throw new InvalidOperationException("Rubral primary projection changed; review its contact mapping.");
                int index = _kernel.ResolveOutboundTargetIndex(n, n.PreferredTarget, _circuit);
                string target = $"auto-{n.PreferredTarget}-{index:000}";
                string key = n.Id + "|" + n.PreferredTarget + "|" + target + "|" + n.PreferredNt + "|N";
                Guid synapse = _outboundSynapses.TryGetValue(key, out var existing) ? existing.SynapseId :
                    StableNeuralIdentity.Create(StableNeuralIdentity.TopologyVersion, _instanceKey, key);
                return new RubralOutputRoute(_instanceKey, n.Id, n.Index, n.PreferredTarget, target,
                    index, synapse, synapse, n.PreferredNt);
            }).ToArray());
        }
    }

    internal RubralPopulationObservation ObserveRubralPopulation()
    {
        lock (_stateGate)
        {
            if (_profile.StructureId != StructureId.RedNucleus)
                throw new InvalidOperationException("Not an isolated rubral observation.");
            return new(double.IsFinite(_lastBiologicalTimestampMs) ? _lastBiologicalTimestampMs : null,
                _lastProcessedTick, Array.AsReadOnly(_neurons.Select(n => new RubralCellObservation(n.Id,
                    n.IsActive, n.MembranePotentialMillivolts, n.NetSynapticCurrent, n.FiringRateHz, n.ActivityTrace)).ToArray()));
        }
    }

    internal string ObserveRubralUndrainedOutput()
    {
        lock (_stateGate)
        {
            if (_profile.StructureId != StructureId.RedNucleus)
                throw new InvalidOperationException("Not an isolated rubral failure observation.");
            return JsonSerializer.Serialize(_outbound.ToArray());
        }
    }
}

// Strings retain the original serialized message bytes; callers cannot mutate
// a published event, its diagnostic context or the retained completeness claim.
internal sealed record RubralSourceFrame(Guid Session, long Sequence, double CompleteThroughMs,
    string CanonicalJson, string Sha256);
internal sealed record RubralSourceReceipt(Guid Session, long Sequence, string Sha256);
internal sealed record RubralSourceStatus(long CompletedSamples, double? ActualCompletedThroughMs,
    int AdmittedFixtureEvents, long ProducedEvents, bool HasPendingFrame, bool Faulted, string? Fault,
    double? EngineTickTimestampMs, long EngineCompletedTick, string? UndrainedOriginalJson);
internal sealed record RubralSourcePayload(Guid Session, long Sequence, double CompleteThroughMs,
    string TimingConvention, int FiredCount, int AdmittedFixtureEvents, IReadOnlyList<SpikeMessage> OriginalEvents,
    RubralPopulationObservation Observation, NeuralEnergyDiagnostics? Energy,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RubralInputDelivery>? InputDeliveries = null);

// Owns an actual, full-size DNNE RedNucleus engine, with the service's existing
// Izhikevich/STDP profile. External input is an explicitly declared diagnostic
// fixture, not a source of desired output firing times. No live host selects it.
internal sealed class IsolatedRubralSource : IDisposable
{
    internal const string Version = "isolated-owned-rubral-source-v1";
    internal const string TimingConvention = "existing-DNNE-completed-integration-substep";
    internal const double SampleMs = .125;
    private static readonly object ConstructionGate = new();
    private readonly StructureEngine _engine;
    private readonly Guid _session;
    private readonly double _duration;
    private readonly SpikeMessage[] _inputs;
    private readonly Dictionary<string, RubralOutputRoute> _routes;
    private readonly IsolatedSynapticTargets? _declaredTargets;
    private readonly List<RubralInputDelivery> _deliveries = new();
    private readonly HashSet<Guid> _deliveredIds = new();
    private readonly Dictionary<Guid, SpikeMessage> _originalInputs;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private long _samples, _produced;
    private int _nextInput;
    private RubralSourceFrame? _pending;
    private RubralSourceReceipt? _lastReceipt;
    private bool _faulted, _disposed;
    private string? _fault;
    internal IReadOnlyList<RubralOutputRoute> Routes { get; }
    internal string? InputTargetFingerprint => _declaredTargets?.Fingerprint;

    internal IsolatedRubralSource(Guid session, string instance, string stateDirectory, double durationMs,
        IReadOnlyList<SpikeMessage> fixtureInput, IsolatedSynapticTargets? declaredTargets = null)
    {
        ArgumentNullException.ThrowIfNull(fixtureInput);
        if (session == Guid.Empty || string.IsNullOrWhiteSpace(instance) || instance != instance.Trim() ||
            !instance.StartsWith("isolated-", StringComparison.Ordinal) ||
            instance.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !Path.IsPathFullyQualified(stateDirectory) ||
            !double.IsFinite(durationMs) || durationMs <= 0 || durationMs > 6000 ||
            Math.Round(durationMs / SampleMs) * SampleMs != durationMs)
            throw new ArgumentException("Declare a fresh isolated source, absolute state directory and bounded sample horizon.");
        var inputs = JsonSerializer.Deserialize<SpikeMessage[]>(JsonSerializer.SerializeToUtf8Bytes(fixtureInput))!;
        if (inputs.Any(e => e is null || !SpikeProtocol.validate_spike(e, out _) ||
                e.TargetStructure != StructureId.RedNucleus || e.TimestampMs < 0 || e.TimestampMs >= durationMs) ||
            inputs.Select(e => e.MessageId).Distinct().Count() != inputs.Length ||
            inputs.Zip(inputs.Skip(1)).Any(pair => pair.First.TimestampMs > pair.Second.TimestampMs))
            throw new ArgumentException("Require unique, ordered, valid original diagnostic inputs to this source.");
        if (declaredTargets is not null)
        {
            declaredTargets.ValidateFor(256);
            foreach (var input in inputs)
                if (!declaredTargets.TryResolve(input, out int target) || input.TargetNeuronId != $"n-{target:000}")
                    throw new ArgumentException("Every fixture contact must name its exact declared receiving cell; no routing fallback.");
        }
        _declaredTargets = declaredTargets;
        _originalInputs = inputs.ToDictionary(e => e.MessageId);
        // This process-global configuration is changed only for construction and
        // restored immediately. The visible rig is serial; tests use its existing
        // process-environment collection. State paths are captured by the store.
        lock (ConstructionGate)
        {
            if (File.Exists(stateDirectory) || Directory.Exists(stateDirectory) && Directory.EnumerateFileSystemEntries(stateDirectory).Any())
                throw new InvalidOperationException("Preserve previous saves; this source requires an empty state directory.");
            string? priorDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
            string? priorInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
            try
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", stateDirectory);
                Environment.SetEnvironmentVariable("SERVICE_INSTANCE", instance);
                _engine = new(new StructureProfile(StructureId.RedNucleus, "Izhikevich", "STDP",
                    "Isolated execution of the existing rubral neuronal source; physiology not qualified.", new DelayWindow(3, 9)),
                    null, diagnosticSynapticTargets: declaredTargets);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", priorDirectory);
                Environment.SetEnvironmentVariable("SERVICE_INSTANCE", priorInstance);
            }
        }
        _session = session; _duration = durationMs; _inputs = inputs;
        try
        {
            Routes = _engine.DescribeRubralPrimaryOutputs();
            _routes = Routes.ToDictionary(r => r.SourceNeuron, StringComparer.Ordinal);
            if (declaredTargets is not null) _engine.RubralInputObserver = delivery => _deliveries.Add(delivery);
        }
        catch { _engine.Dispose(); throw; }
    }

    internal RubralSourceStatus Capture()
    {
        RequireOwner(); var observation = _engine.ObserveRubralPopulation();
        return new(_samples, _samples == 0 ? null : _samples * SampleMs, _nextInput, _produced,
            _pending is not null, _faulted, _fault, observation.TickTimestampMs, observation.CompletedTick,
            _faulted ? _engine.ObserveRubralUndrainedOutput() : null);
    }

    internal RubralSourceFrame? Pending() { RequireOwner(); return _pending; }
    // Includes the failed step's delivered prefix, even if it never published a
    // valid frame. Acknowledgement clears only successfully published receipts.
    internal IReadOnlyList<RubralInputDelivery> PendingInputDeliveries()
    { RequireOwner(); return Array.AsReadOnly(_deliveries.ToArray()); }

    internal RubralSourceFrame AdvanceOneSample()
    {
        RequireOwner();
        if (_faulted || _pending is not null)
            throw new InvalidOperationException("Retain the pending source frame; do not advance under backpressure or after a fault.");
        long next = checked(_samples + 1); double through = next * SampleMs;
        if (through > _duration) throw new InvalidOperationException("Declared source horizon is complete.");
        try
        {
            // Admission occurs before the engine tick; its own conduction queue
            // still determines the actual neuronal delivery boundary.
            int end = _nextInput;
            while (end < _inputs.Length && _inputs[end].TimestampMs <= through) ++end;
            if (end > _nextInput)
                _engine.EnqueueSpikeBatchAsync(_inputs[_nextInput..end]).GetAwaiter().GetResult();
            _nextInput = end;
            var tick = new TickSignal(next, through, SampleMs, new NeuromodState(),
                new Dictionary<BrainRhythm, double>(), 0, NeuralSubstrates: NeuralSubstrateSupply.ReferenceBath);
            var result = _engine.ProcessStepAsync(tick, 0).GetAwaiter().GetResult();
            var observation = _engine.ObserveRubralPopulation();
            // Retain the drained original output before validating it. A fault
            // must not erase evidence or permit a later acknowledgement/resume.
            var payload = new RubralSourcePayload(_session, next, through, TimingConvention,
                result.Ack.SpikeCount, _nextInput, result.OutboundSpikes, observation, result.Ack.SynapticLearningDiagnostics?.Energy,
                _declaredTargets is null ? null : Array.AsReadOnly(_deliveries.ToArray()));
            string json = JsonSerializer.Serialize(payload);
            _pending = new(_session, next, through, json, Hash(json));
            if (observation.TickTimestampMs != through || observation.CompletedTick != next ||
                result.Ack.Tick != next || result.Ack.SpikeCount != result.OutboundSpikes.Count)
                throw new InvalidOperationException("Source tick, firing and completed observation do not agree.");
            foreach (var delivered in _deliveries)
            {
                if (!_originalInputs.TryGetValue(delivered.OriginalMessageId, out var original) ||
                    _declaredTargets is null || !_declaredTargets.TryResolve(original, out int target) ||
                    delivered.ActualTargetIndex != target || delivered.ActualTarget != $"n-{target:000}" ||
                    delivered.NamedTarget != original.TargetNeuronId || delivered.ReceivingContactId == Guid.Empty ||
                    delivered.OriginalJson != JsonSerializer.Serialize(original) ||
                    delivered.AppliedAtMs != delivered.ArrivalAtMs || delivered.ArrivalAtMs < original.TimestampMs ||
                    delivered.AppliedAtMs > through || !_deliveredIds.Add(delivered.OriginalMessageId))
                    throw new InvalidOperationException("Actual fixture delivery differs from declared synaptic anatomy or original event.");
            }
            foreach (var e in result.OutboundSpikes)
            {
                if (!_routes.TryGetValue(e.SourceNeuronId, out var route) || e.SourceInstanceKey != route.SourceInstance ||
                    e.SourceStructure != StructureId.RedNucleus || e.TargetStructure != route.TargetStructure ||
                    e.TargetNeuronId != route.TargetNeuron || e.ProjectionId != route.Projection || e.SynapseId != route.Synapse ||
                    e.Neurotransmitter != route.Transmitter || e.IsFeedback || e.IsSyntheticBackground ||
                    e.MessageId == Guid.Empty || !double.IsFinite(e.TimestampMs) || e.TimestampMs <= _samples * SampleMs || e.TimestampMs > through)
                    throw new InvalidOperationException("Observed source event does not match its declared route/completed interval.");
            }
            _samples = next; _produced = checked(_produced + result.OutboundSpikes.Count);
            return _pending;
        }
        catch (Exception error) { _faulted = true; _fault = error.ToString(); throw; }
    }

    internal RubralSourceReceipt Acknowledge(Guid session, long sequence, string sha256)
    {
        RequireOwner();
        if (_faulted) throw new InvalidOperationException("Preserve the faulted source and its original output; do not acknowledge or resume it.");
        var receipt = new RubralSourceReceipt(session, sequence, sha256);
        if (_lastReceipt == receipt) return receipt;
        if (_pending is null || _pending.Session != session || _pending.Sequence != sequence || _pending.Sha256 != sha256)
            throw new ArgumentException("Acknowledgement must name the retained original frame.");
        _lastReceipt = receipt; _pending = null; _deliveries.Clear(); return receipt;
    }

    internal static RubralSourcePayload Decode(RubralSourceFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (Hash(frame.CanonicalJson) != frame.Sha256) throw new ArgumentException("Original source bytes changed.");
        var payload = JsonSerializer.Deserialize<RubralSourcePayload>(frame.CanonicalJson)
            ?? throw new ArgumentException("Missing source payload.");
        if (payload.Session != frame.Session || payload.Sequence != frame.Sequence || payload.CompleteThroughMs != frame.CompleteThroughMs ||
            payload.TimingConvention != TimingConvention || payload.OriginalEvents.Count != payload.FiredCount)
            throw new ArgumentException("Frame envelope does not identify its original payload.");
        return payload;
    }

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private void RequireOwner()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(IsolatedRubralSource));
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("One thread owns this isolated neuronal source.");
    }
    public void Dispose()
    {
        if (_disposed) return;
        RequireOwner(); _engine.Dispose(); _disposed = true;
    }
}
