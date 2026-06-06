namespace NRE.Core.Engine;

/// <summary>
/// Brainstem Modulation Systems (replaces PonsNucleus)
/// 
/// ANATOMICAL STRUCTURE (Steriade & McCarley 2005, Jones 2003):
/// 
/// LOCUS COERULEUS (LC) - Noradrenaline
///   - Located in dorsal pons
///   - Diffuse projections to entire cortex
///   - Arousal, attention, stress response
///   - High during waking, low during REM
/// 
/// RAPHE NUCLEI - Serotonin (5-HT)
///   - Midline brainstem nuclei
///   - Mood, impulse control, satiety
///   - High during quiet waking, low during REM
/// 
/// CHOLINERGIC NUCLEI (PPT/LDT)
///   - Pedunculopontine and laterodorsal tegmental
///   - Acetylcholine → REM sleep, attention
///   - High during waking AND REM
/// 
/// VENTRAL TEGMENTAL AREA (VTA) - Dopamine
///   - Reward, motivation, learning
///   - Modulates via mesolimbic/mesocortical pathways
/// </summary>
public sealed class BrainstemModulation
{
    private readonly object _gate = new();
    
    // === NEUROMODULATORY NUCLEI ===
    private readonly LocusCoeruleus _lc;
    private readonly RapheNuclei _raphe;
    private readonly CholinergicNuclei _cholinergic;
    private readonly VentralTegmentalArea _vta;
    
    // Legacy PonsNucleus interface
    public float Arousal01 { get; private set; } = 0.35f;
    public float Stability01 { get; private set; } = 0.45f;
    public float ResetPressure01 { get; private set; } = 0.10f;
    public float ThetaHz { get; private set; } = 4.0f;
    public bool HomeostasisEnabled { get; private set; } = true;
    
    public BrainstemModulation()
    {
        _lc = new LocusCoeruleus();
        _raphe = new RapheNuclei();
        _cholinergic = new CholinergicNuclei();
        _vta = new VentralTegmentalArea();
    }
    
    public BrainstemState Snapshot()
    {
        lock (_gate)
            return new BrainstemState(
                Arousal01, Stability01, ResetPressure01, ThetaHz,
                _lc.GetOutput(), _raphe.GetOutput(), _cholinergic.GetOutput(), _vta.GetOutput());
    }
    
    // Legacy PonsNucleus interface for backward compatibility
    public PonsDto SnapshotLegacy()
    {
        lock (_gate)
            return new PonsDto(Arousal01, Stability01, ResetPressure01, ThetaHz);
    }
    
    public void Set(float arousal01, float stability01, float resetPressure01, float thetaHz)
    {
        lock (_gate)
        {
            Arousal01 = Math.Clamp(arousal01, 0f, 1f);
            Stability01 = Math.Clamp(stability01, 0f, 1f);
            ResetPressure01 = Math.Clamp(resetPressure01, 0f, 1f);
            ThetaHz = Math.Clamp(thetaHz, 0.1f, 12.0f);
        }
    }
    
    public void SetHomeostasis(bool enabled)
    {
        lock (_gate) HomeostasisEnabled = enabled;
    }
    
    /// <summary>
    /// Step all brainstem nuclei based on current brain state.
    /// </summary>
    public BrainstemOutput Step(float dt, SleepPhase sleepPhase, float corticalActivity)
    {
        lock (_gate)
        {
            // Update each nucleus based on sleep/wake state
            float lcOutput = _lc.Step(dt, sleepPhase, corticalActivity);
            float rapheOutput = _raphe.Step(dt, sleepPhase);
            float achOutput = _cholinergic.Step(dt, sleepPhase);
            float daOutput = _vta.Step(dt, corticalActivity);
            
            // Map to legacy arousal/stability
            Arousal01 = lcOutput * 0.6f + achOutput * 0.4f;
            Stability01 = rapheOutput * 0.7f + (1.0f - lcOutput) * 0.3f;
            
            // Theta frequency varies with state
            ThetaHz = sleepPhase switch
            {
                SleepPhase.Awake => 6.0f + lcOutput * 2.0f,
                SleepPhase.Nrem => 2.0f + rapheOutput,
                SleepPhase.Rem => 5.0f + achOutput * 3.0f, // Theta prominent in REM
                _ => 4.0f
            };
            
            return new BrainstemOutput(
                NoradrenalineLevel: lcOutput,
                SerotoninLevel: rapheOutput,
                AcetylcholineLevel: achOutput,
                DopamineLevel: daOutput,
                Arousal01: Arousal01,
                ThetaHz: ThetaHz);
        }
    }
    
    // Legacy interface
    public ModulationPacket StepAndEmit(float dt)
    {
        lock (_gate)
            return new ModulationPacket(Arousal01, Stability01, ResetPressure01, ThetaHz);
    }
    
    public void ApplyHomeostasis(float measuredDensity01, float target01, float deadband01, float rate)
    {
        lock (_gate)
        {
            if (!HomeostasisEnabled) return;
            
            float err = measuredDensity01 - target01;
            if (MathF.Abs(err) <= deadband01) return;
            
            float e = Math.Clamp(err / Math.Max(1e-6f, target01), -2.0f, 2.0f);
            
            if (e > 0f)
            {
                Stability01 = Math.Clamp(Stability01 + rate * (0.55f * e), 0f, 1f);
                ResetPressure01 = Math.Clamp(ResetPressure01 + rate * (0.22f * e), 0f, 1f);
                Arousal01 = Math.Clamp(Arousal01 - rate * (0.20f * e), 0f, 1f);
            }
            else
            {
                float ne = -e;
                Arousal01 = Math.Clamp(Arousal01 + rate * (0.60f * ne), 0f, 1f);
                Stability01 = Math.Clamp(Stability01 - rate * (0.18f * ne), 0f, 1f);
                ResetPressure01 = Math.Clamp(ResetPressure01 - rate * (0.10f * ne), 0f, 1f);
            }
        }
    }
    
    // === INNER CLASSES: Brainstem Nuclei ===
    
    private sealed class LocusCoeruleus
    {
        private float _output = 0.5f;
        
        public float Step(float dt, SleepPhase phase, float corticalActivity)
        {
            // LC is HIGH during waking (especially stress), LOW during REM
            float target = phase switch
            {
                SleepPhase.Awake => 0.5f + corticalActivity * 0.3f,
                SleepPhase.Nrem => 0.2f,
                SleepPhase.Rem => 0.05f, // Nearly silent in REM
                _ => 0.5f
            };
            _output += (target - _output) * 0.1f * dt * 10f;
            return Math.Clamp(_output, 0f, 1f);
        }
        
        public float GetOutput() => _output;
    }
    
    private sealed class RapheNuclei
    {
        private float _output = 0.5f;
        
        public float Step(float dt, SleepPhase phase)
        {
            // Raphe is HIGH during quiet waking, LOW during REM
            float target = phase switch
            {
                SleepPhase.Awake => 0.6f,
                SleepPhase.Nrem => 0.3f,
                SleepPhase.Rem => 0.05f, // Nearly silent in REM
                _ => 0.5f
            };
            _output += (target - _output) * 0.1f * dt * 10f;
            return Math.Clamp(_output, 0f, 1f);
        }
        
        public float GetOutput() => _output;
    }
    
    private sealed class CholinergicNuclei
    {
        private float _output = 0.5f;
        
        public float Step(float dt, SleepPhase phase)
        {
            // ACh is HIGH during waking AND REM
            float target = phase switch
            {
                SleepPhase.Awake => 0.7f,
                SleepPhase.Nrem => 0.2f,
                SleepPhase.Rem => 0.8f, // High in REM!
                _ => 0.5f
            };
            _output += (target - _output) * 0.1f * dt * 10f;
            return Math.Clamp(_output, 0f, 1f);
        }
        
        public float GetOutput() => _output;
    }
    
    private sealed class VentralTegmentalArea
    {
        private float _output = 0.3f;
        
        public float Step(float dt, float rewardSignal)
        {
            float target = 0.3f + rewardSignal * 0.5f;
            _output += (target - _output) * 0.1f * dt * 10f;
            return Math.Clamp(_output, 0f, 1f);
        }
        
        public float GetOutput() => _output;
    }
}

// Keep PonsNucleus as alias for backward compatibility
public sealed class PonsNucleus
{
    private readonly BrainstemModulation _brainstem = new();
    
    public float Arousal01 => _brainstem.Arousal01;
    public float Stability01 => _brainstem.Stability01;
    public float ResetPressure01 => _brainstem.ResetPressure01;
    public float ThetaHz => _brainstem.ThetaHz;
    public bool HomeostasisEnabled => _brainstem.HomeostasisEnabled;
    
    public PonsDto Snapshot() => _brainstem.SnapshotLegacy();
    public void Set(float a, float s, float r, float t) => _brainstem.Set(a, s, r, t);
    public void SetHomeostasis(bool e) => _brainstem.SetHomeostasis(e);
    public ModulationPacket StepAndEmit(float dt) => _brainstem.StepAndEmit(dt);
    public void ApplyHomeostasis(float measuredDensity01, float target01, float deadband01, float rate) 
        => _brainstem.ApplyHomeostasis(measuredDensity01, target01, deadband01, rate);
}

public readonly record struct BrainstemState(
    float Arousal01, float Stability01, float ResetPressure01, float ThetaHz,
    float LCOutput, float RapheOutput, float CholinergicOutput, float VTAOutput);

public readonly record struct BrainstemOutput(
    float NoradrenalineLevel, float SerotoninLevel, float AcetylcholineLevel,
    float DopamineLevel, float Arousal01, float ThetaHz);

public readonly record struct PonsDto(float Arousal, float Stability, float ResetPressure, float ThetaHz);
