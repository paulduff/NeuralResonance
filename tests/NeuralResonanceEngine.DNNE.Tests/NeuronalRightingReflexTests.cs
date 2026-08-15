using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class NeuronalRightingReflexTests
{
    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);

    [Theory]
    [InlineData(StructureId.ProprioceptiveAfferents)]
    [InlineData(StructureId.VestibularAfferents)]
    public void PrimaryRightingAfferentsExposeRateCodedChannelDiagnostics(StructureId structure)
    {
        Assert.True(ActionChannelTopology.IsActionCircuitStructure(structure));
        Assert.True(ActionChannelTopology.IsRateCodedAfferentOrReflexStructure(structure));
    }

    [Theory]
    [InlineData("L:otolith_pitch_forward")]
    [InlineData("R:otolith_roll_right")]
    [InlineData("L:dynamic_balance_margin_loss")]
    public void FallReceptorsEnterDedicatedStandLane(string receptor)
    {
        var spike = Afferent(StructureId.VestibularAfferents, receptor, 4.0f);

        var mapped = RightingReflexTopology.TryProjectInbound(spike, 256, out var index);

        Assert.True(mapped);
        Assert.Equal(
            RightingReflexTopology.StandChannel,
            ActionChannelTopology.ChannelForNeuron(index, StructureId.VestibularAfferents));
    }

    [Theory]
    [InlineData("L:center_of_mass_ahead_of_pressure")]
    [InlineData("R:support_margin_loss")]
    [InlineData("L:support_area_narrowing")]
    public void SupportLossReceptorsEnterDedicatedStandLane(string receptor)
    {
        var spike = Afferent(StructureId.ProprioceptiveAfferents, receptor, 3.0f);

        var mapped = RightingReflexTopology.TryProjectInbound(spike, 224, out var index);

        Assert.True(mapped);
        Assert.Equal(
            RightingReflexTopology.StandChannel,
            ActionChannelTopology.ChannelForNeuron(index, StructureId.ProprioceptiveAfferents));
    }

    [Fact]
    public void OrdinaryHeadMotionDoesNotAcquireRightingIdentity()
    {
        var spike = Afferent(StructureId.VestibularAfferents, "L:horizontal_canal", 4.0f);

        Assert.False(RightingReflexTopology.TryProjectInbound(spike, 256, out _));
    }

    [Fact]
    public void SubthresholdTiltDoesNotRecruitRightingPopulation()
    {
        var spike = Afferent(StructureId.VestibularAfferents, "L:otolith_pitch_forward", 0.70f);

        Assert.False(RightingReflexTopology.TryProjectInbound(spike, 256, out _));
    }

    [Fact]
    public void RightingLaneIsPreservedAcrossVestibulospinalProjection()
    {
        var spike = Relay(
            StructureId.VestibularNuclei,
            StructureId.SpinalCordMotor,
            sourceIndex: 41);

        var mapped = RightingReflexTopology.TryProjectInbound(spike, 256, out var targetIndex);

        Assert.True(mapped);
        Assert.Equal(
            RightingReflexTopology.StandChannel,
            ActionChannelTopology.ChannelForNeuron(targetIndex, StructureId.SpinalCordMotor));
    }

    [Fact]
    public void RightingFibresConvergeOnCompactSpinalInterneuronPool()
    {
        var first = Relay(
            StructureId.VestibularNuclei,
            StructureId.SpinalCordMotor,
            sourceIndex: 41);
        var second = Relay(
            StructureId.VestibularNuclei,
            StructureId.SpinalCordMotor,
            sourceIndex: 50);

        Assert.True(RightingReflexTopology.TryProjectInbound(first, 256, out var firstTarget));
        Assert.True(RightingReflexTopology.TryProjectInbound(second, 256, out var secondTarget));
        Assert.Equal(
            RightingReflexTopology.StandChannel,
            ActionChannelTopology.ChannelForNeuron(firstTarget, StructureId.SpinalCordMotor));
        Assert.Equal(
            RightingReflexTopology.StandChannel,
            ActionChannelTopology.ChannelForNeuron(secondTarget, StructureId.SpinalCordMotor));
        Assert.NotEqual(firstTarget, secondTarget);
        Assert.InRange(firstTarget, 0, ActionChannelTopology.ChannelCount * RightingReflexTopology.SpinalRightingInterneuronPoolSize - 1);
        Assert.InRange(secondTarget, 0, ActionChannelTopology.ChannelCount * RightingReflexTopology.SpinalRightingInterneuronPoolSize - 1);
    }

    [Fact]
    public void SpinalRightingFibresReceiveBoundedBiologicalSynapticEfficacy()
    {
        var righting = Relay(
            StructureId.ProprioceptiveAfferents,
            StructureId.SpinalCordMotor,
            sourceIndex: 41);
        var otherLane = Relay(
            StructureId.ProprioceptiveAfferents,
            StructureId.SpinalCordMotor,
            sourceIndex: 40);

        var strengthened = RightingReflexTopology.ApplySpinalRelayEfficacy(righting, 1.4f);
        var unchanged = RightingReflexTopology.ApplySpinalRelayEfficacy(otherLane, 1.4f);

        Assert.Equal(1.4f * RightingReflexTopology.SpinalRightingRelayGain, strengthened, precision: 5);
        Assert.Equal(1.4f, unchanged, precision: 5);
        Assert.Equal(5f, RightingReflexTopology.ApplySpinalRelayEfficacy(righting, 5f));
    }

    [Fact]
    public async Task ConvergentRightingFibresRecruitSpinalStandPopulation()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-righting-spinal-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            var quietStand = await MeasureSpinalStandResponseAsync(stimulateRightingLane: false);
            var stimulatedStand = await MeasureSpinalStandResponseAsync(stimulateRightingLane: true);
            var strongestStimulatedStand = stimulatedStand.Max();
            var strongestEvokedDifference = stimulatedStand
                .Zip(quietStand, static (stimulated, quiet) => stimulated - quiet)
                .Max();

            Assert.True(strongestStimulatedStand >= 0.05f,
                $"Convergent righting fibres did not recruit the spinal stand population: {strongestStimulatedStand:F4}.");
            Assert.True(strongestEvokedDifference >= 0.04f,
                $"Spinal stand response did not exceed its quiet baseline: quiet=[{string.Join(",", quietStand.Select(static value => value.ToString("F3")))}], stimulated=[{string.Join(",", stimulatedStand.Select(static value => value.ToString("F3")))}].");
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
    public void OtherVestibularLanesRemainOutsideRightingProjection()
    {
        var spike = Relay(
            StructureId.VestibularNuclei,
            StructureId.SpinalCordMotor,
            sourceIndex: 40);

        Assert.False(RightingReflexTopology.TryProjectInbound(spike, 256, out _));
    }

    [Fact]
    public void UntaggedStandLaneTrafficCannotEnterRightingRelay()
    {
        var spike = Relay(
            StructureId.ProprioceptiveAfferents,
            StructureId.SpinalCordMotor,
            sourceIndex: 41);
        spike.IsRightingCircuitSpike = false;

        Assert.False(RightingReflexTopology.TryProjectInbound(spike, 256, out _));
        Assert.False(RightingReflexTopology.IsSpinalRightingRelay(spike));
    }

    [Fact]
    public async Task SparsePrimaryFallPopulationProducesSpikeConfirmedRightingActivity()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-righting-afferent-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"righting-afferent-{Guid.NewGuid():N}");
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.VestibularAfferents,
                "Izhikevich",
                "STDP",
                "sparse primary righting afferent test",
                new DelayWindow(1, 1)));

            var strongestStand = 0f;
            var strongestOther = 0f;
            for (var tick = 1L; tick <= 20; tick++)
            {
                var timestamp = tick * 20.0;
                var burst = Enumerable.Range(0, 6)
                    .Select(fiber =>
                    {
                        var spike = Afferent(
                            StructureId.VestibularAfferents,
                            $"L:otolith_pitch_forward:fiber_{fiber}",
                            4.5f);
                        spike.TimestampMs = timestamp + (fiber * 0.1);
                        return spike;
                    })
                    .ToArray();
                await engine.EnqueueSpikeBatchAsync(burst);
                var ack = await engine.ProcessTickAsync(new TickSignal(
                    tick,
                    timestamp + 10.0,
                    10.0,
                    new NeuromodState(),
                    new Dictionary<BrainRhythm, double>(),
                    0f));
                var channels = Assert.IsType<ActionSelectionDiagnostics>(ack.ActionSelectionDiagnostics).Channels;
                strongestStand = Math.Max(
                    strongestStand,
                    channels.Single(channel => channel.ChannelIndex == RightingReflexTopology.StandChannel).ReflexDrive);
                strongestOther = Math.Max(
                    strongestOther,
                    channels.Where(channel => channel.ChannelIndex != RightingReflexTopology.StandChannel)
                        .Max(static channel => channel.ReflexDrive));
            }

            Assert.True(strongestStand >= 0.05f,
                $"Sparse primary righting population was diluted below visibility: {strongestStand:F4}.");
            Assert.True(strongestStand > strongestOther,
                $"Righting lane was not dominant: stand={strongestStand:F4}, other={strongestOther:F4}.");
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
    public async Task OrdinaryVestibularTrafficDoesNotMasqueradeAsRightingEvidence()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-righting-ordinary-afferent-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"ordinary-afferent-{Guid.NewGuid():N}");
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.VestibularAfferents,
                "Izhikevich",
                "STDP",
                "ordinary vestibular traffic isolation test",
                new DelayWindow(1, 1)));

            var strongestGeneralActivity = 0f;
            var strongestReflexDrive = 0f;
            for (var tick = 1L; tick <= 20; tick++)
            {
                var timestamp = tick * 20.0;
                var burst = Enumerable.Range(0, 6)
                    .Select(fiber =>
                    {
                        var spike = Afferent(
                            StructureId.VestibularAfferents,
                            $"L:horizontal_canal:fiber_{fiber}",
                            4.5f);
                        spike.TimestampMs = timestamp + (fiber * 0.1);
                        return spike;
                    })
                    .ToArray();
                await engine.EnqueueSpikeBatchAsync(burst);
                var ack = await engine.ProcessTickAsync(new TickSignal(
                    tick,
                    timestamp + 10.0,
                    10.0,
                    new NeuromodState(),
                    new Dictionary<BrainRhythm, double>(),
                    0f));
                var channels = Assert.IsType<ActionSelectionDiagnostics>(ack.ActionSelectionDiagnostics).Channels;
                strongestGeneralActivity = Math.Max(
                    strongestGeneralActivity,
                    channels.Max(static channel => channel.SelectionScore));
                strongestReflexDrive = Math.Max(
                    strongestReflexDrive,
                    channels.Max(static channel => channel.ReflexDrive));
            }

            Assert.True(strongestGeneralActivity >= 0.05f,
                $"Ordinary vestibular test traffic did not recruit its neurons: {strongestGeneralActivity:F4}.");
            Assert.Equal(0f, strongestReflexDrive);
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
    public async Task RightingTraceDecaysByElapsedSimulationTimeAcrossSchedulerGap()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-righting-trace-decay-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"righting-trace-decay-{Guid.NewGuid():N}");
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.ProprioceptiveAfferents,
                "Izhikevich",
                "STDP",
                "righting trace scheduler-gap test",
                new DelayWindow(1, 1)));

            var peakDrive = 0f;
            for (var tick = 1L; tick <= 8; tick++)
            {
                var timestamp = tick * 20.0;
                var burst = Enumerable.Range(0, 6)
                    .Select(fiber =>
                    {
                        var spike = Afferent(
                            StructureId.ProprioceptiveAfferents,
                            $"L:support_margin_loss:fiber_{fiber}",
                            4.8f);
                        spike.TimestampMs = timestamp;
                        return spike;
                    })
                    .ToArray();
                await engine.EnqueueSpikeBatchAsync(burst);
                var ack = await engine.ProcessTickAsync(new TickSignal(
                    tick,
                    timestamp + 10.0,
                    10.0,
                    new NeuromodState(),
                    new Dictionary<BrainRhythm, double>(),
                    0f));
                peakDrive = Math.Max(
                    peakDrive,
                    Assert.IsType<ActionSelectionDiagnostics>(ack.ActionSelectionDiagnostics).Channels
                        .Single(channel => channel.ChannelIndex == RightingReflexTopology.StandChannel)
                        .ReflexDrive);
            }

            var afterGap = await engine.ProcessTickAsync(new TickSignal(
                9,
                920.0,
                10.0,
                new NeuromodState(),
                new Dictionary<BrainRhythm, double>(),
                0f));
            var releasedDrive = Assert.IsType<ActionSelectionDiagnostics>(afterGap.ActionSelectionDiagnostics).Channels
                .Single(channel => channel.ChannelIndex == RightingReflexTopology.StandChannel)
                .ReflexDrive;

            Assert.True(peakDrive >= 0.05f, $"Righting trace never activated: {peakDrive:F4}.");
            Assert.True(releasedDrive < peakDrive * 0.25f,
                $"Righting trace ignored elapsed scheduler time: peak={peakDrive:F4}, afterGap={releasedDrive:F4}.");
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

    private static SpikeMessage Afferent(StructureId structure, string receptor, float quanta)
        => new()
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = 100.0,
            SourceStructure = structure,
            TargetStructure = structure,
            SourceNeuronId = receptor,
            TargetNeuronId = $"primary:{receptor}:fiber_0",
            SynapseId = Guid.NewGuid(),
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = quanta,
            ReuptakeRate = 4.0f,
            SpikeType = SpikeTypeEnum.BURST,
            IsFeedback = false
        };

    private static async Task<float[]> MeasureSpinalStandResponseAsync(bool stimulateRightingLane)
    {
        Environment.SetEnvironmentVariable(
            "SERVICE_INSTANCE",
            $"righting-spinal-{(stimulateRightingLane ? "stimulated" : "quiet")}-{Guid.NewGuid():N}");
        using var engine = new StructureEngine(new StructureProfile(
            StructureId.SpinalCordMotor,
            "Izhikevich",
            "STDP",
            "convergent spinal righting test",
            new DelayWindow(1, 1)));

        var standResponse = new float[12];
        for (var tick = 1L; tick <= 12; tick++)
        {
            var timestamp = tick * 20.0;
            if (stimulateRightingLane && tick == 1)
            {
                foreach (var sourceIndex in new[] { 14, 59, 185 })
                {
                    var spike = Relay(
                        StructureId.ProprioceptiveAfferents,
                        StructureId.SpinalCordMotor,
                        sourceIndex);
                    spike.TimestampMs = timestamp;
                    spike.VesicleQuanta = 1.08f;
                    await engine.EnqueueSpikeAsync(spike);
                }
            }

            var ack = await engine.ProcessTickAsync(new TickSignal(
                tick,
                timestamp + 10.0,
                10.0,
                new NeuromodState(),
                new Dictionary<BrainRhythm, double>(),
                0f));
            var channels = Assert.IsType<ActionSelectionDiagnostics>(ack.ActionSelectionDiagnostics).Channels;
            standResponse[tick - 1] = channels
                .Single(channel => channel.ChannelIndex == RightingReflexTopology.StandChannel)
                .ReflexDrive;
        }

        return standResponse;
    }

    private static SpikeMessage Relay(
        StructureId source,
        StructureId target,
        int sourceIndex)
        => new()
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = 120.0,
            SourceStructure = source,
            TargetStructure = target,
            SourceNeuronId = $"n-{sourceIndex:000}",
            TargetNeuronId = $"auto-{target}-000",
            SynapseId = Guid.NewGuid(),
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = 2.0f,
            ReuptakeRate = 4.0f,
            SpikeType = SpikeTypeEnum.ACTION_POTENTIAL,
            IsFeedback = false,
            IsRightingCircuitSpike = true
        };
}
