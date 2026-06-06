namespace NRE.SimAvatar;

public readonly record struct AvatarNervousSystemBodyState(
    bool IsSleeping,
    double Hunger,
    double Threat,
    double Health,
    double SecondsSinceProgress,
    double NoProgressTimeoutSeconds);
