using System.Buffers.Binary;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarAudioFrameTests
{
    [Fact]
    public void ValidPcmFramePassesValidation()
    {
        var frame = new AvatarAudioFrame(1, 10, 16000, 2, 32, new byte[32 * 2 * sizeof(short)]);

        frame.Validate();

        Assert.Equal(128, frame.RequiredBytes);
    }

    [Theory]
    [InlineData(7999, 1, 16, 32)]
    [InlineData(16000, 0, 16, 0)]
    [InlineData(16000, 3, 16, 96)]
    [InlineData(16000, 1, 0, 0)]
    [InlineData(16000, 1, 16, 31)]
    public void InvalidPcmFrameIsRejected(int sampleRate, int channels, int samples, int bytes)
    {
        var frame = new AvatarAudioFrame(1, 10, sampleRate, channels, samples, new byte[bytes]);

        Assert.ThrowsAny<ArgumentException>(frame.Validate);
    }

    [Fact]
    public void AcousticRendererProducesPhysicalStereoPanWithoutLabels()
    {
        var frame = AvatarAcousticRenderer.RenderFrame(
            [new AvatarAcousticSource(440.0, 0.5, Pan: 1.0)],
            sequence: 3,
            captureTimestampMs: 50);

        var leftEnergy = 0.0;
        var rightEnergy = 0.0;
        for (var sample = 0; sample < frame.SamplesPerChannel; sample++)
        {
            var offset = sample * frame.Channels * sizeof(short);
            var left = BinaryPrimitives.ReadInt16LittleEndian(frame.Pcm16Le.AsSpan(offset, sizeof(short))) / 32768.0;
            var right = BinaryPrimitives.ReadInt16LittleEndian(frame.Pcm16Le.AsSpan(offset + sizeof(short), sizeof(short))) / 32768.0;
            leftEnergy += left * left;
            rightEnergy += right * right;
        }

        Assert.True(rightEnergy > leftEnergy * 100.0);
    }
}
