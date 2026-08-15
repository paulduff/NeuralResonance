namespace NeuralResonanceEngine.Protocol;

public enum StructureAtlasLevel
{
    Region,
    Area,
    Nucleus,
    Layer,
    Tract,
    Interface
}

public enum AnatomicalCardinality
{
    Paired,
    Midline,
    CrossHemisphere,
    Distributed
}

public sealed record StructureAtlasDescriptor(
    StructureId StructureId,
    string ParentGroup,
    StructureAtlasLevel Level,
    AnatomicalCardinality Cardinality);

public static class StructureAtlas
{
    private static readonly IReadOnlyDictionary<StructureId, StructureAtlasDescriptor> Descriptors = Build();

    public static IReadOnlyCollection<StructureAtlasDescriptor> All => Descriptors.Values.ToArray();

    public static StructureAtlasDescriptor Get(StructureId structureId) => Descriptors[structureId];

    private static IReadOnlyDictionary<StructureId, StructureAtlasDescriptor> Build()
    {
        var result = new Dictionary<StructureId, StructureAtlasDescriptor>();

        Add(result, "Sensory interface", StructureAtlasLevel.Interface, AnatomicalCardinality.Paired,
            StructureId.Retina, StructureId.Cochlea, StructureId.SomaticAfferents,
            StructureId.ProprioceptiveAfferents, StructureId.VestibularAfferents,
            StructureId.VisceralAfferents);

        Add(result, "Neocortex", StructureAtlasLevel.Area, AnatomicalCardinality.Paired,
            StructureId.V1, StructureId.V2, StructureId.V3, StructureId.V4, StructureId.Mt,
            StructureId.A1, StructureId.AuditoryAssociationCortex, StructureId.S1,
            StructureId.SecondarySomatosensoryCortex, StructureId.Pfc,
            StructureId.DorsomedialPrefrontalCortex, StructureId.VentromedialPrefrontalCortex,
            StructureId.FrontalEyeFields, StructureId.BrocaBa44Ba45, StructureId.WernickePstgPsts,
            StructureId.SupramarginalAngular, StructureId.OrbitofrontalCortex, StructureId.Insula,
            StructureId.Ppc, StructureId.TemporalAssociation, StructureId.InferotemporalCortex,
            StructureId.FusiformGyrus, StructureId.TemporalPole, StructureId.TemporoparietalJunction,
            StructureId.Precuneus, StructureId.MidcingulateCortex, StructureId.PremotorCortex,
            StructureId.ParahippocampalCortex, StructureId.PerirhinalCortex,
            StructureId.PosteriorCingulate, StructureId.RetrosplenialCortex, StructureId.Acc,
            StructureId.M1, StructureId.Sma);

        Add(result, "Thalamus", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.Trn, StructureId.Pulvinar, StructureId.MediodorsalThalamus,
            StructureId.IntralaminarThalamus, StructureId.MotorThalamus,
            StructureId.LateralGeniculateNucleus, StructureId.MedialGeniculateNucleus,
            StructureId.VentralPosterolateralThalamus, StructureId.VentralPosteromedialThalamus,
            StructureId.AnteriorThalamicNuclei);
        Add(result, "Thalamus", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Midline,
            StructureId.NucleusReuniens);

        Add(result, "Medial temporal lobe", StructureAtlasLevel.Area, AnatomicalCardinality.Paired,
            StructureId.EntorhinalCortex, StructureId.DentateGyrus, StructureId.CA3,
            StructureId.CA2, StructureId.CA1, StructureId.Subiculum,
            StructureId.Presubiculum, StructureId.Parasubiculum);

        Add(result, "Basal ganglia", StructureAtlasLevel.Region, AnatomicalCardinality.Paired,
            StructureId.Striatum);
        Add(result, "Basal ganglia", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.GPe, StructureId.GPi, StructureId.Stn, StructureId.Snr, StructureId.Snc,
            StructureId.NucleusAccumbens, StructureId.VentralPallidum);

        Add(result, "Hypothalamus", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.VentrolateralPreopticNucleus, StructureId.SuprachiasmaticNucleus,
            StructureId.ParaventricularHypothalamicNucleus, StructureId.SupraopticNucleus,
            StructureId.ArcuateNucleus, StructureId.LateralHypothalamicArea,
            StructureId.VentromedialHypothalamicNucleus,
            StructureId.DorsomedialHypothalamicNucleus, StructureId.MammillaryBodies);
        Add(result, "Amygdala and extended limbic", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.BasolateralAmygdala, StructureId.CentralAmygdala,
            StructureId.MedialAmygdala, StructureId.CorticalAmygdala,
            StructureId.BedNucleusStriaTerminalis);
        Add(result, "Septal basal forebrain", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Midline,
            StructureId.MedialSeptalNucleus);
        Add(result, "Septal basal forebrain", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.DiagonalBandNucleus);
        Add(result, "Limbic and homeostatic", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.Habenula);

        Add(result, "Cerebellum", StructureAtlasLevel.Layer, AnatomicalCardinality.Midline,
            StructureId.CerebellarGranule, StructureId.PurkinjeCellLayer);
        Add(result, "Cerebellum", StructureAtlasLevel.Area, AnatomicalCardinality.Midline,
            StructureId.CerebellarVermis, StructureId.CerebellarLobules);
        Add(result, "Cerebellum", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.DentateNucleus, StructureId.InterposedNuclei, StructureId.FastigialNucleus);

        Add(result, "Brainstem", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.CochlearNucleus, StructureId.SuperiorOlive, StructureId.InferiorColliculus,
            StructureId.VestibularNuclei, StructureId.NucleusTractusSolitarius,
            StructureId.InferiorOlive, StructureId.SuperiorColliculus,
            StructureId.RedNucleus, StructureId.PontineNuclei, StructureId.PedunculopontineNucleus,
            StructureId.LaterodorsalTegmentalNucleus, StructureId.ParabrachialComplex,
            StructureId.PrincipalSensoryTrigeminalNucleus, StructureId.SpinalTrigeminalNucleus,
            StructureId.MesencephalicTrigeminalNucleus, StructureId.FacialMotorNucleus,
            StructureId.OculomotorNucleus, StructureId.HypoglossalNucleus);
        Add(result, "Brainstem", StructureAtlasLevel.Region, AnatomicalCardinality.Midline,
            StructureId.ReticularFormation, StructureId.PeriaqueductalGray);
        Add(result, "Spinal cord", StructureAtlasLevel.Region, AnatomicalCardinality.Paired,
            StructureId.SpinalCordMotor);

        Add(result, "Neuromodulatory systems", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.LocusCoeruleus, StructureId.NucleusBasalis, StructureId.Vta);
        Add(result, "Neuromodulatory systems", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Midline,
            StructureId.RapheNuclei);

        Add(result, "Olfactory system", StructureAtlasLevel.Nucleus, AnatomicalCardinality.Paired,
            StructureId.OlfactoryBulb);
        Add(result, "White matter", StructureAtlasLevel.Tract, AnatomicalCardinality.CrossHemisphere,
            StructureId.CorpusCallosum);
        Add(result, "White matter", StructureAtlasLevel.Tract, AnatomicalCardinality.Paired,
            StructureId.ArcuateFasciculus);

        var missing = Enum.GetValues<StructureId>().Where(id => !result.ContainsKey(id)).ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidOperationException($"Structure atlas metadata is missing: {string.Join(", ", missing)}");
        }

        return result;
    }

    private static void Add(
        IDictionary<StructureId, StructureAtlasDescriptor> descriptors,
        string parentGroup,
        StructureAtlasLevel level,
        AnatomicalCardinality cardinality,
        params StructureId[] structureIds)
    {
        foreach (var structureId in structureIds)
        {
            descriptors.Add(structureId, new StructureAtlasDescriptor(structureId, parentGroup, level, cardinality));
        }
    }
}
