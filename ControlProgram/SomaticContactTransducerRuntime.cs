using System.Security.Cryptography;
using System.Text;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record SomaticContactDescriptor(
    long Sequence,
    long TimestampMs,
    float BodyPositionX,
    float BodyPositionY,
    float BodyPositionZ,
    float SurfaceNormalX,
    float SurfaceNormalY,
    float SurfaceNormalZ,
    float ForceNewtons,
    float ImpulseNewtonSeconds,
    float PenetrationMillimeters,
    float TangentialSpeedMetersPerSecond,
    float ContactAreaSquareMillimeters,
    float DurationMilliseconds,
    string InputSource)
{
    private const float MaximumBodyCoordinateMeters = 5f;
    private const float MaximumForceNewtons = 20_000f;
    private const float MaximumImpulseNewtonSeconds = 1_000f;
    private const float MaximumPenetrationMillimeters = 250f;
    private const float MaximumTangentialSpeedMetersPerSecond = 100f;
    private const float MaximumContactAreaSquareMillimeters = 1_000_000f;
    private const float MaximumDurationMilliseconds = 60_000f;

    public static bool TryCreate(
        SomaticContactFrameRequest? request,
        out SomaticContactDescriptor? descriptor,
        out string? error)
    {
        descriptor = null;
        error = null;
        if (request is null)
        {
            error = "Request payload missing.";
            return false;
        }

        if (request.Sequence < 0 || request.TimestampMs < 0)
        {
            error = "Sequence and timestampMs must be non-negative.";
            return false;
        }

        ReadOnlySpan<float> values =
        [
            request.BodyPositionX,
            request.BodyPositionY,
            request.BodyPositionZ,
            request.SurfaceNormalX,
            request.SurfaceNormalY,
            request.SurfaceNormalZ,
            request.ForceNewtons,
            request.ImpulseNewtonSeconds,
            request.PenetrationMillimeters,
            request.TangentialSpeedMetersPerSecond,
            request.ContactAreaSquareMillimeters,
            request.DurationMilliseconds
        ];
        for (var i = 0; i < values.Length; i++)
        {
            if (!float.IsFinite(values[i]))
            {
                error = "All physical contact measurements must be finite numbers.";
                return false;
            }
        }

        if (MathF.Abs(request.BodyPositionX) > MaximumBodyCoordinateMeters ||
            MathF.Abs(request.BodyPositionY) > MaximumBodyCoordinateMeters ||
            MathF.Abs(request.BodyPositionZ) > MaximumBodyCoordinateMeters)
        {
            error = $"Body-local contact coordinates must be within {MaximumBodyCoordinateMeters} metres.";
            return false;
        }

        if (!InRange(request.SurfaceNormalX, -1f, 1f) ||
            !InRange(request.SurfaceNormalY, -1f, 1f) ||
            !InRange(request.SurfaceNormalZ, -1f, 1f))
        {
            error = "Surface-normal components must each be between -1 and 1.";
            return false;
        }

        if (!InRange(request.ForceNewtons, 0f, MaximumForceNewtons) ||
            !InRange(request.ImpulseNewtonSeconds, 0f, MaximumImpulseNewtonSeconds) ||
            !InRange(request.PenetrationMillimeters, 0f, MaximumPenetrationMillimeters) ||
            !InRange(request.TangentialSpeedMetersPerSecond, 0f, MaximumTangentialSpeedMetersPerSecond) ||
            !InRange(request.ContactAreaSquareMillimeters, 0f, MaximumContactAreaSquareMillimeters) ||
            !InRange(request.DurationMilliseconds, 0f, MaximumDurationMilliseconds))
        {
            error = "One or more physical contact measurements exceed the supported range.";
            return false;
        }

        var signalMagnitude = request.ForceNewtons + request.ImpulseNewtonSeconds +
                              request.PenetrationMillimeters + request.TangentialSpeedMetersPerSecond;
        var normalMagnitude = MathF.Sqrt(
            (request.SurfaceNormalX * request.SurfaceNormalX) +
            (request.SurfaceNormalY * request.SurfaceNormalY) +
            (request.SurfaceNormalZ * request.SurfaceNormalZ));
        if (signalMagnitude > 0f && normalMagnitude < 0.01f)
        {
            error = "A non-zero contact signal requires a non-zero surface normal.";
            return false;
        }

        var normalScale = normalMagnitude > 0f ? 1f / normalMagnitude : 0f;
        descriptor = new SomaticContactDescriptor(
            request.Sequence,
            request.TimestampMs,
            request.BodyPositionX,
            request.BodyPositionY,
            request.BodyPositionZ,
            request.SurfaceNormalX * normalScale,
            request.SurfaceNormalY * normalScale,
            request.SurfaceNormalZ * normalScale,
            request.ForceNewtons,
            request.ImpulseNewtonSeconds,
            request.PenetrationMillimeters,
            request.TangentialSpeedMetersPerSecond,
            request.ContactAreaSquareMillimeters,
            request.DurationMilliseconds,
            AdminInputSource.Normalize(request.InputSource));
        return true;
    }

    private static bool InRange(float value, float minimum, float maximum)
        => value >= minimum && value <= maximum;
}

internal sealed record SomaticContactTransduction(
    IReadOnlyList<SpikeMessage> LeftHemisphereSpikes,
    IReadOnlyList<SpikeMessage> RightHemisphereSpikes,
    int ReceptorSector,
    int ActiveReceptorPopulations,
    float PressureActivation,
    float OnsetActivation,
    float VibrationActivation,
    float StretchActivation,
    float HighThresholdActivation)
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

internal sealed class SomaticContactTransducerRuntime
{
    private const float MinimumActivation = 0.035f;
    private const int MaximumFibersPerPopulation = 5;
    private const float MidlineHalfWidthMeters = 0.035f;
    private readonly object _gate = new();
    private readonly Dictionary<string, float> _previousPressureBySourceAndSector = new(StringComparer.OrdinalIgnoreCase);

    public SomaticContactTransduction Transduce(
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var sector = ComputeReceptorSector(
            descriptor.BodyPositionX,
            descriptor.BodyPositionY,
            descriptor.BodyPositionZ);
        var pressure = Math.Clamp(
            (descriptor.ForceNewtons / 850f) +
            (descriptor.PenetrationMillimeters / 50f * 0.38f),
            0f,
            1f);
        var previousPressure = ExchangePressure($"{descriptor.InputSource}:{sector}", pressure);
        var pressureOnset = MathF.Max(0f, pressure - previousPressure);
        var impulse = Math.Clamp(descriptor.ImpulseNewtonSeconds / 70f, 0f, 1f);
        var slip = Math.Clamp(descriptor.TangentialSpeedMetersPerSecond / 8f, 0f, 1f);
        var indentation = Math.Clamp(descriptor.PenetrationMillimeters / 32f, 0f, 1f);
        var duration = Math.Clamp(descriptor.DurationMilliseconds / 600f, 0f, 1f);
        var areaDensity = descriptor.ContactAreaSquareMillimeters > 0f
            ? Math.Clamp(descriptor.ForceNewtons / descriptor.ContactAreaSquareMillimeters / 0.12f, 0f, 1f)
            : pressure;

        var sustainedPressure = Math.Clamp((pressure * 0.78f) + (duration * pressure * 0.22f), 0f, 1f);
        var onset = Math.Clamp((pressureOnset * 0.72f) + (impulse * 0.72f), 0f, 1f);
        var vibration = Math.Clamp((impulse * 0.60f) + (slip * 0.72f), 0f, 1f);
        var stretch = Math.Clamp((slip * 0.62f) + (indentation * 0.38f), 0f, 1f);
        var highThreshold = Math.Clamp(
            MathF.Max(
                MathF.Max((descriptor.ForceNewtons - 900f) / 2_600f, (descriptor.PenetrationMillimeters - 12f) / 38f),
                areaDensity - 0.55f),
            0f,
            1f);

        var left = new List<SpikeMessage>(20);
        var right = new List<SpikeMessage>(20);
        if (descriptor.BodyPositionX >= -MidlineHalfWidthMeters)
        {
            BuildHemisphereSpikes(
                left,
                "L",
                sector,
                sustainedPressure,
                onset,
                vibration,
                stretch,
                highThreshold,
                descriptor,
                tick,
                timestampMs);
        }

        if (descriptor.BodyPositionX <= MidlineHalfWidthMeters)
        {
            BuildHemisphereSpikes(
                right,
                "R",
                sector,
                sustainedPressure,
                onset,
                vibration,
                stretch,
                highThreshold,
                descriptor,
                tick,
                timestampMs);
        }

        var activePopulations = CountActive(sustainedPressure, onset, vibration, stretch, highThreshold);
        return new SomaticContactTransduction(
            left,
            right,
            sector,
            activePopulations,
            sustainedPressure,
            onset,
            vibration,
            stretch,
            highThreshold);
    }

    private float ExchangePressure(string key, float current)
    {
        lock (_gate)
        {
            _previousPressureBySourceAndSector.TryGetValue(key, out var previous);
            _previousPressureBySourceAndSector[key] = current;
            return previous;
        }
    }

    private static void BuildHemisphereSpikes(
        List<SpikeMessage> output,
        string hemisphere,
        int sector,
        float sustainedPressure,
        float onset,
        float vibration,
        float stretch,
        float highThreshold,
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        AddPopulation(output, hemisphere, sector, "merkel_sa1", sustainedPressure, descriptor, tick, timestampMs);
        AddPopulation(output, hemisphere, sector, "meissner_ra1", onset, descriptor, tick, timestampMs);
        AddPopulation(output, hemisphere, sector, "pacinian_ra2", vibration, descriptor, tick, timestampMs);
        AddPopulation(output, hemisphere, sector, "ruffini_sa2", stretch, descriptor, tick, timestampMs);
        AddPopulation(output, hemisphere, sector, "mechanonociceptor", highThreshold, descriptor, tick, timestampMs);
    }

    private static void AddPopulation(
        List<SpikeMessage> output,
        string hemisphere,
        int sector,
        string receptor,
        float activation,
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        if (activation < MinimumActivation)
        {
            return;
        }

        var fibers = Math.Clamp(
            1 + (int)MathF.Floor((activation - MinimumActivation) * 5.5f),
            1,
            MaximumFibersPerPopulation);
        for (var fiber = 0; fiber < fibers; fiber++)
        {
            output.Add(new SpikeMessage
            {
                MessageId = Guid.NewGuid(),
                TimestampMs = timestampMs,
                SourceStructure = StructureId.SomaticAfferents,
                TargetStructure = StructureId.SomaticAfferents,
                SourceNeuronId = $"{hemisphere}:{receptor}:sector_{sector}",
                TargetNeuronId = $"{hemisphere}:primary_afferent:sector_{sector}:fiber_{fiber}",
                SynapseId = CreateStableSynapseId(hemisphere, receptor, sector, fiber),
                Neurotransmitter = NTEnum.GLUTAMATE,
                VesicleQuanta = Math.Clamp(0.22f + (activation * 4.6f), 0.05f, 6f),
                ReuptakeRate = Math.Clamp(6.2f - (activation * 3.1f), 1.8f, 8f),
                SpikeType = activation >= 0.68f ||
                            (activation >= 0.42f && ((tick + descriptor.Sequence + fiber) & 1) == 0)
                    ? SpikeTypeEnum.BURST
                    : SpikeTypeEnum.ACTION_POTENTIAL,
                IsFeedback = false,
                ModulationContext = null
            });
        }
    }

    private static int ComputeReceptorSector(float x, float y, float z)
    {
        var azimuth = MathF.Atan2(x, z);
        var azimuthBin = Math.Clamp((int)MathF.Floor(((azimuth + MathF.PI) / (2f * MathF.PI)) * 16f), 0, 15);
        var elevation = Math.Clamp((y + 2f) / 4f, 0f, 0.9999f);
        var elevationBin = Math.Clamp((int)MathF.Floor(elevation * 8f), 0, 7);
        var radialBin = Math.Clamp((int)MathF.Floor(MathF.Min(1f, MathF.Sqrt((x * x) + (z * z)) / 2f) * 4f), 0, 3);
        return (elevationBin * 64) + (azimuthBin * 4) + radialBin;
    }

    private static int CountActive(params float[] activations)
        => activations.Count(value => value >= MinimumActivation);

    private static Guid CreateStableSynapseId(string hemisphere, string receptor, int sector, int fiber)
    {
        var key = Encoding.UTF8.GetBytes($"somatic:{hemisphere}:{receptor}:{sector}:{fiber}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(key, digest);
        return new Guid(digest[..16]);
    }
}
