using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using Xunit;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class SleepMemoryEngramHarnessTests
{
    [Fact]
    public void Sleep_Homeostasis_Captures_And_Replays_Engrams()
    {
        var state = CreateState();

        SleepTransitionResult transition = new(
            IsSleeping: false,
            EnteredSleep: false,
            ExitedSleep: false,
            AtpBudget: 1.0f,
            SleepTicks: 0,
            EngramCount: 0);

        for (var i = 0; i < 512; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
            transition = state.AdvanceSleepHomeostasis(new SleepTickInput(
                DrainedSpikes: 30,
                DispatchedSpikes: 30,
                ActivePathways: 10,
                SpontaneousGenerated: 1,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0));
            if (transition.IsSleeping)
            {
                break;
            }
        }

        Assert.True(transition.IsSleeping);

        var seeds = new List<MemoryEngramSeed>
        {
            new(
                SourceStructure: StructureId.Pfc,
                SourceHemisphere: "L",
                TargetStructure: StructureId.CA1,
                TargetHemisphere: "L",
                SourceNeuronId: "L:pfc-42",
                TargetNeuronId: "L:ca1-9",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: true,
                VesicleQuanta: 2.2f,
                ReuptakeRate: 6.0f,
                Salience: 9.2f),
            new(
                SourceStructure: StructureId.TemporalAssociation,
                SourceHemisphere: "R",
                TargetStructure: StructureId.Amygdala,
                TargetHemisphere: "R",
                SourceNeuronId: "R:ta-7",
                TargetNeuronId: "R:amyg-3",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: false,
                VesicleQuanta: 1.6f,
                ReuptakeRate: 8.5f,
                Salience: 7.4f)
        };

        var captured = state.RecordSignificantEngrams(state.Tick, seeds);
        Assert.True(captured >= 1);

        state.AdvanceSleepHomeostasis(new SleepTickInput(
            DrainedSpikes: 0,
            DispatchedSpikes: 0,
            ActivePathways: 0,
            SpontaneousGenerated: 0,
            EngramsCaptured: captured,
            ReplayedEngrams: 0,
            ReplayDispatchedSpikes: 0));

        var replayBatch = state.SelectEngramsForReplay(3);
        Assert.NotEmpty(replayBatch);

        var deliveredKeys = replayBatch.Select(e => e.Key).ToArray();
        state.RecordEngramReplayDelivery(state.Tick, deliveredKeys);

        state.AdvanceSleepHomeostasis(new SleepTickInput(
            DrainedSpikes: 0,
            DispatchedSpikes: 0,
            ActivePathways: 0,
            SpontaneousGenerated: 0,
            EngramsCaptured: 0,
            ReplayedEngrams: deliveredKeys.Length,
            ReplayDispatchedSpikes: deliveredKeys.Length));

        var runtime = state.GetSleepMemoryRuntime();
        Assert.True(runtime.IsSleeping);
        Assert.True(runtime.TotalEngramsCaptured >= captured);
        Assert.True(runtime.TotalEngramsReplayed >= deliveredKeys.Length);
    }

    [Fact]
    public void Awake_State_Does_Not_Emit_Replay_Batch()
    {
        var state = CreateState();

        var captured = state.RecordSignificantEngrams(state.Tick, new List<MemoryEngramSeed>
        {
            new(
                SourceStructure: StructureId.Ppc,
                SourceHemisphere: "L",
                TargetStructure: StructureId.Pfc,
                TargetHemisphere: "L",
                SourceNeuronId: "L:ppc-11",
                TargetNeuronId: "L:pfc-2",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: false,
                VesicleQuanta: 1.2f,
                ReuptakeRate: 7.2f,
                Salience: 6.1f)
        });

        Assert.True(captured >= 1);
        Assert.Empty(state.SelectEngramsForReplay(4));
    }

    [Fact]
    public void Exposed_Shelter_Need_Can_Wake_Sleeping_Avatar()
    {
        var state = CreateState();
        var exported = state.ExportNetworkState();
        exported.SleepMemory = SleepMemoryRuntime.Default with
        {
            IsSleeping = true,
            AtpBudget = 0.52f,
            SleepPressure = 0.72f,
            SleepTicks = 34,
            WakeTicks = 0
        };
        Assert.True(state.TryImportNetworkState(exported, out var error), error);
        state.AdvanceClockAndCreateTickSignal();
        state.UpdateEnvironmentalState(
            darkness: 0.88f,
            shelterNeed: 0.96f,
            anxiety: 0.42f,
            hunger: 0.12f,
            predatorThreat: 0.0f,
            inShelter: 0.0f,
            health: 0.86f,
            shelterSafety: 0.0f);

        var transition = state.AdvanceSleepHomeostasis(new SleepTickInput(
            DrainedSpikes: 0,
            DispatchedSpikes: 0,
            ActivePathways: 0,
            SpontaneousGenerated: 0,
            EngramsCaptured: 0,
            ReplayedEngrams: 0,
            ReplayDispatchedSpikes: 0));
        var runtime = state.GetSleepMemoryRuntime();

        Assert.True(transition.ExitedSleep);
        Assert.False(runtime.IsSleeping);
        Assert.True(runtime.WakeInertiaTicksRemaining > 0);
        Assert.Contains("Unsafe sleep arousal", runtime.LastAlert, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Replay_Selection_EarlySleep_Prioritizes_Hippocampal_Engrams()
    {
        var state = CreateState();
        EnterSleep(state);

        var captured = state.RecordSignificantEngrams(state.Tick, new List<MemoryEngramSeed>
        {
            new(
                SourceStructure: StructureId.CA3,
                SourceHemisphere: "L",
                TargetStructure: StructureId.CA1,
                TargetHemisphere: "L",
                SourceNeuronId: "L:ca3-11",
                TargetNeuronId: "L:ca1-21",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: true,
                VesicleQuanta: 1.4f,
                ReuptakeRate: 7.5f,
                Salience: 4.0f),
            new(
                SourceStructure: StructureId.Pfc,
                SourceHemisphere: "L",
                TargetStructure: StructureId.TemporalAssociation,
                TargetHemisphere: "L",
                SourceNeuronId: "L:pfc-1",
                TargetNeuronId: "L:ta-7",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: false,
                VesicleQuanta: 1.8f,
                ReuptakeRate: 8.8f,
                Salience: 5.5f)
        });

        Assert.True(captured >= 2);

        var picks = state.SelectEngramsForReplay(1, SleepReplayStage.EarlyHippocampal);
        Assert.Single(picks);
        Assert.Contains("CA3", picks[0].SourceStructure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Replay_Selection_LateSleep_Prioritizes_Cortical_Engrams()
    {
        var state = CreateState();
        EnterSleep(state);

        for (var i = 0; i < 90; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
            state.AdvanceSleepHomeostasis(new SleepTickInput(
                DrainedSpikes: 0,
                DispatchedSpikes: 0,
                ActivePathways: 0,
                SpontaneousGenerated: 0,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0));
        }

        var captured = state.RecordSignificantEngrams(state.Tick, new List<MemoryEngramSeed>
        {
            new(
                SourceStructure: StructureId.CA3,
                SourceHemisphere: "L",
                TargetStructure: StructureId.CA1,
                TargetHemisphere: "L",
                SourceNeuronId: "L:ca3-4",
                TargetNeuronId: "L:ca1-8",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: true,
                VesicleQuanta: 1.5f,
                ReuptakeRate: 7.8f,
                Salience: 5.0f),
            new(
                SourceStructure: StructureId.Ppc,
                SourceHemisphere: "R",
                TargetStructure: StructureId.Pfc,
                TargetHemisphere: "R",
                SourceNeuronId: "R:ppc-2",
                TargetNeuronId: "R:pfc-2",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: false,
                VesicleQuanta: 1.7f,
                ReuptakeRate: 8.4f,
                Salience: 5.0f)
        });

        Assert.True(captured >= 2);

        var picks = state.SelectEngramsForReplay(1, SleepReplayStage.LateCorticalConsolidation);
        Assert.Single(picks);
        Assert.Contains("Ppc", picks[0].SourceStructure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sleep_Replay_Does_Not_Select_Motor_Output_Engrams()
    {
        var state = CreateState();
        EnterSleep(state);

        var captured = state.RecordSignificantEngrams(state.Tick, new List<MemoryEngramSeed>
        {
            new(
                SourceStructure: StructureId.M1,
                SourceHemisphere: "L",
                TargetStructure: StructureId.SpinalCordMotor,
                TargetHemisphere: "M",
                SourceNeuronId: "L:m1-4",
                TargetNeuronId: "M:spinal-2",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: false,
                VesicleQuanta: 2.8f,
                ReuptakeRate: 6.5f,
                Salience: 12.0f),
            new(
                SourceStructure: StructureId.Pfc,
                SourceHemisphere: "R",
                TargetStructure: StructureId.M1,
                TargetHemisphere: "R",
                SourceNeuronId: "R:pfc-9",
                TargetNeuronId: "R:m1-7",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: false,
                VesicleQuanta: 2.1f,
                ReuptakeRate: 7.0f,
                Salience: 10.0f),
            new(
                SourceStructure: StructureId.CA3,
                SourceHemisphere: "L",
                TargetStructure: StructureId.CA1,
                TargetHemisphere: "L",
                SourceNeuronId: "L:ca3-safe",
                TargetNeuronId: "L:ca1-safe",
                Neurotransmitter: NTEnum.GLUTAMATE,
                SynapseId: Guid.NewGuid(),
                IsFeedback: true,
                VesicleQuanta: 1.2f,
                ReuptakeRate: 7.4f,
                Salience: 4.0f)
        });

        Assert.Equal(3, captured);

        var picks = state.SelectEngramsForReplay(4, SleepReplayStage.EarlyHippocampal);
        Assert.Single(picks);
        Assert.Equal(StructureId.CA3, picks[0].SourceStructure);
        Assert.Equal(StructureId.CA1, picks[0].TargetStructure);
    }

    [Fact]
    public void Sleep_Exit_Does_Not_Immediately_Reenter_Sleep()
    {
        var state = CreateState();
        EnterSleep(state);
        ExitSleep(state);

        for (var i = 0; i < 24; i++)
        {
            var transition = Step(
                state,
                DrainedSpikes: 6,
                DispatchedSpikes: 6,
                ActivePathways: 2,
                SpontaneousGenerated: 1,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0);
            Assert.False(transition.EnteredSleep);
            Assert.False(transition.IsSleeping);
        }

        var runtime = state.GetSleepMemoryRuntime();
        Assert.True(runtime.WakeTicks >= 20);
        Assert.True(runtime.AtpBudget > runtime.SleepEnterThreshold);
    }

    [Fact]
    public void Sleep_Controller_Tracks_Short_And_Long_Wake_Episodes()
    {
        var state = CreateState();
        EnterSleep(state);
        ExitSleep(state);

        var enteredShortWakeSleep = false;
        for (var i = 0; i < 512; i++)
        {
            var transition = Step(
                state,
                DrainedSpikes: 88,
                DispatchedSpikes: 88,
                ActivePathways: 22,
                SpontaneousGenerated: 2,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0);
            if (transition.EnteredSleep)
            {
                enteredShortWakeSleep = true;
                break;
            }
        }

        Assert.True(enteredShortWakeSleep);
        var afterShortWake = state.GetSleepMemoryRuntime();
        Assert.True(afterShortWake.ShortWakeAlerts >= 1);
        Assert.True(afterShortWake.LastWakeDurationTicks > 0);
        Assert.True(afterShortWake.LastWakeDurationTicks < afterShortWake.ShortWakeThresholdTicks);

        ExitSleep(state);

        var threshold = state.GetSleepMemoryRuntime().ShortWakeThresholdTicks;
        for (var i = 0; i < threshold + 20; i++)
        {
            var transition = Step(
                state,
                DrainedSpikes: 1,
                DispatchedSpikes: 1,
                ActivePathways: 0,
                SpontaneousGenerated: 0,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0);
            Assert.False(transition.IsSleeping);
        }

        var enteredLongWakeSleep = false;
        for (var i = 0; i < 1024; i++)
        {
            var transition = Step(
                state,
                DrainedSpikes: 48,
                DispatchedSpikes: 48,
                ActivePathways: 12,
                SpontaneousGenerated: 1,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0);
            if (transition.EnteredSleep)
            {
                enteredLongWakeSleep = true;
                break;
            }
        }

        Assert.True(enteredLongWakeSleep);
        var afterLongWake = state.GetSleepMemoryRuntime();
        Assert.True(afterLongWake.LastWakeDurationTicks >= threshold);
    }

    [Fact]
    public void Sleep_Controller_Keeps_Atp_Bounded_And_Duty_Reasonable_Over_Long_Run()
    {
        var state = CreateState();
        var minAtp = 1.0f;
        var maxAtp = 0.0f;

        for (var i = 0; i < 6000; i++)
        {
            var heavyPhase = (i % 220) < 140;
            Step(
                state,
                DrainedSpikes: heavyPhase ? 42 : 8,
                DispatchedSpikes: heavyPhase ? 42 : 8,
                ActivePathways: heavyPhase ? 12 : 2,
                SpontaneousGenerated: heavyPhase ? 1 : 0,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0);

            var runtime = state.GetSleepMemoryRuntime();
            minAtp = Math.Min(minAtp, runtime.AtpBudget);
            maxAtp = Math.Max(maxAtp, runtime.AtpBudget);
        }

        var finalRuntime = state.GetSleepMemoryRuntime();
        Assert.True(minAtp >= -0.0001f);
        Assert.True(maxAtp <= finalRuntime.MaxAtpBudget + 0.0001f);
        Assert.True(finalRuntime.SleepEpisodes >= 2);
        Assert.InRange(finalRuntime.ObservedWakeDutyCycle, 0.20f, 0.95f);
        Assert.True(Math.Abs(finalRuntime.ObservedWakeDutyCycle - finalRuntime.TargetWakeDutyCycle) <= 0.40f);
        Assert.True(finalRuntime.WakeDurationEwmaTicks >= 1.0f);
        Assert.True(finalRuntime.SleepDurationEwmaTicks >= 1.0f);
    }

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        state.UpdateNeuromod(
            new NeuromodState
            {
                DopamineLevel = 0.5f,
                SerotoninLevel = 0.5f,
                AcetylcholineLevel = 0.5f,
                NorepinephrineLevel = 0.5f
            },
            rewardPredictionError: 0.0f,
            attention: new AttentionVector(0.25f, 0.25f, 0.25f, 0.25f));
        return state;
    }

    private static void EnterSleep(SimulationState state)
    {
        for (var i = 0; i < 512; i++)
        {
            var transition = Step(
                state,
                DrainedSpikes: 40,
                DispatchedSpikes: 40,
                ActivePathways: 12,
                SpontaneousGenerated: 1,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0);
            if (transition.IsSleeping)
            {
                return;
            }
        }

        throw new InvalidOperationException("State did not enter sleep within expected ticks.");
    }

    private static void ExitSleep(SimulationState state)
    {
        for (var i = 0; i < 2048; i++)
        {
            var transition = Step(
                state,
                DrainedSpikes: 0,
                DispatchedSpikes: 0,
                ActivePathways: 0,
                SpontaneousGenerated: 0,
                EngramsCaptured: 0,
                ReplayedEngrams: 0,
                ReplayDispatchedSpikes: 0);
            if (transition.ExitedSleep || !transition.IsSleeping)
            {
                return;
            }
        }

        throw new InvalidOperationException("State did not exit sleep within expected ticks.");
    }

    private static SleepTransitionResult Step(
        SimulationState state,
        int DrainedSpikes,
        int DispatchedSpikes,
        int ActivePathways,
        int SpontaneousGenerated,
        int EngramsCaptured,
        int ReplayedEngrams,
        int ReplayDispatchedSpikes)
    {
        state.AdvanceClockAndCreateTickSignal();
        return state.AdvanceSleepHomeostasis(new SleepTickInput(
            DrainedSpikes,
            DispatchedSpikes,
            ActivePathways,
            SpontaneousGenerated,
            EngramsCaptured,
            ReplayedEngrams,
            ReplayDispatchedSpikes));
    }
}
