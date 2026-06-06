namespace NRE.Core.Engine;

/// <summary>
/// Attention System: Biased Competition and Selective Amplification.
/// 
/// Biology:
/// - Bottom-up (exogenous): Stimulus-driven, automatic capture
/// - Top-down (endogenous): Goal-driven, voluntary control
/// - Biased competition: Stimuli compete for representation; attention biases the competition
/// - Feature-based attention: Enhance all locations with target features
/// - Spatial attention: Enhance all features at target location
/// 
/// Neural substrates:
/// - Frontal eye fields (FEF): Spatial attention control
/// - Intraparietal sulcus (IPS): Priority maps
/// - Pulvinar: Thalamic attention modulation
/// - Prefrontal cortex: Top-down bias signals
/// 
/// References:
/// - Desimone & Duncan 1995 (biased competition)
/// - Corbetta & Shulman 2002 (attention networks)
/// - Reynolds & Heeger 2009 (normalization model)
/// </summary>
public sealed class AttentionSystem
{
    private readonly object _gate = new();
    
    // === SPATIAL ATTENTION (priority map) ===
    private readonly float[] _priorityMap;  // 3D attention weights
    private readonly int _w, _h, _d;
    
    // === FEATURE-BASED ATTENTION ===
    private readonly float[] _featureWeights;  // Per-feature attention weights
    private readonly int _numFeatures;
    
    // === ATTENTION FOCI ===
    private readonly List<AttentionFocus> _foci = new();
    private const int MaxFoci = 4;  // Limited capacity
    
    // === BOTTOM-UP SALIENCE ===
    private float[]? _salienceMap;
    
    // === PARAMETERS ===
    
    /// <summary>Spatial attention sigma (spread of enhancement).</summary>
    public float SpatialSigma { get; set; } = 3.0f;
    
    /// <summary>Decay rate of attention weights.</summary>
    public float AttentionDecayRate { get; set; } = 0.15f;
    
    /// <summary>Strength of bottom-up salience capture.</summary>
    public float BottomUpGain { get; set; } = 0.4f;
    
    /// <summary>Strength of top-down control.</summary>
    public float TopDownGain { get; set; } = 0.6f;
    
    /// <summary>Suppression strength for unattended stimuli.</summary>
    public float SuppressionStrength { get; set; } = 0.3f;
    
    /// <summary>Enhancement strength for attended stimuli.</summary>
    public float EnhancementStrength { get; set; } = 0.5f;
    
    /// <summary>Normalization strength (divisive).</summary>
    public float NormalizationStrength { get; set; } = 0.4f;
    
    /// <summary>Inhibition of return duration (seconds).</summary>
    public float IORDurationSec { get; set; } = 2.0f;
    
    // Inhibition of return tracking
    private readonly Queue<(float x, float y, float z, float expireTime)> _iorLocations = new();
    private float _currentTime;
    
    public AttentionSystem(int w, int h, int d, int numFeatures = 32)
    {
        _w = w;
        _h = h;
        _d = d;
        _numFeatures = numFeatures;
        
        _priorityMap = new float[w * h * d];
        _featureWeights = new float[numFeatures];
        _salienceMap = new float[w * h * d];
        
        // Initialize with uniform attention
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        for (int z = 0; z < d; z++)
            _priorityMap[(z * _h + y) * _w + x] = 0.5f;
        
        for (int f = 0; f < numFeatures; f++)
            _featureWeights[f] = 1.0f;
    }
    
    // ==================== ATTENTION CONTROL ====================
    
    /// <summary>
    /// Set a top-down spatial attention focus.
    /// </summary>
    public void SetSpatialFocus(float x, float y, float z, float strength = 1.0f, AttentionType type = AttentionType.Endogenous)
    {
        lock (_gate)
        {
            // Check for inhibition of return
            if (type == AttentionType.Exogenous && IsUnderIOR(x, y, z))
            {
                strength *= 0.3f; // Reduced capture at recently attended locations
            }
            
            // Limited capacity: if at max, remove weakest
            if (_foci.Count >= MaxFoci)
            {
                float minStrength = float.MaxValue;
                int minIdx = 0;
                for (int i = 0; i < _foci.Count; i++)
                {
                    if (_foci[i].Strength < minStrength)
                    {
                        minStrength = _foci[i].Strength;
                        minIdx = i;
                    }
                }
                if (strength > minStrength)
                    _foci.RemoveAt(minIdx);
                else
                    return; // Can't add, all existing foci are stronger
            }
            
            _foci.Add(new AttentionFocus(x, y, z, strength, type, _currentTime));
            
            // Update priority map with new focus
            UpdatePriorityMap();
        }
    }
    
    /// <summary>
    /// Set feature-based attention (enhance all locations with this feature).
    /// </summary>
    public void SetFeatureAttention(int featureIndex, float weight)
    {
        if (featureIndex < 0 || featureIndex >= _numFeatures) return;
        
        lock (_gate)
        {
            _featureWeights[featureIndex] = Math.Clamp(weight, 0f, 2f);
        }
    }
    
    /// <summary>
    /// Clear all attention foci.
    /// </summary>
    public void ClearFoci()
    {
        lock (_gate)
        {
            _foci.Clear();
            
            // Reset priority map to uniform
            for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            for (int z = 0; z < _d; z++)
                _priorityMap[(z * _h + y) * _w + x] = 0.5f;
        }
    }
    
    // ==================== BOTTOM-UP SALIENCE ====================
    
    /// <summary>
    /// Update bottom-up salience from activity patterns.
    /// High contrast/novelty creates automatic attention capture.
    /// </summary>
    public void UpdateSalience(float[] activity, byte[] regionIds)
    {
        lock (_gate)
        {
            if (_salienceMap == null)
                _salienceMap = new float[_w * _h * _d];
            
            // Compute local contrast as salience
            for (int x = 1; x < _w - 1; x++)
            for (int y = 1; y < _h - 1; y++)
            for (int z = 1; z < _d - 1; z++)
            {
                float center = activity[(z * _h + y) * _w + x];
                float surround = 0f;
                int count = 0;
                
                // 6-neighborhood
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    surround += activity[((z + dz) * _h + (y + dy)) * _w + (x + dx)];
                    count++;
                }
                
                surround /= count;
                
                // Salience = center-surround contrast
                float contrast = MathF.Abs(center - surround);
                
                // Also boost salience at region boundaries (feature discontinuities)
                byte centerRegion = regionIds[(z * _h + y) * _w + x];
                bool atBoundary = false;
                for (int dx = -1; dx <= 1 && !atBoundary; dx++)
                for (int dy = -1; dy <= 1 && !atBoundary; dy++)
                for (int dz = -1; dz <= 1 && !atBoundary; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    if (regionIds[((z + dz) * _h + (y + dy)) * _w + (x + dx)] != centerRegion)
                        atBoundary = true;
                }
                
                float boundaryBoost = atBoundary ? 0.2f : 0f;
                
                _salienceMap[(z * _h + y) * _w + x] = contrast + boundaryBoost;
            }
            
            // Find salience peaks for automatic capture
            DetectSaliencePeaks();
        }
    }
    
    private void DetectSaliencePeaks()
    {
        if (_salienceMap == null) return;
        
        // Find local maxima above threshold
        float threshold = 0.3f;
        
        for (int x = 2; x < _w - 2; x++)
        for (int y = 2; y < _h - 2; y++)
        for (int z = 2; z < _d - 2; z++)
        {
            float s = _salienceMap[(z * _h + y) * _w + x];
            if (s < threshold) continue;
            
            // Check if local maximum
            bool isMax = true;
            for (int dx = -1; dx <= 1 && isMax; dx++)
            for (int dy = -1; dy <= 1 && isMax; dy++)
            for (int dz = -1; dz <= 1 && isMax; dz++)
            {
                if (dx == 0 && dy == 0 && dz == 0) continue;
                if (_salienceMap[((z + dz) * _h + (y + dy)) * _w + (x + dx)] >= s)
                    isMax = false;
            }
            
            if (isMax)
            {
                // Automatic attention capture
                SetSpatialFocus(x, y, z, s * BottomUpGain, AttentionType.Exogenous);
            }
        }
    }
    
    // ==================== ATTENTION MODULATION ====================
    
    /// <summary>
    /// Get attention modulation for a voxel.
    /// Returns: multiplier for neural gain (1.0 = neutral, >1 = enhanced, <1 = suppressed).
    /// </summary>
    public float GetGainModulation(int x, int y, int z, int featureIndex = -1)
    {
        if (x < 0 || x >= _w || y < 0 || y >= _h || z < 0 || z >= _d)
            return 1.0f;
        
        lock (_gate)
        {
            float spatialMod = _priorityMap[(z * _h + y) * _w + x];
            float featureMod = 1.0f;
            
            if (featureIndex >= 0 && featureIndex < _numFeatures)
                featureMod = _featureWeights[featureIndex];
            
            // Combine spatial and feature attention multiplicatively
            float combined = spatialMod * featureMod;
            
            // Apply enhancement/suppression relative to baseline (0.5)
            float baseline = 0.5f;
            float gain;
            
            if (combined > baseline)
            {
                // Enhancement
                gain = 1.0f + (combined - baseline) * 2f * EnhancementStrength;
            }
            else
            {
                // Suppression
                gain = 1.0f - (baseline - combined) * 2f * SuppressionStrength;
            }
            
            return Math.Clamp(gain, 0.3f, 2.0f);
        }
    }
    
    /// <summary>
    /// Apply biased competition: attended representations suppress competitors.
    /// Input: activity levels for competing representations.
    /// Output: modulated activity after competition.
    /// </summary>
    public float[] ApplyBiasedCompetition(float[] activities, float[] attentionWeights)
    {
        if (activities.Length != attentionWeights.Length)
            return activities;
        
        int n = activities.Length;
        float[] result = new float[n];
        
        // Normalization: divide by weighted sum (divisive normalization)
        float weightedSum = 0f;
        for (int i = 0; i < n; i++)
            weightedSum += activities[i] * attentionWeights[i];
        
        float normFactor = 1f + NormalizationStrength * weightedSum;
        
        for (int i = 0; i < n; i++)
        {
            // Biased activity: weighted by attention
            float biased = activities[i] * attentionWeights[i];
            
            // Normalized: divided by pool activity
            result[i] = biased / normFactor;
        }
        
        return result;
    }
    
    // ==================== UPDATE ====================
    
    /// <summary>
    /// Main update step. Called on intermediate lane (~20Hz).
    /// </summary>
    public AttentionOutput Step(float dt)
    {
        lock (_gate)
        {
            _currentTime += dt;
            
            // Decay attention foci
            for (int i = _foci.Count - 1; i >= 0; i--)
            {
                var focus = _foci[i];
                float newStrength = focus.Strength - AttentionDecayRate * dt;
                
                // Endogenous attention decays slower
                if (focus.Type == AttentionType.Endogenous)
                    newStrength = focus.Strength - AttentionDecayRate * 0.5f * dt;
                
                if (newStrength <= 0.05f)
                {
                    // Add to IOR before removing
                    if (focus.Type == AttentionType.Exogenous)
                        AddIOR(focus.X, focus.Y, focus.Z);
                    
                    _foci.RemoveAt(i);
                }
                else
                {
                    _foci[i] = focus with { Strength = newStrength };
                }
            }
            
            // Decay feature weights toward 1.0
            for (int f = 0; f < _numFeatures; f++)
            {
                _featureWeights[f] += (1.0f - _featureWeights[f]) * AttentionDecayRate * dt;
            }
            
            // Update priority map
            UpdatePriorityMap();
            
            // Expire old IOR entries
            while (_iorLocations.Count > 0 && _iorLocations.Peek().expireTime < _currentTime)
                _iorLocations.Dequeue();
            
            return new AttentionOutput(
                ActiveFoci: _foci.Count,
                MeanPriority: ComputeMeanPriority(),
                MaxPriority: ComputeMaxPriority(),
                IORLocations: _iorLocations.Count);
        }
    }
    
    private void UpdatePriorityMap()
    {
        // Reset to baseline
        for (int x = 0; x < _w; x++)
        for (int y = 0; y < _h; y++)
        for (int z = 0; z < _d; z++)
            _priorityMap[(z * _h + y) * _w + x] = 0.1f; // Low baseline
        
        // Add Gaussian attention fields from each focus
        foreach (var focus in _foci)
        {
            float sigma2 = SpatialSigma * SpatialSigma;
            
            // Limit computation to nearby voxels
            int range = (int)(SpatialSigma * 3);
            int fx = (int)focus.X;
            int fy = (int)focus.Y;
            int fz = (int)focus.Z;
            
            for (int x = Math.Max(0, fx - range); x < Math.Min(_w, fx + range); x++)
            for (int y = Math.Max(0, fy - range); y < Math.Min(_h, fy + range); y++)
            for (int z = Math.Max(0, fz - range); z < Math.Min(_d, fz + range); z++)
            {
                float dx = x - focus.X;
                float dy = y - focus.Y;
                float dz = z - focus.Z;
                float dist2 = dx * dx + dy * dy + dz * dz;
                
                float weight = focus.Strength * MathF.Exp(-dist2 / (2 * sigma2));
                
                // Top-down is stronger
                if (focus.Type == AttentionType.Endogenous)
                    weight *= TopDownGain / BottomUpGain;
                
                _priorityMap[(z * _h + y) * _w + x] = MathF.Min(1f, _priorityMap[(z * _h + y) * _w + x] + weight);
            }
        }
        
        // Apply IOR (reduce priority at recently attended locations)
        foreach (var ior in _iorLocations)
        {
            int x = (int)ior.x;
            int y = (int)ior.y;
            int z = (int)ior.z;
            
            if (x >= 0 && x < _w && y >= 0 && y < _h && z >= 0 && z < _d)
            {
                float remaining = (ior.expireTime - _currentTime) / IORDurationSec;
                _priorityMap[(z * _h + y) * _w + x] *= (1f - remaining * 0.5f);
            }
        }
    }
    
    private void AddIOR(float x, float y, float z)
    {
        _iorLocations.Enqueue((x, y, z, _currentTime + IORDurationSec));
    }
    
    private bool IsUnderIOR(float x, float y, float z)
    {
        float threshold = 2f; // Distance threshold
        
        foreach (var ior in _iorLocations)
        {
            float dx = x - ior.x;
            float dy = y - ior.y;
            float dz = z - ior.z;
            float dist2 = dx * dx + dy * dy + dz * dz;
            
            if (dist2 < threshold * threshold)
                return true;
        }
        
        return false;
    }
    
    private float ComputeMeanPriority()
    {
        float sum = 0;
        int count = _w * _h * _d;
        
        for (int x = 0; x < _w; x++)
        for (int y = 0; y < _h; y++)
        for (int z = 0; z < _d; z++)
            sum += _priorityMap[(z * _h + y) * _w + x];
        
        return sum / count;
    }
    
    private float ComputeMaxPriority()
    {
        float max = 0;
        
        for (int x = 0; x < _w; x++)
        for (int y = 0; y < _h; y++)
        for (int z = 0; z < _d; z++)
            max = MathF.Max(max, _priorityMap[(z * _h + y) * _w + x]);
        
        return max;
    }
    
    /// <summary>Get current attention foci.</summary>
    public AttentionFocus[] GetFoci()
    {
        lock (_gate)
            return _foci.ToArray();
    }
    
    /// <summary>Get priority at location.</summary>
    public float GetPriority(int x, int y, int z)
    {
        if (x < 0 || x >= _w || y < 0 || y >= _h || z < 0 || z >= _d)
            return 0f;
        lock (_gate)
            return _priorityMap[(z * _h + y) * _w + x];
    }
    
    /// <summary>Get snapshot for UI.</summary>
    public AttentionSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new AttentionSnapshot(
                ActiveFoci: _foci.Count,
                Foci: _foci.ToArray(),
                MeanPriority: ComputeMeanPriority(),
                MaxPriority: ComputeMaxPriority(),
                IORCount: _iorLocations.Count,
                FeatureWeights: (float[])_featureWeights.Clone());
        }
    }
    
    /// <summary>Reset attention state.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _foci.Clear();
            _iorLocations.Clear();
            _currentTime = 0;
            
            for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            for (int z = 0; z < _d; z++)
                _priorityMap[(z * _h + y) * _w + x] = 0.5f;
            
            for (int f = 0; f < _numFeatures; f++)
                _featureWeights[f] = 1.0f;
        }
    }
}

/// <summary>Type of attention.</summary>
public enum AttentionType
{
    Exogenous,  // Bottom-up, stimulus-driven
    Endogenous  // Top-down, goal-driven
}

/// <summary>An attention focus point.</summary>
public readonly record struct AttentionFocus(
    float X, float Y, float Z,
    float Strength,
    AttentionType Type,
    float CreatedTime);

/// <summary>Per-step output.</summary>
public readonly record struct AttentionOutput(
    int ActiveFoci,
    float MeanPriority,
    float MaxPriority,
    int IORLocations);

/// <summary>Snapshot for UI.</summary>
public readonly record struct AttentionSnapshot(
    int ActiveFoci,
    AttentionFocus[] Foci,
    float MeanPriority,
    float MaxPriority,
    int IORCount,
    float[] FeatureWeights);
