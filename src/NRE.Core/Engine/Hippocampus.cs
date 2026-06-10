namespace NRE.Core.Engine;

/// <summary>
/// Hippocampus: Episodic Memory System with Biologically Accurate Trisynaptic Circuit
/// 
/// ANATOMICAL STRUCTURE (Amaral & Witter 1989, Andersen et al. 1971):
/// 
///   Entorhinal Cortex (EC Layer II)
///         ↓ Perforant Path
///   Dentate Gyrus (DG) - Pattern Separation
///         ↓ Mossy Fibers  
///   CA3 - Autoassociative Network (Pattern Completion)
///         ↓ Schaffer Collaterals
///   CA1 - Comparator/Output
///         ↓
///   Subiculum → Back to EC + other regions
/// 
/// Each subregion has distinct computational properties:
/// - DG: Sparse coding via competitive inhibition (10:1 compression)
/// - CA3: Recurrent collaterals enable attractor dynamics
/// - CA1: Integrates CA3 output + direct EC input for novelty detection
/// </summary>
public sealed class Hippocampus
{
    private readonly object _gate = new();
    private readonly int _maxEpisodes;
    private readonly int _maxVoxelsPerEpisode;
    
    // === TRISYNAPTIC CIRCUIT COMPONENTS ===
    
    /// <summary>Dentate Gyrus: Pattern separation via sparse coding</summary>
    private readonly DentateGyrus _dg;
    
    /// <summary>CA3: Autoassociative network with recurrent collaterals</summary>
    private readonly CA3Region _ca3;
    
    /// <summary>CA1: Output comparator, novelty detection</summary>
    private readonly CA1Region _ca1;
    
    // === EPISODE STORAGE ===
    private readonly List<Episode> _episodes = new();

    // Reusable buffers
    private readonly List<EpisodeVoxel> _captureBuffer = new(512);
    private readonly List<int> _dgOutputBuffer = new(256);
    private readonly List<int> _ca3OutputBuffer = new(256);
    private readonly List<int> _ca1OutputBuffer = new(256);
    
    private long _currentStep;
    private int _nextEpisodeId;
    
    // Circuit activity metrics
    private float _dgSparsity;
    private float _ca3Coherence;
    private float _ca1NoveltySignal;
    
    public Hippocampus(int maxEpisodes = 64, int maxVoxelsPerEpisode = 512)
    {
        _maxEpisodes = maxEpisodes;
        _maxVoxelsPerEpisode = maxVoxelsPerEpisode;

        // Initialize trisynaptic circuit components
        _dg = new DentateGyrus(sparseRatio: 0.1f, lateralInhibitionRadius: 3);
        _ca3 = new CA3Region(recurrentStrength: 0.6f, maxPatterns: 128);
        _ca1 = new CA1Region(noveltyThreshold: 0.25f);
    }
    
    public HippocampusState Snapshot()
    {
        lock (_gate)
        {
            int totalVoxels = 0;
            for (int i = 0; i < _episodes.Count; i++)
                totalVoxels += _episodes[i].Voxels.Count;
            
            return new HippocampusState(
                EpisodeCount: _episodes.Count,
                AssociationCount: _ca3.AssociationCount,
                OldestEpisodeAge: _episodes.Count > 0 ? _currentStep - _episodes[0].CapturedAtStep : 0,
                TotalVoxelsCaptured: totalVoxels,
                DGSparsity: _dgSparsity,
                CA3Coherence: _ca3Coherence,
                CA1NoveltySignal: _ca1NoveltySignal);
        }
    }
    
    /// <summary>
    /// Process spikes through the trisynaptic circuit.
    /// Biology: EC→DG→CA3→CA1 pathway with distinct computations at each stage.
    /// </summary>
    public HippocampalOutput Step(long stepIndex, IReadOnlyList<(byte hemi, int idx)> spikes, float dt)
    {
        lock (_gate)
        {
            _currentStep = stepIndex;
            
            // === 1) PERFORANT PATH: EC input to Dentate Gyrus ===
            // DG performs pattern separation via sparse competitive coding
            _dgOutputBuffer.Clear();
            _dgSparsity = _dg.ProcessInput(spikes, _dgOutputBuffer);
            
            // === 2) MOSSY FIBERS: DG to CA3 ===
            // CA3 receives sparse DG output + performs autoassociative completion
            _ca3OutputBuffer.Clear();
            _ca3Coherence = _ca3.ProcessInput(_dgOutputBuffer, _ca3OutputBuffer, dt);
            
            // === 3) SCHAFFER COLLATERALS: CA3 to CA1 ===
            // CA1 compares CA3 output with direct EC input for novelty detection
            _ca1OutputBuffer.Clear();
            _ca1NoveltySignal = _ca1.ProcessInput(_ca3OutputBuffer, spikes, _ca1OutputBuffer);
            
            // Decay episodes periodically
            if (stepIndex % 8 == 0)
                DecayEpisodes(dt * 8);
            
            return new HippocampalOutput(
                DGSparsity: _dgSparsity,
                CA3Coherence: _ca3Coherence,
                NoveltySignal: _ca1NoveltySignal,
                PatternCompleted: _ca3Coherence > 0.5f,
                OutputIndices: _ca1OutputBuffer.ToArray());
        }
    }
    
    /// <summary>
    /// Capture an episodic snapshot via CA3 pattern storage.
    /// Biology: Episodes are stored as attractor states in CA3 recurrent network.
    /// </summary>
    public int CaptureEpisode(IReadOnlyList<(byte hemi, int idx)> spikes, float salience = 1.0f)
    {
        lock (_gate)
        {
            if (spikes.Count == 0) return -1;
            
            // Process through DG for sparse representation
            _dgOutputBuffer.Clear();
            _dg.ProcessInput(spikes, _dgOutputBuffer);
            
            // Store pattern in CA3 autoassociative network
            _ca3.StorePattern(_dgOutputBuffer, _nextEpisodeId);
            
            // Build episode record
            _captureBuffer.Clear();
            int count = Math.Min(spikes.Count, _maxVoxelsPerEpisode);
            for (int i = 0; i < count; i++)
            {
                var s = spikes[i];
                _captureBuffer.Add(new EpisodeVoxel(s.hemi, s.idx));
            }
            
            var episode = new Episode(
                id: _nextEpisodeId++,
                capturedAtStep: _currentStep,
                voxels: new List<EpisodeVoxel>(_captureBuffer),
                salience: salience,
                strength: 1.0f,
                dgPattern: _dgOutputBuffer.ToArray());
            
            _episodes.Add(episode);
            
            // Evict oldest if over limit
            if (_episodes.Count > _maxEpisodes)
            {
                var evictCount = _episodes.Count - _maxEpisodes;
                for (int i = 0; i < evictCount; i++)
                {
                    _ca3.ForgetPattern(_episodes[i].Id);
                }
                _episodes.RemoveRange(0, evictCount);
            }
            
            return episode.Id;
        }
    }
    
    /// <summary>
    /// Trigger pattern completion from partial cue.
    /// Biology: Partial input to CA3 activates full stored pattern via recurrent collaterals.
    /// </summary>
    public EpisodeReplay? TriggerPatternCompletion(IReadOnlyList<(byte hemi, int idx)> partialCue)
    {
        lock (_gate)
        {
            if (partialCue.Count < 3) return null;
            
            // Process cue through DG
            _dgOutputBuffer.Clear();
            _dg.ProcessInput(partialCue, _dgOutputBuffer);
            
            // CA3 pattern completion
            int? matchedEpisodeId = _ca3.CompletePattern(_dgOutputBuffer);
            
            if (matchedEpisodeId == null) return null;
            
            // Find the episode
            for (int i = 0; i < _episodes.Count; i++)
            {
                var ep = _episodes[i];
                if (ep.Id == matchedEpisodeId.Value)
                {
                    return new EpisodeReplay(
                        EpisodeId: ep.Id,
                        Voxels: ep.Voxels.ToArray(),
                        Strength: ep.Strength,
                        Age: _currentStep - ep.CapturedAtStep);
                }
            }
            return null;
        }
    }
    
    /// <summary>Get the most recent/strongest episode for replay.</summary>
    public EpisodeReplay? GetBestReplayCandidate()
    {
        lock (_gate)
        {
            if (_episodes.Count == 0) return null;
            
            Episode? best = null;
            float bestScore = float.MinValue;
            
            for (int i = 0; i < _episodes.Count; i++)
            {
                var e = _episodes[i];
                float score = e.Strength * e.Salience * (1.0f / (1.0f + (_currentStep - e.CapturedAtStep) * 0.001f));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            
            if (best == null) return null;
            
            return new EpisodeReplay(
                EpisodeId: best.Id,
                Voxels: best.Voxels.ToArray(),
                Strength: best.Strength,
                Age: _currentStep - best.CapturedAtStep);
        }
    }

    /// <summary>Sample an episode for replay with temperature-based stochasticity.</summary>
    public EpisodeReplay? SampleReplayCandidate(Random rng, int sampleCount = 10, float temperature = 1.0f)
    {
        lock (_gate)
        {
            if (_episodes.Count == 0) return null;
            sampleCount = Math.Clamp(sampleCount, 1, Math.Min(_episodes.Count, 20));
            temperature = Math.Max(0.05f, temperature);

            Span<float> weights = stackalloc float[sampleCount];
            Span<int> idxs = stackalloc int[sampleCount];
            float totalW = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                int idx = rng.Next(_episodes.Count);
                idxs[i] = idx;

                var e = _episodes[idx];
                long age = _currentStep - e.CapturedAtStep;
                float recency = 1.0f / (1.0f + age * 0.001f);
                float score = e.Strength * e.Salience * recency;

                float w = MathF.Pow(MathF.Max(1e-6f, score), 1.0f / temperature);
                weights[i] = w;
                totalW += w;
            }

            if (totalW <= 1e-9f)
            {
                var e = _episodes[rng.Next(_episodes.Count)];
                return new EpisodeReplay(e.Id, e.Voxels.ToArray(), e.Strength, _currentStep - e.CapturedAtStep);
            }

            float pick = (float)rng.NextDouble() * totalW;
            float acc = 0f;
            int chosen = idxs[0];
            for (int i = 0; i < sampleCount; i++)
            {
                acc += weights[i];
                if (pick <= acc)
                {
                    chosen = idxs[i];
                    break;
                }
            }

            var best = _episodes[chosen];
            return new EpisodeReplay(
                EpisodeId: best.Id,
                Voxels: best.Voxels.ToArray(),
                Strength: best.Strength,
                Age: _currentStep - best.CapturedAtStep);
        }
    }

    public EpisodeReplay? GetReplayPattern(int episodeId)
    {
        lock (_gate)
        {
            for (int i = 0; i < _episodes.Count; i++)
            {
                var ep = _episodes[i];
                if (ep.Id == episodeId)
                {
                    return new EpisodeReplay(
                        EpisodeId: ep.Id,
                        Voxels: ep.Voxels.ToArray(),
                        Strength: ep.Strength,
                        Age: _currentStep - ep.CapturedAtStep);
                }
            }
            return null;
        }
    }

    public EpisodeSummary[] GetEpisodeSummaries()
    {
        lock (_gate)
        {
            var result = new EpisodeSummary[_episodes.Count];
            for (int i = 0; i < _episodes.Count; i++)
            {
                var e = _episodes[i];
                result[i] = new EpisodeSummary(
                    Id: e.Id,
                    VoxelCount: e.Voxels.Count,
                    Strength: e.Strength,
                    Salience: e.Salience,
                    Age: _currentStep - e.CapturedAtStep);
            }
            return result;
        }
    }
    
    private void DecayEpisodes(float dt)
    {
        float decayRate = 0.0001f;
        
        for (int i = _episodes.Count - 1; i >= 0; i--)
        {
            var ep = _episodes[i];
            ep.Strength -= decayRate * dt;
            
            if (ep.Strength <= 0.01f)
            {
                _ca3.ForgetPattern(ep.Id);
                _episodes.RemoveAt(i);
            }
        }
    }
    
    // === INNER CLASSES: Trisynaptic Circuit Components ===
    
    /// <summary>
    /// Dentate Gyrus: Pattern Separation
    /// Biology: Granule cells provide sparse coding via lateral inhibition.
    /// Approximately 10:1 compression ratio from EC input.
    /// </summary>
    private sealed class DentateGyrus
    {
        private readonly float _sparseRatio;
        private readonly int _lateralInhibRadius;
        private readonly Dictionary<int, float> _granuleCellActivity = new();
        private readonly List<(int idx, float act)> _ranked = new(256);

        public DentateGyrus(float sparseRatio, int lateralInhibitionRadius)
        {
            _sparseRatio = sparseRatio;
            _lateralInhibRadius = lateralInhibitionRadius;
        }
        
        /// <summary>
        /// Process input through competitive lateral inhibition.
        /// Returns sparsity ratio (lower = more sparse = better separation).
        /// </summary>
        public float ProcessInput(IReadOnlyList<(byte hemi, int idx)> input, List<int> output)
        {
            if (input.Count == 0) return 1.0f;
            
            _granuleCellActivity.Clear();
            
            // Compute initial activation for each input
            for (int i = 0; i < input.Count; i++)
            {
                int idx = input[i].idx;
                _granuleCellActivity.TryGetValue(idx, out float act);
                _granuleCellActivity[idx] = act + 1.0f;
            }
            
            // Apply winner-take-all competitive inhibition: only the top
            // _sparseRatio fraction of granule cells (by activation) survive.
            int targetCount = Math.Max(1, (int)(_granuleCellActivity.Count * _sparseRatio));

            _ranked.Clear();
            foreach (var kv in _granuleCellActivity)
                _ranked.Add((kv.Key, kv.Value));
            // Descending by activation; in-place sort reuses the buffer (no alloc).
            _ranked.Sort(static (x, y) => y.act.CompareTo(x.act));

            int take = Math.Min(targetCount, _ranked.Count);
            for (int i = 0; i < take; i++)
                output.Add(_ranked[i].idx);

            return output.Count / (float)Math.Max(1, input.Count);
        }
    }
    
    /// <summary>
    /// CA3: Autoassociative Network with Recurrent Collaterals
    /// Biology: Pyramidal cells have extensive recurrent connections (~3% connectivity).
    /// This creates attractor dynamics for pattern completion.
    /// </summary>
    private sealed class CA3Region
    {
        private readonly float _recurrentStrength;
        private readonly int _maxPatterns;
        private readonly Dictionary<int, int[]> _storedPatterns = new(); // episodeId -> pattern
        private readonly Dictionary<(int pre, int post), float> _associations = new();
        
        public int AssociationCount => _associations.Count;
        
        public CA3Region(float recurrentStrength, int maxPatterns)
        {
            _recurrentStrength = recurrentStrength;
            _maxPatterns = maxPatterns;
        }
        
        /// <summary>
        /// Process input through recurrent network.
        /// Returns coherence measure (how well input matches stored patterns).
        /// </summary>
        public float ProcessInput(List<int> dgInput, List<int> output, float dt)
        {
            if (dgInput.Count == 0) return 0f;
            
            // Hebbian learning: strengthen associations between co-active units
            // Cap learning pairs to avoid quadratic blowup
            int maxPairs = Math.Min(dgInput.Count, 8);
            for (int i = 0; i < maxPairs; i++)
            {
                for (int j = i + 1; j < Math.Min(i + 5, maxPairs); j++)
                {
                    var key = (dgInput[i], dgInput[j]);
                    _associations.TryGetValue(key, out float w);
                    w += 0.01f * (1.0f - w);
                    _associations[key] = w;
                }
            }
            
            // Compute activation: only spread through associations FROM active units
            // Instead of scanning ALL associations, look up from each active unit
            var activation = new Dictionary<int, float>(dgInput.Count * 2);
            foreach (int idx in dgInput)
                activation[idx] = 1.0f;

            // Recurrent collateral spread: each active CA3 unit excites the units
            // it is associated with (autoassociative pattern completion via the
            // learned recurrent weights). Bounded by the association cap below.
            foreach (var kv in _associations)
            {
                var (a, b) = kv.Key;
                float w = kv.Value * _recurrentStrength;

                if (activation.TryGetValue(a, out float aAct) && aAct > 0f)
                {
                    activation.TryGetValue(b, out float bAct);
                    activation[b] = bAct + w;
                }
                if (activation.TryGetValue(b, out float bAct2) && bAct2 > 0f)
                {
                    activation.TryGetValue(a, out float aAct2);
                    activation[a] = aAct2 + w;
                }
            }

            // Use stored patterns for completion (fast path)
            if (_storedPatterns.Count > 0)
            {
                var inputSet = new HashSet<int>(dgInput);
                foreach (var kv in _storedPatterns)
                {
                    int overlap = 0;
                    foreach (int idx in kv.Value)
                        if (inputSet.Contains(idx))
                            overlap++;
                    float ratio = overlap / (float)Math.Max(1, kv.Value.Length);
                    if (ratio > 0.2f)
                    {
                        // Partial match - add pattern units to activation
                        foreach (int idx in kv.Value)
                        {
                            activation.TryGetValue(idx, out float a);
                            activation[idx] = a + ratio * _recurrentStrength;
                        }
                    }
                }
            }
            
            // Threshold and output
            float maxAct = 0f;
            foreach (var kv in activation)
            {
                if (kv.Value > 0.5f)
                    output.Add(kv.Key);
                if (kv.Value > maxAct)
                    maxAct = kv.Value;
            }
            
            // Aggressive cap on associations to prevent unbounded growth
            if (_associations.Count > 2000)
            {
                _associations.Clear();
            }
            
            return output.Count > 0 ? maxAct : 0f;
        }
        
        public void StorePattern(List<int> pattern, int episodeId)
        {
            _storedPatterns[episodeId] = pattern.ToArray();
            
            // Evict oldest if over limit
            if (_storedPatterns.Count > _maxPatterns)
            {
                int oldestId = int.MaxValue;
                foreach (var k in _storedPatterns.Keys)
                    if (k < oldestId) oldestId = k;
                _storedPatterns.Remove(oldestId);
            }
        }
        
        public int? CompletePattern(List<int> partialInput)
        {
            if (partialInput.Count == 0) return null;
            
            var inputSet = new HashSet<int>(partialInput);
            int? bestMatch = null;
            float bestOverlap = 0.3f; // Minimum 30% overlap required
            
            foreach (var kv in _storedPatterns)
            {
                int overlap = 0;
                foreach (int idx in kv.Value)
                    if (inputSet.Contains(idx))
                        overlap++;
                
                float ratio = overlap / (float)Math.Max(1, kv.Value.Length);
                if (ratio > bestOverlap)
                {
                    bestOverlap = ratio;
                    bestMatch = kv.Key;
                }
            }
            
            return bestMatch;
        }
        
        public void ForgetPattern(int episodeId)
        {
            _storedPatterns.Remove(episodeId);
        }
    }
    
    /// <summary>
    /// CA1: Comparator and Output
    /// Biology: Receives CA3 output via Schaffer collaterals + direct EC input.
    /// Computes novelty/mismatch signal when CA3 prediction differs from EC input.
    /// </summary>
    private sealed class CA1Region
    {
        private readonly float _noveltyThreshold;
        
        public CA1Region(float noveltyThreshold)
        {
            _noveltyThreshold = noveltyThreshold;
        }
        
        /// <summary>
        /// Compare CA3 output (prediction) with direct EC input.
        /// Returns novelty signal (high when mismatch detected).
        /// </summary>
        public float ProcessInput(List<int> ca3Input, IReadOnlyList<(byte hemi, int idx)> ecInput, List<int> output)
        {
            var ca3Set = new HashSet<int>(ca3Input);
            var ecSet = new HashSet<int>();
            foreach (var s in ecInput)
                ecSet.Add(s.idx);
            
            // Compute mismatch
            int match = 0;
            int mismatch = 0;
            
            foreach (int idx in ecSet)
            {
                output.Add(idx); // Pass through to subiculum
                if (ca3Set.Contains(idx))
                    match++;
                else
                    mismatch++;
            }
            
            // Add CA3 predictions that weren't in EC (completed patterns)
            foreach (int idx in ca3Set)
            {
                if (!ecSet.Contains(idx))
                {
                    output.Add(idx);
                    mismatch++;
                }
            }
            
            // Novelty signal: high when more mismatch than match
            float total = match + mismatch;
            if (total < 1) return 0f;
            
            return mismatch / total;
        }
    }
    
    private sealed class Episode
    {
        public int Id { get; }
        public long CapturedAtStep { get; }
        public List<EpisodeVoxel> Voxels { get; }
        public float Salience { get; }
        public float Strength { get; set; }
        public int[] DgPattern { get; } // Sparse DG representation
        
        public Episode(int id, long capturedAtStep, List<EpisodeVoxel> voxels, float salience, float strength, int[] dgPattern)
        {
            Id = id;
            CapturedAtStep = capturedAtStep;
            Voxels = voxels;
            Salience = salience;
            Strength = strength;
            DgPattern = dgPattern;
        }
    }
}

// === DTOs ===

public readonly record struct EpisodeVoxel(byte Hemisphere, int Index);

public readonly record struct EpisodeReplay(
    int EpisodeId,
    EpisodeVoxel[] Voxels,
    float Strength,
    long Age);

public readonly record struct EpisodeSummary(
    int Id,
    int VoxelCount,
    float Strength,
    float Salience,
    long Age);

public readonly record struct HippocampusState(
    int EpisodeCount,
    int AssociationCount,
    long OldestEpisodeAge,
    int TotalVoxelsCaptured,
    float DGSparsity,
    float CA3Coherence,
    float CA1NoveltySignal);

public readonly record struct HippocampalOutput(
    float DGSparsity,
    float CA3Coherence,
    float NoveltySignal,
    bool PatternCompleted,
    int[] OutputIndices);
