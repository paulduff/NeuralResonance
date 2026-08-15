using NeuralResonanceEngine.Protocol;

internal static class SleepConsolidationTopology
{
	public const int StateChannelCount = 3;
	public const int WakeChannel = 0;
	public const int NremChannel = 1;
	public const int RemChannel = 2;
	public const int ReplayEnsembleCount = PerceptEnsembleTopology.EnsembleCount;

	public static bool EmitsDiagnostics(StructureId structure)
		=> IsStateStructure(structure) || IsReplayStructure(structure);

	public static bool IsStateStructure(StructureId structure)
		=> structure is StructureId.DorsomedialHypothalamicNucleus
			or StructureId.VentrolateralPreopticNucleus
			or StructureId.SuprachiasmaticNucleus
			or StructureId.LateralHypothalamicArea
			or StructureId.ReticularFormation
			or StructureId.LocusCoeruleus
			or StructureId.RapheNuclei
			or StructureId.NucleusBasalis
			or StructureId.PedunculopontineNucleus
			or StructureId.LaterodorsalTegmentalNucleus
			or StructureId.IntralaminarThalamus
			or StructureId.Trn;

	public static bool IsReplayStructure(StructureId structure)
		=> SynapticMemoryTopology.IsMemoryCircuitStructure(structure)
			|| structure is StructureId.IntralaminarThalamus or StructureId.Trn;

	public static bool IsWakeStructure(StructureId structure)
		=> structure is StructureId.ReticularFormation
			or StructureId.SuprachiasmaticNucleus
			or StructureId.LateralHypothalamicArea
			or StructureId.DorsomedialHypothalamicNucleus
			or StructureId.LocusCoeruleus
			or StructureId.NucleusBasalis
			or StructureId.PedunculopontineNucleus
			or StructureId.LaterodorsalTegmentalNucleus
			or StructureId.IntralaminarThalamus;

	public static bool IsNremStructure(StructureId structure)
		=> structure is StructureId.DorsomedialHypothalamicNucleus
			or StructureId.VentrolateralPreopticNucleus
			or StructureId.ReticularFormation
			or StructureId.RapheNuclei
			or StructureId.IntralaminarThalamus
			or StructureId.Trn;

	public static bool IsRemStructure(StructureId structure)
		=> structure is StructureId.PedunculopontineNucleus
			or StructureId.LaterodorsalTegmentalNucleus;

	public static bool IsSpindleStructure(StructureId structure)
		=> structure is StructureId.IntralaminarThalamus or StructureId.Trn;

	public static int StateChannelForNeuron(int neuronIndex, StructureId structure)
	{
		if (structure == StructureId.DorsomedialHypothalamicNucleus)
		{
			return Math.Max(0, neuronIndex) % StateChannelCount;
		}

		if (structure is StructureId.PedunculopontineNucleus or StructureId.LaterodorsalTegmentalNucleus)
		{
			return (Math.Max(0, neuronIndex) & 1) == 0 ? WakeChannel : RemChannel;
		}

		if (IsWakeStructure(structure))
		{
			return WakeChannel;
		}

		if (IsRemStructure(structure))
		{
			return RemChannel;
		}

		return NremChannel;
	}

	public static int ReplayEnsembleForNeuron(int neuronIndex)
		=> PerceptEnsembleTopology.EnsembleForNeuron(neuronIndex);

	public static void ResolveIntrinsicDrive(
		StructureId structure,
		int neuronIndex,
		float sleepDrive,
		float wakeReserve,
		out float excitatory,
		out float inhibitory)
	{
		sleepDrive = Math.Clamp(sleepDrive, 0f, 1f);
		wakeReserve = Math.Clamp(wakeReserve, 0f, 1f);
		excitatory = 0f;
		inhibitory = 0f;
		var stateChannel = StateChannelForNeuron(neuronIndex, structure);

		if (structure == StructureId.DorsomedialHypothalamicNucleus)
		{
			if (stateChannel == NremChannel)
			{
				excitatory = sleepDrive * 0.34f;
				inhibitory = wakeReserve * 0.10f;
			}
			else if (stateChannel == WakeChannel)
			{
				excitatory = wakeReserve * 0.22f;
				inhibitory = sleepDrive * 0.16f;
			}
			else
			{
				excitatory = sleepDrive * (1f - wakeReserve) * 0.14f;
				inhibitory = wakeReserve * 0.08f;
			}
			return;
		}

		if (structure == StructureId.VentrolateralPreopticNucleus)
		{
			excitatory = sleepDrive * 0.38f;
			inhibitory = wakeReserve * 0.20f;
			return;
		}

		if (structure is StructureId.PedunculopontineNucleus or StructureId.LaterodorsalTegmentalNucleus)
		{
			if (stateChannel == RemChannel)
			{
				excitatory = sleepDrive * (1f - (wakeReserve * 0.35f)) * 0.20f;
				inhibitory = wakeReserve * 0.08f;
			}
			else
			{
				excitatory = wakeReserve * 0.22f;
				inhibitory = sleepDrive * 0.15f;
			}
			return;
		}

		if (IsWakeStructure(structure))
		{
			excitatory = wakeReserve * 0.20f;
			inhibitory = sleepDrive * 0.18f;
			return;
		}

		if (IsRemStructure(structure))
		{
			excitatory = sleepDrive * (1f - (wakeReserve * 0.55f)) * 0.18f;
			inhibitory = wakeReserve * 0.08f;
			return;
		}

		if (IsNremStructure(structure))
		{
			excitatory = sleepDrive * 0.15f;
			inhibitory = wakeReserve * 0.06f;
			return;
		}

		if (IsReplayStructure(structure))
		{
			excitatory = sleepDrive * 0.055f;
			inhibitory = wakeReserve * 0.018f;
		}
	}
}
