using System;
using System.Collections.Generic;

namespace NRE.Core.Engine;

/// <summary>
/// Phoneme-first speech surface for proto-language output.
/// The engine can still attach an English gloss, but articulation is planned from phonemes.
/// </summary>
public static class LexemePhonology
{
    public readonly record struct SpokenForm(string SurfaceText, string GlossText, string[] Phonemes);

    private static readonly Dictionary<LexemeId, string[]> _phonemes = new()
    {
        [LexemeId.Awake] = ["a", "w", "e", "k"],
        [LexemeId.Nrem] = ["n", "r", "e", "m"],
        [LexemeId.Rem] = ["r", "e", "m"],
        [LexemeId.Sensors] = ["s", "e", "n", "s", "o", "r"],
        [LexemeId.Online] = ["o", "n", "l", "a", "i", "n"],
        [LexemeId.Entering] = ["e", "n", "t", "e", "r"],
        [LexemeId.Consolidating] = ["k", "o", "n", "s", "o", "l"],
        [LexemeId.Dream] = ["d", "r", "i", "m"],
        [LexemeId.Replay] = ["r", "e", "p", "l", "a", "i"],
        [LexemeId.Transition] = ["t", "r", "a", "n", "s", "i"],
        [LexemeId.Alert] = ["a", "l", "e", "t"],
        [LexemeId.Now] = ["n", "a", "u"],
        [LexemeId.Look] = ["l", "u", "k"],
        [LexemeId.Listen] = ["l", "i", "s", "e", "n"],
        [LexemeId.Remember] = ["r", "e", "m", "e", "r"],
        [LexemeId.Again] = ["a", "g", "e", "n"],
        [LexemeId.Seen] = ["s", "i", "n"],
        [LexemeId.Known] = ["n", "o", "n"],
        [LexemeId.Unknown] = ["u", "n", "o", "n"],
        [LexemeId.Unsure] = ["u", "n", "s", "u", "r"],
        [LexemeId.What] = ["w", "a", "t"],
        [LexemeId.Why] = ["w", "a", "i"],
        [LexemeId.Good] = ["g", "u", "d"],
        [LexemeId.Yes] = ["j", "e", "s"],
        [LexemeId.Safe] = ["s", "e", "i", "f"],
        [LexemeId.Nice] = ["n", "a", "i", "s"],
        [LexemeId.Bad] = ["b", "a", "d"],
        [LexemeId.No] = ["n", "o"],
        [LexemeId.Danger] = ["d", "e", "n", "j", "e", "r"],
        [LexemeId.Stop] = ["s", "t", "a", "p"],
        [LexemeId.Bright] = ["b", "r", "a", "i", "t"],
        [LexemeId.Shape] = ["sh", "e", "i", "p"],
        [LexemeId.Move] = ["m", "u", "v"],
        [LexemeId.Loud] = ["l", "a", "u", "d"],
        [LexemeId.Noise] = ["n", "o", "i", "z"],
        [LexemeId.Tone] = ["t", "o", "n"],
        [LexemeId.Sound] = ["s", "a", "u", "n", "d"],
        [LexemeId.Here] = ["h", "i", "r"],
        [LexemeId.Ok] = ["o", "k", "e"],
        [LexemeId.Idle] = ["a", "i", "d", "e", "l"],
    };

    public static SpokenForm Compose(ReadOnlySpan<LexemeId> tokens)
    {
        if (tokens.Length == 0)
            return new SpokenForm(string.Empty, string.Empty, Array.Empty<string>());

        var surfaces = new List<string>(tokens.Length);
        var glosses = new List<string>(tokens.Length);
        var phonemes = new List<string>(tokens.Length * 5);

        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token == LexemeId.None)
                continue;

            var tokenPhonemes = GetPhonemes(token);
            if (tokenPhonemes.Length == 0)
                continue;

            surfaces.Add(ToSurface(tokenPhonemes));

            var gloss = LexemeText.ToWord(token);
            if (!string.IsNullOrWhiteSpace(gloss))
                glosses.Add(gloss);

            if (phonemes.Count > 0)
                phonemes.Add("_");
            phonemes.AddRange(tokenPhonemes);
        }

        return new SpokenForm(
            SurfaceText: string.Join(" ", surfaces),
            GlossText: string.Join(" ", glosses),
            Phonemes: phonemes.ToArray());
    }

    public static string[] GetPhonemes(LexemeId token)
        => _phonemes.TryGetValue(token, out var phonemes) ? phonemes : Array.Empty<string>();

    public static string ToSurface(IReadOnlyList<string> phonemes)
    {
        if (phonemes.Count == 0)
            return string.Empty;

        var parts = new string[phonemes.Count];
        for (int i = 0; i < phonemes.Count; i++)
            parts[i] = Romanize(phonemes[i]);
        return string.Concat(parts);
    }

    private static string Romanize(string phoneme)
        => phoneme switch
        {
            "_" => " ",
            "sh" => "sh",
            "ch" => "ch",
            "ng" => "ng",
            "j" => "y",
            "a" => "a",
            "e" => "e",
            "i" => "i",
            "o" => "o",
            "u" => "u",
            "w" => "w",
            "h" => "h",
            "r" => "r",
            "l" => "l",
            "m" => "m",
            "n" => "n",
            "s" => "s",
            "t" => "t",
            "k" => "k",
            "p" => "p",
            "b" => "b",
            "d" => "d",
            "g" => "g",
            "f" => "f",
            "v" => "v",
            "z" => "z",
            _ => phoneme
        };
}
