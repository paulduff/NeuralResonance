using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NRE.WpfWorldSim;

public partial class MainWindow : Window
{
    private const int WorldSize = 132;
    private const double BlockSize = 1.0;
    private const double VoxelVisualScale = 1.0;
    private const int SeaLevel = 3;
    private const int MaxTerrainHeight = 11;
    private const int MinTerrainHeight = 1;
    private const int MountainPeakHeight = 18;
    private const double AvatarRadius = 0.34;
    private const double AvatarStepHeight = 1.45;
    private const double AvatarCollisionHeight = 1.90;
    private const double AvatarFootOffset = 0.03;
    private const double FoodPickupRadius = 0.28;
    private const double WeaponPickupRadius = 0.26;
    private const double ManipulatorReach = 1.20;
    private const double ManipulatorHalfAngleDeg = 72.0;
    private const double ManipulatorActivationDrive = 0.75;
    private const double ManipulatorReleaseDrive = 0.20;
    private const long ManipulatorCycleMs = 420;
    private const double ShortWeaponRange = 2.6;
    private const double LongWeaponRange = 8.5;
    private const double ShortWeaponHalfAngleDeg = 38.0;
    private const double LongWeaponHalfAngleDeg = 16.0;
    private const double PredatorStrikeRadius = 0.65;
    private const double DefaultPredatorSenseRadius = 10.0;
    private const double DefaultShelterRadius = 4.8;
    private const int TrailPointCapacity = 80;
    private const double TrailSampleSeconds = 0.18;
    private const int AvatarPreviewWidth = 96;
    private const int AvatarPreviewHeight = 96;
    private static readonly Int32Rect AvatarPreviewBitmapRect = new(0, 0, AvatarPreviewWidth, AvatarPreviewHeight);
    private const double AvatarVisionEyeHeight = 1.58;
    private const double AvatarVisionEyeForwardOffset = 0.28;
    private const double AvatarVisionHorizontalFovDeg = 62.0;
    private const double AvatarVisionFoodBoxSize = 0.72;
    private const double AvatarVisionWeaponBoxSize = 0.68;
    private const double AvatarVisionPredatorWidth = 1.35;
    private const double AvatarVisionPredatorHeight = 1.10;
    private const double AvatarVisionPredatorLength = 1.85;
    private const int AvatarVisionDispatchTimeoutMs = 3000;
    private const int MaxLogLines = 220;
    private const double SpawnSearchRadiusMin = 1.2;
    private const double SpawnSearchRadiusMax = 16.0;
    private const double SpawnSearchRadiusStep = 0.45;
    private const int SpawnSearchGridDepth = 56;
    private const double SpawnSearchMinClearance = 1.05;
    private const double AvatarVisualYawOffsetDeg = 0.0;
    private const double AvatarHeadReturnRateDeg = 220.0;
    private const double FollowCameraBehindBlocks = 6.0;
    private const double FollowCameraAboveBlocks = 8.0;
    private const double FollowCameraLookAheadBlocks = 2.2;
    private const double SimulationHudUpdateIntervalSeconds = 0.20;
    private const double SurvivalHudUpdateIntervalSeconds = 0.25;
    private const double FollowCameraLookTargetHeightBlocks = 2.4;
    private const double FollowCameraPitchDeg = 10.0;
    private const double FollowCameraYawOffsetDeg = -90.0;
    private const double FollowCameraDistance = 42.0;
    private const int AdditionalShelterHomeCount = 11;
    private const double ShelterHomeSpacingMin = 12.0;
    private const double TelemetryDelayGraceSeconds = 15.0;
    private const int VisionPreviewIntervalMs = 20;
    private const int VisionPreviewMaxLagMs = 250;
    private const int VisionPreviewDropLagMs = 1000;
    private const int VisionBrainInputMaxLagMs = VisionPreviewDropLagMs;
    private const double VisionPreviewEyelidCloseRate = 5.8;
    private const double VisionPreviewEyelidOpenRate = 3.4;
    private const int EnvironmentAudioDispatchTimeoutMs = 6000;
    private const int EnvironmentAudioDispatchIntervalMs = 120;
    private const int OptionalInputOverloadRetryMs = 6000;
    private const int BodyFrameDispatchIntervalMs = 350;
    private const int BodyFrameDispatchTimeoutMs = 1800;
    private const double NominalStoredEnergyJoules = 8_000_000.0;
    private const double MetabolicBurnJoulesPerSecond = 33_600.0;
    private const double HydrationLossPerSecond = 0.00022;
    private const double EnergyDepletionStressEnter = 0.62;
    private const double EnergyDepletionStressFull = 0.92;
    private const double DayNightCycleSeconds = 240.0;
    private const double WorldMaxForwardSpeed = 8.1;

    // Shared physical kinematics for the world avatar: bilateral neuronal drive
    // alone determines speed and turn within these body limits.
    private static readonly AvatarKinematicsOptions WorldKinematicsOptions = new(
        MaxMotorDrive: 240.0,
        ForwardSpeedCoefficient: 0.0128,
        TurnSpeedCoefficient: 3.2,
        MinForwardSpeed: -1.6,
        MaxForwardSpeed: WorldMaxForwardSpeed,
        MaxTurnRateDeg: 240.0,
        AllowSignedMotorDrive: true,
        InPlaceTurnCancelsForwardDrive: true);
    private static readonly AvatarNervousSystemOptions WorldNervousSystemOptions = new(
        WorldKinematicsOptions,
        DriveDecay: 0.92);
    private static readonly AvatarPhysiologyOptions WorldPhysiologyOptions = new(
        NominalStoredEnergyJoules,
        MetabolicBurnJoulesPerSecond,
        HydrationLossPerSecond,
        EnergyDepletionStressEnter,
        EnergyDepletionStressFull,
        EnergyDamageRateMinimum: 0.0028,
        EnergyDamageRateScale: 0.0062,
        DehydrationDamageThreshold: 0.20,
        DehydrationDamageRateMinimum: 0.002,
        DehydrationDamageRateScale: 0.008,
        ShelteredSleepRecoveryRate: 0.010);
    private const long RuntimeLogMaxBytes = 6L * 1024L * 1024L;
    private static readonly string RuntimeLogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NRE.WpfWorldSim");
    private static readonly string RuntimeLogPath = Path.Combine(RuntimeLogDirectory, "worldsim-runtime.log");
    private static readonly string RuntimeLogArchivePath = Path.Combine(RuntimeLogDirectory, "worldsim-runtime.log.1");
    private static readonly string RuntimeStatePath = ResolveRuntimeStatePath();
    private static readonly JsonSerializerOptions RuntimeStateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly IReadOnlyDictionary<long, BlockKind> EmptySurfaceOverrides = new Dictionary<long, BlockKind>();
    private readonly AsyncRuntimeLogWriter _runtimeLogWriter = new(RuntimeLogPath);

    private readonly DispatcherTimer _renderTimer = new(DispatcherPriority.Render)
    {
        Interval = TimeSpan.FromMilliseconds(33)
    };

    private readonly DispatcherTimer _frameTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(90)
    };

    private readonly DispatcherTimer _telemetryTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(950)
    };
    private readonly DispatcherTimer _visionTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(VisionPreviewIntervalMs)
    };

    // Independent clients keep slow telemetry and sensory calls from blocking each other.
    private readonly HttpClient _httpClient = NreHttpClientFactory.Create(
        NreHttpClientOptions.Default with { RequestTimeout = TimeSpan.FromMilliseconds(1800) });
    private readonly HttpClient _sensoryInputHttpClient = NreHttpClientFactory.Create(
        NreHttpClientOptions.Default with { RequestTimeout = TimeSpan.FromMilliseconds(4000) });
    private readonly HttpClient _auditoryInputHttpClient = NreHttpClientFactory.Create(
        NreHttpClientOptions.Default with { RequestTimeout = TimeSpan.FromMilliseconds(9000) });
    private readonly HttpClient _telemetryHttpClient = NreHttpClientFactory.Create(
        NreHttpClientOptions.Default with { RequestTimeout = TimeSpan.FromSeconds(8) });
    private readonly AutoResetEvent _visionRequestSignal = new(false);
    private readonly Thread _visionWorkerThread;
    private readonly AvatarService _avatarService = new(
        WorldNervousSystemOptions,
        "NRE.World.AvatarService",
        new AvatarServiceClockOptions(Enabled: true, TickIntervalMs: 50));

    private readonly List<string> _logLines = [];
    private readonly Model3DGroup _sceneRoot = new();
    private readonly MeshGeometry3D _unitCubeMesh;
    private readonly MeshGeometry3D _brainCoreMesh;
    private readonly MeshGeometry3D _trailPointMesh;
    private readonly MeshGeometry3D _predatorThreatMesh;
    private readonly Dictionary<BlockKind, MaterialGroup> _materials;
    private readonly List<CollisionBox> _collisionBoxes = [];

    // XZ spatial index over _collisionBoxes. Built once after the world is generated
    // (RebuildCollisionGrid). Each cell holds box indices whose AABB overlaps that cell.
    // Replaces the O(N) linear scan in IsCollisionAt with an O(boxes near point) scan.
    // Cell size ~4m: for a 132m world that's ~33x33 = ~1100 cells; typical occupancy 0-4.
    private const double CollisionGridCellSize = 4.0;
    private List<int>[]? _collisionGrid;
    private int _collisionGridDimX;
    private int _collisionGridDimZ;
    private double _collisionGridOriginX;
    private double _collisionGridOriginZ;
    private readonly List<VisionHitBox> _visionHitBoxes = [];
    private readonly List<TranslateTransform3D> _trailPointTransforms = [];
    private readonly Queue<Point3D> _trailPoints = [];
    private readonly Queue<PendingPhysicalContact> _pendingPhysicalContacts = [];
    private readonly Dictionary<long, BlockKind> _surfaceOverrides = [];
    private readonly List<CaveAnchor> _caveAnchors = [];
    private readonly List<FoodPickup> _foodPickups = [];
    private readonly List<WeaponPickup> _weaponPickups = [];
    private readonly List<PredatorNpc> _predators = [];
    private readonly List<ShelterSite> _shelterSites = [];
    private readonly HashSet<int> _visitedTerrainCells = [];
    private readonly object _runtimeLogSync = new();
    private GeometryModel3D?[,]? _terrainColumnModels;
    private GeometryModel3D?[,]? _waterColumnModels;

    private readonly TranslateTransform3D _brainCoreTranslate = new();
    private readonly ScaleTransform3D _brainCoreScale = new(1, 1, 1);
    private readonly SolidColorBrush _brainCoreDiffuseBrush = new(Color.FromRgb(36, 138, 188));
    private readonly SolidColorBrush _brainCoreEmissiveBrush = new(Color.FromArgb(60, 118, 220, 255));
    private readonly SolidColorBrush _avatarDiffuseBrush = new(Color.FromRgb(255, 164, 88));
    private readonly SolidColorBrush _avatarEmissiveBrush = new(Color.FromRgb(142, 70, 30));
    private readonly SolidColorBrush _avatarSecondaryBrush = new(Color.FromRgb(98, 126, 210));
    private readonly SolidColorBrush _avatarFaceBrush = new(Color.FromRgb(20, 26, 38));
    private readonly SolidColorBrush _avatarFaceEmissiveBrush = new(Color.FromArgb(66, 188, 210, 255));
    private readonly SolidColorBrush _avatarDirectionBrush = new(Color.FromRgb(255, 245, 122));
    private readonly SolidColorBrush _avatarDirectionEmissiveBrush = new(Color.FromArgb(120, 255, 245, 140));
    private readonly SolidColorBrush _trailDiffuseBrush = new(Color.FromArgb(170, 96, 188, 255));
    private readonly SolidColorBrush _foodDiffuseBrush = new(Color.FromRgb(255, 222, 92));
    private readonly SolidColorBrush _foodEmissiveBrush = new(Color.FromArgb(90, 255, 230, 120));
    private readonly SolidColorBrush _weaponDiffuseBrush = new(Color.FromRgb(210, 210, 210));
    private readonly SolidColorBrush _weaponEmissiveBrush = new(Color.FromArgb(70, 230, 230, 230));
    private readonly SolidColorBrush _weaponLongDiffuseBrush = new(Color.FromRgb(124, 188, 255));
    private readonly SolidColorBrush _weaponLongEmissiveBrush = new(Color.FromArgb(96, 160, 220, 255));
    private readonly SolidColorBrush _predatorDiffuseBrush = new(Color.FromRgb(78, 47, 28));
    private readonly SolidColorBrush _predatorEmissiveBrush = new(Color.FromArgb(36, 118, 76, 42));
    private readonly SolidColorBrush _predatorMuzzleBrush = new(Color.FromRgb(156, 110, 70));
    private readonly SolidColorBrush _predatorFaceBrush = new(Color.FromRgb(18, 14, 12));
    private readonly SolidColorBrush _predatorEyeBrush = new(Color.FromArgb(210, 255, 210, 118));
    private readonly SolidColorBrush _predatorThreatBrush = new(Color.FromArgb(48, 250, 102, 102));
    private readonly SolidColorBrush _predatorThreatEmissiveBrush = new(Color.FromArgb(28, 255, 150, 150));
    private readonly SolidColorBrush _predatorPathBrush = new(Color.FromArgb(120, 126, 222, 255));
    private readonly SolidColorBrush _skyBrush = new(Color.FromRgb(4, 17, 43));
    private readonly MaterialGroup _transparentMaterial = new();
    private readonly TranslateTransform3D _avatarTranslate = new();
    private readonly AxisAngleRotation3D _avatarYawRotation = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _avatarHeadYawRotation = new(new Vector3D(0, 1, 0), 0);
    private readonly Stopwatch _frameStopwatch = new();
    private AmbientLight? _worldAmbientLight;
    private DirectionalLight? _worldSunLight;
    private DirectionalLight? _worldMoonLight;

    private PerspectiveCamera? _camera;
    private Point3D _cameraTarget = new(0, 3.8, 0);
    private double _cameraYawDeg = -32;
    private double _cameraPitchDeg = -28;
    private double _cameraDistance = 110;
    private bool _dragActive;
    private Point _dragStart;
    private int _seed = 317;
    private int _terrainColumns;
    private int _waterColumns;
    private int _treeClusters;
    private double _habitatBaseY;
    private string _lastEndpointMessage = string.Empty;
    private bool _telemetryInFlight;
    private bool _frameInFlight;
    private int[,]? _heights;
    private double _avatarX;
    private double _avatarY;
    private double _avatarZ;
    private double _avatarHeadingDeg;
    private double _avatarHeadYawDeg;
    private double _leftMotorDrive;
    private double _rightMotorDrive;
    private double _manipulatorDrive;
    private int _lastMotorDispatchCount;
    private int _ticksWithoutMotorDispatch;
    private long _dispatchSinceMs;
    private long _lastNeuronalMotorTick = -1;
    private long _engineServiceNonOkCount;
    private double _engineInputPressure;
    private bool _sleepState;
    private double _storedEnergyJoules = NominalStoredEnergyJoules * 0.75;
    private double _tissueIntegrity = 1.0;
    private double _hydrationFraction = 0.75;
    private double _daylight01 = 1.0;
    private double _darkness01;
    private string _dayNightStage = "day";
    private int _foodConsumed;
    private int _weaponCharges;
    private AvatarDeviceInventory _deviceInventory;
    private int _predatorsNeutralized;
    private int _weaponPickupsCollected;
    private int _waterInteractions;
    private long _interactionAttempts;
    private long _interactionSuccesses;
    private bool _manipulatorLatched;
    private long _lastManipulatorCycleMs;
    private long _lastPredatorContactMs;
    private double _distanceTravelled;
    private long _neuronalMotorDispatchTotal;
    private long _neuronalLocomotorDispatchTotal;
    private long _neuronalManipulatorDispatchTotal;
    private long _retinalFramesAccepted;
    private long _cochlearFramesAccepted;
    private long _physicalBodyFramesAccepted;
    private long _somaticFramesAccepted;
    private int _runtimeStateWriteInFlight;
    private Task? _runtimeStateWriteTask;
    private long _tickFailures;
    private readonly string _runtimeSessionId = Guid.NewGuid().ToString("N");
    private readonly DateTimeOffset _runtimeSessionStartedUtc = DateTimeOffset.UtcNow;
    private int _homesBuilt;
    private int _explorableTerrainCells;
    private double _metabolicBurnRate = 1.0;
    private double _predatorSpeedScale = 1.0;
    private double _predatorSenseRadius = DefaultPredatorSenseRadius;
    private double _shelterRadius = DefaultShelterRadius;
    private int _foodSpawnTarget = 12;
    private int _predatorSpawnTarget = 3;
    private bool _showThreatField = true;
    private bool _showPatrolPaths;
    private double _lastFrameSeconds;
    private double _nextSimulationHudUpdateSeconds;
    private double _nextSurvivalHudUpdateSeconds;
    private double _trailAccumulatorSeconds;
    private double _collisionPulse;
    private int _collisionHits;
    private bool _mapEditorEnabled;
    private WriteableBitmap? _avatarPreviewBitmap;
    private byte[]? _avatarPreviewDisplayPixels;
    private bool _followAvatarCamera = true;
    private bool _sendAvatarVisionToBrain = true;
    private double _avatarPreviewEyelidClosure;
    private long _lastAvatarPreviewEyelidUpdateMs;

    private bool _visionDispatchInFlight;
    private long _lastVisionDispatchMs;
    private readonly AvatarRetryBackoff _visionDispatchBackoff = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CancellationTokenSource _visionComputeCts = new();
    private volatile bool _visionWorkerRunning = true;
    private VisionComputeRequestEnvelope? _pendingVisionComputeRequest;
    private VisionComputeResult? _pendingVisionComputeResult;
    private string _visionComputeWarning = string.Empty;
    private int _visionGeneration;
    private int _textDisplayGeneration;
    private int[,]? _visionHeightsSnapshot;
    private VisionTerrainCell[,]? _visionTerrainCellsSnapshot;
    private VisionHitBox[] _visionHitBoxesSnapshot = [];
    private VisionHitGrid _visionHitGridSnapshot = VisionHitGrid.Empty;
    private IReadOnlyDictionary<long, BlockKind> _visionSurfaceOverridesSnapshot = EmptySurfaceOverrides;
    private bool _visionSceneSnapshotDirty = true;
    private long _lastVisionStaleDropLogMs;
    private int _overrideCells;
    private int _mountainClusters;
    private int _rockClusters;
    private int _caveEntrances;
    private byte[]? _avatarPreviewPixels;
    private bool _logTextInitialized;
    private readonly AvatarWarningGate _visionDispatchWarningGate = new();
    private bool _environmentAudioInFlight;
    private readonly AvatarRetryBackoff _environmentAudioBackoff = new(maxStreak: 8, maxExponent: 7, baseDelayMs: 1000);
    private readonly Dictionary<string, AvatarInputPressureGate> _brainInputPressureGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly AvatarWarningGate _environmentAudioWarningGate = new(minimumIntervalMs: 15000);
    private readonly AvatarWarningGate _optionalBrainInputPressureWarningGate = new(minimumIntervalMs: 8000);
    private string _resolvedEndpoint = ResolveConfiguredControlEndpoint();
    private readonly AvatarWarningGate _endpointValidationWarningGate = new();
    private DateTime _lastTelemetrySuccessUtc = DateTime.MinValue;
    private int _telemetryFailureStreak;
    private int _spawnValidationRetries;
    private int _shelterDoorCorridorClears;
    private double _lastFrontProximity;
    private double _lastLeftProximity;
    private double _lastRightProximity;
    private double _lastForwardSpeed;
    private double _lastTurnRateDeg;
    private long _lastBodyFrameDispatchMs;
    private long _lastEnvironmentAudioDispatchMs;
    private long _environmentAudioFrameSequence;
    private bool _bodyFrameInFlight;
    private long _physicalBodyFrameSequence;
    private long _somaticContactFrameSequence;
    private bool _textDisplayInFlight;
    private string _brainMotorDecisionText = "Motor decision: waiting for brain state.";
    private string _motorPathwayAuditText = "Motor pathway: waiting for brain snapshot.";
    private static readonly MotorPathwayStage[] MotorPathwayStages =
    [
        new("PFC", ["Pfc"]),
        new("ACC", ["Acc"]),
        new("PM", ["PremotorCortex"]),
        new("Str", ["Striatum"]),
        new("STN", ["Stn"]),
        new("GPi/SNr", ["GPi", "Snr"]),
        new("MThal", ["MotorThalamus"]),
        new("SMA", ["Sma"]),
        new("M1", ["M1"]),
        new("DCN", ["DeepCerebellarNuclei"]),
        new("Spinal", ["SpinalCordMotor"])
    ];
    private static readonly IReadOnlyDictionary<string, MotorPathwayStage> MotorPathwayStageLookup = BuildMotorPathwayStageLookup();

    public MainWindow()
    {
        InitializeComponent();
        WorldViewportFrame.Background = _skyBrush;
        ApplyConfiguredEndpointSelection();

        _unitCubeMesh = BuildBoxMesh();
        _brainCoreMesh = BuildSphereMesh(0.9, 16, 12);
        _trailPointMesh = _unitCubeMesh;
        _predatorThreatMesh = BuildSphereMesh(1.0, 14, 10);
        _materials = BuildMaterialLibrary();
        _transparentMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))));

        _renderTimer.Tick += (_, _) => RenderFrame();
        _frameTimer.Tick += async (_, _) => await SafeTickAsync(() => PollFrameAsync(), "frame poll");
        _telemetryTimer.Tick += async (_, _) => await SafeTickAsync(() => PollTelemetryAsync(), "telemetry poll");
        _visionTimer.Tick += (_, _) => UpdateAvatarVisionPreview();

        _visionWorkerThread = new Thread(VisionPreviewWorkerLoop)
        {
            IsBackground = true,
            Name = "NRE.WorldSim.VisionPreviewWorker"
        };
        _visionWorkerThread.Start();

        Loaded += MainWindow_OnLoaded;
        Closed += MainWindow_OnClosed;

        WorldViewport.MouseDown += WorldViewport_OnMouseDown;
        WorldViewport.MouseMove += WorldViewport_OnMouseMove;
        WorldViewport.MouseUp += WorldViewport_OnMouseUp;
        WorldViewport.MouseWheel += WorldViewport_OnMouseWheel;

        Focusable = true;
        Focus();
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeRuntimeLogFile();
        _avatarPreviewBitmap = new WriteableBitmap(AvatarPreviewWidth, AvatarPreviewHeight, 96, 96, PixelFormats.Bgra32, null);
        AvatarPreviewImage.Source = _avatarPreviewBitmap;
        _followAvatarCamera = FollowAvatarCameraCheckBox?.IsChecked ?? _followAvatarCamera;
        _sendAvatarVisionToBrain = SendAvatarVisionCheckBox?.IsChecked ?? _sendAvatarVisionToBrain;
        SyncSurvivalTuningFromUi();
        RefreshSurvivalTuningLabels();
        RebuildWorldFromSeed();
        ResetCamera();
        TextDisplayStatusText.Text = "Text display: idle";
        SetConnectionStatus(AvatarControlStatusText.Connecting(), Brushes.LightGoldenrodYellow, logOnChange: false);
        _frameStopwatch.Restart();
        _lastFrameSeconds = _frameStopwatch.Elapsed.TotalSeconds;
        _renderTimer.Start();
        _frameTimer.Start();
        _telemetryTimer.Start();
        _visionTimer.Start();
        Log("World simulator initialized.");
        Log($"Runtime log file: {RuntimeLogPath}");
        Log($"Runtime state file: {RuntimeStatePath}");
        Log("Voxel habitat generated. This is a persistent environment, not a game loop.");
        Log("Camera: mouse drag to orbit, wheel to zoom.");
        Log("Map editor available: enable terrain paint mode in right panel.");
        Log("Brain frame link active: avatar motor control is sourced from /api/v1/frame polling.");
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _shutdown.Cancel();
        _visionTimer.Stop();
        _frameTimer.Stop();
        _renderTimer.Stop();
        _telemetryTimer.Stop();

        // Signal the vision worker and wait for it to exit BEFORE disposing
        // shared sync primitives the worker may still be touching.
        _visionComputeCts.Cancel();
        _visionWorkerRunning = false;
        _visionRequestSignal.Set();
        try
        {
            if (_visionWorkerThread.IsAlive)
            {
                _visionWorkerThread.Join(TimeSpan.FromSeconds(2));
            }
        }
        catch
        {
            // Best-effort shutdown for background worker thread.
        }

        if (!_visionWorkerThread.IsAlive)
        {
            _visionComputeCts.Dispose();
            _visionRequestSignal.Dispose();
        }
        _avatarPreviewPixels = null;
        _httpClient.Dispose();
        _sensoryInputHttpClient.Dispose();
        _auditoryInputHttpClient.Dispose();
        _telemetryHttpClient.Dispose();
        _avatarService.Dispose();
        try
        {
            _runtimeStateWriteTask?.Wait(TimeSpan.FromSeconds(1));
            WriteRuntimeState(CreateRuntimeStateSnapshot(running: false));
        }
        catch
        {
            // Shutdown must not be blocked by optional qualification telemetry.
        }
        _runtimeLogWriter.Dispose();
        _shutdown.Dispose();
    }

    private void ReconnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        SetConnectionStatus(AvatarControlStatusText.Reconnecting(), Brushes.LightGoldenrodYellow, logOnChange: false);
        _dispatchSinceMs = 0;
        _lastNeuronalMotorTick = -1;
        _avatarService.PostResetMotor();
        ApplyNervousSystemSignal(new AvatarNervousSystemSignal(0.0, 0.0, 0.0, 0, 0, 0));
        _brainMotorDecisionText = "Motor decision: waiting for brain state.";
        _motorPathwayAuditText = "Motor pathway: waiting for brain snapshot.";
        _sleepState = false;
        _collisionHits = 0;
        _collisionPulse = 0.0;
        _spawnValidationRetries = 0;
        _shelterDoorCorridorClears = 0;
        _environmentAudioInFlight = false;
        _lastEnvironmentAudioDispatchMs = 0;
        _environmentAudioBackoff.Reset();
        _environmentAudioWarningGate.Reset();
        ResetBrainInputPressureGates();
        _optionalBrainInputPressureWarningGate.Reset();
        _nextSimulationHudUpdateSeconds = 0.0;
        _nextSurvivalHudUpdateSeconds = 0.0;
        _lastTelemetrySuccessUtc = DateTime.MinValue;
        _telemetryFailureStreak = 0;
        _endpointValidationWarningGate.Reset();
        ResetAvatarPose(logMessage: false);
        _ = PollFrameAsync(forceLogOnFailure: true);
        _ = PollTelemetryAsync(forceLogOnFailure: true);
        Log("Reconnect requested: telemetry + frame stream probes issued.");
    }

    private void ReseedButton_OnClick(object sender, RoutedEventArgs e)
    {
        RebuildWorldFromSeed();
        ResetCamera();
        Log($"World reseeded with {_seed}.");
    }

    private void ResetViewButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetCamera();
        Log("Camera reset.");
    }

    private async void PresentTextButton_OnClick(object sender, RoutedEventArgs e) => await PresentTextToRetinaAsync();

    private async void TextDisplayInputTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await PresentTextToRetinaAsync();
    }

    private async Task PresentTextToRetinaAsync()
    {
        if (_textDisplayInFlight)
        {
            return;
        }

        var visibleText = TextDisplayInputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(visibleText))
        {
            TextDisplayStatusText.Text = "Text display: enter visible text first.";
            return;
        }

        _textDisplayInFlight = true;
        PresentTextButton.IsEnabled = false;
        TextDisplayStatusText.Text = "Text display: presenting pixels to Retina...";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
            var frame = AvatarTextSightRenderer.Render(
                visibleText,
                Interlocked.Increment(ref _textDisplayGeneration),
                Environment.TickCount64);
            var result = await AvatarControlApi.PostRetinalFrameAsync(
                _sensoryInputHttpClient,
                GetSelectedEndpoint(),
                frame,
                AvatarRuntimeDefaults.TypedTextVisualInputSource,
                timeout.Token);

            TextDisplayStatusText.Text = $"Text display: Retina spikes {result.GeneratedSpikes}, targets {result.TargetInstances}";
            Log($"Visible text presented to Retina: \"{TrimForLog(visibleText, 80)}\".");
        }
        catch (Exception ex)
        {
            TextDisplayStatusText.Text = $"Text display: failed ({ex.GetType().Name})";
            Log($"Visible text warning: {ex.GetType().Name}: {TrimForLog(ex.Message, 120)}");
        }
        finally
        {
            PresentTextButton.IsEnabled = true;
            _textDisplayInFlight = false;
        }
    }

    private void MetabolicBurnRateSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _metabolicBurnRate = e.NewValue;
        RefreshSurvivalTuningLabels();
    }

    private void PredatorSpeedSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _predatorSpeedScale = e.NewValue;
        RefreshSurvivalTuningLabels();
    }

    private void PredatorSenseSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _predatorSenseRadius = e.NewValue;
        RefreshSurvivalTuningLabels();
        UpdatePredatorThreatScales();
    }

    private void ShelterRadiusSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _shelterRadius = e.NewValue;
        RefreshSurvivalTuningLabels();
    }

    private void FoodSpawnSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _foodSpawnTarget = (int)Math.Round(e.NewValue);
        RefreshSurvivalTuningLabels();
        RequestWorldRespawn();
    }

    private void PredatorSpawnSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _predatorSpawnTarget = (int)Math.Round(e.NewValue);
        RefreshSurvivalTuningLabels();
        RequestWorldRespawn();
    }

    private void ShowThreatFieldCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        _showThreatField = true;
        UpdateThreatFieldVisibility();
    }

    private void ShowThreatFieldCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _showThreatField = false;
        UpdateThreatFieldVisibility();
    }

    private void ShowPatrolPathsCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        _showPatrolPaths = true;
        UpdatePatrolPathVisibility();
    }

    private void ShowPatrolPathsCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _showPatrolPaths = false;
        UpdatePatrolPathVisibility();
    }

    private void FollowAvatarCameraCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        _followAvatarCamera = true;
        _cameraTarget = new Point3D(_avatarX, _avatarY + 1.30, _avatarZ);
        _cameraYawDeg = NormalizeDegrees(_avatarHeadingDeg + FollowCameraYawOffsetDeg);
        _cameraPitchDeg = FollowCameraPitchDeg;
        _cameraDistance = Math.Max(FollowCameraDistance, WorldSize * 0.72);
        UpdateCamera();
    }

    private void FollowAvatarCameraCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _followAvatarCamera = false;
    }

    private void SendAvatarVisionCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        _sendAvatarVisionToBrain = true;
    }

    private void SendAvatarVisionCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _sendAvatarVisionToBrain = false;
    }

    private void SyncSurvivalTuningFromUi()
    {
        _metabolicBurnRate = MetabolicBurnRateSlider?.Value > 0 ? MetabolicBurnRateSlider.Value : _metabolicBurnRate;
        _predatorSpeedScale = PredatorSpeedSlider?.Value > 0 ? PredatorSpeedSlider.Value : _predatorSpeedScale;
        _predatorSenseRadius = PredatorSenseSlider?.Value > 0 ? PredatorSenseSlider.Value : _predatorSenseRadius;
        _shelterRadius = ShelterRadiusSlider?.Value > 0 ? ShelterRadiusSlider.Value : _shelterRadius;
        _foodSpawnTarget = FoodSpawnSlider?.Value > 0 ? (int)Math.Round(FoodSpawnSlider.Value) : _foodSpawnTarget;
        _predatorSpawnTarget = PredatorSpawnSlider?.Value > 0 ? (int)Math.Round(PredatorSpawnSlider.Value) : _predatorSpawnTarget;
        _showThreatField = ShowThreatFieldCheckBox?.IsChecked ?? _showThreatField;
        _showPatrolPaths = ShowPatrolPathsCheckBox?.IsChecked ?? _showPatrolPaths;
    }

    private void RefreshSurvivalTuningLabels()
    {
        var metabolicBurn = MetabolicBurnRateValueText;
        var predatorSpeed = PredatorSpeedValueText;
        var predatorSense = PredatorSenseValueText;
        var shelterRadius = ShelterRadiusValueText;
        var foodSpawn = FoodSpawnValueText;
        var predatorSpawn = PredatorSpawnValueText;
        if (metabolicBurn is null ||
            predatorSpeed is null ||
            predatorSense is null ||
            shelterRadius is null ||
            foodSpawn is null ||
            predatorSpawn is null)
        {
            return;
        }

        metabolicBurn.Text = $"x{_metabolicBurnRate:0.00}";
        predatorSpeed.Text = $"x{_predatorSpeedScale:0.00}";
        predatorSense.Text = $"{_predatorSenseRadius:0.0} units";
        shelterRadius.Text = $"{_shelterRadius:0.0} units";
        foodSpawn.Text = _foodSpawnTarget.ToString(CultureInfo.InvariantCulture);
        predatorSpawn.Text = _predatorSpawnTarget.ToString(CultureInfo.InvariantCulture);
    }

    private void RequestWorldRespawn()
    {
        if (_heights is null)
        {
            return;
        }

        RebuildWorldSceneCore();
        Log("Survival tuning applied: world respawned.");
    }

    private void RebuildWorldFromSeed()
    {
        Interlocked.Increment(ref _visionGeneration);
        Interlocked.Exchange(ref _pendingVisionComputeRequest, null);
        Interlocked.Exchange(ref _pendingVisionComputeResult, null);
        Interlocked.Exchange(ref _visionComputeWarning, string.Empty);

        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeed))
        {
            parsedSeed = 317;
            SeedTextBox.Text = "317";
        }

        _seed = parsedSeed;
        _surfaceOverrides.Clear();
        _overrideCells = 0;
        _heights = GenerateHeightMap(_seed);
        RebuildWorldSceneCore();
    }

    private void RebuildWorldSceneCore()
    {
        if (_heights is null)
        {
            _heights = GenerateHeightMap(_seed);
        }

        InvalidateVisionSceneSnapshot();
        SyncSurvivalTuningFromUi();
        _sceneRoot.Children.Clear();
        _terrainColumns = 0;
        _waterColumns = 0;
        _treeClusters = 0;
        _mountainClusters = 0;
        _rockClusters = 0;
        _caveEntrances = 0;
        _habitatBaseY = SeaLevel + 2;
        _collisionBoxes.Clear();
        // Spawn placement runs while the scene is being rebuilt. The previous
        // world's grid indexes no longer match the cleared collision list.
        _collisionGrid = null;
        _collisionGridDimX = 0;
        _collisionGridDimZ = 0;
        _visionHitBoxes.Clear();
        _caveAnchors.Clear();
        _trailPoints.Clear();
        _trailPointTransforms.Clear();
        _pendingPhysicalContacts.Clear();
        _foodPickups.Clear();
        _weaponPickups.Clear();
        _predators.Clear();
        _shelterSites.Clear();
        _visitedTerrainCells.Clear();
        _terrainColumnModels = new GeometryModel3D[WorldSize, WorldSize];
        _waterColumnModels = new GeometryModel3D[WorldSize, WorldSize];
        _homesBuilt = 0;
        _explorableTerrainCells = 0;
        _trailAccumulatorSeconds = 0.0;
        _collisionPulse = 0.0;
        _collisionHits = 0;
        _storedEnergyJoules = NominalStoredEnergyJoules * 0.75;
        _tissueIntegrity = 1.0;
        _hydrationFraction = 0.75;
        _daylight01 = 1.0;
        _darkness01 = 0.0;
        _dayNightStage = "day";
        _foodConsumed = 0;
        _weaponCharges = 0;
        _deviceInventory = default;
        _predatorsNeutralized = 0;
        _weaponPickupsCollected = 0;
        _waterInteractions = 0;
        _interactionAttempts = 0;
        _interactionSuccesses = 0;
        _manipulatorDrive = 0.0;
        _manipulatorLatched = false;
        _lastManipulatorCycleMs = 0;
        _lastPredatorContactMs = 0;
        _distanceTravelled = 0.0;
        _neuronalMotorDispatchTotal = 0;
        _neuronalLocomotorDispatchTotal = 0;
        _neuronalManipulatorDispatchTotal = 0;
        _retinalFramesAccepted = 0;
        _cochlearFramesAccepted = 0;
        _physicalBodyFramesAccepted = 0;
        _somaticFramesAccepted = 0;
        _environmentAudioInFlight = false;
        _lastEnvironmentAudioDispatchMs = 0;
        _environmentAudioBackoff.Reset();
        _environmentAudioWarningGate.Reset();
        ResetBrainInputPressureGates();
        _optionalBrainInputPressureWarningGate.Reset();
        _spawnValidationRetries = 0;
        _shelterDoorCorridorClears = 0;

        var worldGroup = new Model3DGroup();

        ConfigureWorldLighting(worldGroup);

        AddTerrainColumns(worldGroup, _heights);
        AddWaterSurface(worldGroup, _heights);
        AddHabitat(worldGroup, _heights);
        AddRockFormations(worldGroup, _heights, _seed + 331);
        AddTreeClusters(worldGroup, _heights, _seed + 991);
        AddCaveStructures(worldGroup, _heights, _seed + 1777);
        AddFoodPickups(worldGroup, _heights, _seed + 2111);
        AddWeaponPickups(worldGroup, _heights, _seed + 2333);
        AddPredators(worldGroup, _heights, _seed + 2777);
        BuildAvatar(worldGroup);
        BuildTrailMarkers(worldGroup);
        _explorableTerrainCells = CountExplorableTerrainCells(_heights);

        _sceneRoot.Children.Add(worldGroup);
        WorldViewport.Children.Clear();
        WorldViewport.Children.Add(new ModelVisual3D { Content = _sceneRoot });

        SeedInfoText.Text = $"Seed: {_seed}";
        TerrainInfoText.Text = $"Terrain columns: {_terrainColumns}";
        WaterInfoText.Text = $"Water columns: {_waterColumns}";
        TreeInfoText.Text = $"Tree clusters: {_treeClusters}";
        HabitatInfoText.Text = $"Habitat core elevation: {_habitatBaseY:0.0} units | homes {_homesBuilt}, mountains {_mountainClusters}, rocks {_rockClusters}, caves {_caveEntrances}, paint {_overrideCells}";
        MotorDispatchText.Text = "Motor dispatch events: 0";
        MotorDriveText.Text = "Motor drive L/R: 0.0 / 0.0";
        ManipulatorDriveText.Text = "Manipulator drive: 0.00";
        MotorDecisionText.Text = _brainMotorDecisionText;
        MotorPathwayAuditText.Text = _motorPathwayAuditText;
        CollisionText.Text = "Collision hits: 0";
        TrailText.Text = "Trail points: 0 | mapped: 0";
        SurvivalEnergyText.Text = "Energy reserve: 75%";
        SurvivalTissueIntegrityText.Text = "Tissue integrity: 100%";
        SurvivalHydrationText.Text = "Hydration: 75%";
        SurvivalThreatText.Text = "Threat: 0%";
        SurvivalFoodText.Text = "Food collected: 0";
        SurvivalWeaponText.Text = "Weapon charge: 0";
        SurvivalShelterText.Text = "Shelter: not reached";
        SurvivalPredatorText.Text = "Predators: 0 active";
        SurvivalInteractionText.Text = "Physical interactions: 0/0";
        DayNightText.Text = "Light cycle: day";
        AvatarPoseText.Text = "Avatar pose: x 0.00, y 0.00, z 0.00, body 0.0 deg, head 0.0 deg";
        MapEditorHintText.Text = _mapEditorEnabled
            ? "Editor on. Left-click terrain to paint using selected brush."
            : "Editor off. Enable map editor and left-click terrain to paint.";
        AvatarPreviewInfoText.Text = "Preview: active";
        RefreshSurvivalTuningLabels();
        RebuildCollisionGrid();
    }

    /// <summary>
    /// Bucket every <see cref="_collisionBoxes"/> entry into a 2D XZ grid so that
    /// <see cref="IsCollisionAt"/> can scan only boxes near the query point instead
    /// of the whole list. Called once after world generation populates the box list.
    /// </summary>
    private void RebuildCollisionGrid()
    {
        if (_collisionBoxes.Count == 0)
        {
            _collisionGrid = null;
            return;
        }

        // Compute world AABB across all boxes, then size the grid in 4m cells.
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minZ = double.PositiveInfinity;
        var maxZ = double.NegativeInfinity;
        for (var i = 0; i < _collisionBoxes.Count; i++)
        {
            var b = _collisionBoxes[i];
            if (b.MinX < minX) minX = b.MinX;
            if (b.MaxX > maxX) maxX = b.MaxX;
            if (b.MinZ < minZ) minZ = b.MinZ;
            if (b.MaxZ > maxZ) maxZ = b.MaxZ;
        }

        // Pad by avatar radius so edge queries land in valid cells.
        minX -= AvatarRadius + 1.0;
        maxX += AvatarRadius + 1.0;
        minZ -= AvatarRadius + 1.0;
        maxZ += AvatarRadius + 1.0;

        _collisionGridOriginX = minX;
        _collisionGridOriginZ = minZ;
        _collisionGridDimX = Math.Max(1, (int)Math.Ceiling((maxX - minX) / CollisionGridCellSize));
        _collisionGridDimZ = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / CollisionGridCellSize));
        _collisionGrid = new List<int>[_collisionGridDimX * _collisionGridDimZ];

        for (var i = 0; i < _collisionBoxes.Count; i++)
        {
            var b = _collisionBoxes[i];
            var x0 = Math.Max(0, (int)Math.Floor((b.MinX - _collisionGridOriginX) / CollisionGridCellSize));
            var x1 = Math.Min(_collisionGridDimX - 1, (int)Math.Floor((b.MaxX - _collisionGridOriginX) / CollisionGridCellSize));
            var z0 = Math.Max(0, (int)Math.Floor((b.MinZ - _collisionGridOriginZ) / CollisionGridCellSize));
            var z1 = Math.Min(_collisionGridDimZ - 1, (int)Math.Floor((b.MaxZ - _collisionGridOriginZ) / CollisionGridCellSize));

            for (var gz = z0; gz <= z1; gz++)
            {
                for (var gx = x0; gx <= x1; gx++)
                {
                    var cellIndex = (gz * _collisionGridDimX) + gx;
                    var bucket = _collisionGrid[cellIndex] ??= new List<int>(4);
                    bucket.Add(i);
                }
            }
        }
    }

    private void ConfigureWorldLighting(Model3DGroup worldGroup)
    {
        _worldAmbientLight = new AmbientLight(Color.FromRgb(58, 74, 112));
        _worldSunLight = new DirectionalLight(Color.FromRgb(186, 209, 255), new Vector3D(-0.85, -1.0, -0.62));
        _worldMoonLight = new DirectionalLight(Color.FromRgb(66, 88, 138), new Vector3D(0.92, -0.45, -0.28));

        worldGroup.Children.Add(_worldAmbientLight);
        worldGroup.Children.Add(_worldSunLight);
        worldGroup.Children.Add(_worldMoonLight);
        UpdateDayNightCycle(_frameStopwatch.Elapsed.TotalSeconds);
    }

    private int[,] GenerateHeightMap(int seed)
    {
        var heights = new int[WorldSize, WorldSize];
        var center = (WorldSize - 1) * 0.5;
        var maxRadius = WorldSize * 0.64;

        var mountainCenters = new (double x, double z, double radius, double gain)[]
        {
            (-center * 0.55, -center * 0.25, WorldSize * 0.20, 5.6),
            (center * 0.42, center * 0.18, WorldSize * 0.18, 4.8),
            (0.0, center * 0.46, WorldSize * 0.16, 3.7)
        };
        _mountainClusters = mountainCenters.Length;

        for (var x = 0; x < WorldSize; x++)
        {
            for (var z = 0; z < WorldSize; z++)
            {
                var wx = (x - center);
                var wz = (z - center);
                var radius = Math.Sqrt((wx * wx) + (wz * wz));
                var radialFalloff = Math.Clamp(radius / maxRadius, 0.0, 1.0);

                var n1 = FractalNoise((wx * 0.075) + (seed * 0.0013), (wz * 0.075) + (seed * 0.0021), 4, 0.55);
                var n2 = FractalNoise((wx * 0.19) + (seed * 0.0032), (wz * 0.19) + (seed * 0.0019), 3, 0.45);
                var ridge = Math.Abs((n2 * 2.0) - 1.0);
                var raw = (n1 * 0.74) + ((1.0 - ridge) * 0.26);
                var sculpted = raw - (radialFalloff * 0.42);

                // Mountain envelope.
                for (var m = 0; m < mountainCenters.Length; m++)
                {
                    var mc = mountainCenters[m];
                    var dx = wx - mc.x;
                    var dz = wz - mc.z;
                    var dist = Math.Sqrt((dx * dx) + (dz * dz));
                    if (dist > mc.radius)
                    {
                        continue;
                    }

                    var t = 1.0 - (dist / mc.radius);
                    sculpted += (t * t) * (mc.gain / (MaxTerrainHeight - MinTerrainHeight));
                }

                // Central valley around habitat.
                var valleyRadius = WorldSize * 0.17;
                var valleyDistance = Math.Sqrt((wx * wx) + (wz * wz));
                if (valleyDistance < valleyRadius)
                {
                    var valleyT = 1.0 - (valleyDistance / valleyRadius);
                    sculpted -= valleyT * 0.25;
                }

                var elevation = MinTerrainHeight + (int)Math.Round(sculpted * (MaxTerrainHeight - MinTerrainHeight));
                heights[x, z] = Math.Clamp(elevation, MinTerrainHeight, MountainPeakHeight);
            }
        }

        return heights;
    }

    private void AddTerrainColumns(Model3DGroup group, int[,] heights)
    {
        var half = (WorldSize - 1) * 0.5;
        for (var x = 0; x < WorldSize; x++)
        {
            for (var z = 0; z < WorldSize; z++)
            {
                var height = heights[x, z];
                var worldX = (x - half) * BlockSize;
                var worldZ = (z - half) * BlockSize;
                var centerY = (height * BlockSize * 0.5) - (BlockSize * 0.5);
                var material = PickTerrainMaterial(height, x, z);
                var model = CreateBlockModel(material, worldX, centerY, worldZ, BlockSize, height * BlockSize, BlockSize);
                group.Children.Add(model);
                if (_terrainColumnModels is not null)
                {
                    _terrainColumnModels[x, z] = model;
                }
                _terrainColumns++;
            }
        }
    }

    private void AddWaterSurface(Model3DGroup group, int[,] heights)
    {
        var half = (WorldSize - 1) * 0.5;
        for (var x = 0; x < WorldSize; x++)
        {
            for (var z = 0; z < WorldSize; z++)
            {
                var height = heights[x, z];
                if (height >= SeaLevel)
                {
                    continue;
                }

                var worldX = (x - half) * BlockSize;
                var worldZ = (z - half) * BlockSize;
                var waterHeight = (SeaLevel - height) + 0.35;
                var centerY = (SeaLevel * BlockSize) - (waterHeight * 0.5);
                var model = CreateBlockModel(_materials[BlockKind.Water], worldX, centerY, worldZ, BlockSize, waterHeight, BlockSize);
                group.Children.Add(model);
                if (_waterColumnModels is not null)
                {
                    _waterColumnModels[x, z] = model;
                }
                _waterColumns++;
            }
        }
    }

    private void AddTreeClusters(Model3DGroup group, int[,] heights, int seed)
    {
        var half = (WorldSize - 1) * 0.5;
        for (var x = 2; x < WorldSize - 2; x++)
        {
            for (var z = 2; z < WorldSize - 2; z++)
            {
                var height = heights[x, z];
                if (height <= SeaLevel + 1)
                {
                    continue;
                }

                var placeNoise = FractalNoise((x * 0.31) + (seed * 0.013), (z * 0.31) + (seed * 0.017), 2, 0.5);
                if (placeNoise < 0.81)
                {
                    continue;
                }

                var worldX = (x - half) * BlockSize;
                var worldZ = (z - half) * BlockSize;
                if (IsNearAnyShelter(worldX, worldZ, _shelterRadius + 1.8))
                {
                    continue;
                }

                var terrainTopY = GetTerrainTopYFromHeight(height);
                var trunkHeight = 2.2 + (placeNoise * 2.0);
                var trunkCenterY = terrainTopY + (trunkHeight * 0.5);
                AddCollisionBlock(group, BlockKind.Wood, worldX, trunkCenterY, worldZ, 0.55, trunkHeight, 0.55);

                var canopyY = terrainTopY + trunkHeight + 0.7;
                AddVisionBlock(group, BlockKind.Leaves, worldX, canopyY, worldZ, 2.2, 1.1, 2.2);
                AddVisionBlock(group, BlockKind.Leaves, worldX, canopyY + 0.85, worldZ, 1.4, 0.8, 1.4);
                _treeClusters++;
            }
        }
    }

    private void AddRockFormations(Model3DGroup group, int[,] heights, int seed)
    {
        var random = new Random(seed);
        var half = (WorldSize - 1) * 0.5;
        var formations = 20;
        _rockClusters = formations;

        for (var i = 0; i < formations; i++)
        {
            var gx = random.Next(3, WorldSize - 4);
            var gz = random.Next(3, WorldSize - 4);
            if (heights[gx, gz] < SeaLevel + 2)
            {
                continue;
            }

            var worldX = (gx - half) * BlockSize;
            var worldZ = (gz - half) * BlockSize;
            if (IsNearAnyShelter(worldX, worldZ, _shelterRadius + 1.4))
            {
                continue;
            }

            var topY = GetTerrainTopYFromHeight(heights[gx, gz]);
            var radius = 0.45 + (random.NextDouble() * 0.80);
            var height = 0.55 + (random.NextDouble() * 1.40);
            AddCollisionBlock(
                group,
                BlockKind.Stone,
                worldX,
                topY + (height * 0.5),
                worldZ,
                radius * 1.6,
                height,
                radius * 1.5);
        }
    }

    private void AddCaveStructures(Model3DGroup group, int[,] heights, int seed)
    {
        var random = new Random(seed);
        var half = (WorldSize - 1) * 0.5;
        var attempts = 9;
        var caves = 0;

        for (var i = 0; i < attempts; i++)
        {
            var gx = random.Next(5, WorldSize - 6);
            var gz = random.Next(5, WorldSize - 6);
            var h = heights[gx, gz];
            if (h < SeaLevel + 5)
            {
                continue;
            }

            var toCenterX = (WorldSize * 0.5) - gx;
            var toCenterZ = (WorldSize * 0.5) - gz;
            var axisX = Math.Abs(toCenterX) > Math.Abs(toCenterZ) ? Math.Sign(toCenterX) : 0;
            var axisZ = axisX == 0 ? Math.Sign(toCenterZ) : 0;
            if (axisX == 0 && axisZ == 0)
            {
                axisZ = 1;
            }

            var worldX = (gx - half) * BlockSize;
            var worldZ = (gz - half) * BlockSize;
            if (IsNearAnyShelter(worldX, worldZ, _shelterRadius + 2.0))
            {
                continue;
            }

            var floorY = (h * BlockSize) - 0.2;
            var corridorLen = 2.6 + (random.NextDouble() * 1.8);
            var corridorWidth = 1.65;
            var corridorHeight = 1.95;

            // Entrances as carved corridors with rock shell.
            AddCollisionBlock(
                group,
                BlockKind.Stone,
                worldX - (axisX * (corridorLen * 0.48)),
                floorY + (corridorHeight + 0.42),
                worldZ - (axisZ * (corridorLen * 0.48)),
                corridorWidth + 0.85,
                0.85,
                corridorLen + 0.9);
            AddCollisionBlock(
                group,
                BlockKind.Stone,
                worldX - (axisX * (corridorLen * 0.34)) + (axisZ * 0.98),
                floorY + (corridorHeight * 0.5),
                worldZ - (axisZ * (corridorLen * 0.34)) + (axisX * 0.98),
                0.48,
                corridorHeight,
                corridorLen + 0.35);
            AddCollisionBlock(
                group,
                BlockKind.Stone,
                worldX - (axisX * (corridorLen * 0.34)) - (axisZ * 0.98),
                floorY + (corridorHeight * 0.5),
                worldZ - (axisZ * (corridorLen * 0.34)) - (axisX * 0.98),
                0.48,
                corridorHeight,
                corridorLen + 0.35);

            _caveAnchors.Add(new CaveAnchor(worldX, floorY + 0.9, worldZ));
            caves++;
        }

        _caveEntrances = caves;
    }

    private void AddHabitat(Model3DGroup group, int[,] heights)
    {
        var center = WorldSize / 2;
        _habitatBaseY = GetTerrainTopYFromHeight(heights[center, center]);
        var baseY = _habitatBaseY;
        var random = new Random(_seed + 4127);

        FlattenTerrainForShelter(heights, 0.0, 0.0, baseY, 1.0);
        AddShelterHome(group, 0.0, baseY, 0.0, scale: 1.0);
        _shelterSites.Add(new ShelterSite(0.0, baseY, 0.0, _shelterRadius));
        _homesBuilt = 1;

        for (var i = 0; i < AdditionalShelterHomeCount; i++)
        {
            if (!TryFindShelterSite(heights, random, out var homeX, out var homeY, out var homeZ))
            {
                continue;
            }

            var homeScale = 0.70 + (random.NextDouble() * 0.20);
            FlattenTerrainForShelter(heights, homeX, homeZ, homeY, homeScale);
            AddShelterHome(group, homeX, homeY, homeZ, homeScale);
            _shelterSites.Add(new ShelterSite(homeX, homeY, homeZ, Math.Max(2.6, _shelterRadius * 0.78)));
            _homesBuilt++;
        }

        var coreMaterial = new MaterialGroup();
        coreMaterial.Children.Add(new DiffuseMaterial(_brainCoreDiffuseBrush));
        coreMaterial.Children.Add(new EmissiveMaterial(_brainCoreEmissiveBrush));

        var core = new GeometryModel3D(_brainCoreMesh, coreMaterial) { BackMaterial = coreMaterial };
        var coreTransform = new Transform3DGroup();
        coreTransform.Children.Add(_brainCoreScale);
        coreTransform.Children.Add(_brainCoreTranslate);
        core.Transform = coreTransform;
        _brainCoreTranslate.OffsetX = 0;
        _brainCoreTranslate.OffsetY = baseY + 1.35;
        _brainCoreTranslate.OffsetZ = 0;

        group.Children.Add(core);
    }

    private void AddShelterHome(Model3DGroup group, double worldX, double baseY, double worldZ, double scale)
    {
        var width = 8.0 * scale;
        var depth = 8.0 * scale;
        var wallHeight = 2.4 * scale;
        var wallThickness = 0.4 * scale;
        var sideOffset = (width * 0.5) - (wallThickness * 0.5);
        var rearOffset = (depth * 0.5) - (wallThickness * 0.5);
        var frontPanelWidth = width * 0.375;
        var frontPanelOffset = (width * 0.28);
        var roofY = baseY + (wallHeight + (0.15 * scale));

        // Solidify the substrate below each habitat so terrain never pokes through floors.
        AddCollisionBlock(group, BlockKind.HabitatFloor, worldX, baseY - (1.4 * scale), worldZ, width * 1.04, 2.8 * scale, depth * 1.04);
        AddCollisionBlock(group, BlockKind.HabitatFloor, worldX, baseY - (0.2 * scale), worldZ, width, 0.4 * scale, depth);
        AddCollisionBlock(group, BlockKind.HabitatWall, worldX, baseY + (wallHeight * 0.5), worldZ - rearOffset, width, wallHeight, wallThickness);
        AddCollisionBlock(group, BlockKind.HabitatWall, worldX - sideOffset, baseY + (wallHeight * 0.5), worldZ, wallThickness, wallHeight, depth - wallThickness);
        AddCollisionBlock(group, BlockKind.HabitatWall, worldX + sideOffset, baseY + (wallHeight * 0.5), worldZ, wallThickness, wallHeight, depth - wallThickness);
        AddCollisionBlock(group, BlockKind.HabitatWall, worldX - frontPanelOffset, baseY + (wallHeight * 0.5), worldZ + rearOffset, frontPanelWidth, wallHeight, wallThickness);
        AddCollisionBlock(group, BlockKind.HabitatWall, worldX + frontPanelOffset, baseY + (wallHeight * 0.5), worldZ + rearOffset, frontPanelWidth, wallHeight, wallThickness);
        AddVisionBlock(group, BlockKind.HabitatGlass, worldX, baseY + (wallHeight * 0.74), worldZ + rearOffset, 1.0 * scale, 1.2 * scale, 0.24 * scale);
        AddVisionBlock(group, BlockKind.HabitatGlass, worldX, roofY, worldZ, width * 0.78, 0.35 * scale, depth * 0.78);
    }

    private void FlattenTerrainForShelter(int[,] heights, double worldX, double worldZ, double baseY, double scale)
    {
        var width = (8.0 * scale) + 3.2;
        var depth = (8.0 * scale) + 3.2;
        var targetHeight = Math.Clamp(
            (int)Math.Round((baseY + (BlockSize * 0.5)) / BlockSize),
            MinTerrainHeight,
            MountainPeakHeight);

        var halfX = (int)Math.Ceiling((width * 0.5) / BlockSize);
        var halfZ = (int)Math.Ceiling((depth * 0.5) / BlockSize);

        if (!TryWorldToGrid(worldX, worldZ, out var centerX, out var centerZ))
        {
            return;
        }

        for (var gx = centerX - halfX; gx <= centerX + halfX; gx++)
        {
            if (gx < 1 || gx >= WorldSize - 1)
            {
                continue;
            }

            for (var gz = centerZ - halfZ; gz <= centerZ + halfZ; gz++)
            {
                if (gz < 1 || gz >= WorldSize - 1)
                {
                    continue;
                }

                heights[gx, gz] = targetHeight;
                UpdateTerrainCellVisual(gx, gz);
            }
        }

        // Keep a clear egress corridor from the shelter opening (+Z side) so
        // the avatar can leave the habitat without immediately colliding.
        FlattenShelterDoorCorridor(heights, worldX, worldZ, targetHeight, scale);
    }

    private void FlattenShelterDoorCorridor(int[,] heights, double worldX, double worldZ, int targetHeight, double scale)
    {
        var corridorWidth = Math.Max(2.2, 1.9 * scale);
        var corridorLength = Math.Max(5.0, 6.2 * scale);
        var step = BlockSize * 0.5;
        var startZ = worldZ + ((8.0 * scale * 0.5) + (0.8 * scale));
        var endZ = startZ + corridorLength;

        for (var wx = worldX - corridorWidth; wx <= worldX + corridorWidth; wx += step)
        {
            for (var wz = startZ; wz <= endZ; wz += step)
            {
                if (!TryWorldToGrid(wx, wz, out var gx, out var gz))
                {
                    continue;
                }

                if (gx < 1 || gx >= WorldSize - 1 || gz < 1 || gz >= WorldSize - 1)
                {
                    continue;
                }

                heights[gx, gz] = targetHeight;
                UpdateTerrainCellVisual(gx, gz);
                _shelterDoorCorridorClears++;
            }
        }
    }

    private bool TryFindShelterSite(int[,] heights, Random random, out double worldX, out double worldY, out double worldZ)
    {
        var half = (WorldSize - 1) * 0.5;
        var minCoreDistance = WorldSize * 0.16;

        for (var attempt = 0; attempt < 64; attempt++)
        {
            var gx = random.Next(5, WorldSize - 6);
            var gz = random.Next(5, WorldSize - 6);
            var h = heights[gx, gz];
            if (h <= SeaLevel + 1)
            {
                continue;
            }

            var wx = (gx - half) * BlockSize;
            var wz = (gz - half) * BlockSize;
            var radial = Math.Sqrt((wx * wx) + (wz * wz));
            if (radial < minCoreDistance)
            {
                continue;
            }

            if (IsNearAnyShelter(wx, wz, ShelterHomeSpacingMin))
            {
                continue;
            }

            worldX = wx;
            worldY = GetTerrainTopYFromHeight(h);
            worldZ = wz;
            return true;
        }

        worldX = 0.0;
        worldY = _habitatBaseY;
        worldZ = 0.0;
        return false;
    }

    private bool IsNearAnyShelter(double worldX, double worldZ, double radius)
    {
        for (var i = 0; i < _shelterSites.Count; i++)
        {
            var shelter = _shelterSites[i];
            var dx = worldX - shelter.X;
            var dz = worldZ - shelter.Z;
            var minDistance = radius + shelter.Radius;
            if ((dx * dx) + (dz * dz) <= (minDistance * minDistance))
            {
                return true;
            }
        }

        return false;
    }

    private void AddFoodPickups(Model3DGroup group, int[,] heights, int seed)
    {
        var random = new Random(seed);
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(_foodDiffuseBrush));
        material.Children.Add(new EmissiveMaterial(_foodEmissiveBrush));

        var spawned = 0;
        var target = Math.Max(6, _foodSpawnTarget);
        for (var i = 0; i < 32 && spawned < target; i++)
        {
            if (!TryFindSpawnLocation(heights, random, out var worldX, out var worldY, out var worldZ, avoidCenter: true))
            {
                continue;
            }

            var translate = new TranslateTransform3D(worldX, worldY + 0.24, worldZ);
            var model = BuildBlockFoodPickupModel(material, translate);
            group.Children.Add(model);

            _foodPickups.Add(new FoodPickup(new Point3D(worldX, worldY + 0.24, worldZ), translate, model));
            spawned++;
        }
    }

    private void AddWeaponPickups(Model3DGroup group, int[,] heights, int seed)
    {
        var random = new Random(seed);
        var shortRangeMaterial = new MaterialGroup();
        shortRangeMaterial.Children.Add(new DiffuseMaterial(_weaponDiffuseBrush));
        shortRangeMaterial.Children.Add(new EmissiveMaterial(_weaponEmissiveBrush));
        var longRangeMaterial = new MaterialGroup();
        longRangeMaterial.Children.Add(new DiffuseMaterial(_weaponLongDiffuseBrush));
        longRangeMaterial.Children.Add(new EmissiveMaterial(_weaponLongEmissiveBrush));

        var spawned = 0;
        var target = Math.Max(3, (int)Math.Round(_foodSpawnTarget * 0.45));
        for (var i = 0; i < 20 && spawned < target; i++)
        {
            if (!TryFindSpawnLocation(heights, random, out var worldX, out var worldY, out var worldZ, avoidCenter: false))
            {
                continue;
            }

            var rangeProfile = random.NextDouble() < 0.36
                ? AvatarDeviceRangeProfile.Long
                : AvatarDeviceRangeProfile.Short;
            var material = rangeProfile == AvatarDeviceRangeProfile.Long
                ? longRangeMaterial
                : shortRangeMaterial;
            var translate = new TranslateTransform3D(worldX, worldY + 0.22, worldZ);
            var model = BuildBlockWeaponPickupModel(material, rangeProfile, translate);
            group.Children.Add(model);

            _weaponPickups.Add(new WeaponPickup(new Point3D(worldX, worldY + 0.22, worldZ), translate, model, rangeProfile));
            spawned++;
        }
    }

    private void AddPredators(Model3DGroup group, int[,] heights, int seed)
    {
        var random = new Random(seed);
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(_predatorDiffuseBrush));
        material.Children.Add(new EmissiveMaterial(_predatorEmissiveBrush));
        var muzzleMaterial = new MaterialGroup();
        muzzleMaterial.Children.Add(new DiffuseMaterial(_predatorMuzzleBrush));
        var faceMaterial = new MaterialGroup();
        faceMaterial.Children.Add(new DiffuseMaterial(_predatorFaceBrush));
        var eyeMaterial = new MaterialGroup();
        eyeMaterial.Children.Add(new DiffuseMaterial(_predatorEyeBrush));
        eyeMaterial.Children.Add(new EmissiveMaterial(_predatorEyeBrush));
        var threatMaterial = new MaterialGroup();
        threatMaterial.Children.Add(new DiffuseMaterial(_predatorThreatBrush));
        threatMaterial.Children.Add(new EmissiveMaterial(_predatorThreatEmissiveBrush));
        var pathMaterial = new MaterialGroup();
        pathMaterial.Children.Add(new DiffuseMaterial(_predatorPathBrush));

        var spawned = 0;
        var target = Math.Max(1, _predatorSpawnTarget);
        for (var i = 0; i < 24 && spawned < target; i++)
        {
            if (!TryFindSpawnLocation(heights, random, out var worldX, out var worldY, out var worldZ, avoidCenter: true))
            {
                continue;
            }

            var headingDeg = random.NextDouble() * 360.0;
            var translate = new TranslateTransform3D(worldX, worldY + 0.18, worldZ);
            var yawRotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), headingDeg);
            var model = BuildBearPredatorModel(material, muzzleMaterial, faceMaterial, eyeMaterial, yawRotation, translate);
            group.Children.Add(model);

            var threatScale = new ScaleTransform3D(_predatorSenseRadius, _predatorSenseRadius * 0.35, _predatorSenseRadius);
            var threatTransform = new Transform3DGroup();
            threatTransform.Children.Add(threatScale);
            threatTransform.Children.Add(new TranslateTransform3D(worldX, worldY + 0.12, worldZ));
            var threatModel = new GeometryModel3D(_predatorThreatMesh, threatMaterial)
            {
                BackMaterial = threatMaterial,
                Transform = threatTransform
            };
            group.Children.Add(threatModel);

            var patrolPoints = BuildPatrolPoints(new Point3D(worldX, worldY + 0.18, worldZ), random);
            var pathModel = BuildPatrolPathModel(patrolPoints, pathMaterial);
            group.Children.Add(pathModel);

            _predators.Add(new PredatorNpc(
                new Point3D(worldX, worldY + 0.18, worldZ),
                headingDeg,
                translate,
                yawRotation,
                model,
                patrolPoints,
                0,
                threatModel,
                threatScale,
                pathModel));
            spawned++;
        }

        UpdateThreatFieldVisibility();
        UpdatePatrolPathVisibility();
    }

    private Model3DGroup BuildBlockFoodPickupModel(Material material, TranslateTransform3D translate)
    {
        var food = new Model3DGroup { Transform = translate };
        AddBlockPart(food, _unitCubeMesh, material, 0.0, 0.0, 0.0, 0.34, 0.34, 0.34);
        AddBlockPart(food, _unitCubeMesh, material, -0.11, 0.16, 0.02, 0.16, 0.12, 0.16);
        AddBlockPart(food, _unitCubeMesh, material, 0.12, 0.12, -0.02, 0.14, 0.14, 0.14);
        return food;
    }

    private Model3DGroup BuildBlockWeaponPickupModel(
        Material material,
        AvatarDeviceRangeProfile rangeProfile,
        TranslateTransform3D translate)
    {
        var weapon = new Model3DGroup { Transform = translate };
        if (rangeProfile == AvatarDeviceRangeProfile.Long)
        {
            AddBlockPart(weapon, _unitCubeMesh, material, 0.0, 0.0, 0.0, 0.82, 0.12, 0.12);
            AddBlockPart(weapon, _unitCubeMesh, material, -0.24, -0.16, 0.0, 0.18, 0.24, 0.10);
            AddBlockPart(weapon, _unitCubeMesh, material, 0.30, 0.12, 0.0, 0.16, 0.14, 0.10);
        }
        else
        {
            AddBlockPart(weapon, _unitCubeMesh, material, 0.0, -0.08, 0.0, 0.14, 0.46, 0.14);
            AddBlockPart(weapon, _unitCubeMesh, material, 0.0, 0.20, 0.0, 0.32, 0.16, 0.16);
            AddBlockPart(weapon, _unitCubeMesh, material, 0.0, 0.34, 0.0, 0.20, 0.12, 0.12);
        }

        return weapon;
    }

    private Model3DGroup BuildBearPredatorModel(
        Material bodyMaterial,
        Material muzzleMaterial,
        Material faceMaterial,
        Material eyeMaterial,
        AxisAngleRotation3D yawRotation,
        TranslateTransform3D translate)
    {
        var bear = new Model3DGroup();
        var rootTransform = new Transform3DGroup();
        rootTransform.Children.Add(new RotateTransform3D(yawRotation));
        rootTransform.Children.Add(translate);
        bear.Transform = rootTransform;

        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, 0.0, 0.56, 0.0, 1.20, 0.70, 1.70);
        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, 0.0, 0.92, 0.94, 0.70, 0.58, 0.56);
        AddBlockPart(bear, _unitCubeMesh, muzzleMaterial, 0.0, 0.78, 1.30, 0.42, 0.24, 0.30);
        AddBlockPart(bear, _unitCubeMesh, faceMaterial, 0.0, 0.76, 1.48, 0.14, 0.08, 0.06);

        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, -0.34, 1.28, 0.92, 0.22, 0.24, 0.16);
        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, 0.34, 1.28, 0.92, 0.22, 0.24, 0.16);
        AddBlockPart(bear, _unitCubeMesh, eyeMaterial, -0.18, 0.96, 1.24, 0.08, 0.08, 0.04);
        AddBlockPart(bear, _unitCubeMesh, eyeMaterial, 0.18, 0.96, 1.24, 0.08, 0.08, 0.04);

        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, -0.45, 0.15, 0.48, 0.24, 0.45, 0.28);
        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, 0.45, 0.15, 0.48, 0.24, 0.45, 0.28);
        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, -0.45, 0.15, -0.62, 0.28, 0.48, 0.32);
        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, 0.45, 0.15, -0.62, 0.28, 0.48, 0.32);
        AddBlockPart(bear, _unitCubeMesh, bodyMaterial, 0.0, 0.62, -0.98, 0.22, 0.18, 0.20);

        return bear;
    }

    private static void AddBlockPart(
        Model3DGroup group,
        MeshGeometry3D mesh,
        Material material,
        double x,
        double y,
        double z,
        double sx,
        double sy,
        double sz)
    {
        var transform = new Transform3DGroup();
        transform.Children.Add(new ScaleTransform3D(sx, sy, sz));
        transform.Children.Add(new TranslateTransform3D(x, y, z));

        var part = new GeometryModel3D(mesh, material)
        {
            BackMaterial = material,
            Transform = transform
        };

        group.Children.Add(part);
    }

    private List<Point3D> BuildPatrolPoints(Point3D center, Random random)
    {
        var points = new List<Point3D>(8);
        var radius = 2.6 + (random.NextDouble() * 2.2);
        var baseAngle = random.NextDouble() * Math.PI * 2.0;
        var count = 6;
        for (var i = 0; i < count; i++)
        {
            var angle = baseAngle + (i * (Math.PI * 2.0 / count));
            var x = center.X + (Math.Sin(angle) * radius);
            var z = center.Z + (Math.Cos(angle) * radius);
            points.Add(new Point3D(x, center.Y, z));
        }

        return points;
    }

    private GeometryModel3D BuildPatrolPathModel(IReadOnlyList<Point3D> points, Material material)
    {
        var mesh = new MeshGeometry3D();
        if (points.Count < 2)
        {
            mesh.Freeze();
            return new GeometryModel3D(mesh, material) { BackMaterial = material };
        }

        for (var i = 0; i < points.Count; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Count];
            AddPathSegment(mesh, start, end, 0.08, start.Y + 0.02);
        }

        mesh.Freeze();
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static void AddPathSegment(MeshGeometry3D mesh, Point3D start, Point3D end, double width, double y)
    {
        var dx = end.X - start.X;
        var dz = end.Z - start.Z;
        var length = Math.Sqrt((dx * dx) + (dz * dz));
        if (length < 0.001)
        {
            return;
        }

        var nx = -dz / length;
        var nz = dx / length;
        var half = width * 0.5;
        var p1 = new Point3D(start.X + (nx * half), y, start.Z + (nz * half));
        var p2 = new Point3D(start.X - (nx * half), y, start.Z - (nz * half));
        var p3 = new Point3D(end.X - (nx * half), y, end.Z - (nz * half));
        var p4 = new Point3D(end.X + (nx * half), y, end.Z + (nz * half));

        var baseIndex = mesh.Positions.Count;
        mesh.Positions.Add(p1);
        mesh.Positions.Add(p2);
        mesh.Positions.Add(p3);
        mesh.Positions.Add(p4);

        mesh.TriangleIndices.Add(baseIndex);
        mesh.TriangleIndices.Add(baseIndex + 1);
        mesh.TriangleIndices.Add(baseIndex + 2);
        mesh.TriangleIndices.Add(baseIndex);
        mesh.TriangleIndices.Add(baseIndex + 2);
        mesh.TriangleIndices.Add(baseIndex + 3);
    }

    private void UpdateThreatFieldVisibility()
    {
        foreach (var predator in _predators)
        {
            predator.ThreatModel.Material = _showThreatField ? (predator.ThreatMaterial ?? _transparentMaterial) : _transparentMaterial;
            predator.ThreatModel.BackMaterial = predator.ThreatModel.Material;
        }
    }

    private void UpdatePatrolPathVisibility()
    {
        foreach (var predator in _predators)
        {
            predator.PathModel.Material = _showPatrolPaths ? (predator.PathMaterial ?? _transparentMaterial) : _transparentMaterial;
            predator.PathModel.BackMaterial = predator.PathModel.Material;
        }
    }

    private void UpdatePredatorThreatScales()
    {
        foreach (var predator in _predators)
        {
            predator.ThreatScale.ScaleX = _predatorSenseRadius;
            predator.ThreatScale.ScaleY = _predatorSenseRadius * 0.35;
            predator.ThreatScale.ScaleZ = _predatorSenseRadius;
        }
    }

    private bool TryFindSpawnLocation(int[,] heights, Random random, out double worldX, out double worldY, out double worldZ, bool avoidCenter)
    {
        var half = (WorldSize - 1) * 0.5;
        for (var attempt = 0; attempt < 18; attempt++)
        {
            var gx = random.Next(3, WorldSize - 4);
            var gz = random.Next(3, WorldSize - 4);
            var h = heights[gx, gz];
            if (h <= SeaLevel + 1)
            {
                continue;
            }

            var wx = (gx - half) * BlockSize;
            var wz = (gz - half) * BlockSize;
            if (avoidCenter && IsNearAnyShelter(wx, wz, _shelterRadius + 2.5))
            {
                continue;
            }

            var terrainY = GetTerrainTopYFromHeight(h);
            if (!IsSpawnLocationClear(wx, terrainY, wz))
            {
                continue;
            }

            worldX = wx;
            worldY = terrainY;
            worldZ = wz;
            return true;
        }

        worldX = 0;
        worldY = _habitatBaseY;
        worldZ = 0;
        return false;
    }

    private MaterialGroup PickTerrainMaterial(int height, int x, int z)
    {
        var key = MakeSurfaceKey(x, z);
        if (_surfaceOverrides.TryGetValue(key, out var overridden))
        {
            return _materials[overridden];
        }

        if (height <= SeaLevel + 1)
        {
            return _materials[BlockKind.Sand];
        }

        if (height >= SeaLevel + 10)
        {
            return _materials[BlockKind.Stone];
        }

        if (height >= SeaLevel + 6)
        {
            return _materials[BlockKind.Dirt];
        }

        return _materials[BlockKind.Grass];
    }

    private Dictionary<BlockKind, MaterialGroup> BuildMaterialLibrary()
    {
        var map = new Dictionary<BlockKind, MaterialGroup>
        {
            [BlockKind.Grass] = MakeMaterial(BlockKind.Grass, Color.FromRgb(90, 168, 96), Color.FromArgb(18, 160, 255, 160)),
            [BlockKind.Dirt] = MakeMaterial(BlockKind.Dirt, Color.FromRgb(129, 98, 73), Color.FromArgb(12, 210, 170, 140)),
            [BlockKind.Stone] = MakeMaterial(BlockKind.Stone, Color.FromRgb(116, 130, 142), Color.FromArgb(10, 175, 192, 214)),
            [BlockKind.Sand] = MakeMaterial(BlockKind.Sand, Color.FromRgb(176, 166, 124), Color.FromArgb(14, 255, 244, 180)),
            [BlockKind.Water] = MakeMaterial(BlockKind.Water, Color.FromArgb(165, 65, 132, 220), Color.FromArgb(40, 100, 170, 255)),
            [BlockKind.Wood] = MakeMaterial(BlockKind.Wood, Color.FromRgb(141, 102, 70), Color.FromArgb(12, 208, 146, 110)),
            [BlockKind.Leaves] = MakeMaterial(BlockKind.Leaves, Color.FromArgb(185, 58, 146, 82), Color.FromArgb(20, 150, 255, 180)),
            [BlockKind.HabitatFloor] = MakeMaterial(BlockKind.HabitatFloor, Color.FromRgb(110, 122, 146), Color.FromArgb(18, 180, 214, 255)),
            [BlockKind.HabitatWall] = MakeMaterial(BlockKind.HabitatWall, Color.FromRgb(128, 145, 176), Color.FromArgb(20, 190, 226, 255)),
            [BlockKind.HabitatGlass] = MakeMaterial(BlockKind.HabitatGlass, Color.FromArgb(145, 132, 204, 244), Color.FromArgb(40, 182, 236, 255))
        };
        return map;
    }

    private static MaterialGroup MakeMaterial(BlockKind kind, Color diffuseColor, Color emissiveColor)
    {
        var diffuseBrush = MakeTextureBrush(kind, diffuseColor);
        var emissiveBrush = new SolidColorBrush(emissiveColor);
        emissiveBrush.Freeze();
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuseBrush));
        material.Children.Add(new EmissiveMaterial(emissiveBrush));
        return material;
    }

    private static DrawingBrush MakeTextureBrush(BlockKind kind, Color baseColor)
    {
        var group = new DrawingGroup();
        var baseBrush = new SolidColorBrush(baseColor);
        baseBrush.Freeze();
        group.Children.Add(new GeometryDrawing(
            baseBrush,
            null,
            new RectangleGeometry(new Rect(0, 0, 32, 32))));

        var dark = new SolidColorBrush(ShadeColor(baseColor, 0.72));
        var light = new SolidColorBrush(ShadeColor(baseColor, 1.20));
        dark.Freeze();
        light.Freeze();
        var penDark = new Pen(dark, kind is BlockKind.Wood or BlockKind.Stone ? 2.0 : 1.0);
        var penLight = new Pen(light, 1.0);
        penDark.Freeze();
        penLight.Freeze();

        switch (kind)
        {
            case BlockKind.Grass:
            case BlockKind.Leaves:
                AddTextureStroke(group, penDark, 3, 7, 10, 4);
                AddTextureStroke(group, penLight, 14, 18, 22, 15);
                AddTextureDot(group, dark, 24, 9, 2.2);
                AddTextureDot(group, light, 8, 24, 1.8);
                break;
            case BlockKind.Dirt:
            case BlockKind.Sand:
                AddTextureDot(group, dark, 6, 6, 1.8);
                AddTextureDot(group, light, 18, 10, 1.4);
                AddTextureDot(group, dark, 24, 24, 1.6);
                AddTextureStroke(group, penDark, 4, 23, 16, 26);
                break;
            case BlockKind.Stone:
                AddTextureStroke(group, penDark, 2, 11, 14, 8);
                AddTextureStroke(group, penLight, 15, 22, 30, 18);
                AddTextureStroke(group, penDark, 11, 2, 26, 7);
                break;
            case BlockKind.Water:
                AddTextureStroke(group, penLight, 1, 9, 31, 7);
                AddTextureStroke(group, penDark, 2, 20, 30, 22);
                break;
            case BlockKind.Wood:
                AddTextureStroke(group, penDark, 7, 0, 9, 32);
                AddTextureStroke(group, penLight, 19, 0, 17, 32);
                AddTextureStroke(group, penDark, 26, 0, 27, 32);
                break;
            default:
                AddTextureStroke(group, penDark, 0, 8, 32, 8);
                AddTextureStroke(group, penLight, 0, 22, 32, 22);
                break;
        }

        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 0.22, 0.22),
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        return brush;
    }

    private static void AddTextureStroke(DrawingGroup group, Pen pen, double x1, double y1, double x2, double y2)
    {
        group.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(x1, y1), new Point(x2, y2))));
    }

    private static void AddTextureDot(DrawingGroup group, Brush brush, double x, double y, double radius)
    {
        group.Children.Add(new GeometryDrawing(brush, null, new EllipseGeometry(new Point(x, y), radius, radius)));
    }

    private static Color ShadeColor(Color color, double scale)
    {
        return Color.FromArgb(
            color.A,
            (byte)Math.Clamp(color.R * scale, 0, 255),
            (byte)Math.Clamp(color.G * scale, 0, 255),
            (byte)Math.Clamp(color.B * scale, 0, 255));
    }

    private GeometryModel3D CreateBlockModel(Material material, double x, double y, double z, double sx, double sy, double sz)
    {
        var model = new GeometryModel3D(_unitCubeMesh, material) { BackMaterial = material };
        var transform = new Transform3DGroup();
        transform.Children.Add(new ScaleTransform3D(sx * VoxelVisualScale, sy * VoxelVisualScale, sz * VoxelVisualScale));
        transform.Children.Add(new TranslateTransform3D(x, y, z));
        model.Transform = transform;
        return model;
    }

    private static void UpdateBlockModelTransform(GeometryModel3D model, double x, double y, double z, double sx, double sy, double sz)
    {
        if (model.Transform is not Transform3DGroup transform || transform.Children.Count < 2)
        {
            return;
        }

        if (transform.Children[0] is ScaleTransform3D scale)
        {
            scale.ScaleX = sx * VoxelVisualScale;
            scale.ScaleY = sy * VoxelVisualScale;
            scale.ScaleZ = sz * VoxelVisualScale;
        }

        if (transform.Children[1] is TranslateTransform3D translate)
        {
            translate.OffsetX = x;
            translate.OffsetY = y;
            translate.OffsetZ = z;
        }
    }

    private void AddBlock(Model3DGroup group, Material material, double x, double y, double z, double sx, double sy, double sz)
    {
        var model = CreateBlockModel(material, x, y, z, sx, sy, sz);
        group.Children.Add(model);
    }

    private void AddVisionBlock(Model3DGroup group, BlockKind kind, double x, double y, double z, double sx, double sy, double sz)
    {
        AddBlock(group, _materials[kind], x, y, z, sx, sy, sz);
        RegisterVisionHitBox(x, y, z, sx * VoxelVisualScale, sy * VoxelVisualScale, sz * VoxelVisualScale, kind);
    }

    private void AddCollisionBlock(Model3DGroup group, BlockKind kind, double x, double y, double z, double sx, double sy, double sz)
    {
        AddBlock(group, _materials[kind], x, y, z, sx, sy, sz);
        RegisterCollisionBox(x, y, z, sx * VoxelVisualScale, sy * VoxelVisualScale, sz * VoxelVisualScale);
        RegisterVisionHitBox(x, y, z, sx * VoxelVisualScale, sy * VoxelVisualScale, sz * VoxelVisualScale, kind);
    }

    private void RegisterCollisionBox(double x, double y, double z, double sx, double sy, double sz)
    {
        var halfX = sx * 0.5;
        var halfY = sy * 0.5;
        var halfZ = sz * 0.5;
        _collisionBoxes.Add(new CollisionBox(
            x - halfX,
            x + halfX,
            y - halfY,
            y + halfY,
            z - halfZ,
            z + halfZ));
    }

    private void RegisterVisionHitBox(double x, double y, double z, double sx, double sy, double sz, BlockKind kind)
    {
        var halfX = sx * 0.5;
        var halfY = sy * 0.5;
        var halfZ = sz * 0.5;
        _visionHitBoxes.Add(new VisionHitBox(
            x - halfX,
            x + halfX,
            y - halfY,
            y + halfY,
            z - halfZ,
            z + halfZ,
            kind));
    }

    private void BuildAvatar(Model3DGroup group)
    {
        var skinMaterial = new MaterialGroup();
        skinMaterial.Children.Add(new DiffuseMaterial(_avatarDiffuseBrush));
        skinMaterial.Children.Add(new EmissiveMaterial(_avatarEmissiveBrush));

        var bodyMaterial = new MaterialGroup();
        bodyMaterial.Children.Add(new DiffuseMaterial(_avatarSecondaryBrush));
        bodyMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(42, 64, 128))));

        var faceMaterial = new MaterialGroup();
        faceMaterial.Children.Add(new DiffuseMaterial(_avatarFaceBrush));
        faceMaterial.Children.Add(new EmissiveMaterial(_avatarFaceEmissiveBrush));

        var directionMaterial = new MaterialGroup();
        directionMaterial.Children.Add(new DiffuseMaterial(_avatarDirectionBrush));
        directionMaterial.Children.Add(new EmissiveMaterial(_avatarDirectionEmissiveBrush));

        var bodyTransform = new Transform3DGroup();
        bodyTransform.Children.Add(new RotateTransform3D(_avatarYawRotation));
        bodyTransform.Children.Add(_avatarTranslate);

        var headCenter = new Point3D(0, 1.55, 0);
        var headTransform = new Transform3DGroup();
        headTransform.Children.Add(new RotateTransform3D(_avatarHeadYawRotation, headCenter));
        headTransform.Children.Add(new RotateTransform3D(_avatarYawRotation));
        headTransform.Children.Add(_avatarTranslate);

        var avatarGroup = new Model3DGroup();

        // Minecraft-style proportions: head + torso + arms + legs
        AddAvatarPart(avatarGroup, skinMaterial, 0, 1.55, 0, 0.55, 0.55, 0.55, headTransform); // head
        AddAvatarPart(avatarGroup, bodyMaterial, 0, 1.05, 0, 0.62, 0.8, 0.32, bodyTransform); // torso
        AddAvatarPart(avatarGroup, bodyMaterial, -0.44, 1.05, 0, 0.2, 0.75, 0.2, bodyTransform); // left arm
        AddAvatarPart(avatarGroup, bodyMaterial, 0.44, 1.05, 0, 0.2, 0.75, 0.2, bodyTransform); // right arm
        AddAvatarPart(avatarGroup, bodyMaterial, -0.18, 0.32, 0, 0.22, 0.7, 0.22, bodyTransform); // left leg
        AddAvatarPart(avatarGroup, bodyMaterial, 0.18, 0.32, 0, 0.22, 0.7, 0.22, bodyTransform); // right leg
        AddAvatarPart(avatarGroup, faceMaterial, -0.11, 1.63, 0.29, 0.08, 0.08, 0.03, headTransform); // left eye
        AddAvatarPart(avatarGroup, faceMaterial, 0.11, 1.63, 0.29, 0.08, 0.08, 0.03, headTransform); // right eye
        AddAvatarPart(avatarGroup, faceMaterial, 0.00, 1.46, 0.29, 0.18, 0.05, 0.03, headTransform); // mouth
        AddAvatarPart(avatarGroup, directionMaterial, 0.00, 1.85, 0.36, 0.06, 0.06, 0.25, headTransform); // forward marker shaft
        AddAvatarPart(avatarGroup, directionMaterial, 0.00, 1.85, 0.52, 0.18, 0.04, 0.09, headTransform); // forward marker tip

        group.Children.Add(avatarGroup);

        ResetAvatarPose(logMessage: false);
    }

    private void AddAvatarPart(
        Model3DGroup group,
        Material material,
        double offsetX,
        double offsetY,
        double offsetZ,
        double sizeX,
        double sizeY,
        double sizeZ,
        Transform3DGroup sharedTransform)
    {
        var model = new GeometryModel3D(_unitCubeMesh, material) { BackMaterial = material };
        var transform = new Transform3DGroup();
        transform.Children.Add(new ScaleTransform3D(sizeX, sizeY, sizeZ));
        transform.Children.Add(new TranslateTransform3D(offsetX, offsetY, offsetZ));
        transform.Children.Add(sharedTransform);
        model.Transform = transform;
        group.Children.Add(model);
    }

    private void BuildTrailMarkers(Model3DGroup group)
    {
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(_trailDiffuseBrush));
        material.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(48, 180, 220, 255))));

        for (var i = 0; i < TrailPointCapacity; i++)
        {
            var translate = new TranslateTransform3D(0, -999, 0);
            _trailPointTransforms.Add(translate);

            var transform = new Transform3DGroup();
            transform.Children.Add(new ScaleTransform3D(0.12, 0.12, 0.12));
            transform.Children.Add(translate);

            var model = new GeometryModel3D(_trailPointMesh, material)
            {
                BackMaterial = material,
                Transform = transform
            };
            group.Children.Add(model);
        }
    }

    private static MeshGeometry3D BuildBoxMesh()
    {
        var half = 0.5;
        var mesh = new MeshGeometry3D();

        var p0 = new Point3D(-half, -half, -half);
        var p1 = new Point3D(half, -half, -half);
        var p2 = new Point3D(half, half, -half);
        var p3 = new Point3D(-half, half, -half);
        var p4 = new Point3D(-half, -half, half);
        var p5 = new Point3D(half, -half, half);
        var p6 = new Point3D(half, half, half);
        var p7 = new Point3D(-half, half, half);

        AddQuad(mesh, p0, p1, p2, p3); // back
        AddQuad(mesh, p5, p4, p7, p6); // front
        AddQuad(mesh, p4, p0, p3, p7); // left
        AddQuad(mesh, p1, p5, p6, p2); // right
        AddQuad(mesh, p3, p2, p6, p7); // top
        AddQuad(mesh, p4, p5, p1, p0); // bottom
        mesh.Freeze();
        return mesh;
    }

    private static MeshGeometry3D BuildSphereMesh(double radius, int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();
        for (var stack = 0; stack <= stacks; stack++)
        {
            var phi = Math.PI * stack / stacks;
            var y = radius * Math.Cos(phi);
            var r = radius * Math.Sin(phi);
            for (var slice = 0; slice <= slices; slice++)
            {
                var theta = 2.0 * Math.PI * slice / slices;
                var x = r * Math.Cos(theta);
                var z = r * Math.Sin(theta);
                mesh.Positions.Add(new Point3D(x, y, z));
            }
        }

        var rowLength = slices + 1;
        for (var stack = 0; stack < stacks; stack++)
        {
            for (var slice = 0; slice < slices; slice++)
            {
                var a = (stack * rowLength) + slice;
                var b = a + rowLength;
                var c = b + 1;
                var d = a + 1;

                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(c);

                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(d);
            }
        }

        mesh.Freeze();
        return mesh;
    }

    private static void AddQuad(MeshGeometry3D mesh, Point3D a, Point3D b, Point3D c, Point3D d)
    {
        var index = mesh.Positions.Count;
        mesh.Positions.Add(a);
        mesh.Positions.Add(b);
        mesh.Positions.Add(c);
        mesh.Positions.Add(d);
        mesh.TextureCoordinates.Add(new Point(0, 1));
        mesh.TextureCoordinates.Add(new Point(1, 1));
        mesh.TextureCoordinates.Add(new Point(1, 0));
        mesh.TextureCoordinates.Add(new Point(0, 0));
        mesh.TriangleIndices.Add(index);
        mesh.TriangleIndices.Add(index + 1);
        mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index);
        mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index + 3);
    }

    private static double FractalNoise(double x, double z, int octaves, double persistence)
    {
        var amplitude = 1.0;
        var frequency = 1.0;
        var total = 0.0;
        var max = 0.0;
        for (var i = 0; i < octaves; i++)
        {
            total += ValueNoise(x * frequency, z * frequency) * amplitude;
            max += amplitude;
            amplitude *= persistence;
            frequency *= 2.0;
        }

        return max <= 1e-9 ? 0.0 : total / max;
    }

    private static double ValueNoise(double x, double z)
    {
        var xi = (int)Math.Floor(x);
        var zi = (int)Math.Floor(z);
        var tx = x - xi;
        var tz = z - zi;

        var c00 = Hash01(xi, zi);
        var c10 = Hash01(xi + 1, zi);
        var c01 = Hash01(xi, zi + 1);
        var c11 = Hash01(xi + 1, zi + 1);

        var sx = SmoothStep(tx);
        var sz = SmoothStep(tz);

        var nx0 = Lerp(c00, c10, sx);
        var nx1 = Lerp(c01, c11, sx);
        return Lerp(nx0, nx1, sz);
    }

    private static double Hash01(int x, int z)
    {
        unchecked
        {
            var n = x * 374761393 + z * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return (n & 0x7FFFFFFF) / (double)int.MaxValue;
        }
    }

    private static double SmoothStep(double t) => t * t * (3.0 - (2.0 * t));
    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
    private static double LerpAngle(double currentDeg, double targetDeg, double t)
    {
        var clamped = Math.Clamp(t, 0.0, 1.0);
        var delta = ((targetDeg - currentDeg + 540.0) % 360.0) - 180.0;
        return NormalizeDegrees(currentDeg + (delta * clamped));
    }
    private static double MoveTowards(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + (Math.Sign(target - current) * maxDelta);
    }
    private static Color LerpColor(Color from, Color to, double t)
    {
        var clamped = Math.Clamp(t, 0.0, 1.0);
        return Color.FromRgb(
            (byte)Math.Clamp((from.R * (1.0 - clamped)) + (to.R * clamped), 0, 255),
            (byte)Math.Clamp((from.G * (1.0 - clamped)) + (to.G * clamped), 0, 255),
            (byte)Math.Clamp((from.B * (1.0 - clamped)) + (to.B * clamped), 0, 255));
    }

    private void UpdateDayNightCycle(double elapsedSeconds)
    {
        var phase = (elapsedSeconds % DayNightCycleSeconds) / DayNightCycleSeconds;
        var rawDaylight = 0.5 + (0.5 * Math.Cos(phase * Math.PI * 2.0));
        _daylight01 = SmoothStep(rawDaylight);
        _darkness01 = 1.0 - _daylight01;
        _dayNightStage = _daylight01 switch
        {
            > 0.72 => "day",
            < 0.18 => "night",
            _ => phase < 0.5 ? "dusk deepening" : "dawn lightening"
        };

        if (_worldAmbientLight is not null)
        {
            _worldAmbientLight.Color = LerpColor(Color.FromRgb(12, 18, 42), Color.FromRgb(58, 74, 112), _daylight01);
        }

        if (_worldSunLight is not null)
        {
            _worldSunLight.Color = LerpColor(Color.FromRgb(8, 10, 18), Color.FromRgb(190, 214, 255), _daylight01);
        }

        if (_worldMoonLight is not null)
        {
            _worldMoonLight.Color = LerpColor(Color.FromRgb(76, 96, 154), Color.FromRgb(42, 52, 84), _daylight01);
        }
        _skyBrush.Color = LerpColor(Color.FromRgb(1, 5, 18), Color.FromRgb(7, 25, 58), _daylight01);
    }

    private void RenderFrame()
    {
        var now = _frameStopwatch.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastFrameSeconds, 0.001, 0.080);
        _lastFrameSeconds = now;
        var nowMs = Environment.TickCount64;

        UpdateDayNightCycle(now);
        UpdateAvatar(dt);
        if (ShouldDispatchPhysicalBodyFrame(nowMs))
        {
            _ = DispatchPhysicalBodyFrameAsync(nowMs, _shutdown.Token);
        }
        UpdateSurvival(dt, now);
        UpdatePredators(dt);
        if (ShouldDispatchEnvironmentAudioInput(nowMs))
        {
            _ = DispatchEnvironmentAudioInputAsync(nowMs, _shutdown.Token);
        }
        UpdateAvatarVisual(dt);
        if (_followAvatarCamera)
        {
            UpdateFollowCamera();
        }

        UpdateCamera();
        if (now >= _nextSimulationHudUpdateSeconds)
        {
            _nextSimulationHudUpdateSeconds = now + SimulationHudUpdateIntervalSeconds;
            UpdateSimulationHud();
        }
    }

    private void UpdateFollowCamera()
    {
        var desiredYaw = NormalizeDegrees(_avatarHeadingDeg + FollowCameraYawOffsetDeg);
        _cameraTarget = new Point3D(
            Lerp(_cameraTarget.X, _avatarX, 0.16),
            Lerp(_cameraTarget.Y, _avatarY + 1.30, 0.12),
            Lerp(_cameraTarget.Z, _avatarZ, 0.16));
        _cameraYawDeg = LerpAngle(_cameraYawDeg, desiredYaw, 0.18);
        _cameraPitchDeg = Lerp(_cameraPitchDeg, FollowCameraPitchDeg, 0.12);
        _cameraDistance = Lerp(_cameraDistance, Math.Max(FollowCameraDistance, WorldSize * 0.72), 0.12);
    }

    private void UpdateSimulationHud()
    {
        MotorDispatchText.Text = $"Motor dispatch events: {_lastMotorDispatchCount}";
        MotorDriveText.Text = $"Motor drive L/R: {_leftMotorDrive:0.0} / {_rightMotorDrive:0.0}";
        ManipulatorDriveText.Text = $"Manipulator drive: {_manipulatorDrive:0.00}";
        MotorDecisionText.Text = _brainMotorDecisionText;
        MotorPathwayAuditText.Text = _motorPathwayAuditText;
        AvatarPoseText.Text = $"Avatar pose: x {_avatarX:0.00}, y {_avatarY:0.00}, z {_avatarZ:0.00}, body {_avatarHeadingDeg:0.0} deg, head {_avatarHeadYawDeg:0.0} deg";
        CollisionText.Text = $"Collision hits: {_collisionHits}";
        var mapped = _visitedTerrainCells.Count;
        var mappedPercent = _explorableTerrainCells > 0 ? (mapped * 100.0 / _explorableTerrainCells) : 0.0;
        TrailText.Text = $"Trail points: {_trailPoints.Count} | mapped: {mapped}/{Math.Max(_explorableTerrainCells, 1)} ({mappedPercent:0.0}%)";
        QueueRuntimeStateSnapshot(running: true);
    }

    private void UpdateAvatar(double dt)
    {
        SyncMotorDriveFromAvatarService();
        var moved = false;
        var blocked = false;

        // Brain motor output is the only locomotion driver. The simulator may block
        // movement through physics and feed consequences back, but it never steers.
        var actionOutput = _avatarService.PublishActionOutput();
        var (forwardSpeed, turnRateDeg) = actionOutput.Movement;
        var previousX = _avatarX;
        var previousZ = _avatarZ;
        UpdateAvatarHeadYaw(dt);

        _avatarHeadingDeg = AvatarKinematics.AdvanceHeading(_avatarHeadingDeg, turnRateDeg, dt);
        var (dirX, dirZ) = AvatarKinematics.ForwardDirection(_avatarHeadingDeg);

        var step = forwardSpeed * dt;
        var nextX = _avatarX + (dirX * step);
        var nextZ = _avatarZ + (dirZ * step);

        // Swept (sub-stepped) movement: at sprint speed and a slow tick the per-frame
        // displacement can approach or exceed AvatarRadius, which would let the
        // avatar tunnel through thin obstacles. Split the move into segments no
        // larger than 0.8 * AvatarRadius and advance through them, stopping at the
        // last clear sub-step if the path collides.
        var moveDx = dirX * step;
        var moveDz = dirZ * step;
        var moveLen = Math.Sqrt((moveDx * moveDx) + (moveDz * moveDz));
        var maxSweep = AvatarRadius * 0.8;
        double nextY;
        if (moveLen <= maxSweep)
        {
            // Short hop: existing single-test path.
            if (!Collides(nextX, nextZ, out nextY))
            {
                _avatarX = nextX;
                _avatarZ = nextZ;
                _avatarY = nextY;
                moved = true;
            }
        }
        else
        {
            // Long hop: walk the segment in sub-steps. Each sub-step is independently
            // collision-tested; we advance through the last clear one.
            var sweepCount = (int)Math.Ceiling(moveLen / maxSweep);
            var subDx = moveDx / sweepCount;
            var subDz = moveDz / sweepCount;
            var sweptX = _avatarX;
            var sweptZ = _avatarZ;
            var sweptY = _avatarY;
            var advanced = false;
            for (var step_i = 0; step_i < sweepCount; step_i++)
            {
                var trialX = sweptX + subDx;
                var trialZ = sweptZ + subDz;
                if (Collides(trialX, trialZ, out var trialY))
                {
                    break;
                }

                sweptX = trialX;
                sweptZ = trialZ;
                sweptY = trialY;
                advanced = true;
            }

            if (advanced)
            {
                _avatarX = sweptX;
                _avatarZ = sweptZ;
                _avatarY = sweptY;
                moved = true;
            }
        }

        if (!moved && !Collides(nextX, _avatarZ, out nextY))
        {
            _avatarX = nextX;
            _avatarY = nextY;
            moved = true;
        }
        else if (!moved && !Collides(_avatarX, nextZ, out nextY))
        {
            _avatarZ = nextZ;
            _avatarY = nextY;
            moved = true;
        }
        else if (!moved && Math.Abs(step) > 0.001)
        {
            blocked = true;
            // Brain-drive only: a blocked avatar stays put. Wall-contact pain below
            // feeds back so the brain can learn a different action.
        }

        _avatarTranslate.OffsetX = _avatarX;
        _avatarTranslate.OffsetY = _avatarY;
        _avatarTranslate.OffsetZ = _avatarZ;
        _avatarYawRotation.Angle = NormalizeDegrees(_avatarHeadingDeg + AvatarVisualYawOffsetDeg);
        _avatarHeadYawRotation.Angle = _avatarHeadYawDeg;
        RegisterVisitedTerrainCell(_avatarX, _avatarZ);
        _distanceTravelled += Math.Sqrt(
            ((_avatarX - previousX) * (_avatarX - previousX)) +
            ((_avatarZ - previousZ) * (_avatarZ - previousZ)));
        ApplyManipulatorOutput(actionOutput.Interaction, Environment.TickCount64);

        if (blocked)
        {
            _collisionHits++;
            _collisionPulse = 1.0;
        }

        _trailAccumulatorSeconds += dt;
        if (moved && _trailAccumulatorSeconds >= TrailSampleSeconds)
        {
            _trailAccumulatorSeconds = 0.0;
            AddTrailPoint(new Point3D(_avatarX, _avatarY + 0.04, _avatarZ));
        }

        _lastForwardSpeed = forwardSpeed;
        _lastTurnRateDeg = turnRateDeg;
    }

    private void UpdateAvatarHeadYaw(double dt)
        => _avatarHeadYawDeg = MoveTowards(_avatarHeadYawDeg, 0.0, AvatarHeadReturnRateDeg * dt);

    private bool TryFindClearSpawnCandidate(out double targetX, out double targetY, out double targetZ, out double targetHeadingDeg)
    {
        targetX = _avatarX;
        targetY = _avatarY;
        targetZ = _avatarZ;
        targetHeadingDeg = _avatarHeadingDeg;

        if (TryFindClearGridSpawnCandidate(out targetX, out targetY, out targetZ, out targetHeadingDeg))
        {
            return true;
        }

        if (TryFindRandomClearSpawnCandidate(out targetX, out targetY, out targetZ, out targetHeadingDeg))
        {
            return true;
        }

        // If inside shelter, first try to place the avatar outside the shelter opening.
        var nearestShelter = GetNearestShelter();
        var avatarIsInShelter = nearestShelter.HasValue && IsInShelter();
        if (nearestShelter.HasValue)
        {
            var shelter = nearestShelter.Value;
            ReadOnlySpan<double> shelterAnglesDeg = [0, 25, -25, 45, -45, 70, -70, 100, -100, 135, -135, 180];
            for (var offset = shelter.Radius + 1.6; offset <= shelter.Radius + 6.0; offset += 0.8)
            {
                for (var i = 0; i < shelterAnglesDeg.Length; i++)
                {
                    var angleRad = DegreesToRadians(shelterAnglesDeg[i]);
                    var outsideX = shelter.X + (Math.Sin(angleRad) * offset);
                    var outsideZ = shelter.Z + (Math.Cos(angleRad) * offset);
                    if (IsCollisionAt(outsideX, outsideZ, out var outsideTopY, ignoreStepHeight: true))
                    {
                        continue;
                    }

                    if (HasBlockingSegment(shelter.X, shelter.Z, outsideX, outsideZ, 0.45))
                    {
                        continue;
                    }

                    var clearance = EstimateLocalClearance(outsideX, outsideZ, 4.6);
                    if (clearance < SpawnSearchMinClearance)
                    {
                        continue;
                    }

                    targetX = outsideX;
                    targetY = outsideTopY + AvatarFootOffset;
                    targetZ = outsideZ;
                    targetHeadingDeg = NormalizeDegrees(shelterAnglesDeg[i]);
                    return true;
                }
            }
        }

        var headingRad = DegreesToRadians(_avatarHeadingDeg);
        ReadOnlySpan<double> headingOffsetsDeg = [0, 24, -24, 42, -42, 68, -68, 96, -96, 128, -128, 158, -158, 180];

        for (var radius = SpawnSearchRadiusMin; radius <= SpawnSearchRadiusMax; radius += SpawnSearchRadiusStep)
        {
            for (var i = 0; i < headingOffsetsDeg.Length; i++)
            {
                var probeHeading = headingRad + DegreesToRadians(headingOffsetsDeg[i]);
                var probeX = _avatarX + (Math.Sin(probeHeading) * radius);
                var probeZ = _avatarZ + (Math.Cos(probeHeading) * radius);
                if (IsCollisionAt(probeX, probeZ, out var probeTopY, ignoreStepHeight: true))
                {
                    continue;
                }

                // If we were trapped in shelter, prefer landing outside shelter bounds.
                if (nearestShelter.HasValue && avatarIsInShelter)
                {
                    var shelter = nearestShelter.Value;
                    var dx = probeX - shelter.X;
                    var dz = probeZ - shelter.Z;
                    var shelterClearRadius = shelter.Radius + 0.6;
                    if (((dx * dx) + (dz * dz)) <= (shelterClearRadius * shelterClearRadius))
                    {
                        continue;
                    }
                }

                var clearance = EstimateLocalClearance(probeX, probeZ, 4.6);
                if (clearance < SpawnSearchMinClearance)
                {
                    continue;
                }

                targetX = probeX;
                targetY = probeTopY + AvatarFootOffset;
                targetZ = probeZ;
                targetHeadingDeg = NormalizeDegrees(probeHeading * (180.0 / Math.PI));
                return true;
            }
        }

        if (TryFindRandomClearSpawnCandidate(out targetX, out targetY, out targetZ, out targetHeadingDeg))
        {
            return true;
        }

        return false;
    }

    private double TraceCollisionDistance(double dirX, double dirZ, double maxDistance)
    {
        if (_heights is null)
        {
            return maxDistance;
        }

        var invLen = 1.0 / Math.Max(0.0001, Math.Sqrt((dirX * dirX) + (dirZ * dirZ)));
        dirX *= invLen;
        dirZ *= invLen;

        const double stepSize = 0.32;
        for (var distance = stepSize; distance <= maxDistance; distance += stepSize)
        {
            var sampleX = _avatarX + (dirX * distance);
            var sampleZ = _avatarZ + (dirZ * distance);
            if (Collides(sampleX, sampleZ, out _))
            {
                return distance;
            }
        }

        return maxDistance;
    }

    private bool TryFindClearGridSpawnCandidate(out double targetX, out double targetY, out double targetZ, out double targetHeadingDeg)
    {
        targetX = _avatarX;
        targetY = _avatarY;
        targetZ = _avatarZ;
        targetHeadingDeg = _avatarHeadingDeg;

        if (_heights is null || !TryWorldToGrid(_avatarX, _avatarZ, out var startX, out var startZ))
        {
            return false;
        }

        var trappedInShelter = IsInShelter();
        var nearestShelter = GetNearestShelter();
        var visited = new bool[WorldSize, WorldSize];
        var queue = new Queue<(int X, int Z, int Depth)>();
        queue.Enqueue((startX, startZ, 0));
        visited[startX, startZ] = true;
        var bestScore = double.NegativeInfinity;
        var foundBest = false;
        double bestX = targetX;
        double bestY = targetY;
        double bestZ = targetZ;
        double bestHeading = targetHeadingDeg;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node.Depth > SpawnSearchGridDepth)
            {
                continue;
            }

            var worldHalf = (WorldSize - 1) * 0.5;
            var worldX = (node.X - worldHalf) * BlockSize;
            var worldZ = (node.Z - worldHalf) * BlockSize;
            if (!IsCollisionAt(worldX, worldZ, out var topY, ignoreStepHeight: true))
            {
                if (nearestShelter.HasValue)
                {
                    var shelter = nearestShelter.Value;
                    var dxShelter = worldX - shelter.X;
                    var dzShelter = worldZ - shelter.Z;
                    var shelterDist = Math.Sqrt((dxShelter * dxShelter) + (dzShelter * dzShelter));
                    if (trappedInShelter && shelterDist <= shelter.Radius + 0.7)
                    {
                        // Keep searching for points outside shelter bounds.
                        goto EnqueueNeighbors;
                    }
                }

                var clearance = EstimateLocalClearance(worldX, worldZ, 4.8);
                if (clearance < SpawnSearchMinClearance)
                {
                    goto EnqueueNeighbors;
                }

                var dist = Math.Sqrt(DistanceSquared(_avatarX, _avatarZ, worldX, worldZ));
                var key = (node.X * WorldSize) + node.Z;
                var unexploredBonus = _visitedTerrainCells.Contains(key) ? 0.0 : 0.75;
                var score = (clearance * 3.6) + (dist * 0.18) + unexploredBonus - (node.Depth * 0.012);
                if (score > bestScore)
                {
                    bestScore = score;
                    foundBest = true;
                    bestX = worldX;
                    bestY = topY + AvatarFootOffset;
                    bestZ = worldZ;
                    var bestHeadingRad = Math.Atan2(worldX - _avatarX, worldZ - _avatarZ);
                    bestHeading = NormalizeDegrees(bestHeadingRad * (180.0 / Math.PI));
                }
            }

            EnqueueNeighbors:
            var currentHeight = _heights[node.X, node.Z];
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    if ((Math.Abs(dx) + Math.Abs(dz)) != 1)
                    {
                        continue;
                    }

                    var nx = node.X + dx;
                    var nz = node.Z + dz;
                    if (nx < 1 || nx >= WorldSize - 1 || nz < 1 || nz >= WorldSize - 1)
                    {
                        continue;
                    }

                    if (visited[nx, nz])
                    {
                        continue;
                    }

                    var neighborHeight = _heights[nx, nz];
                    if (neighborHeight <= SeaLevel + 1)
                    {
                        continue;
                    }

                    // Initial spawn validation may cross steeper terrain than normal movement.
                    if (Math.Abs(neighborHeight - currentHeight) > 4)
                    {
                        continue;
                    }

                    visited[nx, nz] = true;
                    queue.Enqueue((nx, nz, node.Depth + 1));
                }
            }
        }

        if (foundBest)
        {
            targetX = bestX;
            targetY = bestY;
            targetZ = bestZ;
            targetHeadingDeg = bestHeading;
            return true;
        }

        return false;
    }

    private double EstimateLocalClearance(double worldX, double worldZ, double maxDistance)
    {
        ReadOnlySpan<double> probeOffsetsDeg = [0, 45, 90, 135, 180, 225, 270, 315];
        var minDistance = maxDistance;
        for (var i = 0; i < probeOffsetsDeg.Length; i++)
        {
            var rad = DegreesToRadians(probeOffsetsDeg[i]);
            var dirX = Math.Sin(rad);
            var dirZ = Math.Cos(rad);
            var hitDistance = TraceCollisionDistanceFrom(worldX, worldZ, dirX, dirZ, maxDistance);
            if (hitDistance < minDistance)
            {
                minDistance = hitDistance;
            }
        }

        return minDistance;
    }

    private double TraceCollisionDistanceFrom(double startX, double startZ, double dirX, double dirZ, double maxDistance)
    {
        if (_heights is null)
        {
            return maxDistance;
        }

        var invLen = 1.0 / Math.Max(0.0001, Math.Sqrt((dirX * dirX) + (dirZ * dirZ)));
        dirX *= invLen;
        dirZ *= invLen;
        const double stepSize = 0.32;

        for (var distance = stepSize; distance <= maxDistance; distance += stepSize)
        {
            var sampleX = startX + (dirX * distance);
            var sampleZ = startZ + (dirZ * distance);
            if (IsCollisionAt(sampleX, sampleZ, out _, ignoreStepHeight: true))
            {
                return distance;
            }
        }

        return maxDistance;
    }

    private bool HasBlockingSegment(double fromX, double fromZ, double toX, double toZ, double step)
    {
        var dx = toX - fromX;
        var dz = toZ - fromZ;
        var length = Math.Sqrt((dx * dx) + (dz * dz));
        if (length < 0.001)
        {
            return false;
        }

        var samples = Math.Max(2, (int)Math.Ceiling(length / Math.Max(0.12, step)));
        for (var i = 1; i <= samples; i++)
        {
            var t = i / (double)samples;
            var x = fromX + (dx * t);
            var z = fromZ + (dz * t);
            if (IsCollisionAt(x, z, out _, ignoreStepHeight: true))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindRandomClearSpawnCandidate(out double targetX, out double targetY, out double targetZ, out double targetHeadingDeg)
    {
        targetX = _avatarX;
        targetY = _avatarY;
        targetZ = _avatarZ;
        targetHeadingDeg = _avatarHeadingDeg;
        if (_heights is null)
        {
            return false;
        }

        var random = new Random(unchecked((int)(Environment.TickCount64 ^ (_seed * 104729L))));
        var worldHalf = (WorldSize - 1) * 0.5;
        var nearestShelter = GetNearestShelter();
        var bestScore = double.NegativeInfinity;
        var found = false;

        for (var i = 0; i < 520; i++)
        {
            var gx = random.Next(2, WorldSize - 2);
            var gz = random.Next(2, WorldSize - 2);
            if (_heights[gx, gz] <= SeaLevel + 1)
            {
                continue;
            }

            var worldX = (gx - worldHalf) * BlockSize;
            var worldZ = (gz - worldHalf) * BlockSize;
            if (IsCollisionAt(worldX, worldZ, out var topY, ignoreStepHeight: true))
            {
                continue;
            }

            var clearance = EstimateLocalClearance(worldX, worldZ, 5.2);
            if (clearance < SpawnSearchMinClearance)
            {
                continue;
            }

            var dist = Math.Sqrt(DistanceSquared(_avatarX, _avatarZ, worldX, worldZ));
            var shelterPenalty = 0.0;
            if (nearestShelter.HasValue)
            {
                var shelter = nearestShelter.Value;
                var dxShelter = worldX - shelter.X;
                var dzShelter = worldZ - shelter.Z;
                var shelterDist = Math.Sqrt((dxShelter * dxShelter) + (dzShelter * dzShelter));
                if (shelterDist <= shelter.Radius + 1.1)
                {
                    continue;
                }

                shelterPenalty = Math.Max(0.0, (shelter.Radius + 7.5) - shelterDist) * 0.35;
            }

            var cellKey = (gx * WorldSize) + gz;
            var unexploredBonus = _visitedTerrainCells.Contains(cellKey) ? 0.0 : 1.4;
            var score = (clearance * 4.0) + (dist * 0.20) + unexploredBonus - shelterPenalty;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            found = true;
            targetX = worldX;
            targetY = topY + AvatarFootOffset;
            targetZ = worldZ;
            targetHeadingDeg = NormalizeDegrees(Math.Atan2(worldX - _avatarX, worldZ - _avatarZ) * (180.0 / Math.PI));
        }

        return found;
    }

    private ShelterSite? GetNearestShelter()
    {
        if (_shelterSites.Count == 0)
        {
            return null;
        }

        var nearest = _shelterSites[0];
        var bestDistSq = double.MaxValue;
        for (var i = 0; i < _shelterSites.Count; i++)
        {
            var shelter = _shelterSites[i];
            var dx = _avatarX - shelter.X;
            var dz = _avatarZ - shelter.Z;
            var distSq = (dx * dx) + (dz * dz);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = shelter;
            }
        }

        return nearest;
    }

    private void UpdateSurvival(double dt, double nowSeconds)
    {
        var physiology = AvatarWorldDynamics.AdvancePhysiology(
            GetPhysiologyState(),
            WorldPhysiologyOptions,
            dt,
            _metabolicBurnRate,
            _sleepState,
            IsInShelter());
        SetPhysiologyState(physiology);

        if (nowSeconds < _nextSurvivalHudUpdateSeconds)
        {
            return;
        }

        _nextSurvivalHudUpdateSeconds = nowSeconds + SurvivalHudUpdateIntervalSeconds;
        SurvivalEnergyText.Text = $"Energy reserve: {(int)(_storedEnergyJoules / NominalStoredEnergyJoules * 100)}%";
        SurvivalTissueIntegrityText.Text = $"Tissue integrity: {(int)(_tissueIntegrity * 100)}%";
        SurvivalHydrationText.Text = $"Hydration: {(int)(_hydrationFraction * 100)}%";
        var nearestPredator = FindNearest(_predators);
        SurvivalThreatText.Text = nearestPredator.Distance >= 999.0
            ? "Nearest predator: none"
            : $"Nearest predator: {nearestPredator.Distance:0.0} units";
        SurvivalFoodText.Text = $"Food collected: {_foodConsumed}";
        var weaponProfile = GetActiveWeaponRangeProfile();
        SurvivalWeaponText.Text = _weaponCharges > 0
            ? $"Carried weapon charges: {_weaponCharges} ({weaponProfile.ToString().ToLowerInvariant()})"
            : "Carried weapon charges: 0";
        SurvivalShelterText.Text = $"Shelter: {(IsInShelter() ? "inside" : "outside")} | neuronal sleep: {(_sleepState ? "yes" : "no")}";
        SurvivalPredatorText.Text = $"Predators: {_predators.Count} active";
        SurvivalInteractionText.Text =
            $"Physical interactions: {_interactionSuccesses}/{_interactionAttempts} | devices {_weaponPickupsCollected} | water {_waterInteractions} | predators neutralized {_predatorsNeutralized}";
        DayNightText.Text =
            $"Light cycle: {_dayNightStage}, daylight {(int)(_daylight01 * 100)}%, darkness {(int)(_darkness01 * 100)}%";
    }

    private void UpdatePredators(double dt)
    {
        if (_predators.Count == 0 || _heights is null)
        {
            return;
        }

        for (var i = 0; i < _predators.Count; i++)
        {
            var predator = _predators[i];
            var initialDx = _avatarX - predator.Position.X;
            var initialDz = _avatarZ - predator.Position.Z;
            var initialDistance = Math.Sqrt((initialDx * initialDx) + (initialDz * initialDz));

            var targetHeading = predator.HeadingDeg;
            var speed = 0.55 * _predatorSpeedScale;
            if (initialDistance <= _predatorSenseRadius)
            {
                targetHeading = NormalizeDegrees(Math.Atan2(initialDx, initialDz) * (180.0 / Math.PI));
                speed = 1.10 * _predatorSpeedScale;
            }
            else
            {
                if (predator.PatrolPoints.Count > 0)
                {
                    var target = predator.PatrolPoints[predator.PatrolIndex];
                    var px = target.X - predator.Position.X;
                    var pz = target.Z - predator.Position.Z;
                    var patrolDistance = Math.Sqrt((px * px) + (pz * pz));
                    if (patrolDistance < 0.35)
                    {
                        predator.PatrolIndex = (predator.PatrolIndex + 1) % predator.PatrolPoints.Count;
                    }
                    targetHeading = NormalizeDegrees(Math.Atan2(px, pz) * (180.0 / Math.PI));
                }
                else
                {
                    targetHeading = NormalizeDegrees(targetHeading + (Math.Sin((_frameStopwatch.Elapsed.TotalSeconds + i) * 0.35) * 18.0 * dt));
                }
            }

            var headingRad = DegreesToRadians(targetHeading);
            var step = speed * dt;
            var nextX = predator.Position.X + (Math.Sin(headingRad) * step);
            var nextZ = predator.Position.Z + (Math.Cos(headingRad) * step);

            if (!Collides(nextX, nextZ, out var nextY))
            {
                predator.Position = new Point3D(nextX, nextY + 0.18, nextZ);
            }

            predator.HeadingDeg = targetHeading;
            predator.YawRotation.Angle = NormalizeDegrees(targetHeading);
            predator.Transform.OffsetX = predator.Position.X;
            predator.Transform.OffsetY = predator.Position.Y;
            predator.Transform.OffsetZ = predator.Position.Z;
            predator.ThreatTranslate.OffsetX = predator.Position.X;
            predator.ThreatTranslate.OffsetY = predator.Position.Y - 0.06;
            predator.ThreatTranslate.OffsetZ = predator.Position.Z;

            var distance = GetDistanceToAvatar(predator.Position.X, predator.Position.Z);
            if (distance <= PredatorStrikeRadius)
            {
                SetPhysiologyState(AvatarWorldDynamics.ApplyPredatorContact(
                    GetPhysiologyState(),
                    dt,
                    damageRatePerSecond: 0.08,
                    speedScale: _predatorSpeedScale));
                _collisionHits++;
                _collisionPulse = 1.0;
                QueuePredatorContact(Environment.TickCount64);
            }

            _predators[i] = predator;
        }
    }

    private double GetDistanceToAvatar(double worldX, double worldZ)
    {
        var dx = _avatarX - worldX;
        var dz = _avatarZ - worldZ;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    private void ApplyManipulatorOutput(AvatarInteractionOutput output, long nowMs)
    {
        _manipulatorDrive = Math.Max(0.0, output.ManipulatorDrive);
        if (_manipulatorDrive <= ManipulatorReleaseDrive)
        {
            _manipulatorLatched = false;
        }

        if (_manipulatorDrive < ManipulatorActivationDrive ||
            _manipulatorLatched ||
            (nowMs - _lastManipulatorCycleMs) < ManipulatorCycleMs)
        {
            return;
        }

        _manipulatorLatched = true;
        _lastManipulatorCycleMs = nowMs;
        _interactionAttempts++;

        // The body exposes one general effector. Environment geometry and the
        // physically carried object determine the consequence; the host never
        // receives or interprets a symbolic action name.
        if (TryManipulateNearestPickup() || TryDrinkNearbyWater() || TryDischargeCarriedDevice())
        {
            _interactionSuccesses++;
        }
    }

    private bool TryManipulateNearestPickup()
    {
        var nearestFoodIndex = -1;
        var nearestWeaponIndex = -1;
        var nearestFoodDistanceSq = double.MaxValue;
        var nearestWeaponDistanceSq = double.MaxValue;

        for (var i = 0; i < _foodPickups.Count; i++)
        {
            var pickup = _foodPickups[i];
            if (!pickup.Active || !AvatarPhysicalInteraction.IsWithinEffectorCone(
                    _avatarX,
                    _avatarZ,
                    _avatarHeadingDeg,
                    pickup.Position.X,
                    pickup.Position.Z,
                    ManipulatorReach,
                    ManipulatorHalfAngleDeg))
            {
                continue;
            }

            var distanceSq = DistanceSquared(_avatarX, _avatarZ, pickup.Position.X, pickup.Position.Z);
            if (distanceSq < nearestFoodDistanceSq)
            {
                nearestFoodDistanceSq = distanceSq;
                nearestFoodIndex = i;
            }
        }

        for (var i = 0; i < _weaponPickups.Count; i++)
        {
            var pickup = _weaponPickups[i];
            if (!pickup.Active || !AvatarPhysicalInteraction.IsWithinEffectorCone(
                    _avatarX,
                    _avatarZ,
                    _avatarHeadingDeg,
                    pickup.Position.X,
                    pickup.Position.Z,
                    ManipulatorReach,
                    ManipulatorHalfAngleDeg))
            {
                continue;
            }

            var distanceSq = DistanceSquared(_avatarX, _avatarZ, pickup.Position.X, pickup.Position.Z);
            if (distanceSq < nearestWeaponDistanceSq)
            {
                nearestWeaponDistanceSq = distanceSq;
                nearestWeaponIndex = i;
            }
        }

        if (nearestFoodIndex < 0 && nearestWeaponIndex < 0)
        {
            return false;
        }

        if (nearestFoodIndex >= 0 && nearestFoodDistanceSq <= nearestWeaponDistanceSq)
        {
            var pickup = _foodPickups[nearestFoodIndex];
            pickup.Active = false;
            pickup.Transform.OffsetY = -999;
            _foodConsumed++;
            SetPhysiologyState(AvatarWorldDynamics.ConsumeFood(
                GetPhysiologyState(),
                WorldPhysiologyOptions,
                nominalEnergyFraction: 0.35));
            _foodPickups[nearestFoodIndex] = pickup;
            QueueManipulatorContact(45.0, 2.5, 700.0);
            return true;
        }

        var weapon = _weaponPickups[nearestWeaponIndex];
        if (!_deviceInventory.TryCollect(weapon.RangeProfile, capacity: 3, out var collectedInventory))
        {
            return false;
        }

        weapon.Active = false;
        weapon.Transform.OffsetY = -999;
        _deviceInventory = collectedInventory;
        _weaponPickupsCollected++;
        UpdateWeaponChargeCache();
        _weaponPickups[nearestWeaponIndex] = weapon;
        QueueManipulatorContact(70.0, 3.5, 850.0);
        return true;
    }

    private bool TryDrinkNearbyWater()
    {
        if (_hydrationFraction >= 0.995 || EstimateWaterAuditoryProximity(ManipulatorReach) <= 0.0)
        {
            return false;
        }

        SetPhysiologyState(AvatarWorldDynamics.Drink(GetPhysiologyState(), hydrationFraction: 0.38));
        _waterInteractions++;
        QueueManipulatorContact(22.0, 1.2, 1_450.0);
        return true;
    }

    private bool TryDischargeCarriedDevice()
    {
        var profile = GetActiveWeaponRangeProfile();
        if (profile == AvatarDeviceRangeProfile.None || _predators.Count == 0)
        {
            return false;
        }

        var range = profile == AvatarDeviceRangeProfile.Long ? LongWeaponRange : ShortWeaponRange;
        var halfAngle = profile == AvatarDeviceRangeProfile.Long ? LongWeaponHalfAngleDeg : ShortWeaponHalfAngleDeg;
        var selectedIndex = -1;
        var selectedDistanceSq = double.MaxValue;
        for (var i = 0; i < _predators.Count; i++)
        {
            var predator = _predators[i];
            if (!AvatarPhysicalInteraction.IsWithinEffectorCone(
                    _avatarX,
                    _avatarZ,
                    _avatarHeadingDeg,
                    predator.Position.X,
                    predator.Position.Z,
                    range,
                    halfAngle) ||
                HasBlockingSegment(_avatarX, _avatarZ, predator.Position.X, predator.Position.Z, 0.30))
            {
                continue;
            }

            var distanceSq = DistanceSquared(_avatarX, _avatarZ, predator.Position.X, predator.Position.Z);
            if (distanceSq < selectedDistanceSq)
            {
                selectedDistanceSq = distanceSq;
                selectedIndex = i;
            }
        }

        if (selectedIndex < 0)
        {
            return false;
        }

        if (!_deviceInventory.TryDischarge(profile, out var dischargedInventory))
        {
            return false;
        }

        _deviceInventory = dischargedInventory;
        UpdateWeaponChargeCache();
        var target = _predators[selectedIndex];
        target.Transform.OffsetY = -999;
        target.ThreatTranslate.OffsetY = -999;
        target.PathModel.Transform = new TranslateTransform3D(0.0, -999.0, 0.0);
        _predators.RemoveAt(selectedIndex);
        _predatorsNeutralized++;
        QueueManipulatorContact(320.0, 18.0, 620.0);
        return true;
    }

    private void QueueManipulatorContact(double forceNewtons, double impulseNewtonSeconds, double contactAreaSquareMillimeters)
    {
        QueuePhysicalContact(new PendingPhysicalContact(
            BodyPositionX: 0.22f,
            BodyPositionY: 0.48f,
            BodyPositionZ: 0.58f,
            SurfaceNormalX: 0f,
            SurfaceNormalY: 0f,
            SurfaceNormalZ: -1f,
            ForceNewtons: (float)forceNewtons,
            ImpulseNewtonSeconds: (float)impulseNewtonSeconds,
            PenetrationMillimeters: 0.8f,
            TangentialSpeedMetersPerSecond: 0f,
            ContactAreaSquareMillimeters: (float)contactAreaSquareMillimeters,
            DurationMilliseconds: ManipulatorCycleMs,
            InputSource: "avatar_world_manipulator_contact"));
    }

    private void QueuePredatorContact(long nowMs)
    {
        if ((nowMs - _lastPredatorContactMs) < 180)
        {
            return;
        }

        _lastPredatorContactMs = nowMs;
        QueuePhysicalContact(new PendingPhysicalContact(
            BodyPositionX: 0f,
            BodyPositionY: 0.32f,
            BodyPositionZ: 0.18f,
            SurfaceNormalX: 0f,
            SurfaceNormalY: 0f,
            SurfaceNormalZ: -1f,
            ForceNewtons: (float)(1_900.0 * _predatorSpeedScale),
            ImpulseNewtonSeconds: (float)(85.0 * _predatorSpeedScale),
            PenetrationMillimeters: 9f,
            TangentialSpeedMetersPerSecond: (float)(1.10 * _predatorSpeedScale),
            ContactAreaSquareMillimeters: 2_400f,
            DurationMilliseconds: 180f,
            InputSource: "avatar_world_external_contact"));
    }

    private void QueuePhysicalContact(PendingPhysicalContact contact)
    {
        if (_pendingPhysicalContacts.Count >= 8)
        {
            _pendingPhysicalContacts.Dequeue();
        }

        _pendingPhysicalContacts.Enqueue(contact);
    }

    private void UpdateWeaponChargeCache()
        => _weaponCharges = _deviceInventory.TotalCharges;

    private AvatarDeviceRangeProfile GetActiveWeaponRangeProfile()
        => _deviceInventory.ActiveProfile;

    private AvatarPhysiologyState GetPhysiologyState()
        => new(_storedEnergyJoules, _hydrationFraction, _tissueIntegrity);

    private void SetPhysiologyState(AvatarPhysiologyState state)
    {
        _storedEnergyJoules = state.StoredEnergyJoules;
        _hydrationFraction = state.HydrationFraction;
        _tissueIntegrity = state.TissueIntegrityFraction;
    }

    private bool IsInShelter()
    {
        if (_shelterSites.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < _shelterSites.Count; i++)
        {
            var shelter = _shelterSites[i];
            var dx = _avatarX - shelter.X;
            var dz = _avatarZ - shelter.Z;
            var radiusSq = shelter.Radius * shelter.Radius;
            if (((dx * dx) + (dz * dz)) <= radiusSq && _avatarY >= shelter.BaseY && _avatarY <= shelter.BaseY + 2.8)
            {
                return true;
            }
        }

        return false;
    }

    private double GetNearestShelterDistance()
    {
        if (_shelterSites.Count == 0)
        {
            return 0.0;
        }

        var bestSq = double.MaxValue;
        for (var i = 0; i < _shelterSites.Count; i++)
        {
            var shelter = _shelterSites[i];
            var dx = _avatarX - shelter.X;
            var dz = _avatarZ - shelter.Z;
            var distanceSq = (dx * dx) + (dz * dz);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
            }
        }

        return Math.Sqrt(bestSq);
    }

    private async Task<bool> TryPostEnvironmentAudioFrameAsync(
        string endpoint,
        AvatarAudioFrame frame,
        CancellationToken token)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(EnvironmentAudioDispatchTimeoutMs));
            var result = await AvatarControlApi.PostCochlearFrameAsync(
                _auditoryInputHttpClient,
                new Uri(endpoint),
                frame,
                cancellationToken: timeout.Token);
            if (result.Accepted && result.TargetInstances > 0)
            {
                RegisterOptionalBrainInputSuccess("environment audio");
                _cochlearFramesAccepted++;
                return true;
            }

            RegisterEnvironmentAudioFailure("cochlear frame has no live Cochlea dispatch target");
            return false;
        }
        catch (TaskCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            RegisterEnvironmentAudioFailure("timeout posting cochlear audio frame");
            return false;
        }
        catch (Exception ex)
        {
            RegisterEnvironmentAudioFailure($"{ex.GetType().Name}: {TrimForLog(ex.Message, 160)}");
            return false;
        }
    }

    private double ResolveSoundPan(Point3D position)
    {
        var forward = GetAvatarVisualForward();
        var dx = position.X - _avatarX;
        var dz = position.Z - _avatarZ;
        var distance = Math.Sqrt((dx * dx) + (dz * dz));
        if (distance < 0.001)
        {
            return 0.0;
        }

        var lateral = ((forward.X * dz) - (forward.Z * dx)) / distance;
        return Math.Clamp(-lateral, -1.0, 1.0);
    }

    private (Point3D Position, double Distance) FindNearest(IReadOnlyList<WeaponPickup> pickups)
    {
        var bestSq = double.MaxValue;
        var bestPos = new Point3D();
        foreach (var pickup in pickups)
        {
            if (!pickup.Active)
            {
                continue;
            }

            var dx = _avatarX - pickup.Position.X;
            var dz = _avatarZ - pickup.Position.Z;
            var distanceSq = (dx * dx) + (dz * dz);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                bestPos = pickup.Position;
            }
        }

        return (bestPos, bestSq == double.MaxValue ? 999.0 : Math.Sqrt(bestSq));
    }

    private (Point3D Position, double Distance) FindNearestShelter()
    {
        if (_shelterSites.Count == 0)
        {
            return (new Point3D(_avatarX, _avatarY, _avatarZ), 999.0);
        }

        var bestSq = double.MaxValue;
        var bestPos = new Point3D(_avatarX, _avatarY, _avatarZ);
        for (var i = 0; i < _shelterSites.Count; i++)
        {
            var shelter = _shelterSites[i];
            var dx = _avatarX - shelter.X;
            var dz = _avatarZ - shelter.Z;
            var distanceSq = (dx * dx) + (dz * dz);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                bestPos = new Point3D(shelter.X, shelter.BaseY + 0.8, shelter.Z);
            }
        }

        return (bestPos, Math.Sqrt(bestSq));
    }

    private bool TryClassifyAheadTerrain(double distance, out BlockKind kind)
    {
        kind = BlockKind.Grass;
        if (_heights is null)
        {
            return false;
        }

        var forward = GetAvatarVisualForward();
        var probeX = _avatarX + (forward.X * distance);
        var probeZ = _avatarZ + (forward.Z * distance);
        if (!TryWorldToGrid(probeX, probeZ, out var gx, out var gz))
        {
            return false;
        }

        if (TryGetSurfaceOverride(probeX, probeZ, out var overrideKind))
        {
            kind = overrideKind;
            return true;
        }

        kind = ResolveTerrainKindFromHeight(_heights[gx, gz]);
        return true;
    }

    private static BlockKind ResolveTerrainKindFromHeight(int cellHeight)
    {
        if (cellHeight < SeaLevel)
        {
            return BlockKind.Water;
        }

        if (cellHeight <= SeaLevel + 1)
        {
            return BlockKind.Sand;
        }

        if (cellHeight >= SeaLevel + 10)
        {
            return BlockKind.Stone;
        }

        if (cellHeight >= SeaLevel + 6)
        {
            return BlockKind.Dirt;
        }

        return BlockKind.Grass;
    }

    private (Point3D Position, double Distance) FindNearest(IReadOnlyList<FoodPickup> pickups)
    {
        var bestSq = double.MaxValue;
        var bestPos = new Point3D();
        foreach (var pickup in pickups)
        {
            if (!pickup.Active)
            {
                continue;
            }

            var dx = _avatarX - pickup.Position.X;
            var dz = _avatarZ - pickup.Position.Z;
            var distanceSq = (dx * dx) + (dz * dz);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                bestPos = pickup.Position;
            }
        }

        return (bestPos, bestSq == double.MaxValue ? 999.0 : Math.Sqrt(bestSq));
    }

    private (Point3D Position, double Distance) FindNearest(IReadOnlyList<PredatorNpc> predators)
    {
        var bestSq = double.MaxValue;
        var bestPos = new Point3D();
        foreach (var predator in predators)
        {
            var dx = _avatarX - predator.Position.X;
            var dz = _avatarZ - predator.Position.Z;
            var distanceSq = (dx * dx) + (dz * dz);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                bestPos = predator.Position;
            }
        }

        return (bestPos, bestSq == double.MaxValue ? 999.0 : Math.Sqrt(bestSq));
    }

    private void UpdateAvatarVisual(double dt)
    {
        _collisionPulse = Math.Max(0.0, _collisionPulse - (dt * 3.2));
        var blend = _collisionPulse;

        var baseDiffuse = Color.FromRgb(255, 164, 88);
        var baseEmissive = Color.FromRgb(142, 70, 30);
        var hitDiffuse = Color.FromRgb(255, 102, 102);
        var hitEmissive = Color.FromRgb(255, 48, 48);

        _avatarDiffuseBrush.Color = LerpColor(baseDiffuse, hitDiffuse, blend);
        _avatarEmissiveBrush.Color = LerpColor(baseEmissive, hitEmissive, blend);
    }

    private void UpdateAvatarVisionPreview()
    {
        if (_avatarPreviewBitmap is null || _heights is null)
        {
            return;
        }

        var width = _avatarPreviewBitmap.PixelWidth;
        var height = _avatarPreviewBitmap.PixelHeight;
        var stride = width * 4;
        var eyelidsChanged = UpdateAvatarPreviewEyelidState();
        var presentedFrame = false;
        var frame = Interlocked.Exchange(ref _pendingVisionComputeResult, null);
        if (frame is not null && frame.Generation == Volatile.Read(ref _visionGeneration))
        {
            var lagMs = Math.Max(0, Environment.TickCount64 - frame.CaptureTimestampMs);
            if (lagMs > VisionPreviewDropLagMs)
            {
                DropStaleVisionPreviewFrame(frame, lagMs);
            }
            else
            {
                PresentAvatarVisionFrame(frame);
                presentedFrame = true;
                TryDispatchAvatarVisionFrame(frame);
            }
        }

        if (!presentedFrame && eyelidsChanged)
        {
            RepaintAvatarVisionPreview(width, height, stride);
        }

        var warning = Interlocked.Exchange(ref _visionComputeWarning, string.Empty);
        if (!string.IsNullOrWhiteSpace(warning))
        {
            Log($"Vision compute warning: {warning}");
        }

        QueueVisionPreviewCompute(width, height, stride);
    }

    private void QueueVisionPreviewCompute(int width, int height, int stride)
    {
        if (_heights is null)
        {
            return;
        }

        if (Volatile.Read(ref _pendingVisionComputeRequest) is not null)
        {
            return;
        }

        var request = BuildVisionComputeRequest(width, height, stride);
        if (request is null)
        {
            return;
        }
        Interlocked.Exchange(ref _pendingVisionComputeRequest, new VisionComputeRequestEnvelope(request.Value));
        _visionRequestSignal.Set();
    }

    private void PresentAvatarVisionFrame(VisionComputeResult frame)
    {
        var lagMs = Math.Max(0, Environment.TickCount64 - frame.CaptureTimestampMs);
        var isStale = lagMs > VisionPreviewMaxLagMs;

        var rect = frame.Width == AvatarPreviewWidth && frame.Height == AvatarPreviewHeight
            ? AvatarPreviewBitmapRect
            : new Int32Rect(0, 0, frame.Width, frame.Height);
        _avatarPreviewPixels = frame.Pixels;
        var displayPixels = ApplyAvatarPreviewEyelidOverlay(frame.Pixels, frame.Width, frame.Height, frame.Stride);
        _avatarPreviewBitmap!.WritePixels(rect, displayPixels, frame.Stride, 0);
        UpdateAvatarPreviewInfo(frame, lagMs, isStale);
        if (lagMs <= VisionBrainInputMaxLagMs)
        {
            _avatarService.PostSightInputFrame(frame.SightFrame);
        }

        if (isStale)
        {
            LogStaleVisionPreviewFrame(lagMs);
        }
    }

    private bool UpdateAvatarPreviewEyelidState()
    {
        var nowMs = Environment.TickCount64;
        var previousMs = Interlocked.Exchange(ref _lastAvatarPreviewEyelidUpdateMs, nowMs);
        if (previousMs <= 0)
        {
            return false;
        }

        var dt = Math.Clamp((nowMs - previousMs) / 1000.0, 0.0, 0.12);
        var target = _sleepState ? 1.0 : 0.0;
        var rate = _sleepState ? VisionPreviewEyelidCloseRate : VisionPreviewEyelidOpenRate;
        var previous = _avatarPreviewEyelidClosure;
        _avatarPreviewEyelidClosure = MoveTowards(_avatarPreviewEyelidClosure, target, rate * dt);
        return Math.Abs(_avatarPreviewEyelidClosure - previous) > 0.002;
    }

    private void RepaintAvatarVisionPreview(int width, int height, int stride)
    {
        var source = _avatarPreviewPixels;
        if (source is null || source.Length < stride * height)
        {
            source = EnsureAvatarPreviewBuffer(stride, height);
            Array.Clear(source, 0, stride * height);
        }

        var displayPixels = ApplyAvatarPreviewEyelidOverlay(source, width, height, stride);
        var rect = width == AvatarPreviewWidth && height == AvatarPreviewHeight
            ? AvatarPreviewBitmapRect
            : new Int32Rect(0, 0, width, height);
        _avatarPreviewBitmap!.WritePixels(rect, displayPixels, stride, 0);
        if (_sleepState || _avatarPreviewEyelidClosure > 0.02)
        {
            var state = _sleepState ? "eyes closed" : "eyes opening";
            AvatarPreviewInfoText.Text = $"Preview: {state} ({_avatarPreviewEyelidClosure * 100.0:0}% closed)";
        }
    }

    private byte[] ApplyAvatarPreviewEyelidOverlay(byte[] source, int width, int height, int stride)
    {
        var required = stride * height;
        if (_avatarPreviewDisplayPixels is null || _avatarPreviewDisplayPixels.Length != required)
        {
            _avatarPreviewDisplayPixels = new byte[required];
        }

        if (source.Length < required)
        {
            Array.Clear(_avatarPreviewDisplayPixels, 0, required);
        }

        source.AsSpan(0, Math.Min(required, source.Length)).CopyTo(_avatarPreviewDisplayPixels);
        if (_avatarPreviewEyelidClosure <= 0.002)
        {
            return _avatarPreviewDisplayPixels;
        }

        var closure = Math.Clamp(_avatarPreviewEyelidClosure, 0.0, 1.0);
        var halfClosedRows = (int)Math.Round(height * 0.5 * closure);
        var dim = Math.Clamp(1.0 - (closure * 0.72), 0.0, 1.0);
        for (var y = 0; y < height; y++)
        {
            var lidStrength = y < halfClosedRows || y >= height - halfClosedRows
                ? 1.0
                : closure >= 0.98
                    ? 1.0
                    : 0.0;
            var rowOffset = y * stride;
            for (var x = 0; x < width; x++)
            {
                var offset = rowOffset + (x * 4);
                if (lidStrength >= 1.0)
                {
                    _avatarPreviewDisplayPixels[offset] = 0;
                    _avatarPreviewDisplayPixels[offset + 1] = 0;
                    _avatarPreviewDisplayPixels[offset + 2] = 0;
                }
                else if (dim < 0.999)
                {
                    _avatarPreviewDisplayPixels[offset] = (byte)Math.Clamp(_avatarPreviewDisplayPixels[offset] * dim, 0.0, 255.0);
                    _avatarPreviewDisplayPixels[offset + 1] = (byte)Math.Clamp(_avatarPreviewDisplayPixels[offset + 1] * dim, 0.0, 255.0);
                    _avatarPreviewDisplayPixels[offset + 2] = (byte)Math.Clamp(_avatarPreviewDisplayPixels[offset + 2] * dim, 0.0, 255.0);
                }

                _avatarPreviewDisplayPixels[offset + 3] = 255;
            }
        }

        return _avatarPreviewDisplayPixels;
    }

    private void TryDispatchAvatarVisionFrame(VisionComputeResult frame)
    {
        if (!_sendAvatarVisionToBrain)
        {
            return;
        }

        var nowMs = Environment.TickCount64;
        var lagMs = Math.Max(0, nowMs - frame.CaptureTimestampMs);
        var isTooOldForBrain = lagMs > VisionBrainInputMaxLagMs;
        if (isTooOldForBrain ||
            _visionDispatchBackoff.IsBlocked(nowMs))
        {
            return;
        }

        var baseDispatchIntervalMs = GetVisionDispatchIntervalMs();
        var pressureDecision = EvaluateBrainInputPressure(
            "avatar vision",
            nowMs,
            AvatarInputPriority.Critical,
            baseDispatchIntervalMs);
        if (pressureDecision.ShouldPause)
        {
            LogOptionalBrainInputPause("avatar vision", pressureDecision.Reason, nowMs);
            return;
        }

        var minDispatchIntervalMs = Math.Max(baseDispatchIntervalMs, pressureDecision.MinimumIntervalMs);
        if (_visionDispatchInFlight || (nowMs - _lastVisionDispatchMs) < minDispatchIntervalMs)
        {
            return;
        }

        _lastVisionDispatchMs = nowMs;
        _visionDispatchInFlight = true;
        _ = DispatchAvatarVisionAsync(frame.SightFrame, _shutdown.Token);
    }

    private async Task DispatchAvatarVisionAsync(AvatarSightFrame frame, CancellationToken token)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(AvatarVisionDispatchTimeoutMs);
            var dispatch = await AvatarControlApi.PostRetinalFrameAsync(
                _sensoryInputHttpClient,
                GetSelectedEndpoint(),
                frame,
                AvatarRuntimeDefaults.UnifiedVisualInputSource,
                timeout.Token);

            if (dispatch.BlockedByInputGate)
            {
                _visionDispatchBackoff.Reset();
                return;
            }

            if (!dispatch.Accepted || dispatch.TargetInstances <= 0)
            {
                RegisterVisionDispatchFailure(
                    $"retinal frame was not accepted (targets={dispatch.TargetInstances})");
                return;
            }

            _visionDispatchBackoff.Reset();
            RegisterOptionalBrainInputSuccess("avatar vision");
            _retinalFramesAccepted++;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Shutdown path.
        }
        catch (OperationCanceledException)
        {
            RegisterVisionDispatchFailure("retinal frame dispatch timed out");
        }
        catch (Exception ex)
        {
            RegisterVisionDispatchFailure($"retinal frame dispatch failed: {ex.GetType().Name}");
        }
        finally
        {
            _visionDispatchInFlight = false;
        }
    }

    private long GetVisionDispatchIntervalMs() => _visionDispatchBackoff.FailureStreak switch
    {
        >= 8 => 1600L,
        >= 5 => 1000L,
        >= 3 => 600L,
        _ => 200L
    };

    private void UpdateAvatarPreviewInfo(VisionComputeResult frame, long lagMs, bool isStale)
    {
        if (_sleepState || _avatarPreviewEyelidClosure > 0.02)
        {
            var state = _sleepState ? "eyes closed" : "eyes opening";
            AvatarPreviewInfoText.Text = $"Preview: {state}, heading {frame.PreviewHeadingDeg:0.0} deg, lag {lagMs} ms";
            return;
        }

        var staleTag = isStale ? ", stale" : string.Empty;
        var sendStatus = _sendAvatarVisionToBrain ? "send on" : "send off";
        AvatarPreviewInfoText.Text = $"Preview: {frame.Width}x{frame.Height}, heading {frame.PreviewHeadingDeg:0.0} deg, lag {lagMs} ms{staleTag} ({sendStatus})";
    }

    private void LogStaleVisionPreviewFrame(long lagMs)
    {
        var nowMs = Environment.TickCount64;
        if ((nowMs - _lastVisionStaleDropLogMs) < 2000)
        {
            return;
        }

        _lastVisionStaleDropLogMs = nowMs;
        Log($"Avatar preview stale frame rendered ({lagMs} ms > {VisionPreviewMaxLagMs} ms); dispatch skipped.");
    }

    private void DropStaleVisionPreviewFrame(VisionComputeResult frame, long lagMs)
    {
        var sendStatus = _sendAvatarVisionToBrain ? "send on" : "send off";
        AvatarPreviewInfoText.Text = $"Preview: {frame.Width}x{frame.Height}, dropped stale frame {lagMs} ms ({sendStatus})";

        var nowMs = Environment.TickCount64;
        if ((nowMs - _lastVisionStaleDropLogMs) < 2000)
        {
            return;
        }

        _lastVisionStaleDropLogMs = nowMs;
        Log($"Avatar preview dropped stale frame ({lagMs} ms > {VisionPreviewDropLagMs} ms); fresh compute requested.");
    }

    private void VisionPreviewWorkerLoop()
    {
        while (_visionWorkerRunning)
        {
            _visionRequestSignal.WaitOne(120);
            if (!_visionWorkerRunning)
            {
                break;
            }

            while (_visionWorkerRunning)
            {
                var envelope = Interlocked.Exchange(ref _pendingVisionComputeRequest, null);
                if (envelope is null)
                {
                    break;
                }

                ProcessVisionComputeRequest(envelope.Request);
            }
        }
    }

    private void ProcessVisionComputeRequest(VisionComputeRequest request)
    {
        var token = _visionComputeCts.Token;
        if (token.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var result = ComputeVisionPreviewFrame(request, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (result.Generation == Volatile.Read(ref _visionGeneration))
            {
                Interlocked.Exchange(ref _pendingVisionComputeResult, result);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown/cancel.
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _visionComputeWarning, $"{ex.GetType().Name}: {TrimForLog(ex.Message, 160)}");
        }
    }

    private VisionComputeRequest? BuildVisionComputeRequest(int width, int height, int stride)
    {
        if (_heights is null)
        {
            return null;
        }

        var avatarForward = GetAvatarVisualForward();
        var forwardX = avatarForward.X;
        var forwardZ = avatarForward.Z;
        var rightX = forwardZ;
        var rightZ = -forwardX;
        var eyeX = _avatarX + (forwardX * AvatarVisionEyeForwardOffset);
        var eyeY = _avatarY + AvatarVisionEyeHeight;
        var eyeZ = _avatarZ + (forwardZ * AvatarVisionEyeForwardOffset);
        var sceneSnapshot = GetVisionSceneSnapshot();
        var dynamicHitBoxes = BuildDynamicVisionHitBoxes();

        return new VisionComputeRequest(
            Generation: Volatile.Read(ref _visionGeneration),
            CaptureTimestampMs: Environment.TickCount64,
            Width: width,
            Height: height,
            Stride: stride,
            EyeX: eyeX,
            EyeY: eyeY,
            EyeZ: eyeZ,
            ForwardX: forwardX,
            ForwardZ: forwardZ,
            RightX: rightX,
            RightZ: rightZ,
            Heights: sceneSnapshot.Heights,
            TerrainCells: sceneSnapshot.TerrainCells,
            VisionHitGrid: sceneSnapshot.HitGrid,
            DynamicVisionHitBoxes: dynamicHitBoxes,
            SurfaceOverrides: sceneSnapshot.SurfaceOverrides);
    }

    private VisionSceneSnapshot GetVisionSceneSnapshot()
    {
        if (_visionSceneSnapshotDirty || _visionHeightsSnapshot is null)
        {
            _visionHeightsSnapshot = (int[,])_heights!.Clone();
            _visionHitBoxesSnapshot = _visionHitBoxes.Count == 0 ? [] : _visionHitBoxes.ToArray();
            _visionHitGridSnapshot = VisionHitGrid.Build(_visionHitBoxesSnapshot);
            _visionSurfaceOverridesSnapshot =
                _surfaceOverrides.Count == 0
                    ? EmptySurfaceOverrides
                    : new Dictionary<long, BlockKind>(_surfaceOverrides);
            _visionTerrainCellsSnapshot = BuildVisionTerrainCells(_visionHeightsSnapshot, _visionSurfaceOverridesSnapshot);
            _visionSceneSnapshotDirty = false;
        }

        return new VisionSceneSnapshot(
            _visionHeightsSnapshot,
            _visionTerrainCellsSnapshot!,
            _visionHitGridSnapshot,
            _visionSurfaceOverridesSnapshot);
    }

    private void InvalidateVisionSceneSnapshot()
    {
        _visionSceneSnapshotDirty = true;
    }

    private static VisionTerrainCell[,] BuildVisionTerrainCells(
        int[,] heights,
        IReadOnlyDictionary<long, BlockKind> surfaceOverrides)
    {
        var width = heights.GetLength(0);
        var depth = heights.GetLength(1);
        var cells = new VisionTerrainCell[width, depth];

        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < depth; z++)
            {
                var kind = ResolveAvatarVisionTerrainKind(heights[x, z], surfaceOverrides, x, z);
                var color = ApplyAvatarVisionTerrainLighting(GetAvatarVisionObjectColor(kind), kind, heights, x, z);
                cells[x, z] = new VisionTerrainCell(kind, color);
            }
        }

        return cells;
    }

    private VisionHitBox[] BuildDynamicVisionHitBoxes()
    {
        var totalObjects = _foodPickups.Count + _weaponPickups.Count + _predators.Count;
        if (totalObjects == 0)
        {
            return [];
        }

        var hitBoxes = new List<VisionHitBox>(totalObjects);
        for (var i = 0; i < _foodPickups.Count; i++)
        {
            var food = _foodPickups[i];
            if (!food.Active)
            {
                continue;
            }

            AddVisionHitBox(
                hitBoxes,
                food.Position.X,
                food.Position.Y,
                food.Position.Z,
                AvatarVisionFoodBoxSize,
                AvatarVisionFoodBoxSize,
                AvatarVisionFoodBoxSize,
                BlockKind.Food);
        }

        for (var i = 0; i < _weaponPickups.Count; i++)
        {
            var weapon = _weaponPickups[i];
            if (!weapon.Active)
            {
                continue;
            }

            var kind = weapon.RangeProfile == AvatarDeviceRangeProfile.Long ? BlockKind.WeaponLong : BlockKind.WeaponShort;
            AddVisionHitBox(
                hitBoxes,
                weapon.Position.X,
                weapon.Position.Y,
                weapon.Position.Z,
                AvatarVisionWeaponBoxSize,
                AvatarVisionWeaponBoxSize,
                AvatarVisionWeaponBoxSize,
                kind);
        }

        for (var i = 0; i < _predators.Count; i++)
        {
            var predator = _predators[i];
            AddVisionHitBox(
                hitBoxes,
                predator.Position.X,
                predator.Position.Y + (AvatarVisionPredatorHeight * 0.35),
                predator.Position.Z,
                AvatarVisionPredatorWidth,
                AvatarVisionPredatorHeight,
                AvatarVisionPredatorLength,
                BlockKind.Predator);
        }

        return hitBoxes.Count == 0 ? [] : hitBoxes.ToArray();
    }

    private static void AddVisionHitBox(
        List<VisionHitBox> hitBoxes,
        double x,
        double y,
        double z,
        double sx,
        double sy,
        double sz,
        BlockKind kind)
    {
        var halfX = sx * 0.5;
        var halfY = sy * 0.5;
        var halfZ = sz * 0.5;
        hitBoxes.Add(new VisionHitBox(
            x - halfX,
            x + halfX,
            y - halfY,
            y + halfY,
            z - halfZ,
            z + halfZ,
            kind));
    }

    private static VisionComputeResult ComputeVisionPreviewFrame(VisionComputeRequest request, CancellationToken cancellationToken)
    {
        const double maxDistance = 22.0;
        const double nearDistance = 0.08;
        const double rayStep = 0.36;
        const int pixelBlock = 4;

        var width = request.Width;
        var height = request.Height;
        var stride = request.Stride;
        var pixels = new byte[stride * height];

        var fovXRadians = DegreesToRadians(AvatarVisionHorizontalFovDeg);
        var tanHalfFovX = Math.Tan(fovXRadians * 0.5);
        var aspect = width / (double)Math.Max(1, height);
        var fovYRadians = 2.0 * Math.Atan(Math.Tan(fovXRadians * 0.5) / Math.Max(0.2, aspect));
        var tanHalfFovY = Math.Tan(fovYRadians * 0.5);
        var previewHeadingDeg = NormalizeDegrees(Math.Atan2(request.ForwardX, request.ForwardZ) * (180.0 / Math.PI));

        var rowCount = Math.Max(1, (height + (pixelBlock - 1)) / pixelBlock);
        Parallel.For(0, rowCount, new ParallelOptions { CancellationToken = cancellationToken }, row =>
        {
            var py = row * pixelBlock;
            var samplePy = Math.Min(height - 1, py + (pixelBlock / 2));
            var rowT = samplePy / (double)Math.Max(1, height - 1);
            var ndcY = 1.0 - (((samplePy + 0.5) / Math.Max(1.0, height)) * 2.0); // +1 top, -1 bottom

            for (var px = 0; px < width; px += pixelBlock)
            {
                var samplePx = Math.Min(width - 1, px + (pixelBlock / 2));
                var ndcX = 1.0 - (((samplePx + 0.5) / Math.Max(1.0, width)) * 2.0); // mirrored left/right
                var lateralScale = ndcX * tanHalfFovX;
                var verticalScale = ndcY * tanHalfFovY;

                var rayDirX = request.ForwardX + (request.RightX * lateralScale);
                var rayDirY = verticalScale;
                var rayDirZ = request.ForwardZ + (request.RightZ * lateralScale);
                var invLen = 1.0 / Math.Max(0.0001, Math.Sqrt((rayDirX * rayDirX) + (rayDirY * rayDirY) + (rayDirZ * rayDirZ)));
                rayDirX *= invLen;
                rayDirY *= invLen;
                rayDirZ *= invLen;

                var hit = false;
                var hitColor = GetAvatarVisionSkyColor(rowT);
                var hitDistance = maxDistance;

                for (var dist = nearDistance; dist <= maxDistance; dist += rayStep)
                {
                    var sampleX = request.EyeX + (rayDirX * dist);
                    var sampleY = request.EyeY + (rayDirY * dist);
                    var sampleZ = request.EyeZ + (rayDirZ * dist);

                    if (!TryWorldToGrid(sampleX, sampleZ, out var gridX, out var gridZ))
                    {
                        break;
                    }

                    if (TryGetVisionHitKind(request.DynamicVisionHitBoxes, sampleX, sampleY, sampleZ, out var visionKind))
                    {
                        hitColor = GetAvatarVisionObjectColor(visionKind);
                        hitDistance = dist;
                        hit = true;
                        break;
                    }

                    if (TryGetVisionHitKind(request.VisionHitGrid, sampleX, sampleY, sampleZ, out visionKind))
                    {
                        hitColor = GetAvatarVisionObjectColor(visionKind);
                        hitDistance = dist;
                        hit = true;
                        break;
                    }

                    var cellHeight = request.Heights[gridX, gridZ];
                    var topY = GetTerrainTopYFromHeight(cellHeight);
                    if (cellHeight < SeaLevel)
                    {
                        topY = SeaLevel * BlockSize;
                    }

                    if (sampleY <= topY)
                    {
                        hitColor = request.TerrainCells[gridX, gridZ].Color;
                        hitDistance = dist;
                        hit = true;
                        break;
                    }
                }

                if (hit)
                {
                    hitColor = ApplyAvatarVisionDistanceFog(hitColor, hitDistance, maxDistance);
                }

                for (var oy = 0; oy < pixelBlock; oy++)
                {
                    var y = py + oy;
                    if (y >= height)
                    {
                        break;
                    }

                    var rowOffset = y * stride;
                    for (var ox = 0; ox < pixelBlock; ox++)
                    {
                        var x = px + ox;
                        if (x >= width)
                        {
                            break;
                        }

                        var idx = rowOffset + (x * 4);
                        pixels[idx + 0] = hitColor.B;
                        pixels[idx + 1] = hitColor.G;
                        pixels[idx + 2] = hitColor.R;
                        pixels[idx + 3] = 255;
                    }
                }
            }
        });

        var sightFrame = new AvatarSightFrame(
            request.Generation,
            request.CaptureTimestampMs,
            width,
            height,
            stride,
            pixels,
            previewHeadingDeg);

        return new VisionComputeResult(sightFrame);
    }

    private Vector3D GetAvatarVisualForward() => GetForwardVector(GetAvatarLookHeadingDeg());

    private Vector3D GetAvatarBodyForward() => GetForwardVector(_avatarHeadingDeg + AvatarVisualYawOffsetDeg);

    private double GetAvatarLookHeadingDeg() => NormalizeDegrees(_avatarHeadingDeg + AvatarVisualYawOffsetDeg + _avatarHeadYawDeg);

    private static Vector3D GetForwardVector(double headingDeg)
    {
        var headingRad = DegreesToRadians(headingDeg);
        var forward = new Vector3D(Math.Sin(headingRad), 0.0, Math.Cos(headingRad));
        if (forward.LengthSquared < 0.000001)
        {
            return new Vector3D(0, 0, 1);
        }

        forward.Normalize();
        return forward;
    }

    private Color GetAvatarVisionTerrainColor(double sampleX, double sampleZ, int cellHeight)
    {
        if (TryGetSurfaceOverride(sampleX, sampleZ, out var overrideKind))
        {
            return GetAvatarVisionObjectColor(overrideKind);
        }

        if (cellHeight < SeaLevel)
        {
            return GetAvatarVisionObjectColor(BlockKind.Water);
        }

        if (cellHeight <= SeaLevel + 1)
        {
            return GetAvatarVisionObjectColor(BlockKind.Sand);
        }

        if (cellHeight >= SeaLevel + 10)
        {
            return GetAvatarVisionObjectColor(BlockKind.Stone);
        }

        if (cellHeight >= SeaLevel + 6)
        {
            return GetAvatarVisionObjectColor(BlockKind.Dirt);
        }

        return GetAvatarVisionObjectColor(BlockKind.Grass);
    }

    private static BlockKind ResolveAvatarVisionTerrainKind(
        int cellHeight,
        IReadOnlyDictionary<long, BlockKind> surfaceOverrides,
        int gridX,
        int gridZ)
    {
        if (surfaceOverrides.TryGetValue(MakeSurfaceKey(gridX, gridZ), out var overrideKind))
        {
            return overrideKind;
        }

        if (cellHeight < SeaLevel)
        {
            return BlockKind.Water;
        }

        if (cellHeight <= SeaLevel + 1)
        {
            return BlockKind.Sand;
        }

        if (cellHeight >= SeaLevel + 10)
        {
            return BlockKind.Stone;
        }

        if (cellHeight >= SeaLevel + 6)
        {
            return BlockKind.Dirt;
        }

        return BlockKind.Grass;
    }

    private static Color ApplyAvatarVisionTerrainLighting(Color baseColor, BlockKind kind, int[,] heights, int gridX, int gridZ)
    {
        if (kind is BlockKind.Water or BlockKind.HabitatGlass)
        {
            return baseColor;
        }

        var maxX = heights.GetLength(0) - 1;
        var maxZ = heights.GetLength(1) - 1;
        var x0 = Math.Max(0, gridX - 1);
        var x1 = Math.Min(maxX, gridX + 1);
        var z0 = Math.Max(0, gridZ - 1);
        var z1 = Math.Min(maxZ, gridZ + 1);
        var center = heights[gridX, gridZ];
        var dx = heights[x1, gridZ] - heights[x0, gridZ];
        var dz = heights[gridX, z1] - heights[gridX, z0];
        var normal = new Vector3D(-dx * 0.42, 2.0, -dz * 0.42);
        normal.Normalize();

        var lightToSurface = new Vector3D(0.85, 1.0, 0.62);
        lightToSurface.Normalize();
        var direct = Math.Max(0.0, Vector3D.DotProduct(normal, lightToSurface));
        var shade = 0.64 + (direct * 0.42);

        var blockerX = Math.Min(maxX, gridX + 1);
        var blockerZ = Math.Min(maxZ, gridZ + 1);
        var blockerHeight = Math.Max(
            heights[blockerX, gridZ],
            Math.Max(heights[gridX, blockerZ], heights[blockerX, blockerZ]));
        if (blockerHeight > center + 1)
        {
            shade *= 0.76;
        }

        var localAverage =
            (heights[x0, gridZ] + heights[x1, gridZ] + heights[gridX, z0] + heights[gridX, z1]) * 0.25;
        if (center < localAverage - 0.75)
        {
            shade *= 0.88;
        }

        return ShadeColor(baseColor, Math.Clamp(shade, 0.48, 1.18));
    }

    private static Color GetAvatarVisionObjectColor(BlockKind kind)
    {
        return kind switch
        {
            BlockKind.Grass => Color.FromRgb(90, 168, 96),
            BlockKind.Dirt => Color.FromRgb(129, 98, 73),
            BlockKind.Stone => Color.FromRgb(116, 130, 142),
            BlockKind.Sand => Color.FromRgb(204, 172, 116),
            BlockKind.Water => Color.FromRgb(92, 160, 228),
            BlockKind.Wood => Color.FromRgb(141, 102, 70),
            BlockKind.Leaves => Color.FromRgb(72, 156, 95),
            BlockKind.HabitatFloor => Color.FromRgb(110, 122, 146),
            BlockKind.HabitatWall => Color.FromRgb(128, 145, 176),
            BlockKind.HabitatGlass => Color.FromRgb(132, 204, 244),
            BlockKind.Food => Color.FromRgb(238, 214, 72),
            BlockKind.WeaponShort => Color.FromRgb(224, 224, 218),
            BlockKind.WeaponLong => Color.FromRgb(126, 188, 255),
            BlockKind.Predator => Color.FromRgb(124, 73, 38),
            _ => Color.FromRgb(128, 145, 176)
        };
    }

    private static Color GetAvatarVisionSkyColor(double rowT)
    {
        var t = Math.Clamp(rowT, 0.0, 1.0);
        var topSky = Color.FromRgb(76, 124, 186);
        var horizonSky = Color.FromRgb(122, 168, 216);
        var lowerSky = Color.FromRgb(20, 36, 64);
        if (t <= 0.50)
        {
            return LerpColor(topSky, horizonSky, t / 0.50);
        }

        return LerpColor(horizonSky, lowerSky, (t - 0.50) / 0.50);
    }

    private static Color ApplyAvatarVisionDistanceFog(Color baseColor, double distance, double maxDistance)
    {
        var fogStart = maxDistance * 0.55;
        var fogT = Math.Clamp((distance - fogStart) / Math.Max(0.001, maxDistance - fogStart), 0.0, 0.72);
        var fogColor = Color.FromRgb(76, 110, 152);
        return LerpColor(baseColor, fogColor, fogT);
    }

    private void AddTrailPoint(Point3D point)
    {
        _trailPoints.Enqueue(point);
        while (_trailPoints.Count > TrailPointCapacity)
        {
            _trailPoints.Dequeue();
        }

        RefreshTrailVisuals();
    }

    private void RefreshTrailVisuals()
    {
        var hiddenY = _habitatBaseY - 999.0;
        var pointIndex = 0;

        foreach (var point in _trailPoints)
        {
            if (pointIndex >= _trailPointTransforms.Count)
            {
                break;
            }

            _trailPointTransforms[pointIndex].OffsetX = point.X;
            _trailPointTransforms[pointIndex].OffsetY = point.Y;
            _trailPointTransforms[pointIndex].OffsetZ = point.Z;
            pointIndex++;
        }

        for (var i = pointIndex; i < _trailPointTransforms.Count; i++)
        {
            _trailPointTransforms[i].OffsetX = 0.0;
            _trailPointTransforms[i].OffsetY = hiddenY;
            _trailPointTransforms[i].OffsetZ = 0.0;
        }
    }

    private void ResetAvatarPose(bool logMessage)
    {
        if (_shelterSites.Count > 0)
        {
            var spawn = _shelterSites[0];
            _avatarX = spawn.X;
            // Prefer spawning outside the shelter opening to avoid immediate trap loops.
            _avatarZ = spawn.Z + Math.Max(3.2, spawn.Radius + 2.8);
        }
        else
        {
            _avatarX = 0.0;
            _avatarZ = 6.0;
        }

        _avatarHeadingDeg = 0.0;
        _avatarHeadYawDeg = 0.0;
        _avatarService.PostResetMotor();
        ApplyNervousSystemSignal(new AvatarNervousSystemSignal(0.0, 0.0, 0.0, 0, 0, 0));
        var spawnRetryBaseline = _spawnValidationRetries;

        if (!TryGetTerrainTopY(_avatarX, _avatarZ, out var terrainY))
        {
            terrainY = _habitatBaseY;
        }

        _avatarY = terrainY + AvatarFootOffset;
        ValidateAndRepairSpawnPose();
        _avatarTranslate.OffsetX = _avatarX;
        _avatarTranslate.OffsetY = _avatarY;
        _avatarTranslate.OffsetZ = _avatarZ;
        _avatarYawRotation.Angle = NormalizeDegrees(_avatarHeadingDeg + AvatarVisualYawOffsetDeg);
        _avatarHeadYawRotation.Angle = _avatarHeadYawDeg;
        _trailPoints.Clear();
        _trailAccumulatorSeconds = 0.0;
        _lastFrontProximity = 0.0;
        _lastLeftProximity = 0.0;
        _lastRightProximity = 0.0;
        RegisterVisitedTerrainCell(_avatarX, _avatarZ);
        RefreshTrailVisuals();

        if (logMessage)
        {
            Log("Avatar reset to world spawn.");
        }

        if (_spawnValidationRetries > spawnRetryBaseline)
        {
            Log($"Spawn validation repaired pose after {_spawnValidationRetries - spawnRetryBaseline} retries.");
        }
    }

    private void ValidateAndRepairSpawnPose()
    {
        if (!Collides(_avatarX, _avatarZ, out var terrainY))
        {
            _avatarY = terrainY + AvatarFootOffset;
            return;
        }

        _spawnValidationRetries++;
        var headingRad = DegreesToRadians(_avatarHeadingDeg);
        ReadOnlySpan<double> offsetsDeg = [0, 18, -18, 36, -36, 60, -60, 92, -92, 128, -128, 180];
        ReadOnlySpan<double> radii = [0.8, 1.2, 1.8, 2.5, 3.5, 4.8, 6.2, 8.0, 10.0];

        for (var r = 0; r < radii.Length; r++)
        {
            for (var i = 0; i < offsetsDeg.Length; i++)
            {
                var probeHeading = headingRad + DegreesToRadians(offsetsDeg[i]);
                var probeX = _avatarX + (Math.Sin(probeHeading) * radii[r]);
                var probeZ = _avatarZ + (Math.Cos(probeHeading) * radii[r]);
                if (Collides(probeX, probeZ, out var probeY))
                {
                    _spawnValidationRetries++;
                    continue;
                }

                _avatarX = probeX;
                _avatarY = probeY + AvatarFootOffset;
                _avatarZ = probeZ;
                _avatarHeadingDeg = NormalizeDegrees(probeHeading * (180.0 / Math.PI));
                return;
            }
        }

        // Final fallback: search the generated terrain for a clear initial spawn.
        if (TryFindClearSpawnCandidate(out var targetX, out var targetY, out var targetZ, out var targetHeadingDeg))
        {
            _avatarX = targetX;
            _avatarY = targetY;
            _avatarZ = targetZ;
            _avatarHeadingDeg = targetHeadingDeg;
        }
    }

    private bool Collides(double x, double z, out double terrainY)
    {
        return IsCollisionAt(x, z, out terrainY, ignoreStepHeight: false);
    }

    private bool IsCollisionAt(double x, double z, out double terrainY, bool ignoreStepHeight)
    {
        terrainY = _avatarY - AvatarFootOffset;

        var worldHalf = ((WorldSize - 1) * 0.5 * BlockSize) - 1.0;
        if (Math.Abs(x) > worldHalf || Math.Abs(z) > worldHalf)
        {
            return true;
        }

        // Corner-aware terrain sampling: probe the 4 corners of the avatar's XZ
        // footprint as well as its centre. Take the MAX terrain top, so a tall
        // neighbour cell blocks even when the avatar's centre is in a low cell.
        // (Old code only sampled the centre and let the avatar clip into walls.)
        if (!TryGetTerrainTopY(x, z, out var tCenter))
        {
            return true;
        }
        terrainY = tCenter;
        SampleCornerTerrain(x, z, ref terrainY, out var allCornersInBounds);
        if (!allCornersInBounds)
        {
            return true;
        }

        if (terrainY <= (SeaLevel * BlockSize) + 0.01)
        {
            return true;
        }

        var currentTerrain = _avatarY - AvatarFootOffset;
        if (!ignoreStepHeight && Math.Abs(terrainY - currentTerrain) > AvatarStepHeight)
        {
            return true;
        }

        var avatarBottom = terrainY + 0.01;
        var avatarTop = avatarBottom + AvatarCollisionHeight;

        // Box scan via spatial grid: only test boxes in the cells the avatar AABB
        // overlaps. Falls back to the old O(N) scan if the grid hasn't been built
        // (e.g. world generation in flight).
        if (_collisionGrid is null)
        {
            for (var i = 0; i < _collisionBoxes.Count; i++)
            {
                if (BoxBlocksAvatar(_collisionBoxes[i], x, z, avatarTop, avatarBottom))
                {
                    return true;
                }
            }

            return false;
        }

        var x0 = ((x - AvatarRadius) - _collisionGridOriginX) / CollisionGridCellSize;
        var x1 = ((x + AvatarRadius) - _collisionGridOriginX) / CollisionGridCellSize;
        var z0 = ((z - AvatarRadius) - _collisionGridOriginZ) / CollisionGridCellSize;
        var z1 = ((z + AvatarRadius) - _collisionGridOriginZ) / CollisionGridCellSize;
        var gx0 = Math.Max(0, (int)Math.Floor(x0));
        var gx1 = Math.Min(_collisionGridDimX - 1, (int)Math.Floor(x1));
        var gz0 = Math.Max(0, (int)Math.Floor(z0));
        var gz1 = Math.Min(_collisionGridDimZ - 1, (int)Math.Floor(z1));

        for (var gz = gz0; gz <= gz1; gz++)
        {
            for (var gx = gx0; gx <= gx1; gx++)
            {
                var bucket = _collisionGrid[(gz * _collisionGridDimX) + gx];
                if (bucket is null)
                {
                    continue;
                }

                for (var k = 0; k < bucket.Count; k++)
                {
                    if (BoxBlocksAvatar(_collisionBoxes[bucket[k]], x, z, avatarTop, avatarBottom))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool BoxBlocksAvatar(CollisionBox box, double x, double z, double avatarTop, double avatarBottom)
    {
        if ((x + AvatarRadius) < box.MinX || (x - AvatarRadius) > box.MaxX)
        {
            return false;
        }

        if ((z + AvatarRadius) < box.MinZ || (z - AvatarRadius) > box.MaxZ)
        {
            return false;
        }

        if (avatarTop < box.MinY || avatarBottom > box.MaxY)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sample terrain at the 4 corners of the avatar XZ footprint and raise
    /// <paramref name="terrainY"/> to the maximum across centre + corners. Sets
    /// <paramref name="allInBounds"/> = false if any corner is outside the heightmap
    /// (which the caller treats as a blocking collision).
    /// </summary>
    private void SampleCornerTerrain(double x, double z, ref double terrainY, out bool allInBounds)
    {
        allInBounds = true;
        // Corner offsets: avatar XZ AABB at radius. We sample slightly inside the
        // radius to avoid false-blocks on perfectly flush walls.
        var r = AvatarRadius * 0.97;
        ReadOnlySpan<(double dx, double dz)> corners =
        [
            (+r, +r), (+r, -r), (-r, +r), (-r, -r)
        ];

        for (var i = 0; i < corners.Length; i++)
        {
            var (dx, dz) = corners[i];
            if (!TryGetTerrainTopY(x + dx, z + dz, out var t))
            {
                allInBounds = false;
                return;
            }

            if (t > terrainY)
            {
                terrainY = t;
            }
        }
    }

    private int CountExplorableTerrainCells(int[,] heights)
    {
        var count = 0;
        for (var x = 0; x < WorldSize; x++)
        {
            for (var z = 0; z < WorldSize; z++)
            {
                if (heights[x, z] > SeaLevel + 1)
                {
                    count++;
                }
            }
        }

        return Math.Max(1, count);
    }

    private bool RegisterVisitedTerrainCell(double worldX, double worldZ)
    {
        if (!TryWorldToGrid(worldX, worldZ, out var gridX, out var gridZ))
        {
            return false;
        }

        return _visitedTerrainCells.Add((gridX * WorldSize) + gridZ);
    }

    private static bool TryWorldToGrid(double worldX, double worldZ, out int gridX, out int gridZ)
    {
        var half = (WorldSize - 1) * 0.5;
        gridX = (int)Math.Round((worldX / BlockSize) + half);
        gridZ = (int)Math.Round((worldZ / BlockSize) + half);
        return gridX >= 0 && gridX < WorldSize && gridZ >= 0 && gridZ < WorldSize;
    }

    private static double GridToWorld(int coordinate)
    {
        var half = (WorldSize - 1) * 0.5;
        return (coordinate - half) * BlockSize;
    }

    private bool TryGetTerrainTopY(double x, double z, out double topY)
    {
        topY = _habitatBaseY;
        if (_heights is null)
        {
            return false;
        }

        if (!TryWorldToGrid(x, z, out var gridX, out var gridZ))
        {
            return false;
        }

        var h = _heights[gridX, gridZ];
        topY = GetTerrainTopYFromHeight(h);
        if (h < SeaLevel)
        {
            topY = SeaLevel * BlockSize;
        }

        return true;
    }

    private static double GetTerrainTopYFromHeight(int height)
    {
        return (height * BlockSize) - (BlockSize * 0.5);
    }

    private int SampleTerrainHeight(double x, double z)
    {
        if (_heights is null)
        {
            return SeaLevel;
        }

        if (!TryWorldToGrid(x, z, out var gridX, out var gridZ))
        {
            return SeaLevel;
        }

        return _heights[gridX, gridZ];
    }

    private bool TryGetSurfaceOverride(double x, double z, out BlockKind block)
    {
        block = BlockKind.Grass;
        if (_heights is null)
        {
            return false;
        }

        if (!TryWorldToGrid(x, z, out var gridX, out var gridZ))
        {
            return false;
        }

        return _surfaceOverrides.TryGetValue(MakeSurfaceKey(gridX, gridZ), out block);
    }

    private bool IsAnyCollisionNear(double x, double y, double z)
    {
        // Point-containment query (no avatar radius). Spatial-grid accelerated.
        if (_collisionGrid is null)
        {
            for (var i = 0; i < _collisionBoxes.Count; i++)
            {
                if (PointInBoxYTolerant(_collisionBoxes[i], x, y, z))
                {
                    return true;
                }
            }

            return false;
        }

        var gx = (int)Math.Floor((x - _collisionGridOriginX) / CollisionGridCellSize);
        var gz = (int)Math.Floor((z - _collisionGridOriginZ) / CollisionGridCellSize);
        if (gx < 0 || gx >= _collisionGridDimX || gz < 0 || gz >= _collisionGridDimZ)
        {
            return false;
        }

        var bucket = _collisionGrid[(gz * _collisionGridDimX) + gx];
        if (bucket is null)
        {
            return false;
        }

        for (var k = 0; k < bucket.Count; k++)
        {
            if (PointInBoxYTolerant(_collisionBoxes[bucket[k]], x, y, z))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PointInBoxYTolerant(CollisionBox box, double x, double y, double z)
    {
        if (x < box.MinX || x > box.MaxX || z < box.MinZ || z > box.MaxZ)
        {
            return false;
        }

        return y >= (box.MinY - 0.1) && y <= (box.MaxY + 0.1);
    }

    private bool TryGetVisionHitKind(double x, double y, double z, out BlockKind kind)
    {
        for (var i = 0; i < _visionHitBoxes.Count; i++)
        {
            var box = _visionHitBoxes[i];
            if (x >= box.MinX && x <= box.MaxX &&
                y >= box.MinY && y <= box.MaxY &&
                z >= box.MinZ && z <= box.MaxZ)
            {
                kind = box.Kind;
                return true;
            }
        }

        kind = BlockKind.Grass;
        return false;
    }

    private static bool TryGetVisionHitKind(VisionHitBox[] hitBoxes, double x, double y, double z, out BlockKind kind)
    {
        for (var i = 0; i < hitBoxes.Length; i++)
        {
            var box = hitBoxes[i];
            if (x >= box.MinX && x <= box.MaxX &&
                y >= box.MinY && y <= box.MaxY &&
                z >= box.MinZ && z <= box.MaxZ)
            {
                kind = box.Kind;
                return true;
            }
        }

        kind = BlockKind.Grass;
        return false;
    }

    private static bool TryGetVisionHitKind(VisionHitGrid grid, double x, double y, double z, out BlockKind kind)
    {
        if (grid.Count == 0)
        {
            kind = BlockKind.Grass;
            return false;
        }

        var bucket = grid.GetBucket(x, z);
        if (bucket is null)
        {
            kind = BlockKind.Grass;
            return false;
        }

        var boxes = grid.Boxes;
        for (var i = 0; i < bucket.Length; i++)
        {
            var box = boxes[bucket[i]];
            if (x >= box.MinX && x <= box.MaxX &&
                y >= box.MinY && y <= box.MaxY &&
                z >= box.MinZ && z <= box.MaxZ)
            {
                kind = box.Kind;
                return true;
            }
        }

        kind = BlockKind.Grass;
        return false;
    }

    private List<AvatarDispatchSpike> ParseDispatchSpikes(JsonElement root, out long maxWallClockMs)
    {
        return AvatarDispatchSpikeParser.ParseDispatchSpikes(root, _dispatchSinceMs, out maxWallClockMs);
    }

    private void ApplyMotorDispatch(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        for (var i = 0; i < dispatches.Count; i++)
        {
            if (AvatarMotorCatalog.IsMotorStructure(dispatches[i].SourceStructure))
            {
                _neuronalMotorDispatchTotal++;
                if (AvatarEffectorCatalog.IsManipulatorEvent(dispatches[i]))
                {
                    _neuronalManipulatorDispatchTotal++;
                }
                else if (AvatarMotorCatalog.IsLocomotorPopulationEvent(dispatches[i]))
                {
                    _neuronalLocomotorDispatchTotal++;
                }
            }
        }

        _avatarService.PostBrainSignals(dispatches);
    }

    private void ApplyNervousSystemSignal(AvatarNervousSystemSignal signal)
    {
        _leftMotorDrive = signal.LeftMotorDrive;
        _rightMotorDrive = signal.RightMotorDrive;
        _manipulatorDrive = signal.ManipulatorDrive;
        _lastMotorDispatchCount = signal.MotorEvents;
        _ticksWithoutMotorDispatch = signal.TicksWithoutMotorDispatch;
    }

    private void SyncMotorDriveFromAvatarService()
    {
        var applied = false;
        while (_avatarService.TryDequeueSignal(out var signal))
        {
            ApplyNervousSystemSignal(signal);
            applied = true;
        }

        if (!applied)
        {
            var signal = _avatarService.LatestSignal;
            _leftMotorDrive = signal.LeftMotorDrive;
            _rightMotorDrive = signal.RightMotorDrive;
            _manipulatorDrive = signal.ManipulatorDrive;
            _lastMotorDispatchCount = signal.MotorEvents;
            _ticksWithoutMotorDispatch = signal.TicksWithoutMotorDispatch;
        }
    }

    private bool ShouldDispatchPhysicalBodyFrame(long nowMs)
    {
        if (_bodyFrameInFlight)
        {
            return false;
        }

        var pressureDecision = EvaluateBrainInputPressure(
            "physical body frame",
            nowMs,
            AvatarInputPriority.Critical,
            BodyFrameDispatchIntervalMs);
        if (pressureDecision.ShouldPause)
        {
            LogOptionalBrainInputPause("physical body frame", pressureDecision.Reason, nowMs);
            return false;
        }

        var bodyDispatchIntervalMs = Math.Max(BodyFrameDispatchIntervalMs, pressureDecision.MinimumIntervalMs);

        return (nowMs - _lastBodyFrameDispatchMs) >= bodyDispatchIntervalMs;
    }

    private async Task DispatchPhysicalBodyFrameAsync(long nowMs, CancellationToken token)
    {
        if (!ShouldDispatchPhysicalBodyFrame(nowMs))
        {
            return;
        }

        _bodyFrameInFlight = true;
        _lastBodyFrameDispatchMs = nowMs;
        try
        {
            var endpoint = GetSelectedEndpoint();
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return;
            }

            var contactPulse = Math.Clamp(_collisionPulse, 0.0, 1.0);
            var probeTotal = _lastFrontProximity + _lastLeftProximity + _lastRightProximity;
            while (_pendingPhysicalContacts.Count > 0)
            {
                var pending = _pendingPhysicalContacts.Dequeue();
                var pendingResult = await AvatarControlApi.PostSomaticContactFrameAsync(
                    _sensoryInputHttpClient,
                    endpoint,
                    new SomaticContactFrameRequest(
                        Sequence: Interlocked.Increment(ref _somaticContactFrameSequence),
                        TimestampMs: nowMs,
                        BodyPositionX: pending.BodyPositionX,
                        BodyPositionY: pending.BodyPositionY,
                        BodyPositionZ: pending.BodyPositionZ,
                        SurfaceNormalX: pending.SurfaceNormalX,
                        SurfaceNormalY: pending.SurfaceNormalY,
                        SurfaceNormalZ: pending.SurfaceNormalZ,
                        ForceNewtons: pending.ForceNewtons,
                        ImpulseNewtonSeconds: pending.ImpulseNewtonSeconds,
                        PenetrationMillimeters: pending.PenetrationMillimeters,
                        TangentialSpeedMetersPerSecond: pending.TangentialSpeedMetersPerSecond,
                        ContactAreaSquareMillimeters: pending.ContactAreaSquareMillimeters,
                        DurationMilliseconds: pending.DurationMilliseconds,
                        InputSource: pending.InputSource),
                    token);
                if (pendingResult.Accepted && pendingResult.TargetInstances > 0)
                {
                    _somaticFramesAccepted++;
                }
            }

            if (contactPulse > 0.01)
            {
                var localX = probeTotal > 0.001
                    ? (_lastRightProximity - _lastLeftProximity) / probeTotal
                    : 0.0;
                var localZ = probeTotal > 0.001 ? _lastFrontProximity / probeTotal : 1.0;
                var directionLength = Math.Max(0.001, Math.Sqrt((localX * localX) + (localZ * localZ)));
                localX /= directionLength;
                localZ /= directionLength;
                var collisionResult = await AvatarControlApi.PostSomaticContactFrameAsync(
                    _sensoryInputHttpClient,
                    endpoint,
                    new SomaticContactFrameRequest(
                        Sequence: Interlocked.Increment(ref _somaticContactFrameSequence),
                        TimestampMs: nowMs,
                        BodyPositionX: (float)(localX * 0.45),
                        BodyPositionY: 0f,
                        BodyPositionZ: (float)(localZ * 0.45),
                        SurfaceNormalX: (float)-localX,
                        SurfaceNormalY: 0f,
                        SurfaceNormalZ: (float)-localZ,
                        ForceNewtons: (float)(1_200.0 + (contactPulse * 3_200.0)),
                        ImpulseNewtonSeconds: (float)(25.0 + (contactPulse * 120.0)),
                        PenetrationMillimeters: (float)(contactPulse * 28.0),
                        TangentialSpeedMetersPerSecond: (float)Math.Abs(_lastForwardSpeed),
                        ContactAreaSquareMillimeters: 1_100f,
                        DurationMilliseconds: BodyFrameDispatchIntervalMs,
                        InputSource: "avatar_world_contact"),
                    token);
                if (collisionResult.Accepted && collisionResult.TargetInstances > 0)
                {
                    _somaticFramesAccepted++;
                }
            }

            var groundResult = await AvatarControlApi.PostSomaticContactFrameAsync(
                _sensoryInputHttpClient,
                endpoint,
                new SomaticContactFrameRequest(
                    Sequence: Interlocked.Increment(ref _somaticContactFrameSequence),
                    TimestampMs: nowMs,
                    BodyPositionX: 0f,
                    BodyPositionY: -0.9f,
                    BodyPositionZ: 0f,
                    SurfaceNormalX: 0f,
                    SurfaceNormalY: 1f,
                    SurfaceNormalZ: 0f,
                    ForceNewtons: 686.7f,
                    ImpulseNewtonSeconds: 0f,
                    PenetrationMillimeters: 1.2f,
                    TangentialSpeedMetersPerSecond: (float)Math.Abs(_lastForwardSpeed),
                    ContactAreaSquareMillimeters: 20_000f,
                    DurationMilliseconds: BodyFrameDispatchIntervalMs,
                    InputSource: "avatar_world_ground"),
                token);
            if (groundResult.Accepted && groundResult.TargetInstances > 0)
            {
                _somaticFramesAccepted++;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(BodyFrameDispatchTimeoutMs));
            var bodyResult = await AvatarControlApi.PostPhysicalBodyFrameAsync(
                _sensoryInputHttpClient,
                endpoint,
                new PhysicalBodyFrameRequest(
                    Sequence: Interlocked.Increment(ref _physicalBodyFrameSequence),
                    TimestampMs: nowMs,
                    LinearVelocityXMetersPerSecond: 0f,
                    LinearVelocityYMetersPerSecond: 0f,
                    LinearVelocityZMetersPerSecond: (float)_lastForwardSpeed,
                    AngularVelocityXRadiansPerSecond: 0f,
                    AngularVelocityYRadiansPerSecond: (float)(_lastTurnRateDeg * Math.PI / 180.0),
                    AngularVelocityZRadiansPerSecond: 0f,
                    StoredEnergyJoules: (float)_storedEnergyJoules,
                    TissueIntegrityFraction: (float)Math.Clamp(_tissueIntegrity, 0.0, 1.0),
                    CoreTemperatureCelsius: 37f,
                    BloodOxygenSaturationFraction: 0.98f,
                    HydrationFraction: (float)_hydrationFraction,
                    InputSource: AvatarRuntimeDefaults.UnifiedBodyInputSource),
                timeout.Token);
            if (bodyResult.Accepted && bodyResult.TargetInstances > 0)
            {
                _physicalBodyFramesAccepted++;
            }
            RegisterOptionalBrainInputSuccess("physical body frame");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Shutdown path.
        }
        catch (Exception ex)
        {
            RegisterOptionalBrainInputFailure("physical body frame", $"{ex.GetType().Name}: {TrimForLog(ex.Message, 140)}", Environment.TickCount64);
        }
        finally
        {
            _bodyFrameInFlight = false;
        }
    }

    private bool ShouldDispatchEnvironmentAudioInput(long nowMs)
    {
        if (_environmentAudioInFlight)
        {
            return false;
        }

        var pressureDecision = EvaluateBrainInputPressure(
            "environment audio",
            nowMs,
            AvatarInputPriority.Optional,
            EnvironmentAudioDispatchIntervalMs);
        if (pressureDecision.ShouldPause)
        {
            LogOptionalBrainInputPause("environment audio", pressureDecision.Reason, nowMs);
            return false;
        }

        var dispatchIntervalMs = Math.Max(EnvironmentAudioDispatchIntervalMs, pressureDecision.MinimumIntervalMs);
        return (nowMs - _lastEnvironmentAudioDispatchMs) >= dispatchIntervalMs &&
               !_environmentAudioBackoff.IsBlocked(nowMs);
    }

    private async Task DispatchEnvironmentAudioInputAsync(long nowMs, CancellationToken token)
    {
        if (!ShouldDispatchEnvironmentAudioInput(nowMs))
        {
            return;
        }

        var endpoint = GetSelectedEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        var acousticSources = BuildEnvironmentAcousticSources();
        if (acousticSources.Count > 0)
        {
            var frame = AvatarAcousticRenderer.RenderFrame(
                acousticSources,
                Interlocked.Increment(ref _environmentAudioFrameSequence),
                nowMs);
            _avatarService.PostAudioInputFrame(frame);
        }

        var audioFrame = await DrainAvatarAudioInputFrameAsync(token);
        if (audioFrame is null)
        {
            return;
        }

        _environmentAudioInFlight = true;
        _lastEnvironmentAudioDispatchMs = nowMs;
        try
        {
            if (await TryPostEnvironmentAudioFrameAsync(endpoint, audioFrame, token))
            {
                _environmentAudioBackoff.Reset();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Shutdown path.
        }
        catch (Exception ex)
        {
            RegisterEnvironmentAudioFailure($"{ex.GetType().Name}: {TrimForLog(ex.Message, 160)}");
        }
        finally
        {
            _environmentAudioInFlight = false;
        }
    }

    private List<AvatarAcousticSource> BuildEnvironmentAcousticSources()
    {
        var sources = new List<AvatarAcousticSource>(7);
        var movement = Math.Clamp(Math.Abs(_lastForwardSpeed) / WorldMaxForwardSpeed, 0.0, 1.0);
        if (_collisionPulse > 0.12)
        {
            sources.Add(new AvatarAcousticSource(
                FrequencyHz: 92.0,
                Amplitude: Math.Clamp(0.08 + (_collisionPulse * 0.38), 0.08, 0.58),
                NoiseMix: 0.28,
                HarmonicMix: 0.42,
                PulseRateHz: 7.0,
                PulseDutyCycle: 0.18));
        }

        if (movement > 0.05 && TryClassifyCurrentTerrain(out var surfaceKind))
        {
            var acoustic = surfaceKind switch
            {
                BlockKind.Water => (Frequency: 170.0, Noise: 0.72, Harmonic: 0.08),
                BlockKind.Stone => (Frequency: 1450.0, Noise: 0.18, Harmonic: 0.34),
                BlockKind.Dirt => (Frequency: 360.0, Noise: 0.62, Harmonic: 0.12),
                BlockKind.Sand => (Frequency: 760.0, Noise: 0.82, Harmonic: 0.05),
                _ => (Frequency: 520.0, Noise: 0.68, Harmonic: 0.10)
            };
            sources.Add(new AvatarAcousticSource(
                FrequencyHz: acoustic.Frequency,
                Amplitude: Math.Clamp(0.025 + (movement * 0.12), 0.025, 0.16),
                NoiseMix: acoustic.Noise,
                HarmonicMix: acoustic.Harmonic,
                PulseRateHz: Math.Clamp(1.6 + (movement * 2.2), 1.6, 3.8),
                PulseDutyCycle: 0.22));
        }

        var wind = 0.12 + (_darkness01 * 0.12);
        if (wind > 0.08)
        {
            sources.Add(new AvatarAcousticSource(
                FrequencyHz: 240.0,
                Amplitude: Math.Clamp(0.012 + (wind * 0.055), 0.012, 0.05),
                NoiseMix: 0.96,
                HarmonicMix: 0.02,
                PulseRateHz: 0.35,
                PulseDutyCycle: 0.72));
        }

        var nearestPredator = FindNearest(_predators);
        if (nearestPredator.Distance < (_predatorSenseRadius * 1.55))
        {
            var proximity = Math.Clamp(1.0 - (nearestPredator.Distance / Math.Max(0.5, _predatorSenseRadius * 1.55)), 0.0, 1.0);
            var pan = ResolveSoundPan(nearestPredator.Position);
            sources.Add(new AvatarAcousticSource(
                FrequencyHz: 430.0,
                Amplitude: Math.Clamp(0.018 + (proximity * 0.15), 0.018, 0.17),
                Pan: pan,
                NoiseMix: 0.88,
                HarmonicMix: 0.08,
                PulseRateHz: 2.4,
                PulseDutyCycle: 0.36));

            if (nearestPredator.Distance <= _predatorSenseRadius)
            {
                sources.Add(new AvatarAcousticSource(
                    FrequencyHz: 78.0 + (proximity * 28.0),
                    Amplitude: Math.Clamp(0.09 + (proximity * 0.32), 0.09, 0.42),
                    Pan: pan,
                    NoiseMix: 0.16,
                    HarmonicMix: 0.46,
                    PulseRateHz: 3.2,
                    PulseDutyCycle: 0.58));
            }
        }

        var waterProximity = EstimateWaterAuditoryProximity(6.0);
        if (waterProximity > 0.04)
        {
            sources.Add(new AvatarAcousticSource(
                FrequencyHz: 185.0,
                Amplitude: Math.Clamp(0.014 + (waterProximity * 0.09), 0.014, 0.105),
                NoiseMix: 0.73,
                HarmonicMix: 0.12,
                PulseRateHz: 1.15,
                PulseDutyCycle: 0.54));
        }

        return sources;
    }

    private bool TryClassifyCurrentTerrain(out BlockKind kind)
    {
        kind = BlockKind.Grass;
        if (_heights is null || !TryWorldToGrid(_avatarX, _avatarZ, out var gx, out var gz))
        {
            return false;
        }

        if (TryGetSurfaceOverride(_avatarX, _avatarZ, out var overrideKind))
        {
            kind = overrideKind;
            return true;
        }

        kind = ResolveTerrainKindFromHeight(_heights[gx, gz]);
        return true;
    }

    private double EstimateWaterAuditoryProximity(double maxDistance)
    {
        if (_heights is null || !TryWorldToGrid(_avatarX, _avatarZ, out var centerX, out var centerZ))
        {
            return 0.0;
        }

        var radius = Math.Max(1, (int)Math.Ceiling(maxDistance / BlockSize));
        var bestDistanceSq = double.MaxValue;
        for (var dz = -radius; dz <= radius; dz++)
        {
            var gz = centerZ + dz;
            if (gz < 0 || gz >= WorldSize)
            {
                continue;
            }

            for (var dx = -radius; dx <= radius; dx++)
            {
                var gx = centerX + dx;
                if (gx < 0 || gx >= WorldSize)
                {
                    continue;
                }

                var worldX = GridToWorld(gx);
                var worldZ = GridToWorld(gz);
                var isWater = _surfaceOverrides.TryGetValue(MakeSurfaceKey(gx, gz), out var surface)
                    ? surface == BlockKind.Water
                    : ResolveTerrainKindFromHeight(_heights[gx, gz]) == BlockKind.Water;
                if (!isWater)
                {
                    continue;
                }

                var distanceSq = DistanceSquared(_avatarX, _avatarZ, worldX, worldZ);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                }
            }
        }

        if (bestDistanceSq == double.MaxValue)
        {
            return 0.0;
        }

        var distance = Math.Sqrt(bestDistanceSq);
        return Math.Clamp(1.0 - (distance / Math.Max(0.1, maxDistance)), 0.0, 1.0);
    }

    private void RegisterEnvironmentAudioFailure(string message)
    {
        var now = Environment.TickCount64;
        var backoffMs = _environmentAudioBackoff.RegisterFailure(now);
        RegisterOptionalBrainInputFailure("environment audio", message, now);
        var warning = $"{message} (streak {_environmentAudioBackoff.FailureStreak}, backoff {backoffMs}ms)";
        if (_environmentAudioWarningGate.ShouldLog(warning, CreateDispatchWarningKey("environment-audio", message), now))
        {
            Log($"Environment audio dispatch warning: {warning}");
        }
    }

    private async Task<AvatarAudioFrame?> DrainAvatarAudioInputFrameAsync(CancellationToken token)
    {
        if (_avatarService.TryDequeueAudioInput(out var frame))
        {
            return frame;
        }

        await Task.Delay(1, token);
        return _avatarService.TryDequeueAudioInput(out frame) ? frame : null;
    }

    private void UpdateTerrainCellVisual(int gridX, int gridZ)
    {
        if (_heights is null)
        {
            return;
        }

        var half = (WorldSize - 1) * 0.5;
        var worldX = (gridX - half) * BlockSize;
        var worldZ = (gridZ - half) * BlockSize;
        var height = _heights[gridX, gridZ];
        var terrainCenterY = (height * BlockSize * 0.5) - (BlockSize * 0.5);
        var terrainMaterial = PickTerrainMaterial(height, gridX, gridZ);

        var terrainModel = _terrainColumnModels?[gridX, gridZ];
        if (terrainModel is not null)
        {
            terrainModel.Material = terrainMaterial;
            terrainModel.BackMaterial = terrainMaterial;
            UpdateBlockModelTransform(terrainModel, worldX, terrainCenterY, worldZ, BlockSize, height * BlockSize, BlockSize);
        }

        var waterModel = _waterColumnModels?[gridX, gridZ];
        if (height < SeaLevel)
        {
            var waterHeight = (SeaLevel - height) + 0.35;
            var waterCenterY = (SeaLevel * BlockSize) - (waterHeight * 0.5);
            if (waterModel is null)
            {
                var worldGroup = GetWorldGroup();
                if (worldGroup is not null)
                {
                    waterModel = CreateBlockModel(_materials[BlockKind.Water], worldX, waterCenterY, worldZ, BlockSize, waterHeight, BlockSize);
                    worldGroup.Children.Add(waterModel);
                    if (_waterColumnModels is not null)
                    {
                        _waterColumnModels[gridX, gridZ] = waterModel;
                    }
                }
            }
            else
            {
                waterModel.Material = _materials[BlockKind.Water];
                waterModel.BackMaterial = _materials[BlockKind.Water];
                UpdateBlockModelTransform(waterModel, worldX, waterCenterY, worldZ, BlockSize, waterHeight, BlockSize);
            }
        }
        else if (waterModel is not null)
        {
            waterModel.Material = _transparentMaterial;
            waterModel.BackMaterial = _transparentMaterial;
            UpdateBlockModelTransform(waterModel, worldX, -999.0, worldZ, BlockSize, 0.01, BlockSize);
        }
    }

    private Model3DGroup? GetWorldGroup()
    {
        if (_sceneRoot.Children.Count == 0)
        {
            return null;
        }

        return _sceneRoot.Children[0] as Model3DGroup;
    }

    private void ResetCamera()
    {
        if (_followAvatarCamera)
        {
            _cameraTarget = new Point3D(_avatarX, _avatarY + 1.30, _avatarZ);
            _cameraYawDeg = NormalizeDegrees(_avatarHeadingDeg + FollowCameraYawOffsetDeg);
            _cameraPitchDeg = FollowCameraPitchDeg;
            _cameraDistance = Math.Max(FollowCameraDistance, WorldSize * 0.72);
        }
        else
        {
            _cameraTarget = new Point3D(0, Math.Max(4.0, _habitatBaseY + 1.2), 0);
            _cameraYawDeg = -32;
            _cameraPitchDeg = -27;
            _cameraDistance = Math.Max(95, WorldSize * 2.0);
        }

        _camera = new PerspectiveCamera
        {
            FieldOfView = 56,
            UpDirection = new Vector3D(0, 1, 0)
        };
        WorldViewport.Camera = _camera;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (_camera is null)
        {
            return;
        }

        if (_followAvatarCamera)
        {
            var avatarForward = GetAvatarBodyForward();
            var forwardX = avatarForward.X;
            var forwardZ = avatarForward.Z;

            var eye = new Point3D(
                _avatarX - (forwardX * FollowCameraBehindBlocks),
                _avatarY + FollowCameraAboveBlocks,
                _avatarZ - (forwardZ * FollowCameraBehindBlocks));

            var lookTarget = new Point3D(
                _avatarX + (forwardX * FollowCameraLookAheadBlocks),
                _avatarY + FollowCameraLookTargetHeightBlocks,
                _avatarZ + (forwardZ * FollowCameraLookAheadBlocks));

            var lookVec = lookTarget - eye;
            var length = Math.Sqrt((lookVec.X * lookVec.X) + (lookVec.Y * lookVec.Y) + (lookVec.Z * lookVec.Z));
            if (length < 0.0001)
            {
                lookVec = new Vector3D(0, -0.05, 1.0);
                length = 1.0;
            }

            _camera.Position = eye;
            _camera.LookDirection = lookVec * (1.0 / length) * 12.0;
            return;
        }

        var yaw = DegreesToRadians(_cameraYawDeg);
        var pitch = DegreesToRadians(_cameraPitchDeg);

        var cp = Math.Cos(pitch);
        var offset = new Vector3D(
            _cameraDistance * cp * Math.Cos(yaw),
            _cameraDistance * Math.Sin(pitch),
            _cameraDistance * cp * Math.Sin(yaw));

        _camera.Position = new Point3D(
            _cameraTarget.X + offset.X,
            _cameraTarget.Y + offset.Y,
            _cameraTarget.Z + offset.Z);

        var look = _cameraTarget - _camera.Position;
        look.Normalize();
        _camera.LookDirection = look * _cameraDistance;
    }

    private void WorldViewport_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (_mapEditorEnabled)
        {
            var point = e.GetPosition(WorldViewport);
            if (TryProjectScreenToWorld(point, out var worldX, out var worldZ))
            {
                var brush = GetSelectedBrush();
                var radius = (int)Math.Round(BrushRadiusSlider.Value);
                var strength = (int)Math.Round(BrushStrengthSlider.Value);
                if (ApplyTerrainBrush(worldX, worldZ))
                {
                    Log($"Terrain edited at ({worldX:0.0}, {worldZ:0.0}) with {brush} (r={radius}, s={strength}).");
                }
            }

            return;
        }

        _dragActive = true;
        _dragStart = e.GetPosition(WorldViewport);
        WorldViewport.CaptureMouse();
    }

    private void WorldViewport_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragActive)
        {
            return;
        }

        var current = e.GetPosition(WorldViewport);
        var delta = current - _dragStart;
        _dragStart = current;

        _cameraYawDeg += delta.X * 0.38;
        _cameraPitchDeg = Math.Clamp(_cameraPitchDeg - (delta.Y * 0.28), -82, 62);
    }

    private void WorldViewport_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragActive || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _dragActive = false;
        if (WorldViewport.IsMouseCaptured)
        {
            WorldViewport.ReleaseMouseCapture();
        }
    }

    private void WorldViewport_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _cameraDistance = Math.Clamp(_cameraDistance - (e.Delta * 0.05), 20, 280);
    }

    private void EnableMapEditorCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        _mapEditorEnabled = true;
        MapEditorHintText.Text = "Editor on. Left-click terrain to paint using selected brush.";
        Log("Map editor enabled.");
    }

    private void EnableMapEditorCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _mapEditorEnabled = false;
        MapEditorHintText.Text = "Editor off. Enable map editor and left-click terrain to paint.";
        Log("Map editor disabled.");
    }

    private void ClearOverridesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var changedCells = _surfaceOverrides.Keys.ToArray();
        _surfaceOverrides.Clear();
        _overrideCells = 0;
        InvalidateVisionSceneSnapshot();
        foreach (var key in changedCells)
        {
            UpdateTerrainCellVisual((int)(key >> 32), (int)key);
        }

        Log("Surface paint overrides cleared.");
    }

    private bool TryProjectScreenToWorld(Point screenPoint, out double worldX, out double worldZ)
    {
        worldX = 0.0;
        worldZ = 0.0;
        if (_camera is null || WorldViewport.ActualWidth < 2 || WorldViewport.ActualHeight < 2)
        {
            return false;
        }

        var width = WorldViewport.ActualWidth;
        var height = WorldViewport.ActualHeight;
        var nx = ((screenPoint.X / width) * 2.0) - 1.0;
        var ny = 1.0 - ((screenPoint.Y / height) * 2.0);

        var forward = _camera.LookDirection;
        forward.Normalize();
        var right = Vector3D.CrossProduct(forward, _camera.UpDirection);
        if (right.LengthSquared < 1e-8)
        {
            return false;
        }

        right.Normalize();
        var up = Vector3D.CrossProduct(right, forward);
        up.Normalize();

        var fov = DegreesToRadians(_camera.FieldOfView);
        var tan = Math.Tan(fov * 0.5);
        var aspect = width / height;
        var rayDir = forward + (right * (nx * tan * aspect)) + (up * (ny * tan));
        rayDir.Normalize();
        if (Math.Abs(rayDir.Y) < 1e-6)
        {
            return false;
        }

        var planeY = _habitatBaseY + 0.2;
        var t = (planeY - _camera.Position.Y) / rayDir.Y;
        if (t <= 0.0)
        {
            return false;
        }

        worldX = _camera.Position.X + (rayDir.X * t);
        worldZ = _camera.Position.Z + (rayDir.Z * t);
        return true;
    }

    private string GetSelectedBrush()
    {
        if (MapBrushComboBox.SelectedItem is ComboBoxItem selected && selected.Content is string label)
        {
            return label;
        }

        return "Raise";
    }

    private bool ApplyTerrainBrush(double worldX, double worldZ)
    {
        if (_heights is null)
        {
            return false;
        }

        var half = (WorldSize - 1) * 0.5;
        var centerX = (int)Math.Round((worldX / BlockSize) + half);
        var centerZ = (int)Math.Round((worldZ / BlockSize) + half);
        if (centerX < 1 || centerX >= (WorldSize - 1) || centerZ < 1 || centerZ >= (WorldSize - 1))
        {
            return false;
        }

        var radius = (int)Math.Round(BrushRadiusSlider.Value);
        var strength = (int)Math.Round(BrushStrengthSlider.Value);
        var brush = GetSelectedBrush();
        var changed = false;

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dz = -radius; dz <= radius; dz++)
            {
                if ((dx * dx) + (dz * dz) > (radius * radius))
                {
                    continue;
                }

                var gx = centerX + dx;
                var gz = centerZ + dz;
                if (gx < 1 || gx >= (WorldSize - 1) || gz < 1 || gz >= (WorldSize - 1))
                {
                    continue;
                }

                ref var cell = ref _heights[gx, gz];
                var original = cell;
                var key = MakeSurfaceKey(gx, gz);
                var hadOverride = _surfaceOverrides.TryGetValue(key, out var previousOverride);

                if (!IsTerrainCellEditable(gx, gz))
                {
                    continue;
                }

                switch (brush)
                {
                    case "Raise":
                        cell = Math.Clamp(cell + strength, MinTerrainHeight, MountainPeakHeight);
                        if (cell != original)
                        {
                            _surfaceOverrides.Remove(key);
                        }

                        break;
                    case "Lower":
                        cell = Math.Clamp(cell - strength, MinTerrainHeight, MountainPeakHeight);
                        if (cell != original)
                        {
                            _surfaceOverrides.Remove(key);
                        }

                        break;
                    case "Flatten":
                        cell = Math.Clamp((int)Math.Round(_habitatBaseY + 0.5), MinTerrainHeight, MountainPeakHeight);
                        _surfaceOverrides.Remove(key);
                        break;
                    case "Water":
                        cell = Math.Max(MinTerrainHeight, SeaLevel - 1);
                        // Water is defined by elevation. Leaving no surface override
                        // preserves the sandy bed while every subsystem classifies
                        // the cell as water from its height.
                        _surfaceOverrides.Remove(key);
                        break;
                    case "Rock":
                        _surfaceOverrides[key] = BlockKind.Stone;
                        break;
                    case "Grass":
                        _surfaceOverrides[key] = BlockKind.Grass;
                        break;
                }

                var hasOverride = _surfaceOverrides.TryGetValue(key, out var currentOverride);
                var cellChanged = (cell != original) ||
                    (hadOverride != hasOverride) ||
                    (hadOverride && hasOverride && previousOverride != currentOverride);
                if (!cellChanged)
                {
                    continue;
                }

                changed = true;
                UpdateTerrainCellVisual(gx, gz);
            }
        }

        _overrideCells = _surfaceOverrides.Count;
        if (changed)
        {
            InvalidateVisionSceneSnapshot();
        }

        return changed;
    }

    private bool IsTerrainCellEditable(int gridX, int gridZ)
    {
        var worldX = GridToWorld(gridX);
        var worldZ = GridToWorld(gridZ);
        var halfCell = BlockSize * 0.5;
        foreach (var box in _collisionBoxes)
        {
            if (worldX + halfCell >= box.MinX && worldX - halfCell <= box.MaxX &&
                worldZ + halfCell >= box.MinZ && worldZ - halfCell <= box.MaxZ)
            {
                return false;
            }
        }

        var dynamicClearanceSq = 0.80 * 0.80;
        if (DistanceSquared(_avatarX, _avatarZ, worldX, worldZ) < dynamicClearanceSq ||
            _foodPickups.Any(p => p.Active && DistanceSquared(p.Position.X, p.Position.Z, worldX, worldZ) < dynamicClearanceSq) ||
            _weaponPickups.Any(p => p.Active && DistanceSquared(p.Position.X, p.Position.Z, worldX, worldZ) < dynamicClearanceSq) ||
            _predators.Any(p => DistanceSquared(p.Position.X, p.Position.Z, worldX, worldZ) < dynamicClearanceSq))
        {
            return false;
        }

        return true;
    }

    private static string TrimForLog(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (cleaned.Length <= maxLength)
        {
            return cleaned;
        }

        return $"{cleaned[..maxLength]}...";
    }

    private async Task PollFrameAsync(bool forceLogOnFailure = false)
    {
        if (_frameInFlight)
        {
            return;
        }

        _frameInFlight = true;
        try
        {
            var endpoint = GetSelectedEndpoint();
            var frame = await AvatarControlApi.GetJsonAsync(
                _httpClient,
                endpoint,
                AvatarControlApi.GetFramePath(_dispatchSinceMs, includeConnectome: false));
            using var doc = frame.Document;
            if (!frame.IsSuccessStatusCode || doc is null)
            {
                if (forceLogOnFailure)
                {
                    Log($"Frame stream unavailable: HTTP {(int)frame.StatusCode}");
                }

                return;
            }
            var root = doc.RootElement;
            var brainState = default(JsonElement);

            if (TryGetProperty(root, "state", out var stateElement) && stateElement.ValueKind == JsonValueKind.Object)
            {
                brainState = stateElement;
                _sleepState = IsSleepingState(stateElement);
                UpdateBrainMotorDecisionFromState(stateElement);
            }

            var dispatches = ParseDispatchSpikes(root, out var maxWallClockMs);
            if (maxWallClockMs > _dispatchSinceMs)
            {
                _dispatchSinceMs = maxWallClockMs;
            }

            dispatches = AvatarNeuronalMotorBridge.Compose(
                brainState,
                dispatches,
                _lastNeuronalMotorTick,
                out _lastNeuronalMotorTick,
                out var neuronalMotor);
            UpdateMotorPathwayAuditFromFrame(root, dispatches);
            ApplyMotorDispatch(dispatches);
        }
        catch (Exception ex)
        {
            if (forceLogOnFailure)
            {
                Log($"Frame connection issue ({ex.GetType().Name}).");
            }
        }
        finally
        {
            _frameInFlight = false;
        }
    }

    private async Task PollTelemetryAsync(bool forceLogOnFailure = false)
    {
        if (_telemetryInFlight)
        {
            return;
        }

        _telemetryInFlight = true;
        try
        {
            var endpoint = GetSelectedEndpoint();
            var telemetry = await AvatarControlApi.GetJsonAsync(_telemetryHttpClient, endpoint, "/api/v1/transport/stats");
            using var telemetryDoc = telemetry.Document;
            if (!telemetry.IsSuccessStatusCode || telemetryDoc is null)
            {
                if (telemetry.StatusCode == HttpStatusCode.NotFound &&
                    await TryPollTelemetryFromStateAsync(endpoint))
                {
                    return;
                }

                HandleTelemetryFailure($"HTTP {(int)telemetry.StatusCode}", forceLogOnFailure);
                return;
            }

            ApplyTelemetryPayload(telemetryDoc.RootElement);
        }
        catch (Exception ex)
        {
            HandleTelemetryFailure(ex.GetType().Name, forceLogOnFailure);
        }
        finally
        {
            _telemetryInFlight = false;
        }
    }

    private async Task<bool> TryPollTelemetryFromStateAsync(string endpoint)
    {
        try
        {
            var telemetry = await AvatarControlApi.GetJsonAsync(_telemetryHttpClient, endpoint, "/api/v1/state");
            using var telemetryDoc = telemetry.Document;
            if (!telemetry.IsSuccessStatusCode || telemetryDoc is null)
            {
                return false;
            }

            ApplyTelemetryPayload(telemetryDoc.RootElement);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyTelemetryPayload(JsonElement root)
    {
        var tick = GetLong(root, "tick", "Tick");
        var simMs = GetDouble(root, "simulationMs", "SimulationMs", "simulationClockMs", "SimulationClockMs");

        var transport = TryGetObject(root, "transport");
        if (!transport.TryGetValue(out var transportElement))
        {
            transport = TryGetObject(root, "transportStats");
        }

        var services = TryGetObject(root, "services");
        var activePathways = transport.TryGetValue(out var t) ? GetLong(t, "activePathways", "ActivePathways") : 0;
        var dispatched = transport.TryGetValue(out var t2) ? GetLong(t2, "dispatchedSpikes", "DispatchedSpikes") : 0;
        var serviceTotal = services.TryGetValue(out var s)
            ? GetLong(s, "total", "Total")
            : GetLong(root, "serviceCount", "ServiceCount");
        var serviceNonOk = services.TryGetValue(out var s2)
            ? GetLong(s2, "nonOk", "NonOk")
            : CountNonOkServicesFromTelemetry(root);
        _engineServiceNonOkCount = serviceNonOk;
        var transportPressure = transport.TryGetValue(out var pressureTransport)
            ? EstimateEngineInputPressure(pressureTransport)
            : 0.0;
        var ingressPressure = TryGetObject(root, "inputIngress").TryGetValue(out var ingress)
            ? EstimateInputIngressPressure(ingress)
            : 0.0;
        _engineInputPressure = Math.Max(transportPressure, ingressPressure);

        TickText.Text = $"Tick: {tick}";
        SimulationText.Text = $"Simulation ms: {simMs:0.0}";
        DispatchText.Text = $"Dispatched spikes: {dispatched}";
        PathwaysText.Text = $"Active pathways: {activePathways}";
        ServicesText.Text = $"Service health: {serviceTotal} total, {serviceNonOk} non-OK";

        var activity = Math.Clamp((dispatched / 260.0) + (activePathways / 18.0), 0.0, 1.0);
        UpdateHabitatCoreActivity(activity);
        BrainStateText.Text = activity switch
        {
            < 0.06 => "Brain state: quiescent",
            < 0.30 => "Brain state: low activity",
            < 0.62 => "Brain state: active",
            _ => "Brain state: high activity"
        };
        if (_sleepState)
        {
            BrainStateText.Text += " (neuronal sleep state)";
        }

        _lastTelemetrySuccessUtc = DateTime.UtcNow;
        _telemetryFailureStreak = 0;
        SetConnectionStatus(AvatarControlStatusText.ConnectedWithPathways(tick, activePathways),
            Brushes.LightGreen,
            logOnChange: false);
    }

    private bool IsEngineInputOverloaded()
        => _engineServiceNonOkCount > 0 ||
           IsBrainInputPressureHigh(Environment.TickCount64);

    private bool IsBrainInputPressureHigh(long nowMs)
        => _engineInputPressure >= AvatarInputPressurePolicy.OptionalPausePressure ||
           _telemetryFailureStreak >= 2;

    private AvatarInputPressureDecision EvaluateBrainInputPressure(
        string channel,
        long nowMs,
        AvatarInputPriority priority,
        long normalIntervalMs)
    {
        var gate = GetBrainInputPressureGate(channel);
        var channelPaused = gate.ShouldPause(nowMs, out var gateReason);
        return AvatarInputPressurePolicy.Evaluate(
            _engineInputPressure,
            _telemetryFailureStreak,
            channelPaused,
            gateReason,
            priority,
            normalIntervalMs);
    }

    private AvatarInputPressureGate GetBrainInputPressureGate(string channel)
    {
        var key = NormalizeBrainInputChannel(channel);
        lock (_brainInputPressureGates)
        {
            if (!_brainInputPressureGates.TryGetValue(key, out var gate))
            {
                gate = new AvatarInputPressureGate(maxStreak: 8, maxExponent: 5, baseDelayMs: 500);
                _brainInputPressureGates[key] = gate;
            }

            return gate;
        }
    }

    private static string NormalizeBrainInputChannel(string channel)
        => string.IsNullOrWhiteSpace(channel)
            ? "unknown"
            : channel.Trim().ToLowerInvariant();

    private bool ShouldPauseOptionalBrainInput(string channel, long nowMs, out string reason)
    {
        var decision = EvaluateBrainInputPressure(channel, nowMs, AvatarInputPriority.Optional, 1);
        reason = decision.Reason;
        if (!decision.ShouldPause)
        {
            return false;
        }

        LogOptionalBrainInputPause(channel, reason, nowMs);
        return true;
    }

    private void LogOptionalBrainInputPause(string channel, string reason, long nowMs)
    {
        var warning = $"Optional brain input paused ({channel}): {reason}";
        var key = $"optional-brain-input:{channel}:{CreateDispatchWarningKey(channel, reason)}";
        if (_optionalBrainInputPressureWarningGate.ShouldLog(warning, key, nowMs))
        {
            Log(warning);
        }
    }

    private static double EstimateEngineInputPressure(JsonElement transport)
    {
        var reported = GetDouble(transport, "adaptivePressure", "AdaptivePressure");
        if (reported > 0.0)
        {
            return Math.Clamp(reported, 0.0, 1.0);
        }

        var droppedSpikes = Math.Max(0.0, GetDouble(transport, "dispatchQueueDroppedSpikes", "DispatchQueueDroppedSpikes"));
        var dispatchErrors = Math.Max(0.0, GetDouble(transport, "dispatchQueueDispatchErrors", "DispatchQueueDispatchErrors"));
        var spontaneousErrors = Math.Max(0.0, GetDouble(transport, "spontaneousDispatchErrors", "SpontaneousDispatchErrors"));
        var queuedSpikes = Math.Max(0.0, GetDouble(transport, "dispatchQueueQueuedSpikes", "DispatchQueueQueuedSpikes"));
        var dispatchedSpikes = Math.Max(0.0, GetDouble(transport, "dispatchedSpikes", "DispatchedSpikes"));
        var pressureSignal = droppedSpikes + (dispatchErrors * 24.0) + (spontaneousErrors * 6.0) + (queuedSpikes * 0.08);
        var denominator = Math.Max(32.0, queuedSpikes + dispatchedSpikes + 1.0);
        return Math.Clamp(pressureSignal / denominator, 0.0, 1.0);
    }

    private static double EstimateInputIngressPressure(JsonElement ingress)
    {
        var pressure = 0.0;
        foreach (var property in ingress.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var maxConcurrent = Math.Max(1.0, GetDouble(property.Value, "maxConcurrent", "MaxConcurrent"));
            var inFlight = Math.Max(0.0, GetDouble(property.Value, "inFlight", "InFlight"));
            var rejected = Math.Max(0.0, GetDouble(property.Value, "rejected", "Rejected"));
            var accepted = Math.Max(0.0, GetDouble(property.Value, "accepted", "Accepted"));
            var saturation = inFlight / maxConcurrent;
            var rejectionRate = rejected / Math.Max(1.0, accepted + rejected);
            pressure = Math.Max(pressure, Math.Clamp((saturation * 0.72) + (rejectionRate * 0.55), 0.0, 1.0));
        }

        return pressure;
    }

    private void HandleTelemetryFailure(string reason, bool forceLogOnFailure)
    {
        _telemetryFailureStreak++;
        var now = DateTime.UtcNow;
        var hasRecentTelemetry = _lastTelemetrySuccessUtc != DateTime.MinValue &&
                                 (now - _lastTelemetrySuccessUtc) <= TimeSpan.FromSeconds(TelemetryDelayGraceSeconds);
        if (hasRecentTelemetry)
        {
            var staleSeconds = (now - _lastTelemetrySuccessUtc).TotalSeconds;
            var msg = AvatarControlStatusText.TelemetryDelayed(reason, staleSeconds);
            SetConnectionStatus(msg, Brushes.Gold, logOnChange: false);
            return;
        }

        var logOnChange = forceLogOnFailure || _telemetryFailureStreak % 4 == 0;
        var error = AvatarControlStatusText.TelemetryIssue(reason);
        SetConnectionStatus(error, Brushes.OrangeRed, logOnChange);
    }

    private static long CountNonOkServicesFromTelemetry(JsonElement root)
    {
        if (!TryGetProperty(root, "serviceTelemetry", out var telemetry) ||
            telemetry.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        long nonOk = 0;
        foreach (var entry in telemetry.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var status = GetString(entry.Value, "lastStatus", "LastStatus");
            if (!string.IsNullOrWhiteSpace(status) &&
                !status.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                nonOk++;
            }
        }

        return nonOk;
    }

    private void UpdateHabitatCoreActivity(double activity)
    {
        var dimBase = Color.FromRgb(36, 138, 188);
        var bright = Color.FromRgb(165, 238, 255);
        var r = (byte)Math.Clamp((dimBase.R * (1.0 - activity)) + (bright.R * activity), 0, 255);
        var g = (byte)Math.Clamp((dimBase.G * (1.0 - activity)) + (bright.G * activity), 0, 255);
        var b = (byte)Math.Clamp((dimBase.B * (1.0 - activity)) + (bright.B * activity), 0, 255);
        _brainCoreDiffuseBrush.Color = Color.FromRgb(r, g, b);
        _brainCoreEmissiveBrush.Color = Color.FromArgb((byte)(44 + (190 * activity)), r, g, b);

        var pulse = 1.0 + (activity * 0.24);
        _brainCoreScale.ScaleX = pulse;
        _brainCoreScale.ScaleY = pulse;
        _brainCoreScale.ScaleZ = pulse;
    }

    private void ApplyConfiguredEndpointSelection()
    {
        if (!AvatarEndpointResolver.TryNormalizeEndpoint(_resolvedEndpoint, out var normalized))
        {
            normalized = AvatarControlEndpointSettings.DefaultEndpoint;
        }

        _resolvedEndpoint = normalized;
        EndpointComboBox.Text = normalized;

        ComboBoxItem? matchingItem = null;
        foreach (var item in EndpointComboBox.Items)
        {
            if (item is not ComboBoxItem comboItem || comboItem.Content is not string endpoint)
            {
                continue;
            }

            if (!AvatarEndpointResolver.TryNormalizeEndpoint(endpoint, out var itemNormalized))
            {
                continue;
            }

            if (string.Equals(itemNormalized, normalized, StringComparison.OrdinalIgnoreCase))
            {
                matchingItem = comboItem;
                break;
            }
        }

        if (matchingItem is null)
        {
            matchingItem = new ComboBoxItem { Content = normalized };
            EndpointComboBox.Items.Insert(0, matchingItem);
        }

        EndpointComboBox.SelectedItem = matchingItem;
    }

    private static string ResolveConfiguredControlEndpoint()
    {
        return AvatarControlEndpointSettings.ResolveConfiguredEndpoint();
    }

    private string GetSelectedEndpoint()
    {
        if (EndpointComboBox.SelectedItem is ComboBoxItem item &&
            item.Content is string endpoint &&
            AvatarEndpointResolver.TryNormalizeEndpoint(endpoint, out var fromSelection))
        {
            _resolvedEndpoint = fromSelection;
            return _resolvedEndpoint;
        }

        if (AvatarEndpointResolver.TryNormalizeEndpoint(EndpointComboBox.Text, out var fromText))
        {
            _resolvedEndpoint = fromText;
            return _resolvedEndpoint;
        }

        if (AvatarEndpointResolver.TryNormalizeEndpoint(_resolvedEndpoint, out var cached))
        {
            var now = Environment.TickCount64;
            var warning = AvatarControlStatusText.EndpointFallback(cached);
            if (_endpointValidationWarningGate.ShouldLog(warning, now))
            {
                Log(warning);
            }

            _resolvedEndpoint = cached;
            return _resolvedEndpoint;
        }

        _resolvedEndpoint = AvatarControlEndpointSettings.DefaultEndpoint;
        return _resolvedEndpoint;
    }

    private void SetConnectionStatus(string text, Brush brush, bool logOnChange)
    {
        ConnectionStatusText.Text = text;
        ConnectionStatusText.Foreground = brush;
        if (logOnChange && !string.Equals(_lastEndpointMessage, text, StringComparison.Ordinal))
        {
            Log(text);
        }

        _lastEndpointMessage = text;
    }

    private static string ResolveRuntimeStatePath()
    {
        var configured = Environment.GetEnvironmentVariable("NRE_WORLDSIM_STATE_PATH");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(RuntimeLogDirectory, "worldsim-state.json")
            : Path.GetFullPath(configured.Trim());
    }

    private void QueueRuntimeStateSnapshot(bool running)
    {
        if (Interlocked.Exchange(ref _runtimeStateWriteInFlight, 1) != 0)
        {
            return;
        }

        var snapshot = CreateRuntimeStateSnapshot(running);
        _runtimeStateWriteTask = Task.Run(() =>
        {
            try
            {
                WriteRuntimeState(snapshot);
            }
            finally
            {
                Interlocked.Exchange(ref _runtimeStateWriteInFlight, 0);
            }
        });
    }

    private WorldSimulationStatus CreateRuntimeStateSnapshot(bool running)
    {
        var generatedUtc = DateTimeOffset.UtcNow;
        var telemetryAgeSeconds = _lastTelemetrySuccessUtc == DateTime.MinValue
            ? double.MaxValue
            : Math.Max(0.0, (DateTime.UtcNow - _lastTelemetrySuccessUtc).TotalSeconds);
        return new WorldSimulationStatus(
            ProtocolVersion: "dnne.worldsim.state.v1",
            SessionId: _runtimeSessionId,
            ProcessId: Environment.ProcessId,
            Running: running,
            WorldReady: _heights is not null,
            GeneratedUtc: generatedUtc,
            SessionStartedUtc: _runtimeSessionStartedUtc,
            ElapsedSeconds: Math.Max(0.0, (generatedUtc - _runtimeSessionStartedUtc).TotalSeconds),
            ControlEndpoint: GetSelectedEndpoint(),
            BrainConnected: telemetryAgeSeconds <= 5.0,
            TelemetryAgeSeconds: telemetryAgeSeconds,
            Seed: _seed,
            AvatarX: _avatarX,
            AvatarY: _avatarY,
            AvatarZ: _avatarZ,
            AvatarHeadingDeg: _avatarHeadingDeg,
            DistanceTravelled: _distanceTravelled,
            VisitedTerrainCells: _visitedTerrainCells.Count,
            ExplorableTerrainCells: _explorableTerrainCells,
            NeuronalMotorDispatchTotal: _neuronalMotorDispatchTotal,
            NeuronalLocomotorDispatchTotal: _neuronalLocomotorDispatchTotal,
            NeuronalManipulatorDispatchTotal: _neuronalManipulatorDispatchTotal,
            LeftMotorDrive: _leftMotorDrive,
            RightMotorDrive: _rightMotorDrive,
            ManipulatorDrive: _manipulatorDrive,
            InteractionAttempts: _interactionAttempts,
            InteractionSuccesses: _interactionSuccesses,
            RetinalFramesAccepted: _retinalFramesAccepted,
            CochlearFramesAccepted: _cochlearFramesAccepted,
            PhysicalBodyFramesAccepted: _physicalBodyFramesAccepted,
            SomaticFramesAccepted: _somaticFramesAccepted,
            FoodConsumed: _foodConsumed,
            WeaponPickupsCollected: _weaponPickupsCollected,
            WeaponCharges: _weaponCharges,
            WaterInteractions: _waterInteractions,
            PredatorsActive: _predators.Count,
            PredatorsNeutralized: _predatorsNeutralized,
            StoredEnergyJoules: _storedEnergyJoules,
            TissueIntegrityFraction: _tissueIntegrity,
            HydrationFraction: _hydrationFraction,
            InShelter: IsInShelter(),
            NeuronalSleep: _sleepState,
            CollisionHits: _collisionHits,
            TickFailures: _tickFailures);
    }

    private static void WriteRuntimeState(WorldSimulationStatus snapshot)
    {
        var directory = Path.GetDirectoryName(RuntimeStatePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{RuntimeStatePath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(snapshot, RuntimeStateJsonOptions),
            Encoding.UTF8);
        File.Move(temporaryPath, RuntimeStatePath, overwrite: true);
    }

    private void InitializeRuntimeLogFile()
    {
        lock (_runtimeLogSync)
        {
            Directory.CreateDirectory(RuntimeLogDirectory);
            if (!File.Exists(RuntimeLogPath))
            {
                return;
            }

            var length = new FileInfo(RuntimeLogPath).Length;
            if (length <= RuntimeLogMaxBytes)
            {
                return;
            }

            try
            {
                if (File.Exists(RuntimeLogArchivePath))
                {
                    File.Delete(RuntimeLogArchivePath);
                }

                File.Move(RuntimeLogPath, RuntimeLogArchivePath);
            }
            catch
            {
                // Never block simulation startup on log rotation failures.
            }
        }
    }

    private void AppendRuntimeLogLine(string line)
    {
        _runtimeLogWriter.Enqueue(line);
    }

    private async Task SafeTickAsync(Func<Task> tick, string description)
    {
        try
        {
            await tick();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _tickFailures++;
            AppendRuntimeLogLine($"[{DateTime.Now:HH:mm:ss}] Tick failure ({description}): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        AppendRuntimeLogLine(line);
        _logLines.Add(line);

        if (!_logTextInitialized)
        {
            LogTextBox.Text = line;
            _logTextInitialized = true;
        }
        else
        {
            LogTextBox.AppendText(Environment.NewLine + line);
        }

        if (_logLines.Count > MaxLogLines)
        {
            _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
            LogTextBox.Text = string.Join(Environment.NewLine, _logLines);
            _logTextInitialized = _logLines.Count > 0;
        }
        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
    }

    private byte[] EnsureAvatarPreviewBuffer(int stride, int height)
    {
        var required = stride * height;
        if (_avatarPreviewPixels is null || _avatarPreviewPixels.Length != required)
        {
            _avatarPreviewPixels = new byte[required];
        }

        return _avatarPreviewPixels;
    }

    private void LogVisionDispatchWarning(string message)
    {
        var now = Environment.TickCount64;
        if (!_visionDispatchWarningGate.ShouldLog(message, CreateDispatchWarningKey("avatar-vision", message), now))
        {
            return;
        }

        Log($"Avatar vision dispatch warning: {message}");
    }

    private void RegisterVisionDispatchFailure(string message)
    {
        var now = Environment.TickCount64;
        var backoffMs = _visionDispatchBackoff.RegisterFailure(now);
        RegisterOptionalBrainInputFailure("avatar vision", message, now);
        LogVisionDispatchWarning($"{message} (streak {_visionDispatchBackoff.FailureStreak}, backoff {backoffMs}ms)");
    }

    private void RegisterOptionalBrainInputFailure(string channel, string message, long nowMs)
    {
        var severe = IsControlEndpointPressureFailure(message);
        var reason = $"{channel}: {TrimForLog(message, 90)}";
        var gate = GetBrainInputPressureGate(channel);
        var pauseMs = gate.RegisterFailure(nowMs, reason, severe);
        var warning = $"Optional brain input pressure: paused after {reason} (streak {gate.FailureStreak}, backoff {pauseMs}ms)";
        var key = $"optional-brain-input-pressure:{CreateDispatchWarningKey(channel, message)}";
        if (_optionalBrainInputPressureWarningGate.ShouldLog(warning, key, nowMs))
        {
            Log(warning);
        }
    }

    private void RegisterOptionalBrainInputSuccess(string channel)
    {
        GetBrainInputPressureGate(channel).RegisterSuccess(Environment.TickCount64);
    }

    private void ResetBrainInputPressureGates()
    {
        lock (_brainInputPressureGates)
        {
            foreach (var gate in _brainInputPressureGates.Values)
            {
                gate.Reset();
            }
        }
    }

    private static bool IsControlEndpointPressureFailure(string message)
    {
        var normalized = message.ToLowerInvariant();
        return normalized.Contains("timeout", StringComparison.Ordinal) ||
               normalized.Contains("taskcanceledexception", StringComparison.Ordinal) ||
               normalized.Contains("request was canceled", StringComparison.Ordinal) ||
               normalized.Contains("http 429", StringComparison.Ordinal) ||
               normalized.Contains("too many requests", StringComparison.Ordinal) ||
               normalized.Contains("http 500", StringComparison.Ordinal) ||
               normalized.Contains("http 502", StringComparison.Ordinal) ||
               normalized.Contains("http 503", StringComparison.Ordinal) ||
               normalized.Contains("http 504", StringComparison.Ordinal) ||
               normalized.Contains("response ended prematurely", StringComparison.Ordinal);
    }

    private static string CreateDispatchWarningKey(string channel, string message)
    {
        var normalized = message.ToLowerInvariant();
        if (normalized.Contains("timeout", StringComparison.Ordinal) ||
            normalized.Contains("taskcanceledexception", StringComparison.Ordinal) ||
            normalized.Contains("request was canceled", StringComparison.Ordinal))
        {
            return $"{channel}:timeout";
        }

        if (normalized.Contains("responseended", StringComparison.Ordinal) ||
            normalized.Contains("response ended prematurely", StringComparison.Ordinal))
        {
            return $"{channel}:response-ended";
        }

        if (normalized.Contains("actively refused", StringComparison.Ordinal) ||
            normalized.Contains("connection refused", StringComparison.Ordinal))
        {
            return $"{channel}:connection-refused";
        }

        if (normalized.StartsWith("http ", StringComparison.Ordinal))
        {
            var space = message.IndexOf(' ', StringComparison.Ordinal);
            var colon = message.IndexOf(':', StringComparison.Ordinal);
            if (space >= 0 && colon > space)
            {
                return $"{channel}:{message[..colon]}";
            }
        }

        return $"{channel}:{message}";
    }

    private static double DegreesToRadians(double degrees) => AvatarKinematics.DegreesToRadians(degrees);

    private static bool TryGetProperty(JsonElement element, string property, out JsonElement value) => AvatarJson.TryGetProperty(element, property, out value);
    private static string GetString(JsonElement element, params string[] propertyNames) => AvatarJson.GetString(element, propertyNames);
    private static long GetLong(JsonElement element, params string[] propertyNames) => AvatarJson.GetLong(element, propertyNames);
    private static double GetDouble(JsonElement element, params string[] propertyNames) => AvatarJson.GetDouble(element, propertyNames);

    private static long TryGetInt64(JsonElement element, string property) => GetLong(element, property);
    private static double TryGetDouble(JsonElement element, string property) => GetDouble(element, property);

    private static double ComputeNeedDrive(double signal, double enter, double full)
    {
        if (full <= enter)
        {
            return Math.Clamp(signal, 0.0, 1.0);
        }

        var normalized = Math.Clamp((signal - enter) / (full - enter), 0.0, 1.0);
        // Smooth ramp avoids abrupt trigger edges around threshold boundaries.
        return normalized * normalized * (3.0 - (2.0 * normalized));
    }

    private static OptionalElement TryGetObject(JsonElement element, string property)
    {
        if (TryGetProperty(element, property, out var value) && value.ValueKind == JsonValueKind.Object)
        {
            return new OptionalElement(value);
        }

        return new OptionalElement();
    }

    private void UpdateMotorPathwayAuditFromFrame(JsonElement frameRoot, IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        if (!TryGetProperty(frameRoot, "latestSnapshot", out var snapshot) ||
            snapshot.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            _motorPathwayAuditText = "Motor pathway: waiting for brain snapshot.";
            return;
        }

        var signals = ReadMotorPathwaySignals(snapshot, dispatches);
        if (signals.Count == 0)
        {
            _motorPathwayAuditText = "Motor pathway: no motor-chain structures in latest snapshot.";
            return;
        }

        var parts = new List<string>(MotorPathwayStages.Length);
        foreach (var stage in MotorPathwayStages)
        {
            signals.TryGetValue(stage.Label, out var signal);
            parts.Add($"{stage.Label} {signal.MeanRateHz:0.0}Hz d{signal.DispatchCount}");
        }

        _motorPathwayAuditText = $"Motor pathway: {string.Join(" | ", parts)}; {ResolveMotorPathwayBreak(signals)}";
    }

    private static Dictionary<string, MotorPathwaySignal> ReadMotorPathwaySignals(
        JsonElement snapshot,
        IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        var signals = new Dictionary<string, MotorPathwaySignal>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetProperty(snapshot, "structureStates", out var states) || states.ValueKind != JsonValueKind.Array)
        {
            return signals;
        }

        foreach (var state in states.EnumerateArray())
        {
            if (state.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var structure = ParseAnyStructureId(state, "structureId", "structure_id");
            if (string.IsNullOrWhiteSpace(structure) || !MotorPathwayStageLookup.TryGetValue(structure, out var stage))
            {
                continue;
            }

            signals.TryGetValue(stage.Label, out var current);
            signals[stage.Label] = current.AddSnapshot(
                GetDouble(state, "meanFiringRateHz", "mean_firing_rate_hz"),
                (int)Math.Max(0, GetLong(state, "spikeInCount", "spike_in_count")),
                (int)Math.Max(0, GetLong(state, "spikeOutCount", "spike_out_count")));
        }

        foreach (var dispatch in dispatches)
        {
            if (string.IsNullOrWhiteSpace(dispatch.SourceStructure) ||
                !MotorPathwayStageLookup.TryGetValue(dispatch.SourceStructure, out var stage))
            {
                continue;
            }

            signals.TryGetValue(stage.Label, out var current);
            signals[stage.Label] = current.AddDispatch();
        }

        if (TryGetProperty(snapshot, "activePathways", out var pathways) && pathways.ValueKind == JsonValueKind.Array)
        {
            foreach (var pathway in pathways.EnumerateArray())
            {
                var source = ParseAnyStructureId(pathway, "source", "sourceStructure", "source_structure");
                if (string.IsNullOrWhiteSpace(source) || !MotorPathwayStageLookup.TryGetValue(source, out var stage))
                {
                    continue;
                }

                var volume = (int)Math.Max(0, GetLong(pathway, "spikeVolume", "spike_volume"));
                signals.TryGetValue(stage.Label, out var current);
                signals[stage.Label] = current.AddPathwayVolume(volume);
            }
        }

        return signals;
    }

    private static Dictionary<string, MotorPathwayStage> BuildMotorPathwayStageLookup()
    {
        var lookup = new Dictionary<string, MotorPathwayStage>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in MotorPathwayStages)
        {
            foreach (var alias in stage.Structures)
            {
                lookup[alias] = stage;
            }
        }

        return lookup;
    }

    private static string ResolveMotorPathwayBreak(IReadOnlyDictionary<string, MotorPathwaySignal> signals)
    {
        var anyActive = false;
        for (var i = 0; i < MotorPathwayStages.Length; i++)
        {
            var stage = MotorPathwayStages[i];
            signals.TryGetValue(stage.Label, out var signal);
            if (signal.IsActive)
            {
                anyActive = true;
                continue;
            }

            if (anyActive)
            {
                return $"break near {stage.Label}";
            }
        }

        if (!anyActive)
        {
            return "chain quiet";
        }

        signals.TryGetValue("M1", out var m1);
        signals.TryGetValue("Spinal", out var spinal);
        if (m1.IsActive && !spinal.IsActive)
        {
            return "M1 active, spinal output quiet";
        }

        return "descending chain active";
    }

    private void UpdateBrainMotorDecisionFromState(JsonElement stateElement)
    {
        if (!TryGetObject(stateElement, "neuronalMotor").TryGetValue(out var motor))
        {
            _brainMotorDecisionText = "Neuronal motor: waiting for measured population decoder state.";
            return;
        }

        var active = AvatarJson.GetBool(motor, "active", "Active");
        var selectedChannel = (int)GetLong(motor, "selectedActionChannel", "SelectedActionChannel");
        var confidence = Math.Clamp(GetDouble(motor, "confidence", "Confidence"), 0.0, 1.0);
        var actionConfidence = Math.Clamp(GetDouble(motor, "actionSelectionConfidence", "ActionSelectionConfidence"), 0.0, 1.0);
        var gate = Math.Clamp(GetDouble(motor, "selectionGate", "SelectionGate"), 0.0, 1.0);
        var inhibition = Math.Clamp(GetDouble(motor, "outputInhibition", "OutputInhibition"), 0.0, 1.0);
        var motorCoverage = Math.Clamp(GetDouble(motor, "motorCircuitCoverage", "MotorCircuitCoverage"), 0.0, 1.0);
        var actionCoverage = Math.Clamp(GetDouble(motor, "actionCircuitCoverage", "ActionCircuitCoverage"), 0.0, 1.0);
        var margin = Math.Clamp(GetDouble(motor, "actionSelectionMargin", "ActionSelectionMargin"), 0.0, 1.0);
        var left = GetDouble(motor, "leftDrive", "LeftDrive");
        var right = GetDouble(motor, "rightDrive", "RightDrive");
        var holdReason = ResolveMotorDecisionStatus(active, confidence, gate, inhibition);

        _brainMotorDecisionText =
            $"Neuronal motor: channel {selectedChannel}; active {active}; confidence {confidence:0.00}; action confidence {actionConfidence:0.00}; gate {gate:0.00}; inhibition {inhibition:0.00}; coverage motor/action {motorCoverage:0.00}/{actionCoverage:0.00}; margin {margin:0.00}; decoded L/R {left:0.00}/{right:0.00}; body fwd {_lastForwardSpeed:0.00}, turn {_lastTurnRateDeg:0}; status {holdReason}";
    }

    private string ResolveMotorDecisionStatus(bool active, double confidence, double gate, double inhibition)
    {
        if (_sleepState)
        {
            return "neuronal sleep observed";
        }

        if (!active)
        {
            return "population decoder inactive";
        }

        if (gate < 0.18)
        {
            return "basal ganglia gate low";
        }

        if (inhibition > 0.72)
        {
            return "inhibition high";
        }

        if (confidence < 0.18)
        {
            return "selection confidence low";
        }

        if (_lastMotorDispatchCount <= 0 && _ticksWithoutMotorDispatch > 3)
        {
            return "no recent M1/SMA dispatch";
        }

        var drive = Math.Max(Math.Abs(_leftMotorDrive), Math.Abs(_rightMotorDrive));
        if (drive < 1.0 && Math.Abs(_lastForwardSpeed) < 0.05 && Math.Abs(_lastTurnRateDeg) < 2.0)
        {
            return "motor drive near zero";
        }

        if (_lastForwardSpeed > 0.08)
        {
            return "forward command active";
        }

        if (_lastForwardSpeed < -0.08)
        {
            return "reverse command active";
        }

        if (Math.Abs(_lastTurnRateDeg) > 2.0)
        {
            return "turn command active";
        }

        return "standing by";
    }

    private bool IsSpawnLocationClear(double worldX, double terrainY, double worldZ)
    {
        if (IsCollisionAt(worldX, worldZ, out _, ignoreStepHeight: true))
        {
            return false;
        }

        const double minimumSeparation = 1.25;
        var minimumSeparationSq = minimumSeparation * minimumSeparation;
        if (DistanceSquared(_avatarX, _avatarZ, worldX, worldZ) < minimumSeparationSq)
        {
            return false;
        }

        foreach (var pickup in _foodPickups)
        {
            if (pickup.Active && DistanceSquared(pickup.Position.X, pickup.Position.Z, worldX, worldZ) < minimumSeparationSq)
            {
                return false;
            }
        }

        foreach (var pickup in _weaponPickups)
        {
            if (pickup.Active && DistanceSquared(pickup.Position.X, pickup.Position.Z, worldX, worldZ) < minimumSeparationSq)
            {
                return false;
            }
        }

        foreach (var predator in _predators)
        {
            if (DistanceSquared(predator.Position.X, predator.Position.Z, worldX, worldZ) < minimumSeparationSq)
            {
                return false;
            }
        }

        return !IsAnyCollisionNear(worldX, terrainY + AvatarFootOffset, worldZ);
    }

    private static bool IsSleepingState(JsonElement stateElement) => AvatarJson.IsSleepingState(stateElement);
    private static string ParseAnyStructureId(JsonElement element, params string[] propertyNames) => AvatarJson.ParseAnyStructureId(element, propertyNames);
    private static string NormalizeHemisphere(string hemisphere) => AvatarJson.NormalizeHemisphere(hemisphere);

    private static double NormalizeDegrees(double angle) => AvatarKinematics.NormalizeDegrees(angle);

    private static double NormalizeSignedDegrees(double angle)
    {
        var wrapped = ((angle + 540.0) % 360.0) - 180.0;
        return wrapped == -180.0 ? 180.0 : wrapped;
    }

    private static double DistanceSquared(double x1, double z1, double x2, double z2)
    {
        var dx = x2 - x1;
        var dz = z2 - z1;
        return (dx * dx) + (dz * dz);
    }

    private static long MakeSurfaceKey(int x, int z) => ((long)x << 32) | (uint)z;

    private enum BlockKind
    {
        Grass,
        Dirt,
        Stone,
        Sand,
        Water,
        Wood,
        Leaves,
        HabitatWall,
        HabitatGlass,
        HabitatFloor,
        Food,
        WeaponShort,
        WeaponLong,
        Predator
    }

    private readonly record struct VisionComputeRequest(
        int Generation,
        long CaptureTimestampMs,
        int Width,
        int Height,
        int Stride,
        double EyeX,
        double EyeY,
        double EyeZ,
        double ForwardX,
        double ForwardZ,
        double RightX,
        double RightZ,
        int[,] Heights,
        VisionTerrainCell[,] TerrainCells,
        VisionHitGrid VisionHitGrid,
        VisionHitBox[] DynamicVisionHitBoxes,
        IReadOnlyDictionary<long, BlockKind> SurfaceOverrides);

    private readonly record struct VisionSceneSnapshot(
        int[,] Heights,
        VisionTerrainCell[,] TerrainCells,
        VisionHitGrid HitGrid,
        IReadOnlyDictionary<long, BlockKind> SurfaceOverrides);

    private readonly record struct VisionTerrainCell(
        BlockKind Kind,
        Color Color);

    private sealed class VisionComputeRequestEnvelope
    {
        public VisionComputeRequestEnvelope(VisionComputeRequest request)
        {
            Request = request;
        }

        public VisionComputeRequest Request { get; }
    }

    private sealed record VisionComputeResult(AvatarSightFrame SightFrame)
    {
        public int Generation => SightFrame.Generation;
        public long CaptureTimestampMs => SightFrame.CaptureTimestampMs;
        public int Width => SightFrame.Width;
        public int Height => SightFrame.Height;
        public int Stride => SightFrame.Stride;
        public byte[] Pixels => SightFrame.Pixels;
        public double PreviewHeadingDeg => SightFrame.PreviewHeadingDeg;
    }

    private sealed record MotorPathwayStage(string Label, string[] Structures);

    private readonly record struct MotorPathwaySignal(
        double MeanRateHz,
        int SpikeInCount,
        int SpikeOutCount,
        int DispatchCount,
        int PathwayVolume)
    {
        public bool IsActive =>
            DispatchCount > 0 ||
            SpikeOutCount > 0 ||
            PathwayVolume > 0 ||
            MeanRateHz >= 0.30;

        public MotorPathwaySignal AddSnapshot(double meanRateHz, int spikeInCount, int spikeOutCount)
            => this with
            {
                MeanRateHz = Math.Max(MeanRateHz, Math.Max(0.0, meanRateHz)),
                SpikeInCount = SpikeInCount + spikeInCount,
                SpikeOutCount = SpikeOutCount + spikeOutCount
            };

        public MotorPathwaySignal AddDispatch()
            => this with { DispatchCount = DispatchCount + 1 };

        public MotorPathwaySignal AddPathwayVolume(int volume)
            => this with { PathwayVolume = PathwayVolume + volume };
    }

    private readonly struct OptionalElement
    {
        public OptionalElement(JsonElement value)
        {
            Value = value;
            HasValue = true;
        }

        public JsonElement Value { get; }
        public bool HasValue { get; }

        public bool TryGetValue(out JsonElement value)
        {
            value = Value;
            return HasValue;
        }
    }

    private sealed class FoodPickup
    {
        public FoodPickup(Point3D position, TranslateTransform3D transform, Model3D model)
        {
            Position = position;
            Transform = transform;
            Model = model;
        }

        public Point3D Position { get; set; }
        public TranslateTransform3D Transform { get; }
        public Model3D Model { get; }
        public bool Active { get; set; } = true;
    }

    private sealed class WeaponPickup
    {
        public WeaponPickup(
            Point3D position,
            TranslateTransform3D transform,
            Model3D model,
            AvatarDeviceRangeProfile rangeProfile)
        {
            Position = position;
            Transform = transform;
            Model = model;
            RangeProfile = rangeProfile;
        }

        public Point3D Position { get; set; }
        public TranslateTransform3D Transform { get; }
        public Model3D Model { get; }
        public AvatarDeviceRangeProfile RangeProfile { get; }
        public bool Active { get; set; } = true;
    }

    private sealed class PredatorNpc
    {
        public PredatorNpc(
            Point3D position,
            double headingDeg,
            TranslateTransform3D transform,
            AxisAngleRotation3D yawRotation,
            Model3D model,
            List<Point3D> patrolPoints,
            int patrolIndex,
            GeometryModel3D threatModel,
            ScaleTransform3D threatScale,
            GeometryModel3D pathModel)
        {
            Position = position;
            HeadingDeg = headingDeg;
            Transform = transform;
            YawRotation = yawRotation;
            Model = model;
            PatrolPoints = patrolPoints;
            PatrolIndex = patrolIndex;
            ThreatModel = threatModel;
            ThreatScale = threatScale;
            ThreatTranslate = threatModel.Transform is Transform3DGroup group && group.Children.Count > 1
                ? (TranslateTransform3D)group.Children[1]
                : new TranslateTransform3D();
            PathModel = pathModel;
            ThreatMaterial = threatModel.Material;
            PathMaterial = pathModel.Material;
        }

        public Point3D Position { get; set; }
        public double HeadingDeg { get; set; }
        public TranslateTransform3D Transform { get; }
        public AxisAngleRotation3D YawRotation { get; }
        public Model3D Model { get; }
        public List<Point3D> PatrolPoints { get; }
        public int PatrolIndex { get; set; }
        public GeometryModel3D ThreatModel { get; }
        public ScaleTransform3D ThreatScale { get; }
        public TranslateTransform3D ThreatTranslate { get; }
        public GeometryModel3D PathModel { get; }
        public Material? ThreatMaterial { get; }
        public Material? PathMaterial { get; }
    }


    private readonly record struct CaveAnchor(double X, double Y, double Z);
    private readonly record struct ShelterSite(double X, double BaseY, double Z, double Radius);
    private readonly record struct PendingPhysicalContact(
        float BodyPositionX,
        float BodyPositionY,
        float BodyPositionZ,
        float SurfaceNormalX,
        float SurfaceNormalY,
        float SurfaceNormalZ,
        float ForceNewtons,
        float ImpulseNewtonSeconds,
        float PenetrationMillimeters,
        float TangentialSpeedMetersPerSecond,
        float ContactAreaSquareMillimeters,
        float DurationMilliseconds,
        string InputSource);

    private sealed record WorldSimulationStatus(
        string ProtocolVersion,
        string SessionId,
        int ProcessId,
        bool Running,
        bool WorldReady,
        DateTimeOffset GeneratedUtc,
        DateTimeOffset SessionStartedUtc,
        double ElapsedSeconds,
        string ControlEndpoint,
        bool BrainConnected,
        double TelemetryAgeSeconds,
        int Seed,
        double AvatarX,
        double AvatarY,
        double AvatarZ,
        double AvatarHeadingDeg,
        double DistanceTravelled,
        int VisitedTerrainCells,
        int ExplorableTerrainCells,
        long NeuronalMotorDispatchTotal,
        long NeuronalLocomotorDispatchTotal,
        long NeuronalManipulatorDispatchTotal,
        double LeftMotorDrive,
        double RightMotorDrive,
        double ManipulatorDrive,
        long InteractionAttempts,
        long InteractionSuccesses,
        long RetinalFramesAccepted,
        long CochlearFramesAccepted,
        long PhysicalBodyFramesAccepted,
        long SomaticFramesAccepted,
        int FoodConsumed,
        int WeaponPickupsCollected,
        int WeaponCharges,
        int WaterInteractions,
        int PredatorsActive,
        int PredatorsNeutralized,
        double StoredEnergyJoules,
        double TissueIntegrityFraction,
        double HydrationFraction,
        bool InShelter,
        bool NeuronalSleep,
        int CollisionHits,
        long TickFailures);

    private readonly record struct CollisionBox(
        double MinX,
        double MaxX,
        double MinY,
        double MaxY,
        double MinZ,
        double MaxZ);

    private readonly record struct VisionHitBox(
        double MinX,
        double MaxX,
        double MinY,
        double MaxY,
        double MinZ,
        double MaxZ,
        BlockKind Kind);

    private sealed class VisionHitGrid
    {
        private const double CellSize = 4.0;
        public static readonly VisionHitGrid Empty = new([], [], 0, 0, 0.0, 0.0);

        private readonly int[]?[] _buckets;
        private readonly int _dimX;
        private readonly int _dimZ;
        private readonly double _originX;
        private readonly double _originZ;

        private VisionHitGrid(
            VisionHitBox[] boxes,
            int[]?[] buckets,
            int dimX,
            int dimZ,
            double originX,
            double originZ)
        {
            Boxes = boxes;
            _buckets = buckets;
            _dimX = dimX;
            _dimZ = dimZ;
            _originX = originX;
            _originZ = originZ;
        }

        public VisionHitBox[] Boxes { get; }
        public int Count => Boxes.Length;

        public static VisionHitGrid Build(VisionHitBox[] boxes)
        {
            if (boxes.Length == 0)
            {
                return Empty;
            }

            var minX = boxes[0].MinX;
            var maxX = boxes[0].MaxX;
            var minZ = boxes[0].MinZ;
            var maxZ = boxes[0].MaxZ;
            for (var i = 1; i < boxes.Length; i++)
            {
                var box = boxes[i];
                minX = Math.Min(minX, box.MinX);
                maxX = Math.Max(maxX, box.MaxX);
                minZ = Math.Min(minZ, box.MinZ);
                maxZ = Math.Max(maxZ, box.MaxZ);
            }

            var originX = Math.Floor(minX / CellSize) * CellSize;
            var originZ = Math.Floor(minZ / CellSize) * CellSize;
            var dimX = Math.Max(1, (int)Math.Ceiling((maxX - originX) / CellSize) + 1);
            var dimZ = Math.Max(1, (int)Math.Ceiling((maxZ - originZ) / CellSize) + 1);
            var builders = new List<int>?[dimX * dimZ];

            for (var i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                var gx0 = Math.Clamp((int)Math.Floor((box.MinX - originX) / CellSize), 0, dimX - 1);
                var gx1 = Math.Clamp((int)Math.Floor((box.MaxX - originX) / CellSize), 0, dimX - 1);
                var gz0 = Math.Clamp((int)Math.Floor((box.MinZ - originZ) / CellSize), 0, dimZ - 1);
                var gz1 = Math.Clamp((int)Math.Floor((box.MaxZ - originZ) / CellSize), 0, dimZ - 1);
                for (var gz = gz0; gz <= gz1; gz++)
                {
                    for (var gx = gx0; gx <= gx1; gx++)
                    {
                        var bucketIndex = (gz * dimX) + gx;
                        builders[bucketIndex] ??= [];
                        builders[bucketIndex]!.Add(i);
                    }
                }
            }

            var buckets = new int[]?[builders.Length];
            for (var i = 0; i < builders.Length; i++)
            {
                buckets[i] = builders[i]?.ToArray();
            }

            return new VisionHitGrid(boxes, buckets, dimX, dimZ, originX, originZ);
        }

        public int[]? GetBucket(double x, double z)
        {
            var gx = (int)Math.Floor((x - _originX) / CellSize);
            var gz = (int)Math.Floor((z - _originZ) / CellSize);
            if (gx < 0 || gx >= _dimX || gz < 0 || gz >= _dimZ)
            {
                return null;
            }

            return _buckets[(gz * _dimX) + gx];
        }
    }
}







