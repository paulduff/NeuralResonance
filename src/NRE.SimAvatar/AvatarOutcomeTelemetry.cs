namespace NRE.SimAvatar;

public readonly record struct AvatarOutcomeTelemetry(
    double SatietyRelief = 0.0,
    double SafetyRelief = 0.0,
    double PainLevel = 0.0,
    double DamageLevel = 0.0,
    double ShelterComfort = 0.0,
    double Progress = 0.0,
    double EffortCost = 0.0,
    double Novelty = 0.0,
    double SocialApproval = 0.0,
    string Pattern = AvatarRuntimeDefaults.OutcomePattern,
    string InputSource = AvatarRuntimeDefaults.OutcomeInputSource,
    string? Hemisphere = null);
