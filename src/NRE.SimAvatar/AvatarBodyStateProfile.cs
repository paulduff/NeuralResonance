namespace NRE.SimAvatar;

public readonly record struct AvatarBodyStateProfile(
    double MaxForwardSpeed,
    double MaxTurnRateDeg,
    double BaseIntensity,
    double MotionIntensityWeight,
    double TurnIntensityWeight,
    double ContactIntensityWeight,
    double BaseBurstCount,
    double MotionBurstWeight,
    double TurnBurstWeight,
    double ContactBurstWeight,
    double MinIntensity = 0.10,
    double MaxIntensity = 3.50,
    int MinBurstCount = 4,
    int MaxBurstCount = 56,
    string TargetStructure = AvatarRuntimeDefaults.BodyStateTargetStructure,
    string SourceStructure = AvatarRuntimeDefaults.BodyStateSourceStructure,
    string Pattern = AvatarRuntimeDefaults.BodyStatePattern,
    string InputSource = AvatarRuntimeDefaults.BodyStateInputSource);
