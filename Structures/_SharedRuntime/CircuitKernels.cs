using System;
using NeuralResonanceEngine.Protocol;

internal interface ICircuitKernel
{
	int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit);

	int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit);

	SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal);
}

internal static class CircuitKernelFactory
{
	private static readonly ICircuitKernel Sensory = new SensoryCircuitKernel();

	private static readonly ICircuitKernel Thalamic = new ThalamicCircuitKernel();

	private static readonly ICircuitKernel Hippocampal = new HippocampalCircuitKernel();

	private static readonly ICircuitKernel BasalGanglia = new BasalGangliaCircuitKernel();

	private static readonly ICircuitKernel Cerebellar = new CerebellarCircuitKernel();

	private static readonly ICircuitKernel Neuromod = new NeuromodulatoryCircuitKernel();

	private static readonly ICircuitKernel Cortical = new CorticalAssociationCircuitKernel();

	private static readonly ICircuitKernel VisualAssociation = new VisualAssociationCircuitKernel();

	private static readonly ICircuitKernel AuditoryAssociation = new AuditoryAssociationCircuitKernel();

	private static readonly ICircuitKernel SomatosensoryAssociation = new SomatosensoryAssociationCircuitKernel();

	private static readonly ICircuitKernel SelfContext = new SelfContextCircuitKernel();

	private static readonly ICircuitKernel ExecutiveControl = new ExecutiveControlCircuitKernel();

	private static readonly ICircuitKernel HomuncularSensorimotor = new HomuncularSensorimotorCircuitKernel();

	private static readonly ICircuitKernel PosteriorParietalBodySchema = new PosteriorParietalBodySchemaCircuitKernel();

	public static ICircuitKernel For(StructureId structureId)
	{
		if (IsVisualAssociation(structureId))
		{
			return VisualAssociation;
		}
		if (structureId == StructureId.AuditoryAssociationCortex)
		{
			return AuditoryAssociation;
		}
		if (structureId == StructureId.SecondarySomatosensoryCortex)
		{
			return SomatosensoryAssociation;
		}
		if (IsSelfContext(structureId))
		{
			return SelfContext;
		}
		if (IsExecutiveControl(structureId))
		{
			return ExecutiveControl;
		}
		if (IsHomuncularSensorimotor(structureId))
		{
			return HomuncularSensorimotor;
		}
		if (IsPosteriorParietalBodySchema(structureId))
		{
			return PosteriorParietalBodySchema;
		}
		if (IsSensory(structureId))
		{
			return Sensory;
		}
		if (IsThalamic(structureId))
		{
			return Thalamic;
		}
		if (IsHippocampal(structureId))
		{
			return Hippocampal;
		}
		if (IsBasalGanglia(structureId))
		{
			return BasalGanglia;
		}
		if (IsCerebellar(structureId))
		{
			return Cerebellar;
		}
		if (IsNeuromodulatory(structureId))
		{
			return Neuromod;
		}
		return Cortical;
	}

	private static bool IsSensory(StructureId id)
	{
		switch (id)
		{
		case StructureId.Retina:
		case StructureId.Cochlea:
		case StructureId.CochlearNucleus:
		case StructureId.SuperiorOlive:
		case StructureId.InferiorColliculus:
		case StructureId.SomaticAfferents:
		case StructureId.ProprioceptiveAfferents:
		case StructureId.VestibularAfferents:
		case StructureId.VisceralAfferents:
		case StructureId.VestibularNuclei:
		case StructureId.NucleusTractusSolitarius:
		case StructureId.V1:
		case StructureId.V2:
		case StructureId.V4:
		case StructureId.Mt:
		case StructureId.A1:
		case StructureId.S1:
		case StructureId.OlfactoryBulb:
			return true;
		default:
			return false;
		}
	}

	private static bool IsThalamic(StructureId id)
	{
		switch (id)
		{
		case StructureId.Thalamus:
		case StructureId.Trn:
		case StructureId.Pulvinar:
		case StructureId.MediodorsalThalamus:
		case StructureId.IntralaminarThalamus:
		case StructureId.MotorThalamus:
			return true;
		default:
			return false;
		}
	}

	private static bool IsHippocampal(StructureId id)
	{
		switch (id)
		{
		case StructureId.EntorhinalCortex:
		case StructureId.DentateGyrus:
		case StructureId.CA3:
		case StructureId.CA2:
		case StructureId.CA1:
		case StructureId.Subiculum:
		case StructureId.Presubiculum:
		case StructureId.Parasubiculum:
		case StructureId.ParahippocampalCortex:
		case StructureId.PerirhinalCortex:
			return true;
		default:
			return false;
		}
	}

	private static bool IsBasalGanglia(StructureId id)
	{
		switch (id)
		{
		case StructureId.Striatum:
		case StructureId.GlobusPallidus:
		case StructureId.GPe:
		case StructureId.GPi:
		case StructureId.Stn:
		case StructureId.Snr:
		case StructureId.Snc:
		case StructureId.NucleusAccumbens:
		case StructureId.VentralPallidum:
		case StructureId.Habenula:
			return true;
		default:
			return false;
		}
	}

	private static bool IsCerebellar(StructureId id)
	{
		switch (id)
		{
		case StructureId.CerebellarGranule:
		case StructureId.CerebellarVermis:
		case StructureId.CerebellarLobules:
		case StructureId.PurkinjeCellLayer:
		case StructureId.DeepCerebellarNuclei:
		case StructureId.InferiorOlive:
			return true;
		default:
			return false;
		}
	}

	private static bool IsNeuromodulatory(StructureId id)
	{
		switch (id)
		{
		case StructureId.LocusCoeruleus:
		case StructureId.RapheNuclei:
		case StructureId.BasalForebrain:
		case StructureId.Vta:
			return true;
		default:
			return false;
		}
	}

	private static bool IsHomuncularSensorimotor(StructureId id)
	{
		switch (id)
		{
		case StructureId.S1:
		case StructureId.M1:
			return true;
		default:
			return false;
		}
	}

	private static bool IsPosteriorParietalBodySchema(StructureId id)
	{
		return id == StructureId.Ppc;
	}

	private static bool IsVisualAssociation(StructureId id)
		=> id is StructureId.V3 or StructureId.InferotemporalCortex or StructureId.FusiformGyrus;

	private static bool IsSelfContext(StructureId id)
		=> id is StructureId.TemporalPole or StructureId.TemporoparietalJunction or StructureId.Precuneus;

	private static bool IsExecutiveControl(StructureId id)
		=> id is StructureId.MidcingulateCortex
			or StructureId.DorsomedialPrefrontalCortex
			or StructureId.VentromedialPrefrontalCortex
			or StructureId.FrontalEyeFields;
}

internal abstract class CircuitKernelBase : ICircuitKernel
{
	public virtual int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure) &&
			PerceptEnsembleTopology.IsPerceptCircuitStructure(message.TargetStructure))
		{
			return PerceptEnsembleTopology.Project(sourceIndex, neuronCount, message.IsFeedback ? 3 : 2);
		}
		return TopographicMap.ProjectLinear(sourceIndex, neuronCount, message.SourceStructure, message.TargetStructure, message.IsFeedback ? 1 : 0);
	}

	public virtual int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(circuit.StructureId) &&
			PerceptEnsembleTopology.IsPerceptCircuitStructure(targetStructure))
		{
			return PerceptEnsembleTopology.Project(source.Index, Math.Max(16, circuit.TargetMapModulo), 5);
		}
		return TopographicMap.ProjectLinear(source.Index, Math.Max(16, circuit.TargetMapModulo), targetStructure, targetStructure, circuit.TargetMapStride);
	}

	public virtual SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		return SpikeTypeEnum.ACTION_POTENTIAL;
	}

	protected static int PositiveMod(int value, int modulo)
	{
		return (int)((uint)value % (uint)modulo);
	}

	protected static bool TryParseNeuronSuffix(string neuronId, out int value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(neuronId))
		{
			return false;
		}
		ReadOnlySpan<char> readOnlySpan = neuronId.AsSpan().Trim();
		int num = readOnlySpan.Length - 1;
		while (num >= 0 && char.IsDigit(readOnlySpan[num]))
		{
			num--;
		}
		return num < readOnlySpan.Length - 1 && int.TryParse(readOnlySpan.Slice(num + 1), out value);
	}
}

internal static class TopographicMap
{
	private const int CanonicalWidth = 32;
	private const int CanonicalHeight = 32;

	public static int ResolveSignalIndex(string sourceNeuronId, string targetNeuronId, Guid synapseId, StructureId source, StructureId target)
	{
		if (TryParseNeuronSuffix(sourceNeuronId, out int sourceIndex))
		{
			return Math.Max(0, sourceIndex);
		}
		if (TryParseNeuronSuffix(targetNeuronId, out int targetIndex))
		{
			return Math.Max(0, targetIndex);
		}
		return PositiveMod(HashCode.Combine(sourceNeuronId ?? string.Empty, targetNeuronId ?? string.Empty, synapseId, (int)source, (int)target), CanonicalWidth * CanonicalHeight);
	}

	public static int ProjectLinear(int sourceIndex, int targetCount, StructureId source, StructureId target, int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}
		var sourceGrid = ToGrid(sourceIndex, CanonicalWidth, CanonicalHeight);
		int width = ComputeWidth(targetCount);
		int height = Math.Max(1, (targetCount + width - 1) / width);
		int x = Scale(sourceGrid.X, CanonicalWidth, width);
		int y = Scale(sourceGrid.Y, CanonicalHeight, height);
		int jitter = SmallJitter(sourceIndex, source, target, salt);
		return GridToIndex(x + jitter, y - jitter, width, height, targetCount);
	}

	public static int ProjectGrid(int sourceIndex, int targetCount, int sourceWidth, int sourceHeight, StructureId source, StructureId target, int salt, int xBias = 0, int yBias = 0)
	{
		if (targetCount <= 1)
		{
			return 0;
		}
		var sourceGrid = ToGrid(sourceIndex, Math.Max(1, sourceWidth), Math.Max(1, sourceHeight));
		int width = ComputeWidth(targetCount);
		int height = Math.Max(1, (targetCount + width - 1) / width);
		int x = Scale(sourceGrid.X, sourceWidth, width) + xBias + SmallJitter(sourceIndex, source, target, salt);
		int y = Scale(sourceGrid.Y, sourceHeight, height) + yBias + SmallJitter(sourceIndex, target, source, salt + 17);
		return GridToIndex(x, y, width, height, targetCount);
	}

	public static int ProjectLayeredColumn(int sourceIndex, int targetCount, int layers, StructureId source, StructureId target, int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}
		int layerCount = Math.Max(1, layers);
		int columns = Math.Max(1, targetCount / layerCount);
		int sourceLayer = PositiveMod(sourceIndex + salt, layerCount);
		int sourceColumn = Math.Max(0, sourceIndex / layerCount);
		int column = PositiveMod(sourceColumn + SmallJitter(sourceIndex, source, target, salt), columns);
		return PositiveMod(column * layerCount + sourceLayer, targetCount);
	}

	public static int ProjectChannel(int sourceIndex, int targetCount, int channels, int localSpan, StructureId source, StructureId target, int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}
		int channelCount = Math.Clamp(channels, 1, Math.Max(1, targetCount));
		int cellsPerChannel = Math.Max(1, targetCount / channelCount);
		int channel = PositiveMod(sourceIndex + SmallJitter(sourceIndex, source, target, salt), channelCount);
		int local = PositiveMod(sourceIndex / channelCount + salt, Math.Max(cellsPerChannel, localSpan));
		return PositiveMod(channel * cellsPerChannel + local, targetCount);
	}

	public static int ProjectDiffuse(int sourceIndex, int targetCount, StructureId source, StructureId target, int salt)
	{
		if (targetCount <= 1)
		{
			return 0;
		}
		// Golden-ratio stride gives neuromodulatory projections broad coverage without random churn.
		int stride = Math.Max(1, (int)(targetCount * 0.61803398875));
		return PositiveMod(sourceIndex * stride + HashCode.Combine((int)source, (int)target, salt), targetCount);
	}

	private static (int X, int Y) ToGrid(int index, int width, int height)
	{
		int safeWidth = Math.Max(1, width);
		int safeHeight = Math.Max(1, height);
		int safeIndex = PositiveMod(index, safeWidth * safeHeight);
		return (safeIndex % safeWidth, safeIndex / safeWidth);
	}

	private static int GridToIndex(int x, int y, int width, int height, int targetCount)
	{
		int safeWidth = Math.Max(1, width);
		int safeHeight = Math.Max(1, height);
		int safeX = Math.Clamp(x, 0, safeWidth - 1);
		int safeY = Math.Clamp(y, 0, safeHeight - 1);
		return Math.Min(targetCount - 1, safeY * safeWidth + safeX);
	}

	private static int Scale(int value, int fromMax, int toMax)
	{
		if (toMax <= 1)
		{
			return 0;
		}
		return Math.Clamp((int)MathF.Round(value / (float)Math.Max(1, fromMax - 1) * (toMax - 1)), 0, toMax - 1);
	}

	private static int ComputeWidth(int count)
	{
		return Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(Math.Max(1, count))));
	}

	private static int SmallJitter(int sourceIndex, StructureId source, StructureId target, int salt)
	{
		return PositiveMod(HashCode.Combine(sourceIndex, (int)source, (int)target, salt), 3) - 1;
	}

	private static int PositiveMod(int value, int modulo)
	{
		return (int)((uint)value % (uint)modulo);
	}

	private static bool TryParseNeuronSuffix(string neuronId, out int value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(neuronId))
		{
			return false;
		}
		ReadOnlySpan<char> text = neuronId.AsSpan().Trim();
		int start = text.Length - 1;
		while (start >= 0 && char.IsDigit(text[start]))
		{
			start--;
		}
		return start < text.Length - 1 && int.TryParse(text.Slice(start + 1), out value);
	}
}

internal sealed class SensoryCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure) &&
			PerceptEnsembleTopology.IsPerceptCircuitStructure(message.TargetStructure))
		{
			return PerceptEnsembleTopology.Project(sourceIndex, neuronCount, message.IsFeedback ? 13 : 11);
		}
		if (IsAuditory(message.SourceStructure) || IsAuditory(message.TargetStructure))
		{
			return TopographicMap.ProjectChannel(sourceIndex, neuronCount, 32, 8, message.SourceStructure, message.TargetStructure, 11);
		}
		return TopographicMap.ProjectGrid(sourceIndex, neuronCount, 32, 32, message.SourceStructure, message.TargetStructure, 13);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		int targetCount = Math.Max(16, circuit.TargetMapModulo);
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(circuit.StructureId) &&
			PerceptEnsembleTopology.IsPerceptCircuitStructure(targetStructure))
		{
			return PerceptEnsembleTopology.Project(source.Index, targetCount, 17);
		}
		if (IsAuditory(targetStructure))
		{
			return TopographicMap.ProjectChannel(source.Index, targetCount, 32, 8, targetStructure, targetStructure, 19);
		}
		return TopographicMap.ProjectGrid(source.Index, targetCount, 32, 32, targetStructure, targetStructure, 23);
	}

	private static bool IsAuditory(StructureId id)
	{
		return id == StructureId.Cochlea || id == StructureId.CochlearNucleus || id == StructureId.SuperiorOlive || id == StructureId.InferiorColliculus || id == StructureId.A1;
	}
}

internal sealed class ThalamicCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure) &&
			PerceptEnsembleTopology.IsPerceptCircuitStructure(message.TargetStructure))
		{
			return PerceptEnsembleTopology.Project(sourceIndex, neuronCount, message.IsFeedback ? 31 : 29);
		}
		if (message.TargetStructure == StructureId.MotorThalamus &&
			ActionChannelTopology.IsActionCircuitStructure(message.SourceStructure))
		{
			return ActionChannelTopology.Project(
				sourceIndex,
				message.SourceStructure,
				neuronCount,
				message.TargetStructure,
				message.IsFeedback ? 31 : 29);
		}

		int offset = message.IsFeedback ? 1 : 0;
		return TopographicMap.ProjectLayeredColumn(sourceIndex + offset, neuronCount, 3, message.SourceStructure, message.TargetStructure, 29);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(circuit.StructureId) &&
			PerceptEnsembleTopology.IsPerceptCircuitStructure(targetStructure))
		{
			return PerceptEnsembleTopology.Project(source.Index, Math.Max(16, circuit.TargetMapModulo), 31);
		}
		if (circuit.StructureId == StructureId.MotorThalamus)
		{
			return ActionChannelTopology.Project(
				source.Index,
				circuit.StructureId,
				Math.Max(16, circuit.TargetMapModulo),
				targetStructure,
				31);
		}

		return TopographicMap.ProjectLayeredColumn(source.Index, Math.Max(16, circuit.TargetMapModulo), 3, targetStructure, targetStructure, 31);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		if (!isFeedback)
		{
			return SpikeTypeEnum.ACTION_POTENTIAL;
		}
		return (localNeuromod.NorepinephrineLevel > 0.35f) ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL;
	}
}

internal sealed class HippocampalCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure) &&
			PerceptEnsembleTopology.IsPerceptCircuitStructure(message.TargetStructure))
		{
			return PerceptEnsembleTopology.Project(sourceIndex, neuronCount, HippocampalStageOffset(message.TargetStructure));
		}
		return TopographicMap.ProjectLayeredColumn(sourceIndex, neuronCount, 4, message.SourceStructure, message.TargetStructure, HippocampalStageOffset(message.TargetStructure));
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(circuit.StructureId) &&
			PerceptEnsembleTopology.IsPerceptCircuitStructure(targetStructure))
		{
			return PerceptEnsembleTopology.Project(source.Index, Math.Max(16, circuit.TargetMapModulo), HippocampalStageOffset(targetStructure));
		}
		return TopographicMap.ProjectLayeredColumn(source.Index, Math.Max(16, circuit.TargetMapModulo), 4, targetStructure, targetStructure, HippocampalStageOffset(targetStructure));
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		switch (sourceStructure)
		{
		case StructureId.CA3:
		case StructureId.CA2:
			return SpikeTypeEnum.BURST;
		default:
			return SpikeTypeEnum.ACTION_POTENTIAL;
		}
	}

	private static int HippocampalStageOffset(StructureId id)
	{
		return id switch
		{
			StructureId.EntorhinalCortex => 3,
			StructureId.DentateGyrus => 7,
			StructureId.CA3 => 11,
			StructureId.CA2 => 13,
			StructureId.CA1 => 17,
			StructureId.Subiculum => 19,
			_ => 5,
		};
	}
}

internal sealed class BasalGangliaCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		return ActionChannelTopology.Project(
			sourceIndex,
			message.SourceStructure,
			neuronCount,
			message.TargetStructure,
			message.IsFeedback ? 43 : 41);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		return ActionChannelTopology.Project(
			source.Index,
			circuit.StructureId,
			Math.Max(16, circuit.TargetMapModulo),
			targetStructure,
			47);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		switch (sourceStructure)
		{
		case StructureId.Stn:
			return SpikeTypeEnum.BURST;
		case StructureId.Snr:
		case StructureId.GPi:
		case StructureId.GPe:
			return SpikeTypeEnum.ACTION_POTENTIAL;
		default:
			return isFeedback ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL;
		}
	}
}

internal sealed class CerebellarCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		return TopographicMap.ProjectChannel(sourceIndex, neuronCount, 24, 12, message.SourceStructure, message.TargetStructure, 53);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		return TopographicMap.ProjectChannel(source.Index, Math.Max(16, circuit.TargetMapModulo), 24, 12, targetStructure, targetStructure, 59);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		if (sourceStructure == StructureId.PurkinjeCellLayer || sourceStructure == StructureId.InferiorOlive)
		{
			return SpikeTypeEnum.COMPLEX;
		}
		return isFeedback ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL;
	}
}

internal sealed class NeuromodulatoryCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		return TopographicMap.ProjectDiffuse(sourceIndex, neuronCount, message.SourceStructure, message.TargetStructure, 67);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		return TopographicMap.ProjectDiffuse(source.Index, Math.Max(16, circuit.TargetMapModulo), targetStructure, targetStructure, 71);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		return SpikeTypeEnum.GRADED;
	}
}

internal sealed class CorticalAssociationCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		return TopographicMap.ProjectLayeredColumn(sourceIndex, neuronCount, 6, message.SourceStructure, message.TargetStructure, message.IsFeedback ? 83 : 79);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		return TopographicMap.ProjectLayeredColumn(source.Index, Math.Max(16, circuit.TargetMapModulo), 6, targetStructure, targetStructure, 89);
	}
}

internal sealed class VisualAssociationCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		return circuit.StructureId switch
		{
			StructureId.V3 when PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure)
				=> PerceptEnsembleTopology.Project(sourceIndex, neuronCount, message.IsFeedback ? 103 : 97),
			StructureId.V3 => TopographicMap.ProjectGrid(sourceIndex, neuronCount, 32, 32, message.SourceStructure, message.TargetStructure, message.IsFeedback ? 103 : 97),
			StructureId.InferotemporalCortex when PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure)
				=> PerceptEnsembleTopology.ProjectWithinPartition(sourceIndex, neuronCount, VisualInputLane(message.SourceStructure), 6, 107),
			StructureId.InferotemporalCortex => TopographicMap.ProjectChannel(sourceIndex + VisualInputLane(message.SourceStructure) * 97, neuronCount, 12, 8, message.SourceStructure, message.TargetStructure, 107),
			StructureId.FusiformGyrus when PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure)
				=> PerceptEnsembleTopology.ProjectWithinPartition(sourceIndex, neuronCount, ExpertiseInputLane(message.SourceStructure), 5, 109),
			StructureId.FusiformGyrus => TopographicMap.ProjectChannel(sourceIndex + ExpertiseInputLane(message.SourceStructure) * 131, neuronCount, 8, 12, message.SourceStructure, message.TargetStructure, 109),
			_ => base.ResolveInboundNeuronIndex(message, neuronCount, circuit)
		};
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		int targetCount = Math.Max(16, circuit.TargetMapModulo);
		return circuit.StructureId switch
		{
			StructureId.V3 => TopographicMap.ProjectGrid(source.Index, targetCount, 32, 32, circuit.StructureId, targetStructure, 113),
			StructureId.InferotemporalCortex => TopographicMap.ProjectChannel(source.Index, targetCount, 12, 8, circuit.StructureId, targetStructure, 127),
			StructureId.FusiformGyrus => TopographicMap.ProjectChannel(source.Index, targetCount, 8, 12, circuit.StructureId, targetStructure, 131),
			_ => base.ResolveOutboundTargetIndex(source, targetStructure, circuit)
		};
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		float attention = Math.Max(
			localNeuromod.AcetylcholineLevel,
			localNeuromod.NorepinephrineLevel);
		return isFeedback || (sourceStructure == StructureId.FusiformGyrus && attention > 0.48f)
			? SpikeTypeEnum.BURST
			: SpikeTypeEnum.ACTION_POTENTIAL;
	}

	private static int VisualInputLane(StructureId source)
		=> source switch
		{
			StructureId.V3 or StructureId.V4 => 0,
			StructureId.Mt => 1,
			StructureId.Pulvinar or StructureId.Thalamus => 2,
			StructureId.TemporalAssociation or StructureId.PerirhinalCortex => 3,
			StructureId.Pfc => 4,
			_ => 5
		};

	private static int ExpertiseInputLane(StructureId source)
		=> source switch
		{
			StructureId.InferotemporalCortex or StructureId.V4 => 0,
			StructureId.TemporalAssociation or StructureId.TemporalPole => 1,
			StructureId.AuditoryAssociationCortex => 2,
			StructureId.Pfc or StructureId.DorsomedialPrefrontalCortex => 3,
			_ => 4
		};
}

internal sealed class AuditoryAssociationCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		int streamOffset = message.SourceStructure switch
		{
			StructureId.A1 or StructureId.InferiorColliculus => 0,
			StructureId.TemporalPole or StructureId.TemporalAssociation => 211,
			StructureId.WernickePstgPsts => 421,
			_ => 631
		};
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure))
		{
			var stream = message.SourceStructure switch
			{
				StructureId.A1 or StructureId.InferiorColliculus => 0,
				StructureId.TemporalPole or StructureId.TemporalAssociation => 1,
				StructureId.WernickePstgPsts => 2,
				_ => 3
			};
			return PerceptEnsembleTopology.ProjectWithinPartition(sourceIndex, neuronCount, stream, 4, 137);
		}
		return TopographicMap.ProjectChannel(
			sourceIndex + streamOffset,
			neuronCount,
			32,
			8,
			message.SourceStructure,
			message.TargetStructure,
			137);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		var targetCount = Math.Max(16, circuit.TargetMapModulo);
		return TopographicMap.ProjectChannel(
			source.Index,
			targetCount,
			32,
			8,
			circuit.StructureId,
			targetStructure,
			139);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		float auditoryAttention = Math.Max(
			localNeuromod.AcetylcholineLevel,
			localNeuromod.NorepinephrineLevel);
		return isFeedback || auditoryAttention > 0.52f
			? SpikeTypeEnum.BURST
			: SpikeTypeEnum.ACTION_POTENTIAL;
	}
}

internal sealed class SomatosensoryAssociationCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		int bodyLane = ResolveBodyLane(message.SourceNeuronId, message.TargetNeuronId, sourceIndex);
		if (PerceptEnsembleTopology.IsPerceptCircuitStructure(message.SourceStructure))
		{
			return PerceptEnsembleTopology.ProjectWithinPartition(
				sourceIndex,
				neuronCount,
				bodyLane,
				4,
				message.IsFeedback ? 151 : 149);
		}
		return ProjectBodyLane(sourceIndex, neuronCount, bodyLane, message.IsFeedback ? 151 : 149);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		int bodyLane = ResolveBodyLane(source.Id, string.Empty, source.Index);
		return ProjectBodyLane(source.Index, Math.Max(16, circuit.TargetMapModulo), bodyLane, 157);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
		=> isFeedback || localNeuromod.NorepinephrineLevel > 0.58f
			? SpikeTypeEnum.BURST
			: SpikeTypeEnum.ACTION_POTENTIAL;

	private static int ResolveBodyLane(string sourceNeuronId, string targetNeuronId, int sourceIndex)
	{
		string text = $"{sourceNeuronId} {targetNeuronId}";
		if (MentionsAny(text, "face", "head", "eye", "mouth", "jaw", "tongue"))
		{
			return 0;
		}
		if (MentionsAny(text, "hand", "arm", "finger", "grasp", "reach", "texture"))
		{
			return 1;
		}
		if (MentionsAny(text, "trunk", "body", "torso", "pressure", "pain", "contact"))
		{
			return 2;
		}
		if (MentionsAny(text, "leg", "foot", "walk", "run", "stride"))
		{
			return 3;
		}
		return PositiveMod(sourceIndex, 4);
	}

	private static int ProjectBodyLane(int sourceIndex, int targetCount, int lane, int salt)
	{
		int laneCount = 4;
		int laneSpan = Math.Max(1, targetCount / laneCount);
		int local = PositiveMod(sourceIndex + HashCode.Combine(sourceIndex, salt), laneSpan);
		return Math.Clamp(lane * laneSpan + local, 0, Math.Max(0, targetCount - 1));
	}

	private static bool MentionsAny(string text, params string[] terms)
	{
		foreach (string term in terms)
		{
			if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}

internal sealed class SelfContextCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		int lane = ResolveContextLane(message.SourceStructure);
		int structureOffset = circuit.StructureId switch
		{
			StructureId.TemporalPole => 0,
			StructureId.TemporoparietalJunction => 3,
			StructureId.Precuneus => 6,
			_ => 1
		};
		return TopographicMap.ProjectChannel(
			sourceIndex + (lane + structureOffset) * 173,
			neuronCount,
			8,
			12,
			message.SourceStructure,
			message.TargetStructure,
			163);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
		=> TopographicMap.ProjectChannel(
			source.Index + ResolveContextLane(targetStructure) * 179,
			Math.Max(16, circuit.TargetMapModulo),
			8,
			12,
			circuit.StructureId,
			targetStructure,
			167);

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		float bindingGain = Math.Max(
			localNeuromod.AcetylcholineLevel,
			localNeuromod.DopamineLevel);
		return isFeedback || bindingGain > 0.55f
			? SpikeTypeEnum.BURST
			: SpikeTypeEnum.ACTION_POTENTIAL;
	}

	private static int ResolveContextLane(StructureId source)
		=> source switch
		{
			StructureId.SecondarySomatosensoryCortex or StructureId.S1 or StructureId.Ppc => 0,
			StructureId.PosteriorCingulate or StructureId.RetrosplenialCortex or StructureId.EntorhinalCortex or StructureId.CA1 => 1,
			StructureId.Amygdala or StructureId.Insula or StructureId.VentromedialPrefrontalCortex => 2,
			StructureId.TemporalAssociation or StructureId.FusiformGyrus or StructureId.AuditoryAssociationCortex => 3,
			StructureId.Pfc or StructureId.DorsomedialPrefrontalCortex => 4,
			_ => 5
		};
}

internal sealed class ExecutiveControlCircuitKernel : CircuitKernelBase
{
	private const int ControlLaneCount = 6;

	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		int lane = ResolveControlLane(message.SourceStructure);

		if (circuit.StructureId == StructureId.FrontalEyeFields)
		{
			return TopographicMap.ProjectGrid(
				sourceIndex + lane * 37,
				neuronCount,
				32,
				32,
				message.SourceStructure,
				message.TargetStructure,
				message.IsFeedback ? 191 : 181);
		}

		return ProjectControlLane(
			sourceIndex,
			neuronCount,
			lane,
			message.SourceStructure,
			message.TargetStructure,
			message.IsFeedback ? 197 : 193);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		int targetCount = Math.Max(16, circuit.TargetMapModulo);
		return circuit.StructureId == StructureId.FrontalEyeFields
			? TopographicMap.ProjectGrid(source.Index, targetCount, 32, 32, circuit.StructureId, targetStructure, 199)
			: ProjectControlLane(
				source.Index,
				targetCount,
				ResolveControlLane(targetStructure),
				circuit.StructureId,
				targetStructure,
				211);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		if (isFeedback)
		{
			return SpikeTypeEnum.BURST;
		}

		float attention = Math.Max(
			localNeuromod.AcetylcholineLevel,
			localNeuromod.NorepinephrineLevel);
		if (sourceStructure == StructureId.FrontalEyeFields && attention > 0.40f)
		{
			return SpikeTypeEnum.BURST;
		}
		if (sourceStructure == StructureId.MidcingulateCortex && MathF.Abs(localRewardSignal) > 0.22f)
		{
			return SpikeTypeEnum.BURST;
		}
		if (sourceStructure is StructureId.DorsomedialPrefrontalCortex or StructureId.VentromedialPrefrontalCortex
			&& localNeuromod.DopamineLevel > 0.48f)
		{
			return SpikeTypeEnum.BURST;
		}
		return SpikeTypeEnum.ACTION_POTENTIAL;
	}

	private static int ResolveControlLane(StructureId source)
		=> source switch
		{
			StructureId.Pfc or StructureId.DorsomedialPrefrontalCortex => 0,
			StructureId.Acc or StructureId.MidcingulateCortex => 1,
			StructureId.OrbitofrontalCortex or StructureId.VentromedialPrefrontalCortex or StructureId.Amygdala => 2,
			StructureId.Striatum or StructureId.NucleusAccumbens or StructureId.MediodorsalThalamus => 3,
			StructureId.Ppc or StructureId.Pulvinar or StructureId.SuperiorColliculus or StructureId.FrontalEyeFields => 4,
			_ => 5
		};

	private static int ProjectControlLane(
		int sourceIndex,
		int targetCount,
		int lane,
		StructureId source,
		StructureId target,
		int salt)
	{
		int safeCount = Math.Max(1, targetCount);
		int safeLane = Math.Clamp(lane, 0, ControlLaneCount - 1);
		int laneStart = safeLane * safeCount / ControlLaneCount;
		int laneEnd = (safeLane + 1) * safeCount / ControlLaneCount;
		int laneSpan = Math.Max(1, laneEnd - laneStart);
		int localIndex = TopographicMap.ProjectChannel(
			sourceIndex,
			laneSpan,
			4,
			4,
			source,
			target,
			salt);
		return Math.Min(safeCount - 1, laneStart + localIndex);
	}
}

internal sealed class HomuncularSensorimotorCircuitKernel : CircuitKernelBase
{
	private const int FaceStart = 0;
	private const int HandStart = 96;
	private const int TrunkStart = 224;
	private const int LegStart = 288;
	private const int FaceSpan = 96;
	private const int HandSpan = 128;
	private const int TrunkSpan = 64;
	private const int LegSpan = 96;

	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		var zone = ResolveBodyZone(message.SourceNeuronId, message.TargetNeuronId, sourceIndex);
		return ProjectBodyZone(sourceIndex, neuronCount, zone, message.IsFeedback ? 7 : 3);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		var zone = ResolveBodyZone(source.Id, string.Empty, source.Index);
		return ProjectBodyZone(source.Index, Math.Max(16, circuit.TargetMapModulo), zone, 11);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		if (sourceStructure == StructureId.M1 && localNeuromod.DopamineLevel > 0.42f)
		{
			return SpikeTypeEnum.BURST;
		}

		return isFeedback ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL;
	}

	private static BodyZone ResolveBodyZone(string sourceNeuronId, string targetNeuronId, int sourceIndex)
	{
		var text = $"{sourceNeuronId} {targetNeuronId}";
		if (MentionsAny(text, "face", "head", "eye", "mouth", "jaw", "tongue", "neck", "scan", "look", "orient"))
		{
			return BodyZone.FaceHead;
		}

		if (MentionsAny(text, "hand", "arm", "finger", "grasp", "tool", "weapon", "reach", "pick"))
		{
			return BodyZone.HandArm;
		}

		if (MentionsAny(text, "trunk", "body", "posture", "balance", "torso"))
		{
			return BodyZone.Trunk;
		}

		if (MentionsAny(text, "leg", "foot", "walk", "run", "forward", "turn", "stride", "locomotor", "escape"))
		{
			return BodyZone.LegFoot;
		}

		return PositiveMod(sourceIndex, 4) switch
		{
			0 => BodyZone.FaceHead,
			1 => BodyZone.HandArm,
			2 => BodyZone.Trunk,
			_ => BodyZone.LegFoot
		};
	}

	private static int ProjectBodyZone(int sourceIndex, int targetCount, BodyZone zone, int salt)
	{
		var (start, span) = zone switch
		{
			BodyZone.FaceHead => (FaceStart, FaceSpan),
			BodyZone.HandArm => (HandStart, HandSpan),
			BodyZone.Trunk => (TrunkStart, TrunkSpan),
			BodyZone.LegFoot => (LegStart, LegSpan),
			_ => (TrunkStart, TrunkSpan)
		};

		var scaledStart = ScaleToTarget(start, targetCount);
		var scaledSpan = Math.Max(1, ScaleSpanToTarget(span, targetCount));
		var local = PositiveMod(sourceIndex + SmallSalt(sourceIndex, salt), scaledSpan);
		return Math.Clamp(scaledStart + local, 0, Math.Max(0, targetCount - 1));
	}

	private static bool MentionsAny(string text, params string[] terms)
	{
		for (var i = 0; i < terms.Length; i++)
		{
			if (text.Contains(terms[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static int ScaleToTarget(int canonicalIndex, int targetCount)
		=> Math.Clamp((int)MathF.Round(canonicalIndex / 384f * Math.Max(1, targetCount)), 0, Math.Max(0, targetCount - 1));

	private static int ScaleSpanToTarget(int canonicalSpan, int targetCount)
		=> Math.Max(1, (int)MathF.Round(canonicalSpan / 384f * Math.Max(1, targetCount)));

	private static int SmallSalt(int sourceIndex, int salt)
		=> PositiveMod(HashCode.Combine(sourceIndex, salt), 7);

	private enum BodyZone
	{
		FaceHead,
		HandArm,
		Trunk,
		LegFoot
	}
}

internal sealed class PosteriorParietalBodySchemaCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(
			message.SourceNeuronId,
			message.TargetNeuronId,
			message.SynapseId,
			message.SourceStructure,
			message.TargetStructure);
		var bodyZone = ResolveBodyZone(message.SourceStructure, message.SourceNeuronId, message.TargetNeuronId, sourceIndex);
		var spatialZone = ResolveSpatialZone(message.SourceStructure, message.SourceNeuronId, message.TargetNeuronId, sourceIndex);
		return ProjectBodySchema(sourceIndex, neuronCount, bodyZone, spatialZone, message.IsFeedback ? 17 : 13);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		var bodyZone = ResolveBodyZone(StructureId.Ppc, source.Id, string.Empty, source.Index);
		var spatialZone = ResolveSpatialZone(StructureId.Ppc, source.Id, string.Empty, source.Index);
		return ProjectBodySchema(source.Index, Math.Max(16, circuit.TargetMapModulo), bodyZone, spatialZone, 19);
	}

	public override SpikeTypeEnum SelectSpikeType(
		StructureId sourceStructure,
		bool isFeedback,
		NeuromodState localNeuromod,
		float localRewardSignal)
	{
		var attentionGain =
			Math.Max(localNeuromod.AcetylcholineLevel, localNeuromod.NorepinephrineLevel);
		return attentionGain > 0.38f || isFeedback
			? SpikeTypeEnum.BURST
			: SpikeTypeEnum.ACTION_POTENTIAL;
	}

	private static BodyZone ResolveBodyZone(StructureId sourceStructure, string sourceNeuronId, string targetNeuronId, int sourceIndex)
	{
		var text = $"{sourceNeuronId} {targetNeuronId}";
		if (MentionsAny(text, "face", "head", "eye", "mouth", "jaw", "tongue", "neck", "scan", "look", "orient"))
		{
			return BodyZone.FaceHead;
		}

		if (MentionsAny(text, "hand", "arm", "finger", "grasp", "tool", "weapon", "reach", "pick", "carry"))
		{
			return BodyZone.HandArm;
		}

		if (MentionsAny(text, "leg", "foot", "walk", "run", "forward", "turn", "stride", "locomotor", "escape"))
		{
			return BodyZone.LegFoot;
		}

		if (MentionsAny(text, "trunk", "body", "posture", "balance", "torso", "vestibular", "pressure", "pain", "contact"))
		{
			return BodyZone.Trunk;
		}

		return sourceStructure switch
		{
			StructureId.S1 => PositiveMod(sourceIndex, 4) switch
			{
				0 => BodyZone.FaceHead,
				1 => BodyZone.HandArm,
				2 => BodyZone.Trunk,
				_ => BodyZone.LegFoot
			},
			StructureId.M1 or StructureId.PremotorCortex or StructureId.Sma => BodyZone.LegFoot,
			StructureId.VestibularNuclei or StructureId.ReticularFormation or StructureId.CerebellarVermis => BodyZone.Trunk,
			StructureId.Insula or StructureId.NucleusTractusSolitarius or StructureId.Hypothalamus => BodyZone.Trunk,
			_ => PositiveMod(sourceIndex, 4) switch
			{
				0 => BodyZone.FaceHead,
				1 => BodyZone.HandArm,
				2 => BodyZone.Trunk,
				_ => BodyZone.LegFoot
			}
		};
	}

	private static SpatialZone ResolveSpatialZone(StructureId sourceStructure, string sourceNeuronId, string targetNeuronId, int sourceIndex)
	{
		var text = $"{sourceNeuronId} {targetNeuronId}";
		if (MentionsAny(text, "left", "l:"))
		{
			return SpatialZone.LeftPeripersonal;
		}

		if (MentionsAny(text, "right", "r:"))
		{
			return SpatialZone.RightPeripersonal;
		}

		if (MentionsAny(text, "near", "touch", "contact", "pressure", "pain", "grasp", "carry"))
		{
			return SpatialZone.NearBody;
		}

		if (MentionsAny(text, "far", "ahead", "front", "visual", "motion", "spatial", "target"))
		{
			return SpatialZone.FarSpace;
		}

		return sourceStructure switch
		{
			StructureId.S1 or StructureId.Insula or StructureId.NucleusTractusSolitarius => SpatialZone.NearBody,
			StructureId.Mt or StructureId.V1 or StructureId.V2 or StructureId.Pulvinar => SpatialZone.FarSpace,
			StructureId.VestibularNuclei or StructureId.RetrosplenialCortex => SpatialZone.RightPeripersonal,
			_ => PositiveMod(sourceIndex, 4) switch
			{
				0 => SpatialZone.NearBody,
				1 => SpatialZone.LeftPeripersonal,
				2 => SpatialZone.RightPeripersonal,
				_ => SpatialZone.FarSpace
			}
		};
	}

	private static int ProjectBodySchema(int sourceIndex, int targetCount, BodyZone bodyZone, SpatialZone spatialZone, int salt)
	{
		var bodyCount = 4;
		var spatialCount = 4;
		var channelCount = bodyCount * spatialCount;
		var channel = ((int)bodyZone * spatialCount) + (int)spatialZone;
		var cellsPerChannel = Math.Max(1, targetCount / channelCount);
		var local = PositiveMod((sourceIndex / channelCount) + SmallSalt(sourceIndex, salt), Math.Max(1, cellsPerChannel));
		return Math.Clamp((channel * cellsPerChannel) + local, 0, Math.Max(0, targetCount - 1));
	}

	private static bool MentionsAny(string text, params string[] terms)
	{
		for (var i = 0; i < terms.Length; i++)
		{
			if (text.Contains(terms[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static int SmallSalt(int sourceIndex, int salt)
		=> PositiveMod(HashCode.Combine(sourceIndex, salt, 131), 11);

	private enum BodyZone
	{
		FaceHead,
		HandArm,
		Trunk,
		LegFoot
	}

	private enum SpatialZone
	{
		NearBody,
		LeftPeripersonal,
		RightPeripersonal,
		FarSpace
	}
}
