using NeuralResonanceEngine.Protocol;

internal static class IntrinsicCircuitDriveTopology
{
	public static void Resolve(
		StructureId structure,
		int neuronIndex,
		long tick,
		out float excitation,
		out float inhibition)
	{
		var phase = ((tick * 0.017) + (neuronIndex * 0.113)) % (Math.PI * 2.0);
		var oscillation = (float)((Math.Sin(phase) + 1.0) * 0.5);
		var sparseLane = ((tick / 18) + neuronIndex) % 11 == 0 ? 1f : 0f;
		excitation = structure switch
		{
			// These cell groups are physiological pacemakers or maintain substantial
			// tonic output even without an external sensory volley.
			StructureId.PurkinjeCellLayer => 0.80f + (oscillation * 0.18f),
			StructureId.GPi or StructureId.Snr => 0.70f + (oscillation * 0.15f),
			StructureId.GPe => 0.64f + (oscillation * 0.14f),
			StructureId.Snc or StructureId.Vta => 0.20f + (oscillation * 0.08f),
			StructureId.InferiorOlive => 0.72f + (oscillation * 0.24f),
			StructureId.CerebellarVermis => 0.78f + (oscillation * 0.18f),
			StructureId.CerebellarLobules => 0.52f + (oscillation * 0.16f),
			StructureId.CerebellarGranule => 0.28f + (sparseLane * 0.52f),
			StructureId.Striatum => 0.06f + (sparseLane * 1.90f),
			StructureId.Habenula or StructureId.VentralPallidum => 0.38f + (oscillation * 0.18f),
			StructureId.MedialSeptalNucleus or StructureId.DiagonalBandNucleus => 0.15f + (oscillation * 0.09f),
			StructureId.DorsomedialHypothalamicNucleus => 0.11f + (oscillation * 0.07f),
			StructureId.VentrolateralPreopticNucleus => 0.08f + ((1f - oscillation) * 0.08f),
			StructureId.SuperiorOlive => 0.07f + (sparseLane * 0.11f),
			_ => 0f
		};
		inhibition = structure switch
		{
			// Sparse striatal up states prevent all five action channels from rising
			// together and preserve competition in the direct/indirect pathways.
			StructureId.Striatum when sparseLane <= 0f => 0.035f,
			_ => 0f
		};
	}
}
