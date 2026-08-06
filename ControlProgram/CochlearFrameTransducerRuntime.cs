using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal enum CochlearSampleFormat
{
    Pcm16Le
}

internal sealed record CochlearFrameDescriptor(
    int SampleRate,
    int Channels,
    int SamplesPerChannel,
    CochlearSampleFormat SampleFormat,
    string InputSource)
{
    public const int MinimumSampleRate = 8000;
    public const int MaximumSampleRate = 48000;
    public const int MaximumSamplesPerChannel = 4096;
    public const int MaximumPayloadBytes = MaximumSamplesPerChannel * 2 * sizeof(short);

    public int RequiredBytes => checked(SamplesPerChannel * Channels * sizeof(short));

    public static bool TryCreate(
        int sampleRate,
        int channels,
        int samplesPerChannel,
        string? sampleFormat,
        string? inputSource,
        out CochlearFrameDescriptor? descriptor,
        out string? error)
    {
        descriptor = null;
        error = null;
        if (sampleRate < MinimumSampleRate || sampleRate > MaximumSampleRate)
        {
            error = $"Sample rate must be between {MinimumSampleRate} and {MaximumSampleRate} Hz.";
            return false;
        }

        if (channels is < 1 or > 2)
        {
            error = "Channel count must be one (mono) or two (stereo).";
            return false;
        }

        if (samplesPerChannel <= 0 || samplesPerChannel > MaximumSamplesPerChannel)
        {
            error = $"Samples per channel must be between 1 and {MaximumSamplesPerChannel}.";
            return false;
        }

        if (!Enum.TryParse<CochlearSampleFormat>(sampleFormat, ignoreCase: true, out var parsedFormat))
        {
            error = "Sample format must be Pcm16Le.";
            return false;
        }

        int requiredBytes;
        try
        {
            requiredBytes = checked(samplesPerChannel * channels * sizeof(short));
        }
        catch (OverflowException)
        {
            error = "Audio frame dimensions overflow the supported payload size.";
            return false;
        }

        if (requiredBytes > MaximumPayloadBytes)
        {
            error = $"Audio frame payload must not exceed {MaximumPayloadBytes} bytes.";
            return false;
        }

        descriptor = new CochlearFrameDescriptor(
            sampleRate,
            channels,
            samplesPerChannel,
            parsedFormat,
            AdminInputSource.Normalize(inputSource));
        return true;
    }
}

internal sealed record CochlearFrameTransduction(
    IReadOnlyList<SpikeMessage> LeftEarSpikes,
    IReadOnlyList<SpikeMessage> RightEarSpikes,
    int FrequencyBands,
    int ActiveLeftBands,
    int ActiveRightBands,
    float RootMeanSquare,
    float PeakAmplitude,
    float MeanBandAmplitude,
    float MeanOnset)
{
    public int GeneratedSpikes => LeftEarSpikes.Count + RightEarSpikes.Count;

    public IReadOnlyList<SpikeMessage> ForHemisphere(string? hemisphere)
    {
        if (string.Equals(hemisphere, "L", StringComparison.OrdinalIgnoreCase))
        {
            return LeftEarSpikes;
        }

        if (string.Equals(hemisphere, "R", StringComparison.OrdinalIgnoreCase))
        {
            return RightEarSpikes;
        }

        if (LeftEarSpikes.Count == 0)
        {
            return RightEarSpikes;
        }

        if (RightEarSpikes.Count == 0)
        {
            return LeftEarSpikes;
        }

        var combined = new List<SpikeMessage>(GeneratedSpikes);
        combined.AddRange(LeftEarSpikes);
        combined.AddRange(RightEarSpikes);
        return combined;
    }
}

internal sealed class CochlearFrameTransducerRuntime
{
    internal const int FrequencyBandCount = 24;
    private const float MinimumActivation = 0.0045f;
    private const int MaximumFibersPerBand = 4;
    private readonly object _gate = new();
    private readonly Dictionary<string, float[]> _previousBandsByEar = new(StringComparer.OrdinalIgnoreCase);

    public CochlearFrameTransduction Transduce(
        ReadOnlySpan<byte> pcm,
        CochlearFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (pcm.Length != descriptor.RequiredBytes)
        {
            throw new ArgumentException("PCM payload length does not match the audio frame descriptor.", nameof(pcm));
        }

        var leftSamples = DecodeChannel(pcm, descriptor, channel: 0);
        var rightSamples = descriptor.Channels == 1
            ? leftSamples
            : DecodeChannel(pcm, descriptor, channel: 1);
        var centerFrequencies = BuildCenterFrequencies(descriptor.SampleRate);
        var leftAnalysis = AnalyzeEar(leftSamples, centerFrequencies, descriptor.SampleRate);
        var rightAnalysis = descriptor.Channels == 1
            ? leftAnalysis
            : AnalyzeEar(rightSamples, centerFrequencies, descriptor.SampleRate);

        var leftPrevious = ExchangeHistory($"{descriptor.InputSource}:L", leftAnalysis.BandAmplitudes);
        var rightPrevious = descriptor.Channels == 1
            ? leftPrevious
            : ExchangeHistory($"{descriptor.InputSource}:R", rightAnalysis.BandAmplitudes);
        var left = BuildEarSpikes(
            leftAnalysis.BandAmplitudes,
            leftPrevious,
            centerFrequencies,
            "L",
            tick,
            timestampMs,
            out var activeLeft,
            out var leftOnset);
        var right = BuildEarSpikes(
            rightAnalysis.BandAmplitudes,
            rightPrevious,
            centerFrequencies,
            "R",
            tick,
            timestampMs,
            out var activeRight,
            out var rightOnset);

        return new CochlearFrameTransduction(
            left,
            right,
            FrequencyBandCount,
            activeLeft,
            activeRight,
            (leftAnalysis.RootMeanSquare + rightAnalysis.RootMeanSquare) * 0.5f,
            MathF.Max(leftAnalysis.PeakAmplitude, rightAnalysis.PeakAmplitude),
            (leftAnalysis.BandAmplitudes.Average() + rightAnalysis.BandAmplitudes.Average()) * 0.5f,
            (leftOnset + rightOnset) * 0.5f);
    }

    private float[]? ExchangeHistory(string key, float[] current)
    {
        lock (_gate)
        {
            _previousBandsByEar.TryGetValue(key, out var previous);
            _previousBandsByEar[key] = (float[])current.Clone();
            return previous;
        }
    }

    private static float[] DecodeChannel(
        ReadOnlySpan<byte> pcm,
        CochlearFrameDescriptor descriptor,
        int channel)
    {
        var samples = new float[descriptor.SamplesPerChannel];
        var frameStride = descriptor.Channels * sizeof(short);
        for (var i = 0; i < samples.Length; i++)
        {
            var offset = checked((i * frameStride) + (channel * sizeof(short)));
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(offset, sizeof(short))) / 32768f;
        }

        return samples;
    }

    private static float[] BuildCenterFrequencies(int sampleRate)
    {
        const double minimumHz = 80.0;
        var maximumHz = Math.Min(7600.0, sampleRate * 0.45);
        var ratio = Math.Pow(maximumHz / minimumHz, 1.0 / (FrequencyBandCount - 1));
        var frequencies = new float[FrequencyBandCount];
        for (var i = 0; i < frequencies.Length; i++)
        {
            frequencies[i] = (float)(minimumHz * Math.Pow(ratio, i));
        }

        return frequencies;
    }

    private static EarAnalysis AnalyzeEar(float[] samples, float[] frequencies, int sampleRate)
    {
        var sumSquares = 0f;
        var peak = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            sumSquares += samples[i] * samples[i];
            peak = MathF.Max(peak, MathF.Abs(samples[i]));
        }

        var bandAmplitudes = new float[frequencies.Length];
        if (samples.Length > 1)
        {
            var windowScale = 0.0;
            for (var i = 0; i < samples.Length; i++)
            {
                windowScale += 0.5 - (0.5 * Math.Cos((2.0 * Math.PI * i) / (samples.Length - 1)));
            }

            for (var band = 0; band < frequencies.Length; band++)
            {
                var angularStep = (2.0 * Math.PI * frequencies[band]) / sampleRate;
                var real = 0.0;
                var imaginary = 0.0;
                for (var i = 0; i < samples.Length; i++)
                {
                    var window = 0.5 - (0.5 * Math.Cos((2.0 * Math.PI * i) / (samples.Length - 1)));
                    var sample = samples[i] * window;
                    var phase = angularStep * i;
                    real += sample * Math.Cos(phase);
                    imaginary -= sample * Math.Sin(phase);
                }

                bandAmplitudes[band] = (float)Math.Clamp(
                    2.0 * Math.Sqrt((real * real) + (imaginary * imaginary)) / Math.Max(1.0, windowScale),
                    0.0,
                    1.0);
            }
        }

        return new EarAnalysis(
            bandAmplitudes,
            MathF.Sqrt(sumSquares / Math.Max(1, samples.Length)),
            peak);
    }

    private static List<SpikeMessage> BuildEarSpikes(
        float[] current,
        float[]? previous,
        float[] frequencies,
        string hemisphere,
        long tick,
        double timestampMs,
        out int activeBands,
        out float meanOnset)
    {
        var spikes = new List<SpikeMessage>(FrequencyBandCount * 2);
        activeBands = 0;
        var onsetTotal = 0f;
        for (var band = 0; band < current.Length; band++)
        {
            var onset = previous is null ? current[band] : MathF.Max(0f, current[band] - previous[band]);
            onsetTotal += onset;
            var activation = MathF.Max(current[band], onset * 1.6f);
            if (activation < MinimumActivation)
            {
                continue;
            }

            activeBands++;
            var fiberCount = Math.Clamp(
                1 + (int)MathF.Floor((activation - MinimumActivation) * 16f),
                1,
                MaximumFibersPerBand);
            for (var fiber = 0; fiber < fiberCount; fiber++)
            {
                spikes.Add(BuildSpike(
                    timestampMs,
                    hemisphere,
                    band,
                    fiber,
                    frequencies[band],
                    activation,
                    onset,
                    tick));
            }
        }

        meanOnset = onsetTotal / Math.Max(1, current.Length);
        return spikes;
    }

    private static SpikeMessage BuildSpike(
        double timestampMs,
        string hemisphere,
        int band,
        int fiber,
        float frequencyHz,
        float activation,
        float onset,
        long tick)
    {
        var boundedActivation = Math.Clamp(activation, MinimumActivation, 1f);
        return new SpikeMessage
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = timestampMs,
            SourceStructure = StructureId.Cochlea,
            TargetStructure = StructureId.Cochlea,
            SourceNeuronId = $"{hemisphere}:inner_hair_cell_{band}",
            TargetNeuronId = $"{hemisphere}:auditory_nerve_{band}_fiber_{fiber}",
            SynapseId = CreateStableSynapseId(hemisphere, band, fiber),
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = Math.Clamp(0.25f + (boundedActivation * 4.2f) + (onset * 1.2f), 0.05f, 6f),
            ReuptakeRate = Math.Clamp(6.5f - (boundedActivation * 3.0f), 2f, 8f),
            SpikeType = onset >= 0.18f || (boundedActivation >= 0.45f && ((tick + fiber) & 1) == 0)
                ? SpikeTypeEnum.BURST
                : SpikeTypeEnum.ACTION_POTENTIAL,
            IsFeedback = false,
            ModulationContext = null
        };
    }

    private static Guid CreateStableSynapseId(string hemisphere, int band, int fiber)
    {
        var key = Encoding.UTF8.GetBytes($"cochlea:{hemisphere}:{band}:{fiber}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(key, digest);
        return new Guid(digest[..16]);
    }

    private sealed record EarAnalysis(float[] BandAmplitudes, float RootMeanSquare, float PeakAmplitude);
}
