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
    public void HighForceAndPenetrationActivateMechanicalNociceptors()
    {
        var transducer = new SomaticContactTransducerRuntime();
        var descriptor = CreateDescriptor(CreateFrame(bodyPositionX: 0f, force: 3_500f, penetration: 30f));

        var result = transducer.Transduce(descriptor, tick: 30, timestampMs: 70.0);

        Assert.True(result.HighThresholdActivation > 0f);
        Assert.Contains(result.LeftHemisphereSpikes, spike =>
            spike.SourceNeuronId.Contains("mechanonociceptor", StringComparison.Ordinal));
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
