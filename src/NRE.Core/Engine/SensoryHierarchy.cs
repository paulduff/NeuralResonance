namespace NRE.Core.Engine;

/// <summary>
/// Hierarchical Sensory Processing System.
/// 
/// Visual Pathway (ventral "what" stream):
/// - V1: Edge/orientation detection, retinotopic
/// - V2: Texture, simple shapes
/// - V4: Color, complex shapes, moderate invariance
/// - IT: Object identity, high invariance
/// 
/// Auditory Pathway:
/// - A1: Tonotopic, frequency tuning
/// - Belt: Spectrotemporal features
/// - Parabelt: Complex sounds, categories
/// 
/// Each level extracts increasingly abstract features while
/// maintaining decreasing spatial/spectral resolution.
/// 
/// References:
/// - Felleman & Van Essen 1991 (visual hierarchy)
/// - Rauschecker & Scott 2009 (auditory streams)
/// - DiCarlo et al. 2012 (ventral stream)
/// </summary>
public sealed class SensoryHierarchy
{
    private readonly object _gate = new();
    
    // === VISUAL HIERARCHY ===
    
    /// <summary>V1 feature maps: orientation-selective responses.</summary>
    private readonly FeatureMap _v1;
    
    /// <summary>V2 feature maps: texture and contour.</summary>
    private readonly FeatureMap _v2;
    
    /// <summary>V4 feature maps: shape and color.</summary>
    private readonly FeatureMap _v4;
    
    /// <summary>IT feature maps: object identity.</summary>
    private readonly FeatureMap _it;
    
    // === AUDITORY HIERARCHY ===
    
    /// <summary>A1 feature maps: frequency bands.</summary>
    private readonly FeatureMap _a1;
    
    /// <summary>Belt feature maps: spectrotemporal.</summary>
    private readonly FeatureMap _belt;
    
    /// <summary>Parabelt feature maps: complex sounds.</summary>
    private readonly FeatureMap _parabelt;
    
    // Dimension parameters
    private readonly int _visualW, _visualH; // Retinotopic dimensions
    private readonly int _auditoryW;         // Tonotopic dimension (frequency)
    private readonly int _numOrientations = 8;    // V1 orientation channels
    private readonly int _numFreqBands = 16;      // A1 frequency channels
    
    public SensoryHierarchy(int visualW = 16, int visualH = 16, int auditoryW = 16)
    {
        _visualW = visualW;
        _visualH = visualH;
        _auditoryW = auditoryW;
        
        // Visual hierarchy: spatial resolution decreases up the hierarchy
        _v1 = new FeatureMap(visualW, visualH, _numOrientations);
        _v2 = new FeatureMap(visualW / 2, visualH / 2, 16);  // Texture channels
        _v4 = new FeatureMap(visualW / 4, visualH / 4, 32);  // Shape channels
        _it = new FeatureMap(4, 4, 64);                       // Object channels
        
        // Auditory hierarchy: spectral resolution decreases
        _a1 = new FeatureMap(_auditoryW, 1, _numFreqBands);
        _belt = new FeatureMap(_auditoryW / 2, 1, 24);
        _parabelt = new FeatureMap(_auditoryW / 4, 1, 32);
    }
    
    // ==================== VISUAL PROCESSING ====================
    
    /// <summary>
    /// Process raw visual input through the hierarchy.
    /// Input: 2D intensity array (0..1).
    /// </summary>
    public VisualOutput ProcessVisual(float[,] rawInput, float dt)
    {
        lock (_gate)
        {
            // V1: Gabor-like orientation filtering
            ProcessV1(rawInput);
            
            // V2: Pool V1, extract texture features
            ProcessV2(dt);
            
            // V4: Pool V2, extract shape features
            ProcessV4(dt);
            
            // IT: Pool V4, extract object identity
            ProcessIT(dt);
            
            return new VisualOutput(
                V1Activations: _v1.GetSnapshot(),
                V2Activations: _v2.GetSnapshot(),
                V4Activations: _v4.GetSnapshot(),
                ITActivations: _it.GetSnapshot());
        }
    }
    
    private void ProcessV1(float[,] input)
    {
        int w = Math.Min(input.GetLength(0), _visualW);
        int h = Math.Min(input.GetLength(1), _visualH);
        
        // Simple orientation detection using directional gradients
        float[,] padded = new float[w + 2, h + 2];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            padded[x + 1, y + 1] = input[x, y];
        
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            // Compute local gradients
            float gx = padded[x + 2, y + 1] - padded[x, y + 1];
            float gy = padded[x + 1, y + 2] - padded[x + 1, y];
            
            float magnitude = MathF.Sqrt(gx * gx + gy * gy);
            float angle = MathF.Atan2(gy, gx);
            
            // Distribute across orientation channels
            for (int o = 0; o < _numOrientations; o++)
            {
                float preferredAngle = o * MathF.PI / _numOrientations - MathF.PI / 2;
                float diff = MathF.Abs(angle - preferredAngle);
                if (diff > MathF.PI) diff = 2 * MathF.PI - diff;
                
                // Gaussian tuning
                float response = magnitude * MathF.Exp(-diff * diff / 0.5f);
                _v1.Set(x, y, o, response);
            }
        }
    }
    
    private void ProcessV2(float dt)
    {
        // Pool V1 with 2x2 max pooling, extract texture features
        for (int x = 0; x < _v2.Width; x++)
        for (int y = 0; y < _v2.Height; y++)
        for (int c = 0; c < _v2.Channels; c++)
        {
            // Pool from 2x2 V1 region
            float maxVal = 0f;
            for (int dx = 0; dx < 2; dx++)
            for (int dy = 0; dy < 2; dy++)
            {
                int v1x = x * 2 + dx;
                int v1y = y * 2 + dy;
                
                // V2 channels combine multiple V1 orientations
                int o1 = c % _numOrientations;
                int o2 = (c + _numOrientations / 2) % _numOrientations;
                
                float v = _v1.Get(v1x, v1y, o1) + _v1.Get(v1x, v1y, o2) * 0.5f;
                maxVal = MathF.Max(maxVal, v);
            }
            
            // Temporal smoothing
            float prev = _v2.Get(x, y, c);
            _v2.Set(x, y, c, prev * 0.7f + maxVal * 0.3f);
        }
    }
    
    private void ProcessV4(float dt)
    {
        // Pool V2, extract shape features with increasing invariance
        for (int x = 0; x < _v4.Width; x++)
        for (int y = 0; y < _v4.Height; y++)
        for (int c = 0; c < _v4.Channels; c++)
        {
            float sum = 0f;
            int count = 0;
            
            // Pool from 2x2 V2 region
            for (int dx = 0; dx < 2; dx++)
            for (int dy = 0; dy < 2; dy++)
            {
                int v2x = x * 2 + dx;
                int v2y = y * 2 + dy;
                
                // V4 channels combine multiple V2 texture channels
                for (int vc = 0; vc < 4; vc++)
                {
                    int v2c = (c * 4 + vc) % _v2.Channels;
                    sum += _v2.Get(v2x, v2y, v2c);
                    count++;
                }
            }
            
            float avg = count > 0 ? sum / count : 0f;
            
            // Nonlinearity (soft threshold)
            avg = avg > 0.1f ? (avg - 0.1f) * 1.2f : 0f;
            
            float prev = _v4.Get(x, y, c);
            _v4.Set(x, y, c, prev * 0.6f + avg * 0.4f);
        }
    }
    
    private void ProcessIT(float dt)
    {
        // Pool V4 into object-level representations
        int v4PoolW = _v4.Width / _it.Width;
        int v4PoolH = _v4.Height / _it.Height;
        
        for (int x = 0; x < _it.Width; x++)
        for (int y = 0; y < _it.Height; y++)
        for (int c = 0; c < _it.Channels; c++)
        {
            float maxVal = 0f;
            
            // Global max pooling over V4 region
            for (int dx = 0; dx < v4PoolW; dx++)
            for (int dy = 0; dy < v4PoolH; dy++)
            {
                int v4x = x * v4PoolW + dx;
                int v4y = y * v4PoolH + dy;
                
                // Each IT channel pools multiple V4 channels
                for (int vc = 0; vc < 2; vc++)
                {
                    int v4c = (c * 2 + vc) % _v4.Channels;
                    maxVal = MathF.Max(maxVal, _v4.Get(v4x, v4y, v4c));
                }
            }
            
            // Strong nonlinearity (sparseness)
            float sparse = maxVal > 0.2f ? (maxVal - 0.2f) * 1.5f : 0f;
            
            float prev = _it.Get(x, y, c);
            _it.Set(x, y, c, prev * 0.5f + sparse * 0.5f);
        }
    }
    
    // ==================== AUDITORY PROCESSING ====================
    
    /// <summary>
    /// Process raw auditory input through the hierarchy.
    /// Input: 1D spectral array (frequency bins, 0..1).
    /// </summary>
    public AuditoryOutput ProcessAuditory(float[] spectrum, float dt)
    {
        lock (_gate)
        {
            // A1: Tonotopic frequency bands
            ProcessA1(spectrum);
            
            // Belt: Spectrotemporal features
            ProcessBelt(dt);
            
            // Parabelt: Complex sound categories
            ProcessParabelt(dt);
            
            return new AuditoryOutput(
                A1Activations: _a1.GetSnapshot(),
                BeltActivations: _belt.GetSnapshot(),
                ParabeltActivations: _parabelt.GetSnapshot());
        }
    }
    
    private void ProcessA1(float[] spectrum)
    {
        int bins = Math.Min(spectrum.Length, _auditoryW);
        
        for (int x = 0; x < _auditoryW; x++)
        {
            int bin = x * bins / _auditoryW;
            float input = bin < spectrum.Length ? spectrum[bin] : 0f;
            
            // Distribute across frequency-tuned channels
            for (int c = 0; c < _numFreqBands; c++)
            {
                // Each channel has a preferred frequency
                float preferredBin = c * _auditoryW / (float)_numFreqBands;
                float diff = MathF.Abs(x - preferredBin);
                
                // Gaussian tuning with bandwidth
                float bandwidth = _auditoryW / (float)_numFreqBands * 1.5f;
                float response = input * MathF.Exp(-diff * diff / (2 * bandwidth * bandwidth));
                
                _a1.Set(x, 0, c, response);
            }
        }
    }
    
    private void ProcessBelt(float dt)
    {
        // Pool A1, extract spectrotemporal features
        for (int x = 0; x < _belt.Width; x++)
        for (int c = 0; c < _belt.Channels; c++)
        {
            float sum = 0f;
            
            // Pool from 2 A1 positions
            for (int dx = 0; dx < 2; dx++)
            {
                int a1x = x * 2 + dx;
                
                // Belt channels combine multiple A1 frequency bands
                for (int fc = 0; fc < 3; fc++)
                {
                    int a1c = (c + fc) % _numFreqBands;
                    sum += _a1.Get(a1x, 0, a1c);
                }
            }
            
            float avg = sum / 6f;
            
            float prev = _belt.Get(x, 0, c);
            _belt.Set(x, 0, c, prev * 0.6f + avg * 0.4f);
        }
    }
    
    private void ProcessParabelt(float dt)
    {
        // Pool Belt into categorical representations
        for (int x = 0; x < _parabelt.Width; x++)
        for (int c = 0; c < _parabelt.Channels; c++)
        {
            float maxVal = 0f;
            
            // Pool from 2 Belt positions
            for (int dx = 0; dx < 2; dx++)
            {
                int beltx = x * 2 + dx;
                
                // Each Parabelt channel pools multiple Belt channels
                for (int bc = 0; bc < 3; bc++)
                {
                    int beltc = (c * 3 + bc) % _belt.Channels;
                    maxVal = MathF.Max(maxVal, _belt.Get(beltx, 0, beltc));
                }
            }
            
            // Sparse activation
            float sparse = maxVal > 0.15f ? (maxVal - 0.15f) * 1.3f : 0f;
            
            float prev = _parabelt.Get(x, 0, c);
            _parabelt.Set(x, 0, c, prev * 0.5f + sparse * 0.5f);
        }
    }
    
    // ==================== FEEDBACK CONNECTIONS ====================
    
    /// <summary>
    /// Apply top-down attention to modulate lower levels.
    /// </summary>
    public void ApplyTopDownVisualAttention(int focusX, int focusY, float attentionStrength)
    {
        lock (_gate)
        {
            // Attention enhances processing at focused location
            float sigma = 3f / attentionStrength; // Tighter focus with higher attention
            
            for (int x = 0; x < _v1.Width; x++)
            for (int y = 0; y < _v1.Height; y++)
            {
                float dx = x - focusX;
                float dy = y - focusY;
                float dist2 = dx * dx + dy * dy;
                
                float boost = 1f + attentionStrength * MathF.Exp(-dist2 / (2 * sigma * sigma));
                
                for (int c = 0; c < _v1.Channels; c++)
                {
                    float v = _v1.Get(x, y, c);
                    _v1.Set(x, y, c, v * boost);
                }
            }
        }
    }
    
    /// <summary>
    /// Apply top-down attention to auditory processing.
    /// </summary>
    public void ApplyTopDownAuditoryAttention(int focusFreq, float attentionStrength)
    {
        lock (_gate)
        {
            float sigma = 4f / attentionStrength;
            
            for (int x = 0; x < _a1.Width; x++)
            {
                float dx = x - focusFreq;
                float boost = 1f + attentionStrength * MathF.Exp(-dx * dx / (2 * sigma * sigma));
                
                for (int c = 0; c < _a1.Channels; c++)
                {
                    float v = _a1.Get(x, 0, c);
                    _a1.Set(x, 0, c, v * boost);
                }
            }
        }
    }
    
    /// <summary>Get most active IT representation (detected object).</summary>
    public (int channel, float activation) GetDominantObject()
    {
        lock (_gate)
        {
            int bestC = 0;
            float bestA = 0;
            
            for (int x = 0; x < _it.Width; x++)
            for (int y = 0; y < _it.Height; y++)
            for (int c = 0; c < _it.Channels; c++)
            {
                float a = _it.Get(x, y, c);
                if (a > bestA)
                {
                    bestA = a;
                    bestC = c;
                }
            }
            
            return (bestC, bestA);
        }
    }
    
    /// <summary>Get snapshot for monitoring.</summary>
    public SensoryHierarchySnapshot Snapshot()
    {
        lock (_gate)
        {
            return new SensoryHierarchySnapshot(
                V1MeanActivity: _v1.MeanActivity(),
                V2MeanActivity: _v2.MeanActivity(),
                V4MeanActivity: _v4.MeanActivity(),
                ITMeanActivity: _it.MeanActivity(),
                A1MeanActivity: _a1.MeanActivity(),
                BeltMeanActivity: _belt.MeanActivity(),
                ParabeltMeanActivity: _parabelt.MeanActivity(),
                DominantObject: GetDominantObject());
        }
    }
    
    /// <summary>Reset all activations.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _v1.Clear();
            _v2.Clear();
            _v4.Clear();
            _it.Clear();
            _a1.Clear();
            _belt.Clear();
            _parabelt.Clear();
        }
    }
    
    // ==================== INTERNAL FEATURE MAP ====================
    
    private sealed class FeatureMap
    {
        public int Width { get; }
        public int Height { get; }
        public int Channels { get; }
        
        private readonly float[] _data;
        
        public FeatureMap(int w, int h, int c)
        {
            Width = w;
            Height = h;
            Channels = c;
            _data = new float[w * h * c];
        }
        
        public float Get(int x, int y, int c)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || c < 0 || c >= Channels)
                return 0f;
            return _data[(c * Height + y) * Width + x];
        }
        
        public void Set(int x, int y, int c, float v)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || c < 0 || c >= Channels)
                return;
            _data[(c * Height + y) * Width + x] = v;
        }
        
        public float MeanActivity()
        {
            float sum = 0;
            for (int i = 0; i < _data.Length; i++)
                sum += _data[i];
            return _data.Length > 0 ? sum / _data.Length : 0f;
        }
        
        public float[] GetSnapshot() => (float[])_data.Clone();
        
        public void Clear() => Array.Clear(_data, 0, _data.Length);
    }
}

/// <summary>Visual processing output.</summary>
public readonly record struct VisualOutput(
    float[] V1Activations,
    float[] V2Activations,
    float[] V4Activations,
    float[] ITActivations);

/// <summary>Auditory processing output.</summary>
public readonly record struct AuditoryOutput(
    float[] A1Activations,
    float[] BeltActivations,
    float[] ParabeltActivations);

/// <summary>Snapshot for monitoring.</summary>
public readonly record struct SensoryHierarchySnapshot(
    float V1MeanActivity,
    float V2MeanActivity,
    float V4MeanActivity,
    float ITMeanActivity,
    float A1MeanActivity,
    float BeltMeanActivity,
    float ParabeltMeanActivity,
    (int channel, float activation) DominantObject);
