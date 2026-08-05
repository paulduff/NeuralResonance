using NeuralResonanceEngine.Protocol;

internal sealed class StructureCircuitProfile
{
	public StructureId StructureId { get; }

	public int NeuronCount { get; }

	public StructureId DefaultTarget { get; }

	public NTEnum DefaultNt { get; }

	public double ExcitabilityScale { get; }

	public double InhibitoryScale { get; }

	public double TonicDrive { get; }

	public double FeedbackAttenuation { get; }

	public int TargetMapStride { get; }

	public int TargetMapModulo { get; }

	private StructureCircuitProfile(
		StructureId structureId,
		int neuronCount,
		StructureId defaultTarget,
		NTEnum defaultNt,
		double excitabilityScale,
		double inhibitoryScale,
		double tonicDrive,
		double feedbackAttenuation,
		int targetMapStride,
		int targetMapModulo)
	{
		StructureId = structureId;
		NeuronCount = neuronCount;
		DefaultTarget = defaultTarget;
		DefaultNt = defaultNt;
		ExcitabilityScale = excitabilityScale;
		InhibitoryScale = inhibitoryScale;
		TonicDrive = tonicDrive;
		FeedbackAttenuation = feedbackAttenuation;
		TargetMapStride = targetMapStride;
		TargetMapModulo = targetMapModulo;
	}

	public static StructureCircuitProfile For(StructureId structureId)
	{
		(int neuronCount, double excitability, double inhibitory, double tonic, double feedbackAttenuation, int stride, int modulo) = GetDynamics(structureId);
		return new StructureCircuitProfile(
			structureId,
			neuronCount,
			GetDefaultTarget(structureId),
			GetDefaultNt(structureId),
			excitability,
			inhibitory,
			tonic,
			feedbackAttenuation,
			stride,
			modulo);
	}

	private static (int NeuronCount, double Excitability, double Inhibitory, double Tonic, double FeedbackAttenuation, int Stride, int Modulo) GetDynamics(StructureId structureId)
	{
		switch (structureId)
		{
		case StructureId.V1:
		case StructureId.V2:
		case StructureId.V3:
		case StructureId.V4:
		case StructureId.Mt:
		case StructureId.A1:
		case StructureId.S1:
		case StructureId.AuditoryAssociationCortex:
		case StructureId.SecondarySomatosensoryCortex:
		case StructureId.Pfc:
		case StructureId.DorsomedialPrefrontalCortex:
		case StructureId.VentromedialPrefrontalCortex:
		case StructureId.FrontalEyeFields:
		case StructureId.BrocaBa44Ba45:
		case StructureId.WernickePstgPsts:
		case StructureId.OrbitofrontalCortex:
		case StructureId.Insula:
		case StructureId.Ppc:
		case StructureId.TemporalAssociation:
		case StructureId.InferotemporalCortex:
		case StructureId.FusiformGyrus:
		case StructureId.TemporalPole:
		case StructureId.TemporoparietalJunction:
		case StructureId.Precuneus:
		case StructureId.MidcingulateCortex:
		case StructureId.PremotorCortex:
		case StructureId.ParahippocampalCortex:
		case StructureId.PerirhinalCortex:
		case StructureId.PosteriorCingulate:
		case StructureId.RetrosplenialCortex:
		case StructureId.Acc:
		case StructureId.M1:
		case StructureId.Sma:
			return (384, 1.06, 1.02, 1.06, 0.9, 17, 256);
		case StructureId.Thalamus:
		case StructureId.Trn:
		case StructureId.Pulvinar:
		case StructureId.MediodorsalThalamus:
		case StructureId.IntralaminarThalamus:
		case StructureId.MotorThalamus:
			return (320, 0.98, 1.10, 0.96, 0.88, 19, 224);
		case StructureId.EntorhinalCortex:
		case StructureId.DentateGyrus:
		case StructureId.CA3:
		case StructureId.CA2:
		case StructureId.CA1:
		case StructureId.Subiculum:
		case StructureId.Presubiculum:
		case StructureId.Parasubiculum:
			return (288, 1.08, 1.00, 1.12, 0.9, 23, 192);
		case StructureId.CerebellarGranule:
			return (512, 1.02, 1.05, 1.04, 0.9, 31, 320);
		case StructureId.CerebellarVermis:
		case StructureId.CerebellarLobules:
			return (352, 1.00, 1.08, 0.98, 0.89, 27, 288);
		case StructureId.PurkinjeCellLayer:
			return (192, 0.86, 1.30, 0.80, 0.84, 29, 224);
		case StructureId.DeepCerebellarNuclei:
			return (224, 1.03, 1.02, 1.00, 0.9, 21, 224);
		case StructureId.InferiorOlive:
			return (192, 0.92, 1.12, 0.86, 0.86, 15, 192);
		case StructureId.Striatum:
			return (320, 0.88, 1.28, 0.78, 0.85, 13, 224);
		case StructureId.GlobusPallidus:
		case StructureId.GPe:
		case StructureId.GPi:
		case StructureId.Stn:
		case StructureId.Snr:
		case StructureId.NucleusAccumbens:
		case StructureId.VentralPallidum:
		case StructureId.Habenula:
			return (224, 0.90, 1.24, 0.82, 0.86, 13, 192);
		case StructureId.Snc:
		case StructureId.Vta:
		case StructureId.LocusCoeruleus:
		case StructureId.RapheNuclei:
		case StructureId.BasalForebrain:
			return (160, 0.86, 1.18, 0.74, 0.92, 11, 160);
		case StructureId.Pons:
		case StructureId.Medulla:
		case StructureId.SuperiorColliculus:
		case StructureId.Hypothalamus:
			return (224, 0.94, 1.10, 0.90, 0.9, 15, 192);
		default:
			return (256, 1.0, 1.0, 1.0, 0.9, 17, 192);
		}
	}

	private static NTEnum GetDefaultNt(StructureId structureId)
	{
		switch (structureId)
		{
		case StructureId.Striatum:
		case StructureId.GPe:
		case StructureId.GPi:
		case StructureId.Snr:
		case StructureId.Trn:
		case StructureId.PurkinjeCellLayer:
		case StructureId.VentralPallidum:
		case StructureId.GlobusPallidus:
		case StructureId.Habenula:
			return NTEnum.GABA;
		case StructureId.LocusCoeruleus:
			return NTEnum.NOREPINEPHRINE;
		case StructureId.RapheNuclei:
			return NTEnum.SEROTONIN;
		case StructureId.BasalForebrain:
			return NTEnum.ACETYLCHOLINE;
		case StructureId.Snc:
		case StructureId.Vta:
			return NTEnum.DOPAMINE;
		default:
			return NTEnum.GLUTAMATE;
		}
	}

	private static StructureId GetDefaultTarget(StructureId structureId)
	{
		switch (structureId)
		{
		case StructureId.V1:
			return StructureId.V2;
		case StructureId.V2:
			return StructureId.V3;
		case StructureId.V3:
			return StructureId.V4;
		case StructureId.V4:
			return StructureId.TemporalAssociation;
		case StructureId.Mt:
			return StructureId.Ppc;
		case StructureId.A1:
			return StructureId.AuditoryAssociationCortex;
		case StructureId.AuditoryAssociationCortex:
			return StructureId.WernickePstgPsts;
		case StructureId.S1:
			return StructureId.SecondarySomatosensoryCortex;
		case StructureId.SecondarySomatosensoryCortex:
			return StructureId.Ppc;
		case StructureId.OlfactoryBulb:
			return StructureId.TemporalAssociation;
		case StructureId.Thalamus:
			return StructureId.Pfc;
		case StructureId.Trn:
			return StructureId.Thalamus;
		case StructureId.Pulvinar:
			return StructureId.Ppc;
		case StructureId.MediodorsalThalamus:
			return StructureId.Pfc;
		case StructureId.IntralaminarThalamus:
			return StructureId.Striatum;
		case StructureId.MotorThalamus:
			return StructureId.M1;
		case StructureId.EntorhinalCortex:
			return StructureId.DentateGyrus;
		case StructureId.DentateGyrus:
			return StructureId.CA3;
		case StructureId.CA3:
			return StructureId.CA1;
		case StructureId.CA2:
			return StructureId.CA1;
		case StructureId.CA1:
			return StructureId.Subiculum;
		case StructureId.Subiculum:
			return StructureId.EntorhinalCortex;
		case StructureId.Presubiculum:
		case StructureId.Parasubiculum:
		case StructureId.ParahippocampalCortex:
		case StructureId.PerirhinalCortex:
			return StructureId.EntorhinalCortex;
		case StructureId.Pfc:
		case StructureId.OrbitofrontalCortex:
			return StructureId.Ppc;
		case StructureId.DorsomedialPrefrontalCortex:
			return StructureId.Pfc;
		case StructureId.VentromedialPrefrontalCortex:
			return StructureId.OrbitofrontalCortex;
		case StructureId.FrontalEyeFields:
			return StructureId.SuperiorColliculus;
		case StructureId.Ppc:
			return StructureId.Pfc;
		case StructureId.TemporalAssociation:
			return StructureId.Pfc;
		case StructureId.InferotemporalCortex:
		case StructureId.FusiformGyrus:
			return StructureId.TemporalAssociation;
		case StructureId.TemporalPole:
		case StructureId.TemporoparietalJunction:
			return StructureId.Pfc;
		case StructureId.Precuneus:
			return StructureId.PosteriorCingulate;
		case StructureId.MidcingulateCortex:
			return StructureId.Acc;
		case StructureId.BrocaBa44Ba45:
			return StructureId.Sma;
		case StructureId.WernickePstgPsts:
			return StructureId.ArcuateFasciculus;
		case StructureId.ArcuateFasciculus:
		case StructureId.SupramarginalAngular:
			return StructureId.BrocaBa44Ba45;
		case StructureId.PremotorCortex:
		case StructureId.Sma:
			return StructureId.M1;
		case StructureId.M1:
			return StructureId.Pons;
		case StructureId.Striatum:
			return StructureId.GPi;
		case StructureId.GlobusPallidus:
		case StructureId.GPi:
			return StructureId.Snr;
		case StructureId.GPe:
			return StructureId.Stn;
		case StructureId.Stn:
			return StructureId.GPi;
		case StructureId.Snr:
			return StructureId.Thalamus;
		case StructureId.Snc:
			return StructureId.Striatum;
		case StructureId.NucleusAccumbens:
			return StructureId.VentralPallidum;
		case StructureId.VentralPallidum:
			return StructureId.MediodorsalThalamus;
		case StructureId.Habenula:
			return StructureId.Vta;
		case StructureId.Amygdala:
			return StructureId.Pfc;
		case StructureId.Acc:
		case StructureId.Insula:
			return StructureId.Pfc;
		case StructureId.Hypothalamus:
			return StructureId.Pons;
		case StructureId.CerebellarGranule:
		case StructureId.CerebellarLobules:
		case StructureId.InferiorOlive:
			return StructureId.PurkinjeCellLayer;
		case StructureId.CerebellarVermis:
		case StructureId.PurkinjeCellLayer:
			return StructureId.DeepCerebellarNuclei;
		case StructureId.DeepCerebellarNuclei:
			return StructureId.MotorThalamus;
		case StructureId.Pons:
			return StructureId.CerebellarGranule;
		case StructureId.Medulla:
			return StructureId.Pons;
		case StructureId.LocusCoeruleus:
		case StructureId.RapheNuclei:
		case StructureId.BasalForebrain:
		case StructureId.Vta:
			return StructureId.Pfc;
		case StructureId.PosteriorCingulate:
			return StructureId.RetrosplenialCortex;
		case StructureId.RetrosplenialCortex:
			return StructureId.Ppc;
		case StructureId.SuperiorColliculus:
			return StructureId.MotorThalamus;
		case StructureId.CorpusCallosum:
			return StructureId.Pfc;
		default:
			return StructureId.Thalamus;
		}
	}
}
