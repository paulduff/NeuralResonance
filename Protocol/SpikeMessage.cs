using ProtoBuf;

namespace NeuralResonanceEngine.Protocol;

[ProtoContract]
public sealed class SpikeMessage
{
    [ProtoMember(1)] public Guid MessageId { get; set; }
    [ProtoMember(2)] public double TimestampMs { get; set; }
    [ProtoMember(3)] public StructureId SourceStructure { get; set; }
    [ProtoMember(4)] public StructureId TargetStructure { get; set; }
    [ProtoMember(5)] public string SourceNeuronId { get; set; } = string.Empty;
    [ProtoMember(6)] public string TargetNeuronId { get; set; } = string.Empty;
    [ProtoMember(7)] public Guid SynapseId { get; set; }
    [ProtoMember(8)] public NTEnum Neurotransmitter { get; set; }
    [ProtoMember(9)] public float VesicleQuanta { get; set; }
    [ProtoMember(10)] public float ReuptakeRate { get; set; }
    [ProtoMember(11)] public SpikeTypeEnum SpikeType { get; set; }
    [ProtoMember(12)] public bool IsFeedback { get; set; }
    // Nullable so protobuf deserialization does not allocate a default
    // NeuromodState that is immediately overwritten. Callers that read this
    // property must coalesce against null (treat as "no modulation").
    [ProtoMember(13)] public NeuromodState? ModulationContext { get; set; }
    // Axonal tract identity, not a motor command. This bit is set only after a
    // neuron fires from recent fall-sensitive synaptic input and is propagated
    // through declared righting-reflex projections.
    [ProtoMember(14)] public bool IsRightingCircuitSpike { get; set; }
}
