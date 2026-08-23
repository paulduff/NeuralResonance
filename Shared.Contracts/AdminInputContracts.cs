namespace NeuralResonanceEngine.Shared.Contracts;

public static class AdminInputSource
{
    public static string Normalize(string? inputSource)
    {
        if (string.IsNullOrWhiteSpace(inputSource))
        {
            return "unspecified";
        }

        var normalized = inputSource.Trim().ToLowerInvariant();
        return normalized.Length <= 64 ? normalized : normalized[..64];
    }

    public static bool IsAvatarSource(string? inputSource)
    {
        if (string.IsNullOrWhiteSpace(inputSource))
        {
            return false;
        }

        var normalized = inputSource.Trim().ToLowerInvariant();
        return normalized is "avatar" or "avatarvision" or "avatarobject" or "editor_webcam"
            || normalized.StartsWith("avatar_", StringComparison.Ordinal)
            || normalized.StartsWith("avatar-", StringComparison.Ordinal);
    }
}

public sealed record InputGateControlRequest(bool? AvatarVisionEnabled, bool? SpontaneousSpikingEnabled);

public sealed record SomaticContactFrameRequest(
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
    string? InputSource);

public sealed record PhysicalBodyFrameRequest(
    long Sequence,
    long TimestampMs,
    float LinearVelocityXMetersPerSecond,
    float LinearVelocityYMetersPerSecond,
    float LinearVelocityZMetersPerSecond,
    float AngularVelocityXRadiansPerSecond,
    float AngularVelocityYRadiansPerSecond,
    float AngularVelocityZRadiansPerSecond,
    float StoredEnergyJoules,
    float TissueIntegrityFraction,
    float CoreTemperatureCelsius,
    float BloodOxygenSaturationFraction,
    float HydrationFraction,
    string? InputSource,
    PhysicalArticulationFrame? Articulation = null,
    bool MotorTrainingMode = false);

/// <summary>
/// Direct physical measurements from the simulated musculoskeletal body. These
/// values are receptor inputs only; they contain no action labels or outcomes.
/// </summary>
public sealed record PhysicalArticulationFrame(
    float LeftHipAngleRadians,
    float RightHipAngleRadians,
    float LeftKneeAngleRadians,
    float RightKneeAngleRadians,
    float LeftAnkleAngleRadians,
    float RightAnkleAngleRadians,
    float LeftFootLoadNewtons,
    float RightFootLoadNewtons,
    float LeftShoulderAngleRadians,
    float RightShoulderAngleRadians,
    float LeftElbowAngleRadians,
    float RightElbowAngleRadians,
    float LeftHandLoadNewtons,
    float RightHandLoadNewtons,
    float ManipulatorExtensionFraction,
    float TrunkPitchRadians,
    float TrunkRollRadians,
    MusculoskeletalStateFrame? Musculoskeletal = null,
    float LeftShoulderAbductionRadians = 0f,
    float RightShoulderAbductionRadians = 0f,
    float NeckYawRadians = 0f,
    float NeckPitchRadians = 0f,
    float SupportPlaneOffsetMeters = 0f,
    float LeftHipAbductionRadians = 0f,
    float RightHipAbductionRadians = 0f,
    float LeftAnkleRollRadians = 0f,
    float RightAnkleRollRadians = 0f,
    PhysicalFootPressureFrame? LeftFootPressure = null,
    PhysicalFootPressureFrame? RightFootPressure = null,
    float TrunkYawRadians = 0f,
    float LeftHandApertureFraction = 1f,
    float RightHandApertureFraction = 1f,
    float LeftGripForceNewtons = 0f,
    float RightGripForceNewtons = 0f,
    float LeftHandFatigue = 0f,
    float RightHandFatigue = 0f,
    float LeftHandSlip = 0f,
    float RightHandSlip = 0f)
{
    public static PhysicalArticulationFrame Neutral { get; } = new(
        LeftHipAngleRadians: 0f,
        RightHipAngleRadians: 0f,
        LeftKneeAngleRadians: 0f,
        RightKneeAngleRadians: 0f,
        LeftAnkleAngleRadians: 0f,
        RightAnkleAngleRadians: 0f,
        LeftFootLoadNewtons: 0f,
        RightFootLoadNewtons: 0f,
        LeftShoulderAngleRadians: 0f,
        RightShoulderAngleRadians: 0f,
        LeftElbowAngleRadians: 0f,
        RightElbowAngleRadians: 0f,
        LeftHandLoadNewtons: 0f,
        RightHandLoadNewtons: 0f,
        ManipulatorExtensionFraction: 0f,
        TrunkPitchRadians: 0f,
        TrunkRollRadians: 0f,
        Musculoskeletal: MusculoskeletalStateFrame.Neutral,
        LeftShoulderAbductionRadians: 0f,
        RightShoulderAbductionRadians: 0f,
        NeckYawRadians: 0f,
        NeckPitchRadians: 0f,
        SupportPlaneOffsetMeters: 0f,
        LeftHipAbductionRadians: 0f,
        RightHipAbductionRadians: 0f,
        LeftAnkleRollRadians: 0f,
        RightAnkleRollRadians: 0f,
        LeftFootPressure: PhysicalFootPressureFrame.Unloaded,
        RightFootPressure: PhysicalFootPressureFrame.Unloaded,
        TrunkYawRadians: 0f,
        LeftHandApertureFraction: 1f,
        RightHandApertureFraction: 1f,
        LeftGripForceNewtons: 0f,
        RightGripForceNewtons: 0f,
        LeftHandFatigue: 0f,
        RightHandFatigue: 0f,
        LeftHandSlip: 0f,
        RightHandSlip: 0f);
}

/// <summary>
/// Loads measured at four plantar receptor fields. Their sum approximates the
/// corresponding foot load; the values are sensory facts, not balance commands.
/// </summary>
public sealed record PhysicalFootPressureFrame(
    float HeelMedialLoadNewtons,
    float HeelLateralLoadNewtons,
    float ForefootMedialLoadNewtons,
    float ForefootLateralLoadNewtons)
{
    public static PhysicalFootPressureFrame Unloaded { get; } = new(0f, 0f, 0f, 0f);
}

/// <summary>
/// Physical measurements produced by the avatar's muscles and postural plant.
/// Names identify anatomical receptors at the body boundary; they are not action
/// labels and confer no behavioural authority.
/// </summary>
public sealed record MusculoskeletalStateFrame(
    string Posture,
    float BodyHeightMeters,
    float UprightFraction,
    float SupportFraction,
    float BalanceError,
    IReadOnlyList<PhysicalMuscleMeasurement> Muscles,
    PhysicalBalanceStateFrame? Balance = null)
{
    public static MusculoskeletalStateFrame Neutral { get; } = new(
        Posture: "standing",
        BodyHeightMeters: 1.74f,
        UprightFraction: 1f,
        SupportFraction: 0f,
        BalanceError: 0f,
        Muscles: [],
        Balance: PhysicalBalanceStateFrame.Neutral);
}

/// <summary>
/// Mechanical balance measurements produced by the articulated body. These are
/// physical receptor facts, not desired poses, recovery actions, or labels for
/// a host-authored controller.
/// </summary>
public sealed record PhysicalBalanceStateFrame(
    float CenterOfMassXMeters,
    float CenterOfMassYMeters,
    float CenterOfMassZMeters,
    float CenterOfMassVelocityXMetersPerSecond,
    float CenterOfMassVelocityZMetersPerSecond,
    float ExtrapolatedCenterOfMassXMeters,
    float ExtrapolatedCenterOfMassZMeters,
    float CenterOfPressureXMeters,
    float CenterOfPressureZMeters,
    float SupportAreaSquareMeters,
    float SupportMarginMeters,
    float FallPitchRadians,
    float FallRollRadians,
    float FallPitchVelocityRadiansPerSecond,
    float FallRollVelocityRadiansPerSecond,
    string Phase,
    float RightingForceFraction = 0f,
    float DynamicStabilityAllowanceMeters = 0f)
{
    public static PhysicalBalanceStateFrame Neutral { get; } = new(
        CenterOfMassXMeters: 0f,
        CenterOfMassYMeters: 0.94f,
        CenterOfMassZMeters: 0f,
        CenterOfMassVelocityXMetersPerSecond: 0f,
        CenterOfMassVelocityZMetersPerSecond: 0f,
        ExtrapolatedCenterOfMassXMeters: 0f,
        ExtrapolatedCenterOfMassZMeters: 0f,
        CenterOfPressureXMeters: 0f,
        CenterOfPressureZMeters: 0f,
        SupportAreaSquareMeters: 0f,
        SupportMarginMeters: 0f,
        FallPitchRadians: 0f,
        FallRollRadians: 0f,
        FallPitchVelocityRadiansPerSecond: 0f,
        FallRollVelocityRadiansPerSecond: 0f,
        Phase: "stable",
        RightingForceFraction: 0f);
}

public sealed record PhysicalMuscleMeasurement(
    string Name,
    string Side,
    float Activation,
    float ForceNewtons,
    float LengthFraction,
    float VelocityPerSecond,
    float FatigueFraction);

public sealed record InputGateRuntime(
    bool AvatarVisionEnabled,
    bool SpontaneousSpikingEnabled)
{
    public static InputGateRuntime Default { get; } = new(
        AvatarVisionEnabled: true,
        SpontaneousSpikingEnabled: true);

    public static InputGateRuntime Normalize(InputGateRuntime? value)
    {
        if (value is null)
        {
            return Default;
        }

        return value with
        {
            AvatarVisionEnabled = value.AvatarVisionEnabled,
            SpontaneousSpikingEnabled = value.SpontaneousSpikingEnabled
        };
    }
}
