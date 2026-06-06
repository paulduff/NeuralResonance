namespace NRE.SimAvatar;

public readonly record struct AvatarObjectObservation(
    string ObjectId,
    string Label,
    double Salience,
    double Confidence,
    double Intensity,
    int BurstCount,
    double DistanceMeters,
    string? Hemisphere,
    bool EncodeMemory = true,
    string InputSource = "avatar_object");
