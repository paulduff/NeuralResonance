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

	public void Stabilize()
	{
		VesicleQuanta = PlasticityRules.ClampQuanta(VesicleQuanta);
		PreTrace = FiniteClamp(PreTrace, 0f, 8f, 0f);
		PostTrace = FiniteClamp(PostTrace, 0f, 8f, 0f);
		ThetaM = FiniteClamp(ThetaM, 0.001f, 10f, 0.2f);
		EligibilityTrace = FiniteClamp(EligibilityTrace, -1f, 1f, 0f);
		SynapticTagTrace = FiniteClamp(SynapticTagTrace, -1f, 1f, 0f);
		LastUpdateTimestampMs = double.IsFinite(LastUpdateTimestampMs)
			? Math.Max(0.0, LastUpdateTimestampMs)
			: 0.0;
	}

	private static float FiniteClamp(float value, float minimum, float maximum, float fallback)
		=> float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
