namespace NRE.SimAvatar;

public sealed record AvatarServiceClockOptions(
    bool Enabled = false,
    int TickIntervalMs = 50,
    bool ApplyDriveDecay = true,
    double? DriveDecayOverride = null);
