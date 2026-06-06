namespace NRE.SimAvatar;

public readonly record struct AvatarDispatchSpike(
    string SourceStructure,
    string SourceHemisphere,
    long WallClockUnixMs,
    string SourceNeuronId = "");
