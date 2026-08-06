using System.Buffers.Binary;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CochlearFrameTransducerTests
{
    private const int SampleRate = 16000;
    private const int SamplesPerChannel = 800;

    [Fact]
    public void SilenceProducesNoInventedAuditoryFeatures()
    {
        var transducer = new CochlearFrameTransducerRuntime();
        var descriptor = CreateDescriptor(channels: 1, source: "silence");

        var result = transducer.Transduce(
            new byte[descriptor.RequiredBytes],
            descriptor,
            tick: 1,
            timestampMs: 10.0);

        Assert.Empty(result.LeftEarSpikes);
        Assert.Empty(result.RightEarSpikes);
        Assert.Equal(0, result.ActiveLeftBands);
        Assert.Equal(0, result.ActiveRightBands);
        Assert.Equal(0f, result.RootMeanSquare);
        Assert.Equal(0f, result.PeakAmplitude);
    }

    [Fact]
    public void MonauralToneProducesBilateralTonotopicReceptorSpikes()
    {
        var transducer = new CochlearFrameTransducerRuntime();
        var descriptor = CreateDescriptor(channels: 1, source: "mono-tone");
        var pcm = CreatePcm(channels: 1, (sample, _) => Tone(sample, 440.0, 0.35));

        var result = transducer.Transduce(pcm, descriptor, tick: 2, timestampMs: 20.0);

        Assert.NotEmpty(result.LeftEarSpikes);
        Assert.NotEmpty(result.RightEarSpikes);
        Assert.Equal(result.LeftEarSpikes.Count, result.RightEarSpikes.Count);
        Assert.True(result.ActiveLeftBands > 0);
        Assert.Equal(result.ActiveLeftBands, result.ActiveRightBands);
        Assert.All(result.LeftEarSpikes.Concat(result.RightEarSpikes), spike =>
        {
            Assert.Equal(StructureId.Cochlea, spike.SourceStructure);
            Assert.Equal(StructureId.Cochlea, spike.TargetStructure);
            Assert.Contains("auditory_nerve_", spike.TargetNeuronId, StringComparison.Ordinal);
            Assert.Null(spike.ModulationContext);
        });
    }

    [Fact]
    public void LeftChannelToneRemainsIpsilateralAtTheCochleaBoundary()
    {
        var transducer = new CochlearFrameTransducerRuntime();
        var descriptor = CreateDescriptor(channels: 2, source: "stereo-pan");
        var pcm = CreatePcm(
            channels: 2,
            (sample, channel) => channel == 0 ? Tone(sample, 880.0, 0.32) : 0.0);

        var result = transducer.Transduce(pcm, descriptor, tick: 3, timestampMs: 30.0);

        Assert.NotEmpty(result.LeftEarSpikes);
        Assert.Empty(result.RightEarSpikes);
        Assert.True(result.ActiveLeftBands > result.ActiveRightBands);
        Assert.All(result.LeftEarSpikes, spike => Assert.StartsWith("L:", spike.TargetNeuronId, StringComparison.Ordinal));
    }

    [Fact]
    public void StableToneRetainsLearnableSynapseIdentities()
    {
        var transducer = new CochlearFrameTransducerRuntime();
        var descriptor = CreateDescriptor(channels: 1, source: "stable-tone");
        var pcm = CreatePcm(channels: 1, (sample, _) => Tone(sample, 330.0, 0.28));

        var first = transducer.Transduce(pcm, descriptor, tick: 4, timestampMs: 40.0);
        var second = transducer.Transduce(pcm, descriptor, tick: 5, timestampMs: 90.0);

        var firstSynapses = first.LeftEarSpikes.Concat(first.RightEarSpikes).Select(spike => spike.SynapseId).ToHashSet();
        var secondSynapses = second.LeftEarSpikes.Concat(second.RightEarSpikes).Select(spike => spike.SynapseId).ToHashSet();
        Assert.NotEmpty(secondSynapses);
        Assert.Subset(firstSynapses, secondSynapses);
    }

    [Theory]
    [InlineData(7999, 1, 800, "Pcm16Le")]
    [InlineData(16000, 0, 800, "Pcm16Le")]
    [InlineData(16000, 3, 800, "Pcm16Le")]
    [InlineData(16000, 1, 0, "Pcm16Le")]
    [InlineData(16000, 1, 800, "Float32")]
    public void InvalidFrameDescriptorsAreRejected(int sampleRate, int channels, int samples, string format)
    {
        Assert.False(CochlearFrameDescriptor.TryCreate(
            sampleRate,
            channels,
            samples,
            format,
            "invalid",
            out var descriptor,
            out var error));
        Assert.Null(descriptor);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private static CochlearFrameDescriptor CreateDescriptor(int channels, string source)
    {
        Assert.True(CochlearFrameDescriptor.TryCreate(
            SampleRate,
            channels,
            SamplesPerChannel,
            "Pcm16Le",
            source,
            out var descriptor,
            out var error), error);
        return Assert.IsType<CochlearFrameDescriptor>(descriptor);
    }

    private static byte[] CreatePcm(int channels, Func<int, int, double> sampleFactory)
    {
        var pcm = new byte[SamplesPerChannel * channels * sizeof(short)];
        for (var sample = 0; sample < SamplesPerChannel; sample++)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                var value = (short)Math.Round(Math.Clamp(sampleFactory(sample, channel), -1.0, 1.0) * short.MaxValue);
                var offset = ((sample * channels) + channel) * sizeof(short);
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset, sizeof(short)), value);
            }
        }

        return pcm;
    }

    private static double Tone(int sample, double frequencyHz, double amplitude)
        => Math.Sin(2.0 * Math.PI * frequencyHz * sample / SampleRate) * amplitude;
}
