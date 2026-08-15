using NeuralResonanceEngine.Protocol;
using System.Collections.Concurrent;

internal enum CorticalPopulation
{
	Layer1Modulatory = 0,
	Layer23Intratelencephalic = 1,
	Layer4Input = 2,
	Layer5PyramidalTract = 3,
	Layer6Corticothalamic = 4,
	PvInterneuron = 5,
	SstInterneuron = 6,
	VipInterneuron = 7
}

internal readonly record struct CorticalPopulationDescriptor(
	CorticalPopulation Population,
	string Name,
	string Role,
	NTEnum Neurotransmitter,
	bool LongRangeProjection);

internal static class CorticalLaminarTopology
{
	public const int PopulationCount = 8;
	private const int PopulationCycle = 20;

	private static readonly int[][] PopulationResidues =
	[
		[0],
		[1, 2, 3, 4, 5, 6],
		[7, 8, 9],
		[10, 11, 12],
		[13, 14, 15],
		[16, 17],
		[18],
		[19]
	];

	private static readonly CorticalPopulationDescriptor[] Descriptors =
	[
		new(CorticalPopulation.Layer1Modulatory, "L1 modulatory", "apical integration", NTEnum.GLUTAMATE, false),
		new(CorticalPopulation.Layer23Intratelencephalic, "L2/3 IT", "corticocortical integration", NTEnum.GLUTAMATE, true),
		new(CorticalPopulation.Layer4Input, "L4 input", "thalamocortical input", NTEnum.GLUTAMATE, false),
		new(CorticalPopulation.Layer5PyramidalTract, "L5 PT", "descending output", NTEnum.GLUTAMATE, true),
		new(CorticalPopulation.Layer6Corticothalamic, "L6 CT", "corticothalamic feedback", NTEnum.GLUTAMATE, true),
		new(CorticalPopulation.PvInterneuron, "PV interneuron", "perisomatic inhibition", NTEnum.GABA, false),
		new(CorticalPopulation.SstInterneuron, "SST interneuron", "dendritic inhibition", NTEnum.GABA, false),
		new(CorticalPopulation.VipInterneuron, "VIP interneuron", "interneuron disinhibition", NTEnum.GABA, false)
	];

	private static readonly ConcurrentDictionary<(int TargetCount, CorticalPopulation Population, int Ensemble), int[]> PopulationEnsembleIndexes = new();

	public static bool IsCorticalStructure(StructureId id) => id is
		StructureId.V1 or
		StructureId.V2 or
		StructureId.V3 or
		StructureId.V4 or
		StructureId.Mt or
		StructureId.A1 or
		StructureId.AuditoryAssociationCortex or
		StructureId.S1 or
		StructureId.SecondarySomatosensoryCortex or
		StructureId.EntorhinalCortex or
		StructureId.ParahippocampalCortex or
		StructureId.PerirhinalCortex or
		StructureId.Pfc or
		StructureId.DorsomedialPrefrontalCortex or
		StructureId.VentromedialPrefrontalCortex or
		StructureId.FrontalEyeFields or
		StructureId.BrocaBa44Ba45 or
		StructureId.WernickePstgPsts or
		StructureId.SupramarginalAngular or
		StructureId.PremotorCortex or
		StructureId.OrbitofrontalCortex or
		StructureId.Insula or
		StructureId.Ppc or
		StructureId.TemporalAssociation or
		StructureId.InferotemporalCortex or
		StructureId.FusiformGyrus or
		StructureId.TemporalPole or
		StructureId.TemporoparietalJunction or
		StructureId.Precuneus or
		StructureId.MidcingulateCortex or
		StructureId.PosteriorCingulate or
		StructureId.RetrosplenialCortex or
		StructureId.Acc or
		StructureId.Sma or
		StructureId.M1;

	public static CorticalPopulation PopulationForNeuron(int neuronIndex)
	{
		var residue = PositiveMod(neuronIndex, PopulationCycle);
		return residue switch
		{
			0 => CorticalPopulation.Layer1Modulatory,
			<= 6 => CorticalPopulation.Layer23Intratelencephalic,
			<= 9 => CorticalPopulation.Layer4Input,
			<= 12 => CorticalPopulation.Layer5PyramidalTract,
			<= 15 => CorticalPopulation.Layer6Corticothalamic,
			<= 17 => CorticalPopulation.PvInterneuron,
			18 => CorticalPopulation.SstInterneuron,
			_ => CorticalPopulation.VipInterneuron
		};
	}

	public static CorticalPopulationDescriptor Describe(CorticalPopulation population)
		=> Descriptors[(int)population];

	public static bool IsInhibitory(CorticalPopulation population)
		=> population is CorticalPopulation.PvInterneuron or CorticalPopulation.SstInterneuron or CorticalPopulation.VipInterneuron;

	public static bool EmitsLongRangeProjection(CorticalPopulation population)
		=> Describe(population).LongRangeProjection;

	public static NTEnum NeurotransmitterFor(CorticalPopulation population)
		=> Describe(population).Neurotransmitter;

	public static CorticalPopulation ResolveAfferentPopulation(SpikeMessage message, int sourceIndex)
	{
		if (message.IsFeedback)
		{
			return (sourceIndex & 1) == 0
				? CorticalPopulation.Layer1Modulatory
				: CorticalPopulation.Layer6Corticothalamic;
		}

		if (IsNeuromodulatory(message.SourceStructure))
		{
			return CorticalPopulation.Layer1Modulatory;
		}

		if (IsThalamic(message.SourceStructure) || IsPeripheralSensory(message.SourceStructure))
		{
			return CorticalPopulation.Layer4Input;
		}

		return CorticalPopulation.Layer23Intratelencephalic;
	}

	public static CorticalPopulation ResolveLocalTargetPopulation(CorticalPopulation source, int sourceIndex)
		=> source switch
		{
			CorticalPopulation.Layer1Modulatory => CorticalPopulation.Layer23Intratelencephalic,
			CorticalPopulation.Layer23Intratelencephalic => (sourceIndex & 1) == 0
				? CorticalPopulation.Layer23Intratelencephalic
				: CorticalPopulation.Layer5PyramidalTract,
			CorticalPopulation.Layer4Input => CorticalPopulation.Layer23Intratelencephalic,
			CorticalPopulation.Layer5PyramidalTract => CorticalPopulation.Layer6Corticothalamic,
			CorticalPopulation.Layer6Corticothalamic => CorticalPopulation.Layer4Input,
			CorticalPopulation.PvInterneuron => (sourceIndex & 1) == 0
				? CorticalPopulation.Layer23Intratelencephalic
				: CorticalPopulation.Layer5PyramidalTract,
			CorticalPopulation.SstInterneuron => CorticalPopulation.Layer1Modulatory,
			_ => CorticalPopulation.SstInterneuron
		};

	public static float LocalProjectionGain(CorticalPopulation source)
		=> source switch
		{
			CorticalPopulation.Layer4Input => 0.24f,
			CorticalPopulation.PvInterneuron => 0.32f,
			CorticalPopulation.SstInterneuron => 0.28f,
			CorticalPopulation.VipInterneuron => 0.24f,
			_ => 0.18f
		};

	public static int LocalProjectionDelayMs(CorticalPopulation source)
		=> source switch
		{
			CorticalPopulation.PvInterneuron => 1,
			CorticalPopulation.SstInterneuron or CorticalPopulation.VipInterneuron => 2,
			_ => 3
		};

	public static int ProjectToPopulation(int sourceIndex, int targetCount, CorticalPopulation population, int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}

		var residues = PopulationResidues[(int)population];
		var fullCycles = targetCount / PopulationCycle;
		var remainder = targetCount % PopulationCycle;
		var partialResidues = 0;
		for (var i = 0; i < residues.Length; i++)
		{
			if (residues[i] < remainder)
			{
				partialResidues++;
			}
		}

		var fullCycleCandidates = fullCycles * residues.Length;
		var candidateCount = fullCycleCandidates + partialResidues;
		if (candidateCount == 0)
		{
			return PositiveMod(sourceIndex + salt, targetCount);
		}

		var ordinal = PositiveMod((sourceIndex * 31) + (salt * 17), candidateCount);
		if (ordinal < fullCycleCandidates)
		{
			var cycle = ordinal / residues.Length;
			return (cycle * PopulationCycle) + residues[ordinal % residues.Length];
		}

		return (fullCycles * PopulationCycle) + residues[ordinal - fullCycleCandidates];
	}

	public static int ProjectWithinMicrocolumn(int sourceIndex, int targetCount, CorticalPopulation population, int salt)
	{
		var residues = PopulationResidues[(int)population];
		var column = Math.Max(0, sourceIndex) / PopulationCycle;
		var residue = residues[PositiveMod(sourceIndex + salt, residues.Length)];
		var candidate = (column * PopulationCycle) + residue;
		return candidate < targetCount
			? candidate
			: ProjectToPopulation(sourceIndex, targetCount, population, salt);
	}

	public static int ProjectPreservingEnsemble(
		int sourceIndex,
		int targetCount,
		CorticalPopulation population,
		int ensemble,
		int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}

		var candidates = PopulationEnsembleIndexes.GetOrAdd(
			(targetCount, population, ensemble),
			static key => Enumerable.Range(0, key.TargetCount)
				.Where(index => PopulationForNeuron(index) == key.Population &&
					PerceptEnsembleTopology.EnsembleForNeuron(index) == key.Ensemble)
				.ToArray());
		if (candidates.Length == 0)
		{
			return ProjectWithinMicrocolumn(sourceIndex, targetCount, population, salt);
		}

		return candidates[PositiveMod((sourceIndex * 31) + (salt * 17), candidates.Length)];
	}

	public static bool TryResolveDeclaredTarget(string targetNeuronId, int neuronCount, out int targetIndex)
	{
		targetIndex = 0;
		if (string.IsNullOrWhiteSpace(targetNeuronId) || neuronCount <= 0)
		{
			return false;
		}

		var span = targetNeuronId.AsSpan().Trim();
		var firstDigit = span.Length - 1;
		while (firstDigit >= 0 && char.IsDigit(span[firstDigit]))
		{
			firstDigit--;
		}

		if (firstDigit == span.Length - 1 || !int.TryParse(span[(firstDigit + 1)..], out var parsed))
		{
			return false;
		}

		targetIndex = PositiveMod(parsed, neuronCount);
		return true;
	}

	private static bool IsThalamic(StructureId id) => id is
		StructureId.IntralaminarThalamus or
		StructureId.Trn or
		StructureId.Pulvinar or
		StructureId.MediodorsalThalamus or
		StructureId.IntralaminarThalamus or
		StructureId.MotorThalamus or
		StructureId.LateralGeniculateNucleus or
		StructureId.MedialGeniculateNucleus or
		StructureId.VentralPosterolateralThalamus or
		StructureId.VentralPosteromedialThalamus or
		StructureId.AnteriorThalamicNuclei or
		StructureId.NucleusReuniens;

	private static bool IsNeuromodulatory(StructureId id) => id is
		StructureId.LocusCoeruleus or
		StructureId.RapheNuclei or
		StructureId.NucleusBasalis or
		StructureId.MedialSeptalNucleus or
		StructureId.DiagonalBandNucleus or
		StructureId.Vta or
		StructureId.Snc;

	private static bool IsPeripheralSensory(StructureId id) => id is
		StructureId.Retina or
		StructureId.Cochlea or
		StructureId.CochlearNucleus or
		StructureId.SomaticAfferents or
		StructureId.ProprioceptiveAfferents or
		StructureId.VestibularAfferents or
		StructureId.VisceralAfferents or
		StructureId.OlfactoryBulb;

	private static int PositiveMod(int value, int modulo)
		=> (int)((uint)value % (uint)Math.Max(1, modulo));
}
