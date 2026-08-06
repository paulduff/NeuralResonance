using NeuralResonanceEngine.Protocol;

internal static class AttentionWorkspaceTopology
{
	public const int ChannelCount = 7;

	public static bool IsAttentionCircuitStructure(StructureId structure)
		=> structure is StructureId.Thalamus
			or StructureId.Trn
			or StructureId.Pulvinar
			or StructureId.MediodorsalThalamus
			or StructureId.IntralaminarThalamus
			or StructureId.Pfc
			or StructureId.Ppc
			or StructureId.Acc;

	public static bool IsAttentionSourceStructure(StructureId structure)
		=> IsAttentionCircuitStructure(structure) ||
			structure is StructureId.V1
			or StructureId.V2
			or StructureId.V4
			or StructureId.Mt
			or StructureId.A1
			or StructureId.AuditoryAssociationCortex
			or StructureId.S1
			or StructureId.SecondarySomatosensoryCortex
			or StructureId.Insula
			or StructureId.NucleusTractusSolitarius
			or StructureId.Hypothalamus
			or StructureId.CA1
			or StructureId.CA3
			or StructureId.Subiculum
			or StructureId.EntorhinalCortex
			or StructureId.TemporalAssociation
			or StructureId.BrocaBa44Ba45
			or StructureId.WernickePstgPsts
			or StructureId.ArcuateFasciculus
			or StructureId.M1
			or StructureId.Sma
			or StructureId.PremotorCortex
			or StructureId.MotorThalamus;

	public static bool EmitsAttentionDiagnostics(StructureId structure)
		=> IsAttentionSourceStructure(structure);

	public static int ChannelForNeuron(int neuronIndex, StructureId structure)
	{
		var fixedChannel = FixedSourceChannel(structure);
		return fixedChannel >= 0
			? fixedChannel
			: Math.Max(0, neuronIndex) % ChannelCount;
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
		var local = Math.Max(0, sourceIndex) / ChannelCount;
		var cellsInLane = Math.Max(1, targetCount / ChannelCount);
		var candidate = (PositiveMod(local + salt, cellsInLane) * ChannelCount) + channel;
		if (candidate < targetCount && ChannelForNeuron(candidate, targetStructure) == channel)
		{
			return candidate;
		}

		for (var index = channel; index < targetCount; index += ChannelCount)
		{
			if (ChannelForNeuron(index, targetStructure) == channel)
			{
				return index;
			}
		}

		return Math.Clamp(candidate, 0, targetCount - 1);
	}

	private static int FixedSourceChannel(StructureId structure) => structure switch
	{
		StructureId.V1 or StructureId.V2 or StructureId.V4 or StructureId.Mt => 0,
		StructureId.A1 or StructureId.AuditoryAssociationCortex => 1,
		StructureId.S1 or StructureId.SecondarySomatosensoryCortex => 2,
		StructureId.Insula or StructureId.NucleusTractusSolitarius or StructureId.Hypothalamus => 3,
		StructureId.CA1 or StructureId.CA3 or StructureId.Subiculum or StructureId.EntorhinalCortex or
			StructureId.TemporalAssociation => 4,
		StructureId.BrocaBa44Ba45 or StructureId.WernickePstgPsts or StructureId.ArcuateFasciculus => 5,
		StructureId.M1 or StructureId.Sma or StructureId.PremotorCortex or StructureId.MotorThalamus => 6,
		_ => -1
	};

	private static int PositiveMod(int value, int modulo)
		=> (int)((uint)value % (uint)Math.Max(1, modulo));
}
