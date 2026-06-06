using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public readonly record struct AvatarVisualSignal(
    string Pattern,
    float Intensity,
    int BurstCount,
    double LeftSaliency,
    double RightSaliency,
    double MotionSignal,
    double LuminanceSignal,
    string InputSource)
{
    public VisualInputRequest ToVisualInputRequest(
        string targetStructure = "V1",
        string sourceStructure = AvatarRuntimeDefaults.UnifiedVisualStreamSourceStructure,
        string? hemisphere = null)
        => new(
            Pattern: string.IsNullOrWhiteSpace(Pattern)
                ? AvatarRuntimeDefaults.UnifiedVisualStreamPattern
                : Pattern,
            Intensity: Math.Clamp(Intensity, 0.05f, 3.5f),
            BurstCount: Math.Clamp(BurstCount, 1, 96),
            TargetStructure: string.IsNullOrWhiteSpace(targetStructure) ? "V1" : targetStructure,
            SourceStructure: string.IsNullOrWhiteSpace(sourceStructure)
                ? AvatarRuntimeDefaults.UnifiedVisualStreamSourceStructure
                : sourceStructure,
            Hemisphere: hemisphere,
            LeftFieldSaliency: (float)Math.Clamp(LeftSaliency, 0.0, 1.0),
            RightFieldSaliency: (float)Math.Clamp(RightSaliency, 0.0, 1.0),
            UseAttentionRouting: true,
            InputSource: string.IsNullOrWhiteSpace(InputSource)
                ? AvatarRuntimeDefaults.UnifiedVisualInputSource
                : InputSource);
}
