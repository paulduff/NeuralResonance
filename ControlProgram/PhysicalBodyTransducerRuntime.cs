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
    PhysicalArticulationFrame Articulation,
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

        var articulation = request.Articulation ?? PhysicalArticulationFrame.Neutral;
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
            request.HydrationFraction,
            articulation.LeftHipAngleRadians,
            articulation.RightHipAngleRadians,
            articulation.LeftKneeAngleRadians,
            articulation.RightKneeAngleRadians,
            articulation.LeftAnkleAngleRadians,
            articulation.RightAnkleAngleRadians,
            articulation.LeftFootLoadNewtons,
            articulation.RightFootLoadNewtons,
            articulation.LeftShoulderAngleRadians,
            articulation.RightShoulderAngleRadians,
            articulation.LeftElbowAngleRadians,
            articulation.RightElbowAngleRadians,
            articulation.LeftHandLoadNewtons,
            articulation.RightHandLoadNewtons,
            articulation.ManipulatorExtensionFraction,
            articulation.TrunkPitchRadians,
            articulation.TrunkRollRadians
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

        if (!ArticulationIsPhysical(articulation) || !MusculoskeletalIsPhysical(articulation.Musculoskeletal))
        {
            error = "One or more articulation measurements exceed the supported physical range.";
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
            articulation,
            AdminInputSource.Normalize(request.InputSource));
        return true;
    }

    private static bool WithinMagnitude(float value, float maximum)
        => MathF.Abs(value) <= maximum;

    private static bool ArticulationIsPhysical(PhysicalArticulationFrame value)
        => WithinMagnitude(value.LeftHipAngleRadians, 4f) &&
           WithinMagnitude(value.RightHipAngleRadians, 4f) &&
           WithinMagnitude(value.LeftKneeAngleRadians, 4f) &&
           WithinMagnitude(value.RightKneeAngleRadians, 4f) &&
           WithinMagnitude(value.LeftAnkleAngleRadians, 4f) &&
           WithinMagnitude(value.RightAnkleAngleRadians, 4f) &&
           value.LeftFootLoadNewtons is >= 0f and <= 5_000f &&
           value.RightFootLoadNewtons is >= 0f and <= 5_000f &&
           WithinMagnitude(value.LeftShoulderAngleRadians, 4f) &&
           WithinMagnitude(value.RightShoulderAngleRadians, 4f) &&
           WithinMagnitude(value.LeftElbowAngleRadians, 4f) &&
           WithinMagnitude(value.RightElbowAngleRadians, 4f) &&
           value.LeftHandLoadNewtons is >= 0f and <= 5_000f &&
           value.RightHandLoadNewtons is >= 0f and <= 5_000f &&
           value.ManipulatorExtensionFraction is >= 0f and <= 1f &&
           WithinMagnitude(value.TrunkPitchRadians, 2f) &&
           WithinMagnitude(value.TrunkRollRadians, 2f);

    private static bool MusculoskeletalIsPhysical(MusculoskeletalStateFrame? value)
    {
        if (value is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value.Posture) || value.Posture.Length > 32 ||
            !float.IsFinite(value.BodyHeightMeters) || value.BodyHeightMeters is < 0.15f or > 2.5f ||
            !float.IsFinite(value.UprightFraction) || value.UprightFraction is < 0f or > 1f ||
            !float.IsFinite(value.SupportFraction) || value.SupportFraction is < 0f or > 2f ||
            !float.IsFinite(value.BalanceError) || value.BalanceError is < 0f or > 1f ||
            value.Muscles is null || value.Muscles.Count > 64)
        {
            return false;
        }

        return BalanceIsPhysical(value.Balance) &&
            value.Muscles.All(static muscle =>
            !string.IsNullOrWhiteSpace(muscle.Name) && muscle.Name.Length <= 64 &&
            muscle.Side is "L" or "R" or "M" &&
            float.IsFinite(muscle.Activation) && muscle.Activation is >= 0f and <= 1f &&
            float.IsFinite(muscle.ForceNewtons) && muscle.ForceNewtons is >= 0f and <= 10_000f &&
            float.IsFinite(muscle.LengthFraction) && muscle.LengthFraction is >= 0.4f and <= 1.6f &&
            float.IsFinite(muscle.VelocityPerSecond) && MathF.Abs(muscle.VelocityPerSecond) <= 50f &&
            float.IsFinite(muscle.FatigueFraction) && muscle.FatigueFraction is >= 0f and <= 1f);
    }

    private static bool BalanceIsPhysical(PhysicalBalanceStateFrame? value)
    {
        if (value is null)
        {
            return true;
        }

        ReadOnlySpan<float> measurements =
        [
            value.CenterOfMassXMeters,
            value.CenterOfMassYMeters,
            value.CenterOfMassZMeters,
            value.CenterOfMassVelocityXMetersPerSecond,
            value.CenterOfMassVelocityZMetersPerSecond,
            value.ExtrapolatedCenterOfMassXMeters,
            value.ExtrapolatedCenterOfMassZMeters,
            value.CenterOfPressureXMeters,
            value.CenterOfPressureZMeters,
            value.SupportAreaSquareMeters,
            value.SupportMarginMeters,
            value.FallPitchRadians,
            value.FallRollRadians,
            value.FallPitchVelocityRadiansPerSecond,
            value.FallRollVelocityRadiansPerSecond
        ];

        foreach (var measurement in measurements)
        {
            if (!float.IsFinite(measurement))
            {
                return false;
            }
        }

        return WithinMagnitude(value.CenterOfMassXMeters, 5f) &&
               value.CenterOfMassYMeters is >= -0.5f and <= 3f &&
               WithinMagnitude(value.CenterOfMassZMeters, 5f) &&
               WithinMagnitude(value.CenterOfMassVelocityXMetersPerSecond, 50f) &&
               WithinMagnitude(value.CenterOfMassVelocityZMetersPerSecond, 50f) &&
               WithinMagnitude(value.ExtrapolatedCenterOfMassXMeters, 20f) &&
               WithinMagnitude(value.ExtrapolatedCenterOfMassZMeters, 20f) &&
               WithinMagnitude(value.CenterOfPressureXMeters, 5f) &&
               WithinMagnitude(value.CenterOfPressureZMeters, 5f) &&
               value.SupportAreaSquareMeters is >= 0f and <= 10f &&
               WithinMagnitude(value.SupportMarginMeters, 20f) &&
               WithinMagnitude(value.FallPitchRadians, 4f) &&
               WithinMagnitude(value.FallRollRadians, 4f) &&
               WithinMagnitude(value.FallPitchVelocityRadiansPerSecond, 50f) &&
               WithinMagnitude(value.FallRollVelocityRadiansPerSecond, 50f) &&
               !string.IsNullOrWhiteSpace(value.Phase) &&
               value.Phase.Length <= 32;
    }
}

internal sealed record PhysicalBodyTransduction(
    IReadOnlyList<SpikeMessage> ProprioceptiveLeft,
    IReadOnlyList<SpikeMessage> ProprioceptiveRight,
    IReadOnlyList<SpikeMessage> VestibularLeft,
    IReadOnlyList<SpikeMessage> VestibularRight,
    IReadOnlyList<SpikeMessage> VisceralLeft,
    IReadOnlyList<SpikeMessage> VisceralRight,
    IReadOnlyList<SpikeMessage> HabenularTeaching,
    IReadOnlyList<SpikeMessage> VtaTeaching,
    IReadOnlyList<SpikeMessage> SncTeaching,
    float LinearAccelerationMagnitude,
    float AngularSpeedMagnitude,
    float MotionMagnitude,
    float StoredEnergyReserve,
    float TissueIntegrity,
    float HomeostaticDeviation,
    float HomeostaticChange,
    float PositiveTeachingSignal,
    float NegativeTeachingSignal,
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
            StructureId.Habenula => (HabenularTeaching, HabenularTeaching),
            StructureId.Vta => (VtaTeaching, VtaTeaching),
            StructureId.Snc => (SncTeaching, SncTeaching),
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
        var balance = descriptor.Articulation.Musculoskeletal?.Balance ?? PhysicalBalanceStateFrame.Neutral;
        var centerOfMassOffsetX = balance.CenterOfMassXMeters - balance.CenterOfPressureXMeters;
        var centerOfMassOffsetZ = balance.CenterOfMassZMeters - balance.CenterOfPressureZMeters;
        var supportMarginLoss = Math.Clamp(-balance.SupportMarginMeters / 0.24f, 0f, 1f);
        var narrowSupport = Math.Clamp(1f - (balance.SupportAreaSquareMeters / 0.14f), 0f, 1f);

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
                ("dynamic_spindle_acceleration", Math.Clamp(linearAccelerationMagnitude / 18f, 0f, 1f)),
                ("axial_pitch_flexor_spindle", Positive(descriptor.Articulation.TrunkPitchRadians, 0.6f)),
                ("axial_pitch_extensor_spindle", Negative(descriptor.Articulation.TrunkPitchRadians, 0.6f)),
                ("axial_roll_spindle", MagnitudeActivation(descriptor.Articulation.TrunkRollRadians, 0.6f)),
                ("bilateral_reach_extension_spindle", descriptor.Articulation.ManipulatorExtensionFraction),
                ("center_of_mass_left_of_pressure", Negative(centerOfMassOffsetX, 0.35f)),
                ("center_of_mass_right_of_pressure", Positive(centerOfMassOffsetX, 0.35f)),
                ("center_of_mass_behind_pressure", Negative(centerOfMassOffsetZ, 0.45f)),
                ("center_of_mass_ahead_of_pressure", Positive(centerOfMassOffsetZ, 0.45f)),
                ("support_margin_loss", supportMarginLoss),
                ("support_area_narrowing", narrowSupport)
            ]);
        var proprioceptiveLeft = proprioceptive.Left.ToList();
        var proprioceptiveRight = proprioceptive.Right.ToList();
        var activeProprioceptivePopulations = proprioceptive.ActivePopulations;
        activeProprioceptivePopulations += AddLimbPopulations(
            proprioceptiveLeft,
            "L",
            "right",
            descriptor.Articulation.RightHipAngleRadians,
            descriptor.Articulation.RightKneeAngleRadians,
            descriptor.Articulation.RightAnkleAngleRadians,
            descriptor.Articulation.RightFootLoadNewtons,
            descriptor.Articulation.RightShoulderAngleRadians,
            descriptor.Articulation.RightElbowAngleRadians,
            descriptor.Articulation.RightHandLoadNewtons,
            previous?.Articulation.RightHipAngleRadians,
            deltaSeconds,
            descriptor,
            tick,
            timestampMs);
        activeProprioceptivePopulations += AddMusclePopulations(
            proprioceptiveLeft,
            proprioceptiveRight,
            descriptor,
            tick,
            timestampMs);
        activeProprioceptivePopulations += AddLimbPopulations(
            proprioceptiveRight,
            "R",
            "left",
            descriptor.Articulation.LeftHipAngleRadians,
            descriptor.Articulation.LeftKneeAngleRadians,
            descriptor.Articulation.LeftAnkleAngleRadians,
            descriptor.Articulation.LeftFootLoadNewtons,
            descriptor.Articulation.LeftShoulderAngleRadians,
            descriptor.Articulation.LeftElbowAngleRadians,
            descriptor.Articulation.LeftHandLoadNewtons,
            previous?.Articulation.LeftHipAngleRadians,
            deltaSeconds,
            descriptor,
            tick,
            timestampMs);

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
                ("posterior_canal", MagnitudeActivation(descriptor.AngularVelocityZ, 6f)),
                ("otolith_pitch_forward", Positive(balance.FallPitchRadians, 0.85f)),
                ("otolith_pitch_backward", Negative(balance.FallPitchRadians, 0.85f)),
                ("otolith_roll_left", Negative(balance.FallRollRadians, 0.85f)),
                ("otolith_roll_right", Positive(balance.FallRollRadians, 0.85f)),
                ("dynamic_balance_margin_loss", supportMarginLoss)
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
        var previousEnergyReserve = previous is null
            ? energyReserve
            : Math.Clamp(previous.StoredEnergyJoules / NominalStoredEnergyJoules, 0f, 1f);
        var previousDeviation = previous is null
            ? homeostaticDeviation
            : ComputeHomeostaticDeviation(previous);
        var energyChange = energyReserve - previousEnergyReserve;
        var tissueChange = descriptor.TissueIntegrityFraction - (previous?.TissueIntegrityFraction ?? descriptor.TissueIntegrityFraction);
        var hydrationChange = descriptor.HydrationFraction - (previous?.HydrationFraction ?? descriptor.HydrationFraction);
        var homeostaticChange = previousDeviation - homeostaticDeviation;

        // A reset restores a newly instantiated body; it is not an earned appetitive
        // outcome and must not reinforce the action that preceded death.
        var respawnTransition = previous is not null &&
            previous.TissueIntegrityFraction <= 0.05f &&
            descriptor.TissueIntegrityFraction >= 0.90f;
        var positiveTeaching = respawnTransition
            ? 0f
            : Math.Clamp(
                (Math.Max(0f, energyChange) * 0.42f) +
                (Math.Max(0f, tissueChange) * 0.38f) +
                (Math.Max(0f, hydrationChange) * 0.20f) +
                (Math.Max(0f, homeostaticChange) * 0.35f),
                0f,
                1f);
        var negativeTeaching = Math.Clamp(
            (Math.Max(0f, -energyChange) * 0.12f) +
            (Math.Max(0f, -tissueChange) * 0.78f) +
            (Math.Max(0f, -hydrationChange) * 0.10f) +
            (Math.Max(0f, -homeostaticChange) * 0.30f) +
            (descriptor.TissueIntegrityFraction <= 0.05f ? 0.85f : 0f),
            0f,
            1f);
        var habenularTeaching = BuildTeachingPopulation(
            StructureId.Habenula,
            "aversive_homeostatic_error",
            negativeTeaching,
            descriptor,
            tick,
            timestampMs);
        var vtaTeaching = BuildTeachingPopulation(
            StructureId.Vta,
            "appetitive_homeostatic_improvement",
            positiveTeaching,
            descriptor,
            tick,
            timestampMs);
        var sncTeaching = BuildTeachingPopulation(
            StructureId.Snc,
            "sensorimotor_homeostatic_improvement",
            positiveTeaching,
            descriptor,
            tick,
            timestampMs);
        var motionMagnitude = Math.Clamp(
            Magnitude(descriptor.LinearVelocityX, descriptor.LinearVelocityY, descriptor.LinearVelocityZ) / 4f,
            0f,
            1f);

        return new PhysicalBodyTransduction(
            proprioceptiveLeft,
            proprioceptiveRight,
            vestibular.Left,
            vestibular.Right,
            visceral.Left,
            visceral.Right,
            habenularTeaching,
            vtaTeaching,
            sncTeaching,
            linearAccelerationMagnitude,
            angularSpeedMagnitude,
            motionMagnitude,
            energyReserve,
            descriptor.TissueIntegrityFraction,
            homeostaticDeviation,
            homeostaticChange,
            positiveTeaching,
            negativeTeaching,
            activeProprioceptivePopulations,
            vestibular.ActivePopulations,
            visceral.ActivePopulations);
    }

    private static float ComputeHomeostaticDeviation(PhysicalBodyFrameDescriptor descriptor)
    {
        var energyDeficit = Math.Clamp(1f - (descriptor.StoredEnergyJoules / NominalStoredEnergyJoules), 0f, 1f);
        var tissueDamage = Math.Clamp(1f - descriptor.TissueIntegrityFraction, 0f, 1f);
        var thermalDeviation = Math.Max(
            Math.Clamp((36.8f - descriptor.CoreTemperatureCelsius) / 5f, 0f, 1f),
            Math.Clamp((descriptor.CoreTemperatureCelsius - 37.2f) / 5f, 0f, 1f));
        var hypoxia = Math.Clamp((0.96f - descriptor.BloodOxygenSaturationFraction) / 0.30f, 0f, 1f);
        var dehydration = Math.Clamp(1f - descriptor.HydrationFraction, 0f, 1f);
        return Math.Max(Math.Max(energyDeficit, tissueDamage), Math.Max(thermalDeviation, Math.Max(hypoxia, dehydration)));
    }

    private static IReadOnlyList<SpikeMessage> BuildTeachingPopulation(
        StructureId target,
        string receptor,
        float activation,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        if (activation < MinimumActivation)
        {
            return [];
        }

        var output = new List<SpikeMessage>(MaximumFibersPerPopulation);
        AddPopulation(output, "M", target, receptor, activation, descriptor, tick, timestampMs);
        return output;
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

    private static int AddLimbPopulations(
        List<SpikeMessage> output,
        string cerebralHemisphere,
        string bodySide,
        float hipAngle,
        float kneeAngle,
        float ankleAngle,
        float footLoad,
        float shoulderAngle,
        float elbowAngle,
        float handLoad,
        float? previousHipAngle,
        float deltaSeconds,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        var hipVelocity = previousHipAngle.HasValue && deltaSeconds > 0f
            ? MathF.Abs(hipAngle - previousHipAngle.Value) / deltaSeconds
            : 0f;
        IReadOnlyList<(string Receptor, float Activation)> populations =
        [
            ($"{bodySide}_hip_flexor_spindle", Positive(hipAngle, 1.2f)),
            ($"{bodySide}_hip_extensor_spindle", Negative(hipAngle, 1.2f)),
            ($"{bodySide}_knee_flexor_spindle", Positive(kneeAngle, 1.8f)),
            ($"{bodySide}_ankle_dorsiflexor_spindle", Positive(ankleAngle, 1.0f)),
            ($"{bodySide}_ankle_plantarflexor_spindle", Negative(ankleAngle, 1.0f)),
            ($"{bodySide}_hip_dynamic_spindle", Math.Clamp(hipVelocity / 6f, 0f, 1f)),
            ($"{bodySide}_foot_golgi_load", Math.Clamp(footLoad / 1_000f, 0f, 1f)),
            ($"{bodySide}_shoulder_flexor_spindle", Positive(shoulderAngle, 1.3f)),
            ($"{bodySide}_elbow_flexor_spindle", Positive(elbowAngle, 1.6f)),
            ($"{bodySide}_hand_golgi_load", Math.Clamp(handLoad / 300f, 0f, 1f))
        ];

        var active = 0;
        foreach (var (receptor, activation) in populations)
        {
            if (activation < MinimumActivation)
            {
                continue;
            }

            active++;
            AddPopulation(
                output,
                cerebralHemisphere,
                StructureId.ProprioceptiveAfferents,
                receptor,
                activation,
                descriptor,
                tick,
                timestampMs);
        }

        return active;
    }

    private static int AddMusclePopulations(
        List<SpikeMessage> cerebralLeft,
        List<SpikeMessage> cerebralRight,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        var frame = descriptor.Articulation.Musculoskeletal;
        if (frame?.Muscles is null || frame.Muscles.Count == 0)
        {
            return 0;
        }

        var active = 0;
        foreach (var muscle in frame.Muscles)
        {
            var side = muscle.Side.ToLowerInvariant();
            var name = new string(muscle.Name
                .Where(static character => char.IsLetterOrDigit(character) || character == '_')
                .Select(static character => char.ToLowerInvariant(character))
                .ToArray());
            if (name.Length == 0)
            {
                continue;
            }

            IReadOnlyList<(string Receptor, float Activation)> receptors =
            [
                ($"{side}_{name}_primary_spindle", Math.Clamp(MathF.Abs(muscle.LengthFraction - 1f) / 0.30f, 0f, 1f)),
                ($"{side}_{name}_dynamic_spindle", Math.Clamp(MathF.Abs(muscle.VelocityPerSecond) / 4f, 0f, 1f)),
                ($"{side}_{name}_golgi_tendon", Math.Clamp(muscle.ForceNewtons / 3_500f, 0f, 1f))
            ];
            foreach (var receptor in receptors)
            {
                if (receptor.Activation < MinimumActivation)
                {
                    continue;
                }

                active++;
                if (muscle.Side is "R" or "M")
                {
                    AddPopulation(cerebralLeft, "L", StructureId.ProprioceptiveAfferents,
                        receptor.Receptor, receptor.Activation, descriptor, tick, timestampMs);
                }
                if (muscle.Side is "L" or "M")
                {
                    AddPopulation(cerebralRight, "R", StructureId.ProprioceptiveAfferents,
                        receptor.Receptor, receptor.Activation, descriptor, tick, timestampMs);
                }
            }
        }

        return active;
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
