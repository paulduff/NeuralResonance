using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class RetinalFrameTransducerTests
{
    [Fact]
    public void UniformFirstFrameAdaptsWithoutInventingVisualFeatures()
    {
        var transducer = new RetinalFrameTransducerRuntime();
        var descriptor = CreateDescriptor("uniform");
        var pixels = CreateBgraFrame((_, _) => 128);

        var result = transducer.Transduce(pixels, descriptor, tick: 1, timestampMs: 10.0);

        Assert.Empty(result.LeftHemisphereSpikes);
        Assert.Empty(result.RightHemisphereSpikes);
        Assert.Equal(0, result.OnChannelSpikes);
        Assert.Equal(0, result.OffChannelSpikes);
        Assert.InRange(result.MeanLuminance, 0.49f, 0.51f);
    }

    [Fact]
    public void SpatialEdgeProducesRetinotopicOnAndOffGanglionChannels()
    {
        var transducer = new RetinalFrameTransducerRuntime();
        var descriptor = CreateDescriptor("edge");
        var pixels = CreateBgraFrame((x, _) => x < RetinalFrameTransducerRuntime.SampleColumns / 2 ? (byte)24 : (byte)232);

        var result = transducer.Transduce(pixels, descriptor, tick: 2, timestampMs: 20.0);

        Assert.NotEmpty(result.LeftHemisphereSpikes);
        Assert.NotEmpty(result.RightHemisphereSpikes);
        Assert.True(result.OnChannelSpikes > 0);
        Assert.True(result.OffChannelSpikes > 0);
        Assert.All(result.LeftHemisphereSpikes, spike => Assert.StartsWith("L:", spike.TargetNeuronId, StringComparison.Ordinal));
        Assert.All(result.RightHemisphereSpikes, spike => Assert.StartsWith("R:", spike.TargetNeuronId, StringComparison.Ordinal));
        Assert.All(result.LeftHemisphereSpikes.Concat(result.RightHemisphereSpikes), spike =>
        {
            Assert.Equal(StructureId.Retina, spike.SourceStructure);
            Assert.Equal(StructureId.Retina, spike.TargetStructure);
            Assert.Null(spike.ModulationContext);
        });
    }

    [Fact]
    public void LeftVisualFieldChangeProjectsPrimarilyToRightHemisphere()
    {
        var transducer = new RetinalFrameTransducerRuntime();
        var descriptor = CreateDescriptor("contralateral");
        var baseline = CreateBgraFrame((_, _) => 96);
        var changed = CreateBgraFrame((x, _) => x < RetinalFrameTransducerRuntime.SampleColumns / 2 ? (byte)224 : (byte)96);

        _ = transducer.Transduce(baseline, descriptor, tick: 3, timestampMs: 30.0);
        var result = transducer.Transduce(changed, descriptor, tick: 4, timestampMs: 40.0);

        Assert.True(result.RightHemisphereSpikes.Count > result.LeftHemisphereSpikes.Count * 2);
        Assert.True(result.MeanTemporalChange > 0.20f);
    }

    [Fact]
    public void StableSpatialFeatureRetainsLearnableSynapseIdentities()
    {
        var transducer = new RetinalFrameTransducerRuntime();
        var descriptor = CreateDescriptor("stable-synapses");
        var pixels = CreateBgraFrame((x, _) => x < RetinalFrameTransducerRuntime.SampleColumns / 2 ? (byte)20 : (byte)240);

        var first = transducer.Transduce(pixels, descriptor, tick: 5, timestampMs: 50.0);
        var second = transducer.Transduce(pixels, descriptor, tick: 6, timestampMs: 60.0);

        var firstSynapses = first.LeftHemisphereSpikes.Concat(first.RightHemisphereSpikes).Select(spike => spike.SynapseId).Order().ToArray();
        var secondSynapses = second.LeftHemisphereSpikes.Concat(second.RightHemisphereSpikes).Select(spike => spike.SynapseId).Order().ToArray();
        Assert.Equal(firstSynapses, secondSynapses);
    }

    [Theory]
    [InlineData(0, 12, 64, "Bgra32")]
    [InlineData(16, 0, 64, "Bgra32")]
    [InlineData(16, 12, 12, "Bgra32")]
    [InlineData(16, 12, 64, "Gray8")]
    public void InvalidFrameDescriptorsAreRejected(int width, int height, int stride, string pixelFormat)
    {
        Assert.False(RetinalFrameDescriptor.TryCreate(
            width,
            height,
            stride,
            pixelFormat,
            "avatar_vision",
            out var descriptor,
            out var error));
        Assert.Null(descriptor);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private static RetinalFrameDescriptor CreateDescriptor(string source)
    {
        Assert.True(RetinalFrameDescriptor.TryCreate(
            RetinalFrameTransducerRuntime.SampleColumns,
            RetinalFrameTransducerRuntime.SampleRows,
            RetinalFrameTransducerRuntime.SampleColumns * 4,
            "Bgra32",
            source,
            out var descriptor,
            out var error), error);
        return Assert.IsType<RetinalFrameDescriptor>(descriptor);
    }

    private static byte[] CreateBgraFrame(Func<int, int, byte> luminance)
    {
        var width = RetinalFrameTransducerRuntime.SampleColumns;
        var height = RetinalFrameTransducerRuntime.SampleRows;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = luminance(x, y);
                var offset = ((y * width) + x) * 4;
                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }
}
