using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

public sealed partial class StructureEngine
{
    private readonly string _instanceKey;
    private readonly List<SynapseState>[] _incomingByTarget;
    private readonly float[] _cellPostTrace;
    private readonly double[] _cellPostTimestamp;
    private readonly Dictionary<(Guid projection, string instance, StructureId source, string cell, int target, NTEnum nt, bool feedback, bool synthetic), SynapseState> _contactLookup = new();
    private bool _warmStartPending = true;
    private double _lastBiologicalTimestampMs = double.NaN;
    private long _presynapticLearningEvents;
    private long _postsynapticLearningEvents;

    private SynapticContactSample[] _contactSamples = [];
    private double _contactSampleTimestampMs = -1;

    private SynapticLearningDiagnostics BuildSynapticLearningDiagnostics(double timestampMs)
    {
        // Contact counts remain current, but discovering new contacts must not
        // trigger a scan of the whole graph on every tick during initial exposure.
        if (_contactSampleTimestampMs < 0 || timestampMs - _contactSampleTimestampMs >= 250 ||
            (_contactSamples.Length == 0 && _inboundSynapses.Count > 0))
        {
            _contactSamples = _inboundSynapses.Values.OrderByDescending(contact => contact.TotalAbsolutePlasticityChange)
            .ThenBy(contact => contact.SynapseId).Take(8).Select(contact => new SynapticContactSample(
                contact.SynapseId, contact.ProjectionId, contact.SourceInstanceKey, contact.SourceNeuronId,
                _instanceKey, contact.LastTargetNeuronIndex, contact.VesicleQuanta, contact.EligibilityTrace,
                contact.UpdateCount, contact.IsSyntheticBackground)).ToArray();
            _contactSampleTimestampMs = timestampMs;
        }
        return new(StableNeuralIdentity.TopologyVersion, "Learned weights; warm initialization",
            "Reduced model; physiological calibration pending", _neurons.Length, _inboundSynapses.Count,
            _presynapticLearningEvents, _postsynapticLearningEvents, _contactSamples, _contactSampleTimestampMs);
    }

    internal Guid ResolveContactId(SpikeMessage message, int targetCell = -1) =>
        StableNeuralIdentity.Contact(message, _instanceKey, targetCell < 0 ? ResolveInboundNeuronIndex(message) : targetCell);

    private SynapseState GetOrCreateInboundSynapse(SpikeMessage message, int targetCell, double timestampMs)
    {
        var projection = message.ProjectionId == Guid.Empty ? message.SynapseId : message.ProjectionId;
        var key = (projection, message.SourceInstanceKey, message.SourceStructure, message.SourceNeuronId,
            targetCell, message.Neurotransmitter, message.IsFeedback, message.IsSyntheticBackground);
        if (_contactLookup.TryGetValue(key, out var cached)) return cached;
        var contactId = ResolveContactId(message, targetCell);
        if (!_inboundSynapses.TryGetValue(contactId, out var contact))
        {
            if (_inboundSynapses.Count >= _synapseStore.MaxInboundSynapseCount)
                throw new InvalidOperationException("Contact capacity exceeded; refusing to silently prune learned neural connections.");
            contact = new SynapseState(contactId, message.Neurotransmitter, 1f, 1f)
            {
                ProjectionId = projection, SourceInstanceKey = message.SourceInstanceKey,
                SourceStructure = message.SourceStructure, SourceNeuronId = message.SourceNeuronId,
                IsFeedback = message.IsFeedback, IsSyntheticBackground = message.IsSyntheticBackground,
                LastTargetNeuronIndex = targetCell, LastUpdateTimestampMs = timestampMs,
                PostTrace = PlasticityRules.DecayTrace(_cellPostTrace[targetCell],
                    timestampMs - _cellPostTimestamp[targetCell], ContactPlasticity.PostTauMs)
            };
            _inboundSynapses.Add(contactId, contact);
            _incomingByTarget[targetCell].Add(contact);
        }
        _contactLookup.Add(key, contact);
        return contact;
    }

    private void InitializeLearningClock(double intervalStartMs)
    {
        if (!_warmStartPending) return;
        foreach (var state in _inboundSynapses.Values.Concat(_outboundSynapses.Values))
        {
            state.LastUpdateTimestampMs = intervalStartMs;
            state.LastPlasticityBudgetTimestampMs = intervalStartMs;
        }
        Array.Fill(_cellPostTimestamp, intervalStartMs);
        _warmStartPending = false;
    }

    private void LearnPresynaptic(SpikeMessage message, SynapseState contact, ModelNeuron cell, double timestampMs)
    {
        var previous = ComputeSynapticMemoryContribution(contact);
        var supported = contact.UpdateCount > 0;
        ContactPlasticity.Spike(contact, _profile.PlasticityRule, true, timestampMs, cell.ActivityTrace);
        UpdateContactDiagnostics(contact, previous, supported);
        if (ContactPlasticity.IsLearningContact(contact)) _presynapticLearningEvents++;
        if (_profile.PlasticityRule == "CerebellarLTD" && message.SourceStructure == StructureId.InferiorOlive &&
            message.Neurotransmitter == NTEnum.GLUTAMATE && !message.IsSyntheticBackground)
        {
            foreach (var parallel in _incomingByTarget[cell.Index])
                ContactPlasticity.ClimbingInput(parallel, timestampMs);
        }
        PersistSynapses(timestampMs);
    }

    private void LearnPostsynaptic(ModelNeuron cell, double timestampMs)
    {
        _cellPostTrace[cell.Index] = Math.Min(8f, PlasticityRules.DecayTrace(_cellPostTrace[cell.Index],
            timestampMs - _cellPostTimestamp[cell.Index], ContactPlasticity.PostTauMs) + 1f);
        _cellPostTimestamp[cell.Index] = timestampMs;
        foreach (var contact in _incomingByTarget[cell.Index])
        {
            var previous = ComputeSynapticMemoryContribution(contact);
            var supported = contact.UpdateCount > 0;
            ContactPlasticity.Spike(contact, _profile.PlasticityRule, false, timestampMs, cell.ActivityTrace);
            UpdateContactDiagnostics(contact, previous, supported);
            if (ContactPlasticity.IsLearningContact(contact)) _postsynapticLearningEvents++;
        }
        if (_incomingByTarget[cell.Index].Count > 0) PersistSynapses(timestampMs);
    }

    private void AdvanceContactModulation(ModelNeuron cell, double timestampMs, double elapsedMs)
    {
        var modulation = cell.LocalNeuromodState;
        if (modulation.DopamineLevel <= 0.001f && modulation.AcetylcholineLevel <= 0.001f &&
            !_profile.PlasticityRule.Contains("BCM", StringComparison.Ordinal) && _profile.PlasticityRule != "HomeostaticGain") return;
        foreach (var contact in _incomingByTarget[cell.Index])
        {
            var previous = ComputeSynapticMemoryContribution(contact);
            var supported = contact.UpdateCount > 0;
            ContactPlasticity.Modulate(contact, _profile.PlasticityRule, timestampMs, elapsedMs,
                modulation, cell.LocalRewardSignal, cell.ActivityTrace);
            UpdateContactDiagnostics(contact, previous, supported);
        }
        if (_incomingByTarget[cell.Index].Count > 0) PersistSynapses(timestampMs);
    }

    private void UpdateContactDiagnostics(SynapseState contact, SynapticMemoryContribution previous, bool supported)
    {
        var target = contact.LastTargetNeuronIndex;
        if (SynapticMemoryTopology.IsMemoryCircuitStructure(_profile.StructureId) && contact.Neurotransmitter == NTEnum.GLUTAMATE)
            UpdateSynapticMemoryAggregate(target, previous, supported, target, ComputeSynapticMemoryContribution(contact));
        if (_profile.StructureId == StructureId.Striatum && contact.Neurotransmitter == NTEnum.GLUTAMATE &&
            ActionChannelTopology.IsProposalStructure(contact.SourceStructure))
        {
            var channel = ActionChannelTopology.ChannelForNeuron(target, _profile.StructureId);
            _actionChannelEligibility[channel] = contact.EligibilityTrace;
            _actionChannelSynapticStrength[channel] = contact.VesicleQuanta;
            _actionChannelLearningObserved[channel] = true;
        }
    }
}
