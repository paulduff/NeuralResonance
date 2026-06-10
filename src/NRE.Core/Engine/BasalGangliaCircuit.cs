namespace NRE.Core.Engine;

/// <summary>
/// Basal Ganglia Circuit: Action Selection via Direct/Indirect Pathways
/// 
/// ANATOMICAL STRUCTURE (Frank 2005, Bogacz & Gurney 2007):
/// 
///   Cortex (motor, PFC) → STRIATUM
///                         ↓
///         ┌───────────────┴───────────────┐
///         ↓ (D1 receptors)                ↓ (D2 receptors)
///   DIRECT PATHWAY                  INDIRECT PATHWAY
///   (Go, facilitation)              (NoGo, suppression)
///         ↓                               ↓
///      SNr/GPi ←─────────────────── GPe → STN
///         ↓                               ↑
///      Thalamus ← ← ← ← ← ← ← ← ← ← ← ← ─┘
///         ↓                          (hyperdirect)
///      Motor Cortex
/// 
/// Dopamine from VTA/SNc modulates the balance:
/// - High DA → D1 activation → Direct pathway wins → Action execution
/// - Low DA → D2 activation → Indirect pathway wins → Action suppression
/// 
/// Folded Archive Entry 028: P0 Implementation
/// </summary>
public sealed class BasalGangliaCircuit
{
    private readonly object _gate = new();
    
    // === STRIATUM: Input stage with D1/D2 medium spiny neurons ===
    private readonly StriatumRegion _striatum;
    
    // === GPe: External globus pallidus (indirect pathway) ===
    private readonly GPeRegion _gpe;
    
    // === STN: Subthalamic nucleus (hyperdirect pathway) ===
    private readonly STNRegion _stn;
    
    // === SNr/GPi: Output nuclei (final gating) ===
    private readonly OutputNuclei _output;
    
    // Configuration
    private readonly int _numChannels;
    
    // State
    private float _currentDopamine;
    private float _lastThalamicGating;
    private int _selectedChannel = -1;
    private float _selectionConfidence;

    // GPe→STN is a recurrent loop; STN is evaluated before GPe each step, so it
    // consumes the previous tick's GPe output (standard discrete approximation).
    private float[] _lastGpeToStn;
    
    // Metrics
    private int _totalSelections;
    
    public BasalGangliaCircuit(int numChannels = 8)
    {
        _numChannels = numChannels;
        _striatum = new StriatumRegion(numChannels);
        _gpe = new GPeRegion(numChannels);
        _stn = new STNRegion(numChannels);
        _output = new OutputNuclei(numChannels);

        // Seed with GPe tonic activity so STN starts appropriately inhibited.
        _lastGpeToStn = new float[numChannels];
        Array.Fill(_lastGpeToStn, GPeRegion.TonicActivity);
    }
    
    /// <summary>Get current state snapshot for monitoring.</summary>
    public BasalGangliaState Snapshot()
    {
        lock (_gate)
        {
            return new BasalGangliaState(
                SelectedChannel: _selectedChannel,
                SelectionConfidence: _selectionConfidence,
                DirectPathwayActivity: _striatum.GetDirectActivity(),
                IndirectPathwayActivity: _striatum.GetIndirectActivity(),
                STNActivity: _stn.GetMeanActivity(),
                ThalamicGating: _lastThalamicGating,
                DopamineLevel: _currentDopamine,
                TotalSelections: _totalSelections,
                D1Activation: _striatum.GetD1Activation(),
                D2Activation: _striatum.GetD2Activation());
        }
    }
    
    /// <summary>
    /// Main processing step.
    /// Receives cortical input and dopamine, computes action selection.
    /// </summary>
    /// <param name="corticalInput">Activation per channel from motor/PFC cortex (0..1)</param>
    /// <param name="dopamine01">Dopamine level from VTA (0..1)</param>
    /// <param name="urgency01">Urgency signal that lowers selection threshold</param>
    /// <param name="dt">Time delta</param>
    public BasalGangliaOutput Step(
        float[] corticalInput,
        float dopamine01,
        float urgency01,
        float dt)
    {
        lock (_gate)
        {
            _currentDopamine = Math.Clamp(dopamine01, 0f, 1f);
            
            // === 1) STRIATUM: Process cortical input through D1/D2 pathways ===
            var striatumOut = _striatum.Process(corticalInput, _currentDopamine, dt);
            
            // === 2) HYPERDIRECT PATHWAY: Cortex → STN (fast, global inhibition) ===
            // STN receives direct cortical input and provides broad inhibition
            float corticalSum = 0f;
            for (int i = 0; i < corticalInput.Length && i < _numChannels; i++)
                corticalSum += corticalInput[i];
            float hyperdirectInput = corticalSum / Math.Max(1, _numChannels);
            
            // STN is inhibited by GPe (the indirect pathway reaches STN only via
            // GPe, not directly from striatum). Use the previous tick's GPe output.
            var stnOut = _stn.Process(hyperdirectInput, _lastGpeToStn, dt);

            // === 3) GPe: Process indirect pathway input ===
            var gpeOut = _gpe.Process(striatumOut.IndirectToGPe, stnOut.ToGPe, dt);
            _lastGpeToStn = gpeOut.ToSTN;
            
            // === 4) OUTPUT NUCLEI (SNr/GPi): Final integration ===
            // Direct pathway inhibits SNr/GPi (disinhibits thalamus) → GO
            // STN excites SNr/GPi (inhibits thalamus) → NO-GO
            var outputOut = _output.Process(
                striatumOut.DirectToSNr,
                stnOut.ToSNr,
                gpeOut.ToSNr,
                urgency01,
                dt);
            
            // === 5) Determine selected action ===
            _lastThalamicGating = outputOut.MeanThalamicGating;
            
            int newSelection = -1;
            float maxGating = 0.3f; // Threshold for selection
            
            for (int i = 0; i < _numChannels; i++)
            {
                if (outputOut.ThalamicGating[i] > maxGating)
                {
                    maxGating = outputOut.ThalamicGating[i];
                    newSelection = i;
                }
            }
            
            // Track selection changes
            bool selectionChanged = newSelection != _selectedChannel && newSelection >= 0;
            if (selectionChanged)
            {
                _totalSelections++;
                _selectedChannel = newSelection;
            }
            
            _selectionConfidence = newSelection >= 0 ? maxGating : 0f;
            
            // === 6) Compute motor cortex modulation ===
            // Selected channel gets amplified, others suppressed
            var motorModulation = new float[_numChannels];
            for (int i = 0; i < _numChannels; i++)
            {
                // Thalamic gating directly modulates motor cortex
                motorModulation[i] = outputOut.ThalamicGating[i];
            }
            
            return new BasalGangliaOutput(
                SelectedChannel: _selectedChannel,
                SelectionConfidence: _selectionConfidence,
                ThalamicGating: outputOut.ThalamicGating,
                MotorCortexModulation: motorModulation,
                SelectionChanged: selectionChanged,
                DirectActivity: striatumOut.DirectActivity,
                IndirectActivity: striatumOut.IndirectActivity);
        }
    }
    
    /// <summary>
    /// Apply reward prediction error to update action values.
    /// Positive RPE strengthens direct pathway for selected action.
    /// Negative RPE strengthens indirect pathway.
    /// </summary>
    public void ApplyRewardPredictionError(float rpe, float learningRate = 0.1f)
    {
        lock (_gate)
        {
            if (_selectedChannel < 0) return;
            
            _striatum.UpdateActionValue(_selectedChannel, rpe, learningRate);
        }
    }
    
    /// <summary>Reset to initial state.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _striatum.Reset();
            _gpe.Reset();
            _stn.Reset();
            _output.Reset();
            _selectedChannel = -1;
            _selectionConfidence = 0f;
            _currentDopamine = 0.15f;
            _lastThalamicGating = 0f;
            Array.Fill(_lastGpeToStn, GPeRegion.TonicActivity);
        }
    }
    
    // ==================== INTERNAL COMPONENTS ====================
    
    /// <summary>
    /// Striatum: Input nuclei with D1 (direct) and D2 (indirect) medium spiny neurons.
    /// </summary>
    private sealed class StriatumRegion
    {
        private readonly int _numChannels;
        private readonly float[] _d1Activity;  // Direct pathway (Go)
        private readonly float[] _d2Activity;  // Indirect pathway (NoGo)
        private readonly float[] _actionValues; // Learned action values
        
        // D1/D2 receptor activation levels
        private float _d1Activation;
        private float _d2Activation;
        
        public StriatumRegion(int numChannels)
        {
            _numChannels = numChannels;
            _d1Activity = new float[numChannels];
            _d2Activity = new float[numChannels];
            _actionValues = new float[numChannels];
            
            // Initialize with slight bias toward action
            for (int i = 0; i < numChannels; i++)
                _actionValues[i] = 0.5f;
        }
        
        public StriatumOutput Process(float[] corticalInput, float dopamine01, float dt)
        {
            // Dopamine modulates D1 vs D2 balance
            // High DA → D1 activation, D2 suppression
            // Low DA → D2 activation, D1 suppression
            _d1Activation = 0.5f + (dopamine01 - 0.5f) * 1.5f; // DA enhances D1
            _d2Activation = 0.5f - (dopamine01 - 0.5f) * 1.2f; // DA suppresses D2
            _d1Activation = Math.Clamp(_d1Activation, 0.1f, 1f);
            _d2Activation = Math.Clamp(_d2Activation, 0.1f, 1f);
            
            float directSum = 0f;
            float indirectSum = 0f;
            
            for (int i = 0; i < _numChannels; i++)
            {
                float input = i < corticalInput.Length ? corticalInput[i] : 0f;
                
                // Weight input by learned action value
                float weightedInput = input * _actionValues[i];
                
                // D1 MSNs (direct pathway): enhanced by dopamine
                float d1Drive = weightedInput * _d1Activation;
                _d1Activity[i] = _d1Activity[i] * 0.8f + d1Drive * 0.2f;
                
                // D2 MSNs (indirect pathway): suppressed by dopamine
                float d2Drive = weightedInput * _d2Activation;
                _d2Activity[i] = _d2Activity[i] * 0.8f + d2Drive * 0.2f;
                
                directSum += _d1Activity[i];
                indirectSum += _d2Activity[i];
            }
            
            return new StriatumOutput(
                DirectToSNr: (float[])_d1Activity.Clone(),
                IndirectToGPe: (float[])_d2Activity.Clone(),
                DirectActivity: directSum / _numChannels,
                IndirectActivity: indirectSum / _numChannels);
        }
        
        public void UpdateActionValue(int channel, float rpe, float lr)
        {
            if (channel < 0 || channel >= _numChannels) return;
            
            // Positive RPE → strengthen action value
            // Negative RPE → weaken action value
            _actionValues[channel] += rpe * lr;
            _actionValues[channel] = Math.Clamp(_actionValues[channel], 0.1f, 1.5f);
        }
        
        public float GetDirectActivity()
        {
            float sum = 0f;
            for (int i = 0; i < _numChannels; i++) sum += _d1Activity[i];
            return sum / _numChannels;
        }
        
        public float GetIndirectActivity()
        {
            float sum = 0f;
            for (int i = 0; i < _numChannels; i++) sum += _d2Activity[i];
            return sum / _numChannels;
        }
        
        public float GetD1Activation() => _d1Activation;
        public float GetD2Activation() => _d2Activation;
        
        public void Reset()
        {
            Array.Clear(_d1Activity, 0, _numChannels);
            Array.Clear(_d2Activity, 0, _numChannels);
            for (int i = 0; i < _numChannels; i++)
                _actionValues[i] = 0.5f;
            _d1Activation = 0.5f;
            _d2Activation = 0.5f;
        }
    }
    
    /// <summary>
    /// GPe: External segment of globus pallidus.
    /// Receives inhibition from indirect pathway D2 MSNs.
    /// Projects to STN (inhibitory) and SNr (inhibitory).
    /// </summary>
    private sealed class GPeRegion
    {
        private readonly int _numChannels;
        private readonly float[] _activity;
        
        // GPe has high tonic activity that is suppressed by striatal input
        internal const float TonicActivity = 0.7f;
        
        public GPeRegion(int numChannels)
        {
            _numChannels = numChannels;
            _activity = new float[numChannels];
            for (int i = 0; i < numChannels; i++)
                _activity[i] = TonicActivity;
        }
        
        public GPeOutput Process(float[] striatumIndirect, float[] stnInput, float dt)
        {
            for (int i = 0; i < _numChannels; i++)
            {
                float striatalInhib = i < striatumIndirect.Length ? striatumIndirect[i] : 0f;
                float stnExcite = i < stnInput.Length ? stnInput[i] * 0.3f : 0f;
                
                // GPe is tonically active, inhibited by striatum, excited by STN
                float target = TonicActivity - striatalInhib * 0.6f + stnExcite;
                target = Math.Clamp(target, 0f, 1f);
                
                _activity[i] = _activity[i] * 0.85f + target * 0.15f;
            }
            
            return new GPeOutput(
                ToSTN: (float[])_activity.Clone(),
                ToSNr: (float[])_activity.Clone());
        }
        
        public void Reset()
        {
            for (int i = 0; i < _numChannels; i++)
                _activity[i] = TonicActivity;
        }
    }
    
    /// <summary>
    /// STN: Subthalamic nucleus.
    /// Provides broad excitation to SNr/GPi (global inhibition).
    /// Receives: cortical hyperdirect input, GPe inhibition.
    /// </summary>
    private sealed class STNRegion
    {
        private readonly int _numChannels;
        private readonly float[] _activity;
        private float _globalActivity;
        
        public STNRegion(int numChannels)
        {
            _numChannels = numChannels;
            _activity = new float[numChannels];
        }
        
        public STNOutput Process(float hyperdirectInput, float[] gpeInhibition, float dt)
        {
            // STN has relatively uniform activity (provides global brake)
            float gpeInhibSum = 0f;
            for (int i = 0; i < _numChannels && i < gpeInhibition.Length; i++)
                gpeInhibSum += gpeInhibition[i];
            float meanGpeInhib = gpeInhibSum / Math.Max(1, _numChannels);
            
            // Hyperdirect pathway excites STN (fast, broad brake)
            // GPe inhibits STN (releases the brake)
            float target = hyperdirectInput * 0.8f - meanGpeInhib * 0.4f + 0.2f;
            target = Math.Clamp(target, 0f, 1f);
            
            _globalActivity = _globalActivity * 0.7f + target * 0.3f;
            
            // STN output is relatively uniform across channels
            for (int i = 0; i < _numChannels; i++)
            {
                _activity[i] = _globalActivity * (0.9f + 0.2f * ((float)i / _numChannels));
            }
            
            return new STNOutput(
                ToSNr: (float[])_activity.Clone(),
                ToGPe: (float[])_activity.Clone());
        }
        
        public float GetMeanActivity() => _globalActivity;
        
        public void Reset()
        {
            Array.Clear(_activity, 0, _numChannels);
            _globalActivity = 0f;
        }
    }
    
    /// <summary>
    /// Output Nuclei (SNr/GPi): Final integration and thalamic gating.
    /// High activity → thalamus inhibited → NO-GO
    /// Low activity → thalamus disinhibited → GO
    /// </summary>
    private sealed class OutputNuclei
    {
        private readonly int _numChannels;
        private readonly float[] _activity;
        private readonly float[] _thalamicGating;
        
        // Output nuclei have high tonic activity
        private const float TonicActivity = 0.75f;
        
        public OutputNuclei(int numChannels)
        {
            _numChannels = numChannels;
            _activity = new float[numChannels];
            _thalamicGating = new float[numChannels];
            
            for (int i = 0; i < numChannels; i++)
                _activity[i] = TonicActivity;
        }
        
        public OutputNucleiOutput Process(
            float[] directInhib,
            float[] stnExcite,
            float[] gpeInhib,
            float urgency01,
            float dt)
        {
            float meanGating = 0f;
            
            for (int i = 0; i < _numChannels; i++)
            {
                float direct = i < directInhib.Length ? directInhib[i] : 0f;
                float stn = i < stnExcite.Length ? stnExcite[i] : 0f;
                float gpe = i < gpeInhib.Length ? gpeInhib[i] : 0f;
                
                // Direct pathway inhibits SNr (allows GO)
                // STN excites SNr (blocks GO - global brake)
                // GPe inhibits SNr (complex modulation)
                float target = TonicActivity 
                    - direct * 0.8f      // Direct pathway opens gate
                    + stn * 0.5f         // STN closes gate
                    - gpe * 0.2f;        // GPe modulates
                
                // Urgency lowers the threshold (reduces tonic activity)
                target -= urgency01 * 0.2f;
                
                target = Math.Clamp(target, 0f, 1f);
                _activity[i] = _activity[i] * 0.75f + target * 0.25f;
                
                // Thalamic gating is inverse of SNr activity
                // Low SNr → high thalamic output → action enabled
                _thalamicGating[i] = 1.0f - _activity[i];
                _thalamicGating[i] = Math.Clamp(_thalamicGating[i], 0f, 1f);
                
                meanGating += _thalamicGating[i];
            }
            
            return new OutputNucleiOutput(
                SNrActivity: (float[])_activity.Clone(),
                ThalamicGating: (float[])_thalamicGating.Clone(),
                MeanThalamicGating: meanGating / _numChannels);
        }
        
        public void Reset()
        {
            for (int i = 0; i < _numChannels; i++)
            {
                _activity[i] = TonicActivity;
                _thalamicGating[i] = 0f;
            }
        }
    }
    
    // ==================== INTERNAL DTOs ====================
    
    private readonly record struct StriatumOutput(
        float[] DirectToSNr,
        float[] IndirectToGPe,
        float DirectActivity,
        float IndirectActivity);
    
    private readonly record struct GPeOutput(
        float[] ToSTN,
        float[] ToSNr);
    
    private readonly record struct STNOutput(
        float[] ToSNr,
        float[] ToGPe);
    
    private readonly record struct OutputNucleiOutput(
        float[] SNrActivity,
        float[] ThalamicGating,
        float MeanThalamicGating);
}

// ==================== PUBLIC DTOs ====================

/// <summary>State snapshot for monitoring.</summary>
public readonly record struct BasalGangliaState(
    int SelectedChannel,
    float SelectionConfidence,
    float DirectPathwayActivity,
    float IndirectPathwayActivity,
    float STNActivity,
    float ThalamicGating,
    float DopamineLevel,
    int TotalSelections,
    float D1Activation,
    float D2Activation);

/// <summary>Output from basal ganglia processing.</summary>
public readonly record struct BasalGangliaOutput(
    int SelectedChannel,
    float SelectionConfidence,
    float[] ThalamicGating,
    float[] MotorCortexModulation,
    bool SelectionChanged,
    float DirectActivity,
    float IndirectActivity);
