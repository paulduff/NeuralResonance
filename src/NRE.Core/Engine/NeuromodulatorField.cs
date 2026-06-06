namespace NRE.Core.Engine;

/// <summary>
/// Diffuse neuromodulator field.
/// Folded Archive Entry 018 requires these to be biologically sourced (subcortical/regulatory),
/// not directly set as absolute UI knobs.
///
/// Values are clamped to [0..1] and evolve continuously via decay + internal pulses/deltas.
/// </summary>
public sealed class NeuromodulatorField
{
    // Internal state (0..1)
    public float Dopamine { get; private set; } = 0.0f;
    public float Serotonin { get; private set; } = 0.0f;
    public float Noradrenaline { get; private set; } = 0.0f;

    public NeuromodulatorDto Snapshot() => new(Dopamine, Serotonin, Noradrenaline);

    /// <summary>Direct set for state restoration (save/load). Not for normal use.</summary>
    internal void Set(float da, float ht, float ne)
    {
        Dopamine = Clamp01(da);
        Serotonin = Clamp01(ht);
        Noradrenaline = Clamp01(ne);
    }

    // --- Dynamics ---

    // Half-lives (seconds). These are deliberately slow vs sensory transients.
    public float DopamineHalfLifeSec { get; set; } = 18.0f;
    public float SerotoninHalfLifeSec { get; set; } = 35.0f;
    public float NoradrenalineHalfLifeSec { get; set; } = 2.0f; // fast pulse/decay

    /// <summary>
    /// Global decay step.
    /// Must be called once per engine step.
    /// </summary>
    internal void StepDecay(float dt)
    {
        Dopamine = DecayTowardZero(Dopamine, DopamineHalfLifeSec, dt);
        Serotonin = DecayTowardZero(Serotonin, SerotoninHalfLifeSec, dt);
        Noradrenaline = DecayTowardZero(Noradrenaline, NoradrenalineHalfLifeSec, dt);
    }

    /// <summary>
    /// Apply a small dopamine delta (positive or negative), scaled by dt.
    /// Intended sources: prediction-error, reward-trace surrogates.
    /// </summary>
    internal void ApplyDopamineDelta(float deltaPerSec, float dt)
    {
        Dopamine = Clamp01(Dopamine + deltaPerSec * dt);
    }

    /// <summary>
    /// Apply a small serotonin delta (positive or negative), scaled by dt.
    /// Intended sources: stability / settling surrogate.
    /// </summary>
    internal void ApplySerotoninDelta(float deltaPerSec, float dt)
    {
        Serotonin = Clamp01(Serotonin + deltaPerSec * dt);
    }

    /// <summary>
    /// Apply a noradrenaline pulse (attention capture). This is a fast, transient broadcast.
    /// Intended sources: Amygdala salience events.
    /// </summary>
    internal void ApplyNoradrenalinePulse(float strength01)
    {
        if (strength01 <= 0f) return;
        Noradrenaline = Clamp01(MathF.Max(Noradrenaline, strength01));
    }

    private static float DecayTowardZero(float value01, float halfLifeSec, float dt)
    {
        if (value01 <= 0f) return 0f;
        if (halfLifeSec <= 0.001f) return 0f;

        // Exponential decay using half-life: v *= 0.5^(dt/halfLife)
        float k = MathF.Pow(0.5f, dt / halfLifeSec);
        float v = value01 * k;
        return v < 1e-6f ? 0f : v;
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
