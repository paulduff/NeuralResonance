namespace NRE.Core.Engine;

/// <summary>
/// Brainstem Motor Circuit — biologically detailed motor output pathway.
/// 
/// ANATOMY (Kandel et al. 2021, Purves et al. 2018):
///
/// The brainstem contains several motor systems that work in parallel:
///
/// 1. RETICULAR FORMATION (RF) — Postural tone and locomotion
///    - Pontine RF (medial): facilitates extensors, maintains upright posture
///    - Medullary RF (lateral): inhibits extensors, enables relaxation
///    - Receives: cortex (corticoreticular), cerebellum, sensory afferents
///    - Outputs: reticulospinal tracts → axial and proximal muscles
///    - Controls: trunk stability, gross limb position, locomotor patterns
///    - Arousal-dependent: high arousal = increased tone, low = relaxed
///
/// 2. RED NUCLEUS (RN) — Limb coordination
///    - Receives: cortex (corticorubral from M1/premotor), cerebellum (interposed nucleus)
///    - Outputs: rubrospinal tract → distal flexors (arms > legs)
///    - Controls: reaching, grasping preparation, upper limb flexion
///    - Complements corticospinal for limb movements
///
/// 3. VESTIBULAR NUCLEI (VN) — Balance and head/eye coordination
///    - Receives: vestibular nerve (gravity/acceleration), cerebellum (flocculonodular)
///    - Outputs: vestibulospinal → extensors; medial longitudinal fasciculus → eye/head
///    - Controls: balance, head position, postural corrections, eye stabilization
///    - Tonic activity maintains upright stance against gravity
///
/// 4. CRANIAL MOTOR NUCLEI — Face and vocal control
///    - Facial nucleus (CN VII): facial expression
///    - Hypoglossal (CN XII): tongue for speech
///    - Nucleus ambiguus (CN X): larynx/pharynx for vocalization
///    - Trigeminal motor (CN V): jaw
///    - Receives: cortex (corticobulbar), amygdala (emotional expression)
///
/// 5. SUPERIOR COLLICULUS — Orienting responses
///    - Receives: visual cortex, auditory cortex, somatosensory
///    - Outputs: tectospinal → head/neck turning toward stimuli
///    - Controls: gaze shifts, head orientation, orienting reflexes
///
/// INTEGRATION:
/// All systems receive descending cortical input (M1, premotor, SMA) that is
/// integrated with cerebellar timing, basal ganglia gating, and local pattern 
/// generators. The brainstem is where voluntary commands become coordinated 
/// motor programs.
///
/// SLEEP MODULATION:
/// - Awake: all systems active, RF tone moderate-high
/// - NREM: reduced RF tone, reduced movement, vestibular maintains posture
/// - REM: RF tone SUPPRESSED (atonia via pontine inhibition), except eye/respiratory
/// </summary>
public sealed class BrainstemMotorCircuit
{
    // === Motor nuclei output channels ===
    private readonly ReticularFormation _reticular = new();
    private readonly RedNucleus _redNucleus = new();
    private readonly VestibularNuclei _vestibular = new();
    private readonly CranialMotorPool _cranial = new();
    private readonly SuperiorColliculus _colliculus = new();
    
    // === Accumulated cortical/cerebellar drive per step ===
    private float _corticalDriveAxial;    // M1/premotor → RF (trunk/proximal)
    private float _corticalDriveLimb;     // M1 → RN (distal/limb)
    private float _corticalDriveFace;     // Corticobulbar → cranial nuclei
    private float _cerebellarSmoothing;   // Cerebellum → timing correction
    private float _vestibularInput;       // Gravity/balance signal
    private float _orientingDrive;        // Sensory salience → SC
    private float _amygdalaDrive;         // Emotional expression → cranial
    private int _inputCount;
    
    /// <summary>
    /// Feed cortical and subcortical inputs each step. Call multiple times per step
    /// as spikes arrive — values are accumulated and averaged at Step().
    /// </summary>
    public void AccumulateInput(
        float corticalAxial,      // Motor cortex drive for trunk/posture
        float corticalLimb,       // Motor cortex drive for limbs
        float corticalFace,       // Motor cortex drive for face/vocal
        float cerebellarCorrection, // Cerebellar timing/smoothing
        float vestibular,         // Balance/gravity
        float orientingSalience,  // Sensory salience for orienting
        float emotionalDrive)     // Amygdala → facial expression
    {
        _corticalDriveAxial += corticalAxial;
        _corticalDriveLimb += corticalLimb;
        _corticalDriveFace += corticalFace;
        _cerebellarSmoothing += cerebellarCorrection;
        _vestibularInput += vestibular;
        _orientingDrive += orientingSalience;
        _amygdalaDrive += emotionalDrive;
        _inputCount++;
    }
    
    /// <summary>
    /// Step all motor nuclei. Call once per simulation step after all inputs accumulated.
    /// Returns motor output for each body channel.
    /// </summary>
    public BrainstemMotorOutput Step(float dt, SleepPhase phase, float arousal01, float noradrenaline)
    {
        // Average accumulated inputs
        float n = MathF.Max(1f, _inputCount);
        float axial = _corticalDriveAxial / n;
        float limb = _corticalDriveLimb / n;
        float face = _corticalDriveFace / n;
        float cereb = _cerebellarSmoothing / n;
        float vestib = _vestibularInput / n;
        float orient = _orientingDrive / n;
        float emotion = _amygdalaDrive / n;
        
        // Reset accumulators
        _corticalDriveAxial = 0; _corticalDriveLimb = 0; _corticalDriveFace = 0;
        _cerebellarSmoothing = 0; _vestibularInput = 0;
        _orientingDrive = 0; _amygdalaDrive = 0;
        _inputCount = 0;
        
        // Step each nucleus
        var rf = _reticular.Step(dt, phase, arousal01, noradrenaline, axial, cereb);
        var rn = _redNucleus.Step(dt, phase, limb, cereb);
        var vn = _vestibular.Step(dt, phase, vestib, arousal01);
        var cn = _cranial.Step(dt, phase, face, emotion, arousal01);
        var sc = _colliculus.Step(dt, orient, arousal01);
        
        // === COMPOSE BODY OUTPUTS ===
        // Each joint is driven by a weighted mix of the relevant nuclei
        
        // Trunk/posture: primarily RF + VN
        float torsoTone = rf.ExtensorTone * 0.6f + vn.PosturalDrive * 0.4f;
        float torsoLean = (rf.AxialDrive + vn.LateralCorrection) * 0.5f;
        float torsoTwist = rf.AxialDrive * 0.3f + sc * 0.2f;
        
        // Shoulders: RF (proximal) + RN (reaching)
        float shoulderL = rf.ExtensorTone * 0.3f + rn.FlexorDriveL * 0.5f + torsoTone * 0.2f;
        float shoulderR = rf.ExtensorTone * 0.3f + rn.FlexorDriveR * 0.5f + torsoTone * 0.2f;
        
        // Elbows/wrists: primarily RN (distal flexors)
        float elbowL = rn.FlexorDriveL * 0.7f + rf.ExtensorTone * 0.2f;
        float elbowR = rn.FlexorDriveR * 0.7f + rf.ExtensorTone * 0.2f;
        float wristL = rn.FlexorDriveL * 0.4f;
        float wristR = rn.FlexorDriveR * 0.4f;
        
        // Hips/knees: primarily RF (proximal extensors) + VN (balance)
        float hipL = rf.ExtensorTone * 0.5f + vn.PosturalDrive * 0.3f;
        float hipR = rf.ExtensorTone * 0.5f + vn.PosturalDrive * 0.3f;
        float kneeL = rf.ExtensorTone * 0.6f + vn.PosturalDrive * 0.4f;
        float kneeR = rf.ExtensorTone * 0.6f + vn.PosturalDrive * 0.4f;
        
        // Head: SC (orienting) + VN (vestibular) + cranial
        float headTilt = sc * 0.6f + vn.LateralCorrection * 0.3f;
        float headNod = cn.JawDrive * 0.2f - (1f - arousal01) * 0.3f; // drowsy = nod down
        
        // Facial: cranial motor pool
        float facialExpression = cn.FacialDrive;
        float vocalDrive = cn.VocalDrive;
        
        return new BrainstemMotorOutput(
            TorsoLean: torsoLean,
            TorsoTwist: torsoTwist,
            ShoulderL: shoulderL, ShoulderR: shoulderR,
            ElbowL: elbowL, ElbowR: elbowR,
            WristL: wristL, WristR: wristR,
            HipL: hipL, HipR: hipR,
            KneeL: kneeL, KneeR: kneeR,
            HeadTilt: headTilt, HeadNod: headNod,
            FacialExpression: facialExpression,
            VocalDrive: vocalDrive,
            MuscleTone: rf.ExtensorTone,
            PosturalStability: vn.PosturalDrive);
    }
    
    public BrainstemMotorSnapshot Snapshot() => new(
        ReticulerTone: _reticular.Tone,
        RedNucleusL: _redNucleus.OutputL,
        RedNucleusR: _redNucleus.OutputR,
        VestibularDrive: _vestibular.Drive,
        CranialFacial: _cranial.Facial,
        CranialVocal: _cranial.Vocal,
        ColliculusOrient: _colliculus.Orient);

    // =====================================================================
    // INNER CLASSES: Individual brainstem nuclei
    // =====================================================================
    
    /// <summary>
    /// Reticular Formation — controls postural tone and locomotor patterns.
    /// Pontine RF facilitates extensors; medullary RF inhibits them.
    /// Net output is the balance, modulated by arousal and sleep state.
    /// </summary>
    private sealed class ReticularFormation
    {
        private float _extensorTone = 0.3f;
        private float _axialDrive;
        
        public float Tone => _extensorTone;
        
        public (float ExtensorTone, float AxialDrive) Step(
            float dt, SleepPhase phase, float arousal01, float noradrenaline,
            float corticalDrive, float cerebellarCorrection)
        {
            // Pontine RF: arousal-dependent facilitation
            float pontine = arousal01 * 0.6f + noradrenaline * 0.3f + corticalDrive * 0.3f;
            
            // Medullary RF: inhibitory, stronger during sleep
            float medullary = phase switch
            {
                SleepPhase.Awake => 0.2f + (1f - arousal01) * 0.2f,
                SleepPhase.Nrem => 0.5f,  // moderate inhibition
                SleepPhase.Rem => 0.9f,   // strong inhibition → REM atonia
                _ => 0.3f
            };
            
            // Net tone: pontine excitation minus medullary inhibition
            float targetTone = MathF.Max(0f, pontine - medullary);
            
            // Cerebellar smoothing reduces jitter
            targetTone = targetTone * (1f - cerebellarCorrection * 0.3f) 
                       + _extensorTone * cerebellarCorrection * 0.3f;
            
            // Smooth transition (biological time constant ~100ms)
            _extensorTone += (targetTone - _extensorTone) * Math.Min(1f, dt * 8f);
            _extensorTone = Math.Clamp(_extensorTone, 0f, 1f);
            
            // Axial drive from cortical input (voluntary trunk movement)
            _axialDrive += (corticalDrive * 0.5f - _axialDrive) * Math.Min(1f, dt * 6f);
            _axialDrive = Math.Clamp(_axialDrive, -1f, 1f);
            
            return (_extensorTone, _axialDrive);
        }
    }
    
    /// <summary>
    /// Red Nucleus — distal flexor control for reaching/grasping.
    /// Rubrospinal tract primarily controls upper limb flexors.
    /// Receives corticorubral (M1) and cerebellar (interposed nucleus) input.
    /// </summary>
    private sealed class RedNucleus
    {
        private float _flexorL, _flexorR;
        
        public float OutputL => _flexorL;
        public float OutputR => _flexorR;
        
        public (float FlexorDriveL, float FlexorDriveR) Step(
            float dt, SleepPhase phase, float corticalLimbDrive, float cerebellarCorrection)
        {
            // REM atonia suppresses output
            float sleepGate = phase == SleepPhase.Rem ? 0.05f : 
                              phase == SleepPhase.Nrem ? 0.3f : 1.0f;
            
            // Cortical drive split L/R with some asymmetry
            float targetL = corticalLimbDrive * 0.8f * sleepGate;
            float targetR = corticalLimbDrive * 0.8f * sleepGate;
            
            // Cerebellar correction: smooths and times the movement
            float smooth = 1f - cerebellarCorrection * 0.4f;
            targetL *= smooth;
            targetR *= smooth;
            
            _flexorL += (targetL - _flexorL) * Math.Min(1f, dt * 10f);
            _flexorR += (targetR - _flexorR) * Math.Min(1f, dt * 10f);
            _flexorL = Math.Clamp(_flexorL, 0f, 1f);
            _flexorR = Math.Clamp(_flexorR, 0f, 1f);
            
            return (_flexorL, _flexorR);
        }
    }
    
    /// <summary>
    /// Vestibular Nuclei — balance, head stabilization, postural reflexes.
    /// Provides tonic extensor drive to maintain stance against gravity.
    /// Lateral corrections based on vestibular error signals.
    /// </summary>
    private sealed class VestibularNuclei
    {
        private float _posturalDrive = 0.4f;
        private float _lateralCorrection;
        
        public float Drive => _posturalDrive;
        
        public (float PosturalDrive, float LateralCorrection) Step(
            float dt, SleepPhase phase, float vestibularInput, float arousal01)
        {
            // Tonic vestibular drive: always active to maintain posture
            float tonicTarget = phase switch
            {
                SleepPhase.Awake => 0.4f + arousal01 * 0.2f,
                SleepPhase.Nrem => 0.2f,  // reduced but present
                SleepPhase.Rem => 0.05f,  // minimal during atonia
                _ => 0.3f
            };
            
            _posturalDrive += (tonicTarget + vestibularInput * 0.3f - _posturalDrive) 
                            * Math.Min(1f, dt * 5f);
            _posturalDrive = Math.Clamp(_posturalDrive, 0f, 1f);
            
            // Lateral correction: responds to asymmetric input
            float lateralTarget = vestibularInput * 0.5f;
            _lateralCorrection += (lateralTarget - _lateralCorrection) * Math.Min(1f, dt * 8f);
            _lateralCorrection = Math.Clamp(_lateralCorrection, -1f, 1f);
            
            return (_posturalDrive, _lateralCorrection);
        }
    }
    
    /// <summary>
    /// Cranial Motor Pool — facial expression and vocalization.
    /// Facial nucleus (CN VII), nucleus ambiguus (CN X), hypoglossal (CN XII).
    /// Receives cortical (corticobulbar) and amygdala (emotional) input.
    /// </summary>
    private sealed class CranialMotorPool
    {
        private float _facialDrive;
        private float _vocalDrive;
        private float _jawDrive;
        
        public float Facial => _facialDrive;
        public float Vocal => _vocalDrive;
        
        public (float FacialDrive, float VocalDrive, float JawDrive) Step(
            float dt, SleepPhase phase, float corticalFace, float emotionalDrive, float arousal01)
        {
            float sleepGate = phase == SleepPhase.Awake ? 1.0f : 0.1f;
            
            // Facial: cortical voluntary + amygdala emotional expression
            float faceTarget = (corticalFace * 0.5f + emotionalDrive * 0.5f) * sleepGate;
            _facialDrive += (faceTarget - _facialDrive) * Math.Min(1f, dt * 12f);
            _facialDrive = Math.Clamp(_facialDrive, 0f, 1f);
            
            // Vocal: requires cortical drive AND sufficient arousal
            float vocalTarget = corticalFace * arousal01 * sleepGate;
            _vocalDrive += (vocalTarget - _vocalDrive) * Math.Min(1f, dt * 10f);
            _vocalDrive = Math.Clamp(_vocalDrive, 0f, 1f);
            
            // Jaw: corticobulbar
            _jawDrive += (corticalFace * 0.3f * sleepGate - _jawDrive) * Math.Min(1f, dt * 8f);
            _jawDrive = Math.Clamp(_jawDrive, 0f, 1f);
            
            return (_facialDrive, _vocalDrive, _jawDrive);
        }
    }
    
    /// <summary>
    /// Superior Colliculus — orienting responses.
    /// Drives head/eye turn toward salient stimuli.
    /// Receives visual, auditory, and somatosensory maps.
    /// </summary>
    private sealed class SuperiorColliculus
    {
        private float _headTurnDrive;
        
        public float Orient => _headTurnDrive;
        
        public float Step(float dt, float orientingSalience, float arousal01)
        {
            // Orienting is gated by arousal — don't orient when drowsy
            float target = orientingSalience * arousal01 * 0.8f;
            _headTurnDrive += (target - _headTurnDrive) * Math.Min(1f, dt * 15f);
            _headTurnDrive = Math.Clamp(_headTurnDrive, -1f, 1f);
            
            return _headTurnDrive;
        }
    }
}

/// <summary>Motor output from brainstem circuit — drives the body avatar.</summary>
public readonly record struct BrainstemMotorOutput(
    float TorsoLean, float TorsoTwist,
    float ShoulderL, float ShoulderR,
    float ElbowL, float ElbowR,
    float WristL, float WristR,
    float HipL, float HipR,
    float KneeL, float KneeR,
    float HeadTilt, float HeadNod,
    float FacialExpression, float VocalDrive,
    float MuscleTone, float PosturalStability);

/// <summary>Snapshot for telemetry display.</summary>
public readonly record struct BrainstemMotorSnapshot(
    float ReticulerTone, float RedNucleusL, float RedNucleusR,
    float VestibularDrive, float CranialFacial, float CranialVocal,
    float ColliculusOrient);
