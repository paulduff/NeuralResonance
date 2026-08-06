namespace NRE.SimAvatar;

public readonly record struct AvatarNervousSystemBodyState(
    double Hunger,
    double Threat,
    double Health,
    double SecondsSinceProgress,
    double NoProgressTimeoutSeconds);
