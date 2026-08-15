using NeuralResonanceEngine.Protocol;

internal static class RightingReflexTopology
{
	public const int StandChannel = ActionChannelTopology.StandChannel;
	internal const float SpinalRightingRelayGain = 3.25f;
	internal const int SpinalRightingInterneuronPoolSize = 4;

	public static bool TryProjectInbound(
		SpikeMessage message,
		int targetNeuronCount,
		out int targetNeuronIndex)
	{
		targetNeuronIndex = 0;
		if (targetNeuronCount <= 0)
		{
			return false;
		}

		if (IsPrimaryRightingAfferent(message))
		{
			targetNeuronIndex = ActionChannelTopology.ProjectToChannel(
				StandChannel,
				targetNeuronCount,
				message.TargetStructure,
				StablePopulationSeed(message.SourceNeuronId));
			return true;
		}

		if (!IsTaggedRightingRelay(message))
		{
			return false;
		}

		var sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		if (ActionChannelTopology.ChannelForNeuron(sourceIndex, message.SourceStructure) != StandChannel)
		{
			return false;
		}

		// Primary balance and descending reticulovestibular fibres converge on a
		// compact bilateral spinal interneuron pool. Relays retain lane identity,
		// while spinal convergence supplies the summation needed to recruit the
		// extensor motor pool without creating a host-side recovery command.
		if (message.TargetStructure == StructureId.SpinalCordMotor)
		{
			var sourceLocalIndex = Math.Max(0, sourceIndex) / ActionChannelTopology.ChannelCount;
			var poolSlot = sourceLocalIndex % SpinalRightingInterneuronPoolSize;
			targetNeuronIndex = ActionChannelTopology.ProjectToChannel(
				StandChannel,
				targetNeuronCount,
				StructureId.SpinalCordMotor,
				poolSlot);
			return true;
		}

		targetNeuronIndex = ActionChannelTopology.Project(
			sourceIndex,
			message.SourceStructure,
			targetNeuronCount,
			message.TargetStructure,
			71);
		return true;
	}

	internal static float ApplySpinalRelayEfficacy(SpikeMessage message, float effectiveQuanta)
	{
		if (!float.IsFinite(effectiveQuanta) ||
			!IsSpinalRightingRelay(message))
		{
			return effectiveQuanta;
		}

		// Large-diameter proprioceptive and descending reticulovestibular fibres
		// form strong excitatory synapses on a compact spinal interneuron pool. The
		// bounded gain changes synaptic efficacy only; the target neuron must still
		// emit a spike before the reflex output trace can exist.
		return Math.Clamp(effectiveQuanta * SpinalRightingRelayGain, 0.05f, 5f);
	}

	internal static bool IsSpinalRightingRelay(SpikeMessage message)
	{
		if (message.TargetStructure != StructureId.SpinalCordMotor ||
			!IsTaggedRightingRelay(message))
		{
			return false;
		}

		var sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		if (ActionChannelTopology.ChannelForNeuron(sourceIndex, message.SourceStructure) != StandChannel)
		{
			return false;
		}

		return true;
	}

	internal static bool IsEvokedRightingInput(SpikeMessage message)
		=> IsPrimaryRightingAfferent(message) || IsTaggedRightingRelay(message);

	internal static bool ShouldTagOutboundSpike(
		StructureId structure,
		int sourceNeuronIndex,
		float inputTrace)
		=> ParticipatesInRightingCircuit(structure) &&
			inputTrace > 0.01f &&
			ActionChannelTopology.ChannelForNeuron(sourceNeuronIndex, structure) == StandChannel;

	internal static bool ParticipatesInRightingCircuit(StructureId structure)
		=> structure is StructureId.ProprioceptiveAfferents
			or StructureId.VestibularAfferents
			or StructureId.VestibularNuclei
			or StructureId.CerebellarVermis
			or StructureId.FastigialNucleus
			or StructureId.ReticularFormation
			or StructureId.SpinalCordMotor;

	internal static bool EmitsRightingReflexDiagnostic(StructureId structure)
		=> structure is StructureId.ProprioceptiveAfferents
			or StructureId.VestibularAfferents
			or StructureId.SpinalCordMotor;

	internal static bool IsPrimaryRightingAfferent(SpikeMessage message)
	{
		if (message.SourceStructure != message.TargetStructure ||
			message.Neurotransmitter != NTEnum.GLUTAMATE ||
			message.VesicleQuanta < 0.75f)
		{
			return false;
		}

		return message.SourceStructure switch
		{
			StructureId.VestibularAfferents => IsVestibularFallPopulation(message.SourceNeuronId),
			StructureId.ProprioceptiveAfferents => IsSupportLossPopulation(message.SourceNeuronId),
			_ => false
		};
	}

	private static bool IsVestibularFallPopulation(string neuronId)
		=> ContainsPopulation(neuronId, "otolith_pitch_forward") ||
		   ContainsPopulation(neuronId, "otolith_pitch_backward") ||
		   ContainsPopulation(neuronId, "otolith_roll_left") ||
		   ContainsPopulation(neuronId, "otolith_roll_right") ||
		   ContainsPopulation(neuronId, "dynamic_balance_margin_loss");

	private static bool IsSupportLossPopulation(string neuronId)
		=> ContainsPopulation(neuronId, "center_of_mass_left_of_pressure") ||
		   ContainsPopulation(neuronId, "center_of_mass_right_of_pressure") ||
		   ContainsPopulation(neuronId, "center_of_mass_behind_pressure") ||
		   ContainsPopulation(neuronId, "center_of_mass_ahead_of_pressure") ||
		   ContainsPopulation(neuronId, "support_margin_loss") ||
		   ContainsPopulation(neuronId, "support_area_narrowing");

	private static bool IsRightingRelay(StructureId source, StructureId target)
		=> (source, target) is
			(StructureId.VestibularAfferents, StructureId.VestibularNuclei) or
			(StructureId.ProprioceptiveAfferents, StructureId.SpinalCordMotor) or
			(StructureId.VestibularNuclei, StructureId.CerebellarVermis) or
			(StructureId.VestibularNuclei, StructureId.FastigialNucleus) or
			(StructureId.VestibularNuclei, StructureId.ReticularFormation) or
			(StructureId.VestibularNuclei, StructureId.SpinalCordMotor) or
			(StructureId.CerebellarVermis, StructureId.FastigialNucleus) or
			(StructureId.FastigialNucleus, StructureId.VestibularNuclei) or
			(StructureId.FastigialNucleus, StructureId.ReticularFormation) or
			(StructureId.ReticularFormation, StructureId.SpinalCordMotor);

	private static bool IsTaggedRightingRelay(SpikeMessage message)
		=> message.IsRightingCircuitSpike &&
			IsRightingRelay(message.SourceStructure, message.TargetStructure);

	private static bool ContainsPopulation(string neuronId, string population)
		=> !string.IsNullOrWhiteSpace(neuronId) &&
		   neuronId.Contains(population, StringComparison.OrdinalIgnoreCase);

	private static int StablePopulationSeed(string value)
	{
		unchecked
		{
			var hash = 2166136261u;
			foreach (var character in value ?? string.Empty)
			{
				hash ^= character;
				hash *= 16777619u;
			}
			return (int)(hash & 0x7fffffff);
		}
	}
}
