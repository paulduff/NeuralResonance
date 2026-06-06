namespace NRE.Core.Engine;

/// <summary>
/// Enhanced Neuromodulator System with receptor dynamics.
/// 
/// Biology:
/// - Tonic (baseline) vs Phasic (burst) release patterns
/// - Receptor desensitization from sustained activation
/// - Active reuptake and enzymatic degradation
/// - Multiple receptor subtypes with different effects
/// 
/// References:
/// - Grace et al. 2007 (DA tonic/phasic)
/// - Berridge & Robinson 1998 (DA and motivation)
/// - Aston-Jones & Cohen 2005 (LC-NE gain control)
/// - Cools et al. 2008 (5-HT and behavioral flexibility)
/// </summary>
public sealed class NeuromodulatorSystem
{
    private readonly object _gate = new();
    
    // === DOPAMINE SYSTEM (VTA/SNc → Cortex, Striatum) ===
    
    /// <summary>Tonic dopamine level (baseline motivation/vigor).</summary>
    public float DopamineTonic { get; private set; } = 0.15f;
    
    /// <summary>Phasic dopamine (reward prediction error signal).</summary>
    public float DopaminePhasic { get; private set; } = 0.0f;
    
    /// <summary>D1 receptor sensitivity (excitatory, Go pathway).</summary>
    public float D1Sensitivity { get; private set; } = 1.0f;
    
    /// <summary>D2 receptor sensitivity (inhibitory, NoGo pathway).</summary>
    public float D2Sensitivity { get; private set; } = 1.0f;
    
    // === NOREPINEPHRINE SYSTEM (Locus Coeruleus → Cortex) ===
    
    /// <summary>Tonic NE (arousal, vigilance baseline).</summary>
    public float NorepinephrineTonic { get; private set; } = 0.20f;
    
    /// <summary>Phasic NE (alerting, attention capture).</summary>
    public float NorepinephrinePhasic { get; private set; } = 0.0f;
    
    /// <summary>Alpha-2 receptor sensitivity (presynaptic autoreceptor).</summary>
    public float Alpha2Sensitivity { get; private set; } = 1.0f;
    
    /// <summary>Beta receptor sensitivity (postsynaptic, gain modulation).</summary>
    public float BetaSensitivity { get; private set; } = 1.0f;
    
    // === SEROTONIN SYSTEM (Raphe Nuclei → Cortex) ===
    
    /// <summary>Tonic serotonin (mood, behavioral inhibition).</summary>
    public float SerotoninTonic { get; private set; } = 0.25f;
    
    /// <summary>Phasic serotonin (aversive signals, patience).</summary>
    public float SerotoninPhasic { get; private set; } = 0.0f;
    
    /// <summary>5-HT1A receptor sensitivity (inhibitory, anxiolytic).</summary>
    public float HT1ASensitivity { get; private set; } = 1.0f;
    
    /// <summary>5-HT2A receptor sensitivity (excitatory, cognitive flexibility).</summary>
    public float HT2ASensitivity { get; private set; } = 1.0f;
    
    // === ACETYLCHOLINE SYSTEM (Basal Forebrain → Cortex) ===
    
    /// <summary>Tonic ACh (attention, learning mode).</summary>
    public float AcetylcholineTonic { get; private set; } = 0.20f;
    
    /// <summary>Phasic ACh (cue detection, memory encoding).</summary>
    public float AcetylcholinePhasic { get; private set; } = 0.0f;
    
    /// <summary>Muscarinic receptor sensitivity (slow, modulatory).</summary>
    public float MuscarinicSensitivity { get; private set; } = 1.0f;
    
    /// <summary>Nicotinic receptor sensitivity (fast, attention).</summary>
    public float NicotinicSensitivity { get; private set; } = 1.0f;
    
    // === DYNAMICS PARAMETERS ===
    
    // Phasic decay rates (fast)
    public float DopaminePhasicDecayHz { get; set; } = 4.0f;    // ~250ms
    public float NEPhasicDecayHz { get; set; } = 8.0f;          // ~125ms
    public float SerotoninPhasicDecayHz { get; set; } = 2.0f;   // ~500ms
    public float AChPhasicDecayHz { get; set; } = 5.0f;         // ~200ms
    
    // Tonic drift rates (very slow)
    public float TonicDriftRate { get; set; } = 0.01f;
    
    // Reuptake rates (active clearance)
    public float DopamineReuptakeRate { get; set; } = 0.8f;
    public float NEReuptakeRate { get; set; } = 1.2f;
    public float SerotoninReuptakeRate { get; set; } = 0.5f;
    public float AChDegradationRate { get; set; } = 2.0f;  // ACh uses enzymatic degradation
    
    // Desensitization rates
    public float ReceptorDesensitizationRate { get; set; } = 0.15f;
    public float ReceptorResensitizationRate { get; set; } = 0.02f;
    
    // Tonic setpoints (homeostatic targets)
    public float DopamineTonicSetpoint { get; set; } = 0.15f;
    public float NETonicSetpoint { get; set; } = 0.20f;
    public float SerotoninTonicSetpoint { get; set; } = 0.25f;
    public float AChTonicSetpoint { get; set; } = 0.20f;
    
    /// <summary>
    /// Main update step. Called on slow lane (~5Hz).
    /// </summary>
    public void Step(float dt, float arousalDrive, float stressDrive, float rewardSignal, float attentionDemand)
    {
        lock (_gate)
        {
            // === PHASIC DECAY (fast exponential) ===
            DopaminePhasic *= MathF.Exp(-DopaminePhasicDecayHz * dt);
            NorepinephrinePhasic *= MathF.Exp(-NEPhasicDecayHz * dt);
            SerotoninPhasic *= MathF.Exp(-SerotoninPhasicDecayHz * dt);
            AcetylcholinePhasic *= MathF.Exp(-AChPhasicDecayHz * dt);
            
            // Clamp near-zero
            if (DopaminePhasic < 0.001f) DopaminePhasic = 0f;
            if (NorepinephrinePhasic < 0.001f) NorepinephrinePhasic = 0f;
            if (SerotoninPhasic < 0.001f) SerotoninPhasic = 0f;
            if (AcetylcholinePhasic < 0.001f) AcetylcholinePhasic = 0f;
            
            // === TONIC MODULATION (homeostatic drift toward setpoint) ===
            float tonicRate = TonicDriftRate * dt;
            
            // Dopamine tonic rises with reward history, falls with stress
            float daTarget = DopamineTonicSetpoint + rewardSignal * 0.1f - stressDrive * 0.05f;
            DopamineTonic += (daTarget - DopamineTonic) * tonicRate;
            
            // NE tonic rises with arousal demand
            float neTarget = NETonicSetpoint + arousalDrive * 0.15f;
            NorepinephrineTonic += (neTarget - NorepinephrineTonic) * tonicRate;
            
            // Serotonin tonic inversely related to stress (stress depletes 5-HT)
            float htTarget = SerotoninTonicSetpoint - stressDrive * 0.10f;
            SerotoninTonic += (htTarget - SerotoninTonic) * tonicRate;
            
            // ACh tonic rises with attention demand
            float achTarget = AChTonicSetpoint + attentionDemand * 0.20f;
            AcetylcholineTonic += (achTarget - AcetylcholineTonic) * tonicRate;
            
            // Clamp tonics
            DopamineTonic = Math.Clamp(DopamineTonic, 0.05f, 0.50f);
            NorepinephrineTonic = Math.Clamp(NorepinephrineTonic, 0.05f, 0.60f);
            SerotoninTonic = Math.Clamp(SerotoninTonic, 0.05f, 0.50f);
            AcetylcholineTonic = Math.Clamp(AcetylcholineTonic, 0.05f, 0.50f);
            
            // === RECEPTOR DYNAMICS ===
            UpdateReceptorSensitivity(dt);
        }
    }
    
    private void UpdateReceptorSensitivity(float dt)
    {
        float desensRate = ReceptorDesensitizationRate * dt;
        float resensRate = ReceptorResensitizationRate * dt;
        
        // High DA desensitizes D1/D2 receptors
        float daTotal = DopamineTonic + DopaminePhasic;
        if (daTotal > 0.3f)
        {
            D1Sensitivity -= desensRate * (daTotal - 0.3f);
            D2Sensitivity -= desensRate * (daTotal - 0.3f) * 0.8f; // D2 slightly less affected
        }
        else
        {
            D1Sensitivity += resensRate * (1f - D1Sensitivity);
            D2Sensitivity += resensRate * (1f - D2Sensitivity);
        }
        
        // High NE desensitizes beta receptors (alpha-2 is autoreceptor, different dynamics)
        float neTotal = NorepinephrineTonic + NorepinephrinePhasic;
        if (neTotal > 0.4f)
        {
            BetaSensitivity -= desensRate * (neTotal - 0.4f);
            // Alpha-2 autoreceptor reduces release when NE is high
            Alpha2Sensitivity += desensRate * (neTotal - 0.4f) * 0.5f;
        }
        else
        {
            BetaSensitivity += resensRate * (1f - BetaSensitivity);
            Alpha2Sensitivity -= resensRate * (Alpha2Sensitivity - 1f) * 0.5f;
        }
        
        // 5-HT receptor dynamics
        float htTotal = SerotoninTonic + SerotoninPhasic;
        if (htTotal > 0.35f)
        {
            HT1ASensitivity -= desensRate * (htTotal - 0.35f);
            HT2ASensitivity -= desensRate * (htTotal - 0.35f) * 0.6f;
        }
        else
        {
            HT1ASensitivity += resensRate * (1f - HT1ASensitivity);
            HT2ASensitivity += resensRate * (1f - HT2ASensitivity);
        }
        
        // ACh receptor dynamics
        float achTotal = AcetylcholineTonic + AcetylcholinePhasic;
        if (achTotal > 0.35f)
        {
            MuscarinicSensitivity -= desensRate * (achTotal - 0.35f);
            NicotinicSensitivity -= desensRate * (achTotal - 0.35f) * 1.5f; // Nicotinic desensitizes faster
        }
        else
        {
            MuscarinicSensitivity += resensRate * (1f - MuscarinicSensitivity);
            NicotinicSensitivity += resensRate * (1f - NicotinicSensitivity);
        }
        
        // Clamp all sensitivities
        D1Sensitivity = Math.Clamp(D1Sensitivity, 0.2f, 1.5f);
        D2Sensitivity = Math.Clamp(D2Sensitivity, 0.2f, 1.5f);
        Alpha2Sensitivity = Math.Clamp(Alpha2Sensitivity, 0.5f, 2.0f);
        BetaSensitivity = Math.Clamp(BetaSensitivity, 0.2f, 1.5f);
        HT1ASensitivity = Math.Clamp(HT1ASensitivity, 0.2f, 1.5f);
        HT2ASensitivity = Math.Clamp(HT2ASensitivity, 0.2f, 1.5f);
        MuscarinicSensitivity = Math.Clamp(MuscarinicSensitivity, 0.2f, 1.5f);
        NicotinicSensitivity = Math.Clamp(NicotinicSensitivity, 0.1f, 1.5f);
    }
    
    // === PHASIC RELEASE TRIGGERS ===
    
    /// <summary>
    /// Trigger phasic dopamine release (reward prediction error).
    /// Positive = better than expected, Negative = worse than expected.
    /// </summary>
    public void TriggerDopaminePhasic(float rpe)
    {
        lock (_gate)
        {
            if (rpe > 0)
                DopaminePhasic = MathF.Min(1f, DopaminePhasic + rpe * 0.5f);
            else
                DopaminePhasic = MathF.Max(0f, DopaminePhasic + rpe * 0.3f); // Dips are smaller
        }
    }
    
    /// <summary>
    /// Trigger phasic NE release (alerting signal).
    /// </summary>
    public void TriggerNEPhasic(float intensity)
    {
        lock (_gate)
        {
            // Alpha-2 autoreceptor inhibits release when sensitivity is high
            float effectiveIntensity = intensity / Alpha2Sensitivity;
            NorepinephrinePhasic = MathF.Min(1f, NorepinephrinePhasic + effectiveIntensity * 0.4f);
        }
    }
    
    /// <summary>
    /// Trigger phasic serotonin (punishment/aversive signal or patience signal).
    /// </summary>
    public void TriggerSerotoninPhasic(float intensity)
    {
        lock (_gate)
        {
            SerotoninPhasic = MathF.Min(1f, SerotoninPhasic + intensity * 0.3f);
        }
    }
    
    /// <summary>
    /// Trigger phasic ACh (cue detection, memory encoding trigger).
    /// </summary>
    public void TriggerAChPhasic(float intensity)
    {
        lock (_gate)
        {
            AcetylcholinePhasic = MathF.Min(1f, AcetylcholinePhasic + intensity * 0.35f);
        }
    }
    
    // === EFFECTIVE LEVELS (combining tonic, phasic, and receptor sensitivity) ===
    
    /// <summary>Effective dopamine via D1 pathway (motivation, Go).</summary>
    public float EffectiveD1() => (DopamineTonic + DopaminePhasic) * D1Sensitivity;
    
    /// <summary>Effective dopamine via D2 pathway (inhibition, NoGo).</summary>
    public float EffectiveD2() => (DopamineTonic + DopaminePhasic) * D2Sensitivity;
    
    /// <summary>Effective NE (gain modulation).</summary>
    public float EffectiveNE() => (NorepinephrineTonic + NorepinephrinePhasic) * BetaSensitivity;
    
    /// <summary>Effective 5-HT1A (anxiolytic, inhibitory).</summary>
    public float Effective5HT1A() => (SerotoninTonic + SerotoninPhasic) * HT1ASensitivity;
    
    /// <summary>Effective 5-HT2A (flexibility, mildly excitatory).</summary>
    public float Effective5HT2A() => (SerotoninTonic + SerotoninPhasic) * HT2ASensitivity;
    
    /// <summary>Effective ACh (attention, learning rate).</summary>
    public float EffectiveACh() => (AcetylcholineTonic + AcetylcholinePhasic) * 
                                   (MuscarinicSensitivity + NicotinicSensitivity) * 0.5f;
    
    // === COMPUTED EFFECTS ===
    
    /// <summary>
    /// Get threshold modifier from neuromodulator state.
    /// Negative = easier to fire.
    /// </summary>
    public float GetThresholdMod()
    {
        float mod = 0f;
        
        // DA lowers threshold (increases excitability)
        mod -= EffectiveD1() * 0.08f;
        
        // NE lowers threshold (gain increase)
        mod -= EffectiveNE() * 0.06f;
        
        // 5-HT1A raises threshold (inhibitory)
        mod += Effective5HT1A() * 0.04f;
        
        // ACh lowers threshold (enhanced signal-to-noise)
        mod -= EffectiveACh() * 0.03f;
        
        return mod;
    }
    
    /// <summary>
    /// Get learning rate modifier from neuromodulator state.
    /// </summary>
    public float GetLearningRateMod()
    {
        float mod = 1.0f;
        
        // DA enhances learning for rewarded actions
        mod += EffectiveD1() * 0.5f;
        
        // ACh enhances memory encoding
        mod += EffectiveACh() * 0.4f;
        
        // NE enhances learning for salient events
        mod += EffectiveNE() * 0.3f;
        
        return Math.Clamp(mod, 0.5f, 3.0f);
    }
    
    /// <summary>
    /// Get gain modifier (signal amplification/contrast).
    /// </summary>
    public float GetGainMod()
    {
        // NE is the primary gain modulator
        float ne = EffectiveNE();
        
        // Inverted-U: optimal NE is ~0.3-0.4
        float gain = 1.0f;
        if (ne < 0.3f)
            gain = 0.7f + ne; // Low NE = low gain
        else if (ne > 0.5f)
            gain = 1.3f - (ne - 0.5f) * 0.6f; // High NE = reduced gain (stress impairs)
        else
            gain = 1.0f + (ne - 0.3f) * 0.5f; // Optimal range
            
        return Math.Clamp(gain, 0.5f, 1.5f);
    }
    
    /// <summary>
    /// Snapshot for UI/monitoring.
    /// </summary>
    public NeuromodulatorSystemSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new NeuromodulatorSystemSnapshot(
                DopamineTonic, DopaminePhasic, D1Sensitivity, D2Sensitivity,
                NorepinephrineTonic, NorepinephrinePhasic, Alpha2Sensitivity, BetaSensitivity,
                SerotoninTonic, SerotoninPhasic, HT1ASensitivity, HT2ASensitivity,
                AcetylcholineTonic, AcetylcholinePhasic, MuscarinicSensitivity, NicotinicSensitivity,
                GetThresholdMod(), GetLearningRateMod(), GetGainMod());
        }
    }
    
    /// <summary>Reset to baseline.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            DopamineTonic = DopamineTonicSetpoint;
            DopaminePhasic = 0f;
            D1Sensitivity = 1f;
            D2Sensitivity = 1f;
            
            NorepinephrineTonic = NETonicSetpoint;
            NorepinephrinePhasic = 0f;
            Alpha2Sensitivity = 1f;
            BetaSensitivity = 1f;
            
            SerotoninTonic = SerotoninTonicSetpoint;
            SerotoninPhasic = 0f;
            HT1ASensitivity = 1f;
            HT2ASensitivity = 1f;
            
            AcetylcholineTonic = AChTonicSetpoint;
            AcetylcholinePhasic = 0f;
            MuscarinicSensitivity = 1f;
            NicotinicSensitivity = 1f;
        }
    }
}

/// <summary>Complete snapshot of neuromodulator state.</summary>
public readonly record struct NeuromodulatorSystemSnapshot(
    float DopamineTonic, float DopaminePhasic, float D1Sensitivity, float D2Sensitivity,
    float NETonic, float NEPhasic, float Alpha2Sensitivity, float BetaSensitivity,
    float SerotoninTonic, float SerotoninPhasic, float HT1ASensitivity, float HT2ASensitivity,
    float AChTonic, float AChPhasic, float MuscarinicSensitivity, float NicotinicSensitivity,
    float EffectiveThresholdMod, float EffectiveLearningMod, float EffectiveGainMod);
