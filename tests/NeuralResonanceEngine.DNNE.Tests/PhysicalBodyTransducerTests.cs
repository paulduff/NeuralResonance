using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class PhysicalBodyTransducerTests
{
    [Fact]
    public void PhysicalMeasurementsBecomeThreeFixedNeuronalAfferentPopulations()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        var first = CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.80f, "test_body"));
        runtime.Transduce(first, tick: 1, timestampMs: 1);

        var second = CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 0.4f, 0f, 3f, 0f, 1.2f, 0f,
            2_000_000f, 0.72f, 39f, 0.82f, 0.54f, "test_body"));
        var result = runtime.Transduce(second, tick: 2, timestampMs: 2);

        Assert.True(result.ActiveProprioceptivePopulations > 0);
        Assert.True(result.ActiveVestibularPopulations > 0);
        Assert.True(result.ActiveVisceralPopulations > 0);
        Assert.True(result.LinearAccelerationMagnitude > 0f);
        Assert.True(result.HomeostaticDeviation > 0f);

        AssertFixedAfferentSpikes(result.ProprioceptiveLeft, StructureId.ProprioceptiveAfferents, "L");
        AssertFixedAfferentSpikes(result.VestibularRight, StructureId.VestibularAfferents, "R");
        AssertFixedAfferentSpikes(result.VisceralLeft, StructureId.VisceralAfferents, "L");
    }

    [Fact]
    public void InvalidPhysiologyIsRejectedBeforeNeuralTransduction()
    {
        var valid = PhysicalBodyFrameDescriptor.TryCreate(
            new PhysicalBodyFrameRequest(
                1, 1, 0f, 0f, 0f, 0f, 0f, 0f,
                8_000_000f, 1.2f, 37f, 0.98f, 0.8f, "test"),
            out var descriptor,
            out var error);

        Assert.False(valid);
        Assert.Null(descriptor);
        Assert.Contains("physiological", error, StringComparison.OrdinalIgnoreCase);
    }

    private static PhysicalBodyFrameDescriptor CreateDescriptor(PhysicalBodyFrameRequest request)
    {
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(request, out var descriptor, out var error), error);
        return Assert.IsType<PhysicalBodyFrameDescriptor>(descriptor);
    }

    private static void AssertFixedAfferentSpikes(
        IReadOnlyList<SpikeMessage> spikes,
        StructureId expectedStructure,
        string expectedHemisphere)
    {
        Assert.NotEmpty(spikes);
        Assert.All(spikes, spike =>
        {
            Assert.Equal(expectedStructure, spike.SourceStructure);
            Assert.Equal(expectedStructure, spike.TargetStructure);
            Assert.StartsWith($"{expectedHemisphere}:", spike.SourceNeuronId, StringComparison.Ordinal);
            Assert.NotEqual(Guid.Empty, spike.SynapseId);
            Assert.False(spike.IsFeedback);
            Assert.Null(spike.ModulationContext);
        });
    }
}
