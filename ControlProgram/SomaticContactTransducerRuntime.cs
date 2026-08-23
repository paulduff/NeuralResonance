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

        (string Name, float Value)[] values =
        [
            ("bodyPositionX", request.BodyPositionX),
            ("bodyPositionY", request.BodyPositionY),
            ("bodyPositionZ", request.BodyPositionZ),
            ("surfaceNormalX", request.SurfaceNormalX),
            ("surfaceNormalY", request.SurfaceNormalY),
            ("surfaceNormalZ", request.SurfaceNormalZ),
            ("forceNewtons", request.ForceNewtons),
            ("impulseNewtonSeconds", request.ImpulseNewtonSeconds),
            ("penetrationMillimeters", request.PenetrationMillimeters),
            ("tangentialSpeedMetersPerSecond", request.TangentialSpeedMetersPerSecond),
            ("contactAreaSquareMillimeters", request.ContactAreaSquareMillimeters),
            ("durationMilliseconds", request.DurationMilliseconds)
        ];
        foreach (var measurement in values)
        {
            if (!float.IsFinite(measurement.Value))
            {
                error = $"Physical contact measurement '{measurement.Name}' must be finite.";
                return false;
            }
        }

        if (!TryValidateMagnitude("bodyPositionX", request.BodyPositionX, MaximumBodyCoordinateMeters, out error) ||
            !TryValidateMagnitude("bodyPositionY", request.BodyPositionY, MaximumBodyCoordinateMeters, out error) ||
            !TryValidateMagnitude("bodyPositionZ", request.BodyPositionZ, MaximumBodyCoordinateMeters, out error))
        {
            return false;
        }

        if (!InRange(request.SurfaceNormalX, -1f, 1f) ||
            !InRange(request.SurfaceNormalY, -1f, 1f) ||
            !InRange(request.SurfaceNormalZ, -1f, 1f))
        {
            error = "Surface-normal components must each be between -1 and 1.";
            return false;
        }

        if (!TryValidateRange("forceNewtons", request.ForceNewtons, 0f, MaximumForceNewtons, out error) ||
            !TryValidateRange(
                "impulseNewtonSeconds",
                request.ImpulseNewtonSeconds,
                0f,
                MaximumImpulseNewtonSeconds,
                out error) ||
            !TryValidateRange(
                "penetrationMillimeters",
                request.PenetrationMillimeters,
                0f,
                MaximumPenetrationMillimeters,
                out error) ||
            !TryValidateRange(
                "tangentialSpeedMetersPerSecond",
                request.TangentialSpeedMetersPerSecond,
                0f,
                MaximumTangentialSpeedMetersPerSecond,
                out error) ||
            !TryValidateRange(
                "contactAreaSquareMillimeters",
                request.ContactAreaSquareMillimeters,
                0f,
                MaximumContactAreaSquareMillimeters,
                out error) ||
            !TryValidateRange(
                "durationMilliseconds",
                request.DurationMilliseconds,
                0f,
                MaximumDurationMilliseconds,
                out error))
        {
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

    private static bool TryValidateMagnitude(
        string name,
        float value,
        float maximum,
        out string? error)
        => TryValidateRange(name, value, -maximum, maximum, out error);

    private static bool TryValidateRange(
        string name,
        float value,
        float minimum,
        float maximum,
        out string? error)
    {
        if (InRange(value, minimum, maximum))
        {
            error = null;
            return true;
        }

        error = $"Physical contact measurement '{name}' must be within [{minimum}, {maximum}]; received {value}.";
        return false;
    }
}

internal sealed record SomaticContactTransduction(
    IReadOnlyList<SpikeMessage> LeftHemisphereSpikes,
    IReadOnlyList<SpikeMessage> RightHemisphereSpikes,
    IReadOnlyList<SpikeMessage> LeftSpinalWithdrawalSpikes,
    IReadOnlyList<SpikeMessage> RightSpinalWithdrawalSpikes,
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
    public int GeneratedSpinalWithdrawalSpikes =>
        LeftSpinalWithdrawalSpikes.Count + RightSpinalWithdrawalSpikes.Count;

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

    public IReadOnlyList<SpikeMessage> ForSpinalHemisphere(string? hemisphere)
    {
        if (string.Equals(hemisphere, "L", StringComparison.OrdinalIgnoreCase))
        {
            return LeftSpinalWithdrawalSpikes;
        }

        if (string.Equals(hemisphere, "R", StringComparison.OrdinalIgnoreCase))
        {
            return RightSpinalWithdrawalSpikes;
        }

        if (LeftSpinalWithdrawalSpikes.Count == 0)
        {
            return RightSpinalWithdrawalSpikes;
        }

        if (RightSpinalWithdrawalSpikes.Count == 0)
        {
            return LeftSpinalWithdrawalSpikes;
        }

        var combined = new List<SpikeMessage>(GeneratedSpinalWithdrawalSpikes);
        combined.AddRange(LeftSpinalWithdrawalSpikes);
        combined.AddRange(RightSpinalWithdrawalSpikes);
        return combined;
    }
}

internal sealed class SomaticContactTransducerRuntime
{
    private const float MinimumActivation = 0.035f;
    private const int MaximumFibersPerPopulation = 18;
    private const float MidlineHalfWidthMeters = 0.035f;
    private const double WithdrawalIntegrationWindowMilliseconds = 20.0;
    private const double WithdrawalContactContinuityMilliseconds = 180.0;
    private const double WithdrawalPressureMemoryDecayMilliseconds = 180.0;
    private const double AcuteWithdrawalRefractoryMilliseconds = 220.0;
    private const double MinimumStaticWithdrawalIntervalMilliseconds = 900.0;
    private const double MaximumStaticWithdrawalIntervalMilliseconds = 3_000.0;
    private const float WithdrawalThreatIncreaseThreshold = 0.04f;
    private const float MaximumPhysiologicalPlantarPressureKPa = 400f;
    private const float PlantarPressureThreatRangeKPa = 400f;
    private const float MaximumPhysiologicalPlantarImpulseNewtonSeconds = 35f;
    private const float PlantarImpactThreatRangeNewtonSeconds = 70f;
    private const int MaximumReplayEntries = 4_096;
    private readonly object _gate = new();
    private readonly Dictionary<string, float> _previousPressureBySourceAndSector = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WithdrawalFieldState> _withdrawalFieldStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<SomaticFrameIdentity, SomaticContactTransduction> _replayCache = [];
    private readonly Queue<SomaticFrameIdentity> _replayOrder = [];

    public SomaticContactTransduction Transduce(
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var identity = new SomaticFrameIdentity(
            descriptor.InputSource,
            descriptor.Sequence,
            descriptor.TimestampMs);
        lock (_gate)
        {
            if (_replayCache.TryGetValue(identity, out var replay))
            {
                return replay;
            }

            var transduction = TransduceCore(descriptor, tick, timestampMs);
            _replayCache.Add(identity, transduction);
            _replayOrder.Enqueue(identity);
            while (_replayOrder.Count > MaximumReplayEntries)
            {
                _replayCache.Remove(_replayOrder.Dequeue());
            }
            return transduction;
        }
    }

    private SomaticContactTransduction TransduceCore(
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {

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
        var contactPressureKPa = descriptor.ContactAreaSquareMillimeters > 0f
            ? descriptor.ForceNewtons / descriptor.ContactAreaSquareMillimeters * 1_000f
            : 0f;
        var plantarSupportContact = receptorField.Name == "foot" &&
            descriptor.SurfaceNormalY >= 0.55f;
        var plantarPressureThreat = plantarSupportContact
            ? Math.Clamp(
                (contactPressureKPa - MaximumPhysiologicalPlantarPressureKPa) /
                PlantarPressureThreatRangeKPa,
                0f,
                1f)
            : 0f;
        var plantarImpactThreat = plantarSupportContact
            ? Math.Clamp(
                (descriptor.ImpulseNewtonSeconds - MaximumPhysiologicalPlantarImpulseNewtonSeconds) /
                PlantarImpactThreatRangeNewtonSeconds,
                0f,
                1f)
            : 0f;
        var ordinaryPlantarSupport = plantarSupportContact &&
            descriptor.SurfaceNormalY >= 0.55f &&
            descriptor.PenetrationMillimeters <= 4f &&
            descriptor.ForceNewtons <= 1_800f &&
            plantarPressureThreat < MinimumActivation &&
            plantarImpactThreat < MinimumActivation;

        var sustainedPressure = Math.Clamp((pressure * 0.78f) + (duration * pressure * 0.22f), 0f, 1f);
        var onset = Math.Clamp((pressureOnset * 0.72f) + (impulse * 0.72f), 0f, 1f);
        var vibration = Math.Clamp((impulse * 0.60f) + (slip * 0.72f), 0f, 1f);
        var stretch = Math.Clamp((slip * 0.62f) + (indentation * 0.38f), 0f, 1f);
        var chronicDuration = Math.Clamp(
            (descriptor.DurationMilliseconds - 1_500f) / 58_500f,
            0f,
            1f);
        var sustainedMechanicalThreat = ordinaryPlantarSupport
            ? 0f
            : Math.Clamp(
                MathF.Max(
                    MathF.Max(0f, descriptor.ForceNewtons - 180f) / 1_500f *
                    (0.20f + (duration * 0.80f)),
                    ((pressure * 0.20f) + (areaDensity * 0.45f)) * chronicDuration),
                0f,
                1f);
        var highThreshold = Math.Clamp(
            MathF.Max(
                MathF.Max(
                    MathF.Max(
                        MathF.Max(
                            (descriptor.ForceNewtons - 900f) / 2_600f,
                            (descriptor.PenetrationMillimeters - 12f) / 38f),
                        plantarSupportContact ? plantarPressureThreat : areaDensity - 0.55f),
                    plantarImpactThreat),
                sustainedMechanicalThreat),
            0f,
            1f);
        var anatomicalRegion = ResolveAnatomicalRegion(descriptor, receptorField.Name);
        var withdrawalField = ResolveWithdrawalField(descriptor, anatomicalRegion);
        var withdrawalPressureOnset = MathF.Max(
            0f,
            pressure - ExchangeWithdrawalFieldPressure(withdrawalField, pressure, timestampMs));
        var withdrawalActivation = ResolveWithdrawalActivation(
            withdrawalField,
            timestampMs,
            descriptor.DurationMilliseconds,
            withdrawalPressureOnset,
            slip,
            descriptor.ForceNewtons,
            descriptor.PenetrationMillimeters,
            highThreshold);

        var left = new List<SpikeMessage>(20);
        var right = new List<SpikeMessage>(20);
        var leftSpinal = new List<SpikeMessage>(12);
        var rightSpinal = new List<SpikeMessage>(12);
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
            BuildSpinalWithdrawalCollaterals(
                leftSpinal,
                "L",
                sector,
                anatomicalRegion,
                withdrawalActivation,
                receptorField.FreeNerveEndingScale,
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
            BuildSpinalWithdrawalCollaterals(
                rightSpinal,
                "R",
                sector,
                anatomicalRegion,
                withdrawalActivation,
                receptorField.FreeNerveEndingScale,
                descriptor,
                tick,
                timestampMs);
        }

        var activePopulations = CountActive(sustainedPressure, onset, vibration, stretch, highThreshold);
        return new SomaticContactTransduction(
            left,
            right,
            leftSpinal,
            rightSpinal,
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

    private readonly record struct SomaticFrameIdentity(
        string InputSource,
        long Sequence,
        long TimestampMs);

    private static void BuildSpinalWithdrawalCollaterals(
        List<SpikeMessage> output,
        string hemisphere,
        int sector,
        string anatomicalRegion,
        float activation,
        float receptorDensityScale,
        SomaticContactDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        if (activation < MinimumActivation ||
            !NeuronalWithdrawalReflex.SupportsAnatomicalRegion(anatomicalRegion) ||
            !CanResolveWithdrawalDirection(anatomicalRegion, descriptor))
        {
            return;
        }

        var fibers = Math.Clamp(
            (int)MathF.Ceiling((1f + (activation * 6f)) * Math.Clamp(receptorDensityScale, 0.5f, 2.8f)),
            2,
            12);
        for (var fiber = 0; fiber < fibers; fiber++)
        {
            output.Add(new SpikeMessage
            {
                MessageId = Guid.NewGuid(),
                TimestampMs = timestampMs,
                SourceStructure = StructureId.SomaticAfferents,
                TargetStructure = StructureId.SpinalCordMotor,
                SourceNeuronId = $"{hemisphere}:{anatomicalRegion}:free_nerve_ending_mechanonociceptor:{ResolveContactNormalSector(descriptor)}:sector_{sector}:fiber_{fiber}",
                TargetNeuronId = $"{hemisphere}:spinal_withdrawal_interneuron:sector_{sector}:fiber_{fiber}",
                SynapseId = CreateStableWithdrawalSynapseId(hemisphere, anatomicalRegion, sector, fiber),
                Neurotransmitter = NTEnum.GLUTAMATE,
                VesicleQuanta = Math.Clamp(0.75f + (activation * 4.25f), 0.75f, 5f),
                ReuptakeRate = Math.Clamp(5.2f - (activation * 2.2f), 1.8f, 6f),
                SpikeType = activation >= 0.48f || ((tick + descriptor.Sequence + fiber) & 1) == 0
                    ? SpikeTypeEnum.BURST
                    : SpikeTypeEnum.ACTION_POTENTIAL,
                IsFeedback = false,
                ModulationContext = null
            });
        }
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

    private float ExchangeWithdrawalFieldPressure(string key, float current, double timestampMs)
    {
        lock (_gate)
        {
            if (!_withdrawalFieldStates.TryGetValue(key, out var state))
            {
                state = new WithdrawalFieldState
                {
                    WindowTimestampMilliseconds = timestampMs,
                    WindowPeakPressure = current
                };
                _withdrawalFieldStates[key] = state;
                return 0f;
            }

            var elapsed = timestampMs - state.WindowTimestampMilliseconds;
            if (!double.IsFinite(elapsed) || elapsed < 0.0)
            {
                state.PreviousWindowPeakPressure = 0f;
                state.WindowPeakPressure = current;
                state.WindowTimestampMilliseconds = timestampMs;
                return 0f;
            }

            if (elapsed <= WithdrawalIntegrationWindowMilliseconds)
            {
                var reference = MathF.Max(
                    state.PreviousWindowPeakPressure,
                    state.WindowPeakPressure);
                state.WindowPeakPressure = MathF.Max(state.WindowPeakPressure, current);
                return reference;
            }

            var previousWindowPeak = state.WindowPeakPressure;
            if (elapsed > WithdrawalContactContinuityMilliseconds)
            {
                var inactiveDuration = elapsed - WithdrawalContactContinuityMilliseconds;
                previousWindowPeak *= (float)Math.Exp(
                    -inactiveDuration / WithdrawalPressureMemoryDecayMilliseconds);
            }

            state.PreviousWindowPeakPressure = previousWindowPeak;
            state.WindowPeakPressure = current;
            state.WindowTimestampMilliseconds = timestampMs;
            return previousWindowPeak;
        }
    }

    private float ResolveWithdrawalActivation(
        string key,
        double timestampMs,
        float durationMilliseconds,
        float pressureOnset,
        float slip,
        float forceNewtons,
        float penetrationMillimeters,
        float highThreshold)
    {
        if (highThreshold < MinimumActivation)
        {
            return 0f;
        }

        // Fast nociceptive change remains authoritative. Static pressure pain is
        // still represented continuously by the ascending free-nerve-ending
        // population, but its spinal collateral adapts into separated bursts.
        // This prevents an unchanging wall contact from becoming a permanent
        // flexor-withdrawal command while preserving immediate local protection.
        var damagingLoad = Math.Clamp(
            MathF.Max(
                (forceNewtons - 1_800f) / 2_200f,
                (penetrationMillimeters - 8f) / 28f),
            0f,
            1f);
        var acuteActivation = Math.Clamp(
            MathF.Max(MathF.Max(pressureOnset, slip * 0.72f), damagingLoad),
            0f,
            1f);
        var adaptation = Math.Clamp(durationMilliseconds / 60_000f, 0f, 1f);
        var pulseInterval = MinimumStaticWithdrawalIntervalMilliseconds +
            ((MaximumStaticWithdrawalIntervalMilliseconds - MinimumStaticWithdrawalIntervalMilliseconds) * adaptation);
        var threatActivation = MathF.Max(highThreshold, acuteActivation);
        var requiredInterval = acuteActivation >= MinimumActivation
            ? AcuteWithdrawalRefractoryMilliseconds
            : pulseInterval;
        if (!TryBeginWithdrawalPulse(
                key,
                timestampMs,
                requiredInterval,
                threatActivation,
                allowThreatIncreaseBypass: acuteActivation >= MinimumActivation))
        {
            return 0f;
        }

        if (acuteActivation >= MinimumActivation)
        {
            return threatActivation;
        }

        return Math.Clamp(highThreshold * (0.30f - (adaptation * 0.08f)), 0f, 1f);
    }

    private bool TryBeginWithdrawalPulse(
        string key,
        double timestampMs,
        double intervalMilliseconds,
        float threatActivation,
        bool allowThreatIncreaseBypass)
    {
        lock (_gate)
        {
            if (!_withdrawalFieldStates.TryGetValue(key, out var state))
            {
                state = new WithdrawalFieldState();
                _withdrawalFieldStates[key] = state;
            }

            var elapsed = timestampMs - state.LastPulseTimestampMilliseconds;
            var meaningfulThreatIncrease = allowThreatIncreaseBypass &&
                threatActivation >= state.LastPulseThreatActivation + WithdrawalThreatIncreaseThreshold;
            if (state.HasPulse &&
                elapsed >= 0.0 &&
                elapsed < intervalMilliseconds &&
                !meaningfulThreatIncrease)
            {
                return false;
            }

            state.HasPulse = true;
            state.LastPulseTimestampMilliseconds = timestampMs;
            state.LastPulseThreatActivation = threatActivation;
            return true;
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

    private static string ResolveAnatomicalRegion(SomaticContactDescriptor descriptor, string receptorField)
    {
        var source = descriptor.InputSource.ToLowerInvariant();
        foreach (var region in new[] { "left_hand", "right_hand", "left_forearm", "right_forearm", "left_arm", "right_arm", "left_foot", "right_foot", "left_shin", "right_shin", "left_knee", "right_knee", "left_thigh", "right_thigh" })
        {
            if (source.Contains(region, StringComparison.Ordinal))
            {
                return region[(region.IndexOf('_') + 1)..];
            }
        }

        foreach (var region in new[] { "chest", "pelvis" })
        {
            if (source.Contains($"_{region}_", StringComparison.Ordinal))
            {
                return region;
            }
        }

        return receptorField switch
        {
            "hand" => "hand",
            "foot" => "foot",
            "distal_limb" when descriptor.BodyPositionY >= 0f => "forearm",
            "distal_limb" => "shin",
            _ => receptorField
        };
    }

    private static string ResolveContactNormalSector(SomaticContactDescriptor descriptor)
    {
        var x = MathF.Abs(descriptor.SurfaceNormalX);
        var y = MathF.Abs(descriptor.SurfaceNormalY);
        var z = MathF.Abs(descriptor.SurfaceNormalZ);
        if (x >= y && x >= z)
        {
            return descriptor.SurfaceNormalX < 0f ? "normal_x_neg" : "normal_x_pos";
        }

        if (y >= z)
        {
            return descriptor.SurfaceNormalY < 0f ? "normal_y_neg" : "normal_y_pos";
        }

        return descriptor.SurfaceNormalZ < 0f ? "normal_z_neg" : "normal_z_pos";
    }

    private static bool CanResolveWithdrawalDirection(
        string anatomicalRegion,
        SomaticContactDescriptor descriptor)
    {
        if (anatomicalRegion is not ("chest" or "pelvis"))
        {
            return true;
        }

        var horizontalNormal = MathF.Max(
            MathF.Abs(descriptor.SurfaceNormalX),
            MathF.Abs(descriptor.SurfaceNormalZ));
        return horizontalNormal > MathF.Abs(descriptor.SurfaceNormalY);
    }

    private static string ResolveWithdrawalField(
        SomaticContactDescriptor descriptor,
        string anatomicalRegion)
    {
        var side = descriptor.BodyPositionX switch
        {
            < -MidlineHalfWidthMeters => "left",
            > MidlineHalfWidthMeters => "right",
            _ when descriptor.InputSource.Contains("left_", StringComparison.OrdinalIgnoreCase) => "left",
            _ when descriptor.InputSource.Contains("right_", StringComparison.OrdinalIgnoreCase) => "right",
            _ => "midline"
        };
        return $"{side}:{anatomicalRegion}";
    }

    private static Guid CreateStableSynapseId(string hemisphere, string receptor, int sector, int fiber)
    {
        var key = Encoding.UTF8.GetBytes($"somatic:{hemisphere}:{receptor}:{sector}:{fiber}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(key, digest);
        return new Guid(digest[..16]);
    }

    private static Guid CreateStableWithdrawalSynapseId(
        string hemisphere,
        string anatomicalRegion,
        int sector,
        int fiber)
    {
        var key = Encoding.UTF8.GetBytes($"spinal-withdrawal:{hemisphere}:{anatomicalRegion}:{sector}:{fiber}");
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

    private sealed class WithdrawalFieldState
    {
        public double WindowTimestampMilliseconds { get; set; } = double.NaN;

        public float PreviousWindowPeakPressure { get; set; }

        public float WindowPeakPressure { get; set; }

        public bool HasPulse { get; set; }

        public double LastPulseTimestampMilliseconds { get; set; } = double.NaN;

        public float LastPulseThreatActivation { get; set; }
    }
}
