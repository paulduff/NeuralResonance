using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CorticalSpecializationKernelTests
{
    private static readonly Guid TestSynapseId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    public static TheoryData<StructureId, Type> SpecializedCircuits => new()
    {
        { StructureId.V3, typeof(VisualAssociationCircuitKernel) },
        { StructureId.InferotemporalCortex, typeof(VisualAssociationCircuitKernel) },
        { StructureId.FusiformGyrus, typeof(VisualAssociationCircuitKernel) },
        { StructureId.AuditoryAssociationCortex, typeof(AuditoryAssociationCircuitKernel) },
        { StructureId.SecondarySomatosensoryCortex, typeof(SomatosensoryAssociationCircuitKernel) },
        { StructureId.TemporalPole, typeof(SelfContextCircuitKernel) },
        { StructureId.TemporoparietalJunction, typeof(SelfContextCircuitKernel) },
        { StructureId.Precuneus, typeof(SelfContextCircuitKernel) },
        { StructureId.MidcingulateCortex, typeof(ExecutiveControlCircuitKernel) },
        { StructureId.DorsomedialPrefrontalCortex, typeof(ExecutiveControlCircuitKernel) },
        { StructureId.VentromedialPrefrontalCortex, typeof(ExecutiveControlCircuitKernel) },
        { StructureId.FrontalEyeFields, typeof(ExecutiveControlCircuitKernel) }
    };

    [Theory]
    [MemberData(nameof(SpecializedCircuits))]
    public void ExpandedCircuits_SelectPurposeSpecificKernels(StructureId structureId, Type expectedKernelType)
    {
        Assert.IsType(expectedKernelType, CircuitKernelFactory.For(structureId));
    }

    [Fact]
    public void VisualAssociation_SeparatesFeedforwardMotionAndSemanticStreams()
    {
        int feedforward = MapInbound(StructureId.InferotemporalCortex, StructureId.V4, "visual-173");
        int motion = MapInbound(StructureId.InferotemporalCortex, StructureId.Mt, "visual-173");
        int semantic = MapInbound(StructureId.InferotemporalCortex, StructureId.TemporalAssociation, "visual-173");

        Assert.Equal(3, new[] { feedforward, motion, semantic }.Distinct().Count());
    }

    [Fact]
    public void AuditoryAssociation_SeparatesAcousticSemanticAndLanguageStreams()
    {
        int acoustic = MapInbound(StructureId.AuditoryAssociationCortex, StructureId.A1, "tone-205");
        int semantic = MapInbound(StructureId.AuditoryAssociationCortex, StructureId.TemporalAssociation, "tone-205");
        int language = MapInbound(StructureId.AuditoryAssociationCortex, StructureId.WernickePstgPsts, "tone-205");

        Assert.Equal(3, new[] { acoustic, semantic, language }.Distinct().Count());
    }

    [Fact]
    public void SecondarySomatosensoryCortex_PreservesDistinctBodyZones()
    {
        int face = MapInbound(StructureId.SecondarySomatosensoryCortex, StructureId.S1, "touch-face-12");
        int hand = MapInbound(StructureId.SecondarySomatosensoryCortex, StructureId.S1, "touch-hand-12");
        int trunk = MapInbound(StructureId.SecondarySomatosensoryCortex, StructureId.S1, "touch-trunk-12");
        int foot = MapInbound(StructureId.SecondarySomatosensoryCortex, StructureId.S1, "touch-foot-12");

        Assert.InRange(face, 0, 95);
        Assert.InRange(hand, 96, 191);
        Assert.InRange(trunk, 192, 287);
        Assert.InRange(foot, 288, 383);
    }

    [Fact]
    public void SelfContextKernel_SeparatesBodyMemoryAffectAndSemanticEvidence()
    {
        int body = MapInbound(StructureId.TemporoparietalJunction, StructureId.SecondarySomatosensoryCortex, "context-91");
        int memory = MapInbound(StructureId.TemporoparietalJunction, StructureId.PosteriorCingulate, "context-91");
        int affect = MapInbound(StructureId.TemporoparietalJunction, StructureId.BasolateralAmygdala, "context-91");
        int semantic = MapInbound(StructureId.TemporoparietalJunction, StructureId.TemporalAssociation, "context-91");

        Assert.Equal(4, new[] { body, memory, affect, semantic }.Distinct().Count());
    }

    [Fact]
    public void ExecutiveKernel_UsesAttentionErrorAndValueToSelectBurstOutput()
    {
        var kernel = Assert.IsType<ExecutiveControlCircuitKernel>(CircuitKernelFactory.For(StructureId.FrontalEyeFields));

        Assert.Equal(SpikeTypeEnum.ACTION_POTENTIAL, kernel.SelectSpikeType(StructureId.FrontalEyeFields, false, MakeNeuromod(), 0f));
        Assert.Equal(
            SpikeTypeEnum.BURST,
            kernel.SelectSpikeType(
                StructureId.FrontalEyeFields,
                false,
                MakeNeuromod(acetylcholine: 0.62f, norepinephrine: 0.48f),
                0f));
        Assert.Equal(
            SpikeTypeEnum.BURST,
            kernel.SelectSpikeType(StructureId.MidcingulateCortex, false, MakeNeuromod(), -0.35f));
        Assert.Equal(
            SpikeTypeEnum.BURST,
            kernel.SelectSpikeType(StructureId.VentromedialPrefrontalCortex, false, MakeNeuromod(dopamine: 0.61f), 0f));
    }

    [Fact]
    public void ExecutiveKernel_ReservesDistinctFunctionalControlLanes()
    {
        int planning = MapInbound(StructureId.DorsomedialPrefrontalCortex, StructureId.Pfc, "control-247");
        int conflict = MapInbound(StructureId.DorsomedialPrefrontalCortex, StructureId.Acc, "control-247");
        int value = MapInbound(StructureId.DorsomedialPrefrontalCortex, StructureId.OrbitofrontalCortex, "control-247");
        int action = MapInbound(StructureId.DorsomedialPrefrontalCortex, StructureId.Striatum, "control-247");
        int attention = MapInbound(StructureId.DorsomedialPrefrontalCortex, StructureId.Ppc, "control-247");

        Assert.InRange(planning, 0, 63);
        Assert.InRange(conflict, 64, 127);
        Assert.InRange(value, 128, 191);
        Assert.InRange(action, 192, 255);
        Assert.InRange(attention, 256, 319);
        Assert.Equal(5, new[] { planning, conflict, value, action, attention }.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(SpecializedCircuits))]
    public void SpecializedMappings_AreDeterministicAndBounded(StructureId structureId, Type _)
    {
        int first = MapInbound(structureId, StructureId.Pfc, "probe-247");
        int second = MapInbound(structureId, StructureId.Pfc, "probe-247");

        Assert.Equal(first, second);
        Assert.InRange(first, 0, 383);
    }

    private static int MapInbound(StructureId target, StructureId source, string sourceNeuronId)
    {
        var circuit = StructureCircuitProfile.For(target);
        var spike = new SpikeMessage
        {
            MessageId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            SourceStructure = source,
            TargetStructure = target,
            SourceNeuronId = sourceNeuronId,
            TargetNeuronId = string.Empty,
            SynapseId = TestSynapseId,
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = 1f,
            SpikeType = SpikeTypeEnum.ACTION_POTENTIAL
        };

        return CircuitKernelFactory.For(target).ResolveInboundNeuronIndex(spike, circuit.NeuronCount, circuit);
    }

    private static NeuromodState MakeNeuromod(
        float dopamine = 0f,
        float acetylcholine = 0f,
        float norepinephrine = 0f)
        => new()
        {
            DopamineLevel = dopamine,
            AcetylcholineLevel = acetylcholine,
            NorepinephrineLevel = norepinephrine
        };
}
