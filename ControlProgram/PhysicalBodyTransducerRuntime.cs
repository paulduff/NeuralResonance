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
    string InputSource,
    bool MotorTrainingMode,
    int SaturatedMuscleVelocityCount)
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
        articulation = SaturateMuscleVelocityDerivatives(articulation, out var saturatedMuscleVelocityCount);
        (string Name, float Value)[] values =
        [
            ("linearVelocityX", request.LinearVelocityXMetersPerSecond),
            ("linearVelocityY", request.LinearVelocityYMetersPerSecond),
            ("linearVelocityZ", request.LinearVelocityZMetersPerSecond),
            ("angularVelocityX", request.AngularVelocityXRadiansPerSecond),
            ("angularVelocityY", request.AngularVelocityYRadiansPerSecond),
            ("angularVelocityZ", request.AngularVelocityZRadiansPerSecond),
            ("storedEnergyJoules", request.StoredEnergyJoules),
            ("tissueIntegrityFraction", request.TissueIntegrityFraction),
            ("coreTemperatureCelsius", request.CoreTemperatureCelsius),
            ("bloodOxygenSaturationFraction", request.BloodOxygenSaturationFraction),
            ("hydrationFraction", request.HydrationFraction)
        ];
        foreach (var measurement in values)
        {
            if (!float.IsFinite(measurement.Value))
            {
                error = $"Physical body measurement '{measurement.Name}' must be finite.";
                return false;
            }
        }

        if (!TryValidateMagnitude("linearVelocityX", request.LinearVelocityXMetersPerSecond, 100f, out error) ||
            !TryValidateMagnitude("linearVelocityY", request.LinearVelocityYMetersPerSecond, 100f, out error) ||
            !TryValidateMagnitude("linearVelocityZ", request.LinearVelocityZMetersPerSecond, 100f, out error) ||
            !TryValidateMagnitude("angularVelocityX", request.AngularVelocityXRadiansPerSecond, 50f, out error) ||
            !TryValidateMagnitude("angularVelocityY", request.AngularVelocityYRadiansPerSecond, 50f, out error) ||
            !TryValidateMagnitude("angularVelocityZ", request.AngularVelocityZRadiansPerSecond, 50f, out error))
        {
            return false;
        }

        if (!TryValidateRange("storedEnergyJoules", request.StoredEnergyJoules, 0f, 100_000_000f, out error) ||
            !TryValidateRange("tissueIntegrityFraction", request.TissueIntegrityFraction, 0f, 1f, out error) ||
            !TryValidateRange("coreTemperatureCelsius", request.CoreTemperatureCelsius, 20f, 45f, out error) ||
            !TryValidateRange(
                "bloodOxygenSaturationFraction",
                request.BloodOxygenSaturationFraction,
                0f,
                1f,
                out error) ||
            !TryValidateRange("hydrationFraction", request.HydrationFraction, 0f, 1f, out error))
        {
            error = $"Physiological {error}";
            return false;
        }

        if (!TryValidateArticulation(articulation, out error) ||
            !TryValidateMusculoskeletal(articulation.Musculoskeletal, out error))
        {
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
            AdminInputSource.Normalize(request.InputSource),
            request.MotorTrainingMode,
            saturatedMuscleVelocityCount);
        return true;
    }

    private static PhysicalArticulationFrame SaturateMuscleVelocityDerivatives(
        PhysicalArticulationFrame articulation,
        out int saturationCount)
    {
        saturationCount = 0;
        var musculoskeletal = articulation.Musculoskeletal;
        if (musculoskeletal?.Muscles is null || musculoskeletal.Muscles.Count == 0)
        {
            return articulation;
        }

        List<PhysicalMuscleMeasurement>? normalized = null;
        for (var index = 0; index < musculoskeletal.Muscles.Count; index++)
        {
            var muscle = musculoskeletal.Muscles[index];
            if (!float.IsFinite(muscle.VelocityPerSecond) ||
                muscle.VelocityPerSecond is >= -50f and <= 50f)
            {
                normalized?.Add(muscle);
                continue;
            }

            normalized ??= musculoskeletal.Muscles.Take(index).ToList();
            normalized.Add(muscle with
            {
                VelocityPerSecond = Math.Clamp(muscle.VelocityPerSecond, -50f, 50f)
            });
            saturationCount++;
        }

        return normalized is null
            ? articulation
            : articulation with
            {
                Musculoskeletal = musculoskeletal with { Muscles = normalized }
            };
    }

    private static bool TryValidateArticulation(PhysicalArticulationFrame value, out string? error)
    {
        (string Name, float Value, float Minimum, float Maximum)[] measurements =
        [
            ("leftHipAngleRadians", value.LeftHipAngleRadians, -4f, 4f),
            ("rightHipAngleRadians", value.RightHipAngleRadians, -4f, 4f),
            ("leftHipAbductionRadians", value.LeftHipAbductionRadians, -4f, 4f),
            ("rightHipAbductionRadians", value.RightHipAbductionRadians, -4f, 4f),
            ("leftKneeAngleRadians", value.LeftKneeAngleRadians, -4f, 4f),
            ("rightKneeAngleRadians", value.RightKneeAngleRadians, -4f, 4f),
            ("leftAnkleAngleRadians", value.LeftAnkleAngleRadians, -4f, 4f),
            ("rightAnkleAngleRadians", value.RightAnkleAngleRadians, -4f, 4f),
            ("leftAnkleRollRadians", value.LeftAnkleRollRadians, -4f, 4f),
            ("rightAnkleRollRadians", value.RightAnkleRollRadians, -4f, 4f),
            ("leftFootLoadNewtons", value.LeftFootLoadNewtons, 0f, 5_000f),
            ("rightFootLoadNewtons", value.RightFootLoadNewtons, 0f, 5_000f),
            ("leftShoulderAngleRadians", value.LeftShoulderAngleRadians, -4f, 4f),
            ("rightShoulderAngleRadians", value.RightShoulderAngleRadians, -4f, 4f),
            ("leftShoulderAbductionRadians", value.LeftShoulderAbductionRadians, -4f, 4f),
            ("rightShoulderAbductionRadians", value.RightShoulderAbductionRadians, -4f, 4f),
            ("leftElbowAngleRadians", value.LeftElbowAngleRadians, -4f, 4f),
            ("rightElbowAngleRadians", value.RightElbowAngleRadians, -4f, 4f),
            ("neckYawRadians", value.NeckYawRadians, -4f, 4f),
            ("neckPitchRadians", value.NeckPitchRadians, -4f, 4f),
            ("leftHandLoadNewtons", value.LeftHandLoadNewtons, 0f, 5_000f),
            ("rightHandLoadNewtons", value.RightHandLoadNewtons, 0f, 5_000f),
            ("leftHandApertureFraction", value.LeftHandApertureFraction, 0f, 1f),
            ("rightHandApertureFraction", value.RightHandApertureFraction, 0f, 1f),
            ("leftGripForceNewtons", value.LeftGripForceNewtons, 0f, 5_000f),
            ("rightGripForceNewtons", value.RightGripForceNewtons, 0f, 5_000f),
            ("leftHandFatigue", value.LeftHandFatigue, 0f, 1f),
            ("rightHandFatigue", value.RightHandFatigue, 0f, 1f),
            ("leftHandSlip", value.LeftHandSlip, 0f, 1f),
            ("rightHandSlip", value.RightHandSlip, 0f, 1f),
            ("manipulatorExtensionFraction", value.ManipulatorExtensionFraction, 0f, 1f),
            ("trunkPitchRadians", value.TrunkPitchRadians, -2f, 2f),
            ("trunkRollRadians", value.TrunkRollRadians, -2f, 2f),
            ("trunkYawRadians", value.TrunkYawRadians, -2f, 2f),
            ("supportPlaneOffsetMeters", value.SupportPlaneOffsetMeters, -1f, 1.5f)
        ];
        foreach (var measurement in measurements)
        {
            if (!float.IsFinite(measurement.Value))
            {
                error = $"Articulation measurement '{measurement.Name}' must be finite.";
                return false;
            }
            if (measurement.Value < measurement.Minimum || measurement.Value > measurement.Maximum)
            {
                error = $"Articulation measurement '{measurement.Name}' must be within " +
                    $"[{measurement.Minimum}, {measurement.Maximum}]; received {measurement.Value}.";
                return false;
            }
        }

        if (!TryValidateFootPressure("leftFootPressure", value.LeftFootPressure, out error) ||
            !TryValidateFootPressure("rightFootPressure", value.RightFootPressure, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateFootPressure(
        string name,
        PhysicalFootPressureFrame? value,
        out string? error)
    {
        if (value is null)
        {
            error = null;
            return true;
        }

        var measurements = new (string Field, float Value)[]
        {
            ("heelMedialLoadNewtons", value.HeelMedialLoadNewtons),
            ("heelLateralLoadNewtons", value.HeelLateralLoadNewtons),
            ("forefootMedialLoadNewtons", value.ForefootMedialLoadNewtons),
            ("forefootLateralLoadNewtons", value.ForefootLateralLoadNewtons)
        };
        foreach (var measurement in measurements)
        {
            if (!float.IsFinite(measurement.Value) || measurement.Value < 0f || measurement.Value > 5_000f)
            {
                error = $"Articulation measurement '{name}.{measurement.Field}' must be finite and within [0, 5000].";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateMusculoskeletal(MusculoskeletalStateFrame? value, out string? error)
    {
        if (value is null)
        {
            error = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(value.Posture) || value.Posture.Length > 32)
        {
            error = "Articulation musculoskeletal posture must contain 1 to 32 characters.";
            return false;
        }
        if (!TryValidateRange("bodyHeightMeters", value.BodyHeightMeters, 0.15f, 2.5f, out error) ||
            !TryValidateRange("uprightFraction", value.UprightFraction, 0f, 1f, out error) ||
            !TryValidateRange("supportFraction", value.SupportFraction, 0f, 2f, out error) ||
            !TryValidateRange("balanceError", value.BalanceError, 0f, 1f, out error))
        {
            error = $"Articulation musculoskeletal {error}";
            return false;
        }
        if (value.Muscles is null || value.Muscles.Count > 64)
        {
            error = "Articulation musculoskeletal muscles must contain between 0 and 64 measurements.";
            return false;
        }
        if (!TryValidateBalance(value.Balance, out error))
        {
            return false;
        }

        for (var index = 0; index < value.Muscles.Count; index++)
        {
            var muscle = value.Muscles[index];
            var label = string.IsNullOrWhiteSpace(muscle.Name) ? $"muscle[{index}]" : muscle.Name;
            if (string.IsNullOrWhiteSpace(muscle.Name) || muscle.Name.Length > 64)
            {
                error = $"Articulation muscle[{index}] name must contain 1 to 64 characters.";
                return false;
            }
            if (muscle.Side is not ("L" or "R" or "M"))
            {
                error = $"Articulation muscle '{label}' side must be L, R, or M.";
                return false;
            }
            if (!TryValidateRange($"muscle '{label}' activation", muscle.Activation, 0f, 1f, out error) ||
                !TryValidateRange($"muscle '{label}' forceNewtons", muscle.ForceNewtons, 0f, 10_000f, out error) ||
                !TryValidateRange($"muscle '{label}' lengthFraction", muscle.LengthFraction, 0.4f, 1.6f, out error) ||
                !TryValidateRange($"muscle '{label}' velocityPerSecond", muscle.VelocityPerSecond, -50f, 50f, out error) ||
                !TryValidateRange($"muscle '{label}' fatigueFraction", muscle.FatigueFraction, 0f, 1f, out error))
            {
                error = $"Articulation {error}";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateBalance(PhysicalBalanceStateFrame? value, out string? error)
    {
        if (value is null)
        {
            error = null;
            return true;
        }

        (string Name, float Value, float Minimum, float Maximum)[] measurements =
        [
            ("centerOfMassX", value.CenterOfMassXMeters, -5f, 5f),
            ("centerOfMassY", value.CenterOfMassYMeters, -0.5f, 3f),
            ("centerOfMassZ", value.CenterOfMassZMeters, -5f, 5f),
            ("centerOfMassVelocityX", value.CenterOfMassVelocityXMetersPerSecond, -50f, 50f),
            ("centerOfMassVelocityZ", value.CenterOfMassVelocityZMetersPerSecond, -50f, 50f),
            ("extrapolatedCenterOfMassX", value.ExtrapolatedCenterOfMassXMeters, -20f, 20f),
            ("extrapolatedCenterOfMassZ", value.ExtrapolatedCenterOfMassZMeters, -20f, 20f),
            ("centerOfPressureX", value.CenterOfPressureXMeters, -5f, 5f),
            ("centerOfPressureZ", value.CenterOfPressureZMeters, -5f, 5f),
            ("supportAreaSquareMeters", value.SupportAreaSquareMeters, 0f, 10f),
            ("supportMarginMeters", value.SupportMarginMeters, -20f, 20f),
            ("dynamicStabilityAllowanceMeters", value.DynamicStabilityAllowanceMeters, 0f, 1f),
            ("fallPitchRadians", value.FallPitchRadians, -4f, 4f),
            ("fallRollRadians", value.FallRollRadians, -4f, 4f),
            ("fallPitchVelocity", value.FallPitchVelocityRadiansPerSecond, -50f, 50f),
            ("fallRollVelocity", value.FallRollVelocityRadiansPerSecond, -50f, 50f)
        ];

        foreach (var measurement in measurements)
        {
            if (!float.IsFinite(measurement.Value) ||
                measurement.Value < measurement.Minimum ||
                measurement.Value > measurement.Maximum)
            {
                error = $"Articulation balance measurement '{measurement.Name}' must be within " +
                    $"[{measurement.Minimum}, {measurement.Maximum}]; received {measurement.Value}.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(value.Phase) || value.Phase.Length > 32)
        {
            error = "Articulation balance phase must contain 1 to 32 characters.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateMagnitude(string name, float value, float maximum, out string? error)
        => TryValidateRange(name, value, -maximum, maximum, out error);

    private static bool TryValidateRange(
        string name,
        float value,
        float minimum,
        float maximum,
        out string? error)
    {
        if (float.IsFinite(value) && value >= minimum && value <= maximum)
        {
            error = null;
            return true;
        }

        error = $"measurement '{name}' must be within [{minimum}, {maximum}]; received {value}.";
        return false;
    }
}

internal sealed record PhysicalBodyTransduction(
    IReadOnlyList<SpikeMessage> ProprioceptiveLeft,
    IReadOnlyList<SpikeMessage> ProprioceptiveRight,
    IReadOnlyList<SpikeMessage> SomaticLeft,
    IReadOnlyList<SpikeMessage> SomaticRight,
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
    float HungerDrive,
    float ThirstDrive,
    float EnergyRestorationTeachingSignal,
    float HydrationRestorationTeachingSignal,
    float PositiveTeachingSignal,
    float NegativeTeachingSignal,
    float SupportMarginImprovement,
    float BalanceImprovement,
    float IneffectiveForceEvidence,
    float PeakMuscleFatigueDistress,
    int ActiveProprioceptivePopulations,
    int ActiveSomaticPopulations,
    int ActiveVestibularPopulations,
    int ActiveVisceralPopulations,
    bool MotorTrainingMode,
    int SaturatedMuscleVelocityCount,
    float TissueChange,
    bool DeathTransition,
    bool RespawnTransition,
    bool HomeostaticCadenceDispatch,
    int HomeostaticCadenceMilliseconds)
{
    public IReadOnlyList<SpikeMessage> For(StructureId structure, string? hemisphere)
    {
        var (left, right) = structure switch
        {
            StructureId.ProprioceptiveAfferents => (ProprioceptiveLeft, ProprioceptiveRight),
            StructureId.SomaticAfferents => (SomaticLeft, SomaticRight),
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
    private const float PassiveBipedalSupportLoadNewtons = 900f;
    internal const int HomeostaticCadenceMilliseconds = 250;
    private readonly object _gate = new();
    private readonly Dictionary<string, PhysicalBodyFrameDescriptor> _previousBySource =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HomeostaticCadenceState> _homeostaticBySource =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly VestibularLabyrinthTransducer _vestibularLabyrinth = new();

    public PhysicalBodyTransduction Transduce(
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var previous = ExchangePrevious(descriptor);
        var elapsedSeconds = previous is null
            ? 0f
            : (descriptor.TimestampMs - previous.TimestampMs) / 1000f;
        var deltaSeconds = elapsedSeconds > 0f
            ? Math.Clamp(elapsedSeconds, 0.001f, 5f)
            : 0f;

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
        var effectiveSupportMargin = balance.SupportMarginMeters + balance.DynamicStabilityAllowanceMeters;
        var dynamicReserve = Math.Clamp(balance.DynamicStabilityAllowanceMeters / 0.075f, 0f, 1f);
        var supportMarginLoss = Math.Clamp(-effectiveSupportMargin / 0.24f, 0f, 1f);
        var narrowSupport = Math.Clamp(1f - (balance.SupportAreaSquareMeters / 0.14f), 0f, 1f) *
            (1f - dynamicReserve);

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
                ("axial_yaw_right_rotator_spindle", Positive(descriptor.Articulation.TrunkYawRadians, 0.61f)),
                ("axial_yaw_left_rotator_spindle", Negative(descriptor.Articulation.TrunkYawRadians, 0.61f)),
                ("bilateral_reach_extension_spindle", descriptor.Articulation.ManipulatorExtensionFraction),
                ("center_of_mass_left_of_pressure", Negative(centerOfMassOffsetX, 0.35f)),
                ("center_of_mass_right_of_pressure", Positive(centerOfMassOffsetX, 0.35f)),
                ("center_of_mass_behind_pressure", Negative(centerOfMassOffsetZ, 0.45f)),
                ("center_of_mass_ahead_of_pressure", Positive(centerOfMassOffsetZ, 0.45f)),
                ("support_margin_loss", supportMarginLoss),
                ("support_area_narrowing", narrowSupport),
                ("dynamic_stability_reserve", dynamicReserve)
            ]);
        var proprioceptiveLeft = proprioceptive.Left.ToList();
        var proprioceptiveRight = proprioceptive.Right.ToList();
        var activeProprioceptivePopulations = proprioceptive.ActivePopulations;
        activeProprioceptivePopulations += AddLimbPopulations(
            proprioceptiveLeft,
            "L",
            "right",
            descriptor.Articulation.RightHipAngleRadians,
            descriptor.Articulation.RightHipAbductionRadians,
            descriptor.Articulation.RightKneeAngleRadians,
            descriptor.Articulation.RightAnkleAngleRadians,
            descriptor.Articulation.RightAnkleRollRadians,
            descriptor.Articulation.RightFootLoadNewtons,
            descriptor.Articulation.RightFootPressure,
            descriptor.Articulation.RightShoulderAngleRadians,
            descriptor.Articulation.RightElbowAngleRadians,
            descriptor.Articulation.RightHandLoadNewtons,
            descriptor.Articulation.RightHandApertureFraction,
            descriptor.Articulation.RightGripForceNewtons,
            descriptor.Articulation.RightHandSlip,
            previous?.Articulation.RightHipAngleRadians,
            previous?.Articulation.RightHipAbductionRadians,
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
        var somaticLeft = new List<SpikeMessage>();
        var somaticRight = new List<SpikeMessage>();
        var activeSomaticPopulations = AddMuscleFatiguePopulations(
            somaticLeft,
            somaticRight,
            descriptor,
            tick,
            timestampMs,
            out var peakMuscleFatigueDistress);
        activeProprioceptivePopulations += AddLimbPopulations(
            proprioceptiveRight,
            "R",
            "left",
            descriptor.Articulation.LeftHipAngleRadians,
            descriptor.Articulation.LeftHipAbductionRadians,
            descriptor.Articulation.LeftKneeAngleRadians,
            descriptor.Articulation.LeftAnkleAngleRadians,
            descriptor.Articulation.LeftAnkleRollRadians,
            descriptor.Articulation.LeftFootLoadNewtons,
            descriptor.Articulation.LeftFootPressure,
            descriptor.Articulation.LeftShoulderAngleRadians,
            descriptor.Articulation.LeftElbowAngleRadians,
            descriptor.Articulation.LeftHandLoadNewtons,
            descriptor.Articulation.LeftHandApertureFraction,
            descriptor.Articulation.LeftGripForceNewtons,
            descriptor.Articulation.LeftHandSlip,
            previous?.Articulation.LeftHipAngleRadians,
            previous?.Articulation.LeftHipAbductionRadians,
            deltaSeconds,
            descriptor,
            tick,
            timestampMs);
        activeSomaticPopulations += AddHandFatiguePopulations(
            somaticLeft,
            somaticRight,
            descriptor,
            tick,
            timestampMs);

        var labyrinth = _vestibularLabyrinth.Transduce(descriptor, previous, deltaSeconds);
        var vestibular = BuildBilateral(
            StructureId.VestibularAfferents,
            descriptor,
            tick,
            timestampMs,
            labyrinth.Left,
            labyrinth.Right);

        var energyReserve = Math.Clamp(descriptor.StoredEnergyJoules / NominalStoredEnergyJoules, 0f, 1f);
        var thermalCold = Math.Clamp((36.8f - descriptor.CoreTemperatureCelsius) / 5f, 0f, 1f);
        var thermalWarm = Math.Clamp((descriptor.CoreTemperatureCelsius - 37.2f) / 5f, 0f, 1f);
        var hypoxia = Math.Clamp((0.96f - descriptor.BloodOxygenSaturationFraction) / 0.30f, 0f, 1f);
        var dehydration = Math.Clamp(1f - descriptor.HydrationFraction, 0f, 1f);
        var tissueDamage = Math.Clamp(1f - descriptor.TissueIntegrityFraction, 0f, 1f);
        var energyDeficit = Math.Clamp(1f - energyReserve, 0f, 1f);
        var hungerDrive = ComputeNeedDrive(energyDeficit, enter: 0.12f, full: 0.85f);
        var thirstDrive = ComputeNeedDrive(dehydration, enter: 0.10f, full: 0.65f);
        var muscleMetabolicFatigue = ComputePeakMuscleMetabolicFatigue(descriptor);
        var visceral = BuildBilateral(
            StructureId.VisceralAfferents,
            descriptor,
            tick,
            timestampMs,
            [
                ("glucose_energy_deficit_chemoreceptor", energyDeficit),
                ("arcuate_agrp_npy_hunger_drive", hungerDrive),
                ("tissue_damage_chemoreceptor", tissueDamage),
                ("core_cold_thermoreceptor", thermalCold),
                ("core_warm_thermoreceptor", thermalWarm),
                ("carotid_hypoxia_chemoreceptor", hypoxia),
                ("osmotic_dehydration_receptor", dehydration),
                ("lamina_terminalis_osmotic_thirst_drive", thirstDrive),
                ("muscle_metabolic_fatigue_interoceptor", muscleMetabolicFatigue)
            ]);
        var homeostaticDeviation = Math.Max(
            Math.Max(Math.Max(energyDeficit, tissueDamage), muscleMetabolicFatigue),
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
        var previousEnergyDeficit = Math.Clamp(1f - previousEnergyReserve, 0f, 1f);
        var previousDehydration = previous is null
            ? dehydration
            : Math.Clamp(1f - previous.HydrationFraction, 0f, 1f);
        var previousBalance = previous?.Articulation.Musculoskeletal?.Balance;
        var previousBalanceError = previous?.Articulation.Musculoskeletal?.BalanceError ??
            descriptor.Articulation.Musculoskeletal?.BalanceError ?? 0f;
        var currentBalanceError = descriptor.Articulation.Musculoskeletal?.BalanceError ?? 0f;
        var previousEffectiveSupportMargin = previousBalance is null
            ? effectiveSupportMargin
            : previousBalance.SupportMarginMeters + previousBalance.DynamicStabilityAllowanceMeters;
        var supportMarginImprovement = previousBalance is null
            ? 0f
            : Math.Clamp((effectiveSupportMargin - previousEffectiveSupportMargin) / 0.20f, -1f, 1f);
        var balanceImprovement = Math.Clamp(previousBalanceError - currentBalanceError, -1f, 1f);
        var recoveryLearningEligible = IsRecoveryLearningEligible(
            previousBalance,
            balance,
            previousBalanceError,
            currentBalanceError);
        var supportTeachingImprovement = recoveryLearningEligible
            ? Math.Max(0f, supportMarginImprovement)
            : 0f;
        var balanceTeachingImprovement = recoveryLearningEligible
            ? Math.Max(0f, balanceImprovement)
            : 0f;
        var totalHandLoad = descriptor.Articulation.LeftHandLoadNewtons + descriptor.Articulation.RightHandLoadNewtons;
        var totalFootLoad = descriptor.Articulation.LeftFootLoadNewtons + descriptor.Articulation.RightFootLoadNewtons;
        // Ordinary weight-bearing is support, not an attempted action. Only foot
        // loading above the passive body-weight envelope can teach that muscular
        // force failed to produce motion; hand loading remains direct evidence.
        var activeFootLoad = Math.Max(0f, totalFootLoad - PassiveBipedalSupportLoadNewtons);
        var appliedForce = Math.Clamp((totalHandLoad + (activeFootLoad * 0.20f)) / 360f, 0f, 1f);
        var measuredMotion = Math.Clamp(
            Magnitude(descriptor.LinearVelocityX, descriptor.LinearVelocityY, descriptor.LinearVelocityZ) / 1.2f,
            0f,
            1f);
        var ineffectiveForceEvidence = appliedForce * (1f - measuredMotion) *
            (1f - Math.Max(0f, supportMarginImprovement)) *
            (1f - Math.Max(0f, balanceImprovement));

        // A reset restores a newly instantiated body; it is not an earned appetitive
        // outcome and must not reinforce the action that preceded death.
        var respawnTransition = previous is not null &&
            previous.TissueIntegrityFraction <= 0.05f &&
            descriptor.TissueIntegrityFraction >= 0.90f;
        var deathTransition = previous is not null &&
            previous.TissueIntegrityFraction > 0.05f &&
            descriptor.TissueIntegrityFraction <= 0.05f;
        var energyRestorationTeaching = respawnTransition
            ? 0f
            : Math.Clamp(
                Math.Max(0f, energyChange) * (0.65f + (previousEnergyDeficit * 1.35f)),
                0f,
                1f);
        var hydrationRestorationTeaching = respawnTransition
            ? 0f
            : Math.Clamp(
                Math.Max(0f, hydrationChange) * (0.75f + (previousDehydration * 1.65f)),
                0f,
                1f);
        var positiveTeaching = respawnTransition
            ? 0f
            : Math.Clamp(
                (energyRestorationTeaching * 0.42f) +
                (Math.Max(0f, tissueChange) * 0.38f) +
                (hydrationRestorationTeaching * 0.35f) +
                (Math.Max(0f, homeostaticChange) * 0.35f),
                0f,
                1f);
        var negativeTeaching = Math.Clamp(
            (Math.Max(0f, -energyChange) * 0.12f) +
            (Math.Max(0f, -tissueChange) * 0.78f) +
            (Math.Max(0f, -hydrationChange) * 0.10f) +
            (Math.Max(0f, -homeostaticChange) * 0.30f) +
            (Math.Max(0f, -supportMarginImprovement) * 0.18f) +
            (Math.Max(0f, -balanceImprovement) * 0.20f) +
            (peakMuscleFatigueDistress * 0.18f) +
            (ineffectiveForceEvidence * peakMuscleFatigueDistress * 0.25f) +
            (descriptor.MotorTrainingMode ? ineffectiveForceEvidence * 0.30f : 0f) +
            (descriptor.TissueIntegrityFraction <= 0.05f ? 0.85f : 0f),
            0f,
            1f);
        var cadence = AdvanceHomeostaticCadence(
            descriptor,
            energyRestorationTeaching,
            hydrationRestorationTeaching,
            positiveTeaching,
            negativeTeaching);
        var habenularTeaching = BuildTeachingPopulation(
            StructureId.Habenula,
            "aversive_homeostatic_error",
            cadence.NegativeTeaching,
            descriptor,
            tick,
            timestampMs);
        var vtaTeaching = new List<SpikeMessage>();
        vtaTeaching.AddRange(BuildTeachingPopulation(
            StructureId.Vta,
            "appetitive_homeostatic_improvement",
            cadence.PositiveTeaching,
            descriptor,
            tick,
            timestampMs));
        vtaTeaching.AddRange(BuildTeachingPopulation(
            StructureId.Vta,
            "need_weighted_energy_restoration",
            cadence.EnergyRestorationTeaching,
            descriptor,
            tick,
            timestampMs));
        vtaTeaching.AddRange(BuildTeachingPopulation(
            StructureId.Vta,
            "need_weighted_hydration_restoration",
            cadence.HydrationRestorationTeaching,
            descriptor,
            tick,
            timestampMs));
        var sncTeaching = BuildTeachingPopulation(
            StructureId.Snc,
            "sensorimotor_homeostatic_improvement",
            cadence.PositiveTeaching,
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
            somaticLeft,
            somaticRight,
            vestibular.Left,
            vestibular.Right,
            cadence.Dispatch ? visceral.Left : [],
            cadence.Dispatch ? visceral.Right : [],
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
            hungerDrive,
            thirstDrive,
            energyRestorationTeaching,
            hydrationRestorationTeaching,
            positiveTeaching,
            negativeTeaching,
            supportMarginImprovement,
            balanceImprovement,
            ineffectiveForceEvidence,
            peakMuscleFatigueDistress,
            activeProprioceptivePopulations,
            activeSomaticPopulations,
            vestibular.ActivePopulations,
            visceral.ActivePopulations,
            descriptor.MotorTrainingMode,
            descriptor.SaturatedMuscleVelocityCount,
            tissueChange,
            deathTransition,
            respawnTransition,
            cadence.Dispatch,
            HomeostaticCadenceMilliseconds);
    }

    private HomeostaticCadenceResult AdvanceHomeostaticCadence(
        PhysicalBodyFrameDescriptor descriptor,
        float energyRestorationTeaching,
        float hydrationRestorationTeaching,
        float positiveTeaching,
        float negativeTeaching)
    {
        lock (_gate)
        {
            if (!_homeostaticBySource.TryGetValue(descriptor.InputSource, out var state))
            {
                state = new HomeostaticCadenceState();
                _homeostaticBySource[descriptor.InputSource] = state;
            }

            if (state.LastObservedTimestampMs > 0 &&
                descriptor.TimestampMs <= state.LastObservedTimestampMs)
            {
                state.Reset();
            }

            state.LastObservedTimestampMs = descriptor.TimestampMs;
            state.PendingEnergyRestoration = Math.Max(
                state.PendingEnergyRestoration,
                energyRestorationTeaching);
            state.PendingHydrationRestoration = Math.Max(
                state.PendingHydrationRestoration,
                hydrationRestorationTeaching);
            state.PendingPositiveTeaching = Math.Max(state.PendingPositiveTeaching, positiveTeaching);
            state.PendingNegativeTeaching = Math.Max(state.PendingNegativeTeaching, negativeTeaching);

            var dispatch = state.LastDispatchTimestampMs == 0 ||
                descriptor.TimestampMs - state.LastDispatchTimestampMs >= HomeostaticCadenceMilliseconds;
            if (!dispatch)
            {
                return HomeostaticCadenceResult.Buffered;
            }

            var result = new HomeostaticCadenceResult(
                true,
                state.PendingEnergyRestoration,
                state.PendingHydrationRestoration,
                state.PendingPositiveTeaching,
                state.PendingNegativeTeaching);
            state.LastDispatchTimestampMs = descriptor.TimestampMs;
            state.PendingEnergyRestoration = 0f;
            state.PendingHydrationRestoration = 0f;
            state.PendingPositiveTeaching = 0f;
            state.PendingNegativeTeaching = 0f;
            return result;
        }
    }

    private static bool IsRecoveryLearningEligible(
        PhysicalBalanceStateFrame? previous,
        PhysicalBalanceStateFrame current,
        float previousBalanceError,
        float currentBalanceError)
    {
        static bool IsRecoveryPhase(string? phase) => phase?.Trim().ToLowerInvariant() is
            "falling" or "fallen" or "righting" or "unstable";

        return IsRecoveryPhase(previous?.Phase) ||
            IsRecoveryPhase(current.Phase) ||
            previousBalanceError >= 0.40f ||
            currentBalanceError >= 0.40f ||
            (previous?.SupportMarginMeters ?? 0f) < 0f ||
            current.SupportMarginMeters < 0f;
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
        var muscleMetabolicFatigue = ComputePeakMuscleMetabolicFatigue(descriptor);
        return Math.Max(
            Math.Max(Math.Max(energyDeficit, tissueDamage), muscleMetabolicFatigue),
            Math.Max(thermalDeviation, Math.Max(hypoxia, dehydration)));
    }

    private static float ComputePeakMuscleMetabolicFatigue(PhysicalBodyFrameDescriptor descriptor)
        => descriptor.Articulation.Musculoskeletal?.Muscles is { Count: > 0 } muscles
            ? muscles.Max(static muscle =>
                Math.Clamp((muscle.FatigueFraction - 0.35f) / 0.65f, 0f, 1f))
            : 0f;

    private static float ComputeNeedDrive(float value, float enter, float full)
        => Math.Clamp((value - enter) / Math.Max(0.000001f, full - enter), 0f, 1f);

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
            if (previous is not null &&
                (current.Sequence <= previous.Sequence || current.TimestampMs <= previous.TimestampMs))
            {
                return null;
            }
            return previous;
        }
    }

    private sealed class HomeostaticCadenceState
    {
        public long LastObservedTimestampMs { get; set; }
        public long LastDispatchTimestampMs { get; set; }
        public float PendingEnergyRestoration { get; set; }
        public float PendingHydrationRestoration { get; set; }
        public float PendingPositiveTeaching { get; set; }
        public float PendingNegativeTeaching { get; set; }

        public void Reset()
        {
            LastObservedTimestampMs = 0;
            LastDispatchTimestampMs = 0;
            PendingEnergyRestoration = 0f;
            PendingHydrationRestoration = 0f;
            PendingPositiveTeaching = 0f;
            PendingNegativeTeaching = 0f;
        }
    }

    private readonly record struct HomeostaticCadenceResult(
        bool Dispatch,
        float EnergyRestorationTeaching,
        float HydrationRestorationTeaching,
        float PositiveTeaching,
        float NegativeTeaching)
    {
        public static HomeostaticCadenceResult Buffered { get; } = new(false, 0f, 0f, 0f, 0f);
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

    private static BilateralSpikes BuildBilateral(
        StructureId structure,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs,
        IReadOnlyList<(string Receptor, float Activation)> leftPopulations,
        IReadOnlyList<(string Receptor, float Activation)> rightPopulations)
    {
        var left = new List<SpikeMessage>(leftPopulations.Count * 3);
        var right = new List<SpikeMessage>(rightPopulations.Count * 3);
        var active = 0;
        foreach (var (receptor, activation) in leftPopulations)
        {
            if (activation < MinimumActivation)
            {
                continue;
            }

            active++;
            AddPopulation(left, "L", structure, receptor, activation, descriptor, tick, timestampMs);
        }
        foreach (var (receptor, activation) in rightPopulations)
        {
            if (activation < MinimumActivation)
            {
                continue;
            }

            active++;
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
        float hipAbduction,
        float kneeAngle,
        float ankleAngle,
        float ankleRoll,
        float footLoad,
        PhysicalFootPressureFrame? footPressure,
        float shoulderAngle,
        float elbowAngle,
        float handLoad,
        float handAperture,
        float gripForce,
        float handSlip,
        float? previousHipAngle,
        float? previousHipAbduction,
        float deltaSeconds,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        var hipVelocity = previousHipAngle.HasValue && deltaSeconds > 0f
            ? MathF.Abs(hipAngle - previousHipAngle.Value) / deltaSeconds
            : 0f;
        var hipCoronalVelocity = previousHipAbduction.HasValue && deltaSeconds > 0f
            ? MathF.Abs(hipAbduction - previousHipAbduction.Value) / deltaSeconds
            : 0f;
        var pressure = footPressure ?? PhysicalFootPressureFrame.Unloaded;
        IReadOnlyList<(string Receptor, float Activation)> populations =
        [
            ($"{bodySide}_hip_flexor_spindle", Positive(hipAngle, 1.2f)),
            ($"{bodySide}_hip_extensor_spindle", Negative(hipAngle, 1.2f)),
            ($"{bodySide}_hip_abductor_spindle", Positive(hipAbduction, 0.78f)),
            ($"{bodySide}_hip_adductor_spindle", Negative(hipAbduction, 0.45f)),
            ($"{bodySide}_knee_flexor_spindle", Positive(kneeAngle, 1.8f)),
            ($"{bodySide}_ankle_dorsiflexor_spindle", Positive(ankleAngle, 1.0f)),
            ($"{bodySide}_ankle_plantarflexor_spindle", Negative(ankleAngle, 1.0f)),
            ($"{bodySide}_ankle_invertor_spindle", Positive(ankleRoll, 0.52f)),
            ($"{bodySide}_ankle_evertor_spindle", Negative(ankleRoll, 0.26f)),
            ($"{bodySide}_hip_dynamic_spindle", Math.Clamp(hipVelocity / 6f, 0f, 1f)),
            ($"{bodySide}_hip_coronal_dynamic_spindle", Math.Clamp(hipCoronalVelocity / 4f, 0f, 1f)),
            ($"{bodySide}_foot_golgi_load", Math.Clamp(footLoad / 1_000f, 0f, 1f)),
            ($"{bodySide}_heel_medial_plantar_pressure", Math.Clamp(pressure.HeelMedialLoadNewtons / 500f, 0f, 1f)),
            ($"{bodySide}_heel_lateral_plantar_pressure", Math.Clamp(pressure.HeelLateralLoadNewtons / 500f, 0f, 1f)),
            ($"{bodySide}_forefoot_medial_plantar_pressure", Math.Clamp(pressure.ForefootMedialLoadNewtons / 500f, 0f, 1f)),
            ($"{bodySide}_forefoot_lateral_plantar_pressure", Math.Clamp(pressure.ForefootLateralLoadNewtons / 500f, 0f, 1f)),
            ($"{bodySide}_shoulder_flexor_spindle", Positive(shoulderAngle, 1.3f)),
            ($"{bodySide}_elbow_flexor_spindle", Positive(elbowAngle, 1.6f)),
            ($"{bodySide}_hand_golgi_load", Math.Clamp(handLoad / 300f, 0f, 1f)),
            ($"{bodySide}_finger_flexor_spindle", Math.Clamp(1f - handAperture, 0f, 1f)),
            ($"{bodySide}_finger_extensor_spindle", Math.Clamp(handAperture, 0f, 1f)),
            ($"{bodySide}_grip_golgi_tendon", Math.Clamp(gripForce / 92f, 0f, 1f)),
            ($"{bodySide}_grip_slip_mechanoreceptor", Math.Clamp(handSlip, 0f, 1f))
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

    private static int AddHandFatiguePopulations(
        List<SpikeMessage> cerebralLeft,
        List<SpikeMessage> cerebralRight,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs)
    {
        var active = 0;
        var rightDistress = Math.Clamp(
            (descriptor.Articulation.RightHandFatigue - 0.45f) / 0.55f,
            0f,
            1f);
        var leftDistress = Math.Clamp(
            (descriptor.Articulation.LeftHandFatigue - 0.45f) / 0.55f,
            0f,
            1f);
        if (rightDistress >= MinimumActivation)
        {
            active++;
            AddPopulation(
                cerebralLeft,
                "L",
                StructureId.SomaticAfferents,
                "hand:group_iii_iv_flexor_fatigue:right",
                rightDistress,
                descriptor,
                tick,
                timestampMs);
        }
        if (leftDistress >= MinimumActivation)
        {
            active++;
            AddPopulation(
                cerebralRight,
                "R",
                StructureId.SomaticAfferents,
                "hand:group_iii_iv_flexor_fatigue:left",
                leftDistress,
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

    private static int AddMuscleFatiguePopulations(
        List<SpikeMessage> cerebralLeft,
        List<SpikeMessage> cerebralRight,
        PhysicalBodyFrameDescriptor descriptor,
        long tick,
        double timestampMs,
        out float peakDistress)
    {
        peakDistress = 0f;
        var muscles = descriptor.Articulation.Musculoskeletal?.Muscles;
        if (muscles is null || muscles.Count == 0)
        {
            return 0;
        }

        var active = 0;
        foreach (var muscle in muscles)
        {
            var fatigue = Math.Clamp((muscle.FatigueFraction - 0.45f) / 0.55f, 0f, 1f);
            var contraction = 0.25f + (Math.Clamp(muscle.Activation, 0f, 1f) * 0.75f);
            var distress = fatigue * contraction;
            peakDistress = Math.Max(peakDistress, distress);
            if (distress < MinimumActivation)
            {
                continue;
            }

            active++;
            var normalizedName = new string(muscle.Name
                .Where(static character => char.IsLetterOrDigit(character) || character == '_')
                .Select(static character => char.ToLowerInvariant(character))
                .ToArray());
            var bodySide = muscle.Side.ToLowerInvariant();
            var region = ResolveMuscleRegion(normalizedName);
            var receptor = $"{region}:group_iii_iv_muscle_nociceptor:{bodySide}_{normalizedName}";

            // Ascending body afferents project contralaterally. Midline axial
            // muscles reach both hemispheres without inventing a preferred side.
            if (muscle.Side is "R" or "M")
            {
                AddPopulation(
                    cerebralLeft,
                    "L",
                    StructureId.SomaticAfferents,
                    receptor,
                    distress,
                    descriptor,
                    tick,
                    timestampMs);
            }
            if (muscle.Side is "L" or "M")
            {
                AddPopulation(
                    cerebralRight,
                    "R",
                    StructureId.SomaticAfferents,
                    receptor,
                    distress,
                    descriptor,
                    tick,
                    timestampMs);
            }
        }

        return active;
    }

    private static string ResolveMuscleRegion(string normalizedName)
    {
        if (normalizedName.Contains("deltoid", StringComparison.Ordinal) ||
            normalizedName.Contains("pectoralis", StringComparison.Ordinal) ||
            normalizedName.Contains("latissimus", StringComparison.Ordinal) ||
            normalizedName.Contains("biceps", StringComparison.Ordinal) ||
            normalizedName.Contains("triceps", StringComparison.Ordinal))
        {
            return "arm";
        }

        if (normalizedName.Contains("iliopsoas", StringComparison.Ordinal) ||
            normalizedName.Contains("gluteus", StringComparison.Ordinal) ||
            normalizedName.Contains("adductor", StringComparison.Ordinal))
        {
            return "hip";
        }

        if (normalizedName.Contains("hamstring", StringComparison.Ordinal) ||
            normalizedName.Contains("quadriceps", StringComparison.Ordinal))
        {
            return "thigh";
        }

        if (normalizedName.Contains("tibialis", StringComparison.Ordinal) ||
            normalizedName.Contains("gastrocnemius", StringComparison.Ordinal) ||
            normalizedName.Contains("fibularis", StringComparison.Ordinal))
        {
            return "shin";
        }

        if (normalizedName.Contains("capitis", StringComparison.Ordinal))
        {
            return "neck";
        }

        return "trunk";
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
