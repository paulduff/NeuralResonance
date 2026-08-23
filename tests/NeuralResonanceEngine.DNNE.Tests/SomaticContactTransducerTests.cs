using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class SomaticContactTransducerTests
{
    [Fact]
    public void ContactOnLeftBodyMapsToRightHemisphereAfferents()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(bodyPositionX: -0.2f));

        var result = transducer.Transduce(descriptor, tick: 12, timestampMs: 42.0);

        Assert.Empty(result.LeftHemisphereSpikes);
        Assert.NotEmpty(result.RightHemisphereSpikes);
        Assert.All(result.RightHemisphereSpikes, spike =>
        {
            Assert.Equal(StructureId.SomaticAfferents, spike.SourceStructure);
            Assert.Equal(StructureId.SomaticAfferents, spike.TargetStructure);
            Assert.StartsWith("R:", spike.SourceNeuronId, StringComparison.Ordinal);
            Assert.NotEqual(Guid.Empty, spike.SynapseId);
            Assert.Null(spike.ModulationContext);
        });
    }

    [Fact]
    public void ContactOnRightBodyMapsToLeftHemisphereAfferents()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(bodyPositionX: 0.2f));

        var result = transducer.Transduce(descriptor, tick: 12, timestampMs: 42.0);

        Assert.NotEmpty(result.LeftHemisphereSpikes);
        Assert.Empty(result.RightHemisphereSpikes);
        Assert.All(result.LeftHemisphereSpikes, spike => Assert.StartsWith("L:", spike.SourceNeuronId, StringComparison.Ordinal));
    }

    [Fact]
    public void MidlineContactProducesBilateralAfference()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(bodyPositionX: 0f));

        var result = transducer.Transduce(descriptor, tick: 18, timestampMs: 51.0);

        Assert.NotEmpty(result.LeftHemisphereSpikes);
        Assert.NotEmpty(result.RightHemisphereSpikes);
    }

    [Fact]
    public void RepeatedPressureAdaptsRapidOnsetPopulation()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(bodyPositionX: 0f, impulse: 0f));

        var first = transducer.Transduce(descriptor, tick: 20, timestampMs: 60.0);
        var second = transducer.Transduce(descriptor with { Sequence = descriptor.Sequence + 1 }, tick: 21, timestampMs: 61.0);

        Assert.True(first.OnsetActivation > second.OnsetActivation);
        Assert.True(second.PressureActivation > 0f);
    }

    [Fact]
    public void RetriedSourceSequenceReusesTheOriginalTransductionAndSpikeIdentities()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(bodyPositionX: 0.2f));

        var first = transducer.Transduce(descriptor, tick: 20, timestampMs: 60.0);
        var replay = transducer.Transduce(descriptor, tick: 21, timestampMs: 2_060.0);
        var newSessionFrame = transducer.Transduce(
            descriptor with { TimestampMs = descriptor.TimestampMs + 1 },
            tick: 22,
            timestampMs: 2_080.0);

        Assert.Same(first, replay);
        Assert.NotSame(first, newSessionFrame);
        Assert.Equal(
            first.LeftHemisphereSpikes
                .Concat(first.RightHemisphereSpikes)
                .Concat(first.LeftSpinalWithdrawalSpikes)
                .Concat(first.RightSpinalWithdrawalSpikes)
                .Select(static spike => spike.MessageId),
            replay.LeftHemisphereSpikes
                .Concat(replay.RightHemisphereSpikes)
                .Concat(replay.LeftSpinalWithdrawalSpikes)
                .Concat(replay.RightSpinalWithdrawalSpikes)
                .Select(static spike => spike.MessageId));
    }

    [Fact]
    public void HighForceAndPenetrationActivateMechanicalNociceptors()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(bodyPositionX: 0f, force: 3_500f, penetration: 30f));

        var result = transducer.Transduce(descriptor, tick: 30, timestampMs: 70.0);

        Assert.True(result.HighThresholdActivation > 0f);
        Assert.Contains(result.LeftHemisphereSpikes, spike =>
            spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal));
        Assert.Equal(0, result.GeneratedSpinalWithdrawalSpikes);
    }

    [Fact]
    public void SustainedModerateForceBecomesMorePainfulWhenPressureContinues()
    {
        var brief = new SomaticContactTransducerRuntime().Transduce(
            CreateDescriptor(CreateFrame(
                bodyPositionX: 0.42f,
                force: 520f,
                impulse: 0f,
                penetration: 0.3f,
                contactArea: 20_000f,
                duration: 40f)),
            tick: 32,
            timestampMs: 72.0);
        var sustained = new SomaticContactTransducerRuntime().Transduce(
            CreateDescriptor(CreateFrame(
                bodyPositionX: 0.42f,
                force: 520f,
                impulse: 0f,
                penetration: 0.3f,
                contactArea: 20_000f,
                duration: 1_200f)),
            tick: 32,
            timestampMs: 72.0);

        Assert.True(sustained.HighThresholdActivation > brief.HighThresholdActivation);
        Assert.Contains(sustained.LeftHemisphereSpikes, spike =>
            spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal));
        Assert.True(sustained.GeneratedSpinalWithdrawalSpikes > 0);
        Assert.True(sustained.GeneratedSpinalWithdrawalSpikes >= brief.GeneratedSpinalWithdrawalSpikes);
        Assert.All(sustained.LeftSpinalWithdrawalSpikes, spike =>
        {
            Assert.Equal(StructureId.SomaticAfferents, spike.SourceStructure);
            Assert.Equal(StructureId.SpinalCordMotor, spike.TargetStructure);
            Assert.Contains("mechanonociceptor", spike.SourceNeuronId, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void OrdinaryLongDurationPlantarSupportDoesNotBecomePainfulFromDurationAlone()
    {
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: -0.14f,
            force: 360f,
            impulse: 8f,
            penetration: 1.2f,
            bodyPositionY: -0.72f,
            bodyPositionZ: 0.08f,
            contactArea: 6_200f,
            duration: 60_000f) with
        {
            SurfaceNormalY = 1f,
            SurfaceNormalZ = 0f,
            InputSource = "avatar_world_left_foot_support"
        });

        var result = new SomaticContactTransducerRuntime().Transduce(
            descriptor,
            tick: 34,
            timestampMs: 74.0);

        Assert.Equal(0f, result.HighThresholdActivation);
        Assert.DoesNotContain(result.RightHemisphereSpikes, spike =>
            spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal));
    }

    [Fact]
    public void PhysiologicalPeakPlantarPressureDoesNotRecruitSpinalWithdrawal()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: -0.14f,
            force: 542.5f,
            impulse: 12f,
            penetration: 1.2f,
            bodyPositionY: -0.72f,
            bodyPositionZ: 0.08f,
            contactArea: 1_550f,
            duration: 60_000f) with
        {
            SurfaceNormalY = 1f,
            SurfaceNormalZ = 0f,
            InputSource = "avatar_world_left_foot_forefoot_medial_support"
        });

        var onset = transducer.Transduce(descriptor, tick: 34, timestampMs: 100.0);
        var later = transducer.Transduce(
            descriptor with { Sequence = 2 },
            tick: 134,
            timestampMs: 4_100.0);

        Assert.Equal(0f, onset.HighThresholdActivation);
        Assert.Equal(0, onset.GeneratedSpinalWithdrawalSpikes);
        Assert.Equal(0f, later.HighThresholdActivation);
        Assert.Equal(0, later.GeneratedSpinalWithdrawalSpikes);
        Assert.True(later.PressureActivation > 0f);
    }

    [Fact]
    public void ExcessiveLocalizedPlantarPressureStillRecruitsSpinalWithdrawal()
    {
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: 0.14f,
            force: 1_007.5f,
            impulse: 12f,
            penetration: 1.2f,
            bodyPositionY: -0.72f,
            bodyPositionZ: 0.08f,
            contactArea: 1_550f,
            duration: 120f) with
        {
            SurfaceNormalY = 1f,
            SurfaceNormalZ = 0f,
            InputSource = "avatar_world_right_foot_heel_lateral_support"
        });

        var result = new SomaticContactTransducerRuntime().Transduce(
            descriptor,
            tick: 35,
            timestampMs: 120.0);

        Assert.True(result.HighThresholdActivation > 0f);
        Assert.True(result.GeneratedSpinalWithdrawalSpikes > 0);
        Assert.Contains(result.LeftSpinalWithdrawalSpikes, spike =>
            spike.SourceNeuronId.Contains(":foot:", StringComparison.Ordinal));
    }

    [Fact]
    public void PenetratingPlantarContactStillRecruitsSpinalWithdrawalBelowPressureLimit()
    {
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: -0.14f,
            force: 360f,
            impulse: 12f,
            penetration: 24f,
            bodyPositionY: -0.72f,
            bodyPositionZ: 0.08f,
            contactArea: 6_200f,
            duration: 120f) with
        {
            SurfaceNormalY = 1f,
            SurfaceNormalZ = 0f,
            InputSource = "avatar_world_left_foot_penetrating_contact"
        });

        var result = new SomaticContactTransducerRuntime().Transduce(
            descriptor,
            tick: 36,
            timestampMs: 140.0);

        Assert.True(result.HighThresholdActivation > 0f);
        Assert.True(result.GeneratedSpinalWithdrawalSpikes > 0);
    }

    [Fact]
    public void SustainedNonFootSupportProducesLocalizedPressurePain()
    {
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: 0.18f,
            force: 420f,
            impulse: 0f,
            penetration: 1.2f,
            bodyPositionY: -0.34f,
            bodyPositionZ: -0.08f,
            contactArea: 20_000f,
            duration: 60_000f) with
        {
            SurfaceNormalY = 1f,
            SurfaceNormalZ = 0f,
            InputSource = "avatar_world_pelvis_support"
        });

        var result = new SomaticContactTransducerRuntime().Transduce(
            descriptor,
            tick: 35,
            timestampMs: 75.0);

        Assert.True(result.HighThresholdActivation > 0f);
        Assert.Contains(result.LeftHemisphereSpikes, spike =>
            spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal));
    }

    [Fact]
    public void AxialCollisionProducesDirectionCodedSpinalNociception()
    {
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: 0f,
            force: 3_500f,
            penetration: 30f,
            bodyPositionY: 0.24f,
            bodyPositionZ: -0.18f,
            duration: 4_000f) with
        {
            SurfaceNormalX = 0f,
            SurfaceNormalY = 0f,
            SurfaceNormalZ = -1f,
            InputSource = "avatar_world_chest_contact_z_neg"
        });

        var result = new SomaticContactTransducerRuntime().Transduce(
            descriptor,
            tick: 36,
            timestampMs: 100.0);

        Assert.True(result.GeneratedSpinalWithdrawalSpikes > 0);
        Assert.All(
            result.LeftSpinalWithdrawalSpikes.Concat(result.RightSpinalWithdrawalSpikes),
            spike => Assert.Contains(
                ":chest:free_nerve_ending_mechanonociceptor:normal_z_neg:",
                spike.SourceNeuronId,
                StringComparison.Ordinal));
    }

    [Fact]
    public void StaticPressurePainPersistsWhileSpinalWithdrawalAdaptsIntoPulses()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: 0.42f,
            force: 520f,
            impulse: 0f,
            penetration: 0.3f,
            contactArea: 20_000f,
            duration: 60_000f) with
        {
            TangentialSpeedMetersPerSecond = 0f,
            InputSource = "avatar_world_left_hand_contact"
        });

        var onset = transducer.Transduce(descriptor, tick: 40, timestampMs: 1_000.0);
        var adapted = transducer.Transduce(
            descriptor with { Sequence = 2 },
            tick: 41,
            timestampMs: 1_033.0);
        var laterPulse = transducer.Transduce(
            descriptor with { Sequence = 3 },
            tick: 134,
            timestampMs: 4_100.0);

        Assert.True(onset.GeneratedSpinalWithdrawalSpikes > 0);
        Assert.True(adapted.HighThresholdActivation > 0f);
        Assert.Contains(adapted.LeftHemisphereSpikes, spike =>
            spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal));
        Assert.Equal(0, adapted.GeneratedSpinalWithdrawalSpikes);
        Assert.True(laterPulse.HighThresholdActivation > 0f);
        Assert.True(laterPulse.GeneratedSpinalWithdrawalSpikes > 0);
    }

    [Fact]
    public void RisingForceRetriggersWithdrawalDuringStaticPressureRefractoryPeriod()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: 0.42f,
            force: 520f,
            impulse: 0f,
            penetration: 0.3f,
            contactArea: 20_000f,
            duration: 60_000f) with
        {
            TangentialSpeedMetersPerSecond = 0f,
            InputSource = "avatar_world_left_hand_contact"
        });

        _ = transducer.Transduce(descriptor, tick: 40, timestampMs: 1_000.0);
        var increasedLoad = transducer.Transduce(
            descriptor with
            {
                Sequence = 2,
                ForceNewtons = 1_200f
            },
            tick: 41,
            timestampMs: 1_033.0);

        Assert.True(increasedLoad.OnsetActivation > 0f);
        Assert.True(increasedLoad.GeneratedSpinalWithdrawalSpikes > 0);
    }

    [Fact]
    public void ColliderFacesWithinOneAnatomicalFieldShareWithdrawalRefractoryGate()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: 0.42f,
            force: 520f,
            impulse: 0f,
            penetration: 0.3f,
            contactArea: 20_000f,
            duration: 60_000f) with
        {
            TangentialSpeedMetersPerSecond = 0f,
            InputSource = "avatar_world_right_hand_palm_contact"
        });

        var palm = transducer.Transduce(descriptor, tick: 50, timestampMs: 2_000.0);
        var fingers = transducer.Transduce(
            descriptor with
            {
                Sequence = 2,
                BodyPositionY = descriptor.BodyPositionY + 0.06f,
                BodyPositionZ = descriptor.BodyPositionZ + 0.09f,
                InputSource = "avatar_world_right_hand_finger_contact"
            },
            tick: 51,
            timestampMs: 2_033.0);

        Assert.True(palm.GeneratedSpinalWithdrawalSpikes > 0);
        Assert.True(fingers.HighThresholdActivation > 0f);
        Assert.Contains(fingers.LeftHemisphereSpikes, spike =>
            spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal));
        Assert.Equal(0, fingers.GeneratedSpinalWithdrawalSpikes);
    }

    [Fact]
    public void SeparateAnatomicalFieldsRetainIndependentWithdrawalGates()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: 0.42f,
            force: 520f,
            impulse: 0f,
            penetration: 0.3f,
            contactArea: 20_000f,
            duration: 60_000f) with
        {
            TangentialSpeedMetersPerSecond = 0f,
            InputSource = "avatar_world_right_hand_contact"
        });

        var hand = transducer.Transduce(descriptor, tick: 60, timestampMs: 3_000.0);
        var forearm = transducer.Transduce(
            descriptor with
            {
                Sequence = 2,
                BodyPositionY = descriptor.BodyPositionY - 0.18f,
                InputSource = "avatar_world_right_forearm_contact"
            },
            tick: 61,
            timestampMs: 3_033.0);

        Assert.True(hand.GeneratedSpinalWithdrawalSpikes > 0);
        Assert.True(forearm.GeneratedSpinalWithdrawalSpikes > 0);
    }

    [Fact]
    public void StrongerLoadOnAnotherColliderFaceBypassesSharedRecoveryGate()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(
            bodyPositionX: 0.42f,
            force: 520f,
            impulse: 0f,
            penetration: 0.3f,
            contactArea: 20_000f,
            duration: 60_000f) with
        {
            TangentialSpeedMetersPerSecond = 0f,
            InputSource = "avatar_world_right_hand_palm_contact"
        });

        _ = transducer.Transduce(descriptor, tick: 70, timestampMs: 4_000.0);
        var increasedLoad = transducer.Transduce(
            descriptor with
            {
                Sequence = 2,
                ForceNewtons = 1_200f,
                InputSource = "avatar_world_right_hand_finger_contact"
            },
            tick: 71,
            timestampMs: 4_033.0);

        Assert.True(increasedLoad.GeneratedSpinalWithdrawalSpikes > 0);
    }

    [Fact]
    public void StableReceptorFibersUseStableSynapseIds()
    {
        var firstTransducer = new SomaticContactTransducerRuntime();
        var secondTransducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(bodyPositionX: 0.2f));

        var first = firstTransducer.Transduce(descriptor, tick: 40, timestampMs: 80.0);
        var second = secondTransducer.Transduce(descriptor, tick: 40, timestampMs: 80.0);

        Assert.NotEmpty(first.LeftHemisphereSpikes);
        Assert.Equal(
            first.LeftHemisphereSpikes.Select(spike => spike.SynapseId),
            second.LeftHemisphereSpikes.Select(spike => spike.SynapseId));
        Assert.DoesNotContain(first.LeftHemisphereSpikes, spike => spike.SynapseId == Guid.Empty);
    }

    [Fact]
    public void NonZeroContactWithoutSurfaceNormalIsRejected()
    {
        var frame = CreateFrame(bodyPositionX: 0f) with
        {
            SurfaceNormalX = 0f,
            SurfaceNormalY = 0f,
            SurfaceNormalZ = 0f
        };

        Assert.False(SomaticContactDescriptor.TryCreate(frame, out _, out var error));
        Assert.Contains("surface normal", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OutOfRangeTangentialVelocityNamesTheRejectedMeasurement()
    {
        var frame = CreateFrame(bodyPositionX: 0f) with
        {
            TangentialSpeedMetersPerSecond = 101f
        };

        Assert.False(SomaticContactDescriptor.TryCreate(frame, out _, out var error));
        Assert.Contains("tangentialSpeedMetersPerSecond", error, StringComparison.Ordinal);
        Assert.Contains("101", error, StringComparison.Ordinal);
    }

    [Fact]
    public void HandsUseDenserDiscriminativeReceptorFieldsThanGeneralSkin()
    {
        var hand = new SomaticContactTransducerRuntime().Transduce(
            CreateDescriptor(CreateFrame(0.42f, bodyPositionY: 0.34f, bodyPositionZ: 0.58f)),
            tick: 50,
            timestampMs: 90.0);
        var trunk = new SomaticContactTransducerRuntime().Transduce(
            CreateDescriptor(CreateFrame(0.20f, bodyPositionY: 0.05f, bodyPositionZ: 0.10f)),
            tick: 50,
            timestampMs: 90.0);

        Assert.Equal("hand", hand.ReceptorField);
        Assert.Equal("general_skin", trunk.ReceptorField);
        Assert.True(hand.ReceptorDensityScale > trunk.ReceptorDensityScale);
        Assert.True(hand.GeneratedSpikes > trunk.GeneratedSpikes);
    }

    [Theory]
    [InlineData("avatar_world_left_hand_contact", 0.29f, 0.72f, 0.18f, "hand")]
    [InlineData("avatar_world_right_foot_contact", -0.14f, 0.03f, 0.12f, "foot")]
    [InlineData("avatar_world_left_forearm_contact", 0.30f, 0.92f, 0.16f, "distal_limb")]
    public void ArticulatedColliderRegionPreservesAnatomicalReceptorField(
        string source,
        float x,
        float y,
        float z,
        string expectedField)
    {
        var descriptor = CreateDescriptor(CreateFrame(
            x,
            bodyPositionY: y,
            bodyPositionZ: z) with
        {
            InputSource = source
        });

        var result = new SomaticContactTransducerRuntime().Transduce(
            descriptor,
            tick: 55,
            timestampMs: 95.0);

        Assert.Equal(expectedField, result.ReceptorField);
    }

    [Fact]
    public void NociceptionRetainsThePhysicalLocationOfTheDamagingContact()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var anterior = transducer.Transduce(
            CreateDescriptor(CreateFrame(
                0.20f,
                force: 3_500f,
                penetration: 30f,
                bodyPositionY: 0.10f,
                bodyPositionZ: 0.25f)),
            tick: 60,
            timestampMs: 100.0);
        var posterior = transducer.Transduce(
            CreateDescriptor(CreateFrame(
                0.20f,
                force: 3_500f,
                penetration: 30f,
                bodyPositionY: 0.10f,
                bodyPositionZ: -0.25f) with { Sequence = 2 }),
            tick: 61,
            timestampMs: 101.0);

        Assert.NotEqual(anterior.ReceptorSector, posterior.ReceptorSector);
        var anteriorPain = anterior.LeftHemisphereSpikes
            .Where(spike => spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal))
            .Select(spike => spike.SynapseId)
            .ToHashSet();
        var posteriorPain = posterior.LeftHemisphereSpikes
            .Where(spike => spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal))
            .Select(spike => spike.SynapseId)
            .ToHashSet();
        Assert.NotEmpty(anteriorPain);
        Assert.NotEmpty(posteriorPain);
        Assert.Empty(anteriorPain.Intersect(posteriorPain));
    }

    private static SomaticContactDescriptor CreateDescriptor(SomaticContactFrameRequest frame)
    {
        Assert.True(SomaticContactDescriptor.TryCreate(frame, out var descriptor, out var error), error);
        return Assert.IsType<SomaticContactDescriptor>(descriptor);
    }

    private static SomaticContactFrameRequest CreateFrame(
        float bodyPositionX,
        float force = 1_100f,
        float impulse = 35f,
        float penetration = 8f,
        float bodyPositionY = 0.1f,
        float bodyPositionZ = 0.2f,
        float contactArea = 4_000f,
        float duration = 80f) =>
        new(
            Sequence: 1,
            TimestampMs: 10,
            BodyPositionX: bodyPositionX,
            BodyPositionY: bodyPositionY,
            BodyPositionZ: bodyPositionZ,
            SurfaceNormalX: 0f,
            SurfaceNormalY: 0f,
            SurfaceNormalZ: -1f,
            ForceNewtons: force,
            ImpulseNewtonSeconds: impulse,
            PenetrationMillimeters: penetration,
            TangentialSpeedMetersPerSecond: 0.6f,
            ContactAreaSquareMillimeters: contactArea,
            DurationMilliseconds: duration,
            InputSource: "test_contact");
}
