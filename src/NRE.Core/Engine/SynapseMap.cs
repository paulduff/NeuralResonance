namespace NRE.Core.Engine;

/// <summary>
/// Sparse connectivity: preIndex -> list of synapses to postIndex (with delay).
/// Includes STP (facilitation/depression), explicit pre/post-synaptic plasticity,
/// and Hebbian-lite updates.
/// OPTIMIZED: Incremental STP recovery to avoid full-map iteration every step.
/// </summary>
public sealed class SynapseMap
{
    public sealed class Synapse
    {
        public int Post;
        public int DelayTicks;

        public float W;

        public float Facil = 1.0f;
        public float Depr = 1.0f;
        public float PreRelease = 1.0f;
        public float PostSensitivity = 1.0f;
        public float PreTrace = 0.0f;
        public float PostTrace = 0.0f;
        public long LastPlasticityStep = 0;

        // Structural plasticity (Entry 021 / developmental pruning)
        public float UsageEma01 = 0.0f; // 0..1

        public Synapse(int post, int delayTicks, float w)
        {
            Post = post;
            DelayTicks = delayTicks;
            W = w;
        }
    }

    private readonly Dictionary<int, List<Synapse>> _out = new();
    private readonly Dictionary<int, List<Synapse>> _in = new();
    
    // Track recently-activated synapses for targeted STP recovery
    private readonly HashSet<Synapse> _recentlyActivated = new(1024);
    private int _recoveryBatchIndex;

    public IEnumerable<int> Pres => _out.Keys;

    /// <summary>Total number of synapses across all presynaptic neurons.</summary>
    public int TotalSynapses => _totalSynapses;
    private int _totalSynapses;

    /// <summary>
    /// Clears all synapses and STP tracking state.
    /// Used when rebuilding tracts (e.g., corpus callosum) during reinitialization.
    /// </summary>
    public void Clear()
    {
        _out.Clear();
        _in.Clear();
        _recentlyActivated.Clear();
        _recoveryBatchIndex = 0;
        _allSynapsesList = null;
        _allSynapsesListDirty = true;
        _totalSynapses = 0;
    }
    
    // Expose flat list for batch recovery (built once at init)
    private List<Synapse>? _allSynapsesList;
    private bool _allSynapsesListDirty = true;

    public void Add(int pre, int post, int delayTicks, float w)
    {
        if (!_out.TryGetValue(pre, out var list))
        {
            list = new List<Synapse>(8);
            _out[pre] = list;
        }
        var syn = new Synapse(post, delayTicks, w);
        list.Add(syn);
        if (!_in.TryGetValue(post, out var inList))
        {
            inList = new List<Synapse>(8);
            _in[post] = inList;
        }
        inList.Add(syn);
        _totalSynapses++;
        _allSynapsesListDirty = true;
    }

    public List<Synapse>? GetOutgoing(int pre)
        => _out.TryGetValue(pre, out var list) ? list : null;

    public List<Synapse>? GetIncoming(int post)
        => _in.TryGetValue(post, out var list) ? list : null;

    /// <summary>Compute aggregate weight/usage statistics across all synapses.</summary>
    public (float meanW, float stdW, float meanUsage, float maxAbsW, float skew, int activeCount) GetStats(float usageThreshold = 0.05f)
    {
        float sumW = 0, sumW2 = 0, sumUsage = 0, maxAbs = 0;
        int n = 0, active = 0;
        foreach (var kv in _out)
        {
            foreach (var s in kv.Value)
            {
                float absW = MathF.Abs(s.W);
                sumW += s.W;
                sumW2 += s.W * s.W;
                sumUsage += s.UsageEma01;
                if (absW > maxAbs) maxAbs = absW;
                if (s.UsageEma01 > usageThreshold) active++;
                n++;
            }
        }
        if (n == 0) return (0, 0, 0, 0, 0, 0);
        float mean = sumW / n;
        float variance = (sumW2 / n) - (mean * mean);
        float std = variance > 0 ? MathF.Sqrt(variance) : 0;
        // Skew: positive = more potentiated, negative = more depressed
        float skew = std > 0 ? mean / std : 0;
        return (mean, std, sumUsage / n, maxAbs, skew, active);
    }

    public void ApplyStpOnSpike(Synapse syn, NreEngineOptions opt, float dt)
    {
        syn.Facil = Math.Clamp(syn.Facil + opt.FacilitationGain, 0.2f, 2.0f);
        syn.Depr = Math.Clamp(syn.Depr - opt.DepressionGain, 0.05f, 1.0f);
        
        // Track for targeted recovery
        _recentlyActivated.Add(syn);
            // Usage is an activity proxy for structural survival
            syn.UsageEma01 = Math.Clamp(syn.UsageEma01 + opt.StructuralUsageBoostOnSpike, 0f, 1f);
    }

    /// <summary>
    /// OPTIMIZED: Two-phase STP recovery:
    /// 1) Fast recovery for recently-activated synapses (always)
    /// 2) Slow batch recovery for inactive synapses (incremental, spread across steps)
    /// </summary>
    public void RecoverStpAll(NreEngineOptions opt, float dt)
    {
        float rec = opt.StpRecoverPerSec * dt;
        float preRec = opt.PreSynapticRecoverPerSec * dt;
        float postRec = opt.PostSynapticRecoverPerSec * dt;
        
        // Phase 1: Recover recently-activated synapses (high priority, always done)
        foreach (var s in _recentlyActivated)
        {
            s.Facil = Lerp(s.Facil, 1.0f, rec);
            s.Depr = Lerp(s.Depr, 1.0f, rec);
            if (opt.EnablePrePostSynapticPlasticity)
            {
                s.PreRelease = Lerp(s.PreRelease, 1.0f, preRec);
                s.PostSensitivity = Lerp(s.PostSensitivity, opt.PostSynapticTargetSensitivity, postRec);
            }
        }
        _recentlyActivated.Clear();
        
        // Phase 2: Incremental batch recovery for all synapses (spread across ~16 steps)
        EnsureAllSynapsesList();
        if (_allSynapsesList == null || _allSynapsesList.Count == 0) return;
        
        int batchSize = Math.Max(1, _allSynapsesList.Count / 16);
        int start = _recoveryBatchIndex;
        int end = Math.Min(start + batchSize, _allSynapsesList.Count);
        
        // Use slightly slower recovery for background batch (multiplied by 16 to compensate for 1/16 frequency)
        float batchRec = rec * 0.5f;
        
        for (int i = start; i < end; i++)
        {
            var s = _allSynapsesList[i];
            // Only recover if not at baseline (avoid unnecessary writes)
            bool stpNeedsRecovery = MathF.Abs(s.Facil - 1.0f) > 0.001f || MathF.Abs(s.Depr - 1.0f) > 0.001f;
            bool prePostNeedsRecovery = opt.EnablePrePostSynapticPlasticity
                && (MathF.Abs(s.PreRelease - 1.0f) > 0.001f
                    || MathF.Abs(s.PostSensitivity - opt.PostSynapticTargetSensitivity) > 0.001f);
            if (stpNeedsRecovery || prePostNeedsRecovery)
            {
                s.Facil = Lerp(s.Facil, 1.0f, batchRec);
                s.Depr = Lerp(s.Depr, 1.0f, batchRec);
                if (opt.EnablePrePostSynapticPlasticity)
                {
                    float preBatchRec = preRec * 0.5f;
                    float postBatchRec = postRec * 0.5f;
                    s.PreRelease = Lerp(s.PreRelease, 1.0f, preBatchRec);
                    s.PostSensitivity = Lerp(s.PostSensitivity, opt.PostSynapticTargetSensitivity, postBatchRec);
                }
            }
        }
        
        _recoveryBatchIndex = end >= _allSynapsesList.Count ? 0 : end;

        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    }
    
    
    /// <summary>
    /// Entry 021 (biological next notch): Developmental pruning + synaptic competition.
    /// - UsageEma01 decays over time unless reinforced by spikes.
    /// - Weights decay toward 0 when unused.
    /// - Weak, unused synapses are removed.
    /// - Outgoing absolute weight budget enforced per pre neuron (competition).
    /// Returns number of pruned synapses.
    /// </summary>
    public int StructuralUpdate(
        NreEngineOptions opt,
        float dt,
        Func<int, float> outgoingAbsBudgetForPre,
        Func<int, byte>? preRegionForIndex = null,
        Func<int, byte>? postRegionForIndex = null,
        Func<byte, byte, bool>? isProtectedTract = null)
    {
        int pruned = 0;

        // 1) Usage decay and weight decay toward 0 (handled per-synapse in per-pre lists to allow pruning).
        float usageDecayT = Math.Clamp(opt.StructuralUsageDecayPerSec * dt, 0f, 1f);

        foreach (var kv in _out)
        {
            int pre = kv.Key;
            var list = kv.Value;
            if (list.Count == 0) continue;

            byte preRegion = 255;
            if (preRegionForIndex != null)
                preRegion = preRegionForIndex(pre);

            // Decay + prune pass (reverse loop to remove safely)
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var s = list[i];

                byte postRegion = 255;
                if (postRegionForIndex != null)
                    postRegion = postRegionForIndex(s.Post);

                bool protectedTract = isProtectedTract != null && preRegionForIndex != null && postRegionForIndex != null
                                      && isProtectedTract(preRegion, postRegion);

                // Usage decays toward 0
                if (!protectedTract)
                {
                    s.UsageEma01 = Lerp(s.UsageEma01, 0f, usageDecayT);
                }
                else
                {
                    // Hardening: protected tracts should not vanish under low stimulation.
                    s.UsageEma01 = Math.Max(s.UsageEma01, opt.StructuralProtectedUsageFloor01);
                }

                // Weight decays toward 0 in proportion to (1-usage)
                if (!protectedTract)
                {
                    float decayStrength = opt.StructuralWeightDecayPerSec * (1.0f - s.UsageEma01);
                    float k = Math.Clamp(decayStrength * dt, 0f, 0.25f); // stability guard
                    s.W = s.W * (1.0f - k);
                }

                // Prune if both weak and unused
                if (!protectedTract && MathF.Abs(s.W) < opt.StructuralPruneAbsWThreshold && s.UsageEma01 < opt.StructuralPruneUsageThreshold01)
                {
                    if (_in.TryGetValue(s.Post, out var inList))
                    {
                        inList.Remove(s);
                        if (inList.Count == 0)
                            _in.Remove(s.Post);
                    }
                    list.RemoveAt(i);
                    pruned++;
                    _totalSynapses--;
                    _allSynapsesListDirty = true;
                }
            }

            if (list.Count == 0)
                continue;

            // 2) Synaptic competition: enforce outgoing absolute weight budget per pre
            float budget = outgoingAbsBudgetForPre(pre);
            if (budget > 0f)
            {
                float sumAbs = 0f;
                for (int i = 0; i < list.Count; i++)
                    sumAbs += MathF.Abs(list[i].W);

                if (sumAbs > budget && sumAbs > 1e-6f)
                {
                    float scale = budget / sumAbs;
                    // Soft scaling to preserve relative structure
                    for (int i = 0; i < list.Count; i++)
                        list[i].W *= scale;
                }
            }

            // 3) Post-competition hardening: keep protected tracts above a minimal magnitude
            if (isProtectedTract != null && preRegionForIndex != null && postRegionForIndex != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var s = list[i];
                    byte postRegion = postRegionForIndex(s.Post);
                    if (isProtectedTract(preRegion, postRegion))
                    {
                        float abs = MathF.Abs(s.W);
                        if (abs < opt.StructuralProtectedMinAbsW)
                            s.W = MathF.Sign(s.W == 0f ? 1f : s.W) * opt.StructuralProtectedMinAbsW;
                    }
                }
            }
        }

        return pruned;

        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    }

    private void EnsureAllSynapsesList()
    {
        if (!_allSynapsesListDirty && _allSynapsesList != null) return;
        
        _allSynapsesList = new List<Synapse>();
        foreach (var kv in _out)
            _allSynapsesList.AddRange(kv.Value);
        _allSynapsesListDirty = false;
        _recoveryBatchIndex = 0;
    }

    private static void DecayStdpTracesToStep(Synapse syn, NreEngineOptions opt, long stepIndex, float dtPerStep)
    {
        if (!opt.EnableBiologicalStdpPlasticity)
        {
            syn.LastPlasticityStep = stepIndex;
            return;
        }

        long elapsedSteps = syn.LastPlasticityStep <= 0
            ? 1
            : Math.Max(1, stepIndex - syn.LastPlasticityStep);
        float elapsedSec = dtPerStep * elapsedSteps;

        float tauPre = MathF.Max(0.001f, opt.StdpTauPreSec);
        float tauPost = MathF.Max(0.001f, opt.StdpTauPostSec);

        float preDecay = MathF.Exp(-elapsedSec / tauPre);
        float postDecay = MathF.Exp(-elapsedSec / tauPost);
        syn.PreTrace *= preDecay;
        syn.PostTrace *= postDecay;
        syn.LastPlasticityStep = stepIndex;
    }

    public void ApplyPrePostPlasticityOnSpike(
        Synapse syn,
        NreEngineOptions opt,
        float preSpk,
        float postVm,
        long stepIndex,
        float dtPerStep,
        float hebbMod = 1.0f,
        float neuromodGate = 1.0f)
    {
        DecayStdpTracesToStep(syn, opt, stepIndex, dtPerStep);
        float effectiveGate = Math.Clamp(hebbMod * neuromodGate, 0.0f, 4.0f);

        float postTerm = MathF.Tanh(MathF.Max(0f, postVm));

        // Pair-based STDP on presynaptic spike: LTD proportional to recent postsynaptic trace.
        if (opt.EnableBiologicalStdpPlasticity)
        {
            float ltd = opt.StdpLtdRate * effectiveGate * preSpk * syn.PostTrace;
            if (syn.W >= 0f)
                syn.W = Math.Clamp(syn.W - ltd, opt.WeightMin, opt.WeightMax);
            else
                syn.W = Math.Clamp(syn.W + ltd * opt.StdpInhibitoryScale, opt.WeightMin, opt.WeightMax);

            syn.PreTrace = Math.Clamp(syn.PreTrace + opt.StdpTraceIncrement * preSpk, 0f, opt.StdpTraceMax);
        }

        if (!opt.EnablePrePostSynapticPlasticity) return;

        // Presynaptic term models release-probability adaptation based on pre-spike drive and postsynaptic success.
        float preDelta =
            (opt.PreSynapticPotentiationRate * preSpk * effectiveGate) -
            (opt.PreSynapticDepressionRate * preSpk * (1f - postTerm));
        syn.PreRelease = Math.Clamp(syn.PreRelease + preDelta, opt.PreSynapticMin, opt.PreSynapticMax);

        // Postsynaptic term models receptor/sensitivity adaptation with a homeostatic pull to target.
        float postDelta = opt.PostSynapticPotentiationRate * preSpk * postTerm * effectiveGate;
        float postHomeo = opt.PostSynapticHomeostasisRate * (syn.PostSensitivity - opt.PostSynapticTargetSensitivity);
        syn.PostSensitivity = Math.Clamp(
            syn.PostSensitivity + postDelta - postHomeo,
            opt.PostSynapticMin,
            opt.PostSynapticMax);
    }

    public void ApplyPostSpikePlasticity(
        int postIndex,
        NreEngineOptions opt,
        long stepIndex,
        float dtPerStep,
        float postSpk = 1.0f,
        float neuromodGate = 1.0f)
    {
        if (!_in.TryGetValue(postIndex, out var incoming) || incoming.Count == 0) return;

        float gate = Math.Clamp(neuromodGate, 0.0f, 4.0f);
        int count = incoming.Count;
        for (int i = 0; i < count; i++)
        {
            var syn = incoming[i];
            DecayStdpTracesToStep(syn, opt, stepIndex, dtPerStep);

            if (opt.EnableBiologicalStdpPlasticity)
            {
                float ltp = opt.StdpLtpRate * gate * postSpk * syn.PreTrace;
                if (syn.W >= 0f)
                    syn.W = Math.Clamp(syn.W + ltp, opt.WeightMin, opt.WeightMax);
                else
                    syn.W = Math.Clamp(syn.W - ltp * opt.StdpInhibitoryScale, opt.WeightMin, opt.WeightMax);

                syn.PostTrace = Math.Clamp(syn.PostTrace + opt.StdpTraceIncrement * postSpk, 0f, opt.StdpTraceMax);
            }
        }
    }

    public void HebbianLite(Synapse syn, NreEngineOptions opt, float preSpk, float postVm, float hebbMod = 1.0f)
    {
        float postTerm = MathF.Tanh(MathF.Max(0f, postVm));
        float prePostMod = opt.EnablePrePostSynapticPlasticity
            ? syn.PreRelease * syn.PostSensitivity
            : 1.0f;
        float dW = opt.HebbRate * hebbMod * prePostMod * preSpk * postTerm;

        syn.W = Math.Clamp(syn.W + dW, opt.WeightMin, opt.WeightMax);
    }
}
