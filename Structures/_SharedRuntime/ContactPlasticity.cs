using NeuralResonanceEngine.Protocol;

// Reduced event-based rules. The timing constants are explicit model parameters,
// not a claim of calibration for every nucleus. See the foundation repair report.
internal static class ContactPlasticity
{
    internal const float PreTauMs = 20f;
    internal const float PostTauMs = 35f;
    internal const float EligibilityTauMs = 900f;
    internal const float TagTauMs = 8000f;

    internal static bool IsLearningContact(SynapseState synapse) =>
        !synapse.IsSyntheticBackground && synapse.Neurotransmitter is NTEnum.GLUTAMATE or NTEnum.GABA;

    internal static void DecayTo(SynapseState synapse, double timestampMs)
    {
        if (!double.IsFinite(timestampMs) || timestampMs < synapse.LastUpdateTimestampMs - 0.000001)
            throw new InvalidOperationException("Synaptic biological time cannot run backwards.");
        var elapsed = Math.Max(0, timestampMs - synapse.LastUpdateTimestampMs);
        synapse.PreTrace = PlasticityRules.DecayTrace(synapse.PreTrace, elapsed, PreTauMs);
        synapse.PostTrace = PlasticityRules.DecayTrace(synapse.PostTrace, elapsed, PostTauMs);
        synapse.EligibilityTrace = PlasticityRules.DecayTrace(synapse.EligibilityTrace, elapsed, EligibilityTauMs);
        synapse.SynapticTagTrace = PlasticityRules.DecayTrace(synapse.SynapticTagTrace, elapsed, TagTauMs);
        synapse.LastUpdateTimestampMs = timestampMs;
    }

    internal static void Spike(SynapseState synapse, string rule, bool presynaptic, double timestampMs, float activity)
    {
        DecayTo(synapse, timestampMs);
        if (!IsLearningContact(synapse)) return;
        // Read the opposite trace before adding the current spike impulse.
        var pair = presynaptic ? -0.014f * synapse.PostTrace : 0.018f * synapse.PreTrace;
        if (synapse.Neurotransmitter == NTEnum.GABA)
        {
            // Symmetric inhibitory homeostasis, with a target of 0.4 spikes in
            // the post trace. This has its own rule instead of excitatory STDP.
            pair = 0.012f * (presynaptic ? synapse.PostTrace - 0.4f : synapse.PreTrace);
        }
        if (presynaptic) synapse.PreTrace = Math.Min(8f, synapse.PreTrace + 1f);
        else synapse.PostTrace = Math.Min(8f, synapse.PostTrace + 1f);
        synapse.EligibilityTrace = Math.Clamp(synapse.EligibilityTrace + pair, -1f, 1f);
        synapse.SynapticTagTrace = Math.Clamp(synapse.SynapticTagTrace + pair, -1f, 1f);
        synapse.UpdateCount = Math.Min(int.MaxValue - 1, synapse.UpdateCount) + 1;

        float delta;
        if (synapse.Neurotransmitter == NTEnum.GABA)
            delta = PlasticityRules.LocalTraceDelta(pair, synapse.VesicleQuanta);
        else
            delta = rule switch
            {
                "STDP" or "STDP+SynapticTaggingCapture" => PlasticityRules.LocalTraceDelta(pair, synapse.VesicleQuanta),
                "BCM" or "BCM+STDP" => (presynaptic ? 0f : synapse.PreTrace * PlasticityRules.BcmWithSlidingThreshold(activity, synapse.ThetaM))
                    + (rule == "BCM+STDP" ? PlasticityRules.LocalTraceDelta(pair, synapse.VesicleQuanta) : 0f),
                "MossyFiberLTP" => PlasticityRules.LocalTraceDelta(Math.Max(0f, pair), synapse.VesicleQuanta),
                "DopamineModulatedSTDP" or "DopamineModulatedSTDP+SynapticTaggingCapture" or
                "SynapticTaggingCapture" or "CerebellarLTD" or "HomeostaticGain" or "DopamineHomeostasis" => 0f,
                _ => throw new InvalidOperationException("Undefined contact learning rule: " + rule)
            };
        Apply(synapse, delta, timestampMs);
    }

    internal static void Modulate(SynapseState synapse, string rule, double timestampMs, double elapsedMs,
        NeuromodState modulation, float teaching, float activity)
    {
        // A contact created or updated at this instant has no earlier interval
        // over which a newly arriving modulator could already have acted.
        var integrationMs = Math.Min(Math.Max(0, elapsedMs), Math.Max(0, timestampMs - synapse.LastUpdateTimestampMs));
        DecayTo(synapse, timestampMs);
        if (!IsLearningContact(synapse)) return;
        synapse.ThetaM = PlasticityRules.UpdateBcmTheta(synapse.ThetaM, activity, integrationMs);
        if (synapse.Neurotransmitter != NTEnum.GLUTAMATE) return;
        float rate = 0;
        if (rule.Contains("Dopamine", StringComparison.Ordinal) && modulation.DopamineLevel > 0.001f)
            rate += PlasticityRules.DopamineThreeFactor(synapse.EligibilityTrace, modulation.DopamineLevel, teaching);
        if (rule.Contains("SynapticTag", StringComparison.Ordinal) && modulation.AcetylcholineLevel > 0.001f)
            rate += PlasticityRules.SynapticTagCapture(synapse.SynapticTagTrace, synapse.VesicleQuanta, modulation.AcetylcholineLevel, modulation.DopamineLevel);
        if (rule == "HomeostaticGain")
            rate += 0.002f * (0.2f - activity) * synapse.VesicleQuanta;
        // Continuous consolidation is per biological second, never per API call.
        var delta = rate * (float)(integrationMs / 1000.0);
        if (delta != 0f) synapse.UpdateCount = Math.Min(int.MaxValue - 1, synapse.UpdateCount) + 1;
        Apply(synapse, delta, timestampMs);
    }

    internal static void ClimbingInput(SynapseState synapse, double timestampMs)
    {
        DecayTo(synapse, timestampMs);
        // The teaching input changes coactive granule/parallel-fibre contacts,
        // never its own climbing-fibre weight or an unrelated dormant contact.
        if (!IsLearningContact(synapse) || synapse.Neurotransmitter != NTEnum.GLUTAMATE ||
            synapse.SourceStructure != StructureId.CerebellarGranule || synapse.PreTrace < 0.1f) return;
        var delta = PlasticityRules.CerebellarLtdCoincidence(synapse.VesicleQuanta, true, Math.Min(1, synapse.PreTrace));
        synapse.UpdateCount = Math.Min(int.MaxValue - 1, synapse.UpdateCount) + 1;
        Apply(synapse, delta, timestampMs);
    }

    private static void Apply(SynapseState synapse, float delta, double timestampMs)
    {
        delta = PlasticityRules.ApplyCadenceInvariantBudget(synapse, delta, timestampMs);
        synapse.VesicleQuanta = PlasticityRules.ClampQuanta(synapse.VesicleQuanta + delta);
        synapse.Stabilize();
    }
}
