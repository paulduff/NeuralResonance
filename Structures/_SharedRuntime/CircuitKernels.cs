using System;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal interface ICircuitKernel
{
	int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit);

	int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit);

	SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal);
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

	private static readonly ICircuitKernel HomuncularSensorimotor = new HomuncularSensorimotorCircuitKernel();

	private static readonly ICircuitKernel PosteriorParietalBodySchema = new PosteriorParietalBodySchemaCircuitKernel();

	public static ICircuitKernel For(StructureId structureId)
	{
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
}

internal abstract class CircuitKernelBase : ICircuitKernel
{
	public virtual int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		return TopographicMap.ProjectLinear(sourceIndex, neuronCount, message.SourceStructure, message.TargetStructure, message.IsFeedback ? 1 : 0);
	}

	public virtual int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		return TopographicMap.ProjectLinear(source.Index, Math.Max(16, circuit.TargetMapModulo), targetStructure, targetStructure, circuit.TargetMapStride);
	}

	public virtual SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal)
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
		if (IsAuditory(message.SourceStructure) || IsAuditory(message.TargetStructure))
		{
			return TopographicMap.ProjectChannel(sourceIndex, neuronCount, 32, 8, message.SourceStructure, message.TargetStructure, 11);
		}
		return TopographicMap.ProjectGrid(sourceIndex, neuronCount, 32, 32, message.SourceStructure, message.TargetStructure, 13);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		int targetCount = Math.Max(16, circuit.TargetMapModulo);
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
		int offset = message.IsFeedback ? 1 : 0;
		return TopographicMap.ProjectLayeredColumn(sourceIndex + offset, neuronCount, 3, message.SourceStructure, message.TargetStructure, 29);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		return TopographicMap.ProjectLayeredColumn(source.Index, Math.Max(16, circuit.TargetMapModulo), 3, targetStructure, targetStructure, 31);
	}

	public override SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal)
	{
		if (!isFeedback)
		{
			return SpikeTypeEnum.ACTION_POTENTIAL;
		}
		return (tickSignal.GlobalNeuromodState.NorepinephrineLevel > 0.35f) ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL;
	}
}

internal sealed class HippocampalCircuitKernel : CircuitKernelBase
{
	public override int ResolveInboundNeuronIndex(SpikeMessage message, int neuronCount, StructureCircuitProfile circuit)
	{
		int sourceIndex = TopographicMap.ResolveSignalIndex(message.SourceNeuronId, message.TargetNeuronId, message.SynapseId, message.SourceStructure, message.TargetStructure);
		return TopographicMap.ProjectLayeredColumn(sourceIndex, neuronCount, 4, message.SourceStructure, message.TargetStructure, HippocampalStageOffset(message.TargetStructure));
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		return TopographicMap.ProjectLayeredColumn(source.Index, Math.Max(16, circuit.TargetMapModulo), 4, targetStructure, targetStructure, HippocampalStageOffset(targetStructure));
	}

	public override SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal)
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
		return TopographicMap.ProjectChannel(sourceIndex, neuronCount, 32, 6, message.SourceStructure, message.TargetStructure, message.IsFeedback ? 43 : 41);
	}

	public override int ResolveOutboundTargetIndex(ModelNeuron source, StructureId targetStructure, StructureCircuitProfile circuit)
	{
		return TopographicMap.ProjectChannel(source.Index, Math.Max(16, circuit.TargetMapModulo), 32, 6, targetStructure, targetStructure, 47);
	}

	public override SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal)
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

	public override SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal)
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

	public override SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal)
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

	public override SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal)
	{
		if (sourceStructure == StructureId.M1 && tickSignal.GlobalNeuromodState.DopamineLevel > 0.42f)
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

	public override SpikeTypeEnum SelectSpikeType(StructureId sourceStructure, bool isFeedback, TickSignal tickSignal)
	{
		var attentionGain =
			Math.Max(tickSignal.GlobalNeuromodState.AcetylcholineLevel, tickSignal.GlobalNeuromodState.NorepinephrineLevel);
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
