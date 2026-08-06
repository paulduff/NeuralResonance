using System.Security.Cryptography;
using System.Text;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal enum RetinalPixelFormat
{
    Bgra32,
    Rgb24
}

internal sealed record RetinalFrameDescriptor(
    int Width,
    int Height,
    int Stride,
    RetinalPixelFormat PixelFormat,
    string InputSource)
{
    public const int MaximumDimension = 1024;
    public const int MaximumPayloadBytes = 4 * 1024 * 1024;

    public int BytesPerPixel => PixelFormat == RetinalPixelFormat.Bgra32 ? 4 : 3;
    public int RequiredBytes => checked(Stride * Height);

    public static bool TryCreate(
        int width,
        int height,
        int stride,
        string? pixelFormat,
        string? inputSource,
        out RetinalFrameDescriptor? descriptor,
        out string? error)
    {
        descriptor = null;
        error = null;
        if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension)
        {
            error = $"Frame dimensions must be between 1 and {MaximumDimension} pixels.";
            return false;
        }

        if (!Enum.TryParse<RetinalPixelFormat>(pixelFormat, ignoreCase: true, out var parsedFormat))
        {
            error = "Pixel format must be Bgra32 or Rgb24.";
            return false;
        }

        var bytesPerPixel = parsedFormat == RetinalPixelFormat.Bgra32 ? 4 : 3;
        int minimumStride;
        int requiredBytes;
        try
        {
            minimumStride = checked(width * bytesPerPixel);
            requiredBytes = checked(stride * height);
        }
        catch (OverflowException)
        {
            error = "Frame dimensions overflow the supported payload size.";
            return false;
        }

        if (stride < minimumStride)
        {
            error = "Frame stride is smaller than the pixel width requires.";
            return false;
        }

        if (requiredBytes <= 0 || requiredBytes > MaximumPayloadBytes)
        {
            error = $"Frame payload must not exceed {MaximumPayloadBytes} bytes.";
            return false;
        }

        descriptor = new RetinalFrameDescriptor(
            width,
            height,
            stride,
            parsedFormat,
            AdminInputSource.Normalize(inputSource));
        return true;
    }
}

internal sealed record RetinalFrameTransduction(
    IReadOnlyList<SpikeMessage> LeftHemisphereSpikes,
    IReadOnlyList<SpikeMessage> RightHemisphereSpikes,
    int SampleColumns,
    int SampleRows,
    int OnChannelSpikes,
    int OffChannelSpikes,
    float MeanLuminance,
    float MeanTemporalChange)
{
    public int GeneratedSpikes => LeftHemisphereSpikes.Count + RightHemisphereSpikes.Count;

    public IReadOnlyList<SpikeMessage> ForHemisphere(string? hemisphere)
    {
        if (string.Equals(hemisphere, "L", StringComparison.OrdinalIgnoreCase))
        {
            return LeftHemisphereSpikes;
        }

        if (string.Equals(hemisphere, "R", StringComparison.OrdinalIgnoreCase))
        {
            return RightHemisphereSpikes;
        }

        if (LeftHemisphereSpikes.Count == 0)
        {
            return RightHemisphereSpikes;
        }

        if (RightHemisphereSpikes.Count == 0)
        {
            return LeftHemisphereSpikes;
        }

        var combined = new List<SpikeMessage>(GeneratedSpikes);
        combined.AddRange(LeftHemisphereSpikes);
        combined.AddRange(RightHemisphereSpikes);
        return combined;
    }
}

internal sealed class RetinalFrameTransducerRuntime
{
    internal const int SampleColumns = 16;
    internal const int SampleRows = 12;
    private const float ActivationThreshold = 0.035f;
    private readonly object _gate = new();
    private readonly Dictionary<string, RetinalHistory> _historyBySource = new(StringComparer.OrdinalIgnoreCase);

    public RetinalFrameTransduction Transduce(
        ReadOnlySpan<byte> pixels,
        RetinalFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (pixels.Length != descriptor.RequiredBytes)
        {
            throw new ArgumentException("Pixel payload length does not match the frame descriptor.", nameof(pixels));
        }

        var luminance = SampleLuminance(pixels, descriptor);
        float[]? previous;
        lock (_gate)
        {
            if (_historyBySource.TryGetValue(descriptor.InputSource, out var history) &&
                history.Width == SampleColumns &&
                history.Height == SampleRows)
            {
                previous = history.Luminance;
            }
            else
            {
                previous = null;
            }

            _historyBySource[descriptor.InputSource] = new RetinalHistory(
                SampleColumns,
                SampleRows,
                (float[])luminance.Clone());
        }

        var left = new List<SpikeMessage>(SampleColumns * SampleRows / 2);
        var right = new List<SpikeMessage>(SampleColumns * SampleRows / 2);
        var onCount = 0;
        var offCount = 0;
        var luminanceTotal = 0f;
        var temporalTotal = 0f;

        for (var y = 0; y < SampleRows; y++)
        {
            for (var x = 0; x < SampleColumns; x++)
            {
                var index = (y * SampleColumns) + x;
                var center = luminance[index];
                var surround = ComputeSurroundMean(luminance, x, y);
                var spatialContrast = center - surround;
                var temporalDelta = previous is null ? 0f : center - previous[index];
                luminanceTotal += center;
                temporalTotal += MathF.Abs(temporalDelta);

                var hemisphere = x < SampleColumns / 2 ? "R" : "L";
                var target = hemisphere == "L" ? left : right;
                var onActivation = MathF.Max(spatialContrast, temporalDelta);
                var offActivation = MathF.Max(-spatialContrast, -temporalDelta);

                if (onActivation >= ActivationThreshold)
                {
                    target.Add(BuildSpike(
                        tick,
                        timestampMs,
                        hemisphere,
                        "on",
                        index,
                        onActivation));
                    onCount++;
                }

                if (offActivation >= ActivationThreshold)
                {
                    target.Add(BuildSpike(
                        tick,
                        timestampMs,
                        hemisphere,
                        "off",
                        (SampleColumns * SampleRows) + index,
                        offActivation));
                    offCount++;
                }
            }
        }

        var sampleCount = SampleColumns * SampleRows;
        return new RetinalFrameTransduction(
            left,
            right,
            SampleColumns,
            SampleRows,
            onCount,
            offCount,
            luminanceTotal / sampleCount,
            temporalTotal / sampleCount);
    }

    private static float[] SampleLuminance(ReadOnlySpan<byte> pixels, RetinalFrameDescriptor descriptor)
    {
        var sampled = new float[SampleColumns * SampleRows];
        for (var sampleY = 0; sampleY < SampleRows; sampleY++)
        {
            var sourceY = Math.Min(
                descriptor.Height - 1,
                (int)(((sampleY + 0.5) * descriptor.Height) / SampleRows));
            for (var sampleX = 0; sampleX < SampleColumns; sampleX++)
            {
                var sourceX = Math.Min(
                    descriptor.Width - 1,
                    (int)(((sampleX + 0.5) * descriptor.Width) / SampleColumns));
                var offset = checked((sourceY * descriptor.Stride) + (sourceX * descriptor.BytesPerPixel));
                float red;
                float green;
                float blue;
                if (descriptor.PixelFormat == RetinalPixelFormat.Bgra32)
                {
                    blue = pixels[offset];
                    green = pixels[offset + 1];
                    red = pixels[offset + 2];
                }
                else
                {
                    red = pixels[offset];
                    green = pixels[offset + 1];
                    blue = pixels[offset + 2];
                }

                sampled[(sampleY * SampleColumns) + sampleX] =
                    ((0.2126f * red) + (0.7152f * green) + (0.0722f * blue)) / 255f;
            }
        }

        return sampled;
    }

    private static float ComputeSurroundMean(float[] luminance, int x, int y)
    {
        var total = 0f;
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            var sampleY = y + dy;
            if (sampleY < 0 || sampleY >= SampleRows)
            {
                continue;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var sampleX = x + dx;
                if (sampleX < 0 || sampleX >= SampleColumns)
                {
                    continue;
                }

                total += luminance[(sampleY * SampleColumns) + sampleX];
                count++;
            }
        }

        return count > 0 ? total / count : luminance[(y * SampleColumns) + x];
    }

    private static SpikeMessage BuildSpike(
        long tick,
        double timestampMs,
        string hemisphere,
        string channel,
        int retinotopicIndex,
        float activation)
    {
        var boundedActivation = Math.Clamp(activation, ActivationThreshold, 1f);
        return new SpikeMessage
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = timestampMs,
            SourceStructure = StructureId.Retina,
            TargetStructure = StructureId.Retina,
            SourceNeuronId = $"{hemisphere}:photoreceptor_{channel}_{retinotopicIndex}",
            TargetNeuronId = $"{hemisphere}:retinal_ganglion_{channel}_{retinotopicIndex}",
            SynapseId = CreateStableSynapseId(hemisphere, channel, retinotopicIndex),
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = Math.Clamp(0.35f + (boundedActivation * 3.2f), 0.05f, 5f),
            ReuptakeRate = Math.Clamp(3.5f + ((1f - boundedActivation) * 3f), 2f, 8f),
            SpikeType = boundedActivation >= 0.55f ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL,
            IsFeedback = false,
            ModulationContext = null
        };
    }

    private static Guid CreateStableSynapseId(string hemisphere, string channel, int retinotopicIndex)
    {
        var key = Encoding.UTF8.GetBytes($"retina:{hemisphere}:{channel}:{retinotopicIndex}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(key, digest);
        return new Guid(digest[..16]);
    }

    private sealed record RetinalHistory(int Width, int Height, float[] Luminance);
}
