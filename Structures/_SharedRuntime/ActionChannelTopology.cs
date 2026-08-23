using NeuralResonanceEngine.Protocol;

internal static class ActionChannelTopology
{
	public const string PersistenceNamespace = "synapses-action38-hand-v1";

	// Four locomotor lanes, twelve opposing upper-limb lanes, four posture lanes,
	// four opposing lateral hip lanes, eight opposing two-axis ankle lanes, and
	// two opposing axial-rotation lanes, and four antagonistic hand open/close
	// lanes. The
	// populations remain unlabeled inside the brain; anatomical meaning is
	// assigned only where descending populations meet physical effectors.
	public const int ChannelCount = 38;
	public const int ForwardChannel = 0;
	public const int LeftTurnChannel = 1;
	public const int RightTurnChannel = 2;
	public const int ReverseChannel = 3;
	public const int LeftShoulderFlexionChannel = 4;
	public const int LeftShoulderExtensionChannel = 5;
	public const int RightShoulderFlexionChannel = 6;
	public const int RightShoulderExtensionChannel = 7;
	public const int LeftShoulderAbductionChannel = 8;
	public const int LeftShoulderAdductionChannel = 9;
	public const int RightShoulderAbductionChannel = 10;
	public const int RightShoulderAdductionChannel = 11;
	public const int LeftElbowFlexionChannel = 12;
	public const int LeftElbowExtensionChannel = 13;
	public const int RightElbowFlexionChannel = 14;
	public const int RightElbowExtensionChannel = 15;
	public const int StandChannel = 16;
	public const int CrouchChannel = 17;
	public const int SitChannel = 18;
	public const int LieChannel = 19;
	public const int LeftHipAbductionChannel = 20;
	public const int LeftHipAdductionChannel = 21;
	public const int RightHipAbductionChannel = 22;
	public const int RightHipAdductionChannel = 23;
	public const int LeftAnkleDorsiflexionChannel = 24;
	public const int LeftAnklePlantarflexionChannel = 25;
	public const int RightAnkleDorsiflexionChannel = 26;
	public const int RightAnklePlantarflexionChannel = 27;
	public const int LeftAnkleInversionChannel = 28;
	public const int LeftAnkleEversionChannel = 29;
	public const int RightAnkleInversionChannel = 30;
	public const int RightAnkleEversionChannel = 31;
	public const int TrunkRotateLeftChannel = 32;
	public const int TrunkRotateRightChannel = 33;
	public const int LeftHandCloseChannel = 34;
	public const int LeftHandOpenChannel = 35;
	public const int RightHandCloseChannel = 36;
	public const int RightHandOpenChannel = 37;

	public static bool IsActionCircuitStructure(StructureId structure)
		=> structure is StructureId.ProprioceptiveAfferents
			or StructureId.VestibularAfferents
			or StructureId.Pfc
			or StructureId.Acc
			or StructureId.PremotorCortex
			or StructureId.Sma
			or StructureId.Striatum
			or StructureId.GPe
			or StructureId.GPi
			or StructureId.Stn
			or StructureId.Snr
			or StructureId.MotorThalamus
			or StructureId.SpinalCordMotor;

	public static bool IsRateCodedAfferentOrReflexStructure(StructureId structure)
		=> structure is StructureId.ProprioceptiveAfferents
			or StructureId.VestibularAfferents
			or StructureId.SpinalCordMotor;

	public static bool IsProposalStructure(StructureId structure)
		=> structure is StructureId.Pfc
			or StructureId.Acc
			or StructureId.PremotorCortex
			or StructureId.Sma;

	public static bool IsStriatal(StructureId structure)
		=> structure is StructureId.Striatum or StructureId.NucleusAccumbens;

	public static bool IsDirectPathwayNeuron(int neuronIndex)
		=> (Math.Max(0, neuronIndex) & 1) == 0;

	public static bool UsesConvergentStriatalArbor(SpikeMessage message)
		=> message.TargetStructure == StructureId.Striatum &&
			message.Neurotransmitter == NTEnum.GLUTAMATE &&
			message.SourceStructure is StructureId.Pfc
				or StructureId.PremotorCortex
				or StructureId.Sma
				or StructureId.OrbitofrontalCortex
				or StructureId.IntralaminarThalamus;

	public static int StriatalArborTarget(
		int primaryIndex,
		int targetCount,
		int arborOffset)
	{
		if (targetCount <= 1 || arborOffset <= 0)
		{
			return Math.Clamp(primaryIndex, 0, Math.Max(0, targetCount - 1));
		}

		var primary = Math.Clamp(primaryIndex, 0, targetCount - 1);
		var channel = ChannelForNeuron(primary, StructureId.Striatum);
		var receptorClass = primary & 1;
		var stride = ChannelCount * 2;
		var localCount = Math.Max(1, (targetCount + stride - 1) / stride);
		var primaryLocal = primary / stride;
		for (var attempt = 1; attempt <= localCount; attempt++)
		{
			var local = PositiveMod(primaryLocal + arborOffset + attempt - 1, localCount);
			var candidate = ((local * ChannelCount + channel) * 2) + receptorClass;
			if (candidate >= 0 && candidate < targetCount && candidate != primary)
			{
				return candidate;
			}
		}

		return primary;
	}

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

	public static int ProjectToChannel(
		int channel,
		int targetCount,
		StructureId targetStructure,
		int seed)
	{
		if (targetCount <= 1)
		{
			return 0;
		}

		var boundedChannel = PositiveMod(channel, ChannelCount);
		var targetCellsPerLane = Math.Max(1, targetCount / ChannelCount);
		var local = PositiveMod(seed, targetCellsPerLane);
		var targetIndex = local * ChannelCount + boundedChannel;
		return FindBoundedLaneIndex(targetIndex, targetCount, targetStructure, boundedChannel);
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
