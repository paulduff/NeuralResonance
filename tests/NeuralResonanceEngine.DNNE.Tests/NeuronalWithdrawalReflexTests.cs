using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class NeuronalWithdrawalReflexTests
{
    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);

    [Theory]
    [InlineData("R:hand:free_nerve_ending_mechanonociceptor:sector_1:fiber_0", ActionChannelTopology.LeftShoulderExtensionChannel)]
    [InlineData("R:hand:free_nerve_ending_mechanonociceptor:sector_1:fiber_1", ActionChannelTopology.LeftElbowFlexionChannel)]
    [InlineData("L:hand:free_nerve_ending_mechanonociceptor:sector_1:fiber_0", ActionChannelTopology.RightShoulderExtensionChannel)]
    [InlineData("L:hand:free_nerve_ending_mechanonociceptor:sector_1:fiber_1", ActionChannelTopology.RightElbowFlexionChannel)]
    public void HandNociceptorsRecruitContralaterallyEncodedWithdrawalPools(
        string sourceNeuronId,
        int expectedChannel)
    {
        var spike = NociceptiveCollateral(sourceNeuronId);

        var mapped = WithdrawalReflexTopology.TryProjectInbound(spike, 256, out var targetIndex);

        Assert.True(mapped);
        Assert.Equal(
            expectedChannel,
            ActionChannelTopology.ChannelForNeuron(targetIndex, StructureId.SpinalCordMotor));
    }

    [Theory]
    [InlineData("R:hand:free_nerve_ending_mechanonociceptor:normal_z_neg:sector_1:fiber_2", ActionChannelTopology.ReverseChannel)]
    [InlineData("L:forearm:free_nerve_ending_mechanonociceptor:normal_z_pos:sector_1:fiber_2", ActionChannelTopology.ForwardChannel)]
    [InlineData("R:arm:free_nerve_ending_mechanonociceptor:normal_x_pos:sector_1:fiber_2", ActionChannelTopology.RightTurnChannel)]
    [InlineData("R:arm:free_nerve_ending_mechanonociceptor:normal_x_pos:sector_1:fiber_3", ActionChannelTopology.TrunkRotateRightChannel)]
    public void UpperLimbNociceptorsAlsoRecruitDirectionSensitiveAxialRelease(
        string sourceNeuronId,
        int expectedChannel)
    {
        var spike = NociceptiveCollateral(sourceNeuronId);

        Assert.True(WithdrawalReflexTopology.TryProjectInbound(spike, 256, out var targetIndex));
        Assert.Equal(
            expectedChannel,
            ActionChannelTopology.ChannelForNeuron(targetIndex, StructureId.SpinalCordMotor));
    }

    [Theory]
    [InlineData("R:foot:free_nerve_ending_mechanonociceptor:sector_2:fiber_0", ActionChannelTopology.LeftAnkleDorsiflexionChannel)]
    [InlineData("R:foot:free_nerve_ending_mechanonociceptor:sector_2:fiber_1", ActionChannelTopology.LeftHipAbductionChannel)]
    [InlineData("L:shin:free_nerve_ending_mechanonociceptor:sector_2:fiber_0", ActionChannelTopology.RightAnkleDorsiflexionChannel)]
    [InlineData("L:shin:free_nerve_ending_mechanonociceptor:sector_2:fiber_1", ActionChannelTopology.RightHipAbductionChannel)]
    public void LowerLimbNociceptorsRecruitFlexorAndSupportWideningPools(
        string sourceNeuronId,
        int expectedChannel)
    {
        var spike = NociceptiveCollateral(sourceNeuronId);

        Assert.True(WithdrawalReflexTopology.TryProjectInbound(spike, 256, out var targetIndex));
        Assert.Equal(
            expectedChannel,
            ActionChannelTopology.ChannelForNeuron(targetIndex, StructureId.SpinalCordMotor));
    }

    [Theory]
    [InlineData("R:chest:free_nerve_ending_mechanonociceptor:normal_z_pos:sector_2:fiber_0", ActionChannelTopology.ForwardChannel)]
    [InlineData("L:pelvis:free_nerve_ending_mechanonociceptor:normal_z_neg:sector_2:fiber_0", ActionChannelTopology.ReverseChannel)]
    [InlineData("R:chest:free_nerve_ending_mechanonociceptor:normal_x_pos:sector_2:fiber_0", ActionChannelTopology.RightTurnChannel)]
    [InlineData("R:chest:free_nerve_ending_mechanonociceptor:normal_x_pos:sector_2:fiber_1", ActionChannelTopology.TrunkRotateRightChannel)]
    public void AxialNociceptorsRecruitDirectionSensitiveReleasePools(
        string sourceNeuronId,
        int expectedChannel)
    {
        var spike = NociceptiveCollateral(sourceNeuronId);

        Assert.True(WithdrawalReflexTopology.TryProjectInbound(spike, 256, out var targetIndex));
        Assert.Equal(
            expectedChannel,
            ActionChannelTopology.ChannelForNeuron(targetIndex, StructureId.SpinalCordMotor));
    }

    [Fact]
    public void WithdrawalRouteRetainsAnatomicalSourceAndPhysicalProjection()
    {
        var spike = NociceptiveCollateral(
            "R:chest:free_nerve_ending_mechanonociceptor:normal_z_neg:sector_2:fiber_0");

        Assert.True(WithdrawalReflexTopology.TryResolveSourceRoute(spike, out var route));
        Assert.Equal("left", route.BodySide);
        Assert.Equal("chest", route.Region);
        Assert.Equal("normal_z_neg", route.ContactNormalSector);
        Assert.Equal(ActionChannelTopology.ReverseChannel, route.ChannelIndex);
        Assert.Equal("reverse", route.MotorProjection);
        Assert.Equal("left:chest:normal_z_neg:channel_3", route.SourceKey);
    }

    [Fact]
    public void OrdinaryTouchCannotAcquireWithdrawalAuthority()
    {
        var spike = NociceptiveCollateral("R:hand:merkel_sa1:sector_1:fiber_0");

        Assert.False(WithdrawalReflexTopology.TryProjectInbound(spike, 256, out _));
        Assert.False(WithdrawalReflexTopology.IsEvokedWithdrawalInput(spike));
    }

    [Fact]
    public void NociceptiveSpinalCollateralReceivesBoundedRelayEfficacy()
    {
        var spike = NociceptiveCollateral(
            "R:hand:free_nerve_ending_mechanonociceptor:sector_1:fiber_0");

        Assert.Equal(
            1.2f * WithdrawalReflexTopology.SpinalNociceptiveRelayGain,
            WithdrawalReflexTopology.ApplySpinalRelayEfficacy(spike, 1.2f),
            precision: 5);
        Assert.Equal(5f, WithdrawalReflexTopology.ApplySpinalRelayEfficacy(spike, 5f));
    }

    [Fact]
    public void RecurrentInhibitionSuppressesUnchangedWithdrawalMoreThanAcuteThreat()
    {
        var unchanged = NociceptiveCollateral(
            "R:hand:free_nerve_ending_mechanonociceptor:sector_1:fiber_0");
        unchanged.VesicleQuanta = 1.2f;
        var acute = NociceptiveCollateral(
            "R:hand:free_nerve_ending_mechanonociceptor:sector_2:fiber_0");
        acute.VesicleQuanta = 5f;

        var inhibitedUnchanged = WithdrawalReflexTopology.ApplyRecurrentInhibition(
            unchanged,
            unchanged.VesicleQuanta,
            inhibitoryTrace: 1f);
        var inhibitedAcute = WithdrawalReflexTopology.ApplyRecurrentInhibition(
            acute,
            acute.VesicleQuanta,
            inhibitoryTrace: 1f);

        Assert.True(inhibitedUnchanged < unchanged.VesicleQuanta * 0.25f);
        Assert.True(inhibitedAcute > acute.VesicleQuanta * 0.75f);
        Assert.True(inhibitedAcute > inhibitedUnchanged);
    }

    [Fact]
    public void RecurrentWithdrawalInhibitionCannotSuppressOrdinaryTouch()
    {
        var ordinaryTouch = NociceptiveCollateral("R:hand:merkel_sa1:sector_1:fiber_0");

        Assert.Equal(
            ordinaryTouch.VesicleQuanta,
            WithdrawalReflexTopology.ApplyRecurrentInhibition(
                ordinaryTouch,
                ordinaryTouch.VesicleQuanta,
                inhibitoryTrace: 1f));
    }

    [Fact]
    public async Task SustainedNociceptivePopulationRecruitsSpikingWithdrawalPools()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-withdrawal-spinal-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"withdrawal-spinal-{Guid.NewGuid():N}");
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.SpinalCordMotor,
                "Izhikevich",
                "STDP",
                "spinal nociceptive withdrawal test",
                new DelayWindow(1, 1)));

            var peakShoulder = 0f;
            var peakElbow = 0f;
            IReadOnlyList<SpinalWithdrawalSourceActivity> attributedSources = [];
            for (var tick = 1L; tick <= 18; tick++)
            {
                var timestamp = tick * 20.0;
                var burst = Enumerable.Range(0, 8)
                    .Select(fiber =>
                    {
                        var spike = NociceptiveCollateral(
                            $"R:hand:free_nerve_ending_mechanonociceptor:sector_4:fiber_{fiber}");
                        spike.TimestampMs = timestamp + (fiber * 0.1);
                        spike.VesicleQuanta = 4.6f;
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
                var diagnostics = Assert.IsType<ActionSelectionDiagnostics>(ack.ActionSelectionDiagnostics);
                var channels = diagnostics.Channels;
                if (diagnostics.WithdrawalSources is { Count: > 0 })
                {
                    attributedSources = diagnostics.WithdrawalSources;
                }
                peakShoulder = Math.Max(
                    peakShoulder,
                    channels.Single(channel =>
                        channel.ChannelIndex == ActionChannelTopology.LeftShoulderExtensionChannel).ReflexDrive);
                peakElbow = Math.Max(
                    peakElbow,
                    channels.Single(channel =>
                        channel.ChannelIndex == ActionChannelTopology.LeftElbowFlexionChannel).ReflexDrive);
            }

            Assert.True(peakShoulder >= 0.04f, $"Shoulder withdrawal pool remained quiet: {peakShoulder:F4}.");
            Assert.True(peakElbow >= 0.04f, $"Elbow withdrawal pool remained quiet: {peakElbow:F4}.");
            Assert.Contains(
                attributedSources,
                source => source.SourceKey == "left:hand:unspecified:channel_5" &&
                          source.MotorProjection == "left_shoulder_extension" &&
                          source.ReflexDrive > 0f);
            Assert.Contains(
                attributedSources,
                source => source.SourceKey == "left:hand:unspecified:channel_12" &&
                          source.MotorProjection == "left_elbow_flexion" &&
                          source.ReflexDrive > 0f);
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
    public async Task WithdrawalSourceAndMotorAuthorityExpireWithoutFreshAfferentEvidence()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-withdrawal-expiry-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"withdrawal-expiry-{Guid.NewGuid():N}");
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.SpinalCordMotor,
                "Izhikevich",
                "STDP",
                "spinal nociceptive expiry test",
                new DelayWindow(1, 1)));

            var observedAttributedSource = false;
            TickAck? final = null;
            for (var tick = 1L; tick <= 44; tick++)
            {
                var timestamp = tick * 20.0;
                if (tick <= 12)
                {
                    var burst = Enumerable.Range(0, 8)
                        .Select(fiber =>
                        {
                            var spike = NociceptiveCollateral(
                                $"R:hand:free_nerve_ending_mechanonociceptor:sector_4:fiber_{fiber}");
                            spike.TimestampMs = timestamp + (fiber * 0.1);
                            spike.VesicleQuanta = 4.6f;
                            return spike;
                        })
                        .ToArray();
                    await engine.EnqueueSpikeBatchAsync(burst);
                }

                final = await engine.ProcessTickAsync(new TickSignal(
                    tick,
                    timestamp + 10.0,
                    10.0,
                    new NeuromodState(),
                    new Dictionary<BrainRhythm, double>(),
                    0f));
                observedAttributedSource |= final.ActionSelectionDiagnostics?.WithdrawalSources is { Count: > 0 };
            }

            Assert.True(observedAttributedSource);
            var diagnostics = Assert.IsType<ActionSelectionDiagnostics>(final!.ActionSelectionDiagnostics);
            Assert.Empty(diagnostics.WithdrawalSources ?? []);
            Assert.All(
                diagnostics.Channels.Where(channel =>
                    WithdrawalReflexTopology.IsWithdrawalChannel(channel.ChannelIndex)),
                channel => Assert.Equal(0f, channel.ReflexDrive));
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

    private static SpikeMessage NociceptiveCollateral(string sourceNeuronId) => new()
    {
        MessageId = Guid.NewGuid(),
        TimestampMs = 10.0,
        SourceStructure = StructureId.SomaticAfferents,
        TargetStructure = StructureId.SpinalCordMotor,
        SourceNeuronId = sourceNeuronId,
        TargetNeuronId = "spinal-withdrawal",
        SynapseId = Guid.Parse("7a7c1098-c2cc-4b4d-a62e-f3b79b03aa42"),
        Neurotransmitter = NTEnum.GLUTAMATE,
        VesicleQuanta = 1.2f,
        ReuptakeRate = 4f,
        SpikeType = SpikeTypeEnum.BURST
    };
}
