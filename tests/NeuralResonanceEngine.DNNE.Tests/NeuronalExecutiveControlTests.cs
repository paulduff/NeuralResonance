using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalExecutiveControlTests
{
    [Fact]
    public void CompleteExecutiveCircuitMirrorsNeuronalActionWinner()
    {
        var motor = Motor(selectedChannel: 2);
        var decision = NeuronalExecutiveDecoder.Decode(
            CompleteCircuit(),
            Attention(selectedChannel: 5),
            motor);

        Assert.True(decision.Available);
        Assert.True(decision.Active);
        Assert.Equal(motor.SelectedActionChannel, decision.SelectedActionChannel);
        Assert.Equal(5, decision.MaintainedContextChannel);
        Assert.Equal(1.0, decision.CircuitCoverage, 6);
    }

    [Fact]
    public void IncompletePrefrontalThalamicStriatalCircuitFailsClosed()
    {
        var incomplete = CompleteCircuit()
            .Where(static snapshot => snapshot.StructureId != StructureId.MediodorsalThalamus)
            .ToArray();

        var decision = NeuronalExecutiveDecoder.Decode(
            incomplete,
            Attention(selectedChannel: 4),
            Motor(selectedChannel: 1));

        Assert.True(decision.Available);
        Assert.False(decision.Active);
        Assert.False(decision.Committed);
        Assert.Equal(-1, decision.SelectedActionChannel);
    }

    [Fact]
    public void ExecutiveMonitorCannotSelectWithoutNeuronalActionCircuit()
    {
        var motor = Motor(selectedChannel: 3) with
        {
            ActionCircuitObserved = false,
            SelectedActionChannel = -1
        };

        var decision = NeuronalExecutiveDecoder.Decode(
            CompleteCircuit(),
            Attention(selectedChannel: 0),
            motor);

        Assert.False(decision.Active);
        Assert.Equal(-1, decision.SelectedActionChannel);
    }

    [Fact]
    public void HumanReadableControlModeCannotChangeExecutiveState()
    {
        var baseline = NeuronalExecutiveDecoder.Decode(
            CompleteCircuit("maintain"),
            Attention(selectedChannel: 2),
            Motor(selectedChannel: 1));
        var renamed = NeuronalExecutiveDecoder.Decode(
            CompleteCircuit("override-everything"),
            Attention(selectedChannel: 2),
            Motor(selectedChannel: 1));

        Assert.Equal(baseline.SelectedActionChannel, renamed.SelectedActionChannel);
        Assert.Equal(baseline.TaskSetStability, renamed.TaskSetStability, 10);
        Assert.Equal(baseline.Confidence, renamed.Confidence, 10);
    }

    [Fact]
    public void RuntimeOnlyCountsPersistenceOfObservedNeuronalWinner()
    {
        var runtime = new NeuronalExecutiveRuntime();
        var circuit = CompleteCircuit();
        var attention = Attention(selectedChannel: 4);

        var first = runtime.Update(10, circuit, attention, Motor(selectedChannel: 0));
        var second = runtime.Update(11, circuit, attention, Motor(selectedChannel: 0));
        var changed = runtime.Update(12, circuit, attention, Motor(selectedChannel: 3));
        var snapshot = runtime.GetSnapshot();

        Assert.Equal(1, first.SustainedSelectionTicks);
        Assert.Equal(2, second.SustainedSelectionTicks);
        Assert.Equal(1, changed.SustainedSelectionTicks);
        Assert.True(snapshot.ReadOnlyMonitor);
        Assert.False(snapshot.CanInjectGoals);
        Assert.False(snapshot.CanOverrideActionSelection);
        Assert.False(snapshot.LegacyPlanningEnabled);
    }

    private static IReadOnlyList<InstanceStructureSnapshot> CompleteCircuit(string mode = "neuronal")
        =>
        [
            Snapshot(StructureId.Pfc, mode, pfc: 20f),
            Snapshot(StructureId.MediodorsalThalamus, mode, md: 18f),
            Snapshot(StructureId.Ppc, mode, frontoparietal: 15f),
            Snapshot(StructureId.TemporalAssociation, mode, semantic: 12f),
            Snapshot(StructureId.Striatum, mode, striatal: 17f),
            Snapshot(StructureId.Acc, mode, conflict: 3f)
        ];

    private static NeuronalAttentionWorkspaceDecision Attention(int selectedChannel)
        => NeuronalAttentionWorkspaceDecision.Unavailable with
        {
            Available = true,
            Active = true,
            SelectedChannel = selectedChannel
        };

    private static NeuronalMotorRuntime Motor(int selectedChannel)
        => NeuronalMotorRuntime.Default with
        {
            Active = true,
            SelectedActionChannel = selectedChannel,
            ActionSelectionConfidence = 0.82,
            ActionCircuitCoverage = 1.0,
            ActionSelectionMargin = 0.18,
            ActionCircuitObserved = true
        };

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        string mode,
        float pfc = 0f,
        float md = 0f,
        float frontoparietal = 0f,
        float semantic = 0f,
        float striatal = 0f,
        float conflict = 0f)
        => new(
            new ServiceInstance(structure, $"{structure}-M", "M", new Uri("http://localhost")),
            structure,
            ActiveNeuronCount: 32,
            MeanFiringRateHz: 8f,
            DominantRhythm: BrainRhythm.BETA,
            TopActiveNeurons: [],
            NeuromodLocal: new NeuromodState(),
            SpikeInCount: 0,
            SpikeOutCount: 0,
            FeedbackQueueDepth: 0,
            PrefrontalWorkingMemoryDiagnostics: new PrefrontalWorkingMemoryDiagnostics(
                mode,
                pfc,
                md,
                frontoparietal,
                semantic,
                striatal,
                conflict,
                Math.Max(0f, pfc + md + frontoparietal + semantic - conflict),
                Math.Max(0f, pfc + md + striatal + frontoparietal - conflict)));
}
