namespace NRE.SimAvatar;

public readonly record struct AvatarBodyTelemetry(
    double ForwardVelocity,
    double TurnRateDeg,
    double ContactLevel,
    double LeftMotorDrive,
    double RightMotorDrive,
    double EnvironmentalDarkness = 0.0,
    double ShelterNeed = 0.0,
    double Anxiety = 0.0,
    double Hunger = 0.0,
    double PredatorThreat = 0.0,
    double InShelter = 0.0,
    double Health = 1.0,
    double ShelterSafety = 0.0,
    double TactileFront = 0.0,
    double TactileLeft = 0.0,
    double TactileRight = 0.0,
    double TactileGround = 0.0,
    double PainLevel = 0.0,
    double Urgency = 0.0);
