using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalSleepConsolidationTests
{
    [Fact]
    public void HomeostaticDriveTargetsSleepPopulationsAndOpposesWakePopulations()
    {
        SleepConsolidationTopology.ResolveIntrinsicDrive(
            StructureId.Hypothalamus,
            1,
            sleepDrive: 0.95f,
            wakeReserve: 0.10f,
            out var sleepExcitation,
            out var sleepInhibition);
        SleepConsolidationTopology.ResolveIntrinsicDrive(
            StructureId.LocusCoeruleus,
            0,
            sleepDrive: 0.95f,
            wakeReserve: 0.10f,
            out var wakeExcitation,
            out var wakeInhibition);

        Assert.True(sleepExcitation > sleepInhibition);
        Assert.True(wakeInhibition > wakeExcitation);
        Assert.Equal(SleepConsolidationTopology.NremChannel,
            SleepConsolidationTopology.StateChannelForNeuron(1, StructureId.Hypothalamus));
    }

    [Fact]
    public void DistributedNremCircuitSelectsNumericReplayEnsemble()
    {
        var decision = NeuronalSleepConsolidationDecoder.Decode(CreateNremCircuit(3));

        Assert.True(decision.CircuitObserved);
        Assert.True(decision.Available);
        Assert.True(decision.StateActive);
        Assert.Equal(NeuronalSleepState.Nrem, decision.State);
        Assert.True(decision.ReplayActive);
        Assert.Equal(3, decision.ReplayEnsemble);
        Assert.True(decision.SpindleCoupling > 0f);
        Assert.True(decision.SlowWaveCoupling > 0f);
    }

    [Fact]
    public void WakeSystemStimulationOpposesSleepState()
    {
        var decision = NeuronalSleepConsolidationDecoder.Decode(
            CreateNremCircuit(2, wakeDrive: 0.98f));

        Assert.True(decision.Available);
        Assert.Equal(NeuronalSleepState.Wake, decision.State);
        Assert.False(decision.ReplayActive);
    }

    [Fact]
    public void TrnOrCa3AblationRemovesReplayAuthority()
    {
        var circuit = CreateNremCircuit(5);
        var noTrn = NeuronalSleepConsolidationDecoder.Decode(
            circuit.Where(static snapshot => snapshot.StructureId != StructureId.Trn).ToArray());
        var noCa3 = NeuronalSleepConsolidationDecoder.Decode(
            circuit.Where(static snapshot => snapshot.StructureId != StructureId.CA3).ToArray());

        Assert.Equal(NeuronalSleepState.Nrem, noTrn.State);
        Assert.False(noTrn.ReplayActive);
        Assert.Equal(0.0, noTrn.SpindleCoupling);
        Assert.Equal(-1, noTrn.ReplayEnsemble);
        Assert.Equal(NeuronalSleepState.Nrem, noCa3.State);
        Assert.False(noCa3.ReplayActive);
        Assert.Equal(-1, noCa3.ReplayEnsemble);
    }

    [Fact]
    public void IncompleteObservedCircuitCannotHoldSleepThroughHostFallback()
    {
        var state = new SimulationState();
        state.AdvanceClockAndCreateTickSignal();
        var nrem = NeuronalSleepConsolidationDecoder.Decode(CreateNremCircuit(1));
        var entered = state.AdvanceMetabolicPhysiology(IdleTick(), nrem);
        var incomplete = NeuronalSleepConsolidationDecoder.Decode([
            CreateNremCircuit(1).Single(static snapshot => snapshot.StructureId == StructureId.Hypothalamus)
        ]);
        var released = state.AdvanceMetabolicPhysiology(IdleTick(), incomplete);

        Assert.True(entered.EnteredSleep);
        Assert.True(entered.NeuronalSleepObserved);
        Assert.True(incomplete.CircuitObserved);
        Assert.False(incomplete.Available);
        Assert.False(released.NeuronalSleepObserved);
        Assert.True(released.ExitedSleep);
        Assert.False(state.GetMetabolicPhysiologyRuntime().NeuronalSleepObserved);
    }

    [Fact]
    public void NeuronalPayloadContainsNoSemanticReplaySelectors()
    {
        var payload = JsonSerializer.Serialize(
            NeuronalSleepConsolidationDecoder.Decode(CreateNremCircuit(6)));

        Assert.DoesNotContain("goal", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("category", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actionkey", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("theme", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CentralMemoryAndReplayApisArePhysicallyAbsent()
    {
        var methodNames = typeof(SimulationState)
            .GetMethods()
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var retiredMethod in new[]
        {
            "RecordSignificantEngrams",
            "SelectEngramsForReplay",
            "SelectEngramsForNeuronalReplay",
            "RecordEngramReplayDelivery",
            "GetEpisodicMemorySnapshot",
            "GetSemanticMemorySnapshot",
            "GetObjectMemorySnapshot",
            "EvaluateCounterfactual"
        })
        {
            Assert.DoesNotContain(retiredMethod, methodNames);
        }

        Assert.DoesNotContain(
            typeof(SimulationState).GetMethods(),
            static method => method.Name == "AdvanceSleepHomeostasis");

        var physiologyOverloads = typeof(SimulationState)
            .GetMethods()
            .Where(static method => method.Name == nameof(SimulationState.AdvanceMetabolicPhysiology))
            .ToArray();
        Assert.Single(physiologyOverloads);
        Assert.Equal(2, physiologyOverloads[0].GetParameters().Length);
    }

    private static MetabolicTickInput IdleTick()
        => new(
            DrainedSpikes: 0,
            GeneratedSpikes: 0,
            ActivePathways: 0,
            SpontaneousGenerated: 0);

    private static IReadOnlyList<InstanceStructureSnapshot> CreateNremCircuit(
        int selectedEnsemble,
        float wakeDrive = 0.04f)
        =>
        [
            Snapshot(StructureId.Hypothalamus, selectedEnsemble, homeostatic: 0.95f, nrem: 0.82f, replayGate: 0.76f),
            Snapshot(StructureId.ReticularFormation, selectedEnsemble, wake: wakeDrive),
            Snapshot(StructureId.Pons, selectedEnsemble, rem: 0.05f),
            Snapshot(StructureId.LocusCoeruleus, selectedEnsemble, wake: wakeDrive),
            Snapshot(StructureId.BasalForebrain, selectedEnsemble, wake: wakeDrive),
            Snapshot(StructureId.IntralaminarThalamus, selectedEnsemble, wake: wakeDrive),
            Snapshot(StructureId.Trn, selectedEnsemble, nrem: 0.78f, spindle: 0.88f),
            Snapshot(StructureId.Thalamus, selectedEnsemble, nrem: 0.76f, spindle: 0.84f),
            Snapshot(StructureId.CA3, selectedEnsemble, nrem: 0.60f, replayGate: 0.90f, hippocampal: 0.96f, engram: 0.82f),
            Snapshot(StructureId.CA1, selectedEnsemble, nrem: 0.58f, replayGate: 0.72f, hippocampal: 0.76f, engram: 0.74f),
            Snapshot(StructureId.Pfc, selectedEnsemble, nrem: 0.68f, slowWave: 0.80f, corticalEcho: 0.74f, consolidation: 0.66f)
        ];

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        int selectedEnsemble,
        float homeostatic = 0f,
        float wake = 0f,
        float nrem = 0f,
        float rem = 0f,
        float spindle = 0f,
        float slowWave = 0f,
        float replayGate = 0f,
        float hippocampal = 0f,
        float corticalEcho = 0f,
        float engram = 0f,
        float consolidation = 0f)
    {
        var stateChannels = Enumerable.Range(0, 3)
            .Select(channel => new SleepStateChannelActivity(
                channel,
                channel == 1 ? homeostatic : 0f,
                channel == 0 ? wake : 0f,
                channel == 1 ? nrem : 0f,
                channel == 2 ? rem : 0f,
                channel == 1 ? spindle : 0f,
                channel == 1 ? slowWave : 0f,
                channel == 1 ? replayGate : 0f))
            .ToArray();
        var replayEnsembles = Enumerable.Range(0, 8)
            .Select(ensemble =>
            {
                var selected = ensemble == selectedEnsemble;
                return new SleepReplayEnsembleActivity(
                    ensemble,
                    selected ? hippocampal : hippocampal * 0.04f,
                    selected ? spindle : spindle * 0.05f,
                    selected ? slowWave : slowWave * 0.05f,
                    selected ? corticalEcho : corticalEcho * 0.05f,
                    selected ? engram : engram * 0.05f,
                    selected ? 0.02f : 0.18f,
                    selected ? consolidation : consolidation * 0.05f);
            })
            .ToArray();
        var diagnostic = new NeuronalSleepConsolidationDiagnostics(
            structure,
            stateChannels,
            replayEnsembles);
        return new InstanceStructureSnapshot(
            new ServiceInstance(structure, $"{structure}-M", "M", new Uri("http://localhost")),
            structure,
            32,
            4f,
            BrainRhythm.DELTA,
            [],
            new NeuromodState(),
            0,
            0,
            0,
            NeuronalSleepConsolidationDiagnostics: diagnostic);
    }
}
