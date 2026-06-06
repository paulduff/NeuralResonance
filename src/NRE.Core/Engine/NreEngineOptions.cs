namespace NRE.Core.Engine;

public sealed class NreEngineOptions
{
    // Canon baseline lattice size requested for full-circuit visualization.
    public int W { get; init; } = 32;
    public int H { get; init; } = 32;
    public int D { get; init; } = 32;

    // === Anatomy positioning nudges (voxels) ===
    // Positive Z = more posterior (toward occipital/cerebellar pole).
    // Negative Z = more anterior (toward frontal pole).
    // These are small "+/-" trims to fine-tune where structures sit along the anterior/posterior axis.
    public float AnatomyZOffsetThalamusVoxels { get; init; } = 0f;
    public float AnatomyZOffsetHypothalamusVoxels { get; init; } = 0f;
    public float AnatomyZOffsetBasalGangliaVoxels { get; init; } = 0f;
    public float AnatomyZOffsetAmygdalaVoxels { get; init; } = 0f;
    public float AnatomyZOffsetHippocampusVoxels { get; init; } = 0f;
    public float AnatomyZOffsetCerebellumVoxels { get; init; } = 0f;
    public float AnatomyZOffsetBrainstemVoxels { get; init; } = 0f;
    public float AnatomyZOffsetPonsVoxels { get; init; } = 0f;

    // LIF-ish dynamics
    public float Leak { get; init; } = 0.85f;
    public float Rest { get; init; } = 0.0f;
    public float InputGain { get; init; } = 0.18f;

    // Threshold dynamics
    public float BaseThreshold { get; init; } = 1.4f;
    public float ThresholdRecover { get; init; } = 0.0008f;
    public float ThresholdSpikeBump { get; init; } = 0.08f;

    // Energy (ATP)
    //
    // Sized so that under normal activity the brain stays awake for a full
    // world-sim day half (~120 s) and recovers to fully rested during a sleep
    // cycle. EnergyMax is the per-voxel ceiling; ComputeGlobalAtpSampled clamps
    // the mean to [0,1] before handing it to the SleepController, so a higher
    // ceiling just keeps the mean pegged near 1.0 longer (more buffer before
    // SleepTriggerAtp = 0.40 is crossed).
    //
    // Wake recovery rate is the per-voxel baseline. NREM/REM multiplies it so
    // sleep is metabolically restorative — NREM dominates ATP refill, REM less so.
    public float EnergyMax { get; init; } = 4.0f;
    public float EnergyRecoverPerSec { get; init; } = 0.40f;
    public float EnergySpikeCost { get; init; } = 0.35f;
    public float EnergyMinToFire { get; init; } = 0.10f;

    // Sleep-phase ATP recovery multipliers (applied on top of EnergyRecoverPerSec
    // inside StepHemisphere). Awake = 1.0 (use base rate). NREM is the primary
    // recovery phase. REM is moderately restorative — slower than NREM because
    // REM is metabolically active (dreaming, replay).
    public float EnergyRecoveryNremMultiplier { get; init; } = 4.0f;
    public float EnergyRecoveryRemMultiplier { get; init; } = 2.5f;

    // Axonal delay + synapses
    public int MinDelayTicks { get; init; } = 1;
    public int MaxDelayTicks { get; init; } = 4;
    public float InitialWeightMean { get; init; } = 0.12f;
    public float InitialWeightJitter { get; init; } = 0.08f;

    // === Biological connection scaffolding ===
    // Initial wiring should be dominated by local, distance-biased microcircuitry, with sparse
    // structured long-range tracts.

    /// <summary>Local microcircuit fanout per active neuron (same-region preferred).</summary>
    public int LocalFanoutPerNeuron { get; init; } = 4;

    /// <summary>Voxel radius for local target selection.</summary>
    public int LocalRadiusVoxels { get; init; } = 3;

    /// <summary>Fraction of local synapses that are inhibitory (approx interneuron effect).</summary>
    public float LocalInhibitoryFraction { get; init; } = 0.18f;

    /// <summary>Additional sparse cortico-cortical long-range fanout per cortical neuron.</summary>
    public int CorticoCorticalLongRangeFanout { get; init; } = 2;

    /// <summary>Radius (in voxels) considered "near" for cortex-to-cortex targets; beyond this is long-range.</summary>
    public int CorticoCorticalNearRadiusVoxels { get; init; } = 6;

    /// <summary>Thalamus -> cortex fanout per thalamic neuron (cortex-only, topographic).</summary>
    public int ThalamoCorticalFanout { get; init; } = 3;

    /// <summary>Cortex -> thalamus fanout per cortical neuron (feedback).</summary>
    public int CorticoThalamicFanout { get; init; } = 1;

    /// <summary>Hippocampus <-> cortex fanout per hippocampal neuron (to PFC/association).</summary>
    public int HippocampoCorticalFanout { get; init; } = 2;

    /// <summary>Amygdala -> cortex fanout per amygdala neuron (diffuse bias).</summary>
    public int AmygdaloCorticalFanout { get; init; } = 2;

    /// <summary>Basal ganglia loop fanout (cortex<->BG<->thalamus simplified).</summary>
    public int BasalGangliaFanout { get; init; } = 2;

    /// <summary>Cerebellum -> motor fanout per cerebellar neuron (timing bias).</summary>
    public int CerebelloMotorFanout { get; init; } = 2;

    /// <summary>Placement jitter for topographic projection targets (voxels).</summary>
    public int ProjectionTopographicJitterVoxels { get; init; } = 2;

    /// <summary>Max attempts to place a projection onto an active voxel of the desired region.</summary>
    public int ProjectionMaxPlacementAttempts { get; init; } = 10;

    // Subcortical local wiring budget
    public int SubcorticalLocalFanoutPerNeuron { get; init; } = 3;

    // STP
    public float FacilitationGain { get; init; } = 0.05f;
    public float DepressionGain { get; init; } = 0.06f;
    public float StpRecoverPerSec { get; init; } = 0.35f;

    // Hebbian-lite
    public float HebbRate { get; init; } = 0.0012f;
    public float WeightMin { get; init; } = -0.25f;
    public float WeightMax { get; init; } = 0.55f;

    // Pre-/post-synaptic plasticity
    public bool EnablePrePostSynapticPlasticity { get; init; } = true;

    /// <summary>Presynaptic release potentiation per pre-spike.</summary>
    public float PreSynapticPotentiationRate { get; init; } = 0.0015f;

    /// <summary>Presynaptic release depression when transmission is less effective.</summary>
    public float PreSynapticDepressionRate { get; init; } = 0.0012f;

    /// <summary>Per-second recovery of presynaptic release efficacy back to baseline.</summary>
    public float PreSynapticRecoverPerSec { get; init; } = 0.10f;

    /// <summary>Clamp bounds for presynaptic release efficacy.</summary>
    public float PreSynapticMin { get; init; } = 0.40f;
    public float PreSynapticMax { get; init; } = 1.80f;

    /// <summary>Postsynaptic sensitivity potentiation per successful pre/post co-activation.</summary>
    public float PostSynapticPotentiationRate { get; init; } = 0.0012f;

    /// <summary>Postsynaptic homeostatic pull toward target sensitivity.</summary>
    public float PostSynapticHomeostasisRate { get; init; } = 0.0010f;

    /// <summary>Target postsynaptic receptor/sensitivity baseline.</summary>
    public float PostSynapticTargetSensitivity { get; init; } = 1.0f;

    /// <summary>Per-second recovery of postsynaptic sensitivity toward the target.</summary>
    public float PostSynapticRecoverPerSec { get; init; } = 0.08f;

    /// <summary>Clamp bounds for postsynaptic sensitivity.</summary>
    public float PostSynapticMin { get; init; } = 0.40f;
    public float PostSynapticMax { get; init; } = 1.80f;

    // Biologically-inspired pair-based STDP (pre/post spike traces)
    public bool EnableBiologicalStdpPlasticity { get; init; } = true;
    public float StdpTauPreSec { get; init; } = 0.020f;
    public float StdpTauPostSec { get; init; } = 0.020f;
    public float StdpTraceIncrement { get; init; } = 1.0f;
    public float StdpTraceMax { get; init; } = 4.0f;
    public float StdpLtpRate { get; init; } = 0.0015f;
    public float StdpLtdRate { get; init; } = 0.0017f;
    public float StdpInhibitoryScale { get; init; } = 0.35f;

    // Neuromodulator gating for plasticity (DA/ACh/NE promote; 5-HT restrains)
    public float StdpNeuromodGateStrength { get; init; } = 1.0f;
    public float StdpSleepGateMultiplier { get; init; } = 0.25f;


// === Developmental structural plasticity (Entry 021: pruning + competition) ===
/// <summary>Usage EMA boost per spike event on a synapse (0..1).</summary>
public float StructuralUsageBoostOnSpike { get; init; } = 0.06f;

/// <summary>Per-second decay of synapse UsageEma01 toward 0.</summary>
public float StructuralUsageDecayPerSec { get; init; } = 0.15f;

/// <summary>Per-second weight decay toward 0 for unused synapses (scaled by 1-usage).</summary>
public float StructuralWeightDecayPerSec { get; init; } = 0.06f;

/// <summary>Prune synapses whose |W| is below this threshold AND usage is low.</summary>
public float StructuralPruneAbsWThreshold { get; init; } = 0.012f;

/// <summary>Prune synapses whose UsageEma01 is below this threshold AND |W| is weak.</summary>
public float StructuralPruneUsageThreshold01 { get; init; } = 0.05f;

/// <summary>Outgoing absolute weight budget per cortical neuron (competition).</summary>
public float StructuralOutgoingAbsBudgetCortex { get; init; } = 1.10f;

/// <summary>Outgoing absolute weight budget per subcortical neuron (competition).</summary>
public float StructuralOutgoingAbsBudgetSubcortex { get; init; } = 0.80f;

/// <summary>Outgoing absolute weight budget per callosal projection neuron (competition).</summary>
public float StructuralOutgoingAbsBudgetCallosal { get; init; } = 0.55f;

	/// <summary>
	/// Entry 021 hardening: minimum absolute weight preserved for protected (anatomically essential) tracts.
	/// These tracts may compete (scale) but should not disappear.
	/// </summary>
	public float StructuralProtectedMinAbsW { get; init; } = 0.025f;

	/// <summary>
	/// Entry 021 hardening: minimum usage floor for protected tracts to prevent aggressive pruning under low stimulation.
	/// </summary>
	public float StructuralProtectedUsageFloor01 { get; init; } = 0.20f;


    // Lateral inhibition
    public int InhibRadius { get; init; } = 1;
    public float InhibStrength { get; init; } = 0.15f;

    // Intrinsic oscillation (overridden by Pons theta packet)
    public float OscGain { get; init; } = 0.02f;

    // === Intrinsic Drive Field (IDF) safety floors (Folded Archive Entry 018) ===
    // Prevents biologically-impossible "dead cortex" states during Wake and Sleep.
    public float AwakeArousalMin01 { get; init; } = 0.10f;
    public float NremArousalMin01  { get; init; } = 0.03f;
    public float RemArousalMin01   { get; init; } = 0.05f;
    public float ThetaMinHz { get; init; } = 0.8f;


        // === Multi-timescale resonance stratification (Folded Archive Entry 019) ===
    // Fast path runs every Step(dt).
    // Intermediate path runs at IntermediateUpdateHz (e.g. salience, replay gating).
    // Slow path runs at SlowUpdateHz (e.g. pons packet emission, neuromodulator decay, homeostasis).
    public float IntermediateUpdateHz { get; init; } = 20.0f;
    public float SlowUpdateHz { get; init; } = 1.0f;

// Pons modulation mapping strengths
    public float PonsArousalThresholdLowering { get; init; } = 0.10f;
    public float PonsStabilityThresholdRaising { get; init; } = 0.08f;
    public float PonsResetLeakBoost { get; init; } = 0.06f;
    public float PonsArousalInputGainBoost { get; init; } = 0.35f;
    public float PonsStabilityInhibBoost { get; init; } = 0.60f;

    // Corpus callosum
    public int CallosalFanoutPerNeuron { get; init; } = 2;
    public int CallosalMinDelayTicks { get; init; } = 2;
    public int CallosalMaxDelayTicks { get; init; } = 6;
    public float CallosalWeightMean { get; init; } = 0.08f;
    public float CallosalWeightJitter { get; init; } = 0.05f;

    // Callosal biological constraints (Entry 020 CC patch)
    // Topographic mapping uses mirrored (x,y,z) with small jitter (in voxels).
    public int CallosalTopographicJitterVoxels { get; init; } = 2;
    public int CallosalMaxPlacementAttempts { get; init; } = 8;
    // Region-weighted fanout multipliers (homotopic cortex-only)
    public float CallosalFanoutMulVisual { get; init; } = 0.6f;   // RegionId=9
    public float CallosalFanoutMulAuditory { get; init; } = 0.8f; // RegionId=10
    public float CallosalFanoutMulMotor { get; init; } = 1.0f;    // RegionId=11
    public float CallosalFanoutMulSomatic { get; init; } = 1.0f;  // RegionId=12
    public float CallosalFanoutMulPfc { get; init; } = 1.4f;      // RegionId=13

    // Thalamic afferent floor (OPTIONAL)
    public bool EnableAfferentFloor { get; init; } = true;
    public int AfferentEventsPerStep { get; init; } = 6;
    public float AfferentIntensity { get; init; } = 0.08f;
    public int AfferentDelayMinTicks { get; init; } = 0;
    public int AfferentDelayMaxTicks { get; init; } = 2;

    // === HEMISPHERE SPECIALIZATION ===
    // Hemispheric asymmetry (parameterised). Avoid semantic labels; emergent behaviour only.

    public bool EnableHemisphereSpecialization { get; init; } = false;
    
    // HEMI-0 - tighter connectivity, higher inhibition, lower spontaneous noise
    public float LeftInhibStrengthMod { get; init; } = 1.25f;      // 25% stronger inhibition
    public float LeftThresholdMod { get; init; } = 1.05f;          // Slightly higher threshold (more selective)
    public float LeftLocalConnectivityMod { get; init; } = 1.2f;   // More local connections
    public float LeftSpontaneousNoiseMod { get; init; } = 0.7f;    // Less spontaneous noise
    public float LeftHebbRateMod { get; init; } = 1.1f;            // Faster learning (sequential)
    
    // HEMI-1 - more diffuse connectivity, lower inhibition, higher spontaneous noise
    public float RightInhibStrengthMod { get; init; } = 0.8f;      // 20% weaker inhibition (more flow)
    public float RightThresholdMod { get; init; } = 0.95f;         // Lower threshold (more excitable)
    public float RightLocalConnectivityMod { get; init; } = 0.85f; // Fewer local, more diffuse
    public float RightLongRangeConnectivityMod { get; init; } = 1.4f; // More long-range connections
    public float RightSpontaneousNoiseMod { get; init; } = 1.3f;   // More spontaneous activity
    public float RightHebbRateMod { get; init; } = 0.9f;           // Slower learning (holistic)
    
    // Region emphasis per hemisphere (which regions are "dominant")
    // Left-dominant: PFC(13), Motor(11), Somatosensory(12), Basal Ganglia(3)
    // Right-dominant: Visual(9), Auditory(10), Hippocampus(5), Amygdala(4)
    public float LeftDominantRegionBoost { get; init; } = 1.15f;
    public float RightDominantRegionBoost { get; init; } = 1.15f;

// Pons homeostasis (criticality / SOC bias)
public bool EnablePonsHomeostasis { get; init; } = true;

/// <summary>Target spike density per step, roughly in [0..1].</summary>
public float TargetSpikeDensity01 { get; init; } = 0.015f;

/// <summary>Allowed band around target where Pons does not adapt.</summary>
public float SpikeDensityDeadband01 { get; init; } = 0.004f;

/// <summary>How fast Pons adapts its tones (smaller = slower).</summary>
public float PonsHomeostasisRate { get; init; } = 0.015f;

/// <summary>EMA smoothing for measured spike density.</summary>
public float SpikeDensityEmaAlpha { get; init; } = 0.08f;

    
    // === Active perception (Folded Archive Entry 020) ===
    /// <summary>Enable active perception: motor-driven sensor framing (gaze/focus) biases sensory injection.</summary>
    public bool EnableActivePerception { get; init; } = true;

    /// <summary>Smoothing factor (0..1) for gaze/focus updates. Higher = smoother (slower).</summary>
    public float SensorFrameSmoothingAlpha { get; init; } = 0.15f;

    /// <summary>Minimum visual sampling sigma (as fraction of width/height) when strongly focused.</summary>
    public float VisualSigmaMin01 { get; init; } = 0.06f;

    /// <summary>Maximum visual sampling sigma (as fraction of width/height) when exploring.</summary>
    public float VisualSigmaMax01 { get; init; } = 0.28f;

    /// <summary>How strongly IDF pressure widens the sampling window (0..1).</summary>
    public float VisualExploreFromPressure { get; init; } = 0.65f;

    /// <summary>How much motor activity can shift auditory focus from tonotopic center (0..1).</summary>
    public float AuditoryFocusShiftMax01 { get; init; } = 0.25f;


// Sensory cortex stimulus sampling budget
    public int SensoryBudgetPerStep { get; init; } = 256;

    // ─── Interoception ────────────────────────────────────────────────────────
    // When enabled, the engine pipes its own internal state (ATP, sleep pressure,
    // neuromodulator levels, arousal) BACK into the brain as a perceived signal
    // through dedicated Y-stripes of the Thalamus region. This gives the brain
    // the opportunity to build a self-model that includes its own dynamics —
    // it can learn that certain voxel clusters mean "I am tired", "I am hungry",
    // "I am alarmed", etc., instead of those signals only affecting dynamics
    // invisibly. Six signals, each on its own thalamic stripe so the brain can
    // disambiguate them. Always on regardless of Sleep.SensorsConnected —
    // interoception is not an external sensor.
    public bool EnableInteroception { get; init; } = true;
    public int InteroceptionBudgetPerStep { get; init; } = 96;
    public float InteroceptionIntensity { get; init; } = 0.55f;

    // ─── Continuous persistence ───────────────────────────────────────────────
    // If PersistenceSnapshotPath is set, the engine:
    //   1. On Start(), attempts to load the file as a brain snapshot if it exists,
    //      restoring synapses, neuromodulators, sleep state, episodes, etc.
    //   2. During Step(), periodically writes a full SaveState snapshot to that
    //      path so a process restart resumes within at-most PersistenceSnapshotIntervalSeconds
    //      of the prior state. Death of the process must not be death of the self.
    //
    // Snapshot writes go to a .tmp file and rename-on-close so a crash during
    // write never leaves a corrupt snapshot in place. Disk I/O is synchronous
    // under the engine gate but only every N seconds, so the cost amortises.
    public string? PersistenceSnapshotPath { get; init; } = null;
    public float PersistenceSnapshotIntervalSeconds { get; init; } = 60f;

    // Render / monitoring

    /// <summary>
    /// Max number of cross-module traffic events packed into a single UI frame.
    /// Keeping this bounded prevents rare bursty phases from stalling the renderer.
    /// </summary>
    // Cross-module traffic flashes are a UI diagnostic; keep the default conservative
    // to avoid rare burst phases causing render hitches.
    public int UiMaxTrafficEvents { get; init; } = 300;

    

    /// <summary>
    /// Maximum spikes packed into each FAST UI frame. Spikes above this are downsampled deterministically.
    /// Lower = faster UI + less network/JS load.
    /// </summary>
    public int UiMaxSpikesPerFrame { get; init; } = 3500;
/// <summary>
    /// How often the engine publishes the fast UI frame (spikes + traffic) to be consumed by /api/engine/framefast.
    /// Higher values reduce allocations and JSON payload churn at the cost of lower visual update rate.
    /// </summary>
    public int UiPublishEveryNSteps { get; init; } = 1;


    // Simulation performance

    /// <summary>
    /// Enable parallel voxel state update (phase-1 of hemisphere stepping).
    /// Propagation + neighborhood effects remain sequential for determinism and biological locality.
    /// </summary>
    public bool EnableParallelVoxelUpdate { get; init; } = true;

    /// <summary>Minimum active voxel count to use Parallel.For (avoid overhead on tiny volumes).</summary>
    public int ParallelMinVoxelCount { get; init; } = 8192;

    /// <summary>
    /// Max degree of parallelism for voxel update. 0 or less = use Environment.ProcessorCount.
    /// </summary>
    public int ParallelMaxDegreeOfParallelism { get; init; } = 0;

    /// <summary>Enable detailed per-layer step profiling (adds timing overhead). Keep OFF for best performance.</summary>
    public bool EnableStepProfiling { get; init; } = false;

    /// <summary>Enable periodic console diagnostics output (adds IO overhead). Keep OFF for best performance.</summary>
    public bool EnableConsoleDiagnostics { get; init; } = false;


    // === Entry 022: Predictive Coding ===
    
    /// <summary>Enable hierarchical predictive coding (prediction errors, precision weighting).</summary>
    public bool EnablePredictiveCoding { get; init; } = false;
    
    /// <summary>Learning rate for prediction updates (how fast predictions track reality).</summary>
    public float PredictionLearningRate { get; init; } = 0.02f;
    
    /// <summary>How much prediction error lowers firing thresholds (attention effect).</summary>
    public float PredictionErrorThresholdGain { get; init; } = 0.06f;
    
    /// <summary>Slow decay rate of predictions toward baseline (prevents staleness).</summary>
    public float PredictionDecayRate { get; init; } = 0.001f;
    
    /// <summary>Base precision level (modulated by neuromodulators).</summary>
    public float BasePrecision { get; init; } = 0.5f;
    
    /// <summary>How much noradrenaline boosts precision (bottom-up attention).</summary>
    public float NoradrenalinePrecisionGain { get; init; } = 0.4f;
    
    /// <summary>How much dopamine boosts precision (reward-related attention).</summary>
    public float DopaminePrecisionGain { get; init; } = 0.25f;
    
    /// <summary>Fanout for feedback (top-down prediction) projections.</summary>
    public int FeedbackProjectionFanout { get; init; } = 2;
    
    /// <summary>Delay range for feedback projections (slower than feedforward).</summary>
    public int FeedbackMinDelayTicks { get; init; } = 2;
    public int FeedbackMaxDelayTicks { get; init; } = 5;

    // === Edge Budgets for Connection Display ===
    // Ensures all neural circuits are visible in the renderer
    
    /// <summary>Total edge budget for connection display.</summary>
    public int EdgeBudgetTotal { get; init; } = 15000;
    
    /// <summary>Edge budget for left hemisphere local (intra-region) connections.</summary>
    public int EdgeBudgetLeftLocal { get; init; } = 3000;
    
    /// <summary>Edge budget for right hemisphere local (intra-region) connections.</summary>
    public int EdgeBudgetRightLocal { get; init; } = 3000;
    
    /// <summary>Edge budget for callosal (inter-hemisphere) connections.</summary>
    public int EdgeBudgetCallosal { get; init; } = 4000;
    
    /// <summary>Edge budget for thalamo-cortical projections (region 1 → cortex).</summary>
    public int EdgeBudgetThalamoCortical { get; init; } = 1000;
    
    /// <summary>Edge budget for cortico-thalamic feedback (cortex → region 1).</summary>
    public int EdgeBudgetCorticoThalamic { get; init; } = 800;
    
    /// <summary>Edge budget for cortico-cortical long-range (between cortical regions).</summary>
    public int EdgeBudgetCorticoCortical { get; init; } = 1200;
    
    /// <summary>Edge budget for hippocampal-cortical loops (region 5 ↔ cortex).</summary>
    public int EdgeBudgetHippocampal { get; init; } = 600;
    
    /// <summary>Edge budget for amygdala projections (region 4 → cortex).</summary>
    public int EdgeBudgetAmygdala { get; init; } = 500;
    
    /// <summary>Edge budget for basal ganglia loops (cortex ↔ region 3 ↔ thalamus).</summary>
    public int EdgeBudgetBasalGanglia { get; init; } = 600;
    
    /// <summary>Edge budget for cerebellar-motor projections (region 27 → motor).</summary>
    public int EdgeBudgetCerebellar { get; init; } = 400;
    
    /// <summary>Edge budget for feedback (top-down prediction) connections.</summary>
    // NOTE: Default budgets are set so that the sum of all EdgeBudget* values equals EdgeBudgetTotal.
    public int EdgeBudgetFeedback { get; init; } = 2000;
    
    /// <summary>Edge budget for brainstem/pons connections.</summary>
    public int EdgeBudgetBrainstem { get; init; } = 400;
    
    // === CORTICAL MICROCIRCUIT (Entry 025) ===
    
    /// <summary>Enable E/I neuron type diversity in cortical regions.</summary>
    public bool EnableCorticalMicrocircuit { get; init; } = true;
    
    
    /// <summary>
    /// How often to rebuild microcircuit per-voxel caches (threshold/sign/VIP/leak).
    /// Rebuilding is expensive (O(W*H*D)); higher values are faster and still biologically plausible
    /// because microcircuit parameters drift slowly vs. membrane dynamics.
    /// </summary>
    public int MicrocircuitCacheRefreshEveryNSteps { get; init; } = 24;
/// <summary>Fraction of cortical neurons that are pyramidal (excitatory).</summary>
    public float PyramidalFraction { get; init; } = 0.80f;
    
    /// <summary>Fraction of cortical neurons that are PV+ interneurons.</summary>
    public float PVFraction { get; init; } = 0.10f;
    
    /// <summary>Fraction of cortical neurons that are SOM+ interneurons.</summary>
    public float SOMFraction { get; init; } = 0.07f;
    
    /// <summary>Fraction of cortical neurons that are VIP+ interneurons.</summary>
    public float VIPFraction { get; init; } = 0.03f;
    
    // === ENHANCED NEUROMODULATOR SYSTEM (Entry 025) ===
    
    /// <summary>Enable enhanced neuromodulator dynamics with receptor desensitization.</summary>
    public bool EnableEnhancedNeuromodulators { get; init; } = true;
    
    /// <summary>Dopamine tonic setpoint.</summary>
    public float DopamineTonicSetpoint { get; init; } = 0.15f;
    
    /// <summary>Norepinephrine tonic setpoint.</summary>
    public float NETonicSetpoint { get; init; } = 0.20f;
    
    /// <summary>Serotonin tonic setpoint.</summary>
    public float SerotoninTonicSetpoint { get; init; } = 0.25f;
    
    /// <summary>Acetylcholine tonic setpoint.</summary>
    public float AChTonicSetpoint { get; init; } = 0.20f;
    
    /// <summary>Rate of receptor desensitization under sustained stimulation.</summary>
    public float ReceptorDesensitizationRate { get; init; } = 0.15f;
    
    /// <summary>Rate of receptor resensitization during low stimulation.</summary>
    public float ReceptorResensitizationRate { get; init; } = 0.02f;
    
    // === SYNAPTIC TAGGING AND CAPTURE (Entry 025) ===
    
    /// <summary>Enable synaptic tagging and capture for long-term memory consolidation.</summary>
    public bool EnableSynapticTagging { get; init; } = true;
    
    /// <summary>Minimum weight change to set a synaptic tag.</summary>
    public float SynapticTagThreshold { get; init; } = 0.02f;
    
    /// <summary>Activation threshold for plasticity-related protein synthesis.</summary>
    public float PRPSynthesisThreshold { get; init; } = 0.15f;
    
    /// <summary>Tag decay half-life (seconds, compressed for simulation).</summary>
    public float TagHalfLifeSec { get; init; } = 120f;
    
    /// <summary>PRP decay half-life (seconds).</summary>
    public float PRPHalfLifeSec { get; init; } = 180f;
    
    /// <summary>Weight consolidation boost when tag captures PRP.</summary>
    public float ConsolidationBoost { get; init; } = 2.5f;
    
    // === SENSORY HIERARCHY (Entry 025) ===
    
    /// <summary>Enable hierarchical sensory processing.</summary>
    public bool EnableSensoryHierarchy { get; init; } = true;
    
    // === LANGUAGE SYSTEM (Entry 027) ===
    
    /// <summary>Enable language processing (Wernicke's, Broca's, dual-stream).</summary>
    public bool EnableLanguageSystem { get; init; } = true;
    
    /// <summary>Visual hierarchy width (retinotopic).</summary>
    public int SensoryVisualWidth { get; init; } = 16;
    
    /// <summary>Visual hierarchy height (retinotopic).</summary>
    public int SensoryVisualHeight { get; init; } = 16;
    
    /// <summary>Auditory hierarchy width (tonotopic).</summary>
    public int SensoryAuditoryWidth { get; init; } = 16;
    
    /// <summary>Number of visual orientation channels in V1.</summary>
    public int V1OrientationChannels { get; init; } = 8;
    
    /// <summary>Number of auditory frequency bands in A1.</summary>
    public int A1FrequencyBands { get; init; } = 16;
    
    // === ATTENTION SYSTEM (Entry 025) ===
    
    /// <summary>Enable attention system with biased competition.</summary>
    public bool EnableAttentionSystem { get; init; } = true;
    
    /// <summary>Maximum number of simultaneous attention foci.</summary>
    public int MaxAttentionFoci { get; init; } = 4;
    
    /// <summary>Spatial attention spread (sigma in voxels).</summary>
    public float AttentionSpatialSigma { get; init; } = 3.0f;
    
    /// <summary>Bottom-up (exogenous) attention gain.</summary>
    public float AttentionBottomUpGain { get; init; } = 0.4f;
    
    /// <summary>Top-down (endogenous) attention gain.</summary>
    public float AttentionTopDownGain { get; init; } = 0.6f;
    
    /// <summary>Suppression strength for unattended locations.</summary>
    public float AttentionSuppressionStrength { get; init; } = 0.3f;
    
    /// <summary>Enhancement strength for attended locations.</summary>
    public float AttentionEnhancementStrength { get; init; } = 0.5f;
    
    /// <summary>Inhibition of return duration (seconds).</summary>
    public float AttentionIORDuration { get; init; } = 1.0f;
    
    /// <summary>Number of feature attention channels.</summary>
    public int AttentionFeatureChannels { get; init; } = 32;
    
    // === BASAL GANGLIA CIRCUIT (Entry 028) ===
    
    /// <summary>Enable biologically accurate basal ganglia action selection.</summary>
    public bool EnableBasalGangliaCircuit { get; init; } = true;
    
    /// <summary>Number of action channels in basal ganglia.</summary>
    public int BasalGangliaChannels { get; init; } = 8;
    
    /// <summary>Direct pathway (Go) strength.</summary>
    public float DirectPathwayStrength { get; init; } = 0.8f;
    
    /// <summary>Indirect pathway (NoGo) strength.</summary>
    public float IndirectPathwayStrength { get; init; } = 0.6f;
    
    /// <summary>Hyperdirect pathway (STN global brake) strength.</summary>
    public float HyperdirectPathwayStrength { get; init; } = 0.5f;
    
    // === REWARD PREDICTION ERROR SYSTEM (Entry 028) ===
    
    /// <summary>Enable VTA reward prediction error signaling.</summary>
    public bool EnableRewardPrediction { get; init; } = true;
    
    /// <summary>Temporal discount factor for value learning (gamma).</summary>
    public float RPEDiscountFactor { get; init; } = 0.95f;
    
    /// <summary>Learning rate for value updates (alpha).</summary>
    public float RPELearningRate { get; init; } = 0.1f;
    
    /// <summary>Tonic dopamine setpoint.</summary>
    public float RPETonicDopamineSetpoint { get; init; } = 0.15f;
    
    /// <summary>Phasic dopamine decay rate.</summary>
    public float RPEPhasicDecayRate { get; init; } = 2.0f;
    
    /// <summary>Maximum phasic dopamine burst for positive RPE.</summary>
    public float RPEBurstMagnitude { get; init; } = 0.6f;
    
    /// <summary>Maximum phasic dopamine pause for negative RPE.</summary>
    public float RPEPauseMagnitude { get; init; } = 0.4f;
    
    // === WORKING MEMORY PFC (Entry 028) ===
    
    /// <summary>Enable working memory via PFC attractor dynamics.</summary>
    public bool EnableWorkingMemory { get; init; } = true;
    
    /// <summary>Number of working memory slots (capacity).</summary>
    public int WorkingMemorySlots { get; init; } = 7;
    
    /// <summary>Pattern size for working memory representations.</summary>
    public int WorkingMemoryPatternSize { get; init; } = 64;
    
    /// <summary>NMDA-like recurrent excitation strength.</summary>
    public float WorkingMemoryRecurrentStrength { get; init; } = 0.85f;
    
    /// <summary>Lateral inhibition between WM slots.</summary>
    public float WorkingMemoryLateralInhibition { get; init; } = 0.25f;
    
    /// <summary>Passive decay rate for WM contents.</summary>
    public float WorkingMemoryDecayRate { get; init; } = 0.02f;
    
    /// <summary>Dopamine threshold for opening WM gate.</summary>
    public float WorkingMemoryGatingThreshold { get; init; } = 0.4f;
    
    // === SYSTEMS CONSOLIDATION (Entry 028) ===
    
    /// <summary>Enable sleep-dependent systems consolidation.</summary>
    public bool EnableSystemsConsolidation { get; init; } = true;
    
    /// <summary>Maximum number of cortical consolidation traces.</summary>
    public int ConsolidationMaxTraces { get; init; } = 256;
    
    /// <summary>Cortical learning rate during consolidation.</summary>
    public float ConsolidationCorticalLearningRate { get; init; } = 0.01f;
    
    /// <summary>Hippocampal trace decay rate.</summary>
    public float ConsolidationHippocampalDecayRate { get; init; } = 0.001f;
    
    /// <summary>Cortical strength threshold for full consolidation.</summary>
    public float ConsolidationThreshold { get; init; } = 0.7f;
    
    /// <summary>Number of replays needed for consolidation.</summary>
    public int ConsolidationReplaysRequired { get; init; } = 5;
    
    /// <summary>Learning bonus when sleep triplet aligns.</summary>
    public float ConsolidationTripletBonus { get; init; } = 2.0f;
    
    // === BIOLOGICAL NEURON PROPORTIONS (Entry 028) ===
    
    /// <summary>
    /// Enable biological neuron count proportions for region sizing.
    /// When enabled, voxel allocation follows real neuroanatomical ratios.
    /// </summary>
    public bool EnableBiologicalProportions { get; init; } = true;
    
    /// <summary>
    /// Scale factor for voxel density based on biological neuron counts.
    /// 1.0 = pure biological ratios (cerebellum dominates)
    /// 0.0 = equal allocation across regions
    /// 0.5 = balanced (recommended for cognitive simulation)
    /// </summary>
    public float BiologicalProportionWeight { get; init; } = 0.5f;
    
    /// <summary>
    /// Enable region-specific synapse density based on biology.
    /// Cortical neurons: ~7000 synapses, Cerebellar granule: ~5 synapses.
    /// </summary>
    public bool EnableBiologicalSynapseDensity { get; init; } = true;
    
    /// <summary>
    /// Scale connectivity radius per region based on biological density.
    /// Dense regions (cerebellum) get tighter connectivity.
    /// </summary>
    public bool EnableBiologicalConnectivityRadius { get; init; } = true;
}
