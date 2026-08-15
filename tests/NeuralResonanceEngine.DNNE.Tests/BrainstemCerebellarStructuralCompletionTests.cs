using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class BrainstemCerebellarStructuralCompletionTests
{
    private static readonly StructureId[] BrainstemIds =
    [
        StructureId.RedNucleus,
        StructureId.PedunculopontineNucleus,
        StructureId.LaterodorsalTegmentalNucleus,
        StructureId.ParabrachialComplex,
        StructureId.PrincipalSensoryTrigeminalNucleus,
        StructureId.SpinalTrigeminalNucleus,
        StructureId.MesencephalicTrigeminalNucleus,
        StructureId.FacialMotorNucleus,
        StructureId.OculomotorNucleus,
        StructureId.HypoglossalNucleus
    ];

    private static readonly StructureId[] DeepCerebellarIds =
    [
        StructureId.DentateNucleus,
        StructureId.InterposedNuclei,
        StructureId.FastigialNucleus
    ];

    public static TheoryData<StructureId> BrainstemPopulations => new()
    {
        StructureId.RedNucleus,
        StructureId.PedunculopontineNucleus,
        StructureId.LaterodorsalTegmentalNucleus,
        StructureId.ParabrachialComplex,
        StructureId.PrincipalSensoryTrigeminalNucleus,
        StructureId.SpinalTrigeminalNucleus,
        StructureId.MesencephalicTrigeminalNucleus,
        StructureId.FacialMotorNucleus,
        StructureId.OculomotorNucleus,
        StructureId.HypoglossalNucleus
    };

    public static TheoryData<StructureId> DeepCerebellarPopulations => new()
    {
        StructureId.DentateNucleus,
        StructureId.InterposedNuclei,
        StructureId.FastigialNucleus
    };

    [Fact]
    public void AtlasMetadata_IsExhaustiveAndConcrete()
    {
        Assert.Equal(119, Enum.GetValues<StructureId>().Length);
        Assert.Equal(119, StructureAtlas.All.Count);

        foreach (var population in BrainstemIds)
        {
            var descriptor = StructureAtlas.Get(population);
            Assert.Equal("Brainstem", descriptor.ParentGroup);
            Assert.Equal(StructureAtlasLevel.Nucleus, descriptor.Level);
            Assert.Equal(AnatomicalCardinality.Paired, descriptor.Cardinality);
        }

        foreach (var population in DeepCerebellarIds)
        {
            var descriptor = StructureAtlas.Get(population);
            Assert.Equal("Cerebellum", descriptor.ParentGroup);
            Assert.Equal(StructureAtlasLevel.Nucleus, descriptor.Level);
            Assert.Equal(AnatomicalCardinality.Paired, descriptor.Cardinality);
        }
    }

    [Theory]
    [MemberData(nameof(BrainstemPopulations))]
    public void BrainstemPopulations_UseDedicatedSpikingKernel(StructureId structureId)
    {
        Assert.IsType<BrainstemCranialCircuitKernel>(CircuitKernelFactory.For(structureId));
        AssertNeuronalService(structureId);
    }

    [Theory]
    [MemberData(nameof(DeepCerebellarPopulations))]
    public void DeepCerebellarPopulations_UseCerebellarSpikingKernel(StructureId structureId)
    {
        Assert.IsType<CerebellarCircuitKernel>(CircuitKernelFactory.For(structureId));
        AssertNeuronalService(structureId);
    }

    [Fact]
    public void TegmentalNuclei_ContainSeparateWakeAndRemPopulations()
    {
        foreach (var structureId in new[]
        {
            StructureId.PedunculopontineNucleus,
            StructureId.LaterodorsalTegmentalNucleus
        })
        {
            Assert.Equal(SleepConsolidationTopology.WakeChannel,
                SleepConsolidationTopology.StateChannelForNeuron(0, structureId));
            Assert.Equal(SleepConsolidationTopology.RemChannel,
                SleepConsolidationTopology.StateChannelForNeuron(1, structureId));

            SleepConsolidationTopology.ResolveIntrinsicDrive(
                structureId, 1, sleepDrive: 0.8f, wakeReserve: 0.2f,
                out var excitatory, out var inhibitory);
            Assert.True(excitatory > inhibitory);
        }
    }

    [Fact]
    public void Connectome_ContainsBrainstemCranialAndCerebellarRoutes()
    {
        var edges = LoadEdges();

        AssertRoute(edges, StructureId.RedNucleus, StructureId.SpinalCordMotor, "rubrospinal_motor_correction");
        AssertRoute(edges, StructureId.PedunculopontineNucleus, StructureId.IntralaminarThalamus, "ppn_intralaminar_arousal", NTEnum.ACETYLCHOLINE);
        AssertRoute(edges, StructureId.LaterodorsalTegmentalNucleus, StructureId.Vta, "ldt_vta_salience_state", NTEnum.ACETYLCHOLINE);
        AssertRoute(edges, StructureId.ParabrachialComplex, StructureId.CentralAmygdala, "parabrachial_central_amygdala_alarm");
        AssertRoute(edges, StructureId.PrincipalSensoryTrigeminalNucleus, StructureId.VentralPosteromedialThalamus, "principal_trigeminal_vpm_touch_relay");
        AssertRoute(edges, StructureId.SpinalTrigeminalNucleus, StructureId.PeriaqueductalGray, "spinal_trigeminal_pag_defense");
        AssertRoute(edges, StructureId.MesencephalicTrigeminalNucleus, StructureId.CerebellarGranule, "mesencephalic_trigeminal_cerebellar_proprioception");
        AssertRoute(edges, StructureId.FacialMotorNucleus, StructureId.SpinalCordMotor, "facial_motor_efference_bridge", NTEnum.ACETYLCHOLINE);
        AssertRoute(edges, StructureId.OculomotorNucleus, StructureId.VestibularNuclei, "oculomotor_vestibular_corollary", NTEnum.ACETYLCHOLINE);
        AssertRoute(edges, StructureId.HypoglossalNucleus, StructureId.NucleusTractusSolitarius, "hypoglossal_solitary_oral_coordination", NTEnum.ACETYLCHOLINE);
        AssertRoute(edges, StructureId.DentateNucleus, StructureId.MotorThalamus, "dentatothalamic_motor_planning");
        AssertRoute(edges, StructureId.InterposedNuclei, StructureId.RedNucleus, "interpositorubral_limb_correction");
        AssertRoute(edges, StructureId.FastigialNucleus, StructureId.VestibularNuclei, "fastigiovestibular_balance_correction");
        AssertRoute(edges, StructureId.FastigialNucleus, StructureId.InferiorOlive, "fastigial_nucleo_olivary_inhibition", NTEnum.GABA);

        foreach (var population in BrainstemIds.Concat(DeepCerebellarIds))
        {
            Assert.Contains(edges, edge => edge.Source == population);
            Assert.Contains(edges, edge => edge.Target == population);
        }
    }

    private static void AssertNeuronalService(StructureId structureId)
    {
        var profile = StructureCircuitProfile.For(structureId);
        Assert.InRange(profile.NeuronCount, 192, 288);
        var expectedNt = structureId is StructureId.PedunculopontineNucleus or
            StructureId.LaterodorsalTegmentalNucleus or StructureId.FacialMotorNucleus or
            StructureId.OculomotorNucleus or StructureId.HypoglossalNucleus
                ? NTEnum.ACETYLCHOLINE
                : NTEnum.GLUTAMATE;
        Assert.Equal(expectedNt, profile.DefaultNt);

        var program = File.ReadAllText(ResolveRepositoryFile("Structures", structureId.ToString(), "Program.cs"));
        Assert.Contains("\"Izhikevich\"", program, StringComparison.Ordinal);
        Assert.Contains("\"STDP\"", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ML.NET", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("classifier", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prediction engine", program, StringComparison.OrdinalIgnoreCase);
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
