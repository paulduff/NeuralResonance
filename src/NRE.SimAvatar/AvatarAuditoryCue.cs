namespace NRE.SimAvatar;

public readonly record struct AvatarAuditoryCue(
    string Pattern,
    float Intensity,
    int BurstCount,
    string? Hemisphere = null,
    string SourceStructure = AvatarRuntimeDefaults.UnifiedAudioSourceStructure,
    string TargetStructure = AvatarRuntimeDefaults.UnifiedAudioTargetStructure,
    string InputSource = AvatarRuntimeDefaults.UnifiedAudioInputSource);
