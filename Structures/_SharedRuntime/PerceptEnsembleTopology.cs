using NeuralResonanceEngine.Protocol;

internal static class PerceptEnsembleTopology
{
	public const int EnsembleCount = 8;
	private const int CellsPerFeatureBand = 4;

	public static bool IsPerceptCircuitStructure(StructureId structure)
		=> structure is StructureId.Retina
			or StructureId.V1
			or StructureId.V2
			or StructureId.V3
			or StructureId.V4
			or StructureId.Mt
			or StructureId.InferotemporalCortex
			or StructureId.FusiformGyrus
			or StructureId.A1
			or StructureId.AuditoryAssociationCortex
			or StructureId.S1
			or StructureId.SecondarySomatosensoryCortex
			or StructureId.Ppc
			or StructureId.Insula
			or StructureId.TemporalAssociation
			or StructureId.PerirhinalCortex
			or StructureId.ParahippocampalCortex
			or StructureId.EntorhinalCortex
			or StructureId.DentateGyrus
			or StructureId.CA3
			or StructureId.CA1
			or StructureId.Pulvinar
			or StructureId.IntralaminarThalamus
			or StructureId.Pfc;

	public static int EnsembleForNeuron(int neuronIndex)
		=> (Math.Max(0, neuronIndex) / CellsPerFeatureBand) % EnsembleCount;

	public static int Project(int sourceIndex, int targetCount, int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}

		var ensemble = EnsembleForNeuron(sourceIndex);
		var sourceLocal = Math.Max(0, sourceIndex) / (EnsembleCount * CellsPerFeatureBand);
		var targetBandCount = Math.Max(1, targetCount / (EnsembleCount * CellsPerFeatureBand));
		var targetBand = PositiveMod(sourceLocal + salt, targetBandCount);
		var withinBand = PositiveMod(sourceIndex + salt, CellsPerFeatureBand);
		var candidate = ((targetBand * EnsembleCount + ensemble) * CellsPerFeatureBand) + withinBand;
		return FindBoundedEnsembleIndex(candidate, targetCount, ensemble);
	}

	public static int ProjectWithinPartition(
		int sourceIndex,
		int targetCount,
		int partition,
		int partitionCount,
		int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}

		var safePartitionCount = Math.Clamp(partitionCount, 1, targetCount);
		var safePartition = Math.Clamp(partition, 0, safePartitionCount - 1);
		var start = safePartition * targetCount / safePartitionCount;
		var end = Math.Max(start + 1, (safePartition + 1) * targetCount / safePartitionCount);
		var span = Math.Max(1, end - start);
		var ensemble = EnsembleForNeuron(sourceIndex);
		var offset = PositiveMod((sourceIndex / CellsPerFeatureBand) + salt, span);
		var candidate = start + offset;
		for (var step = 0; step < span; step++)
		{
			var index = start + PositiveMod((candidate - start) + step, span);
			if (index < targetCount && EnsembleForNeuron(index) == ensemble)
			{
				return index;
			}
		}

		return Math.Clamp(candidate, start, Math.Min(targetCount - 1, end - 1));
	}

	private static int FindBoundedEnsembleIndex(int candidate, int targetCount, int ensemble)
	{
		if (candidate >= 0 && candidate < targetCount && EnsembleForNeuron(candidate) == ensemble)
		{
			return candidate;
		}

		for (var i = 0; i < targetCount; i++)
		{
			if (EnsembleForNeuron(i) == ensemble)
			{
				return i;
			}
		}

		return Math.Clamp(candidate, 0, targetCount - 1);
	}

	private static int PositiveMod(int value, int modulo)
		=> (int)((uint)value % (uint)Math.Max(1, modulo));
}
