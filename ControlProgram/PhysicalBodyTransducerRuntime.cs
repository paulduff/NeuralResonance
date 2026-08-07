using System.Security.Cryptography;
using System.Text;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record PhysicalBodyFrameDescriptor(
    long Sequence,
    long TimestampMs,
    float LinearVelocityX,
    float LinearVelocityY,
    float LinearVelocityZ,
    float AngularVelocityX,
    float AngularVelocityY,
    float AngularVelocityZ,
    float StoredEnergyJoules,
    float TissueIntegrityFraction,
    float CoreTemperatureCelsius,
    float BloodOxygenSaturationFraction,
    float HydrationFraction,
    string InputSource)
{
    public static bool TryCreate(
        PhysicalBodyFrameRequest? request,
        out PhysicalBodyFrameDescriptor? descriptor,
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
            request.LinearVelocityXMetersPerSecond,
            request.LinearVelocityYMetersPerSecond,
            request.LinearVelocityZMetersPerSecond,
            request.AngularVelocityXRadiansPerSecond,
            request.AngularVelocityYRadiansPerSecond,
            request.AngularVelocityZRadiansPerSecond,
            request.StoredEnergyJoules,
            request.TissueIntegrityFraction,
            request.CoreTemperatureCelsius,
            request.BloodOxygenSaturationFraction,
            request.HydrationFraction
        ];
        for (var i = 0; i < values.Length; i++)
        {
            if (!float.IsFinite(values[i]))
            {
                error = "All physical body measurements must be finite numbers.";
                return false;
            }
        }

        if (!WithinMagnitude(request.LinearVelocityXMetersPerSecond, 100f) ||
            !WithinMagnitude(request.LinearVelocityYMetersPerSecond, 100f) ||
            !WithinMagnitude(request.LinearVelocityZMetersPerSecond, 100f) ||
            !WithinMagnitude(request.AngularVelocityXRadiansPerSecond, 50f) ||
            !WithinMagnitude(request.AngularVelocityYRadiansPerSecond, 50f) ||
            !WithinMagnitude(request.AngularVelocityZRadiansPerSecond, 50f))
        {
            error = "Body-local velocity measurements exceed the supported physical range.";
            return false;
        }

        if (request.StoredEnergyJoules is < 0f or > 100_000_000f ||
            request.TissueIntegrityFraction is < 0f or > 1f ||
            request.CoreTemperatureCelsius is < 20f or > 45f ||
            request.BloodOxygenSaturationFraction is < 0f or > 1f ||
            request.HydrationFraction is < 0f or > 1f)
        {
            error = "One or more physiological measurements exceed the supported physical range.";
            return false;
        }

        descriptor = new PhysicalBodyFrameDescriptor(
            request.Sequence,
            request.TimestampMs,
            request.LinearVelocityXMetersPerSecond,
            request.LinearVelocityYMetersPerSecond,
            request.LinearVelocityZMetersPerSecond,
            request.AngularVelocityXRadiansPerSecond,
            request.AngularVelocityYRadiansPerSecond,
            request.AngularVelocityZRadiansPerSecond,
            request.StoredEnergyJoules,
            request.TissueIntegrityFraction,
            request.CoreTemperatureCelsius,
            request.BloodOxygenSaturationFraction,
            request.HydrationFraction,
            AdminInputSource.Normalize(request.InputSource));
        return true;
    }

    private static bool WithinMagnitude(float value, float maximum)
        => MathF.Abs(value) <= maximum;
}

internal sealed record PhysicalBodyTransduction(
    IReadOnlyList<SpikeMessage> ProprioceptiveLeft,
    IReadOnlyList<SpikeMessage> ProprioceptiveRight,
    IReadOnlyList<SpikeMessage> VestibularLeft,
    IReadOnlyList<SpikeMessage> VestibularRight,
    IReadOnlyList<SpikeMessage> VisceralLeft,
    IReadOnlyList<SpikeMessage> VisceralRight,
    float LinearAccelerationMagnitude,
    float AngularSpeedMagnitude,
    float StoredEnergyReserve,
    float TissueIntegrity,
    float HomeostaticDeviation,
    int ActiveProprioceptivePopulations,
    int ActiveVestibularPopulations,
    int ActiveVisceralPopulations)
{
    public IReadOnlyList<SpikeMessage> For(StructureId structure, string? hemisphere)
    {
        var (left, right) = structure switch
        {
            StructureId.ProprioceptiveAfferents => (ProprioceptiveLeft, ProprioceptiveRight),
            StructureId.VestibularAfferents => (VestibularLeft, VestibularRight),
            StructureId.VisceralAfferents => (VisceralLeft, VisceralRight),
            _ => (Array.Empty<SpikeMessage>(), Array.Empty<SpikeMessage>())
        };

        if (string.Equals(hemisphere, "L", StringComparison.OrdinalIgnoreCase))
        {
            return left;
        }
        if (string.Equals(hemisphere, "R", StringComparison.OrdinalIgnoreCase))
        {
            return right;
        }
        if (left.Count == 0)
        {
            return right;
        }
        if (right.Count == 0)
        {
            return left;
        }

        var combined = new List<SpikeMessage>(left.Count + right.Count);
        combined.AddRange(left);
        combined.AddRange(right);
        return combined;
    }
}

internal sealed class PhysicalBodyTransducerRuntime
{
    private const float MinimumActivation = 0.035f;
    private const int MaximumFibersPerPopulation = 5;
    private const float NominalStoredEnergyJoules = 8_000_000f;
    private readonly object _gate = new();
    private readonly Dictionary<string, PhysicalBodyFrameDescriptor> _previousBySource =
        new(StringComparer.OrdinalIgnoreCase);

    public PhysicalBodyTransduction Transduce(
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var previous = ExchangePrevious(descriptor);
        var deltaSeconds = previous is null
            ? 0f
            : Math.Clamp((descriptor.TimestampMs - previous.TimestampMs) / 1000f, 0.001f, 5f);

        var ax = deltaSeconds > 0f ? (descriptor.LinearVelocityX - previous!.LinearVelocityX) / deltaSeconds : 0f;
        var ay = deltaSeconds > 0f ? (descriptor.LinearVelocityY - previous!.LinearVelocityY) / deltaSeconds : 0f;
        var az = deltaSeconds > 0f ? (descriptor.LinearVelocityZ - previous!.LinearVelocityZ) / deltaSeconds : 0f;
        var linearAccelerationMagnitude = Magnitude(ax, ay, az);
        var angularSpeedMagnitude = Magnitude(
            descriptor.AngularVelocityX,
            descriptor.AngularVelocityY,
            descriptor.AngularVelocityZ);

        var proprioceptive = BuildBilateral(
            StructureId.ProprioceptiveAfferents,
            descriptor,
            tick,
            timestampMs,
            [
                ("spindle_sway_positive", Positive(descriptor.LinearVelocityX, 8f)),
                ("spindle_sway_negative", Negative(descriptor.LinearVelocityX, 8f)),
                ("spindle_vertical_positive", Positive(descriptor.LinearVelocityY, 8f)),
                ("spindle_vertical_negative", Negative(descriptor.LinearVelocityY, 8f)),
                ("spindle_forward_positive", Positive(descriptor.LinearVelocityZ, 12f)),
                ("spindle_forward_negative", Negative(descriptor.LinearVelocityZ, 12f)),
                ("dynamic_spindle_acceleration", Math.Clamp(linearAccelerationMagnitude / 18f, 0f, 1f))
            ]);

        var vestibular = BuildBilateral(
            StructureId.VestibularAfferents,
            descriptor,
            tick,
            timestampMs,
            [
                ("utricle_left", Negative(ax, 12f)),
                ("utricle_right", Positive(ax, 12f)),
                ("saccule_down", Negative(ay, 12f)),
                ("saccule_up", Positive(ay, 12f)),
                ("utricle_backward", Negative(az, 12f)),
                ("utricle_forward", Positive(az, 12f)),
                ("anterior_canal", MagnitudeActivation(descriptor.AngularVelocityX, 6f)),
                ("horizontal_canal", MagnitudeActivation(descriptor.AngularVelocityY, 6f)),
                ("posterior_canal", MagnitudeActivation(descriptor.AngularVelocityZ, 6f))
            ]);

        var energyReserve = Math.Clamp(descriptor.StoredEnergyJoules / NominalStoredEnergyJoules, 0f, 1f);
        var thermalCold = Math.Clamp((36.8f - descriptor.CoreTemperatureCelsius) / 5f, 0f, 1f);
        var thermalWarm = Math.Clamp((descriptor.CoreTemperatureCelsius - 37.2f) / 5f, 0f, 1f);
        var hypoxia = Math.Clamp((0.96f - descriptor.BloodOxygenSaturationFraction) / 0.30f, 0f, 1f);
        var dehydration = Math.Clamp(1f - descriptor.HydrationFraction, 0f, 1f);
        var tissueDamage = Math.Clamp(1f - descriptor.TissueIntegrityFraction, 0f, 1f);
        var energyDeficit = Math.Clamp(1f - energyReserve, 0f, 1f);
        var visceral = BuildBilateral(
            StructureId.VisceralAfferents,
            descriptor,
            tick,
            timestampMs,
            [
                ("glucose_energy_deficit_chemoreceptor", energyDeficit),
                ("tissue_damage_chemoreceptor", tissueDamage),
                ("core_cold_thermoreceptor", thermalCold),
                ("core_warm_thermoreceptor", thermalWarm),
                ("carotid_hypoxia_chemoreceptor", hypoxia),
                ("osmotic_dehydration_receptor", dehydration)
            ]);
        var homeostaticDeviation = Math.Max(
            Math.Max(energyDeficit, tissueDamage),
            Math.Max(Math.Max(thermalCold, thermalWarm), Math.Max(hypoxia, dehydration)));

        return new PhysicalBodyTransduction(
            proprioceptive.Left,
            proprioceptive.Right,
            vestibular.Left,
            vestibular.Right,
            visceral.Left,
            visceral.Right,
            linearAccelerationMagnitude,
            angularSpeedMagnitude,
            energyReserve,
            descriptor.TissueIntegrityFraction,
            homeostaticDeviation,
            proprioceptive.ActivePopulations,
            vestibular.ActivePopulations,
            visceral.ActivePopulations);
    }

    private PhysicalBodyFrameDescriptor? ExchangePrevious(PhysicalBodyFrameDescriptor current)
    {
        lock (_gate)
        {
            _previousBySource.TryGetValue(current.InputSource, out var previous);
            _previousBySource[current.InputSource] = current;
            return previous;
        }
    }

    private static BilateralSpikes BuildBilateral(
        StructureId structure,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs,
        IReadOnlyList<(string Receptor, float Activation)> populations)
    {
        var left = new List<SpikeMessage>(populations.Count * 3);
        var right = new List<SpikeMessage>(populations.Count * 3);
        var active = 0;
        foreach (var (receptor, activation) in populations)
        {
            if (activation < MinimumActivation)
            {
                continue;
            }

            active++;
            AddPopulation(left, "L", structure, receptor, activation, descriptor, tick, timestampMs);
            AddPopulation(right, "R", structure, receptor, activation, descriptor, tick, timestampMs);
        }
        return new BilateralSpikes(left, right, active);
    }

    private static void AddPopulation(
        List<SpikeMessage> output,
        string hemisphere,
        StructureId structure,
        string receptor,
        float activation,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
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
                SourceStructure = structure,
                TargetStructure = structure,
                SourceNeuronId = $"{hemisphere}:{receptor}",
                TargetNeuronId = $"{hemisphere}:primary_afferent:{receptor}:fiber_{fiber}",
                SynapseId = CreateStableSynapseId(structure, hemisphere, receptor, fiber),
                Neurotransmitter = NTEnum.GLUTAMATE,
                VesicleQuanta = Math.Clamp(0.20f + (activation * 4.7f), 0.05f, 6f),
                ReuptakeRate = Math.Clamp(6.4f - (activation * 3.2f), 1.8f, 8f),
                SpikeType = activation >= 0.68f ||
                            (activation >= 0.42f && ((tick + descriptor.Sequence + fiber) & 1) == 0)
                    ? SpikeTypeEnum.BURST
                    : SpikeTypeEnum.ACTION_POTENTIAL,
                IsFeedback = false,
                ModulationContext = null
            });
        }
    }

    private static float Positive(float value, float scale) => Math.Clamp(value / scale, 0f, 1f);
    private static float Negative(float value, float scale) => Math.Clamp(-value / scale, 0f, 1f);
    private static float MagnitudeActivation(float value, float scale) => Math.Clamp(MathF.Abs(value) / scale, 0f, 1f);
    private static float Magnitude(float x, float y, float z) => MathF.Sqrt((x * x) + (y * y) + (z * z));

    private static Guid CreateStableSynapseId(StructureId structure, string hemisphere, string receptor, int fiber)
    {
        var key = Encoding.UTF8.GetBytes($"body-afferent:{structure}:{hemisphere}:{receptor}:{fiber}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(key, digest);
        return new Guid(digest[..16]);
    }

    private sealed record BilateralSpikes(
        IReadOnlyList<SpikeMessage> Left,
        IReadOnlyList<SpikeMessage> Right,
        int ActivePopulations);
}
