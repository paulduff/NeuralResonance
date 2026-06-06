using System.Text;

namespace NRE.Core.Engine;

/// <summary>
/// Canonical proto-language symbols.
///
/// The core brain simulation should traffic in <see cref="LexemeId"/> values rather than strings.
/// Strings only appear at the final UI boundary when emitting a <see cref="NreEngine.VoiceUtterance"/>.
/// </summary>
public enum LexemeId : ushort
{
    None = 0,

    // State / phases
    Awake,
    Nrem,
    Rem,
    Sensors,
    Online,
    Entering,
    Consolidating,
    Dream,
    Replay,
    Transition,

    // Attention / urgency
    Alert,
    Now,
    Look,
    Listen,

    // Memory
    Remember,
    Again,
    Seen,
    Known,

    // Uncertainty
    Unknown,
    Unsure,
    What,
    Why,

    // Valence / safety
    Good,
    Yes,
    Safe,
    Nice,
    Bad,
    No,
    Danger,
    Stop,

    // Sensory
    Bright,
    Shape,
    Move,
    Loud,
    Noise,
    Tone,
    Sound,

    // Minimal idle
    Here,
    Ok,
    Idle
}

public static class LexemeText
{
    public static string ToText(ReadOnlySpan<LexemeId> tokens)
    {
        if (tokens.Length == 0) return string.Empty;
        var sb = new StringBuilder(capacity: tokens.Length * 6);
        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t == LexemeId.None) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(ToWord(t));
        }
        return sb.ToString();
    }

    public static string ToWord(LexemeId id)
        => id switch
        {
            LexemeId.Awake => "awake",
            LexemeId.Nrem => "nrem",
            LexemeId.Rem => "rem",
            LexemeId.Sensors => "sensors",
            LexemeId.Online => "online",
            LexemeId.Entering => "entering",
            LexemeId.Consolidating => "consolidating",
            LexemeId.Dream => "dream",
            LexemeId.Replay => "replay",
            LexemeId.Transition => "transition",
            LexemeId.Alert => "alert",
            LexemeId.Now => "now",
            LexemeId.Look => "look",
            LexemeId.Listen => "listen",
            LexemeId.Remember => "remember",
            LexemeId.Again => "again",
            LexemeId.Seen => "seen",
            LexemeId.Known => "known",
            LexemeId.Unknown => "unknown",
            LexemeId.Unsure => "unsure",
            LexemeId.What => "what",
            LexemeId.Why => "why",
            LexemeId.Good => "good",
            LexemeId.Yes => "yes",
            LexemeId.Safe => "safe",
            LexemeId.Nice => "nice",
            LexemeId.Bad => "bad",
            LexemeId.No => "no",
            LexemeId.Danger => "danger",
            LexemeId.Stop => "stop",
            LexemeId.Bright => "bright",
            LexemeId.Shape => "shape",
            LexemeId.Move => "move",
            LexemeId.Loud => "loud",
            LexemeId.Noise => "noise",
            LexemeId.Tone => "tone",
            LexemeId.Sound => "sound",
            LexemeId.Here => "here",
            LexemeId.Ok => "ok",
            LexemeId.Idle => "idle",
            _ => ""
        };
}
