using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AmygdalaSeptalStructuralCompletionTests
{
    public static TheoryData<StructureId> AddedPopulations => new()
    {
        StructureId.BasolateralAmygdala,
        StructureId.CentralAmygdala,
        StructureId.MedialAmygdala,
        StructureId.CorticalAmygdala,
        StructureId.BedNucleusStriaTerminalis,
        StructureId.MedialSeptalNucleus,
        StructureId.DiagonalBandNucleus
    };

    [Fact]
    public void AtlasMetadata_IsExhaustiveAndConcrete()
    {
        Assert.Equal(119, Enum.GetValues<StructureId>().Length);
        Assert.Equal(Enum.GetValues<StructureId>().Length, StructureAtlas.All.Count);

        foreach (var nucleus in new[]
        {
            StructureId.BasolateralAmygdala,
            StructureId.CentralAmygdala,
            StructureId.MedialAmygdala,
            StructureId.CorticalAmygdala,
            StructureId.BedNucleusStriaTerminalis
        })
        {
            var descriptor = StructureAtlas.Get(nucleus);
            Assert.Equal("Amygdala and extended limbic", descriptor.ParentGroup);
            Assert.Equal(StructureAtlasLevel.Nucleus, descriptor.Level);
            Assert.Equal(AnatomicalCardinality.Paired, descriptor.Cardinality);
        }

        Assert.Equal(AnatomicalCardinality.Midline,
            StructureAtlas.Get(StructureId.MedialSeptalNucleus).Cardinality);
        Assert.Equal(AnatomicalCardinality.Paired,
            StructureAtlas.Get(StructureId.DiagonalBandNucleus).Cardinality);
    }

    [Theory]
    [MemberData(nameof(AddedPopulations))]
    public void AddedPopulations_UseSpikingNeuronsAndLocalPlasticity(StructureId structureId)
    {
        Assert.IsType<AmygdalaSeptalCircuitKernel>(CircuitKernelFactory.For(structureId));

        var profile = StructureCircuitProfile.For(structureId);
        Assert.InRange(profile.NeuronCount, 224, 288);
        var expectedNt = structureId switch
        {
            StructureId.CentralAmygdala or
            StructureId.MedialAmygdala or
            StructureId.BedNucleusStriaTerminalis => NTEnum.GABA,
            StructureId.MedialSeptalNucleus or
            StructureId.DiagonalBandNucleus => NTEnum.ACETYLCHOLINE,
            _ => NTEnum.GLUTAMATE
        };
        Assert.Equal(expectedNt, profile.DefaultNt);

        var program = File.ReadAllText(ResolveRepositoryFile(
            "Structures",
            structureId.ToString(),
            "Program.cs"));
        Assert.Contains("\"Izhikevich\"", program, StringComparison.Ordinal);
        Assert.Contains("\"STDP\"", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ML.NET", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("classifier", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prediction engine", program, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Connectome_ContainsSpecificAmygdalaAndSeptohippocampalRoutes()
    {
        var edges = LoadEdges();

        AssertRoute(edges, StructureId.BasolateralAmygdala, StructureId.CentralAmygdala, "basolateral_central_conditioned_drive");
        AssertRoute(edges, StructureId.CentralAmygdala, StructureId.PeriaqueductalGray, "central_amygdala_pag_inhibitory_pattern", NTEnum.GABA);
        AssertRoute(edges, StructureId.CorticalAmygdala, StructureId.BasolateralAmygdala, "cortical_basolateral_olfactory_association");
        AssertRoute(edges, StructureId.MedialAmygdala, StructureId.BedNucleusStriaTerminalis, "medial_amygdala_bnst_olfactory_visceral", NTEnum.GABA);
        AssertRoute(edges, StructureId.BedNucleusStriaTerminalis, StructureId.ParaventricularHypothalamicNucleus, "bnst_pvn_sustained_threat", NTEnum.GABA);
        AssertRoute(edges, StructureId.NucleusBasalis, StructureId.MedialSeptalNucleus, "nucleus_basalis_medial_septal_coordination", NTEnum.ACETYLCHOLINE);
        AssertRoute(edges, StructureId.MedialSeptalNucleus, StructureId.CA1, "medial_septal_ca1_theta", NTEnum.ACETYLCHOLINE);
        AssertRoute(edges, StructureId.DiagonalBandNucleus, StructureId.EntorhinalCortex, "diagonal_band_entorhinal_theta", NTEnum.ACETYLCHOLINE);
        AssertRoute(edges, StructureId.MedialSeptalNucleus, StructureId.DiagonalBandNucleus, "medial_septal_diagonal_band_coordination", NTEnum.GABA);
        AssertRoute(edges, StructureId.DiagonalBandNucleus, StructureId.MedialSeptalNucleus, "diagonal_band_medial_septal_coordination", NTEnum.GABA);

        foreach (var population in AddedPopulations)
        {
            Assert.Contains(edges, edge => edge.Source == population);
            Assert.Contains(edges, edge => edge.Target == population);
        }
    }

    private static void AssertRoute(
        IReadOnlyList<Edge> edges,
        StructureId source,
        StructureId target,
        string projectionType,
        NTEnum neurotransmitter = NTEnum.GLUTAMATE) =>
        Assert.Contains(edges, edge =>
            edge.Source == source &&
            edge.Target == target &&
            edge.ProjectionType == projectionType &&
            edge.Neurotransmitter == neurotransmitter);

    private static IReadOnlyList<Edge> LoadEdges()
    {
        var path = ResolveRepositoryFile("connectivity", "dnne-connectivity.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateArray()
            .SelectMany(rule => rule.GetProperty("connections").EnumerateArray()
                .Select(connection => new Edge(
                    Enum.Parse<StructureId>(rule.GetProperty("source").GetString()!, true),
                    Enum.Parse<StructureId>(connection.GetProperty("target").GetString()!, true),
                    Enum.Parse<NTEnum>(connection.GetProperty("neurotransmitter").GetString()!, true),
                    connection.GetProperty("projectionType").GetString()!)))
            .ToArray();
    }

    private static string ResolveRepositoryFile(params string[] segments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, segments));
    }

    private sealed record Edge(
        StructureId Source,
        StructureId Target,
        NTEnum Neurotransmitter,
        string ProjectionType);
}
