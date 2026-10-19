using NeuralResonanceEngine.Protocol;

// Opt-in, read-only values from actual integration, before plasticity updates.
// Used by isolated probes; no observer is installed by structure service hosts.
internal readonly record struct InboundSynapticObservation(
    double ScheduledDeliveryTimestampMs,
    double ProcessingTimestampMs,
    StructureId Source,
    int TargetNeuronIndex,
    NTEnum Neurotransmitter,
    float PresynapticQuanta,
    float PostsynapticQuanta,
    float EffectiveQuanta,
    bool IsRightingCircuitSpike);
