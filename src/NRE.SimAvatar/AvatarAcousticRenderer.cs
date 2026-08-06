using System.Buffers.Binary;

namespace NRE.SimAvatar;

/// <summary>
/// Renders physical acoustic source parameters into stereo pressure samples.
/// It deliberately has no event labels, brain targets, or semantic channels.
/// </summary>
public static class AvatarAcousticRenderer
{
    public const int DefaultSampleRate = 16000;
    public const int DefaultFrameMilliseconds = 50;

    public static AvatarAudioFrame RenderFrame(
        IReadOnlyList<AvatarAcousticSource> sources,
        long sequence,
        long captureTimestampMs,
        int sampleRate = DefaultSampleRate,
        int frameMilliseconds = DefaultFrameMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sampleRate is < AvatarAudioFrame.MinimumSampleRate or > AvatarAudioFrame.MaximumSampleRate)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        var samplesPerChannel = checked(sampleRate * frameMilliseconds / 1000);
        if (samplesPerChannel is <= 0 or > AvatarAudioFrame.MaximumSamplesPerChannel)
        {
            throw new ArgumentOutOfRangeException(nameof(frameMilliseconds));
        }

        var pcm = new byte[checked(samplesPerChannel * 2 * sizeof(short))];
        var frameStartSample = checked(sequence * samplesPerChannel);
        for (var sampleIndex = 0; sampleIndex < samplesPerChannel; sampleIndex++)
        {
            var left = 0.0;
            var right = 0.0;
            var absoluteSample = frameStartSample + sampleIndex;
            for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                var source = sources[sourceIndex].Clamp(sampleRate);
                if (source.Amplitude <= 0)
                {
                    continue;
                }

                var timeSeconds = absoluteSample / (double)sampleRate;
                var fundamental = Math.Sin((2.0 * Math.PI * source.FrequencyHz * timeSeconds) + source.PhaseRadians);
                var harmonic = Math.Sin((4.0 * Math.PI * source.FrequencyHz * timeSeconds) + source.PhaseRadians);
                var pressure = (fundamental * (1.0 - source.HarmonicMix)) + (harmonic * source.HarmonicMix);
                if (source.NoiseMix > 0)
                {
                    var noise = DeterministicNoise(sequence, sampleIndex, sourceIndex);
                    pressure = (pressure * (1.0 - source.NoiseMix)) + (noise * source.NoiseMix);
                }

                if (source.PulseRateHz > 0)
                {
                    var pulsePhase = (timeSeconds * source.PulseRateHz) % 1.0;
                    var pulseEnvelope = pulsePhase < source.PulseDutyCycle ? 1.0 : 0.08;
                    pressure *= pulseEnvelope;
                }

                var edge = Math.Min(sampleIndex, samplesPerChannel - 1 - sampleIndex);
                var edgeEnvelope = Math.Clamp(edge / Math.Max(1.0, sampleRate * 0.0025), 0.0, 1.0);
                pressure *= source.Amplitude * edgeEnvelope;

                var pan = Math.Clamp(source.Pan, -1.0, 1.0);
                var leftGain = Math.Sqrt((1.0 - pan) * 0.5);
                var rightGain = Math.Sqrt((1.0 + pan) * 0.5);
                left += pressure * leftGain;
                right += pressure * rightGain;
            }

            var leftSample = (short)Math.Round(Math.Clamp(left, -1.0, 1.0) * short.MaxValue);
            var rightSample = (short)Math.Round(Math.Clamp(right, -1.0, 1.0) * short.MaxValue);
            var offset = sampleIndex * 2 * sizeof(short);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset, sizeof(short)), leftSample);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset + sizeof(short), sizeof(short)), rightSample);
        }

        return new AvatarAudioFrame(
            sequence,
            captureTimestampMs,
            sampleRate,
            Channels: 2,
            samplesPerChannel,
            pcm);
    }

    private static double DeterministicNoise(long sequence, int sampleIndex, int sourceIndex)
    {
        unchecked
        {
            var value = (uint)sequence;
            value ^= (uint)(sampleIndex * 0x9E3779B9);
            value ^= (uint)(sourceIndex * 0x85EBCA6B);
            value ^= value >> 16;
            value *= 0x7FEB352D;
            value ^= value >> 15;
            value *= 0x846CA68B;
            value ^= value >> 16;
            return ((value & 0x00FFFFFF) / 8388607.5) - 1.0;
        }
    }
}

public readonly record struct AvatarAcousticSource(
    double FrequencyHz,
    double Amplitude,
    double Pan = 0.0,
    double NoiseMix = 0.0,
    double HarmonicMix = 0.0,
    double PulseRateHz = 0.0,
    double PulseDutyCycle = 0.5,
    double PhaseRadians = 0.0)
{
    internal AvatarAcousticSource Clamp(int sampleRate)
        => this with
        {
            FrequencyHz = Math.Clamp(FrequencyHz, 20.0, sampleRate * 0.45),
            Amplitude = Math.Clamp(Amplitude, 0.0, 1.0),
            Pan = Math.Clamp(Pan, -1.0, 1.0),
            NoiseMix = Math.Clamp(NoiseMix, 0.0, 1.0),
            HarmonicMix = Math.Clamp(HarmonicMix, 0.0, 1.0),
            PulseRateHz = Math.Clamp(PulseRateHz, 0.0, 200.0),
            PulseDutyCycle = Math.Clamp(PulseDutyCycle, 0.01, 1.0)
        };
}
