using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;
using System.Diagnostics;
using NeuralResonanceEngine.Shared.Contracts;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NRE.WpfMazeSim;

public partial class MainWindow : Window
{
    private const double CellSize = 1.5;
    private const double WallThicknessScale = 0.5;
    private const double WallJoinOverlapScale = 0.02;
    private const double WallHeight = 1.8;
    private const double AvatarRadius = 0.20;
    private const double GoalRadius = 0.28;
    private const double FoodRadius = 0.15;
    private const double HazardRadius = 0.22;
    private const double CheckpointRadius = 0.19;
    private const int MazeRows = 31;
    private const int MazeCols = 31;
    private const int FoodTargetCount = 16;
    private const int HazardTargetCount = 10;
    private const int CheckpointTargetCount = 5;
    private const int DefaultMazeSeed = 317;
    private const int MaxLogLines = 240;
    private const int VisionWidth = 196;
    private const int VisionHeight = 196;
    private static readonly Int32Rect VisionBitmapRect = new(0, 0, VisionWidth, VisionHeight);
    private const double VisionFovDegrees = 96.0;
    private const double VisionRange = 9.5;
    private const double CameraMinPitchDeg = -85.0;
    private const double CameraMaxPitchDeg = 65.0;
    private const double EscapeAggressivenessDefault = 1.0;
    private const double EscapeCooldownDefaultMs = 900.0;
    private const double WallProbeRange = 1.7;
    private const double WallProbeSideAngleDeg = 34.0;
    private const double HardStuckTimeoutSec = 6.5;
    private const double HardStuckCollisionBurstWindowSec = 1.8;
    private const int HardStuckCollisionBurstThreshold = 5;
    private static readonly TimeSpan HazardDamageCooldown = TimeSpan.FromMilliseconds(820);
    private static readonly TimeSpan WallImpactPenaltyCooldown = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan NoProgressRecoveryTimeout = TimeSpan.FromSeconds(4.0);
    private static readonly TimeSpan ObjectStimulusCooldown = TimeSpan.FromMilliseconds(450);
    private const int BodyStateDispatchIntervalMs = 350;
    private const int EnvironmentAudioDispatchIntervalMs = 1800;
    private const double MazeWalkMaxForwardSpeed = 3.2;
    private const double MazeRunSpeedMultiplier = 3.0;
    private const double MazeRunMaxForwardSpeed = MazeWalkMaxForwardSpeed * MazeRunSpeedMultiplier;
    private const double AvatarHeadMaxYawDeg = 76.0;
    private const double AvatarHeadReturnRateDeg = 220.0;

    // Shared kinematics options for the maze avatar: forward-only (no reverse),
    // capped to walk speed (run scale is applied externally), and tighter turn cap.
    private static readonly AvatarKinematicsOptions MazeKinematicsOptions = new(
        MaxMotorDrive: 240.0,
        ForwardSpeedCoefficient: 0.0125,
        TurnSpeedCoefficient: 3.2,
        MinForwardSpeed: 0.0,
        MaxForwardSpeed: MazeWalkMaxForwardSpeed,
        MaxTurnRateDeg: 220.0,
        AllowSignedMotorDrive: true,
        InPlaceTurnCancelsForwardDrive: true);
    private static readonly AvatarNervousSystemOptions MazeNervousSystemOptions = new(
        MazeKinematicsOptions,
        DriveDecay: 0.92,
        IdleMotorFallbackTicks: int.MaxValue);
    private static readonly AvatarBodyStateProfile MazeBodyStateProfile = new(
        MaxForwardSpeed: MazeRunMaxForwardSpeed,
        MaxTurnRateDeg: 260.0,
        BaseIntensity: 0.22,
        MotionIntensityWeight: 0.95,
        TurnIntensityWeight: 0.40,
        ContactIntensityWeight: 0.55,
        BaseBurstCount: 6,
        MotionBurstWeight: 16.0,
        TurnBurstWeight: 10.0,
        ContactBurstWeight: 10.0);
    private static readonly (int Dr, int Dc)[] MazeCarveDirections =
    [
        (-2, 0),
        (2, 0),
        (0, -2),
        (0, 2)
    ];

    private readonly DispatcherTimer _renderTimer;
    private readonly DispatcherTimer _brainPollTimer;
    private readonly DispatcherTimer _navigationTimer;
    private readonly DispatcherTimer _visionTimer;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly HttpClient _httpClient;
    private readonly HttpClient _auditoryInputHttpClient;
    private readonly VisualInputDispatchClient _visualInputClient;

    // Pixel-vision dispatch: posts raw grayscale frames to NRE.Api (the brain engine
    // that hosts SetVisualFrame). Separate target from the ControlProgram endpoint
    // used for symbolic visual stimuli — drives the brain's V1 Gabor hierarchy +
    // retinotopic occipital injection with actual pixels. If NRE.Api isn't running
    // the post fails silently; the existing symbolic path continues regardless.
    private readonly HttpClient _pixelVisionHttpClient = NreHttpClientFactory.Create(
        NreHttpClientOptions.Default with { RequestTimeout = TimeSpan.FromMilliseconds(1500) });
    private readonly AvatarService _avatarService = new(
        MazeNervousSystemOptions,
        "NRE.Maze.AvatarService",
        new AvatarServiceClockOptions(Enabled: true, TickIntervalMs: 50, DriveDecayOverride: 0.86));
    private string _pixelVisionEndpoint = "http://localhost:5005";
    private bool _pixelVisionDispatchInFlight;
    private long _pixelVisionConsecutiveFailures;
    private long _lastPixelVisionFailureLogMs;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly JsonDocumentOptions _jsonDocOptions = new() { AllowTrailingCommas = true };
    private readonly Model3DGroup _sceneRoot = new();
    private readonly Queue<DateTime> _recentWallCollisionUtc = new();
    private readonly AxisAngleRotation3D _avatarYawRotation = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _avatarHeadYawRotation = new(new Vector3D(0, 1, 0), 0);
    private readonly TranslateTransform3D _avatarTranslate = new();
    private readonly List<string> _logLines = [];
    private readonly List<VisionSprite> _visionSprites = new(24);
    private readonly Dictionary<string, DateTime> _lastObjectStimulusUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _lastObjectStimulusSalience = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _mazeSeed;
    private readonly Random _mazeRandom;
    private readonly WriteableBitmap _visionBitmap;
    private readonly byte[] _visionPixels = new byte[VisionWidth * VisionHeight * 4];
    private readonly double[] _visionDepthBuffer = new double[VisionWidth];
    private readonly Point3D _cameraTarget = new(0.0, 0.78, 0.0);

    private int _mazeRows;
    private int _mazeCols;
    private char[][] _mazeLayout = [];
    private string _mazeLayoutFingerprint = "uninitialized";
    private Point _startWorld;
    private Point _goalWorld;
    private Point _respawnWorld;
    private readonly List<(int Row, int Col)> _foodCells = [];
    private readonly List<(int Row, int Col)> _hazardCells = [];
    private readonly List<(int Row, int Col)> _checkpointCells = [];
    private readonly List<WallBounds> _wallBounds = [];

    private readonly List<FoodEntity> _foodEntities = [];
    private readonly List<HazardEntity> _hazardEntities = [];
    private readonly List<CheckpointEntity> _checkpointEntities = [];
    private MaterialGroup? _checkpointInactiveMaterial;
    private MaterialGroup? _checkpointActiveMaterial;

    private double _avatarX;
    private double _avatarZ;
    private double _avatarHeadingDeg;
    private double _avatarHeadYawDeg;
    private double _lastForwardSpeed;
    private double _lastTurnRateDeg;

    private double _leftMotorDrive;
    private double _rightMotorDrive;
    private int _lastMotorDispatchCount;
    private long _lastTick;
    private long _dispatchSinceMs;
    private long _lastNeuronalMotorTick = -1;
    private bool _brainPollInFlight;
    private bool _navigationPollInFlight;
    private bool _connectedOnce;
    private string _lastConnectionMessage = string.Empty;
    private string _lastVisionInputStatus = string.Empty;
    private string _lastObjectMemoryText = string.Empty;
    private double _lastSimTimeSeconds;
    private DateTime _lastHazardDamageUtc = DateTime.MinValue;
    private DateTime _lastWallImpactUtc = DateTime.MinValue;
    private DateTime _lastEscapeReflexUtc = DateTime.MinValue;
    private DateTime _lastProgressUtc = DateTime.UtcNow;
    private bool _collisionStimulusInFlight;
    private bool _bodyStimulusInFlight;
    private bool _outcomeStimulusInFlight;
    private bool _environmentAudioInFlight;
    private bool _englishCommandInFlight;
    private bool _visionTickInFlight;
    private readonly AvatarRetryBackoff _environmentAudioBackoff = new(maxStreak: 8, maxExponent: 7, baseDelayMs: 1000);
    private long _lastBodyStateDispatchMs;
    private long _lastOutcomeDispatchMs;
    private long _lastEnvironmentAudioDispatchMs;
    private long _lastBrainNarrationSequence = -1;
    private string _lastBrainNarrationText = string.Empty;
    private PerspectiveCamera? _mazeCamera;
    private bool _cameraDragActive;
    private Point _cameraDragStart;
    private double _cameraYawDeg;
    private double _cameraPitchDeg = -31.0;
    private double _cameraDistance;

    private int _score;
    private int _health = 100;
    private int _foodsCollected;
    private int _hazardContacts;
    private int _wallImpacts;
    private int _consecutiveWallContacts;
    private int _stuckRecoveries;
    private int _hardUnstuckEvents;
    private double _lastWallProximity;
    private double _lastFrontProximity;
    private double _lastLeftProximity;
    private double _lastRightProximity;
    private string? _collisionEscapeHemisphere;
    private int _checkpointActivations;
    private string _lastMazeEvent = "-";
    private string _limbicStage = "unknown";
    private double _limbicSalience;
    private double _limbicThreat;
    private double _limbicInteroceptiveDrive;
    private double _limbicAversiveDrive;
    private double _limbicHippocampalContext;
    private double _limbicValence;
    private double _limbicRewardPredictionError;
    private double _limbicDopamine;
    private double _limbicNorepinephrine;
    private readonly Queue<long> _recentWallImpactTicks = new();
    private readonly Queue<LearningSample> _learningSamples = new();
    private readonly string _navigationSessionId = $"wpf-maze-{Environment.ProcessId}-{Guid.NewGuid():N}";
    private double _totalDistanceTravelled;
    private double _bestDistanceToGoal = double.MaxValue;
    private long _lastLearningSampleTick = -1;
    private bool _navigationResetPending = true;
    private string _lastNavigationDirective = "motor_stop";

    public MainWindow()
    {
        _mazeSeed = ResolveMazeSeed();
        _mazeRandom = new Random(_mazeSeed);
        InitializeComponent();
        ApplyConfiguredEndpointSelection();

        _visionBitmap = new WriteableBitmap(VisionWidth, VisionHeight, 96, 96, PixelFormats.Bgra32, null);
        VisionPreviewImage.Source = _visionBitmap;

        // Maze polls more aggressively than the editor; use a finite request timeout
        // (~1.5s) but otherwise share the standard pooling/connect tuning.
        _httpClient = NreHttpClientFactory.Create(
            NreHttpClientOptions.Default with { RequestTimeout = TimeSpan.FromMilliseconds(1500) });
        _auditoryInputHttpClient = NreHttpClientFactory.Create(
            NreHttpClientOptions.Default with { RequestTimeout = TimeSpan.FromMilliseconds(1800) });
        _visualInputClient = new VisualInputDispatchClient(_httpClient);

        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _renderTimer.Tick += RenderTimer_OnTick;

        _brainPollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(110)
        };
        _brainPollTimer.Tick += BrainPollTimer_OnTick;

        _navigationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _navigationTimer.Tick += NavigationTimer_OnTick;

        _visionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _visionTimer.Tick += VisionTimer_OnTick;

        EscapeAggressivenessSlider.Value = EscapeAggressivenessDefault;
        EscapeCooldownSlider.Value = EscapeCooldownDefaultMs;

        ForwardGainSlider.ValueChanged += GainSlider_OnValueChanged;
        TurnGainSlider.ValueChanged += GainSlider_OnValueChanged;
        DriveSmoothingSlider.ValueChanged += GainSlider_OnValueChanged;
        EscapeAggressivenessSlider.ValueChanged += GainSlider_OnValueChanged;
        EscapeCooldownSlider.ValueChanged += GainSlider_OnValueChanged;

        Loaded += MainWindow_OnLoaded;
        Closed += MainWindow_OnClosed;
        MazeViewport.MouseDown += MazeViewport_OnMouseDown;
        MazeViewport.MouseMove += MazeViewport_OnMouseMove;
        MazeViewport.MouseUp += MazeViewport_OnMouseUp;
        MazeViewport.MouseWheel += MazeViewport_OnMouseWheel;

        Focusable = true;

        GenerateMazeLayoutAndEntityCells();
        _mazeLayoutFingerprint = ComputeMazeLayoutFingerprint();
        BuildMazeMetadata();
        ResetRun(logMessage: false);
        UpdateGainLabels();
        UpdateHud();
        VisionSignalText.Text = "Vision signal: preview ready";
        VisionInputStatusText.Text = "Visual input: waiting for first frame";
        ObjectMemoryTextBox.Text = "Object memory: waiting for first frame";
        LimbicStageText.Text = "Limbic stage: awaiting telemetry";
        LimbicDriveText.Text = "Limbic drives: waiting for Control Program state.";
        BrainNarrationText.Text = "Brain narration: waiting for brain state.";
        EnglishCommandStatusText.Text = "Command: idle";
        NavigationStatusText.Text = "Navigation: waiting for Control Program";
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        _renderTimer.Start();
        _brainPollTimer.Start();
        _navigationTimer.Start();
        _visionTimer.Start();
        SetConnectionStatus(AvatarControlStatusText.Connecting(), Brushes.LightGoldenrodYellow, logOnChange: false);
        Log("Maze simulator ready. Waiting for motor pathway spikes from Control Program dispatch stream.");
        Log($"Qualification world: seed {_mazeSeed}; layout {_mazeLayoutFingerprint}.");
        Log("Brain-drive only: movement follows motor pathway spikes.");
        Log("Mouse controls: drag in viewport to rotate, mouse wheel to zoom.");
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _shutdown.Cancel();
        _renderTimer.Stop();
        _brainPollTimer.Stop();
        _navigationTimer.Stop();
        _visionTimer.Stop();
        _avatarService.Dispose();
        _auditoryInputHttpClient.Dispose();
        _httpClient.Dispose();
        _pixelVisionHttpClient.Dispose();
        _shutdown.Dispose();
    }

    private void MazeViewport_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _cameraDragActive = true;
        _cameraDragStart = e.GetPosition(MazeViewport);
        MazeViewport.CaptureMouse();
        e.Handled = true;
    }

    private void MazeViewport_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_cameraDragActive || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(MazeViewport);
        var dx = position.X - _cameraDragStart.X;
        var dy = position.Y - _cameraDragStart.Y;
        _cameraDragStart = position;

        _cameraYawDeg += dx * 0.28;
        _cameraPitchDeg = Math.Clamp(_cameraPitchDeg - (dy * 0.20), CameraMinPitchDeg, CameraMaxPitchDeg);
        UpdateMazeCameraPose();
    }

    private void MazeViewport_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_cameraDragActive || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _cameraDragActive = false;
        MazeViewport.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void MazeViewport_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_cameraDistance <= 0)
        {
            return;
        }

        var baseDistance = Math.Max(_mazeRows, _mazeCols);
        var minDistance = Math.Max(3.4, baseDistance * 0.32);
        var maxDistance = Math.Max(14.0, baseDistance * 3.6);
        var factor = e.Delta > 0 ? 0.90 : 1.11;

        _cameraDistance = Math.Clamp(_cameraDistance * factor, minDistance, maxDistance);
        UpdateMazeCameraPose();
        e.Handled = true;
    }

    private void GainSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateGainLabels();
        if (ReferenceEquals(sender, DriveSmoothingSlider))
        {
            _avatarService.SetClockDriveDecayOverride(DriveSmoothingSlider.Value);
        }
    }

    private void UpdateGainLabels()
    {
        ForwardGainText.Text = ForwardGainSlider.Value.ToString("0.00", CultureInfo.InvariantCulture);
        TurnGainText.Text = TurnGainSlider.Value.ToString("0.00", CultureInfo.InvariantCulture);
        DriveSmoothingText.Text = DriveSmoothingSlider.Value.ToString("0.00", CultureInfo.InvariantCulture);
        EscapeAggressivenessText.Text = $"{EscapeAggressivenessSlider.Value:0.00}x";
        EscapeCooldownText.Text = $"{EscapeCooldownSlider.Value:0} ms";
    }

    private double GetEscapeAggressiveness() =>
        Math.Clamp(EscapeAggressivenessSlider.Value, 0.50, 2.50);

    private TimeSpan GetEscapeReflexCooldown() =>
        TimeSpan.FromMilliseconds(Math.Clamp(EscapeCooldownSlider.Value, 250.0, 3000.0));

    private void RenderTimer_OnTick(object? sender, EventArgs e)
    {
        var now = _stopwatch.Elapsed.TotalSeconds;
        if (_lastSimTimeSeconds <= 0)
        {
            _lastSimTimeSeconds = now;
            return;
        }

        var dt = Math.Clamp(now - _lastSimTimeSeconds, 0.001, 0.08);
        _lastSimTimeSeconds = now;

        UpdateAvatar(dt);
        _ = FireAndForgetAsync(DispatchBodyStateInputAsync(_shutdown.Token), "body state dispatch");
        _ = FireAndForgetAsync(DispatchEnvironmentAudioInputAsync(_shutdown.Token), "environment audio dispatch");
        UpdateHud();
    }

    private async Task FireAndForgetAsync(Task task, string description)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log($"Background task failed ({description}): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async void BrainPollTimer_OnTick(object? sender, EventArgs e)
    {
        if (_brainPollInFlight || _shutdown.IsCancellationRequested)
        {
            return;
        }

        _brainPollInFlight = true;
        try
        {
            await PollBrainFrameAsync(_shutdown.Token);
        }
        finally
        {
            _brainPollInFlight = false;
        }
    }

    private async void NavigationTimer_OnTick(object? sender, EventArgs e)
    {
        if (_navigationPollInFlight ||
            _shutdown.IsCancellationRequested ||
            SpatialNavigationCheckBox.IsChecked != true)
        {
            return;
        }

        var endpoint = ResolveEndpointUri();
        if (endpoint is null)
        {
            StopSpatialNavigation("Navigation: invalid Control Program endpoint");
            return;
        }

        _navigationPollInFlight = true;
        try
        {
            await PollSpatialNavigationAsync(endpoint, _shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            // No-op on shutdown or request timeout.
        }
        catch (Exception ex)
        {
            StopSpatialNavigation($"Navigation: {ex.GetType().Name}");
        }
        finally
        {
            _navigationPollInFlight = false;
        }
    }

    private async void VisionTimer_OnTick(object? sender, EventArgs e)
    {
        if (_visionTickInFlight || _shutdown.IsCancellationRequested)
        {
            return;
        }

        _visionTickInFlight = true;
        try
        {
            await UpdateAvatarVisionAsync(_shutdown.Token);
        }
        finally
        {
            _visionTickInFlight = false;
        }
    }

    private async Task PollBrainFrameAsync(CancellationToken token)
    {
        var endpoint = ResolveEndpointUri();
        if (endpoint is null)
        {
            SetConnectionStatus(AvatarControlStatusText.InvalidEndpoint(), Brushes.OrangeRed);
            return;
        }

        try
        {
            var frame = await AvatarControlApi.GetJsonAsync(
                _httpClient,
                endpoint,
                AvatarControlApi.GetFramePath(_dispatchSinceMs),
                token);
            using var doc = frame.Document;
            if (!frame.IsSuccessStatusCode || doc is null)
            {
                SetConnectionStatus(AvatarControlStatusText.FramePollFailed(frame.StatusCode), Brushes.OrangeRed);
                return;
            }

            var root = doc.RootElement;
            var brainState = default(JsonElement);
            if (TryGetProperty(root, "state", out var stateElement) && stateElement.ValueKind == JsonValueKind.Object)
            {
                brainState = stateElement;
                _lastTick = GetLong(stateElement, "tick");
                UpdateLimbicFromState(stateElement);
                UpdateObjectMemoryFromState(stateElement);
                UpdateBrainNarrationFromState(stateElement);
            }
            else
            {
                SetObjectMemoryText("Object memory unavailable: /api/v1/frame missing state payload.");
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
            if (SpatialNavigationCheckBox.IsChecked != true)
            {
                ApplyMotorDispatch(dispatches);
            }
            SetConnectionStatus(AvatarControlStatusText.ConnectedWithMotorEvents(_lastTick, _lastMotorDispatchCount), Brushes.LightGreen, logOnChange: false);
            if (!_connectedOnce)
            {
                _connectedOnce = true;
                Log($"Connected to {endpoint}. Motor drive now follows M1/SMA dispatch spikes.");
            }
        }
        catch (OperationCanceledException)
        {
            // No-op on shutdown/timeouts.
        }
        catch (Exception ex)
        {
            SetConnectionStatus(AvatarControlStatusText.PollError(ex.GetType().Name), Brushes.OrangeRed);
        }
    }

    private async Task UpdateAvatarVisionAsync(CancellationToken token)
    {
        var analysis = RenderAvatarVisionFrame();
        VisionSignalText.Text = $"Vision signal: i={analysis.Intensity:0.00} b={analysis.BurstCount} L={analysis.LeftSaliency:0.00} R={analysis.RightSaliency:0.00} m={analysis.MotionSignal:0.00} p={analysis.Pattern}";

        // Pixel-vision dispatch (in addition to the symbolic path below). Fires
        // off the same rendered frame but uses a separate in-flight flag + HTTP
        // client so a slow/down NRE.Api can't block the symbolic ControlProgram
        // dispatch. The two channels are independent.
        if (PixelVisionCheckBox?.IsChecked == true
            && !_pixelVisionDispatchInFlight
            && !string.IsNullOrWhiteSpace(_pixelVisionEndpoint))
        {
            _pixelVisionDispatchInFlight = true;
            _ = DispatchAvatarVisionPixelsAsync();
        }

        if (VisionInputCheckBox.IsChecked != true)
        {
            SetVisionInputStatus("Visual input: disabled");
            return;
        }

        var endpoint = ResolveEndpointUri();
        if (endpoint is null)
        {
            SetVisionInputStatus("Visual input: invalid endpoint URI");
            return;
        }

        try
        {
            var visualDispatch = await SendAvatarVisualStimulusAsync(endpoint, analysis, token);
            var objectDispatch = await SendAvatarObjectStimuliAsync(endpoint, token);
            SetVisionInputStatus(
                $"Visual input: gen={visualDispatch.Generated} del={visualDispatch.Delivered} tgt={visualDispatch.TargetCount} | " +
                $"obj={objectDispatch.Delivered}/{objectDispatch.Generated} vis={objectDispatch.VisibleObjects} err={objectDispatch.Errors}");
        }
        catch (OperationCanceledException)
        {
            // noop during shutdown/timeout.
        }
        catch (Exception ex)
        {
            SetVisionInputStatus($"Visual input: {ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Convert the vision preview (BGRA32) to 8-bit grayscale and POST it to
    /// NRE.Api's <c>/api/engine/visual-frame</c>. The brain's V1 Gabor hierarchy
    /// + retinotopic occipital injection consumes the raw bytes — true pixel
    /// vision rather than the symbolic intensity/saliency dispatch.
    /// </summary>
    private async Task DispatchAvatarVisionPixelsAsync()
    {
        try
        {
            int w = VisionWidth, h = VisionHeight;
            var gray = new byte[w * h];
            AvatarPixelVision.ConvertBgra32ToGrayscale(_visionPixels, w, h, gray);

            var uri = new Uri(new Uri(_pixelVisionEndpoint), $"/api/engine/visual-frame?w={w}&h={h}");
            using var content = new ByteArrayContent(gray);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            using var resp = await _pixelVisionHttpClient.PostAsync(uri, content, _shutdown.Token);
            if (resp.IsSuccessStatusCode)
            {
                if (Interlocked.Read(ref _pixelVisionConsecutiveFailures) > 0)
                {
                    Log($"Pixel-vision dispatch recovered ({_pixelVisionEndpoint}).");
                    Interlocked.Exchange(ref _pixelVisionConsecutiveFailures, 0);
                }
            }
            else
            {
                RegisterPixelVisionFailure($"HTTP {(int)resp.StatusCode}");
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — silent.
        }
        catch (Exception ex)
        {
            RegisterPixelVisionFailure($"{ex.GetType().Name}");
        }
        finally
        {
            _pixelVisionDispatchInFlight = false;
        }
    }

    private void PixelVisionCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        // Suppress Log() during XAML init: the Checked event fires from the
        // XAML parser before the log target exists, which would NRE. Only log
        // once the window has finished loading.
        if (IsLoaded) Log($"Pixel vision enabled (target {_pixelVisionEndpoint}).");
    }

    private void PixelVisionCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Log("Pixel vision disabled.");
    }

    private void PixelVisionEndpointTextBox_OnLostFocus(object sender, RoutedEventArgs e)
        => CommitPixelVisionEndpoint();

    private void PixelVisionEndpointTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            CommitPixelVisionEndpoint();
            e.Handled = true;
        }
    }

    private void CommitPixelVisionEndpoint()
    {
        if (PixelVisionEndpointTextBox is null) return;
        var raw = (PixelVisionEndpointTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            PixelVisionEndpointTextBox.Text = _pixelVisionEndpoint;
            return;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            if (IsLoaded) Log($"Pixel vision endpoint rejected (must be http/https): {raw}");
            PixelVisionEndpointTextBox.Text = _pixelVisionEndpoint;
            return;
        }

        if (_pixelVisionEndpoint == parsed.AbsoluteUri) return;
        _pixelVisionEndpoint = parsed.AbsoluteUri;
        Interlocked.Exchange(ref _pixelVisionConsecutiveFailures, 0);
        if (IsLoaded) Log($"Pixel vision endpoint set: {_pixelVisionEndpoint}");
    }

    private void RegisterPixelVisionFailure(string reason)
    {
        var failures = Interlocked.Increment(ref _pixelVisionConsecutiveFailures);
        // First failure + every 16th thereafter, max once per 5 s. Keeps the log
        // readable when NRE.Api is offline.
        var nowMs = Environment.TickCount64;
        if (failures == 1 || (failures % 16 == 0 && (nowMs - Interlocked.Read(ref _lastPixelVisionFailureLogMs)) >= 5000))
        {
            Interlocked.Exchange(ref _lastPixelVisionFailureLogMs, nowMs);
            Log($"Pixel-vision dispatch warning ({_pixelVisionEndpoint}): {reason} (failures={failures}). NRE.Api may be offline.");
        }
    }

    private AvatarVisualSignal RenderAvatarVisionFrame()
    {
        var headingRad = GetAvatarLookHeadingRad();
        var fovRad = VisionFovDegrees * Math.PI / 180.0;
        FillVisionBackground();
        RenderVisionWalls(headingRad, fovRad);

        DrawVisionSprites(headingRad, fovRad);
        var visualSignal = BuildWorldBasedAvatarVisionSignal();

        DrawVisionDebugOverlay(visualSignal.LeftSaliency, visualSignal.RightSaliency, visualSignal.MotionSignal);

        _visionBitmap.WritePixels(VisionBitmapRect, _visionPixels, VisionWidth * 4, 0);

        return visualSignal;
    }

    private AvatarVisualSignal BuildWorldBasedAvatarVisionSignal()
    {
        var headingRad = GetAvatarLookHeadingRad();
        var fovRad = VisionFovDegrees * Math.PI / 180.0;
        const int rayCount = 32;

        var leftSum = 0.0;
        var rightSum = 0.0;
        var allSum = 0.0;
        var leftCount = 0;
        var rightCount = 0;
        for (var i = 0; i < rayCount; i++)
        {
            var t = (i + 0.5) / rayCount;
            var angle = headingRad + ((t - 0.5) * fovRad);
            var ray = TraceVisionRay(Math.Sin(angle), Math.Cos(angle), VisionRange);
            var saliency = ray.HitWall
                ? Math.Clamp(0.18 + ((1.0 - (ray.Distance / VisionRange)) * 0.62), 0.08, 0.82)
                : 0.04;

            allSum += saliency;
            if (i < rayCount / 2)
            {
                leftSum += saliency;
                leftCount++;
            }
            else
            {
                rightSum += saliency;
                rightCount++;
            }
        }

        var visibleObjects = BuildVisibleObjectObservations();
        foreach (var observation in visibleObjects)
        {
            var objectBoost = Math.Clamp(observation.Salience * 0.72, 0.0, 1.0);
            allSum += objectBoost;
            if (string.Equals(observation.Hemisphere, "M", StringComparison.OrdinalIgnoreCase))
            {
                leftSum += objectBoost * 0.5;
                rightSum += objectBoost * 0.5;
                leftCount++;
                rightCount++;
            }
            else if (string.Equals(observation.Hemisphere, "L", StringComparison.OrdinalIgnoreCase))
            {
                rightSum += objectBoost;
                rightCount++;
            }
            else
            {
                leftSum += objectBoost;
                leftCount++;
            }
        }

        var motionSignal = Math.Clamp((Math.Abs(_lastForwardSpeed) / MazeRunMaxForwardSpeed) + (Math.Abs(_lastTurnRateDeg) / 280.0) + (Math.Abs(_avatarHeadYawDeg) / AvatarHeadMaxYawDeg * 0.22), 0.0, 1.0);
        var leftSaliency = Math.Clamp(leftSum / Math.Max(1, leftCount), 0.0, 1.0);
        var rightSaliency = Math.Clamp(rightSum / Math.Max(1, rightCount), 0.0, 1.0);
        var luminanceSignal = Math.Clamp(allSum / Math.Max(1, rayCount + visibleObjects.Count), 0.0, 1.0);
        var intensity = (float)Math.Clamp(0.15 + (0.55 * Math.Max(leftSaliency, rightSaliency)) + (0.30 * motionSignal), 0.05, 1.0);
        var burstCount = Math.Clamp((int)Math.Round(4 + (intensity * 20.0) + (motionSignal * 8.0)), 4, 40);

        return AvatarVisualSignalFactory.FromRenderedFrame(
            AvatarRuntimeDefaults.UnifiedVisualStreamPattern,
            intensity,
            burstCount,
            leftSaliency,
            rightSaliency,
            motionSignal,
            luminanceSignal);
    }

    private void FillVisionBackground()
    {
        var centerY = VisionHeight / 2;
        for (var y = 0; y < VisionHeight; y++)
        {
            var rowOffset = y * VisionWidth * 4;
            var isSky = y < centerY;
            var falloff = isSky
                ? (double)y / Math.Max(1, centerY)
                : (double)(y - centerY) / Math.Max(1, VisionHeight - centerY);

            var baseR = isSky ? (byte)(8 + (16 * (1.0 - falloff))) : (byte)(4 + (7 * falloff));
            var baseG = isSky ? (byte)(16 + (26 * (1.0 - falloff))) : (byte)(10 + (9 * falloff));
            var baseB = isSky ? (byte)(30 + (42 * (1.0 - falloff))) : (byte)(16 + (12 * falloff));

            for (var x = 0; x < VisionWidth; x++)
            {
                var idx = rowOffset + (x * 4);
                _visionPixels[idx] = baseB;
                _visionPixels[idx + 1] = baseG;
                _visionPixels[idx + 2] = baseR;
                _visionPixels[idx + 3] = 255;
            }
        }
    }

    private void RenderVisionWalls(double headingRad, double fovRad)
    {
        var centerY = VisionHeight / 2;
        for (var x = 0; x < VisionWidth; x++)
        {
            var t = x / (double)(VisionWidth - 1);
            var angle = headingRad + ((t - 0.5) * fovRad);
            var dirX = Math.Sin(angle);
            var dirZ = Math.Cos(angle);
            if (Math.Abs(dirX) < 1e-6 && Math.Abs(dirZ) < 1e-6)
            {
                dirZ = 1e-6;
            }

            var hit = TraceVisionRay(dirX, dirZ, VisionRange);
            _visionDepthBuffer[x] = hit.Distance;

            if (!hit.HitWall)
            {
                continue;
            }

            RenderVisionWallColumn(x, centerY, hit.Distance);
        }
    }

    private void RenderVisionWallColumn(int x, int centerY, double hitDistance)
    {
        var projectedHeight = (int)Math.Clamp((VisionHeight * 0.95) / Math.Max(0.2, hitDistance), 8, VisionHeight - 2);
        var top = Math.Max(0, centerY - (projectedHeight / 2));
        var bottom = Math.Min(VisionHeight - 1, centerY + (projectedHeight / 2));
        var shade = Math.Clamp(1.0 - (hitDistance / VisionRange), 0.16, 1.0);
        var blue = (byte)(52 * shade);
        var green = (byte)(84 * shade);
        var red = (byte)(146 * shade);

        for (var y = top; y <= bottom; y++)
        {
            var idx = (y * VisionWidth * 4) + (x * 4);
            _visionPixels[idx] = blue;
            _visionPixels[idx + 1] = green;
            _visionPixels[idx + 2] = red;
            _visionPixels[idx + 3] = 255;
        }
    }

    private void DrawVisionSprites(double headingRad, double fovRad)
    {
        var sprites = _visionSprites;
        sprites.Clear();
        sprites.Add(new VisionSprite(_goalWorld, Color.FromRgb(74, 222, 128), 1.0));

        for (var i = 0; i < _foodEntities.Count; i++)
        {
            var food = _foodEntities[i];
            if (!food.Collected)
            {
                sprites.Add(new VisionSprite(food.World, Color.FromRgb(251, 191, 36), 0.62));
            }
        }

        for (var i = 0; i < _hazardEntities.Count; i++)
        {
            sprites.Add(new VisionSprite(_hazardEntities[i].World, Color.FromRgb(239, 68, 68), 0.78));
        }

        for (var i = 0; i < _checkpointEntities.Count; i++)
        {
            var cp = _checkpointEntities[i];
            var color = cp.Activated ? Color.FromRgb(52, 211, 153) : Color.FromRgb(245, 158, 11);
            sprites.Add(new VisionSprite(cp.World, color, 0.54));
        }

        sprites.Sort(static (a, b) => b.World.Y.CompareTo(a.World.Y));

        for (var i = 0; i < sprites.Count; i++)
        {
            var sprite = sprites[i];
            var dx = sprite.World.X - _avatarX;
            var dz = sprite.World.Y - _avatarZ;
            var distance = Math.Sqrt((dx * dx) + (dz * dz));
            if (distance <= 0.16 || distance > VisionRange)
            {
                continue;
            }

            var objectAngle = Math.Atan2(dx, dz);
            var delta = NormalizeRadians(objectAngle - headingRad);
            if (Math.Abs(delta) > (fovRad * 0.58))
            {
                continue;
            }

            var screenX = (int)Math.Round(((delta / fovRad) + 0.5) * VisionWidth);
            var spriteHeight = (int)Math.Clamp((VisionHeight * sprite.SizeScale) / Math.Max(0.28, distance), 6, VisionHeight / 2);
            var spriteWidth = Math.Max(4, spriteHeight);
            var top = (VisionHeight / 2) - (spriteHeight / 2);
            var left = screenX - (spriteWidth / 2);
            var right = screenX + (spriteWidth / 2);
            var attenuation = Math.Clamp(1.0 - (distance / VisionRange), 0.28, 1.0);
            var halfSpriteWidth = Math.Max(1, spriteWidth / 2);
            var halfSpriteHeight = Math.Max(1, spriteHeight / 2);

            for (var x = Math.Max(0, left); x < Math.Min(VisionWidth, right); x++)
            {
                if (distance > (_visionDepthBuffer[x] - 0.05))
                {
                    continue;
                }

                var nx = (x - screenX) / (double)halfSpriteWidth;
                for (var y = Math.Max(0, top); y < Math.Min(VisionHeight, top + spriteHeight); y++)
                {
                    var ny = (y - (top + (spriteHeight / 2.0))) / halfSpriteHeight;
                    if ((nx * nx) + (ny * ny) > 1.0)
                    {
                        continue;
                    }

                    BlendVisionPixel(x, y, sprite.Color, attenuation);
                }
            }
        }
    }

    private void DrawVisionDebugOverlay(double leftSaliency, double rightSaliency, double motionSignal)
    {
        DrawVisionRectOutline(0, 0, VisionWidth - 1, VisionHeight - 1, Color.FromRgb(48, 91, 166), 0.42);

        var centerX = VisionWidth / 2;
        for (var y = 0; y < VisionHeight; y++)
        {
            BlendVisionPixel(centerX, y, Color.FromRgb(198, 224, 255), 0.25);
        }

        var barTop = 6;
        var barHeight = 6;
        var half = VisionWidth / 2;
        var leftLength = (int)Math.Round((half - 14) * Math.Clamp(leftSaliency, 0.0, 1.0));
        var rightLength = (int)Math.Round((half - 14) * Math.Clamp(rightSaliency, 0.0, 1.0));

        for (var x = 8; x < 8 + leftLength; x++)
        {
            for (var y = barTop; y < barTop + barHeight; y++)
            {
                BlendVisionPixel(x, y, Color.FromRgb(74, 222, 128), 0.75);
            }
        }

        for (var x = half + 6; x < half + 6 + rightLength; x++)
        {
            for (var y = barTop; y < barTop + barHeight; y++)
            {
                BlendVisionPixel(x, y, Color.FromRgb(96, 165, 250), 0.75);
            }
        }

        var focusLeft = leftSaliency >= rightSaliency;
        var focusMagnitude = Math.Abs(leftSaliency - rightSaliency);
        var boxW = 62;
        var boxH = 62;
        var boxY = (VisionHeight / 2) - (boxH / 2);
        var boxX = focusLeft
            ? (VisionWidth / 4) - (boxW / 2)
            : ((VisionWidth * 3) / 4) - (boxW / 2);
        var boxAlpha = Math.Clamp(0.22 + (focusMagnitude * 0.60), 0.22, 0.88);
        DrawVisionRectOutline(boxX, boxY, boxX + boxW, boxY + boxH, Color.FromRgb(196, 231, 255), boxAlpha);

        var motionBarWidth = (int)Math.Round((VisionWidth - 16) * Math.Clamp(motionSignal, 0.0, 1.0));
        var motionY = VisionHeight - 9;
        for (var x = 8; x < 8 + motionBarWidth; x++)
        {
            BlendVisionPixel(x, motionY, Color.FromRgb(252, 211, 77), 0.82);
            BlendVisionPixel(x, motionY + 1, Color.FromRgb(252, 211, 77), 0.82);
        }
    }

    private void DrawVisionRectOutline(int x0, int y0, int x1, int y1, Color color, double alpha)
    {
        var clampedX0 = Math.Clamp(x0, 0, VisionWidth - 1);
        var clampedY0 = Math.Clamp(y0, 0, VisionHeight - 1);
        var clampedX1 = Math.Clamp(x1, 0, VisionWidth - 1);
        var clampedY1 = Math.Clamp(y1, 0, VisionHeight - 1);
        if (clampedX1 <= clampedX0 || clampedY1 <= clampedY0)
        {
            return;
        }

        for (var x = clampedX0; x <= clampedX1; x++)
        {
            BlendVisionPixel(x, clampedY0, color, alpha);
            BlendVisionPixel(x, clampedY1, color, alpha);
        }

        for (var y = clampedY0; y <= clampedY1; y++)
        {
            BlendVisionPixel(clampedX0, y, color, alpha);
            BlendVisionPixel(clampedX1, y, color, alpha);
        }
    }

    private RayHit TraceVisionRay(double dirX, double dirZ, double maxDistance)
    {
        return TraceVisionRay(_avatarX, _avatarZ, dirX, dirZ, maxDistance);
    }

    private RayHit TraceVisionRay(double startX, double startZ, double dirX, double dirZ, double maxDistance)
    {
        var step = Math.Clamp(CellSize * 0.08, 0.05, 0.16);
        var x = startX;
        var z = startZ;

        for (double distance = step; distance <= maxDistance; distance += step)
        {
            x += dirX * step;
            z += dirZ * step;
            if (PointIntersectsWall(x, z))
            {
                return new RayHit(true, distance);
            }
        }

        return new RayHit(false, maxDistance);
    }

    private void BlendVisionPixel(int x, int y, Color color, double alpha)
    {
        var idx = (y * VisionWidth * 4) + (x * 4);
        var a = Math.Clamp(alpha, 0.0, 1.0);

        _visionPixels[idx] = (byte)Math.Clamp((_visionPixels[idx] * (1.0 - a)) + (color.B * a), 0.0, 255.0);
        _visionPixels[idx + 1] = (byte)Math.Clamp((_visionPixels[idx + 1] * (1.0 - a)) + (color.G * a), 0.0, 255.0);
        _visionPixels[idx + 2] = (byte)Math.Clamp((_visionPixels[idx + 2] * (1.0 - a)) + (color.R * a), 0.0, 255.0);
        _visionPixels[idx + 3] = 255;
    }

    private async Task<VisualStimulusDispatchResult> SendAvatarVisualStimulusAsync(
        Uri baseUri,
        AvatarVisualSignal analysis,
        CancellationToken token)
    {
        var request = analysis.ToVisualInputRequest();

        var outcome = await _visualInputClient.DispatchWithHemisphereFallbackAsync(
            baseUri,
            request,
            ex => VisualInputDispatchClient.ShouldRetryHemisphereFallback(ex, "V1"),
            token);
        if (outcome.FallbackAttempted)
        {
            if (outcome.LeftResponse is not null &&
                outcome.RightResponse is null &&
                outcome.RightError is not null)
            {
                Log($"Avatar vision partial dispatch (R failed): {VisualInputDispatchClient.FormatHttpError(outcome.RightError, 80)}");
            }
            else if (outcome.RightResponse is not null &&
                     outcome.LeftResponse is null &&
                     outcome.LeftError is not null)
            {
                Log($"Avatar vision partial dispatch (L failed): {VisualInputDispatchClient.FormatHttpError(outcome.LeftError, 80)}");
            }
        }

        var response = outcome.Response;
        return new VisualStimulusDispatchResult(
            response.GeneratedSpikes,
            response.DeliveredSpikes,
            response.TargetInstances);
    }

    private async Task<ObjectStimulusDispatchResult> SendAvatarObjectStimuliAsync(Uri baseUri, CancellationToken token)
    {
        var visibleObjects = BuildVisibleObjectObservations();
        if (visibleObjects.Count == 0)
        {
            return new ObjectStimulusDispatchResult(0, 0, 0, 0);
        }

        _avatarService.PostObjectCandidates(visibleObjects.Select(ToAvatarObjectObservation), maxObservations: visibleObjects.Count);
        var avatarObservations = await DrainAvatarObjectObservationsAsync(visibleObjects.Count, token);
        if (avatarObservations.Count == 0)
        {
            return new ObjectStimulusDispatchResult(0, 0, visibleObjects.Count, 0);
        }

        var generated = 0;
        var delivered = 0;
        var errors = 0;
        var now = DateTime.UtcNow;

        foreach (var observation in avatarObservations)
        {
            if (!ShouldDispatchObjectObservation(observation, now))
            {
                continue;
            }

            var request = new
            {
                ObjectId = observation.ObjectId,
                Label = observation.Label,
                Salience = (float)Math.Clamp(observation.Salience, 0.05, 1.0),
                Confidence = (float)Math.Clamp(observation.Confidence, 0.05, 1.0),
                Intensity = (float)Math.Clamp(observation.Intensity, 0.2, 3.5),
                BurstCount = observation.BurstCount,
                Hemisphere = observation.Hemisphere,
                EncodeMemory = observation.EncodeMemory,
                InputSource = observation.InputSource
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    new Uri(baseUri, "/api/v1/admin/input/object"),
                    request,
                    token);
                var payload = await response.Content.ReadAsStringAsync(token);
                if (!response.IsSuccessStatusCode)
                {
                    errors++;
                    continue;
                }

                var localGenerated = 0;
                var localDelivered = 0;
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        localGenerated = GetInt(doc.RootElement, "generatedSpikes");
                        localDelivered = GetInt(doc.RootElement, "deliveredSpikes");
                    }
                }
                catch
                {
                    // best effort parse only
                }

                generated += localGenerated;
                delivered += localDelivered;
                _lastObjectStimulusUtc[observation.ObjectId] = now;
                _lastObjectStimulusSalience[observation.ObjectId] = observation.Salience;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                errors++;
            }
        }

        return new ObjectStimulusDispatchResult(generated, delivered, avatarObservations.Count, errors);
    }

    private async Task<List<AvatarObjectObservation>> DrainAvatarObjectObservationsAsync(int maxObservations, CancellationToken token)
    {
        var observations = new List<AvatarObjectObservation>(Math.Clamp(maxObservations, 1, 8));
        while (observations.Count < maxObservations && _avatarService.TryDequeueObjectObservation(out var observation))
        {
            observations.Add(observation);
        }

        if (observations.Count > 0)
        {
            return observations;
        }

        await Task.Delay(1, token);
        while (observations.Count < maxObservations && _avatarService.TryDequeueObjectObservation(out var observation))
        {
            observations.Add(observation);
        }

        return observations;
    }

    private static AvatarObjectObservation ToAvatarObjectObservation(ObjectObservation observation)
        => new(
            observation.ObjectId,
            observation.Label,
            observation.Salience,
            observation.Confidence,
            observation.Intensity,
            observation.BurstCount,
            observation.DistanceMeters,
            observation.Hemisphere,
            EncodeMemory: true);

    private List<ObjectObservation> BuildVisibleObjectObservations()
    {
        var observations = new List<ObjectObservation>(4);

        if (TryBuildObjectObservation("goal.main", "goal", _goalWorld, salienceBias: 0.26, out var goalObservation))
        {
            observations.Add(goalObservation);
        }

        if (TryBuildNearestFoodObservation(out var foodObservation))
        {
            observations.Add(foodObservation);
        }

        if (TryBuildNearestHazardObservation(out var hazardObservation))
        {
            observations.Add(hazardObservation);
        }

        if (TryBuildNearestCheckpointObservation(out var checkpointObservation))
        {
            observations.Add(checkpointObservation);
        }

        return observations;
    }

    private bool TryBuildNearestFoodObservation(out ObjectObservation observation)
    {
        observation = default;
        var found = false;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _foodEntities.Count; i++)
        {
            var entity = _foodEntities[i];
            if (entity.Collected)
            {
                continue;
            }

            if (!TryBuildObjectObservation($"food.{entity.Id}", "food", entity.World, salienceBias: 0.12, out var current))
            {
                continue;
            }

            if (current.DistanceMeters < bestDistance)
            {
                bestDistance = current.DistanceMeters;
                observation = current;
                found = true;
            }
        }

        return found;
    }

    private bool TryBuildNearestHazardObservation(out ObjectObservation observation)
    {
        observation = default;
        var found = false;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _hazardEntities.Count; i++)
        {
            var entity = _hazardEntities[i];
            if (!TryBuildObjectObservation($"hazard.{entity.Id}", "hazard", entity.World, salienceBias: 0.20, out var current))
            {
                continue;
            }

            if (current.DistanceMeters < bestDistance)
            {
                bestDistance = current.DistanceMeters;
                observation = current;
                found = true;
            }
        }

        return found;
    }

    private bool TryBuildNearestCheckpointObservation(out ObjectObservation observation)
    {
        observation = default;
        var found = false;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _checkpointEntities.Count; i++)
        {
            var entity = _checkpointEntities[i];
            if (entity.Activated)
            {
                continue;
            }

            if (!TryBuildObjectObservation($"checkpoint.{entity.Id}", "checkpoint", entity.World, salienceBias: 0.08, out var current))
            {
                continue;
            }

            if (current.DistanceMeters < bestDistance)
            {
                bestDistance = current.DistanceMeters;
                observation = current;
                found = true;
            }
        }

        return found;
    }

    private bool TryBuildObjectObservation(
        string objectId,
        string label,
        Point world,
        double salienceBias,
        out ObjectObservation observation)
    {
        observation = default;
        var headingRad = GetAvatarLookHeadingRad();
        var fovRad = VisionFovDegrees * Math.PI / 180.0;

        var dx = world.X - _avatarX;
        var dz = world.Y - _avatarZ;
        var distance = Math.Sqrt((dx * dx) + (dz * dz));
        if (distance <= 0.08 || distance > VisionRange)
        {
            return false;
        }

        var objectAngle = Math.Atan2(dx, dz);
        var delta = NormalizeRadians(objectAngle - headingRad);
        var fovLimit = fovRad * 0.58;
        if (Math.Abs(delta) > fovLimit)
        {
            return false;
        }

        var ray = TraceVisionRay(Math.Sin(objectAngle), Math.Cos(objectAngle), distance);
        if (ray.HitWall && ray.Distance < Math.Max(0.08, distance - 0.10))
        {
            return false;
        }

        var distanceNorm = Math.Clamp(1.0 - (distance / VisionRange), 0.0, 1.0);
        var centerBias = 1.0 - Math.Clamp(Math.Abs(delta) / Math.Max(0.001, fovLimit), 0.0, 1.0);
        var salience = Math.Clamp(0.34 + (0.50 * distanceNorm) + (0.22 * centerBias) + salienceBias, 0.05, 1.0);
        var confidence = Math.Clamp(0.48 + (0.44 * distanceNorm) + (0.14 * centerBias), 0.05, 1.0);
        var intensity = Math.Clamp(0.42 + (salience * 1.15) + (confidence * 0.65), 0.2, 3.5);
        var burstCount = Math.Clamp((int)Math.Round(7 + (salience * 18.0) + (confidence * 10.0)), 6, 48);

        observation = new ObjectObservation(
            ObjectId: objectId,
            Label: label,
            Hemisphere: ResolveObjectStimulusHemisphere(delta, fovLimit),
            Salience: salience,
            Confidence: confidence,
            Intensity: intensity,
            BurstCount: burstCount,
            DistanceMeters: distance);
        return true;
    }

    private bool ShouldDispatchObjectObservation(AvatarObjectObservation observation, DateTime now)
    {
        if (!_lastObjectStimulusUtc.TryGetValue(observation.ObjectId, out var lastUtc))
        {
            return true;
        }

        var elapsed = now - lastUtc;
        if (elapsed >= ObjectStimulusCooldown)
        {
            return true;
        }

        var previousSalience = _lastObjectStimulusSalience.TryGetValue(observation.ObjectId, out var salience)
            ? salience
            : 0.0;
        if (Math.Abs(previousSalience - observation.Salience) >= 0.15 && elapsed >= TimeSpan.FromMilliseconds(110))
        {
            return true;
        }

        return false;
    }

    private static string ResolveObjectStimulusHemisphere(double deltaRadians, double fovLimitRadians)
    {
        if (Math.Abs(deltaRadians) <= fovLimitRadians * 0.08)
        {
            return "M";
        }

        // Contralateral cortical mapping for visual field input.
        return deltaRadians >= 0.0 ? "L" : "R";
    }

    private void SetVisionInputStatus(string status)
    {
        VisionInputStatusText.Text = status;
        if (string.Equals(status, _lastVisionInputStatus, StringComparison.Ordinal))
        {
            return;
        }

        _lastVisionInputStatus = status;
    }

    private void SetObjectMemoryText(string text)
    {
        if (string.Equals(text, _lastObjectMemoryText, StringComparison.Ordinal))
        {
            return;
        }

        _lastObjectMemoryText = text;
        ObjectMemoryTextBox.Text = text;
        ObjectMemoryTextBox.CaretIndex = 0;
    }

    private void UpdateObjectMemoryFromState(JsonElement stateElement)
    {
        SetObjectMemoryText(FormatObjectMemoryState(stateElement));
    }

    private static string FormatObjectMemoryState(JsonElement stateRoot)
    {
        if (!TryGetProperty(stateRoot, "objectMemory", out var objectMemory) || objectMemory.ValueKind != JsonValueKind.Object)
        {
            return "Object memory unavailable: state payload missing objectMemory.";
        }

        var tick = GetLong(stateRoot, "tick");
        var simMs = GetDouble(stateRoot, "simulationClockMs");
        var count = GetInt(objectMemory, "count");
        var topList = TryGetProperty(objectMemory, "top", out var top) && top.ValueKind == JsonValueKind.Array
            ? top
            : default;

        var lines = new List<string>(32)
        {
            $"Tick: {tick}",
            $"Simulation ms: {simMs:0.0}",
            $"Object traces: {count}",
            string.Empty,
            "Most recent objects:"
        };

        if (topList.ValueKind != JsonValueKind.Array || topList.GetArrayLength() == 0)
        {
            lines.Add("  -");
            return string.Join(Environment.NewLine, lines);
        }

        var index = 1;
        foreach (var item in topList.EnumerateArray().Take(8))
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var objectId = GetString(item, "objectId");
            var label = GetString(item, "label");
            var hemisphere = GetString(item, "dominantHemisphere");
            var familiarity = GetDouble(item, "familiarity");
            var seenCount = GetInt(item, "seenCount");
            var salienceEma = GetDouble(item, "salienceEma");

            lines.Add($"{index,2}. {label} [{objectId}] hemi={hemisphere} fam={familiarity:0.000} seen={seenCount} sal={salienceEma:0.000}");
            index++;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private List<AvatarDispatchSpike> ParseDispatchSpikes(JsonElement root, out long maxWallClockMs)
    {
        return AvatarDispatchSpikeParser.ParseDispatchSpikes(root, _dispatchSinceMs, out maxWallClockMs);
    }

    private void ApplyMotorDispatch(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        var body = new AvatarNervousSystemBodyState(
            _limbicInteroceptiveDrive,
            _limbicThreat,
            _health / 100.0,
            (DateTime.UtcNow - _lastProgressUtc).TotalSeconds,
            NoProgressRecoveryTimeout.TotalSeconds);
        _avatarService.PostBrainSignals(dispatches, body);
    }

    private void ApplyNervousSystemSignal(AvatarNervousSystemSignal signal)
    {
        _leftMotorDrive = signal.LeftMotorDrive;
        _rightMotorDrive = signal.RightMotorDrive;
        _lastMotorDispatchCount = signal.MotorEvents;
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
            _lastMotorDispatchCount = signal.MotorEvents;
        }
    }

    private void UpdateLimbicFromState(JsonElement stateElement)
    {
        if (!TryGetProperty(stateElement, "limbicState", out var limbic) || limbic.ValueKind != JsonValueKind.Object)
        {
            _limbicStage = "unknown";
            _limbicSalience = 0.0;
            _limbicThreat = 0.0;
            _limbicInteroceptiveDrive = 0.0;
            _limbicAversiveDrive = 0.0;
            _limbicHippocampalContext = 0.0;
            _limbicValence = 0.0;
            _limbicRewardPredictionError = 0.0;
            _limbicDopamine = 0.0;
            _limbicNorepinephrine = 0.0;
            return;
        }

        _limbicStage = GetString(limbic, "stage");
        if (string.IsNullOrWhiteSpace(_limbicStage))
        {
            _limbicStage = "unknown";
        }

        _limbicSalience = Math.Clamp(GetDouble(limbic, "salience"), 0.0, 1.0);
        _limbicThreat = Math.Clamp(GetDouble(limbic, "threat"), 0.0, 1.0);
        _limbicInteroceptiveDrive = Math.Clamp(GetDouble(limbic, "interoceptiveDrive"), 0.0, 1.0);
        _limbicAversiveDrive = Math.Clamp(GetDouble(limbic, "aversiveDrive"), 0.0, 1.0);
        _limbicHippocampalContext = Math.Clamp(GetDouble(limbic, "hippocampalContext"), 0.0, 1.0);
        _limbicValence = Math.Clamp(GetDouble(limbic, "valence"), -1.0, 1.0);
        _limbicRewardPredictionError = Math.Clamp(GetDouble(limbic, "rewardPredictionError"), -1.0, 1.0);

        if (TryGetProperty(stateElement, "globalNeuromodState", out var neuromod) && neuromod.ValueKind == JsonValueKind.Object)
        {
            _limbicDopamine = Math.Clamp(GetDouble(neuromod, "dopamineLevel"), 0.0, 1.0);
            _limbicNorepinephrine = Math.Clamp(GetDouble(neuromod, "norepinephrineLevel"), 0.0, 1.0);
            return;
        }

        _limbicDopamine = Math.Clamp(GetDouble(limbic, "dopamineTarget"), 0.0, 1.0);
        _limbicNorepinephrine = Math.Clamp(GetDouble(limbic, "norepinephrineTarget"), 0.0, 1.0);
    }


    private void UpdateAvatar(double dt)
    {
        SyncMotorDriveFromAvatarService();

        var actionOutput = _avatarService.PublishActionOutput(
            forwardGain: ForwardGainSlider.Value,
            turnGain: TurnGainSlider.Value);
        var (forwardSpeed, turnRateDeg) = actionOutput.Movement;

        var movingForward = forwardSpeed > 0.08;
        var translating = Math.Abs(forwardSpeed) > 0.08;
        bool navigationTurning = SpatialNavigationCheckBox.IsChecked == true &&
                                 (_lastNavigationDirective.Contains("turn", StringComparison.OrdinalIgnoreCase) ||
                                  _lastNavigationDirective.Contains("about_face", StringComparison.OrdinalIgnoreCase));
        var bodyTurnRateDeg = (movingForward || translating || navigationTurning) ? turnRateDeg : 0.0;
        if (Math.Abs(bodyTurnRateDeg) > 0.001)
        {
            _avatarHeadingDeg = AvatarKinematics.AdvanceHeading(_avatarHeadingDeg, bodyTurnRateDeg, dt);
        }

        UpdateDirectionalWallProximity();

        if (movingForward || navigationTurning)
        {
            _avatarHeadYawDeg = MoveTowards(_avatarHeadYawDeg, 0.0, AvatarHeadReturnRateDeg * dt);
        }
        else if (!translating && Math.Abs(turnRateDeg) > 0.01)
        {
            _avatarHeadYawDeg = Math.Clamp(_avatarHeadYawDeg + (turnRateDeg * dt), -AvatarHeadMaxYawDeg, AvatarHeadMaxYawDeg);
        }

        var (dirX, dirZ) = AvatarKinematics.ForwardDirection(_avatarHeadingDeg);

        var previousX = _avatarX;
        var previousZ = _avatarZ;
        var step = forwardSpeed * dt;
        var nextX = _avatarX + (dirX * step);
        var nextZ = _avatarZ + (dirZ * step);
        var collisionDetected = false;
        var slidAlongWall = false;

        if (!Collides(nextX, nextZ))
        {
            _avatarX = nextX;
            _avatarZ = nextZ;
        }
        else
        {
            collisionDetected = true;
            if (!Collides(nextX, _avatarZ))
            {
                _avatarX = nextX;
                slidAlongWall = true;
            }

            if (!Collides(_avatarX, nextZ))
            {
                _avatarZ = nextZ;
                slidAlongWall = true;
            }

            ApplyWallImpactPenalty(slidAlongWall);
        }

        UpdateProgressAndRecoverIfStuck(previousX, previousZ, collisionDetected, slidAlongWall);
        UpdateDirectionalWallProximity();
        var movedDistance = Math.Sqrt(DistanceSquared(previousX, previousZ, _avatarX, _avatarZ));
        _totalDistanceTravelled += movedDistance;

        HandleMazeEvents();

        _avatarTranslate.OffsetX = _avatarX;
        _avatarTranslate.OffsetY = AvatarRadius + 0.04;
        _avatarTranslate.OffsetZ = _avatarZ;
        _avatarYawRotation.Angle = _avatarHeadingDeg;
        _avatarHeadYawRotation.Angle = _avatarHeadYawDeg;

        _lastForwardSpeed = forwardSpeed;
        _lastTurnRateDeg = bodyTurnRateDeg;

        var dx = _avatarX - _goalWorld.X;
        var dz = _avatarZ - _goalWorld.Y;
        var distanceToGoal = Math.Sqrt((dx * dx) + (dz * dz));
        if (distanceToGoal < _bestDistanceToGoal)
        {
            if (_bestDistanceToGoal < double.MaxValue)
            {
                var progress = Math.Clamp((_bestDistanceToGoal - distanceToGoal) / CellSize, 0.0, 1.0);
                if (progress > 0.05)
                {
                    QueueOutcomeInput(new AvatarOutcomeTelemetry(Progress: progress * 0.38, EffortCost: Math.Clamp(Math.Abs(_lastForwardSpeed) / MazeRunMaxForwardSpeed * 0.08, 0.0, 0.12)));
                }
            }
            _bestDistanceToGoal = distanceToGoal;
        }

        if ((dx * dx) + (dz * dz) <= GoalRadius * GoalRadius)
        {
            _score += 120;
            _health = Math.Min(100, _health + 12);
            QueueOutcomeInput(new AvatarOutcomeTelemetry(SafetyRelief: 0.60, ShelterComfort: 0.22, Progress: 1.0, Novelty: 0.25, SocialApproval: 0.18), force: true);
            SetMazeEvent("Goal reached: reward +120, health +12");
            RespawnToCheckpoint();
        }
    }

    private double ComputeUrgentRunScale()
    {
        var hazardProximity = EstimateNearestHazardAudioProximity(CellSize * 4.5);
        var healthUrgency = _health < 38 ? Math.Clamp((38.0 - _health) / 38.0, 0.0, 1.0) : 0.0;
        var urgency = Math.Clamp(
            Math.Max(Math.Max(_limbicThreat, _limbicAversiveDrive), Math.Max(_limbicNorepinephrine, Math.Max(hazardProximity, healthUrgency))),
            0.0,
            1.0);
        var smoothUrgency = urgency * urgency * (3.0 - (2.0 * urgency));
        return 1.0 + ((MazeRunSpeedMultiplier - 1.0) * smoothUrgency);
    }

    private bool Collides(double x, double z)
    {
        return PointIntersectsWall(x, z, AvatarRadius * 0.85);
    }

    private bool PointIntersectsWall(double x, double z, double radius = 0.0)
    {
        for (var i = 0; i < _wallBounds.Count; i++)
        {
            var bounds = _wallBounds[i];
            if (radius <= 0.0)
            {
                if (x >= bounds.XMin && x <= bounds.XMax &&
                    z >= bounds.ZMin && z <= bounds.ZMax)
                {
                    return true;
                }

                continue;
            }

            var nearestX = Math.Clamp(x, bounds.XMin, bounds.XMax);
            var nearestZ = Math.Clamp(z, bounds.ZMin, bounds.ZMax);
            var dx = x - nearestX;
            var dz = z - nearestZ;
            if ((dx * dx) + (dz * dz) < radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    private static double GetWallHalfThickness()
    {
        return CellSize * WallThicknessScale * 0.5;
    }

    private void HandleMazeEvents()
    {
        HandleFoodCollections();
        HandleCheckpointActivation();
        HandleHazardContacts();
    }

    private void HandleFoodCollections()
    {
        for (var i = 0; i < _foodEntities.Count; i++)
        {
            var food = _foodEntities[i];
            if (food.Collected)
            {
                continue;
            }

            if (DistanceSquared(_avatarX, _avatarZ, food.World.X, food.World.Y) > FoodRadius * FoodRadius * 1.8)
            {
                continue;
            }

            food.Collected = true;
            _sceneRoot.Children.Remove(food.Model);
            _foodsCollected++;
            _score += 30;
            QueueOutcomeInput(new AvatarOutcomeTelemetry(SatietyRelief: 0.82, Progress: 0.28, Novelty: 0.06), force: true);
            SetMazeEvent($"Food collected ({_foodsCollected}/{_foodEntities.Count})");
        }
    }

    private void HandleCheckpointActivation()
    {
        if (_checkpointInactiveMaterial is null || _checkpointActiveMaterial is null)
        {
            return;
        }

        for (var i = 0; i < _checkpointEntities.Count; i++)
        {
            var checkpoint = _checkpointEntities[i];
            if (checkpoint.Activated)
            {
                continue;
            }

            if (DistanceSquared(_avatarX, _avatarZ, checkpoint.World.X, checkpoint.World.Y) > CheckpointRadius * CheckpointRadius * 2.1)
            {
                continue;
            }

            checkpoint.Activated = true;
            checkpoint.Model.Material = _checkpointActiveMaterial;
            checkpoint.Model.BackMaterial = _checkpointActiveMaterial;
            _respawnWorld = checkpoint.World;
            _checkpointActivations++;
            _score += 15;
            QueueOutcomeInput(new AvatarOutcomeTelemetry(SafetyRelief: 0.36, ShelterComfort: 0.18, Progress: 0.42, Novelty: 0.16), force: true);
            SetMazeEvent($"Checkpoint activated ({_checkpointActivations}/{_checkpointEntities.Count})");
        }
    }

    private void HandleHazardContacts()
    {
        var now = DateTime.UtcNow;
        if (now - _lastHazardDamageUtc < HazardDamageCooldown)
        {
            return;
        }

        for (var i = 0; i < _hazardEntities.Count; i++)
        {
            var hazard = _hazardEntities[i];
            if (DistanceSquared(_avatarX, _avatarZ, hazard.World.X, hazard.World.Y) > HazardRadius * HazardRadius * 2.25)
            {
                continue;
            }

            _lastHazardDamageUtc = now;
            _hazardContacts++;
            _health = Math.Max(0, _health - 18);
            _score = Math.Max(0, _score - 10);
            QueueOutcomeInput(new AvatarOutcomeTelemetry(PainLevel: 0.78, DamageLevel: 0.52, EffortCost: 0.18), force: true);

            var headingRad = _avatarHeadingDeg * Math.PI / 180.0;
            var pushbackX = _avatarX - (Math.Sin(headingRad) * 0.55);
            var pushbackZ = _avatarZ - (Math.Cos(headingRad) * 0.55);
            if (!Collides(pushbackX, pushbackZ))
            {
                _avatarX = pushbackX;
                _avatarZ = pushbackZ;
            }

            if (_health <= 0)
            {
                _health = 100;
                _score = Math.Max(0, _score - 25);
                SetMazeEvent("Hazard incapacitation: respawn to checkpoint");
                RespawnToCheckpoint();
            }
            else
            {
                SetMazeEvent($"Hazard contact #{_hazardContacts}: health {_health}");
            }

            break;
        }
    }

    private void ApplyWallImpactPenalty(bool slidAlongWall)
    {
        var now = DateTime.UtcNow;
        if (now - _lastWallImpactUtc < WallImpactPenaltyCooldown)
        {
            return;
        }

        _lastWallImpactUtc = now;
        _wallImpacts++;
        _recentWallImpactTicks.Enqueue(Math.Max(0, _lastTick));
        RegisterWallCollisionEvent(now, slidAlongWall);

        var scorePenalty = slidAlongWall ? 2 : 4;
        var healthPenalty = slidAlongWall ? 1 : 2;

        _score = Math.Max(0, _score - scorePenalty);
        _health = Math.Max(1, _health - healthPenalty);

        // Wall impacts are mild aversive teaching signals; never force a hard reset.
        _limbicAversiveDrive = Math.Clamp(_limbicAversiveDrive + (slidAlongWall ? 0.05 : 0.08), 0.0, 1.0);
        _limbicThreat = Math.Clamp(_limbicThreat + (slidAlongWall ? 0.04 : 0.07), 0.0, 1.0);
        QueueOutcomeInput(new AvatarOutcomeTelemetry(PainLevel: slidAlongWall ? 0.18 : 0.32, DamageLevel: slidAlongWall ? 0.06 : 0.12, EffortCost: 0.22), force: true);

        SetMazeEvent($"Wall impact #{_wallImpacts}: score -{scorePenalty}");
        TriggerOrientingStimulus("wall-impact", severeImpact: !slidAlongWall);
    }

    private void UpdateProgressAndRecoverIfStuck(double previousX, double previousZ, bool collisionDetected, bool slidAlongWall)
    {
        var movedSquared = DistanceSquared(previousX, previousZ, _avatarX, _avatarZ);
        var now = DateTime.UtcNow;
        if (movedSquared >= 0.0016)
        {
            _lastProgressUtc = now;
            _consecutiveWallContacts = 0;
            if (!collisionDetected)
            {
                _collisionEscapeHemisphere = null;
            }
            return;
        }

        if (collisionDetected)
        {
            _consecutiveWallContacts++;
            var threshold = slidAlongWall ? 10 : 6;
            if (_consecutiveWallContacts >= threshold)
            {
                TriggerOrientingStimulus("wall-trap recovery", severeImpact: true);
                _consecutiveWallContacts = 0;
            }

            return;
        }

        if (now - _lastProgressUtc > NoProgressRecoveryTimeout)
        {
            var severe = (now - _lastProgressUtc).TotalSeconds >= HardStuckTimeoutSec;
            TriggerOrientingStimulus("no-progress recovery", severeImpact: severe);
            _lastProgressUtc = now;
            _consecutiveWallContacts = 0;
        }
    }

    private void RegisterWallCollisionEvent(DateTime now, bool slidAlongWall)
    {
        _recentWallCollisionUtc.Enqueue(now);

        var window = TimeSpan.FromSeconds(HardStuckCollisionBurstWindowSec);
        while (_recentWallCollisionUtc.Count > 0 && (now - _recentWallCollisionUtc.Peek()) > window)
        {
            _recentWallCollisionUtc.Dequeue();
        }

        if (_recentWallCollisionUtc.Count < HardStuckCollisionBurstThreshold)
        {
            return;
        }

        TriggerOrientingStimulus("collision burst recovery", severeImpact: true);
        _recentWallCollisionUtc.Clear();
    }

    private async Task PollSpatialNavigationAsync(Uri endpoint, CancellationToken token)
    {
        var (row, column) = WorldToGrid(_avatarX, _avatarZ);
        var (goalRow, goalColumn) = WorldToGrid(_goalWorld.X, _goalWorld.Y);
        int headingQuarter = NavigationCoordinateFrame.HeadingQuarterFromDegrees(_avatarHeadingDeg);
        Point cellCenter = GridToWorld(row, column);
        double distanceFromCellCenter = Math.Sqrt(DistanceSquared(_avatarX, _avatarZ, cellCenter.X, cellCenter.Y));
        double distanceToGoal = Math.Sqrt(DistanceSquared(_avatarX, _avatarZ, _goalWorld.X, _goalWorld.Y));
        double relativeGoalBearing = NavigationCoordinateFrame.RelativeBearingDegrees(
            _avatarX,
            _avatarZ,
            _goalWorld.X,
            _goalWorld.Y,
            _avatarHeadingDeg);
        var observation = new HippocampalNavigationObservation(
            row,
            column,
            headingQuarter,
            IsNavigationDirectionOpen(row, column, headingQuarter),
            IsNavigationDirectionOpen(row, column, NavigationCoordinateFrame.RotateQuarter(headingQuarter, 1)),
            IsNavigationDirectionOpen(row, column, NavigationCoordinateFrame.RotateQuarter(headingQuarter, -1)),
            IsNavigationDirectionOpen(row, column, NavigationCoordinateFrame.RotateQuarter(headingQuarter, 2)),
            goalRow,
            goalColumn,
            relativeGoalBearing,
            distanceToGoal,
            _wallImpacts,
            distanceToGoal <= GoalRadius);
        var request = new HippocampalNavigationControlRequest(
            _navigationSessionId,
            $"wpf-maze-{_mazeSeed}-{_mazeRows}x{_mazeCols}",
            _navigationResetPending,
            distanceFromCellCenter <= CellSize * 0.20,
            _avatarHeadingDeg,
            observation,
            (_avatarX - cellCenter.X) / CellSize,
            (_avatarZ - cellCenter.Y) / CellSize);

        using var response = await _httpClient.PostAsJsonAsync(
            new Uri(endpoint, "/api/v1/navigation/decision"),
            request,
            token);
        if (!response.IsSuccessStatusCode)
        {
            string detail = await ReadNavigationErrorAsync(response, token);
            StopSpatialNavigation($"Navigation: HTTP {(int)response.StatusCode} {detail}".TrimEnd());
            return;
        }

        HippocampalNavigationControlResponse? result = await response.Content
            .ReadFromJsonAsync<HippocampalNavigationControlResponse>(cancellationToken: token);
        if (result is null)
        {
            StopSpatialNavigation("Navigation: empty response");
            return;
        }

        if (!string.Equals(result.ProtocolVersion, NavigationCoordinateFrame.ControlProtocolVersion, StringComparison.Ordinal))
        {
            StopSpatialNavigation($"Navigation: protocol mismatch ({result.ProtocolVersion})");
            return;
        }

        if (string.IsNullOrWhiteSpace(result.MotorDirective))
        {
            StopSpatialNavigation("Navigation: response omitted motor directive");
            return;
        }

        AvatarDispatchSpike[] spikes = result.MotorSpikes
            .Where(static spike => !string.IsNullOrWhiteSpace(spike.SourceStructure))
            .Select(static spike => new AvatarDispatchSpike(
                spike.SourceStructure,
                AvatarJson.NormalizeHemisphere(spike.SourceHemisphere),
                spike.WallClockUnixMs,
                spike.SourceNeuronId))
            .ToArray();
        if (!result.MotorDirective.Equals("motor_stop", StringComparison.OrdinalIgnoreCase) && spikes.Length == 0)
        {
            StopSpatialNavigation("Navigation: motor directive contained no M1 spikes");
            return;
        }

        ApplySpatialNavigationDirective(result.MotorDirective, spikes, result.ResetApplied);
        _navigationResetPending = false;

        NavigationStatusText.Text =
            $"Navigation: {result.MotorDirective} -> ({result.Decision.TargetRow},{result.Decision.TargetColumn}) | error {result.HeadingErrorDeg:0.0} deg | " +
            $"places {result.ExploredPlaceCount} | edges {result.LearnedEdgeCount} | " +
            $"backtracks {result.BacktrackCount} | decisions {result.DecisionCount}";
    }

    private static async Task<string> ReadNavigationErrorAsync(
        HttpResponseMessage response,
        CancellationToken token)
    {
        string payload = await response.Content.ReadAsStringAsync(token);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            string error = AvatarJson.GetString(document.RootElement, "error", "detail", "title");
            if (!string.IsNullOrWhiteSpace(error))
            {
                return error;
            }
        }
        catch (JsonException)
        {
            // Fall through to a compact plain-text diagnostic.
        }

        string compact = string.Join(' ', payload.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 120 ? compact : compact[..120];
    }

    private void ApplySpatialNavigationDirective(
        string directive,
        IReadOnlyList<AvatarDispatchSpike> spikes,
        bool forceReset)
    {
        bool changed = !string.Equals(directive, _lastNavigationDirective, StringComparison.OrdinalIgnoreCase);
        if (changed || forceReset)
        {
            _avatarService.PostResetMotor();
            ApplyNervousSystemSignal(new AvatarNervousSystemSignal(0.0, 0.0, 0, 0, AvatarToolSignal.None));
        }

        if (!directive.Equals("motor_stop", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMotorDispatch(spikes);
        }

        _lastNavigationDirective = directive;
    }

    private void StopSpatialNavigation(string status)
    {
        if (!_lastNavigationDirective.Equals("motor_stop", StringComparison.OrdinalIgnoreCase))
        {
            _avatarService.PostResetMotor();
            ApplyNervousSystemSignal(new AvatarNervousSystemSignal(0.0, 0.0, 0, 0, AvatarToolSignal.None));
        }

        _lastNavigationDirective = "motor_stop";
        NavigationStatusText.Text = status;
    }

    private void TriggerOrientingStimulus(string reason, bool severeImpact)
    {
        var now = DateTime.UtcNow;
        var cooldown = GetEscapeReflexCooldown();
        if (now - _lastEscapeReflexUtc < cooldown || _collisionStimulusInFlight)
        {
            return;
        }

        _lastEscapeReflexUtc = now;
        _stuckRecoveries++;
        var aggressiveness = GetEscapeAggressiveness();

        var hemisphere = AvatarWallSensing.ResolveEscapeHemisphere(
            _collisionEscapeHemisphere,
            _lastLeftProximity,
            _lastRightProximity,
            _lastTurnRateDeg);
        _collisionEscapeHemisphere = hemisphere;
        var intensity = Math.Clamp(0.85 + (0.35 * aggressiveness) + (severeImpact ? 0.25 : 0.0), 0.20, 3.50);
        var burstCount = Math.Clamp((int)Math.Round(10 + (aggressiveness * 6.0) + (severeImpact ? 6.0 : 0.0) + (_consecutiveWallContacts * 0.8)), 6, 48);

        SetMazeEvent($"Orienting reflex #{_stuckRecoveries}: {reason}");
        _ = DispatchCollisionStimulusAsync(hemisphere, intensity, burstCount, reason, _shutdown.Token);
    }

    private async Task DispatchCollisionStimulusAsync(
        string hemisphere,
        double intensity,
        int burstCount,
        string reason,
        CancellationToken token)
    {
        var endpoint = ResolveEndpointUri();
        if (endpoint is null)
        {
            return;
        }

        _collisionStimulusInFlight = true;
        try
        {
            var request = new
            {
                Pattern = reason,
                Intensity = Math.Clamp(intensity, 0.20, 3.50),
                BurstCount = Math.Clamp(burstCount, 6, 64),
                TargetStructure = "SuperiorColliculus",
                SourceStructure = "S1",
                Hemisphere = hemisphere,
                IsFeedback = false
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(endpoint, "/api/v1/admin/input/collision"), request, token);
            var payload = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode)
            {
                Log($"Collision stimulus warning: HTTP {(int)response.StatusCode}. {payload}");
                return;
            }

            using var doc = JsonDocument.Parse(payload);
            var delivered = GetInt(doc.RootElement, "deliveredSpikes");
            var generated = GetInt(doc.RootElement, "generatedSpikes");
            if (delivered <= 0)
            {
                Log($"Collision orienting injected but not delivered ({generated}/0).");
                return;
            }

            Log($"Collision orienting delivered {delivered}/{generated} spikes to SuperiorColliculus ({hemisphere}).");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // app shutdown path
        }
        catch (Exception ex)
        {
            Log($"Collision stimulus warning: {ex.GetType().Name}.");
        }
        finally
        {
            _collisionStimulusInFlight = false;
        }
    }

    private void QueueOutcomeInput(AvatarOutcomeTelemetry telemetry, bool force = false)
        => _ = DispatchOutcomeInputAsync(telemetry, force, _shutdown.Token);

    private async Task DispatchOutcomeInputAsync(AvatarOutcomeTelemetry telemetry, bool force, CancellationToken token)
    {
        if (_outcomeStimulusInFlight)
        {
            return;
        }

        var nowMs = Environment.TickCount64;
        if (!force && (nowMs - _lastOutcomeDispatchMs) < 650)
        {
            return;
        }

        var endpoint = ResolveEndpointUri();
        if (endpoint is null)
        {
            return;
        }

        _outcomeStimulusInFlight = true;
        _lastOutcomeDispatchMs = nowMs;
        try
        {
            _avatarService.PostOutcome(telemetry);
            var outcome = await DrainAvatarOutcomeAsync(token);
            if (outcome.HasValue)
            {
                await AvatarControlApi.PostOutcomeAsync(_httpClient, endpoint, outcome.Value, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // shutdown path
        }
        catch
        {
            // Best-effort biological outcome input.
        }
        finally
        {
            _outcomeStimulusInFlight = false;
        }
    }

    private async Task DispatchBodyStateInputAsync(CancellationToken token)
    {
        if (_bodyStimulusInFlight)
        {
            return;
        }

        var nowMs = Environment.TickCount64;
        if ((nowMs - _lastBodyStateDispatchMs) < BodyStateDispatchIntervalMs)
        {
            return;
        }

        var endpoint = ResolveEndpointUri();
        if (endpoint is null)
        {
            return;
        }

        _bodyStimulusInFlight = true;
        _lastBodyStateDispatchMs = nowMs;
        try
        {
            var wall = Math.Clamp(_lastWallProximity, 0.0, 1.0);
            var frontTouch = Math.Clamp(Math.Max(_lastFrontProximity, wall * 0.35), 0.0, 1.0);
            var leftTouch = Math.Clamp(_lastLeftProximity, 0.0, 1.0);
            var rightTouch = Math.Clamp(_lastRightProximity, 0.0, 1.0);
            var groundTouch = Math.Clamp(0.16 + (Math.Abs(_lastForwardSpeed) / MazeRunMaxForwardSpeed * 0.70), 0.0, 1.0);
            var hazardProximity = EstimateNearestHazardAudioProximity(CellSize * 4.5);
            var urgency = Math.Clamp((ComputeUrgentRunScale() - 1.0) / (MazeRunSpeedMultiplier - 1.0), 0.0, 1.0);
            var telemetry = new AvatarBodyTelemetry(
                _lastForwardSpeed,
                _lastTurnRateDeg,
                wall,
                _leftMotorDrive,
                _rightMotorDrive,
                TactileFront: frontTouch,
                TactileLeft: leftTouch,
                TactileRight: rightTouch,
                TactileGround: groundTouch,
                PainLevel: Math.Clamp((wall * 0.45) + (hazardProximity * 0.55), 0.0, 1.0),
                Urgency: urgency);
            _avatarService.PostBodyInput(telemetry, MazeBodyStateProfile);
            var bodyInput = await DrainAvatarBodyInputAsync(token);
            if (bodyInput.HasValue)
            {
                await AvatarControlApi.PostBodyStateAsync(
                    _httpClient,
                    endpoint,
                    bodyInput.Value.Telemetry,
                    bodyInput.Value.Profile,
                    token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // shutdown path
        }
        catch
        {
            // Best-effort body-state input.
        }
        finally
        {
            _bodyStimulusInFlight = false;
        }
    }

    private async Task<AvatarOutcomeTelemetry?> DrainAvatarOutcomeAsync(CancellationToken token)
    {
        if (_avatarService.TryDequeueOutcome(out var outcome))
        {
            return outcome;
        }

        await Task.Delay(1, token);
        return _avatarService.TryDequeueOutcome(out outcome) ? outcome : null;
    }

    private async Task<AvatarBodyStateInput?> DrainAvatarBodyInputAsync(CancellationToken token)
    {
        if (_avatarService.TryDequeueBodyInput(out var input))
        {
            return input;
        }

        await Task.Delay(1, token);
        return _avatarService.TryDequeueBodyInput(out input) ? input : null;
    }

    private async Task DispatchEnvironmentAudioInputAsync(CancellationToken token)
    {
        if (_environmentAudioInFlight)
        {
            return;
        }

        var nowMs = Environment.TickCount64;
        if ((nowMs - _lastEnvironmentAudioDispatchMs) < EnvironmentAudioDispatchIntervalMs)
        {
            return;
        }

        if (_environmentAudioBackoff.IsBlocked(nowMs))
        {
            return;
        }

        var endpoint = ResolveEndpointUri();
        if (endpoint is null)
        {
            return;
        }

        var candidateCues = BuildEnvironmentAuditoryCues();
        if (candidateCues.Count > 0)
        {
            _avatarService.PostAuditoryInputCandidates(candidateCues, maxCues: 1);
        }

        var cues = await DrainAvatarAuditoryInputCuesAsync(maxCues: 1, token);
        if (cues.Count == 0)
        {
            return;
        }

        _environmentAudioInFlight = true;
        _lastEnvironmentAudioDispatchMs = nowMs;
        try
        {
            await AvatarControlApi.PostAuditoryCueAsync(_auditoryInputHttpClient, endpoint, cues[0], token);
            _environmentAudioBackoff.Reset();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // shutdown path
        }
        catch
        {
            _environmentAudioBackoff.RegisterFailure(Environment.TickCount64);
        }
        finally
        {
            _environmentAudioInFlight = false;
        }
    }

    private async Task<List<AvatarAuditoryCue>> DrainAvatarAuditoryInputCuesAsync(int maxCues, CancellationToken token)
    {
        var cues = new List<AvatarAuditoryCue>(Math.Clamp(maxCues, 1, 8));
        while (cues.Count < maxCues && _avatarService.TryDequeueAuditoryInput(out var cue))
        {
            cues.Add(cue);
        }

        if (cues.Count > 0)
        {
            return cues;
        }

        await Task.Delay(1, token);
        while (cues.Count < maxCues && _avatarService.TryDequeueAuditoryInput(out var cue))
        {
            cues.Add(cue);
        }

        return cues;
    }

    private List<AvatarAuditoryCue> BuildEnvironmentAuditoryCues()
    {
        var cues = new List<AvatarAuditoryCue>(7);
        var movement = Math.Clamp(Math.Abs(_lastForwardSpeed) / MazeRunMaxForwardSpeed, 0.0, 1.0);
        if (_lastWallProximity > 0.82)
        {
            cues.Add(new AvatarAuditoryCue(
                Pattern: "WallImpactMazeThud",
                Intensity: (float)Math.Clamp(0.30 + (_lastWallProximity * 1.30), 0.30, 2.0),
                BurstCount: (int)Math.Clamp(8 + (_lastWallProximity * 26), 6, 42),
                Hemisphere: null));
        }

        if (movement > 0.05)
        {
            cues.Add(new AvatarAuditoryCue(
                Pattern: _lastWallProximity > 0.52 ? "AvatarFootstepMazeNarrowEcho" : "AvatarFootstepMazeFloor",
                Intensity: (float)Math.Clamp(0.22 + (movement * 0.82), 0.20, 1.25),
                BurstCount: (int)Math.Clamp(7 + (movement * 18), 5, 30),
                Hemisphere: null));
        }

        var corridorAir = Math.Clamp((_lastWallProximity * 0.24) + (Math.Abs(_lastTurnRateDeg) / 260.0 * 0.18), 0.0, 1.0);
        if (corridorAir > 0.08)
        {
            cues.Add(new AvatarAuditoryCue(
                Pattern: "MazeCorridorAir",
                Intensity: (float)Math.Clamp(0.16 + (corridorAir * 0.68), 0.16, 0.95),
                BurstCount: (int)Math.Clamp(5 + (corridorAir * 16), 4, 26),
                Hemisphere: null));
        }

        if (_lastWallProximity > 0.34)
        {
            cues.Add(new AvatarAuditoryCue(
                Pattern: "WallScrapeMazeEcho",
                Intensity: (float)Math.Clamp(0.20 + (_lastWallProximity * 1.10), 0.20, 1.55),
                BurstCount: (int)Math.Clamp(6 + (_lastWallProximity * 18), 4, 32),
                Hemisphere: null));
        }

        var hazardProximity = EstimateNearestHazardAudioProximity(CellSize * 4.5);
        if (hazardProximity > 0.04)
        {
            cues.Add(new AvatarAuditoryCue(
                Pattern: "HazardElectricalBuzz",
                Intensity: (float)Math.Clamp(0.22 + (hazardProximity * 1.30), 0.20, 1.85),
                BurstCount: (int)Math.Clamp(8 + (hazardProximity * 24), 6, 42),
                Hemisphere: ResolveHemisphereHint(GetNearestHazardPoint())));
        }

        var goalProximity = EstimatePointAudioProximity(_goalWorld, CellSize * 7.0);
        if (goalProximity > 0.05)
        {
            cues.Add(new AvatarAuditoryCue(
                Pattern: "GoalShelterHum",
                Intensity: (float)Math.Clamp(0.18 + (goalProximity * 0.96), 0.18, 1.35),
                BurstCount: (int)Math.Clamp(6 + (goalProximity * 20), 5, 32),
                Hemisphere: ResolveHemisphereHint(_goalWorld)));
        }

        var checkpointProximity = EstimateNearestCheckpointAudioProximity(CellSize * 5.0, out var checkpointPoint);
        if (checkpointProximity > 0.08)
        {
            cues.Add(new AvatarAuditoryCue(
                Pattern: "CheckpointSafeChime",
                Intensity: (float)Math.Clamp(0.16 + (checkpointProximity * 0.72), 0.16, 1.0),
                BurstCount: (int)Math.Clamp(4 + (checkpointProximity * 14), 4, 22),
                Hemisphere: ResolveHemisphereHint(checkpointPoint)));
        }

        return cues;
    }

    private double EstimatePointAudioProximity(Point world, double maxDistance)
    {
        var distance = Math.Sqrt(DistanceSquared(_avatarX, _avatarZ, world.X, world.Y));
        return Math.Clamp(1.0 - (distance / Math.Max(0.1, maxDistance)), 0.0, 1.0);
    }

    private double EstimateNearestCheckpointAudioProximity(double maxDistance, out Point nearestPoint)
    {
        var bestSq = double.MaxValue;
        nearestPoint = new Point(_avatarX, _avatarZ);
        for (var i = 0; i < _checkpointEntities.Count; i++)
        {
            var checkpoint = _checkpointEntities[i];
            var distanceSq = DistanceSquared(_avatarX, _avatarZ, checkpoint.World.X, checkpoint.World.Y);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                nearestPoint = checkpoint.World;
            }
        }

        if (bestSq == double.MaxValue)
        {
            return 0.0;
        }

        return Math.Clamp(1.0 - (Math.Sqrt(bestSq) / Math.Max(0.1, maxDistance)), 0.0, 1.0);
    }

    private double EstimateNearestHazardAudioProximity(double maxDistance)
    {
        var bestSq = double.MaxValue;
        for (var i = 0; i < _hazardEntities.Count; i++)
        {
            var hazard = _hazardEntities[i];
            var distanceSq = DistanceSquared(_avatarX, _avatarZ, hazard.World.X, hazard.World.Y);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
            }
        }

        if (bestSq == double.MaxValue)
        {
            return 0.0;
        }

        return Math.Clamp(1.0 - (Math.Sqrt(bestSq) / Math.Max(0.1, maxDistance)), 0.0, 1.0);
    }

    private Point GetNearestHazardPoint()
    {
        var bestSq = double.MaxValue;
        var best = new Point(_avatarX, _avatarZ);
        for (var i = 0; i < _hazardEntities.Count; i++)
        {
            var hazard = _hazardEntities[i];
            var distanceSq = DistanceSquared(_avatarX, _avatarZ, hazard.World.X, hazard.World.Y);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                best = hazard.World;
            }
        }

        return best;
    }

    private string? ResolveHemisphereHint(Point world)
    {
        var headingRad = GetAvatarLookHeadingRad();
        var rightX = Math.Sin(headingRad + (Math.PI * 0.5));
        var rightZ = Math.Cos(headingRad + (Math.PI * 0.5));
        var toSourceX = world.X - _avatarX;
        var toSourceZ = world.Y - _avatarZ;
        var lateral = (toSourceX * rightX) + (toSourceZ * rightZ);
        if (Math.Abs(lateral) < 0.08)
        {
            return null;
        }

        return lateral < 0.0 ? "L" : "R";
    }

    private void RespawnToCheckpoint()
    {
        _avatarX = _respawnWorld.X;
        _avatarZ = _respawnWorld.Y;
        _avatarHeadingDeg = 0;
        _avatarHeadYawDeg = 0;
        _avatarService.PostResetMotor();
        ApplyNervousSystemSignal(new AvatarNervousSystemSignal(0.0, 0.0, 0, 0, AvatarToolSignal.None));
        _avatarTranslate.OffsetX = _avatarX;
        _avatarTranslate.OffsetY = AvatarRadius + 0.04;
        _avatarTranslate.OffsetZ = _avatarZ;
        _avatarYawRotation.Angle = _avatarHeadingDeg;
        _avatarHeadYawRotation.Angle = _avatarHeadYawDeg;
    }

    private void GenerateMazeLayoutAndEntityCells()
    {
        _mazeRows = MazeRows;
        _mazeCols = MazeCols;
        InitializeMazeLayout();
        CarveMazePaths();
        AddExtraMazeOpenings();
        PlaceMazeEndpoints();
        BuildEntityCellSets();
    }

    private void UpdateDirectionalWallProximity()
    {
        var headingRad = _avatarHeadingDeg * Math.PI / 180.0;
        var sideAngleRad = WallProbeSideAngleDeg * Math.PI / 180.0;
        var front = TraceVisionRay(Math.Sin(headingRad), Math.Cos(headingRad), WallProbeRange);
        var left = TraceVisionRay(Math.Sin(headingRad - sideAngleRad), Math.Cos(headingRad - sideAngleRad), WallProbeRange);
        var right = TraceVisionRay(Math.Sin(headingRad + sideAngleRad), Math.Cos(headingRad + sideAngleRad), WallProbeRange);

        _lastFrontProximity = AvatarWallSensing.ProximityFromRay(front.HitWall, front.Distance, AvatarRadius, WallProbeRange);
        _lastLeftProximity = AvatarWallSensing.ProximityFromRay(left.HitWall, left.Distance, AvatarRadius, WallProbeRange);
        _lastRightProximity = AvatarWallSensing.ProximityFromRay(right.HitWall, right.Distance, AvatarRadius, WallProbeRange);
        _lastWallProximity = Math.Max(_lastFrontProximity, Math.Max(_lastLeftProximity, _lastRightProximity));
    }

    private static int ResolveMazeSeed()
    {
        var configured = Environment.GetEnvironmentVariable("NRE_MAZE_SEED");
        return int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)
            ? seed
            : DefaultMazeSeed;
    }

    private string ComputeMazeLayoutFingerprint()
    {
        var builder = new StringBuilder((_mazeRows * (_mazeCols + 1)) + 32);
        builder.Append(_mazeSeed).Append('|').Append(_mazeRows).Append('x').Append(_mazeCols).Append('\n');
        foreach (var row in _mazeLayout)
        {
            builder.Append(row).Append('\n');
        }

        AppendCells(builder, 'F', _foodCells);
        AppendCells(builder, 'H', _hazardCells);
        AppendCells(builder, 'C', _checkpointCells);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static void AppendCells(
        StringBuilder builder,
        char kind,
        IEnumerable<(int Row, int Col)> cells)
    {
        foreach (var cell in cells.OrderBy(static cell => cell.Row).ThenBy(static cell => cell.Col))
        {
            builder.Append(kind).Append(':').Append(cell.Row).Append(',').Append(cell.Col).Append(';');
        }

        builder.Append('\n');
    }

    private void InitializeMazeLayout()
    {
        _mazeLayout = new char[_mazeRows][];
        for (var row = 0; row < _mazeRows; row++)
        {
            _mazeLayout[row] = new string('#', _mazeCols).ToCharArray();
        }
    }

    private void CarveMazePaths()
    {
        var stack = new Stack<(int Row, int Col)>();
        _mazeLayout[1][1] = '.';
        stack.Push((1, 1));
        var neighbors = new (int Row, int Col, int WallRow, int WallCol)[MazeCarveDirections.Length];

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var neighborCount = 0;
            for (var i = 0; i < MazeCarveDirections.Length; i++)
            {
                var nextRow = current.Row + MazeCarveDirections[i].Dr;
                var nextCol = current.Col + MazeCarveDirections[i].Dc;
                if (nextRow <= 0 || nextRow >= _mazeRows - 1 || nextCol <= 0 || nextCol >= _mazeCols - 1)
                {
                    continue;
                }

                if (_mazeLayout[nextRow][nextCol] != '#')
                {
                    continue;
                }

                neighbors[neighborCount++] = (
                    nextRow,
                    nextCol,
                    current.Row + (MazeCarveDirections[i].Dr / 2),
                    current.Col + (MazeCarveDirections[i].Dc / 2));
            }

            if (neighborCount == 0)
            {
                stack.Pop();
                continue;
            }

            var chosen = neighbors[_mazeRandom.Next(neighborCount)];
            _mazeLayout[chosen.WallRow][chosen.WallCol] = '.';
            _mazeLayout[chosen.Row][chosen.Col] = '.';
            stack.Push((chosen.Row, chosen.Col));
        }
    }

    private void AddExtraMazeOpenings()
    {
        var extraOpenings = (_mazeRows * _mazeCols) / 16;
        for (var i = 0; i < extraOpenings; i++)
        {
            var row = _mazeRandom.Next(1, _mazeRows - 1);
            var col = _mazeRandom.Next(1, _mazeCols - 1);
            if (_mazeLayout[row][col] != '#')
            {
                continue;
            }

            var horizontalPassage = _mazeLayout[row][col - 1] == '.' && _mazeLayout[row][col + 1] == '.';
            var verticalPassage = _mazeLayout[row - 1][col] == '.' && _mazeLayout[row + 1][col] == '.';
            if (horizontalPassage || verticalPassage)
            {
                _mazeLayout[row][col] = '.';
            }
        }
    }

    private void PlaceMazeEndpoints()
    {
        _mazeLayout[1][1] = 'S';
        _mazeLayout[_mazeRows - 2][_mazeCols - 2] = 'G';
        _mazeLayout[1][2] = '.';
        _mazeLayout[2][1] = '.';
        _mazeLayout[_mazeRows - 2][_mazeCols - 3] = '.';
        _mazeLayout[_mazeRows - 3][_mazeCols - 2] = '.';
    }

    private void BuildEntityCellSets()
    {
        _foodCells.Clear();
        _hazardCells.Clear();
        _checkpointCells.Clear();

        var startCell = (Row: 1, Col: 1);
        var goalCell = (Row: _mazeRows - 2, Col: _mazeCols - 2);
        var walkable = new List<(int Row, int Col, int DistStart, int DistGoal)>(_mazeRows * _mazeCols);

        for (var row = 1; row < _mazeRows - 1; row++)
        {
            for (var col = 1; col < _mazeCols - 1; col++)
            {
                var tile = _mazeLayout[row][col];
                if (tile == '#')
                {
                    continue;
                }

                if (tile == 'S' || tile == 'G')
                {
                    continue;
                }

                var distStart = Math.Abs(row - startCell.Row) + Math.Abs(col - startCell.Col);
                var distGoal = Math.Abs(row - goalCell.Row) + Math.Abs(col - goalCell.Col);
                walkable.Add((row, col, distStart, distGoal));
            }
        }

        if (walkable.Count == 0)
        {
            return;
        }

        walkable.Sort((a, b) => (b.DistStart + b.DistGoal).CompareTo(a.DistStart + a.DistGoal));
        var used = new HashSet<(int Row, int Col)>();
        PopulateCheckpointCells(walkable, used);
        PopulateHazardCells(walkable, used);
        PopulateFoodCells(walkable, used);
    }

    private void PopulateCheckpointCells(IReadOnlyList<(int Row, int Col, int DistStart, int DistGoal)> walkable, ISet<(int Row, int Col)> used)
    {
        for (var i = 0; i < walkable.Count && _checkpointCells.Count < CheckpointTargetCount; i++)
        {
            var candidate = walkable[i];
            if (candidate.DistStart < 8 || candidate.DistGoal < 6)
            {
                continue;
            }

            if (IsNearAny(candidate.Row, candidate.Col, _checkpointCells, 5))
            {
                continue;
            }

            var cell = (candidate.Row, candidate.Col);
            _checkpointCells.Add(cell);
            used.Add(cell);
        }
    }

    private void PopulateHazardCells(IReadOnlyList<(int Row, int Col, int DistStart, int DistGoal)> walkable, ISet<(int Row, int Col)> used)
    {
        var hazardPool = new List<(int Row, int Col)>(walkable.Count);
        for (var i = 0; i < walkable.Count; i++)
        {
            var candidate = walkable[i];
            var cell = (candidate.Row, candidate.Col);
            if (candidate.DistStart > 5 && candidate.DistGoal > 5 && !used.Contains(cell))
            {
                hazardPool.Add(cell);
            }
        }

        ShuffleInPlace(hazardPool);

        for (var i = 0; i < hazardPool.Count && _hazardCells.Count < HazardTargetCount; i++)
        {
            var candidate = hazardPool[i];
            if (IsNearAny(candidate.Row, candidate.Col, _checkpointCells, 3) || IsNearAny(candidate.Row, candidate.Col, _hazardCells, 4))
            {
                continue;
            }

            _hazardCells.Add(candidate);
            used.Add(candidate);
        }
    }

    private void PopulateFoodCells(IReadOnlyList<(int Row, int Col, int DistStart, int DistGoal)> walkable, ISet<(int Row, int Col)> used)
    {
        var foodPool = new List<(int Row, int Col)>(walkable.Count);
        for (var i = 0; i < walkable.Count; i++)
        {
            var candidate = walkable[i];
            var cell = (candidate.Row, candidate.Col);
            if (!used.Contains(cell))
            {
                foodPool.Add(cell);
            }
        }

        ShuffleInPlace(foodPool);

        for (var i = 0; i < foodPool.Count && _foodCells.Count < FoodTargetCount; i++)
        {
            var candidate = foodPool[i];
            if (IsNearAny(candidate.Row, candidate.Col, _hazardCells, 2))
            {
                continue;
            }

            _foodCells.Add(candidate);
        }
    }

    private bool IsNearAny(int row, int col, IReadOnlyList<(int Row, int Col)> cells, int manhattanDistance)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            var distance = Math.Abs(row - cells[i].Row) + Math.Abs(col - cells[i].Col);
            if (distance <= manhattanDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void ShuffleInPlace(IList<(int Row, int Col)> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _mazeRandom.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void BuildMazeMetadata()
    {
        _mazeRows = _mazeLayout.Length;
        _mazeCols = _mazeLayout[0].Length;

        for (var row = 0; row < _mazeRows; row++)
        {
            if (_mazeLayout[row].Length != _mazeCols)
            {
                throw new InvalidOperationException("Maze rows are not equal length.");
            }

            for (var col = 0; col < _mazeCols; col++)
            {
                if (_mazeLayout[row][col] == 'S')
                {
                    _startWorld = GridToWorld(row, col);
                }
                else if (_mazeLayout[row][col] == 'G')
                {
                    _goalWorld = GridToWorld(row, col);
                }
            }
        }

        _respawnWorld = _startWorld;
    }

    private void BuildScene()
    {
        MazeViewport.Children.Clear();

        var baseDistance = Math.Max(_mazeRows, _mazeCols) * 0.96;
        if (_mazeCamera is null)
        {
            _mazeCamera = new PerspectiveCamera
            {
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 58
            };
            _cameraYawDeg = 0.0;
            _cameraPitchDeg = -31.0;
            _cameraDistance = baseDistance * 1.35;
        }
        else if (_cameraDistance <= 0)
        {
            _cameraDistance = baseDistance * 1.35;
        }

        MazeViewport.Camera = _mazeCamera;
        UpdateMazeCameraPose();

        _sceneRoot.Children.Clear();
        _sceneRoot.Children.Add(new AmbientLight(Color.FromRgb(48, 56, 72)));
        _sceneRoot.Children.Add(new DirectionalLight(Color.FromRgb(215, 225, 245), new Vector3D(-0.4, -1.0, -0.25)));
        _sceneRoot.Children.Add(new DirectionalLight(Color.FromRgb(110, 132, 172), new Vector3D(0.5, -0.5, 0.35)));

        BuildFloor();
        BuildWalls();
        BuildGoal();
        BuildMazeEvents();
        BuildAvatar();

        MazeViewport.Children.Add(new ModelVisual3D { Content = _sceneRoot });
    }

    private void UpdateMazeCameraPose()
    {
        if (_mazeCamera is null)
        {
            return;
        }

        var yawRad = _cameraYawDeg * Math.PI / 180.0;
        var pitchRad = _cameraPitchDeg * Math.PI / 180.0;
        var planar = _cameraDistance * Math.Cos(pitchRad);
        var x = _cameraTarget.X + (planar * Math.Sin(yawRad));
        var z = _cameraTarget.Z + (planar * Math.Cos(yawRad));
        var y = _cameraTarget.Y + (_cameraDistance * Math.Sin(pitchRad));

        var position = new Point3D(x, y, z);
        var look = _cameraTarget - position;

        _mazeCamera.Position = position;
        _mazeCamera.LookDirection = look;
        _mazeCamera.UpDirection = new Vector3D(0, 1, 0);
    }

    private void BuildFloor()
    {
        var halfW = _mazeCols * CellSize * 0.5;
        var halfH = _mazeRows * CellSize * 0.5;

        var floor = CreateBoxMesh(-halfW, halfW, -0.04, 0.0, -halfH, halfH);
        var brush = new SolidColorBrush(Color.FromRgb(20, 38, 68));
        brush.Freeze();

        var material = new DiffuseMaterial(brush);
        material.Freeze();

        _sceneRoot.Children.Add(new GeometryModel3D
        {
            Geometry = floor,
            Material = material,
            BackMaterial = material
        });
    }

    private void BuildWalls()
    {
        var wallBrush = new SolidColorBrush(Color.FromRgb(72, 94, 126));
        var wallEmissive = new SolidColorBrush(Color.FromRgb(16, 28, 44));
        wallBrush.Freeze();
        wallEmissive.Freeze();

        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(wallBrush));
        material.Children.Add(new EmissiveMaterial(wallEmissive));
        material.Freeze();
        _wallBounds.Clear();

        var thinHalf = GetWallHalfThickness();
        var longHalf = (CellSize * 0.5) + (CellSize * WallJoinOverlapScale);

        for (var row = 0; row < _mazeRows; row++)
        {
            for (var col = 0; col < _mazeCols; col++)
            {
                if (!IsWall(row, col))
                {
                    continue;
                }

                var center = GridToWorld(row, col);
                var left = IsWall(row, col - 1);
                var right = IsWall(row, col + 1);
                var up = IsWall(row - 1, col);
                var down = IsWall(row + 1, col);

                var emitted = false;

                if (left || right)
                {
                    AddWallSegment(center.X - longHalf, center.X + longHalf, center.Y - thinHalf, center.Y + thinHalf, material);
                    emitted = true;
                }

                if (up || down)
                {
                    AddWallSegment(center.X - thinHalf, center.X + thinHalf, center.Y - longHalf, center.Y + longHalf, material);
                    emitted = true;
                }

                if (!emitted)
                {
                    // Isolated pillar fallback.
                    AddWallSegment(center.X - thinHalf, center.X + thinHalf, center.Y - thinHalf, center.Y + thinHalf, material);
                }
            }
        }
    }

    private void AddWallSegment(double xMin, double xMax, double zMin, double zMax, Material material)
    {
        var mesh = CreateBoxMesh(
            xMin,
            xMax,
            0.0,
            WallHeight,
            zMin,
            zMax);

        _sceneRoot.Children.Add(new GeometryModel3D
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = material
        });

        _wallBounds.Add(new WallBounds(xMin, xMax, zMin, zMax));
    }

    private void BuildGoal()
    {
        var goalMesh = CreateSphereMesh(new Point3D(_goalWorld.X, GoalRadius + 0.02, _goalWorld.Y), GoalRadius, 18, 12);
        var diffuseBrush = new SolidColorBrush(Color.FromRgb(42, 190, 92));
        var emissiveBrush = new SolidColorBrush(Color.FromRgb(22, 108, 58));
        diffuseBrush.Freeze();
        emissiveBrush.Freeze();

        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuseBrush));
        material.Children.Add(new EmissiveMaterial(emissiveBrush));
        material.Freeze();

        _sceneRoot.Children.Add(new GeometryModel3D
        {
            Geometry = goalMesh,
            Material = material,
            BackMaterial = material
        });
    }

    private void BuildMazeEvents()
    {
        _foodEntities.Clear();
        _hazardEntities.Clear();
        _checkpointEntities.Clear();

        _checkpointInactiveMaterial = CreateMaterial(Color.FromRgb(216, 166, 62), Color.FromRgb(74, 52, 14));
        _checkpointActiveMaterial = CreateMaterial(Color.FromRgb(84, 218, 140), Color.FromRgb(24, 86, 44));

        BuildFoodEntities();
        BuildHazardEntities();
        BuildCheckpointEntities();
    }

    private void BuildFoodEntities()
    {
        var material = CreateMaterial(Color.FromRgb(255, 210, 92), Color.FromRgb(104, 76, 22));
        for (var i = 0; i < _foodCells.Count; i++)
        {
            var (row, col) = _foodCells[i];
            if (IsWall(row, col))
            {
                continue;
            }

            var world = GridToWorld(row, col);
            var mesh = CreateSphereMesh(new Point3D(world.X, FoodRadius + 0.08, world.Y), FoodRadius, 14, 10);
            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };
            _sceneRoot.Children.Add(model);
            _foodEntities.Add(new FoodEntity($"food-{i}", world, model));
        }
    }

    private void BuildHazardEntities()
    {
        var material = CreateMaterial(Color.FromRgb(214, 84, 84), Color.FromRgb(88, 22, 22));
        for (var i = 0; i < _hazardCells.Count; i++)
        {
            var (row, col) = _hazardCells[i];
            if (IsWall(row, col))
            {
                continue;
            }

            var world = GridToWorld(row, col);
            var mesh = CreateBoxMesh(
                world.X - (HazardRadius * 0.88),
                world.X + (HazardRadius * 0.88),
                0.02,
                0.34,
                world.Y - (HazardRadius * 0.88),
                world.Y + (HazardRadius * 0.88));
            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };
            _sceneRoot.Children.Add(model);
            _hazardEntities.Add(new HazardEntity($"hazard-{i}", world, model));
        }
    }

    private void BuildCheckpointEntities()
    {
        if (_checkpointInactiveMaterial is null || _checkpointActiveMaterial is null)
        {
            return;
        }

        for (var i = 0; i < _checkpointCells.Count; i++)
        {
            var (row, col) = _checkpointCells[i];
            if (IsWall(row, col))
            {
                continue;
            }

            var world = GridToWorld(row, col);
            var mesh = CreateBoxMesh(
                world.X - (CheckpointRadius * 1.05),
                world.X + (CheckpointRadius * 1.05),
                0.02,
                0.10,
                world.Y - (CheckpointRadius * 1.05),
                world.Y + (CheckpointRadius * 1.05));
            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = _checkpointInactiveMaterial,
                BackMaterial = _checkpointInactiveMaterial
            };
            _sceneRoot.Children.Add(model);
            _checkpointEntities.Add(new CheckpointEntity($"checkpoint-{i}", world, model));
        }
    }

    private static MaterialGroup CreateMaterial(Color diffuse, Color emissive)
    {
        var diffuseBrush = new SolidColorBrush(diffuse);
        var emissiveBrush = new SolidColorBrush(emissive);
        diffuseBrush.Freeze();
        emissiveBrush.Freeze();

        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuseBrush));
        material.Children.Add(new EmissiveMaterial(emissiveBrush));
        material.Freeze();
        return material;
    }

    private void BuildAvatar()
    {
        var avatarMesh = CreateSphereMesh(new Point3D(0, 0, 0), AvatarRadius, 16, 11);
        var material = CreateMaterial(Color.FromRgb(255, 178, 54), Color.FromRgb(100, 68, 28));

        var bodyTransform = new Transform3DGroup();
        bodyTransform.Children.Add(new RotateTransform3D(_avatarYawRotation));
        bodyTransform.Children.Add(_avatarTranslate);

        _sceneRoot.Children.Add(new GeometryModel3D
        {
            Geometry = avatarMesh,
            Material = material,
            BackMaterial = material,
            Transform = bodyTransform
        });

        var headRadius = AvatarRadius * 0.62;
        var headCenter = new Point3D(0.0, AvatarRadius * 0.86, AvatarRadius * 0.34);
        var headMesh = CreateSphereMesh(headCenter, headRadius, 16, 10);
        var headMaterial = CreateMaterial(Color.FromRgb(255, 206, 92), Color.FromRgb(120, 82, 28));
        var faceMesh = CreateBoxMesh(
            -AvatarRadius * 0.09,
            AvatarRadius * 0.09,
            (AvatarRadius * 0.86) - (AvatarRadius * 0.05),
            (AvatarRadius * 0.86) + (AvatarRadius * 0.06),
            (AvatarRadius * 0.34) + headRadius - (AvatarRadius * 0.01),
            (AvatarRadius * 0.34) + headRadius + (AvatarRadius * 0.06));
        var faceMaterial = CreateMaterial(Color.FromRgb(44, 58, 88), Color.FromRgb(78, 124, 178));

        var headTransform = new Transform3DGroup();
        headTransform.Children.Add(new RotateTransform3D(_avatarHeadYawRotation, headCenter));
        headTransform.Children.Add(new RotateTransform3D(_avatarYawRotation));
        headTransform.Children.Add(_avatarTranslate);

        _sceneRoot.Children.Add(new GeometryModel3D
        {
            Geometry = headMesh,
            Material = headMaterial,
            BackMaterial = headMaterial,
            Transform = headTransform
        });

        _sceneRoot.Children.Add(new GeometryModel3D
        {
            Geometry = faceMesh,
            Material = faceMaterial,
            BackMaterial = faceMaterial,
            Transform = headTransform
        });
    }

    private static MeshGeometry3D CreateBoxMesh(double xMin, double xMax, double yMin, double yMax, double zMin, double zMax)
    {
        var p0 = new Point3D(xMin, yMin, zMin);
        var p1 = new Point3D(xMax, yMin, zMin);
        var p2 = new Point3D(xMax, yMax, zMin);
        var p3 = new Point3D(xMin, yMax, zMin);
        var p4 = new Point3D(xMin, yMin, zMax);
        var p5 = new Point3D(xMax, yMin, zMax);
        var p6 = new Point3D(xMax, yMax, zMax);
        var p7 = new Point3D(xMin, yMax, zMax);

        var mesh = new MeshGeometry3D();

        AddQuad(mesh, p0, p1, p2, p3); // Front
        AddQuad(mesh, p5, p4, p7, p6); // Back
        AddQuad(mesh, p4, p0, p3, p7); // Left
        AddQuad(mesh, p1, p5, p6, p2); // Right
        AddQuad(mesh, p3, p2, p6, p7); // Top
        AddQuad(mesh, p4, p5, p1, p0); // Bottom

        mesh.Freeze();
        return mesh;
    }

    private static MeshGeometry3D CreateSphereMesh(Point3D center, double radius, int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();

        for (var stack = 0; stack <= stacks; stack++)
        {
            var phi = Math.PI * stack / stacks;
            var y = Math.Cos(phi);
            var ring = Math.Sin(phi);

            for (var slice = 0; slice <= slices; slice++)
            {
                var theta = (2.0 * Math.PI * slice) / slices;
                var x = Math.Cos(theta) * ring;
                var z = Math.Sin(theta) * ring;
                mesh.Positions.Add(new Point3D(center.X + (radius * x), center.Y + (radius * y), center.Z + (radius * z)));
            }
        }

        var stride = slices + 1;
        for (var stack = 0; stack < stacks; stack++)
        {
            for (var slice = 0; slice < slices; slice++)
            {
                var a = (stack * stride) + slice;
                var b = a + stride;
                var c = a + 1;
                var d = b + 1;

                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(c);

                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(b);
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

        mesh.TriangleIndices.Add(index);
        mesh.TriangleIndices.Add(index + 1);
        mesh.TriangleIndices.Add(index + 2);

        mesh.TriangleIndices.Add(index);
        mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index + 3);
    }

    private void ResetAvatarPose(bool logMessage = true)
    {
        _avatarX = _startWorld.X;
        _avatarZ = _startWorld.Y;
        _avatarHeadingDeg = 0;
        _avatarHeadYawDeg = 0;
        _avatarService.PostResetMotor();
        ApplyNervousSystemSignal(new AvatarNervousSystemSignal(0.0, 0.0, 0, 0, AvatarToolSignal.None));
        _navigationResetPending = true;
        _lastNavigationDirective = "motor_stop";
        _avatarTranslate.OffsetX = _avatarX;
        _avatarTranslate.OffsetY = AvatarRadius + 0.04;
        _avatarTranslate.OffsetZ = _avatarZ;
        _avatarYawRotation.Angle = _avatarHeadingDeg;
        _avatarHeadYawRotation.Angle = _avatarHeadYawDeg;
        _lastProgressUtc = DateTime.UtcNow;
        _lastWallProximity = 0.0;
        _lastFrontProximity = 0.0;
        _lastLeftProximity = 0.0;
        _lastRightProximity = 0.0;
        _collisionEscapeHemisphere = null;
        _recentWallCollisionUtc.Clear();
        _recentWallImpactTicks.Clear();
        if (logMessage)
        {
            Log("Avatar reset to maze start.");
        }
    }

    private void ResetRun(bool logMessage = true)
    {
        _score = 0;
        _health = 100;
        _foodsCollected = 0;
        _hazardContacts = 0;
        _wallImpacts = 0;
        _consecutiveWallContacts = 0;
        _stuckRecoveries = 0;
        _hardUnstuckEvents = 0;
        _lastWallProximity = 0.0;
        _lastFrontProximity = 0.0;
        _lastLeftProximity = 0.0;
        _lastRightProximity = 0.0;
        _collisionEscapeHemisphere = null;
        _checkpointActivations = 0;
        _lastMazeEvent = "-";
        _lastHazardDamageUtc = DateTime.MinValue;
        _lastWallImpactUtc = DateTime.MinValue;
        _lastEscapeReflexUtc = DateTime.MinValue;
        _recentWallCollisionUtc.Clear();
        _recentWallImpactTicks.Clear();
        _collisionStimulusInFlight = false;
        _bodyStimulusInFlight = false;
        _lastBodyStateDispatchMs = 0;
        _respawnWorld = _startWorld;
        _totalDistanceTravelled = 0.0;
        _bestDistanceToGoal = Math.Sqrt(DistanceSquared(_startWorld.X, _startWorld.Y, _goalWorld.X, _goalWorld.Y));
        _learningSamples.Clear();
        _lastLearningSampleTick = -1;
        _lastObjectStimulusUtc.Clear();
        _lastObjectStimulusSalience.Clear();

        BuildScene();
        ResetAvatarPose(logMessage: false);
        UpdateHud();

        if (logMessage)
        {
            Log("Maze run reset: food/hazards/checkpoints restored.");
        }
    }

    private void ReconnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        _dispatchSinceMs = 0;
        _lastNeuronalMotorTick = -1;
        _lastTick = 0;
        _avatarService.PostResetMotor();
        ApplyNervousSystemSignal(new AvatarNervousSystemSignal(0.0, 0.0, 0, 0, AvatarToolSignal.None));
        _navigationResetPending = true;
        _lastNavigationDirective = "motor_stop";
        SetConnectionStatus(AvatarControlStatusText.Reconnecting(), Brushes.LightGoldenrodYellow, logOnChange: false);
        Log("Connection cursor reset. Polling frame stream from dispatch origin.");
    }

    private void ResetAvatarButton_OnClick(object sender, RoutedEventArgs e) => ResetRun();

    private void SpatialNavigationCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        _navigationResetPending = true;
        _lastNavigationDirective = "motor_stop";
        if (NavigationStatusText is not null)
        {
            NavigationStatusText.Text = "Navigation: ready to bind a new world session";
        }

        if (IsLoaded)
        {
            Log("Spatial navigator enabled. Rendered maze observations now drive brain-side navigation decisions.");
        }
    }

    private void SpatialNavigationCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _navigationResetPending = true;
        StopSpatialNavigation("Navigation: disabled; raw Control Program dispatch active");
        if (IsLoaded)
        {
            Log("Spatial navigator disabled. Raw Control Program motor dispatch restored.");
        }
    }

    private async void SendEnglishCommandButton_OnClick(object sender, RoutedEventArgs e) => await SendEnglishCommandAsync();

    private async void EnglishCommandTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await SendEnglishCommandAsync();
    }

    private async Task SendEnglishCommandAsync()
    {
        if (_englishCommandInFlight)
        {
            return;
        }

        var endpoint = ResolveEndpointUri();
        if (endpoint is null)
        {
            EnglishCommandStatusText.Text = "Command: invalid endpoint.";
            return;
        }

        var commandText = EnglishCommandTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            EnglishCommandStatusText.Text = "Command: enter an English instruction first.";
            return;
        }

        _englishCommandInFlight = true;
        SendEnglishCommandButton.IsEnabled = false;
        EnglishCommandStatusText.Text = "Command: sending to brain...";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
            var result = await AvatarControlApi.PostLanguageCommandAsync(
                _httpClient,
                endpoint,
                new AvatarLanguageCommand(commandText),
                timeout.Token);

            var directive = string.IsNullOrWhiteSpace(result.MotorDirective) ? "motor_idle" : result.MotorDirective;
            EnglishCommandStatusText.Text = $"Command: {directive}, spikes {result.DeliveredSpikes}/{result.GeneratedSpikes}";
            Log($"English command accepted: \"{TrimForLog(commandText, 80)}\" -> {directive}.");

            ApplyBrainNarration(result.Narration, forceLog: true);
        }
        catch (Exception ex)
        {
            EnglishCommandStatusText.Text = $"Command: failed ({ex.GetType().Name})";
            Log($"English command warning: {ex.GetType().Name}: {TrimForLog(ex.Message, 120)}");
        }
        finally
        {
            SendEnglishCommandButton.IsEnabled = true;
            _englishCommandInFlight = false;
        }
    }

    private void UpdateHud()
    {
        TickText.Text = $"Tick: {_lastTick}";
        DispatchText.Text = $"Dispatch spikes (motor): {_lastMotorDispatchCount}";
        MotorText.Text = $"Motor L/R: {_leftMotorDrive:0.0} / {_rightMotorDrive:0.0}";
        MoveText.Text = $"Speed/Turn: {_lastForwardSpeed:0.00} m/s | body {_lastTurnRateDeg:0.0} deg/s | head {_avatarHeadYawDeg:0.0}° | wall={_lastWallProximity:0.00}";
        PoseText.Text = $"Avatar pose: x {_avatarX:0.00}, z {_avatarZ:0.00}, body yaw {_avatarHeadingDeg:0.0}°, look {GetAvatarLookHeadingDeg():0.0}°";
        ScoreText.Text = $"Score: {_score} | Food {_foodsCollected}/{_foodEntities.Count}";
        HealthText.Text = $"Health: {_health} | Hazard contacts {_hazardContacts} | Wall impacts {_wallImpacts} | Recoveries {_stuckRecoveries} | Unsticks {_hardUnstuckEvents} | Burst {_recentWallCollisionUtc.Count}/{HardStuckCollisionBurstThreshold}";
        CheckpointText.Text = $"Checkpoint: {_checkpointActivations}/{_checkpointEntities.Count} @ ({_respawnWorld.X:0.0}, {_respawnWorld.Y:0.0})";
        EventText.Text = $"Event: {_lastMazeEvent}";
        LimbicStageText.Text = $"Limbic stage: {_limbicStage} | sal={_limbicSalience:0.00} thr={_limbicThreat:0.00}";
        LimbicDriveText.Text = $"Limbic drives: val={_limbicValence:0.00} int={_limbicInteroceptiveDrive:0.00} av={_limbicAversiveDrive:0.00} hip={_limbicHippocampalContext:0.00} rpe={_limbicRewardPredictionError:0.00} da={_limbicDopamine:0.00} ne={_limbicNorepinephrine:0.00}";
        UpdateLearningProgressHud();
    }

    private void UpdateLearningProgressHud()
    {
        CaptureLearningSample();

        const int collisionWindowTicks = 250;
        while (_recentWallImpactTicks.Count > 0 && (_lastTick - _recentWallImpactTicks.Peek()) > collisionWindowTicks)
        {
            _recentWallImpactTicks.Dequeue();
        }

        var wallImpactsInWindow = _recentWallImpactTicks.Count;
        var collisionsPer100Ticks = (wallImpactsInWindow * 100.0) / Math.Max(1.0, collisionWindowTicks);
        CollisionRateText.Text = $"Collision rate: {collisionsPer100Ticks:0.00} wall hits / 100 ticks ({wallImpactsInWindow} in last {collisionWindowTicks} ticks)";

        var startToGoalDistance = Math.Sqrt(DistanceSquared(_startWorld.X, _startWorld.Y, _goalWorld.X, _goalWorld.Y));
        var directProgress = Math.Clamp(startToGoalDistance - _bestDistanceToGoal, 0.0, Math.Max(0.001, startToGoalDistance));
        var pathEfficiency = Math.Clamp(directProgress / Math.Max(0.001, _totalDistanceTravelled), 0.0, 1.0);
        PathEfficiencyText.Text = $"Path efficiency: {pathEfficiency:P1} | traveled={_totalDistanceTravelled:0.0}m | best goal distance={_bestDistanceToGoal:0.0}m";

        var trend = CalculateRewardTrendPer100Ticks();
        var trendLabel = trend switch
        {
            > 1.0 => "improving",
            < -1.0 => "declining",
            _ => "stable"
        };
        RewardTrendText.Text = $"Reward trend: {trend:+0.00;-0.00;0.00} score / 100 ticks ({trendLabel})";
    }

    private void CaptureLearningSample()
    {
        if (_lastTick == _lastLearningSampleTick)
        {
            return;
        }

        _lastLearningSampleTick = _lastTick;
        _learningSamples.Enqueue(new LearningSample(_lastTick, _score));

        const int maxTrendWindowTicks = 800;
        while (_learningSamples.Count > 0 && (_lastTick - _learningSamples.Peek().Tick) > maxTrendWindowTicks)
        {
            _learningSamples.Dequeue();
        }
    }

    private double CalculateRewardTrendPer100Ticks()
    {
        if (_learningSamples.Count < 2)
        {
            return 0.0;
        }

        var first = _learningSamples.Peek();
        var last = first;
        foreach (var sample in _learningSamples)
        {
            last = sample;
        }

        var dt = Math.Max(1, last.Tick - first.Tick);
        var ds = last.Score - first.Score;
        return (ds * 100.0) / dt;
    }

    private void ApplyConfiguredEndpointSelection()
    {
        var configuredEndpoint = ResolveConfiguredControlEndpoint();
        EndpointComboBox.Text = configuredEndpoint;

        ComboBoxItem? matchingItem = null;
        foreach (var item in EndpointComboBox.Items)
        {
            if (item is not ComboBoxItem comboItem)
            {
                continue;
            }

            if (!AvatarEndpointResolver.TryNormalizeEndpoint(comboItem.Content?.ToString(), out var normalizedItem))
            {
                continue;
            }
            if (string.Equals(normalizedItem, configuredEndpoint, StringComparison.OrdinalIgnoreCase))
            {
                matchingItem = comboItem;
                break;
            }
        }

        if (matchingItem is null)
        {
            matchingItem = new ComboBoxItem { Content = configuredEndpoint };
            EndpointComboBox.Items.Insert(0, matchingItem);
        }

        EndpointComboBox.SelectedItem = matchingItem;
    }

    private static string ResolveConfiguredControlEndpoint()
    {
        return AvatarControlEndpointSettings.ResolveConfiguredEndpoint();
    }

    private Uri? ResolveEndpointUri()
    {
        if (EndpointComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var selectedUri = AvatarEndpointResolver.ResolveUri(selectedItem.Content?.ToString());
            if (selectedUri is not null)
            {
                return selectedUri;
            }
        }

        return AvatarEndpointResolver.ResolveUri(EndpointComboBox.Text);
    }

    private void SetConnectionStatus(string message, Brush foreground, bool logOnChange = true)
    {
        ConnectionStatusText.Text = message;
        ConnectionStatusText.Foreground = foreground;

        if (logOnChange && !string.Equals(message, _lastConnectionMessage, StringComparison.Ordinal))
        {
            Log(message);
        }

        _lastConnectionMessage = message;
    }

    private bool _logTextInitialized;

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logLines.Add(line);

        var trimmed = _logLines.Count > MaxLogLines;
        if (trimmed)
        {
            _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
        }

        if (!_logTextInitialized)
        {
            LogTextBox.Text = line;
            _logTextInitialized = true;
        }
        else if (trimmed)
        {
            LogTextBox.Text = string.Join(Environment.NewLine, _logLines);
        }
        else
        {
            LogTextBox.AppendText(Environment.NewLine + line);
        }

        LogTextBox.ScrollToEnd();
    }

    private void UpdateBrainNarrationFromState(JsonElement stateElement)
    {
        if (AvatarControlApi.TryReadBrainNarration(stateElement, out var narration))
        {
            ApplyBrainNarration(narration, forceLog: false);
        }
    }

    private void ApplyBrainNarration(AvatarBrainNarration narration, bool forceLog)
    {
        if (!narration.HasText)
        {
            return;
        }

        BrainNarrationText.Text = $"Brain narration: {narration.Utterance}";
        var changed = narration.Sequence != _lastBrainNarrationSequence ||
                      !string.Equals(narration.Utterance, _lastBrainNarrationText, StringComparison.Ordinal);
        if (forceLog || changed)
        {
            Log($"Brain narration: {TrimForLog(narration.Utterance, 110)}");
        }

        _lastBrainNarrationSequence = narration.Sequence;
        _lastBrainNarrationText = narration.Utterance;
    }

    private static string TrimForLog(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length <= maxLength ? cleaned : $"{cleaned[..maxLength]}...";
    }

    private void SetMazeEvent(string message)
    {
        if (string.Equals(_lastMazeEvent, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastMazeEvent = message;
        Log(message);
    }

    private static double DistanceSquared(double x1, double z1, double x2, double z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return (dx * dx) + (dz * dz);
    }

    private Point GridToWorld(int row, int col)
    {
        var x = (col - (_mazeCols / 2.0) + 0.5) * CellSize;
        var z = (row - (_mazeRows / 2.0) + 0.5) * CellSize;
        return new Point(x, z);
    }

    private (int Row, int Col) WorldToGrid(double x, double z)
    {
        var col = (int)Math.Floor((x / CellSize) + (_mazeCols / 2.0));
        var row = (int)Math.Floor((z / CellSize) + (_mazeRows / 2.0));
        return (row, col);
    }

    private bool IsWall(int row, int col)
    {
        if (row < 0 || row >= _mazeRows || col < 0 || col >= _mazeCols)
        {
            return true;
        }

        return _mazeLayout[row][col] == '#';
    }

    private bool IsNavigationDirectionOpen(int row, int column, int headingQuarter)
    {
        (int dr, int dc) = NavigationCoordinateFrame.DirectionDelta(headingQuarter);
        return !IsWall(row + dr, column + dc);
    }

    private double GetAvatarLookHeadingDeg() => NormalizeDegrees(_avatarHeadingDeg + _avatarHeadYawDeg);

    private double GetAvatarLookHeadingRad() => GetAvatarLookHeadingDeg() * Math.PI / 180.0;

    private static double MoveTowards(double current, double target, double maxDelta)
    {
        var delta = target - current;
        if (Math.Abs(delta) <= maxDelta)
        {
            return target;
        }

        return current + (Math.Sign(delta) * maxDelta);
    }

    private static double NormalizeDegrees(double value)
    {
        while (value > 180.0)
        {
            value -= 360.0;
        }

        while (value < -180.0)
        {
            value += 360.0;
        }

        return value;
    }

    private static string ParseAnyStructureId(JsonElement element, string property) => AvatarJson.ParseAnyStructureId(element, property);
    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value) => AvatarJson.TryGetProperty(element, propertyName, out value);
    private static string GetString(JsonElement element, params string[] names) => AvatarJson.GetString(element, names);
    private static long GetLong(JsonElement element, params string[] names) => AvatarJson.GetLong(element, names);
    private static int GetInt(JsonElement element, params string[] names) => AvatarJson.GetInt(element, names);
    private static double GetDouble(JsonElement element, params string[] names) => AvatarJson.GetDouble(element, names);
    private static bool GetBool(JsonElement element, params string[] names) => AvatarJson.GetBool(element, names);
    private static string NormalizeHemisphere(string value) => AvatarJson.NormalizeHemisphere(value);

    private static double NormalizeRadians(double value)
    {
        while (value > Math.PI)
        {
            value -= 2.0 * Math.PI;
        }

        while (value < -Math.PI)
        {
            value += 2.0 * Math.PI;
        }

        return value;
    }

    private sealed class FoodEntity(string id, Point world, GeometryModel3D model)
    {
        public string Id { get; } = id;
        public Point World { get; } = world;
        public GeometryModel3D Model { get; } = model;
        public bool Collected { get; set; }
    }

    private sealed class HazardEntity(string id, Point world, GeometryModel3D model)
    {
        public string Id { get; } = id;
        public Point World { get; } = world;
        public GeometryModel3D Model { get; } = model;
    }

    private sealed class CheckpointEntity(string id, Point world, GeometryModel3D model)
    {
        public string Id { get; } = id;
        public Point World { get; } = world;
        public GeometryModel3D Model { get; } = model;
        public bool Activated { get; set; }
    }

    private readonly record struct RayHit(bool HitWall, double Distance);
    private readonly record struct VisionSprite(Point World, Color Color, double SizeScale);
    private readonly record struct WallBounds(double XMin, double XMax, double ZMin, double ZMax);
    private readonly record struct LearningSample(long Tick, int Score);
    private readonly record struct VisualStimulusDispatchResult(int Generated, int Delivered, int TargetCount);
    private readonly record struct ObjectStimulusDispatchResult(int Generated, int Delivered, int VisibleObjects, int Errors);
    private readonly record struct ObjectObservation(
        string ObjectId,
        string Label,
        string Hemisphere,
        double Salience,
        double Confidence,
        double Intensity,
        int BurstCount,
        double DistanceMeters);
}








