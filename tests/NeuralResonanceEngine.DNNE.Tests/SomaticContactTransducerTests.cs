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

    private static SomaticContactDescriptor CreateDescriptor(SomaticContactFrameRequest frame)
    {
        Assert.True(SomaticContactDescriptor.TryCreate(frame, out var descriptor, out var error), error);
        return Assert.IsType<SomaticContactDescriptor>(descriptor);
    }

    private static SomaticContactFrameRequest CreateFrame(
        float bodyPositionX,
        float force = 1_100f,
        float impulse = 35f,
        float penetration = 8f) =>
        new(
            Sequence: 1,
            TimestampMs: 10,
            BodyPositionX: bodyPositionX,
            BodyPositionY: 0.1f,
            BodyPositionZ: 0.2f,
            SurfaceNormalX: 0f,
            SurfaceNormalY: 0f,
            SurfaceNormalZ: -1f,
            ForceNewtons: force,
            ImpulseNewtonSeconds: impulse,
            PenetrationMillimeters: penetration,
            TangentialSpeedMetersPerSecond: 0.6f,
            ContactAreaSquareMillimeters: 4_000f,
            DurationMilliseconds: 80f,
            InputSource: "test_contact");
}
