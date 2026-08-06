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

public sealed record ObjectInputRequest(
    string? ObjectId,
    string? Label,
    float? Salience,
    float? Confidence,
    float? Intensity,
    int? BurstCount,
    string? Hemisphere,
    bool? EncodeMemory,
    string? InputSource);

public sealed record VisualInputRequest(
    string? Pattern,
    float? Intensity,
    int? BurstCount,
    string? TargetStructure,
    string? SourceStructure,
    string? Hemisphere,
    float? LeftFieldSaliency,
    float? RightFieldSaliency,
    bool? UseAttentionRouting,
    string? InputSource);

public sealed record BodyStateInputRequest(
    float? ForwardVelocity,
    float? TurnRateDeg,
    float? ContactLevel,
    float? LeftMotorDrive,
    float? RightMotorDrive,
    float? Intensity,
    int? BurstCount,
    string? TargetStructure,
    string? SourceStructure,
    string? Hemisphere,
    bool? IncludeVestibular,
    bool? IncludeCerebellar,
    bool? IsFeedback,
    string? Pattern,
    string? InputSource,
    float? Hunger = null,
    float? Health = null,
    float? TactileFront = null,
    float? TactileLeft = null,
    float? TactileRight = null,
    float? TactileGround = null,
    float? PainLevel = null);

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
