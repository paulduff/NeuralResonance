using System.Collections.Concurrent;

namespace NRE.Core.Engine;

/// <summary>
/// Vocal motor output (biology-inspired scaffold).
///
/// This module intentionally keeps "what to say" separate from "whether/when to vocalize".
/// Higher systems (salience, sleep transitions, later: Wernicke/Broca) can request an utterance.
/// The vocal motor then decides if it will execute that request, optionally gated by action
/// selection (Basal Ganglia).
/// </summary>
public sealed class VocalMotor
{
    // Reserve channel 0 for vocalization (future: make configurable).
    public const int VocalizeChannel = 0;

    public readonly record struct VoiceDrive(
        float Arousal01,
        float Urgency01,
        float Valence11,
        float Certainty01);

    private readonly ConcurrentQueue<NreEngine.VoiceUtterance> _queue = new();
    private long _nextAllowedStep;

    // === Vocal governor (Option 4) ===
    private long _lastUpdateStep;
    private float _energy01 = 1.0f; // "metabolic" budget for vocalization
    private long _refractoryUntilStep;
	private ulong _lastSpokenSig;
    private long _lastSpokenStep = -10_000_000;


	private PendingPlan? _pending;

    private readonly Random _rng;

    // Concurrency: simulation thread updates + API thread may force-say.
    private readonly object _gate = new();

    // Tunables (settable at runtime via API)
    public long CooldownSteps { get; set; } = 30; // ~1s at 60Hz
    public float BgConfidenceThreshold { get; set; } = 0.30f;

    // Governor knobs
    private const float EnergyRegenPerStep = 0.0042f; // ~4s from empty to full at 60Hz
    public long RepeatBlockSteps { get; set; } = 260; // ~4.3s

    private sealed record PendingPlan(LexemeId[] Tokens, int Count, ulong Signature, VoiceDrive Drive, long ExpiresStep);

    public VocalMotor(Random rng)
        => _rng = rng;

    /// <summary>
    /// Request speech. This does NOT necessarily enqueue immediately (it becomes an intention).
    /// </summary>
    public void RequestUtterance(ReadOnlySpan<LexemeId> tokens, int count, VoiceDrive drive, long stepIndex, long ttlSteps = 40)
    {
        if (count <= 0) return;
        count = Math.Clamp(count, 1, Math.Min(tokens.Length, 32));
        drive = Sanitize(drive);

        var expires = stepIndex + Math.Max(10, ttlSteps);

        // Copy tokens into a private array (intention may survive across ticks).
        var copy = new LexemeId[count];
        for (int i = 0; i < count; i++) copy[i] = tokens[i];
        ulong sig = HashTokens(copy);

        lock (_gate)
        {
            // If an intention already exists, keep the more urgent one.
            var cur = _pending;
            if (cur == null || drive.Urgency01 > cur.Drive.Urgency01 + 0.05f)
                _pending = new PendingPlan(copy, count, sig, drive, expires);
        }
    }

    /// <summary>
    /// Force speech enqueue (external/manual). Still obeys queue bounds, but bypasses BG gating.
    /// </summary>
    public void ForceSpeak(string text, VoiceDrive drive, long stepIndex)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        drive = Sanitize(drive);

        lock (_gate)
        {
            TickUnsafe(stepIndex);

            // Allow forced speech even during cooldown, but don't spam: apply a short 0.6s throttle.
            if (stepIndex < _nextAllowedStep - 70) return;

            EnqueueText(text.Trim(), drive, stepIndex);
        }
    }

    /// <summary>
    /// Update execution of pending intention. If BG is enabled, speech is treated as an action
    /// (VocalizeChannel) that must be selected. If BG is disabled, urgency alone can trigger speech.
    /// </summary>
    public void Update(
        long stepIndex,
        bool isAwake,
        bool basalGangliaEnabled,
        int selectedChannel,
        float selectionConfidence)
    {
        if (!isAwake)
        {
            // Preserve intention briefly (e.g., waking transition), but don't speak while asleep.
            return;
        }

        lock (_gate)
        {
            TickUnsafe(stepIndex);

            // Expire stale intentions.
            if (_pending != null && stepIndex > _pending.ExpiresStep)
                _pending = null;

            if (_pending == null) return;
            if (stepIndex < Math.Max(_nextAllowedStep, _refractoryUntilStep)) return;

            var p = _pending;

            bool allowed;
            if (basalGangliaEnabled)
            {
                allowed = selectedChannel == VocalizeChannel && selectionConfidence >= BgConfidenceThreshold;
                // Allow very high urgency to break through, but still require some confidence.
                if (!allowed && p.Drive.Urgency01 >= 0.92f && selectionConfidence >= 0.45f)
                    allowed = true;
            }
            else
            {
                allowed = p.Drive.Urgency01 >= 0.22f;
            }

            if (!allowed) return;

            EnqueuePlan(p.Tokens, p.Count, p.Signature, p.Drive, stepIndex);
            _pending = null;
        }
    }

    public NreEngine.VoiceUtterance[] Dequeue(int max)
    {
        max = Math.Clamp(max, 1, 32);
        var list = new List<NreEngine.VoiceUtterance>(max);
        while (list.Count < max && _queue.TryDequeue(out var v))
            list.Add(v);
        return list.ToArray();
    }

    /// <summary>Returns urgency of the current pending intention (0 if none).</summary>
    public float GetPendingUrgency01()
    {
        lock (_gate)
            return _pending?.Drive.Urgency01 ?? 0f;
    }

    private void TickUnsafe(long stepIndex)
    {
        // Regen energy based on elapsed steps (works even if Update isn't called every single step).
        if (stepIndex <= _lastUpdateStep) return;
        long ds = stepIndex - _lastUpdateStep;
        _lastUpdateStep = stepIndex;

        _energy01 = Math.Clamp(_energy01 + ds * EnergyRegenPerStep, 0f, 1f);
    }

    private void EnqueuePlan(LexemeId[] tokens, int count, ulong signature, VoiceDrive drive, long stepIndex)
    {
        // Novelty gate: avoid immediate repetitions unless urgency is high.
        if (signature == _lastSpokenSig
            && (stepIndex - _lastSpokenStep) < RepeatBlockSteps
            && drive.Urgency01 < 0.90f)
        {
            return;
        }

        var spoken = LexemePhonology.Compose(tokens.AsSpan(0, count));
        string text = spoken.SurfaceText;
        string gloss = spoken.GlossText;
        var phonemes = spoken.Phonemes;
        if (string.IsNullOrWhiteSpace(text) && (phonemes == null || phonemes.Length == 0)) return;


        // Bound queue.
        if (_queue.Count > 64)
        {
            while (_queue.TryDequeue(out _))
            {
                if (_queue.Count <= 16) break;
            }
        }

        // Prosody mapping (simple, stable, debuggable).
        float ar = drive.Arousal01;
        float ur = drive.Urgency01;
        float va = Math.Clamp(drive.Valence11, -1f, 1f);
        float ce = drive.Certainty01;

        // Rate increases with urgency + arousal, decreases with low certainty.
        float rate = 0.86f + 0.30f * ar + 0.22f * ur - 0.10f * (1f - ce);

        // Pitch rises with arousal and (slightly) positive valence.
        float pitch = 0.88f + 0.22f * ar + 0.06f * (va * 0.5f + 0.5f);

        // Volume driven by urgency and arousal.
        float volume = 0.72f + 0.22f * ur + 0.12f * ar;

        // Energy budget: speaking costs energy (metabolic proxy). High-urgency can break through.
        float cost = 0.18f + 0.008f * Math.Clamp(text.Length, 1, 200) + 0.10f * volume;
        cost = Math.Clamp(cost, 0.12f, 0.95f);

        if (_energy01 < cost && drive.Urgency01 < 0.95f)
            return;

        // Spend energy (clamp so it never goes negative).
        _energy01 = Math.Max(0f, _energy01 - cost);


        // Tiny deterministic-ish jitter so repeated phrases don't sound identical.
        float jitter = (float)(_rng.NextDouble() * 0.04 - 0.02);
        rate = Math.Clamp(rate + jitter, 0.70f, 1.45f);
        pitch = Math.Clamp(pitch + jitter, 0.70f, 1.40f);
        volume = Math.Clamp(volume, 0.55f, 1.00f);

        _queue.Enqueue(new NreEngine.VoiceUtterance(stepIndex, text, rate, pitch, volume, gloss, phonemes));
        _lastSpokenSig = signature;
        _lastSpokenStep = stepIndex;

        _nextAllowedStep = stepIndex + CooldownSteps;
        // Refractory grows slightly with utterance length.
        _refractoryUntilStep = Math.Max(_refractoryUntilStep, stepIndex + CooldownSteps / 2 + (long)Math.Clamp(text.Length, 4, 80));
    }

    private void EnqueueText(string text, VoiceDrive drive, long stepIndex)
    {
        // For external/manual speech only. Compute a signature from text.
        ulong sig = HashText(text);

        // Adapt same novelty gate.
        if (sig == _lastSpokenSig
            && (stepIndex - _lastSpokenStep) < RepeatBlockSteps
            && drive.Urgency01 < 0.90f)
            return;

        // Route through plan enqueue path for prosody + governor.
        // Use a single fake token signature; the actual emitted text stays as provided.
        // (This keeps manual testing ergonomic.)
        EnqueueTextInternal(text, sig, drive, stepIndex);
    }

    private void EnqueueTextInternal(string text, ulong signature, VoiceDrive drive, long stepIndex)
    {
        // Bound queue.
        if (_queue.Count > 64)
        {
            while (_queue.TryDequeue(out _))
            {
                if (_queue.Count <= 16) break;
            }
        }

        float ar = drive.Arousal01;
        float ur = drive.Urgency01;
        float va = Math.Clamp(drive.Valence11, -1f, 1f);
        float ce = drive.Certainty01;

        float rate = 0.86f + 0.30f * ar + 0.22f * ur - 0.10f * (1f - ce);
        float pitch = 0.88f + 0.22f * ar + 0.06f * (va * 0.5f + 0.5f);
        float volume = 0.72f + 0.22f * ur + 0.12f * ar;

        float cost = 0.18f + 0.008f * Math.Clamp(text.Length, 1, 200) + 0.10f * volume;
        cost = Math.Clamp(cost, 0.12f, 0.95f);

        if (_energy01 < cost && drive.Urgency01 < 0.95f)
            return;

        _energy01 = Math.Max(0f, _energy01 - cost);

        float jitter = (float)(_rng.NextDouble() * 0.04 - 0.02);
        rate = Math.Clamp(rate + jitter, 0.70f, 1.45f);
        pitch = Math.Clamp(pitch + jitter, 0.70f, 1.40f);
        volume = Math.Clamp(volume, 0.55f, 1.00f);

        string gloss = text;
        string[] phonemes = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(word => DiphthongVocalTract.GraphemeToPhoneme(word).Concat(new[] { "_" }))
            .ToArray();

        _queue.Enqueue(new NreEngine.VoiceUtterance(stepIndex, text, rate, pitch, volume, gloss, phonemes));
        _lastSpokenSig = signature;
        _lastSpokenStep = stepIndex;

        _nextAllowedStep = stepIndex + CooldownSteps;
        _refractoryUntilStep = Math.Max(_refractoryUntilStep, stepIndex + CooldownSteps / 2 + (long)Math.Clamp(text.Length, 4, 80));
    }

    private static ulong HashTokens(LexemeId[] tokens)
    {
        // FNV-1a 64-bit.
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong h = offset;
        for (int i = 0; i < tokens.Length; i++)
        {
			h ^= (ulong)tokens[i];
            h *= prime;
        }
        return h;
    }

    private static ulong HashText(string text)
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong h = offset;
        for (int i = 0; i < text.Length; i++)
        {
            h ^= text[i];
            h *= prime;
        }
        return h;
    }

    private static VoiceDrive Sanitize(VoiceDrive d)
        => new(
            Arousal01: Math.Clamp(d.Arousal01, 0f, 1f),
            Urgency01: Math.Clamp(d.Urgency01, 0f, 1f),
            Valence11: Math.Clamp(d.Valence11, -1f, 1f),
            Certainty01: Math.Clamp(d.Certainty01, 0f, 1f));
}


