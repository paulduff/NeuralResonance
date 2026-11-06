// Isolated numerical candidate for the EXISTING reduced excitatory cortical
// contact rule. Not selected by StructureEngine, persisted saves or any host.
// Double state and removal of per-update numerical dead zones do not constitute
// a new receptor/biochemical model. The legacy rate limiter remains provisional.
internal sealed record CorticalContactState(Guid ContactId, double TimeMs, double Weight, double Baseline,
    double PreTrace, double PostTrace, double Eligibility, double Tag, double Theta,
    int Updates, double Budget, double BudgetTimeMs, double ChargedPlasticity);

internal sealed class CorticalContactNumerics
{
    internal const string Version = "isolated-cortical-contact-double-v1";
    private readonly bool _pairChangesWeight;
    internal CorticalContactState State { get; private set; }

    internal CorticalContactNumerics(CorticalContactState initial, bool pairChangesWeight)
    {
        Require(initial); State = initial; _pairChangesWeight = pairChangesWeight;
    }

    internal static CorticalContactState FromLegacy(MemoryContactEvidence c)
    {
        if (c.Neurotransmitter != NeuralResonanceEngine.Protocol.NTEnum.GLUTAMATE || c.Synthetic)
            throw new ArgumentException("Only explicit nonsynthetic excitatory contacts are supported.");
        return new(c.ContactId, c.TimestampMs, c.Weight, c.Baseline, c.PreTrace, c.PostTrace,
            c.Eligibility, c.Tag, c.Theta, c.Updates, c.Budget, c.BudgetTimestampMs, c.AbsoluteChange);
    }

    internal void Spike(bool presynaptic, double timeMs)
    {
        var s = Decay(State, timeMs);
        double pair = presynaptic ? -(double).014f * s.PostTrace : (double).018f * s.PreTrace;
        s = s with { PreTrace = presynaptic ? Math.Min(8, s.PreTrace + 1) : s.PreTrace,
            PostTrace = presynaptic ? s.PostTrace : Math.Min(8, s.PostTrace + 1),
            Eligibility = Math.Clamp(s.Eligibility + pair, -1, 1), Tag = Math.Clamp(s.Tag + pair, -1, 1),
            Updates = Math.Min(int.MaxValue - 1, s.Updates) + 1 };
        Accept(Apply(s, _pairChangesWeight ? LocalDelta(pair, s.Weight) : 0));
    }

    internal void Modulate(double timeMs, double elapsedMs, double dopamine, double acetylcholine,
        double activity, bool captureEnabled = true)
    {
        if (!double.IsFinite(elapsedMs) || elapsedMs < 0 || !double.IsFinite(dopamine) ||
            !double.IsFinite(acetylcholine) || !double.IsFinite(activity))
            throw new ArgumentException("Finite modulation and nonnegative interval required.");
        double interval = Math.Min(elapsedMs, Math.Max(0, timeMs - State.TimeMs));
        var s = Decay(State, timeMs);
        double boundedActivity = Math.Clamp(activity, 0, 1);
        s = s with { Theta = Math.Clamp(s.Theta + (boundedActivity * boundedActivity - s.Theta) *
            (1 - Math.Exp(-interval / 2500)), (double).001f, 10) };
        double rate = 0;
        // Preserve the original concentration gate and receptor multiplier.
        // Only the numerical update-size cutoff is removed in this candidate.
        if (captureEnabled && acetylcholine > .001f)
            rate = LocalDelta(s.Tag, s.Weight) * .35f * Math.Clamp(acetylcholine, 0, 1) *
                ((double).35f + (double).65f * Math.Clamp(dopamine, 0, 1));
        double rawDelta = rate * (interval / 1000);
        if (rawDelta != 0) s = s with { Updates = Math.Min(int.MaxValue - 1, s.Updates) + 1 };
        Accept(Apply(s, rawDelta));
    }

    private static double LocalDelta(double tag, double weight)
    {
        tag = Math.Clamp(tag, -1, 1);
        double saturation = tag >= 0 ? 1 - weight / 5 : weight / 5;
        return Math.Clamp(tag * Math.Clamp(saturation, (double).05f, 1), -(double).05f, (double).05f);
    }

    private static CorticalContactState Decay(CorticalContactState s, double timeMs)
    {
        if (!double.IsFinite(timeMs) || timeMs < s.TimeMs)
            throw new ArgumentException("Finite monotonic contact time required.");
        double dt = timeMs - s.TimeMs;
        return s with { TimeMs = timeMs, PreTrace = s.PreTrace * Math.Exp(-dt / ContactPlasticity.PreTauMs),
            PostTrace = s.PostTrace * Math.Exp(-dt / ContactPlasticity.PostTauMs),
            Eligibility = s.Eligibility * Math.Exp(-dt / ContactPlasticity.EligibilityTauMs),
            Tag = s.Tag * Math.Exp(-dt / ContactPlasticity.TagTauMs) };
    }

    private static CorticalContactState Apply(CorticalContactState s, double rawDelta)
    {
        if (!double.IsFinite(rawDelta)) throw new InvalidOperationException("Non-finite plasticity increment.");
        if (rawDelta == 0) return s;
        double budget = s.Budget;
        if (s.BudgetTimeMs >= 0 && s.TimeMs > s.BudgetTimeMs)
            budget = Math.Min(PlasticityRules.PlasticityBurstCapacityQuanta,
                budget + (s.TimeMs - s.BudgetTimeMs) / 1000 * PlasticityRules.PlasticityRefillQuantaPerBiologicalSecond);
        double scaled = rawDelta * PlasticityRules.RestoredSpikeDensityLearningScale;
        double magnitude = Math.Min(Math.Abs(scaled), budget);
        double weight = Math.Clamp(s.Weight + Math.CopySign(magnitude, scaled), (double).05f, 5);
        return s with { Weight = weight, Budget = budget - magnitude, BudgetTimeMs = s.TimeMs,
            ChargedPlasticity = s.ChargedPlasticity + magnitude };
    }

    private void Accept(CorticalContactState next) { Require(next); State = next; }

    private static void Require(CorticalContactState s)
    {
        ArgumentNullException.ThrowIfNull(s);
        double[] values = [s.TimeMs, s.Weight, s.Baseline, s.PreTrace, s.PostTrace, s.Eligibility,
            s.Tag, s.Theta, s.Budget, s.BudgetTimeMs, s.ChargedPlasticity];
        if (s.ContactId == Guid.Empty || values.Any(x => !double.IsFinite(x)) || s.TimeMs < 0 ||
            s.Weight < (double).05f || s.Weight > 5 || s.Baseline < (double).05f || s.Baseline > 5 ||
            s.PreTrace is < 0 or > 8 || s.PostTrace is < 0 or > 8 || s.Eligibility is < -1 or > 1 || s.Tag is < -1 or > 1 ||
            s.Theta < (double).001f || s.Theta > 10 || s.Updates < 0 || s.Budget < 0 ||
            s.Budget > PlasticityRules.PlasticityBurstCapacityQuanta || s.BudgetTimeMs < -1 ||
            s.BudgetTimeMs > s.TimeMs || s.ChargedPlasticity < 0)
            throw new ArgumentException("Invalid explicit cortical contact state; no silent repair.");
    }
}
