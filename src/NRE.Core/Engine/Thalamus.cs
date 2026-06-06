namespace NRE.Core.Engine;

/// <summary>
/// Thalamus: Gateway to Cortex with TRN, Relay Nuclei, and Tonic/Burst Modes
/// Biology: Jones 2007, Sherman & Guillery 2006, Crick 1984
/// </summary>
public sealed class Thalamus
{
    private readonly object _gate = new();
    
    // Core oscillator (40Hz gamma)
    public float FrequencyHz { get; private set; } = 40.0f;
    public float Phase { get; private set; }
    public float BindingWindowRadians { get; private set; } = 0.35f;
    public float BindingSpeedBoost { get; private set; } = 2.0f;
    public bool IsAtPulse { get; private set; }
    public float PulseAmplitude { get; private set; }
    
    private long _pulseCount;
    
    // Thalamic Reticular Nucleus (TRN) - GABAergic shell
    private float _trnInhibition;
    private float _trnActivity;
    
    // Relay nuclei activity
    private float _sensoryRelayGain = 1.0f;
    private float _motorRelayGain = 1.0f;
    
    // Firing mode
    private ThalamicMode _currentMode = ThalamicMode.Tonic;
    private float _membranePotential = -55f;
    
    // Sleep spindles
    private float _spindlePhase;
    private bool _inSpindle;
    private float _spindleAmplitude;
    
    public ThalamicState Snapshot()
    {
        lock (_gate)
            return new ThalamicState(FrequencyHz, Phase, IsAtPulse, PulseAmplitude, _pulseCount,
                _currentMode, _trnInhibition, _inSpindle, _spindleAmplitude);
    }
    
    public void Configure(float frequencyHz, float bindingWindowRadians, float speedBoost)
    {
        lock (_gate)
        {
            FrequencyHz = Math.Clamp(frequencyHz, 20f, 80f);
            BindingWindowRadians = Math.Clamp(bindingWindowRadians, 0.1f, 1.0f);
            BindingSpeedBoost = Math.Clamp(speedBoost, 1.0f, 4.0f);
        }
    }
    
    public void SetMode(ThalamicMode mode)
    {
        lock (_gate)
        {
            _currentMode = mode;
            _membranePotential = mode == ThalamicMode.Tonic ? -55f : -70f;
        }
    }
    
    /// <summary>
    /// Step the thalamic circuit with mode-dependent dynamics.
    /// </summary>
    public ThalamicPulse Step(float dt, SleepPhase sleepPhase = SleepPhase.Awake)
    {
        lock (_gate)
        {
            // Mode switching based on sleep state
            _currentMode = sleepPhase switch
            {
                SleepPhase.Awake => ThalamicMode.Tonic,
                SleepPhase.Nrem => ThalamicMode.Burst,
                SleepPhase.Rem => ThalamicMode.Tonic,
                _ => ThalamicMode.Tonic
            };
            
            float targetVm = _currentMode == ThalamicMode.Tonic ? -55f : -70f;
            _membranePotential += (targetVm - _membranePotential) * 0.1f * dt * 50f;
            
            // Oscillator dynamics (mode-dependent frequency)
            float effectiveFrequency = _currentMode == ThalamicMode.Tonic ? FrequencyHz : 
                (sleepPhase == SleepPhase.Nrem ? 12f : 4f);
            
            float prevPhase = Phase;
            Phase += dt * effectiveFrequency * 2f * MathF.PI;
            while (Phase >= 2f * MathF.PI) Phase -= 2f * MathF.PI;
            
            PulseAmplitude = MathF.Max(0f, MathF.Sin(Phase));
            
            float distFromPeak = MathF.Abs(Phase - MathF.PI * 0.5f);
            if (distFromPeak > MathF.PI) distFromPeak = 2f * MathF.PI - distFromPeak;
            IsAtPulse = _currentMode == ThalamicMode.Tonic && distFromPeak <= BindingWindowRadians;
            
            if (prevPhase < MathF.PI * 0.5f && Phase >= MathF.PI * 0.5f)
                _pulseCount++;
            
            // TRN dynamics - provides lateral inhibition between relay nuclei
            _trnActivity = _currentMode == ThalamicMode.Tonic ? 0.3f : 0.6f;
            _trnInhibition = _trnActivity * 0.5f;
            
            // Sleep spindles (NREM only)
            _inSpindle = false;
            _spindleAmplitude = 0f;
            if (sleepPhase == SleepPhase.Nrem)
            {
                _spindlePhase += dt * 13f * 2f * MathF.PI;
                while (_spindlePhase >= 2f * MathF.PI) _spindlePhase -= 2f * MathF.PI;
                float envelope = MathF.Sin(_pulseCount * 0.1f) * 0.5f + 0.5f;
                _spindleAmplitude = MathF.Sin(_spindlePhase) * envelope;
                _inSpindle = envelope > 0.3f;
            }
            
            // Relay gain modulation by TRN
            _sensoryRelayGain = 1.0f - _trnInhibition * 0.3f;
            _motorRelayGain = 1.0f - _trnInhibition * 0.2f;
            
            return new ThalamicPulse(IsAtPulse, PulseAmplitude, BindingSpeedBoost,
                _currentMode, _sensoryRelayGain, _motorRelayGain, _spindleAmplitude);
        }
    }
    
    public float GetBindingFactor()
    {
        lock (_gate) return IsAtPulse ? BindingSpeedBoost : 1.0f;
    }
    
    public float GetTRNInhibition()
    {
        lock (_gate) return _trnInhibition;
    }
    
    public void ApplyAttentionalBias(byte targetRegion, float bias)
    {
        lock (_gate)
        {
            // TRN searchlight: reduce inhibition for attended region
            if (targetRegion >= 9 && targetRegion <= 12) // Sensory regions
                _sensoryRelayGain = Math.Clamp(_sensoryRelayGain + bias * 0.2f, 0.5f, 1.5f);
        }
    }
}

public enum ThalamicMode { Tonic, Burst }

public readonly record struct ThalamicState(
    float FrequencyHz, float Phase, bool IsAtPulse, float PulseAmplitude, long PulseCount,
    ThalamicMode Mode, float TRNInhibition, bool SpindleActive, float SpindleAmplitude);

public readonly record struct ThalamicPulse(
    bool IsBinding, float Amplitude, float SpeedBoost,
    ThalamicMode Mode, float SensoryRelayGain, float MotorRelayGain, float SpindleAmplitude);
