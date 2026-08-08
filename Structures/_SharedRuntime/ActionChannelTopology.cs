using NeuralResonanceEngine.Protocol;

internal static class ActionChannelTopology
{
	// Four locomotor lanes plus one general physical manipulator lane. The fifth
	// lane remains an unlabeled neuronal population throughout the brain; only
	// the body boundary maps it onto an effector.
	public const int ChannelCount = 5;

	public static bool IsActionCircuitStructure(StructureId structure)
		=> structure is StructureId.Pfc
			or StructureId.Acc
			or StructureId.PremotorCortex
			or StructureId.Sma
			or StructureId.Striatum
			or StructureId.GPe
			or StructureId.GlobusPallidus
			or StructureId.GPi
			or StructureId.Stn
			or StructureId.Snr
			or StructureId.MotorThalamus;

	public static bool IsProposalStructure(StructureId structure)
		=> structure is StructureId.Pfc
			or StructureId.Acc
			or StructureId.PremotorCortex
			or StructureId.Sma;

	public static bool IsStriatal(StructureId structure)
		=> structure is StructureId.Striatum or StructureId.NucleusAccumbens;

	public static bool IsDirectPathwayNeuron(int neuronIndex)
		=> (Math.Max(0, neuronIndex) & 1) == 0;

	public static int ChannelForNeuron(int neuronIndex, StructureId structure)
	{
		var index = Math.Max(0, neuronIndex);
		return IsStriatal(structure)
			? (index / 2) % ChannelCount
			: index % ChannelCount;
	}

	public static int Project(
		int sourceIndex,
		StructureId sourceStructure,
		int targetCount,
		StructureId targetStructure,
		int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}

		var channel = ChannelForNeuron(sourceIndex, sourceStructure);
		var sourceStride = IsStriatal(sourceStructure) ? ChannelCount * 2 : ChannelCount;
		var sourceLocal = Math.Max(0, sourceIndex) / sourceStride;
		if (IsStriatal(targetStructure))
		{
			var cellsPerLane = Math.Max(1, targetCount / (ChannelCount * 2));
			var local = PositiveMod(sourceLocal + salt, cellsPerLane);
			var receptorClass = IsStriatal(sourceStructure)
				? Math.Max(0, sourceIndex) & 1
				: PositiveMod(sourceLocal + salt, 2);
			var projected = ((local * ChannelCount + channel) * 2) + receptorClass;
			return FindBoundedLaneIndex(projected, targetCount, targetStructure, channel);
		}

		var targetCellsPerLane = Math.Max(1, targetCount / ChannelCount);
		var targetLocal = PositiveMod(sourceLocal + salt, targetCellsPerLane);
		var targetIndex = targetLocal * ChannelCount + channel;
		return FindBoundedLaneIndex(targetIndex, targetCount, targetStructure, channel);
	}

	private static int FindBoundedLaneIndex(int candidate, int targetCount, StructureId targetStructure, int channel)
	{
		if (candidate >= 0 && candidate < targetCount && ChannelForNeuron(candidate, targetStructure) == channel)
		{
			return candidate;
		}

		for (var i = channel; i < targetCount; i++)
		{
			if (ChannelForNeuron(i, targetStructure) == channel)
			{
				return i;
			}
		}

		return Math.Clamp(candidate, 0, targetCount - 1);
	}

	private static int PositiveMod(int value, int modulo)
		=> (int)((uint)value % (uint)Math.Max(1, modulo));
}
