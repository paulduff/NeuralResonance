namespace NRE.SimAvatar;

public readonly record struct AvatarPlaceObservation(
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
    string Source = "avatar_place");
