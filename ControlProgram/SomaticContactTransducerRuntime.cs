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
    float HighThresholdActivation,
    string ReceptorField,
    float ReceptorDensityScale)
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
    private const int MaximumFibersPerPopulation = 18;
    private const float MidlineHalfWidthMeters = 0.035f;
    private readonly object _gate = new();
    private readonly Dictionary<string, float> _previousPressureBySourceAndSector = new(StringComparer.OrdinalIgnoreCase);

    public SomaticContactTransduction Transduce(
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var receptorField = ResolveReceptorField(
            descriptor.BodyPositionX,
            descriptor.BodyPositionY,
            descriptor.BodyPositionZ,
            descriptor.InputSource);
        var sector = receptorField.Sector;
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
        var sustainedMechanicalThreat = Math.Clamp(
            MathF.Max(0f, descriptor.ForceNewtons - 180f) / 1_500f *
            (0.20f + (duration * 0.80f)),
            0f,
            1f);
        var highThreshold = Math.Clamp(
            MathF.Max(
                MathF.Max(
                    MathF.Max((descriptor.ForceNewtons - 900f) / 2_600f, (descriptor.PenetrationMillimeters - 12f) / 38f),
                    areaDensity - 0.55f),
                sustainedMechanicalThreat),
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
                receptorField,
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
                receptorField,
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
            highThreshold,
            receptorField.Name,
            receptorField.DensityScale);
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
        SomaticReceptorField receptorField,
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        AddPopulation(output, hemisphere, sector, "merkel_sa1", sustainedPressure,
            receptorField.DensityScale * receptorField.DiscriminativeTouchScale, descriptor, tick, timestampMs);
        AddPopulation(output, hemisphere, sector, "meissner_ra1", onset,
            receptorField.DensityScale * receptorField.DiscriminativeTouchScale, descriptor, tick, timestampMs);
        AddPopulation(output, hemisphere, sector, "pacinian_ra2", vibration,
            0.80f + (receptorField.DensityScale * 0.20f), descriptor, tick, timestampMs);
        AddPopulation(output, hemisphere, sector, "ruffini_sa2", stretch,
            0.72f + (receptorField.DensityScale * 0.28f), descriptor, tick, timestampMs);
        AddPopulation(output, hemisphere, sector, "free_nerve_ending_mechanonociceptor", highThreshold,
            receptorField.FreeNerveEndingScale, descriptor, tick, timestampMs);
    }

    private static void AddPopulation(
        List<SpikeMessage> output,
        string hemisphere,
        int sector,
        string receptor,
        float activation,
        float receptorDensityScale,
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        if (activation < MinimumActivation)
        {
            return;
        }

        var baseFibers = 1 + (int)MathF.Floor((activation - MinimumActivation) * 5.5f);
        var fibers = Math.Clamp(
            (int)MathF.Ceiling(baseFibers * Math.Clamp(receptorDensityScale, 0.35f, 3.5f)),
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

    private static SomaticReceptorField ResolveReceptorField(float x, float y, float z, string inputSource)
    {
        var absoluteX = MathF.Abs(x);
        var normalizedSource = inputSource.ToLowerInvariant();
        SomaticReceptorField field;
        if (normalizedSource.Contains("hand", StringComparison.Ordinal))
        {
            field = new SomaticReceptorField("hand", 0.022f, 3.0f, 1.18f, 2.4f);
        }
        else if (normalizedSource.Contains("foot", StringComparison.Ordinal))
        {
            field = new SomaticReceptorField("foot", 0.050f, 1.8f, 1.05f, 1.7f);
        }
        else if (normalizedSource.Contains("head", StringComparison.Ordinal))
        {
            field = z >= 0.14f && absoluteX <= 0.15f
                ? new SomaticReceptorField("lips", 0.018f, 3.4f, 1.15f, 2.6f)
                : new SomaticReceptorField("face", 0.032f, 2.5f, 1.10f, 2.1f);
        }
        else if (normalizedSource.Contains("arm", StringComparison.Ordinal) ||
                 normalizedSource.Contains("forearm", StringComparison.Ordinal) ||
                 normalizedSource.Contains("thigh", StringComparison.Ordinal) ||
                 normalizedSource.Contains("shin", StringComparison.Ordinal) ||
                 normalizedSource.Contains("knee", StringComparison.Ordinal))
        {
            field = new SomaticReceptorField("distal_limb", 0.075f, 1.25f, 1.0f, 1.4f);
        }
        else
        {
            field = y switch
            {
                >= 0.58f when z >= 0.28f && absoluteX <= 0.15f =>
                    new SomaticReceptorField("lips", 0.018f, 3.4f, 1.15f, 2.6f),
                >= 0.52f =>
                    new SomaticReceptorField("face", 0.032f, 2.5f, 1.10f, 2.1f),
                >= 0.12f when absoluteX >= 0.32f && z >= 0.28f =>
                    new SomaticReceptorField("hand", 0.022f, 3.0f, 1.18f, 2.4f),
                <= -0.68f =>
                    new SomaticReceptorField("foot", 0.050f, 1.8f, 1.05f, 1.7f),
                _ when absoluteX >= 0.34f =>
                    new SomaticReceptorField("distal_limb", 0.075f, 1.25f, 1.0f, 1.4f),
                _ =>
                    new SomaticReceptorField("general_skin", 0.145f, 0.72f, 0.82f, 1.0f)
            };
        }

        return field with { Sector = ComputeSpatialSector(field.Name, field.ReceptiveFieldSpacingMeters, x, y, z) };
    }

    private static int ComputeSpatialSector(string field, float spacing, float x, float y, float z)
    {
        var qx = (int)MathF.Round(x / spacing);
        var qy = (int)MathF.Round(y / spacing);
        var qz = (int)MathF.Round(z / spacing);
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in field)
            {
                hash = (hash ^ character) * 16777619;
            }
            hash = (hash ^ (uint)qx) * 16777619;
            hash = (hash ^ (uint)qy) * 16777619;
            hash = (hash ^ (uint)qz) * 16777619;
            return (int)(hash & 0x7FFFFFFF);
        }
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

    private sealed record SomaticReceptorField(
        string Name,
        float ReceptiveFieldSpacingMeters,
        float DensityScale,
        float DiscriminativeTouchScale,
        float FreeNerveEndingScale,
        int Sector = 0);
}
