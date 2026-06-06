namespace NRE.SimAvatar;

public sealed record AvatarLanguageCommand(
    string Text,
    string Mode = "english",
    string? Hemisphere = "L",
    float? Intensity = null,
    int? BurstPerToken = null);

public sealed record AvatarLanguageCommandResult(
    string Mode,
    int TokenCount,
    int BrainTokenCount,
    int GeneratedSpikes,
    int DeliveredSpikes,
    int TargetInstances,
    string Utterance,
    bool PausedDueToSleep,
    string GrammarIntent,
    string GrammarMood,
    string CommandKey,
    string MotorDirective,
    float Strength,
    AvatarBrainNarration Narration);

public sealed record AvatarBrainNarration(
    string Utterance,
    long Sequence,
    long LastUpdatedTick,
    string Source)
{
    public static AvatarBrainNarration Empty { get; } = new(string.Empty, -1, -1, string.Empty);

    public bool HasText => !string.IsNullOrWhiteSpace(Utterance);
}
