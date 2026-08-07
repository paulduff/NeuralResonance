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
    string? InputSource);

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
