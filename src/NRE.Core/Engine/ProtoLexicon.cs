namespace NRE.Core.Engine;

/// <summary>
/// Proto-language lexicon: a tiny, controllable vocabulary grounded in internal state.
///
/// IMPORTANT: The lexicon operates on <see cref="LexemeId"/> values, not strings.
/// This keeps language from "leaking" into the brain. Strings are only formed at the
/// final UI boundary when the engine emits a voice utterance.
/// </summary>
public sealed class ProtoLexicon
{
    private readonly Random _rng;

    public ProtoLexicon(Random rng) => _rng = rng;

    public readonly record struct Context(
        VocalMotor.VoiceDrive Drive,
        float VisualIntensity01,
        float AuditoryIntensity01,
        bool MemoryHit,
        SleepPhase Phase,
        ushort ReplayRegionMask = 0,
        float ReplayStrength01 = 0f,
        long ReplayAgeSteps = 0);

    /// <summary>
    /// Generate a short utterance plan into <paramref name="buffer"/>.
    /// Returns the number of tokens written (0 means "say nothing").
    /// </summary>
    public int GenerateTokens(in Context ctx, Span<LexemeId> buffer, int maxTokens = 4)
    {
        if (ctx.Phase != SleepPhase.Awake) return 0;
        if (buffer.Length == 0) return 0;

        maxTokens = Math.Clamp(maxTokens, 1, 6);

        // Temporary pool for candidate tokens.
        Span<LexemeId> tokens = stackalloc LexemeId[16];
        int n = 0;

        float ar = ctx.Drive.Arousal01;
        float ur = ctx.Drive.Urgency01;
        float ce = ctx.Drive.Certainty01;
        float va = ctx.Drive.Valence11;

        
        // Replay-driven candidates (Option 3):
        // If hippocampus completed an episode from a partial cue, bias tokens toward what that episode contained.
        if (ctx.ReplayRegionMask != 0)
        {
            // Core memory token.
            if (ctx.ReplayAgeSteps <= 2500)
            {
                if (n < tokens.Length) tokens[n++] = Pick(LexemeId.Again, LexemeId.Seen, LexemeId.Remember);
            }
            else
            {
                if (n < tokens.Length) tokens[n++] = Pick(LexemeId.Remember, LexemeId.Known, LexemeId.Seen);
            }

            ushort m = ctx.ReplayRegionMask;

            // Visual cortex present → visual words.
            if ((m & (1 << 9)) != 0)
                if (n < tokens.Length) tokens[n++] = Pick(LexemeId.Seen, LexemeId.Bright, LexemeId.Shape, LexemeId.Move);

            // Auditory cortex present → auditory words.
            if ((m & (1 << 10)) != 0)
                if (n < tokens.Length) tokens[n++] = Pick(LexemeId.Sound, LexemeId.Noise, LexemeId.Tone, LexemeId.Loud, LexemeId.Listen);

            // Amygdala present → alert/danger/now.
            if ((m & (1 << 4)) != 0)
                if (n < tokens.Length) tokens[n++] = Pick(LexemeId.Alert, LexemeId.Danger, LexemeId.Now);

            // Motor cortex present → now/stop (very primitive).
            if ((m & (1 << 11)) != 0)
                if (n < tokens.Length) tokens[n++] = Pick(LexemeId.Now, LexemeId.Stop);
        }

// Urgency/arousal primitives
        if (ur >= 0.78f || ar >= 0.78f)
            tokens[n++] = Pick(LexemeId.Alert, LexemeId.Now, LexemeId.Look, LexemeId.Listen);

        // Memory cue
        if (ctx.MemoryHit)
            tokens[n++] = Pick(LexemeId.Remember, LexemeId.Again, LexemeId.Seen, LexemeId.Known);

        // Uncertainty
        if (ce <= 0.42f)
            tokens[n++] = Pick(LexemeId.Unknown, LexemeId.Unsure, LexemeId.What, LexemeId.Why);

        // Valence
        if (va >= 0.25f)
            tokens[n++] = Pick(LexemeId.Good, LexemeId.Yes, LexemeId.Safe, LexemeId.Nice);
        else if (va <= -0.25f)
            tokens[n++] = Pick(LexemeId.Bad, LexemeId.No, LexemeId.Danger, LexemeId.Stop);

        // Sensory grounding
        if (ctx.VisualIntensity01 >= 0.62f)
            tokens[n++] = Pick(LexemeId.Bright, LexemeId.Seen, LexemeId.Shape, LexemeId.Move);

        if (ctx.AuditoryIntensity01 >= 0.62f)
            tokens[n++] = Pick(LexemeId.Loud, LexemeId.Noise, LexemeId.Tone, LexemeId.Sound);

        // If nothing triggered, emit a minimal "alive" token.
        if (n == 0)
            tokens[n++] = Pick(LexemeId.Here, LexemeId.Ok, LexemeId.Idle);

        LightShuffle(tokens, n);

        int take = Math.Min(maxTokens, Math.Min(n, buffer.Length));
        for (int i = 0; i < take; i++)
            buffer[i] = tokens[i];

        return take;
    }

    private LexemeId Pick(params LexemeId[] options)
        => options.Length == 0 ? LexemeId.None : options[_rng.Next(options.Length)];

    private void LightShuffle(Span<LexemeId> tokens, int n)
    {
        // Fisher–Yates.
        for (int i = n - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (tokens[i], tokens[j]) = (tokens[j], tokens[i]);
        }
    }
}
