namespace NRE.SimAvatar;

public readonly record struct AvatarActionOutput(
    AvatarMotorOutput Movement,
    AvatarToolSignal Tool,
    AvatarAttentionOutput Attention,
    AvatarAudioOutput? Voice,
    AvatarGestureOutput Gesture,
    AvatarArousalOutput Arousal,
    AvatarBodySoundOutput BodySound,
    AvatarNeedsRhythmState Needs,
    AvatarReflexOutput Reflex,
    AvatarAffectiveWeather Weather,
    long EmittedUnixMs,
    string OutputSource = "avatar_action");
