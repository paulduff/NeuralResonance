using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using static NRE.Core.Engine.NeuralVolume;

namespace NRE.Core.Engine;

public sealed class NreEngine : IDisposable
{
    private readonly object _gate = new();
    private const float LanguageStepDt = 1f / 60f;
    /// <summary>Dedicated thread pool for parallel sim work (NOT the .NET ThreadPool).</summary>
    private readonly SimWorkerPool _simPool = new(3); // 3 workers + caller = 4-way
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate) Running = false;
        _simPool.Dispose();
        _tlsSpikesLeft.Dispose();
        _tlsSpikesRight.Dispose();
    }

    private readonly NreEngineOptions _opt;
    private readonly Random _rng;
    
        // Fast deterministic RNG (xorshift32) for high-frequency noise injection.
    // Avoids allocations and improves reproducibility (Folded Archive principle: deterministic runs).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint XorShift32(ref uint s)
    {
        s ^= s << 13;
        s ^= s >> 17;
        s ^= s << 5;
        return s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextInt(ref uint s, int maxExclusive)
    {
        // maxExclusive must be > 0
        return (int)(XorShift32(ref s) % (uint)maxExclusive);
    }

    private void WriteDiagnostic(string message)
    {
        if (_opt.EnableConsoleDiagnostics)
        {
            Console.WriteLine($"[NRE] {message}");
        }
    }

    // Per-hemisphere noise seeds (persist across steps)
    private uint _noiseSeedLeft;
    private uint _noiseSeedRight;
public NeuralVolume Left { get; }
    public NeuralVolume Right { get; }

    public SynapseMap SynLeft { get; } = new();
    public SynapseMap SynRight { get; } = new();

    /// <summary>Corpus Callosum: left->right and right->left bridging maps.</summary>
    public SynapseMap CallosumLR { get; } = new();
    public SynapseMap CallosumRL { get; } = new();

    public NeuromodulatorField Mods { get; } = new();
    public PonsNucleus Pons { get; } = new();
    public BrainstemMotorCircuit BrainstemMotor { get; } = new();

    // User offsets (debug/experimentation only). Internal neuromodulators remain biologically sourced.
    private float _userDopaOffset01;
    private float _userSeroOffset01;
    private float _userNoradOffset01;

    public ResonanceDetector ResonLeft { get; }
    public ResonanceDetector ResonRight { get; }

    // === NEW SUBSYSTEMS (spec.md compliant) ===
    
    /// <summary>Thalamus: 40Hz Master Clock with Binding Priority.</summary>
    public Thalamus Thalamus { get; } = new();
    
    /// <summary>Hippocampus: Episodic Memory with Hebbian Plasticity.</summary>
    public Hippocampus Hippocampus { get; } = new();
    
    /// <summary>Amygdala: Salience Gating with Noradrenaline Pulses.</summary>
    public Amygdala Amygdala { get; } = new();
    
    /// <summary>Cerebellum: Error Correction and Activity Smoothing.</summary>
    public Cerebellum Cerebellum { get; } = new();
    
    /// <summary>Sleep Controller: REM/Wake State and Memory Consolidation.</summary>
    public SleepController Sleep { get; } = new();
    
    /// <summary>Predictive Coding: Hierarchical prediction and error signaling.</summary>
    public PredictiveCoding Prediction { get; } = new();
    
    // === ENTRY 025: Enhanced Neural Systems ===
    
    /// <summary>Cortical Microcircuit: E/I neuron populations with interneuron subtypes.</summary>
    public CorticalMicrocircuit Microcircuit { get; }
    
    /// <summary>Enhanced Neuromodulator System: Receptor dynamics, desensitization, reuptake.</summary>
    public NeuromodulatorSystem NeuromodSystem { get; } = new();
    
    /// <summary>Synaptic Tagging: LTP consolidation via tag-and-capture.</summary>
    public SynapticTagging SynapticTags { get; } = new();
    
    /// <summary>Sensory hierarchy: hierarchical feature extraction (V1 to IT, A1 to parabelt).</summary>
    public SensoryHierarchy SensoryHierarchy { get; }
    
    /// <summary>Attention System: Biased competition and selective amplification.</summary>
    public AttentionSystem Attention { get; }
    
    // === ENTRY 027: Language System ===
    
    /// <summary>Language System: Wernicke's, Broca's, dual-stream processing.</summary>
    public LanguageSystem Language { get; } = new();
    
    // === ENTRY 028: P0 Cognitive Systems ===
    
    /// <summary>Basal Ganglia Circuit: Action selection via direct/indirect pathways.</summary>
    public BasalGangliaCircuit BasalGanglia { get; }
    
    /// <summary>Reward Prediction System: VTA dopamine RPE signaling.</summary>
    public RewardPredictionSystem RewardPrediction { get; }
    
    /// <summary>Working Memory PFC: Sustained activity via attractor dynamics.</summary>
    public WorkingMemoryPFC WorkingMemory { get; }
    
    /// <summary>Systems Consolidation: Sleep-dependent hippocampus-to-cortex transfer.</summary>
    public SystemsConsolidation Consolidation { get; }

    public long StepIndex { get; private set; }
    public bool Running { get; private set; }
    
    // Wall-clock uptime tracking (matches real elapsed time, not simulated time)
    private readonly System.Diagnostics.Stopwatch _wallClock = new();
    public float WallClockUptimeSeconds => (float)_wallClock.Elapsed.TotalSeconds;
    
    // Step performance tracking
    private float _lastStepMs;
    
    // Microcircuit per-voxel cache. Microcircuit parameters are SHARED between hemispheres,
    // so a single cache (not [ThreadStatic]) is correct: it avoids duplicate rebuild work
    // and out-of-sync rebuild cadences across hemisphere workers. The cache is populated
    // sequentially before the parallel hemisphere step (single writer), then read concurrently
    // by both hemispheres (read-only).
    private float[]? _mcThresholdCache;
    private float[]? _mcSignCache;
    private bool[]? _mcVipCache;
    private byte[]? _mcTypeCache;
    private float[]? _mcLeakCache;

    private int _mcCacheCountdown;
    private bool _mcCacheDirty = true;


    // Sparse anatomy iteration cache (indices where vol.Active == true).
    // Built lazily per hemisphere volume and reused every step. Callers that mutate
    // Active[] (e.g. anatomy edits, structural pruning that deactivates voxels) must
    // call InvalidateActiveIndexCache() to force a rebuild.
    private int[]? _activeIdxLeft;
    private int[]? _activeIdxRight;

    /// <summary>
    /// Invalidate the cached sparse active-voxel indices (Left and Right). Call after any
    /// mutation of Left.Active or Right.Active so the next Step() rebuilds the cache.
    /// </summary>
    public void InvalidateActiveIndexCache()
    {
        lock (_gate)
        {
            _activeIdxLeft = null;
            _activeIdxRight = null;
        }
    }

    // Reused per-hemisphere predictive-coding threshold modifiers (avoids per-step allocations).
    private readonly float[] _predErrorCacheLeft = new float[16];
    private readonly float[] _predErrorCacheRight = new float[16];

    // Reused per-hemisphere cerebellar inhibition cache (avoids per-step allocations).
    private readonly float[] _regionInhibCacheLeft = new float[16];
    private readonly float[] _regionInhibCacheRight = new float[16];

    // Reused per-hemisphere thread-local spike buffers for parallel voxel update.
    // ThreadLocal allocation is non-trivial (global slot table); cache once per hemisphere.
    private readonly ThreadLocal<List<int>> _tlsSpikesLeft = new(() => new List<int>(256), trackAllValues: true);
    private readonly ThreadLocal<List<int>> _tlsSpikesRight = new(() => new List<int>(256), trackAllValues: true);

    // Precomputed inhibition kernel (dx,dy,dz offsets and distance weight) for the configured radius.
    private int _inhibKernelRadius = -1;
    private short[]? _inhibDx;
    private short[]? _inhibDy;
    private short[]? _inhibDz;
    private float[]? _inhibW;


    private static int[] BuildActiveIndexCache(bool[] active)
    {
        // Two-pass: count then fill (no LINQ, no allocations beyond final array).
        int count = 0;
        for (int i = 0; i < active.Length; i++)
            if (active[i]) count++;

        var idx = new int[count];
        int w = 0;
        for (int i = 0; i < active.Length; i++)
            if (active[i]) idx[w++] = i;

        return idx;
    }
private float _avgStepMs;
    public float AvgStepMs => _avgStepMs;
    /// <summary>Per-layer timing breakdown (ms) from last step. Read from API for diagnostics.</summary>
    public volatile string StepTimingBreakdown = "";
    public volatile string HemiTimingDetail = "";
    public float SimRatio => StepIndex > 0 ? (StepIndex / 60f) / Math.Max(0.001f, WallClockUptimeSeconds) : 1f;


    // === Entry 019: Multi-timescale stratification scheduler ===
    private float _accInter;
    private float _accSlow;
    private float _interDt;
    private float _slowDt;

    // Cached slow fields (IDF / pons packet) used by fast path.
    private ModulationPacket _ponsCached = new(0.35f, 0.45f, 0.10f, 4.0f);

    // === Entry 020: Active perception sensor framing (gaze/focus) ===
    private struct SensorFrame
    {
        public float GazeX01;      // 0..1
        public float GazeY01;      // 0..1
        public float VisualFocus01; // 0..1 (1=focused, 0=explore)
        public float AudioShift01; // -1..1 shift relative to tonotopic center
    }

    private SensorFrame _sensorFrame = new() { GazeX01 = 0.5f, GazeY01 = 0.5f, VisualFocus01 = 0.5f, AudioShift01 = 0f };

    // Interoception oscillator phase. Slow heartbeat-like (~0.5 Hz) modulation
    // so signal voxels keep firing instead of saturating at a constant Vm.
    private float _interoceptionPhase;
    private const float InteroceptionFrequencyHz = 0.5f;

    // Continuous persistence: accumulator (real wall-clock seconds) until the
    // next snapshot write. Reset to 0 after each successful write. Set to a
    // negative value on Restore so the first write happens late, not immediately
    // after a restored snapshot.
    private double _persistenceSecondsAccum;


    // Spike accumulation between slow ticks for homeostatic regulation.
    private long _fastStepsSinceSlow;
    private long _spikeCountLSinceSlow;
    private long _spikeCountRSinceSlow;



// Sensory cortex stimulus state (purely neural generators)
private VisualStimulusState _visual = new();
private AuditoryStimulusState _auditory = new();
private AuditoryStimulusState _auditorySelf = new() { Enabled = false, IsSelf = true };

// Pixel-level vision: when a frame is supplied via SetVisualFrame, both the
// SensoryHierarchy (V1 Gabor filtering) AND the voxel-level occipital cortex
// receive the real pixel buffer instead of a synthesised sinusoidal grating.
// This is the substrate-side change that lets external imagers (world-sim
// camera, real webcam) actually drive the brain rather than feeding it
// pre-categorised symbolic stimuli.
private VisualFrameState _visualFrame = new();
private sealed class VisualFrameState
{
    public bool HasFrame;
    public int Width;
    public int Height;
    // Pixel intensities in 0..1, row-major (index = y * Width + x). Owned
    // by this class; SetVisualFrame copies caller's buffer to avoid sharing
    // mutable state with the simulator threads.
    public float[]? Pixels;
}

private sealed class VisualStimulusState
{
    public float Intensity01 = 0.15f; // 0..1
    public float SpeedHz = 1.0f;
    public float SpatialFreq = 0.18f; // cycles per voxel
    public bool Enabled = true;
    public float Phase;
}

private sealed class AuditoryStimulusState
{
    public float Intensity01 = 0.12f; // 0..1
    public float ToneHz = 220f;
    public bool Enabled = true;
    public float Phase;

    // Reafference (self-generated voice) support
    public bool IsSelf = false;
    public float HoldSeconds = 0f;   // countdown while active
    public string LastText = "";
}

// Homeostatic criticality control
private const float TargetSpikeRate = 180f; // per step (tunable)
private const float HomeoGain = 0.0005f;

    // Packed spike list: hemi + index
    private readonly List<(byte hemi, int idx)> _spikes = new(16384);
    
    // Per-hemisphere spike lists reused to avoid per-tick allocation in parallel stepping.
    private readonly List<(byte hemi, int idx)> _spikesL = new(8192);
    private readonly List<(byte hemi, int idx)> _spikesR = new(8192);
    
    // Extended spike list with region info for subsystems (REUSED - not allocated each step)
    private readonly List<(byte hemi, int idx, byte region)> _spikesWithRegion = new(16384);
    
    // Reusable lists for resonance update (avoid allocations in hot path)
    private readonly List<int> _leftSpikesBuffer = new(8192);
    private readonly List<int> _rightSpikesBuffer = new(8192);

    // Reusable cue buffer for hippocampal pattern completion (avoid allocations in hot path)
    private readonly List<(byte hemi, int idx)> _hippoCueBuffer = new(32);

    // UI "bridge brightness"
    private float _callosalTraffic01;

    // Cached affect/uncertainty scalars for motor outputs (including voice).
    private float _lastPredictionError01; // 0..1 (1 = very uncertain)
    private float _lastValence11;         // -1..+1

    // === Render publishing (decouples simulation tick from UI fetch) ===
    // Split into:
    //  - Fast frame (spikes + scalars) @ ~20Hz
    // Published render payloads are immutable snapshots.
    // Make reads lock-free so UI polling never blocks the simulation tick (biology: readout doesn't stall cortex).
    private volatile RenderFrameFastDto? _publishedFast;

    // === Voice (motor output scaffold; browser TTS via Web Speech API) ===
    // Voice is treated as a motor output gated by action selection (Basal Ganglia).
    // Higher subsystems request utterances (intent); the vocal motor executes them.
    private readonly VocalMotor _vocal;
    private readonly ProtoLexicon _lexicon;

    // === Entry 029: Diphthong Vocal Tract (articulatory synthesis) ===
    public DiphthongVocalTract VocalTract { get; }
    
    // === Entry 030: Peer Bridge (inter-instance communication) ===
    public PeerBridge Peer { get; }

    private SleepPhase _lastVoicePhase = SleepPhase.Awake;
    private long _lastNarrationStep = -10000;
    private long _narrationIntervalSteps = 150; // ~5s at 60Hz
    private int _lastNarrationEpisodes;
    private long _lastUncertaintyVoiceStep = -10_000_000;

    public sealed record VoiceUtterance(long StepIndex, string Text, float Rate = 1.0f, float Pitch = 1.0f, float Volume = 1.0f, string? Gloss = null, string[]? Phonemes = null);

    /// <summary>
    /// Enqueue a short utterance for client-side speech synthesis.
    /// This is primarily used for manual/test endpoints.
    /// Internally, subsystems should request utterances through the vocal motor.
    /// </summary>
    public void EnqueueVoice(string text, float urgency01 = 0.35f)
    {
        var drive = new VocalMotor.VoiceDrive(
            Arousal01: Math.Clamp(GetArousal01(), 0f, 1f),
            Urgency01: Math.Clamp(urgency01, 0f, 1f),
            Valence11: GetValence11(),
            Certainty01: GetCertainty01());

        _vocal.ForceSpeak(text, drive, StepIndex);
        
        // Feed through diphthong vocal tract for articulatory synthesis
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            VocalTract.EnqueueWord(word);
        
        // Peer send happens in DequeueVoice to avoid double-sending
    }

    /// <summary>Dequeue up to <paramref name="max"/> utterances. Also sends to peers.</summary>
    public VoiceUtterance[] DequeueVoice(int max = 8)
    {
        var items = _vocal.Dequeue(max);
        // Send each dequeued utterance to peers
        foreach (var item in items)
        {
            SendSpeechToPeers(item.Text, item.Rate, item.Volume, item.Phonemes);
        }
        return items;
    }

    // Cross-module traffic events (option 3: only cross-module traffic flashes)
    private readonly ConcurrentBag<TrafficEvent> _trafficEventsBag = new();
    private readonly List<TrafficEvent> _trafficEvents = new(8192);

    private sealed record TrafficEvent(byte PreH, byte PreX, byte PreY, byte PreZ, byte PreRegion,
                                       byte PostH, byte PostX, byte PostY, byte PostZ, byte PostRegion,
                                       byte StrengthByte);

    // Homeostasis measurement (EMA spike density per step)
    private float _spikeEmaL;
    private float _spikeEmaR;
    
    // Global ATP tracking for sleep controller (cached, updated every N steps)
    private float _globalAtp01 = 1.0f;
    private int _atpComputeCounter;
    private const int AtpComputeInterval = 8; // Compute ATP every 8 steps instead of every step

    public NreEngine(NreEngineOptions opt, int seed = 12345)
    {
        _opt = opt;
        _rng = new Random(seed);

        // Deterministic per-hemisphere noise seeds
        unchecked
        {
            _noiseSeedLeft = (uint)(seed * 747796405 + 2891336453u);
            _noiseSeedRight = (uint)(seed * 277803737 + 429496729u);
        }

        _vocal = new VocalMotor(_rng);
        _lexicon = new ProtoLexicon(_rng);
        VocalTract = new DiphthongVocalTract();
        Peer = new PeerBridge();

        _interDt = 1.0f / MathF.Max(1.0f, _opt.IntermediateUpdateHz);
        _slowDt  = 1.0f / MathF.Max(1.0f, _opt.SlowUpdateHz);

        WriteDiagnostic($"Grid size: {opt.W}x{opt.H}x{opt.D} = {opt.W * opt.H * opt.D} voxels per hemisphere");
        Left = new NeuralVolume(opt.W, opt.H, opt.D, baseHz: 4.0f, oscGain: opt.OscGain, baseThreshold: opt.BaseThreshold, energyMax: opt.EnergyMax, signalRingSize: Math.Max(Math.Max(Math.Max(_opt.MaxDelayTicks, _opt.CallosalMaxDelayTicks), Math.Max(_opt.FeedbackMaxDelayTicks, _opt.AfferentDelayMaxTicks)), 4) + 2);
        Right = new NeuralVolume(opt.W, opt.H, opt.D, baseHz: 4.0f, oscGain: opt.OscGain, baseThreshold: opt.BaseThreshold, energyMax: opt.EnergyMax, signalRingSize: Math.Max(Math.Max(Math.Max(_opt.MaxDelayTicks, _opt.CallosalMaxDelayTicks), Math.Max(_opt.FeedbackMaxDelayTicks, _opt.AfferentDelayMaxTicks)), 4) + 2);

        // Region layout (visual + monitoring)
        // IMPORTANT: The renderer can mirror the right hemisphere in X (x' = w-1-x) so the visible
        // brain is a true sagittal mirror (folds/noise included). When that is enabled, both volumes
        // must share the SAME internal anatomical layout; the visual mirror will produce the right-side
        // anatomy automatically. If we also mirror deep nuclei here, they get "double-mirrored" and land
        // in biologically-wrong positions on the right.
        ApplyRegionLayout(Left, isLeftHemisphere: true);
        ApplyRegionLayout(Right, isLeftHemisphere: true);

        ResonLeft = new ResonanceDetector(opt.W, opt.H, opt.D);
        ResonRight = new ResonanceDetector(opt.W, opt.H, opt.D);

        // Build connectivity --- LEFT and RIGHT hemispheres in parallel (fully independent)
        {
            int seedL = _rng.Next(), seedR = _rng.Next();
            Parallel.Invoke(
                () => BuildInitialConnectivityBiological(SynLeft, Left, new Random(seedL)),
                () => BuildInitialConnectivityBiological(SynRight, Right, new Random(seedR))
            );
        }
        BuildCorpusCallosum();
        
        // === ENTRY 025: Initialize new subsystems ===
        
        // Cortical Microcircuit: E/I populations
        Microcircuit = new CorticalMicrocircuit();
        if (opt.EnableCorticalMicrocircuit)
        {
            Microcircuit.PyramidalRatio = opt.PyramidalFraction;
            Microcircuit.PVRatio = opt.PVFraction;
            Microcircuit.SOMRatio = opt.SOMFraction;
            Microcircuit.VIPRatio = opt.VIPFraction;
            Microcircuit.Initialize(opt.W, opt.H, opt.D, Left.RegionId, _rng);
        }
        
        // Enhanced Neuromodulator System
        if (opt.EnableEnhancedNeuromodulators)
        {
            NeuromodSystem.DopamineTonicSetpoint = opt.DopamineTonicSetpoint;
            NeuromodSystem.NETonicSetpoint = opt.NETonicSetpoint;
            NeuromodSystem.SerotoninTonicSetpoint = opt.SerotoninTonicSetpoint;
            NeuromodSystem.AChTonicSetpoint = opt.AChTonicSetpoint;
            NeuromodSystem.ReceptorDesensitizationRate = opt.ReceptorDesensitizationRate;
            NeuromodSystem.ReceptorResensitizationRate = opt.ReceptorResensitizationRate;
        }
        
        // Synaptic Tagging and Capture
        if (opt.EnableSynapticTagging)
        {
            SynapticTags.TagThreshold = opt.SynapticTagThreshold;
            SynapticTags.PRPSynthesisThreshold = opt.PRPSynthesisThreshold;
            SynapticTags.TagHalfLifeSec = opt.TagHalfLifeSec;
            SynapticTags.PRPHalfLifeSec = opt.PRPHalfLifeSec;
            SynapticTags.ConsolidationBoost = opt.ConsolidationBoost;
        }
        
        // Sensory Hierarchy
        SensoryHierarchy = new SensoryHierarchy(
            opt.SensoryVisualWidth, 
            opt.SensoryVisualHeight, 
            opt.SensoryAuditoryWidth);
        
        // Attention System
        Attention = new AttentionSystem(opt.W, opt.H, opt.D, opt.AttentionFeatureChannels);
        if (opt.EnableAttentionSystem)
        {
            Attention.SpatialSigma = opt.AttentionSpatialSigma;
            Attention.BottomUpGain = opt.AttentionBottomUpGain;
            Attention.TopDownGain = opt.AttentionTopDownGain;
            Attention.SuppressionStrength = opt.AttentionSuppressionStrength;
            Attention.EnhancementStrength = opt.AttentionEnhancementStrength;
            Attention.IORDurationSec = opt.AttentionIORDuration;
        }
        
        // === ENTRY 028: P0 Cognitive Systems ===
        
        // Basal Ganglia Circuit
        BasalGanglia = new BasalGangliaCircuit(opt.BasalGangliaChannels);
        
        // Reward Prediction System (VTA dopamine)
        RewardPrediction = new RewardPredictionSystem(
            discountFactor: opt.RPEDiscountFactor,
            learningRate: opt.RPELearningRate,
            tonicSetpoint: opt.RPETonicDopamineSetpoint,
            phasicDecayRate: opt.RPEPhasicDecayRate,
            burstMagnitude: opt.RPEBurstMagnitude,
            pauseMagnitude: opt.RPEPauseMagnitude);
        
        // Working Memory PFC
        WorkingMemory = new WorkingMemoryPFC(
            numSlots: opt.WorkingMemorySlots,
            patternSize: opt.WorkingMemoryPatternSize,
            recurrentStrength: opt.WorkingMemoryRecurrentStrength,
            lateralInhibition: opt.WorkingMemoryLateralInhibition,
            decayRate: opt.WorkingMemoryDecayRate,
            gatingThreshold: opt.WorkingMemoryGatingThreshold);
        
        // Systems Consolidation
        Consolidation = new SystemsConsolidation(
            maxTraces: opt.ConsolidationMaxTraces,
            corticalLearningRate: opt.ConsolidationCorticalLearningRate,
            hippocampalDecayRate: opt.ConsolidationHippocampalDecayRate,
            consolidationThreshold: opt.ConsolidationThreshold,
            replaysForConsolidation: opt.ConsolidationReplaysRequired,
            tripletBonus: opt.ConsolidationTripletBonus);
        
        // Initialize cached counts so status shows correct values before first slow tick
        _cachedSynapseCount = SynLeft.TotalSynapses + SynRight.TotalSynapses
                            + CallosumLR.TotalSynapses + CallosumRL.TotalSynapses;
        int n = 0;
        for (int z = 0; z < opt.D; z++)
        for (int y = 0; y < opt.H; y++)
        for (int x = 0; x < opt.W; x++)
        {
            if (Left.Active[Left.IndexOf(x, y, z)]) n++;
            if (Right.Active[Right.IndexOf(x, y, z)]) n++;
        }
        _cachedNeuronCount = n;
        
        // Initialize per-region counts
        for (int z = 0; z < opt.D; z++)
        for (int y = 0; y < opt.H; y++)
        for (int x = 0; x < opt.W; x++)
        {
            void InitRegCount(NeuralVolume vol)
            {
                if (!vol.Active[vol.IndexOf(x, y, z)]) return;
                byte r = vol.RegionId[vol.IndexOf(x, y, z)];
                if (r == 255) return;
                _cachedRegionNeuronCount.TryGetValue(r, out int c);
                _cachedRegionNeuronCount[r] = c + 1;
            }
            InitRegCount(Left);
            InitRegCount(Right);
        }
    }

    /// <summary>Count active neurons across both hemispheres.</summary>
    public int GetTotalNeurons()
    {
        lock (_gate)
        {
            int count = 0;
            int w = _opt.W, h = _opt.H, d = _opt.D;
            for (int z = 0; z < d; z++)
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (Left.Active[Left.IndexOf(x, y, z)]) count++;
                if (Right.Active[Right.IndexOf(x, y, z)]) count++;
            }
            return count;
        }
    }

    /// <summary>Count total synapses across all maps (intra-hemi + callosal).</summary>
    public int GetTotalSynapses()
    {
        lock (_gate)
        {
            return SynLeft.TotalSynapses
                 + SynRight.TotalSynapses
                 + CallosumLR.TotalSynapses
                 + CallosumRL.TotalSynapses;
        }
    }

    public AnatomyValidationReportDto GetAnatomyValidationReport()
        => AnatomyValidationHarness.Validate(Left, Right, _opt);

    public EngineStatusDto GetStatus(float dtSeconds)
    {
        // Built on the API/UI thread; never blocks the sim loop.
        return BuildStatusSnapshot(dtSeconds);
    }

    private EngineStatusDto BuildStatusSnapshot(float dtSeconds)
    {
        // Completely lock-free; all reads are on fields written atomically by Step().
        bool running = Running;
        long step = StepIndex;
        int totalNeurons = _cachedNeuronCount;
        int totalSynapses = _cachedSynapseCount;
        
        // Everything below is lock-free (subsystems snapshot their own state)
        var mods = Mods.Snapshot();
        var pons = Pons.Snapshot();
        
        // Capture subsystem states outside of _gate lock to avoid deadlock
        var thalamicState = Thalamus.Snapshot();
        var sleepState = Sleep.Snapshot();
        var hippoState = Hippocampus.Snapshot();
        var amygState = Amygdala.Snapshot();
        var cerebState = Cerebellum.Snapshot();
        var predState = Prediction.Snapshot();
        
        // Entry 025: Capture new subsystem states
        var microState = _opt.EnableCorticalMicrocircuit ? Microcircuit.GetStats() : default;
        var neuromodState = _opt.EnableEnhancedNeuromodulators ? NeuromodSystem.Snapshot() : default;
        var tagState = _opt.EnableSynapticTagging ? SynapticTags.Snapshot() : default;
        var sensoryState = _opt.EnableSensoryHierarchy ? SensoryHierarchy.Snapshot() : default;
        var attentionState = _opt.EnableAttentionSystem ? Attention.Snapshot() : default;
        
        // Entry 027: Language System
        var langState = _opt.EnableLanguageSystem ? Language.Snapshot() : default;
        
        return new EngineStatusDto(
            running,
            step,
            dtSeconds,
            _opt.W, _opt.H, _opt.D,
            mods,
            pons,
            totalNeurons,
            totalSynapses,
            Thalamus: new ThalamicStatusDto(
                thalamicState.FrequencyHz,
                thalamicState.Phase,
                thalamicState.IsAtPulse,
                thalamicState.PulseAmplitude,
                thalamicState.PulseCount,
                thalamicState.Mode.ToString(),
                thalamicState.TRNInhibition,
                thalamicState.SpindleActive,
                thalamicState.SpindleAmplitude),
            Sleep: new SleepStatusDto(
                sleepState.Phase.ToString(),
                sleepState.PhaseTimerSeconds,
                sleepState.SleepPressure,
                sleepState.ConsolidationCycles,
                sleepState.ReplaysTriggered,
                sleepState.IsDreaming,
                sleepState.SensorsConnected),
            Hippocampus: new HippocampusStatusDto(
                hippoState.EpisodeCount,
                hippoState.AssociationCount,
                hippoState.OldestEpisodeAge,
                hippoState.TotalVoxelsCaptured,
                hippoState.DGSparsity,
                hippoState.CA3Coherence,
                hippoState.CA1NoveltySignal),
            Amygdala: new AmygdalaStatusDto(
                amygState.SalientVoxelCount,
                amygState.NoradrenalinePulseActive,
                amygState.PulseIntensity,
                amygState.PulseRemainingMs,
                amygState.SalientEventsPerSecond,
                amygState.LAActivity,
                amygState.BasalActivity,
                amygState.CeAOutput,
                amygState.ITCGating),
            Cerebellum: new CerebellumStatusDto(
                cerebState.GlobalPredictionError,
                cerebState.SmoothedActivity,
                cerebState.RegionCount,
                cerebState.ClimbingFiberSignal,
                cerebState.MossyFiberActivity,
                cerebState.PurkinjeOutput,
                cerebState.DeepNucleiOutput),
            Prediction: new PredictionStatusDto(
                predState.GlobalError,
                predState.GlobalPrecision),
            // Entry 025: New subsystem DTOs
            Microcircuit: _opt.EnableCorticalMicrocircuit ? new MicrocircuitStatusDto(
                microState.PyramidalCount,
                microState.PVCount,
                microState.SOMCount,
                microState.VIPCount,
                microState.SubcorticalCount,
                microState.MeanAdaptation) : null,
            NeuromodSystem: _opt.EnableEnhancedNeuromodulators ? new NeuromodSystemStatusDto(
                neuromodState.DopamineTonic,
                neuromodState.DopaminePhasic,
                neuromodState.D1Sensitivity,
                neuromodState.D2Sensitivity,
                neuromodState.NETonic,
                neuromodState.NEPhasic,
                neuromodState.BetaSensitivity,
                neuromodState.SerotoninTonic,
                neuromodState.SerotoninPhasic,
                neuromodState.AChTonic,
                neuromodState.AChPhasic,
                neuromodState.EffectiveThresholdMod,
                neuromodState.EffectiveLearningMod,
                neuromodState.EffectiveGainMod) : null,
            SynapticTagging: _opt.EnableSynapticTagging ? new SynapticTaggingStatusDto(
                tagState.UnconsolidatedTags,
                tagState.ConsolidatedTags,
                tagState.NeuronsWithPRP,
                tagState.MeanTagStrength,
                tagState.TotalConsolidations) : null,
            Sensory: _opt.EnableSensoryHierarchy ? new SensoryHierarchyStatusDto(
                sensoryState.V1MeanActivity,
                sensoryState.V2MeanActivity,
                sensoryState.V4MeanActivity,
                sensoryState.ITMeanActivity,
                sensoryState.A1MeanActivity,
                sensoryState.BeltMeanActivity,
                sensoryState.ParabeltMeanActivity,
                sensoryState.DominantObject.channel,
                sensoryState.DominantObject.activation) : null,
            Attention: _opt.EnableAttentionSystem ? new AttentionStatusDto(
                attentionState.ActiveFoci,
                attentionState.MeanPriority,
                attentionState.MaxPriority,
                attentionState.IORCount) : null,
            Language: _opt.EnableLanguageSystem ? new LanguageStatusDto(
                langState.LexiconSize,
                langState.PhonologicalBufferCount,
                langState.ActiveSemanticNodes,
                langState.ComprehensionConfidence,
                langState.ProductionReadiness,
                langState.WernickeActivity,
                langState.BrocaActivity,
                langState.PendingOutputWords) : null,
            // Entry 028: P0 Systems
            BasalGanglia: _opt.EnableBasalGangliaCircuit ? CreateBasalGangliaStatus() : null,
            RewardPrediction: _opt.EnableRewardPrediction ? CreateRewardPredictionStatus() : null,
            WorkingMemory: _opt.EnableWorkingMemory ? CreateWorkingMemoryStatus() : null,
            Consolidation: _opt.EnableSystemsConsolidation ? CreateConsolidationStatus() : null,
            VocalTract: CreateVocalTractStatus(),
            PeerBridge: CreatePeerBridgeStatus(),
            WallClockUptimeSeconds: WallClockUptimeSeconds,
            AvgStepMs: AvgStepMs,
            SimRatio: SimRatio);
    }

    public VocalTractStatusDto CreateVocalTractStatus()
    {
        var s = VocalTract.CurrentState;
        var f = VocalTract.LastFormant;
        return new VocalTractStatusDto(
            IsSpeaking: VocalTract.IsSpeaking,
            PendingGestures: VocalTract.PendingGestures,
            F0: f.F0, F1: f.F1, F2: f.F2, F3: f.F3,
            Amplitude: f.Amplitude,
            JawOpen: s.JawOpen, TongueHeight: s.TongueHeight, TongueAdvance: s.TongueAdvance,
            LipRound: s.LipRound, VelumHeight: s.VelumHeight, Voicing: s.Voicing);
    }

    public PeerBridgeStatusDto CreatePeerBridgeStatus()
    {
        var snap = Peer.GetSnapshot();

        // PERF: avoid LINQ allocations in UI/status paths.
        var peersSrc = snap.Peers;
        var peers = new PeerStatusDto[peersSrc.Length];
        for (int i = 0; i < peersSrc.Length; i++)
        {
            var p = peersSrc[i];
            peers[i] = new PeerStatusDto(
                Id: p.Id,
                Name: p.Name,
                LastHeardAt: p.LastHeardAt.ToString("HH:mm:ss"),
                SpeechReceived: p.SpeechSegmentsReceived,
                SpeechSent: p.SpeechSegmentsSent,
                Arousal: p.LastState.Arousal01,
                Valence: p.LastState.Valence11,
                IsSpeaking: p.LastState.IsSpeaking);
        }

        return new PeerBridgeStatusDto(
            InstanceId: snap.InstanceId,
            InstanceName: snap.InstanceName,
            PeerCount: snap.PeerCount,
            PendingIncomingSpeech: snap.PendingIncomingSpeech,
            Peers: peers);
    }
    
    private BasalGangliaStatusDto CreateBasalGangliaStatus()
    {
        var state = BasalGanglia.Snapshot();
        return new BasalGangliaStatusDto(
            state.SelectedChannel,
            state.SelectionConfidence,
            state.DirectPathwayActivity,
            state.IndirectPathwayActivity,
            state.STNActivity,
            state.ThalamicGating,
            state.DopamineLevel,
            state.TotalSelections,
            state.D1Activation,
            state.D2Activation);
    }
    
    private RewardPredictionStatusDto CreateRewardPredictionStatus()
    {
        var state = RewardPrediction.Snapshot();
        return new RewardPredictionStatusDto(
            state.TonicDopamine,
            state.PhasicDopamine,
            state.EffectiveDopamine,
            state.LastRPE,
            state.CurrentStateValue,
            state.TotalRewards,
            state.CumulativeReward,
            state.MeanRPE,
            state.RPEVariance,
            state.LearnedStates,
            state.LearnedActions);
    }
    
    private WorkingMemoryStatusDto CreateWorkingMemoryStatus()
    {
        var state = WorkingMemory.Snapshot();
        var slots = new SlotStatusDto[state.Slots.Length];
        for (int i = 0; i < state.Slots.Length; i++)
        {
            slots[i] = new SlotStatusDto(
                state.Slots[i].IsActive,
                state.Slots[i].Strength,
                state.Slots[i].Age,
                state.Slots[i].Label);
        }
        return new WorkingMemoryStatusDto(
            state.ActiveSlots,
            state.GateOpenness,
            state.CurrentDopamine,
            state.MeanPersistence,
            state.TotalUpdates,
            state.TotalEvictions,
            slots);
    }
    
    private ConsolidationStatusDto CreateConsolidationStatus()
    {
        var state = Consolidation.Snapshot();
        return new ConsolidationStatusDto(
            state.SlowOscPhase,
            state.SpindleActive,
            state.RippleActive,
            state.TotalTraces,
            state.HippocampusDependent,
            state.CorticalOnly,
            state.Transitional,
            state.TotalReplays,
            state.TotalConsolidations,
            state.TotalTransfers,
            state.TripletAlignments,
            state.ConsolidationQueueSize,
            state.CurrentConsolidationStrength);
    }

    public void Start()
    {
        // Try restoring a prior snapshot BEFORE acquiring the running state.
        // LoadState takes _gate internally; we don't hold it across the file read
        // so a slow disk doesn't stall the caller indefinitely under the lock.
        TryRestorePersistenceSnapshot();

        lock (_gate)
        {
            // Entry 019: reset schedulers and accumulation
            _accInter = 0f;
            _accSlow = 0f;
            _fastStepsSinceSlow = 0;
            _spikeCountLSinceSlow = 0;
            _spikeCountRSinceSlow = 0;

            // Bias the next snapshot write toward "after a full interval" so we
            // don't immediately overwrite a freshly-restored snapshot.
            _persistenceSecondsAccum = 0.0;

            _ponsCached = Pons.StepAndEmit(0f);
            Running = true;
        }
    }

    /// <summary>
    /// Continuous persistence: if PersistenceSnapshotPath is configured and the
    /// file exists, load it as a brain snapshot. Death of the process should not
    /// be death of the self — the brain that boots is the brain that last saved.
    /// </summary>
    private void TryRestorePersistenceSnapshot()
    {
        var path = _opt.PersistenceSnapshotPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!System.IO.File.Exists(path)) return;

        try
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            LoadState(bytes);
            WriteDiagnostic($"Restored brain snapshot from {path} ({bytes.Length:N0} bytes)");
        }
        catch (Exception ex)
        {
            // Don't take down the engine because of a corrupt snapshot; just log
            // and continue with the freshly-initialised brain.
            WriteDiagnostic($"Failed to restore snapshot from {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Continuous persistence: write the current brain state to PersistenceSnapshotPath.
    /// Goes via a .tmp sibling + atomic rename so a crash mid-write never leaves a
    /// corrupt snapshot in place. Called periodically from Step() via the
    /// _persistenceSecondsAccum timer.
    /// </summary>
    private void WritePersistenceSnapshot()
    {
        var path = _opt.PersistenceSnapshotPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        byte[] bytes;
        try
        {
            bytes = SaveState();
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Snapshot serialization failed: {ex.Message}");
            return;
        }

        try
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            var tmp = path + ".tmp";
            System.IO.File.WriteAllBytes(tmp, bytes);
            // Atomic-replace (overwrite if exists). On Windows this maps to MoveFileEx.
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Replace(tmp, path, destinationBackupFileName: null);
            }
            else
            {
                System.IO.File.Move(tmp, path);
            }
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Snapshot write failed for {path}: {ex.Message}");
        }
    }
    public void Stop() { lock (_gate) Running = false; }

    public void SetNeuromodulator(string type, float value)
    {
        // Folded Archive Entry 018: neuromodulators are biologically sourced.
        // The UI/API may apply *offsets* for experimentation, but cannot set absolute values.
        value = Math.Clamp(value, -1f, 1f);
        lock (_gate)
        {
            switch (type.Trim().ToLowerInvariant())
            {
                case "dopamine": _userDopaOffset01 = Math.Clamp(value, -1f, 1f); break;
                case "serotonin": _userSeroOffset01 = Math.Clamp(value, -1f, 1f); break;
                case "noradrenaline":
                case "norepinephrine": _userNoradOffset01 = Math.Clamp(value, -1f, 1f); break;
                default: throw new ArgumentException($"Unknown neuromodulator '{type}'.");
            }
        }
    }

    public void SetPons(float arousal01, float stability01, float resetPressure01, float thetaHz)
    {
        lock (_gate) Pons.Set(arousal01, stability01, resetPressure01, thetaHz);
    }

    public void Inject(string hemisphere, int x, int y, int z, float intensity, int delayTicks = 0)
    {
        x = Math.Clamp(x, 0, _opt.W - 1);
        y = Math.Clamp(y, 0, _opt.H - 1);
        z = Math.Clamp(z, 0, _opt.D - 1);

        lock (_gate)
        {
            var vol = (hemisphere.Trim().ToLowerInvariant() == "right") ? Right : Left;
            vol.EnqueueSignal(x, y, z, new DelayedSignal(delayTicks, intensity));
        }
    }

// --------------------------------------------------------------------
// Sensory cortices: visual (V1) and auditory (A1).
// --------------------------------------------------------------------







/// <summary>Configure the purely-neural Visual Cortex (InferiorOccipital=26) drive.</summary>
public void SetVisualStimulus(float intensity01, float speedHz, float spatialFreq, bool enabled = true)
{
    lock (_gate)
    {
        _visual.Intensity01 = Math.Clamp(intensity01, 0f, 1f);
        _visual.SpeedHz = Math.Clamp(speedHz, 0f, 25f);
        _visual.SpatialFreq = Math.Clamp(spatialFreq, 0.01f, 2.0f);
        _visual.Enabled = enabled;
    }
}

/// <summary>
/// Pixel-level vision: provide a raw frame buffer (intensities in 0..1, row-major).
/// While a frame is set, the brain receives this image both at the
/// <see cref="SensoryHierarchy"/> (V1 Gabor filtering through V2/V4/IT) and at
/// the voxel-level occipital cortex (deterministic retinotopic mapping) — the
/// synthetic sinusoidal grating from <see cref="SetVisualStimulus"/> is
/// overridden. Call <see cref="ClearVisualFrame"/> to revert to the synthetic path.
///
/// Caller's buffer is copied so the simulator does not share mutable state with
/// the caller's render thread.
/// </summary>
public void SetVisualFrame(ReadOnlySpan<float> pixels01, int width, int height)
{
    if (width <= 0 || height <= 0) throw new ArgumentException("Frame dimensions must be positive.");
    if (pixels01.Length < width * height) throw new ArgumentException("Pixel buffer is smaller than width*height.");

    var copy = new float[width * height];
    for (int i = 0; i < copy.Length; i++)
    {
        copy[i] = Math.Clamp(pixels01[i], 0f, 1f);
    }

    lock (_gate)
    {
        _visualFrame.HasFrame = true;
        _visualFrame.Width = width;
        _visualFrame.Height = height;
        _visualFrame.Pixels = copy;
    }
}

/// <summary>
/// Convenience overload: ingest 8-bit grayscale pixels (0..255) from any
/// renderer that emits a byte buffer. Each byte is normalised to 0..1.
/// </summary>
public void SetVisualFrame(ReadOnlySpan<byte> grayscale, int width, int height)
{
    if (width <= 0 || height <= 0) throw new ArgumentException("Frame dimensions must be positive.");
    if (grayscale.Length < width * height) throw new ArgumentException("Pixel buffer is smaller than width*height.");

    var copy = new float[width * height];
    for (int i = 0; i < copy.Length; i++)
    {
        copy[i] = grayscale[i] / 255f;
    }

    lock (_gate)
    {
        _visualFrame.HasFrame = true;
        _visualFrame.Width = width;
        _visualFrame.Height = height;
        _visualFrame.Pixels = copy;
    }
}

/// <summary>
/// Stop driving the brain from an external frame buffer. After this call the
/// synthetic-grating path (<see cref="SetVisualStimulus"/>) regains control of
/// the visual input until another frame is supplied.
/// </summary>
public void ClearVisualFrame()
{
    lock (_gate)
    {
        _visualFrame.HasFrame = false;
        _visualFrame.Pixels = null;
    }
}

/// <summary>Configure the purely-neural Auditory Cortex (SuperiorTemporalGyrus=22) drive.</summary>
public void SetAuditoryStimulus(float intensity01, float toneHz, bool enabled = true)
{
    lock (_gate)
    {
        _auditory.Intensity01 = Math.Clamp(intensity01, 0f, 1f);
        _auditory.ToneHz = Math.Clamp(toneHz, 20f, 16000f);
        _auditory.Enabled = enabled;
    }
}



/// <summary>
/// Register reafferent (self-generated) voice as an auditory drive into A1.
/// This closes the perception-action loop: the system "hears" its own vocal output.
/// </summary>
public void RegisterSelfVoice(string text, float intensity01, float toneHz, float holdSeconds = 0.45f, IReadOnlyList<string>? phonemes = null, string? gloss = null)
{
    lock (_gate)
    {
        if (_opt.EnableLanguageSystem)
        {
            if (phonemes is { Count: > 0 })
            {
                Language.LearnHeardWord(gloss ?? text, phonemes);
                Language.ProcessPhonemeInput(phonemes, LanguageStepDt, gloss ?? text);
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                Language.ProcessInput(text, LanguageStepDt);
            }
        }

        _auditorySelf.Intensity01 = Math.Clamp(intensity01, 0f, 1f);
        _auditorySelf.ToneHz = Math.Clamp(toneHz, 20f, 16000f);
        _auditorySelf.Enabled = true;
        _auditorySelf.HoldSeconds = Math.Clamp(holdSeconds, 0.05f, 2.5f);
        _auditorySelf.LastText = text ?? string.Empty;
        _auditorySelf.Phase = 0f;
    }
}
private void ApplySensoryStimuli(float dt)
{
    // Visual input is routed biologically:
    // retina -> thalamus (LGN relay) -> occipital cortex, with orienting drive to brainstem.
    //
    // Two paths, mutually exclusive on a given tick:
    //   A. Real frame buffer (set via SetVisualFrame): retinotopic pixel injection
    //      into thalamus + occipital cortex. Pixel position drives voxel position;
    //      pixel intensity drives signal strength.
    //   B. Synthetic sinusoidal grating (SetVisualStimulus): pre-existing path
    //      preserved for tests and demos.
    if (_visualFrame.HasFrame)
    {
        // Retinotopic injection into LGN-relay (thalamus) and occipital cortex.
        // Brainstem and cerebellum still get a coarse orienting drive from the
        // overall scene brightness via the synthetic path's scalar form, derived
        // from the frame mean.
        ApplyRetinotopicVisualFrame(Left,  RegionIds.Thalamus,          0.85f, delayTicks: 0);
        ApplyRetinotopicVisualFrame(Right, RegionIds.Thalamus,          0.85f, delayTicks: 0);
        ApplyRetinotopicVisualFrame(Left,  RegionIds.SuperiorOccipital, 0.30f, delayTicks: 1);
        ApplyRetinotopicVisualFrame(Right, RegionIds.SuperiorOccipital, 0.30f, delayTicks: 1);
        ApplyRetinotopicVisualFrame(Left,  RegionIds.InferiorOccipital, 0.55f, delayTicks: 2);
        ApplyRetinotopicVisualFrame(Right, RegionIds.InferiorOccipital, 0.55f, delayTicks: 2);

        // Orienting + cerebellar drive: use frame mean as a scalar brightness
        // signal so a bright scene still produces tectal orienting / cerebellar
        // smoothing, just at lower fidelity (these regions are not retinotopic).
        float frameMean = ComputeFrameMean();
        _visual.Phase += dt * Math.Max(0.5f, frameMean * 4f) * 2f * MathF.PI;
        ApplyVisualToRegion(Left,  RegionIds.Brainstem,  hemi: 0, frameMean * 0.18f, _visual.Phase, 0.35f, delayTicks: 1);
        ApplyVisualToRegion(Right, RegionIds.Brainstem,  hemi: 1, frameMean * 0.18f, _visual.Phase, 0.35f, delayTicks: 1);
        ApplyVisualToRegion(Left,  RegionIds.Cerebellum, hemi: 0, frameMean * 0.14f, _visual.Phase, 0.25f, delayTicks: 3);
        ApplyVisualToRegion(Right, RegionIds.Cerebellum, hemi: 1, frameMean * 0.14f, _visual.Phase, 0.25f, delayTicks: 3);
    }
    else if (_visual.Enabled && _visual.Intensity01 > 0.0001f)
    {
        _visual.Phase += dt * _visual.SpeedHz * 2f * MathF.PI;
        ApplyVisualToRegion(Left, RegionIds.Thalamus, hemi: 0, _visual.Intensity01 * 0.95f, _visual.Phase, _visual.SpatialFreq * 0.45f, delayTicks: 0);
        ApplyVisualToRegion(Right, RegionIds.Thalamus, hemi: 1, _visual.Intensity01 * 0.95f, _visual.Phase, _visual.SpatialFreq * 0.45f, delayTicks: 0);
        ApplyVisualToRegion(Left, RegionIds.SuperiorOccipital, hemi: 0, _visual.Intensity01 * 0.28f, _visual.Phase, _visual.SpatialFreq, delayTicks: 1);
        ApplyVisualToRegion(Right, RegionIds.SuperiorOccipital, hemi: 1, _visual.Intensity01 * 0.28f, _visual.Phase, _visual.SpatialFreq, delayTicks: 1);
        ApplyVisualToRegion(Left, RegionIds.InferiorOccipital, hemi: 0, _visual.Intensity01 * 0.52f, _visual.Phase, _visual.SpatialFreq, delayTicks: 2);
        ApplyVisualToRegion(Right, RegionIds.InferiorOccipital, hemi: 1, _visual.Intensity01 * 0.52f, _visual.Phase, _visual.SpatialFreq, delayTicks: 2);
        ApplyVisualToRegion(Left, RegionIds.Brainstem, hemi: 0, _visual.Intensity01 * 0.16f, _visual.Phase, _visual.SpatialFreq * 0.35f, delayTicks: 1);
        ApplyVisualToRegion(Right, RegionIds.Brainstem, hemi: 1, _visual.Intensity01 * 0.16f, _visual.Phase, _visual.SpatialFreq * 0.35f, delayTicks: 1);
        ApplyVisualToRegion(Left, RegionIds.Cerebellum, hemi: 0, _visual.Intensity01 * 0.12f, _visual.Phase, _visual.SpatialFreq * 0.25f, delayTicks: 3);
        ApplyVisualToRegion(Right, RegionIds.Cerebellum, hemi: 1, _visual.Intensity01 * 0.12f, _visual.Phase, _visual.SpatialFreq * 0.25f, delayTicks: 3);
    }

    // Auditory input is routed biologically:
    // cochlear/brainstem relay -> thalamus (MGB) -> STG temporal cortex.
    if (_auditory.Enabled && _auditory.Intensity01 > 0.0001f)
    {
        _auditory.Phase += dt * _auditory.ToneHz * 2f * MathF.PI;
        ApplyToneToRegion(Left, RegionIds.Pons, hemi: 0, _auditory.Intensity01 * 0.58f, _auditory.Phase, _auditory.ToneHz, delayTicks: 0);
        ApplyToneToRegion(Right, RegionIds.Pons, hemi: 1, _auditory.Intensity01 * 0.58f, _auditory.Phase, _auditory.ToneHz, delayTicks: 0);
        ApplyToneToRegion(Left, RegionIds.Thalamus, hemi: 0, _auditory.Intensity01 * 0.88f, _auditory.Phase, _auditory.ToneHz, delayTicks: 1);
        ApplyToneToRegion(Right, RegionIds.Thalamus, hemi: 1, _auditory.Intensity01 * 0.88f, _auditory.Phase, _auditory.ToneHz, delayTicks: 1);
        ApplyToneToRegion(Left, RegionIds.SuperiorTemporalGyrus, hemi: 0, _auditory.Intensity01 * 0.54f, _auditory.Phase, _auditory.ToneHz, delayTicks: 2);
        ApplyToneToRegion(Right, RegionIds.SuperiorTemporalGyrus, hemi: 1, _auditory.Intensity01 * 0.54f, _auditory.Phase, _auditory.ToneHz, delayTicks: 2);
        ApplyToneToRegion(Left, RegionIds.MiddleTemporalGyrus, hemi: 0, _auditory.Intensity01 * 0.22f, _auditory.Phase, _auditory.ToneHz, delayTicks: 3);
        ApplyToneToRegion(Right, RegionIds.MiddleTemporalGyrus, hemi: 1, _auditory.Intensity01 * 0.22f, _auditory.Phase, _auditory.ToneHz, delayTicks: 3);
        ApplyToneToRegion(Left, RegionIds.Cerebellum, hemi: 0, _auditory.Intensity01 * 0.14f, _auditory.Phase, _auditory.ToneHz * 0.65f, delayTicks: 2);
        ApplyToneToRegion(Right, RegionIds.Cerebellum, hemi: 1, _auditory.Intensity01 * 0.14f, _auditory.Phase, _auditory.ToneHz * 0.65f, delayTicks: 2);
    }


    // Reafferent self-voice (also mapped into A1; kept separate from mic/world input)
    if (_auditorySelf.Enabled && _auditorySelf.Intensity01 > 0.0001f)
    {
        _auditorySelf.Phase += dt * _auditorySelf.ToneHz * 2f * MathF.PI;
        ApplyToneToRegion(Left, RegionIds.Pons, hemi: 0, _auditorySelf.Intensity01 * 0.36f, _auditorySelf.Phase, _auditorySelf.ToneHz, delayTicks: 0);
        ApplyToneToRegion(Right, RegionIds.Pons, hemi: 1, _auditorySelf.Intensity01 * 0.36f, _auditorySelf.Phase, _auditorySelf.ToneHz, delayTicks: 0);
        ApplyToneToRegion(Left, RegionIds.Thalamus, hemi: 0, _auditorySelf.Intensity01 * 0.62f, _auditorySelf.Phase, _auditorySelf.ToneHz, delayTicks: 1);
        ApplyToneToRegion(Right, RegionIds.Thalamus, hemi: 1, _auditorySelf.Intensity01 * 0.62f, _auditorySelf.Phase, _auditorySelf.ToneHz, delayTicks: 1);
        ApplyToneToRegion(Left, RegionIds.SuperiorTemporalGyrus, hemi: 0, _auditorySelf.Intensity01 * 0.78f, _auditorySelf.Phase, _auditorySelf.ToneHz, delayTicks: 2);
        ApplyToneToRegion(Right, RegionIds.SuperiorTemporalGyrus, hemi: 1, _auditorySelf.Intensity01 * 0.78f, _auditorySelf.Phase, _auditorySelf.ToneHz, delayTicks: 2);
        ApplyToneToRegion(Left, RegionIds.Cerebellum, hemi: 0, _auditorySelf.Intensity01 * 0.18f, _auditorySelf.Phase, _auditorySelf.ToneHz * 0.65f, delayTicks: 2);
        ApplyToneToRegion(Right, RegionIds.Cerebellum, hemi: 1, _auditorySelf.Intensity01 * 0.18f, _auditorySelf.Phase, _auditorySelf.ToneHz * 0.65f, delayTicks: 2);
    }
}

// --------------------------------------------------------------------
// Entry 020: Active perception via resonance seeking
// Motor-region resonance biases sensor framing (gaze/focus) rather than explicit goals.
// --------------------------------------------------------------------
private void UpdateSensorFrame(float dt, ModulationPacket p)
{
    if (!_opt.EnableActivePerception) return;

    // Sample motor resonance centroids sparsely to stay cheap.
    var a = SampleRegionCentroid(Left, regionId: 11, samples: 512);
    var b = SampleRegionCentroid(Right, regionId: 11, samples: 512);

    float strength = Math.Clamp((a.Strength01 + b.Strength01) * 0.5f, 0f, 1f);

    float desiredX = 0.5f;
    float desiredY = 0.5f;

    // If motor resonance is present, gaze follows the centroid.
    if (strength > 0.02f)
    {
        desiredX = (a.X01 * a.Strength01 + b.X01 * b.Strength01) / MathF.Max(1e-5f, (a.Strength01 + b.Strength01));
        desiredY = (a.Y01 * a.Strength01 + b.Y01 * b.Strength01) / MathF.Max(1e-5f, (a.Strength01 + b.Strength01));
    }
    else
    {
        // Otherwise drift slowly (biological micro-saccade / exploration).
        float jitter = 0.02f + 0.06f * p.ResetPressure01;
        desiredX = Math.Clamp(_sensorFrame.GazeX01 + (float)(_rng.NextDouble() * 2 - 1) * jitter * dt, 0f, 1f);
        desiredY = Math.Clamp(_sensorFrame.GazeY01 + (float)(_rng.NextDouble() * 2 - 1) * jitter * dt, 0f, 1f);
    }

    // Focus increases with motor coherence; exploration increases with IDF pressure.
    float focus = 0.20f + 0.75f * strength - 0.40f * p.ResetPressure01;
    focus = Math.Clamp(focus, 0f, 1f);

    // Auditory focus shift: bias tonotopic center by motor x-bias.
    float audioShift = Math.Clamp((desiredX - 0.5f) * 2f, -1f, 1f);

    // Smooth updates to avoid twitching.
    float aS = Math.Clamp(_opt.SensorFrameSmoothingAlpha, 0.001f, 1f);
    _sensorFrame.GazeX01      = Lerp(_sensorFrame.GazeX01, desiredX, aS);
    _sensorFrame.GazeY01      = Lerp(_sensorFrame.GazeY01, desiredY, aS);
    _sensorFrame.VisualFocus01 = Lerp(_sensorFrame.VisualFocus01, focus, aS);
    _sensorFrame.AudioShift01 = Lerp(_sensorFrame.AudioShift01, audioShift, aS);
}

private (float X01, float Y01, float Strength01) SampleRegionCentroid(NeuralVolume vol, byte regionId, int samples)
{
    int w = vol.W, h = vol.H, d = vol.D;

    float sumW = 0f;
    float sumX = 0f;
    float sumY = 0f;

    // Sparse Monte Carlo over the volume.
    for (int i = 0; i < samples; i++)
    {
        int x, y;
        int z = _rng.Next(d);

        if (_opt.EnableActivePerception)
        {
            // Visual sampling is biased by the current sensor frame (gaze/focus).
            float cx = _sensorFrame.GazeX01 * (w - 1);
            float cy = _sensorFrame.GazeY01 * (h - 1);

            float explore01 = Math.Clamp((1f - _sensorFrame.VisualFocus01) + (_opt.VisualExploreFromPressure * _ponsCached.ResetPressure01), 0f, 1f);
            float sigmaX = Lerp(_opt.VisualSigmaMin01, _opt.VisualSigmaMax01, explore01) * w;
            float sigmaY = Lerp(_opt.VisualSigmaMin01, _opt.VisualSigmaMax01, explore01) * h;

            x = SampleGaussianIndex(cx, MathF.Max(1f, sigmaX), w);
            y = SampleGaussianIndex(cy, MathF.Max(1f, sigmaY), h);
        }
        else
        {
            x = _rng.Next(w);
            y = _rng.Next(h);
        }

        if (!vol.Active[vol.IndexOf(x, y, z)]) continue;
        if (vol.RegionId[vol.IndexOf(x, y, z)] != regionId) continue;

        // Vm is a usable proxy for resonance/activity.
        float wgt = MathF.Max(0f, vol.Vm[vol.IndexOf(x, y, z)]);
        if (wgt <= 0f) continue;

        sumW += wgt;
        sumX += wgt * (x / (float)(w - 1));
        sumY += wgt * (y / (float)(h - 1));
    }

    if (sumW <= 1e-6f) return (0.5f, 0.5f, 0f);

    // Strength: saturating mapping, robust to scale.
    float strength01 = MathF.Tanh(sumW / (samples * 0.35f));
    return (sumX / sumW, sumY / sumW, strength01);
}

private int SampleGaussianIndex(float center, float sigma, int maxExclusive)
{
    // Box-Muller gaussian
    float u1 = MathF.Max(1e-6f, (float)_rng.NextDouble());
    float u2 = MathF.Max(1e-6f, (float)_rng.NextDouble());
    float z0 = MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(2f * MathF.PI * u2);

    int idx = (int)MathF.Round(center + z0 * sigma);
    return Math.Clamp(idx, 0, maxExclusive - 1);
}

private static float Lerp(float a, float b, float t) => a + (b - a) * t;



private void ApplyVisualToRegion(NeuralVolume vol, byte regionId, byte hemi, float intensity01, float phase, float spatialFreq, int delayTicks = 0)
{
    int budget = Math.Clamp(_opt.SensoryBudgetPerStep, 64, 4096);
    int w = vol.W, h = vol.H, d = vol.D;

    for (int n = 0; n < budget; n++)
    {
        int x = _rng.Next(w);
        int y = _rng.Next(h);
        int z = _rng.Next(d);
        if (!vol.Active[vol.IndexOf(x, y, z)]) continue;
        if (vol.RegionId[vol.IndexOf(x, y, z)] != regionId) continue;

        float v = MathF.Sin(phase + (x * spatialFreq * 2f * MathF.PI));
        vol.EnqueueSignal(x, y, z, new DelayedSignal(Math.Max(0, delayTicks), v * intensity01));
    }
}

/// <summary>
/// Mean intensity of the currently-set visual frame, used as a coarse scalar
/// brightness drive for non-retinotopic regions (brainstem orienting, cerebellar
/// smoothing). Returns 0 if no frame is set.
/// </summary>
private float ComputeFrameMean()
{
    var frame = _visualFrame;
    if (!frame.HasFrame || frame.Pixels is null) return 0f;
    var pixels = frame.Pixels;
    if (pixels.Length == 0) return 0f;
    double sum = 0.0;
    for (int i = 0; i < pixels.Length; i++) sum += pixels[i];
    return (float)(sum / pixels.Length);
}

/// <summary>
/// Retinotopic injection of a real pixel frame into the brain's occipital cortex.
/// Pixel (px,py) maps to voxel (x,y,z) in the target region by linearly stretching
/// the frame's XY extent across the region's voxel-space XY extent and walking
/// the full Z depth so the visual scene drives a column of cortex (mimicking
/// how V1 layers receive the same retinotopic input). Pixel intensity controls
/// signal strength directly — no sinusoidal synthesis, no stochastic sampling.
///
/// This is the substrate-side change that turns the brain from "consumer of
/// pre-categorised visual stimuli" into "consumer of pixels".
/// </summary>
private void ApplyRetinotopicVisualFrame(NeuralVolume vol, byte regionId, float intensityScale, int delayTicks)
{
    var frame = _visualFrame;
    if (!frame.HasFrame || frame.Pixels is null) return;

    int w = vol.W, h = vol.H, d = vol.D;
    int fw = frame.Width, fh = frame.Height;
    if (fw <= 0 || fh <= 0) return;

    var pixels = frame.Pixels;
    for (int vx = 0; vx < w; vx++)
    {
        // Linear retinotopic stretch: voxel column vx samples the frame at
        // px ≈ vx * fw / w. Nearest-pixel resampling — adequate for the
        // typical case fw >> w (32x32 voxels reading a 96x96 camera buffer).
        int px = Math.Clamp((int)((vx + 0.5f) * fw / w), 0, fw - 1);

        for (int vy = 0; vy < h; vy++)
        {
            int py = Math.Clamp((int)((vy + 0.5f) * fh / h), 0, fh - 1);
            float pixel = pixels[py * fw + px];
            if (pixel < 0.02f) continue; // skip near-zero pixels (no signal)

            float strength = pixel * intensityScale;

            // Drive a column of voxels at (vx, vy, *) in the target region.
            // The retinotopic mapping is in XY; Z is the cortical-depth axis.
            for (int vz = 0; vz < d; vz++)
            {
                int fi = vol.IndexOf(vx, vy, vz);
                if (!vol.Active[fi]) continue;
                if (vol.RegionId[fi] != regionId) continue;

                vol.EnqueueSignal(vx, vy, vz, new DelayedSignal(Math.Max(0, delayTicks), strength));
            }
        }
    }
}

private void ApplyToneToRegion(NeuralVolume vol, byte regionId, byte hemi, float intensity01, float phase, float toneHz, int delayTicks = 0)
{
    int budget = Math.Clamp(_opt.SensoryBudgetPerStep, 64, 4096);
    int w = vol.W, h = vol.H, d = vol.D;

    float norm = MathF.Log(MathF.Max(20f, toneHz) / 20f) / MathF.Log(16000f / 20f);
    int xCenter = Math.Clamp((int)(norm * (w - 1)), 0, w - 1);

    if (_opt.EnableActivePerception)
    {
        int shift = (int)MathF.Round(_sensorFrame.AudioShift01 * _opt.AuditoryFocusShiftMax01 * (w - 1));
        xCenter = Math.Clamp(xCenter + shift, 0, w - 1);
    }

    for (int n = 0; n < budget; n++)
    {
        int x = Math.Clamp(xCenter + _rng.Next(-2, 3), 0, w - 1);
        int y = _rng.Next(h);
        int z = _rng.Next(d);
        if (!vol.Active[vol.IndexOf(x, y, z)]) continue;
        if (vol.RegionId[vol.IndexOf(x, y, z)] != regionId) continue;

        float v = MathF.Sin(phase);
        vol.EnqueueSignal(x, y, z, new DelayedSignal(Math.Max(0, delayTicks), v * intensity01));
    }
}

/// <summary>
/// Interoception loop: feed the brain's own internal state (ATP, sleep pressure,
/// neuromodulator levels, arousal) back into Thalamus as a perceived signal so
/// the brain has the opportunity to build a self-model that includes its own
/// dynamics. Each signal occupies a distinct Y-stripe of Thalamus voxels so
/// the brain can topographically separate them via STDP just as visual/auditory
/// cortex separates retinotopic / tonotopic inputs.
///
/// Always on (not gated by Sleep.SensorsConnected) — interoception is not an
/// external sensor and a sleeping brain still perceives its own pressure rising.
/// </summary>
private void ApplyInteroceptiveSignals(float dt)
{
    if (!_opt.EnableInteroception) return;

    _interoceptionPhase += dt * 2f * MathF.PI * InteroceptionFrequencyHz;
    if (_interoceptionPhase > 2f * MathF.PI) _interoceptionPhase -= 2f * MathF.PI;

    // Snapshot the 6 interoceptive signals. Includes user neuromodulator offsets
    // because the brain perceives the effective level, not the underlying source.
    float atp01 = _globalAtp01;
    float sleepPressure01 = Sleep.GetPressure();
    float dopa01 = Math.Clamp(Mods.Dopamine + _userDopaOffset01, 0f, 1f);
    float sero01 = Math.Clamp(Mods.Serotonin + _userSeroOffset01, 0f, 1f);
    float norad01 = Math.Clamp(Mods.Noradrenaline + _userNoradOffset01, 0f, 1f);
    float arousal01 = Math.Clamp(Pons.Arousal01, 0f, 1f);

    int h = _opt.H;
    // Split Thalamus into 6 horizontal stripes, one per signal.
    int stripeH = Math.Max(1, h / 6);
    int budget = Math.Max(6, _opt.InteroceptionBudgetPerStep);
    int perSignal = Math.Max(1, budget / 6);
    float intensity = _opt.InteroceptionIntensity;

    ApplyInteroceptiveSlice(Left,  0, atp01,           0 * stripeH, 1 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Left,  1, sleepPressure01, 1 * stripeH, 2 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Left,  2, dopa01,          2 * stripeH, 3 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Left,  3, sero01,          3 * stripeH, 4 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Left,  4, norad01,         4 * stripeH, 5 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Left,  5, arousal01,       5 * stripeH, h,           perSignal, intensity);

    ApplyInteroceptiveSlice(Right, 0, atp01,           0 * stripeH, 1 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Right, 1, sleepPressure01, 1 * stripeH, 2 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Right, 2, dopa01,          2 * stripeH, 3 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Right, 3, sero01,          3 * stripeH, 4 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Right, 4, norad01,         4 * stripeH, 5 * stripeH, perSignal, intensity);
    ApplyInteroceptiveSlice(Right, 5, arousal01,       5 * stripeH, h,           perSignal, intensity);
}

private void ApplyInteroceptiveSlice(NeuralVolume vol, int signalIndex, float level01, int yMin, int yMaxExclusive, int budget, float intensity)
{
    // Below ~0.5% level the signal is below the firing floor anyway; skip the
    // stochastic loop to save voxel sampling work in the common low-signal case.
    if (level01 <= 0.005f) return;

    int w = vol.W, d = vol.D;
    yMaxExclusive = Math.Max(yMin + 1, Math.Min(_opt.H, yMaxExclusive));

    // Per-signal phase offset spreads simultaneous peaks across the cycle so
    // multiple high signals remain distinguishable instead of all firing in lockstep.
    float phase = _interoceptionPhase + (signalIndex * (MathF.PI / 3f));
    // Modulation rides at 0.6..1.0 (always positive) so signal stays detectable
    // through the gentle oscillation that keeps voxels from saturating.
    float modulation = 0.6f + (0.4f * (MathF.Sin(phase) * 0.5f + 0.5f));

    for (int n = 0; n < budget; n++)
    {
        int x = _rng.Next(w);
        int y = _rng.Next(yMin, yMaxExclusive);
        int z = _rng.Next(d);
        int fi = vol.IndexOf(x, y, z);
        if (!vol.Active[fi]) continue;
        if (vol.RegionId[fi] != RegionIds.Thalamus) continue;

        vol.EnqueueSignal(x, y, z, new DelayedSignal(0, modulation * intensity * level01));
    }
}


public ThoughtClustersDto GetThoughtClusters()
{
    lock (_gate)
    {
        // Lower threshold and smaller minPts for better thought detection
        var leftVox = ResonLeft.GetResonantVoxels(minDensity: 0.08f, maxPoints: 8192);
        var rightVox = ResonRight.GetResonantVoxels(minDensity: 0.08f, maxPoints: 8192);

        // Smaller radius and minPts to detect smaller coherent assemblies
        var clL = ThoughtClusterer.ClusterVoxels(leftVox, radius: 2.5f, minPts: 2, maxClusters: 20);
        var clR = ThoughtClusterer.ClusterVoxels(rightVox, radius: 2.5f, minPts: 2, maxClusters: 20);

        ThoughtClusterDto[] map(ThoughtClusterer.Cluster[] cs)
        {
            var arr = new ThoughtClusterDto[cs.Length];
            for (int i = 0; i < cs.Length; i++)
            {
                var c = cs[i];
                // Coherence proxy: mean density scaled by sqrt(size) into [0..1]
                float coh = MathF.Tanh(c.MeanDensity01 * MathF.Sqrt(MathF.Max(1, c.Size)) * 0.85f);
                arr[i] = new ThoughtClusterDto(c.Id, c.Size, c.Centroid, c.MeanDensity01, coh);
            }
            return arr;
        }

        return new ThoughtClustersDto(StepIndex, map(clL), map(clR));
    }
}


    public ResonantClustersDto GetResonantClusters()
    {
        lock (_gate)
        {
            // Lower threshold to detect resonance more easily
            var leftPts = ResonLeft.GetHighResonantClusters(minDensity: 0.08f);
            var rightPts = ResonRight.GetHighResonantClusters(minDensity: 0.08f);
            return new ResonantClustersDto(StepIndex, leftPts, rightPts);
        }
    }

    // Static region-name table (allocated once, reused across all GetTelemetry calls).
    private static readonly Dictionary<byte, string> s_regionNames = new()
    {
        [1] = "Thalamus", [2] = "Hypothalamus", [3] = "Basal Ganglia",
        [4] = "Amygdala", [5] = "Hippocampus", [7] = "Brainstem",
        [8] = "Pons", [14] = "Corpus Callosum", [27] = "Cerebellum",
        [11] = "Precentral", [12] = "Postcentral",
        [15] = "Sup.Frontal", [16] = "Mid.Frontal", [17] = "Inf.Frontal",
        [18] = "Sup.Parietal", [19] = "Inf.Parietal",
        [20] = "Supramarginal", [21] = "Angular",
        [22] = "Sup.Temporal", [23] = "Mid.Temporal", [24] = "Inf.Temporal",
        [25] = "Sup.Occipital", [26] = "Inf.Occipital"
    };

    // Telemetry: firing rate history for sparklines.
    private readonly Queue<float> _firingRateHistory = new(120);
    private int _spikesThisTelemetryWindow;

    /// <summary>Record a spike for firing rate tracking (called from Step).</summary>
    internal void RecordSpikeForTelemetry() => _spikesThisTelemetryWindow++;

    public TelemetrySnapshotDto GetTelemetry()
    {
        // Minimal lock: just scalars and dictionary snapshots.
        long step;
        float callosal, spikeL, spikeR;
        Dictionary<byte, int> regionNeuronCount;
        Dictionary<byte, float> regionRateEma;
        int tagConsolidations;
        
        // Single _gate acquire: snapshot the dictionaries we need AND record the
        // firing-rate history sample in one critical section. Previously this
        // method took the gate twice (once for snapshots, once for history),
        // which doubled lock contention for every UI poll.
        float globalRate;
        lock (_gate)
        {
            step = StepIndex;
            callosal = _callosalTraffic01;
            spikeL = _spikeEmaL;
            spikeR = _spikeEmaR;
            regionNeuronCount = new Dictionary<byte, int>(_cachedRegionNeuronCount);
            regionRateEma = new Dictionary<byte, float>(_regionSpikeRateEma);
            tagConsolidations = _opt.EnableSynapticTagging ? SynapticTags.Snapshot().TotalConsolidations : 0;

            globalRate = 0f;
            foreach (var kv in regionRateEma) globalRate += kv.Value;
            _firingRateHistory.Enqueue(globalRate);
            while (_firingRateHistory.Count > 120) _firingRateHistory.Dequeue();
        }
        // Lock released: heavy stats and DTO construction done without blocking sim.
        int synL = SynLeft.TotalSynapses, synR = SynRight.TotalSynapses;
        int synCLR = CallosumLR.TotalSynapses, synCRL = CallosumRL.TotalSynapses;
        var slS = SynLeft.GetStats(); var srS = SynRight.GetStats();
        var clrS = CallosumLR.GetStats(); var crlS = CallosumRL.GetStats();
        var episodes = Hippocampus.GetEpisodeSummaries();

        float dt = 1f / 60f;
        float uptime = step * dt;

        var regionNames = s_regionNames;

        var regions = new List<RegionTelemetryDto>(regionNeuronCount.Count);
        var keys = new byte[regionNeuronCount.Count];
        int kx = 0;
        foreach (var kv in regionNeuronCount)
            keys[kx++] = kv.Key;
        Array.Sort(keys);

        for (int i = 0; i < keys.Length; i++)
        {
            byte r = keys[i];
            regionNeuronCount.TryGetValue(r, out int nCount);
            regionRateEma.TryGetValue(r, out float firingRate);
            string name = regionNames.TryGetValue(r, out var n) ? n : $"Region {r}";
            regions.Add(new RegionTelemetryDto(r, name, nCount, 0, firingRate, 0, 0, 0, 0, 0));
        }

        var pathways = new PathwayTelemetryDto[]
        {
            new("Left Intrahemispheric", synL, slS.meanW, slS.meanUsage, slS.skew, slS.activeCount),
            new("Right Intrahemispheric", synR, srS.meanW, srS.meanUsage, srS.skew, srS.activeCount),
            new("Callosum L->R", synCLR, clrS.meanW, clrS.meanUsage, clrS.skew, clrS.activeCount),
            new("Callosum R->L", synCRL, crlS.meanW, crlS.meanUsage, crlS.skew, crlS.activeCount),
        };

        float meanSize = 0, meanSal = 0, meanStr = 0;
        int strong = 0, weak = 0;
        float oldestAge = 0, newestAge = episodes.Length > 0 ? float.MaxValue : 0;
        foreach (var e in episodes)
        {
            meanSize += e.VoxelCount; meanSal += e.Salience; meanStr += e.Strength;
            if (e.Strength > 0.5f) strong++; if (e.Strength < 0.2f) weak++;
            float ageS = e.Age * dt;
            if (ageS > oldestAge) oldestAge = ageS;
            if (ageS < newestAge) newestAge = ageS;
        }
        if (episodes.Length > 0) { meanSize /= episodes.Length; meanSal /= episodes.Length; meanStr /= episodes.Length; }

        float diversity = (slS.stdW + srS.stdW + clrS.stdW + crlS.stdW) / 4;
        float learningProgress = MathF.Abs(slS.meanW) + MathF.Abs(srS.meanW);
        // PERF: avoid LINQ allocations in telemetry math.
        // Welford's algorithm for mean/std.
        int rN = 0;
        float rMean = 0f;
        float rM2 = 0f;
        for (int i = 0; i < regions.Count; i++)
        {
            float x = regions[i].MeanFiringRate;
            rN++;
            float delta = x - rMean;
            rMean += delta / rN;
            float delta2 = x - rMean;
            rM2 += delta * delta2;
        }
        float rVar = rN > 0 ? (rM2 / rN) : 0f;
        float rStd = rVar > 0f ? MathF.Sqrt(rVar) : 0f;
        float stability = rMean > 0f ? MathF.Max(0f, 1f - rStd / rMean) : 1f;


        int totalEpisodeVoxels = 0;
        for (int i = 0; i < episodes.Length; i++) totalEpisodeVoxels += episodes[i].VoxelCount;

        return new TelemetrySnapshotDto(
            step, uptime,
            new NetworkTelemetryDto(globalRate,
                (spikeL + spikeR) > 0.0001f ? 1f - MathF.Abs(spikeL - spikeR) / (spikeL + spikeR) : 0.5f,
                callosal, diversity, 0, tagConsolidations, learningProgress, stability),
            new EpisodeTelemetryDto(episodes.Length, totalEpisodeVoxels,
                meanSize, meanSal, meanStr, strong, weak, oldestAge, newestAge),
            regions.ToArray(), pathways, _firingRateHistory.ToArray());
    }

    /// <summary>
    /// Map neural activity to a humanoid stick figure body state.
    /// 
    /// Motor cortex (M1, region 11 = Precentral) is somatotopically organized:
    ///   - Dorsal-medial voxels (small Y, small X) --- leg/foot (homunculus leg area)
    ///   - Middle voxels --- trunk/arm
    ///   - Lateral-ventral voxels (large X, large Y) --- hand/face
    /// 
    /// Left M1 controls RIGHT body, Right M1 controls LEFT body (contralateral).
    /// Firing rate in each strip drives joint activation.
    /// Emotional expression comes from amygdala, dopamine, prediction error.
    /// </summary>
    // Cached body state --- updated every N steps inside Step() so GetBodyState() is lock-free.
    private volatile BodyStateDto _cachedBodyState = new(0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0,0,0, 0,0, 0);
    private int _bodyStateCountdown;
    private const int BodyStateUpdateInterval = 1; // update every sim step for smooth avatar motion
    
    /// <summary>Multiplier for M1 strip activations --- body joint angles. Higher = more visible movement.</summary>
    public float MotorGain { get; set; } = 1.0f;
    
    /// <summary>Expose vocal motor for runtime tuning of speech thresholds.</summary>
    public VocalMotor Vocal => _vocal;
    
    // Cached counts --- updated once at init and on slow lane, avoids volume scan in GetStatus()
    private int _cachedNeuronCount;
    private int _cachedSynapseCount;
    private int _countRefreshCountdown;
    
    // Brainstem motor output strip accumulators --- fed from spikes, not volume scan.
    // Brainstem (region 7) is the final common pathway integrating M1, cerebellum, and BG.
    // 6 strips by Y-position: [0]=axial/leg, [1]=hip/trunk, [2]=trunk, [3]=shoulder, [4]=arm, [5]=face/head
    private readonly float[] _m1StripL = new float[6]; // Left brainstem -> right body
    private readonly float[] _m1StripR = new float[6]; // Right brainstem -> left body
    private readonly int[] _m1CountL = new int[6];
    private readonly int[] _m1CountR = new int[6];
    
    // Brainstem motor circuit input accumulators (per-step, from spike loop)
    private float _bsAccAxial, _bsAccLimb, _bsAccFace;
    private float _bsAccCereb, _bsAccVestib, _bsAccOrient;
    private int _bsAccCount;

    // Per-region spike counts --- accumulated per step, EMA-smoothed for telemetry
    // Per-region spike accumulator. Indexed by region byte (0-255). A flat array
    // beats Dictionary<byte,int> here because this is touched once per spike in the
    // fused spike loop — dictionary lookup overhead (hash + bucket walk) was the
    // largest avoidable cost in the hot path.
    private readonly int[] _regionSpikeAccStep = new int[256];
    private readonly Dictionary<byte, float> _regionSpikeRateEma = new();
private readonly List<byte> _tmpRegionKeys = new(64);
    private int _regionSpikeStepCount; // steps since last EMA update
    
    // Per-region neuron counts --- cached from slow lane, avoids volume scan in GetTelemetry
    private readonly Dictionary<byte, int> _cachedRegionNeuronCount = new();

    public BodyStateDto GetBodyState() => _cachedBodyState;

    /// <summary>Compute body state from brainstem motor circuit output. Called inside Step() under _gate.</summary>
    private BodyStateDto ComputeBodyState(BrainstemMotorOutput bsm)
    {
            // Subcortical drives for emotions
            float arousal = Pons.Arousal01;
            float sleepPressure = Sleep.GetPressure();
            float dopamine = Mods.Dopamine;
            float noradrenaline = Mods.Noradrenaline;
            float serotonin = Mods.Serotonin;
            float ceaOut = Amygdala.CurrentSalience;
            float predErr = Prediction.Snapshot().GlobalError;

            float curiosity = Math.Clamp(dopamine * 0.4f + (1f - ceaOut) * 0.3f, 0f, 1f);
            float fear = Math.Clamp(ceaOut, 0f, 1f);
            float pleasure = Math.Clamp((dopamine - 0.3f) * 1.5f + serotonin * 0.3f, 0f, 1f);
            float surprise = Math.Clamp(MathF.Abs(predErr) * 3f, 0f, 1f);
            float drowsiness = Math.Clamp(sleepPressure + (1f - arousal) * 0.3f, 0f, 1f);

            // Body joints from brainstem motor circuit + emotional modulation
            float headTilt = bsm.HeadTilt + (curiosity - 0.3f) * 0.4f;
            float headNod = bsm.HeadNod - drowsiness * 0.5f;
            float energy = Math.Clamp(arousal * 0.6f + noradrenaline * 0.3f + (1f - sleepPressure) * 0.1f, 0f, 1f);

            return new BodyStateDto(
                StepIndex,
                Math.Clamp(headTilt, -1f, 1f),
                Math.Clamp(headNod, -1f, 1f),
                Math.Clamp(bsm.ShoulderL, -1f, 1f),
                Math.Clamp(bsm.ShoulderR, -1f, 1f),
                Math.Clamp(bsm.ElbowL, -1f, 1f),
                Math.Clamp(bsm.ElbowR, -1f, 1f),
                Math.Clamp(bsm.WristL, -1f, 1f),
                Math.Clamp(bsm.WristR, -1f, 1f),
                Math.Clamp(bsm.HipL, -1f, 1f),
                Math.Clamp(bsm.HipR, -1f, 1f),
                Math.Clamp(bsm.KneeL, -1f, 1f),
                Math.Clamp(bsm.KneeR, -1f, 1f),
                Math.Clamp(bsm.TorsoLean, -1f, 1f),
                Math.Clamp(bsm.TorsoTwist, -1f, 1f),
                curiosity, fear, pleasure, surprise, drowsiness,
                arousal, energy);
    }

    
public PackedPoints GetLayoutPoints()
{
    // Returns all ACTIVE neurons (both hemispheres) once for stable connection anchoring in the renderer.
    // Format per point (6 bytes): [hemi, x, y, z, energyByte(255), region]
    var data = new List<byte>(Math.Max(1024, (Left.W * Left.H * Left.D) / 2));

    void AddVolume(byte hemi, NeuralVolume v)
    {
        for (int x = 0; x < v.W; x++)
        for (int y = 0; y < v.H; y++)
        for (int z = 0; z < v.D; z++)
        {
            if (!v.Active[v.IndexOf(x, y, z)]) continue;

            data.Add(hemi);
            data.Add((byte)x);
            data.Add((byte)y);
            data.Add((byte)z);
            data.Add(255); // rest energy for layout points
            data.Add(v.RegionId[v.IndexOf(x, y, z)]);
        }
    }

    AddVolume(0, Left);
    AddVolume(1, Right);

    return new PackedPoints(data.Count / 6, data.ToArray());
}

public PackedLines GetConnectionLines(int maxEdges = 15000)
{
    lock (_gate)
    {
        // Format per edge (12 bytes):
        // [hemiA, x1, y1, z1, region1, hemiB, x2, y2, z2, region2, wByte, kind]
        // kind:
        //  0 = local (within region)
        //  1 = callosal (cortex<->cortex across hemispheres ONLY)
        //  2 = thalamo-cortical (1 -> cortex)
        //  3 = cortico-thalamic (cortex -> 1)
        //  4 = hippocampal (5 <-> cortex)
        //  5 = amygdala (4 -> cortex)
        //  6 = basal-ganglia loops (3 <-> cortex/thalamus)
        //  7 = cerebellar (6 -> motor)
        //  8 = cortico-cortical (between different cortical regions)
        //  9 = feedback (reserved; if present)
        // 10 = brainstem/pons (7/8)

        // Total budget: if caller passes a positive value, treat it as the total edge cap.
        // Otherwise fall back to the configured total.
        int totalBudget = (maxEdges > 0) ? maxEdges : Math.Max(0, _opt.EdgeBudgetTotal);
        if (totalBudget < 0) totalBudget = 0;

        var data = new List<byte>(Math.Max(0, totalBudget) * 12);

        // Circuit-specific budgets (these are DISPLAY budgets, not simulation budgets).
        // IMPORTANT: normalize so the sum equals totalBudget, then split bilateral circuits across hemispheres
        // so midline-rendered nuclei show connections to BOTH hemispheres.
        int budgetLeftLocal = _opt.EdgeBudgetLeftLocal;
        int budgetRightLocal = _opt.EdgeBudgetRightLocal;
        // Canon (Paul): Corpus callosum should always be visible, but it does not have its own
        // user-managed display "budget"/toggle. Therefore we derive a reasonable share from the
        // total edge cap if the configured callosal budget is unset/zero.
        int budgetCallosal = _opt.EdgeBudgetCallosal;
        if (budgetCallosal <= 0)
        {
            // Reserve ~10% for callosal edges, with a sensible floor so it reads clearly.
            // (This is a DISPLAY budget only; simulation wiring is unaffected.)
            budgetCallosal = Math.Max(1200, totalBudget / 10);
        }
        int budgetThalamoCortical = _opt.EdgeBudgetThalamoCortical;
        int budgetCorticoThalamic = _opt.EdgeBudgetCorticoThalamic;
        int budgetCorticoCortical = _opt.EdgeBudgetCorticoCortical;
        int budgetHippocampal = _opt.EdgeBudgetHippocampal;
        int budgetAmygdala = _opt.EdgeBudgetAmygdala;
        int budgetBasalGanglia = _opt.EdgeBudgetBasalGanglia;
        int budgetCerebellar = _opt.EdgeBudgetCerebellar;
        int budgetFeedback = _opt.EdgeBudgetFeedback;
        int budgetBrainstem = _opt.EdgeBudgetBrainstem;

        void NormalizeBudgetsToTotal()
        {
            // Keep caller-requested local budgets, but still normalize overall.
            // This avoids a common failure mode where one hemisphere consumes the shared circuit budgets first.
            var raw = new (string key, int value)[]
            {
                ("LLocal", Math.Max(0, budgetLeftLocal)),
                ("RLocal", Math.Max(0, budgetRightLocal)),
                ("Callosal", Math.Max(0, budgetCallosal)),
                ("ThalC", Math.Max(0, budgetThalamoCortical)),
                ("CortTh", Math.Max(0, budgetCorticoThalamic)),
                ("CortCort", Math.Max(0, budgetCorticoCortical)),
                ("Hip", Math.Max(0, budgetHippocampal)),
                ("Amy", Math.Max(0, budgetAmygdala)),
                ("BG", Math.Max(0, budgetBasalGanglia)),
                ("Cer", Math.Max(0, budgetCerebellar)),
                ("Fb", Math.Max(0, budgetFeedback)),
                ("Brainstem", Math.Max(0, budgetBrainstem)),
            };

            int sum = 0;
            for (int i = 0; i < raw.Length; i++) sum += raw[i].value;

            if (sum <= 0)
            {
                // Degenerate case: show something.
                budgetLeftLocal = totalBudget / 2;
                budgetRightLocal = totalBudget - budgetLeftLocal;
                budgetCallosal = 0;
                budgetThalamoCortical = 0;
                budgetCorticoThalamic = 0;
                budgetCorticoCortical = 0;
                budgetHippocampal = 0;
                budgetAmygdala = 0;
                budgetBasalGanglia = 0;
                budgetCerebellar = 0;
                budgetFeedback = 0;
                budgetBrainstem = 0;
                return;
            }

            // Proportional scaling with exact remainder distribution.
            var scaled = new (string key, int baseVal, double frac) [raw.Length];
            int baseSum = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                double exact = (double)raw[i].value * (double)totalBudget / (double)sum;
                int b = (int)Math.Floor(exact);
                double frac = exact - b;
                scaled[i] = (raw[i].key, b, frac);
                baseSum += b;
            }

            int rem = Math.Max(0, totalBudget - baseSum);
            Array.Sort(scaled, (a, b) => b.frac.CompareTo(a.frac));
            for (int i = 0; i < scaled.Length && rem > 0; i++, rem--)
                scaled[i].baseVal++;

            int Get(string k)
            {
                for (int i = 0; i < scaled.Length; i++)
                    if (scaled[i].key == k) return scaled[i].baseVal;
                return 0;
            }

            budgetLeftLocal = Get("LLocal");
            budgetRightLocal = Get("RLocal");
            budgetCallosal = Get("Callosal");
            budgetThalamoCortical = Get("ThalC");
            budgetCorticoThalamic = Get("CortTh");
            budgetCorticoCortical = Get("CortCort");
            budgetHippocampal = Get("Hip");
            budgetAmygdala = Get("Amy");
            budgetBasalGanglia = Get("BG");
            budgetCerebellar = Get("Cer");
            budgetFeedback = Get("Fb");
            budgetBrainstem = Get("Brainstem");
        }

        NormalizeBudgetsToTotal();

        // Split shared circuit budgets across hemispheres so both sides remain visible.
        static (int left, int right) SplitLR(int total)
        {
            int l = (total + 1) / 2;
            int r = total - l;
            return (l, r);
        }

        var (budgetThalamoCorticalL, budgetThalamoCorticalR) = SplitLR(budgetThalamoCortical);
        var (budgetCorticoThalamicL, budgetCorticoThalamicR) = SplitLR(budgetCorticoThalamic);
        var (budgetCorticoCorticalL, budgetCorticoCorticalR) = SplitLR(budgetCorticoCortical);
        var (budgetHippocampalL, budgetHippocampalR) = SplitLR(budgetHippocampal);
        var (budgetAmygdalaL, budgetAmygdalaR) = SplitLR(budgetAmygdala);
        var (budgetBasalGangliaL, budgetBasalGangliaR) = SplitLR(budgetBasalGanglia);
        var (budgetCerebellarL, budgetCerebellarR) = SplitLR(budgetCerebellar);
        var (budgetFeedbackL, budgetFeedbackR) = SplitLR(budgetFeedback);
        var (budgetBrainstemL, budgetBrainstemR) = SplitLR(budgetBrainstem);
        // Split callosal budget so CallosumLR doesn't consume everything before CallosumRL.
        var (budgetCallosalLR, budgetCallosalRL) = SplitLR(budgetCallosal);

        // Track counts per circuit
        int countLeftLocal = 0, countRightLocal = 0;
        int countCallosalLR = 0, countCallosalRL = 0;
        int countThalamoCorticalL = 0, countThalamoCorticalR = 0;
        int countCorticoThalamicL = 0, countCorticoThalamicR = 0;
        int countCorticoCorticalL = 0, countCorticoCorticalR = 0;
        int countHippocampalL = 0, countHippocampalR = 0;
        int countAmygdalaL = 0, countAmygdalaR = 0;
        int countBasalGangliaL = 0, countBasalGangliaR = 0;
        int countCerebellarL = 0, countCerebellarR = 0;
        int countFeedbackL = 0, countFeedbackR = 0;
        int countBrainstemL = 0, countBrainstemR = 0;

        // Helper to classify a synapse by circuit type
        static bool IsCortex(byte r) => RegionIds.IsCortex(r);
        
        byte ClassifyCircuit(byte srcRegion, byte dstRegion, byte srcHemi, byte dstHemi)
        {
            // Corpus callosum pathway:
            // If either endpoint is the CC region, treat it as callosal. This allows the renderer
            // to show CC --- cortex and cortex --- CC tracts (instead of a flat sheet of direct
            // inter-hemisphere cortical lines).
            if (srcRegion == RegionIds.CorpusCallosum || dstRegion == RegionIds.CorpusCallosum) return 1;

            // Back-compat: if any direct inter-hemisphere cortex---cortex edges exist, classify as callosal.
            if (srcHemi != dstHemi && IsCortex(srcRegion) && IsCortex(dstRegion)) return 1;

            // Thalamo-cortical (region1 --- cortex)
            if ((srcRegion == RegionIds.Thalamus && IsCortex(dstRegion)) ||
                (dstRegion == RegionIds.Thalamus && IsCortex(srcRegion))) return 2;

            // Hippocampus --- cortex
            if ((srcRegion == RegionIds.Hippocampus && IsCortex(dstRegion)) ||
                (dstRegion == RegionIds.Hippocampus && IsCortex(srcRegion))) return 3;

            // Pons --- cortex
            if ((srcRegion == RegionIds.Pons && IsCortex(dstRegion)) ||
                (dstRegion == RegionIds.Pons && IsCortex(srcRegion))) return 4;

            // Cerebellum --- cortex
            if ((srcRegion == RegionIds.Cerebellum && IsCortex(dstRegion)) ||
                (dstRegion == RegionIds.Cerebellum && IsCortex(srcRegion))) return 5;

            return 0;
        }
        
        bool CanAddCircuit(ref int count, int budget)
        {
            if (count >= budget) return false;
            count++;
            return true;
        }

        bool CanAddCircuitLR(byte hemi, ref int countL, ref int countR, int budgetL, int budgetR)
        {
            if (hemi == 0) return CanAddCircuit(ref countL, budgetL);
            return CanAddCircuit(ref countR, budgetR);
        }

        void addFromMapClassified(SynapseMap map, NeuralVolume volA, byte hemiA, NeuralVolume volB, byte hemiB)
        {
            foreach (var pre in map.Pres)
            {
                var (x1, y1, z1) = volA.FromIndex(pre);
                if (!volA.Active[volA.IndexOf(x1, y1, z1)]) continue;
                
                byte region1 = volA.RegionId[volA.IndexOf(x1, y1, z1)];

                var outs = map.GetOutgoing(pre);
                if (outs == null) continue;
                int outCount = outs.Count;
        for (int i = 0; i < outCount; i++)
                {
                    var syn = outs[i];
                    var (x2, y2, z2) = volB.FromIndex(syn.Post);
                    if (!volB.Active[volB.IndexOf(x2, y2, z2)]) continue;
                    
                    byte region2 = volB.RegionId[volB.IndexOf(x2, y2, z2)];
                    byte circuitKind = ClassifyCircuit(region1, region2, hemiA, hemiB);
                    
                    // Check budget for this circuit type
                    bool canAdd = circuitKind switch
                    {
                        0 when hemiA == 0 => CanAddCircuit(ref countLeftLocal, budgetLeftLocal),
                        0 when hemiA == 1 => CanAddCircuit(ref countRightLocal, budgetRightLocal),

                        // callosal: split by direction so both LR and RL get fair share
                        1 => CanAddCircuitLR(hemiA, ref countCallosalLR, ref countCallosalRL, budgetCallosalLR, budgetCallosalRL),

                        // Bilateral circuits: split budgets across hemispheres so both are visible.
                        2 => CanAddCircuitLR(hemiA, ref countThalamoCorticalL, ref countThalamoCorticalR, budgetThalamoCorticalL, budgetThalamoCorticalR),
                        3 => CanAddCircuitLR(hemiA, ref countCorticoThalamicL, ref countCorticoThalamicR, budgetCorticoThalamicL, budgetCorticoThalamicR),
                        4 => CanAddCircuitLR(hemiA, ref countHippocampalL, ref countHippocampalR, budgetHippocampalL, budgetHippocampalR),
                        5 => CanAddCircuitLR(hemiA, ref countAmygdalaL, ref countAmygdalaR, budgetAmygdalaL, budgetAmygdalaR),
                        6 => CanAddCircuitLR(hemiA, ref countBasalGangliaL, ref countBasalGangliaR, budgetBasalGangliaL, budgetBasalGangliaR),
                        7 => CanAddCircuitLR(hemiA, ref countCerebellarL, ref countCerebellarR, budgetCerebellarL, budgetCerebellarR),
                        8 => CanAddCircuitLR(hemiA, ref countCorticoCorticalL, ref countCorticoCorticalR, budgetCorticoCorticalL, budgetCorticoCorticalR),
                        9 => CanAddCircuitLR(hemiA, ref countFeedbackL, ref countFeedbackR, budgetFeedbackL, budgetFeedbackR),
                        10 => CanAddCircuitLR(hemiA, ref countBrainstemL, ref countBrainstemR, budgetBrainstemL, budgetBrainstemR),

                        _ => true
                    };
                    
                    if (!canAdd) continue;

                    byte w = (byte)Math.Clamp((int)MathF.Round((syn.W * 0.5f + 0.5f) * 255f), 0, 255);

                    data.Add(hemiA);
                    data.Add((byte)x1); data.Add((byte)y1); data.Add((byte)z1);
                    data.Add(region1);
                    data.Add(hemiB);
                    data.Add((byte)x2); data.Add((byte)y2); data.Add((byte)z2);
                    data.Add(region2);
                    data.Add(w);
                    data.Add(circuitKind);
                }
            }
        }

        // Local edges for both hemispheres
        addFromMapClassified(SynLeft, Left, 0, Left, 0);
        addFromMapClassified(SynRight, Right, 1, Right, 1);

        // Callosal edges
        addFromMapClassified(CallosumLR, Left, 0, Right, 1);
        addFromMapClassified(CallosumRL, Right, 1, Left, 0);

        return new PackedLines(data.Count / 12, data.ToArray());
    }
}

// === SAVE / LOAD BRAIN STATE ===

/// <summary>
/// Serialize the full brain state to a binary stream.
/// Format: Magic(4) + Version(4) + StepIndex(8) + Neuromods(12) + SleepPhase(4) + SleepPressure(4)
///       + Volume(L) + Volume(R) + SynapseMap(SynL) + SynapseMap(SynR) + SynapseMap(CalLR) + SynapseMap(CalRL)
/// </summary>
public byte[] SaveState()
{
    lock (_gate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Header
        bw.Write(0x4E524531); // "NRE1" magic
        bw.Write(3);          // version
        bw.Write(StepIndex);

        // Neuromodulators
        bw.Write(Mods.Dopamine);
        bw.Write(Mods.Serotonin);
        bw.Write(Mods.Noradrenaline);

        // Sleep state
        bw.Write((int)Sleep.CurrentPhase);
        bw.Write(Sleep.GetPressure());

        // Volume dimensions
        bw.Write(_opt.W);
        bw.Write(_opt.H);
        bw.Write(_opt.D);

        // NeuralVolume state
        BrainSerializer.WriteVolume(bw, Left);
        BrainSerializer.WriteVolume(bw, Right);

        // Synapse maps
        BrainSerializer.WriteSynapseMap(bw, SynLeft, 3);
        BrainSerializer.WriteSynapseMap(bw, SynRight, 3);
        BrainSerializer.WriteSynapseMap(bw, CallosumLR, 3);
        BrainSerializer.WriteSynapseMap(bw, CallosumRL, 3);

        return ms.ToArray();
    }
}

public void LoadState(byte[] data)
{
    lock (_gate)
    {
        bool wasRunning = Running;
        Running = false;

        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        int magic = br.ReadInt32();
        if (magic != 0x4E524531) throw new InvalidDataException("Not a valid NRE brain file");
        int version = br.ReadInt32();
        if (version < 1 || version > 3) throw new InvalidDataException($"Unsupported version {version}");

        StepIndex = br.ReadInt64();

        // Neuromodulators
        float da = br.ReadSingle();
        float ht = br.ReadSingle();
        float ne = br.ReadSingle();
        Mods.Set(da, ht, ne);

        // Sleep
        int sleepPhase = br.ReadInt32();
        float sleepPressure = br.ReadSingle();
        Sleep.ForcePhase((SleepPhase)sleepPhase);
        Sleep.BuildPressure(sleepPressure - Sleep.GetPressure());

        // Volume dimensions check
        int fw = br.ReadInt32(), fh = br.ReadInt32(), fd = br.ReadInt32();
        if (fw != _opt.W || fh != _opt.H || fd != _opt.D)
            throw new InvalidDataException($"Volume size mismatch: file={fw}x{fh}x{fd}, engine={_opt.W}x{_opt.H}x{_opt.D}");

        BrainSerializer.ReadVolume(br, Left);
        BrainSerializer.ReadVolume(br, Right);

        BrainSerializer.ReadSynapseMap(br, SynLeft, version);
        BrainSerializer.ReadSynapseMap(br, SynRight, version);
        BrainSerializer.ReadSynapseMap(br, CallosumLR, version);
        BrainSerializer.ReadSynapseMap(br, CallosumRL, version);

        if (wasRunning) Running = true;
    }
}

/// <summary>
    /// Return the most recently published FAST frame (spikes + a few scalars).
    /// Publishing is decoupled from the simulation tick to prevent UI polling from stalling the engine.
    /// </summary>
    public RenderFrameFastDto GetPublishedFastFrame()
    {
        // Fast path: lock-free read of last published snapshot.
        var snap = _publishedFast;
        if (snap is not null) return snap;

        // Cold start: build once under lock.
        lock (_gate)
        {
            _publishedFast ??= BuildFastFrameLocked();
            return _publishedFast;
        }
    }

    /// <summary>Build a fast-frame snapshot for UI publishing. Caller must hold _gate.</summary>
    private RenderFrameFastDto BuildFastFrameLocked()
    {
        // NOTE: caller must hold _gate
        var step = StepIndex;
        var callosal = _callosalTraffic01;

        var spikes = PackSpikes(_spikes, _opt.UiMaxSpikesPerFrame);
        var traffic = PackTrafficEventsLocked(maxEvents: _opt.UiMaxTrafficEvents);

        var sleepPhase = Sleep.CurrentPhase.ToString();
        var thalamicPulse = Thalamus.IsAtPulse;

        // Pack cached body state as flat float[] (21 values, 84 bytes)
        var b = _cachedBodyState;
        var body = new float[] {
            b.HeadTilt, b.HeadNod, b.ShoulderL, b.ShoulderR,
            b.ElbowL, b.ElbowR, b.WristL, b.WristR,
            b.HipL, b.HipR, b.KneeL, b.KneeR,
            b.TorsoLean, b.TorsoTwist,
            b.Curiosity, b.Fear, b.Pleasure, b.Surprise,
            b.Drowsiness, b.Arousal, b.AnimationEnergy
        };

        return new RenderFrameFastDto(
            step,
            spikes,
            traffic,
            callosal,
            SleepPhase: sleepPhase,
            ThalamicPulseActive: thalamicPulse,
            Body: body);
    }


    private PackedTrafficEvents PackTrafficEventsLocked(int maxEvents)
    {
        // Drain ConcurrentBag into List for packing.
        while (_trafficEventsBag.TryTake(out var ev))
            _trafficEvents.Add(ev);

        int available = _trafficEvents.Count;
        if (available <= 0) return new PackedTrafficEvents(0, Array.Empty<byte>());

        int take = Math.Min(available, Math.Max(0, maxEvents));
        if (take <= 0) return new PackedTrafficEvents(0, Array.Empty<byte>());

        // Publish the most recent events when capped.
        int start = Math.Max(0, available - take);

        var buf = new byte[take * 11];
        int o = 0;
        for (int i = 0; i < take; i++)
        {
            var e = _trafficEvents[start + i];
            buf[o++] = e.PreH;
            buf[o++] = e.PreX;
            buf[o++] = e.PreY;
            buf[o++] = e.PreZ;
            buf[o++] = e.PreRegion;
            buf[o++] = e.PostH;
            buf[o++] = e.PostX;
            buf[o++] = e.PostY;
            buf[o++] = e.PostZ;
            buf[o++] = e.PostRegion;
            buf[o++] = e.StrengthByte;
        }

        // Events were copied into the packed payload; clear to avoid replaying stale traffic.
        _trafficEvents.Clear();

        return new PackedTrafficEvents(take, buf);
    }

    /// <summary>
    /// Single simulation step:
    /// - Pons emits modulation (replaces IdleDrive)
    /// - Optional afferent floor perturbs system diffusely (not content)
    /// - True callosal routing delivers spikes across hemispheres
    /// OPTIMIZED: Reuses lists, samples ATP, batches operations
    /// </summary>
    public void Step(float dt)
    {
        lock (_gate)
        {
            if (!Running) return;
            if (!_wallClock.IsRunning) _wallClock.Start();

            StepIndex++;
            _accInter += dt;
            _accSlow  += dt;
            _fastStepsSinceSlow++;
            _regionSpikeStepCount++;

            int publishEvery = _opt.UiPublishEveryNSteps <= 0 ? 8 : _opt.UiPublishEveryNSteps;
            bool profile = _opt.EnableStepProfiling;
            bool consoleDiag = _opt.EnableConsoleDiagnostics;

            // PERF: Avoid Stopwatch allocations in the hot path. Use timestamp deltas and only
            // collect detailed layer timings when profiling is enabled.
            double msPerTick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            long totalStart = System.Diagnostics.Stopwatch.GetTimestamp();
            long mark = totalStart;

            long t1 = 0, t3 = 0, tSig = 0, tMicro = 0, tHemi = 0, tSub = 0, tCog = 0, tOut = 0;
            long tLang = 0, tSens = 0, tAttn = 0, tTag = 0, tRwd = 0, tWm = 0;

            static long ElapsedMs(long from, long to, double msPerTickLocal)
                => (long)((to - from) * msPerTickLocal);

            long MarkMs()
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                long ms = ElapsedMs(mark, now, msPerTick);
                mark = now;
                return ms;
            }
            // Layer 1: Clock & Sleep.
            var thalamicPulse = Thalamus.Step(dt, Sleep.CurrentPhase);
            StepIntermediateLane(dt);
            StepVoiceSleepTransitions();
            StepReafferentDecay(dt);
            if (profile) t1 = MarkMs();
            // Layer 2: Sensory Input.
            if (Sleep.SensorsConnected)
                ApplySensoryStimuli(dt);
            // Layer 2b: Interoception. Always on (not gated by SensorsConnected) —
            // the brain perceives its own internal state regardless of external sensors.
            ApplyInteroceptiveSignals(dt);
            // Layer 3: Neuromodulation (slow lane).
            StepSlowLane();
            ModulationPacket p = _ponsCached;
            if (profile) t3 = MarkMs();
            // Layer 4: Neural Dynamics.
            Left.BaseHz = p.ThetaHz;
            Right.BaseHz = p.ThetaHz;
            Left.StepOscillator(dt);
            Right.StepOscillator(dt);

            if (_opt.EnableAfferentFloor && _opt.AfferentEventsPerStep > 0)
                ApplyAfferentFloor(p);
            if (Sleep.IsDreaming)
                ApplyDreamNoise(0.08f);

            // STP recovery + Signal processing in parallel (no data conflicts)
            _spikes.Clear();
            _spikesL.Clear();
            _spikesR.Clear();
            _simPool.Run(
                () => { SynLeft.RecoverStpAll(_opt, dt); SynRight.RecoverStpAll(_opt, dt); },
                () => { CallosumLR.RecoverStpAll(_opt, dt); CallosumRL.RecoverStpAll(_opt, dt); },
                () => ProcessSignalArrivals(Left),
                () => ProcessSignalArrivals(Right)
            );
            if (profile) tSig = MarkMs(); // includes STP + arrivals

            float noradEff01 = Math.Clamp(Mods.Noradrenaline + _userNoradOffset01, 0f, 1f);
            float noradThresholdAdj = Amygdala.GetThresholdAdjustment() - (noradEff01 * 0.04f);
            float sleepLeakMod = Sleep.CurrentPhase == SleepPhase.Nrem ? 1.15f :
                                 Sleep.CurrentPhase == SleepPhase.Rem ? 0.95f : 1.0f;
            // ATP recovery scaling by sleep phase: NREM is the dominant recovery
            // phase, REM is moderately restorative, wake uses the base rate.
            float sleepEnergyRecoveryMul = Sleep.CurrentPhase == SleepPhase.Nrem ? _opt.EnergyRecoveryNremMultiplier :
                                           Sleep.CurrentPhase == SleepPhase.Rem ? _opt.EnergyRecoveryRemMultiplier : 1.0f;
            float energyRecoverPerTick = _opt.EnergyRecoverPerSec * sleepEnergyRecoveryMul * dt;

            float callosalL = 0f, callosalR = 0f;

            if (_opt.EnableCorticalMicrocircuit)
            {
                if (profile)
                {
                    long s = System.Diagnostics.Stopwatch.GetTimestamp();
                    Microcircuit.Step(dt);
                    tMicro = ElapsedMs(s, System.Diagnostics.Stopwatch.GetTimestamp(), msPerTick);
                }
                else
                {
                    Microcircuit.Step(dt);
                }
            }

            uint seedL = _noiseSeedLeft;
            uint seedR = _noiseSeedRight;

            // Refresh the shared microcircuit cache once (sequentially) before the parallel
            // hemisphere section. This avoids both hemispheres independently rebuilding it.
            RefreshMicrocircuitCache();

            _simPool.Run(
                () => { callosalL = StepHemisphere(0, ref seedL, Left, Right, SynLeft, CallosumLR, CallosumRL, dt, p, thalamicPulse, noradThresholdAdj, sleepLeakMod, energyRecoverPerTick, _spikesL); },
                () => { callosalR = StepHemisphere(1, ref seedR, Right, Left, SynRight, CallosumRL, CallosumLR, dt, p, thalamicPulse, noradThresholdAdj, sleepLeakMod, energyRecoverPerTick, _spikesR); }
            );

            _noiseSeedLeft = seedL;
            _noiseSeedRight = seedR;

            _spikes.AddRange(_spikesL);
            _spikes.AddRange(_spikesR);
            // Aggregate per-hemisphere counts after parallel section completes (no race).
            HemiTimingDetail = $"MC={tMicro} spkL={_spikesL.Count} spkR={_spikesR.Count}";

            // LAYER 6: Spike processing.
            ProcessSpikesFused();

            // integrate spike counts (avoid second pass)
            for (int si = 0; si < _spikes.Count; si++)
            {
                if (_spikes[si].hemi == 0) _spikeCountLSinceSlow++;
                else _spikeCountRSinceSlow++;
            }
            if (profile) tHemi = MarkMs();

            // LAYER 7: Subcortical processing.
            var amOut = Amygdala.Step(dt, StepIndex, _spikesWithRegion);
            StepVoiceOnSalience(amOut);
            StepNeuromodulatorRouting(amOut, p, dt);

            // Cerebellum, prediction, and hippocampus are independent, so they can run in parallel.
            float motorError = Prediction.GetRegionError(11);
            PredictiveCodingOutput predOut = default;
            HippocampalOutput hippoOut = default;
            int totalVoxels = _opt.W * _opt.H * _opt.D * 2;
            _simPool.Run(
                () => { Cerebellum.Step(dt, _spikesWithRegion, totalVoxels, motorCommandIntensity: 0f, sensoryMismatch: motorError); },
                () => { predOut = Prediction.Step(dt, _spikesWithRegion, totalVoxels, Mods, _opt); },
                () => { hippoOut = Hippocampus.Step(StepIndex, _spikes, dt); }
            );

            _lastPredictionError01 = Math.Clamp(predOut.TotalError, 0f, 1f);
            StepVoiceOnUncertainty();
            if (predOut.SurprisedRegions.Length > 0)
                Mods.ApplyNoradrenalinePulse(MathF.Min(0.15f, predOut.TotalError * 0.3f));

            if (profile) tSub = MarkMs();

            // --- LAYER 8: Memory
            if ((Amygdala.CurrentSalience > 0.4f || hippoOut.NoveltySignal > 0.5f) && _spikes.Count > 10)
                Hippocampus.CaptureEpisode(_spikes, Math.Max(Amygdala.CurrentSalience, hippoOut.NoveltySignal));
            if (Sleep.IsDreaming && _rng.NextDouble() < 0.15 * dt)
                StepDreamReplay();

            // --- LAYER 9: Cognitive Systems
            if (_opt.EnableLanguageSystem)
            {
                if (profile)
                {
                    long s = System.Diagnostics.Stopwatch.GetTimestamp();
                    Language.BindToNeuralActivity(_spikesWithRegion, dt);
                    tLang = ElapsedMs(s, System.Diagnostics.Stopwatch.GetTimestamp(), msPerTick);
                }
                else
                {
                    Language.BindToNeuralActivity(_spikesWithRegion, dt);
                }
            }

            if (StepIndex % 4 == 0)
            {
                if (profile)
                {
                    long s = System.Diagnostics.Stopwatch.GetTimestamp();
                    StepSensoryHierarchy(dt * 4f);
                    tSens = ElapsedMs(s, System.Diagnostics.Stopwatch.GetTimestamp(), msPerTick);
                }
                else
                {
                    StepSensoryHierarchy(dt * 4f);
                }
            }

            if (StepIndex % 4 == 0)
            {
                if (profile)
                {
                    long s = System.Diagnostics.Stopwatch.GetTimestamp();
                    StepAttention(amOut, dt * 4f);
                    tAttn = ElapsedMs(s, System.Diagnostics.Stopwatch.GetTimestamp(), msPerTick);
                }
                else
                {
                    StepAttention(amOut, dt * 4f);
                }
            }

            if (_opt.EnableSynapticTagging && StepIndex % 8 == 0)
            {
                if (profile)
                {
                    long s = System.Diagnostics.Stopwatch.GetTimestamp();
                    SynapticTags.Step(dt * 8f, Sleep.CurrentPhase);
                    tTag = ElapsedMs(s, System.Diagnostics.Stopwatch.GetTimestamp(), msPerTick);
                }
                else
                {
                    SynapticTags.Step(dt * 8f, Sleep.CurrentPhase);
                }
            }

            if (profile)
            {
                long s = System.Diagnostics.Stopwatch.GetTimestamp();
                StepRewardPrediction(amOut, dt);
                tRwd = ElapsedMs(s, System.Diagnostics.Stopwatch.GetTimestamp(), msPerTick);

                s = System.Diagnostics.Stopwatch.GetTimestamp();
                StepWorkingMemory(amOut, dt);
                tWm = ElapsedMs(s, System.Diagnostics.Stopwatch.GetTimestamp(), msPerTick);
            }
            else
            {
                StepRewardPrediction(amOut, dt);
                StepWorkingMemory(amOut, dt);
            }

            if (profile) tCog = MarkMs();
            // Layer 10: Action Selection & Motor Output.
            var (bgSelected, bgConfidence) = StepBasalGanglia(amOut, p, dt);
            _lastValence11 = _opt.EnableRewardPrediction
                ? Math.Clamp(RewardPrediction.GetLastRPE(), -1f, 1f)
                : Math.Clamp((Mods.Dopamine - Mods.Serotonin) * 2f, -1f, 1f);
            _vocal.Update(StepIndex, Sleep.CurrentPhase == SleepPhase.Awake,
                _opt.EnableBasalGangliaCircuit, bgSelected, bgConfidence);
            StepVocalTractAndPeers(dt);
            if (_opt.EnableSystemsConsolidation)
                StepConsolidation(hippoOut, dt);
            // Layer 11: Output / Readout.
            StepResonanceBuffers();
            _callosalTraffic01 = Math.Clamp((callosalL + callosalR) / Math.Max(1f, (_opt.W * _opt.H * _opt.D) * 0.30f), 0f, 1f);
            StepBodyState();

            // Publish FAST frame every N ticks.
            // IMPORTANT: do NOT mutate/clone the published record on non-publish ticks.
            // The record-copy was allocating every step and also defeats downstream caching.
            if (StepIndex % publishEvery == 0)
                _publishedFast = BuildFastFrameLocked();

            if (profile) tOut = MarkMs();

            long totalEnd = System.Diagnostics.Stopwatch.GetTimestamp();
            _lastStepMs = (float)((totalEnd - totalStart) * msPerTick);
            _avgStepMs = _avgStepMs * 0.95f + _lastStepMs * 0.05f; // EMA

            // Continuous persistence: accumulate real-time and flush a snapshot
            // every PersistenceSnapshotIntervalSeconds. The write goes through
            // SaveState (briefly re-takes _gate — reentrant, safe) into a .tmp file
            // and is atomically renamed into place. RPO ≤ interval seconds.
            if (!string.IsNullOrWhiteSpace(_opt.PersistenceSnapshotPath))
            {
                _persistenceSecondsAccum += dt;
                if (_persistenceSecondsAccum >= _opt.PersistenceSnapshotIntervalSeconds)
                {
                    _persistenceSecondsAccum = 0.0;
                    WritePersistenceSnapshot();
                }
            }

            if (profile)
            {
                if (consoleDiag && StepIndex % 60 == 0)
                    WriteDiagnostic($"Step {StepIndex}: L1={t1} L3={t3} Sig={tSig} Hemi={tHemi} Sub={tSub} Cog={tCog}(La{tLang}Se{tSens}At{tAttn}Ta{tTag}Rw{tRwd}Wm{tWm}) Out={tOut} Total={(long)_lastStepMs}ms spikes={_spikes.Count}");
                StepTimingBreakdown = $"L1={t1} L3={t3} Sig={tSig} Hemi={tHemi} Sub={tSub} Cog={tCog}(La{tLang}Se{tSens}At{tAttn}Ta{tTag}Rw{tRwd}Wm{tWm}) Out={tOut} spk={_spikes.Count}";
            }
            else
            {
                if (consoleDiag && StepIndex % 60 == 0)
                    WriteDiagnostic($"Step {StepIndex}: Total={(long)_lastStepMs}ms spikes={_spikes.Count}");
                StepTimingBreakdown = "";
            }
        }
    }

    // Extracted layer methods.
    //

    private void StepIntermediateLane(float dt)
    {
        while (_accInter >= _interDt)
        {
            if (++_atpComputeCounter >= AtpComputeInterval)
            { _globalAtp01 = ComputeGlobalAtpSampled(); _atpComputeCounter = 0; }
            Sleep.Step(_interDt, _globalAtp01, GetActivityLevel());
            if (Sleep.SensorsConnected) UpdateSensorFrame(_interDt, _ponsCached);
            _accInter -= _interDt;
        }
    }

    private void StepVoiceSleepTransitions()
    {
        if (Sleep.CurrentPhase != _lastVoicePhase)
        {
            Span<LexemeId> plan = stackalloc LexemeId[6];
            int n = 0;
            switch (Sleep.CurrentPhase)
            {
                case SleepPhase.Awake:  plan[n++] = LexemeId.Awake; plan[n++] = LexemeId.Sensors; plan[n++] = LexemeId.Online; break;
                case SleepPhase.Nrem:   plan[n++] = LexemeId.Entering; plan[n++] = LexemeId.Nrem; plan[n++] = LexemeId.Consolidating; break;
                case SleepPhase.Rem:    plan[n++] = LexemeId.Entering; plan[n++] = LexemeId.Rem; plan[n++] = LexemeId.Dream; plan[n++] = LexemeId.Replay; break;
                default:                plan[n++] = LexemeId.Transition; break;
            }
            _vocal.RequestUtterance(plan, n, MakeVoiceDrive(0.85f), StepIndex, ttlSteps: 300);
            _lastVoicePhase = Sleep.CurrentPhase;
        }
        // Periodic narration
        if (Sleep.CurrentPhase == SleepPhase.Awake && StepIndex - _lastNarrationStep > _narrationIntervalSteps)
        {
            _lastNarrationStep = StepIndex;
            Span<LexemeId> narr = stackalloc LexemeId[6];
            int nn = 0;
            float arousal = GetArousal01();
            int episodes = Hippocampus.Snapshot().EpisodeCount;
            if (arousal > 0.6f) narr[nn++] = LexemeId.Alert;
            else if (GetActivityLevel() < 0.05f) narr[nn++] = LexemeId.Idle;
            else narr[nn++] = LexemeId.Here;
            if (episodes > _lastNarrationEpisodes)
            { narr[nn++] = LexemeId.Remember; if (nn < narr.Length) narr[nn++] = LexemeId.Again; _lastNarrationEpisodes = episodes; }
            if (_visual.Enabled && _visual.Intensity01 > 0.3f && nn < narr.Length) narr[nn++] = LexemeId.Bright;
            if (_auditory.Enabled && _auditory.Intensity01 > 0.3f && nn < narr.Length) narr[nn++] = LexemeId.Sound;
            if (_lastValence11 > 0.3f && nn < narr.Length) narr[nn++] = LexemeId.Good;
            else if (_lastValence11 < -0.3f && nn < narr.Length) narr[nn++] = LexemeId.Bad;
            if (GetCertainty01() < 0.4f && nn < narr.Length) narr[nn++] = LexemeId.Unsure;
            if (nn > 0) _vocal.RequestUtterance(narr, nn, MakeVoiceDrive(0.65f), StepIndex, ttlSteps: 45);
        }
    }

    private void StepReafferentDecay(float dt)
    {
        if (!_auditorySelf.Enabled) return;
        _auditorySelf.HoldSeconds -= dt;
        if (_auditorySelf.HoldSeconds <= 0f)
        { _auditorySelf.Enabled = false; _auditorySelf.HoldSeconds = 0f; _auditorySelf.LastText = ""; }
    }

    /// <summary>
    /// Rebuild the microcircuit per-voxel cache if dirty or the refresh cadence has elapsed.
    /// Runs sequentially before the parallel hemisphere section in Step().
    /// </summary>
    private void RefreshMicrocircuitCache()
    {
        if (!_opt.EnableCorticalMicrocircuit) return;

        int total = _opt.W * _opt.H * _opt.D;
        if (_mcThresholdCache == null || _mcThresholdCache.Length != total)
        {
            _mcThresholdCache = new float[total];
            _mcSignCache = new float[total];
            _mcVipCache = new bool[total];
            _mcTypeCache = new byte[total];
            _mcLeakCache = new float[total];
            _mcCacheDirty = true;
            _mcCacheCountdown = 0;
        }

        if (!_mcCacheDirty && _mcCacheCountdown > 0)
        {
            _mcCacheCountdown--;
            return;
        }

        for (int zz = 0; zz < _opt.D; zz++)
        for (int yy = 0; yy < _opt.H; yy++)
        for (int xx = 0; xx < _opt.W; xx++)
        {
            int fi = (zz * _opt.H + yy) * _opt.W + xx;
            _mcThresholdCache![fi] = Microcircuit.GetEffectiveThresholdMod(xx, yy, zz);
            _mcSignCache![fi] = Microcircuit.GetSynapticSign(xx, yy, zz);
            _mcVipCache![fi] = Microcircuit.TargetsInterneurons(xx, yy, zz);
            var ntype = Microcircuit.GetNeuronType(xx, yy, zz);
            _mcTypeCache![fi] = (byte)ntype;
            var nparams = Microcircuit.GetParameters(ntype);
            _mcLeakCache![fi] = nparams.LeakMod;
        }

        _mcCacheDirty = false;
        _mcCacheCountdown = Math.Max(1, _opt.MicrocircuitCacheRefreshEveryNSteps);
    }

    /// <summary>Fused spike loop: region annotation + motor accumulators + strip display + telemetry in ONE pass.</summary>
    private void ProcessSpikesFused()
    {
        _spikesWithRegion.Clear();
        bool useMicrocircuit = _opt.EnableCorticalMicrocircuit;
        float mcDt = 1f / 60f; // matches Step's nominal dt; OnSpike only uses this to set refractory window
        int count = _spikes.Count;
        for (int i = 0; i < count; i++)
        {
            var s = _spikes[i];
            var vol = s.hemi == 0 ? Left : Right;
            var (x, y, z) = vol.FromIndex(s.idx);
            byte region = s.idx >= 0 && s.idx < vol.Total ? vol.RegionId[s.idx] : (byte)255;
            _spikesWithRegion.Add((s.hemi, s.idx, region));

            // Deferred from StepHemisphere: applied here so concurrent hemisphere
            // workers do not race on Microcircuit's shared adaptation/refractory arrays.
            if (useMicrocircuit)
                Microcircuit.OnSpike(x, y, z, mcDt);
            
            switch (region)
            {
                case RegionIds.PrecentralGyrus:
                    float yNorm = y / (float)Math.Max(1, _opt.H - 1);
                    float xNorm = x / (float)Math.Max(1, _opt.W - 1);
                    float lateral = MathF.Abs((xNorm - 0.5f) * 2f); // 0=medial, 1=lateral
                    float somato = Math.Clamp(lateral * 0.72f + yNorm * 0.28f, 0f, 1f);
                    if (somato < 0.28f) _bsAccAxial += 1f;
                    else if (somato < 0.74f) _bsAccLimb += 1f;
                    else _bsAccFace += 1f;
                    _bsAccCount++;
                    {
                        int strip = Math.Clamp((int)MathF.Round(somato * 5f), 0, 5);
                        if (s.hemi == 0) { _m1StripL[strip] += 1f; _m1CountL[strip]++; }
                        else             { _m1StripR[strip] += 1f; _m1CountR[strip]++; }
                    }
                    break;
                case 27: _bsAccCereb += 1f; _bsAccCount++; break;
                case RegionIds.PostcentralGyrus: _bsAccVestib += 0.5f; _bsAccCount++; break;
                case RegionIds.InferiorOccipital: case RegionIds.SuperiorOccipital: _bsAccOrient += 0.3f; _bsAccCount++; break;
                case RegionIds.SuperiorTemporalGyrus: _bsAccOrient += 0.4f; _bsAccCount++; break;
                case RegionIds.SuperiorFrontalGyrus: case RegionIds.MiddleFrontalGyrus: _bsAccAxial += 0.3f; _bsAccCount++; break;
                case RegionIds.Brainstem: _bsAccAxial += 0.2f; _bsAccCount++; break;
            }
            if (region != 255) _regionSpikeAccStep[region]++;
        }
    }

    private void StepVoiceOnSalience(AmygdalaOutput amOut)
    {
        if (Sleep.CurrentPhase != SleepPhase.Awake || !amOut.SalientEventDetected) return;
        RequestContextualVoice(Math.Clamp(amOut.Salience, 0.25f, 1.0f), 60);
    }

    private void StepVoiceOnUncertainty()
    {
        if (Sleep.CurrentPhase != SleepPhase.Awake || _lastPredictionError01 < 0.88f
            || (StepIndex - _lastUncertaintyVoiceStep) <= 120) return;
        RequestContextualVoice(Math.Clamp(_lastPredictionError01, 0.30f, 1.0f), 70);
        _lastUncertaintyVoiceStep = StepIndex;
    }

    /// <summary>Deduplicated: generate voice tokens from current spike context + memory.</summary>
    private void RequestContextualVoice(float urgency01, long ttl)
    {
        bool memoryHit = false;
        ushort replayMask = 0; long replayAgeSteps = 0; float replayStrength01 = 0f;
        if (_spikes.Count >= 12)
        {
            _hippoCueBuffer.Clear();
            int stride = Math.Max(1, _spikes.Count / 12);
            for (int i = 0; i < _spikes.Count && _hippoCueBuffer.Count < 18; i += stride)
                _hippoCueBuffer.Add(_spikes[i]);
            var replay = Hippocampus.TriggerPatternCompletion(_hippoCueBuffer);
            memoryHit = replay != null;
            if (replay != null)
            { replayMask = ComputeReplayRegionMask(replay.Value); replayAgeSteps = replay.Value.Age; replayStrength01 = Math.Clamp(replay.Value.Strength, 0f, 1f); }
        }
        var drive = MakeVoiceDrive(urgency01);
        Span<LexemeId> plan = stackalloc LexemeId[8];
        int n = _lexicon.GenerateTokens(new ProtoLexicon.Context(
            Drive: drive, VisualIntensity01: _visual.Intensity01, AuditoryIntensity01: _auditory.Intensity01,
            MemoryHit: memoryHit, Phase: Sleep.CurrentPhase,
            ReplayRegionMask: replayMask, ReplayStrength01: replayStrength01, ReplayAgeSteps: replayAgeSteps),
            plan, maxTokens: 4);
        _vocal.RequestUtterance(plan, n, drive, StepIndex, ttlSteps: ttl);
    }

    private void StepNeuromodulatorRouting(AmygdalaOutput amOut, ModulationPacket p, float dt)
    {
        Mods.ApplyNoradrenalinePulse(amOut.NoradrenalineLevel);
        if (amOut.SalientEventDetected) Mods.ApplyDopamineDelta(0.10f * amOut.Salience, dt);
        float activity01 = GetActivityLevel();
        float settle01 = Math.Clamp(p.Stability01 * (1.0f - Math.Clamp(activity01 * 20f, 0f, 1f)), 0f, 1f);
        Mods.ApplySerotoninDelta((0.02f * settle01) - (0.03f * amOut.Salience), dt);
    }

    private void StepDreamReplay()
    {
        EpisodeReplay? replay = null;
        var cue = BuildHippocampalCue(desiredCount: 12);
        if (cue.Count >= 3) replay = Hippocampus.TriggerPatternCompletion(cue);
        replay ??= Hippocampus.SampleReplayCandidate(_rng, sampleCount: 10);
        if (replay != null) { ApplyReplay(replay.Value); Sleep.IncrementReplays(); }
    }

    private void StepSensoryHierarchy(float dt)
    {
        if (!_opt.EnableSensoryHierarchy) return;
        // Pixel-level path: a real frame buffer feeds V1's Gabor filters directly.
        // Resample frame to the hierarchy's input width via nearest-pixel.
        if (_visualFrame.HasFrame && _visualFrame.Pixels is not null)
        {
            int vw = _opt.SensoryVisualWidth;
            int fw = _visualFrame.Width, fh = _visualFrame.Height;
            var pixels = _visualFrame.Pixels;
            var vi = new float[vw, vw];
            for (int vy = 0; vy < vw; vy++)
            {
                int py = Math.Clamp((int)((vy + 0.5f) * fh / vw), 0, fh - 1);
                for (int vx = 0; vx < vw; vx++)
                {
                    int px = Math.Clamp((int)((vx + 0.5f) * fw / vw), 0, fw - 1);
                    vi[vx, vy] = pixels[py * fw + px];
                }
            }
            SensoryHierarchy.ProcessVisual(vi, dt);
        }
        else if (_visual.Enabled && _visual.Intensity01 > 0.001f)
        {
            int vw = _opt.SensoryVisualWidth;
            var vi = new float[vw, vw];
            float intens = _visual.Intensity01, phase = _visual.Phase, sf = _visual.SpatialFreq;
            for (int vy = 0; vy < vw; vy++)
            for (int vx = 0; vx < vw; vx++)
                vi[vx, vy] = intens * 0.5f * (1f + MathF.Sin(phase + (float)vx / vw * sf * MathF.PI * 2f));
            SensoryHierarchy.ProcessVisual(vi, dt);
        }
        if (_auditory.Enabled && _auditory.Intensity01 > 0.001f)
        {
            int nb = _opt.SensoryAuditoryWidth;
            var ai = new float[nb];
            float toneNorm = Math.Clamp((_auditory.ToneHz - 200f) / 8000f, 0f, 1f);
            int peak = (int)(toneNorm * (nb - 1));
            for (int b = 0; b < nb; b++)
            { float d = Math.Abs(b - peak) / (float)Math.Max(1, nb); ai[b] = _auditory.Intensity01 * MathF.Exp(-d * d * 8f); }
            SensoryHierarchy.ProcessAuditory(ai, dt);
        }
    }

    private void StepAttention(AmygdalaOutput amOut, float dt)
    {
        if (!_opt.EnableAttentionSystem) return;
        Attention.UpdateSalience(Left.Vm, Left.RegionId);
        if (amOut.SalientEventDetected)
            Attention.SetSpatialFocus(_opt.W / 2f, _opt.H / 2f, _opt.D / 2f, amOut.Salience, AttentionType.Exogenous);
        Attention.Step(dt);
    }

    private void StepRewardPrediction(AmygdalaOutput amOut, float dt)
    {
        if (!_opt.EnableRewardPrediction) return;
        RewardPrediction.Step(dt);
        if (amOut.SalientEventDetected)
        { var rpe = RewardPrediction.ProcessReward(amOut.Salience * 0.5f); Mods.ApplyDopamineDelta(rpe.PhasicDopamine * 0.5f, dt); }
    }

    private void StepWorkingMemory(AmygdalaOutput amOut, float dt)
    {
        if (!_opt.EnableWorkingMemory) return;
        float wmD = _opt.EnableRewardPrediction ? RewardPrediction.GetEffectiveDopamine() : Math.Clamp(Mods.Dopamine + _userDopaOffset01, 0f, 1f);
        WorkingMemory.Step(dt, wmD);
        if (amOut.SalientEventDetected && amOut.Salience > 0.5f && _spikes.Count > 5)
            WorkingMemory.Encode(CreateWorkingMemoryPattern(_spikesWithRegion), $"episode_{StepIndex}");
    }

    private (int selected, float confidence) StepBasalGanglia(AmygdalaOutput amOut, ModulationPacket p, float dt)
    {
        if (!_opt.EnableBasalGangliaCircuit) return (-1, 0f);
        var bgInput = ComputeBasalGangliaInput(_spikesWithRegion);
        float bgD = _opt.EnableRewardPrediction ? RewardPrediction.GetEffectiveDopamine() : Math.Clamp(Mods.Dopamine + _userDopaOffset01, 0f, 1f);
        float urgency = Math.Clamp(p.Arousal01 * 0.5f + amOut.Salience * 0.3f, 0f, 1f);
        var bgOut = BasalGanglia.Step(bgInput, bgD, urgency, dt);
        if (bgOut.SelectionChanged && _opt.EnableRewardPrediction)
            BasalGanglia.ApplyRewardPredictionError(RewardPrediction.GetLastRPE(), _opt.RPELearningRate);
        return (bgOut.SelectedChannel, bgOut.SelectionConfidence);
    }

    private void StepVocalTractAndPeers(float dt)
    {
        float pitchMod = Math.Clamp(_lastValence11 * 0.3f + (GetArousal01() - 0.5f) * 0.4f, -1f, 1f);
        VocalTract.Step(dt, pitchMod);
        var incomingSpeech = Peer.ReceiveSpeech(4);
        foreach (var seg in incomingSpeech)
        {
            _auditorySelf.Intensity01 = Math.Clamp(seg.Volume * 0.6f, 0.05f, 0.8f);
            _auditorySelf.ToneHz = seg.Pitch > 0 ? seg.Pitch : 150f;
            _auditorySelf.Enabled = true;
            _auditorySelf.HoldSeconds = Math.Max(0.3f, seg.Phonemes.Length * 0.08f);
            _auditorySelf.LastText = seg.Text;
            _auditorySelf.Phase = 0f;
            if (_opt.EnableLanguageSystem)
            {
                if (seg.Phonemes is { Length: > 0 })
                    Language.ProcessPhonemeInput(seg.Phonemes, dt, seg.Text);
                else if (!string.IsNullOrEmpty(seg.Text))
                    Language.ProcessInput(seg.Text, dt);
            }
        }
        var sv = new PeerBridge.NeuralStateVector(Peer.InstanceId, StepIndex, GetArousal01(), _lastValence11,
            Thalamus.FrequencyHz, GetMostActiveRegion(), (_spikeEmaL + _spikeEmaR) * 0.5f,
            _opt.EnableAttentionSystem ? Attention.Snapshot().MaxPriority : 0.5f,
            VocalTract.IsSpeaking, _auditorySelf.Enabled);
        Peer.MaybePublishState(sv, dt);
        // Reliable-delivery housekeeping: poll peer progress to prune outboxes,
        // then retry the oldest unacked message per peer per channel.
        Peer.ProcessAcksAndRetry(dt);
    }

    private void StepConsolidation(HippocampalOutput hippoOut, float dt)
    {
        Consolidation.Step(dt, Sleep.CurrentPhase, Hippocampus, Thalamus);
        if (Sleep.CurrentPhase != SleepPhase.Nrem || hippoOut.NoveltySignal >= 0.3f) return;
        foreach (var ep in Hippocampus.GetEpisodeSummaries())
        {
            if (ep.Strength > 0.5f && ep.Age > 100)
            { var pat = CreateConsolidationPattern(ep.Id); if (pat != null) Consolidation.QueueForConsolidation(ep.Id, pat, ep.Salience); }
        }
    }

    private void StepResonanceBuffers()
    {
        _leftSpikesBuffer.Clear();
        _rightSpikesBuffer.Clear();
        int count = _spikes.Count;
        for (int i = 0; i < count; i++)
        {
            var s = _spikes[i];
            if (s.hemi == 0) _leftSpikesBuffer.Add(s.idx);
            else _rightSpikesBuffer.Add(s.idx);
        }
        ResonLeft.PushSpikesFromList(_leftSpikesBuffer);
        ResonRight.PushSpikesFromList(_rightSpikesBuffer);
    }

    private void StepBodyState()
    {
        if (--_bodyStateCountdown > 0) return;
        _bodyStateCountdown = BodyStateUpdateInterval;
        float mg = MotorGain;
        const float ss = 1f / 4f;
        BrainstemMotor.AccumulateInput(
            _bsAccAxial * ss * mg, _bsAccLimb * ss * mg, _bsAccFace * ss * mg,
            Math.Clamp(_bsAccCereb * ss * mg, 0f, 1f), Math.Clamp(_bsAccVestib * ss * mg, 0f, 1f),
            Math.Clamp(_bsAccOrient * ss * mg + Amygdala.CurrentSalience * 0.5f, 0f, 1f),
            Math.Clamp(Amygdala.CurrentSalience * 0.7f, 0f, 1f));

        // Contralateral M1 strip bias:
        // left cortex drives right body; right cortex drives left body.
        float armR = Math.Clamp(_m1StripL[4] * ss * mg, 0f, 1.5f);
        float armL = Math.Clamp(_m1StripR[4] * ss * mg, 0f, 1.5f);
        float legR = Math.Clamp((_m1StripL[0] + _m1StripL[1]) * 0.5f * ss * mg, 0f, 1.5f);
        float legL = Math.Clamp((_m1StripR[0] + _m1StripR[1]) * 0.5f * ss * mg, 0f, 1.5f);
        float sideTwist = Math.Clamp((armL - armR) * 0.22f, -0.6f, 0.6f);

        var bsm0 = BrainstemMotor.Step(1f / 60f * BodyStateUpdateInterval, Sleep.CurrentPhase, Pons.Arousal01, Mods.Noradrenaline);
        var bsm = new BrainstemMotorOutput(
            TorsoLean: bsm0.TorsoLean + (legR - legL) * 0.09f,
            TorsoTwist: bsm0.TorsoTwist + sideTwist,
            ShoulderL: bsm0.ShoulderL + armL * 0.55f,
            ShoulderR: bsm0.ShoulderR + armR * 0.55f,
            ElbowL: bsm0.ElbowL + armL * 0.85f,
            ElbowR: bsm0.ElbowR + armR * 0.85f,
            WristL: bsm0.WristL + armL * 0.60f,
            WristR: bsm0.WristR + armR * 0.60f,
            HipL: bsm0.HipL + legL * 0.75f,
            HipR: bsm0.HipR + legR * 0.75f,
            KneeL: bsm0.KneeL + legL * 0.85f,
            KneeR: bsm0.KneeR + legR * 0.85f,
            HeadTilt: bsm0.HeadTilt + sideTwist * 0.25f,
            HeadNod: bsm0.HeadNod,
            FacialExpression: bsm0.FacialExpression,
            VocalDrive: bsm0.VocalDrive,
            MuscleTone: bsm0.MuscleTone,
            PosturalStability: bsm0.PosturalStability
        );

        _bsAccAxial = 0; _bsAccLimb = 0; _bsAccFace = 0; _bsAccCereb = 0; _bsAccVestib = 0; _bsAccOrient = 0; _bsAccCount = 0;
        for (int i = 0; i < 6; i++)
        { _m1StripL[i] = Math.Clamp(_m1StripL[i] * ss * mg, -1f, 1f); _m1StripR[i] = Math.Clamp(_m1StripR[i] * ss * mg, -1f, 1f); }
        _cachedBodyState = ComputeBodyState(bsm);
        Array.Clear(_m1StripL); Array.Clear(_m1StripR); Array.Clear(_m1CountL); Array.Clear(_m1CountR);
    }

    private void StepSlowLane()
    {
        while (_accSlow >= _slowDt)
        {
            long steps = Math.Max(1, _fastStepsSinceSlow);
            int nVox = _opt.W * _opt.H * _opt.D;
            float densL = Math.Clamp((_spikeCountLSinceSlow / (float)steps) / Math.Max(1, nVox), 0f, 1f);
            float densR = Math.Clamp((_spikeCountRSinceSlow / (float)steps) / Math.Max(1, nVox), 0f, 1f);
            _spikeEmaL = Ema(_spikeEmaL, densL, _opt.SpikeDensityEmaAlpha);
            _spikeEmaR = Ema(_spikeEmaR, densR, _opt.SpikeDensityEmaAlpha);
            float measured = (_spikeEmaL + _spikeEmaR) * 0.5f;
            Pons.ApplyHomeostasis(measured, _opt.TargetSpikeDensity01, _opt.SpikeDensityDeadband01, _opt.PonsHomeostasisRate);
            _fastStepsSinceSlow = 0; _spikeCountLSinceSlow = 0; _spikeCountRSinceSlow = 0;

            // Region firing rate EMA. Accumulator is a flat int[256] (see field comment);
            // we walk the dense array once and emit only the regions that actually fired
            // or had a previous EMA, then zero the accumulator in-place.
            {
                int regSteps = Math.Max(1, _regionSpikeStepCount);
                const float alpha = 0.15f;
                for (int region = 0; region < _regionSpikeAccStep.Length; region++)
                {
                    int count = _regionSpikeAccStep[region];
                    if (count > 0)
                    {
                        float rate = count / (float)regSteps;
                        _regionSpikeRateEma.TryGetValue((byte)region, out float prev);
                        _regionSpikeRateEma[(byte)region] = prev + alpha * (rate - prev);
                    }
                }

                // Decay EMA for regions present in the EMA dict but not seen this window.
                _tmpRegionKeys.Clear();
                foreach (var k in _regionSpikeRateEma.Keys)
                    _tmpRegionKeys.Add(k);
                for (int i = 0; i < _tmpRegionKeys.Count; i++)
                {
                    var key = _tmpRegionKeys[i];
                    if (_regionSpikeAccStep[key] == 0)
                        _regionSpikeRateEma[key] *= (1f - alpha);
                }

                Array.Clear(_regionSpikeAccStep);
                _regionSpikeStepCount = 0;
            }

            ModulationPacket pSlow = Pons.StepAndEmit(_slowDt);
            if (Sleep.CurrentPhase == SleepPhase.Nrem)
                pSlow = pSlow with { Arousal01 = pSlow.Arousal01 * 0.3f };
            else if (Sleep.CurrentPhase == SleepPhase.Rem)
                pSlow = pSlow with { Arousal01 = pSlow.Arousal01 * 0.6f };

            float minArousal = Sleep.CurrentPhase == SleepPhase.Nrem ? _opt.NremArousalMin01 :
                               Sleep.CurrentPhase == SleepPhase.Rem ? _opt.RemArousalMin01 :
                               _opt.AwakeArousalMin01;
            pSlow = pSlow with
            {
                Arousal01 = MathF.Max(pSlow.Arousal01, minArousal),
                ThetaHz = MathF.Max(pSlow.ThetaHz, _opt.ThetaMinHz)
            };
            _ponsCached = pSlow;
            Mods.StepDecay(_slowDt);

            // Structural plasticity --- 4-way parallel on dedicated threads
            _simPool.Run(
                () => SynLeft.StructuralUpdate(_opt, _slowDt, pre => OutgoingBudgetForPre(_opt, Left, pre), pre => RegionForIndex(Left, pre), post => RegionForIndex(Left, post), IsProtectedTract),
                () => SynRight.StructuralUpdate(_opt, _slowDt, pre => OutgoingBudgetForPre(_opt, Right, pre), pre => RegionForIndex(Right, pre), post => RegionForIndex(Right, post), IsProtectedTract),
                () => CallosumLR.StructuralUpdate(_opt, _slowDt, _ => _opt.StructuralOutgoingAbsBudgetCallosal, pre => RegionForIndex(Left, pre), post => RegionForIndex(Right, post), IsProtectedTract),
                () => CallosumRL.StructuralUpdate(_opt, _slowDt, _ => _opt.StructuralOutgoingAbsBudgetCallosal, pre => RegionForIndex(Right, pre), post => RegionForIndex(Left, post), IsProtectedTract)
            );

            _accSlow -= _slowDt;

            // Refresh neuron/synapse counts periodically
            if (--_countRefreshCountdown <= 0)
            {
                _countRefreshCountdown = 4;
                int nnL = 0, nnR = 0;
                var rcL = new Dictionary<byte, int>();
                var rcR = new Dictionary<byte, int>();
                for (int z2=0;z2<_opt.D;z2++) for (int y2=0;y2<_opt.H;y2++) for (int x2=0;x2<_opt.W;x2++) { if (!Left.Active[Left.IndexOf(x2, y2, z2)]) continue; nnL++; byte r=Left.RegionId[Left.IndexOf(x2, y2, z2)]; if (r!=255){rcL.TryGetValue(r,out int c);rcL[r]=c+1;} }
                for (int z2=0;z2<_opt.D;z2++) for (int y2=0;y2<_opt.H;y2++) for (int x2=0;x2<_opt.W;x2++) { if (!Right.Active[Right.IndexOf(x2, y2, z2)]) continue; nnR++; byte r=Right.RegionId[Right.IndexOf(x2, y2, z2)]; if (r!=255){rcR.TryGetValue(r,out int c);rcR[r]=c+1;} }
                _cachedNeuronCount = nnL + nnR;
                _cachedRegionNeuronCount.Clear();
                foreach (var kv in rcL) _cachedRegionNeuronCount[kv.Key] = kv.Value;
                foreach (var kv in rcR) { _cachedRegionNeuronCount.TryGetValue(kv.Key, out int c); _cachedRegionNeuronCount[kv.Key] = c + kv.Value; }
                _cachedSynapseCount = SynLeft.TotalSynapses + SynRight.TotalSynapses + CallosumLR.TotalSynapses + CallosumRL.TotalSynapses;
            }
        }
    }
    
    /// <summary>
    /// OPTIMIZED: Sample ATP from a subset of voxels instead of full scan.
    /// Uses stratified sampling for better coverage.
    /// </summary>
    
    private float ComputeGlobalAtpSampled()
    {
    // NOTE:
    // The anatomical mask is sparse (only module volumes are Active).
    // A fixed grid sample can miss most active tissue or over-sample a single hot module,
    // biasing ATP low and preventing wake transitions. We therefore compute the mean ATP
    // across *all* active voxels (still cheap at 32---).
    float sum = 0f;
    int count = 0;

    for (int x = 0; x < _opt.W; x++)
    for (int y = 0; y < _opt.H; y++)
    for (int z = 0; z < _opt.D; z++)
    {
        if (Left.Active[Left.IndexOf(x, y, z)]) { sum += Left.Energy[Left.IndexOf(x, y, z)]; count++; }
        if (Right.Active[Right.IndexOf(x, y, z)]) { sum += Right.Energy[Right.IndexOf(x, y, z)]; count++; }
    }

    // Keep this within [0..1] because SleepController thresholds are expressed in that range.
    // Energy can be slightly above/below bounds depending on integration, so clamp defensively.
    float mean = count > 0 ? (sum / count) : 1.0f;
    return Math.Clamp(mean, 0.0f, 1.0f);
}
    private float GetActivityLevel()
    {
        return _spikes.Count / (float)Math.Max(1, _opt.W * _opt.H * _opt.D * 2);
    }
    
    private void ApplyDreamNoise(float intensity)
    {
        int n = (int)(_opt.W * _opt.H * _opt.D * intensity * 0.01f);
        for (int i = 0; i < n; i++)
        {
            int x = _rng.Next(_opt.W);
            int y = _rng.Next(_opt.H);
            int z = _rng.Next(_opt.D);
            float inten = (float)_rng.NextDouble() * intensity;
            int delay = _rng.Next(0, 3);
            
            if (_rng.NextDouble() < 0.5)
                Left.EnqueueSignal(x, y, z, new DelayedSignal(delay, inten));
            else
                Right.EnqueueSignal(x, y, z, new DelayedSignal(delay, inten));
        }
    }
    
    private ushort ComputeReplayRegionMask(EpisodeReplay replay)
    {
        ushort mask = 0;
        foreach (var voxel in replay.Voxels)
        {
            var vol = voxel.Hemisphere == 0 ? Left : Right;
            var (x, y, z) = vol.FromIndex(voxel.Index);
            if (x < 0 || x >= _opt.W || y < 0 || y >= _opt.H || z < 0 || z >= _opt.D) continue;

            byte region = vol.RegionId[vol.IndexOf(x, y, z)];
            if (region < 16)
                mask |= (ushort)(1 << region);
        }
        return mask;
    }

    private void ApplyReplay(EpisodeReplay replay)
    {
        foreach (var voxel in replay.Voxels)
        {
            var vol = voxel.Hemisphere == 0 ? Left : Right;
            var (x, y, z) = vol.FromIndex(voxel.Index);
            if (x >= 0 && x < _opt.W && y >= 0 && y < _opt.H && z >= 0 && z < _opt.D)
            {
                vol.EnqueueSignal(x, y, z, new DelayedSignal(_rng.Next(0, 3), replay.Strength * 0.15f));
            }
        }
    }
    
    // === ENTRY 028: Helper Methods for P0 Systems ===
    
    /// <summary>
    /// Create a working memory pattern from current neural activity.
    /// Encodes region-based activity distribution into a fixed-size vector.
    /// </summary>
    private float[] CreateWorkingMemoryPattern(IReadOnlyList<(byte hemi, int idx, byte region)> spikes)
    {
        var pattern = new float[_opt.WorkingMemoryPatternSize];
        
        // Encode activity by region (first 16 elements)
        var regionCounts = new float[16];
        foreach (var s in spikes)
        {
            if (s.region < 16)
                regionCounts[s.region]++;
        }
        
        // Normalize and copy to pattern
        float maxCount = 1f;
        for (int i = 0; i < 16; i++)
            maxCount = MathF.Max(maxCount, regionCounts[i]);
        for (int i = 0; i < 16 && i < pattern.Length; i++)
            pattern[i] = regionCounts[i] / maxCount;
        
        // Encode spatial distribution (remaining elements)
        int spatialSlots = Math.Min(pattern.Length - 16, 48);
        int slotSize = Math.Max(1, spikes.Count / Math.Max(1, spatialSlots));
        
        for (int i = 0; i < spatialSlots && i * slotSize < spikes.Count; i++)
        {
            var s = spikes[Math.Min(i * slotSize, spikes.Count - 1)];
            var vol = s.hemi == 0 ? Left : Right;
            var (x, y, z) = vol.FromIndex(s.idx);
            
            // Encode normalized position
            float posHash = (x / (float)_opt.W + y / (float)_opt.H + z / (float)_opt.D) / 3f;
            pattern[16 + i] = posHash;
        }
        
        return pattern;
    }
    
    /// <summary>
    /// Compute basal ganglia input from motor and PFC cortical regions.
    /// </summary>
    private float[] ComputeBasalGangliaInput(IReadOnlyList<(byte hemi, int idx, byte region)> spikes)
    {
        var input = new float[_opt.BasalGangliaChannels];
        
        // Count spikes from motor (11) and PFC (13) regions
        int motorCount = 0;
        int pfcCount = 0;
        
        foreach (var s in spikes)
        {
            if (s.region == RegionIds.PrecentralGyrus) motorCount++;
            else if (s.region == RegionIds.SuperiorFrontalGyrus 
                  || s.region == RegionIds.MiddleFrontalGyrus
                  || s.region == RegionIds.InferiorFrontalGyrus) pfcCount++;
        }
        
        int totalCortical = motorCount + pfcCount + 1;
        
        // Distribute across channels based on activity patterns
        // This is a simplified model - real BG receives topographic projections
        for (int i = 0; i < input.Length; i++)
        {
            // Motor contributes to action channels, PFC to goal channels
            float motorContrib = (i < input.Length / 2) 
                ? motorCount / (float)totalCortical 
                : motorCount / (float)totalCortical * 0.5f;
            float pfcContrib = (i >= input.Length / 2) 
                ? pfcCount / (float)totalCortical 
                : pfcCount / (float)totalCortical * 0.3f;
            
            input[i] = Math.Clamp(motorContrib + pfcContrib, 0f, 1f);
        }

        // Voice is treated as a motor action channel.
        // If the vocal motor has a pending intention, bias the vocalize channel so BG can select it.
        if (input.Length > VocalMotor.VocalizeChannel)
        {
            float u = _vocal.GetPendingUrgency01();
            if (u > 0f)
                input[VocalMotor.VocalizeChannel] = Math.Clamp(input[VocalMotor.VocalizeChannel] + u, 0f, 1f);
        }
        
        return input;
    }
    
    /// <summary>
    /// Create a consolidation pattern from a hippocampal episode.
    /// </summary>
    private float[]? CreateConsolidationPattern(int episodeId)
    {
        // Consolidation patterns should be derived from the *specific* episode
        // being queued, not from an empty cue completion.
        var replay = Hippocampus.GetReplayPattern(episodeId);
        if (replay == null) return null;
        
        // Convert episode voxels to pattern
        var pattern = new float[_opt.WorkingMemoryPatternSize];
        
        foreach (var voxel in replay.Value.Voxels)
        {
            int patternIdx = (voxel.Index * 7) % pattern.Length; // Hash to pattern index
            pattern[patternIdx] += 0.1f;
        }
        
        // Normalize
        float maxVal = 0.001f;
        for (int i = 0; i < pattern.Length; i++)
            maxVal = MathF.Max(maxVal, pattern[i]);
        for (int i = 0; i < pattern.Length; i++)
            pattern[i] /= maxVal;
        
        return pattern;
    }

    /// <summary>
    /// Build a small partial cue from the current spike field to drive CA3 pattern completion.
    /// We bias toward cortical + hippocampal-adjacent regions (better retrieval signal),
    /// then fall back to a stratified sample of raw spikes.
    /// </summary>
    private IReadOnlyList<(byte hemi, int idx)> BuildHippocampalCue(int desiredCount)
    {
        _hippoCueBuffer.Clear();

        if (desiredCount < 3) desiredCount = 3;

        // 1) Prefer region-annotated spikes when available.
        // Regions: cortex (gyri, see RegionIds), hippocampus 5, amygdala 4, thalamus 1 (relay cue).
        if (_spikesWithRegion.Count > 0)
        {
            int step = Math.Max(1, _spikesWithRegion.Count / Math.Min(desiredCount * 2, _spikesWithRegion.Count));
            for (int i = 0; i < _spikesWithRegion.Count && _hippoCueBuffer.Count < desiredCount; i += step)
            {
                var s = _spikesWithRegion[i];
                byte r = s.region;
                bool cueRegion = RegionIds.IsCortex(r) || r == 5 || r == 4 || r == 1;
                if (!cueRegion) continue;
                _hippoCueBuffer.Add((s.hemi, s.idx));
            }
        }

        // 2) If still short, stratified sample from raw spikes.
        if (_hippoCueBuffer.Count < desiredCount && _spikes.Count > 0)
        {
            int remaining = desiredCount - _hippoCueBuffer.Count;
            int step = Math.Max(1, _spikes.Count / Math.Min(remaining, _spikes.Count));
            for (int i = 0; i < _spikes.Count && _hippoCueBuffer.Count < desiredCount; i += step)
                _hippoCueBuffer.Add(_spikes[i]);
        }

        // 3) Ensure minimum cue size if any spikes exist.
        for (int i = 0; _hippoCueBuffer.Count < 3 && i < _spikes.Count; i++)
            _hippoCueBuffer.Add(_spikes[i]);

        return _hippoCueBuffer;
    }


private float CountHemiSpikes01(byte hemi, int nVox)
{
    int count = 0;
    for (int i = 0; i < _spikes.Count; i++)
        if (_spikes[i].hemi == hemi) count++;
    return count / (float)Math.Max(1, nVox);
}

private static float Ema(float prev, float x, float alpha)
{
    alpha = Math.Clamp(alpha, 0f, 1f);
    return prev + (x - prev) * alpha;
}

private static bool IsCortexRegion(byte regionId) => RegionIds.IsCortex(regionId);

    private static byte RegionForIndex(NeuralVolume vol, int idx)
    {
        var (x, y, z) = vol.FromIndex(idx);
        return vol.Active[vol.IndexOf(x, y, z)] ? vol.RegionId[vol.IndexOf(x, y, z)] : (byte)255;
    }

    /// <summary>
    /// Entry 021 hardening: protect anatomically essential long-range tracts from disappearing
    /// under low stimulation (especially when pruning is enabled). These tracts may still
    /// compete (budget scaling), but are not allowed to vanish.
    /// </summary>
    private static bool IsProtectedTract(byte preRegion, byte postRegion)
    {
        // Cortex range helper (gyri-based)
        bool preCortex = RegionIds.IsCortex(preRegion);
        bool postCortex = RegionIds.IsCortex(postRegion);

        // Cerebellum -> Motor (Region 27 -> Precentral)
        if (preRegion == RegionIds.Cerebellum && postRegion == RegionIds.PrecentralGyrus) return true;

        // Thalamus <-> Cortex (Region 1 <-> cortex)
        if (preRegion == RegionIds.Thalamus && postCortex) return true;
        if (postRegion == RegionIds.Thalamus && preCortex) return true;

        // Hippocampus <-> Cortex (Region 5 <-> frontal + sensory association)
        if (preRegion == RegionIds.Hippocampus && postCortex) return true;
        if (postRegion == RegionIds.Hippocampus && preCortex) return true;

        // Amygdala -> Cortex (Region 4 -> cortex)
        if (preRegion == RegionIds.Amygdala && postCortex) return true;

        // Cortex -> Basal Ganglia and BG -> Thalamus (Region 3)
        if (preCortex && postRegion == 3) return true;
        if (preRegion == 3 && postRegion == 1) return true;

        // Callosal cortex<->cortex projections (handled in callosal maps)
        if (preCortex && postCortex) return true;

        return false;
    }

private static float OutgoingBudgetForPre(NreEngineOptions opt, NeuralVolume vol, int preIdx)
{
    var (x, y, z) = vol.FromIndex(preIdx);
    byte r = vol.RegionId[vol.IndexOf(x, y, z)];
    return IsCortexRegion(r) ? opt.StructuralOutgoingAbsBudgetCortex : opt.StructuralOutgoingAbsBudgetSubcortex;
}





    private void ApplyAfferentFloor(ModulationPacket p)
    {
        float ar = p.Arousal01;
        float st = p.Stability01;

        float scale = (0.55f + ar * 0.75f) * (1.0f - st * 0.65f);
        float baseI = _opt.AfferentIntensity * scale;

        int n = Math.Max(1, _opt.AfferentEventsPerStep);

        for (int i = 0; i < n; i++)
        {
            int x = _rng.Next(0, _opt.W);
            int y = _rng.Next(0, _opt.H);
            int z = _rng.Next(0, _opt.D);
            int delay = _rng.Next(_opt.AfferentDelayMinTicks, _opt.AfferentDelayMaxTicks + 1);

            float inten = baseI * (0.75f + (float)_rng.NextDouble() * 0.5f);
            Left.EnqueueSignal(x, y, z, new DelayedSignal(delay, inten));
        }

        for (int i = 0; i < n; i++)
        {
            int x = _rng.Next(0, _opt.W);
            int y = _rng.Next(0, _opt.H);
            int z = _rng.Next(0, _opt.D);
            int delay = _rng.Next(_opt.AfferentDelayMinTicks, _opt.AfferentDelayMaxTicks + 1);

            float inten = baseI * (0.75f + (float)_rng.NextDouble() * 0.5f);
            Right.EnqueueSignal(x, y, z, new DelayedSignal(delay, inten));
        }
    }

    private float StepHemisphere(
        byte hemiByte,
        ref uint noiseSeed,
        NeuralVolume volSelf,
        NeuralVolume volOther,
        SynapseMap local,
        SynapseMap crossOut,
        SynapseMap crossIn,
        float dt,
        ModulationPacket pons,
        ThalamicPulse thalamicPulse,
        float noradThresholdAdj,
        float sleepLeakMod,
        float energyRecoverPerTick,
        List<(byte hemi, int idx)> spikeList)
    {
        float osc = volSelf.OscTerm();

        float pArousal = pons.Arousal01;
        float pStab = pons.Stability01;
        float pReset = pons.ResetPressure01;

        // === HEMISPHERE SPECIALIZATION ===
        // Left (hemi=0): Logical, analytical, sequential - tighter control
        // Right (hemi=1): Creative, holistic, spatial - freer flow
        bool isLeft = (hemiByte == 0);
        
        float hemiInhibMod = 1.0f;
        float hemiThresholdMod = 1.0f;
        float hemiNoiseMod = 1.0f;
        float hemiHebbMod = 1.0f;
        
        if (_opt.EnableHemisphereSpecialization)
        {
            if (isLeft)
            {
                // HEMI-0: stronger inhibition, higher thresholds, lower spontaneous noise
                hemiInhibMod = _opt.LeftInhibStrengthMod;       // 1.25 - more focused
                hemiThresholdMod = _opt.LeftThresholdMod;       // 1.05 - more selective
                hemiNoiseMod = _opt.LeftSpontaneousNoiseMod;    // 0.7 - less random
                hemiHebbMod = _opt.LeftHebbRateMod;             // 1.1 - faster sequential learning
            }
            else
            {
                // HEMI-1: weaker inhibition, lower thresholds, higher spontaneous noise
                hemiInhibMod = _opt.RightInhibStrengthMod;      // 0.8 - more flow
                hemiThresholdMod = _opt.RightThresholdMod;      // 0.95 - more excitable
                hemiNoiseMod = _opt.RightSpontaneousNoiseMod;   // 1.3 - more spontaneous
                hemiHebbMod = _opt.RightHebbRateMod;            // 0.9 - slower holistic learning
            }
        }

        float inputGain = _opt.InputGain * (1f + _opt.PonsArousalInputGainBoost * pArousal);
        float inhibStrength = _opt.InhibStrength * (1f + _opt.PonsStabilityInhibBoost * pStab);
        inhibStrength *= hemiInhibMod; // Apply hemisphere modifier

        // Biologically inspired plasticity gating:
        // DA/ACh/NE promote synaptic change; 5-HT restrains lability.
        float dopa01 = Math.Clamp(Mods.Dopamine + _userDopaOffset01, 0f, 1f);
        float ne01 = Math.Clamp(Mods.Noradrenaline + _userNoradOffset01, 0f, 1f);
        float sero01 = Math.Clamp(Mods.Serotonin + _userSeroOffset01, 0f, 1f);
        float ach01 = 0.20f + pArousal * 0.15f;
        if (_opt.EnableEnhancedNeuromodulators)
        {
            var nm = NeuromodSystem.Snapshot();
            ach01 = Math.Clamp(nm.AChTonic + nm.AChPhasic, 0f, 1f);
            dopa01 = Math.Clamp(dopa01 * 0.6f + (nm.DopamineTonic + nm.DopaminePhasic) * 0.4f, 0f, 1f);
            ne01 = Math.Clamp(ne01 * 0.6f + (nm.NETonic + nm.NEPhasic) * 0.4f, 0f, 1f);
            sero01 = Math.Clamp(sero01 * 0.6f + (nm.SerotoninTonic + nm.SerotoninPhasic) * 0.4f, 0f, 1f);
        }
        float neuromodDrive = (0.45f * dopa01) + (0.30f * ach01) + (0.20f * ne01) - (0.25f * sero01);
        float plasticityNeuromodGate = 1.0f + (_opt.StdpNeuromodGateStrength * neuromodDrive);
        if (Sleep.CurrentPhase != SleepPhase.Awake)
            plasticityNeuromodGate *= _opt.StdpSleepGateMultiplier;
        plasticityNeuromodGate = Math.Clamp(plasticityNeuromodGate, 0.05f, 2.5f);
        
        // OPTIMIZED: Cache global inhibition once (avoids lock per voxel)
        float globalCerebInhib = Cerebellum.GetGlobalInhibition() * 0.1f;
        inhibStrength += globalCerebInhib;

        float baseLeak = _opt.Leak + (_opt.PonsResetLeakBoost * pReset);
        baseLeak *= sleepLeakMod; // Sleep modulates leak rate
        baseLeak = Math.Clamp(baseLeak, 0.80f, 0.995f);

        float callosalEventCount = 0f;
        
        // OPTIMIZED: Pre-cache cerebellar inhibition per region (max ~16 regions)
        // This avoids acquiring lock for every single voxel.
        // Per-hemisphere reusable buffer avoids per-step heap allocation in the hot path.
        var regionInhibCache = hemiByte == 0 ? _regionInhibCacheLeft : _regionInhibCacheRight;
        for (int r = 0; r < 16; r++)
            regionInhibCache[r] = Cerebellum.GetRegionInhibition((byte)r) * 0.05f;
        
        // Entry 025: Cortical microcircuit. The microcircuit cache is rebuilt
        // sequentially before the parallel hemisphere section (see Step()), so here
        // we just bind the (already-populated, read-only) arrays to non-null locals.
        bool useMicrocircuit = _opt.EnableCorticalMicrocircuit;

        float[] mcThreshold = System.Array.Empty<float>();
        float[] mcSign = System.Array.Empty<float>();
        bool[]  mcVip = System.Array.Empty<bool>();
        float[] mcLeak = System.Array.Empty<float>();
        byte[]  mcType = System.Array.Empty<byte>();

        if (useMicrocircuit && _mcThresholdCache != null)
        {
            mcThreshold = _mcThresholdCache;
            mcSign      = _mcSignCache!;
            mcVip       = _mcVipCache!;
            mcLeak      = _mcLeakCache!;
            mcType      = _mcTypeCache!;
        }


        // Pre-cache prediction error threshold modifiers per region (Entry 022).
        var predErrorCache = hemiByte == 0 ? _predErrorCacheLeft : _predErrorCacheRight;
        if (_opt.EnablePredictiveCoding)
        {
            for (int r = 0; r < 16; r++)
                predErrorCache[r] = Prediction.GetThresholdMod((byte)r);
        }

        // OPTIMIZED: batch spontaneous noise injection (thread-safe RNG for parallel hemispheres).
        if (_opt.EnableHemisphereSpecialization)
        {
            float spontaneousNoiseChance = 0.002f * hemiNoiseMod;
            int nVoxels = _opt.W * _opt.H * _opt.D;
            int noiseCount = (int)(nVoxels * spontaneousNoiseChance);

            // Deterministic, allocation-free noise injection (xorshift32).
            for (int i = 0; i < noiseCount; i++)
            {
                int idx = NextInt(ref noiseSeed, nVoxels);
                int x = idx % _opt.W;
                int t = idx / _opt.W;
                int y = t % _opt.H;
                int z = t / _opt.H;

                int nfi = (z * _opt.H + y) * _opt.W + x;
                if (volSelf.Active[nfi])
                {
                    // amplitude in [0..0.15] scaled by hemiNoiseMod
                    float u01 = (XorShift32(ref noiseSeed) & 0x00FFFFFF) / 16777215f;
                    volSelf.Vm[nfi] += u01 * 0.15f * hemiNoiseMod;
                }
            }
        }

        // Pre-compute thalamic contribution.
        float thalamicAdd = thalamicPulse.IsBinding ? thalamicPulse.Amplitude * 0.01f : 0f;

        // Pre-compute common threshold terms
        float dopaTerm = dopa01 * 0.0015f;
        float seroTerm = sero01 * 0.0015f;
        float arousalTerm = pArousal * _opt.PonsArousalThresholdLowering;
        float stabTerm = pStab * _opt.PonsStabilityThresholdRaising;
        float resetMul = 1.0f - (pReset * 0.02f);
        float baseT = _opt.BaseThreshold + sero01 * 0.08f + pStab * 0.10f;
        baseT *= hemiThresholdMod;

        var sampleHemiTiming = _opt.EnableConsoleDiagnostics && StepIndex % 60 == 0;
        var innerSw = sampleHemiTiming ? System.Diagnostics.Stopwatch.StartNew() : null;
        int spikeCount = 0;
        var _vm = volSelf.Vm;
        var _energy = volSelf.Energy;
        var _theta = volSelf.Theta;
        var _active = volSelf.Active;
        var _regionId = volSelf.RegionId;

        // === Next notch: sparse + parallel voxel state update ===
        // Phase 1 (parallel): update Vm/Energy/Theta per-voxel, detect spikes, but DO NOT run neighborhood effects or propagation.
        // Phase 2 (sequential, deterministic): process spikes in sorted index order to apply inhibition, microcircuit bookkeeping, and synaptic propagation.
        var activeIdx = hemiByte == 0 ? (_activeIdxLeft ??= BuildActiveIndexCache(_active)) : (_activeIdxRight ??= BuildActiveIndexCache(_active));

        // Fast scratch buffers (per-thread / per-call)
        int[] spikesTmp = System.Buffers.ArrayPool<int>.Shared.Rent(Math.Min(activeIdx.Length, 8192));
        int spikesTmpCount = 0;

        // Use thread-local spike buffers in parallel mode to avoid contention.
        // Reuse the per-hemisphere ThreadLocal instance (allocating one per step generates
        // significant GC pressure — ThreadLocal registers in a global slot table).
        var tls = _opt.EnableParallelVoxelUpdate && activeIdx.Length >= _opt.ParallelMinVoxelCount
            ? (hemiByte == 0 ? _tlsSpikesLeft : _tlsSpikesRight)
            : null;

        float oscLocal = osc;
        float thalamicAddLocal = thalamicAdd;

        if (tls is null)
        {
            for (int ai = 0; ai < activeIdx.Length; ai++)
            {
                int fi = activeIdx[ai];

                // === ENTRY 025: neuron-type specific dynamics ===
                float neuronThresholdMod = 1.0f;
                float neuronLeakMod = 1.0f;
                float neuronSynapticSign = 1.0f;
                bool isVIP = false;

                if (useMicrocircuit)
                {
                    neuronThresholdMod = mcThreshold[fi];
                    neuronSynapticSign = mcSign[fi];
                    isVIP = mcVip[fi];
                    neuronLeakMod = mcLeak[fi];
                }

                float leak = baseLeak * neuronLeakMod;
                leak = Math.Clamp(leak, 0.80f, 0.998f);

                float v = _vm[fi];
                v = v * leak + oscLocal + thalamicAddLocal;

                float theta = _theta[fi] - dopaTerm + seroTerm;
                theta -= arousalTerm;
                theta += stabTerm;
                theta += noradThresholdAdj;
                theta *= neuronThresholdMod;

                byte region = _regionId[fi];
                if (region < 16)
                {
                    theta += regionInhibCache[region];
                    if (_opt.EnablePredictiveCoding)
                        theta += predErrorCache[region];
                }

                float energyMinToFire = _opt.EnergyMinToFire;
                if (region == RegionIds.CorpusCallosum)
                {
                    // Keep callosal relay activity visible/readable in the live render.
                    theta *= 0.60f;
                    energyMinToFire *= 0.45f;
                    v += 0.012f;
                }
                else if (region == RegionIds.Cerebellum)
                {
                    // Cerebellar microcircuit should remain active enough to read in spikes.
                    theta *= 0.72f;
                    energyMinToFire *= 0.65f;
                    v += 0.006f;
                }

                if (_opt.EnableHemisphereSpecialization)
                {
                    bool isLeftDominant = region == RegionIds.InferiorFrontalGyrus
                        || region == RegionIds.PrecentralGyrus || region == RegionIds.PostcentralGyrus
                        || region == RegionIds.SuperiorFrontalGyrus || region == RegionIds.MiddleFrontalGyrus
                        || region == RegionIds.BasalGanglia;

                    bool isRightDominant = region == RegionIds.SuperiorParietal
                        || region == RegionIds.InferiorParietal || region == RegionIds.AngularGyrus
                        || region == RegionIds.SuperiorOccipital || region == RegionIds.InferiorOccipital
                        || region == RegionIds.SuperiorTemporalGyrus
                        || region == RegionIds.Hippocampus || region == RegionIds.Amygdala;

                    if (isLeft && isLeftDominant)
                        theta *= (1.0f / _opt.LeftDominantRegionBoost);
                    else if (!isLeft && isRightDominant)
                        theta *= (1.0f / _opt.RightDominantRegionBoost);
                }

                v *= resetMul;
                _vm[fi] = v;

                float e = _energy[fi];
                // Sleep-phase-aware ATP recovery: energyRecoverPerTick was
                // pre-scaled in Step() by the sleep multiplier (NREM/REM/wake).
                e = MathF.Min(_opt.EnergyMax, e + energyRecoverPerTick);
                _energy[fi] = e;

                theta = Approach(theta, baseT, _opt.ThresholdRecover);
                _theta[fi] = theta;

                if (v > theta && e > energyMinToFire)
                {
                    // Apply intrinsic spike costs + reset here (per-voxel only)
                    e = MathF.Max(0f, e - _opt.EnergySpikeCost);
                    _energy[fi] = e;
                    theta += _opt.ThresholdSpikeBump;
                    _theta[fi] = theta;
                    _vm[fi] = _opt.Rest;

                    if (spikesTmpCount == spikesTmp.Length)
                    {
                        // grow scratch
                        var bigger = System.Buffers.ArrayPool<int>.Shared.Rent(spikesTmp.Length * 2);
                        System.Array.Copy(spikesTmp, bigger, spikesTmp.Length);
                        System.Buffers.ArrayPool<int>.Shared.Return(spikesTmp);
                        spikesTmp = bigger;
                    }
                    spikesTmp[spikesTmpCount++] = fi;
                }
            }
        }
        else
        {
            System.Threading.Tasks.Parallel.For(0, activeIdx.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = (_opt.ParallelMaxDegreeOfParallelism <= 0 ? -1 : _opt.ParallelMaxDegreeOfParallelism) }, ai =>
            {
                int fi = activeIdx[ai];

                float neuronThresholdMod = 1.0f;
                float neuronLeakMod = 1.0f;

                if (useMicrocircuit)
                {
                    neuronThresholdMod = mcThreshold[fi];
                    neuronLeakMod = mcLeak[fi];
                }

                float leak = baseLeak * neuronLeakMod;
                leak = Math.Clamp(leak, 0.80f, 0.998f);

                float v = _vm[fi];
                v = v * leak + oscLocal + thalamicAddLocal;

                float theta = _theta[fi] - dopaTerm + seroTerm;
                theta -= arousalTerm;
                theta += stabTerm;
                theta += noradThresholdAdj;
                theta *= neuronThresholdMod;

                byte region = _regionId[fi];
                if (region < 16)
                {
                    theta += regionInhibCache[region];
                    if (_opt.EnablePredictiveCoding)
                        theta += predErrorCache[region];
                }

                float energyMinToFire = _opt.EnergyMinToFire;
                if (region == RegionIds.CorpusCallosum)
                {
                    theta *= 0.60f;
                    energyMinToFire *= 0.45f;
                    v += 0.012f;
                }
                else if (region == RegionIds.Cerebellum)
                {
                    theta *= 0.72f;
                    energyMinToFire *= 0.65f;
                    v += 0.006f;
                }

                if (_opt.EnableHemisphereSpecialization)
                {
                    bool isLeftDominant = region == RegionIds.InferiorFrontalGyrus
                        || region == RegionIds.PrecentralGyrus || region == RegionIds.PostcentralGyrus
                        || region == RegionIds.SuperiorFrontalGyrus || region == RegionIds.MiddleFrontalGyrus
                        || region == RegionIds.BasalGanglia;

                    bool isRightDominant = region == RegionIds.SuperiorParietal
                        || region == RegionIds.InferiorParietal || region == RegionIds.AngularGyrus
                        || region == RegionIds.SuperiorOccipital || region == RegionIds.InferiorOccipital
                        || region == RegionIds.SuperiorTemporalGyrus
                        || region == RegionIds.Hippocampus || region == RegionIds.Amygdala;

                    if (isLeft && isLeftDominant)
                        theta *= (1.0f / _opt.LeftDominantRegionBoost);
                    else if (!isLeft && isRightDominant)
                        theta *= (1.0f / _opt.RightDominantRegionBoost);
                }

                v *= resetMul;
                _vm[fi] = v;

                float e = _energy[fi];
                // Sleep-phase-aware ATP recovery: energyRecoverPerTick was
                // pre-scaled in Step() by the sleep multiplier (NREM/REM/wake).
                e = MathF.Min(_opt.EnergyMax, e + energyRecoverPerTick);
                _energy[fi] = e;

                theta = Approach(theta, baseT, _opt.ThresholdRecover);
                _theta[fi] = theta;

                if (v > theta && e > energyMinToFire)
                {
                    e = MathF.Max(0f, e - _opt.EnergySpikeCost);
                    _energy[fi] = e;
                    theta += _opt.ThresholdSpikeBump;
                    _theta[fi] = theta;
                    _vm[fi] = _opt.Rest;

                    tls!.Value!.Add(fi);
                }
            });

            // Merge thread-local spikes into scratch
            foreach (var list in tls.Values)
            {
                if (list is null || list.Count == 0) continue;
                int needed = spikesTmpCount + list.Count;
                if (needed > spikesTmp.Length)
                {
                    var bigger = System.Buffers.ArrayPool<int>.Shared.Rent(Math.Max(needed, spikesTmp.Length * 2));
                    System.Array.Copy(spikesTmp, bigger, spikesTmpCount);
                    System.Buffers.ArrayPool<int>.Shared.Return(spikesTmp);
                    spikesTmp = bigger;
                }
                list.CopyTo(spikesTmp, spikesTmpCount);
                spikesTmpCount += list.Count;
                list.Clear();
            }
            // tls is reused across calls; do not dispose here.
        }

        // Phase 2 (deterministic): sort spikes by voxel index and apply non-local effects & propagation
        if (spikesTmpCount > 1) System.Array.Sort(spikesTmp, 0, spikesTmpCount);

        // Precompute inhibition kernel for this step (avoids per-neighbor sqrt work)
        EnsureInhibitionKernel(_opt.InhibRadius);

        // PERF: batch delayed-signal enqueues into per-bucket buffers (reduces lock pressure & overhead).
        using var batchLocal = new SignalBatcher(volSelf);
        using var batchCross = new SignalBatcher(volOther);

        for (int si = 0; si < spikesTmpCount; si++)
        {
            int fi = spikesTmp[si];

            // convert index -> x,y,z (only for spikes)
            int x = fi % _opt.W;
            int t0 = fi / _opt.W;
            int y = t0 % _opt.H;
            int z = t0 / _opt.H;

            // recover neuron-type sign/VIP for E/I differentiation
            float neuronSynapticSign = 1.0f;
            bool isVIP = false;
            bool isInterneuron = false;
            if (useMicrocircuit)
            {
                neuronSynapticSign = mcSign[fi];
                isVIP = mcVip[fi];
                isInterneuron = (neuronSynapticSign < 0);
                // Microcircuit.OnSpike is deferred to the sequential post-pass
                // (ProcessSpikesFused) because the microcircuit instance is shared
                // between hemispheres and its arrays are not thread-safe.
            }

            spikeList.Add((hemiByte, fi));
            spikeCount++;

            // Biologically inspired post-spike plasticity:
            // a postsynaptic spike updates all incoming synapses (local + callosal incoming).
            local.ApplyPostSpikePlasticity(fi, _opt, StepIndex, dt, postSpk: 1f, neuromodGate: plasticityNeuromodGate);
            crossIn.ApplyPostSpikePlasticity(fi, _opt, StepIndex, dt, postSpk: 1f, neuromodGate: plasticityNeuromodGate);

            // Lateral effects
            if (isInterneuron)
            {
                if (isVIP)
                    ApplyVIPDisinhibition(volSelf, x, y, z, _opt.InhibRadius, inhibStrength * 0.6f);
                else
                    ApplyLateralInhibition(volSelf, x, y, z, _opt.InhibRadius, inhibStrength * 1.5f);
            }
            else
            {
                ApplyLateralInhibition(volSelf, x, y, z, _opt.InhibRadius, inhibStrength);
            }

            // Local propagation
            PropagateToWithSign(volSelf, volSelf, local, srcHemi: hemiByte, dstHemi: hemiByte, preIdx: fi, dt: dt, inputGain: inputGain,
                countAsCallosal: false, ref callosalEventCount, thalamicPulse: thalamicPulse, hebbMod: hemiHebbMod,
                synapticSign: neuronSynapticSign, isVIP: isVIP, batcher: batchLocal,
                plasticityNeuromodGate: plasticityNeuromodGate);

            // Callosal propagation
            PropagateToWithSign(volSelf, volOther, crossOut, srcHemi: hemiByte, dstHemi: (byte)(hemiByte == 0 ? 1 : 0), preIdx: fi, dt: dt, inputGain: inputGain,
                countAsCallosal: true, ref callosalEventCount, thalamicPulse: thalamicPulse, hebbMod: hemiHebbMod,
                synapticSign: neuronSynapticSign, isVIP: isVIP, batcher: batchCross,
                plasticityNeuromodGate: plasticityNeuromodGate);
        }

        System.Buffers.ArrayPool<int>.Shared.Return(spikesTmp);
        innerSw?.Stop();
        // Per-hemisphere timing is aggregated after the parallel section returns.
        // (Writing HemiTimingDetail from two threads concurrently was racy and was
        // overwritten unconditionally by the caller — see NreEngine.Step.)
        return callosalEventCount;

        static float Approach(float cur, float target, float rate)
        {
            if (cur < target) return MathF.Min(target, cur + rate);
            return MathF.Max(target, cur - rate);
        }
    }

    private void ProcessSignalArrivals(NeuralVolume vol)
    {
        // Next-notch: ring-buffered delayed buckets (no per-voxel Queue, no locking while draining).
        vol.DrainDueSignals();
    }


    private void EnsureInhibitionKernel(int radius)
    {
        if (_inhibKernelRadius == radius && _inhibDx is not null) return;

        // Build offsets within radius (excluding center) with precomputed distance falloff weights.
        var dxList = new System.Collections.Generic.List<short>(256);
        var dyList = new System.Collections.Generic.List<short>(256);
        var dzList = new System.Collections.Generic.List<short>(256);
        var wList  = new System.Collections.Generic.List<float>(256);

        float denom = radius + 0.0001f;
        for (int dz = -radius; dz <= radius; dz++)
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx == 0 && dy == 0 && dz == 0) continue;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist > radius) continue;
            float w = 1f - dist / denom;
            if (w <= 0f) continue;
            dxList.Add((short)dx);
            dyList.Add((short)dy);
            dzList.Add((short)dz);
            wList.Add(w);
        }

        _inhibDx = dxList.ToArray();
        _inhibDy = dyList.ToArray();
        _inhibDz = dzList.ToArray();
        _inhibW  = wList.ToArray();
        _inhibKernelRadius = radius;
    }

    private void ApplyLateralInhibition(NeuralVolume vol, int cx, int cy, int cz, int radius, float strength)
    {
        EnsureInhibitionKernel(radius);
        var dxA = _inhibDx!;
        var dyA = _inhibDy!;
        var dzA = _inhibDz!;
        var wA  = _inhibW!;
        var vm = vol.Vm;
        var active = vol.Active;
        int W = vol.W, H = vol.H, D = vol.D;
        int WH = W * H;

        for (int i = 0; i < wA.Length; i++)
        {
            int x = cx + dxA[i];
            int y = cy + dyA[i];
            int z = cz + dzA[i];
            if ((uint)x >= (uint)W || (uint)y >= (uint)H || (uint)z >= (uint)D) continue;
            int nfi = (z * H + y) * W + x;
            if (!active[nfi]) continue;
            vm[nfi] -= strength * wA[i];
        }
    }

    /// <summary>
    /// Entry 025: VIP interneuron disinhibition.
    /// VIP cells inhibit other interneurons (PV, SOM), which reduces inhibition on pyramidal cells.
    /// Net effect: EXCITATION of nearby pyramidal neurons (disinhibition circuit).
    /// - VIP -> PV/SOM (inhibitory)
    /// - PV/SOM -> pyramidal (inhibitory)
    /// - VIP fires -> less PV/SOM activity -> more pyramidal activity
    /// - PV/SOM --- Pyramidal (inhibitory)
    /// - VIP fires --- less PV/SOM activity --- more pyramidal activity
    /// 
    /// Implementation: Boost Vm of nearby neurons (simulates reduced inhibition = net excitation)
    /// </summary>
    private void ApplyVIPDisinhibition(NeuralVolume vol, int cx, int cy, int cz, int radius, float strength)
    {
        EnsureInhibitionKernel(radius);
        var dxA = _inhibDx!;
        var dyA = _inhibDy!;
        var dzA = _inhibDz!;
        var wA  = _inhibW!;
        var vm = vol.Vm;
        var active = vol.Active;
        int W = vol.W, H = vol.H, D = vol.D;
        int WH = W * H;

        // If microcircuit is enabled, use cached neuron types (avoids per-neighbor Microcircuit.GetNeuronType calls).
        var typeCache = _opt.EnableCorticalMicrocircuit ? _mcTypeCache : null;

        for (int i = 0; i < wA.Length; i++)
        {
            int x = cx + dxA[i];
            int y = cy + dyA[i];
            int z = cz + dzA[i];
            if ((uint)x >= (uint)W || (uint)y >= (uint)H || (uint)z >= (uint)D) continue;
            int nfi = (z * H + y) * W + x;
            if (!active[nfi]) continue;

            float factor = strength * wA[i];
            if (typeCache is not null)
            {
                var targetType = (CorticalMicrocircuit.NeuronType)typeCache[nfi];
                if (targetType == CorticalMicrocircuit.NeuronType.Pyramidal ||
                    targetType == CorticalMicrocircuit.NeuronType.Subcortical)
                    vm[nfi] += factor;
                else
                    vm[nfi] -= factor * 0.8f;
            }
            else
            {
                vm[nfi] += factor;
            }
        }
    }
    
    /// <summary>
    /// Entry 025: Propagate spike with synaptic sign (E/I differentiation).
    /// Excitatory neurons (pyramidal) send positive signals.
    /// Inhibitory neurons (PV, SOM, VIP) send negative signals.
    /// VIP neurons have special targeting (prefer interneurons over pyramidals).
    /// </summary>
    private void PropagateToWithSign(
        NeuralVolume srcVol,
        NeuralVolume dstVol,
        SynapseMap map,
        byte srcHemi,
        byte dstHemi,
        int preIdx,
        float dt,
        float inputGain,
        bool countAsCallosal,
        ref float callosalCount,
        ThalamicPulse thalamicPulse,
        float hebbMod,
        float synapticSign,
        bool isVIP,
        SignalBatcher batcher,
        float plasticityNeuromodGate)
    {
        var outs = map.GetOutgoing(preIdx);
        if (outs == null) return;
        byte sRegion = srcVol.RegionId[preIdx];
        
        int outCount = outs.Count;
        for (int i = 0; i < outCount; i++)
        {
            var syn = outs[i];
            int postIdx = syn.Post;
            float postVm = dstVol.Vm[postIdx];

            map.ApplyStpOnSpike(syn, _opt, dt);
            map.ApplyPrePostPlasticityOnSpike(
                syn,
                _opt,
                preSpk: 1f,
                postVm: postVm,
                stepIndex: StepIndex,
                dtPerStep: dt,
                hebbMod: hebbMod,
                neuromodGate: plasticityNeuromodGate);

            float eff = syn.W * syn.Facil * syn.Depr;
            if (_opt.EnablePrePostSynapticPlasticity)
                eff *= syn.PreRelease * syn.PostSensitivity;
            eff *= inputGain;
            
            // === Entry 025: Apply synaptic sign ===
            // Excitatory neurons: positive effect
            // Inhibitory neurons: negative effect (reduce target Vm)
            eff *= synapticSign;
            
            byte tRegion = dstVol.RegionId[postIdx];
            // === Entry 025: VIP targeting ===
            // VIP interneurons preferentially target other interneurons
            // This creates disinhibition when VIP fires
            if (isVIP && _opt.EnableCorticalMicrocircuit && _mcTypeCache is not null)
            {
                var targetType = (CorticalMicrocircuit.NeuronType)_mcTypeCache[postIdx];
if (targetType == CorticalMicrocircuit.NeuronType.PVInterneuron ||
                    targetType == CorticalMicrocircuit.NeuronType.SOMInterneuron)
                {
                    // VIP strongly inhibits other interneurons
                    eff *= 1.5f;
                }
                else if (targetType == CorticalMicrocircuit.NeuronType.Pyramidal)
                {
                    // VIP weakly affects pyramidals directly
                    eff *= 0.3f;
                }
            }
            
            // Thalamic binding: reduce delay for synchronized spikes
            int effectiveDelay = syn.DelayTicks;
            if (thalamicPulse.IsBinding)
                effectiveDelay = Math.Max(1, (int)(syn.DelayTicks / thalamicPulse.SpeedBoost));
            
            // PERF: batch enqueue into delayed-signal ring buckets to reduce lock overhead.
            batcher.Add(postIdx, effectiveDelay, eff);

            // Apply hemisphere-specific Hebbian learning rate
            map.HebbianLite(syn, _opt, preSpk: 1f, postVm: postVm, hebbMod: hebbMod * plasticityNeuromodGate);

            if (countAsCallosal)
                callosalCount += 1f;

            // PERF:             // Option 3: Cross-module traffic flash events only (region change or inter-hemisphere).
            // PERF:             if (sRegion != tRegion || srcHemi != dstHemi)
            // PERF:             {
            // PERF:                 if (Interlocked.CompareExchange(ref _trafficSincePublish, 0, 0) >= _opt.UiMaxTrafficEvents * 2)
            // PERF:                     continue;
            // PERF: 
            // PERF:                 // Compact strength encoding.
            // PERF:                 byte strength = (byte)Math.Clamp((int)(MathF.Abs(eff) * 255f), 0, 255);
            // PERF:                 if (strength > 0)
            // PERF:                 {
            // PERF:                     _trafficEventsBag.Add(new TrafficEvent(
            // PERF:                         srcHemi, (byte)sx, (byte)sy, (byte)sz, sRegion,
            // PERF:                         dstHemi, (byte)tx, (byte)ty, (byte)tz, tRegion,
            // PERF:                         strength));
            // PERF:                     Interlocked.Increment(ref _trafficSincePublish);
            // PERF:                 }
            // PERF:             }
            // PERF:             }
        }
    }

    /// <summary>
    /// High-performance batching of delayed signals into NeuralVolume ring buckets.
    /// Locks each bucket at most once per flush, reducing contention and allocations.
    /// </summary>
    private sealed class SignalBatcher : IDisposable
    {
        private readonly NeuralVolume _dst;
        private readonly int _ringSize;
        private readonly int _ringCursor;
        private readonly SignalEvent[][] _bufs;
        private readonly int[] _counts;
        private readonly int _initialCap;

        public SignalBatcher(NeuralVolume dst, int initialCapacityPerBucket = 2048)
        {
            _dst = dst;
            _ringSize = dst.RingSize;
            _ringCursor = dst.RingCursor;
            _initialCap = Math.Max(256, initialCapacityPerBucket);
            _bufs = new SignalEvent[_ringSize][];
            _counts = new int[_ringSize];
            for (int i = 0; i < _ringSize; i++)
                _bufs[i] = ArrayPool<SignalEvent>.Shared.Rent(_initialCap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BucketIndex(int delayTicks)
        {
            if (delayTicks < 0) delayTicks = 0;
            return (_ringCursor + delayTicks) % _ringSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int fi, int delayTicks, float intensity)
        {
            if (!_dst.Active[fi]) return;
            int bi = BucketIndex(delayTicks);
            var arr = _bufs[bi];
            int c = _counts[bi];
            if (c >= arr.Length)
            {
                FlushBucket(bi);
                c = 0;
            }

            arr[c] = new SignalEvent(fi, intensity);
            _counts[bi] = c + 1;
        }

        private void FlushBucket(int bi)
        {
            int c = _counts[bi];
            if (c <= 0) return;
            _dst.AddManyToBucket(bi, _bufs[bi], c);
            _counts[bi] = 0;
        }

        public void FlushAll()
        {
            for (int bi = 0; bi < _ringSize; bi++)
                FlushBucket(bi);
        }

        public void Dispose()
        {
            try { FlushAll(); }
            finally
            {
                for (int i = 0; i < _ringSize; i++)
                {
                    var arr = _bufs[i];
                    if (arr != null)
                        ArrayPool<SignalEvent>.Shared.Return(arr, clearArray: false);
                }
            }
        }
    }

    
private void ApplyRegionLayout(NeuralVolume vol, bool isLeftHemisphere)
{
    int w = vol.W, h = vol.H, d = vol.D;

    float cx = (w - 1) * 0.5f;
    float cy = (h - 1) * 0.5f;
    float cz = (d - 1) * 0.5f;

    float rx0 = w * 0.48f;
    float ry0 = h * 0.44f;
    float rz0 = d * 0.46f;

    float cortexInner = 1.0f - (2.2f / MathF.Max(8f, (rx0 + ry0 + rz0) / 3f));

    static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    static float SmoothStep(float a, float b, float x)
    {
        float t = Clamp01((x - a) / (b - a + 1e-6f));
        return t * t * (3f - 2f * t);
    }

    bool InBrainOuter(int x, int y, int z)
    {
        float nx0 = (x - cx) / (rx0 + 1e-6f);
        float ny0 = (cy - y) / (ry0 + 1e-6f);
        float nz0 = (z - cz) / (rz0 + 1e-6f);

        float frontal = 1f - SmoothStep(-0.85f, -0.05f, nz0);
        float occip = SmoothStep(0.05f, 0.85f, nz0);
        float inferior = 1f - SmoothStep(-0.75f, -0.05f, ny0);
        float lateral = SmoothStep(0.15f, 0.95f, MathF.Abs(nx0));

        float mx = 1.00f + 0.22f * frontal - 0.12f * occip + 0.26f * inferior * lateral;
        float my = 1.00f + 0.10f * frontal - 0.06f * occip + 0.10f * inferior;
        float mz = 1.00f + 0.10f * frontal + 0.02f * occip;

        float nx = (x - cx) / (rx0 * mx + 1e-6f);
        float ny = (cy - y) / (ry0 * my + 1e-6f);
        float nz = (z - cz) / (rz0 * mz + 1e-6f);

        return (nx * nx + ny * ny + nz * nz) <= 1.0f;
    }

    bool InBrainInner(int x, int y, int z)
    {
        float nx0 = (x - cx) / (rx0 + 1e-6f);
        float ny0 = (cy - y) / (ry0 + 1e-6f);
        float nz0 = (z - cz) / (rz0 + 1e-6f);

        float frontal = 1f - SmoothStep(-0.85f, -0.05f, nz0);
        float occip = SmoothStep(0.05f, 0.85f, nz0);
        float inferior = 1f - SmoothStep(-0.75f, -0.05f, ny0);
        float lateral = SmoothStep(0.15f, 0.95f, MathF.Abs(nx0));

        float mx = (1.00f + 0.24f * frontal - 0.06f * occip + 0.32f * inferior * lateral) * cortexInner;
        float my = (1.00f + 0.12f * frontal - 0.04f * occip + 0.14f * inferior) * cortexInner;
        float mz = (1.00f + 0.10f * frontal + 0.02f * occip) * cortexInner;

        float nx = (x - cx) / (rx0 * mx + 1e-6f);
        float ny = (cy - y) / (ry0 * my + 1e-6f);
        float nz = (z - cz) / (rz0 * mz + 1e-6f);

        return (nx * nx + ny * ny + nz * nz) <= 1.0f;
    }

    static bool InCapsule(int x, int y, int z, (float x, float y, float z) a, (float x, float y, float z) b, float r)
    {
        float px = x, py = y, pz = z;
        float ax = a.x, ay = a.y, az = a.z;
        float bx = b.x, by = b.y, bz = b.z;

        float abx = bx - ax, aby = by - ay, abz = bz - az;
        float apx = px - ax, apy = py - ay, apz = pz - az;

        float ab2 = abx * abx + aby * aby + abz * abz + 1e-6f;
        float t = (apx * abx + apy * aby + apz * abz) / ab2;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;

        float cxp = ax + abx * t;
        float cyp = ay + aby * t;
        float czp = az + abz * t;

        float dx = px - cxp, dy = py - cyp, dz = pz - czp;
        return (dx * dx + dy * dy + dz * dz) <= (r * r);
    }

    static bool InEll(int x, int y, int z, (float x, float y, float z) c, (float x, float y, float z) r)
    {
        float rx = MathF.Max(0.0001f, r.x);
        float ry = MathF.Max(0.0001f, r.y);
        float rz = MathF.Max(0.0001f, r.z);

        float dx = (x - c.x) / rx;
        float dy = (y - c.y) / ry;
        float dz = (z - c.z) / rz;
        return (dx * dx + dy * dy + dz * dz) <= 1f;
    }

    // Relative volumes (mL) -> linear scale via cube-root.
    float ScaleFromMl(float ml, float reference = 14f) => MathF.Pow(MathF.Max(0.1f, ml) / reference, 1f / 3f);

    // === ANATOMICALLY-CORRECT BASE RADII (tuned for 32^3 voxel grid) ===
    var thBase = (x: w * 0.12f, y: h * 0.09f, z: d * 0.08f);
    float thS = ScaleFromMl(14f);

    var hyBase = (x: w * 0.05f, y: h * 0.04f, z: d * 0.035f);
    float hyS = ScaleFromMl(1.0f);

    var bgBase = (x: w * 0.09f, y: h * 0.11f, z: d * 0.08f);
    float bgS = ScaleFromMl(10f);

    var amBase = (x: w * 0.055f, y: h * 0.05f, z: d * 0.045f);
    float amS = ScaleFromMl(2.0f);

    var cbBase = (x: w * 0.18f, y: h * 0.14f, z: d * 0.12f);
    float cbS = ScaleFromMl(57f, reference: 14f);
    cbS = MathF.Min(cbS, 1.8f);

    float medialDir = isLeftHemisphere ? 1f : -1f;

    float Z01(float z01, float zOffsetVoxels)
        => Math.Clamp(z01 * (d - 1) + zOffsetVoxels, 0f, d - 1);

    const float Z_TEMPORAL_ANT = 0.35f;
    const float Z_TEMPORAL_MID = 0.50f;
    const float Z_TEMPORAL_POST = 0.65f;
    const float Z_CEREBELLAR = 0.85f;
    const float Z_BRAINSTEM = 0.75f;
    const float Y_DORSAL = -0.15f;

    // === STRUCTURE PLACEMENTS ===
    var thC = (x: cx, y: cy + h * (Y_DORSAL + 0.01f), z: Z01(0.44f, _opt.AnatomyZOffsetThalamusVoxels));
    var thR = (x: thBase.x * thS, y: thBase.y * thS, z: thBase.z * thS);

    var hyC = (x: cx, y: cy + h * 0.12f, z: Z01(0.38f, _opt.AnatomyZOffsetHypothalamusVoxels));
    var hyR = (x: hyBase.x * hyS, y: hyBase.y * hyS, z: hyBase.z * hyS);

    var bgC = (x: cx + medialDir * w * 0.16f, y: cy + h * 0.04f, z: Z01(0.36f, _opt.AnatomyZOffsetBasalGangliaVoxels));
    var bgR = (x: bgBase.x * bgS, y: bgBase.y * bgS, z: bgBase.z * bgS);

    var amC = (x: cx + medialDir * w * 0.20f, y: cy + h * 0.14f, z: Z01(Z_TEMPORAL_ANT, _opt.AnatomyZOffsetAmygdalaVoxels));
    var amR = (x: amBase.x * amS, y: amBase.y * amS, z: amBase.z * amS);

    var hipLat = medialDir * w * 0.18f;
    var hipA = (x: cx + hipLat, y: cy + h * 0.10f, z: Z01(Z_TEMPORAL_MID - 0.06f, _opt.AnatomyZOffsetHippocampusVoxels));
    var hipB = (x: cx + hipLat, y: cy + h * 0.03f, z: Z01(Z_TEMPORAL_POST + 0.02f, _opt.AnatomyZOffsetHippocampusVoxels));
    float hipR = MathF.Max(2.2f, (w * 0.075f) * ScaleFromMl(3.3f));

    var cbC = (x: cx, y: cy + h * 0.26f, z: Z01(Z_CEREBELLAR, _opt.AnatomyZOffsetCerebellumVoxels));
    var cbR = (x: cbBase.x * cbS, y: cbBase.y * cbS, z: cbBase.z * cbS);

    var bsA = (x: cx, y: cy + h * 0.20f, z: Z01(Z_BRAINSTEM - 0.05f, _opt.AnatomyZOffsetBrainstemVoxels));
    var bsB = (x: cx, y: cy + h * 0.36f, z: Z01(0.92f, _opt.AnatomyZOffsetBrainstemVoxels));
    float bsR = MathF.Max(1.6f, w * 0.038f);

    var pnC = (x: cx, y: cy + h * 0.22f, z: Z01(0.72f, _opt.AnatomyZOffsetPonsVoxels));
    var pnR = (x: w * 0.08f, y: h * 0.06f, z: d * 0.06f);

    byte PickCortexRegion(int x, int y, int z)
    {
        float cxL = (w - 1) * 0.5f;
        float cyL = (h - 1) * 0.5f;
        float czL = (d - 1) * 0.5f;

        float xn = ((x - cxL) / MathF.Max(1f, cxL));
        float yn = ((cyL - y) / MathF.Max(1f, cyL));
        float zn = ((z - czL) / MathF.Max(1f, czL));

        float len = MathF.Sqrt(xn * xn + yn * yn + zn * zn);
        if (len > 1e-5f) { xn /= len; yn /= len; zn /= len; }

        float lat = MathF.Abs(xn);
        bool lateralB = lat > 0.28f;
        bool dorsal = yn > 0.18f;
        bool ventral = yn < -0.18f;
        bool anterior = zn < -0.20f;
        bool posterior = zn > 0.28f;

        if (lateralB && yn > 0.05f)
        {
            if (zn >= -0.10f && zn < 0.03f) return RegionIds.PrecentralGyrus;
            if (zn >= 0.03f && zn < 0.16f) return RegionIds.PostcentralGyrus;
        }

        if (anterior)
        {
            if (yn > 0.42f) return RegionIds.SuperiorFrontalGyrus;
            if (yn > 0.10f) return RegionIds.MiddleFrontalGyrus;
            return RegionIds.InferiorFrontalGyrus;
        }

        if (posterior)
        {
            if (yn > 0.22f) return RegionIds.SuperiorOccipital;
            return RegionIds.InferiorOccipital;
        }

        if (dorsal && zn >= 0.12f)
        {
            if (yn > 0.45f) return RegionIds.SuperiorParietal;
            if (zn < 0.26f) return RegionIds.SupramarginalGyrus;
            return RegionIds.AngularGyrus;
        }

        if (!ventral && zn >= 0.12f && zn < 0.35f)
            return RegionIds.InferiorParietal;

        if ((ventral || yn < 0.08f) && zn > -0.06f && zn < 0.42f)
        {
            if (yn > -0.08f) return RegionIds.SuperiorTemporalGyrus;
            if (yn > -0.28f) return RegionIds.MiddleTemporalGyrus;
            return RegionIds.InferiorTemporalGyrus;
        }

        if (dorsal) return RegionIds.SuperiorParietal;
        if (ventral) return RegionIds.MiddleTemporalGyrus;
        return RegionIds.MiddleFrontalGyrus;
    }

    // === MAIN VOXEL LOOP ===
    for (int x = 0; x < w; x++)
    for (int y = 0; y < h; y++)
    for (int z = 0; z < d; z++)
    {
        if (!InBrainOuter(x, y, z))
        {
            vol.Active[vol.IndexOf(x, y, z)] = false;
            vol.RegionId[vol.IndexOf(x, y, z)] = 255;
            continue;
        }

        // Brainstem/pons/cerebellum checked FIRST --- they extend into mantle zone
        if (InCapsule(x, y, z, bsA, bsB, bsR))
        {
            vol.Active[vol.IndexOf(x, y, z)] = true;
            vol.RegionId[vol.IndexOf(x, y, z)] = 7; // Brainstem
            continue;
        }
        if (InEll(x, y, z, pnC, pnR))
        {
            vol.Active[vol.IndexOf(x, y, z)] = true;
            vol.RegionId[vol.IndexOf(x, y, z)] = 8; // Pons
            continue;
        }
        if (InEll(x, y, z, cbC, cbR))
        {
            vol.Active[vol.IndexOf(x, y, z)] = true;
            vol.RegionId[vol.IndexOf(x, y, z)] = RegionIds.Cerebellum;
            continue;
        }

        // Cortex mantle: outer shell between outer and inner envelopes
        if (!InBrainInner(x, y, z))
        {
            float fissHalf = 0.90f;
            float cxMid = (w - 1) * 0.5f;
            if (MathF.Abs(x - cxMid) < fissHalf)
            {
                vol.Active[vol.IndexOf(x, y, z)] = false;
                vol.RegionId[vol.IndexOf(x, y, z)] = 255;
                continue;
            }

            byte cr = PickCortexRegion(x, y, z);
            vol.Active[vol.IndexOf(x, y, z)] = true;
            vol.RegionId[vol.IndexOf(x, y, z)] = cr;
            continue;
        }

        // Deep structures (inside mantle)
        byte region = 255;
        bool active = false;

        if (InEll(x, y, z, thC, thR)) { region = RegionIds.Thalamus; active = true; }
        else if (InEll(x, y, z, hyC, hyR)) { region = RegionIds.Hypothalamus; active = true; }
        else if (InEll(x, y, z, bgC, bgR)) { region = RegionIds.BasalGanglia; active = true; }
        else if (InEll(x, y, z, amC, amR)) { region = RegionIds.Amygdala; active = true; }
        else if (InCapsule(x, y, z, hipA, hipB, hipR)) { region = RegionIds.Hippocampus; active = true; }
        else
        {
            float fissHalf2 = 0.90f;
            float cxMid2 = (w - 1) * 0.5f;
            if (MathF.Abs(x - cxMid2) < fissHalf2)
            {
                region = 255;
                active = false;
            }
            else
            {
                region = PickCortexRegion(x, y, z);
                active = true;
            }
        }

        vol.Active[vol.IndexOf(x, y, z)] = active;
        vol.RegionId[vol.IndexOf(x, y, z)] = active ? region : (byte)255;
    }

    // Corpus callosum band
    CarveCorpusCallosumBand(vol);
}

void CarveCorpusCallosumBand(NeuralVolume vol)
{
    int w = vol.W, h = vol.H, d = vol.D;
    int xMid = w / 2;
    int ccHalfWidth = 5;

    int zMin = (int)(d * 0.14f);
    int zMax = (int)(d * 0.86f);
    if (zMax <= zMin) { zMin = 0; zMax = d - 1; }

    int yEnd = (int)(h * 0.26f);
    int yMid = (int)(h * 0.14f);
    int yHalf = Math.Max(3, (int)(h * 0.08f));

    int zMidPt = (zMin + zMax) / 2;
    float zSpan = Math.Max(1.0f, (zMax - zMin) * 0.5f);

    for (int z = zMin; z <= zMax; z++)
    {
        float t = (z - zMidPt) / zSpan;
        t = Math.Clamp(t, -1f, 1f);

        float yf = yMid + (yEnd - yMid) * (t * t);
        int yCenter = (int)MathF.Round(yf);

        int y0 = Math.Clamp(yCenter - yHalf, 0, h - 1);
        int y1 = Math.Clamp(yCenter + yHalf, 0, h - 1);

        for (int y = y0; y <= y1; y++)
        for (int x = Math.Max(0, xMid - ccHalfWidth); x <= Math.Min(w - 1, xMid + ccHalfWidth); x++)
        {
            byte existing = vol.RegionId[vol.IndexOf(x, y, z)];
            if (existing != 255 && !RegionIds.IsCortex(existing)) continue;
            vol.Active[vol.IndexOf(x, y, z)] = true;
            vol.RegionId[vol.IndexOf(x, y, z)] = RegionIds.CorpusCallosum;
        }
    }
}
    private void BuildInitialConnectivityBiological(SynapseMap map, NeuralVolume vol, Random? rngOverride = null)
    {
        var rng = rngOverride ?? _rng;
        // BIOLOGY (Folded Archive):
        // - Wiring is dominated by local, distance-biased microcircuits.
        // - Long-range tracts are sparse and structured (area-to-area, not uniform random).
        // - Corpus callosum handled separately.
        // - Subcortical nuclei have mostly local wiring + specific loop projections.

        int n = _opt.W * _opt.H * _opt.D;

        static bool IsCortex(byte r) => RegionIds.IsCortex(r);

        // Build region pools for fast selection.
        var regionPool = new List<int>[RegionIds.MaxRegionIdUsed + 1];
        for (int r = 0; r < regionPool.Length; r++) regionPool[r] = new List<int>(512);

        for (int idx = 0; idx < n; idx++)
        {
            var (x, y, z) = vol.FromIndex(idx);
            if (!vol.Active[vol.IndexOf(x, y, z)]) continue;
            byte r = vol.RegionId[vol.IndexOf(x, y, z)];
            if (r < regionPool.Length) regionPool[r].Add(idx);
        }

        int jitter = Math.Max(0, _opt.ProjectionTopographicJitterVoxels);
        int maxAttempts = Math.Max(1, _opt.ProjectionMaxPlacementAttempts);

        int PickLocalSameRegion(int x, int y, int z, byte region, int radius)
        {
            for (int a = 0; a < maxAttempts; a++)
            {
                int dx = rng.Next(-radius, radius + 1);
                int dy = rng.Next(-radius, radius + 1);
                int dz = rng.Next(-radius, radius + 1);
                if (dx == 0 && dy == 0 && dz == 0) continue;

                int xx = x + dx, yy = y + dy, zz = z + dz;
                if (xx < 0 || yy < 0 || zz < 0 || xx >= _opt.W || yy >= _opt.H || zz >= _opt.D) continue;
                if (!vol.Active[vol.IndexOf(xx, yy, zz)]) continue;
                if (vol.RegionId[vol.IndexOf(xx, yy, zz)] != region) continue;
                return vol.IndexOf(xx, yy, zz);
            }
            var pool = region < regionPool.Length ? regionPool[region] : null;
            if (pool is { Count: > 0 }) return pool[rng.Next(0, pool.Count)];
            return -1;
        }

        int PickTopographicToRegion(int x, int y, int z, byte targetRegion)
        {
            // Same-hemisphere topographic placement with jitter (mirrors thalamo-cortical style mapping).
            for (int a = 0; a < maxAttempts; a++)
            {
                int dx = jitter == 0 ? 0 : rng.Next(-jitter, jitter + 1);
                int dy = jitter == 0 ? 0 : rng.Next(-jitter, jitter + 1);
                int dz = jitter == 0 ? 0 : rng.Next(-jitter, jitter + 1);

                int xx = Math.Clamp(x + dx, 0, _opt.W - 1);
                int yy = Math.Clamp(y + dy, 0, _opt.H - 1);
                int zz = Math.Clamp(z + dz, 0, _opt.D - 1);
                if (!vol.Active[vol.IndexOf(xx, yy, zz)]) continue;
                if (vol.RegionId[vol.IndexOf(xx, yy, zz)] != targetRegion) continue;
                return vol.IndexOf(xx, yy, zz);
            }
            var pool = targetRegion < regionPool.Length ? regionPool[targetRegion] : null;
            if (pool is { Count: > 0 }) return pool[rng.Next(0, pool.Count)];
            return -1;
        }

        float SampleWeight(bool inhibitory)
        {
            float w = _opt.InitialWeightMean + ((float)rng.NextDouble() - 0.5f) * 2f * _opt.InitialWeightJitter;
            if (inhibitory) w = -Math.Abs(w);
            return w;
        }

        int SampleDelay(int min, int max) => rng.Next(min, max + 1);

        // --- 1) Local microcircuits (dominant) ---
        for (int idx = 0; idx < n; idx++)
        {
            var (x, y, z) = vol.FromIndex(idx);
            if (!vol.Active[vol.IndexOf(x, y, z)]) continue;
            byte r = vol.RegionId[vol.IndexOf(x, y, z)];

            bool cortex = IsCortex(r);
            int fan = cortex ? _opt.LocalFanoutPerNeuron : _opt.SubcorticalLocalFanoutPerNeuron;
            int rad = _opt.LocalRadiusVoxels;
            float inhibFrac = _opt.LocalInhibitoryFraction;

            for (int i = 0; i < fan; i++)
            {
                int post = PickLocalSameRegion(x, y, z, r, rad);
                if (post < 0) continue;
                bool inhibitory = rng.NextDouble() < inhibFrac;
                int delay = SampleDelay(_opt.MinDelayTicks, _opt.MaxDelayTicks);
                map.Add(idx, post, delay, SampleWeight(inhibitory));
            }
        }

        byte SampleNeighborOrSelf(byte cr)
        {
            var neigh = RegionIds.Neighbors(cr);
            if (neigh.Length == 0) return cr;
            // Allow self-targets sometimes to keep local clustering stable.
            if (rng.NextDouble() < 0.15) return cr;
            return neigh[rng.Next(0, neigh.Length)];
        }

        // --- 2) Structured long-range loops (sparse) ---

        // Thalamo-cortical (Region 1 -> cortical gyri)
        if (regionPool[1].Count > 0)
        {
            // Target distribution: sensory + motor + PFC (roughly plausible).
            byte[] targets =
            {
                RegionIds.InferiorOccipital,     // visual
                RegionIds.SuperiorTemporalGyrus, // auditory
                RegionIds.PostcentralGyrus,      // somatosensory
                RegionIds.PrecentralGyrus,       // motor
                RegionIds.MiddleFrontalGyrus     // frontal association
            };
            float[] probs = { 0.28f, 0.18f, 0.18f, 0.18f, 0.18f };

            byte SampleTarget()
            {
                float u = (float)rng.NextDouble();
                float acc = 0;
                for (int i = 0; i < targets.Length; i++)
                {
                    acc += probs[i];
                    if (u <= acc) return targets[i];
                }
                return targets[^1];
            }

            foreach (int pre in regionPool[1])
            {
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < _opt.ThalamoCorticalFanout; i++)
                {
                    byte tr = SampleTarget();
                    int post = PickTopographicToRegion(x, y, z, tr);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }
        }

        // Cortico-thalamic feedback (cortex -> Region 1)
        if (regionPool[1].Count > 0)
        {
            foreach (byte cr in RegionIds.AllCorticalRegions)
            {
                var pres = regionPool[cr];
                if (pres.Count == 0) continue;

                foreach (int pre in pres)
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    for (int i = 0; i < _opt.CorticoThalamicFanout; i++)
                    {
                        int post = PickTopographicToRegion(x, y, z, 1);
                        if (post < 0) continue;
                        int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2);
                        map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                    }
                }
            }
        }

        // Cortico-cortical long-range (within hemisphere): sparse, mostly excitatory
        foreach (byte cr in RegionIds.AllCorticalRegions)
        {
            var pres = regionPool[cr];
            if (pres.Count == 0) continue;

            foreach (int pre in pres)
            {
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < _opt.CorticoCorticalLongRangeFanout; i++)
                {
                    // Target selection:
                    // - Bias to anatomical neighbors (local association tracts).
                    // - Frontal association still projects broadly (executive broadcasting).
                    byte tr;

                    if (cr == RegionIds.SuperiorFrontalGyrus || cr == RegionIds.MiddleFrontalGyrus || cr == RegionIds.InferiorFrontalGyrus)
                    {
                        // 60% neighbor, 40% anywhere in cortex
                        if (rng.NextDouble() < 0.60)
                            tr = SampleNeighborOrSelf(cr);
                        else
                            tr = RegionIds.AllCorticalRegions[rng.Next(0, RegionIds.AllCorticalRegions.Length)];
                    }
                    else
                    {
                        // 75% neighbor, 25% frontal association
                        if (rng.NextDouble() < 0.75)
                            tr = SampleNeighborOrSelf(cr);
                        else
                            tr = RegionIds.FrontalAssociation[rng.Next(0, RegionIds.FrontalAssociation.Length)];
                    }

                    int post = PickTopographicToRegion(x, y, z, tr);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }
        }

        // Hippocampus <-> cortex (Region 5 with frontal + temporal/parietal association)
        if (regionPool[5].Count > 0)
        {
            byte[] hipTargets =
            {
                RegionIds.MiddleFrontalGyrus,
                RegionIds.InferiorFrontalGyrus,
                RegionIds.AngularGyrus,
                RegionIds.SuperiorTemporalGyrus,
                RegionIds.InferiorOccipital,
            };
            foreach (int pre in regionPool[5])
            {
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < _opt.HippocampoCorticalFanout; i++)
                {
                    byte tr = hipTargets[rng.Next(0, hipTargets.Length)];
                    int post = PickTopographicToRegion(x, y, z, tr);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }

            // Cortex -> hippocampus (feedback) - sparse
            foreach (byte cr in hipTargets)
            {
                var pres = regionPool[cr];
                if (pres.Count == 0) continue;
                foreach (int pre in pres)
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, 5);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }
        }

        // Amygdala -> cortex (Region 4) diffuse bias to frontal + sensory association
        if (regionPool[4].Count > 0)
        {
            byte[] amTargets =
            {
                RegionIds.InferiorFrontalGyrus,
                RegionIds.MiddleFrontalGyrus,
                RegionIds.SuperiorTemporalGyrus,
                RegionIds.PostcentralGyrus,
                RegionIds.PrecentralGyrus,
                RegionIds.AngularGyrus
            };
            foreach (int pre in regionPool[4])
            {
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < _opt.AmygdaloCorticalFanout; i++)
                {
                    byte tr = amTargets[rng.Next(0, amTargets.Length)];
                    int post = PickTopographicToRegion(x, y, z, tr);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }
        }

        // Basal ganglia loop (simplified): cortex -> BG (exc), BG -> thalamus (inhib)
        if (regionPool[3].Count > 0)
        {
            // Cortex -> BG
            foreach (byte cr in new byte[]
                     {
                         RegionIds.SuperiorFrontalGyrus,
                         RegionIds.MiddleFrontalGyrus,
                         RegionIds.InferiorFrontalGyrus,
                         RegionIds.PrecentralGyrus,
                         RegionIds.PostcentralGyrus
                     })
            {
                var pres = regionPool[cr];
                if (pres.Count == 0) continue;
                foreach (int pre in pres)
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    for (int i = 0; i < _opt.BasalGangliaFanout; i++)
                    {
                        int post = PickTopographicToRegion(x, y, z, 3);
                        if (post < 0) continue;
                        int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2);
                        map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                    }
                }
            }

            // BG -> Thalamus (inhibitory gating proxy)
            if (regionPool[1].Count > 0)
            {
                foreach (int pre in regionPool[3])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    for (int i = 0; i < _opt.BasalGangliaFanout; i++)
                    {
                        int post = PickTopographicToRegion(x, y, z, 1);
                        if (post < 0) continue;
                        int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2);
                        map.Add(pre, post, delay, SampleWeight(inhibitory: true));
                    }
                }
            }
        }

        // Cerebellum -> Motor (Region 27 -> 11) via deep nuclei -> VL thalamus -> motor cortex
        if (regionPool[RegionIds.Cerebellum].Count > 0 && regionPool[11].Count > 0)
        {
            foreach (int pre in regionPool[RegionIds.Cerebellum])
            {
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < _opt.CerebelloMotorFanout; i++)
                {
                    int post = PickTopographicToRegion(x, y, z, 11);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }
        }

        // --- CORTICOPONTOCEREBELLAR PATHWAY ---
        // Cortex --- Pons (corticopontine) + Pons --- Cerebellum (pontocerebellar mossy fibers)
        // This is the primary input pathway to the cerebellum.
        if (regionPool[8].Count > 0)
        {
            // Cortex -> Pons (many cortical areas project to pontine nuclei)
            byte[] pontineSourceRegions =
            {
                RegionIds.PrecentralGyrus,       // motor planning
                RegionIds.PostcentralGyrus,       // somatosensory
                RegionIds.SuperiorFrontalGyrus,   // frontal executive
                RegionIds.MiddleFrontalGyrus,     // prefrontal
                RegionIds.SuperiorParietal,        // spatial/proprioceptive
                RegionIds.InferiorOccipital,       // visual
            };
            foreach (byte cr in pontineSourceRegions)
            {
                var pres = regionPool[cr];
                if (pres.Count == 0) continue;
                foreach (int pre in pres)
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Pons);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }

            // Pons -> Cerebellum (mossy fiber relay)
            if (regionPool[RegionIds.Cerebellum].Count > 0)
            {
                foreach (int pre in regionPool[8])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    for (int i = 0; i < 2; i++)
                    {
                        int post = PickTopographicToRegion(x, y, z, RegionIds.Cerebellum);
                        if (post < 0) continue;
                        int delay = SampleDelay(Math.Max(1, _opt.MinDelayTicks), _opt.MaxDelayTicks + 1);
                        map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                    }
                }
            }
        }

        // --- BRAINSTEM CONNECTIVITY ---
        // Brainstem ascending arousal --- Thalamus (reticular activating system)
        // Brainstem --- Pons (adjacent nuclei, bidirectional)
        // Brainstem --- Cerebellum (inferior olive climbing fibers)
        if (regionPool[7].Count > 0)
        {
            // Brainstem -> Thalamus (ascending arousal)
            if (regionPool[1].Count > 0)
            {
                foreach (int pre in regionPool[7])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Thalamus);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }

            // Brainstem <-> Pons (bidirectional)
            if (regionPool[8].Count > 0)
            {
                foreach (int pre in regionPool[7])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Pons);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(_opt.MinDelayTicks, _opt.MaxDelayTicks), SampleWeight(false));
                }
                foreach (int pre in regionPool[8])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Brainstem);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(_opt.MinDelayTicks, _opt.MaxDelayTicks), SampleWeight(false));
                }
            }

            // Brainstem -> Cerebellum (inferior olive climbing fibers = error signal)
            if (regionPool[RegionIds.Cerebellum].Count > 0)
            {
                foreach (int pre in regionPool[7])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Cerebellum);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }
        }

        // --- HYPOTHALAMUS CONNECTIVITY ---
        // Amygdala --- Hypothalamus (emotional --- autonomic/endocrine)
        // Hypothalamus --- Brainstem (autonomic output)
        // Hypothalamus --- Pons (arousal modulation)
        // Hippocampus --- Hypothalamus (contextual regulation of stress/HPA axis)
        if (regionPool[2].Count > 0)
        {
            // Amygdala -> Hypothalamus
            if (regionPool[4].Count > 0)
            {
                foreach (int pre in regionPool[4])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Hypothalamus);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
                }
            }

            // Hippocampus -> Hypothalamus (contextual HPA regulation)
            if (regionPool[5].Count > 0)
            {
                foreach (int pre in regionPool[5])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    if (rng.NextDouble() > 0.3) continue; // sparse
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Hypothalamus);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3), SampleWeight(false));
                }
            }

            // Hypothalamus -> Brainstem (autonomic output)
            if (regionPool[7].Count > 0)
            {
                foreach (int pre in regionPool[2])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Brainstem);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
                }
            }

            // Hypothalamus -> Pons (arousal modulation)
            if (regionPool[8].Count > 0)
            {
                foreach (int pre in regionPool[2])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Pons);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
                }
            }
        }

        // --- AMYGDALA BIDIRECTIONAL ---
        // Cortex --- Amygdala (top-down regulation, threat evaluation input)
        // Hippocampus --- Amygdala (contextual fear conditioning)
        // Amygdala --- Brainstem (autonomic fear response)
        if (regionPool[4].Count > 0)
        {
            // Cortex -> Amygdala (prefrontal regulation + sensory association input)
            byte[] amInputRegions =
            {
                RegionIds.InferiorFrontalGyrus,   // PFC inhibition of fear (extinction)
                RegionIds.MiddleFrontalGyrus,      // executive regulation
                RegionIds.SuperiorTemporalGyrus,   // auditory threat cues
                RegionIds.InferiorOccipital,        // visual threat cues
                RegionIds.InferiorTemporalGyrus,    // object recognition -> threat assessment
            };
            foreach (byte cr in amInputRegions)
            {
                var pres = regionPool[cr];
                if (pres.Count == 0) continue;
                foreach (int pre in pres)
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    if (rng.NextDouble() > 0.25) continue; // sparse top-down
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Amygdala);
                    if (post < 0) continue;
                    int delay = SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3);
                    map.Add(pre, post, delay, SampleWeight(inhibitory: false));
                }
            }

            // Hippocampus -> Amygdala (contextual fear conditioning)
            if (regionPool[5].Count > 0)
            {
                foreach (int pre in regionPool[5])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    if (rng.NextDouble() > 0.3) continue; // sparse
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Amygdala);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3), SampleWeight(false));
                }
            }

            // Amygdala -> Brainstem (autonomic fear response via CeA)
            if (regionPool[7].Count > 0)
            {
                foreach (int pre in regionPool[4])
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Brainstem);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
                }
            }
        }

        // ============================================================
        // MISSING PATHWAYS --- added per wiring audit 2026-02-21
        // ============================================================

        // --- PRIORITY 1: MOTOR SYSTEM (broken without these) ---

        // P1.1 Corticospinal / Corticobulbar: M1 --- Brainstem
        //   THE primary voluntary motor pathway. Without this, cortical motor
        //   commands never synaptically reach brainstem motor nuclei.
        if (regionPool[11].Count > 0 && regionPool[7].Count > 0)
        {
            foreach (int pre in regionPool[11])
            {
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < 2; i++) // strong projection
                {
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Brainstem);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
                }
            }
        }

        // P1.2 Cerebellar deep nuclei --- Brainstem motor nuclei
        //   Fastigial --- vestibular nuclei, Interposed --- red nucleus.
        //   This is how cerebellar corrections reach the motor output directly.
        if (regionPool[RegionIds.Cerebellum].Count > 0 && regionPool[7].Count > 0)
        {
            foreach (int pre in regionPool[RegionIds.Cerebellum])
            {
                var (x, y, z) = vol.FromIndex(pre);
                int post = PickTopographicToRegion(x, y, z, RegionIds.Brainstem);
                if (post < 0) continue;
                map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
            }
        }

        // P1.3 BG --- Brainstem (pedunculopontine nucleus / locomotor drive)
        //   BG output reaches brainstem for movement initiation and gait.
        if (regionPool[3].Count > 0 && regionPool[7].Count > 0)
        {
            foreach (int pre in regionPool[3])
            {
                var (x, y, z) = vol.FromIndex(pre);
                if (rng.NextDouble() > 0.5) continue; // moderate density
                int post = PickTopographicToRegion(x, y, z, RegionIds.Brainstem);
                if (post < 0) continue;
                // Inhibitory: GPi/SNr --- PPN is GABAergic, gating locomotion
                map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(inhibitory: true));
            }
        }

        // --- PRIORITY 2: FEEDBACK LOOPS ---

        // P2.1 Amygdala --- Hippocampus (emotional modulation of memory encoding)
        //   BLA --- hippocampus enhances consolidation of emotionally salient events.
        if (regionPool[4].Count > 0 && regionPool[5].Count > 0)
        {
            foreach (int pre in regionPool[4])
            {
                var (x, y, z) = vol.FromIndex(pre);
                if (rng.NextDouble() > 0.3) continue; // sparse
                int post = PickTopographicToRegion(x, y, z, RegionIds.Hippocampus);
                if (post < 0) continue;
                map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3), SampleWeight(false));
            }
        }

        // P2.2 Premotor / PFC --- Brainstem (corticoreticular tract)
        //   Frontal cortex drives reticular formation for postural preparation.
        if (regionPool[7].Count > 0)
        {
            byte[] preFrontalSources = {
                RegionIds.SuperiorFrontalGyrus,
                RegionIds.MiddleFrontalGyrus,
                RegionIds.InferiorFrontalGyrus
            };
            foreach (byte cr in preFrontalSources)
            {
                var pres = regionPool[cr];
                if (pres.Count == 0) continue;
                foreach (int pre in pres)
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    if (rng.NextDouble() > 0.3) continue; // sparse
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Brainstem);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
                }
            }
        }

        // P2.3 Sensory cortex --- Brainstem (proprioceptive/auditory feedback)
        //   S1 --- vestibular nuclei (proprioceptive postural correction)
        //   STG --- superior colliculus (auditory orienting)
        if (regionPool[7].Count > 0)
        {
            byte[] sensorySources = {
                RegionIds.PostcentralGyrus,     // somatosensory -> vestibular
                RegionIds.SuperiorTemporalGyrus, // auditory -> superior colliculus
                RegionIds.InferiorOccipital      // visual -> superior colliculus
            };
            foreach (byte cr in sensorySources)
            {
                var pres = regionPool[cr];
                if (pres.Count == 0) continue;
                foreach (int pre in pres)
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    if (rng.NextDouble() > 0.25) continue; // sparse
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Brainstem);
                    if (post < 0) continue;
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3), SampleWeight(false));
                }
            }
        }

        // P2.4 Prefrontal cortex --- Hypothalamus (stress/HPA axis regulation)
        //   vmPFC inhibition of hypothalamic stress response.
        if (regionPool[2].Count > 0)
        {
            byte[] pfcSources = { RegionIds.MiddleFrontalGyrus, RegionIds.InferiorFrontalGyrus };
            foreach (byte cr in pfcSources)
            {
                var pres = regionPool[cr];
                if (pres.Count == 0) continue;
                foreach (int pre in pres)
                {
                    var (x, y, z) = vol.FromIndex(pre);
                    if (rng.NextDouble() > 0.2) continue; // very sparse
                    int post = PickTopographicToRegion(x, y, z, RegionIds.Hypothalamus);
                    if (post < 0) continue;
                    // Inhibitory: PFC downregulates HPA stress axis
                    map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3), SampleWeight(inhibitory: true));
                }
            }
        }

        // P2.5 Brainstem --- Hypothalamus (visceral interoceptive feedback)
        //   NTS/parabrachial --- hypothalamus for autonomic state monitoring.
        if (regionPool[7].Count > 0 && regionPool[2].Count > 0)
        {
            foreach (int pre in regionPool[7])
            {
                var (x, y, z) = vol.FromIndex(pre);
                if (rng.NextDouble() > 0.3) continue; // sparse
                int post = PickTopographicToRegion(x, y, z, RegionIds.Hypothalamus);
                if (post < 0) continue;
                map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
            }
        }

        // --- PRIORITY 3: LOOP COMPLETIONS ---

        // P3.1 Thalamus --- BG (thalamic feedback to striatum)
        //   CM/Pf intralaminar nuclei --- striatum. Provides excitatory drive
        //   that helps maintain BG activity and close the thalamo-striatal loop.
        if (regionPool[1].Count > 0 && regionPool[3].Count > 0)
        {
            foreach (int pre in regionPool[1])
            {
                var (x, y, z) = vol.FromIndex(pre);
                if (rng.NextDouble() > 0.3) continue; // sparse
                int post = PickTopographicToRegion(x, y, z, RegionIds.BasalGanglia);
                if (post < 0) continue;
                map.Add(pre, post, SampleDelay(Math.Max(2, _opt.MinDelayTicks), _opt.MaxDelayTicks + 2), SampleWeight(false));
            }
        }

        // P3.2 Brainstem --- Cortex (ascending reticular projections beyond thalamus)
        //   Direct brainstem --- cortex projections from LC, raphe, VTA.
        //   Mostly modulatory but represented as sparse excitatory synapses.
        if (regionPool[7].Count > 0)
        {
            byte[] ascendingTargets = {
                RegionIds.PrecentralGyrus,
                RegionIds.SuperiorFrontalGyrus,
                RegionIds.MiddleFrontalGyrus
            };
            foreach (int pre in regionPool[7])
            {
                var (x, y, z) = vol.FromIndex(pre);
                if (rng.NextDouble() > 0.2) continue; // very sparse
                byte tr = ascendingTargets[rng.Next(0, ascendingTargets.Length)];
                int post = PickTopographicToRegion(x, y, z, tr);
                if (post < 0) continue;
                map.Add(pre, post, SampleDelay(Math.Max(3, _opt.MinDelayTicks), _opt.MaxDelayTicks + 3), SampleWeight(false));
            }
        }
    }

    private void BuildCorpusCallosum()
    {
    // Corpus callosum is a midline white-matter band that relays cortical activity between hemispheres.
    // Implementation: cortex -> local callosum band (SynLeft/SynRight), then callosum band -> contralateral cortex (CallosumLR/RL).
    // NOTE (Paul): There is *no user-facing* display budget/toggle for the CC. The tract is always present (subject only to global edge caps).

    CallosumLR.Clear();
    CallosumRL.Clear();

    int n = _opt.W * _opt.H * _opt.D;

    int fanBase = Math.Max(0, _opt.CallosalFanoutPerNeuron);
    if (fanBase == 0) return;

    // Collect callosum-band voxels (region 14) in each hemi
    var leftCc = new List<int>(2048);
    var rightCc = new List<int>(2048);

    // Build per-region cortical index lists for homotopic selection.
    var leftByRegion = new List<int>[256];
    var rightByRegion = new List<int>[256];

    for (int idx = 0; idx < n; idx++)
    {
        var (lx, ly, lz) = Left.FromIndex(idx);
        if (Left.Active[Left.IndexOf(lx, ly, lz)])
        {
            byte lr = Left.RegionId[Left.IndexOf(lx, ly, lz)];
            if (lr == RegionIds.CorpusCallosum) leftCc.Add(idx);
            else if (RegionIds.IsCortex(lr))
            {
                leftByRegion[lr] ??= new List<int>(256);
                leftByRegion[lr].Add(idx);
            }
        }

        var (rx, ry, rz) = Right.FromIndex(idx);
        if (Right.Active[Right.IndexOf(rx, ry, rz)])
        {
            byte rr = Right.RegionId[Right.IndexOf(rx, ry, rz)];
            if (rr == RegionIds.CorpusCallosum) rightCc.Add(idx);
            else if (RegionIds.IsCortex(rr))
            {
                rightByRegion[rr] ??= new List<int>(256);
                rightByRegion[rr].Add(idx);
            }
        }
    }

    if (leftCc.Count == 0 || rightCc.Count == 0) return;

    float FanoutMul(byte r) => r switch
    {
        // primary belts
        RegionIds.PrecentralGyrus => _opt.CallosalFanoutMulMotor,
        RegionIds.PostcentralGyrus => _opt.CallosalFanoutMulSomatic,

        // occipital (visual)
        RegionIds.SuperiorOccipital => _opt.CallosalFanoutMulVisual,
        RegionIds.InferiorOccipital => _opt.CallosalFanoutMulVisual,

        // temporal (auditory + ventral stream)
        RegionIds.SuperiorTemporalGyrus => _opt.CallosalFanoutMulAuditory,
        RegionIds.MiddleTemporalGyrus => _opt.CallosalFanoutMulAuditory,
        RegionIds.InferiorTemporalGyrus => _opt.CallosalFanoutMulAuditory,

        // frontal + parietal association (use PFC multiplier as the "association" knob)
        RegionIds.SuperiorFrontalGyrus => _opt.CallosalFanoutMulPfc,
        RegionIds.MiddleFrontalGyrus => _opt.CallosalFanoutMulPfc,
        RegionIds.InferiorFrontalGyrus => _opt.CallosalFanoutMulPfc,
        RegionIds.SuperiorParietal => _opt.CallosalFanoutMulPfc,
        RegionIds.InferiorParietal => _opt.CallosalFanoutMulPfc,
        RegionIds.SupramarginalGyrus => _opt.CallosalFanoutMulPfc,
        RegionIds.AngularGyrus => _opt.CallosalFanoutMulPfc,

        _ => 0.0f
    };

    int jitter = Math.Max(0, _opt.CallosalTopographicJitterVoxels);
    int maxAttempts = Math.Max(1, _opt.CallosalMaxPlacementAttempts);

	int FindNodeAtVoxel(NeuralVolume v, List<int> _pool, int x, int y, int z)
	{
		// We treat any active CC voxel as a valid callosal "neuron".
		// (The simulation is voxel-based; IndexOf is always defined.)
		if ((uint)x >= (uint)v.W || (uint)y >= (uint)v.H || (uint)z >= (uint)v.D) return -1;
		if (!v.Active[v.IndexOf(x, y, z)]) return -1;
		if (v.RegionId[v.IndexOf(x, y, z)] != RegionIds.CorpusCallosum) return -1;
		return v.IndexOf(x, y, z);
	}

    int PickHomotopic(NeuralVolume dst, List<int> dstPool, int x, int y, int z, byte region)
    {
        for (int a = 0; a < maxAttempts; a++)
        {
            int dx = jitter == 0 ? 0 : _rng.Next(-jitter, jitter + 1);
            int dy = jitter == 0 ? 0 : _rng.Next(-jitter, jitter + 1);
            int dz = jitter == 0 ? 0 : _rng.Next(-jitter, jitter + 1);

            int xx = Math.Clamp(x + dx, 0, _opt.W - 1);
            int yy = Math.Clamp(y + dy, 0, _opt.H - 1);
            int zz = Math.Clamp(z + dz, 0, _opt.D - 1);

            if (!dst.Active[dst.IndexOf(xx, yy, zz)]) continue;
            if (dst.RegionId[dst.IndexOf(xx, yy, zz)] != region) continue;

            return dst.IndexOf(xx, yy, zz);
        }

        if (dstPool.Count == 0) return -1;
        return dstPool[_rng.Next(0, dstPool.Count)];
    }

    int PickCallosumNode(NeuralVolume vol, List<int> pool, int y, int z)
    {
        // Map cortical positions to the CC midline arch rather than using cortex y directly.
        // This prevents the "huge horizontal sheet" of callosal lines and keeps tracts anchored
        // to the real corpus callosum structure.

        int w = vol.W, h = vol.H, d = vol.D;
        int xMid = w / 2;

        // Match the carved CC: thicker and slightly longer so points don't collapse.
        int ccHalfWidth = 5;

        int zMin = (int)(d * 0.14f);
        int zMax = (int)(d * 0.86f);
        if (zMax <= zMin) { zMin = 0; zMax = d - 1; }

        int zz = Math.Clamp(z, zMin, zMax);
        // Add Z jitter (+/- 1) to spread connections across nearby CC voxels
        // rather than always hitting the exact same one for a given cortex Z.
        zz = Math.Clamp(zz + _rng.Next(-1, 2), zMin, zMax);

        // More superior (smaller y) than before so it sits between hemispheres,
        // not down in the brainstem region.
        int yEnd = (int)(h * 0.26f);
        int yMid = (int)(h * 0.14f);
        int yHalf = Math.Max(2, (int)(h * 0.07f));

        int zMid = (zMin + zMax) / 2;
        float zSpan = Math.Max(1.0f, (zMax - zMin) * 0.5f);

        float t = (zz - zMid) / zSpan;
        t = Math.Clamp(t, -1f, 1f);
        int yCenter = (int)MathF.Round(yMid + (yEnd - yMid) * (t * t));

        for (int a = 0; a < maxAttempts; a++)
        {
            int yy = yCenter + (jitter == 0 ? 0 : _rng.Next(-yHalf, yHalf + 1));
            yy = Math.Clamp(yy, 0, h - 1);

            // Try to hit an actual CC voxel. Randomize X start to spread selections.
            int dxStart = _rng.Next(-ccHalfWidth, ccHalfWidth + 1);
            for (int di = 0; di <= ccHalfWidth * 2; di++)
            {
                int dx = ((dxStart + di + ccHalfWidth * 3) % (ccHalfWidth * 2 + 1)) - ccHalfWidth;
                int xx = xMid + dx;
                if ((uint)xx >= (uint)w) continue;

                int id = FindNodeAtVoxel(vol, pool, xx, yy, zz);
                if (id >= 0) return id;
            }
        }

        // Fallback: pick any CC node in the pool.
        return pool[_rng.Next(0, pool.Count)];
    }

    int delayLocalMin = 1;
    int delayLocalMax = 3;

    // Left cortex -> Left callosum band; callosum band -> Right cortex
    foreach (byte r in RegionIds.AllCorticalRegions)
    {
        var pres = leftByRegion[r];
        var posts = rightByRegion[r];
        if (pres is null || posts is null || pres.Count == 0 || posts.Count == 0) continue;

        int fan = Math.Max(1, (int)MathF.Round(fanBase * FanoutMul(r)));

        for (int p = 0; p < pres.Count; p++)
        {
            int pre = pres[p];
            var (x, y, z) = Left.FromIndex(pre);

            int cc = PickCallosumNode(Left, leftCc, y, z);

            int dLocal = _rng.Next(delayLocalMin, delayLocalMax + 1);
            // Strengthen cortex->CC relay so callosal fibers actually spike in the visible stream.
            float wLocal = (_opt.CallosalWeightMean * 2.40f) + ((float)_rng.NextDouble() - 0.5f) * 2f * (_opt.CallosalWeightJitter * 0.80f);
            SynLeft.Add(pre, cc, dLocal, wLocal);

            for (int i = 0; i < fan; i++)
            {
                int post = PickHomotopic(Right, posts, x, y, z, r);
                if (post < 0) continue;

                int delay = _rng.Next(_opt.CallosalMinDelayTicks, _opt.CallosalMaxDelayTicks + 1);
                float w = _opt.CallosalWeightMean + ((float)_rng.NextDouble() - 0.5f) * 2f * _opt.CallosalWeightJitter;

                // callosum band -> contralateral cortex
                CallosumLR.Add(cc, post, delay, w);
            }
        }
    }

    // Right cortex -> Right callosum band; callosum band -> Left cortex
    foreach (byte r in RegionIds.AllCorticalRegions)
    {
        var pres = rightByRegion[r];
        var posts = leftByRegion[r];
        if (pres is null || posts is null || pres.Count == 0 || posts.Count == 0) continue;

        int fan = Math.Max(1, (int)MathF.Round(fanBase * FanoutMul(r)));

        for (int p = 0; p < pres.Count; p++)
        {
            int pre = pres[p];
            var (x, y, z) = Right.FromIndex(pre);

            int cc = PickCallosumNode(Right, rightCc, y, z);

            int dLocal = _rng.Next(delayLocalMin, delayLocalMax + 1);
            float wLocal = (_opt.CallosalWeightMean * 2.40f) + ((float)_rng.NextDouble() - 0.5f) * 2f * (_opt.CallosalWeightJitter * 0.80f);
            SynRight.Add(pre, cc, dLocal, wLocal);

            for (int i = 0; i < fan; i++)
            {
                int post = PickHomotopic(Left, posts, x, y, z, r);
                if (post < 0) continue;

                int delay = _rng.Next(_opt.CallosalMinDelayTicks, _opt.CallosalMaxDelayTicks + 1);
                float w = _opt.CallosalWeightMean + ((float)_rng.NextDouble() - 0.5f) * 2f * _opt.CallosalWeightJitter;

                CallosumRL.Add(cc, post, delay, w);
            }
        }
    }
}


    private PackedPoints PackSpikes(List<(byte hemi, int idx)> spikes, int maxSpikes)
    {
        // PERF: idx is already the linear index into the volume arrays.
        int total = spikes.Count;
        if (total <= 0) return new PackedPoints(0, Array.Empty<byte>());

        int cap = maxSpikes <= 0 ? 3500 : maxSpikes;
        int count = total <= cap ? total : cap;
        var data = new byte[count * 6];
        int o = 0;

        void WriteSpikeAtSource(int sourceIndex)
        {
            var (hemi, idx) = spikes[sourceIndex];
            var vol = hemi == 1 ? Right : Left;
            var (x, y, z) = vol.FromIndex(idx);

            data[o++] = hemi;
            data[o++] = (byte)x;
            data[o++] = (byte)y;
            data[o++] = (byte)z;

            float e = vol.Energy[idx];
            int eb = (int)(e * 255f);
            if (eb < 0) eb = 0; else if (eb > 255) eb = 255;
            data[o++] = (byte)eb;
            data[o++] = vol.RegionId[idx];
        }

        if (total <= count)
        {
            for (int i = 0; i < count; i++)
                WriteSpikeAtSource(i);
            return new PackedPoints(count, data);
        }

        // Preserve sparse-but-important regions so they do not disappear when
        // the spike stream is downsampled.
        int availCc = 0, availCereb = 0;
        for (int i = 0; i < total; i++)
        {
            var (hemi, idx) = spikes[i];
            var vol = hemi == 1 ? Right : Left;
            byte region = vol.RegionId[idx];
            if (region == RegionIds.CorpusCallosum) availCc++;
            else if (region == RegionIds.Cerebellum) availCereb++;
        }

        // Keep sparse deep structures visible under downsample pressure.
        // Larger reservation avoids "disappearing spikes" in dense cortical scenes.
        int targetCc = Math.Min(availCc, Math.Clamp(count / 30, 20, 180));
        int targetCereb = Math.Min(availCereb, Math.Clamp(count / 24, 28, 240));

        byte[] selected = System.Buffers.ArrayPool<byte>.Shared.Rent(total);
        Array.Clear(selected, 0, total);

        int emitted = 0;
        try
        {
            void SelectPriority(byte regionId, int available, int target)
            {
                if (available <= 0 || target <= 0) return;

                double step = (double)available / target;
                double nextPick = 0.0;
                int seen = 0;
                int picked = 0;

                for (int i = 0; i < total; i++)
                {
                    if (selected[i] != 0) continue;

                    var (hemi, idx) = spikes[i];
                    var vol = hemi == 1 ? Right : Left;
                    if (vol.RegionId[idx] != regionId) continue;

                    if ((seen + 0.5) >= nextPick)
                    {
                        selected[i] = 1;
                        picked++;
                        nextPick += step;
                        if (picked >= target) break;
                    }
                    seen++;
                }
            }

            SelectPriority(RegionIds.CorpusCallosum, availCc, targetCc);
            SelectPriority(RegionIds.Cerebellum, availCereb, targetCereb);

            // Pass 1: emit selected priority spikes (in source order).
            for (int i = 0; i < total && emitted < count; i++)
            {
                if (selected[i] == 0) continue;
                WriteSpikeAtSource(i);
                emitted++;
            }

            // Pass 2: deterministic stride across remaining spikes.
            int remainingNeed = count - emitted;
            int remainingAvail = total - emitted;
            if (remainingNeed > 0 && remainingAvail > 0)
            {
                int stride = Math.Max(1, remainingAvail / remainingNeed);
                int seen = 0;

                for (int i = 0; i < total && emitted < count; i++)
                {
                    if (selected[i] != 0) continue;
                    if ((seen++ % stride) != 0) continue;
                    WriteSpikeAtSource(i);
                    emitted++;
                }
            }

            // Pass 3: backfill if stride undershot due integer rounding.
            if (emitted < count)
            {
                for (int i = 0; i < total && emitted < count; i++)
                {
                    if (selected[i] != 0) continue;
                    WriteSpikeAtSource(i);
                    emitted++;
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(selected, clearArray: true);
        }

        return new PackedPoints(emitted, emitted == count ? data : data.AsSpan(0, emitted * 6).ToArray());
    }

    // === Voice motor helpers ===

    private VocalMotor.VoiceDrive MakeVoiceDrive(float urgency01)
        => new(
            Arousal01: GetArousal01(),
            Urgency01: Math.Clamp(urgency01, 0f, 1f),
            Valence11: GetValence11(),
            Certainty01: GetCertainty01());

    private float GetArousal01() => Math.Clamp(Pons.Arousal01, 0f, 1f);

    private float GetCertainty01() => Math.Clamp(1f - _lastPredictionError01, 0f, 1f);

    private float GetValence11() => Math.Clamp(_lastValence11, -1f, 1f);

    private byte GetMostActiveRegion()
    {
        // Find the region with the most spikes this step
        if (_spikesWithRegion.Count == 0) return 0;
        Span<int> counts = stackalloc int[32];
        foreach (var (_, _, region) in _spikesWithRegion)
        {
            if (region < 32) counts[region]++;
        }
        byte best = 0;
        int bestCount = 0;
        for (byte r = 0; r < 32; r++)
        {
            if (counts[r] > bestCount) { bestCount = counts[r]; best = r; }
        }
        return best;
    }

    /// <summary>Send an utterance through the vocal tract and to connected peers.</summary>
    public void SendSpeechToPeers(string text, float rate = 1.0f, float volume = 0.8f, IReadOnlyList<string>? phonemesOverride = null)
    {
        if (Peer.GetPeers().Count == 0) return;
        
        string[] phonemeArray;
        if (phonemesOverride is { Count: > 0 })
        {
            phonemeArray = phonemesOverride.ToArray();
        }
        else
        {
            var phonemes = new List<string>();
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                phonemes.AddRange(DiphthongVocalTract.GraphemeToPhoneme(word));
                phonemes.Add("_");
            }

            phonemeArray = phonemes.ToArray();
        }
        
        // Generate formant trajectory
        var tractCopy = new DiphthongVocalTract();
        tractCopy.BasePitchHz = VocalTract.BasePitchHz;
        tractCopy.EnqueuePhonemes(phonemeArray, rate);
        
        var formants = new List<DiphthongVocalTract.FormantState>();
        float dt = 1f / 60f;
        while (tractCopy.IsSpeaking)
        {
            formants.Add(tractCopy.Step(dt));
        }
        
        var segment = new PeerBridge.SpeechSegment(
            SourceId: Peer.InstanceId,
            StepIndex: StepIndex,
            Phonemes: phonemeArray,
            Rate: rate,
            Pitch: VocalTract.BasePitchHz,
            Volume: volume,
            Text: text,
            Formants: formants.ToArray());
        
        Peer.SendSpeech(segment);
    }
}



