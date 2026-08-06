using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Text.Json;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Input;
using System.Windows.Threading;
using AvalonDock.Layout;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NAudio.Wave;
using NRE.SimAvatar;
using CV = OpenCvSharp;
using Cv2 = OpenCvSharp.Cv2;

namespace NRE.WpfEditor;

public partial class MainWindow : Window
{
    private const double CorticalShellMedialRollDeg = 0.0;
    private static readonly double CorticalShellMedialRollCos = Math.Cos(CorticalShellMedialRollDeg * (Math.PI / 180.0));
    private static readonly double CorticalShellMedialRollSin = Math.Sin(CorticalShellMedialRollDeg * (Math.PI / 180.0));

    private readonly AxisAngleRotation3D _yawRotation = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _pitchRotation = new(new Vector3D(1, 0, 0), 0);
    private readonly ScaleTransform3D _globalPulseScale = new(1, 1, 1);
    private readonly ScaleTransform3D _sceneScale = new(1.35, 1.35, 1.35);
    private readonly DispatcherTimer _densityDebounceTimer = new();
    private readonly DispatcherTimer _cameraFitDebounceTimer = new();
    private readonly DispatcherTimer _minWakeDebounceTimer = new();
    private readonly DispatcherTimer _sleepPressureDebounceTimer = new();
    private readonly DispatcherTimer _autoProfileDebounceTimer = new();
    private readonly DispatcherTimer _sensoryHealthTimer = new();
    private readonly ConcurrentQueue<InputDelta> _inputQueue = new();
    private readonly SemaphoreSlim _inputSignal = new(0);
    private readonly CancellationTokenSource _workerCts = new();
    private readonly HttpClient _httpClient = NreHttpClientFactory.CreateDefault();
    private readonly VisualInputDispatchClient _visualInputClient;
    private readonly AvatarService _avatarService = new(EditorNervousSystemOptions, "NRE.Editor.AvatarService");
    private readonly Uri[] _snapshotBaseUris = BuildSnapshotBaseUris();
    private readonly Random _random = new(42);
    private readonly Dictionary<string, StructureVisual> _structureVisuals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<StructureVisual>> _structureVisualsByBaseId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _displayToSnapshotId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, StructureStatusBadge> _structureStatusBadges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PathwayVisual> _pathwayVisuals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<PathwayVisual>> _pathwayVisualsByBasePair = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point3D> _structureAnchorPoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Point3D> _cameraFitSamplePoints = [];
    private readonly HashSet<SolidColorBrush> _activeSpikeNeuronBrushes = [];
    private readonly List<SolidColorBrush> _expiredSpikeNeuronBrushes = new(256);
    private readonly List<PathwayVisual> _activePathwayVisuals = new(512);
    private CorpusCallosumVisual? _corpusCallosumVisual;
    private IReadOnlyList<PathwayDefinition>? _pathwayDefinitionsCache;

    private DateTime _animationStartUtc;
    private DateTime _lastSnapshotUtc = DateTime.MinValue;
    private DateTime _lastInspectorRefreshUtc = DateTime.MinValue;
    private bool _isSnapshotPolling;
    private bool _isDragging;
    private bool _presetTransformsLocked = true;
    private bool _isApplyingPresetView;
    private bool _suppressViewMenuEvents;
    private bool _anatomyDisplayMode = true;
    private bool _displayModeControlsReady;
    private Point _lastMousePosition;
    private const double BaseSceneScale = 1.35;
    private const double MinSceneZoom = 0.18;
    private const double MaxSceneZoom = 2.8;
    private double _sceneZoom = 1.0;
    private double _targetYaw;
    private double _targetPitch;
    private double _targetZoom = 1.0;
    private int _renderDispatchPending;
    private int _uiOverrunCount;

    // Adaptive frame rate: the render loop ticks at 10 Hz while anything is
    // visibly animating, and drops to 4 Hz when no spike brushes or pathway
    // visuals are active. Set via MarkVisualDirty whenever ApplySnapshotNeuronActivity
    // lights a brush or pathway, and cleared by ApplyVisualDecay when the active
    // lists drain to empty.
    private int _visualActivity;
    private static readonly TimeSpan ActiveRenderInterval = TimeSpan.FromMilliseconds(100.0);
    private static readonly TimeSpan IdleRenderInterval = TimeSpan.FromMilliseconds(250.0);
    private int _displayNeuronGridEdge = 36;
    private int _displayNeuronsPerHemisphereBudget = 36 * 36 * 36;
    private int _minWakeTicks = 220;
    private float _sleepPressureEnterThreshold = 0.68f;
    private string _lastRenderStatus = string.Empty;
    private string _lastServiceHealthSummary = string.Empty;
    private string _pendingServiceHealthSummary = string.Empty;
    private int _pendingServiceHealthSummaryCount;
    private string _lastOutputMessage = string.Empty;
    private DateTime _lastOutputMessageUtc = DateTime.MinValue;
    private readonly Dictionary<string, DateTime> _lastOutputMessageByText = new(StringComparer.Ordinal);
    private DateTime _lastFramePayloadUtc = DateTime.MinValue;
    private DateTime _lastFramePollWarningUtc = DateTime.MinValue;
    private DateTime _lastFrameFallbackPollUtc = DateTime.MinValue;
    private DateTime _lastFramePollFailureLogUtc = DateTime.MinValue;
    private int _framePollConsecutiveFailures;
    private bool _framePollCursorResetApplied;
    private Task? _controlWorkerTask;
    private Task? _renderWorkerTask;
    private Task? _framePollTask;
    private DateTime _lastStatusBadgeRefreshUtc = DateTime.MinValue;
    private DateTime _lastTransportStatsRefreshUtc = DateTime.MinValue;
    private DateTime _lastFallbackHealthProbeUtc = DateTime.MinValue;
    private DateTime _lastVerifiedControlProbeUtc = DateTime.MinValue;
    private DateTime _lastFrameTelemetryPaneQueueUtc = DateTime.MinValue;
    private string _lastTransportStatsText = string.Empty;
    private string _lastBrainDashboardText = string.Empty;
    private string _lastInhabitanceText = string.Empty;
    private string _lastCircuitAuditText = string.Empty;
    private string _lastReasoningText = string.Empty;
    private string _lastLanguageCommandTelemetryText = string.Empty;
    private DateTime _lastUiInputUtc = DateTime.UtcNow;
    private long _lastRemoteOutputLogWallClockMs;
    private long _lastRemoteSpikeLogWallClockMs;
    private long _lastRemoteDispatchWallClockMs;
    private readonly Queue<string> _outputLogLines = new();
    private readonly HashSet<string> _unmatchedSpikeDiagnostics = new(StringComparer.Ordinal);
    private Dictionary<string, ServiceHealthEntry>? _lastServiceTelemetrySnapshot;
    private DateTime _lastServiceTelemetrySnapshotUtc = DateTime.MinValue;
    private readonly HashSet<string> _structureRestartInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastStructureRestartUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, SolidColorBrush> _statusBadgeBrushes = new();
    private readonly object _audioMetricsGate = new();
    private readonly object _webcamStimulusGate = new();
    private readonly Channel<string> _speechQueue = Channel.CreateBounded<string>(new BoundedChannelOptions(128)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly object _endpointStateGate = new();
    private readonly SemaphoreSlim _endpointResolutionGate = new(1, 1);
    private readonly PaneWorker _transportStatsPaneWorker = new("NRE.Editor.Pane.TransportStats");
    private readonly PaneWorker _brainDashboardPaneWorker = new("NRE.Editor.Pane.BrainDashboard");
    private readonly PaneWorker _inhabitancePaneWorker = new("NRE.Editor.Pane.Inhabitance");
    private readonly PaneWorker _circuitAuditPaneWorker = new("NRE.Editor.Pane.CircuitAudit");
    private readonly PaneWorker _reasoningPaneWorker = new("NRE.Editor.Pane.Reasoning");
    private bool _simRestartInFlight;
    private bool _perfProfileSwitchInFlight;
    private bool _minWakeUpdateInFlight;
    private bool _sleepPressureUpdateInFlight;
    private bool _autoProfileUpdateInFlight;
    private bool _inputGatesUpdateInFlight;
    private bool _suppressMinWakeSliderEvents;
    private bool _suppressSleepPressureSliderEvents;
    private bool _suppressAutoProfileControlEvents;
    private bool _suppressInputGatesControlEvents;
    private bool _webcamInputInFlight;
    private bool _microphoneInputInFlight;
    private bool _languageInputInFlight;
    private bool _speechOutputEnabled = true;
    private bool _suppressSpeechUiEvents;
    private bool _microphoneLanguageRouteAvailable = true;
    private volatile bool _isSimulationSleeping;
    private string _visualAttentionFocusField = "neutral";
    private string _visualAttentionFocusHemisphere = "M";
    private double _visualAttentionFocusConfidence;
    private bool _webcamRunning;
    private bool _webcamStimulusInFlight;
    private bool _webcamStimulusPending;
    private bool _microphoneRunning;
    private bool _sensoryHealthCheckInFlight;
    private bool _visualRouteRecoveryInFlight;
    private bool _suppressReasoningControlEvents;
    private bool _reasoningApplyPlanningInFlight;
    private bool _reasoningApplyCurriculumInFlight;
    private bool _reasoningApplyConsolidationInFlight;
    private bool _reasoningCounterfactualInFlight;
    private int _shutdownRequested;
    private bool _shutdownComplete;
    private bool _shutdownInFlight;
    private int _v1RouteConsecutiveFailures;
    private int _webcamFrameEdgePx = DefaultWebcamFrameEdgePx;
    private long _webcamFrameCount;
    private long _webcamStimulusDroppedCount;
    private long _webcamStimulusSentCount;
    private DateTime _lastSimRestartUtc = DateTime.MinValue;
    private DateTime _lastPerfProfileSwitchUtc = DateTime.MinValue;
    private DateTime _lastMinWakeUpdateUtc = DateTime.MinValue;
    private DateTime _lastSleepPressureUpdateUtc = DateTime.MinValue;
    private DateTime _lastAutoProfileUpdateUtc = DateTime.MinValue;
    private DateTime _lastInputGatesUpdateUtc = DateTime.MinValue;
    private DateTime _lastWebcamStimulusUtc = DateTime.MinValue;
    private DateTime _lastWebcamFrameUtc = DateTime.MinValue;
    private DateTime _lastWebcamPreviewUiUtc = DateTime.MinValue;
    private DateTime _lastMicrophoneStimulusUtc = DateTime.MinValue;
    private DateTime _lastMicrophoneRecoveryUtc = DateTime.MinValue;
    private DateTime _lastWebcamWatchdogRecoveryUtc = DateTime.MinValue;
    private DateTime _lastMicrophoneWatchdogRecoveryUtc = DateTime.MinValue;
    private DateTime _lastV1RouteRecoveryUtc = DateTime.MinValue;
    private DateTime _lastV1RouteSuccessUtc = DateTime.MinValue;
    private DateTime _lastV1RouteFailureUtc = DateTime.MinValue;
    private DateTime _lastLanguageInputUtc = DateTime.MinValue;
    private DateTime _lastSpeechUtc = DateTime.MinValue;
    private DateTime _lastBrainNarrationSpeechUtc = DateTime.MinValue;
    private DateTime _lastLanguageUtteranceUtc = DateTime.MinValue;
    private string _lastLanguageUtterance = "hello world";
    private string _lastSpokenPhrase = string.Empty;
    private long _languageUtteranceSequence;
    private long _lastBrainNarrationSequence;
    private long _lastSpokenLanguageUtteranceSequence;
    private long _lastSpeechDispatchWallClockMs;
    private int _speechVolume = 95;
    private int _speechRatePercent = 100;
    private int _speechMinDispatchSpikes = SpeechDefaultMinDispatchSpikes;
    private SpeechTriggerMode _speechTriggerMode = SpeechTriggerMode.LanguagePathway;
    private CancellationTokenSource? _webcamCts;
    private CancellationTokenSource? _microphoneCts;
    private Thread? _speechThread;
    private Task? _webcamTask;
    private Task? _microphoneTask;
    private double _audioRmsEwma;
    private double _audioZcrEwma;
    private double _audioLevelEwma;
    private double _pendingWebcamMotionSignal;
    private double _pendingWebcamLuminanceSignal;
    private double _pendingWebcamLeftSaliencySignal = 0.5;
    private double _pendingWebcamRightSaliencySignal = 0.5;
    private DateTime _lastMicrophoneDataUtc = DateTime.MinValue;
    private DateTime _lastMicrophoneMeterUiUtc = DateTime.MinValue;
    private Uri? _preferredControlBaseUri;
    private Uri? _verifiedControlBaseUri;
    private int _controlEndpointFailureCount;
    private static readonly TimeSpan VerifiedControlProbeInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan FallbackHealthProbeInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StructureRestartCooldown = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan SimulationRestartCooldown = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan PerfProfileSwitchCooldown = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MinWakeUpdateCooldown = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan SleepPressureUpdateCooldown = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan AutoProfileUpdateCooldown = TimeSpan.FromMilliseconds(650);
    private static readonly TimeSpan InputGatesUpdateCooldown = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan WebcamStimulusInterval = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan WebcamPreviewUiInterval = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan MicrophoneStimulusInterval = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan LanguageInputCooldown = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SpeechCooldown = TimeSpan.FromMilliseconds(6000);
    private static readonly TimeSpan BrainNarrationSpeechCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SpeechDuplicateSuppression = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan LanguageUtteranceRetention = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PassiveLanguageUtteranceUpdateCooldown = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan WebcamSignalStallTimeout = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan WebcamHardReconnectTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan MicrophoneSignalStallTimeout = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan MicrophoneRecoveryCooldown = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan SensoryHealthPollInterval = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan WebcamWatchdogRecoveryCooldown = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MicrophoneWatchdogRecoveryCooldown = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan V1RouteRecoveryCooldown = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan V1RouteStallWarningTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OutputDuplicateSuppressionWindow = TimeSpan.FromSeconds(5);
    private const int DefaultWebcamFrameEdgePx = 196;
    private const int MaxOutputLogLines = 400;
    private static readonly TimeSpan ControlEndpointGraceFallbackWindow = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan FramePollWarningCooldown = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ServiceHealthTelemetryCacheWindow = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan FrameFallbackPollInterval = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan FramePollTimeout = TimeSpan.FromMilliseconds(12000);
    private static readonly TimeSpan FramePollFailureLogCooldown = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan FramePollOnlyLoopDelay = TimeSpan.FromMilliseconds(550);
    private const int FramePollCursorResetThreshold = 4;
    private const int FramePollMaxOutputLog = 40;
    private const int FramePollMaxSpikeLog = 40;
    private const int FramePollMaxDispatchSpikes = 1024;
    private const int ControlEndpointFailureThreshold = 10;
    private const int SpeechDefaultMinDispatchSpikes = 12;
    private const int WebcamReadFailureWarnThreshold = 30;
    private const int WebcamReadFailureReconnectThreshold = 250;
    private const int V1RouteRecoveryFailureThreshold = 3;
    private const int MaxNeuronHighlightsPerStructurePerFrame = 1024;
    private const int MaxPathwayActivationsPerFrame = 160;
    private const double MicrophoneUtterancePromoteRmsThreshold = 0.045;
    private const double MicrophoneUtterancePromoteZcrThreshold = 0.06;
    private static readonly TimeSpan UiInputRenderYieldWindow = TimeSpan.FromMilliseconds(80);
    private static readonly AvatarNervousSystemOptions EditorNervousSystemOptions = new(
        new AvatarKinematicsOptions(
            MaxMotorDrive: 240.0,
            ForwardSpeedCoefficient: 0.0125,
            TurnSpeedCoefficient: 3.2,
            MinForwardSpeed: 0.0,
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0),
        IdleMotorFallbackTicks: int.MaxValue);
    private static readonly HashSet<string> SpeechLanguageStructures = new(StringComparer.OrdinalIgnoreCase)
    {
        "BrocaBa44Ba45",
        "WernickePstgPsts",
        "ArcuateFasciculus",
        "SupramarginalAngular",
        "TemporalAssociation",
        "Pfc",
        "Ppc"
    };

    private static Uri[] BuildSnapshotBaseUris()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uris = new List<Uri>(10);

        void AddIfValid(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
            {
                return;
            }

            if (uri.Scheme is not ("http" or "https"))
            {
                return;
            }

            var key = uri.AbsoluteUri.TrimEnd('/');
            if (seen.Add(key))
            {
                uris.Add(uri);
            }
        }

        var configured = Environment.GetEnvironmentVariable("NRE_CONTROL_ENDPOINTS")
            ?? Environment.GetEnvironmentVariable("CONTROLPROGRAM_BASE_URLS")
            ?? Environment.GetEnvironmentVariable("CONTROLPROGRAM_BASE_URL");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            foreach (var token in configured.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                AddIfValid(token);
            }
        }

        AddIfValid("http://localhost:5080");
        AddIfValid("http://localhost:5000");
        AddIfValid("http://127.0.0.1:5080");
        AddIfValid("http://127.0.0.1:5000");
        AddIfValid("https://localhost:5081");
        AddIfValid("https://localhost:5001");
        AddIfValid("https://127.0.0.1:5081");
        AddIfValid("https://127.0.0.1:5001");

        return uris.ToArray();
    }

    public MainWindow()
    {
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _visualInputClient = new VisualInputDispatchClient(_httpClient);
        InitializeComponent();
        _displayModeControlsReady = true;
        BuildDockContent();
        ApplyDefaultPanelVisibility();
        SyncViewMenuItemStates();
        BuildBrainScene();
        StartWorkers();
        StartFramePolling();
        _densityDebounceTimer.Interval = TimeSpan.FromMilliseconds(220);
        _densityDebounceTimer.Tick += (_, _) =>
        {
            _densityDebounceTimer.Stop();
            BuildBrainScene();
        };
        _cameraFitDebounceTimer.Interval = TimeSpan.FromMilliseconds(90);
        _cameraFitDebounceTimer.Tick += (_, _) =>
        {
            _cameraFitDebounceTimer.Stop();
            ApplyAutoFitCamera();
        };
        _minWakeDebounceTimer.Interval = TimeSpan.FromMilliseconds(280);
        _minWakeDebounceTimer.Tick += async (_, _) =>
        {
            _minWakeDebounceTimer.Stop();
            await ApplyMinWakeTicksAsync(_minWakeTicks);
        };
        _sleepPressureDebounceTimer.Interval = TimeSpan.FromMilliseconds(280);
        _sleepPressureDebounceTimer.Tick += async (_, _) =>
        {
            _sleepPressureDebounceTimer.Stop();
            await ApplySleepPressureEnterThresholdAsync(_sleepPressureEnterThreshold);
        };
        _autoProfileDebounceTimer.Interval = TimeSpan.FromMilliseconds(380);
        _autoProfileDebounceTimer.Tick += async (_, _) =>
        {
            _autoProfileDebounceTimer.Stop();
            await SafeHandlerAsync(() => ApplyAutoProfileControlsAsync(), "Auto-profile debounce");
        };
        _sensoryHealthTimer.Interval = SensoryHealthPollInterval;
        _sensoryHealthTimer.Tick += async (_, _) => await SafeHandlerAsync(SensoryHealthTimerTickAsync, "Sensory health poll");
        _sensoryHealthTimer.Start();
        Loaded += MainWindow_OnLoaded;
        BrainViewport.SizeChanged += (_, _) => ScheduleCameraAutoFit();
        InputManager.Current.PreProcessInput += InputManager_PreProcessInput;
        Closing += MainWindow_OnClosing;
        _ = Dispatcher.InvokeAsync(async () => await SafeHandlerAsync(() => RefreshAutoProfileControlsFromControlAsync(), "Refresh auto-profile controls"), DispatcherPriority.Background);
        _ = Dispatcher.InvokeAsync(async () => await SafeHandlerAsync(() => RefreshInputGatesControlsFromControlAsync(), "Refresh input gates"), DispatcherPriority.Background);
    }

    private void MainWindow_OnLoaded(object? sender, RoutedEventArgs e)
    {
        // First pass after load plus a deferred pass after full layout settle.
        ScheduleCameraAutoFit(immediate: true);
        _ = Dispatcher.InvokeAsync(() => ScheduleCameraAutoFit(immediate: true), DispatcherPriority.ContextIdle);
    }

    private void ApplyDefaultPanelVisibility()
    {
        // Startup layout: keep only core panes visible.
        var hiddenByDefault = new LayoutAnchorable?[]
        {
            SensoryIoAnchorable,
            TransportStatsAnchorable,
            ReasoningAnchorable,
            SpikeLogAnchorable,
            InspectorAnchorable
        };

        foreach (var pane in hiddenByDefault)
        {
            pane?.Hide();
        }

        StructuresAnchorable?.Show();
        OutputAnchorable?.Show();
        ControlBarAnchorable?.Show();

        if (OutputAnchorable is not null)
        {
            OutputAnchorable.IsSelected = true;
        }
    }

    private void InputManager_PreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e?.StagingItem?.Input is MouseEventArgs or KeyboardEventArgs)
        {
            _lastUiInputUtc = DateTime.UtcNow;
        }
    }

    private async void FileOpenNetworkMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open DNNE Network State",
            Filter = "DNNE Network State (*.nre)|*.nre|JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var payload = await File.ReadAllTextAsync(dialog.FileName);
            if (string.IsNullOrWhiteSpace(payload))
            {
                AddOutputLog($"Network load failed: '{dialog.FileName}' is empty.");
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                AddOutputLog("Network load skipped: Control Program endpoint not available.");
                return;
            }

            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(new Uri(baseUri, "/api/v1/admin/network/import"), content, cts.Token);
            var responsePayload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                AddOutputLog(
                    $"Network load failed ({Path.GetFileName(dialog.FileName)}): HTTP {(int)response.StatusCode}. {TrimForStatus(responsePayload, 220)}");
                return;
            }

            NoteControlEndpointSuccess(baseUri);
            AddOutputLog($"Network loaded from {dialog.FileName}.");
            SetRenderStatus("Render: connected (awaiting first snapshot)");
            _lastRemoteOutputLogWallClockMs = 0;
            _lastRemoteSpikeLogWallClockMs = 0;
            _lastRemoteDispatchWallClockMs = 0;
            await RefreshTransportStatsPanelAsync(baseUri);
            await RefreshSleepMemoryControlsFromControlAsync(baseUri);
            await RefreshAutoProfileControlsFromControlAsync(baseUri);
            await RefreshInputGatesControlsFromControlAsync(baseUri);
        }
        catch (Exception ex)
        {
            AddOutputLog($"Network load failed: {ex.Message}");
        }
    }

    private async void FileSaveNetworkMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save DNNE Network State",
            Filter = "DNNE Network State (*.nre)|*.nre|JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".nre",
            AddExtension = true,
            FileName = $"dnne-network-{DateTime.Now:yyyyMMdd-HHmmss}.nre",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                AddOutputLog("Network save skipped: Control Program endpoint not available.");
                return;
            }

            using var response = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/admin/network/export"), cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                AddOutputLog($"Network save failed: HTTP {(int)response.StatusCode}. {TrimForStatus(payload, 220)}");
                return;
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                AddOutputLog("Network save failed: export payload was empty.");
                return;
            }

            await File.WriteAllTextAsync(dialog.FileName, payload, cts.Token);
            NoteControlEndpointSuccess(baseUri);
            AddOutputLog($"Network saved to {dialog.FileName}.");
        }
        catch (Exception ex)
        {
            AddOutputLog($"Network save failed: {ex.Message}");
        }
    }

    private void FileExitMenuItem_OnClick(object sender, RoutedEventArgs e) => Close();

    private void EditCopyOutputMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (OutputLogTextBox is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(OutputLogTextBox.Text ?? string.Empty);
            AddOutputLog("Output panel text copied to clipboard.");
        }
        catch (Exception ex)
        {
            AddOutputLog($"Unable to copy output text: {ex.Message}");
        }
    }

    private void EditClearOutputMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        _outputLogLines.Clear();
        _lastOutputMessage = string.Empty;
        _lastOutputMessageUtc = DateTime.MinValue;
        _lastOutputMessageByText.Clear();
        if (OutputLogTextBox is not null)
        {
            OutputLogTextBox.Clear();
        }
    }

    private void ViewPanelMenuItem_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressViewMenuEvents)
        {
            return;
        }

        SetViewPanelVisibility(sender, isVisible: true);
    }

    private void ViewPanelMenuItem_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressViewMenuEvents)
        {
            return;
        }

        SetViewPanelVisibility(sender, isVisible: false);
    }

    private void SetViewPanelVisibility(object sender, bool isVisible)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string viewTag)
        {
            return;
        }

        var panel = ResolveViewPanelByTag(viewTag);
        if (panel is null)
        {
            return;
        }

        if (isVisible)
        {
            panel.Show();
        }
        else
        {
            panel.Hide();
        }

        SyncViewMenuItemStates();
    }

    private LayoutAnchorable? ResolveViewPanelByTag(string viewTag)
    {
        return viewTag switch
        {
            "Structures" => StructuresAnchorable,
            "SensoryIo" => SensoryIoAnchorable,
            "TransportStats" => TransportStatsAnchorable,
            "BrainDashboard" => BrainDashboardAnchorable,
            "Inhabitance" => InhabitanceAnchorable,
            "CircuitAudit" => CircuitAuditAnchorable,
            "Reasoning" => ReasoningAnchorable,
            "SpikeLog" => SpikeLogAnchorable,
            "Output" => OutputAnchorable,
            "ControlBar" => ControlBarAnchorable,
            "Inspector" => InspectorAnchorable,
            _ => null
        };
    }

    private void SyncViewMenuItemStates()
    {
        _suppressViewMenuEvents = true;
        try
        {
            SyncViewMenuItem(ViewStructuresMenuItem, StructuresAnchorable);
            SyncViewMenuItem(ViewSensoryIoMenuItem, SensoryIoAnchorable);
            SyncViewMenuItem(ViewTransportStatsMenuItem, TransportStatsAnchorable);
            SyncViewMenuItem(ViewBrainDashboardMenuItem, BrainDashboardAnchorable);
            SyncViewMenuItem(ViewInhabitanceMenuItem, InhabitanceAnchorable);
            SyncViewMenuItem(ViewCircuitAuditMenuItem, CircuitAuditAnchorable);
            SyncViewMenuItem(ViewReasoningMenuItem, ReasoningAnchorable);
            SyncViewMenuItem(ViewSpikeLogMenuItem, SpikeLogAnchorable);
            SyncViewMenuItem(ViewOutputMenuItem, OutputAnchorable);
            SyncViewMenuItem(ViewControlBarMenuItem, ControlBarAnchorable);
            SyncViewMenuItem(ViewInspectorMenuItem, InspectorAnchorable);
        }
        finally
        {
            _suppressViewMenuEvents = false;
        }
    }

    private static void SyncViewMenuItem(MenuItem? menuItem, LayoutAnchorable? panel)
    {
        if (menuItem is null || panel is null)
        {
            return;
        }

        menuItem.IsChecked = panel.IsVisible && !panel.IsHidden;
    }

    private void BuildDockContent()
    {
        var groups = new[]
        {
            (Name: "Neocortex - Visual and Sensory", Nodes: new[] { "V1", "V2", "V3", "V4", "MT", "A1", "Auditory Association", "S1", "S2", "Insula" }),
            (Name: "Neocortex - Frontal and Motor", Nodes: new[] { "PFC", "Dorsomedial PFC", "Ventromedial PFC", "Orbitofrontal Cortex", "Frontal Eye Fields", "Broca (BA44/45)", "Premotor Cortex", "SMA", "M1", "ACC", "Midcingulate Cortex" }),
            (Name: "Neocortex - Temporal and Parietal", Nodes: new[] { "Wernicke (pSTG/pSTS)", "Supramarginal/Angular", "Temporoparietal Junction", "PPC", "Precuneus", "Temporal Association", "Inferotemporal Cortex", "Fusiform Gyrus", "Temporal Pole", "Posterior Cingulate", "Retrosplenial Cortex", "Arcuate Fasciculus" }),
            (Name: "Telencephalon - Medial Temporal", Nodes: new[] { "EC", "DG", "CA3", "CA2", "CA1", "Subiculum", "Presubiculum", "Parasubiculum", "Parahippocampal Cortex", "Perirhinal Cortex", "Olfactory Bulb" }),
            (Name: "Telencephalon - Commissural/Basal/Limbic", Nodes: new[] { "Corpus Callosum", "Striatum", "Nucleus Accumbens", "Globus Pallidus", "Ventral Pallidum", "GPe", "GPi", "Amygdala", "ACC", "Basal Forebrain" }),
            (Name: "Peripheral Sensory Interface", Nodes: new[] { "Retina", "Cochlea" }),
            (Name: "Diencephalon", Nodes: new[] { "Thalamus", "Motor Thalamus", "TRN", "Pulvinar", "Mediodorsal Thalamus", "Intralaminar Thalamus", "Hypothalamus", "Habenula", "STN" }),
            (Name: "Mesencephalon", Nodes: new[] { "Superior Colliculus", "Inferior Colliculus", "Periaqueductal Gray", "SNr", "SNc", "VTA" }),
            (Name: "Metencephalon", Nodes: new[] { "Cochlear Nucleus", "Superior Olive", "Vestibular Nuclei", "Granule Layer", "Purkinje Layer", "Cerebellar Vermis", "Cerebellar Lobules", "DCN", "Pons" }),
            (Name: "Myelencephalon", Nodes: new[] { "Nucleus Tractus Solitarius", "Reticular Formation", "Inferior Olive", "Medulla", "LC", "Raphe", "Spinal Cord Motor" })
        };

        foreach (var group in groups)
        {
            var root = new TreeViewItem { Header = group.Name, IsExpanded = true, Foreground = Brushes.LightSteelBlue };
            foreach (var node in group.Nodes)
            {
                var snapshotId = ResolveSnapshotIdFromDisplay(node);
                _displayToSnapshotId[node] = snapshotId;
                var leaf = new TreeViewItem
                {
                    Foreground = Brushes.WhiteSmoke,
                    Tag = new StructureTreeNode(node, snapshotId),
                    Header = BuildStructureHeader(node, snapshotId)
                };
                root.Items.Add(leaf);
            }
            StructuresTree.Items.Add(root);
        }

        StructuresTree.SelectedItemChanged += (_, _) =>
        {
            if (StructuresTree.SelectedItem is not TreeViewItem item || item.Tag is not StructureTreeNode nodeMeta)
            {
                return;
            }

            RefreshSelectedStructureInspector(force: true);
            AddSpikeLog($"Inspector focused on {nodeMeta.DisplayName}");
        };
        StructuresTree.MouseDoubleClick += async (_, _) => await TryRestartSelectedOffStructureAsync();

        SelectionNameText.Text = "Structure: none selected";
        SelectionModelText.Text = "Neuron Model: -";
        SelectionPlasticityText.Text = "Plasticity: -";
        SelectionMicrotubuleText.Text = "Experimental: intracellular microtubule approximation - waiting for live diagnostics";
        TransportStatsTextBox.Text = "Waiting for /api/v1/frame ...";
        InhabitanceTextBox.Text = "Waiting for inhabitance telemetry ...";
        ReasoningTextBox.Text = "Waiting for reasoning telemetry ...";
        ReasoningCounterfactualResultTextBox.Text = "Counterfactual result will appear here.";
        ApplyPresetTransformLock(LockPresetTransformsCheckBox?.IsChecked ?? true);
        NeuronBudgetSlider.Value = _displayNeuronGridEdge;
        NeuronBudgetText.Text = FormatNeuronBudgetLabel(_displayNeuronGridEdge);
        SetMinWakeTicksUi(_minWakeTicks, syncedFromRuntime: false);
        SetSleepPressureEnterUi(_sleepPressureEnterThreshold, syncedFromRuntime: false);
        UpdateAutoProfileControlLabels();
        AutoProfileStatusText.Text = "Auto profile: awaiting runtime settings";
        SetInputGatesControlsUi(new InputGateControlSettings(AvatarVisionEnabled: true, SpontaneousSpikingEnabled: true), syncedFromRuntime: false);
        InputGatesStatusText.Text = "Input gates: awaiting runtime settings";
        _webcamFrameEdgePx = ResolveSelectedWebcamFrameEdgePx();
        UpdateWebcamPreviewViewportSize(_webcamFrameEdgePx);
        WebcamStatusText.Text = $"Webcam: idle ({_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
        SetWebcamPreviewUnavailable("Avatar sight unavailable");
        MicrophoneStatusText.Text = "Microphone: idle";
        UpdateMicrophoneLevelMeterUi(0, isActive: false);
        LanguageInputStatusText.Text = "Language: idle";
        SetLanguageCommandTelemetryText("Brain telemetry: awaiting runtime state");
        SetInputHealthIndicator(WebcamHealthLight, WebcamHealthText, InputHealthState.Idle, "Webcam pipeline: inactive");
        SetInputHealthIndicator(MicrophoneHealthLight, MicrophoneHealthText, InputHealthState.Idle, "Microphone pipeline: inactive");
        SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Idle, "V1 route: awaiting webcam input");
        UpdateAvatarSelfDiagnosticsPanel();
        InitializeSpeechControlsUi();
        UpdateReasoningSliderLabels();
        SetRenderStatus("Render: initializing 3D scene");
    }

    private void RefreshSelectedStructureInspector(bool force = false)
    {
        if (StructuresTree.SelectedItem is not TreeViewItem item || item.Tag is not StructureTreeNode nodeMeta)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!force && (now - _lastInspectorRefreshUtc) < TimeSpan.FromMilliseconds(250))
        {
            return;
        }

        _lastInspectorRefreshUtc = now;

        var displayName = nodeMeta.DisplayName;
        var snapshotId = nodeMeta.SnapshotId;

        if (_structureVisualsByBaseId.TryGetValue(snapshotId, out var visuals) && visuals.Count > 0)
        {
            var visual = visuals[0];
            var avgRate = ComputeMeanFiringRate(visuals);
            SelectionNameText.Text = $"Structure: {displayName} ({snapshotId})";
            SelectionModelText.Text = $"Neuron Model: {visual.NeuronModel}";
            SelectionPlasticityText.Text = $"Plasticity: {visual.Plasticity}. Firing: {avgRate:0.0} Hz";
            SelectionMicrotubuleText.Text = BuildAtlasInspectorText(snapshotId) + Environment.NewLine +
                BuildStructureDiagnosticsInspectorText(visual.Microtubules, visual.BodySchema, visual.BasalGanglia, visual.Cerebellar, visual.VestibuloReticular, visual.SuperiorColliculus, visual.HippocampalSpatial, visual.SalienceAffect, visual.PrefrontalWorkingMemory, visual.ThalamicAttentionGate, visual.HypothalamicHomeostasis, visual.SleepWakeArousal, visual.DescendingDefense, visual.DopamineReward, visual.SeptohippocampalTheta, visual.SpinalProprioceptive, visual.OlfactoryLimbicMemory, visual.AuditoryLanguageMotor, visual.VisualObjectRecognition, snapshotId);
        }
        else
        {
            SelectionNameText.Text = $"Structure: {displayName}";
            SelectionModelText.Text = "Neuron Model: -";
            SelectionPlasticityText.Text = "Plasticity: -";
            SelectionMicrotubuleText.Text = BuildAtlasInspectorText(snapshotId) + Environment.NewLine +
                BuildStructureDiagnosticsInspectorText(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, snapshotId);
        }
    }

    private static string BuildAtlasInspectorText(string snapshotId)
    {
        if (!SubcorticalAtlasProfiles.TryGetValue(snapshotId, out var profile))
        {
            return "Atlas geometry: cortical surface registration";
        }

        static string FormatGeometry(string label, AtlasGeometry geometry) =>
            $"{label} centre {geometry.CenterMm.X:0.0}, {geometry.CenterMm.Y:0.0}, {geometry.CenterMm.Z:0.0} mm; " +
            $"extent {geometry.DimensionsMm.X:0.0} x {geometry.DimensionsMm.Y:0.0} x {geometry.DimensionsMm.Z:0.0} mm";

        return profile.IsMidline
            ? $"Atlas geometry: {FormatGeometry("M", profile.Left)}. Source: {profile.Left.Source}."
            : $"Atlas geometry: {FormatGeometry("L", profile.Left)}; {FormatGeometry("R", profile.Right)}. Source: {profile.Left.Source}.";
    }

    private FrameworkElement BuildStructureHeader(string displayName, string snapshotId)
    {
        var label = new TextBlock
        {
            Text = displayName,
            Foreground = Brushes.WhiteSmoke,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var badgeText = new TextBlock
        {
            Text = "INIT",
            Foreground = Brushes.White,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(69, 85, 115)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 1, 6, 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = badgeText,
            ToolTip = "Awaiting service telemetry"
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(label);
        Grid.SetColumn(label, 0);
        grid.Children.Add(badge);
        Grid.SetColumn(badge, 1);

        _structureStatusBadges[snapshotId] = new StructureStatusBadge(snapshotId, displayName, badge, badgeText);
        return grid;
    }

    private static double ComputeMeanFiringRate(IReadOnlyList<StructureVisual> visuals)
    {
        if (visuals.Count == 0)
        {
            return 0.0;
        }

        var total = 0.0;
        for (var i = 0; i < visuals.Count; i++)
        {
            total += visuals[i].MeanFiringRateHz;
        }

        return total / visuals.Count;
    }

    private static void TryFreeze(Freezable freezable)
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }
    }

    private static string ResolveSnapshotIdFromDisplay(string displayName) => displayName switch
    {
        "Olfactory Bulb" => "OlfactoryBulb",
        "Corpus Callosum" => "CorpusCallosum",
        "V2" => "V2",
        "V3" => "V3",
        "V4" => "V4",
        "MT" => "Mt",
        "TRN" => "Trn",
        "Retina" => "Retina",
        "Cochlea" => "Cochlea",
        "Cochlear Nucleus" => "CochlearNucleus",
        "Superior Olive" => "SuperiorOlive",
        "Inferior Colliculus" => "InferiorColliculus",
        "Vestibular Nuclei" => "VestibularNuclei",
        "Nucleus Tractus Solitarius" => "NucleusTractusSolitarius",
        "Reticular Formation" => "ReticularFormation",
        "Periaqueductal Gray" => "PeriaqueductalGray",
        "Spinal Cord Motor" => "SpinalCordMotor",
        "Motor Thalamus" => "MotorThalamus",
        "Pulvinar" => "Pulvinar",
        "Mediodorsal Thalamus" => "MediodorsalThalamus",
        "Intralaminar Thalamus" => "IntralaminarThalamus",
        "Superior Colliculus" => "SuperiorColliculus",
        "EC" => "EntorhinalCortex",
        "DG" => "DentateGyrus",
        "CA2" => "CA2",
        "Presubiculum" => "Presubiculum",
        "Parasubiculum" => "Parasubiculum",
        "Parahippocampal Cortex" => "ParahippocampalCortex",
        "Perirhinal Cortex" => "PerirhinalCortex",
        "Auditory Association" => "AuditoryAssociationCortex",
        "S2" => "SecondarySomatosensoryCortex",
        "Wernicke (pSTG/pSTS)" => "WernickePstgPsts",
        "Supramarginal/Angular" => "SupramarginalAngular",
        "Temporoparietal Junction" => "TemporoparietalJunction",
        "Precuneus" => "Precuneus",
        "Arcuate Fasciculus" => "ArcuateFasciculus",
        "Broca (BA44/45)" => "BrocaBa44Ba45",
        "PFC" => "Pfc",
        "Dorsomedial PFC" => "DorsomedialPrefrontalCortex",
        "Ventromedial PFC" => "VentromedialPrefrontalCortex",
        "Frontal Eye Fields" => "FrontalEyeFields",
        "Premotor Cortex" => "PremotorCortex",
        "Orbitofrontal Cortex" => "OrbitofrontalCortex",
        "Insula" => "Insula",
        "Posterior Cingulate" => "PosteriorCingulate",
        "Retrosplenial Cortex" => "RetrosplenialCortex",
        "Inferotemporal Cortex" => "InferotemporalCortex",
        "Fusiform Gyrus" => "FusiformGyrus",
        "Temporal Pole" => "TemporalPole",
        "Midcingulate Cortex" => "MidcingulateCortex",
        "Nucleus Accumbens" => "NucleusAccumbens",
        "Ventral Pallidum" => "VentralPallidum",
        "GPe" => "GPe",
        "GPi" => "GPi",
        "Habenula" => "Habenula",
        "Hypothalamus" => "Hypothalamus",
        "PPC" => "Ppc",
        "ACC" => "Acc",
        "DCN" => "DeepCerebellarNuclei",
        "Cerebellar Vermis" => "CerebellarVermis",
        "Cerebellar Lobules" => "CerebellarLobules",
        "Pons" => "Pons",
        "Medulla" => "Medulla",
        "Granule Layer" => "CerebellarGranule",
        "Purkinje Layer" => "PurkinjeCellLayer",
        "LC" => "LocusCoeruleus",
        "Raphe" => "RapheNuclei",
        "Basal Forebrain" => "BasalForebrain",
        "VTA" => "Vta",
        _ => displayName.Replace(" ", string.Empty, StringComparison.Ordinal)
    };

    private void BrainDisplayMode_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radio || radio.Tag is not string mode)
        {
            return;
        }

        _anatomyDisplayMode = mode.Equals("Anatomy", StringComparison.OrdinalIgnoreCase);
        if (!_displayModeControlsReady)
        {
            return;
        }

        BuildBrainScene();
        AddOutputLog($"Brain display mode: {(_anatomyDisplayMode ? "Anatomy" : "Circuit")}");
    }

    private void BuildBrainScene()
    {
        var sceneBuildTimer = Stopwatch.StartNew();
        BrainViewport.Children.Clear();
        _structureVisuals.Clear();
        _structureVisualsByBaseId.Clear();
        _pathwayVisuals.Clear();
        _pathwayVisualsByBasePair.Clear();
        _structureAnchorPoints.Clear();
        _cameraFitSamplePoints.Clear();
        _activeSpikeNeuronBrushes.Clear();
        _activePathwayVisuals.Clear();
        _corpusCallosumVisual = null;

        var root = new Model3DGroup();
        root.Children.Add(new AmbientLight(Color.FromRgb(52, 50, 62)));
        root.Children.Add(new DirectionalLight(Color.FromRgb(255, 226, 216), new Vector3D(-1.0, -1.15, -1.35)));
        root.Children.Add(new DirectionalLight(Color.FromRgb(112, 146, 210), new Vector3D(1.15, 0.35, -0.20)));
        root.Children.Add(new DirectionalLight(Color.FromRgb(150, 178, 236), new Vector3D(0.10, 0.80, 1.35)));

        var brainContent = new Model3DGroup();
        var combined = new Transform3DGroup();
        combined.Children.Add(_globalPulseScale);
        combined.Children.Add(_sceneScale);
        combined.Children.Add(new RotateTransform3D(_pitchRotation));
        combined.Children.Add(new RotateTransform3D(_yawRotation));
        brainContent.Transform = combined;
        // Keep the corpus callosum recognizable without covering the deep nuclei.
        _corpusCallosumVisual = _anatomyDisplayMode
            ? null
            : AddCorpusCallosumPathwayScaffold(brainContent);
        var centers = new Dictionary<string, Point3D>(StringComparer.OrdinalIgnoreCase);
        var atlasCenters = new Dictionary<string, Point3D>(StringComparer.OrdinalIgnoreCase);
        var atlasGeometryByInstance = new Dictionary<string, AtlasGeometry>(StringComparer.OrdinalIgnoreCase);
        var renderedDimensionsByInstance = new Dictionary<string, Vector3D>(StringComparer.OrdinalIgnoreCase);
        var sampledWorldPointsByInstance = new Dictionary<string, List<Point3D>>(StringComparer.OrdinalIgnoreCase);
        // Neural markers deliberately use a low-poly soma. This keeps the spatial
        // sampling legible while avoiding the 60-vertex ellipsoid cost per neuron.
        var neuronMesh = BuildNeuronMarkerMesh(0.0065);
        var spikeNeuronMesh = BuildNeuronMarkerMesh(0.010);
        TryFreeze(neuronMesh);
        TryFreeze(spikeNeuronMesh);
        var definitions = GetStructureDefinitions().ToList();
        var requiredSnapshotIds = _displayToSnapshotId.Values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var knownSnapshotIds = new HashSet<string>(definitions.Select(d => d.SnapshotId), StringComparer.OrdinalIgnoreCase);
        var missingSnapshotIds = requiredSnapshotIds.Where(id => !knownSnapshotIds.Contains(id)).ToList();
        for (var i = 0; i < missingSnapshotIds.Count; i++)
        {
            definitions.Add(CreateFallbackStructureDefinition(missingSnapshotIds[i], i));
        }

        var layoutBySnapshotId = definitions
            .GroupBy(d => d.SnapshotId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var def = g.First();
                    return GetEffectiveStructureLayout(def.SnapshotId, def.Layout);
                },
                StringComparer.OrdinalIgnoreCase);

        var uniqueStructureColors = BuildUniqueStructureColorMap(definitions);

        foreach (var def in definitions)
        {
            _displayToSnapshotId[def.DisplayName] = def.SnapshotId;
            var effectiveLayout = GetEffectiveStructureLayout(def.SnapshotId, def.Layout);
            var targetNeuronCount = GetTargetNeuronCountPerHemisphere(def.SnapshotId);
            var hemispheres = IsBilaterallyDuplicated(def.SnapshotId) ? new[] { "L", "R" } : new[] { "M" };
            var localPointSets = new Dictionary<(double X, double Y, double Z, double Pitch, double Yaw, double Roll), List<Point3D>>();
            foreach (var hemi in hemispheres)
            {
                var instanceDefinition = effectiveLayout == StructureLayout.CorticalSheet
                    ? def
                    : ApplySubcorticalAtlasGeometry(def, hemi);
                var pointSetKey = (
                    instanceDefinition.RadiusX,
                    instanceDefinition.RadiusY,
                    instanceDefinition.RadiusZ,
                    instanceDefinition.PitchDeg,
                    instanceDefinition.YawDeg,
                    instanceDefinition.RollDeg);
                if (!localPointSets.TryGetValue(pointSetKey, out var baseLocalPoints))
                {
                    var generatedPoints = GenerateNeuronMatrix(instanceDefinition, effectiveLayout, targetNeuronCount);
                    baseLocalPoints = effectiveLayout == StructureLayout.CorticalSheet
                        ? generatedPoints.ToList()
                        : generatedPoints
                            .Select(p => RotateLocalPoint(
                                p,
                                instanceDefinition.PitchDeg,
                                instanceDefinition.YawDeg,
                                instanceDefinition.RollDeg))
                            .ToList();
                    localPointSets[pointSetKey] = baseLocalPoints;
                }

                var hemiCenter = GetHemisphereCenter(instanceDefinition.Center, hemi);
                var center = effectiveLayout == StructureLayout.CorticalSheet
                    ? GetCorticalHemisphereCenter(hemi)
                    : hemiCenter;
                center = GetEnforcedAtlasCenter(def.SnapshotId, hemi, center, effectiveLayout);
                var instanceId = $"{hemi}_{def.SnapshotId}";
                var orientedLocalPoints = baseLocalPoints;
                AtlasGeometry? atlasGeometry = null;
                if (TryGetSubcorticalAtlasGeometry(def.SnapshotId, hemi, out var measuredGeometry))
                {
                    atlasGeometry = measuredGeometry;
                    atlasGeometryByInstance[instanceId] = measuredGeometry;
                }
                var renderBaseColor = uniqueStructureColors[def.SnapshotId];
                // Every registered structure remains visible in both display
                // modes. Anatomy mode changes context and materials; it must
                // not remove the neural structures being inspected.
                var renderStructure = true;

                var baseDim = ScaleColor(renderBaseColor, 0.22);
                var diffuse = new SolidColorBrush(baseDim)
                {
                    Opacity = effectiveLayout == StructureLayout.CorticalSheet ? 0.075 : 0.12
                };
                var emissive = new SolidColorBrush(Color.FromRgb(baseDim.R, baseDim.G, baseDim.B))
                {
                    Opacity = effectiveLayout == StructureLayout.CorticalSheet ? 0.018 : 0.030
                };
                var material = new MaterialGroup();
                material.Children.Add(new DiffuseMaterial(diffuse));
                material.Children.Add(new EmissiveMaterial(emissive));
                material.Children.Add(NeuralStructureSpecularMaterial);

                var cluster = new Model3DGroup();
                var displayLocalPoints = orientedLocalPoints
                    .Select(p => hemi == "L"
                        ? new Point3D(-p.X, p.Y, p.Z)
                        : p)
                    .ToList();

                if (effectiveLayout == StructureLayout.CorticalSheet)
                {
                    // Cortical points are generated directly on the anatomical shell.
                    // Additional anchoring/reprojection distorts preset views and can remove ventral cortex.
                    displayLocalPoints = displayLocalPoints.ToList();
                }

                var anchor = ComputeAnchorPoint(center, displayLocalPoints);
                centers[instanceId] = anchor;
                atlasCenters[instanceId] = center;
                if (displayLocalPoints.Count > 0)
                {
                    var localBounds = ComputeLocalBounds(displayLocalPoints);
                    renderedDimensionsByInstance[instanceId] = new Vector3D(
                        localBounds.MaxX - localBounds.MinX,
                        localBounds.MaxY - localBounds.MinY,
                        localBounds.MaxZ - localBounds.MinZ);
                }
                _structureAnchorPoints[instanceId] = anchor;
                var sampledPoints = SampleWorldPoints(displayLocalPoints, center, 40);
                sampledWorldPointsByInstance[instanceId] = sampledPoints;
                _cameraFitSamplePoints.AddRange(sampledPoints);

                if (renderStructure)
                {
                    if (effectiveLayout == StructureLayout.CorticalSheet)
                    {
                        AddCorticalGyrusSurface(cluster, def.SnapshotId, hemi, renderBaseColor);
                        AddHomuncularCorticalBands(cluster, def.SnapshotId, hemi);
                    }
                    else
                    {
                        AddDeepCircuitReferenceSurfaces(
                            cluster,
                            def.SnapshotId,
                            hemi,
                            renderBaseColor,
                            displayLocalPoints,
                            atlasGeometry);
                    }

                    foreach (var batchedNeuronMesh in BuildRepeatedMeshes(neuronMesh, displayLocalPoints, maxCentersPerMesh: 1000))
                    {
                        if (batchedNeuronMesh.Positions.Count > 0)
                        {
                            TryFreeze(batchedNeuronMesh);
                            cluster.Children.Add(new GeometryModel3D(batchedNeuronMesh, material)
                            {
                                BackMaterial = material
                            });
                        }
                    }
                }

                var spikeBrushes = new List<SolidColorBrush>();
                var spikeBase = BoostSpikeColor(renderBaseColor);
                var isCorticalDisplay = effectiveLayout == StructureLayout.CorticalSheet;
                var requestedSpikeCapacity = (int)Math.Round(displayLocalPoints.Count * (isCorticalDisplay ? 0.115 : 0.070));
                var spikeMarkerCapacity = renderStructure
                    ? Math.Min(displayLocalPoints.Count, Math.Clamp(requestedSpikeCapacity, isCorticalDisplay ? 96 : 48, isCorticalDisplay ? 896 : 384))
                    : 0;
                var spikeIndices = SelectSpikeNeuronIndices(displayLocalPoints, spikeMarkerCapacity, $"{def.SnapshotId}_{hemi}");
                var spikeSurfaceLift = effectiveLayout == StructureLayout.CorticalSheet ? 0.020 : 0.007;
                foreach (var spikeIndex in spikeIndices)
                {
                    var spikePoint = displayLocalPoints[spikeIndex];
                    var spikeOffset = new Vector3D(spikePoint.X, spikePoint.Y, spikePoint.Z);
                    if (spikeOffset.LengthSquared > 1e-9)
                    {
                        spikeOffset.Normalize();
                        spikePoint = new Point3D(
                            spikePoint.X + (spikeOffset.X * spikeSurfaceLift),
                            spikePoint.Y + (spikeOffset.Y * spikeSurfaceLift),
                            spikePoint.Z + (spikeOffset.Z * spikeSurfaceLift));
                    }
                    var spikeBrush = new SolidColorBrush(Color.FromArgb(0, spikeBase.R, spikeBase.G, spikeBase.B))
                    {
                        Opacity = 0.0
                    };
                    var spikeMaterial = new MaterialGroup();
                    spikeMaterial.Children.Add(new DiffuseMaterial(spikeBrush));
                    spikeMaterial.Children.Add(new EmissiveMaterial(spikeBrush));
                    cluster.Children.Add(new GeometryModel3D(spikeNeuronMesh, spikeMaterial)
                    {
                        Transform = new TranslateTransform3D(spikePoint.X, spikePoint.Y, spikePoint.Z)
                    });
                    spikeBrushes.Add(spikeBrush);
                }

                var clusterScale = new ScaleTransform3D(1, 1, 1);
                var clusterTransform = new Transform3DGroup();
                clusterTransform.Children.Add(clusterScale);
                clusterTransform.Children.Add(new TranslateTransform3D(center.X, center.Y, center.Z));
                cluster.Transform = clusterTransform;
                brainContent.Children.Add(cluster);

                var structureVisual = new StructureVisual(def.DisplayName, def.SnapshotId, hemi, def.NeuronModel, def.Plasticity, renderBaseColor, spikeBase, diffuse, emissive, clusterScale, spikeBrushes);
                _structureVisuals[instanceId] = structureVisual;
                if (!_structureVisualsByBaseId.TryGetValue(def.SnapshotId, out var byBase))
                {
                    byBase = [];
                    _structureVisualsByBaseId[def.SnapshotId] = byBase;
                }
                byBase.Add(structureVisual);
            }
        }

        // WPF 3D transparency is order-sensitive. Draw the closed reference
        // shells after neural structures so their depth buffer cannot hide the
        // anatomy they are meant to contextualize. Pathways follow the shell
        // and therefore remain crisp above it.
        AddAnatomicalReferenceSurfaces(brainContent, _anatomyDisplayMode);

        foreach (var pathway in LoadPathwayDefinitions())
        {
            foreach (var pairing in GetHemispherePathwayPairs(pathway.SourceId, pathway.TargetId, pathway.ProjectionType))
            {
                if (!centers.TryGetValue(pairing.SourceInstance, out var s) || !centers.TryGetValue(pairing.TargetInstance, out var t))
                {
                    continue;
                }

                var mesh = BuildTubeMesh(s, t, pathway.IsFeedback ? 0.0075 : 0.0105, 8);
                TryFreeze(mesh);
                var baseColor = GetPathwayColor(pathway.Neurotransmitter);
                var diffuse = new SolidColorBrush(baseColor);
                var emissive = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255));
                diffuse.Opacity = 0;
                emissive.Opacity = 0;
                var material = new MaterialGroup();
                material.Children.Add(new DiffuseMaterial(diffuse));
                material.Children.Add(new EmissiveMaterial(emissive));
                brainContent.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });

                var key = $"{pairing.SourceInstance}>{pairing.TargetInstance}";
                var pathwayVisual = new PathwayVisual(pathway.SourceId, pathway.TargetId, pairing.Hemisphere, pathway.Neurotransmitter, baseColor, diffuse, emissive);
                _pathwayVisuals[key] = pathwayVisual;

                var baseKey = PathwayKey(pathway.SourceId, pathway.TargetId);
                if (!_pathwayVisualsByBasePair.TryGetValue(baseKey, out var baseList))
                {
                    baseList = [];
                    _pathwayVisualsByBasePair[baseKey] = baseList;
                }
                baseList.Add(pathwayVisual);
            }
        }

        // Draw sulci after circuit parcels so the anatomical boundaries remain
        // legible in both Anatomy and Circuit display modes.
        AddAnatomicalLandmarks(brainContent);
        root.Children.Add(brainContent);
        BrainViewport.Children.Add(new ModelVisual3D { Content = root });
        ScheduleCameraAutoFit();
        ReportAnatomicalValidation(
            sampledWorldPointsByInstance,
            atlasCenters,
            atlasGeometryByInstance,
            renderedDimensionsByInstance,
            layoutBySnapshotId);
        sceneBuildTimer.Stop();
        SetRenderStatus(
            $"Render: scene rebuilt in {sceneBuildTimer.ElapsedMilliseconds:N0} ms " +
            $"({_structureVisuals.Count} structures, {_pathwayVisuals.Count} pathways)");
        if (missingSnapshotIds.Count > 0)
        {
            AddOutputLog($"Render: added {missingSnapshotIds.Count} fallback structure visuals for missing definitions ({string.Join(", ", missingSnapshotIds)})");
        }
    }

    private void StartWorkers()
    {
        _animationStartUtc = DateTime.UtcNow;
        StartSpeechWorker();
        _controlWorkerTask = Task.Run(() => ControlWorkerLoopAsync(_workerCts.Token));
        _renderWorkerTask = Task.Run(() => RenderWorkerLoopAsync(_workerCts.Token));
    }

    private void ReportAnatomicalValidation(
        IReadOnlyDictionary<string, List<Point3D>> sampledWorldPointsByInstance,
        IReadOnlyDictionary<string, Point3D> centers,
        IReadOnlyDictionary<string, AtlasGeometry> atlasGeometryByInstance,
        IReadOnlyDictionary<string, Vector3D> renderedDimensionsByInstance,
        IReadOnlyDictionary<string, StructureLayout> layoutBySnapshotId)
    {
        if (sampledWorldPointsByInstance.Count == 0)
        {
            return;
        }

        // Sulcal depth, gyral crown relief, and cortical thickness together can
        // legitimately depart from the smooth reference shell by roughly 16 mm.
        var corticalShellTolerance = MmToRender(16.0);
        const double nonCorticalEnvelopeTolerance = 0.205;

        var corticalInstances = 0;
        var corticalSamples = 0;
        var corticalOffShell = 0;
        var nonCorticalSamples = 0;
        var nonCorticalOutsideEnvelope = 0;

        foreach (var (instanceId, samples) in sampledWorldPointsByInstance)
        {
            if (!TrySplitInstanceId(instanceId, out var hemisphere, out var snapshotId))
            {
                continue;
            }

            layoutBySnapshotId.TryGetValue(snapshotId, out var layout);
            if (layout == StructureLayout.CorticalSheet)
            {
                corticalInstances++;
                foreach (var sample in samples)
                {
                    corticalSamples++;
                    var shellPoint = ProjectPointToCorticalShell(sample, hemisphere);
                    if ((sample - shellPoint).Length > corticalShellTolerance)
                    {
                        corticalOffShell++;
                    }
                }
            }
            else if (ShouldBeInsideCerebralEnvelope(snapshotId))
            {
                foreach (var sample in samples)
                {
                    nonCorticalSamples++;
                    var envelopeHemisphere = sample.X < 0.0 ? "L" : "R";
                    var shellPoint = ProjectPointToCorticalShell(
                        new Point3D(sample.X < 0.0 ? -Math.Abs(sample.X) : Math.Abs(sample.X), sample.Y, sample.Z),
                        envelopeHemisphere);
                    var shellRadius = Math.Sqrt((shellPoint.X * shellPoint.X) + (shellPoint.Y * shellPoint.Y) + (shellPoint.Z * shellPoint.Z));
                    var sampleRadius = Math.Sqrt((sample.X * sample.X) + (sample.Y * sample.Y) + (sample.Z * sample.Z));
                    if (sampleRadius > (shellRadius + nonCorticalEnvelopeTolerance))
                    {
                        nonCorticalOutsideEnvelope++;
                    }
                }
            }
        }

        var bilateralPairs = 0;
        var bilateralMisalignment = 0;
        foreach (var snapshotId in layoutBySnapshotId.Keys)
        {
            if (layoutBySnapshotId[snapshotId] == StructureLayout.CorticalSheet ||
                SubcorticalAtlasProfiles.ContainsKey(snapshotId))
            {
                // Cortical clusters are centred on the shared origin, while atlas
                // nuclei are validated against their measured asymmetric centres.
                continue;
            }

            var leftKey = $"L_{snapshotId}";
            var rightKey = $"R_{snapshotId}";
            if (!centers.TryGetValue(leftKey, out var leftCenter) || !centers.TryGetValue(rightKey, out var rightCenter))
            {
                continue;
            }

            bilateralPairs++;
            var mirrored = Math.Sign(leftCenter.X) != Math.Sign(rightCenter.X) &&
                           Math.Abs(Math.Abs(leftCenter.X) - Math.Abs(rightCenter.X)) <= 0.125 &&
                           Math.Abs(leftCenter.Y - rightCenter.Y) <= 0.125 &&
                           Math.Abs(leftCenter.Z - rightCenter.Z) <= 0.125;
            if (!mirrored)
            {
                bilateralMisalignment++;
            }
        }

        var atlasCenterChecks = 0;
        var atlasCenterDrift = 0;
        foreach (var (instanceId, center) in centers)
        {
            if (!TrySplitInstanceId(instanceId, out var hemisphere, out var snapshotId) ||
                !layoutBySnapshotId.TryGetValue(snapshotId, out var layout) ||
                layout == StructureLayout.CorticalSheet ||
                !TryGetSubcorticalAtlasCenterMm(snapshotId, out _))
            {
                continue;
            }

            atlasCenterChecks++;
            var expected = GetCanonicalAtlasCenter(snapshotId, hemisphere);
            if ((center - expected).Length > MmToRender(0.25))
            {
                atlasCenterDrift++;
            }
        }

        var atlasExtentChecks = 0;
        var atlasExtentOverflow = 0;
        foreach (var (instanceId, geometry) in atlasGeometryByInstance)
        {
            if (!renderedDimensionsByInstance.TryGetValue(instanceId, out var renderedDimensions))
            {
                continue;
            }

            atlasExtentChecks++;
            var expected = new Vector3D(
                MmToRender(geometry.DimensionsMm.X),
                MmToRender(geometry.DimensionsMm.Y),
                MmToRender(geometry.DimensionsMm.Z));
            var tolerance = MmToRender(1.0);
            if (renderedDimensions.X > expected.X + tolerance ||
                renderedDimensions.Y > expected.Y + tolerance ||
                renderedDimensions.Z > expected.Z + tolerance)
            {
                atlasExtentOverflow++;
            }
        }

        var corticalOffShellRatio = corticalSamples > 0 ? (corticalOffShell / (double)corticalSamples) : 0.0;
        var nonCorticalOutsideRatio = nonCorticalSamples > 0 ? (nonCorticalOutsideEnvelope / (double)nonCorticalSamples) : 0.0;
        var status = (corticalOffShellRatio <= 0.08 &&
                      nonCorticalOutsideRatio <= 0.08 &&
                      bilateralMisalignment == 0 &&
                      atlasCenterDrift == 0 &&
                      atlasExtentOverflow == 0)
            ? "OK"
            : "WARN";

        AddOutputLog(
            $"Render anatomy validation ({status}): cortical off-shell {corticalOffShell}/{Math.Max(1, corticalSamples)} " +
            $"({corticalOffShellRatio:P1}), non-cortical out-of-envelope {nonCorticalOutsideEnvelope}/{Math.Max(1, nonCorticalSamples)} " +
            $"({nonCorticalOutsideRatio:P1}), mirror mismatches {bilateralMisalignment}/{Math.Max(1, bilateralPairs)}, " +
            $"atlas centre drift {atlasCenterDrift}/{Math.Max(1, atlasCenterChecks)}, " +
            $"atlas extent overflow {atlasExtentOverflow}/{Math.Max(1, atlasExtentChecks)}, cortical instances {corticalInstances}. " +
            $"Profiles: {string.Join(", ", atlasGeometryByInstance.Values.Select(g => g.Source).Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal))}.");
    }

    private static bool ShouldBeInsideCerebralEnvelope(string snapshotId)
    {
        return snapshotId switch
        {
            "Retina" or "Cochlea" or "OlfactoryBulb" or
            "CerebellarGranule" or "PurkinjeCellLayer" or "CerebellarVermis" or "CerebellarLobules" or "DeepCerebellarNuclei" or
            "Pons" or "Medulla" or "SpinalCordMotor" or "ReticularFormation" or "InferiorOlive" or "LocusCoeruleus" or "RapheNuclei" or
            "CochlearNucleus" or "SuperiorOlive" or "VestibularNuclei" or "NucleusTractusSolitarius" => false,
            _ => true
        };
    }

    private static void AddAnatomicalReferenceSurfaces(Model3DGroup brainContent, bool anatomyDisplayMode)
    {
        var cortexDiffuse = anatomyDisplayMode
            ? Color.FromArgb(48, 184, 128, 138)
            : Color.FromArgb(32, 118, 106, 122);
        var cortexEmissive = anatomyDisplayMode
            ? Color.FromArgb(2, 236, 170, 180)
            : Color.FromArgb(2, 236, 170, 180);
        var cerebellumDiffuse = anatomyDisplayMode
            ? Color.FromArgb(48, 168, 108, 120)
            : Color.FromArgb(32, 158, 102, 114);
        var brainstemDiffuse = anatomyDisplayMode
            ? Color.FromArgb(48, 176, 126, 106)
            : Color.FromArgb(32, 166, 116, 98);

        AddReferenceMesh(
            brainContent,
            BuildCorticalReferenceSurfaceMesh(-1.0, 72, 40),
            cortexDiffuse,
            cortexEmissive);
        AddReferenceMesh(
            brainContent,
            BuildCorticalReferenceSurfaceMesh(1.0, 72, 40),
            cortexDiffuse,
            cortexEmissive);
        // The callosum is rendered as its own structure; an extra translucent
        // scaffold reads as an overlay across basal/thalamic structures.
        AddReferenceMesh(
            brainContent,
            BuildCerebellarReferenceSurfaceMesh(48, 24),
            cerebellumDiffuse,
            Color.FromArgb(2, 226, 156, 170));
        AddReferenceMesh(
            brainContent,
            BuildBrainstemReferenceSurfaceMesh(24, 32),
            brainstemDiffuse,
            Color.FromArgb(2, 236, 172, 142));

    }

    // Static mesh-builder helpers extracted to MainWindow.Brain3D.Meshes.cs.

    private static bool TrySplitInstanceId(string instanceId, out string hemisphere, out string snapshotId)
    {
        hemisphere = string.Empty;
        snapshotId = string.Empty;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        var separator = instanceId.IndexOf('_');
        if (separator <= 0 || separator >= (instanceId.Length - 1))
        {
            return false;
        }

        hemisphere = instanceId[..separator];
        snapshotId = instanceId[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(hemisphere) && !string.IsNullOrWhiteSpace(snapshotId);
    }

    // Speech worker, dispatch-spike phrase building, and language utterance memory
    // moved to MainWindow.Speech.cs.

    private async Task ControlWorkerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await _inputSignal.WaitAsync(token);
            while (_inputQueue.TryDequeue(out var delta))
            {
                _targetYaw += delta.DeltaX * 0.35;
                _targetPitch = Math.Clamp(_targetPitch - (delta.DeltaY * 0.3), -90, 90);
                _targetZoom = Math.Clamp(_targetZoom + (delta.WheelDelta * 0.0008), MinSceneZoom, MaxSceneZoom);
            }
        }
    }

    private async Task RenderWorkerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // Adaptive interval: tick fast while spikes/pathways are visibly
            // animating, slow when fully decayed. MarkVisualDirty sets
            // _visualActivity so the next sleep is short whenever a new spike
            // arrives; ApplyVisualDecay clears it when nothing is animating.
            var interval = Volatile.Read(ref _visualActivity) != 0
                ? ActiveRenderInterval
                : IdleRenderInterval;
            try
            {
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _renderDispatchPending, 1, 0) != 0)
            {
                continue;
            }

            // CRITICAL: wrap the dispatcher invoke in try/catch so an exception
            // from Apply3dFrame (or the dispatcher itself) cannot silently kill
            // the loop. Without this, a single bad frame stopped all subsequent
            // frames - which broke both spike decay and camera smoothing.
            try
            {
                await Dispatcher.InvokeAsync(Apply3dFrame, DispatcherPriority.Background, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Surface the failure once via the output log so it's debuggable,
                // but don't let it tear down the render loop.
                try
                {
                    await Dispatcher.InvokeAsync(
                        () => AddOutputLog($"Render frame error: {ex.GetType().Name}: {ex.Message}"),
                        DispatcherPriority.Background,
                        token);
                }
                catch
                {
                    // Best-effort log only.
                }
            }
            finally
            {
                Interlocked.Exchange(ref _renderDispatchPending, 0);
            }
        }
    }

    private void MarkVisualDirty() => Volatile.Write(ref _visualActivity, 1);

    private void Apply3dFrame()
    {
        var frameSw = Stopwatch.StartNew();

        _yawRotation.Angle += (_targetYaw - _yawRotation.Angle) * 0.35;
        _pitchRotation.Angle += (_targetPitch - _pitchRotation.Angle) * 0.35;
        _sceneZoom += (_targetZoom - _sceneZoom) * 0.35;

        var scaleValue = BaseSceneScale * _sceneZoom;
        _sceneScale.ScaleX = scaleValue;
        _sceneScale.ScaleY = scaleValue;
        _sceneScale.ScaleZ = scaleValue;

        _globalPulseScale.ScaleX = 1.0;
        _globalPulseScale.ScaleY = 1.0;
        _globalPulseScale.ScaleZ = 1.0;

        if (_lastSnapshotUtc != DateTime.MinValue && (DateTime.UtcNow - _lastSnapshotUtc).TotalSeconds > 3.0)
        {
            SetRenderStatus($"Render: no recent live snapshots (UI overruns: {_uiOverrunCount})", appendToOutput: false);
        }

        ApplyVisualDecay();

        if (frameSw.ElapsedMilliseconds > 34)
        {
            _uiOverrunCount++;
        }
        else if (_uiOverrunCount > 0)
        {
            _uiOverrunCount--;
        }
    }

    // Per-frame fade-out for spike brushes, pathway visuals, and the corpus
    // callosum glow. Extracted from Apply3dFrame so it can run on every render
    // tick - including while the camera is being dragged - so neurons that fired
    // before the interaction do not stay stuck at peak intensity.
    private void ApplyVisualDecay()
    {
        // 0.55 per ~100 ms tick takes a spike from A=255 to invisible in ~9 ticks
        // (~0.9 s at the 10 Hz active rate). The brain dispatches dozens of spikes
        // per second across 70 structures, so a slow fade accumulates a permanent
        // sea of bright dots. Short fade + dispatch-only lighting keeps the
        // visualization legible: each spike is a clear quick flash.
        _expiredSpikeNeuronBrushes.Clear();
        foreach (var brush in _activeSpikeNeuronBrushes)
        {
            var current = brush.Color;
            var decayedAlpha = (byte)Math.Max(0, current.A * 0.55);
            if (decayedAlpha <= 2)
            {
                brush.Color = Color.FromArgb(0, current.R, current.G, current.B);
                brush.Opacity = 0.0;
                _expiredSpikeNeuronBrushes.Add(brush);
                continue;
            }

            brush.Color = Color.FromArgb(decayedAlpha, current.R, current.G, current.B);
            brush.Opacity = decayedAlpha / 255.0;
        }

        for (var i = 0; i < _expiredSpikeNeuronBrushes.Count; i++)
        {
            _activeSpikeNeuronBrushes.Remove(_expiredSpikeNeuronBrushes[i]);
        }

        for (var p = _activePathwayVisuals.Count - 1; p >= 0; p--)
        {
            var pathway = _activePathwayVisuals[p];
            pathway.SpikeLevel *= 0.86;
            var i = Math.Clamp(pathway.SpikeLevel, 0.0, 1.0);
            var visible = Math.Clamp((i - 0.06) / 0.94, 0.0, 1.0);
            if (visible <= 0.001)
            {
                pathway.SpikeLevel = 0.0;
                pathway.IsActive = false;
                pathway.DiffuseBrush.Opacity = 0.0;
                pathway.EmissiveBrush.Opacity = 0.0;
                _activePathwayVisuals.RemoveAt(p);
                continue;
            }

            var baseDim = ScaleColor(pathway.BaseColor, 0.02 + (0.42 * visible));
            var pathwayTint = BrightenPreserveHue(baseDim, 0.30 + (0.55 * visible));
            pathway.DiffuseBrush.Color = pathwayTint;
            pathway.EmissiveBrush.Color = Color.FromArgb((byte)(185 * visible), pathwayTint.R, pathwayTint.G, pathwayTint.B);
            pathway.DiffuseBrush.Opacity = visible;
            pathway.EmissiveBrush.Opacity = Math.Clamp(visible * 0.95, 0.0, 1.0);
        }
        if (_corpusCallosumVisual is not null)
        {
            _corpusCallosumVisual.SpikeLevel *= 0.86;
            var active = Math.Clamp(_corpusCallosumVisual.SpikeLevel, 0.0, 1.0);
            var baseTint = ScaleColor(_corpusCallosumVisual.BaseColor, 0.18 + (0.64 * active));
            var glowTint = BrightenPreserveHue(baseTint, 0.35 + (0.55 * active));
            _corpusCallosumVisual.DiffuseBrush.Color = baseTint;
            _corpusCallosumVisual.EmissiveBrush.Color = Color.FromArgb((byte)(40 + (185 * active)), glowTint.R, glowTint.G, glowTint.B);
            _corpusCallosumVisual.DiffuseBrush.Opacity = 0.18 + (0.50 * active);
            _corpusCallosumVisual.EmissiveBrush.Opacity = 0.06 + (0.88 * active);
        }

        // If nothing is animating, drop the render loop to its idle interval
        // so it doesn't pay full dispatcher work to do nothing.
        if (_activeSpikeNeuronBrushes.Count == 0
            && _activePathwayVisuals.Count == 0
            && (_corpusCallosumVisual?.SpikeLevel ?? 0.0) <= 0.001)
        {
            Volatile.Write(ref _visualActivity, 0);
        }
    }

    // Frame polling transport (StartFramePolling, FramePollLoopAsync,
    // TryFallbackSnapshotPollFromWorkerAsync, EmitFramePollWarning, PollSnapshotAsync,
    // ProbeFrameCapableBaseUriAsync, ProbeDiagnosticsBaseUriAsync, TryProcessEndpointFallbackFrameAsync,
    // BuildFallbackFrameDocument, FetchJsonDocumentAsync, BuildFrameUri)
    // moved to MainWindow.Frames.cs.

    private void ProcessFramePayload(JsonElement frame, Uri verifiedBaseUri)
    {
        _lastFramePayloadUtc = DateTime.UtcNow;
        var frameState = IngestFrameState(frame, verifiedBaseUri);
        ProcessFrameLogs(frame);

        if (!TryGetProperty(frame, "latestSnapshot", out var latestSnapshot) ||
            latestSnapshot.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            NoteControlEndpointSuccess(verifiedBaseUri);
            SetTransportStatsText(AppendFrameSpikeMetrics(frameState.TransportStatsBaseText, FrameSpikeMetrics.Empty));
            SetRenderStatus("Render: connected (awaiting first snapshot)", appendToOutput: false);
            return;
        }

        ProcessSnapshotFramePayload(frame, latestSnapshot, verifiedBaseUri, frameState);
    }

    private FrameStateContext IngestFrameState(JsonElement frame, Uri verifiedBaseUri)
    {
        Dictionary<string, ServiceHealthEntry>? telemetry = null;
        var spikePipeline = TransportSpikePipeline.Empty;
        var transportStatsBaseText = "Transport stats unavailable: /api/v1/frame missing state payload.";
        if (TryGetProperty(frame, "state", out var stateElement) && stateElement.ValueKind == JsonValueKind.Object)
        {
            var previousSleepState = _isSimulationSleeping;
            _isSimulationSleeping = ParseSimulationSleepState(stateElement);
            if (previousSleepState != _isSimulationSleeping)
            {
                ApplySleepInputPauseState(_isSimulationSleeping);
            }

            SyncMinWakeTicksFromState(stateElement);
            SyncAutoProfileFromState(stateElement);
            SyncInputGatesFromState(stateElement);
            transportStatsBaseText = FormatTransportStats(stateElement);
            SetLanguageCommandTelemetryText(FormatBrainTelemetry(stateElement));
            SyncBrainNarrationFromState(stateElement);
            UpdateVisualAttentionReticleFromState(stateElement);
            spikePipeline = ParseTransportSpikePipeline(stateElement);
            telemetry = ParseServiceTelemetryFromState(stateElement);
            ApplyStructureStatusBadges(telemetry, null);
            EmitServiceHealthDiagnostics(telemetry, null);
            SyncReasoningControlsFromState(stateElement);
            QueueTelemetryPaneUpdatesFromFrame(stateElement);
        }
        else
        {
            _visualAttentionFocusField = "neutral";
            _visualAttentionFocusHemisphere = "M";
            _visualAttentionFocusConfidence = 0.0;
            SetLanguageCommandTelemetryText("Brain telemetry unavailable: /api/v1/frame missing state payload.");
            UpdateWebcamAttentionReticle();
            EmitServiceHealthDiagnostics(null, "/api/v1/frame missing state payload");
            QueueTelemetryPaneError("/api/v1/frame missing state payload.");
        }

        return new FrameStateContext(telemetry, spikePipeline, transportStatsBaseText);
    }

    private void QueueTelemetryPaneUpdatesFromFrame(JsonElement stateElement)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastFrameTelemetryPaneQueueUtc) < TimeSpan.FromMilliseconds(750))
        {
            return;
        }

        _lastFrameTelemetryPaneQueueUtc = now;
        QueueTelemetryPaneUpdates(stateElement.Clone(), includeTransportStats: false);
    }

    private void ProcessFrameLogs(JsonElement frame)
    {
        if (TryGetProperty(frame, "outputLog", out var outputLog))
        {
            IngestRemoteLogEntries(outputLog, spike: false);
        }

        if (TryGetProperty(frame, "spikeLog", out var spikeLog))
        {
            IngestRemoteLogEntries(spikeLog, spike: true);
        }
    }

    private void SyncBrainNarrationFromState(JsonElement stateElement)
    {
        if (!TryGetProperty(stateElement, "brainNarration", out var narration) ||
            narration.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var sequence = GetLong(narration, "sequence");
        if (sequence <= 0 || sequence <= _lastBrainNarrationSequence)
        {
            return;
        }

        var utterance = GetString(narration, "utterance");
        if (string.IsNullOrWhiteSpace(utterance))
        {
            _lastBrainNarrationSequence = sequence;
            return;
        }

        _lastBrainNarrationSequence = sequence;
        var spokenEligible = GetBool(narration, "spokenEligible", true);
        var speechReleaseGate = GetDouble(narration, "speechReleaseGate");
        var speechSuppression = GetDouble(narration, "speechSuppression");
        if (!spokenEligible || speechReleaseGate < 0.32 || speechSuppression > 0.78)
        {
            return;
        }

        if (!IsSpeakableBrainNarration(utterance))
        {
            return;
        }

        if (!_speechOutputEnabled || _isSimulationSleeping)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastSpeechUtc) < SpeechCooldown ||
            (now - _lastBrainNarrationSpeechUtc) < BrainNarrationSpeechCooldown)
        {
            return;
        }

        if (string.Equals(_lastSpokenPhrase, utterance, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastSpeechUtc) < SpeechDuplicateSuppression)
        {
            return;
        }

        _lastSpeechUtc = now;
        _lastBrainNarrationSpeechUtc = now;
        _lastSpokenPhrase = utterance;
        _speechQueue.Writer.TryWrite(utterance);
    }

    private static bool IsSpeakableBrainNarration(string utterance)
    {
        var normalized = NormalizeSpeechUtterance(utterance);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var lower = normalized.ToLowerInvariant();
        if (lower.Contains('=') ||
            lower.Contains("dopamine", StringComparison.Ordinal) ||
            lower.Contains("hippocamp", StringComparison.Ordinal) ||
            lower.Contains("ca1", StringComparison.Ordinal) ||
            lower.Contains("ca2", StringComparison.Ordinal) ||
            lower.Contains("ca3", StringComparison.Ordinal) ||
            lower.Contains("dentate", StringComparison.Ordinal) ||
            lower.Contains("entorhinal", StringComparison.Ordinal) ||
            lower.Contains("subiculum", StringComparison.Ordinal) ||
            lower.Contains("insula=", StringComparison.Ordinal) ||
            lower.Contains("binding", StringComparison.Ordinal) ||
            lower.Contains("confidence", StringComparison.Ordinal) ||
            lower.Contains("trace", StringComparison.Ordinal) ||
            lower.Contains("runtime", StringComparison.Ordinal) ||
            lower.Contains("telemetry", StringComparison.Ordinal))
        {
            return false;
        }

        return lower.StartsWith("i ", StringComparison.Ordinal) ||
               lower.StartsWith("it is ", StringComparison.Ordinal);
    }

    private void ProcessSnapshotFramePayload(
        JsonElement frame,
        JsonElement latestSnapshot,
        Uri verifiedBaseUri,
        FrameStateContext frameState)
    {
        var payload = ParseSnapshotPayload(latestSnapshot);
        var dispatchSpikes = ParseDispatchSpikeTraces(frame);
        var dispatchPathwayActivities = BuildDispatchPathwayActivities(dispatchSpikes);
        TryQueueSpeechFromLanguageDispatch(dispatchSpikes);
        var dispatchIdsByStructure = BuildDispatchNeuronIdLookup(dispatchSpikes);
        var distinctDispatchNeuronIds = CountDistinctConcreteNeuronIds(dispatchIdsByStructure);
        var unmatchedNeuronIds = CountUnmatchedDispatchStructures(dispatchIdsByStructure);

        NoteControlEndpointSuccess(verifiedBaseUri);
        _lastSnapshotUtc = DateTime.UtcNow;

        if (payload.StructureStates.Count == 0 && dispatchSpikes.Count == 0)
        {
            SetTransportStatsText(AppendFrameSpikeMetrics(
                frameState.TransportStatsBaseText,
                new FrameSpikeMetrics(
                    frameState.SpikePipeline.Generated,
                    frameState.SpikePipeline.Routed,
                    frameState.SpikePipeline.Delivered,
                    dispatchSpikes.Count,
                    distinctDispatchNeuronIds,
                    StructuresWithNeuronSpikes: 0,
                    VisibleNeuronHighlights: 0,
                    UnmatchedNeuronIds: unmatchedNeuronIds)));
            SetRenderStatus("Render: connected, but no structures are currently reporting");
            if (frameState.Telemetry is null)
            {
                QueueHealthDiagnosticsProbe();
            }
            return;
        }

        if (payload.StructureStates.Count == 0)
        {
            SetRenderStatus("Render: live dispatch activity (awaiting structure snapshots)", appendToOutput: false);
        }

        var expectedStructureCount = Math.Max(1, _structureVisualsByBaseId.Count);
        if (payload.StructureStates.Count > 0 && payload.StructureStates.Count < (expectedStructureCount / 3))
        {
            var missing = Math.Max(0, expectedStructureCount - payload.StructureStates.Count);
            SetRenderStatus($"Render: partial live snapshot ({payload.StructureStates.Count}/{expectedStructureCount}); {missing} structures offline/unresponsive");
            if (frameState.Telemetry is null)
            {
                QueueHealthDiagnosticsProbe();
            }
        }

        // Synchronous on the UI thread: the prior async-off-thread refactor
        // introduced a hang in the dispatcher chain (most likely the inner
        // continuation of `await await Dispatcher.InvokeAsync(asyncDelegate)`
        // getting wedged behind other dispatcher work), which silently stopped
        // Apply3dFrame from running and broke both spike decay and camera zoom.
        var prepared = PrepareNeuronHighlights(payload, dispatchIdsByStructure, unmatchedNeuronIds);
        var highlightResult = ApplyPreparedNeuronHighlights(prepared);
        ApplySnapshotPathwayActivity(payload.Pathways);
        ApplyDispatchPathwayActivity(dispatchPathwayActivities);

        SetTransportStatsText(AppendFrameSpikeMetrics(
            frameState.TransportStatsBaseText,
            new FrameSpikeMetrics(
                frameState.SpikePipeline.Generated,
                frameState.SpikePipeline.Routed,
                frameState.SpikePipeline.Delivered,
                dispatchSpikes.Count,
                distinctDispatchNeuronIds,
                highlightResult.StructuresWithNeuronSpikes,
                highlightResult.VisibleNeuronHighlights,
                highlightResult.UnmatchedNeuronIds)));
        _lastSnapshotUtc = DateTime.UtcNow;
    }

    private int CountUnmatchedDispatchStructures(Dictionary<string, HashSet<string>> dispatchIdsByStructure)
    {
        var unmatchedNeuronIds = 0;
        foreach (var structureId in dispatchIdsByStructure.Keys)
        {
            if (_structureVisualsByBaseId.ContainsKey(structureId))
            {
                continue;
            }

            unmatchedNeuronIds++;
            LogUnmatchedSpikeNeuronOnce(structureId, "M", "n/a", "structure not rendered");
        }

        return unmatchedNeuronIds;
    }

    private static List<string> BuildFrameNeuronIdList(
        IReadOnlyCollection<string>? dispatchNeuronIds,
        IReadOnlyList<string> topNeuronIds)
    {
        var realNeuronIds = new List<string>(Math.Min(MaxNeuronHighlightsPerStructurePerFrame, (dispatchNeuronIds?.Count ?? 0) + topNeuronIds.Count));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? populationFallback0 = null;
        string? populationFallback1 = null;
        string? populationFallback2 = null;
        string? populationFallback3 = null;
        var populationFallbackCount = 0;

        if (dispatchNeuronIds is not null)
        {
            foreach (var neuronId in dispatchNeuronIds)
            {
                AddFrameNeuronId(
                    neuronId,
                    realNeuronIds,
                    seen,
                    ref populationFallback0,
                    ref populationFallback1,
                    ref populationFallback2,
                    ref populationFallback3,
                    ref populationFallbackCount,
                    collectPopulationFallback: true);
                if (realNeuronIds.Count >= MaxNeuronHighlightsPerStructurePerFrame)
                {
                    return realNeuronIds;
                }
            }
        }

        for (var i = 0; i < topNeuronIds.Count && realNeuronIds.Count < MaxNeuronHighlightsPerStructurePerFrame; i++)
        {
            AddFrameNeuronId(
                topNeuronIds[i],
                realNeuronIds,
                seen,
                ref populationFallback0,
                ref populationFallback1,
                ref populationFallback2,
                ref populationFallback3,
                ref populationFallbackCount,
                collectPopulationFallback: false);
        }

        if (realNeuronIds.Count == 0 && populationFallbackCount > 0)
        {
            AddPopulationFallback(realNeuronIds, populationFallback0);
            AddPopulationFallback(realNeuronIds, populationFallback1);
            AddPopulationFallback(realNeuronIds, populationFallback2);
            AddPopulationFallback(realNeuronIds, populationFallback3);
        }

        return realNeuronIds;
    }

    private static void AddFrameNeuronId(
        string? neuronId,
        List<string> realNeuronIds,
        HashSet<string> seen,
        ref string? populationFallback0,
        ref string? populationFallback1,
        ref string? populationFallback2,
        ref string? populationFallback3,
        ref int populationFallbackCount,
        bool collectPopulationFallback)
    {
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return;
        }

        if (IsPopulationNeuronId(neuronId))
        {
            if (collectPopulationFallback)
            {
                switch (populationFallbackCount)
                {
                    case 0:
                        populationFallback0 = neuronId;
                        populationFallbackCount++;
                        break;
                    case 1:
                        populationFallback1 = neuronId;
                        populationFallbackCount++;
                        break;
                    case 2:
                        populationFallback2 = neuronId;
                        populationFallbackCount++;
                        break;
                    case 3:
                        populationFallback3 = neuronId;
                        populationFallbackCount++;
                        break;
                }
            }
            return;
        }

        if (seen.Add(neuronId))
        {
            realNeuronIds.Add(neuronId);
        }
    }

    private static bool IsPopulationNeuronId(string neuronId)
        => neuronId.Contains("population-", StringComparison.OrdinalIgnoreCase);

    private static void AddPopulationFallback(List<string> neuronIds, string? neuronId)
    {
        if (!string.IsNullOrWhiteSpace(neuronId))
        {
            neuronIds.Add(neuronId);
        }
    }

    private static List<PathwayTick> SelectTopPathwaysByVolume(IReadOnlyList<PathwayTick> pathways, int maxCount)
    {
        var top = new List<PathwayTick>(Math.Min(maxCount, pathways.Count));
        for (var i = 0; i < pathways.Count; i++)
        {
            var pathway = pathways[i];
            if (pathway.Volume <= 0)
            {
                continue;
            }

            InsertByVolume(top, pathway, maxCount, static pathway => pathway.Volume);
        }

        return top;
    }

    private static List<DispatchPathwayActivity> SelectTopDispatchPathwaysByVolume(IReadOnlyList<DispatchPathwayActivity> activities, int maxCount)
    {
        var top = new List<DispatchPathwayActivity>(Math.Min(maxCount, activities.Count));
        for (var i = 0; i < activities.Count; i++)
        {
            var activity = activities[i];
            if (activity.Volume <= 0)
            {
                continue;
            }

            InsertByVolume(top, activity, maxCount, static activity => activity.Volume);
        }

        return top;
    }

    private static void InsertByVolume<T>(List<T> top, T item, int maxCount, Func<T, int> volumeSelector)
    {
        var volume = volumeSelector(item);
        var insertAt = top.Count;
        for (var i = 0; i < top.Count; i++)
        {
            if (volume > volumeSelector(top[i]))
            {
                insertAt = i;
                break;
            }
        }

        if (insertAt >= maxCount)
        {
            return;
        }

        top.Insert(insertAt, item);
        if (top.Count > maxCount)
        {
            top.RemoveAt(top.Count - 1);
        }
    }

    private static bool HasCallosalHemisphere(IReadOnlyList<PathwayVisual> visuals)
    {
        for (var i = 0; i < visuals.Count; i++)
        {
            if (IsCallosalHemisphere(visuals[i].Hemisphere))
            {
                return true;
            }
        }

        return false;
    }

    // PURE CPU - safe to run on a background thread. Walks the snapshot payload,
    // resolves each neuron ID to its target SpikeNeuronBrush, builds a list of
    // (brush, color, isDispatchSpike) ops plus metadata. Does NOT mutate any UI
    // state. Reads _structureVisualsByBaseId which is rebuilt only on the UI
    // thread inside BuildBrainScene - if a rebuild races with us, we may end up
    // with stale brush refs but the apply phase tolerates that (orphaned brushes
    // are simply not in any visual tree and the mutation is a no-op).
    private NeuronHighlightPrep PrepareNeuronHighlights(
        SnapshotPayload payload,
        Dictionary<string, HashSet<string>> dispatchIdsByStructure,
        int initialUnmatchedNeuronIds)
    {
        var ops = new List<NeuronHighlightOp>(64);
        var litSpikeSlots = new HashSet<(string SnapshotId, string Hemisphere, int Index)>();
        var unmatchedLog = new List<UnmatchedNeuronLog>();
        var snapshotStructureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        double callosumLevel = -1.0;
        var visibleNeuronHighlights = 0;
        var structuresWithNeuronSpikes = 0;
        var unmatchedNeuronIds = initialUnmatchedNeuronIds;
        var meanFiringRateUpdates = new List<(StructureVisual Visual, float MeanRateHz, MicrotubuleTick? Microtubules, BodySchemaTick? BodySchema, BasalGangliaTick? BasalGanglia, CerebellarTick? Cerebellar, VestibuloReticularTick? VestibuloReticular, SuperiorColliculusTick? SuperiorColliculus, HippocampalSpatialTick? HippocampalSpatial, SalienceAffectTick? SalienceAffect, PrefrontalWorkingMemoryTick? PrefrontalWorkingMemory, ThalamicAttentionGateTick? ThalamicAttentionGate, HypothalamicHomeostasisTick? HypothalamicHomeostasis, SleepWakeArousalTick? SleepWakeArousal, DescendingDefenseTick? DescendingDefense, DopamineRewardTick? DopamineReward, SeptohippocampalThetaTick? SeptohippocampalTheta, SpinalProprioceptiveTick? SpinalProprioceptive, OlfactoryLimbicMemoryTick? OlfactoryLimbicMemory, AuditoryLanguageMotorTick? AuditoryLanguageMotor, VisualObjectRecognitionTick? VisualObjectRecognition)>(payload.StructureStates.Count);

        foreach (var state in payload.StructureStates)
        {
            snapshotStructureIds.Add(state.StructureId);
            if (string.Equals(state.StructureId, "CorpusCallosum", StringComparison.OrdinalIgnoreCase))
            {
                callosumLevel = Math.Clamp(((state.SpikeOut + state.SpikeIn) / 96.0) + (state.MeanRateHz / 45.0), 0.0, 1.0);
            }

            if (!_structureVisualsByBaseId.TryGetValue(state.StructureId, out var visuals))
            {
                continue;
            }

            dispatchIdsByStructure.TryGetValue(state.StructureId, out var dispatchNeuronIds);
            var realNeuronIds = BuildFrameNeuronIdList(dispatchNeuronIds, state.TopNeuronIds);
            if (realNeuronIds.Count > 0)
            {
                structuresWithNeuronSpikes++;
            }

            foreach (var visual in visuals)
            {
                meanFiringRateUpdates.Add((visual, state.MeanRateHz, state.Microtubules, state.BodySchema, state.BasalGanglia, state.Cerebellar, state.VestibuloReticular, state.SuperiorColliculus, state.HippocampalSpatial, state.SalienceAffect, state.PrefrontalWorkingMemory, state.ThalamicAttentionGate, state.HypothalamicHomeostasis, state.SleepWakeArousal, state.DescendingDefense, state.DopamineReward, state.SeptohippocampalTheta, state.SpinalProprioceptive, state.OlfactoryLimbicMemory, state.AuditoryLanguageMotor, state.VisualObjectRecognition));
            }

            PrepareStructureNeuronHighlights(
                state.StructureId,
                visuals,
                realNeuronIds,
                dispatchNeuronIds,
                litSpikeSlots,
                ops,
                unmatchedLog,
                ref visibleNeuronHighlights,
                ref unmatchedNeuronIds);
        }

        // Direct dispatch traces are the authoritative per-event activity
        // stream. A slow or overloaded structure may miss the aggregate
        // snapshot for this frame, but its routed spikes must remain visible.
        foreach (var dispatchEntry in dispatchIdsByStructure)
        {
            if (snapshotStructureIds.Contains(dispatchEntry.Key) ||
                !_structureVisualsByBaseId.TryGetValue(dispatchEntry.Key, out var visuals))
            {
                continue;
            }

            var dispatchNeuronIds = dispatchEntry.Value;
            var realNeuronIds = BuildFrameNeuronIdList(dispatchNeuronIds, []);
            if (realNeuronIds.Count == 0)
            {
                continue;
            }

            structuresWithNeuronSpikes++;
            PrepareStructureNeuronHighlights(
                dispatchEntry.Key,
                visuals,
                realNeuronIds,
                dispatchNeuronIds,
                litSpikeSlots,
                ops,
                unmatchedLog,
                ref visibleNeuronHighlights,
                ref unmatchedNeuronIds);
        }

        return new NeuronHighlightPrep(
            ops,
            meanFiringRateUpdates,
            unmatchedLog,
            callosumLevel,
            new NeuronHighlightResult(structuresWithNeuronSpikes, visibleNeuronHighlights, unmatchedNeuronIds));
    }

    private static void PrepareStructureNeuronHighlights(
        string structureId,
        IReadOnlyList<StructureVisual> visuals,
        IReadOnlyList<string> neuronIds,
        HashSet<string>? dispatchNeuronIds,
        HashSet<(string SnapshotId, string Hemisphere, int Index)> litSpikeSlots,
        List<NeuronHighlightOp> ops,
        List<UnmatchedNeuronLog> unmatchedLog,
        ref int visibleNeuronHighlights,
        ref int unmatchedNeuronIds)
    {
        if (neuronIds.Count == 0)
        {
            return;
        }

        foreach (var visual in visuals)
        {
            if (visual.SpikeNeuronBrushes.Count == 0)
            {
                continue;
            }

            foreach (var neuronId in FilterNeuronIdsForHemisphere(neuronIds, visual.Hemisphere))
            {
                var idx = ResolveSpikeBrushIndexForNeuronId(visual, neuronId);
                if (idx < 0 || idx >= visual.SpikeNeuronBrushes.Count)
                {
                    unmatchedNeuronIds++;
                    unmatchedLog.Add(new UnmatchedNeuronLog(structureId, visual.Hemisphere, neuronId, "no spike brush slot"));
                    continue;
                }

                if (!litSpikeSlots.Add((visual.SnapshotId, visual.Hemisphere, idx)))
                {
                    continue;
                }

                visibleNeuronHighlights++;
                ops.Add(new NeuronHighlightOp(
                    visual.SpikeNeuronBrushes[idx],
                    visual.SpikeColor,
                    dispatchNeuronIds?.Contains(neuronId) ?? false));
            }
        }
    }

    // UI THREAD ONLY - takes the precomputed ops and mutates the brushes.
    // Brushes are DependencyObjects with thread affinity, so all of this must
    // run on the dispatcher. The decision (flash / refresh / ignore) reads
    // brush.Color.A which is only safe from the UI thread.
    //
    // Lighting rules:
    //   - Dispatch event (real per-tick spike): ALWAYS flash brush to full alpha.
    //     This re-lights dark brushes AND refreshes already-lit ones.
    //   - Top-N only (cumulative EWMA "this neuron is currently active"):
    //     NEVER ignite a dark brush, and NEVER refresh a lit one. Top-N is a
    //     steady-state signal, not a spike event - using it to light brushes
    //     causes them to accumulate without ever fading because new top-N
    //     entries keep re-lighting brushes that just decayed.
    private NeuronHighlightResult ApplyPreparedNeuronHighlights(NeuronHighlightPrep prepared)
    {
        if (prepared.CallosumLevel > 0.0)
        {
            ApplyCorpusCallosumActivity(prepared.CallosumLevel);
        }

        foreach (var update in prepared.MeanFiringRateUpdates)
        {
            update.Visual.MeanFiringRateHz = update.MeanRateHz;
            update.Visual.Microtubules = update.Microtubules;
            update.Visual.BodySchema = update.BodySchema;
            update.Visual.BasalGanglia = update.BasalGanglia;
            update.Visual.Cerebellar = update.Cerebellar;
            update.Visual.VestibuloReticular = update.VestibuloReticular;
            update.Visual.SuperiorColliculus = update.SuperiorColliculus;
            update.Visual.HippocampalSpatial = update.HippocampalSpatial;
            update.Visual.SalienceAffect = update.SalienceAffect;
            update.Visual.PrefrontalWorkingMemory = update.PrefrontalWorkingMemory;
            update.Visual.ThalamicAttentionGate = update.ThalamicAttentionGate;
            update.Visual.HypothalamicHomeostasis = update.HypothalamicHomeostasis;
            update.Visual.SleepWakeArousal = update.SleepWakeArousal;
            update.Visual.DescendingDefense = update.DescendingDefense;
            update.Visual.DopamineReward = update.DopamineReward;
            update.Visual.SeptohippocampalTheta = update.SeptohippocampalTheta;
            update.Visual.SpinalProprioceptive = update.SpinalProprioceptive;
            update.Visual.OlfactoryLimbicMemory = update.OlfactoryLimbicMemory;
            update.Visual.AuditoryLanguageMotor = update.AuditoryLanguageMotor;
            update.Visual.VisualObjectRecognition = update.VisualObjectRecognition;
        }

        RefreshSelectedStructureInspector();

        foreach (var unmatched in prepared.UnmatchedLog)
        {
            LogUnmatchedSpikeNeuronOnce(unmatched.StructureId, unmatched.Hemisphere, unmatched.NeuronId, unmatched.Reason);
        }

        var litAnyBrush = false;
        foreach (var op in prepared.Ops)
        {
            if (!op.IsDispatchSpike)
            {
                // Top-N only: ignore. Brushes that are already lit decay; dark
                // brushes stay dark until an actual spike event arrives.
                continue;
            }

            var brush = op.Brush;
            _activeSpikeNeuronBrushes.Add(brush);
            brush.Color = Color.FromArgb(255, op.SpikeColor.R, op.SpikeColor.G, op.SpikeColor.B);
            brush.Opacity = 1.0;
            litAnyBrush = true;
        }

        if (litAnyBrush)
        {
            MarkVisualDirty();
        }

        return prepared.Result;
    }

    private readonly record struct NeuronHighlightOp(SolidColorBrush Brush, Color SpikeColor, bool IsDispatchSpike);
    private readonly record struct UnmatchedNeuronLog(string StructureId, string Hemisphere, string NeuronId, string Reason);
    private sealed record NeuronHighlightPrep(
        List<NeuronHighlightOp> Ops,
        List<(StructureVisual Visual, float MeanRateHz, MicrotubuleTick? Microtubules, BodySchemaTick? BodySchema, BasalGangliaTick? BasalGanglia, CerebellarTick? Cerebellar, VestibuloReticularTick? VestibuloReticular, SuperiorColliculusTick? SuperiorColliculus, HippocampalSpatialTick? HippocampalSpatial, SalienceAffectTick? SalienceAffect, PrefrontalWorkingMemoryTick? PrefrontalWorkingMemory, ThalamicAttentionGateTick? ThalamicAttentionGate, HypothalamicHomeostasisTick? HypothalamicHomeostasis, SleepWakeArousalTick? SleepWakeArousal, DescendingDefenseTick? DescendingDefense, DopamineRewardTick? DopamineReward, SeptohippocampalThetaTick? SeptohippocampalTheta, SpinalProprioceptiveTick? SpinalProprioceptive, OlfactoryLimbicMemoryTick? OlfactoryLimbicMemory, AuditoryLanguageMotorTick? AuditoryLanguageMotor, VisualObjectRecognitionTick? VisualObjectRecognition)> MeanFiringRateUpdates,
        List<UnmatchedNeuronLog> UnmatchedLog,
        double CallosumLevel,
        NeuronHighlightResult Result);

    private void ApplySnapshotPathwayActivity(IReadOnlyList<PathwayTick> pathways)
    {
        var ranked = SelectTopPathwaysByVolume(pathways, 56);
        if (ranked.Count == 0)
        {
            return;
        }

        var minVolume = Math.Max(2, (int)Math.Round(ranked[0].Volume * 0.35));
        foreach (var pathway in ranked)
        {
            if (pathway.Volume < minVolume ||
                !_pathwayVisualsByBasePair.TryGetValue(PathwayKey(pathway.Source, pathway.Target), out var pathVisuals))
            {
                continue;
            }

            var level = Math.Clamp(pathway.Volume / 240f, 0f, 1f);
            if (IsCorpusCallosumPathway(pathway.Source, pathway.Target) || HasCallosalHemisphere(pathVisuals))
            {
                ApplyCorpusCallosumActivity(level);
            }
            foreach (var edge in pathVisuals)
            {
                ActivatePathway(edge, level);
            }
        }
    }

    private void ApplyDispatchPathwayActivity(IReadOnlyList<DispatchPathwayActivity> dispatchPathwayActivities)
    {
        var ranked = SelectTopDispatchPathwaysByVolume(dispatchPathwayActivities, MaxPathwayActivationsPerFrame);
        for (var i = 0; i < ranked.Count; i++)
        {
            var activity = ranked[i];
            if (!_pathwayVisualsByBasePair.TryGetValue(PathwayKey(activity.Source, activity.Target), out var visuals))
            {
                continue;
            }

            var level = Math.Clamp(activity.Volume / 24f, 0f, 1f);
            if (IsCorpusCallosumPathway(activity.Source, activity.Target) || IsCallosalHemisphere(activity.Hemisphere))
            {
                ApplyCorpusCallosumActivity(level);
            }
            foreach (var edge in visuals)
            {
                if (!PathwayHemisphereMatches(edge.Hemisphere, activity.Hemisphere))
                {
                    continue;
                }

                ActivatePathway(edge, level);
            }
        }
    }

    private void ActivatePathway(PathwayVisual pathway, double level)
    {
        pathway.SpikeLevel = Math.Max(pathway.SpikeLevel, Math.Clamp(level, 0.0, 1.0));
        MarkVisualDirty();
        if (pathway.IsActive)
        {
            return;
        }

        pathway.IsActive = true;
        _activePathwayVisuals.Add(pathway);
    }

    private void ApplyCorpusCallosumActivity(double level)
    {
        if (_corpusCallosumVisual is null || level <= 0.0)
        {
            return;
        }

        _corpusCallosumVisual.SpikeLevel = Math.Max(_corpusCallosumVisual.SpikeLevel, Math.Clamp(level, 0.0, 1.0));
        MarkVisualDirty();
    }

    private sealed record FrameStateContext(
        Dictionary<string, ServiceHealthEntry>? Telemetry,
        TransportSpikePipeline SpikePipeline,
        string TransportStatsBaseText);

    private readonly record struct NeuronHighlightResult(
        int StructuresWithNeuronSpikes,
        int VisibleNeuronHighlights,
        int UnmatchedNeuronIds);

    private static bool ParseSimulationSleepState(JsonElement stateElement)
    {
        if (!TryGetProperty(stateElement, "sleepMemory", out var sleepMemory) ||
            sleepMemory.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetProperty(sleepMemory, "isSleeping", out var isSleeping) ||
            isSleeping.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        return isSleeping.GetBoolean();
    }

    private void UpdateVisualAttentionReticleFromState(JsonElement stateElement)
    {
        if (!TryGetProperty(stateElement, "visualAttention", out var attention) ||
            attention.ValueKind != JsonValueKind.Object)
        {
            _visualAttentionFocusField = "neutral";
            _visualAttentionFocusHemisphere = "M";
            _visualAttentionFocusConfidence = 0.0;
            UpdateWebcamAttentionReticle();
            return;
        }

        var field = GetString(attention, "focusedField");
        var hemisphere = GetString(attention, "focusedHemisphere");
        _visualAttentionFocusField = string.IsNullOrWhiteSpace(field)
            ? "neutral"
            : field.Trim().ToLowerInvariant();
        _visualAttentionFocusHemisphere = string.IsNullOrWhiteSpace(hemisphere)
            ? "M"
            : hemisphere.Trim().ToUpperInvariant();
        _visualAttentionFocusConfidence = Math.Clamp(GetDouble(attention, "focusConfidence"), 0.0, 1.0);
        UpdateWebcamAttentionReticle();
    }

    private void ApplySleepInputPauseState(bool isSleeping)
    {
        if (isSleeping)
        {
            while (_speechQueue.Reader.TryRead(out _))
            {
                // Drain queued utterances when sleep begins.
            }

            if (_webcamRunning)
            {
                WebcamStatusText.Text = $"Webcam: paused during sleep ({_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
            }

            if (_microphoneRunning)
            {
                MicrophoneStatusText.Text = "Microphone: paused during sleep";
            }

            if (LanguageInputStatusText is not null)
            {
                LanguageInputStatusText.Text = "Language: paused during sleep";
            }

            UpdateSpeechStatusText("Speech: paused during sleep");
            AddOutputLog("Sleep gating active: webcam, microphone, and speech output paused.");
            return;
        }

        if (_webcamRunning)
        {
            WebcamStatusText.Text = $"Webcam: running (camera active, {_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
        }

        if (_microphoneRunning)
        {
            MicrophoneStatusText.Text = "Microphone: running (resumed)";
        }

        if (LanguageInputStatusText is not null)
        {
            LanguageInputStatusText.Text = "Language: resumed";
        }

        UpdateSpeechStatusText(_speechOutputEnabled ? "Speech: listening for activity" : "Speech: disabled");
        AddOutputLog("Sleep gating cleared: webcam, microphone, and speech output resumed.");
    }

    private static TransportSpikePipeline ParseTransportSpikePipeline(JsonElement root)
    {
        if (!TryGetProperty(root, "transportStats", out var transport) || transport.ValueKind != JsonValueKind.Object)
        {
            return TransportSpikePipeline.Empty;
        }

        return new TransportSpikePipeline(
            GetInt(transport, "generatedSpikes"),
            GetInt(transport, "routedSpikes"),
            GetInt(transport, "deliveredSpikes"));
    }

    private static string ParseStructureId(JsonElement state) => ParseAnyStructureId(state, "structureId");

    private static int ResolveSpikeBrushIndexForNeuronId(StructureVisual visual, string neuronId)
    {
        if (visual.SpikeNeuronBrushes.Count == 0 || string.IsNullOrWhiteSpace(neuronId))
        {
            return -1;
        }

        if (visual.NeuronIdToSpikeIndex.TryGetValue(neuronId, out var mapped))
        {
            return mapped;
        }

        if (visual.AssignedSpikeIndices.Count < visual.SpikeNeuronBrushes.Count)
        {
            var idx = visual.NextSpikeBrushAssignment % visual.SpikeNeuronBrushes.Count;
            var attempts = 0;
            while (visual.AssignedSpikeIndices.Contains(idx) && attempts < visual.SpikeNeuronBrushes.Count)
            {
                idx = (idx + 1) % visual.SpikeNeuronBrushes.Count;
                attempts++;
            }

            visual.AssignedSpikeIndices.Add(idx);
            visual.NeuronIdToSpikeIndex[neuronId] = idx;
            visual.NextSpikeBrushAssignment = (idx + 1) % visual.SpikeNeuronBrushes.Count;
            return idx;
        }

        var fallback = MapNeuronIdToSpikeIndex(neuronId, visual.SpikeNeuronBrushes.Count, visual.Hemisphere);
        visual.NeuronIdToSpikeIndex[neuronId] = fallback;
        return fallback;
    }

    private static IEnumerable<string> FilterNeuronIdsForHemisphere(IEnumerable<string> neuronIds, string hemisphere)
    {
        foreach (var neuronId in neuronIds)
        {
            if (string.IsNullOrWhiteSpace(neuronId))
            {
                continue;
            }

            var colon = neuronId.IndexOf(':');
            if (colon <= 0)
            {
                if (hemisphere == "M")
                {
                    yield return neuronId;
                }
                else if (ShouldAssignUnscopedNeuronToHemisphere(neuronId, hemisphere))
                {
                    yield return $"{hemisphere}:{neuronId}";
                }

                continue;
            }

            var idHemi = neuronId[..colon];
            if (idHemi.Equals(hemisphere, StringComparison.OrdinalIgnoreCase))
            {
                yield return neuronId;
            }
            else if (hemisphere == "M" && idHemi.Equals("M", StringComparison.OrdinalIgnoreCase))
            {
                yield return neuronId;
            }
            else if (idHemi.Equals("M", StringComparison.OrdinalIgnoreCase) &&
                     ShouldAssignUnscopedNeuronToHemisphere(neuronId[(colon + 1)..], hemisphere))
            {
                yield return $"{hemisphere}:{neuronId[(colon + 1)..]}";
            }
        }
    }

    private static bool ShouldAssignUnscopedNeuronToHemisphere(string neuronId, string hemisphere)
    {
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return false;
        }

        if (hemisphere.Equals("L", StringComparison.OrdinalIgnoreCase))
        {
            return MapNeuronIdToSpikeIndex(neuronId, 2, "L") == 0;
        }

        if (hemisphere.Equals("R", StringComparison.OrdinalIgnoreCase))
        {
            return MapNeuronIdToSpikeIndex(neuronId, 2, "R") == 1;
        }

        return true;
    }

    private static int MapNeuronIdToSpikeIndex(string neuronId, int count, string hemisphere)
    {
        if (count <= 0)
        {
            return 0;
        }

        unchecked
        {
            uint h = 2166136261;
            foreach (var c in neuronId)
            {
                h ^= c;
                h *= 16777619;
            }

            foreach (var c in hemisphere)
            {
                h ^= c;
                h *= 16777619;
            }

            return (int)(h % (uint)count);
        }
    }

    private static string ParseAnyStructureId(JsonElement element, string property)
    {
        if (!TryGetProperty(element, property, out var idElement))
        {
            if (property.Equals("structureId", StringComparison.OrdinalIgnoreCase) && TryGetProperty(element, "structure_id", out idElement))
            {
                // snake_case fallback
            }
            else
            {
                return string.Empty;
            }
        }

        if (idElement.ValueKind == JsonValueKind.String)
        {
            return idElement.GetString() ?? string.Empty;
        }

        if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt32(out var ordinal))
        {
            return MapStructureOrdinal(ordinal);
        }

        return string.Empty;
    }

    private static string MapStructureOrdinal(int ordinal)
    {
        return Enum.IsDefined(typeof(StructureId), ordinal)
            ? ((StructureId)ordinal).ToString()
            : string.Empty;
    }

    private void AddSpikeLog(string message)
    {
        SpikeLogList.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (SpikeLogList.Items.Count > 250)
        {
            SpikeLogList.Items.RemoveAt(SpikeLogList.Items.Count - 1);
        }
    }

    private void SetRenderStatus(string status, bool appendToOutput = true)
    {
        if (string.Equals(_lastRenderStatus, status, StringComparison.Ordinal))
        {
            return;
        }

        _lastRenderStatus = status;
        RenderStatusText.Text = status;
        if (appendToOutput)
        {
            AddOutputLog(status);
        }
    }

    private void AddOutputLog(string message)
    {
        AddOutputLogs([message]);
    }

    // Wrap async-void event handler bodies so HTTP / IO failures become log lines
    // instead of unobserved exceptions that crash the WPF dispatcher.
    private async Task SafeHandlerAsync(Func<Task> handler, string description)
    {
        try
        {
            await handler();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AddOutputLog($"{description} failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void AddOutputLogs(IEnumerable<string> messages)
    {
        var appended = false;
        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            var normalized = message.Trim();
            var now = DateTime.Now;
            if (_lastOutputMessageByText.TryGetValue(normalized, out var lastSeenUtc) &&
                (now - lastSeenUtc) <= OutputDuplicateSuppressionWindow)
            {
                continue;
            }

            if (string.Equals(_lastOutputMessage, normalized, StringComparison.Ordinal) &&
                (now - _lastOutputMessageUtc) <= OutputDuplicateSuppressionWindow)
            {
                continue;
            }

            _outputLogLines.Enqueue($"[{now:HH:mm:ss}] {normalized}");
            _lastOutputMessage = normalized;
            _lastOutputMessageUtc = now;
            _lastOutputMessageByText[normalized] = now;
            appended = true;
        }

        if (!appended)
        {
            return;
        }

        while (_outputLogLines.Count > MaxOutputLogLines)
        {
            _outputLogLines.Dequeue();
        }

        if (_lastOutputMessageByText.Count > 4096)
        {
            var pruneBefore = DateTime.Now - (OutputDuplicateSuppressionWindow + OutputDuplicateSuppressionWindow);
            var stale = _lastOutputMessageByText
                .Where(kvp => kvp.Value < pruneBefore)
                .Select(kvp => kvp.Key)
                .ToArray();
            foreach (var key in stale)
            {
                _lastOutputMessageByText.Remove(key);
            }
        }

        OutputLogTextBox.Text = string.Join(Environment.NewLine, _outputLogLines);
        OutputLogTextBox.CaretIndex = OutputLogTextBox.Text.Length;
        OutputLogTextBox.ScrollToEnd();
    }

    private void PostUi(Action action)
    {
        if (Volatile.Read(ref _shutdownRequested) != 0 ||
            Dispatcher.HasShutdownStarted ||
            Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            _ = Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        }
        catch (InvalidOperationException)
        {
            // Dispatcher shutdown raced a background status update.
        }
    }

    private static Dictionary<string, ServiceHealthEntry>? ParseServiceTelemetryFromState(JsonElement stateRoot)
    {
        if (!TryGetProperty(stateRoot, "serviceTelemetry", out var telemetry) || telemetry.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var map = new Dictionary<string, ServiceHealthEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in telemetry.EnumerateObject())
        {
            var status = "INIT";
            var error = string.Empty;

            if (TryGetProperty(service.Value, "lastStatus", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
            {
                status = statusProp.GetString() ?? "INIT";
            }

            if (TryGetProperty(service.Value, "lastError", out var errProp) && errProp.ValueKind == JsonValueKind.String)
            {
                error = errProp.GetString() ?? string.Empty;
            }

            map[service.Name] = new ServiceHealthEntry(status, error);
        }

        return map;
    }

    private void IngestRemoteLogEntries(JsonElement entries, bool spike)
    {
        if (entries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var previousCursor = spike ? _lastRemoteSpikeLogWallClockMs : _lastRemoteOutputLogWallClockMs;
        var nextCursor = previousCursor;
        var capacity = Math.Min(entries.GetArrayLength(), spike ? 48 : 64);
        var outputBatch = spike ? null : new List<string>(capacity);
        var spikeBatch = spike ? new List<string>(capacity) : null;
        foreach (var entry in entries.EnumerateArray())
        {
            var wallClockMs = GetLong(entry, "wallClockUnixMs", "wall_clock_unix_ms");
            if (wallClockMs > 0 && wallClockMs <= previousCursor)
            {
                continue;
            }

            var message = GetString(entry, "message");
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            if (wallClockMs > nextCursor)
            {
                nextCursor = wallClockMs;
            }

            if (spike)
            {
                spikeBatch!.Add($"CP: {message}");
            }
            else
            {
                outputBatch!.Add($"CP: {message}");
            }
        }

        if (spike)
        {
            foreach (var message in spikeBatch!)
            {
                AddSpikeLog(message);
            }
            _lastRemoteSpikeLogWallClockMs = nextCursor;
        }
        else
        {
            AddOutputLogs(outputBatch!);
            _lastRemoteOutputLogWallClockMs = nextCursor;
        }
    }

    private void QueueTelemetryPaneError(string issue)
    {
        QueuePaneUpdate(_transportStatsPaneWorker, $"Transport stats unavailable: {issue}", SetTransportStatsText);
        QueuePaneUpdate(_brainDashboardPaneWorker, $"Brain dashboard unavailable: {issue}", SetBrainDashboardText);
        QueuePaneUpdate(_inhabitancePaneWorker, $"Inhabitance unavailable: {issue}", SetInhabitanceText);
        QueuePaneUpdate(_circuitAuditPaneWorker, $"Circuit audit unavailable: {issue}", SetCircuitAuditText);
        QueuePaneUpdate(_reasoningPaneWorker, $"Reasoning telemetry unavailable: {issue}", SetReasoningText);
    }

    private void QueueTelemetryPaneUpdates(JsonElement stateRoot, bool includeTransportStats = true)
    {
        if (includeTransportStats)
        {
            QueueFormattedTelemetryPane(_transportStatsPaneWorker, stateRoot, FormatTransportStats, SetTransportStatsText);
        }
        QueueFormattedTelemetryPane(_brainDashboardPaneWorker, stateRoot, FormatBrainDashboard, SetBrainDashboardText);
        QueueFormattedTelemetryPane(_inhabitancePaneWorker, stateRoot, FormatInhabitanceTelemetry, SetInhabitanceText);
        QueueFormattedTelemetryPane(_circuitAuditPaneWorker, stateRoot, FormatCircuitAudit, SetCircuitAuditText);
        QueueFormattedTelemetryPane(_reasoningPaneWorker, stateRoot, FormatReasoningState, SetReasoningText);
    }

    private void QueueFormattedTelemetryPane(
        PaneWorker worker,
        JsonElement stateRoot,
        Func<JsonElement, string> formatter,
        Action<string> setter)
    {
        var paneRoot = stateRoot.Clone();
        worker.Post(async token =>
        {
            var text = formatter(paneRoot);
            await Dispatcher.InvokeAsync(() => setter(text), DispatcherPriority.Background, token);
        });
    }

    private void QueuePaneUpdate(PaneWorker worker, string text, Action<string> setter)
    {
        worker.Post(async token =>
        {
            await Dispatcher.InvokeAsync(() => setter(text), DispatcherPriority.Background, token);
        });
    }

    private async Task RefreshTransportStatsPanelAsync(Uri? preferredBaseUri = null)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTransportStatsRefreshUtc) < TimeSpan.FromMilliseconds(1500))
        {
            return;
        }

        _lastTransportStatsRefreshUtc = now;
        string? lastIssue = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(3200));
        var baseUri = preferredBaseUri ?? await ResolveVerifiedControlBaseUriAsync(cts.Token);
        if (baseUri is null)
        {
            QueueTelemetryPaneError("no verified Control Program endpoint.");
            return;
        }

        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/state"), cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                NoteControlEndpointFailure();
                QueueTelemetryPaneError($"HTTP {(int)response.StatusCode} from {baseUri}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            var stateRoot = doc.RootElement.Clone();
            NoteControlEndpointSuccess(baseUri);
            QueueTelemetryPaneUpdates(stateRoot);
            SyncReasoningControlsFromState(stateRoot);
        }
        catch (Exception ex)
        {
            NoteControlEndpointFailure();
            lastIssue = $"{baseUri}: {ex.Message}";
            QueueTelemetryPaneError(lastIssue);
        }
    }

    // Telemetry text-panel setters moved to MainWindow.Telemetry.cs.

    private async Task RefreshStructureStatusBadgesAsync()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastStatusBadgeRefreshUtc) < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastStatusBadgeRefreshUtc = now;
        var (telemetry, _) = await FetchServiceTelemetryAsync();
        if (telemetry is null)
        {
            return;
        }

        ApplyStructureStatusBadges(telemetry, null);
    }

    private async Task<(Dictionary<string, ServiceHealthEntry>? Telemetry, string? QueryIssue)> FetchServiceTelemetryAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(4500));
        var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
        if (baseUri is null)
        {
            return (null, "no verified Control Program endpoint");
        }

        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/service-health"), cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                using var fallbackResponse = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/state"), cts.Token);
                if (!fallbackResponse.IsSuccessStatusCode)
                {
                    NoteControlEndpointFailure();
                    return (null, $"HTTP {(int)fallbackResponse.StatusCode} from {baseUri}");
                }

                var fallbackJson = await fallbackResponse.Content.ReadAsStringAsync(cts.Token);
                using var fallbackDoc = JsonDocument.Parse(fallbackJson);
                if (!TryGetProperty(fallbackDoc.RootElement, "serviceTelemetry", out var fallbackTelemetry) || fallbackTelemetry.ValueKind != JsonValueKind.Object)
                {
                    return (null, $"/api/v1/state missing serviceTelemetry on {baseUri}");
                }

                var fallbackMap = ParseServiceTelemetryMap(fallbackTelemetry);
                if (fallbackMap.Count == 0)
                {
                    return (null, $"/api/v1/state returned empty service telemetry on {baseUri}");
                }

                NoteControlEndpointSuccess(baseUri);
                _lastServiceTelemetrySnapshot = new Dictionary<string, ServiceHealthEntry>(fallbackMap, StringComparer.OrdinalIgnoreCase);
                _lastServiceTelemetrySnapshotUtc = DateTime.UtcNow;
                return (fallbackMap, null);
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, $"/api/v1/service-health returned malformed payload on {baseUri}");
            }

            var map = ParseServiceTelemetryMap(doc.RootElement);
            if (map.Count == 0)
            {
                return (null, $"/api/v1/service-health returned empty payload on {baseUri}");
            }

            NoteControlEndpointSuccess(baseUri);
            _lastServiceTelemetrySnapshot = new Dictionary<string, ServiceHealthEntry>(map, StringComparer.OrdinalIgnoreCase);
            _lastServiceTelemetrySnapshotUtc = DateTime.UtcNow;
            return (map, null);
        }
        catch (OperationCanceledException)
        {
            if (_lastServiceTelemetrySnapshot is not null &&
                (DateTime.UtcNow - _lastServiceTelemetrySnapshotUtc) <= ServiceHealthTelemetryCacheWindow)
            {
                return (new Dictionary<string, ServiceHealthEntry>(_lastServiceTelemetrySnapshot, StringComparer.OrdinalIgnoreCase), null);
            }

            NoteControlEndpointFailure();
            return (null, $"{baseUri}: request timed out while reading service telemetry");
        }
        catch (Exception ex)
        {
            if (_lastServiceTelemetrySnapshot is not null &&
                (DateTime.UtcNow - _lastServiceTelemetrySnapshotUtc) <= ServiceHealthTelemetryCacheWindow)
            {
                return (new Dictionary<string, ServiceHealthEntry>(_lastServiceTelemetrySnapshot, StringComparer.OrdinalIgnoreCase), null);
            }

            NoteControlEndpointFailure();
            return (null, $"{baseUri}: {ex.Message}");
        }
    }

    private static Dictionary<string, ServiceHealthEntry> ParseServiceTelemetryMap(JsonElement telemetryRoot)
    {
        var map = new Dictionary<string, ServiceHealthEntry>(StringComparer.OrdinalIgnoreCase);
        if (telemetryRoot.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var service in telemetryRoot.EnumerateObject())
        {
            var status = "INIT";
            var error = string.Empty;

            if (TryGetProperty(service.Value, "lastStatus", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
            {
                status = statusProp.GetString() ?? "INIT";
            }

            if (TryGetProperty(service.Value, "lastError", out var errProp) && errProp.ValueKind == JsonValueKind.String)
            {
                error = errProp.GetString() ?? string.Empty;
            }

            map[service.Name] = new ServiceHealthEntry(status, error);
        }

        return map;
    }

    private void ApplyStructureStatusBadges(Dictionary<string, ServiceHealthEntry>? telemetry, string? unavailableReason)
    {
        foreach (var badge in _structureStatusBadges.Values)
        {
            if (telemetry is null)
            {
                SetStructureBadge(badge, "N/A", Color.FromRgb(79, 79, 92), unavailableReason ?? "Service health unavailable");
                continue;
            }

            if (!telemetry.TryGetValue(badge.SnapshotId, out var entry))
            {
                SetStructureBadge(badge, "N/A", Color.FromRgb(79, 79, 92), "No status reported");
                continue;
            }

            var (label, color) = entry.Status.ToUpperInvariant() switch
            {
                "OK" => ("OK", Color.FromRgb(31, 122, 72)),
                "DEGRADED" => ("DEG", Color.FromRgb(157, 113, 24)),
                "BACKOFF" => ("BKO", Color.FromRgb(153, 55, 55)),
                "INIT" => ("INIT", Color.FromRgb(69, 85, 115)),
                _ => ("UNK", Color.FromRgb(84, 84, 96))
            };

            var tooltip = string.IsNullOrWhiteSpace(entry.Error)
                ? $"{badge.DisplayName}: {entry.Status}"
                : $"{badge.DisplayName}: {entry.Status} ({entry.Error})";
            SetStructureBadge(badge, label, color, tooltip);
        }
    }

    private void SetStructureBadge(StructureStatusBadge badge, string label, Color color, string tooltip)
    {
        badge.BadgeText.Text = label;
        badge.BadgeBorder.Background = GetFrozenStatusBrush(color);
        badge.BadgeBorder.ToolTip = tooltip;
    }

    private SolidColorBrush GetFrozenStatusBrush(Color color)
    {
        var key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (_statusBadgeBrushes.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _statusBadgeBrushes[key] = brush;
        return brush;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        var target = Normalize(propertyName);
        foreach (var prop in element.EnumerateObject())
        {
            if (Normalize(prop.Name) == target)
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static List<string> ParseTopNeuronIds(JsonElement state)
    {
        if (!TryGetProperty(state, "topActiveNeurons", out var top) &&
            !TryGetProperty(state, "top_active_neurons", out top))
        {
            return [];
        }

        if (top.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var ids = new List<string>(20);
        foreach (var item in top.EnumerateArray())
        {
            if (!TryGetProperty(item, "neuronId", out var neuronIdElement) &&
                !TryGetProperty(item, "neuron_id", out neuronIdElement))
            {
                continue;
            }

            var id = neuronIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private List<DispatchSpikeTrace> ParseDispatchSpikeTraces(JsonElement frame)
    {
        if (!TryGetProperty(frame, "dispatchSpikes", out var dispatchSpikes) ||
            dispatchSpikes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var traces = new List<DispatchSpikeTrace>(dispatchSpikes.GetArrayLength());
        var previousCursor = _lastRemoteDispatchWallClockMs;
        var nextCursor = previousCursor;
        foreach (var item in dispatchSpikes.EnumerateArray())
        {
            var sourceStructure = ParseAnyStructureId(item, "sourceStructure");
            var targetStructure = ParseAnyStructureId(item, "targetStructure");
            var sourceNeuronId = GetString(item, "sourceNeuronId");
            var targetNeuronId = GetString(item, "targetNeuronId");
            var sourceHemisphere = GetString(item, "sourceHemisphere");
            var targetHemisphere = GetString(item, "targetHemisphere");
            var wallClockUnixMs = GetLong(item, "wallClockUnixMs");

            if (wallClockUnixMs > 0 && wallClockUnixMs <= previousCursor)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(sourceStructure) ||
                string.IsNullOrWhiteSpace(targetStructure) ||
                string.IsNullOrWhiteSpace(sourceNeuronId) ||
                string.IsNullOrWhiteSpace(targetNeuronId))
            {
                continue;
            }

            var normalizedSourceNeuron = EnsureHemisphereNeuronId(sourceNeuronId, sourceHemisphere);
            var normalizedTargetNeuron = EnsureHemisphereNeuronId(targetNeuronId, targetHemisphere);
            traces.Add(new DispatchSpikeTrace(
                sourceStructure,
                normalizedSourceNeuron,
                targetStructure,
                normalizedTargetNeuron,
                wallClockUnixMs));

            if (wallClockUnixMs > nextCursor)
            {
                nextCursor = wallClockUnixMs;
            }
        }

        _lastRemoteDispatchWallClockMs = nextCursor;
        return traces;
    }

    private static Dictionary<string, HashSet<string>> BuildDispatchNeuronIdLookup(IEnumerable<DispatchSpikeTrace> traces)
    {
        var lookup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var trace in traces)
        {
            AddDispatchNeuronId(lookup, trace.SourceStructure, trace.SourceNeuronId);
            AddDispatchNeuronId(lookup, trace.TargetStructure, trace.TargetNeuronId);
        }

        return lookup;
    }

    private static int CountDistinctConcreteNeuronIds(Dictionary<string, HashSet<string>> idsByStructure)
    {
        if (idsByStructure.Count == 0)
        {
            return 0;
        }

        var distinct = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ids in idsByStructure.Values)
        {
            foreach (var id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id) &&
                    !id.Contains("population-", StringComparison.OrdinalIgnoreCase))
                {
                    distinct.Add(id);
                }
            }
        }

        return distinct.Count;
    }

    private static List<DispatchPathwayActivity> BuildDispatchPathwayActivities(IEnumerable<DispatchSpikeTrace> traces)
    {
        var aggregate = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var trace in traces)
        {
            if (string.IsNullOrWhiteSpace(trace.SourceStructure) || string.IsNullOrWhiteSpace(trace.TargetStructure))
            {
                continue;
            }

            var hemi = ResolveHemisphereFromNeuronId(trace.SourceNeuronId);
            var key = $"{trace.SourceStructure}>{trace.TargetStructure}|{hemi}";
            aggregate[key] = aggregate.TryGetValue(key, out var count) ? (count + 1) : 1;
        }

        var result = new List<DispatchPathwayActivity>(aggregate.Count);
        foreach (var pair in aggregate)
        {
            var split = pair.Key.LastIndexOf('|');
            if (split <= 0 || split >= pair.Key.Length - 1)
            {
                continue;
            }

            var pairKey = pair.Key[..split];
            var hemi = pair.Key[(split + 1)..];
            var edgeSplit = pairKey.IndexOf('>');
            if (edgeSplit <= 0 || edgeSplit >= pairKey.Length - 1)
            {
                continue;
            }

            var source = pairKey[..edgeSplit];
            var target = pairKey[(edgeSplit + 1)..];
            result.Add(new DispatchPathwayActivity(source, target, hemi, pair.Value));
        }

        return result;
    }

    private static bool PathwayHemisphereMatches(string visualHemisphere, string activityHemisphere)
    {
        var visual = string.IsNullOrWhiteSpace(visualHemisphere) ? "M" : visualHemisphere.Trim().ToUpperInvariant();
        var activity = string.IsNullOrWhiteSpace(activityHemisphere) ? "M" : activityHemisphere.Trim().ToUpperInvariant();
        if (activity is "L" or "R")
        {
            return visual.Equals(activity, StringComparison.OrdinalIgnoreCase);
        }
        if (IsCallosalHemisphere(activity))
        {
            return visual.Equals(activity, StringComparison.OrdinalIgnoreCase) || visual == "M";
        }

        return visual is "M" or "L" or "R";
    }

    private static bool IsCorpusCallosumPathway(string source, string target)
    {
        return string.Equals(source, "CorpusCallosum", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(target, "CorpusCallosum", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCallosalHemisphere(string hemisphere)
    {
        var normalized = string.IsNullOrWhiteSpace(hemisphere)
            ? string.Empty
            : hemisphere.Trim().Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        return normalized is "LTOR" or "RTOL";
    }

    private static void AddDispatchNeuronId(Dictionary<string, HashSet<string>> lookup, string structureId, string neuronId)
    {
        if (string.IsNullOrWhiteSpace(structureId) || string.IsNullOrWhiteSpace(neuronId))
        {
            return;
        }

        if (!lookup.TryGetValue(structureId, out var ids))
        {
            ids = new HashSet<string>(StringComparer.Ordinal);
            lookup[structureId] = ids;
        }

        ids.Add(neuronId.Trim());
    }

    private static string EnsureHemisphereNeuronId(string neuronId, string hemisphere)
    {
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return string.Empty;
        }

        var trimmed = neuronId.Trim();
        if (trimmed.IndexOf(':') > 0)
        {
            return trimmed;
        }

        if (string.IsNullOrWhiteSpace(hemisphere))
        {
            return trimmed;
        }

        var hemi = hemisphere.Trim().ToUpperInvariant();
        return $"{hemi}:{trimmed}";
    }

    private static string ResolveHemisphereFromNeuronId(string neuronId)
    {
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return "M";
        }

        var trimmed = neuronId.Trim();
        var split = trimmed.IndexOf(':');
        if (split > 0)
        {
            var prefix = trimmed[..split].Trim();
            if (prefix.Equals("L", StringComparison.OrdinalIgnoreCase))
            {
                return "L";
            }

            if (prefix.Equals("R", StringComparison.OrdinalIgnoreCase))
            {
                return "R";
            }
        }

        return "M";
    }

    private void LogUnmatchedSpikeNeuronOnce(string structureId, string hemisphere, string neuronId, string reason)
    {
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return;
        }

        var key = $"{structureId}|{hemisphere}|{neuronId}|{reason}";
        if (!_unmatchedSpikeDiagnostics.Add(key))
        {
            return;
        }

        AddOutputLog($"Spike neuron mapping miss: {structureId}/{hemisphere}/{neuronId} ({reason})");
    }

    private static string BuildMicrotubuleInspectorText(MicrotubuleTick? microtubules)
    {
        const string label = "Experimental: intracellular microtubule approximation";
        if (microtubules == null)
        {
            return $"{label} - no live diagnostics yet";
        }

        var state = microtubules.Enabled ? microtubules.Mode : "off";
        var experimental = microtubules.Experimental ? " experimental terms on" : " classical terms";
        return $"{label} - {state},{experimental}; stability {microtubules.MeanStability:0.00}, spine {microtubules.MeanSpineInvasionEligibility:0.00}, transport {microtubules.MeanTransportSupport:0.00}, consolidation x{microtubules.MeanConsolidationSupport:0.00}";
    }

    private static string BuildStructureDiagnosticsInspectorText(
        MicrotubuleTick? microtubules,
        BodySchemaTick? bodySchema,
        BasalGangliaTick? basalGanglia,
        CerebellarTick? cerebellar,
        VestibuloReticularTick? vestibuloReticular,
        SuperiorColliculusTick? superiorColliculus,
        HippocampalSpatialTick? hippocampalSpatial,
        SalienceAffectTick? salienceAffect,
        PrefrontalWorkingMemoryTick? prefrontalWorkingMemory,
        ThalamicAttentionGateTick? thalamicAttentionGate,
        HypothalamicHomeostasisTick? hypothalamicHomeostasis,
        SleepWakeArousalTick? sleepWakeArousal,
        DescendingDefenseTick? descendingDefense,
        DopamineRewardTick? dopamineReward,
        SeptohippocampalThetaTick? septohippocampalTheta,
        SpinalProprioceptiveTick? spinalProprioceptive,
        OlfactoryLimbicMemoryTick? olfactoryLimbicMemory,
        AuditoryLanguageMotorTick? auditoryLanguageMotor,
        VisualObjectRecognitionTick? visualObjectRecognition,
        string snapshotId)
    {
        var text = BuildMicrotubuleInspectorText(microtubules);
        if (bodySchema != null)
        {
            text += Environment.NewLine + BuildBodySchemaInspectorText(bodySchema);
        }
        else if (IsBodySchemaStructure(snapshotId))
        {
            text += Environment.NewLine + "Body map: awaiting live M1/S1/PPC diagnostics";
        }

        if (basalGanglia != null)
        {
            text += Environment.NewLine + BuildBasalGangliaInspectorText(basalGanglia);
        }
        else if (IsBasalGangliaDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Action gate: awaiting live basal ganglia diagnostics";
        }

        if (cerebellar != null)
        {
            text += Environment.NewLine + BuildCerebellarInspectorText(cerebellar);
        }
        else if (IsCerebellarDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Cerebellar correction: awaiting live loop diagnostics";
        }

        if (vestibuloReticular != null)
        {
            text += Environment.NewLine + BuildVestibuloReticularInspectorText(vestibuloReticular);
        }
        else if (IsVestibuloReticularDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Posture loop: awaiting live vestibulo-reticular diagnostics";
        }

        if (superiorColliculus != null)
        {
            text += Environment.NewLine + BuildSuperiorColliculusInspectorText(superiorColliculus);
        }
        else if (IsSuperiorColliculusDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Orienting loop: awaiting live superior-colliculus diagnostics";
        }

        if (hippocampalSpatial != null)
        {
            text += Environment.NewLine + BuildHippocampalSpatialInspectorText(hippocampalSpatial);
        }
        else if (IsHippocampalSpatialDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Spatial memory: awaiting live hippocampal-entorhinal diagnostics";
        }

        if (salienceAffect != null)
        {
            text += Environment.NewLine + BuildSalienceAffectInspectorText(salienceAffect);
        }
        else if (IsSalienceAffectDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Salience loop: awaiting live amygdala-insula-ACC diagnostics";
        }

        if (prefrontalWorkingMemory != null)
        {
            text += Environment.NewLine + BuildPrefrontalWorkingMemoryInspectorText(prefrontalWorkingMemory);
        }
        else if (IsPrefrontalWorkingMemoryDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Working memory: awaiting live PFC/MD-thalamus diagnostics";
        }

        if (thalamicAttentionGate != null)
        {
            text += Environment.NewLine + BuildThalamicAttentionGateInspectorText(thalamicAttentionGate);
        }
        else if (IsThalamicAttentionGateDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Thalamic gate: awaiting live TRN/thalamocortical diagnostics";
        }

        if (hypothalamicHomeostasis != null)
        {
            text += Environment.NewLine + BuildHypothalamicHomeostasisInspectorText(hypothalamicHomeostasis);
        }
        else if (IsHypothalamicHomeostasisDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Homeostasis loop: awaiting live hypothalamus/NTS/insula diagnostics";
        }

        if (sleepWakeArousal != null)
        {
            text += Environment.NewLine + BuildSleepWakeArousalInspectorText(sleepWakeArousal);
        }
        else if (IsSleepWakeArousalDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Sleep/wake loop: awaiting live hypothalamus/reticular/LC diagnostics";
        }

        if (descendingDefense != null)
        {
            text += Environment.NewLine + BuildDescendingDefenseInspectorText(descendingDefense);
        }
        else if (IsDescendingDefenseDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Defense loop: awaiting live amygdala/PAG/reticular diagnostics";
        }

        if (dopamineReward != null)
        {
            text += Environment.NewLine + BuildDopamineRewardInspectorText(dopamineReward);
        }
        else if (IsDopamineRewardDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Reward loop: awaiting live VTA/SNc/OFC/striatal diagnostics";
        }

        if (septohippocampalTheta != null)
        {
            text += Environment.NewLine + BuildSeptohippocampalThetaInspectorText(septohippocampalTheta);
        }
        else if (IsSeptohippocampalThetaDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Theta navigation: awaiting live septal/hippocampal diagnostics";
        }

        if (spinalProprioceptive != null)
        {
            text += Environment.NewLine + BuildSpinalProprioceptiveInspectorText(spinalProprioceptive);
        }
        else if (IsSpinalProprioceptiveDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Spinal reflex: awaiting live spinal/S1/cerebellar diagnostics";
        }

        if (olfactoryLimbicMemory != null)
        {
            text += Environment.NewLine + BuildOlfactoryLimbicMemoryInspectorText(olfactoryLimbicMemory);
        }
        else if (IsOlfactoryLimbicMemoryDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Odor memory: awaiting live olfactory/limbic/hippocampal diagnostics";
        }

        if (auditoryLanguageMotor != null)
        {
            text += Environment.NewLine + BuildAuditoryLanguageMotorInspectorText(auditoryLanguageMotor);
        }
        else if (IsAuditoryLanguageMotorDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Language motor: awaiting live A1/Wernicke/Broca/motor diagnostics";
        }

        if (visualObjectRecognition != null)
        {
            text += Environment.NewLine + BuildVisualObjectRecognitionInspectorText(visualObjectRecognition);
        }
        else if (IsVisualObjectRecognitionDiagnosticsStructure(snapshotId))
        {
            text += Environment.NewLine + "Object recognition: awaiting live V1/V4/temporal diagnostics";
        }

        return text;
    }

    private static string BuildBodySchemaInspectorText(BodySchemaTick bodySchema)
    {
        var bodyZone = BlankAsDash(bodySchema.DominantBodyZone);
        var spatialZone = BlankAsDash(bodySchema.DominantSpatialZone);
        var text =
            $"Body map: {bodyZone}; face/head {bodySchema.FaceHeadActivation:0.0} Hz, " +
            $"hand/arm {bodySchema.HandArmActivation:0.0} Hz, trunk {bodySchema.TrunkActivation:0.0} Hz, " +
            $"leg/foot {bodySchema.LegFootActivation:0.0} Hz";

        if (!spatialZone.Equals("Somatotopic", StringComparison.OrdinalIgnoreCase))
        {
            text +=
                $"; space {spatialZone} (near {bodySchema.NearBodyActivation:0.0}, " +
                $"left {bodySchema.LeftPeripersonalActivation:0.0}, right {bodySchema.RightPeripersonalActivation:0.0}, " +
                $"far {bodySchema.FarSpaceActivation:0.0} Hz)";
        }

        return text;
    }

    private static bool IsBodySchemaStructure(string snapshotId)
        => snapshotId.Equals("M1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("S1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Ppc", StringComparison.OrdinalIgnoreCase);

    private static string BuildBasalGangliaInspectorText(BasalGangliaTick basalGanglia)
    {
        return
            $"Action gate: {BlankAsDash(basalGanglia.DominantMode)}; " +
            $"direct {basalGanglia.DirectPathwayActivation:0.0} Hz, " +
            $"indirect {basalGanglia.IndirectPathwayActivation:0.0} Hz, " +
            $"hyperdirect {basalGanglia.HyperdirectPathwayActivation:0.0} Hz; " +
            $"GPi/SNr output {basalGanglia.OutputNucleusInhibition:0.0} Hz, " +
            $"thalamic release {basalGanglia.ThalamicDisinhibition:0.0} Hz, " +
            $"dopamine x{basalGanglia.DopamineModulation:0.00}, bias {basalGanglia.ActionSelectionBias:+0.0;-0.0;0.0}";
    }

    private static bool IsBasalGangliaDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("Striatum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("NucleusAccumbens", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("GlobusPallidus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("VentralPallidum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("GPe", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("GPi", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Stn", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Snr", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Snc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase);

    private static string BuildCerebellarInspectorText(CerebellarTick cerebellar)
    {
        return
            $"Cerebellar correction: {BlankAsDash(cerebellar.CorrectionMode)}; " +
            $"mossy {cerebellar.MossyFiberDrive:0.0} Hz, " +
            $"climbing error {cerebellar.ClimbingFiberError:0.0} Hz, " +
            $"Purkinje inhibition {cerebellar.PurkinjeInhibition:0.0} Hz, " +
            $"DCN output {cerebellar.DeepNucleusOutput:0.0} Hz, " +
            $"vermis stabilize {cerebellar.VermisStabilization:0.0} Hz, " +
            $"gain {cerebellar.CorrectionGain:0.0}, error {cerebellar.PredictionError:0.0}";
    }

    private static bool IsCerebellarDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("CerebellarGranule", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CerebellarVermis", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CerebellarLobules", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("PurkinjeCellLayer", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("DeepCerebellarNuclei", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("InferiorOlive", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("M1", StringComparison.OrdinalIgnoreCase);

    private static string BuildVestibuloReticularInspectorText(VestibuloReticularTick posture)
    {
        return
            $"Posture loop: {BlankAsDash(posture.PostureMode)}; " +
            $"vestibular {posture.VestibularDrive:0.0} Hz, " +
            $"reticular arousal {posture.ReticularArousal:0.0} Hz, " +
            $"vermis correction {posture.VermisBalanceCorrection:0.0} Hz, " +
            $"spinal tone {posture.SpinalMotorTone:0.0} Hz, " +
            $"stability {posture.PostureStability:0.0}, balance error {posture.BalanceError:0.0}";
    }

    private static bool IsVestibuloReticularDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("VestibularNuclei", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("ReticularFormation", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CerebellarVermis", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("SpinalCordMotor", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CerebellarLobules", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("DeepCerebellarNuclei", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("M1", StringComparison.OrdinalIgnoreCase);

    private static string BuildSuperiorColliculusInspectorText(SuperiorColliculusTick orienting)
    {
        return
            $"Orienting loop: {BlankAsDash(orienting.OrientingMode)}; " +
            $"visual {orienting.VisualOrientingDrive:0.0} Hz, " +
            $"auditory {orienting.AuditoryOrientingDrive:0.0} Hz, " +
            $"SNr brake {orienting.NigrotectalInhibition:0.0} Hz, " +
            $"pulvinar attention {orienting.PulvinarAttention:0.0} Hz, " +
            $"head/eye command {orienting.HeadEyeCommand:0.0} Hz, " +
            $"readiness {orienting.SaccadeReadiness:0.0}, salience {orienting.SalienceBias:0.0}";
    }

    private static bool IsSuperiorColliculusDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("Retina", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("V1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Mt", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("InferiorColliculus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("SuperiorColliculus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Snr", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pulvinar", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Ppc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("PremotorCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pons", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("M1", StringComparison.OrdinalIgnoreCase);

    private static string BuildHippocampalSpatialInspectorText(HippocampalSpatialTick spatial)
    {
        return
            $"Spatial memory: {BlankAsDash(spatial.MemoryMode)}; " +
            $"grid {spatial.EntorhinalGridDrive:0.0} Hz, " +
            $"dentate separation {spatial.DentatePatternSeparation:0.0} Hz, " +
            $"CA3 completion {spatial.Ca3PatternCompletion:0.0} Hz, " +
            $"CA1 place {spatial.Ca1PlaceIndex:0.0} Hz, " +
            $"subiculum {spatial.SubicularOutput:0.0} Hz, " +
            $"head direction {spatial.HeadDirectionAlignment:0.0} Hz, " +
            $"coherence {spatial.SpatialCoherence:0.0}, novelty {spatial.NoveltyMismatch:0.0}";
    }

    private static bool IsHippocampalSpatialDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("EntorhinalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("DentateGyrus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA3", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA2", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Subiculum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Presubiculum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Parasubiculum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("RetrosplenialCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("ParahippocampalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Ppc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("VestibularNuclei", StringComparison.OrdinalIgnoreCase);

    private static string BuildSalienceAffectInspectorText(SalienceAffectTick salience)
    {
        return
            $"Salience loop: {BlankAsDash(salience.SalienceMode)}; " +
            $"threat {salience.ThreatSalience:0.0} Hz, " +
            $"interoception {salience.InteroceptiveDrive:0.0} Hz, " +
            $"ACC conflict {salience.ConflictMonitoring:0.0} Hz, " +
            $"arousal {salience.AutonomicArousal:0.0} Hz, " +
            $"attention {salience.AttentionGain:0.0} Hz, " +
            $"defense {salience.DefensiveReadiness:0.0} Hz, " +
            $"control {salience.ControlBias:0.0}, affect {salience.AffectIntensity:0.0}";
    }

    private static bool IsSalienceAffectDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("Amygdala", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Insula", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Acc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Hypothalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("LocusCoeruleus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("BasalForebrain", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("NucleusAccumbens", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pfc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("PeriaqueductalGray", StringComparison.OrdinalIgnoreCase);

    private static string BuildPrefrontalWorkingMemoryInspectorText(PrefrontalWorkingMemoryTick workingMemory)
    {
        return
            $"Working memory: {BlankAsDash(workingMemory.ControlMode)}; " +
            $"PFC persist {workingMemory.PfcPersistentActivity:0.0} Hz, " +
            $"MD support {workingMemory.MediodorsalThalamicSupport:0.0} Hz, " +
            $"frontoparietal {workingMemory.FrontoparietalContext:0.0} Hz, " +
            $"semantic {workingMemory.SemanticContext:0.0} Hz, " +
            $"striatal gate {workingMemory.StriatalGate:0.0} Hz, " +
            $"ACC demand {workingMemory.AccControlDemand:0.0} Hz, " +
            $"top-down {workingMemory.TopDownBias:0.0}, stability {workingMemory.TaskSetStability:0.0}";
    }

    private static bool IsPrefrontalWorkingMemoryDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("Pfc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("MediodorsalThalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Ppc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("TemporalAssociation", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Striatum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Acc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("OrbitofrontalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("BasalForebrain", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("LocusCoeruleus", StringComparison.OrdinalIgnoreCase);

    private static string BuildThalamicAttentionGateInspectorText(ThalamicAttentionGateTick gate)
    {
        return
            $"Thalamic gate: {BlankAsDash(gate.GateMode)}; " +
            $"relay {gate.ThalamocorticalRelay:0.0} Hz, " +
            $"TRN brake {gate.TrnInhibitoryGate:0.0} Hz, " +
            $"pulvinar {gate.PulvinarSpotlight:0.0} Hz, " +
            $"MD access {gate.MediodorsalAccess:0.0} Hz, " +
            $"intralaminar {gate.IntralaminarBroadcast:0.0} Hz, " +
            $"sensory gain {gate.SensoryGain:0.0}, cortical access {gate.CorticalAccess:0.0}, selection {gate.RelaySelectionBias:0.0}";
    }

    private static bool IsThalamicAttentionGateDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("Thalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Trn", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pulvinar", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("MediodorsalThalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("IntralaminarThalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pfc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Ppc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("V1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("A1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("S1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("BasalForebrain", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("LocusCoeruleus", StringComparison.OrdinalIgnoreCase);

    private static string BuildHypothalamicHomeostasisInspectorText(HypothalamicHomeostasisTick homeostasis)
    {
        return
            $"Homeostasis loop: {BlankAsDash(homeostasis.HomeostasisMode)}; " +
            $"NTS visceral {homeostasis.VisceralAfferentDrive:0.0} Hz, " +
            $"set-point error {homeostasis.HypothalamicSetpointError:0.0}, " +
            $"insula feeling {homeostasis.InsulaBodyFeeling:0.0} Hz, " +
            $"limbic pressure {homeostasis.LimbicHomeostaticPressure:0.0} Hz, " +
            $"brainstem drive {homeostasis.AutonomicBrainstemDrive:0.0}, " +
            $"arousal {homeostasis.ArousalPressure:0.0}, comfort deficit {homeostasis.ComfortDeficit:0.0}, " +
            $"defense {homeostasis.DefensiveBodyCommand:0.0}";
    }

    private static bool IsHypothalamicHomeostasisDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("NucleusTractusSolitarius", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Hypothalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Insula", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Amygdala", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("LocusCoeruleus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("RapheNuclei", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("BasalForebrain", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pons", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Medulla", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("ReticularFormation", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("PeriaqueductalGray", StringComparison.OrdinalIgnoreCase);

    private static string BuildSleepWakeArousalInspectorText(SleepWakeArousalTick arousal)
    {
        return
            $"Sleep/wake loop: {BlankAsDash(arousal.ArousalMode)}; " +
            $"hypothalamic pressure {arousal.HypothalamicSleepPressure:0.0} Hz, " +
            $"reticular drive {arousal.ReticularActivatingDrive:0.0} Hz, " +
            $"pons/medulla {arousal.PontomedullaryStateTone:0.0} Hz, " +
            $"LC wake {arousal.LocusCoeruleusWakeTone:0.0} Hz, " +
            $"raphe tone {arousal.RapheStabilizationTone:0.0} Hz, " +
            $"basal forebrain {arousal.BasalForebrainWakeDrive:0.0} Hz, " +
            $"intralaminar {arousal.IntralaminarArousalBroadcast:0.0} Hz, readiness {arousal.CorticalReadiness:0.0}";
    }

    private static bool IsSleepWakeArousalDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("Hypothalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("ReticularFormation", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pons", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Medulla", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("LocusCoeruleus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("RapheNuclei", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("BasalForebrain", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("IntralaminarThalamus", StringComparison.OrdinalIgnoreCase);

    private static string BuildDescendingDefenseInspectorText(DescendingDefenseTick defense)
    {
        return
            $"Defense loop: {BlankAsDash(defense.DefenseMode)}; " +
            $"amygdala {defense.AmygdalaThreatDrive:0.0} Hz, " +
            $"hypothalamus {defense.HypothalamicDefenseDrive:0.0} Hz, " +
            $"PAG command {defense.PagDefensiveCommand:0.0} Hz, " +
            $"raphe modulation {defense.RaphePainModulation:0.0} Hz, " +
            $"medulla {defense.MedullaryAutonomicSupport:0.0} Hz, " +
            $"reticular release {defense.ReticularPatternRelease:0.0} Hz, " +
            $"spinal withdrawal {defense.SpinalWithdrawalDrive:0.0} Hz, protection {defense.ProtectionReadiness:0.0}";
    }

    private static bool IsDescendingDefenseDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("Amygdala", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Hypothalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("PeriaqueductalGray", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("RapheNuclei", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Medulla", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("ReticularFormation", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("SpinalCordMotor", StringComparison.OrdinalIgnoreCase);

    private static string BuildDopamineRewardInspectorText(DopamineRewardTick reward)
    {
        return
            $"Reward loop: {BlankAsDash(reward.RewardMode)}; " +
            $"VTA {reward.VtaPhasicDopamine:0.0} Hz, " +
            $"SNc teaching {reward.SncActionTeaching:0.0} Hz, " +
            $"accumbens {reward.NucleusAccumbensIncentive:0.0} Hz, " +
            $"striatum value {reward.StriatalActionValue:0.0} Hz, " +
            $"habenula {reward.HabenulaNegativePrediction:0.0} Hz, " +
            $"OFC expected {reward.OrbitofrontalExpectedValue:0.0} Hz, " +
            $"PFC goal {reward.PfcGoalBias:0.0} Hz, RPE {reward.RewardPredictionError:+0.00;-0.00;0.00}, readiness {reward.LearningReadiness:0.0}";
    }

    private static bool IsDopamineRewardDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("Vta", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Snc", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("NucleusAccumbens", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Striatum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Habenula", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("OrbitofrontalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pfc", StringComparison.OrdinalIgnoreCase);

    private static string BuildSeptohippocampalThetaInspectorText(SeptohippocampalThetaTick theta)
    {
        return
            $"Theta navigation: {BlankAsDash(theta.ThetaMode)}; " +
            $"septal drive {theta.SeptalThetaDrive:0.0} Hz, " +
            $"EC grid {theta.EntorhinalGridPhase:0.0} Hz, " +
            $"DG gate {theta.DentateEncodingGate:0.0} Hz, " +
            $"CA3 sequence {theta.Ca3SequenceReplay:0.0} Hz, " +
            $"CA1 place {theta.Ca1PlaceTiming:0.0} Hz, " +
            $"subiculum {theta.SubicularNavigationOutput:0.0} Hz, " +
            $"head direction {theta.HeadDirectionAlignment:0.0} Hz, " +
            $"RSC anchor {theta.RetrosplenialSceneAnchor:0.0} Hz, " +
            $"vestibular {theta.VestibularPathIntegration:0.0} Hz, coherence {theta.ThetaCoherence:0.0}";
    }

    private static bool IsSeptohippocampalThetaDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("BasalForebrain", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("EntorhinalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("DentateGyrus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA3", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA2", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Subiculum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Presubiculum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Parasubiculum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("RetrosplenialCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("VestibularNuclei", StringComparison.OrdinalIgnoreCase);

    private static string BuildSpinalProprioceptiveInspectorText(SpinalProprioceptiveTick reflex)
    {
        return
            $"Spinal reflex: {BlankAsDash(reflex.ReflexMode)}; " +
            $"spinal {reflex.SpinalReflexDrive:0.0} Hz, " +
            $"S1 proprio {reflex.S1ProprioceptiveMap:0.0} Hz, " +
            $"M1 command {reflex.M1DescendingCommand:0.0} Hz, " +
            $"mossy feedback {reflex.CerebellarMossyFeedback:0.0} Hz, " +
            $"vestibular {reflex.VestibularBalanceInput:0.0} Hz, " +
            $"reticular set {reflex.ReticularPosturalSet:0.0} Hz, " +
            $"thalamic relay {reflex.ThalamicRelayTone:0.0} Hz, readiness {reflex.ReflexReadiness:0.0}, coherence {reflex.ProprioceptiveCoherence:0.0}";
    }

    private static bool IsSpinalProprioceptiveDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("SpinalCordMotor", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("S1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("M1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CerebellarGranule", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("VestibularNuclei", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("ReticularFormation", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Thalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase);

    private static string BuildOlfactoryLimbicMemoryInspectorText(OlfactoryLimbicMemoryTick memory)
    {
        return
            $"Odor memory: {BlankAsDash(memory.MemoryMode)}; " +
            $"olfactory {memory.OlfactoryCueDrive:0.0} Hz, " +
            $"temporal/piriform {memory.TemporalPiriformAssociation:0.0} Hz, " +
            $"amygdala tag {memory.AmygdalaAffectiveTag:0.0} Hz, " +
            $"EC gate {memory.EntorhinalMemoryGate:0.0} Hz, " +
            $"hippocampal index {memory.HippocampalEpisodeIndex:0.0} Hz, " +
            $"OFC valence {memory.OrbitofrontalValenceContext:0.0} Hz, " +
            $"PFC autobiographical {memory.PfcAutobiographicalControl:0.0} Hz, " +
            $"familiarity {memory.FamiliaritySignal:0.0}, coherence {memory.AutobiographicalCoherence:0.0}";
    }

    private static bool IsOlfactoryLimbicMemoryDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("OlfactoryBulb", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("TemporalAssociation", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("PerirhinalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("ParahippocampalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Amygdala", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("EntorhinalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("DentateGyrus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA3", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA2", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("CA1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Subiculum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("OrbitofrontalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pfc", StringComparison.OrdinalIgnoreCase);

    private static string BuildAuditoryLanguageMotorInspectorText(AuditoryLanguageMotorTick language)
    {
        return
            $"Language motor: {BlankAsDash(language.LanguageMode)}; " +
            $"A1 {language.A1AuditoryDrive:0.0} Hz, " +
            $"Wernicke {language.WernickeComprehension:0.0} Hz, " +
            $"arcuate {language.ArcuatePhonologicalRelay:0.0} Hz, " +
            $"Broca {language.BrocaSpeechSequence:0.0} Hz, " +
            $"premotor {language.PremotorArticulationPlan:0.0} Hz, " +
            $"M1 speech {language.M1SpeechMotorCommand:0.0} Hz, " +
            $"BG gate {language.BasalGangliaSpeechGate:0.0} Hz, " +
            $"motor thalamus {language.MotorThalamicRelay:0.0} Hz, coherence {language.LanguageMotorCoherence:0.0}";
    }

    private static bool IsAuditoryLanguageMotorDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("A1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("WernickePstgPsts", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("ArcuateFasciculus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("BrocaBa44Ba45", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("PremotorCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("M1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Striatum", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("GPi", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Snr", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Thalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase);

    private static string BuildVisualObjectRecognitionInspectorText(VisualObjectRecognitionTick visual)
    {
        return
            $"Object recognition: {BlankAsDash(visual.RecognitionMode)}; " +
            $"V1 {visual.V1EdgeDrive:0.0} Hz, " +
            $"V2 contours {visual.V2ContourIntegration:0.0} Hz, " +
            $"V4 features {visual.V4ObjectFeatureBinding:0.0} Hz, " +
            $"MT motion {visual.MtMotionCue:0.0} Hz, " +
            $"temporal identity {visual.TemporalObjectIdentity:0.0} Hz, " +
            $"perirhinal familiarity {visual.PerirhinalFamiliarity:0.0} Hz, " +
            $"pulvinar attention {visual.PulvinarVisualAttention:0.0} Hz, " +
            $"thalamic relay {visual.ThalamicRelayGain:0.0} Hz, " +
            $"PFC context {visual.PfcObjectContext:0.0} Hz, coherence {visual.ObjectRecognitionCoherence:0.0}";
    }

    private static bool IsVisualObjectRecognitionDiagnosticsStructure(string snapshotId)
        => snapshotId.Equals("V1", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("V2", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("V4", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Mt", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("TemporalAssociation", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("PerirhinalCortex", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pulvinar", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Thalamus", StringComparison.OrdinalIgnoreCase) ||
           snapshotId.Equals("Pfc", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string name) => name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();


    private static float GetSingle(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetSingle(out var result)) return result;
            if (prop.ValueKind == JsonValueKind.String && float.TryParse(prop.GetString(), out result)) return result;
        }

        return 0f;
    }

    private static int GetInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var result)) return result;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out result)) return result;
        }

        return 0;
    }

    private static long GetLong(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var result)) return result;
            if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out result)) return result;
        }

        return 0L;
    }

    private static double GetDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var result)) return result;
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out result)) return result;
        }

        return 0.0;
    }

    private static bool GetBool(JsonElement element, string name, bool defaultValue = false)
    {
        if (!TryGetProperty(element, name, out var prop))
        {
            return defaultValue;
        }

        if (prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return prop.GetBoolean();
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var numeric))
        {
            return numeric != 0;
        }

        if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static string GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.String) return prop.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string BlankAsDash(string value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string ReadStringArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return "-";
        }

        var values = array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        return values.Length == 0 ? "-" : string.Join("; ", values);
    }

    // Color/visual helpers extracted to MainWindow.Visuals.cs.


    private void YawSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_presetTransformsLocked && !_isApplyingPresetView)
        {
            return;
        }

        _targetYaw = e.NewValue;
    }

    private void NeuronBudgetSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _displayNeuronGridEdge = (int)Math.Round(e.NewValue);
        _displayNeuronsPerHemisphereBudget = _displayNeuronGridEdge * _displayNeuronGridEdge * _displayNeuronGridEdge;
        if (NeuronBudgetText is not null)
        {
            NeuronBudgetText.Text = FormatNeuronBudgetLabel(_displayNeuronGridEdge);
        }

        _densityDebounceTimer.Stop();
        _densityDebounceTimer.Start();
    }

    private void MinWakeTicksSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressMinWakeSliderEvents)
        {
            return;
        }

        _minWakeTicks = (int)Math.Round(e.NewValue);
        if (MinWakeTicksText is not null)
        {
            MinWakeTicksText.Text = _minWakeTicks.ToString();
        }

        if (!IsLoaded)
        {
            return;
        }

        _minWakeDebounceTimer.Stop();
        _minWakeDebounceTimer.Start();
    }

    private void SleepPressureEnterSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSleepPressureSliderEvents)
        {
            return;
        }

        _sleepPressureEnterThreshold = (float)e.NewValue;
        if (SleepPressureEnterText is not null)
        {
            SleepPressureEnterText.Text = _sleepPressureEnterThreshold.ToString("0.00");
        }

        if (!IsLoaded)
        {
            return;
        }

        _sleepPressureDebounceTimer.Stop();
        _sleepPressureDebounceTimer.Start();
    }

    private void AutoProfileControl_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressAutoProfileControlEvents)
        {
            return;
        }

        UpdateAutoProfileControlLabels();
        if (!IsLoaded)
        {
            return;
        }

        _autoProfileDebounceTimer.Stop();
        _autoProfileDebounceTimer.Start();
    }

    private void AutoProfileControl_OnChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressAutoProfileControlEvents)
        {
            return;
        }

        UpdateAutoProfileControlLabels();
        if (!IsLoaded)
        {
            return;
        }

        _autoProfileDebounceTimer.Stop();
        _autoProfileDebounceTimer.Start();
    }

    private async void InputGatesCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressInputGatesControlEvents || !IsLoaded)
        {
            return;
        }

        await SafeHandlerAsync(() => ApplyInputGatesControlsAsync(), "Apply input gates");
    }


    // ToggleWebcamInputButton_OnClick moved to MainWindow.Webcam.cs.

    // ToggleMicrophoneInputButton_OnClick moved to MainWindow.Microphone.cs.

    private async void SendLanguageInputButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(SendLanguageInputAsync, "Send language input");

    private void ToggleSpeechOutputButton_OnClick(object sender, RoutedEventArgs e)
    {
        _speechOutputEnabled = !_speechOutputEnabled;
        if (ToggleSpeechOutputButton is not null)
        {
            ToggleSpeechOutputButton.Content = _speechOutputEnabled ? "Disable Speech Output" : "Enable Speech Output";
        }

        if (_speechOutputEnabled)
        {
            _lastSpokenLanguageUtteranceSequence = _languageUtteranceSequence;
            UpdateSpeechStatusText("Speech: listening for activity");
            AddOutputLog($"Speech output enabled ({GetSpeechTriggerModeLabel(_speechTriggerMode)} trigger).");
            return;
        }

        while (_speechQueue.Reader.TryRead(out _))
        {
            // Drop stale utterances when speech output is disabled.
        }

        UpdateSpeechStatusText("Speech: disabled");
        AddOutputLog("Speech output disabled.");
    }

    private void SpeechTriggerModeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSpeechUiEvents || !IsLoaded)
        {
            return;
        }

        var selected = ParseSelectedSpeechTriggerMode();
        if (selected == _speechTriggerMode)
        {
            return;
        }

        _speechTriggerMode = selected;
        RefreshSpeechControlLabels();
        UpdateSpeechStatusText("Speech: listening for activity");
        AddOutputLog($"Speech trigger mode set to {GetSpeechTriggerModeLabel(_speechTriggerMode)}.");
    }

    private void SpeechThresholdSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var threshold = (int)Math.Round(e.NewValue);
        _speechMinDispatchSpikes = Math.Clamp(threshold, 1, 256);
        if (_suppressSpeechUiEvents || !IsLoaded)
        {
            return;
        }

        RefreshSpeechControlLabels();
        UpdateSpeechStatusText("Speech: listening for activity");
    }

    private void SpeechRateSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var rate = (int)Math.Round(e.NewValue);
        _speechRatePercent = Math.Clamp(rate, 50, 200);
        if (_suppressSpeechUiEvents || !IsLoaded)
        {
            return;
        }

        RefreshSpeechControlLabels();
        UpdateSpeechStatusText("Speech: listening for activity");
    }

    private void SpeechVolumeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var volume = (int)Math.Round(e.NewValue);
        _speechVolume = Math.Clamp(volume, 0, 100);
        if (_suppressSpeechUiEvents || !IsLoaded)
        {
            return;
        }

        RefreshSpeechControlLabels();
        UpdateSpeechStatusText("Speech: listening for activity");
    }

    private void InitializeSpeechControlsUi()
    {
        _suppressSpeechUiEvents = true;
        try
        {
            if (SpeechTriggerModeCombo is not null)
            {
                SpeechTriggerModeCombo.SelectedIndex = _speechTriggerMode == SpeechTriggerMode.GlobalDispatch ? 1 : 0;
            }

            if (SpeechThresholdSlider is not null)
            {
                SpeechThresholdSlider.Value = _speechMinDispatchSpikes;
            }

            if (SpeechRateSlider is not null)
            {
                SpeechRateSlider.Value = _speechRatePercent;
            }

            if (SpeechVolumeSlider is not null)
            {
                SpeechVolumeSlider.Value = _speechVolume;
            }
        }
        finally
        {
            _suppressSpeechUiEvents = false;
        }

        RefreshSpeechControlLabels();
        UpdateSpeechStatusText(_speechOutputEnabled ? "Speech: listening for activity" : "Speech: disabled");
        if (ToggleSpeechOutputButton is not null)
        {
            ToggleSpeechOutputButton.Content = _speechOutputEnabled ? "Disable Speech Output" : "Enable Speech Output";
        }
    }

    private void RefreshSpeechControlLabels()
    {
        if (SpeechThresholdText is not null)
        {
            SpeechThresholdText.Text = _speechMinDispatchSpikes.ToString();
        }

        if (SpeechRateText is not null)
        {
            SpeechRateText.Text = FormatSpeechRateLabel(_speechRatePercent);
        }

        if (SpeechVolumeText is not null)
        {
            SpeechVolumeText.Text = $"{_speechVolume}%";
        }
    }

    private void UpdateSpeechStatusText(string baseMessage)
    {
        if (SpeechOutputStatusText is null)
        {
            return;
        }

        if (!_speechOutputEnabled)
        {
            SpeechOutputStatusText.Text = "Speech: disabled";
            return;
        }

        SpeechOutputStatusText.Text = $"{baseMessage} ({GetSpeechTriggerModeLabel(_speechTriggerMode)}, min {_speechMinDispatchSpikes})";
    }

    private SpeechTriggerMode ParseSelectedSpeechTriggerMode()
    {
        if (SpeechTriggerModeCombo?.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString()?.Trim().ToLowerInvariant();
            if (tag == "global")
            {
                return SpeechTriggerMode.GlobalDispatch;
            }
        }

        return SpeechTriggerMode.LanguagePathway;
    }

    private static string FormatSpeechRateLabel(int percent)
    {
        var clamped = Math.Clamp(percent, 50, 200);
        return $"{clamped / 100.0:0.00}x";
    }

    private static string GetSpeechTriggerModeLabel(SpeechTriggerMode mode)
    {
        return mode switch
        {
            SpeechTriggerMode.GlobalDispatch => "Global dispatch",
            _ => "Language pathway"
        };
    }

    // Webcam input pipeline (toggle, capture loop, stimulus dispatch, preview, attention reticle,
    // visual stimulus dispatch, hemifield saliency) moved to MainWindow.Webcam.cs.




    // Microphone input pipeline (toggle, capture loop, RMS/ZCR, auditory + language stimulus
    // dispatch, level meter UI) moved to MainWindow.Microphone.cs.


    private async Task SendLanguageInputAsync()
    {
        if (_languageInputInFlight)
        {
            AddOutputLog("Language input request already in flight.");
            return;
        }

        var remaining = LanguageInputCooldown - (DateTime.UtcNow - _lastLanguageInputUtc);
        if (remaining > TimeSpan.Zero)
        {
            return;
        }

        var text = LanguageInputTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            AddOutputLog("Language input skipped: enter text to stimulate language pathways.");
            return;
        }

        if (_isSimulationSleeping)
        {
            LanguageInputStatusText.Text = "Language: paused during sleep";
            AddOutputLog("Language input paused: simulation is sleeping.");
            return;
        }

        _languageInputInFlight = true;
        _lastLanguageInputUtc = DateTime.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(3500));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                LanguageInputStatusText.Text = "Language: control endpoint unavailable";
                AddOutputLog("Language input skipped: Control Program endpoint not available.");
                return;
            }

            var mode = ResolveLanguageInputMode();
            var hemisphere = ResolveLanguageInputHemisphere();
            var tokenCountEstimate = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
            var intensity = (float)Math.Clamp(0.80 + (tokenCountEstimate * 0.06), 0.2, 3.0);
            var burstPerToken = Math.Clamp(5 + tokenCountEstimate, 4, 24);

            var result = await SendLanguageStimulusAsync(
                baseUri,
                text,
                mode,
                hemisphere,
                intensity,
                burstPerToken,
                cts.Token);

            if (result.PausedDueToSleep)
            {
                LanguageInputStatusText.Text = "Language: paused during sleep";
                AddOutputLog("Language input paused by ControlProgram sleep gate.");
                return;
            }

            var grammarSuffix = string.IsNullOrWhiteSpace(result.GrammarIntent)
                ? string.Empty
                : $" grammar={result.GrammarIntent}/{result.GrammarMood}";
            LanguageInputStatusText.Text = $"Language: {result.Mode} tokens={result.TokenCount}/{result.BrainTokenCount} del={result.Delivered}";
            AddOutputLog(
                $"Language input sent ({result.Mode}): delivered {result.Delivered}/{result.Generated} spikes across {result.TargetCount} targets{grammarSuffix}.");
            if (result.Delivered > 0)
            {
                RememberLanguageUtterance(string.IsNullOrWhiteSpace(result.Utterance) ? text : result.Utterance, force: true);
                AddSpikeLog(
                    $"Language {result.Mode}: delivered {result.Delivered} spikes ({result.TokenCount} tokens, {result.TargetCount} targets)");
            }
        }
        catch (Exception ex)
        {
            LanguageInputStatusText.Text = $"Language: error ({ex.Message})";
            AddOutputLog($"Language input failed: {ex.Message}");
        }
        finally
        {
            _languageInputInFlight = false;
        }
    }

    private string ResolveLanguageInputMode()
    {
        if (LanguageModeCombo?.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString();
            if (!string.IsNullOrWhiteSpace(tag))
            {
                return tag.Trim().ToLowerInvariant();
            }

            var content = item.Content?.ToString();
            if (!string.IsNullOrWhiteSpace(content))
            {
                return content.Trim().ToLowerInvariant();
            }
        }

        return "repetition";
    }

    private string? ResolveLanguageInputHemisphere()
    {
        if (LanguageHemisphereCombo?.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(tag) || tag == "*")
            {
                return null;
            }

            return tag.Trim().ToUpperInvariant();
        }

        return "L";
    }

    private async Task<LanguageStimulusDispatchResult> SendLanguageStimulusAsync(
        Uri baseUri,
        string text,
        string mode,
        string? hemisphere,
        float intensity,
        int burstPerToken,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            Text = text,
            Mode = mode,
            Intensity = intensity,
            BurstPerToken = burstPerToken,
            Hemisphere = hemisphere,
            TokenCount = mode.Equals("emergent", StringComparison.OrdinalIgnoreCase) ? Math.Clamp(text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length, 4, 16) : (int?)null,
            NoveltyBias = mode.Equals("emergent", StringComparison.OrdinalIgnoreCase)
                ? 0.72f
                : mode.Equals("english", StringComparison.OrdinalIgnoreCase) ? 0.0f : 0.35f
        };

        using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/input/language"), request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}. {payload}");
        }

        var tokenCount = 0;
        var generated = 0;
        var delivered = 0;
        var targetCount = 0;
        var resolvedMode = mode;
        var generatedUtterance = text;
        var pausedDueToSleep = false;
        var brainTokenCount = 0;
        var grammarIntent = string.Empty;
        var grammarMood = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var responseMode = GetString(doc.RootElement, "mode");
                if (!string.IsNullOrWhiteSpace(responseMode))
                {
                    resolvedMode = responseMode;
                }

                tokenCount = GetInt(doc.RootElement, "tokenCount");
                brainTokenCount = GetInt(doc.RootElement, "brainTokenCount");
                generated = GetInt(doc.RootElement, "generatedSpikes");
                delivered = GetInt(doc.RootElement, "deliveredSpikes");
                targetCount = GetInt(doc.RootElement, "targetInstances");
                var responseUtterance = GetString(doc.RootElement, "generatedUtterance");
                if (!string.IsNullOrWhiteSpace(responseUtterance))
                {
                    generatedUtterance = responseUtterance;
                }
                pausedDueToSleep = GetBool(doc.RootElement, "pausedDueToSleep");
                if (TryGetProperty(doc.RootElement, "grammar", out var grammar) && grammar.ValueKind == JsonValueKind.Object)
                {
                    grammarIntent = GetString(grammar, "intent");
                    grammarMood = GetString(grammar, "mood");
                }
            }
        }
        catch
        {
            // Best effort details parsing only.
        }

        if (brainTokenCount <= 0)
        {
            brainTokenCount = tokenCount;
        }

        return new LanguageStimulusDispatchResult(
            resolvedMode,
            tokenCount,
            brainTokenCount,
            generated,
            delivered,
            targetCount,
            generatedUtterance,
            pausedDueToSleep,
            grammarIntent,
            grammarMood);
    }

    // Viewport mouse handling, hover label, and auto-fit zoom moved to MainWindow.Camera.cs.

    private async void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_shutdownComplete)
        {
            return;
        }

        e.Cancel = true;
        if (_shutdownInFlight)
        {
            return;
        }

        _shutdownInFlight = true;
        try
        {
            await ShutdownAsync();
        }
        finally
        {
            _shutdownComplete = true;
            _shutdownInFlight = false;
            Close();
        }
    }

    private async Task ShutdownAsync()
    {
        Interlocked.Exchange(ref _shutdownRequested, 1);
        try
        {
            InputManager.Current.PreProcessInput -= InputManager_PreProcessInput;
            _densityDebounceTimer.Stop();
            _cameraFitDebounceTimer.Stop();
            _minWakeDebounceTimer.Stop();
            _sleepPressureDebounceTimer.Stop();
            _autoProfileDebounceTimer.Stop();
            _sensoryHealthTimer.Stop();
            _speechOutputEnabled = false;
            _speechQueue.Writer.TryComplete();
            var webcamStopped = await StopWebcamInputAsync();
            var microphoneStopped = await StopMicrophoneInputAsync();
            _workerCts.Cancel();
            try
            {
                _inputSignal.Release();
            }
            catch (SemaphoreFullException)
            {
            }

            var workers = Task.WhenAll(new[]
            {
                _controlWorkerTask ?? Task.CompletedTask,
                _renderWorkerTask ?? Task.CompletedTask,
                _framePollTask ?? Task.CompletedTask,
                webcamStopped ? Task.CompletedTask : (_webcamTask ?? Task.CompletedTask),
                microphoneStopped ? Task.CompletedTask : (_microphoneTask ?? Task.CompletedTask)
            });
            try
            {
                await workers.WaitAsync(TimeSpan.FromSeconds(5));
                await Task.Run(() => _speechThread?.Join(TimeSpan.FromSeconds(2)));
            }
            catch (TimeoutException)
            {
                // Process shutdown will end background workers. Do not dispose
                // shared cancellation primitives while one still owns them.
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Editor shutdown warning: {ex}");
        }

        _workerCts.Dispose();
        _inputSignal.Dispose();
        _endpointResolutionGate.Dispose();
        _transportStatsPaneWorker.Dispose();
        _brainDashboardPaneWorker.Dispose();
        _inhabitancePaneWorker.Dispose();
        _circuitAuditPaneWorker.Dispose();
        _reasoningPaneWorker.Dispose();
        _avatarService.Dispose();
        _httpClient.Dispose();
    }

    private sealed class StructureVisual(
        string displayName,
        string snapshotId,
        string hemisphere,
        string neuronModel,
        string plasticity,
        Color baseColor,
        Color spikeColor,
        SolidColorBrush diffuseBrush,
        SolidColorBrush emissiveBrush,
        ScaleTransform3D clusterScale,
        IReadOnlyList<SolidColorBrush> spikeNeuronBrushes)
    {
        public string DisplayName { get; } = displayName;
        public string SnapshotId { get; } = snapshotId;
        public string Hemisphere { get; } = hemisphere;
        public string NeuronModel { get; } = neuronModel;
        public string Plasticity { get; } = plasticity;
        public Color BaseColor { get; } = baseColor;
        public Color SpikeColor { get; } = spikeColor;
        public SolidColorBrush DiffuseBrush { get; } = diffuseBrush;
        public SolidColorBrush EmissiveBrush { get; } = emissiveBrush;
        public ScaleTransform3D ClusterScale { get; } = clusterScale;
        public IReadOnlyList<SolidColorBrush> SpikeNeuronBrushes { get; } = spikeNeuronBrushes;
        public Dictionary<string, int> NeuronIdToSpikeIndex { get; } = new(StringComparer.Ordinal);
        public HashSet<int> AssignedSpikeIndices { get; } = [];
        public int NextSpikeBrushAssignment { get; set; }
        public double SpikeLevel { get; set; }
        public float MeanFiringRateHz { get; set; }
        public MicrotubuleTick? Microtubules { get; set; }
        public BodySchemaTick? BodySchema { get; set; }
        public BasalGangliaTick? BasalGanglia { get; set; }
        public CerebellarTick? Cerebellar { get; set; }
        public VestibuloReticularTick? VestibuloReticular { get; set; }
        public SuperiorColliculusTick? SuperiorColliculus { get; set; }
        public HippocampalSpatialTick? HippocampalSpatial { get; set; }
        public SalienceAffectTick? SalienceAffect { get; set; }
        public PrefrontalWorkingMemoryTick? PrefrontalWorkingMemory { get; set; }
        public ThalamicAttentionGateTick? ThalamicAttentionGate { get; set; }
        public HypothalamicHomeostasisTick? HypothalamicHomeostasis { get; set; }
        public SleepWakeArousalTick? SleepWakeArousal { get; set; }
        public DescendingDefenseTick? DescendingDefense { get; set; }
        public DopamineRewardTick? DopamineReward { get; set; }
        public SeptohippocampalThetaTick? SeptohippocampalTheta { get; set; }
        public SpinalProprioceptiveTick? SpinalProprioceptive { get; set; }
        public OlfactoryLimbicMemoryTick? OlfactoryLimbicMemory { get; set; }
        public AuditoryLanguageMotorTick? AuditoryLanguageMotor { get; set; }
        public VisualObjectRecognitionTick? VisualObjectRecognition { get; set; }
        public DateTime LastSpikeLogUtc { get; set; }
    }

    private sealed class PathwayVisual(string sourceId, string targetId, string hemisphere, string neurotransmitter, Color baseColor, SolidColorBrush diffuseBrush, SolidColorBrush emissiveBrush)
    {
        public string SourceId { get; } = sourceId;
        public string TargetId { get; } = targetId;
        public string Hemisphere { get; } = hemisphere;
        public string Neurotransmitter { get; } = neurotransmitter;
        public Color BaseColor { get; } = baseColor;
        public SolidColorBrush DiffuseBrush { get; } = diffuseBrush;
        public SolidColorBrush EmissiveBrush { get; } = emissiveBrush;
        public double SpikeLevel { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class CorpusCallosumVisual(Color baseColor, SolidColorBrush diffuseBrush, SolidColorBrush emissiveBrush)
    {
        public Color BaseColor { get; } = baseColor;
        public SolidColorBrush DiffuseBrush { get; } = diffuseBrush;
        public SolidColorBrush EmissiveBrush { get; } = emissiveBrush;
        public double SpikeLevel { get; set; }
    }

    private sealed record StructureDefinition(
        string DisplayName,
        string SnapshotId,
        Point3D Center,
        Color BaseColor,
        string NeuronModel,
        string Plasticity,
        StructureLayout Layout,
        int GridX,
        int GridY,
        int GridZ,
        double RadiusX,
        double RadiusY,
        double RadiusZ,
        double PitchDeg,
        double YawDeg,
        double RollDeg);
    private sealed record CorticalGyrusProfile(
        string Name,
        double Center,
        double HalfWidth,
        double Waves,
        double WaveAmplitude,
        double CrownLiftMm,
        double SulcusDepthMm,
        double FoldReliefMm,
        double ThetaSkew);
    private sealed record HomuncularBand(double AlongStart, double AlongEnd, Color Diffuse, Color Emissive);
    private sealed record PathwayDefinition(string SourceId, string TargetId, string Neurotransmitter, string ProjectionType, bool IsFeedback);
    private sealed record HemispherePairing(string SourceInstance, string TargetInstance, string Hemisphere);
private sealed record StructureTick(string StructureId, float MeanRateHz, int SpikeOut, int SpikeIn, IReadOnlyList<string> TopNeuronIds, MicrotubuleTick? Microtubules, BodySchemaTick? BodySchema, BasalGangliaTick? BasalGanglia, CerebellarTick? Cerebellar, VestibuloReticularTick? VestibuloReticular, SuperiorColliculusTick? SuperiorColliculus, HippocampalSpatialTick? HippocampalSpatial, SalienceAffectTick? SalienceAffect, PrefrontalWorkingMemoryTick? PrefrontalWorkingMemory, ThalamicAttentionGateTick? ThalamicAttentionGate, HypothalamicHomeostasisTick? HypothalamicHomeostasis, SleepWakeArousalTick? SleepWakeArousal, DescendingDefenseTick? DescendingDefense, DopamineRewardTick? DopamineReward, SeptohippocampalThetaTick? SeptohippocampalTheta, SpinalProprioceptiveTick? SpinalProprioceptive, OlfactoryLimbicMemoryTick? OlfactoryLimbicMemory, AuditoryLanguageMotorTick? AuditoryLanguageMotor, VisualObjectRecognitionTick? VisualObjectRecognition);
private sealed record MicrotubuleTick(
    string Mode,
    bool Enabled,
    bool Experimental,
    float MeanStability,
    float MeanSpineInvasionEligibility,
    float MeanTransportSupport,
    float MeanOpticalCollectiveBias,
    float MeanRadicalPairSensitivity,
    float MeanPlasticitySupport,
    float MeanTracePersistenceSupport,
    float MeanIntegrationGain,
    float MeanConsolidationSupport);
private sealed record BodySchemaTick(
    string DominantBodyZone,
    string DominantSpatialZone,
    float FaceHeadActivation,
    float HandArmActivation,
    float TrunkActivation,
    float LegFootActivation,
    float NearBodyActivation,
    float LeftPeripersonalActivation,
    float RightPeripersonalActivation,
    float FarSpaceActivation);
private sealed record BasalGangliaTick(
    string DominantMode,
    float DirectPathwayActivation,
    float IndirectPathwayActivation,
    float HyperdirectPathwayActivation,
    float OutputNucleusInhibition,
    float ThalamicDisinhibition,
    float DopamineModulation,
    float ActionSelectionBias);
private sealed record CerebellarTick(
    string CorrectionMode,
    float MossyFiberDrive,
    float ClimbingFiberError,
    float PurkinjeInhibition,
    float DeepNucleusOutput,
    float VermisStabilization,
    float CorrectionGain,
    float PredictionError);
private sealed record VestibuloReticularTick(
    string PostureMode,
    float VestibularDrive,
    float ReticularArousal,
    float VermisBalanceCorrection,
    float SpinalMotorTone,
    float PostureStability,
    float BalanceError);
private sealed record SuperiorColliculusTick(
    string OrientingMode,
    float VisualOrientingDrive,
    float AuditoryOrientingDrive,
    float NigrotectalInhibition,
    float PulvinarAttention,
    float HeadEyeCommand,
    float SaccadeReadiness,
    float SalienceBias);
private sealed record HippocampalSpatialTick(
    string MemoryMode,
    float EntorhinalGridDrive,
    float DentatePatternSeparation,
    float Ca3PatternCompletion,
    float Ca1PlaceIndex,
    float SubicularOutput,
    float HeadDirectionAlignment,
    float SpatialCoherence,
    float NoveltyMismatch);
private sealed record SalienceAffectTick(
    string SalienceMode,
    float ThreatSalience,
    float InteroceptiveDrive,
    float ConflictMonitoring,
    float AutonomicArousal,
    float AttentionGain,
    float DefensiveReadiness,
    float ControlBias,
    float AffectIntensity);
private sealed record PrefrontalWorkingMemoryTick(
    string ControlMode,
    float PfcPersistentActivity,
    float MediodorsalThalamicSupport,
    float FrontoparietalContext,
    float SemanticContext,
    float StriatalGate,
    float AccControlDemand,
    float TopDownBias,
    float TaskSetStability);
private sealed record ThalamicAttentionGateTick(
    string GateMode,
    float ThalamocorticalRelay,
    float TrnInhibitoryGate,
    float PulvinarSpotlight,
    float MediodorsalAccess,
    float IntralaminarBroadcast,
    float SensoryGain,
    float CorticalAccess,
    float RelaySelectionBias);
private sealed record HypothalamicHomeostasisTick(
    string HomeostasisMode,
    float VisceralAfferentDrive,
    float HypothalamicSetpointError,
    float InsulaBodyFeeling,
    float LimbicHomeostaticPressure,
    float AutonomicBrainstemDrive,
    float ArousalPressure,
    float ComfortDeficit,
    float DefensiveBodyCommand);
private sealed record SleepWakeArousalTick(
    string ArousalMode,
    float HypothalamicSleepPressure,
    float ReticularActivatingDrive,
    float PontomedullaryStateTone,
    float LocusCoeruleusWakeTone,
    float RapheStabilizationTone,
    float BasalForebrainWakeDrive,
    float IntralaminarArousalBroadcast,
    float CorticalReadiness);
private sealed record DescendingDefenseTick(
    string DefenseMode,
    float AmygdalaThreatDrive,
    float HypothalamicDefenseDrive,
    float PagDefensiveCommand,
    float RaphePainModulation,
    float MedullaryAutonomicSupport,
    float ReticularPatternRelease,
    float SpinalWithdrawalDrive,
    float ProtectionReadiness);
private sealed record DopamineRewardTick(
    string RewardMode,
    float VtaPhasicDopamine,
    float SncActionTeaching,
    float NucleusAccumbensIncentive,
    float StriatalActionValue,
    float HabenulaNegativePrediction,
    float OrbitofrontalExpectedValue,
    float PfcGoalBias,
    float RewardPredictionError,
    float LearningReadiness);
private sealed record SeptohippocampalThetaTick(
    string ThetaMode,
    float SeptalThetaDrive,
    float EntorhinalGridPhase,
    float DentateEncodingGate,
    float Ca3SequenceReplay,
    float Ca1PlaceTiming,
    float SubicularNavigationOutput,
    float HeadDirectionAlignment,
    float RetrosplenialSceneAnchor,
    float VestibularPathIntegration,
    float ThetaCoherence);
private sealed record SpinalProprioceptiveTick(
    string ReflexMode,
    float SpinalReflexDrive,
    float S1ProprioceptiveMap,
    float M1DescendingCommand,
    float CerebellarMossyFeedback,
    float VestibularBalanceInput,
    float ReticularPosturalSet,
    float ThalamicRelayTone,
    float ReflexReadiness,
    float ProprioceptiveCoherence);
private sealed record OlfactoryLimbicMemoryTick(
    string MemoryMode,
    float OlfactoryCueDrive,
    float TemporalPiriformAssociation,
    float AmygdalaAffectiveTag,
    float EntorhinalMemoryGate,
    float HippocampalEpisodeIndex,
    float OrbitofrontalValenceContext,
    float PfcAutobiographicalControl,
    float FamiliaritySignal,
    float AutobiographicalCoherence);
private sealed record AuditoryLanguageMotorTick(
    string LanguageMode,
    float A1AuditoryDrive,
    float WernickeComprehension,
    float ArcuatePhonologicalRelay,
    float BrocaSpeechSequence,
    float PremotorArticulationPlan,
    float M1SpeechMotorCommand,
    float BasalGangliaSpeechGate,
    float MotorThalamicRelay,
    float LanguageMotorCoherence);
private sealed record VisualObjectRecognitionTick(
    string RecognitionMode,
    float V1EdgeDrive,
    float V2ContourIntegration,
    float V4ObjectFeatureBinding,
    float MtMotionCue,
    float TemporalObjectIdentity,
    float PerirhinalFamiliarity,
    float PulvinarVisualAttention,
    float ThalamicRelayGain,
    float PfcObjectContext,
    float ObjectRecognitionCoherence);
private sealed record PathwayTick(string Source, string Target, int Volume);
private sealed record DispatchSpikeTrace(string SourceStructure, string SourceNeuronId, string TargetStructure, string TargetNeuronId, long WallClockUnixMs);
private sealed record DispatchPathwayActivity(string Source, string Target, string Hemisphere, int Volume);
private sealed record TransportSpikePipeline(int Generated, int Routed, int Delivered)
{
    public static TransportSpikePipeline Empty { get; } = new(0, 0, 0);
}
private sealed record VisualStimulusDispatchResult(
    string Pattern,
    int Generated,
    int Delivered,
    int TargetCount,
    bool RecoveryAttempted,
    int RecoveryRestarted,
    int RecoveryHealthy,
    int RecoveryRetriedInstances,
    bool PausedDueToSleep,
    string? FocusField,
    string? FocusHemisphere,
    float FocusConfidence);
private sealed record AuditoryStimulusDispatchResult(
    string Pattern,
    int Generated,
    int Delivered,
    int TargetCount,
    bool RecoveryAttempted,
    int RecoveryRestarted,
    int RecoveryHealthy,
    int RecoveryRetriedInstances,
    bool PausedDueToSleep);
private sealed record LanguageStimulusDispatchResult(
    string Mode,
    int TokenCount,
    int BrainTokenCount,
    int Generated,
    int Delivered,
    int TargetCount,
    string Utterance,
    bool PausedDueToSleep,
    string GrammarIntent,
    string GrammarMood);
private sealed record FrameSpikeMetrics(
    int GeneratedSpikes,
    int RoutedSpikes,
    int DeliveredSpikes,
    int DispatchTraceCount,
    int DistinctNeuronIdCount,
    int StructuresWithNeuronSpikes,
    int VisibleNeuronHighlights,
    int UnmatchedNeuronIds)
{
    public static FrameSpikeMetrics Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);
}
private sealed record AutoProfileControlSettings(
    bool Enabled,
    bool AllowRecovery,
    int WarmupTicks,
    int ManualHoldTicks,
    double DegradeNonOkRatio,
    double DegradeAckLatencyMs,
    long DegradeSnapshotAgeTicks,
    int DegradeConsecutiveTicks,
    double RecoveryNonOkRatio,
    double RecoveryAckLatencyMs,
    long RecoverySnapshotAgeTicks,
    int RecoveryConsecutiveTicks);
private sealed record InputGateControlSettings(
    bool AvatarVisionEnabled,
    bool SpontaneousSpikingEnabled);
    private enum InputHealthState
    {
        Idle,
        Healthy,
        Warning,
        Failed
    }
    private sealed record StructureTreeNode(string DisplayName, string SnapshotId);
    private sealed record ServiceHealthEntry(string Status, string Error);
    private sealed record InputDelta(double DeltaX, double DeltaY, int WheelDelta);

    private sealed class StructureStatusBadge(string snapshotId, string displayName, Border badgeBorder, TextBlock badgeText)
    {
        public string SnapshotId { get; } = snapshotId;
        public string DisplayName { get; } = displayName;
        public Border BadgeBorder { get; } = badgeBorder;
        public TextBlock BadgeText { get; } = badgeText;
    }

    private sealed class SnapshotPayload
    {
        public List<StructureTick> StructureStates { get; } = [];
        public List<PathwayTick> Pathways { get; } = [];
    }

    private enum SpeechTriggerMode
    {
        LanguagePathway,
        GlobalDispatch
    }

    private enum StructureLayout
    {
        CorticalSheet,
        NucleusBlock,
        HippocampalArc,
        CerebellarSheet,
        BrainstemColumn,
        OlfactoryBulbShell
    }

    private sealed class ConnectivityRuleJson
    {
        public string? Source { get; set; }
        public List<ConnectivityConnectionJson>? Connections { get; set; }
    }

    private sealed class ConnectivityConnectionJson
    {
        public string? Target { get; set; }
        public string? Neurotransmitter { get; set; }
        public string? ProjectionType { get; set; }
    }
}
