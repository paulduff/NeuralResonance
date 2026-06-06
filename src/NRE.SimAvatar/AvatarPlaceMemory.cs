namespace NRE.SimAvatar;

public sealed record AvatarPlaceMemory(
    string PlaceId,
    string Label,
    double X,
    double Y,
    double Z,
    double Safety,
    double Danger,
    double Food,
    double Blockage,
    double Interest,
    double Confidence,
    long FirstSeenUnixMs,
    long LastSeenUnixMs,
    int ObservationCount,
    string DominantKind,
    string Source);
