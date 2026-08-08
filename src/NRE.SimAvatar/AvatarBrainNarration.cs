namespace NRE.SimAvatar;

public sealed record AvatarBrainNarration(
    string Utterance,
    long Sequence,
    long LastUpdatedTick,
    string Source)
{
    public static AvatarBrainNarration Empty { get; } = new(string.Empty, -1, -1, string.Empty);

    public bool HasText => !string.IsNullOrWhiteSpace(Utterance);
}
