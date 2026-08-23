using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class NeuronalActionSelectionTests
{
    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);

    [Fact]
    public void StableTopologyPreservesEveryLaneAcrossBasalGangliaNuclei()
    {
        for (var channel = 0; channel < ActionChannelTopology.ChannelCount; channel++)
        {
            var corticalIndex = (ActionChannelTopology.ChannelCount * 4) + channel;
            var striatal = ActionChannelTopology.Project(
                corticalIndex,
                StructureId.Pfc,
                320,
                StructureId.Striatum,
                41);
            var pallidal = ActionChannelTopology.Project(
                striatal,
                StructureId.Striatum,
                224,
                StructureId.GPi,
                47);
            var thalamic = ActionChannelTopology.Project(
                pallidal,
                StructureId.GPi,
                320,
                StructureId.MotorThalamus,
                29);

            Assert.Equal(channel, ActionChannelTopology.ChannelForNeuron(striatal, StructureId.Striatum));
            Assert.Equal(channel, ActionChannelTopology.ChannelForNeuron(pallidal, StructureId.GPi));
            Assert.Equal(channel, ActionChannelTopology.ChannelForNeuron(thalamic, StructureId.MotorThalamus));
        }
    }

    [Fact]
    public void EveryStriatalLaneContainsPairedD1AndD2Populations()
    {
        for (var channel = 0; channel < ActionChannelTopology.ChannelCount; channel++)
        {
            var d1 = channel * 2;
            var d2 = d1 + 1;
            Assert.True(ActionChannelTopology.IsDirectPathwayNeuron(d1));
            Assert.False(ActionChannelTopology.IsDirectPathwayNeuron(d2));
            Assert.Equal(channel, ActionChannelTopology.ChannelForNeuron(d1, StructureId.Striatum));
            Assert.Equal(channel, ActionChannelTopology.ChannelForNeuron(d2, StructureId.Striatum));
        }
    }

    [Fact]
    public void CompetingPopulationsSelectTheStrongestDisinhibitedLane()
    {
        var decision = NeuronalActionSelectionDecoder.Decode(CreateCircuit(selectedChannel: 1));

        Assert.True(decision.Available);
        Assert.True(decision.Active);
        Assert.Equal(1, decision.SelectedChannel);
        Assert.True(decision.SelectionMargin > 0.01);
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(decision, 0.8, 0.8);
        Assert.True(shaped.Left < shaped.Right);

        var payload = JsonSerializer.Serialize(
            CreateCircuit(selectedChannel: 1)
                .Select(static snapshot => snapshot.ActionSelectionDiagnostics));
        Assert.DoesNotContain("forward", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("turn", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("retreat", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GpiStimulationSuppressesSelectedMotorOutput()
    {
        var snapshots = CreateCircuit(selectedChannel: 0, selectedOutputInhibition: 1f);
        var decision = NeuronalActionSelectionDecoder.Decode(snapshots);
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(decision, 0.8, 0.8);

        Assert.True(decision.Available);
        Assert.False(decision.Active);
        Assert.Equal(-1, decision.SelectedChannel);
        Assert.Equal(0.0, shaped.Left);
        Assert.Equal(0.0, shaped.Right);
    }

    [Fact]
    public void DirectPathwayStimulationDisinhibitsItsOwnLane()
    {
        var snapshots = CreateCircuit(selectedChannel: 2, selectedDirectActivation: 1f);
        var decision = NeuronalActionSelectionDecoder.Decode(snapshots);

        Assert.True(decision.Active);
        Assert.Equal(2, decision.SelectedChannel);
        Assert.True(decision.ChannelScores[2] > decision.ChannelScores[0]);
    }

    [Fact]
    public void CoreCircuitAblationPreventsActionAuthority()
    {
        var ablated = CreateCircuit(selectedChannel: 0)
            .Where(snapshot => snapshot.StructureId is StructureId.Pfc or StructureId.PremotorCortex or StructureId.Stn)
            .ToArray();
        var decision = NeuronalActionSelectionDecoder.Decode(ablated);

        Assert.True(decision.Available);
        Assert.False(decision.Active);
        Assert.True(decision.CircuitCoverage < 0.60);
    }

    [Fact]
    public void CorticostriatalArborPreservesLaneAndReceptorClass()
    {
        const int channel = 7;
        const int neuronCount = 320;
        var directPrimary = channel * 2;
        var indirectPrimary = directPrimary + 1;

        var directTargets = Enumerable.Range(1, 2)
            .Select(offset => ActionChannelTopology.StriatalArborTarget(
                directPrimary,
                neuronCount,
                offset))
            .ToArray();
        var indirectTargets = Enumerable.Range(1, 2)
            .Select(offset => ActionChannelTopology.StriatalArborTarget(
                indirectPrimary,
                neuronCount,
                offset))
            .ToArray();

        Assert.Equal(2, directTargets.Distinct().Count());
        Assert.Equal(2, indirectTargets.Distinct().Count());
        Assert.All(directTargets, target =>
        {
            Assert.Equal(channel, ActionChannelTopology.ChannelForNeuron(target, StructureId.Striatum));
            Assert.True(ActionChannelTopology.IsDirectPathwayNeuron(target));
        });
        Assert.All(indirectTargets, target =>
        {
            Assert.Equal(channel, ActionChannelTopology.ChannelForNeuron(target, StructureId.Striatum));
            Assert.False(ActionChannelTopology.IsDirectPathwayNeuron(target));
        });
    }

    [Fact]
    public void IndependentCorticalAxonsConvergeWithinTheSameMsnEnsemble()
    {
        const int channel = 11;
        const int neuronCount = 320;
        const int salt = 0;
        var sourceA = channel;
        var sourceB = (ActionChannelTopology.ChannelCount * 2) + channel;

        var primaryA = ActionChannelTopology.Project(
            sourceA,
            StructureId.Pfc,
            neuronCount,
            StructureId.Striatum,
            salt);
        var primaryB = ActionChannelTopology.Project(
            sourceB,
            StructureId.Pfc,
            neuronCount,
            StructureId.Striatum,
            salt);
        var arborA = Enumerable.Range(0, 3)
            .Select(offset => offset == 0
                ? primaryA
                : ActionChannelTopology.StriatalArborTarget(primaryA, neuronCount, offset))
            .ToHashSet();
        var arborB = Enumerable.Range(0, 3)
            .Select(offset => offset == 0
                ? primaryB
                : ActionChannelTopology.StriatalArborTarget(primaryB, neuronCount, offset))
            .ToHashSet();

        Assert.NotEqual(primaryA, primaryB);
        Assert.NotEmpty(arborA.Intersect(arborB));
        Assert.All(arborA.Concat(arborB), target =>
        {
            Assert.Equal(channel, ActionChannelTopology.ChannelForNeuron(target, StructureId.Striatum));
            Assert.Equal(primaryA & 1, target & 1);
        });
    }

    [Fact]
    public async Task FreshStriatumRecruitsBothMsnPopulationsAtLiveOneMillisecondCadence()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-fresh-striatal-recruitment", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        const int channel = 6;

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"fresh-striatum-{Guid.NewGuid():N}");
            using var engine = CreateStriatalEngine("fresh convergent MSN recruitment assay");
            var peakDirectActivation = 0f;
            var peakIndirectActivation = 0f;
            var peakDirectUpState = 0f;
            var peakIndirectUpState = 0f;
            var peakDirectCurrent = float.MinValue;
            var peakIndirectCurrent = float.MinValue;

            long structureTick = 0;
            double lastStructureTimestamp = 0.0;
            for (var globalTick = 1L; globalTick <= 2_400; globalTick++)
            {
                var timestamp = (double)globalTick;
                if (globalTick % 4 == 0)
                {
                    var sourceLocal = (int)((globalTick / 4) % 8);
                    var sourceIndex = (sourceLocal * ActionChannelTopology.ChannelCount) + channel;
                    await engine.EnqueueSpikeAsync(CorticostriatalSpike(
                        sourceIndex,
                        channel,
                        timestamp,
                        1.0f));
                }

                // The stable laptop profile selected about 25 of 68 fast-lane
                // instances per coordinator pass. This deterministic three-pass
                // cadence exercises the same sparse 1 ms stream without using a
                // concentrated synthetic barrage.
                if (globalTick != 1 && (globalTick - 1) % 3 != 0)
                {
                    continue;
                }

                structureTick++;
                var elapsed = timestamp - lastStructureTimestamp;
                lastStructureTimestamp = timestamp;
                var ack = await engine.ProcessTickAsync(new TickSignal(
                    structureTick,
                    timestamp,
                    elapsed,
                    new NeuromodState(),
                    new Dictionary<BrainRhythm, double>(),
                    0f));
                var striatal = Assert.IsType<ActionSelectionDiagnostics>(ack.ActionSelectionDiagnostics);
                var activity = striatal.Channels[channel];
                peakDirectActivation = Math.Max(peakDirectActivation, activity.DirectPathwayActivation);
                peakIndirectActivation = Math.Max(peakIndirectActivation, activity.IndirectPathwayActivation);
                peakDirectUpState = Math.Max(peakDirectUpState, activity.DirectMeanUpState);
                peakIndirectUpState = Math.Max(peakIndirectUpState, activity.IndirectMeanUpState);
                peakDirectCurrent = Math.Max(peakDirectCurrent, activity.DirectMeanSynapticCurrent);
                peakIndirectCurrent = Math.Max(peakIndirectCurrent, activity.IndirectMeanSynapticCurrent);
            }

            Assert.True(peakDirectUpState > 0.01f, $"D1 up-state remained silent ({peakDirectUpState:F5}).");
            Assert.True(peakIndirectUpState > 0.01f, $"D2 up-state remained silent ({peakIndirectUpState:F5}).");
            Assert.True(peakDirectCurrent > 0f, $"D1 synaptic current remained non-positive ({peakDirectCurrent:F5}).");
            Assert.True(peakIndirectCurrent > 0f, $"D2 synaptic current remained non-positive ({peakIndirectCurrent:F5}).");
            Assert.True(peakDirectActivation > 0f, $"D1 population emitted no activity ({peakDirectActivation:F5}).");
            Assert.True(peakIndirectActivation > 0f, $"D2 population emitted no activity ({peakIndirectActivation:F5}).");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            EnvironmentGate.Release();
        }
    }

    [Fact]
    public void SilentStriatumCannotAuthorizeVoluntaryAction()
    {
        var snapshots = CreateCircuit(selectedChannel: ForwardChannel())
            .Select(snapshot => snapshot.StructureId == StructureId.Striatum
                ? snapshot with
                {
                    ActionSelectionDiagnostics = snapshot.ActionSelectionDiagnostics! with
                    {
                        Channels = snapshot.ActionSelectionDiagnostics!.Channels
                            .Select(channel => channel with
                            {
                                DirectPathwayActivation = 0f,
                                IndirectPathwayActivation = 0f,
                                EligibilityTrace = 1f,
                                LearnedSynapticStrength = 5f
                            })
                            .ToArray()
                    }
                }
                : snapshot)
            .ToArray();

        var decision = NeuronalActionSelectionDecoder.Decode(snapshots);

        Assert.True(decision.Available);
        Assert.False(decision.Active);
        Assert.Equal(-1, decision.SelectedChannel);
        Assert.Contains("silent D1/D2", decision.AuthorityReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaturatedEligibilityAndPersistenceCannotReplaceFunctionalCompetition()
    {
        var snapshots = CreateCircuit(selectedChannel: ForwardChannel())
            .Select(snapshot => snapshot.StructureId == StructureId.Striatum
                ? snapshot with
                {
                    ActionSelectionDiagnostics = snapshot.ActionSelectionDiagnostics! with
                    {
                        Channels = snapshot.ActionSelectionDiagnostics!.Channels
                            .Select(channel => channel with
                            {
                                DirectPathwayActivation = 0f,
                                IndirectPathwayActivation = 0f,
                                EligibilityTrace = 1f,
                                LearnedSynapticStrength = 5f
                            })
                            .ToArray()
                    }
                }
                : snapshot)
            .ToArray();

        var decision = NeuronalActionSelectionDecoder.Decode(
            snapshots,
            preferredChannel: ForwardChannel(),
            persistenceBias: 0.25);

        Assert.False(decision.Active);
        Assert.Equal(0f, decision.ChannelTraces[ForwardChannel()].PersistenceBias);
    }

    [Fact]
    public void SilentMotorThalamusCannotAuthorizeVoluntaryAction()
    {
        var snapshots = CreateCircuit(selectedChannel: ForwardChannel())
            .Select(snapshot => snapshot.StructureId == StructureId.MotorThalamus
                ? snapshot with
                {
                    ActionSelectionDiagnostics = snapshot.ActionSelectionDiagnostics! with
                    {
                        Channels = snapshot.ActionSelectionDiagnostics!.Channels
                            .Select(channel => channel with { ThalamicRelayActivation = 0f })
                            .ToArray()
                    }
                }
                : snapshot)
            .ToArray();

        var decision = NeuronalActionSelectionDecoder.Decode(snapshots);

        Assert.False(decision.Active);
        Assert.Contains("motor-thalamic relay", decision.AuthorityReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DopamineContingencyReversalChangesCorticostriatalSynapticPreference()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-action-channel-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        var channel0 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa0");
        var channel1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"action-reversal-{Guid.NewGuid():N}");
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.Striatum,
                "LIF",
                "DopamineModulatedSTDP",
                "action-channel reward reversal test",
                new DelayWindow(8, 12)));

            var tick = 0L;
            var timestamp = 0.0;
            (tick, timestamp) = await TrainChannel(engine, channel0, 0, reinforce: true, 36, tick, timestamp);
            (tick, timestamp) = await TrainChannel(engine, channel1, 1, reinforce: false, 36, tick, timestamp);
            var firstChannel0 = engine.GetInboundSynapseStrength(channel0);
            var firstChannel1 = engine.GetInboundSynapseStrength(channel1);

            (tick, timestamp) = await TrainChannel(engine, channel0, 0, reinforce: false, 72, tick, timestamp);
            (tick, timestamp) = await TrainChannel(engine, channel1, 1, reinforce: true, 72, tick, timestamp);
            var reversedChannel0 = engine.GetInboundSynapseStrength(channel0);
            var reversedChannel1 = engine.GetInboundSynapseStrength(channel1);

            Assert.True(firstChannel0 > firstChannel1,
                $"Initial contingency did not prefer channel 0: {firstChannel0:F5} <= {firstChannel1:F5}.");
            Assert.True(reversedChannel1 > reversedChannel0,
                $"Reversed contingency did not prefer channel 1: {reversedChannel1:F5} <= {reversedChannel0:F5}.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            EnvironmentGate.Release();
        }
    }

    [Fact]
    public async Task LearnedCorticostriatalStrengthSurvivesEngineRestart()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-action-channel-persistence", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        var instance = $"action-persistence-{Guid.NewGuid():N}";
        var synapseId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        StructureEngine? engine = null;

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", instance);
            engine = CreateStriatalEngine("action-channel persistence test");
            _ = await TrainChannel(engine, synapseId, 2, reinforce: true, 24, 0L, 0.0);
            var learned = engine.GetInboundSynapseStrength(synapseId);
            engine.Dispose();
            engine = null;

            using var reloaded = CreateStriatalEngine("action-channel persistence reload");
            Assert.Equal(learned, reloaded.GetInboundSynapseStrength(synapseId), precision: 5);
        }
        finally
        {
            engine?.Dispose();
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            EnvironmentGate.Release();
        }
    }

    [Fact]
    public void FirstArmLaneSelectsPhysicalEffectorWithoutSynthesizingLocomotion()
    {
        var decision = NeuronalActionSelectionDecoder.Decode(CreateCircuit(selectedChannel: 4));
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(decision, 0.8, 0.8);

        Assert.Equal(38, ActionChannelTopology.ChannelCount);
        Assert.True(decision.Active);
        Assert.Equal(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel, decision.SelectedChannel);
        Assert.Equal(0.0, shaped.Left);
        Assert.Equal(0.0, shaped.Right);
    }

    [Theory]
    [InlineData(NeuronalActionSelectionDecoder.LeftHipAbductionChannel)]
    [InlineData(NeuronalActionSelectionDecoder.LeftHipAdductionChannel)]
    [InlineData(NeuronalActionSelectionDecoder.RightHipAbductionChannel)]
    [InlineData(NeuronalActionSelectionDecoder.RightHipAdductionChannel)]
    public void LateralHipLanesDoNotLeakIntoLocomotorPopulations(int channel)
    {
        var decision = NeuronalActionSelectionDecoder.Decode(CreateCircuit(selectedChannel: channel));
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(decision, 0.8, 0.8);

        Assert.True(decision.Active);
        Assert.Equal(channel, decision.SelectedChannel);
        Assert.Equal(0.0, shaped.Left);
        Assert.Equal(0.0, shaped.Right);
    }

    [Theory]
    [InlineData(NeuronalActionSelectionDecoder.TrunkRotateLeftChannel)]
    [InlineData(NeuronalActionSelectionDecoder.TrunkRotateRightChannel)]
    public void AxialRotationLanesDoNotLeakIntoLocomotorPopulations(int channel)
    {
        var decision = NeuronalActionSelectionDecoder.Decode(CreateCircuit(selectedChannel: channel));
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(decision, 0.8, 0.8);

        Assert.True(decision.Active);
        Assert.Equal(channel, decision.SelectedChannel);
        Assert.Equal(0.0, shaped.Left);
        Assert.Equal(0.0, shaped.Right);
    }

    [Theory]
    [InlineData(NeuronalActionSelectionDecoder.StandChannel)]
    [InlineData(NeuronalActionSelectionDecoder.CrouchChannel)]
    [InlineData(NeuronalActionSelectionDecoder.SitChannel)]
    [InlineData(NeuronalActionSelectionDecoder.LieChannel)]
    public void PostureLanesDoNotLeakIntoLocomotorPopulations(int channel)
    {
        var decision = NeuronalActionSelectionDecoder.Decode(CreateCircuit(selectedChannel: channel));
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(decision, 0.8, 0.8);

        Assert.True(decision.Active);
        Assert.Equal(channel, decision.SelectedChannel);
        Assert.Equal(0.0, shaped.Left);
        Assert.Equal(0.0, shaped.Right);
    }

    [Fact]
    public void MissingActionCircuitCannotFallBackToRawLocomotorDrive()
    {
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(
            NeuronalActionDecision.Unavailable,
            0.9,
            0.7);

        Assert.Equal(0.0, shaped.Left);
        Assert.Equal(0.0, shaped.Right);
    }

    [Fact]
    public async Task LegacyGlobalNeuromodulationCannotAlterNeuronalPlasticity()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-local-neuromod-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        var synapseId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc0");
        StructureEngine? baseline = null;
        StructureEngine? legacyBiased = null;

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"local-baseline-{Guid.NewGuid():N}");
            baseline = CreateStriatalEngine("local neuromod baseline");
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"legacy-biased-{Guid.NewGuid():N}");
            legacyBiased = CreateStriatalEngine("legacy global neuromod invariant");

            for (var tick = 1L; tick <= 24; tick++)
            {
                var timestamp = tick * 30.0;
                var burst = Enumerable.Range(0, 6)
                    .Select(burstIndex => new SpikeMessage
                    {
                        MessageId = Guid.NewGuid(),
                        TimestampMs = timestamp + burstIndex * 0.2,
                        SourceStructure = StructureId.Pfc,
                        TargetStructure = StructureId.Striatum,
                        SourceNeuronId = "n-000",
                        TargetNeuronId = "striatal-lane-000",
                        SynapseId = synapseId,
                        Neurotransmitter = NTEnum.GLUTAMATE,
                        VesicleQuanta = 1f,
                        ReuptakeRate = 8f,
                        SpikeType = SpikeTypeEnum.BURST
                    })
                    .ToArray();

                await baseline.EnqueueSpikeBatchAsync(burst);
                await legacyBiased.EnqueueSpikeBatchAsync(burst);
                await baseline.ProcessTickAsync(Tick(tick, timestamp + 20.0, new NeuromodState(), 0f));
                await legacyBiased.ProcessTickAsync(Tick(
                    tick,
                    timestamp + 20.0,
                    new NeuromodState
                    {
                        DopamineLevel = 1f,
                        SerotoninLevel = 1f,
                        AcetylcholineLevel = 1f,
                        NorepinephrineLevel = 1f
                    },
                    1f));
            }

            Assert.Equal(
                baseline.GetInboundSynapseStrength(synapseId),
                legacyBiased.GetInboundSynapseStrength(synapseId),
                precision: 6);
        }
        finally
        {
            baseline?.Dispose();
            legacyBiased?.Dispose();
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            EnvironmentGate.Release();
        }
    }

    private static async Task<(long Tick, double Timestamp)> TrainChannel(
        StructureEngine engine,
        Guid synapseId,
        int channel,
        bool reinforce,
        int repetitions,
        long tick,
        double timestamp)
    {
        for (var i = 0; i < repetitions; i++)
        {
            tick++;
            timestamp += 30.0;
            var sourceIndex = channel + (reinforce ? ActionChannelTopology.ChannelCount : 0);
            var burst = Enumerable.Range(0, 6)
                .Select(burstIndex => new SpikeMessage
                {
                    MessageId = Guid.NewGuid(),
                    TimestampMs = timestamp + burstIndex * 0.2,
                    SourceStructure = StructureId.Pfc,
                    TargetStructure = StructureId.Striatum,
                    SourceNeuronId = $"n-{sourceIndex:000}",
                    TargetNeuronId = $"striatal-lane-{channel:000}",
                    SynapseId = synapseId,
                    Neurotransmitter = NTEnum.GLUTAMATE,
                    VesicleQuanta = 1f,
                    ReuptakeRate = 8f,
                    SpikeType = SpikeTypeEnum.BURST
                })
                .ToArray();
            var dopamineBurst = Enumerable.Range(0, 4)
                .Select(burstIndex => new SpikeMessage
                {
                    MessageId = Guid.NewGuid(),
                    TimestampMs = timestamp + burstIndex * 0.2,
                    SourceStructure = StructureId.Snc,
                    TargetStructure = StructureId.Striatum,
                    SourceNeuronId = $"n-{sourceIndex:000}",
                    TargetNeuronId = $"striatal-modulation-{channel:000}",
                    SynapseId = Guid.Parse(reinforce
                        ? $"dddddddd-dddd-dddd-dddd-dddddddddd{channel}1"
                        : $"dddddddd-dddd-dddd-dddd-dddddddddd{channel}2"),
                    Neurotransmitter = NTEnum.DOPAMINE,
                    VesicleQuanta = 1.4f,
                    ReuptakeRate = 40f,
                    SpikeType = SpikeTypeEnum.GRADED
                })
                .ToArray();
            await engine.EnqueueSpikeBatchAsync(burst.Concat(dopamineBurst).ToArray());
            await engine.ProcessTickAsync(Tick(tick, timestamp + 20.0, new NeuromodState(), 0f));
        }

        return (tick, timestamp);
    }

    private static TickSignal Tick(
        long tick,
        double timestamp,
        NeuromodState legacyGlobal,
        float legacyReward)
        => new(
            tick,
            timestamp,
            10.0,
            legacyGlobal,
            new Dictionary<BrainRhythm, double>(),
            legacyReward);

    private static SpikeMessage CorticostriatalSpike(
        int sourceIndex,
        int channel,
        double timestamp,
        float vesicleQuanta)
        => new()
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = timestamp,
            SourceStructure = StructureId.Pfc,
            TargetStructure = StructureId.Striatum,
            SourceNeuronId = $"n-{sourceIndex:000}",
            TargetNeuronId = $"striatal-lane-{channel:000}",
            SynapseId = Guid.NewGuid(),
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = vesicleQuanta,
            ReuptakeRate = 8f,
            SpikeType = SpikeTypeEnum.BURST
        };

    private static StructureEngine CreateStriatalEngine(string description)
        => new(new StructureProfile(
            StructureId.Striatum,
            "LIF",
            "DopamineModulatedSTDP",
            description,
            new DelayWindow(8, 12)));

    private static IReadOnlyList<InstanceStructureSnapshot> CreateCircuit(
        int selectedChannel,
        float selectedOutputInhibition = 0.04f,
        float selectedDirectActivation = 0.90f)
    {
        return
        [
            Snapshot(StructureId.Pfc, ProposalChannels(selectedChannel, 0.82f)),
            Snapshot(StructureId.Acc, ProposalChannels(selectedChannel, 0.65f)),
            Snapshot(StructureId.PremotorCortex, ProposalChannels(selectedChannel, 0.90f)),
            Snapshot(StructureId.Sma, ProposalChannels(selectedChannel, 0.78f)),
            Snapshot(StructureId.Striatum, StriatalChannels(selectedChannel, selectedDirectActivation)),
            Snapshot(StructureId.Stn, RoleChannels(selectedChannel, hyperdirect: 0.04f)),
            Snapshot(StructureId.GPi, RoleChannels(selectedChannel, output: selectedOutputInhibition)),
            Snapshot(StructureId.Snr, RoleChannels(selectedChannel, output: selectedOutputInhibition)),
            Snapshot(StructureId.MotorThalamus, RoleChannels(selectedChannel, thalamic: 0.82f))
        ];
    }

    private static int ForwardChannel() => NeuronalActionSelectionDecoder.ForwardChannel;

    private static ActionChannelActivity[] ProposalChannels(int selected, float activation)
        => Enumerable.Range(0, ActionChannelTopology.ChannelCount)
            .Select(channel => new ActionChannelActivity(
                channel,
                channel == selected ? activation : 0.06f,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f))
            .ToArray();

    private static ActionChannelActivity[] StriatalChannels(int selected, float direct)
        => Enumerable.Range(0, ActionChannelTopology.ChannelCount)
            .Select(channel => new ActionChannelActivity(
                channel,
                0f,
                channel == selected ? direct : 0.10f,
                channel == selected ? 0.05f : 0.32f,
                0f, 0f, 0f,
                channel == selected ? 0.35f : 0f,
                channel == selected ? 2.8f : 0.8f,
                0f))
            .ToArray();

    private static ActionChannelActivity[] RoleChannels(
        int selected,
        float hyperdirect = 0f,
        float output = 0f,
        float thalamic = 0f)
        => Enumerable.Range(0, ActionChannelTopology.ChannelCount)
            .Select(channel => new ActionChannelActivity(
                channel,
                0f, 0f, 0f,
                channel == selected ? hyperdirect : Math.Max(hyperdirect, 0.20f),
                channel == selected ? output : Math.Max(output, 0.40f),
                channel == selected ? thalamic : 0.06f,
                0f, 0f, 0f))
            .ToArray();

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        IReadOnlyList<ActionChannelActivity> channels)
    {
        var diagnostics = new ActionSelectionDiagnostics(structure, channels, 0, 0f, 0.6f);
        return new InstanceStructureSnapshot(
            new ServiceInstance(structure, $"{structure}-M", "M", new Uri("http://localhost")),
            structure,
            32,
            4f,
            BrainRhythm.BETA,
            [],
            new NeuromodState(),
            0,
            0,
            0,
            ActionSelectionDiagnostics: diagnostics);
    }
}
