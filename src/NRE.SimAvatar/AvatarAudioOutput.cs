namespace NRE.SimAvatar;

public readonly record struct AvatarAudioOutput(
    string Pattern,
    float Intensity,
    string? Text = null,
    string OutputSource = "avatar_voice");
