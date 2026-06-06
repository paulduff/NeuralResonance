using System;
using NeuralResonanceEngine.Protocol;

internal sealed class SynapseState(Guid synapseId, NTEnum neurotransmitter, float vesicleQuanta)
{
	public Guid SynapseId { get; } = synapseId;

	public NTEnum Neurotransmitter { get; } = neurotransmitter;

	public float VesicleQuanta { get; set; } = vesicleQuanta;

	public float PreTrace { get; set; }

	public float PostTrace { get; set; }

	public float ThetaM { get; set; } = 0.2f;

	public float EligibilityTrace { get; set; }

	public float SynapticTagTrace { get; set; }

	public double LastUpdateTimestampMs { get; set; }

	public int LastTargetNeuronIndex { get; set; } = -1;
}
