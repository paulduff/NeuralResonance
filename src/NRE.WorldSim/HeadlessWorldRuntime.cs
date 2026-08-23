using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NRE.WorldSim;

/// <summary>
/// Authoritative, UI-neutral world process. It owns physical time and consequences;
/// all avatar locomotion and manipulation originate in neuronal motor telemetry.
/// </summary>
public sealed class HeadlessWorldRuntime : IAsyncDisposable
{
    private const double NominalStoredEnergyJoules = 8_000_000.0;
    private const double WorldMaximumForwardSpeed = 1.8;
    private const double WorldMaximumReverseSpeed = 0.65;
    private const double GravityMetersPerSecondSquared = 9.81;
    private const double TerminalVelocityMetersPerSecond = 24.0;
    private const double AvatarFootClearance = 0.03;
    private const double HandTargetContactRadiusMeters = 0.24;
    private const double HandReachObservationRadiusMeters = 0.70;
    private const double StableFoodHoldSeconds = 0.24;
    private const double StableDeviceHoldSeconds = 0.35;
    private const double ShelterRadius = 4.8;
    private const double PredatorSenseRadius = 10.0;
    private const double PredatorStrikeRadius = 0.65;
    private const int SightWidth = 64;
    private const int SightHeight = 40;
    private const int AudioSampleRate = 8_000;
    private const int AudioSamples = 960;
    private const double NominalFullRecruitmentMotorDrive = 48.0;
    private static readonly TimeSpan BrainFreshness = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions ControlFrameJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly AvatarKinematicsOptions KinematicsOptions = new(
        MaxMotorDrive: 240.0,
        ForwardSpeedCoefficient: 0.006,
        TurnSpeedCoefficient: 0.9,
        MinForwardSpeed: -WorldMaximumReverseSpeed,
        MaxForwardSpeed: WorldMaximumForwardSpeed,
        MaxTurnRateDeg: 120.0,
        AllowSignedMotorDrive: true,
        InPlaceTurnCancelsForwardDrive: true);

    private static readonly AvatarNervousSystemOptions NervousSystemOptions = new(
        KinematicsOptions,
        DriveDecay: 0.975);

    private static readonly AvatarPhysiologyOptions PhysiologyOptions = new(
        NominalStoredEnergyJoules,
        MetabolicBurnJoulesPerSecond: 3_360.0,
        HydrationLossPerSecond: 0.00022,
        EnergyDepletionStressEnter: 0.62,
        EnergyDepletionStressFull: 0.92,
        EnergyDamageRateMinimum: 0.0028,
        EnergyDamageRateScale: 0.0062,
        DehydrationDamageThreshold: 0.20,
        DehydrationDamageRateMinimum: 0.002,
        DehydrationDamageRateScale: 0.008,
        ShelteredSleepRecoveryRate: 0.010);

    private readonly object gate = new();
    private readonly object lifecycleGate = new();
    private readonly HeadlessWorldOptions options;
    private readonly HttpClient frameClient;
    private readonly HttpClient sensoryClient;
    private readonly HttpClient somaticClient;
    private readonly HttpClient audioClient;
    private readonly AvatarService avatarService = new(
        NervousSystemOptions,
        "NRE.HeadlessWorld.AvatarService",
        new AvatarServiceClockOptions(Enabled: true, TickIntervalMs: 50));
    private readonly AvatarArticulatedBody articulatedBody = new();
    private readonly AvatarTerrainAscentController terrainAscent = new();
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private readonly DateTimeOffset sessionStartedUtc = DateTimeOffset.UtcNow;
    private readonly HashSet<int> visitedCells = [];
    private readonly List<MutableEntity> foods = [];
    private readonly List<MutableEntity> devices = [];
    private readonly List<MutableEntity> predators = [];
    private readonly List<MutableEntity> shelters = [];
    private readonly List<BodyContactSample> activeBodyContacts = [];
    private readonly Dictionary<string, double> contactDurationMilliseconds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> fallbackHandContactDurationMilliseconds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldRunContactObservation> activeFallbackHandContacts =
        new(StringComparer.Ordinal);
    private readonly Queue<PhysicalBodyFrameRequest> pendingCriticalBodyFrames = [];
    private readonly WorldRunTelemetryAccumulator runTelemetry = new();
    private readonly AvatarHandPlant leftHandPlant = new();
    private readonly AvatarHandPlant rightHandPlant = new();

    private CancellationTokenSource? lifetime;
    private Task[] loops = [];
    private WorldTerrain terrain;
    private WorldPhysicsScene? physicsScene;
    private PhysicalArticulationFrame acceptedArticulation = PhysicalArticulationFrame.Neutral;
    private bool running = true;
    private bool disposed;
    private long worldTick;
    private double elapsedSeconds;
    private double avatarX;
    private double avatarY;
    private double avatarZ;
    private double avatarHeadingDegrees = 180.0;
    private double avatarVerticalVelocity;
    private bool avatarGrounded = true;
    private double distanceTravelled;
    private AvatarPhysiologyState physiology;
    private AvatarVitalState vitalState = AvatarVitalState.Viable;
    private double vitalStateSinceSeconds;
    private int physicalDeaths;
    private bool neuronalSleep;
    private long dispatchSinceMilliseconds;
    private long lastNeuronalMotorTick = -1;
    private double latestSpinalWithdrawalDrive;
    private IReadOnlyList<SpinalWithdrawalSourceActivity> latestSpinalWithdrawalSources = [];
    private DateTimeOffset? lastFrameUtc;
    private string brainStatus = "Waiting for ControlProgram";
    private double lastForwardSpeed;
    private double lastTurnRateDegrees;
    private double collisionPulse;
    private double collisionBodyPositionX;
    private double collisionBodyPositionY;
    private double collisionBodyPositionZ = 0.45;
    private double collisionNormalX;
    private double collisionNormalY;
    private double collisionNormalZ = -1.0;
    private bool movementBlockedLastTick;
    private int collisionHits;
    private long tickFailures;
    private long neuronalMotorDispatchTotal;
    private long neuronalLocomotorDispatchTotal;
    private long neuronalManipulatorDispatchTotal;
    private long interactionAttempts;
    private long interactionSuccesses;
    private long interactionOutOfReach;
    private long interactionOutsideCone;
    private long interactionOccluded;
    private long interactionUnavailable;
    private string lastInteractionOutcome = "none";
    private MutableEntity? leftHeldTarget;
    private MutableEntity? rightHeldTarget;
    private bool leftReachObserved;
    private bool rightReachObserved;
    private long reachEntries;
    private long handContacts;
    private long grasps;
    private long holds;
    private long releases;
    private long graspMisses;
    private long fatigueReleases;
    private long retinalFramesAccepted;
    private long leftRetinalFramesAccepted;
    private long rightRetinalFramesAccepted;
    private long cochlearFramesAccepted;
    private long physicalBodyFramesAccepted;
    private long physicalBodyFramesRejected;
    private long somaticFramesAccepted;
    private long somaticFramesRejected;
    private long somaticFrameRetries;
    private long somaticFrameRetryRecoveries;
    private long bodyInputFailures;
    private string lastBodyInputError = "none";
    private DateTimeOffset lastBodyInputDiagnosticUtc = DateTimeOffset.MinValue;
    private long physicalSequence;
    private long somaticSequence;
    private long audioSequence;
    private int sightGeneration;
    private int foodConsumed;
    private int devicePickupsCollected;
    private AvatarDeviceInventory inventory;
    private int waterInteractions;
    private int predatorsNeutralized;
    private double physicalContactTissueDamageFraction;
    private long physicalImpactDamageEvents;
    private long sustainedPressureDamageEpisodes;
    private readonly Dictionary<string, double> physicalContactDamageByRegion = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> bodyTissueDamageByCause = new(StringComparer.Ordinal);
    private bool previousWaterContact;
    private string? lastRunReportPath;
    private string? lastRunReportError;
    private string? lastRollingReportPath;
    private string? lastRollingReportError;
    private DateTimeOffset lastRollingReportAttemptUtc = DateTimeOffset.MinValue;
    private double latestBrainFrameLatencyMilliseconds;
    private int consecutiveSlowBrainFrames;
    private long brainFrameOverloadSafetyPauses;
    private string lastBrainFrameOverloadReason = "none";

    public HeadlessWorldRuntime(HeadlessWorldOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
        terrain = new WorldTerrain(options.Seed);
        physiology = AvatarWorldDynamics.CreateRespawnState(PhysiologyOptions);
        frameClient = CreateHttpClient(TimeSpan.FromSeconds(5));
        sensoryClient = CreateHttpClient(TimeSpan.FromSeconds(6));
        somaticClient = CreateHttpClient(TimeSpan.FromSeconds(5));
        audioClient = CreateHttpClient(TimeSpan.FromSeconds(9));
        ResetCore(options.Seed);
    }

    public bool IsStarted
    {
        get
        {
            lock (lifecycleGate)
            {
                return lifetime is not null;
            }
        }
    }

    public string? LastRunReportPath
    {
        get
        {
            lock (gate)
            {
                return lastRunReportPath;
            }
        }
    }

    public string? LastRunReportError
    {
        get
        {
            lock (gate)
            {
                return lastRunReportError;
            }
        }
    }

    public string? LastRollingReportPath
    {
        get
        {
            lock (gate)
            {
                return lastRollingReportPath;
            }
        }
    }

    public string? LastRollingReportError
    {
        get
        {
            lock (gate)
            {
                return lastRollingReportError;
            }
        }
    }

    public void Start(CancellationToken cancellationToken = default)
    {
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (lifetime is not null)
            {
                Resume();
                return;
            }

            lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = lifetime.Token;
            loops =
            [
                Task.Run(() => SimulationLoopAsync(token), token),
                Task.Run(() => BrainFrameLoopAsync(token), token),
                Task.Run(() => BodyFrameLoopAsync(token), token),
                Task.Run(() => VisionFrameLoopAsync(token), token),
                Task.Run(() => AudioFrameLoopAsync(token), token)
            ];
        }
    }

    public void Resume()
    {
        lock (gate)
        {
            running = true;
            consecutiveSlowBrainFrames = 0;
        }
    }

    public void Pause()
    {
        lock (gate)
        {
            running = false;
            lastForwardSpeed = 0.0;
            lastTurnRateDegrees = 0.0;
        }
        PersistRunReport("paused");
    }

    public WorldSimulationSnapshot Reset(int? seed = null)
    {
        PersistRunReport("reset");
        lock (gate)
        {
            ResetCore(seed ?? terrain.Seed);
            avatarService.PostResetMotor();
            return GetSnapshot();
        }
    }

    public WorldSimulationSnapshot GetSnapshot()
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            var frameAge = lastFrameUtc.HasValue
                ? Math.Max(0.0, (now - lastFrameUtc.Value).TotalSeconds)
                : 999_999.0;
            var signal = avatarService.LatestSignal;
            var assessment = AvatarWorldDynamics.AssessVitalState(physiology, PhysiologyOptions);
            return new WorldSimulationSnapshot(
                ProtocolVersion: "dnne.worldsim.state.v3",
                SessionId: sessionId,
                ProcessId: Environment.ProcessId,
                Running: running,
                WorldReady: true,
                GeneratedUtc: now,
                SessionStartedUtc: sessionStartedUtc,
                ElapsedSeconds: elapsedSeconds,
                WorldTick: worldTick,
                ControlEndpoint: options.ControlEndpoint.ToString(),
                BrainConnected: frameAge <= BrainFreshness.TotalSeconds,
                TelemetryAgeSeconds: frameAge,
                FrameAgeSeconds: frameAge,
                BrainStatus: brainStatus,
                Seed: terrain.Seed,
                AvatarX: avatarX,
                AvatarY: avatarY,
                AvatarZ: avatarZ,
                AvatarHeadingDeg: avatarHeadingDegrees,
                AvatarForwardSpeed: lastForwardSpeed,
                AvatarTurnRateDeg: lastTurnRateDegrees,
                AvatarVerticalVelocity: avatarVerticalVelocity,
                AvatarGrounded: avatarGrounded,
                MotorTrainingMode: options.MotorTrainingMode,
                TerrainAscentMode: terrainAscent.ModeName,
                TerrainAscentProgress: terrainAscent.Progress,
                TerrainAscentEncounters: terrainAscent.EncounterCount,
                TerrainAscentStarted: terrainAscent.StartedCount,
                TerrainStepStarted: terrainAscent.StepStartedCount,
                TerrainMantleStarted: terrainAscent.MantleStartedCount,
                TerrainAscentCompleted: terrainAscent.CompletedCount,
                TerrainStepCompleted: terrainAscent.StepCompletedCount,
                TerrainMantleCompleted: terrainAscent.MantleCompletedCount,
                TerrainAscentAborted: terrainAscent.AbortedCount,
                TerrainAscentRejected: terrainAscent.RejectedCount,
                TerrainAscentLastOutcome: terrainAscent.LastOutcome,
                DistanceTravelled: distanceTravelled,
                VisitedTerrainCells: visitedCells.Count,
                ExplorableTerrainCells: terrain.ExplorableCellCount,
                NeuronalMotorDispatchTotal: neuronalMotorDispatchTotal,
                NeuronalLocomotorDispatchTotal: neuronalLocomotorDispatchTotal,
                NeuronalManipulatorDispatchTotal: neuronalManipulatorDispatchTotal,
                LeftMotorDrive: signal.LeftMotorDrive,
                RightMotorDrive: signal.RightMotorDrive,
                ManipulatorDrive: signal.ManipulatorDrive,
                HeadYawDrive: signal.HeadYawDrive,
                HeadPitchDrive: signal.HeadPitchDrive,
                StandDrive: signal.StandDrive,
                CrouchDrive: signal.CrouchDrive,
                SitDrive: signal.SitDrive,
                LieDrive: signal.LieDrive,
                LeftHipCoronalDrive: signal.LeftHipCoronalDrive,
                RightHipCoronalDrive: signal.RightHipCoronalDrive,
                LeftAnkleSagittalDrive: signal.LeftAnkleSagittalDrive,
                RightAnkleSagittalDrive: signal.RightAnkleSagittalDrive,
                LeftAnkleCoronalDrive: signal.LeftAnkleCoronalDrive,
                RightAnkleCoronalDrive: signal.RightAnkleCoronalDrive,
                TrunkYawDrive: signal.TrunkYawDrive,
                Articulation: acceptedArticulation,
                InteractionAttempts: interactionAttempts,
                InteractionSuccesses: interactionSuccesses,
                InteractionOutOfReach: interactionOutOfReach,
                InteractionOutsideCone: interactionOutsideCone,
                InteractionOccluded: interactionOccluded,
                InteractionUnavailable: interactionUnavailable,
                LastInteractionOutcome: lastInteractionOutcome,
                RetinalFramesAccepted: retinalFramesAccepted,
                LeftRetinalFramesAccepted: leftRetinalFramesAccepted,
                RightRetinalFramesAccepted: rightRetinalFramesAccepted,
                CochlearFramesAccepted: cochlearFramesAccepted,
                PhysicalBodyFramesAccepted: physicalBodyFramesAccepted,
                PhysicalBodyFramesRejected: physicalBodyFramesRejected,
                SomaticFramesAccepted: somaticFramesAccepted,
                SomaticFramesRejected: somaticFramesRejected,
                SomaticFrameRetries: somaticFrameRetries,
                SomaticFrameRetryRecoveries: somaticFrameRetryRecoveries,
                BodyInputFailures: bodyInputFailures,
                LastBodyInputError: lastBodyInputError,
                FoodConsumed: foodConsumed,
                WeaponPickupsCollected: devicePickupsCollected,
                WeaponCharges: inventory.TotalCharges,
                WaterInteractions: waterInteractions,
                PredatorsActive: predators.Count,
                PredatorsSuspended: !options.PredatorsEnabled,
                PredatorsNeutralized: predatorsNeutralized,
                StoredEnergyJoules: physiology.StoredEnergyJoules,
                TissueIntegrityFraction: physiology.TissueIntegrityFraction,
                HydrationFraction: physiology.HydrationFraction,
                VitalState: assessment.State.ToString(),
                VitalStateSeconds: Math.Max(0.0, elapsedSeconds - vitalStateSinceSeconds),
                PhysicalDeaths: physicalDeaths,
                InShelter: IsInShelterCore(),
                NeuronalSleep: neuronalSleep,
                CollisionHits: collisionHits,
                TickFailures: tickFailures,
                FoodPickups: SnapshotEntities(foods),
                WeaponPickups: SnapshotEntities(devices),
                Predators: SnapshotEntities(predators),
                Shelters: SnapshotEntities(shelters),
                PhysicalContactTissueDamageFraction: physicalContactTissueDamageFraction,
                PhysicalImpactDamageEvents: physicalImpactDamageEvents,
                SustainedPressureDamageEpisodes: sustainedPressureDamageEpisodes,
                MostDamagedContactRegion: physicalContactDamageByRegion.Count == 0
                    ? "none"
                    : physicalContactDamageByRegion.MaxBy(static pair => pair.Value).Key,
                DevelopmentStage: options.DevelopmentStage.ToString(),
                LeftHandGraspDrive: signal.LeftHandGraspDrive,
                RightHandGraspDrive: signal.RightHandGraspDrive,
                LeftHandPhase: leftHandPlant.State.Phase.ToString(),
                RightHandPhase: rightHandPlant.State.Phase.ToString(),
                LeftHandPhaseSeconds: leftHandPlant.State.PhaseDurationSeconds,
                RightHandPhaseSeconds: rightHandPlant.State.PhaseDurationSeconds,
                LeftHandApertureFraction: leftHandPlant.State.ApertureFraction,
                RightHandApertureFraction: rightHandPlant.State.ApertureFraction,
                LeftGripForceNewtons: leftHandPlant.State.GripForceNewtons,
                RightGripForceNewtons: rightHandPlant.State.GripForceNewtons,
                LeftHandFatigue: leftHandPlant.State.FatigueFraction,
                RightHandFatigue: rightHandPlant.State.FatigueFraction,
                LeftHandSlip: leftHandPlant.State.SlipFraction,
                RightHandSlip: rightHandPlant.State.SlipFraction,
                ReachEntries: reachEntries,
                HandContacts: handContacts,
                Grasps: grasps,
                Holds: holds,
                Releases: releases,
                GraspMisses: graspMisses,
                FatigueReleases: fatigueReleases,
                BrainFrameLatencyMilliseconds: latestBrainFrameLatencyMilliseconds,
                ConsecutiveSlowBrainFrames: consecutiveSlowBrainFrames,
                BrainFrameOverloadSafetyPauses: brainFrameOverloadSafetyPauses,
                LastBrainFrameOverloadReason: lastBrainFrameOverloadReason);
        }
    }

    public async Task StopAsync()
    {
        Task[] pending;
        lock (lifecycleGate)
        {
            if (lifetime is null)
            {
                return;
            }

            lifetime.Cancel();
            pending = loops;
            loops = [];
        }

        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal host shutdown.
        }
        finally
        {
            lock (lifecycleGate)
            {
                lifetime?.Dispose();
                lifetime = null;
            }
            PersistRunReport("stopped");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await StopAsync().ConfigureAwait(false);
        avatarService.Dispose();
        frameClient.Dispose();
        sensoryClient.Dispose();
        somaticClient.Dispose();
        audioClient.Dispose();
        lock (gate)
        {
            physicsScene?.Dispose();
            physicsScene = null;
        }
    }

    private async Task SimulationLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(options.EffectiveSimulationInterval);
        var clock = Stopwatch.StartNew();
        var previous = clock.Elapsed;
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            var now = clock.Elapsed;
            var elapsed = Math.Clamp((now - previous).TotalSeconds, 0.001, 0.2);
            previous = now;
            try
            {
                AdvanceWorld(elapsed);
            }
            catch
            {
                lock (gate)
                {
                    tickFailures++;
                }
            }

            PersistRollingRunReportIfDue();
        }
    }

    private async Task BrainFrameLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(options.EffectiveFramePollInterval);
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            var frameClock = Stopwatch.StartNew();
            try
            {
                var since = Interlocked.Read(ref dispatchSinceMilliseconds);
                var response = await AvatarControlApi.GetJsonAsync(
                    frameClient,
                    options.ControlEndpoint,
                    AvatarControlApi.GetFramePath(since, includeConnectome: false),
                    token).ConfigureAwait(false);
                using var document = response.Document;
                if (!response.IsSuccessStatusCode || document is null)
                {
                    ObserveBrainFrameLatency(frameClock.Elapsed, frameSucceeded: false);
                    ClearSpinalWithdrawalTelemetry();
                    SetBrainStatus($"ControlProgram returned HTTP {(int)response.StatusCode}");
                    continue;
                }

                var root = document.RootElement;
                var state = TryGetObject(root, "state", out var stateElement) ? stateElement : default;
                var actionAuthorityHistory = ReadActionAuthorityHistory(state);
                if (actionAuthorityHistory is not null)
                {
                    runTelemetry.ObserveActionAuthority(actionAuthorityHistory);
                }
                var dispatches = AvatarDispatchSpikeParser.ParseDispatchSpikes(root, since, out var maximumWallClock);
                if (maximumWallClock > since)
                {
                    Interlocked.Exchange(ref dispatchSinceMilliseconds, maximumWallClock);
                }

                long previousNeuronalTick;
                lock (gate)
                {
                    previousNeuronalTick = lastNeuronalMotorTick;
                }

                dispatches = AvatarNeuronalMotorBridge.Compose(
                    state,
                    dispatches,
                    previousNeuronalTick,
                    out var nextNeuronalTick,
                    out var neuronalState);
                var motorEvents = 0;
                var locomotorEvents = 0;
                var manipulatorEvents = 0;
                foreach (var dispatch in dispatches)
                {
                    if (AvatarMotorCatalog.IsMotorStructure(dispatch.SourceStructure))
                    {
                        motorEvents++;
                    }
                    if (AvatarMotorCatalog.IsLocomotorPopulationEvent(dispatch))
                    {
                        locomotorEvents++;
                    }
                    if (AvatarEffectorCatalog.IsHandEvent(dispatch))
                    {
                        manipulatorEvents++;
                    }
                }

                avatarService.PostBrainSignals(dispatches);
                lock (gate)
                {
                    lastNeuronalMotorTick = nextNeuronalTick;
                    latestSpinalWithdrawalDrive = neuronalState.SpinalWithdrawalDrive;
                    latestSpinalWithdrawalSources = neuronalState.SpinalWithdrawalSources?.ToArray() ?? [];
                    neuronalMotorDispatchTotal += motorEvents;
                    neuronalLocomotorDispatchTotal += locomotorEvents;
                    neuronalManipulatorDispatchTotal += manipulatorEvents;
                    neuronalSleep = ReadSleepState(state);
                    lastFrameUtc = DateTimeOffset.UtcNow;
                    brainStatus = "Neuronal frame stream live";
                }
                ObserveBrainFrameLatency(frameClock.Elapsed, frameSucceeded: true);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                ObserveBrainFrameLatency(frameClock.Elapsed, frameSucceeded: false);
                ClearSpinalWithdrawalTelemetry();
                SetBrainStatus($"Brain link unavailable ({error.GetType().Name})");
            }
        }
    }

    private void ClearSpinalWithdrawalTelemetry()
    {
        lock (gate)
        {
            latestSpinalWithdrawalDrive = 0.0;
            latestSpinalWithdrawalSources = [];
        }
    }

    private async Task BodyFrameLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(options.EffectiveBodyFrameInterval);
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            if (!ShouldSendSensoryFrames())
            {
                continue;
            }

            try
            {
                List<PhysicalBodyFrameRequest> bodies = [];
                List<SomaticContactFrameRequest> contacts = [];
                lock (gate)
                {
                    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var articulation = acceptedArticulation;
                    foreach (var sample in activeBodyContacts)
                    {
                        contacts.Add(new SomaticContactFrameRequest(
                            Interlocked.Increment(ref somaticSequence), nowMs,
                            (float)sample.BodyX,
                            (float)sample.BodyY,
                            (float)sample.BodyZ,
                            (float)sample.NormalX,
                            (float)sample.NormalY,
                            (float)sample.NormalZ,
                            (float)sample.ForceNewtons,
                            (float)sample.ImpulseNewtonSeconds,
                            (float)(sample.PenetrationMeters * 1_000.0),
                            (float)sample.TangentialSpeedMetersPerSecond,
                            (float)sample.ContactAreaSquareMillimeters,
                            (float)sample.DurationMilliseconds,
                            sample.InputSource));
                    }
                    if (collisionPulse > 0.05)
                    {
                        contacts.Add(new SomaticContactFrameRequest(
                            Interlocked.Increment(ref somaticSequence), nowMs,
                            (float)collisionBodyPositionX,
                            (float)collisionBodyPositionY,
                            (float)collisionBodyPositionZ,
                            (float)collisionNormalX,
                            (float)collisionNormalY,
                            (float)collisionNormalZ,
                            (float)(1_200.0 + (collisionPulse * 3_200.0)),
                            (float)(25.0 + (collisionPulse * 120.0)),
                            (float)(collisionPulse * 28.0),
                            (float)Math.Abs(lastForwardSpeed), 1_100f,
                            (float)options.EffectiveBodyFrameInterval.TotalMilliseconds,
                            "avatar_world_contact"));
                    }
                    if (!activeBodyContacts.Any(static sample => sample.Region == "left_hand"))
                    {
                        AddHandContactFrame(
                            contacts,
                            nowMs,
                            articulation.LeftHandLoadNewtons,
                            bodyPositionX: -0.42f,
                            region: "left_hand");
                    }
                    else
                    {
                        fallbackHandContactDurationMilliseconds.Remove("left_hand");
                        activeFallbackHandContacts.Remove("left_hand");
                    }
                    if (!activeBodyContacts.Any(static sample => sample.Region == "right_hand"))
                    {
                        AddHandContactFrame(
                            contacts,
                            nowMs,
                            articulation.RightHandLoadNewtons,
                            bodyPositionX: 0.42f,
                            region: "right_hand");
                    }
                    else
                    {
                        fallbackHandContactDurationMilliseconds.Remove("right_hand");
                        activeFallbackHandContacts.Remove("right_hand");
                    }
                    while (pendingCriticalBodyFrames.Count > 0)
                    {
                        bodies.Add(pendingCriticalBodyFrames.Dequeue());
                    }
                    bodies.Add(CreatePhysicalBodyFrameCore(nowMs, articulation));
                }

                await DispatchSomaticContactsAsync(contacts, token).ConfigureAwait(false);

                foreach (var body in bodies)
                {
                    try
                    {
                        var bodyResult = await AvatarControlApi.PostPhysicalBodyFrameAsync(
                            sensoryClient, options.ControlEndpoint, body, token).ConfigureAwait(false);
                        if (bodyResult.Accepted && bodyResult.TargetInstances > 0)
                        {
                            Interlocked.Increment(ref physicalBodyFramesAccepted);
                        }
                        else
                        {
                            Interlocked.Increment(ref physicalBodyFramesRejected);
                            RecordBodyInputFailure(
                                "physical",
                                $"source={body.InputSource} accepted={bodyResult.Accepted} " +
                                $"targets={bodyResult.TargetInstances}");
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception error)
                    {
                        Interlocked.Increment(ref physicalBodyFramesRejected);
                        RecordBodyInputFailure(
                            "physical",
                            $"source={body.InputSource} sequence={body.Sequence}: " +
                            $"{error.GetType().Name}: {error.Message}");
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                RecordBodyInputFailure("body-cycle", $"{error.GetType().Name}: {error.Message}");
                SetBrainStatus($"Body input delayed ({error.GetType().Name})");
            }
        }
    }

    private async Task DispatchSomaticContactsAsync(
        IReadOnlyList<SomaticContactFrameRequest> contacts,
        CancellationToken token)
    {
        await Parallel.ForEachAsync(
            contacts,
            new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = 4
            },
            async (contact, cancellationToken) =>
            {
                var retried = false;
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        var result = await AvatarControlApi.PostSomaticContactFrameAsync(
                            somaticClient,
                            options.ControlEndpoint,
                            contact,
                            cancellationToken).ConfigureAwait(false);
                        if (result.Accepted && result.TargetInstances > 0)
                        {
                            Interlocked.Increment(ref somaticFramesAccepted);
                            if (retried)
                            {
                                Interlocked.Increment(ref somaticFrameRetryRecoveries);
                            }
                        }
                        else
                        {
                            Interlocked.Increment(ref somaticFramesRejected);
                            RecordBodyInputFailure(
                                "somatic",
                                $"source={contact.InputSource} accepted={result.Accepted} " +
                                $"targets={result.TargetInstances}");
                        }
                        return;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception error) when (attempt == 0 && IsTransientSomaticFailure(error))
                    {
                        retried = true;
                        Interlocked.Increment(ref somaticFrameRetries);
                        await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception error)
                    {
                        Interlocked.Increment(ref somaticFramesRejected);
                        RecordBodyInputFailure("somatic", DescribeRejectedContact(contact, error));
                        return;
                    }
                }
            }).ConfigureAwait(false);
    }

    private static bool IsTransientSomaticFailure(Exception error)
        => error is HttpRequestException or TaskCanceledException or TimeoutException;

    internal static ActionAuthorityCumulativeTelemetry? ReadActionAuthorityHistory(JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object ||
            !TryGetObject(state, "actionAuthorityHistory", out var history) ||
            history.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            return history.Deserialize<ActionAuthorityCumulativeTelemetry>(ControlFrameJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void RecordBodyInputFailure(string channel, string detail)
    {
        var now = DateTimeOffset.UtcNow;
        var emitDiagnostic = false;
        lock (gate)
        {
            bodyInputFailures++;
            lastBodyInputError = $"{channel}: {detail}";
            if ((now - lastBodyInputDiagnosticUtc).TotalSeconds >= 5.0)
            {
                lastBodyInputDiagnosticUtc = now;
                emitDiagnostic = true;
            }
        }

        if (emitDiagnostic)
        {
            Console.WriteLine($"World body-input warning: {channel}: {detail}");
        }
    }

    private static string DescribeRejectedContact(
        SomaticContactFrameRequest contact,
        Exception error) =>
        $"source={contact.InputSource} sequence={contact.Sequence} " +
        $"position=({contact.BodyPositionX:F3},{contact.BodyPositionY:F3},{contact.BodyPositionZ:F3}) " +
        $"normal=({contact.SurfaceNormalX:F3},{contact.SurfaceNormalY:F3},{contact.SurfaceNormalZ:F3}) " +
        $"forceN={contact.ForceNewtons:F2} impulseNs={contact.ImpulseNewtonSeconds:F3} " +
        $"penetrationMm={contact.PenetrationMillimeters:F3} " +
        $"tangentMps={contact.TangentialSpeedMetersPerSecond:F3} " +
        $"areaMm2={contact.ContactAreaSquareMillimeters:F1} durationMs={contact.DurationMilliseconds:F1}: " +
        $"{error.GetType().Name}: {error.Message}";

    private async Task VisionFrameLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(options.EffectiveVisionFrameInterval);
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            if (!ShouldSendSensoryFrames())
            {
                continue;
            }

            try
            {
                var frames = RenderBinocularSightFrames();
                var retinalInputs = new[]
                {
                    (Frame: frames.Left, Source: AvatarRuntimeDefaults.LeftRetinalInputSource, IsLeft: true),
                    (Frame: frames.Right, Source: AvatarRuntimeDefaults.RightRetinalInputSource, IsLeft: false)
                };
                foreach (var retinalInput in retinalInputs)
                {
                    var result = await AvatarControlApi.PostRetinalFrameAsync(
                        sensoryClient,
                        options.ControlEndpoint,
                        retinalInput.Frame,
                        retinalInput.Source,
                        token).ConfigureAwait(false);
                    if (!result.Accepted || result.TargetInstances <= 0)
                    {
                        continue;
                    }

                    Interlocked.Increment(ref retinalFramesAccepted);
                    if (retinalInput.IsLeft)
                    {
                        Interlocked.Increment(ref leftRetinalFramesAccepted);
                    }
                    else
                    {
                        Interlocked.Increment(ref rightRetinalFramesAccepted);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                SetBrainStatus($"Visual input delayed ({error.GetType().Name})");
            }
        }
    }

    private async Task AudioFrameLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(options.EffectiveAudioFrameInterval);
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            if (!ShouldSendSensoryFrames())
            {
                continue;
            }

            try
            {
                var frame = RenderAudioFrame();
                var result = await AvatarControlApi.PostCochlearFrameAsync(
                    audioClient,
                    options.ControlEndpoint,
                    frame,
                    AvatarRuntimeDefaults.UnifiedAudioInputSource,
                    token).ConfigureAwait(false);
                if (result.Accepted && result.TargetInstances > 0)
                {
                    Interlocked.Increment(ref cochlearFramesAccepted);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                SetBrainStatus($"Auditory input delayed ({error.GetType().Name})");
            }
        }
    }

    private void AdvanceWorld(double dt)
    {
        lock (gate)
        {
            if (!running)
            {
                return;
            }

            worldTick++;
            elapsedSeconds += dt;
            collisionPulse = Math.Max(0.0, collisionPulse - (dt * 2.4));
            var inShelter = IsInShelterCore();
            var physiologyBeforeMetabolism = physiology;
            physiology = AvatarWorldDynamics.AdvancePhysiology(
                physiology,
                PhysiologyOptions,
                dt,
                metabolicRateScale: options.MotorTrainingMode ? 0.0 : 1.0,
                sleeping: neuronalSleep,
                inShelter: inShelter);
            ObserveMetabolicTissueDamageCore(physiologyBeforeMetabolism, physiology);
            var assessment = AvatarWorldDynamics.AssessVitalState(physiology, PhysiologyOptions);
            if (assessment.State != vitalState)
            {
                vitalState = assessment.State;
                vitalStateSinceSeconds = elapsedSeconds;
            }

            if (assessment.State == AvatarVitalState.Dead)
            {
                physicalDeaths++;
                runTelemetry.ObserveDeath(CreateDeathRunEventCore());
                pendingCriticalBodyFrames.Enqueue(CreatePhysicalBodyFrameCore(
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    acceptedArticulation));
                ResetBodyCore();
                return;
            }

            var action = avatarService.PublishActionOutput();
            var motorSignal = avatarService.LatestSignal;
            var desiredForwardSpeed = action.Movement.ForwardSpeed * assessment.MotorCapacity;
            var desiredTurnRate = action.Movement.TurnRateDeg * assessment.MotorCapacity;
            var previousRoot = new Vector3((float)avatarX, (float)avatarY, (float)avatarZ);
            var previousHeadingDegrees = (float)avatarHeadingDegrees;
            var previousArticulation = acceptedArticulation;
            var mechanics = articulatedBody.Advance(
                dt,
                NormalizeMotorRecruitment(motorSignal.LeftMotorDrive),
                NormalizeMotorRecruitment(motorSignal.RightMotorDrive),
                desiredForwardSpeed,
                desiredTurnRate,
                action.Interaction.ManipulatorDrive,
                avatarGrounded,
                movementBlockedLastTick,
                motorSignal.StandDrive,
                motorSignal.CrouchDrive,
                motorSignal.SitDrive,
                motorSignal.LieDrive,
                motorSignal.HeadYawDrive,
                motorSignal.HeadPitchDrive,
                motorSignal.LeftShoulderSagittalDrive,
                motorSignal.RightShoulderSagittalDrive,
                motorSignal.LeftShoulderCoronalDrive,
                motorSignal.RightShoulderCoronalDrive,
                motorSignal.LeftElbowDrive,
                motorSignal.RightElbowDrive,
                motorSignal.LeftHipCoronalDrive,
                motorSignal.RightHipCoronalDrive,
                motorSignal.LeftAnkleSagittalDrive,
                motorSignal.RightAnkleSagittalDrive,
                motorSignal.LeftAnkleCoronalDrive,
                motorSignal.RightAnkleCoronalDrive,
                motorSignal.TrunkYawDrive);
            var proposedArticulation = articulatedBody.CaptureFrame();
            var planarMotion = AvatarPlanarDynamics.Advance(
                new AvatarPlanarMotionState(lastForwardSpeed, lastTurnRateDegrees),
                mechanics.ForwardSpeedMetersPerSecond,
                mechanics.TurnRateDegreesPerSecond,
                articulatedBody.CurrentPosture,
                avatarGrounded,
                dt);
            var forwardSpeed = planarMotion.ForwardVelocityMetersPerSecond;
            var turnRate = planarMotion.TurnVelocityDegreesPerSecond;
            avatarHeadingDegrees = AvatarKinematics.AdvanceHeading(avatarHeadingDegrees, turnRate, dt);
            var proposedHeadingDegrees = (float)avatarHeadingDegrees;
            var (directionX, directionZ) = AvatarKinematics.ForwardDirection(avatarHeadingDegrees);
            var previousX = avatarX;
            var previousZ = avatarZ;
            var proposedX = avatarX + (directionX * forwardSpeed * dt);
            var proposedZ = avatarZ + (directionZ * forwardSpeed * dt);
            var blockedByWater = !terrainAscent.IsActive &&
                terrain.IsInside(proposedX, proposedZ) &&
                terrain.IsWater(proposedX, proposedZ);
            var candidateRoot = blockedByWater || terrainAscent.IsActive
                ? previousRoot
                : new Vector3((float)proposedX, (float)avatarY, (float)proposedZ);
            var physicsResolution = physicsScene?.ResolveAvatar(
                previousRoot,
                previousHeadingDegrees,
                previousArticulation,
                candidateRoot,
                proposedHeadingDegrees,
                proposedArticulation,
                (float)dt) ?? new AvatarPhysicsResolution(
                    candidateRoot,
                    proposedHeadingDegrees,
                    proposedArticulation,
                    1f,
                    1f,
                    1f,
                    [],
                    false,
                    false,
                    []);

            var initialRootMotionConstrained = physicsResolution.RootMotionConstrained;
            var resolvedContacts = physicsResolution.Contacts.ToList();
            var ascentReadiness = CreateTerrainAscentReadiness(
                motorSignal,
                mechanics,
                proposedArticulation,
                resolvedContacts);
            if (!terrainAscent.IsActive &&
                !blockedByWater &&
                forwardSpeed > 0.015 &&
                initialRootMotionConstrained &&
                terrain.TryProbeRise(
                    previousRoot.X,
                    previousRoot.Z,
                    directionX,
                    directionZ,
                    maximumDistance: 0.85,
                    out var terrainRise))
            {
                terrainAscent.TryBegin(physicsResolution.RootPosition, terrainRise, ascentReadiness);
            }

            var ascentMoved = false;
            if (terrainAscent.IsActive)
            {
                var proposal = terrainAscent.Propose(dt, ascentReadiness);
                if (proposal.Active && physicsScene is not null)
                {
                    var beforeAscent = physicsResolution.RootPosition;
                    var ascentResolution = physicsScene.ResolveAvatar(
                        beforeAscent,
                        physicsResolution.HeadingDegrees,
                        physicsResolution.Articulation,
                        proposal.RootPosition,
                        physicsResolution.HeadingDegrees,
                        physicsResolution.Articulation,
                        (float)dt);
                    terrainAscent.Commit(proposal, ascentResolution.RootPosition, dt);
                    resolvedContacts = MergePhysicsContacts(resolvedContacts, ascentResolution.Contacts);
                    physicsResolution = ascentResolution;
                    ascentMoved = Vector3.DistanceSquared(beforeAscent, ascentResolution.RootPosition) > 0.000001f;
                }
            }

            avatarX = physicsResolution.RootPosition.X;
            avatarY = physicsResolution.RootPosition.Y;
            avatarZ = physicsResolution.RootPosition.Z;
            avatarHeadingDegrees = physicsResolution.HeadingDegrees;
            acceptedArticulation = physicsResolution.Articulation;
            articulatedBody.ReconcileResolvedFrame(
                previousArticulation,
                proposedArticulation,
                acceptedArticulation,
                dt);
            var movedDistance = Math.Sqrt(
                ((avatarX - previousX) * (avatarX - previousX)) +
                ((avatarZ - previousZ) * (avatarZ - previousZ)));
            distanceTravelled += movedDistance;
            var attemptedRootMotion = Math.Abs(proposedX - previousX) + Math.Abs(proposedZ - previousZ) > 0.00001;
            var rootMotionConstrained = blockedByWater ||
                                        (attemptedRootMotion && initialRootMotionConstrained && !ascentMoved);
            if ((rootMotionConstrained || resolvedContacts.Count > 0) && Math.Abs(forwardSpeed) > 0.001)
            {
                collisionHits++;
            }

            foreach (var contact in resolvedContacts)
            {
                articulatedBody.ApplyExternalContact(new AvatarExternalBodyContact(
                    contact.Region,
                    contact.BodyPosition,
                    contact.BodyNormal,
                    contact.ForceNewtons,
                    contact.ImpulseNewtonSeconds,
                    contact.ContactAreaSquareMillimeters));

                if (contact.Region is "left_hand" or "right_hand")
                {
                    articulatedBody.ApplyHandContact(contact.Region, contact.ForceNewtons);
                }
            }

            if (terrainAscent.IsActive)
            {
                avatarVerticalVelocity = 0.0;
                avatarGrounded = false;
            }
            else
            {
                ApplyVerticalPhysicsCore(dt);
            }
            visitedCells.Add(terrain.CellKey(avatarX, avatarZ));
            lastForwardSpeed = ascentMoved
                ? movedDistance / Math.Max(0.001, dt)
                : blockedByWater
                ? 0.0
                : forwardSpeed * physicsResolution.RootProgressFraction;
            lastTurnRateDegrees = turnRate * physicsResolution.HeadingProgressFraction;
            movementBlockedLastTick = rootMotionConstrained;
            RefreshBodyContactsCore(resolvedContacts, dt * 1_000.0);
            ApplyPhysicalContactDamageCore(dt);
            ApplyWaterContactCore(blockedByWater || terrain.IsWater(avatarX, avatarZ));
            AdvanceHandInteractionsCore(action.Interaction, dt);
            var contactAdjustedArticulation = articulatedBody.CaptureFrame();
            acceptedArticulation = acceptedArticulation with
            {
                LeftHandLoadNewtons = Math.Max(
                    acceptedArticulation.LeftHandLoadNewtons,
                    contactAdjustedArticulation.LeftHandLoadNewtons),
                RightHandLoadNewtons = Math.Max(
                    acceptedArticulation.RightHandLoadNewtons,
                    contactAdjustedArticulation.RightHandLoadNewtons),
                LeftHandApertureFraction = (float)leftHandPlant.State.ApertureFraction,
                RightHandApertureFraction = (float)rightHandPlant.State.ApertureFraction,
                LeftGripForceNewtons = (float)leftHandPlant.State.GripForceNewtons,
                RightGripForceNewtons = (float)rightHandPlant.State.GripForceNewtons,
                LeftHandFatigue = (float)leftHandPlant.State.FatigueFraction,
                RightHandFatigue = (float)rightHandPlant.State.FatigueFraction,
                LeftHandSlip = (float)leftHandPlant.State.SlipFraction,
                RightHandSlip = (float)rightHandPlant.State.SlipFraction
            };
            runTelemetry.Observe(
                dt,
                acceptedArticulation.Musculoskeletal?.Balance,
                acceptedArticulation,
                motorSignal,
                activeBodyContacts
                    .Select(static contact => new WorldRunContactObservation(
                        contact.InputSource,
                        contact.Region,
                        contact.ForceNewtons,
                        contact.ImpulseNewtonSeconds,
                        contact.ForceNewtons * Math.Max(0.0, contact.NormalY)))
                    .Concat(activeFallbackHandContacts.Values)
                    .ToArray(),
                latestSpinalWithdrawalDrive,
                latestSpinalWithdrawalSources);
            AdvancePredatorsCore(dt);
        }
    }

    private static AvatarTerrainAscentReadiness CreateTerrainAscentReadiness(
        AvatarNervousSystemSignal motorSignal,
        AvatarMechanicalOutput mechanics,
        PhysicalArticulationFrame articulation,
        IReadOnlyList<AvatarPhysicsContact> contacts)
    {
        var muscles = articulation.Musculoskeletal?.Muscles ?? [];
        var legEffort = MaximumMuscleActivation(muscles, arm: false);
        var armEffort = MaximumMuscleActivation(muscles, arm: true);
        var forwardEffort = Math.Clamp(
            (NormalizeMotorRecruitment(motorSignal.LeftMotorDrive) +
             NormalizeMotorRecruitment(motorSignal.RightMotorDrive)) * 0.5,
            0.0,
            1.0);
        var leftHandSupported = articulation.LeftHandLoadNewtons >= 12f ||
            contacts.Any(static contact => contact.Region == "left_hand" && contact.ForceNewtons >= 12f);
        var rightHandSupported = articulation.RightHandLoadNewtons >= 12f ||
            contacts.Any(static contact => contact.Region == "right_hand" && contact.ForceNewtons >= 12f);

        return new AvatarTerrainAscentReadiness(
            forwardEffort,
            legEffort,
            armEffort,
            Math.Clamp(motorSignal.ManipulatorDrive, 0.0, 1.0),
            mechanics.UprightFraction,
            mechanics.SupportFraction,
            leftHandSupported,
            rightHandSupported);
    }

    internal static double NormalizeMotorRecruitment(double accumulatedDrive)
    {
        if (!double.IsFinite(accumulatedDrive))
        {
            return 0.0;
        }

        // MaxMotorDrive is an accumulator safety ceiling, not the motor pool's
        // physiological full-recruitment point. Calibrating the muscle plant to
        // the sustained population envelope preserves low-speed translation
        // while giving spinal gait enough excursion to lift a swing foot.
        return Math.Clamp(
            accumulatedDrive / NominalFullRecruitmentMotorDrive,
            -1.0,
            1.0);
    }

    private static double MaximumMuscleActivation(
        IReadOnlyList<PhysicalMuscleMeasurement> muscles,
        bool arm)
    {
        var maximum = 0.0;
        foreach (var muscle in muscles)
        {
            var included = arm
                ? muscle.Name is "AnteriorDeltoid" or "LatissimusDorsi" or "MiddleDeltoid" or
                    "PectoralisMajor" or "BicepsBrachii" or "TricepsBrachii"
                : muscle.Name is "Iliopsoas" or "GluteusMaximus" or "Hamstrings" or "Quadriceps" or
                    "TibialisAnterior" or "GastrocnemiusSoleus";
            if (included)
            {
                maximum = Math.Max(maximum, muscle.Activation);
            }
        }

        return maximum;
    }

    private static List<AvatarPhysicsContact> MergePhysicsContacts(
        IEnumerable<AvatarPhysicsContact> first,
        IEnumerable<AvatarPhysicsContact> second)
        => first
            .Concat(second)
            .GroupBy(static contact => contact.InputSource, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(static contact => contact.ForceNewtons).First())
            .ToList();

    private void ApplyVerticalPhysicsCore(double dt)
    {
        var supportY = terrain.SurfaceAt(avatarX, avatarZ) + AvatarFootClearance;
        if (supportY > avatarY)
        {
            avatarY = supportY;
            avatarVerticalVelocity = 0.0;
            avatarGrounded = true;
            return;
        }

        if (avatarGrounded && avatarY - supportY <= 0.01)
        {
            avatarY = supportY;
            avatarVerticalVelocity = 0.0;
            return;
        }

        avatarGrounded = false;
        avatarVerticalVelocity = Math.Max(
            avatarVerticalVelocity - (GravityMetersPerSecondSquared * dt),
            -TerminalVelocityMetersPerSecond);
        avatarY += avatarVerticalVelocity * dt;
        if (avatarY > supportY)
        {
            return;
        }

        var impactSpeed = Math.Abs(avatarVerticalVelocity);
        avatarY = supportY;
        avatarVerticalVelocity = 0.0;
        avatarGrounded = true;
        if (impactSpeed > 1.2)
        {
            collisionPulse = Math.Max(collisionPulse, Math.Clamp(impactSpeed / 12.0, 0.1, 1.0));
            collisionBodyPositionX = 0.0;
            collisionBodyPositionY = -0.90;
            collisionBodyPositionZ = 0.0;
            collisionNormalX = 0.0;
            collisionNormalY = 1.0;
            collisionNormalZ = 0.0;
        }
    }

    private void RefreshBodyContactsCore(
        IReadOnlyList<AvatarPhysicsContact>? collisionContacts,
        double elapsedMilliseconds)
    {
        activeBodyContacts.Clear();
        var activeSources = new HashSet<string>(StringComparer.Ordinal);
        if (avatarGrounded)
        {
            foreach (var contact in articulatedBody.CaptureGroundContacts())
            {
                AddGroundContact(
                    contact.Region,
                    contact.BodyX,
                    contact.BodyY,
                    contact.BodyZ,
                    contact.LoadNewtons,
                    contact.AreaSquareMillimeters,
                    elapsedMilliseconds,
                    activeSources);
            }
        }

        if (collisionContacts is not null)
        {
            foreach (var collision in collisionContacts)
            {
                var duration = ContinueContactDuration(
                    collision.InputSource,
                    elapsedMilliseconds,
                    activeSources);
                activeBodyContacts.Add(new BodyContactSample(
                    collision.Region,
                    collision.BodyPosition.X,
                    collision.BodyPosition.Y,
                    collision.BodyPosition.Z,
                    collision.BodyNormal.X,
                    collision.BodyNormal.Y,
                    collision.BodyNormal.Z,
                    collision.ForceNewtons,
                    collision.ImpulseNewtonSeconds,
                    collision.PenetrationMeters,
                    collision.TangentialSpeedMetersPerSecond,
                    collision.ContactAreaSquareMillimeters,
                    duration,
                    collision.InputSource));
            }
        }

        contactDurationMilliseconds.Keys
            .Where(source => !activeSources.Contains(source))
            .ToList()
            .ForEach(source => contactDurationMilliseconds.Remove(source));
    }

    private void AddGroundContact(
        string region,
        double bodyX,
        double bodyY,
        double bodyZ,
        double forceNewtons,
        double areaSquareMillimeters,
        double elapsedMilliseconds,
        HashSet<string> activeSources)
    {
        if (forceNewtons < 0.5)
        {
            return;
        }

        var source = $"avatar_world_{region}_support";
        var duration = ContinueContactDuration(source, elapsedMilliseconds, activeSources);
        activeBodyContacts.Add(new BodyContactSample(
            region,
            bodyX,
            bodyY,
            bodyZ,
            0.0,
            1.0,
            0.0,
            forceNewtons,
            forceNewtons * options.EffectiveBodyFrameInterval.TotalSeconds,
            0.0012,
            Math.Abs(lastForwardSpeed),
            areaSquareMillimeters,
            duration,
            source));
    }

    private void ApplyPhysicalContactDamageCore(double elapsedSecondsForSample)
    {
        foreach (var contact in activeBodyContacts)
        {
            var assessment = AvatarWorldDynamics.ApplyPhysicalContact(
                physiology,
                new AvatarPhysicalContactExposure(
                    contact.Region,
                    contact.ForceNewtons,
                    contact.ImpulseNewtonSeconds,
                    contact.ContactAreaSquareMillimeters,
                    contact.DurationMilliseconds / 1_000.0,
                    elapsedSecondsForSample));
            physiology = assessment.State;
            if (assessment.DamageFraction <= 0.0)
            {
                continue;
            }

            if (assessment.ImpactDamageFraction > 0.0)
            {
                ObserveTissueDamageCore(
                    $"contact_impact:{NormalizeDamageRegion(contact.Region)}",
                    assessment.ImpactDamageFraction);
            }
            if (assessment.SustainedPressureDamageFraction > 0.0)
            {
                ObserveTissueDamageCore(
                    $"sustained_pressure:{NormalizeDamageRegion(contact.Region)}",
                    assessment.SustainedPressureDamageFraction);
            }

            physicalContactTissueDamageFraction += assessment.DamageFraction;
            physicalContactDamageByRegion[contact.Region] =
                physicalContactDamageByRegion.GetValueOrDefault(contact.Region) + assessment.DamageFraction;
            if (assessment.ImpactEvent)
            {
                physicalImpactDamageEvents++;
            }
            if (assessment.SustainedPressureEpisodeBegan)
            {
                sustainedPressureDamageEpisodes++;
            }
        }
    }

    private double ContinueContactDuration(
        string source,
        double elapsedMilliseconds,
        HashSet<string> activeSources)
    {
        activeSources.Add(source);
        var duration = contactDurationMilliseconds.GetValueOrDefault(source) +
                       Math.Max(0.0, elapsedMilliseconds);
        duration = Math.Min(duration, 60_000.0);
        contactDurationMilliseconds[source] = duration;
        return duration;
    }

    private void SetCollisionContactFromWorldDirection(
        double worldDirectionX,
        double worldDirectionZ,
        double bodyPositionY)
    {
        var local = BodyLocalDirection(worldDirectionX, worldDirectionZ);
        var magnitude = Math.Sqrt((local.X * local.X) + (local.Z * local.Z));
        if (magnitude <= 0.0001)
        {
            return;
        }

        var unitX = local.X / magnitude;
        var unitZ = local.Z / magnitude;
        collisionBodyPositionX = unitX * 0.45;
        collisionBodyPositionY = bodyPositionY;
        collisionBodyPositionZ = unitZ * 0.45;
        collisionNormalX = -unitX;
        collisionNormalY = 0.0;
        collisionNormalZ = -unitZ;
    }

    private (double X, double Z) BodyLocalDirection(double worldDirectionX, double worldDirectionZ)
    {
        var headingRadians = AvatarKinematics.DegreesToRadians(avatarHeadingDegrees);
        var rightX = Math.Cos(headingRadians);
        var rightZ = -Math.Sin(headingRadians);
        var forwardX = Math.Sin(headingRadians);
        var forwardZ = Math.Cos(headingRadians);
        return (
            (worldDirectionX * rightX) + (worldDirectionZ * rightZ),
            (worldDirectionX * forwardX) + (worldDirectionZ * forwardZ));
    }

    private void AddHandContactFrame(
        List<SomaticContactFrameRequest> contacts,
        long timestampMs,
        float loadNewtons,
        float bodyPositionX,
        string region)
    {
        if (loadNewtons < 0.5f)
        {
            fallbackHandContactDurationMilliseconds.Remove(region);
            activeFallbackHandContacts.Remove(region);
            return;
        }

        var durationMilliseconds = Math.Min(
            fallbackHandContactDurationMilliseconds.GetValueOrDefault(region) +
            options.EffectiveBodyFrameInterval.TotalMilliseconds,
            60_000.0);
        fallbackHandContactDurationMilliseconds[region] = durationMilliseconds;
        var impulseNewtonSeconds = CalculateFrameImpulseNewtonSeconds(
            loadNewtons,
            options.EffectiveBodyFrameInterval);
        var source = $"avatar_world_{region}_load";
        contacts.Add(new SomaticContactFrameRequest(
            Interlocked.Increment(ref somaticSequence),
            timestampMs,
            bodyPositionX,
            0.34f,
            0.58f,
            0f,
            0f,
            -1f,
            loadNewtons,
            impulseNewtonSeconds,
            0.8f,
            0f,
            480f,
            (float)durationMilliseconds,
            source));
        activeFallbackHandContacts[region] = new WorldRunContactObservation(
            source,
            region,
            loadNewtons,
            impulseNewtonSeconds,
            VerticalSupportNewtons: 0.0);
    }

    internal static float CalculateFrameImpulseNewtonSeconds(float forceNewtons, TimeSpan frameInterval)
    {
        if (!float.IsFinite(forceNewtons) || forceNewtons <= 0f ||
            !double.IsFinite(frameInterval.TotalSeconds) || frameInterval <= TimeSpan.Zero)
        {
            return 0f;
        }

        return (float)Math.Clamp(forceNewtons * frameInterval.TotalSeconds, 0.0, 1_000.0);
    }

    private void AdvanceHandInteractionsCore(AvatarInteractionOutput interaction, double deltaSeconds)
    {
        var colliders = AvatarColliderRig.CaptureResolved(acceptedArticulation);
        AdvanceHandInteractionCore(
            left: true,
            interaction.LeftHandGraspDrive,
            colliders.First(static collider => collider.Region == "left_hand"),
            deltaSeconds);
        AdvanceHandInteractionCore(
            left: false,
            interaction.RightHandGraspDrive,
            colliders.First(static collider => collider.Region == "right_hand"),
            deltaSeconds);
    }

    private void AdvanceHandInteractionCore(
        bool left,
        double signedDrive,
        AvatarBodyCollider handCollider,
        double deltaSeconds)
    {
        var plant = left ? leftHandPlant : rightHandPlant;
        var heldTarget = left ? leftHeldTarget : rightHeldTarget;
        var handPosition = ToWorldPosition(handCollider.Position);
        var target = heldTarget ?? FindHandContactTarget(handPosition, left);
        var targetContact = target is not null &&
            Vector3.Distance(handPosition, EntityPosition(target)) <= HandTargetContactRadiusMeters;
        var targetOccluded = target is not null && !targetContact && IsHandTargetOccluded(handPosition, target);
        var targetDistance = target is null
            ? double.PositiveInfinity
            : Vector3.Distance(handPosition, EntityPosition(target));
        var reachObserved = targetDistance <= HandReachObservationRadiusMeters && !targetOccluded;
        var wasReachObserved = left ? leftReachObserved : rightReachObserved;
        if (reachObserved && !wasReachObserved)
        {
            reachEntries++;
        }
        if (left)
        {
            leftReachObserved = reachObserved;
        }
        else
        {
            rightReachObserved = reachObserved;
        }

        var requiredGrip = heldTarget is null ? RequiredGripForce(target) : RequiredGripForce(heldTarget);
        var previousState = plant.State;
        var output = plant.Advance(deltaSeconds, new AvatarHandPlantInput(
            signedDrive,
            targetContact || heldTarget is not null,
            heldTarget is not null,
            requiredGrip));

        if (targetContact && previousState.Phase != AvatarHandPhase.Contact && heldTarget is null)
        {
            handContacts++;
            lastInteractionOutcome = $"{(left ? "left" : "right")} hand physical contact";
        }
        if (targetOccluded && signedDrive > 0.08)
        {
            interactionOccluded++;
            lastInteractionOutcome = $"{(left ? "left" : "right")} hand target occluded";
        }

        if (output.GraspAcquired && target is not null && heldTarget is null)
        {
            heldTarget = target;
            grasps++;
            interactionAttempts++;
            lastInteractionOutcome = $"{(left ? "left" : "right")} hand grasp acquired";
        }
        else if (signedDrive > 0.65 && target is null &&
                 previousState.Phase != AvatarHandPhase.Closing &&
                 previousState.Phase != AvatarHandPhase.Contact)
        {
            interactionAttempts++;
            interactionOutOfReach++;
            graspMisses++;
            lastInteractionOutcome = $"{(left ? "left" : "right")} hand closed without contact";
        }

        if (heldTarget is not null)
        {
            heldTarget.X = handPosition.X;
            heldTarget.Y = handPosition.Y;
            heldTarget.Z = handPosition.Z;
            holds++;
            if (output.Released)
            {
                releases++;
                if (output.FatigueRelease)
                {
                    fatigueReleases++;
                }
                lastInteractionOutcome = output.FatigueRelease
                    ? $"{(left ? "left" : "right")} hand fatigue release"
                    : $"{(left ? "left" : "right")} hand opened and released";
                heldTarget = null;
            }
            else if (TryCompleteHeldInteraction(heldTarget, output.PhaseDurationSeconds))
            {
                heldTarget = null;
            }
        }

        if (left)
        {
            leftHeldTarget = heldTarget;
        }
        else
        {
            rightHeldTarget = heldTarget;
        }
    }

    private MutableEntity? FindHandContactTarget(Vector3 handPosition, bool left)
    {
        var otherHeld = left ? rightHeldTarget : leftHeldTarget;
        return foods.Concat(devices)
            .Where(target => !ReferenceEquals(target, otherHeld))
            .OrderBy(target => Vector3.DistanceSquared(handPosition, EntityPosition(target)))
            .FirstOrDefault(target =>
                Vector3.Distance(handPosition, EntityPosition(target)) <= HandReachObservationRadiusMeters);
    }

    private bool TryCompleteHeldInteraction(MutableEntity target, double holdSeconds)
    {
        if (target.Kind == "food" && holdSeconds >= StableFoodHoldSeconds)
        {
            foods.Remove(target);
            physiology = AvatarWorldDynamics.ConsumeFood(physiology, PhysiologyOptions, 0.16);
            foodConsumed++;
            interactionSuccesses++;
            lastInteractionOutcome = "food physically grasped and consumed";
            return true;
        }
        if (target.Kind == "device" && holdSeconds >= StableDeviceHoldSeconds)
        {
            var profile = string.Equals(target.Variant, "Long", StringComparison.OrdinalIgnoreCase)
                ? AvatarDeviceRangeProfile.Long
                : AvatarDeviceRangeProfile.Short;
            if (inventory.TryCollect(profile, capacity: 8, out var next))
            {
                inventory = next;
                devices.Remove(target);
                devicePickupsCollected++;
                interactionSuccesses++;
                lastInteractionOutcome = "device physically grasped and collected";
                return true;
            }

            interactionUnavailable++;
            lastInteractionOutcome = "device capacity reached";
        }
        return false;
    }

    private Vector3 ToWorldPosition(Vector3 bodyPosition)
    {
        var headingRadians = AvatarKinematics.DegreesToRadians(avatarHeadingDegrees);
        var sin = Math.Sin(headingRadians);
        var cos = Math.Cos(headingRadians);
        return new Vector3(
            (float)(avatarX + (bodyPosition.X * cos) + (bodyPosition.Z * sin)),
            (float)(avatarY + bodyPosition.Y),
            (float)(avatarZ - (bodyPosition.X * sin) + (bodyPosition.Z * cos)));
    }

    private static Vector3 EntityPosition(MutableEntity entity) =>
        new((float)entity.X, (float)entity.Y, (float)entity.Z);

    private static double RequiredGripForce(MutableEntity? target) => target?.Kind switch
    {
        "food" => 6.0,
        "device" => 16.0,
        _ => 4.0
    };

    private bool IsHandTargetOccluded(Vector3 handPosition, MutableEntity target)
    {
        var targetPosition = EntityPosition(target);
        for (var sample = 1; sample < 8; sample++)
        {
            var fraction = sample / 8f;
            var point = Vector3.Lerp(handPosition, targetPosition, fraction);
            if (terrain.SurfaceAt(point.X, point.Z) + 0.025 > point.Y)
            {
                return true;
            }
        }
        return false;
    }

    private void AdvancePredatorsCore(double dt)
    {
        if (!options.PredatorsEnabled)
        {
            predators.Clear();
            return;
        }

        foreach (var predator in predators)
        {
            var dx = avatarX - predator.X;
            var dz = avatarZ - predator.Z;
            var distance = Math.Sqrt((dx * dx) + (dz * dz));
            if (distance is > 0.001 and <= PredatorSenseRadius)
            {
                predator.HeadingDegrees = AvatarKinematics.NormalizeDegrees(
                    Math.Atan2(dx, dz) * 180.0 / Math.PI);
                var speed = 0.85;
                var nextX = predator.X + ((dx / distance) * speed * dt);
                var nextZ = predator.Z + ((dz / distance) * speed * dt);
                if (terrain.IsInside(nextX, nextZ))
                {
                    predator.X = nextX;
                    predator.Z = nextZ;
                    predator.Y = terrain.SurfaceAt(nextX, nextZ) + 0.2;
                }
            }
            else
            {
                predator.HeadingDegrees = AvatarKinematics.NormalizeDegrees(predator.HeadingDegrees + (dt * 7.0));
            }

            if (distance <= PredatorStrikeRadius)
            {
                var tissueBeforePredatorContact = physiology.TissueIntegrityFraction;
                physiology = AvatarWorldDynamics.ApplyPredatorContact(physiology, dt, 0.035, 1.0);
                ObserveTissueDamageCore(
                    "predator_contact",
                    tissueBeforePredatorContact - physiology.TissueIntegrityFraction);
                collisionPulse = Math.Max(collisionPulse, 0.75);
                SetCollisionContactFromWorldDirection(
                    predator.X - avatarX,
                    predator.Z - avatarZ,
                    bodyPositionY: 0.24);
                lastInteractionOutcome = "predator contact";
            }
        }
    }

    private void ApplyWaterContactCore(bool waterContact)
    {
        if (waterContact && !previousWaterContact)
        {
            physiology = AvatarWorldDynamics.Drink(physiology, 0.06);
            waterInteractions++;
            lastInteractionOutcome = "water edge contact";
        }
        previousWaterContact = waterContact;
    }

    private (AvatarSightFrame Left, AvatarSightFrame Right) RenderBinocularSightFrames()
    {
        double x;
        double z;
        double heading;
        List<MutableEntity> visibleEntities;
        lock (gate)
        {
            x = avatarX;
            z = avatarZ;
            heading = avatarHeadingDegrees;
            visibleEntities = [.. foods, .. devices, .. predators];
        }

        var eyePose = AvatarBinocularVision.ComputeEyePose(x, z, heading);
        var generation = Interlocked.Increment(ref sightGeneration);
        var capturedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return (
            RenderSightFrame(
                eyePose.Left.X,
                eyePose.Left.Z,
                eyePose.Left.HeadingDegrees,
                visibleEntities,
                generation,
                capturedAt),
            RenderSightFrame(
                eyePose.Right.X,
                eyePose.Right.Z,
                eyePose.Right.HeadingDegrees,
                visibleEntities,
                generation,
                capturedAt));
    }

    private AvatarSightFrame RenderSightFrame(
        double x,
        double z,
        double heading,
        IReadOnlyList<MutableEntity> visibleEntities,
        int generation,
        long capturedAt)
    {
        var pixels = new byte[SightWidth * SightHeight * 3];

        for (var row = 0; row < SightHeight; row++)
        {
            var sky = row < SightHeight * 0.46;
            for (var column = 0; column < SightWidth; column++)
            {
                var offset = (row * SightWidth + column) * 3;
                if (sky)
                {
                    pixels[offset] = (byte)(112 + (row * 2));
                    pixels[offset + 1] = (byte)(166 + row);
                    pixels[offset + 2] = 202;
                }
                else
                {
                    var distance = 2.0 + ((row - (SightHeight * 0.46)) * 0.9);
                    var bearing = heading + (((column / (double)(SightWidth - 1)) - 0.5) * 62.0);
                    var (dirX, dirZ) = AvatarKinematics.ForwardDirection(bearing);
                    var sampleX = x + (dirX * distance);
                    var sampleZ = z + (dirZ * distance);
                    var height = terrain.SurfaceAt(sampleX, sampleZ) + WorldTerrain.HalfHeightUnitMeters;
                    var water = terrain.IsWater(sampleX, sampleZ);
                    pixels[offset] = water ? (byte)56 : (byte)(70 + Math.Clamp(height * 4.0, 0.0, 70.0));
                    pixels[offset + 1] = water ? (byte)142 : (byte)(105 + Math.Clamp(height * 6.0, 0.0, 110.0));
                    pixels[offset + 2] = water ? (byte)178 : (byte)68;
                }
            }
        }

        foreach (var entity in visibleEntities)
        {
            PaintEntity(pixels, x, z, heading, entity);
        }

        return new AvatarSightFrame(
            generation,
            capturedAt,
            SightWidth,
            SightHeight,
            SightWidth * 3,
            pixels,
            heading,
            "Rgb24");
    }

    private static void PaintEntity(byte[] pixels, double x, double z, double heading, MutableEntity entity)
    {
        var dx = entity.X - x;
        var dz = entity.Z - z;
        var distance = Math.Sqrt((dx * dx) + (dz * dz));
        if (distance is < 0.15 or > 30.0)
        {
            return;
        }

        var bearing = AvatarKinematics.NormalizeDegrees(Math.Atan2(dx, dz) * 180.0 / Math.PI);
        var relative = ((bearing - heading + 540.0) % 360.0) - 180.0;
        if (Math.Abs(relative) > 31.0)
        {
            return;
        }

        var centerX = (int)Math.Round(((relative / 62.0) + 0.5) * (SightWidth - 1));
        var size = Math.Clamp((int)Math.Round(13.0 / Math.Sqrt(distance)), 1, 8);
        var centerY = (int)Math.Round((SightHeight * 0.56) + (distance * 0.22));
        (byte R, byte G, byte B) color = entity.Kind switch
        {
            "food" => (245, 205, 40),
            "device" => (92, 176, 232),
            "predator" => (124, 54, 35),
            _ => (220, 220, 220)
        };
        for (var py = Math.Max(0, centerY - size); py <= Math.Min(SightHeight - 1, centerY + size); py++)
        {
            for (var px = Math.Max(0, centerX - size); px <= Math.Min(SightWidth - 1, centerX + size); px++)
            {
                var offset = (py * SightWidth + px) * 3;
                pixels[offset] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
            }
        }
    }

    private AvatarAudioFrame RenderAudioFrame()
    {
        double nearestPredator;
        bool water;
        lock (gate)
        {
            nearestPredator = predators.Count == 0 ? 100.0 : predators.Min(DistanceTo);
            water = terrain.IsWater(avatarX, avatarZ);
        }

        var bytes = new byte[AudioSamples * sizeof(short)];
        var sequence = Interlocked.Increment(ref audioSequence);
        var predatorGain = Math.Clamp((PredatorSenseRadius - nearestPredator) / PredatorSenseRadius, 0.0, 1.0);
        for (var sample = 0; sample < AudioSamples; sample++)
        {
            var absoluteSample = (sequence * AudioSamples) + sample;
            var time = absoluteSample / (double)AudioSampleRate;
            var wind = Math.Sin(2.0 * Math.PI * 73.0 * time) * 0.025;
            var waterTone = water ? Math.Sin(2.0 * Math.PI * 180.0 * time) * 0.08 : 0.0;
            var predatorTone = Math.Sin(2.0 * Math.PI * 440.0 * time) * predatorGain * 0.35;
            var value = (short)Math.Clamp((wind + waterTone + predatorTone) * short.MaxValue, short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(sample * sizeof(short), sizeof(short)), value);
        }

        return new AvatarAudioFrame(
            sequence,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            AudioSampleRate,
            Channels: 1,
            SamplesPerChannel: AudioSamples,
            Pcm16Le: bytes);
    }

    private bool ShouldSendSensoryFrames()
    {
        lock (gate)
        {
            return running && lastFrameUtc.HasValue && DateTimeOffset.UtcNow - lastFrameUtc.Value <= BrainFreshness;
        }
    }

    private void ResetCore(int seed)
    {
        physicsScene?.Dispose();
        terrain = new WorldTerrain(seed);
        physicsScene = new WorldPhysicsScene(terrain);
        foods.Clear();
        devices.Clear();
        predators.Clear();
        shelters.Clear();
        pendingCriticalBodyFrames.Clear();
        visitedCells.Clear();
        PopulateEntitiesCore(seed);
        elapsedSeconds = 0.0;
        worldTick = 0;
        distanceTravelled = 0.0;
        avatarX = 0.0;
        avatarZ = 6.0;
        avatarY = terrain.SurfaceAt(avatarX, avatarZ) + AvatarFootClearance;
        avatarVerticalVelocity = 0.0;
        avatarGrounded = true;
        avatarHeadingDegrees = 180.0;
        physiology = AvatarWorldDynamics.CreateRespawnState(PhysiologyOptions);
        vitalState = AvatarVitalState.Viable;
        vitalStateSinceSeconds = 0.0;
        neuronalSleep = false;
        lastForwardSpeed = 0.0;
        lastTurnRateDegrees = 0.0;
        collisionPulse = 0.0;
        collisionBodyPositionX = 0.0;
        collisionBodyPositionY = 0.0;
        collisionBodyPositionZ = 0.45;
        collisionNormalX = 0.0;
        collisionNormalY = 0.0;
        collisionNormalZ = -1.0;
        collisionHits = 0;
        foodConsumed = 0;
        devicePickupsCollected = 0;
        inventory = default;
        waterInteractions = 0;
        predatorsNeutralized = 0;
        physicalContactTissueDamageFraction = 0.0;
        physicalImpactDamageEvents = 0;
        sustainedPressureDamageEpisodes = 0;
        physicalContactDamageByRegion.Clear();
        bodyTissueDamageByCause.Clear();
        interactionAttempts = 0;
        interactionSuccesses = 0;
        interactionOutOfReach = 0;
        interactionOutsideCone = 0;
        interactionOccluded = 0;
        interactionUnavailable = 0;
        lastInteractionOutcome = "world reset";
        leftHeldTarget = null;
        rightHeldTarget = null;
        leftReachObserved = false;
        rightReachObserved = false;
        reachEntries = 0;
        handContacts = 0;
        grasps = 0;
        holds = 0;
        releases = 0;
        graspMisses = 0;
        fatigueReleases = 0;
        leftHandPlant.Reset();
        rightHandPlant.Reset();
        previousWaterContact = terrain.IsWater(avatarX, avatarZ);
        movementBlockedLastTick = false;
        terrainAscent.Reset();
        activeBodyContacts.Clear();
        contactDurationMilliseconds.Clear();
        fallbackHandContactDurationMilliseconds.Clear();
        activeFallbackHandContacts.Clear();
        leftHeldTarget = null;
        rightHeldTarget = null;
        leftReachObserved = false;
        rightReachObserved = false;
        leftHandPlant.Reset();
        rightHandPlant.Reset();
        latestSpinalWithdrawalDrive = 0.0;
        latestSpinalWithdrawalSources = [];
        runTelemetry.Reset();
        articulatedBody.Reset();
        acceptedArticulation = articulatedBody.CaptureFrame();
        RefreshBodyContactsCore(null, 0.0);
        visitedCells.Add(terrain.CellKey(avatarX, avatarZ));
        running = true;
    }

    private void ResetBodyCore()
    {
        avatarX = 0.0;
        avatarZ = 6.0;
        avatarY = terrain.SurfaceAt(avatarX, avatarZ) + AvatarFootClearance;
        avatarVerticalVelocity = 0.0;
        avatarGrounded = true;
        avatarHeadingDegrees = 180.0;
        physiology = AvatarWorldDynamics.CreateRespawnState(PhysiologyOptions);
        vitalState = AvatarVitalState.Viable;
        vitalStateSinceSeconds = elapsedSeconds;
        lastForwardSpeed = 0.0;
        lastTurnRateDegrees = 0.0;
        collisionPulse = 0.0;
        movementBlockedLastTick = false;
        terrainAscent.CancelActive("physical body respawned");
        activeBodyContacts.Clear();
        contactDurationMilliseconds.Clear();
        fallbackHandContactDurationMilliseconds.Clear();
        activeFallbackHandContacts.Clear();
        bodyTissueDamageByCause.Clear();
        articulatedBody.Reset();
        acceptedArticulation = articulatedBody.CaptureFrame();
        RefreshBodyContactsCore(null, 0.0);
        avatarService.PostResetMotor();
        lastInteractionOutcome = "physical body respawned";
    }

    private void ObserveMetabolicTissueDamageCore(
        AvatarPhysiologyState before,
        AvatarPhysiologyState after)
    {
        var damage = before.TissueIntegrityFraction - after.TissueIntegrityFraction;
        if (damage <= 0.0)
        {
            return;
        }

        var energyDepletion = 1.0 -
            (after.StoredEnergyJoules / PhysiologyOptions.NominalStoredEnergyJoules);
        var energyStress = energyDepletion > PhysiologyOptions.EnergyDepletionStressEnter;
        var dehydrationStress = after.HydrationFraction < PhysiologyOptions.DehydrationDamageThreshold;
        var cause = (energyStress, dehydrationStress) switch
        {
            (true, true) => "combined_energy_dehydration_failure",
            (true, false) => "energy_depletion",
            (false, true) => "dehydration",
            _ => "unclassified_physiological_damage"
        };
        ObserveTissueDamageCore(cause, damage);
    }

    private void ObserveTissueDamageCore(string cause, double damageFraction)
    {
        if (string.IsNullOrWhiteSpace(cause) ||
            !double.IsFinite(damageFraction) ||
            damageFraction <= 0.0)
        {
            return;
        }

        bodyTissueDamageByCause[cause] =
            bodyTissueDamageByCause.GetValueOrDefault(cause) + damageFraction;
    }

    private WorldDeathRunEvent CreateDeathRunEventCore()
    {
        var primary = bodyTissueDamageByCause.Count == 0
            ? new KeyValuePair<string, double>("unknown", 0.0)
            : bodyTissueDamageByCause.MaxBy(static pair => pair.Value);
        return new WorldDeathRunEvent(
            worldTick,
            elapsedSeconds,
            primary.Key,
            primary.Value,
            physiology.StoredEnergyJoules,
            physiology.HydrationFraction,
            physiology.TissueIntegrityFraction,
            lastInteractionOutcome,
            new Dictionary<string, double>(bodyTissueDamageByCause, StringComparer.Ordinal));
    }

    private static string NormalizeDamageRegion(string region)
        => string.IsNullOrWhiteSpace(region)
            ? "unknown"
            : region.Trim().ToLowerInvariant().Replace(' ', '_');

    private PhysicalBodyFrameRequest CreatePhysicalBodyFrameCore(
        long timestampMs,
        PhysicalArticulationFrame articulation)
    {
        var balance = articulation.Musculoskeletal?.Balance ?? PhysicalBalanceStateFrame.Neutral;
        return new PhysicalBodyFrameRequest(
            Interlocked.Increment(ref physicalSequence), timestampMs,
            0f, (float)avatarVerticalVelocity, (float)lastForwardSpeed,
            balance.FallPitchVelocityRadiansPerSecond,
            (float)AvatarKinematics.DegreesToRadians(lastTurnRateDegrees),
            balance.FallRollVelocityRadiansPerSecond,
            (float)physiology.StoredEnergyJoules,
            (float)physiology.TissueIntegrityFraction,
            37f, 0.98f, (float)physiology.HydrationFraction,
            AvatarRuntimeDefaults.UnifiedBodyInputSource,
            articulation,
            options.MotorTrainingMode);
    }

    private void PersistRunReport(string reason)
    {
        try
        {
            var path = WorldRunReportStore.WriteAtomic(
                options.EffectiveReportDirectory,
                reason,
                GetSnapshot(),
                runTelemetry.Capture());
            lock (gate)
            {
                lastRunReportPath = path;
                lastRunReportError = null;
            }
        }
        catch (Exception error)
        {
            lock (gate)
            {
                lastRunReportError = $"{error.GetType().Name}: {error.Message}";
            }
        }
    }

    private void PersistRollingRunReportIfDue()
    {
        var now = DateTimeOffset.UtcNow;
        lock (gate)
        {
            if (now - lastRollingReportAttemptUtc < options.EffectiveRollingReportInterval)
            {
                return;
            }

            lastRollingReportAttemptUtc = now;
        }

        try
        {
            var path = WorldRunReportStore.WriteRollingAtomic(
                options.EffectiveReportDirectory,
                GetSnapshot(),
                runTelemetry.Capture());
            lock (gate)
            {
                lastRollingReportPath = path;
                lastRollingReportError = null;
            }
        }
        catch (Exception error)
        {
            lock (gate)
            {
                lastRollingReportError = $"{error.GetType().Name}: {error.Message}";
            }
        }
    }

    internal bool ObserveBrainFrameLatency(TimeSpan elapsed, bool frameSucceeded)
    {
        var pauseForOverload = false;
        lock (gate)
        {
            latestBrainFrameLatencyMilliseconds = Math.Max(0.0, elapsed.TotalMilliseconds);
            if (elapsed < options.EffectiveBrainFrameOverloadThreshold)
            {
                consecutiveSlowBrainFrames = 0;
                return false;
            }

            consecutiveSlowBrainFrames++;
            if (running && consecutiveSlowBrainFrames >= options.ConsecutiveBrainFrameOverloadLimit)
            {
                running = false;
                lastForwardSpeed = 0.0;
                lastTurnRateDegrees = 0.0;
                brainFrameOverloadSafetyPauses++;
                lastBrainFrameOverloadReason =
                    $"{consecutiveSlowBrainFrames} consecutive brain frames at or above " +
                    $"{options.EffectiveBrainFrameOverloadThreshold.TotalMilliseconds:F0} ms; " +
                    $"latest {latestBrainFrameLatencyMilliseconds:F0} ms; success={frameSucceeded}";
                brainStatus = "World safety-paused for sustained brain-frame latency";
                lastInteractionOutcome = "host safety pause: sustained brain-frame latency";
                pauseForOverload = true;
            }
        }

        if (pauseForOverload)
        {
            PersistRunReport("brain-frame-overload-safety-pause");
        }

        return pauseForOverload;
    }

    private void PopulateEntitiesCore(int seed)
    {
        foreach (var site in terrain.ShelterSites)
        {
            shelters.Add(new MutableEntity(
                "shelter",
                site.X,
                terrain.SurfaceAt(site.X, site.Z),
                site.Z,
                0.0,
                null));
        }

        var random = new Mulberry32(unchecked((uint)(seed + 2111)));
        switch (options.DevelopmentStage)
        {
            case WorldDevelopmentStage.HandSpace:
                AddDevelopmentTarget(foods, "food", 0.74, 1.00);
                break;
            case WorldDevelopmentStage.NearFlat:
                AddDevelopmentTarget(foods, "food", 2.8, 0.20);
                break;
            case WorldDevelopmentStage.NormalDistance:
                AddRadialEntities(foods, 4, "food", 4.0, 12.0, random);
                break;
            case WorldDevelopmentStage.Terrain:
                AddRadialEntities(foods, 12, "food", 12.0, 56.0, random);
                AddRadialEntities(devices, 5, "device", 10.0, 48.0, random);
                break;
            case WorldDevelopmentStage.Ecology:
                AddRadialEntities(foods, 12, "food", 12.0, 56.0, random);
                AddRadialEntities(devices, 5, "device", 10.0, 48.0, random);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.DevelopmentStage));
        }
        if (options.PredatorsEnabled && options.DevelopmentStage == WorldDevelopmentStage.Ecology)
        {
            AddRadialEntities(predators, 3, "predator", 24.0, 52.0, random);
        }
    }

    private void AddDevelopmentTarget(
        List<MutableEntity> destination,
        string kind,
        double forwardDistance,
        double heightAboveTerrain)
    {
        var (forwardX, forwardZ) = AvatarKinematics.ForwardDirection(180.0);
        var x = forwardX * forwardDistance;
        var z = 6.0 + (forwardZ * forwardDistance);
        destination.Add(new MutableEntity(
            kind,
            x,
            terrain.SurfaceAt(x, z) + heightAboveTerrain,
            z,
            0.0,
            "Developmental"));
    }

    private void AddRadialEntities(
        List<MutableEntity> destination,
        int count,
        string kind,
        double minimumRadius,
        double maximumRadius,
        Mulberry32 random)
    {
        var added = 0;
        for (var attempt = 0; attempt < count * 20 && added < count; attempt++)
        {
            var angle = random.NextDouble() * Math.PI * 2.0;
            var radius = minimumRadius + (random.NextDouble() * (maximumRadius - minimumRadius));
            var x = Math.Cos(angle) * radius;
            var z = Math.Sin(angle) * radius;
            var heading = random.NextDouble() * 360.0;
            if (terrain.IsInsideShelterClearance(x, z))
            {
                continue;
            }

            destination.Add(new MutableEntity(
                kind,
                x,
                terrain.SurfaceAt(x, z) + 0.2,
                z,
                heading,
                added % 3 == 0 ? "Long" : "Short"));
            added++;
        }
    }

    private bool IsInShelterCore() => shelters.Any(shelter => DistanceTo(shelter) <= ShelterRadius);

    private double DistanceTo(MutableEntity entity)
    {
        var dx = entity.X - avatarX;
        var dz = entity.Z - avatarZ;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    private static IReadOnlyList<WorldEntityStatus> SnapshotEntities(List<MutableEntity> source) =>
        source.Select(entity => new WorldEntityStatus(
            entity.Kind,
            entity.X,
            entity.Y,
            entity.Z,
            entity.HeadingDegrees,
            entity.Variant)).ToArray();

    private void SetBrainStatus(string status)
    {
        lock (gate)
        {
            brainStatus = status;
        }
    }

    private static bool TryGetObject(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object &&
                    string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool ReadSleepState(JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        foreach (var name in new[] { "sleeping", "isSleeping", "sleepState" })
        {
            if (!state.TryGetProperty(name, out var value))
            {
                continue;
            }
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }
            if (value.ValueKind == JsonValueKind.String)
            {
                return string.Equals(value.GetString(), "sleeping", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(value.GetString(), "asleep", StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    private static HttpClient CreateHttpClient(TimeSpan timeout) => new(new SocketsHttpHandler
    {
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(2),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = timeout
    };

    private sealed class MutableEntity(
        string kind,
        double x,
        double y,
        double z,
        double headingDegrees,
        string? variant)
    {
        public string Kind { get; } = kind;
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double Z { get; set; } = z;
        public double HeadingDegrees { get; set; } = headingDegrees;
        public string? Variant { get; } = variant;
    }

    private readonly record struct BodyContactSample(
        string Region,
        double BodyX,
        double BodyY,
        double BodyZ,
        double NormalX,
        double NormalY,
        double NormalZ,
        double ForceNewtons,
        double ImpulseNewtonSeconds,
        double PenetrationMeters,
        double TangentialSpeedMetersPerSecond,
        double ContactAreaSquareMillimeters,
        double DurationMilliseconds,
        string InputSource);

    private sealed class Mulberry32(uint state)
    {
        private uint state = state;

        public double NextDouble()
        {
            state = unchecked(state + 0x6D2B79F5u);
            var result = state;
            result = unchecked((result ^ (result >> 15)) * (result | 1u));
            result ^= unchecked(result + ((result ^ (result >> 7)) * (result | 61u)));
            return (result ^ (result >> 14)) / 4294967296.0;
        }
    }
}
