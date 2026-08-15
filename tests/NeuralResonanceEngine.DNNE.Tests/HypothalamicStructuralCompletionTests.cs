using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HypothalamicStructuralCompletionTests
{
    public static TheoryData<StructureId> AddedHypothalamicNuclei => new()
    {
        StructureId.VentrolateralPreopticNucleus,
        StructureId.SuprachiasmaticNucleus,
        StructureId.ParaventricularHypothalamicNucleus,
        StructureId.SupraopticNucleus,
        StructureId.ArcuateNucleus,
        StructureId.LateralHypothalamicArea,
        StructureId.VentromedialHypothalamicNucleus,
        StructureId.DorsomedialHypothalamicNucleus,
        StructureId.MammillaryBodies
    };

    [Fact]
    public void AtlasMetadata_IsExhaustiveAndConcrete()
    {
        Assert.Equal(119, Enum.GetValues<StructureId>().Length);
        Assert.Equal(Enum.GetValues<StructureId>().Length, StructureAtlas.All.Count);

        foreach (var nucleus in AddedHypothalamicNuclei)
        {
            var descriptor = StructureAtlas.Get(nucleus);
            Assert.Equal("Hypothalamus", descriptor.ParentGroup);
            Assert.Equal(StructureAtlasLevel.Nucleus, descriptor.Level);
            Assert.Equal(AnatomicalCardinality.Paired, descriptor.Cardinality);
        }
    }

    [Theory]
    [MemberData(nameof(AddedHypothalamicNuclei))]
    public void AddedNuclei_UseSpikingNeuronsAndLocalPlasticity(StructureId structureId)
    {
        Assert.IsType<HypothalamicCircuitKernel>(CircuitKernelFactory.For(structureId));

        var profile = StructureCircuitProfile.For(structureId);
        Assert.InRange(profile.NeuronCount, 192, 320);
        var expectedNt = structureId is StructureId.VentrolateralPreopticNucleus or StructureId.SuprachiasmaticNucleus
            ? NTEnum.GABA
            : NTEnum.GLUTAMATE;
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
    public void Connectome_ContainsCircadianHomeostaticAutonomicAndMemoryRoutes()
    {
        var edges = LoadEdges();

        AssertRoute(edges, StructureId.Retina, StructureId.SuprachiasmaticNucleus, "retinohypothalamic_circadian_input");
        AssertRoute(edges, StructureId.SuprachiasmaticNucleus, StructureId.VentrolateralPreopticNucleus, "scn_vlpo_circadian_gate", NTEnum.GABA);
        AssertRoute(edges, StructureId.VentrolateralPreopticNucleus, StructureId.LocusCoeruleus, "vlpo_locus_coeruleus_sleep_inhibition", NTEnum.GABA);
        AssertRoute(edges, StructureId.NucleusTractusSolitarius, StructureId.ParaventricularHypothalamicNucleus, "nts_pvn_autonomic_input");
        AssertRoute(edges, StructureId.ParaventricularHypothalamicNucleus, StructureId.NucleusTractusSolitarius, "pvn_solitary_autonomic_command");
        AssertRoute(edges, StructureId.ArcuateNucleus, StructureId.LateralHypothalamicArea, "arcuate_lha_satiety_inhibition", NTEnum.GABA);
        AssertRoute(edges, StructureId.LateralHypothalamicArea, StructureId.ReticularFormation, "lha_reticular_motivational_drive");
        AssertRoute(edges, StructureId.VentromedialHypothalamicNucleus, StructureId.PeriaqueductalGray, "vmh_pag_defense_recruitment");
        AssertRoute(edges, StructureId.DorsomedialHypothalamicNucleus, StructureId.LocusCoeruleus, "dmh_locus_coeruleus_arousal");
        AssertRoute(edges, StructureId.Subiculum, StructureId.MammillaryBodies, "subiculum_mammillary_papez_input");
        AssertRoute(edges, StructureId.MammillaryBodies, StructureId.AnteriorThalamicNuclei, "mammillothalamic_memory_relay");

        foreach (var nucleus in AddedHypothalamicNuclei)
        {
            Assert.Contains(edges, edge => edge.Source == nucleus);
            Assert.Contains(edges, edge => edge.Target == nucleus);
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
