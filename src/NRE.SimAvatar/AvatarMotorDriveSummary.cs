namespace NRE.SimAvatar;

public readonly record struct AvatarMotorDriveSummary(
    double LeftInput,
    double RightInput,
    int MotorEvents,
    int InPlaceTurnEvents = 0);
