using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ThalamicStructuralCompletionTests
{
    public static TheoryData<StructureId> AddedThalamicNuclei => new()
    {
        StructureId.LateralGeniculateNucleus,
        StructureId.MedialGeniculateNucleus,
        StructureId.VentralPosterolateralThalamus,
        StructureId.VentralPosteromedialThalamus,
        StructureId.AnteriorThalamicNuclei,
        StructureId.NucleusReuniens
    };

    [Fact]
    public void AtlasMetadata_IsExhaustiveAndConcrete()
    {
        Assert.Equal(119, Enum.GetValues<StructureId>().Length);
        Assert.Equal(Enum.GetValues<StructureId>().Length, StructureAtlas.All.Count);
        Assert.Equal(AnatomicalCardinality.Midline,
            StructureAtlas.Get(StructureId.NucleusReuniens).Cardinality);
        Assert.Equal(StructureAtlasLevel.Nucleus,
            StructureAtlas.Get(StructureId.LateralGeniculateNucleus).Level);
    }

    [Theory]
    [MemberData(nameof(AddedThalamicNuclei))]
    public void AddedNuclei_UseSpikingThalamicKernel(StructureId structureId)
    {
        Assert.IsType<ThalamicCircuitKernel>(CircuitKernelFactory.For(structureId));

        var profile = StructureCircuitProfile.For(structureId);
        Assert.InRange(profile.NeuronCount, 288, 384);
        Assert.Equal(NTEnum.GLUTAMATE, profile.DefaultNt);

        var program = File.ReadAllText(ResolveRepositoryFile(
            "Structures",
            structureId.ToString(),
            "Program.cs"));
        Assert.Contains("\"Izhikevich\"", program, StringComparison.Ordinal);
        Assert.Contains("\"STDP\"", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ML.NET", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("classifier", program, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Connectome_ContainsSpecificRelayAndReticularRoutes()
    {
        var edges = LoadEdges();

        AssertRoute(edges, StructureId.Retina, StructureId.LateralGeniculateNucleus, "retinogeniculate_relay");
        AssertRoute(edges, StructureId.LateralGeniculateNucleus, StructureId.V1, "lgn_v1_retinotopic_relay");
        AssertRoute(edges, StructureId.InferiorColliculus, StructureId.MedialGeniculateNucleus, "colliculo_mgn_auditory_relay");
        AssertRoute(edges, StructureId.MedialGeniculateNucleus, StructureId.A1, "mgn_a1_tonotopic_relay");
        AssertRoute(edges, StructureId.SomaticAfferents, StructureId.VentralPosterolateralThalamus, "somatic_vpl_lemniscal_afference");
        AssertRoute(edges, StructureId.VentralPosterolateralThalamus, StructureId.S1, "vpl_s1_somatotopic_relay");
        AssertRoute(edges, StructureId.NucleusTractusSolitarius, StructureId.VentralPosteromedialThalamus, "solitary_vpm_visceral_relay");
        AssertRoute(edges, StructureId.Subiculum, StructureId.AnteriorThalamicNuclei, "subiculum_anterior_thalamic_context");
        AssertRoute(edges, StructureId.Pfc, StructureId.NucleusReuniens, "prefrontal_reuniens_control");

        foreach (var nucleus in AddedThalamicNuclei)
        {
            Assert.Contains(edges, edge =>
                edge.Source == StructureId.Trn &&
                edge.Target == nucleus &&
                edge.Neurotransmitter == NTEnum.GABA);
        }
    }

    private static void AssertRoute(
        IReadOnlyList<Edge> edges,
        StructureId source,
        StructureId target,
        string projectionType) =>
        Assert.Contains(edges, edge =>
            edge.Source == source && edge.Target == target && edge.ProjectionType == projectionType);

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
