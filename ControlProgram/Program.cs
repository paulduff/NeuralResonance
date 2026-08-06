using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Grpc.Net.Client;
using NeuralResonanceEngine.ControlProgram.Services;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
var controlSharedSecret = NreControlPlaneSecurity.ResolveSharedSecret();
var controlListenAnyIp = string.Equals(
    Environment.GetEnvironmentVariable("NRE_CONTROL_LISTEN_ANY_IP"),
    "true",
    StringComparison.OrdinalIgnoreCase);
if (controlListenAnyIp && controlSharedSecret is null)
{
    throw new InvalidOperationException(
        "NRE_CONTROL_LISTEN_ANY_IP=true requires NRE_CONTROL_SHARED_SECRET to protect the control plane.");
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    // Local WPF clients can briefly lag while reading large diagnostic responses.
    options.Limits.MinResponseDataRate = null;
    if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port) && port > 0)
    {
        if (controlListenAnyIp)
        {
            options.ListenAnyIP(port);
        }
        else
        {
            options.ListenLocalhost(port);
        }
    }
});
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: true)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true)
    .AddCommandLine(args);
builder.Services.AddSingleton<SimulationState>();
builder.Services.AddSingleton(NeuronalMotorControlState.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<NeuronalMotorPopulationWindow>();
builder.Services.AddSingleton<NeuronalPerceptionRuntime>();
builder.Services.AddSingleton<NeuronalMemoryRuntime>();
builder.Services.AddSingleton<NeuronalAttentionWorkspaceRuntime>();
builder.Services.AddSingleton<NeuronalVisualAttentionRuntime>();
builder.Services.AddSingleton<NeuronalSleepConsolidationRuntime>();
builder.Services.AddSingleton<NeuronalLanguageGroundingRuntime>();
builder.Services.AddSingleton<NeuronalAffectValuationRuntime>();
builder.Services.AddSingleton<NeuronalExecutiveRuntime>();
builder.Services.AddSingleton<NeuronalCognitionAuthorityRuntime>();
builder.Services.AddSingleton<SnapshotStore>();
builder.Services.AddSingleton<RuntimeInstanceCatalog>();
builder.Services.AddSingleton<ServicePublishBuffer>();
builder.Services.AddSingleton<StructureProcessSupervisor>();
builder.Services.AddSingleton<RuntimePerformanceProfileState>();
builder.Services.AddSingleton<AutoProfileRuntimeState>();
builder.Services.AddSingleton<PhoneticLanguageEngine>();
builder.Services.AddSingleton<LanguageBackoffPolicy>();
builder.Services.AddSingleton<DialogueTurnManager>();
builder.Services.AddSingleton<AdminInputRestartGate>();
builder.Services.AddSingleton<FramePayloadFactory>();
builder.Services.AddSingleton<HippocampalNavigationSessionManager>();
builder.Services.AddSingleton<InputIngressRuntime>();
builder.Services.AddSingleton<RetinalFrameTransducerRuntime>();
builder.Services.AddSingleton<CochlearFrameTransducerRuntime>();
builder.Services.AddSingleton<HttpRequestProfiler>();
builder.Services.AddSingleton(EntityLanguageBridgeOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient("dnne")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromMilliseconds(800),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        MaxConnectionsPerServer = 512,
        AutomaticDecompression = DecompressionMethods.None
    });
builder.Services.AddHttpClient<IEntityLanguageClient, EntityLanguageClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<EntityLanguageBridgeOptions>();
    client.BaseAddress = options.ApiBaseUri;
    client.Timeout = options.Timeout;
});
builder.Services.AddHostedService<TickCoordinator>();

// Compress JSON API responses (frame and large state payloads are text-heavy
// and typically compress >2x). Brotli is preferred when the
// client offers it; gzip is the universal fallback.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = new[]
    {
        "application/json",
        "text/plain",
        "text/json"
    };
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o =>
    o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o =>
    o.Level = System.IO.Compression.CompressionLevel.Fastest);

var app = builder.Build();
app.UseResponseCompression();
const string minimalFramePayloadJson = "{\"state\":{},\"connectomeReport\":null,\"latestSnapshot\":null,\"outputLog\":[],\"spikeLog\":[],\"dispatchSpikes\":[]}";

if (controlSharedSecret is not null)
{
    app.Use(async (context, next) =>
    {
        var suppliedSecret = context.Request.Headers[NreControlPlaneSecurity.HeaderName].ToString();
        if (!NreControlPlaneSecurity.IsAuthorized(suppliedSecret, controlSharedSecret))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next();
    });
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var profiler = context.RequestServices.GetRequiredService<HttpRequestProfiler>();
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("HttpRequestProfiler");
    profiler.RequestStarted(path);
    var started = Stopwatch.GetTimestamp();
    Exception? failure = null;
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        failure = ex;
        throw;
    }
    finally
    {
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        profiler.RequestCompleted(
            path,
            context.Request.Method,
            context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
            context.Response.StatusCode,
            elapsedMs,
            failure?.GetType().Name);
        if (elapsedMs >= 1500.0)
        {
            logger.LogWarning(
                "Slow API request {Method} {Path}{Query} -> {StatusCode} in {ElapsedMs:0.0}ms{ErrorSuffix}",
                context.Request.Method,
                path,
                context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty,
                context.Response.StatusCode,
                elapsedMs,
                failure is null ? string.Empty : $" ({failure.GetType().Name})");
        }
    }
});

app.MapPost("/api/v1/snapshot", async (BrainSnapshot snapshot, SnapshotStore store, CancellationToken ct) =>
{
    await store.AppendAsync(snapshot, ct);
    return Results.Accepted();
});

app.MapGet("/api/v1/snapshot", (SnapshotStore store) => Results.Ok(store.GetAll()));
app.MapGet("/api/v1/snapshot/latest", (SnapshotStore store) =>
{
    var latest = store.GetLatest();
    return latest is null ? Results.NotFound() : Results.Ok(latest);
});
app.MapPost("/api/v1/publish/step", (PublishedStepMessage message, ServicePublishBuffer publishBuffer) =>
{
    publishBuffer.Publish(message);
    return Results.Accepted();
});
app.MapGet("/api/v1/state", (SimulationState state, AutoProfileRuntimeState autoProfileState) => Results.Ok(state.ToDiagnostics(autoProfileState.GetSnapshot())));
app.MapGet("/api/v1/startup-health", (SimulationState state, int? maxNonOkDetails) =>
    Results.Ok(state.GetStartupHealth(maxNonOkDetails ?? 16)));
app.MapGet("/api/v1/validation", (SimulationState state, int? maxSnapshotAgeTicks, int? maxNonOkServices) =>
    Results.Ok(state.GetValidationSnapshot(maxSnapshotAgeTicks ?? 20, maxNonOkServices ?? 2)));
app.MapGet("/api/v1/service-health", (SimulationState state) => Results.Ok(state.GetServiceHealthSnapshot()));
app.MapGet("/api/v1/neuronal-motor", (SimulationState state, NeuronalMotorControlState control) => Results.Ok(new
{
    Control = control.GetSnapshot(),
    Runtime = state.GetNeuronalMotorSnapshot()
}));
app.MapGet("/api/v1/neuronal-perception", (NeuronalPerceptionRuntime perception) =>
    Results.Ok(perception.GetSnapshot()));
app.MapGet("/api/v1/neuronal-memory", (NeuronalMemoryRuntime memory) =>
    Results.Ok(memory.GetSnapshot()));
app.MapGet("/api/v1/neuronal-attention-workspace", (NeuronalAttentionWorkspaceRuntime attentionWorkspace) =>
    Results.Ok(attentionWorkspace.GetSnapshot()));
app.MapGet("/api/v1/neuronal-visual-attention", (NeuronalVisualAttentionRuntime visualAttention) =>
    Results.Ok(visualAttention.GetSnapshot()));
app.MapGet("/api/v1/neuronal-sleep-consolidation", (NeuronalSleepConsolidationRuntime sleepConsolidation) =>
    Results.Ok(sleepConsolidation.GetSnapshot()));
app.MapGet("/api/v1/neuronal-language-grounding", (NeuronalLanguageGroundingRuntime languageGrounding) =>
    Results.Ok(languageGrounding.GetSnapshot()));
app.MapGet("/api/v1/neuronal-affect-valuation", (NeuronalAffectValuationRuntime affectValuation) =>
    Results.Ok(affectValuation.GetSnapshot()));
app.MapGet("/api/v1/neuronal-executive", (NeuronalExecutiveRuntime executive) =>
    Results.Ok(executive.GetSnapshot()));
app.MapGet("/api/v1/cognition-authority", (NeuronalCognitionAuthorityRuntime cognitionAuthority) =>
    Results.Ok(cognitionAuthority.GetSnapshot()));
// /api/v1/active-inference: route was registered but GetActiveInferenceSnapshot()
// is not implemented on SimulationState and no client references this URL. Removed
// so the engine can build/start; reintroduce alongside a real snapshot method if needed.
app.MapGet("/api/v1/circuit-health", (SimulationState state, int? maxWarnings) => Results.Ok(state.GetCircuitHealthPanelSnapshot(maxWarnings ?? 96)));
app.MapAdminReasoningRoutes();
app.MapAdminTelemetryRoutes();
app.MapDyadLanguageRoutes();
app.MapDyadLanguageGenerationRoutes();
app.MapNavigationRoutes();
app.MapGet("/api/v1/frame", (HttpRequest request, SimulationState state, SnapshotStore store, AutoProfileRuntimeState autoProfileState, FramePayloadFactory framePayloadFactory, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("FrameEndpoint");
    _ = long.TryParse(request.Query["output_since_ms"], out var outputSinceMs);
    _ = long.TryParse(request.Query["spike_since_ms"], out var spikeSinceMs);
    _ = long.TryParse(request.Query["dispatch_since_ms"], out var dispatchSinceMs);
    var includeConnectome = ParseBooleanQuery(request, "include_connectome", defaultValue: true);
    var maxOutputLog = ParseIntQuery(request, "max_output_log", 160, 0, 2000);
    var maxSpikeLog = ParseIntQuery(request, "max_spike_log", 160, 0, 2000);
    var maxDispatchSpikes = ParseIntQuery(request, "max_dispatch_spikes", 1200, 0, 4096);

    try
    {
        var frame = framePayloadFactory.Create(
            state,
            store,
            autoProfileState.GetSnapshot(),
            outputSinceMs,
            spikeSinceMs,
            dispatchSinceMs,
            includeConnectome,
            maxOutputLog,
            maxSpikeLog,
            maxDispatchSpikes,
            out _,
            out _,
            out _);
        var json = JsonSerializer.SerializeToUtf8Bytes(frame, DnneJsonContext.Default.FramePayload);
        return Results.Bytes(json, "application/json");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Frame payload generation failed; returning fallback payload.");
        return Results.Text(minimalFramePayloadJson, "application/json");
    }
});
app.MapGet("/api/v1/frame/stream", () => Results.Json(
    new
    {
        Error = "Frame streaming disabled.",
        Detail = "Telemetry frames are generated on demand. Poll /api/v1/frame instead."
    },
    statusCode: StatusCodes.Status410Gone));
app.MapPost("/api/v1/admin/restart-sim", (SimulationState state) =>
{
    var generation = state.RequestSimulationRestart();
    return Results.Ok(new
    {
        Requested = true,
        Generation = generation
    });
});
app.MapGet("/api/v1/admin/perf-profile", (RuntimePerformanceProfileState performanceProfiles) =>
{
    return Results.Ok(performanceProfiles.GetSnapshot());
});
app.MapPost("/api/v1/admin/perf-profile", (
    PerformanceProfileRequest request,
    RuntimePerformanceProfileState performanceProfiles,
    SimulationState state) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Profile))
    {
        return Results.BadRequest(new
        {
            Error = $"Profile is required. Supported values: {RuntimePerformanceProfileSettings.SupportedProfileList}."
        });
    }

    var requestedProfile = request.Profile.Trim();
    if (!RuntimePerformanceProfileSettings.IsSupported(requestedProfile))
    {
        return Results.BadRequest(new
        {
            Error = $"Unsupported profile '{request.Profile}'. Supported values: {RuntimePerformanceProfileSettings.SupportedProfileList}."
        });
    }

    var (generation, settings) = performanceProfiles.ApplyProfile(requestedProfile);
    state.UpdatePerformanceProfile(settings.ProfileName);
    state.AppendOutputLog($"Performance profile set to '{settings.ProfileName}' (generation {generation}).");
    if (request.RestartSimulation is null || request.RestartSimulation.Value)
    {
        var restartGeneration = state.RequestSimulationRestart();
        state.AppendOutputLog($"Simulation restart requested for profile switch (generation {restartGeneration}).");
    }

    return Results.Ok(new
    {
        Applied = true,
        Generation = generation,
        Settings = settings
    });
});
app.MapGet("/api/v1/admin/auto-profile", (AutoProfileRuntimeState autoProfileState) =>
{
    return Results.Ok(autoProfileState.GetSnapshot());
});
app.MapPost("/api/v1/admin/auto-profile", (
    AutoProfileControlRequest request,
    AutoProfileRuntimeState autoProfileState,
    SimulationState state) =>
{
    if (request is null)
    {
        return Results.BadRequest(new
        {
            Error = "Request payload is required."
        });
    }

    var (generation, settings) = autoProfileState.Apply(request);
    state.AppendOutputLog(
        $"Auto-profile controls updated (generation {generation}): enabled={settings.Enabled}, recovery={settings.AllowRecovery}, " +
        $"degrade(nonOk={settings.DegradeNonOkRatio:0.000}, ackMs={settings.DegradeAckLatencyMs:0.0}, snapshotAge={settings.DegradeSnapshotAgeTicks}, ticks={settings.DegradeConsecutiveTicks}), " +
        $"recovery(nonOk={settings.RecoveryNonOkRatio:0.000}, ackMs={settings.RecoveryAckLatencyMs:0.0}, snapshotAge={settings.RecoverySnapshotAgeTicks}, ticks={settings.RecoveryConsecutiveTicks}), " +
        $"warmup={settings.WarmupTicks}, manualHold={settings.ManualHoldTicks}.");
    return Results.Ok(new
    {
        Applied = true,
        Generation = generation,
        Settings = settings
    });
});
app.MapGet("/api/v1/admin/metabolic-physiology", (SimulationState state) =>
{
    return Results.Ok(new
    {
        Role = "ReadOnlyPhysiologicalTransducer",
        CanAuthorizeSleepState = false,
        CanGateNeuralTraffic = false,
        NeuronalAuthorityEndpoint = "/api/v1/neuronal-sleep-consolidation",
        State = state.GetMetabolicPhysiologySnapshot()
    });
});
app.MapAdminInputControlRoutes();
app.MapGet("/api/v1/admin/input/ingress", (InputIngressRuntime ingress) => Results.Ok(ingress.GetSnapshot()));
app.MapPost("/api/v1/admin/input/visual-frame", async (
    HttpRequest request,
    RuntimeInstanceCatalog catalog,
    IHttpClientFactory clientFactory,
    SimulationState state,
    RetinalFrameTransducerRuntime transducer,
    InputIngressRuntime ingress,
    CancellationToken ct) =>
{
    if (!int.TryParse(request.Query["width"], out var width) ||
        !int.TryParse(request.Query["height"], out var height) ||
        !int.TryParse(request.Query["stride"], out var stride))
    {
        return Results.BadRequest(new { Error = "Width, height, and stride query parameters are required integers." });
    }

    if (!RetinalFrameDescriptor.TryCreate(
            width,
            height,
            stride,
            request.Query["pixelFormat"],
            request.Query["inputSource"],
            out var descriptor,
            out var descriptorError) ||
        descriptor is null)
    {
        return Results.BadRequest(new { Error = descriptorError ?? "Invalid retinal frame descriptor." });
    }

    if (request.ContentLength is not null && request.ContentLength.Value != descriptor.RequiredBytes)
    {
        return Results.BadRequest(new
        {
            Error = $"Frame payload length must be exactly {descriptor.RequiredBytes} bytes."
        });
    }

    if (!ingress.TryEnter(AdminInputIngressKind.Video, out var ingressLease, out var ingressSnapshot))
    {
        return Results.Json(new
        {
            Error = "Retinal frame input is temporarily throttled due to ingress backpressure.",
            InputSource = descriptor.InputSource,
            Ingress = ingressSnapshot
        }, statusCode: StatusCodes.Status429TooManyRequests);
    }
    using var _ = ingressLease;

    if (AdminInputSource.IsAvatarSource(descriptor.InputSource) && !state.IsAvatarVisionEnabled())
    {
        return Results.Ok(new
        {
            Accepted = false,
            DispatchDeferred = false,
            BlockedByInputGate = true,
            InputSource = descriptor.InputSource,
            Target = StructureId.Retina.ToString(),
            TargetInstances = 0,
            GeneratedSpikes = 0,
            DeliveredSpikes = 0,
            Errors = Array.Empty<string>()
        });
    }

    var payload = new byte[descriptor.RequiredBytes];
    try
    {
        await request.Body.ReadExactlyAsync(payload, ct);
    }
    catch (EndOfStreamException)
    {
        return Results.BadRequest(new
        {
            Error = $"Frame payload ended before {descriptor.RequiredBytes} bytes were read."
        });
    }

    if (request.ContentLength is null)
    {
        var trailing = new byte[1];
        if (await request.Body.ReadAsync(trailing, ct) > 0)
        {
            return Results.BadRequest(new
            {
                Error = $"Frame payload length must be exactly {descriptor.RequiredBytes} bytes."
            });
        }
    }

    var knownTargets = catalog.GetByStructureWithKnownFallback(StructureId.Retina, hemisphere: null);
    if (knownTargets.Count == 0)
    {
        return Results.NotFound(new
        {
            Error = "No active service instances found for Retina (both)."
        });
    }

    var liveTargets = catalog.GetByStructure(StructureId.Retina, hemisphere: null);
    var transduction = transducer.Transduce(payload, descriptor, state.Tick, state.SimulationClockMs);
    var generatedForTargets = 0;
    for (var i = 0; i < liveTargets.Count; i++)
    {
        generatedForTargets += transduction.ForHemisphere(liveTargets[i].HemisphereNormalized).Count;
    }

    if (liveTargets.Count > 0 && generatedForTargets > 0)
    {
        DispatchStimulusToInstancesInBackground(
            "Retinal frame input",
            liveTargets,
            instance => transduction.ForHemisphere(instance.HemisphereNormalized),
            clientFactory,
            state,
            state.Tick,
            state.SimulationClockMs,
            logSuccess: false);
    }

    return Results.Ok(new
    {
        Accepted = true,
        DispatchDeferred = liveTargets.Count > 0 && generatedForTargets > 0,
        BlockedByInputGate = false,
        InputSource = descriptor.InputSource,
        Target = StructureId.Retina.ToString(),
        TargetInstances = liveTargets.Count,
        KnownTargetInstances = knownTargets.Count,
        LiveTargetInstances = liveTargets.Count,
        GeneratedSpikes = generatedForTargets,
        DeliveredSpikes = 0,
        SampleColumns = transduction.SampleColumns,
        SampleRows = transduction.SampleRows,
        OnChannelSpikes = transduction.OnChannelSpikes,
        OffChannelSpikes = transduction.OffChannelSpikes,
        MeanLuminance = transduction.MeanLuminance,
        MeanTemporalChange = transduction.MeanTemporalChange,
        Errors = liveTargets.Count == 0
            ? new[] { "No live Retina service instances are currently available." }
            : Array.Empty<string>()
    });
});
app.MapPost("/api/v1/admin/input/audio-frame", async (
    HttpRequest request,
    RuntimeInstanceCatalog catalog,
    IHttpClientFactory clientFactory,
    SimulationState state,
    CochlearFrameTransducerRuntime transducer,
    InputIngressRuntime ingress,
    CancellationToken ct) =>
{
    if (!int.TryParse(request.Query["sampleRate"], out var sampleRate) ||
        !int.TryParse(request.Query["channels"], out var channels) ||
        !int.TryParse(request.Query["samplesPerChannel"], out var samplesPerChannel))
    {
        return Results.BadRequest(new
        {
            Error = "SampleRate, channels, and samplesPerChannel query parameters are required integers."
        });
    }

    if (!CochlearFrameDescriptor.TryCreate(
            sampleRate,
            channels,
            samplesPerChannel,
            request.Query["sampleFormat"],
            request.Query["inputSource"],
            out var descriptor,
            out var descriptorError) ||
        descriptor is null)
    {
        return Results.BadRequest(new { Error = descriptorError ?? "Invalid cochlear frame descriptor." });
    }

    if (request.ContentLength is not null && request.ContentLength.Value != descriptor.RequiredBytes)
    {
        return Results.BadRequest(new
        {
            Error = $"Audio frame payload length must be exactly {descriptor.RequiredBytes} bytes."
        });
    }

    if (!ingress.TryEnter(AdminInputIngressKind.Sensory, out var ingressLease, out var ingressSnapshot))
    {
        return Results.Json(new
        {
            Error = "Cochlear frame input is temporarily throttled due to ingress backpressure.",
            InputSource = descriptor.InputSource,
            Ingress = ingressSnapshot
        }, statusCode: StatusCodes.Status429TooManyRequests);
    }
    using var _ = ingressLease;

    var payload = new byte[descriptor.RequiredBytes];
    try
    {
        await request.Body.ReadExactlyAsync(payload, ct);
    }
    catch (EndOfStreamException)
    {
        return Results.BadRequest(new
        {
            Error = $"Audio frame payload ended before {descriptor.RequiredBytes} bytes were read."
        });
    }

    if (request.ContentLength is null)
    {
        var trailing = new byte[1];
        if (await request.Body.ReadAsync(trailing, ct) > 0)
        {
            return Results.BadRequest(new
            {
                Error = $"Audio frame payload length must be exactly {descriptor.RequiredBytes} bytes."
            });
        }
    }

    var knownTargets = catalog.GetByStructureWithKnownFallback(StructureId.Cochlea, hemisphere: null);
    if (knownTargets.Count == 0)
    {
        return Results.NotFound(new
        {
            Error = "No active service instances found for Cochlea (both)."
        });
    }

    var liveTargets = catalog.GetByStructure(StructureId.Cochlea, hemisphere: null);
    var transduction = transducer.Transduce(payload, descriptor, state.Tick, state.SimulationClockMs);
    var generatedForTargets = 0;
    for (var i = 0; i < liveTargets.Count; i++)
    {
        generatedForTargets += transduction.ForHemisphere(liveTargets[i].HemisphereNormalized).Count;
    }

    if (liveTargets.Count > 0 && generatedForTargets > 0)
    {
        DispatchStimulusToInstancesInBackground(
            "Cochlear frame input",
            liveTargets,
            instance => transduction.ForHemisphere(instance.HemisphereNormalized),
            clientFactory,
            state,
            state.Tick,
            state.SimulationClockMs,
            logSuccess: false);
    }

    return Results.Ok(new
    {
        Accepted = true,
        DispatchDeferred = liveTargets.Count > 0 && generatedForTargets > 0,
        InputSource = descriptor.InputSource,
        Target = StructureId.Cochlea.ToString(),
        TargetInstances = liveTargets.Count,
        KnownTargetInstances = knownTargets.Count,
        LiveTargetInstances = liveTargets.Count,
        GeneratedSpikes = generatedForTargets,
        DeliveredSpikes = 0,
        transduction.FrequencyBands,
        transduction.ActiveLeftBands,
        transduction.ActiveRightBands,
        transduction.RootMeanSquare,
        transduction.PeakAmplitude,
        transduction.MeanBandAmplitude,
        transduction.MeanOnset,
        Errors = liveTargets.Count == 0
            ? new[] { "No live Cochlea service instances are currently available." }
            : Array.Empty<string>()
    });
});
app.MapPost("/api/v1/admin/input/collision", async (
    CollisionInputRequest request,
    RuntimeInstanceCatalog catalog,
    IHttpClientFactory clientFactory,
    SimulationState state,
    InputIngressRuntime ingress,
    CancellationToken ct) =>
{
    if (request is null)
    {
        return Results.BadRequest(new { Error = "Request payload missing." });
    }

    var pattern = string.IsNullOrWhiteSpace(request.Pattern) ? "WallImpact" : request.Pattern.Trim();
    var intensity = Math.Clamp(request.Intensity.GetValueOrDefault(1.25f), 0.2f, 4.0f);
    var burstCount = Math.Clamp(request.BurstCount.GetValueOrDefault(20), 4, 96);
    var targetStructure = string.IsNullOrWhiteSpace(request.TargetStructure)
        ? StructureId.SuperiorColliculus
        : Enum.TryParse<StructureId>(request.TargetStructure, ignoreCase: true, out var parsedTarget)
            ? parsedTarget
            : StructureId.SuperiorColliculus;
    var sourceStructure = string.IsNullOrWhiteSpace(request.SourceStructure)
        ? StructureId.S1
        : Enum.TryParse<StructureId>(request.SourceStructure, ignoreCase: true, out var parsedSource)
            ? parsedSource
            : StructureId.S1;
    if (!ingress.TryEnter(AdminInputIngressKind.Sensory, out var ingressLease, out var ingressSnapshot))
    {
        state.AppendOutputLog(
            $"Collision input throttled by ingress gate: pattern={pattern}, source={sourceStructure}, target={targetStructure}.");
        return Results.Json(new
        {
            Error = "Collision input is temporarily throttled due to ingress backpressure.",
            Ingress = ingressSnapshot
        }, statusCode: StatusCodes.Status429TooManyRequests);
    }
    using var _ = ingressLease;

    var hemisphereHint = NormalizeHemisphereHint(request.Hemisphere);
    var isFeedback = request.IsFeedback.GetValueOrDefault(false);

    var knownTargetInstances = catalog.GetByStructureWithKnownFallback(targetStructure, hemisphereHint);
    var liveTargetInstances = catalog.GetByStructure(targetStructure, hemisphereHint);
    if (knownTargetInstances.Count == 0)
    {
        return Results.NotFound(new
        {
            Error = $"No active service instances found for {targetStructure} ({(hemisphereHint ?? "both")})."
        });
    }

    if (liveTargetInstances.Count == 0)
    {
        return Results.Ok(new
        {
            Pattern = pattern,
            Source = sourceStructure.ToString(),
            Target = targetStructure.ToString(),
            Intensity = intensity,
            BurstCount = burstCount,
            IsFeedback = isFeedback,
            TargetInstances = 0,
            KnownTargetInstances = knownTargetInstances.Count,
            LiveTargetInstances = 0,
            GeneratedSpikes = 0,
            DeliveredSpikes = 0,
            Errors = new[]
            {
                $"No live service instances currently available for {targetStructure} ({(hemisphereHint ?? "both")})."
            }
        });
    }

    var tick = state.Tick;
    var timestampMs = state.SimulationClockMs;
    var dispatch = await DispatchStimulusToInstancesAsync(
        liveTargetInstances,
        instance =>
        {
            var hemisphere = instance.HemisphereNormalized;
            return BuildCollisionStimulusSpikes(
                tick,
                timestampMs,
                sourceStructure,
                targetStructure,
                hemisphere,
                pattern,
                intensity,
                burstCount,
                isFeedback);
        },
        clientFactory,
        state,
        tick,
        timestampMs,
        ct);

    state.AppendOutputLog(
        $"Collision input injected: pattern={pattern}, source={sourceStructure}, target={targetStructure}, generated={dispatch.GeneratedSpikes}, delivered={dispatch.DeliveredSpikes}, liveInstances={liveTargetInstances.Count}, knownInstances={knownTargetInstances.Count}, errors={dispatch.Errors.Count}.");
    if (dispatch.DeliveredSpikes > 0)
    {
        state.AppendSpikeLog(
            $"Collision input {pattern}: delivered {dispatch.DeliveredSpikes}/{dispatch.GeneratedSpikes} spikes to {targetStructure}.");
    }

    return Results.Ok(new
    {
        Pattern = pattern,
        Source = sourceStructure.ToString(),
        Target = targetStructure.ToString(),
        Intensity = intensity,
        BurstCount = burstCount,
        IsFeedback = isFeedback,
        TargetInstances = liveTargetInstances.Count,
        KnownTargetInstances = knownTargetInstances.Count,
        LiveTargetInstances = liveTargetInstances.Count,
        GeneratedSpikes = dispatch.GeneratedSpikes,
        DeliveredSpikes = dispatch.DeliveredSpikes,
        Errors = dispatch.Errors
    });
});
app.MapPost("/api/v1/admin/input/body-state", async (
    BodyStateInputRequest request,
    RuntimeInstanceCatalog catalog,
    IHttpClientFactory clientFactory,
    SimulationState state,
    InputIngressRuntime ingress,
    CancellationToken ct) =>
{
    if (request is null)
    {
        return Results.BadRequest(new { Error = "Request payload missing." });
    }

    var pattern = string.IsNullOrWhiteSpace(request.Pattern) ? "BodyState" : request.Pattern.Trim();
    var sourceStructure = string.IsNullOrWhiteSpace(request.SourceStructure)
        ? StructureId.SpinalCordMotor
        : Enum.TryParse<StructureId>(request.SourceStructure, ignoreCase: true, out var parsedSource)
            ? parsedSource
            : StructureId.SpinalCordMotor;
    var targetStructure = string.IsNullOrWhiteSpace(request.TargetStructure)
        ? StructureId.S1
        : Enum.TryParse<StructureId>(request.TargetStructure, ignoreCase: true, out var parsedTarget)
            ? parsedTarget
            : StructureId.S1;
    var hemisphereHint = NormalizeHemisphereHint(request.Hemisphere);
    var includeVestibular = request.IncludeVestibular.GetValueOrDefault(true);
    var includeCerebellar = request.IncludeCerebellar.GetValueOrDefault(true);
    var isFeedback = request.IsFeedback.GetValueOrDefault(true);
    var inputSource = AdminInputSource.Normalize(request.InputSource);
    if (!ingress.TryEnter(AdminInputIngressKind.Sensory, out var ingressLease, out var ingressSnapshot))
    {
        state.AppendOutputLog(
            $"Body-state input throttled by ingress gate: pattern={pattern}, source={sourceStructure}, target={targetStructure}, inputSource={inputSource}.");
        return Results.Json(new
        {
            Error = "Body-state input is temporarily throttled due to ingress backpressure.",
            InputSource = inputSource,
            Ingress = ingressSnapshot
        }, statusCode: StatusCodes.Status429TooManyRequests);
    }
    using var _ = ingressLease;

    var forwardVelocity = Math.Abs(request.ForwardVelocity.GetValueOrDefault(0f));
    var turnRateDeg = Math.Abs(request.TurnRateDeg.GetValueOrDefault(0f));
    var rawContactLevel = Math.Clamp(request.ContactLevel.GetValueOrDefault(0f), 0f, 1f);
    var tactileFront = Math.Clamp(request.TactileFront.GetValueOrDefault(rawContactLevel), 0f, 1f);
    var tactileLeft = Math.Clamp(request.TactileLeft.GetValueOrDefault(0f), 0f, 1f);
    var tactileRight = Math.Clamp(request.TactileRight.GetValueOrDefault(0f), 0f, 1f);
    var tactileGround = Math.Clamp(request.TactileGround.GetValueOrDefault(0f), 0f, 1f);
    var painLevel = Math.Clamp(request.PainLevel.GetValueOrDefault(rawContactLevel * 0.35f), 0f, 1f);
    var hunger = Math.Clamp(request.Hunger.GetValueOrDefault(0f), 0f, 1f);
    var health = Math.Clamp(request.Health.GetValueOrDefault(1f), 0f, 1f);
    var healthDeficit = 1f - health;
    var tactileLoad = Math.Clamp(
        Math.Max(rawContactLevel, Math.Max(Math.Max(tactileFront, Math.Max(tactileLeft, tactileRight)), tactileGround * 0.35f)),
        0f,
        1f);
    var contactLevel = Math.Clamp(Math.Max(tactileLoad, painLevel * 0.72f), 0f, 1f);
    var leftDrive = Math.Max(0f, request.LeftMotorDrive.GetValueOrDefault(0f));
    var rightDrive = Math.Max(0f, request.RightMotorDrive.GetValueOrDefault(0f));
    var motorAsymmetry = (leftDrive + rightDrive) > 0.01f
        ? Math.Clamp(Math.Abs(leftDrive - rightDrive) / (leftDrive + rightDrive), 0f, 1f)
        : 0f;
    var bodyState = state.UpdateBodyState(
        request.ForwardVelocity.GetValueOrDefault(0f),
        request.TurnRateDeg.GetValueOrDefault(0f),
        contactLevel,
        tactileFront,
        tactileLeft,
        tactileRight,
        tactileGround,
        painLevel,
        hunger,
        health,
        leftDrive,
        rightDrive);
    var motionSignal = Math.Clamp((float)(forwardVelocity / 3.0), 0f, 1f);
    var turnSignal = Math.Clamp((float)(turnRateDeg / 260.0), 0f, 1f);
    var derivedIntensity = Math.Clamp(0.20f + (0.95f * motionSignal) + (0.38f * turnSignal) + (0.52f * contactLevel) + (0.22f * motorAsymmetry), 0.10f, 3.50f);
    var intensity = Math.Clamp(request.Intensity.GetValueOrDefault(derivedIntensity), 0.10f, 3.50f);
    var derivedBurstCount = Math.Clamp((int)Math.Round(6 + (16.0 * motionSignal) + (10.0 * turnSignal) + (10.0 * contactLevel)), 4, 72);
    var burstCount = Math.Clamp(request.BurstCount.GetValueOrDefault(derivedBurstCount), 4, 96);
    var interoceptiveSignal = Math.Clamp(Math.Max(hunger, Math.Max(healthDeficit, painLevel)), 0f, 1f);
    var interoceptiveIntensity = Math.Clamp(0.12f + (hunger * 0.92f) + (healthDeficit * 1.18f) + (painLevel * 0.86f), 0.10f, 3.50f);
    var interoceptiveBurstCount = Math.Clamp((int)Math.Round(4 + (hunger * 18) + (healthDeficit * 24) + (painLevel * 20)), 4, 72);
    var interoceptiveTargets = ResolveBodyStateInteroceptiveTargets(interoceptiveSignal);
    var neuronalSleep = state.NeuronalSleepConsolidation;
    var neuronalSleepActive = neuronalSleep.Available &&
        neuronalSleep.StateActive &&
        neuronalSleep.State != NeuronalSleepState.Wake;
    var cerebellarTargets = includeCerebellar
        ? ResolveBodyStateCerebellarTargets(contactLevel, turnSignal, motorAsymmetry)
        : Array.Empty<StructureId>();
    var targetLabel = BuildBodyStateTargetLabel(targetStructure, includeVestibular, cerebellarTargets, interoceptiveTargets);

    var knownTargetInstances = catalog.GetByStructureWithKnownFallback(targetStructure, hemisphereHint)
        .ToList();
    var liveTargetInstances = catalog.GetByStructure(targetStructure, hemisphereHint)
        .ToList();
    if (includeVestibular && targetStructure != StructureId.VestibularNuclei)
    {
        knownTargetInstances.AddRange(catalog.GetByStructureWithKnownFallback(StructureId.VestibularNuclei, hemisphereHint));
        liveTargetInstances.AddRange(catalog.GetByStructure(StructureId.VestibularNuclei, hemisphereHint));
    }

    foreach (var cerebellarTarget in cerebellarTargets)
    {
        if (cerebellarTarget == targetStructure ||
            (includeVestibular && cerebellarTarget == StructureId.VestibularNuclei))
        {
            continue;
        }

        knownTargetInstances.AddRange(catalog.GetByStructureWithKnownFallback(cerebellarTarget, hemisphereHint));
        liveTargetInstances.AddRange(catalog.GetByStructure(cerebellarTarget, hemisphereHint));
    }

    foreach (var interoceptiveTarget in interoceptiveTargets)
    {
        if (interoceptiveTarget == targetStructure)
        {
            continue;
        }

        knownTargetInstances.AddRange(catalog.GetByStructureWithKnownFallback(interoceptiveTarget, hemisphereHint));
        liveTargetInstances.AddRange(catalog.GetByStructure(interoceptiveTarget, hemisphereHint));
    }

    knownTargetInstances = knownTargetInstances
        .GroupBy(i => i.InstanceKey, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First())
        .ToList();
    liveTargetInstances = liveTargetInstances
        .GroupBy(i => i.InstanceKey, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First())
        .ToList();
    if (knownTargetInstances.Count == 0)
    {
        return Results.NotFound(new
        {
            Error = $"No active service instances found for body-state targets ({targetLabel}) with hemisphere {(hemisphereHint ?? "both")}."
        });
    }

    if (liveTargetInstances.Count == 0)
    {
        return Results.Ok(new
        {
            Pattern = pattern,
            Source = sourceStructure.ToString(),
            PrimaryTarget = targetStructure.ToString(),
            IncludeVestibular = includeVestibular,
            IncludeCerebellar = includeCerebellar,
            CerebellarTargets = cerebellarTargets.Select(t => t.ToString()).ToArray(),
            InteroceptiveTargets = interoceptiveTargets.Select(t => t.ToString()).ToArray(),
            Intensity = intensity,
            BurstCount = burstCount,
            InteroceptiveIntensity = interoceptiveIntensity,
            InteroceptiveBurstCount = interoceptiveBurstCount,
            IsFeedback = isFeedback,
            ForwardVelocity = request.ForwardVelocity.GetValueOrDefault(0f),
            TurnRateDeg = request.TurnRateDeg.GetValueOrDefault(0f),
            ContactLevel = contactLevel,
            TactileFront = tactileFront,
            TactileLeft = tactileLeft,
            TactileRight = tactileRight,
            TactileGround = tactileGround,
            PainLevel = painLevel,
            Hunger = hunger,
            Health = health,
            InputSource = inputSource,
            BodyState = bodyState,
            SleepState = neuronalSleepActive ? "sleeping" : "awake",
            TargetInstances = 0,
            KnownTargetInstances = knownTargetInstances.Count,
            LiveTargetInstances = 0,
            Targets = Array.Empty<object>(),
            GeneratedSpikes = 0,
            DeliveredSpikes = 0,
            Accepted = AdminInputSource.IsAvatarSource(inputSource),
            DispatchDeferred = AdminInputSource.IsAvatarSource(inputSource),
            Errors = new[]
            {
                $"No live service instances currently available for body-state targets ({targetLabel}) with hemisphere {(hemisphereHint ?? "both")}."
            }
        });
    }

    var tick = state.Tick;
    var timestampMs = state.SimulationClockMs;
    var targetSummary = liveTargetInstances
        .Select(i => new
        {
            i.InstanceKey,
            Structure = i.StructureId.ToString(),
            Hemisphere = string.IsNullOrWhiteSpace(i.Hemisphere) ? "M" : i.Hemisphere.ToUpperInvariant()
        })
        .ToArray();

    if (AdminInputSource.IsAvatarSource(inputSource))
    {
        DispatchStimulusToInstancesInBackground(
            "Body-state input",
            liveTargetInstances,
            instance =>
            {
                var hemisphere = instance.HemisphereNormalized;
                var isInteroceptive = interoceptiveTargets.Contains(instance.StructureId);
                var dispatchSource = isInteroceptive
                    ? instance.StructureId == StructureId.NucleusTractusSolitarius
                        ? StructureId.Medulla
                        : StructureId.NucleusTractusSolitarius
                    : sourceStructure;
                return BuildBodyStateStimulusSpikes(
                    tick,
                    timestampMs,
                    dispatchSource,
                    instance.StructureId,
                    hemisphere,
                    isInteroceptive ? "InteroceptiveState" : pattern,
                    isInteroceptive ? interoceptiveIntensity : intensity,
                    isInteroceptive ? interoceptiveBurstCount : burstCount,
                    isFeedback);
            },
            clientFactory,
            state,
            tick,
            timestampMs);

        state.AppendOutputLog(
            $"Body-state input accepted for deferred dispatch: pattern={pattern}, source={sourceStructure}, targets={targetLabel}, liveTargets={liveTargetInstances.Count}, knownTargets={knownTargetInstances.Count}, inputSource={inputSource}, neuronalSleep={neuronalSleepActive}.");
        return Results.Ok(new
        {
            Pattern = pattern,
            Source = sourceStructure.ToString(),
            PrimaryTarget = targetStructure.ToString(),
            IncludeVestibular = includeVestibular,
            IncludeCerebellar = includeCerebellar,
            CerebellarTargets = cerebellarTargets.Select(t => t.ToString()).ToArray(),
            InteroceptiveTargets = interoceptiveTargets.Select(t => t.ToString()).ToArray(),
            Intensity = intensity,
            BurstCount = burstCount,
            InteroceptiveIntensity = interoceptiveIntensity,
            InteroceptiveBurstCount = interoceptiveBurstCount,
            IsFeedback = isFeedback,
            ForwardVelocity = request.ForwardVelocity.GetValueOrDefault(0f),
            TurnRateDeg = request.TurnRateDeg.GetValueOrDefault(0f),
            ContactLevel = contactLevel,
            TactileFront = tactileFront,
            TactileLeft = tactileLeft,
            TactileRight = tactileRight,
            TactileGround = tactileGround,
            PainLevel = painLevel,
            Hunger = hunger,
            Health = health,
            InputSource = inputSource,
            BodyState = bodyState,
            SleepState = neuronalSleepActive ? "sleeping" : "awake",
            TargetInstances = liveTargetInstances.Count,
            KnownTargetInstances = knownTargetInstances.Count,
            LiveTargetInstances = liveTargetInstances.Count,
            Targets = targetSummary,
            GeneratedSpikes = 0,
            DeliveredSpikes = 0,
            Accepted = true,
            DispatchDeferred = true,
            Errors = Array.Empty<string>()
        });
    }

    var dispatch = await DispatchStimulusToInstancesAsync(
        liveTargetInstances,
        instance =>
        {
            var hemisphere = instance.HemisphereNormalized;
            var isInteroceptive = interoceptiveTargets.Contains(instance.StructureId);
            var dispatchSource = isInteroceptive
                ? instance.StructureId == StructureId.NucleusTractusSolitarius
                    ? StructureId.Medulla
                    : StructureId.NucleusTractusSolitarius
                : sourceStructure;
            return BuildBodyStateStimulusSpikes(
                tick,
                timestampMs,
                dispatchSource,
                instance.StructureId,
                hemisphere,
                isInteroceptive ? "InteroceptiveState" : pattern,
                isInteroceptive ? interoceptiveIntensity : intensity,
                isInteroceptive ? interoceptiveBurstCount : burstCount,
                isFeedback);
        },
        clientFactory,
        state,
        tick,
        timestampMs,
        ct);

    state.AppendOutputLog(
        $"Body-state input injected: pattern={pattern}, source={sourceStructure}, targets={targetLabel}, liveTargets={liveTargetInstances.Count}, knownTargets={knownTargetInstances.Count}, generated={dispatch.GeneratedSpikes}, delivered={dispatch.DeliveredSpikes}, inputSource={inputSource}, neuronalSleep={neuronalSleepActive}, errors={dispatch.Errors.Count}.");
    if (dispatch.DeliveredSpikes > 0)
    {
        state.AppendSpikeLog(
            $"Body-state input {pattern}: delivered {dispatch.DeliveredSpikes}/{dispatch.GeneratedSpikes} spikes across {liveTargetInstances.Count} targets.");
    }

    return Results.Ok(new
    {
        Pattern = pattern,
        Source = sourceStructure.ToString(),
        PrimaryTarget = targetStructure.ToString(),
        IncludeVestibular = includeVestibular,
        IncludeCerebellar = includeCerebellar,
        CerebellarTargets = cerebellarTargets.Select(t => t.ToString()).ToArray(),
        InteroceptiveTargets = interoceptiveTargets.Select(t => t.ToString()).ToArray(),
        Intensity = intensity,
        BurstCount = burstCount,
        InteroceptiveIntensity = interoceptiveIntensity,
        InteroceptiveBurstCount = interoceptiveBurstCount,
        IsFeedback = isFeedback,
        ForwardVelocity = request.ForwardVelocity.GetValueOrDefault(0f),
        TurnRateDeg = request.TurnRateDeg.GetValueOrDefault(0f),
        ContactLevel = contactLevel,
        TactileFront = tactileFront,
        TactileLeft = tactileLeft,
        TactileRight = tactileRight,
        TactileGround = tactileGround,
        PainLevel = painLevel,
        Hunger = hunger,
        Health = health,
        InputSource = inputSource,
        BodyState = bodyState,
        SleepState = neuronalSleepActive ? "sleeping" : "awake",
        TargetInstances = liveTargetInstances.Count,
        KnownTargetInstances = knownTargetInstances.Count,
        LiveTargetInstances = liveTargetInstances.Count,
        Targets = targetSummary,
        GeneratedSpikes = dispatch.GeneratedSpikes,
        DeliveredSpikes = dispatch.DeliveredSpikes,
        Errors = dispatch.Errors
    });
});
app.MapGet("/api/v1/admin/language/phonetics", (PhoneticLanguageEngine phonetics, int? maxLexemes) =>
{
    var max = Math.Clamp(maxLexemes.GetValueOrDefault(64), 16, 512);
    return Results.Ok(phonetics.GetSnapshot(max));
});
app.MapPost("/api/v1/admin/language/phonetics/generate", (
    PhoneticGenerationRequest request,
    PhoneticLanguageEngine phonetics,
    SimulationState state) =>
{
    var mode = NormalizeLanguageMode(request?.Mode);
    var tokenCount = Math.Clamp(request?.TokenCount ?? 6, 1, 24);
    var noveltyBias = Math.Clamp(request?.NoveltyBias ?? 0.72f, 0.0f, 1.0f);
    var seeds = TokenizeLanguageInput(request?.SeedText ?? string.Empty);
    if (seeds.Length == 0)
    {
        seeds = phonetics.CreateEmergentSemanticSeeds(tokenCount, state.Tick);
    }

    var lexicalization = phonetics.Lexicalize(seeds, mode, state.Tick, noveltyBias);
    state.AppendOutputLog(
        $"Phonetic utterance generated: mode={mode}, tokens={lexicalization.SurfaceTokens.Count}, created={lexicalization.CreatedLexemes}, utterance=\"{lexicalization.Utterance}\".");

    return Results.Ok(new
    {
        Mode = mode,
        TokenCount = lexicalization.SurfaceTokens.Count,
        NoveltyBias = noveltyBias,
        GeneratedUtterance = lexicalization.Utterance,
        SurfaceTokens = lexicalization.SurfaceTokens,
        PhonemeTokens = lexicalization.PhonemeTokens,
        CreatedLexemes = lexicalization.CreatedLexemes,
        ReusedLexemes = lexicalization.ReusedLexemes
    });
});
app.MapPost("/api/v1/admin/language/phonetics/reset", (PhoneticLanguageEngine phonetics, SimulationState state) =>
{
    phonetics.Reset();
    state.AppendOutputLog("Phonetic language lexicon reset.");
    return Results.Ok(new
    {
        Reset = true
    });
});
app.MapGet("/api/v1/admin/language/debug/backoff", (LanguageBackoffPolicy backoff, int? maxEdges) =>
{
    var max = Math.Clamp(maxEdges.GetValueOrDefault(24), 1, 256);
    return Results.Ok(backoff.GetSnapshot(max));
});
app.MapGet("/api/v1/admin/language/dialogue", (DialogueTurnManager dialogue) => Results.Ok(dialogue.GetSnapshot()));
app.MapPost("/api/v1/admin/language/dialogue/reset", (DialogueTurnManager dialogue, SimulationState state) =>
{
    dialogue.Reset();
    state.AppendOutputLog("Dialogue turn manager reset.");
    return Results.Ok(new
    {
        Reset = true,
        Dialogue = dialogue.GetSnapshot()
    });
});
app.MapPost("/api/v1/admin/input/language", async (
    LanguageInputRequest request,
    RuntimeInstanceCatalog catalog,
    IHttpClientFactory clientFactory,
    PhoneticLanguageEngine phonetics,
    LanguageBackoffPolicy backoffPolicy,
    DialogueTurnManager dialogueTurns,
    SimulationState state,
    InputIngressRuntime ingress,
    CancellationToken ct) =>
{
    if (!ingress.TryEnter(AdminInputIngressKind.Sensory, out var ingressLease, out var ingressSnapshot))
    {
        state.AppendOutputLog("Language input throttled by ingress gate.");
        return Results.Json(new
        {
            Error = "Language input is temporarily throttled due to ingress backpressure.",
            Ingress = ingressSnapshot
        }, statusCode: StatusCodes.Status429TooManyRequests);
    }
    using var _ = ingressLease;

    var mode = NormalizeLanguageMode(request?.Mode);
    if (request is null ||
        (string.IsNullOrWhiteSpace(request.Text) && !string.Equals(mode, "emergent", StringComparison.OrdinalIgnoreCase)))
    {
        return Results.BadRequest(new { Error = "Provide non-empty language text input, or use mode='emergent'." });
    }

    var text = request.Text?.Trim() ?? string.Empty;
    if (text.Length > 512)
    {
        text = text[..512];
    }

    var defaultNoveltyBias = mode switch
    {
        "emergent" => 0.72f,
        "english" => 0.0f,
        _ => 0.35f
    };
    var noveltyBias = Math.Clamp(
        request.NoveltyBias.GetValueOrDefault(defaultNoveltyBias),
        0.0f,
        1.0f);
    var intensity = Math.Clamp(request.Intensity.GetValueOrDefault(1.0f), 0.2f, 3.0f);
    var burstPerToken = Math.Clamp(request.BurstPerToken.GetValueOrDefault(8), 2, 48);

    var semanticTokens = TokenizeLanguageInput(text);
    var requestedTokenCount = Math.Clamp(request.TokenCount.GetValueOrDefault(6), 1, 24);
    if (semanticTokens.Length == 0 && string.Equals(mode, "emergent", StringComparison.OrdinalIgnoreCase))
    {
        semanticTokens = phonetics.CreateEmergentSemanticSeeds(requestedTokenCount, state.Tick);
    }

    if (semanticTokens.Length == 0)
    {
        return Results.BadRequest(new { Error = "Language input did not contain valid lexical tokens." });
    }

    var lexicalization = string.Equals(mode, "english", StringComparison.OrdinalIgnoreCase)
        ? EnglishLanguageLexicon.Lexicalize(semanticTokens)
        : phonetics.Lexicalize(semanticTokens, mode, state.Tick, noveltyBias);
    var tokens = lexicalization.SurfaceTokens.ToArray();
    if (tokens.Length == 0)
    {
        return Results.BadRequest(new { Error = "Unable to generate emergent phonetic tokens for language routing." });
    }

    // Text ingress is a sensory transducer. It may tokenize and phoneticize the
    // input, but it must not infer goals, commands, rewards, or memory writes.
    var brainTokens = tokens;

    var tick = state.Tick;
    var dialogueTurn = dialogueTurns.ObserveInput(mode, text, brainTokens, tick);
    var hemisphereHint = NormalizeHemisphereHint(request.Hemisphere);
    var plan = GetLanguageStimulusPlan(mode);
    var errors = new List<string>();

    var targets = new List<(LanguageStimulusTarget Target, ServiceInstance Instance, string Hemisphere, LanguageBackoffEdgeHandle Edge)>();
    foreach (var target in plan)
    {
        var resolution = backoffPolicy.Resolve(mode, target, catalog, hemisphereHint, tick);
        if (!resolution.Resolved || resolution.Target is null)
        {
            var reason = string.IsNullOrWhiteSpace(resolution.FailureReason) ? "no candidate instances available" : resolution.FailureReason;
            errors.Add($"route {target.SourceStructure}->{target.TargetStructure}: {reason}");
            continue;
        }

        foreach (var instance in resolution.Instances)
        {
            var hemisphere = instance.HemisphereNormalized;
            targets.Add((resolution.Target, instance, hemisphere, resolution.Edge));
        }
    }

    if (targets.Count == 0)
    {
        var failedDialogue = dialogueTurns.RecordDelivery(dialogueTurn, 0, 0, errors, tick);
        return Results.NotFound(new
        {
            Error = $"No active service instances found for language route targets (mode={mode}, hemisphere={(hemisphereHint ?? "auto")}).",
            Dialogue = failedDialogue,
            NeuronalLanguageGrounding = state.GetNeuronalLanguageGroundingSnapshot()
        });
    }

    var timestampMs = state.SimulationClockMs;
    var generatedSpikes = 0;
    var deliveredSpikes = 0;
    var deliveredByTarget = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    var dispatchTasks = targets.Select(async targetDispatch =>
    {
        var spikes = BuildLanguageStimulusSpikes(
            tick,
            timestampMs,
            targetDispatch.Target,
            targetDispatch.Hemisphere,
            mode,
            brainTokens,
            intensity,
            burstPerToken);

        Interlocked.Add(ref generatedSpikes, spikes.Count);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(900));
        try
        {
            var client = clientFactory.CreateClient("dnne");
            client.BaseAddress = targetDispatch.Instance.Endpoint;
            client.Timeout = Timeout.InfiniteTimeSpan;

            var accepted = await DispatchStimulusSpikesAsync(client, spikes, timeout.Token);
            accepted = Math.Clamp(accepted, 0, spikes.Count);
            if (accepted <= 0)
            {
                return;
            }

            Interlocked.Add(ref deliveredSpikes, accepted);
            deliveredByTarget.AddOrUpdate(
                targetDispatch.Target.TargetStructure.ToString(),
                accepted,
                (_, prev) => prev + accepted);
            backoffPolicy.RecordDispatchResult(targetDispatch.Edge, accepted, null, tick);

            state.RecordDispatchedSpikes(
                tick,
                timestampMs,
                targetDispatch.Hemisphere,
                targetDispatch.Hemisphere,
                targetDispatch.Instance.InstanceKey,
                spikes,
                accepted);
        }
        catch (Exception ex)
        {
            backoffPolicy.RecordDispatchResult(targetDispatch.Edge, 0, ex, tick);
            lock (errors)
            {
                errors.Add($"{targetDispatch.Instance.InstanceKey}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    });

    await Task.WhenAll(dispatchTasks);

    // Language input may stimulate language populations, but text never writes
    // a semantic directive into motor populations. Action selection is neuronal.
    var dialogue = dialogueTurns.RecordDelivery(dialogueTurn, generatedSpikes, deliveredSpikes, errors, tick);

    var summaryText = lexicalization.Utterance.Length <= 72 ? lexicalization.Utterance : $"{lexicalization.Utterance[..72]}...";
    state.AppendOutputLog(
        $"Language input injected: mode={mode}, tokens={tokens.Length}, brainTokens={brainTokens.Length}, generated={generatedSpikes}, delivered={deliveredSpikes}, targets={targets.Count}, errors={errors.Count}, utterance=\"{summaryText}\".");
    if (deliveredSpikes > 0)
    {
        state.AppendSpikeLog(
            $"Language input {mode}: delivered {deliveredSpikes}/{generatedSpikes} spikes across {deliveredByTarget.Count} targets ({lexicalization.Utterance}).");
    }

    return Results.Ok(new
    {
        Text = text,
        Mode = mode,
        TokenCount = tokens.Length,
        BrainTokenCount = brainTokens.Length,
        SourceTokenCount = semanticTokens.Length,
        NoveltyBias = noveltyBias,
        Intensity = intensity,
        BurstPerToken = burstPerToken,
        TargetInstances = targets.Count,
        GeneratedUtterance = lexicalization.Utterance,
        SurfaceTokens = lexicalization.SurfaceTokens,
        PhonemeTokens = lexicalization.PhonemeTokens,
        NeuronalLanguageGrounding = state.GetNeuronalLanguageGroundingSnapshot(),
        Dialogue = dialogue,
        CreatedLexemes = lexicalization.CreatedLexemes,
        ReusedLexemes = lexicalization.ReusedLexemes,
        GeneratedSpikes = generatedSpikes,
        DeliveredSpikes = deliveredSpikes,
        Backoff = backoffPolicy.GetSnapshot(12),
        DeliveredByTarget = deliveredByTarget.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
        SleepState = state.NeuronalSleepConsolidation.Available &&
            state.NeuronalSleepConsolidation.StateActive &&
            state.NeuronalSleepConsolidation.State != NeuronalSleepState.Wake
                ? "sleeping"
                : "awake",
        Errors = errors
    });
});
app.MapPost("/api/v1/admin/restart-service", async (
    RestartServiceRequest request,
    SimulationState state,
    RuntimeInstanceCatalog catalog,
    StructureProcessSupervisor supervisor,
    CancellationToken ct) =>
{
    var targetInstances = new List<ServiceInstance>();

    if (!string.IsNullOrWhiteSpace(request.InstanceKey))
    {
        if (!catalog.TryGetByInstanceKey(request.InstanceKey.Trim(), out var instance))
        {
            return Results.NotFound(new
            {
                Error = $"Unknown service instance '{request.InstanceKey}'."
            });
        }

        targetInstances.Add(instance);
    }
    else
    {
        if (string.IsNullOrWhiteSpace(request.StructureId) ||
            !Enum.TryParse<StructureId>(request.StructureId, ignoreCase: true, out var structureId))
        {
            return Results.BadRequest(new
            {
                Error = "Provide a valid structureId or instanceKey."
            });
        }

        var hemisphereHint = NormalizeHemisphereHint(request.Hemisphere);
        targetInstances.AddRange(catalog.GetByStructureWithKnownFallback(structureId, hemisphereHint));
        if (targetInstances.Count == 0)
        {
            return Results.NotFound(new
            {
                Error = $"No service instances registered for structure '{structureId}'."
            });
        }
    }

    var result = await supervisor.RestartServicesAsync(targetInstances, ct);
    state.AppendOutputLog($"Restart service request: requested={result.Requested}, restarted={result.Restarted}, healthy={result.Healthy}.");
    return Results.Ok(result);
});
app.MapGet("/api/v1/admin/network/export", (SimulationState state, SnapshotStore store) =>
{
    var document = state.ExportNetworkState(store.GetLatest());
    return Results.Ok(document);
});
app.MapPost("/api/v1/admin/network/import", async (
    NetworkStateDocument document,
    SimulationState state,
    SnapshotStore store,
    CancellationToken ct) =>
{
    if (document is null)
    {
        return Results.BadRequest(new { Error = "Request payload missing." });
    }

    if (!state.TryImportNetworkState(document, out var importReport, out var error))
    {
        return Results.BadRequest(new { Error = error ?? "Unable to import network state." });
    }

    if (document.LatestSnapshot is not null)
    {
        await store.ClearAsync(ct);
        await store.AppendAsync(document.LatestSnapshot, ct);
        state.MarkSnapshot(document.LatestSnapshot);
    }

    state.AppendOutputLog(
        $"Network state imported: tick={state.Tick}, simMs={state.SimulationClockMs:0.0}, schema={document.SchemaVersion}, migrated={importReport.Migrated}.");
    return Results.Ok(new
    {
        Imported = true,
        document.SchemaVersion,
        state.Tick,
        state.SimulationClockMs,
        ImportReport = importReport
    });
});

app.Run();

static bool ParseBooleanQuery(HttpRequest request, string key, bool defaultValue)
{
    if (!request.Query.TryGetValue(key, out var values))
    {
        return defaultValue;
    }

    var value = values.ToString();
    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    if (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("on", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("off", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return defaultValue;
}

static int ParseIntQuery(HttpRequest request, string key, int defaultValue, int min, int max)
{
    if (!request.Query.TryGetValue(key, out var values))
    {
        return defaultValue;
    }

    var raw = values.ToString();
    if (!int.TryParse(raw, out var parsed))
    {
        return defaultValue;
    }

    return Math.Clamp(parsed, min, max);
}

static int ComputeStableStimulusHash(string text)
{
    unchecked
    {
        var hash = 2166136261u;
        foreach (var character in text)
        {
            hash = (hash ^ (byte)character) * 16777619u;
            hash = (hash ^ (byte)(character >> 8)) * 16777619u;
        }

        return (int)(hash & int.MaxValue);
    }
}

static List<SpikeMessage> BuildCollisionStimulusSpikes(
    long tick,
    double timestampMs,
    StructureId sourceStructure,
    StructureId targetStructure,
    string hemisphere,
    string pattern,
    float intensity,
    int burstCount,
    bool isFeedback)
{
    var patternLabel = string.IsNullOrWhiteSpace(pattern) ? "collision" : pattern.Trim();
    var patternToken = Regex.Replace(patternLabel, "[^A-Za-z0-9]+", "_");
    if (string.IsNullOrWhiteSpace(patternToken))
    {
        patternToken = "collision";
    }

    var patternSeed = ComputeStableStimulusHash(patternLabel);
    var spikes = new List<SpikeMessage>(burstCount);
    for (var i = 0; i < burstCount; i++)
    {
        var sector = (patternSeed + i) % 8;
        var lane = (patternSeed + (i * 5)) % 40;
        var vesicle = Math.Clamp((0.95f + (sector * 0.06f)) * intensity, 0.08f, 8.0f);
        var reuptake = Math.Clamp(2.4f + (lane * 0.11f), 1.6f, 14.0f);
        var spikeType = i % 6 == 0 ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL;
        spikes.Add(new SpikeMessage
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = timestampMs,
            SourceStructure = sourceStructure,
            TargetStructure = targetStructure,
            SourceNeuronId = $"{hemisphere}:collision_{patternToken}_{tick}_{sector}_{i}",
            TargetNeuronId = $"{hemisphere}:sc_orient_{sector}_cell_{lane}",
            SynapseId = Guid.NewGuid(),
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = vesicle,
            ReuptakeRate = reuptake,
            SpikeType = spikeType,
            IsFeedback = isFeedback,
            ModulationContext = null
        });
    }

    return spikes;
}

static List<SpikeMessage> BuildBodyStateStimulusSpikes(
    long tick,
    double timestampMs,
    StructureId sourceStructure,
    StructureId targetStructure,
    string hemisphere,
    string pattern,
    float intensity,
    int burstCount,
    bool isFeedback)
{
    var channel = targetStructure switch
    {
        StructureId.S1 => "somatic",
        StructureId.VestibularNuclei => "vestibular",
        StructureId.NucleusTractusSolitarius or StructureId.Hypothalamus or StructureId.Insula => "interoceptive",
        StructureId.CerebellarGranule or StructureId.CerebellarVermis or StructureId.CerebellarLobules or StructureId.InferiorOlive => "proprioceptive",
        _ => "body"
    };
    var patternLabel = string.IsNullOrWhiteSpace(pattern) ? "BodyState" : pattern.Trim();
    var patternToken = Regex.Replace(patternLabel, "[^A-Za-z0-9]+", "_");
    if (string.IsNullOrWhiteSpace(patternToken))
    {
        patternToken = "BodyState";
    }

    var patternSeed = ComputeStableStimulusHash($"{channel}:{patternLabel}");
    var spikes = new List<SpikeMessage>(burstCount);
    for (var i = 0; i < burstCount; i++)
    {
        var receptor = (patternSeed + i) % 16;
        var lane = (patternSeed + (i * 7)) % 48;
        spikes.Add(new SpikeMessage
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = timestampMs,
            SourceStructure = sourceStructure,
            TargetStructure = targetStructure,
            SourceNeuronId = $"{hemisphere}:{channel}_receptor_{patternToken}_{tick}_{receptor}_{i}",
            TargetNeuronId = $"{hemisphere}:{channel}_afferent_cell_{lane}",
            SynapseId = Guid.NewGuid(),
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = Math.Clamp((0.88f + (receptor * 0.045f)) * intensity, 0.08f, 8.0f),
            ReuptakeRate = Math.Clamp(2.6f + (lane * 0.10f), 1.6f, 14.0f),
            SpikeType = i % 7 == 0 ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL,
            IsFeedback = isFeedback,
            ModulationContext = null
        });
    }

    return spikes;
}

static List<SpikeMessage> BuildLanguageStimulusSpikes(
    long tick,
    double timestampMs,
    LanguageStimulusTarget target,
    string hemisphere,
    string mode,
    IReadOnlyList<string> tokens,
    float intensity,
    int burstPerToken)
{
    var spikes = new List<SpikeMessage>(tokens.Count * burstPerToken);
    const float modeGain = 1f;
    for (var tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
    {
        var token = tokens[tokenIndex];
        var tokenHash = ComputeStableStimulusHash(token);
        for (var i = 0; i < burstPerToken; i++)
        {
            var channel = (tokenHash + (i * 17) + (tokenIndex * 7)) % 96;
            var syllableHint = (token.Length + i) % 8;
            var vesicle = Math.Clamp((0.52f + (token.Length * 0.03f) + (syllableHint * 0.015f)) * intensity * target.Gain * modeGain, 0.05f, 6.0f);
            var reuptake = ResolveLanguageReuptakeRate(mode, channel);
            var spikeType = ResolveLanguageSpikeType(mode, token, i);

            spikes.Add(new SpikeMessage
            {
                MessageId = Guid.NewGuid(),
                TimestampMs = timestampMs,
                SourceStructure = target.SourceStructure,
                TargetStructure = target.TargetStructure,
                SourceNeuronId = $"{hemisphere}:lang_{mode}_{tick}_{tokenIndex}_{i}",
                TargetNeuronId = BuildLanguageTargetNeuronId(hemisphere, target.TargetNeuronPrefix, mode, token, tokenIndex, channel),
                SynapseId = Guid.NewGuid(),
                Neurotransmitter = NTEnum.GLUTAMATE,
                VesicleQuanta = vesicle,
                ReuptakeRate = reuptake,
                SpikeType = spikeType,
                IsFeedback = false,
                ModulationContext = null
            });
        }
    }

    return spikes;
}

static SpikeTypeEnum ResolveLanguageSpikeType(string mode, string token, int burstIndex)
{
    if (mode.Equals("prosody", StringComparison.OrdinalIgnoreCase))
    {
        return burstIndex % 3 == 0 ? SpikeTypeEnum.GRADED : SpikeTypeEnum.ACTION_POTENTIAL;
    }

    if (mode.Equals("english", StringComparison.OrdinalIgnoreCase))
    {
        return burstIndex % 4 == 0 || token.Length >= 7
            ? SpikeTypeEnum.BURST
            : SpikeTypeEnum.ACTION_POTENTIAL;
    }

    return mode.Equals("repetition", StringComparison.OrdinalIgnoreCase) || token.Length >= 8
        ? SpikeTypeEnum.BURST
        : SpikeTypeEnum.ACTION_POTENTIAL;
}

static float ResolveLanguageReuptakeRate(string mode, int channel)
{
    var baseRate = mode.Equals("prosody", StringComparison.OrdinalIgnoreCase)
        ? 3.2f
        : mode.Equals("english", StringComparison.OrdinalIgnoreCase) ? 2.6f : 2.8f;
    return Math.Clamp(baseRate + (channel * 0.05f), 1.4f, 10.0f);
}

static string BuildLanguageTargetNeuronId(string hemisphere, string prefix, string mode, string token, int tokenIndex, int channel)
{
    var lexicalKey = Regex.Replace(token.Trim().ToLowerInvariant(), @"[^a-z0-9']+", string.Empty);
    lexicalKey = string.IsNullOrWhiteSpace(lexicalKey) ? "silence" : lexicalKey[..Math.Min(32, lexicalKey.Length)];
    return $"{hemisphere}:{prefix}_lex_{lexicalKey}_tok_{tokenIndex}_cell_{channel}";
}

static string[] TokenizeLanguageInput(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return Array.Empty<string>();
    }

    var tokens = Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9']+")
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Take(24)
        .ToArray();
    return tokens;
}


static string NormalizeLanguageMode(string? mode)
{
    if (string.IsNullOrWhiteSpace(mode))
    {
        return "repetition";
    }

    var normalized = mode.Trim().ToLowerInvariant();
    return normalized switch
    {
        "comprehension" or "comprehend" or "understand" => "comprehension",
        "production" or "produce" or "speech" => "production",
        "prosody" or "intonation" or "affect" or "melody" or "rhythm" => "prosody",
        "emergent" or "proto" or "novel" or "self" => "emergent",
        "english" or "en" or "natural" or "word" or "words" => "english",
        _ => "repetition"
    };
}

static IReadOnlyList<LanguageStimulusTarget> GetLanguageStimulusPlan(string mode) => mode switch
{
    "english" =>
    [
        new(StructureId.Thalamus, StructureId.A1, "a1_english_phoneme", null, 0.92f),
        new(StructureId.A1, StructureId.WernickePstgPsts, "wernicke_english_lexeme", "L", 1.05f),
        new(StructureId.WernickePstgPsts, StructureId.SupramarginalAngular, "smg_english_phonological", "L", 0.95f),
        new(StructureId.WernickePstgPsts, StructureId.TemporalAssociation, "temporal_english_semantic", "L", 1.00f),
        new(StructureId.TemporalAssociation, StructureId.Pfc, "pfc_english_context", "L", 0.90f),
        new(StructureId.WernickePstgPsts, StructureId.ArcuateFasciculus, "arcuate_english_dorsal", "L", 0.88f),
        new(StructureId.ArcuateFasciculus, StructureId.BrocaBa44Ba45, "broca_english_sequence", "L", 0.82f),
        new(StructureId.BrocaBa44Ba45, StructureId.Sma, "sma_english_inner_speech", "L", 0.70f),
        new(StructureId.Sma, StructureId.M1, "m1_english_articulation", null, 0.62f)
    ],
    "comprehension" =>
    [
        new(StructureId.Thalamus, StructureId.A1, "a1_tonotopic", null, 0.90f),
        new(StructureId.A1, StructureId.WernickePstgPsts, "wernicke_lexical", "L", 1.00f),
        new(StructureId.WernickePstgPsts, StructureId.SupramarginalAngular, "smg_phonological", "L", 0.92f),
        new(StructureId.SupramarginalAngular, StructureId.TemporalAssociation, "temporal_semantic", "L", 0.86f),
        new(StructureId.TemporalAssociation, StructureId.Pfc, "pfc_language_context", "L", 0.78f)
    ],
    "production" =>
    [
        new(StructureId.Pfc, StructureId.BrocaBa44Ba45, "broca_sequence", "L", 1.00f),
        new(StructureId.BrocaBa44Ba45, StructureId.Sma, "sma_speech_sequence", "L", 0.95f),
        new(StructureId.Sma, StructureId.M1, "m1_articulation", null, 0.90f)
    ],
    "prosody" =>
    [
        new(StructureId.Thalamus, StructureId.A1, "a1_tonotopic", null, 0.88f),
        new(StructureId.A1, StructureId.TemporalAssociation, "temporal_prosody", "R", 1.00f),
        new(StructureId.TemporalAssociation, StructureId.SupramarginalAngular, "smg_rhythmic", "R", 0.88f),
        new(StructureId.TemporalAssociation, StructureId.Insula, "insula_affective_prosody", "R", 0.92f),
        new(StructureId.Insula, StructureId.OrbitofrontalCortex, "ofc_valence", "R", 0.86f),
        new(StructureId.TemporalAssociation, StructureId.Pfc, "pfc_prosodic_context", "R", 0.82f),
        new(StructureId.Pfc, StructureId.Sma, "sma_prosodic_motor", "R", 0.76f),
        new(StructureId.Sma, StructureId.M1, "m1_prosodic_articulation", null, 0.70f)
    ],
    "emergent" =>
    [
        new(StructureId.Thalamus, StructureId.A1, "a1_tonotopic", null, 0.88f),
        new(StructureId.A1, StructureId.WernickePstgPsts, "wernicke_lexical", "L", 0.96f),
        new(StructureId.WernickePstgPsts, StructureId.ArcuateFasciculus, "arcuate_dorsal", "L", 1.00f),
        new(StructureId.WernickePstgPsts, StructureId.SupramarginalAngular, "smg_phonological", "L", 0.90f),
        new(StructureId.ArcuateFasciculus, StructureId.BrocaBa44Ba45, "broca_dorsal_input", "L", 1.00f),
        new(StructureId.TemporalAssociation, StructureId.Pfc, "pfc_language_context", null, 0.82f),
        new(StructureId.Pfc, StructureId.BrocaBa44Ba45, "broca_sequence", "L", 0.94f),
        new(StructureId.BrocaBa44Ba45, StructureId.Sma, "sma_speech_sequence", "L", 0.94f),
        new(StructureId.Sma, StructureId.M1, "m1_articulation", null, 0.88f)
    ],
    _ =>
    [
        new(StructureId.Thalamus, StructureId.A1, "a1_tonotopic", null, 0.85f),
        new(StructureId.A1, StructureId.WernickePstgPsts, "wernicke_lexical", "L", 0.95f),
        new(StructureId.WernickePstgPsts, StructureId.ArcuateFasciculus, "arcuate_dorsal", "L", 1.00f),
        new(StructureId.WernickePstgPsts, StructureId.SupramarginalAngular, "smg_phonological", "L", 0.90f),
        new(StructureId.ArcuateFasciculus, StructureId.BrocaBa44Ba45, "broca_dorsal_input", "L", 1.00f),
        new(StructureId.BrocaBa44Ba45, StructureId.Sma, "sma_speech_sequence", "L", 0.95f),
        new(StructureId.Sma, StructureId.M1, "m1_articulation", null, 0.90f)
    ]
};

static int ParseAcceptedCount(string responsePayload, int fallback)
{
    if (string.IsNullOrWhiteSpace(responsePayload))
    {
        return fallback;
    }

    try
    {
        using var doc = JsonDocument.Parse(responsePayload);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "accepted", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var accepted))
            {
                return accepted;
            }

            if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out accepted))
            {
                return accepted;
            }
        }
    }
    catch
    {
        // Keep fallback when payload parsing fails.
    }

    return fallback;
}

static void DispatchStimulusToInstancesInBackground(
    string label,
    IReadOnlyList<ServiceInstance> targetInstances,
    Func<ServiceInstance, IReadOnlyList<SpikeMessage>> buildSpikes,
    IHttpClientFactory clientFactory,
    SimulationState state,
    long tick,
    double timestampMs,
    bool logSuccess = true)
{
    if (targetInstances.Count == 0)
    {
        return;
    }

    _ = Task.Run(async () =>
    {
        try
        {
            var result = await DispatchStimulusToInstancesAsync(
                targetInstances,
                buildSpikes,
                clientFactory,
                state,
                tick,
                timestampMs,
                CancellationToken.None);

            if (logSuccess)
            {
                state.AppendOutputLog(
                    $"{label} deferred dispatch completed: generated={result.GeneratedSpikes}, delivered={result.DeliveredSpikes}, targets={targetInstances.Count}, errors={result.Errors.Count}.");
                if (result.DeliveredSpikes > 0)
                {
                    state.AppendSpikeLog(
                        $"{label}: deferred delivered {result.DeliveredSpikes}/{result.GeneratedSpikes} spikes across {targetInstances.Count} targets.");
                }
            }
            else if (result.Errors.Count > 0)
            {
                state.AppendOutputLog(
                    $"{label} deferred dispatch completed with {result.Errors.Count} error(s) across {targetInstances.Count} targets.");
            }
        }
        catch (Exception ex)
        {
            state.AppendOutputLog(
                $"{label} deferred dispatch failed: {ex.GetType().Name}: {ex.Message}");
        }
    }, CancellationToken.None);
}

static async Task<StimulusDispatchResult> DispatchStimulusToInstancesAsync(
    IReadOnlyList<ServiceInstance> targetInstances,
    Func<ServiceInstance, IReadOnlyList<SpikeMessage>> buildSpikes,
    IHttpClientFactory clientFactory,
    SimulationState state,
    long tick,
    double timestampMs,
    CancellationToken ct)
{
    var generatedSpikes = 0;
    var deliveredSpikes = 0;
    var errors = new List<string>();
    var errorGate = new object();
    var dispatchTasks = new Task[targetInstances.Count];

    for (var i = 0; i < targetInstances.Count; i++)
    {
        var instance = targetInstances[i];
        dispatchTasks[i] = DispatchSingleInstanceAsync(instance);
    }

    await Task.WhenAll(dispatchTasks);
    return new StimulusDispatchResult(generatedSpikes, deliveredSpikes, errors);

    async Task DispatchSingleInstanceAsync(ServiceInstance instance)
    {
        var spikes = buildSpikes(instance);
        Interlocked.Add(ref generatedSpikes, spikes.Count);
        if (spikes.Count == 0)
        {
            return;
        }

        var hemisphere = instance.HemisphereNormalized;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(900));
        try
        {
            var client = clientFactory.CreateClient("dnne");
            client.BaseAddress = instance.Endpoint;
            client.Timeout = Timeout.InfiniteTimeSpan;

            var accepted = await DispatchStimulusSpikesAsync(client, spikes, timeout.Token);
            accepted = Math.Clamp(accepted, 0, spikes.Count);
            if (accepted > 0)
            {
                Interlocked.Add(ref deliveredSpikes, accepted);
                state.RecordDispatchedSpikes(
                    tick,
                    timestampMs,
                    hemisphere,
                    hemisphere,
                    instance.InstanceKey,
                    spikes,
                    accepted);
            }
        }
        catch (Exception ex)
        {
            lock (errorGate)
            {
                errors.Add($"{instance.InstanceKey}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}

static async Task<int> DispatchStimulusSpikesAsync(HttpClient client, IReadOnlyList<SpikeMessage> spikes, CancellationToken cancellationToken)
{
    if (spikes.Count == 0)
    {
        return 0;
    }

    byte[] payload;
    await using (var envelope = new MemoryStream(Math.Max(256, spikes.Count * 128)))
    {
        foreach (var spike in spikes)
        {
            await SpikeProtocol.send_spike(spike, envelope, cancellationToken);
        }

        payload = envelope.ToArray();
    }

    try
    {
        using var body = new ByteArrayContent(payload);
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        using var dispatch = await client.PostAsync("/api/v1/structure/spike-batch", body, cancellationToken);
        if (dispatch.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            var deliveredFallback = 0;
            foreach (var spike in spikes)
            {
                byte[] spikePayload;
                await using (var singleEnvelope = new MemoryStream(256))
                {
                    await SpikeProtocol.send_spike(spike, singleEnvelope, cancellationToken);
                    spikePayload = singleEnvelope.ToArray();
                }

                using var singleBody = new ByteArrayContent(spikePayload);
                singleBody.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                using var singleDispatch = await client.PostAsync("/api/v1/structure/spike", singleBody, cancellationToken);
                await EnsureStimulusSuccessAsync(singleDispatch, cancellationToken);
                deliveredFallback++;
            }

            return deliveredFallback;
        }

        var responseText = await dispatch.Content.ReadAsStringAsync(cancellationToken);
        await EnsureStimulusSuccessAsync(dispatch, cancellationToken, responseText);
        return Math.Clamp(ParseAcceptedCount(responseText, spikes.Count), 0, spikes.Count);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch
    {
        using var jsonBody = JsonContent.Create(spikes, DnneJsonContext.Default.ListSpikeMessage);
        using var jsonDispatch = await client.PostAsync("/api/v1/structure/spike-batch", jsonBody, cancellationToken);
        if (jsonDispatch.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            var deliveredJsonFallback = 0;
            foreach (var spike in spikes)
            {
                using var singleJsonBody = JsonContent.Create(spike, DnneJsonContext.Default.SpikeMessage);
                using var singleJsonDispatch = await client.PostAsync("/api/v1/structure/spike", singleJsonBody, cancellationToken);
                await EnsureStimulusSuccessAsync(singleJsonDispatch, cancellationToken);
                deliveredJsonFallback++;
            }

            return deliveredJsonFallback;
        }

        var jsonResponse = await jsonDispatch.Content.ReadAsStringAsync(cancellationToken);
        await EnsureStimulusSuccessAsync(jsonDispatch, cancellationToken, jsonResponse);
        return Math.Clamp(ParseAcceptedCount(jsonResponse, spikes.Count), 0, spikes.Count);
    }
}

static async Task EnsureStimulusSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken, string? responseBody = null)
{
    if (response.IsSuccessStatusCode)
    {
        return;
    }

    string details;
    if (responseBody is not null)
    {
        details = responseBody.Trim();
    }
    else
    {
        try
        {
            details = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        }
        catch
        {
            details = string.Empty;
        }
    }

    if (string.IsNullOrWhiteSpace(details))
    {
        details = response.ReasonPhrase ?? "request failed";
    }

    if (details.Length > 320)
    {
        details = $"{details[..320]}...";
    }

    throw new HttpRequestException($"Response status code {(int)response.StatusCode} ({response.StatusCode}). {details}");
}

static string? NormalizeHemisphereHint(string? hemisphere)
{
    if (string.IsNullOrWhiteSpace(hemisphere))
    {
        return null;
    }

    var trimmed = hemisphere.Trim();
    if (trimmed.Equals("both", StringComparison.OrdinalIgnoreCase) ||
        trimmed.Equals("all", StringComparison.OrdinalIgnoreCase) ||
        trimmed.Equals("any", StringComparison.OrdinalIgnoreCase) ||
        trimmed.Equals("*", StringComparison.Ordinal))
    {
        return null;
    }

    if (trimmed.Equals("left", StringComparison.OrdinalIgnoreCase))
    {
        return "L";
    }

    if (trimmed.Equals("right", StringComparison.OrdinalIgnoreCase))
    {
        return "R";
    }

    if (trimmed.Equals("midline", StringComparison.OrdinalIgnoreCase) ||
        trimmed.Equals("middle", StringComparison.OrdinalIgnoreCase))
    {
        return "M";
    }

    if (trimmed.Length == 1)
    {
        var c = char.ToUpperInvariant(trimmed[0]);
        if (c is 'L' or 'R' or 'M')
        {
            return c.ToString();
        }
    }

    // Unknown hint: default to all hemispheres for robustness.
    return null;
}

static StructureId[] ResolveBodyStateCerebellarTargets(float contactLevel, float turnSignal, float motorAsymmetry)
{
    var targets = new List<StructureId>
    {
        StructureId.CerebellarGranule,
        StructureId.CerebellarVermis,
        StructureId.CerebellarLobules
    };

    var teachingError = Math.Max(contactLevel, Math.Max(turnSignal, motorAsymmetry));
    if (teachingError >= 0.08f)
    {
        targets.Add(StructureId.InferiorOlive);
    }

    return targets.ToArray();
}

static StructureId[] ResolveBodyStateInteroceptiveTargets(float interoceptiveSignal)
    => interoceptiveSignal > 0.005f
        ?
        [
            StructureId.NucleusTractusSolitarius,
            StructureId.Hypothalamus,
            StructureId.Insula
        ]
        : Array.Empty<StructureId>();

static string BuildBodyStateTargetLabel(
    StructureId primaryTarget,
    bool includeVestibular,
    IReadOnlyList<StructureId> cerebellarTargets,
    IReadOnlyList<StructureId> interoceptiveTargets)
{
    var labels = new List<string> { primaryTarget.ToString() };
    if (includeVestibular)
    {
        labels.Add(StructureId.VestibularNuclei.ToString());
    }

    labels.AddRange(cerebellarTargets.Select(t => t.ToString()));
    labels.AddRange(interoceptiveTargets.Select(t => t.ToString()));
    return string.Join(", ", labels.Distinct(StringComparer.OrdinalIgnoreCase));
}

internal static class CachedJsonOptions
{
    public static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

internal sealed class RuntimePerformanceProfileState
{
    private readonly object _gate = new();
    private RuntimePerformanceProfileSettings _settings;
    private long _generation;

    public RuntimePerformanceProfileState(IConfiguration configuration)
    {
        _settings = RuntimePerformanceProfileSettings.FromConfiguration(configuration);
    }

    // Hot-path readers (called from the tick loop and stream handlers). Reference
    // and 64-bit reads on .NET are atomic; the lock was only protecting against
    // torn updates that Interlocked/Volatile already prevent.
    public long Generation => Interlocked.Read(ref _generation);

    public RuntimePerformanceProfileSettings GetSnapshot() => Volatile.Read(ref _settings);

    public (long Generation, RuntimePerformanceProfileSettings Settings) ApplyProfile(string profile)
    {
        lock (_gate)
        {
            var settings = RuntimePerformanceProfileSettings.ForProfile(profile);
            Volatile.Write(ref _settings, settings);
            var nextGeneration = Interlocked.Increment(ref _generation);
            return (nextGeneration, settings);
        }
    }
}

internal sealed record RuntimePerformanceProfileSettings(
    string ProfileName,
    int TickAckTimeoutMs,
    int TickIoTimeoutMs,
    int TickPublishWaitMs,
    int TickPublishSettleMs,
    int MaxTickRequestConcurrency,
    int MaxDispatchConcurrency,
    int MaxSpikeDispatchPerServicePerTick,
    int MaxSpikeDispatchTotalPerTick,
    int TopQueryEveryNTicks,
    int MaxTopQueriesPerTick,
    int SnapshotEveryNTicks,
    bool UseDirectStepFastPath)
{
    public const string SupportedProfileList = "stable, diagnostic, normal, fast, headless";

    public static bool IsSupported(string profile) =>
        profile.Equals("stable", StringComparison.OrdinalIgnoreCase) ||
        profile.Equals("diagnostic", StringComparison.OrdinalIgnoreCase) ||
        profile.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
        profile.Equals("fast", StringComparison.OrdinalIgnoreCase) ||
        profile.Equals("headless", StringComparison.OrdinalIgnoreCase) ||
        profile.Equals("ultra", StringComparison.OrdinalIgnoreCase);

    public static RuntimePerformanceProfileSettings FromConfiguration(IConfiguration configuration)
    {
        var configuredProfile = configuration.GetValue<string>("PerformanceProfile");
        if (!string.IsNullOrWhiteSpace(configuredProfile) && IsSupported(configuredProfile))
        {
            return ForProfile(configuredProfile);
        }

        var tickAckTimeoutMs = Math.Max(100, configuration.GetValue<int>("TickAckTimeoutMs", 2500));
        var tickIoTimeoutMs = Math.Max(200, configuration.GetValue<int>("TickIoTimeoutMs", 6000));
        var tickPublishWaitMs = Math.Max(10, configuration.GetValue<int>("TickPublishWaitMs", 80));
        var tickPublishSettleMs = Math.Clamp(configuration.GetValue<int>("TickPublishSettleMs", 6), 1, 100);
        var maxTickRequestConcurrency = Math.Clamp(configuration.GetValue<int>("MaxTickRequestConcurrency", 48), 4, 512);
        var maxDispatchConcurrency = Math.Clamp(configuration.GetValue<int>("MaxDispatchConcurrency", 192), 4, 2048);
        var maxSpikeDispatchPerServicePerTick = Math.Clamp(configuration.GetValue<int>("MaxSpikeDispatchPerServicePerTick", 12), 8, 4096);
        var maxSpikeDispatchTotalPerTick = Math.Clamp(configuration.GetValue<int>("MaxSpikeDispatchTotalPerTick", 240), 16, 65536);
        var topQueryEveryNTicks = Math.Max(1, configuration.GetValue<int>("TopQueryEveryNTicks", 6));
        var maxTopQueriesPerTick = Math.Clamp(configuration.GetValue<int>("MaxTopQueriesPerTick", 10), 1, 256);
        var snapshotEveryNTicks = Math.Max(1, configuration.GetValue<int>("SnapshotEveryNTicks", 10));
        // The direct step response is the only lossless tick contract. The old
        // acknowledgement-plus-publish transport has been retired.
        const bool useDirectStepFastPath = true;

        return new RuntimePerformanceProfileSettings(
            "normal",
            tickAckTimeoutMs,
            tickIoTimeoutMs,
            tickPublishWaitMs,
            tickPublishSettleMs,
            maxTickRequestConcurrency,
            maxDispatchConcurrency,
            maxSpikeDispatchPerServicePerTick,
            maxSpikeDispatchTotalPerTick,
            topQueryEveryNTicks,
            maxTopQueriesPerTick,
            snapshotEveryNTicks,
            useDirectStepFastPath);
    }

    public static RuntimePerformanceProfileSettings ForProfile(string profile)
    {
        if (profile.Equals("stable", StringComparison.OrdinalIgnoreCase) ||
            profile.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimePerformanceProfileSettings(
                "stable",
                TickAckTimeoutMs: 5200,
                TickIoTimeoutMs: 14000,
                TickPublishWaitMs: 360,
                TickPublishSettleMs: 18,
                MaxTickRequestConcurrency: 10,
                MaxDispatchConcurrency: 32,
                MaxSpikeDispatchPerServicePerTick: 6,
                MaxSpikeDispatchTotalPerTick: 160,
                TopQueryEveryNTicks: 24,
                MaxTopQueriesPerTick: 1,
                SnapshotEveryNTicks: 12,
                UseDirectStepFastPath: true);
        }

        if (profile.Equals("diagnostic", StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimePerformanceProfileSettings(
                "diagnostic",
                TickAckTimeoutMs: 4500,
                TickIoTimeoutMs: 12000,
                TickPublishWaitMs: 140,
                TickPublishSettleMs: 12,
                MaxTickRequestConcurrency: 20,
                MaxDispatchConcurrency: 56,
                MaxSpikeDispatchPerServicePerTick: 10,
                MaxSpikeDispatchTotalPerTick: 220,
                TopQueryEveryNTicks: 2,
                MaxTopQueriesPerTick: 18,
                SnapshotEveryNTicks: 2,
                UseDirectStepFastPath: true);
        }

        if (profile.Equals("fast", StringComparison.OrdinalIgnoreCase) ||
            profile.Equals("ultra", StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimePerformanceProfileSettings(
                "fast",
                TickAckTimeoutMs: 3600,
                TickIoTimeoutMs: 9000,
                TickPublishWaitMs: 220,
                TickPublishSettleMs: 6,
                MaxTickRequestConcurrency: 48,
                MaxDispatchConcurrency: 128,
                MaxSpikeDispatchPerServicePerTick: 18,
                MaxSpikeDispatchTotalPerTick: 768,
                TopQueryEveryNTicks: 18,
                MaxTopQueriesPerTick: 2,
                SnapshotEveryNTicks: 12,
                UseDirectStepFastPath: true);
        }

        if (profile.Equals("headless", StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimePerformanceProfileSettings(
                "headless",
                TickAckTimeoutMs: 3200,
                TickIoTimeoutMs: 8000,
                TickPublishWaitMs: 180,
                TickPublishSettleMs: 4,
                MaxTickRequestConcurrency: 72,
                MaxDispatchConcurrency: 192,
                MaxSpikeDispatchPerServicePerTick: 24,
                MaxSpikeDispatchTotalPerTick: 1536,
                TopQueryEveryNTicks: 48,
                MaxTopQueriesPerTick: 1,
                SnapshotEveryNTicks: 30,
                UseDirectStepFastPath: true);
        }

        return new RuntimePerformanceProfileSettings(
            "normal",
            TickAckTimeoutMs: 3800,
            TickIoTimeoutMs: 9500,
            TickPublishWaitMs: 240,
            TickPublishSettleMs: 6,
            MaxTickRequestConcurrency: 32,
            MaxDispatchConcurrency: 96,
            MaxSpikeDispatchPerServicePerTick: 10,
            MaxSpikeDispatchTotalPerTick: 320,
            TopQueryEveryNTicks: 8,
            MaxTopQueriesPerTick: 8,
            SnapshotEveryNTicks: 6,
            UseDirectStepFastPath: true);
    }
}

internal sealed class AutoProfileRuntimeState
{
    private readonly object _gate = new();
    private AutoProfileSettings _settings;
    private long _generation;

    public AutoProfileRuntimeState(IConfiguration configuration)
    {
        _settings = AutoProfileSettings.FromConfiguration(configuration);
    }

    public long Generation
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    public AutoProfileSettings GetSnapshot()
    {
        lock (_gate)
        {
            return _settings;
        }
    }

    public (long Generation, AutoProfileSettings Settings) Apply(AutoProfileControlRequest request)
    {
        lock (_gate)
        {
            var next = _settings with
            {
                Enabled = request.Enabled ?? _settings.Enabled,
                AllowRecovery = request.AllowRecovery ?? _settings.AllowRecovery,
                WarmupTicks = request.WarmupTicks ?? _settings.WarmupTicks,
                ManualHoldTicks = request.ManualHoldTicks ?? _settings.ManualHoldTicks,
                DegradeNonOkRatio = request.DegradeNonOkRatio ?? _settings.DegradeNonOkRatio,
                DegradeAckLatencyMs = request.DegradeAckLatencyMs ?? _settings.DegradeAckLatencyMs,
                DegradeSnapshotAgeTicks = request.DegradeSnapshotAgeTicks ?? _settings.DegradeSnapshotAgeTicks,
                DegradeConsecutiveTicks = request.DegradeConsecutiveTicks ?? _settings.DegradeConsecutiveTicks,
                RecoveryNonOkRatio = request.RecoveryNonOkRatio ?? _settings.RecoveryNonOkRatio,
                RecoveryAckLatencyMs = request.RecoveryAckLatencyMs ?? _settings.RecoveryAckLatencyMs,
                RecoverySnapshotAgeTicks = request.RecoverySnapshotAgeTicks ?? _settings.RecoverySnapshotAgeTicks,
                RecoveryConsecutiveTicks = request.RecoveryConsecutiveTicks ?? _settings.RecoveryConsecutiveTicks
            };

            _settings = AutoProfileSettings.Normalize(next);
            _generation++;
            return (_generation, _settings);
        }
    }
}

internal sealed record AutoProfileSettings(
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
    int RecoveryConsecutiveTicks)
{
    public static AutoProfileSettings Default => Normalize(new AutoProfileSettings(
        Enabled: true,
        AllowRecovery: true,
        WarmupTicks: 80,
        ManualHoldTicks: 500,
        DegradeNonOkRatio: 0.12,
        DegradeAckLatencyMs: 900.0,
        DegradeSnapshotAgeTicks: 20,
        DegradeConsecutiveTicks: 6,
        RecoveryNonOkRatio: 0.02,
        RecoveryAckLatencyMs: 350.0,
        RecoverySnapshotAgeTicks: 8,
        RecoveryConsecutiveTicks: 350));

    public static AutoProfileSettings FromConfiguration(IConfiguration configuration)
    {
        var baseline = Default;
        var configured = baseline with
        {
            Enabled = configuration.GetValue<bool>("AutoProfile:Enabled", baseline.Enabled),
            AllowRecovery = configuration.GetValue<bool>("AutoProfile:AllowRecovery", baseline.AllowRecovery),
            WarmupTicks = configuration.GetValue<int>("AutoProfile:WarmupTicks", baseline.WarmupTicks),
            ManualHoldTicks = configuration.GetValue<int>("AutoProfile:ManualHoldTicks", baseline.ManualHoldTicks),
            DegradeNonOkRatio = configuration.GetValue<double>("AutoProfile:DegradeNonOkRatio", baseline.DegradeNonOkRatio),
            DegradeAckLatencyMs = configuration.GetValue<double>("AutoProfile:DegradeAckLatencyMs", baseline.DegradeAckLatencyMs),
            DegradeSnapshotAgeTicks = configuration.GetValue<long>("AutoProfile:DegradeSnapshotAgeTicks", baseline.DegradeSnapshotAgeTicks),
            DegradeConsecutiveTicks = configuration.GetValue<int>("AutoProfile:DegradeConsecutiveTicks", baseline.DegradeConsecutiveTicks),
            RecoveryNonOkRatio = configuration.GetValue<double>("AutoProfile:RecoveryNonOkRatio", baseline.RecoveryNonOkRatio),
            RecoveryAckLatencyMs = configuration.GetValue<double>("AutoProfile:RecoveryAckLatencyMs", baseline.RecoveryAckLatencyMs),
            RecoverySnapshotAgeTicks = configuration.GetValue<long>("AutoProfile:RecoverySnapshotAgeTicks", baseline.RecoverySnapshotAgeTicks),
            RecoveryConsecutiveTicks = configuration.GetValue<int>("AutoProfile:RecoveryConsecutiveTicks", baseline.RecoveryConsecutiveTicks)
        };

        return Normalize(configured);
    }

    public static AutoProfileSettings Normalize(AutoProfileSettings value)
    {
        var warmupTicks = Math.Max(0, value.WarmupTicks);
        var manualHoldTicks = Math.Max(0, value.ManualHoldTicks);
        var degradeNonOkRatio = Math.Clamp(value.DegradeNonOkRatio, 0.01, 1.0);
        var degradeAckLatencyMs = Math.Max(100.0, value.DegradeAckLatencyMs);
        var degradeSnapshotAgeTicks = Math.Max(4L, value.DegradeSnapshotAgeTicks);
        var degradeConsecutiveTicks = Math.Max(2, value.DegradeConsecutiveTicks);
        var recoveryNonOkRatio = Math.Clamp(value.RecoveryNonOkRatio, 0.0, 0.20);
        var recoveryAckLatencyMs = Math.Max(50.0, value.RecoveryAckLatencyMs);
        var recoverySnapshotAgeTicks = Math.Max(2L, value.RecoverySnapshotAgeTicks);
        var recoveryConsecutiveTicks = Math.Max(20, value.RecoveryConsecutiveTicks);

        return value with
        {
            WarmupTicks = warmupTicks,
            ManualHoldTicks = manualHoldTicks,
            DegradeNonOkRatio = degradeNonOkRatio,
            DegradeAckLatencyMs = degradeAckLatencyMs,
            DegradeSnapshotAgeTicks = degradeSnapshotAgeTicks,
            DegradeConsecutiveTicks = degradeConsecutiveTicks,
            RecoveryNonOkRatio = recoveryNonOkRatio,
            RecoveryAckLatencyMs = recoveryAckLatencyMs,
            RecoverySnapshotAgeTicks = recoverySnapshotAgeTicks,
            RecoveryConsecutiveTicks = recoveryConsecutiveTicks
        };
    }
}

internal sealed record IssuedDyadPrompt(
    string PromptFingerprint,
    long GroundingTick,
    DateTimeOffset IssuedAtUtc);

internal sealed class SimulationState
{
    private readonly object _gate = new();
    private object? _lastStartupHealthSnapshot;
    private object? _lastValidationSnapshot;
    private readonly Queue<RuntimeLogEntry> _outputLog = new();
    private readonly Queue<RuntimeLogEntry> _spikeLog = new();
    private readonly Queue<DispatchedSpikeTrace> _dispatchSpikeTrace = new();
    private readonly Queue<DyadLanguageCandidateAuditRecord> _dyadLanguageCandidateReviews = new();
    private readonly Dictionary<string, IssuedDyadPrompt> _issuedDyadPrompts = new(StringComparer.Ordinal);
    private readonly Dictionary<StructureId, int> _dispatchLifetimeOut = new();
    private readonly Dictionary<StructureId, int> _dispatchLifetimeIn = new();
    private readonly Dictionary<StructureId, long> _dispatchLastOutTick = new();
    private readonly Dictionary<StructureId, long> _dispatchLastInTick = new();
    private readonly List<CurriculumTaskAccumulator> _curriculumTasks = CurriculumTaskAccumulator.CreateDefaults();
    private long _cachedCircuitAuditTick = -1;
    private int _cachedCircuitAuditWarningLimit;
    private object? _cachedCircuitAuditSnapshot;
    private long _cachedConsolidationTelemetryTick = -1;
    private object? _cachedConsolidationTelemetrySnapshot;
    private long _cachedBrainBehaviorTick = -1;
    private object? _cachedBrainBehaviorSnapshot;
    private long _cachedProsodyTelemetryTick = -1;
    private object? _cachedProsodyTelemetrySnapshot;
    private const int MaxRuntimeLogEntries = 500;
    private const int MaxDispatchTraceEntries = 20000;
    private const int MaxDyadLanguageCandidateReviews = 256;
    private const int MaxIssuedDyadPrompts = 256;
    private static readonly TimeSpan IssuedDyadPromptLifetime = TimeSpan.FromMinutes(10);
    private const int MaxPopulationDispatchTracePerBatch = 96;
    private const float MetabolicReferenceIntervalMs = 20_000f;
    private const float MinMetabolicRateScale = 0.000001f;
    private long _restartGeneration;
    private int _curriculumStageIndex;
    private long _curriculumLastStageTransitionTick;

    public double SimulationClockMs { get; private set; }
    public double TickDurationMs { get; private set; } = 1.0;
    public Dictionary<StructureId, string> ServiceRegistry { get; } = new();
    public Dictionary<StructureId, List<SynapticConnection>> ConnectivityMap { get; } = new();
    public Dictionary<BrainRhythm, double> OscillationPhases { get; } = new()
    {
        [BrainRhythm.DELTA] = 0,
        [BrainRhythm.THETA] = 0,
        [BrainRhythm.ALPHA] = 0,
        [BrainRhythm.BETA] = 0,
        [BrainRhythm.GAMMA] = 0
    };

    public long Tick { get; private set; }
    public long LastSnapshotTick { get; private set; }
    public double LastSnapshotSimulationMs { get; private set; }
    public long LastSnapshotWallClockUnixMs { get; private set; }
    public long TotalSpontaneousGenerated { get; private set; }
    public long TotalSpontaneousDelivered { get; private set; }
    public long TotalSpontaneousDispatchErrors { get; private set; }
    public string PerformanceProfileName { get; private set; } = "normal";
    public MetabolicPhysiologyRuntime MetabolicPhysiology { get; private set; } = MetabolicPhysiologyRuntime.Default;
    public InputGateRuntime InputGates { get; private set; } = InputGateRuntime.Default;
    public BodyStateRuntime BodyState { get; private set; } = BodyStateRuntime.Default;
    public NeuronalVisualAttentionDecision VisualAttention { get; private set; } = NeuronalVisualAttentionDecision.Unavailable;
    public NeuronalMotorRuntime NeuronalMotor { get; private set; } = NeuronalMotorRuntime.Default;
    public NeuronalLanguageGroundingDecision NeuronalLanguageGrounding { get; private set; } = NeuronalLanguageGroundingDecision.Unavailable;
    public NeuronalPerceptDecision NeuronalPerception { get; private set; } = NeuronalPerceptDecision.Unavailable;
    public NeuronalMemoryDecision NeuronalMemory { get; private set; } = NeuronalMemoryDecision.Unavailable;
    public NeuronalAttentionWorkspaceDecision NeuronalAttentionWorkspace { get; private set; } = NeuronalAttentionWorkspaceDecision.Unavailable;
    public NeuronalSleepConsolidationDecision NeuronalSleepConsolidation { get; private set; } = NeuronalSleepConsolidationDecision.Unavailable;
    public NeuronalAffectValuationDecision NeuronalAffectValuation { get; private set; } = NeuronalAffectValuationDecision.Unavailable;
    public NeuronalExecutiveDecision NeuronalExecutive { get; private set; } = NeuronalExecutiveDecision.Unavailable;
    public CurriculumRuntime Curriculum { get; private set; } = CurriculumRuntime.Default;
    public Dictionary<StructureId, ServiceRuntimeTelemetry> ServiceTelemetry { get; } = new();
    public TransportRuntimeStats TransportStats { get; private set; } = TransportRuntimeStats.Empty;

    public void Configure(double tickDurationMs, Dictionary<StructureId, string> registry, Dictionary<StructureId, List<SynapticConnection>> connectivity)
    {
        lock (_gate)
        {
            TickDurationMs = tickDurationMs;
            ServiceRegistry.Clear();
            foreach (var pair in registry) ServiceRegistry[pair.Key] = pair.Value;
            ConnectivityMap.Clear();
            foreach (var pair in connectivity) ConnectivityMap[pair.Key] = pair.Value;
            RefreshCurriculumSnapshotLocked(Tick);
        }
    }

    public TickSignal AdvanceClockAndCreateTickSignal()
    {
        lock (_gate)
        {
            Tick++;
            SimulationClockMs += TickDurationMs;
            AdvancePhase(BrainRhythm.DELTA, 2.0);
            AdvancePhase(BrainRhythm.THETA, 6.0);
            AdvancePhase(BrainRhythm.ALPHA, 10.0);
            AdvancePhase(BrainRhythm.BETA, 20.0);
            AdvancePhase(BrainRhythm.GAMMA, 40.0);
            var atpReserve = Math.Clamp(
                MetabolicPhysiology.AtpBudget / Math.Max(0.0001f, MetabolicPhysiology.MaxAtpBudget),
                0f,
                1f);
            var pressure = Math.Clamp(
                MetabolicPhysiology.HomeostaticPressure / Math.Max(0.0001f, MetabolicPhysiology.MaxHomeostaticPressure),
                0f,
                1f);
            var homeostaticSleepDrive = Math.Clamp((pressure * 0.58f) + ((1f - atpReserve) * 0.42f), 0f, 1f);
            var metabolicWakeReserve = Math.Clamp(atpReserve * (1f - (pressure * 0.38f)), 0f, 1f);
            return new TickSignal(
                Tick,
                SimulationClockMs,
                TickDurationMs,
                // Compatibility fields remain on the wire for old tools, but structures
                // derive modulation and teaching from local receptor currents only.
                new NeuromodState(),
                new Dictionary<BrainRhythm, double>(OscillationPhases),
                0f,
                homeostaticSleepDrive,
                metabolicWakeReserve);
        }
    }

    public DyadLanguageCandidateResponse ReviewDyadLanguageCandidate(DyadLanguageCandidateProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        lock (_gate)
        {
            var existing = _dyadLanguageCandidateReviews.LastOrDefault(record =>
                string.Equals(record.Proposal.SessionId, proposal.SessionId, StringComparison.Ordinal) &&
                string.Equals(record.Proposal.TurnId, proposal.TurnId, StringComparison.Ordinal));
            if (existing is not null)
            {
                var sameProposal = string.Equals(existing.Proposal.PromptFingerprint, proposal.PromptFingerprint, StringComparison.Ordinal) &&
                                   string.Equals(existing.Proposal.CandidateText, proposal.CandidateText, StringComparison.Ordinal) &&
                                   string.Equals(existing.Proposal.CandidateKind, proposal.CandidateKind, StringComparison.Ordinal);
                return new DyadLanguageCandidateResponse(
                    DyadLanguageContract.ProtocolVersion,
                    proposal.SessionId,
                    proposal.TurnId,
                    sameProposal ? existing.Decision : DyadLanguageCandidateDecision.Deferred,
                    sameProposal ? existing.DecisionReason : "A different candidate already exists for this session and turn; conflicting replay was rejected.",
                    existing.Grounding,
                    existing.ReviewSequence,
                    existing.ReviewedAtUtc);
            }

            var grounding = BuildDyadLanguageGroundingSnapshotLocked();
            var promptWasIssued = TryResolveIssuedDyadPromptLocked(proposal, out var promptReason);
            var (decision, reason) = promptWasIssued
                ? ResolveDyadLanguageCandidateDecision(grounding)
                : (DyadLanguageCandidateDecision.Deferred, promptReason);
            var reviewedAtUtc = DateTimeOffset.UtcNow;
            var reviewSequence = _dyadLanguageCandidateReviews.Count == 0
                ? 1L
                : _dyadLanguageCandidateReviews.Last().ReviewSequence + 1L;
            var response = new DyadLanguageCandidateResponse(
                DyadLanguageContract.ProtocolVersion,
                proposal.SessionId,
                proposal.TurnId,
                decision,
                reason,
                grounding,
                reviewSequence,
                reviewedAtUtc);

            _dyadLanguageCandidateReviews.Enqueue(new DyadLanguageCandidateAuditRecord(
                reviewSequence,
                reviewedAtUtc,
                proposal,
                decision,
                reason,
                grounding));
            while (_dyadLanguageCandidateReviews.Count > MaxDyadLanguageCandidateReviews)
            {
                _dyadLanguageCandidateReviews.Dequeue();
            }

            return response;
        }
    }

    public DyadEntityPromptSnapshot CreateDyadEntityPrompt(DyadEntityGenerationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        lock (_gate)
        {
            var grounding = BuildDyadLanguageGroundingSnapshotLocked();
            var prompt = grounding.NeuronalCircuitObserved
                ? BuildNeuronalDyadPrompt(parameters, grounding)
                : BuildAwaitingNeuronalDyadPrompt(parameters, grounding);
            if (prompt.Length > DyadLanguageContract.MaxPromptLength)
            {
                prompt = prompt[..DyadLanguageContract.MaxPromptLength];
            }

            var fingerprint = DyadLanguageContract.CreatePromptFingerprint(prompt);
            RecordIssuedDyadPromptLocked(parameters, fingerprint, grounding.Tick);
            return new DyadEntityPromptSnapshot(
                prompt,
                fingerprint,
                string.Empty,
                grounding);
        }
    }

    private static string BuildNeuronalDyadPrompt(
        DyadEntityGenerationParameters parameters,
        DyadLanguageGroundingSnapshot grounding)
        => string.Join('\n',
        [
            "You are Entity, the language component of Dyad.",
            "Produce one short language candidate for DNNE review. The numeric neuronal reports below are bounded internal evidence, not proof of external events.",
            "Do not prescribe motor actions, reward changes, memory writes, or unobserved world facts. State uncertainty plainly.",
            $"Requested candidate kind: {parameters.CandidateKind}.",
            $"Requested purpose: {parameters.Purpose}.",
            $"Verified DNNE tick: {grounding.Tick}; authority={grounding.Authority}.",
            $"Grounding: available={grounding.NeuronalGroundingAvailable}; grounded={grounding.NeuronalGrounded}; confidence={grounding.GroundingConfidence:0.00}; uncertainty={grounding.Uncertainty:0.00}.",
            $"Numeric populations: percept={grounding.PerceptEnsemble}; recall={grounding.MemoryEnsemble}; attention={grounding.AttentionChannel}; language-circuit-coverage={grounding.LanguageCircuitCoverage:0.00}.",
            $"Post-percept annotation: {grounding.GroundedLabel}. It may describe an existing percept but may not create or select one.",
            $"Speech authorization: {grounding.NeuronalSpeechAuthorized}; sleeping={grounding.IsSleeping}.",
            "Neuronal source provenance:",
            ..grounding.NeuronalSources.Select(source =>
                $"- {source.SourceId}: population={source.PopulationIndex}; confidence={source.Confidence:0.00}; tick={source.Tick}; evidence={source.Evidence}"),
            "Grounded neuronal excerpts:",
            ..grounding.MemoryExcerpts.Select(excerpt =>
                $"- {excerpt.MemorySystem}: {excerpt.Summary} (confidence={excerpt.Confidence:0.00}; tick={excerpt.LastUpdatedTick}; evidence={excerpt.Evidence})")
        ]);

    private static string BuildAwaitingNeuronalDyadPrompt(
        DyadEntityGenerationParameters parameters,
        DyadLanguageGroundingSnapshot grounding)
        => string.Join('\n',
        [
            "You are Entity, the language component of Dyad.",
            "DNNE has not observed a neuronal language-grounding circuit for this turn.",
            "Any candidate will be held. Do not infer a reference from legacy symbolic telemetry.",
            "Do not prescribe motor actions, reward changes, memory writes, or unobserved world facts.",
            $"Requested candidate kind: {parameters.CandidateKind}.",
            $"Requested purpose: {parameters.Purpose}.",
            $"Verified DNNE tick: {grounding.Tick}.",
            "Authority: none until grounded neuronal evidence is available."
        ]);

    private void RecordIssuedDyadPromptLocked(
        DyadEntityGenerationParameters parameters,
        string fingerprint,
        long groundingTick)
    {
        var now = DateTimeOffset.UtcNow;
        RemoveExpiredIssuedDyadPromptsLocked(now);
        _issuedDyadPrompts[CreateDyadTurnKey(parameters.SessionId, parameters.TurnId)] = new IssuedDyadPrompt(
            fingerprint,
            groundingTick,
            now);
        while (_issuedDyadPrompts.Count > MaxIssuedDyadPrompts)
        {
            var oldest = _issuedDyadPrompts.MinBy(static pair => pair.Value.IssuedAtUtc);
            _issuedDyadPrompts.Remove(oldest.Key);
        }
    }

    private bool TryResolveIssuedDyadPromptLocked(
        DyadLanguageCandidateProposal proposal,
        out string reason)
    {
        var now = DateTimeOffset.UtcNow;
        RemoveExpiredIssuedDyadPromptsLocked(now);
        if (!_issuedDyadPrompts.TryGetValue(
                CreateDyadTurnKey(proposal.SessionId, proposal.TurnId),
                out var issued))
        {
            reason = "DNNE did not issue a prompt for this session and turn; the candidate cannot be grounded or emitted.";
            return false;
        }

        if (!string.Equals(issued.PromptFingerprint, proposal.PromptFingerprint, StringComparison.Ordinal))
        {
            reason = "The candidate does not match DNNE's issued prompt fingerprint for this session and turn.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void RemoveExpiredIssuedDyadPromptsLocked(DateTimeOffset now)
    {
        var expired = _issuedDyadPrompts
            .Where(pair => now - pair.Value.IssuedAtUtc > IssuedDyadPromptLifetime)
            .Select(static pair => pair.Key)
            .ToArray();
        foreach (var key in expired)
        {
            _issuedDyadPrompts.Remove(key);
        }
    }

    private static string CreateDyadTurnKey(string sessionId, string turnId)
        => sessionId + "\u001f" + turnId;

    public IReadOnlyList<DyadLanguageCandidateAuditRecord> GetDyadLanguageCandidateReviews(int limit)
    {
        var boundedLimit = Math.Clamp(limit, 1, MaxDyadLanguageCandidateReviews);
        lock (_gate)
        {
            return _dyadLanguageCandidateReviews
                .TakeLast(boundedLimit)
                .Reverse()
                .ToArray();
        }
    }

    private static (DyadLanguageCandidateDecision Decision, string Reason) ResolveDyadLanguageCandidateDecision(
        DyadLanguageGroundingSnapshot grounding)
    {
        if (!grounding.NeuronalCircuitObserved)
        {
            return (DyadLanguageCandidateDecision.Deferred, "DNNE has no observed neuronal language circuit; legacy symbolic telemetry cannot authorize language emission.");
        }

        if (!grounding.NeuronalGroundingAvailable)
        {
            return (DyadLanguageCandidateDecision.Deferred, "The observed neuronal language circuit is incomplete; symbolic telemetry cannot replace missing circuit evidence.");
        }

        if (grounding.IsSleeping)
        {
            return (DyadLanguageCandidateDecision.Deferred, "DNNE's neuronal sleep circuit is active; the candidate remains available for later review.");
        }

        if (!grounding.NeuronalGrounded || grounding.GroundingConfidence < 0.20f || grounding.Uncertainty > 0.70f)
        {
            return (DyadLanguageCandidateDecision.Deferred, "DNNE does not have a sufficiently certain neuronal reference for this candidate.");
        }

        if (grounding.AttentionChannel != NeuronalLanguageGroundingDecoder.LanguageAttentionChannel)
        {
            return (DyadLanguageCandidateDecision.Deferred, "DNNE's neuronal attention workspace has not selected the language population.");
        }

        if (!grounding.NeuronalSpeechAuthorized)
        {
            return (DyadLanguageCandidateDecision.Deferred, "DNNE's distributed neuronal speech circuit has not authorized emission.");
        }

        return (
            DyadLanguageCandidateDecision.AcceptedForEmission,
            "DNNE's grounded neuronal reference, attention broadcast, and distributed speech circuit authorize this text-only emission.");
    }

    private DyadLanguageGroundingSnapshot BuildDyadLanguageGroundingSnapshotLocked()
    {
        return BuildNeuronalDyadLanguageGroundingSnapshotLocked(NeuronalLanguageGrounding);
    }

    private DyadLanguageGroundingSnapshot BuildNeuronalDyadLanguageGroundingSnapshotLocked(
        NeuronalLanguageGroundingDecision grounding)
    {
        var attentionSelected = grounding.AttentionChannel == NeuronalLanguageGroundingDecoder.LanguageAttentionChannel;
        var communicationIntent = new DyadCommunicationIntentSnapshot(
            grounding.Grounded,
            grounding.SpeechAuthorized ? "candidate-emission" : "candidate-review",
            "neuronal-state-unlabelled",
            grounding.GroundedLabel == "unlabelled"
                ? $"population-{Math.Max(grounding.PerceptEnsemble, grounding.MemoryEnsemble)}"
                : grounding.GroundedLabel,
            (float)grounding.GroundingConfidence,
            $"{NeuronalLanguageGroundingDecision.Authority}; attention-population={grounding.AttentionChannel}");
        return new DyadLanguageGroundingSnapshot(
            Tick,
            grounding.IsSleeping,
            grounding.Grounded,
            (float)grounding.GroundingConfidence,
            (float)grounding.MemoryConfidence,
            "unavailable-under-neuronal-authority",
            grounding.GroundedLabel == "unlabelled"
                ? $"population-{Math.Max(grounding.PerceptEnsemble, grounding.MemoryEnsemble)}"
                : grounding.GroundedLabel,
            "unavailable-under-neuronal-authority",
            "unavailable-under-neuronal-authority",
            (float)grounding.LanguageAttention,
            (float)grounding.AttentionConfidence,
            grounding.SpeechAuthorized ? "speakable" : "deferred",
            grounding.SpeechAuthorized,
            (float)grounding.GroundingConfidence,
            (float)grounding.ExpressionDrive,
            (float)Math.Clamp(1.0 - grounding.ExpressionDrive, 0.0, 1.0),
            $"{NeuronalLanguageGroundingDecision.Authority}; grounded={grounding.Grounded}; attention-selected={attentionSelected}; uncertainty={grounding.Uncertainty:0.000}",
            BuildDyadNeuronalMemoryExcerptsLocked(grounding),
            communicationIntent)
        {
            Authority = NeuronalLanguageGroundingDecision.Authority,
            NeuronalCircuitObserved = grounding.CircuitObserved,
            NeuronalGroundingAvailable = grounding.Available,
            NeuronalGrounded = grounding.Grounded,
            PerceptEnsemble = grounding.PerceptEnsemble,
            MemoryEnsemble = grounding.MemoryEnsemble,
            AttentionChannel = grounding.AttentionChannel,
            LanguageCircuitCoverage = (float)grounding.LanguageCircuitCoverage,
            GroundingConfidence = (float)grounding.GroundingConfidence,
            Uncertainty = (float)grounding.Uncertainty,
            NeuronalSpeechAuthorized = grounding.SpeechAuthorized,
            GroundedLabel = grounding.GroundedLabel,
            NeuronalSources = grounding.Sources
        };
    }

    private IReadOnlyList<DyadVerifiedMemoryExcerpt> BuildDyadNeuronalMemoryExcerptsLocked(
        NeuronalLanguageGroundingDecision grounding)
    {
        var excerpts = new List<DyadVerifiedMemoryExcerpt>(2);
        if (grounding.PerceptEnsemble >= 0)
        {
            excerpts.Add(new DyadVerifiedMemoryExcerpt(
                "neuronal-percept-reference",
                grounding.GroundedLabel == "unlabelled"
                    ? $"Bound percept population {grounding.PerceptEnsemble}."
                    : $"Bound percept population {grounding.PerceptEnsemble}; post-percept annotation={grounding.GroundedLabel}.",
                (float)grounding.PerceptConfidence,
                Tick,
                NeuronalPerceptDecisionAuthority));
        }

        if (grounding.MemoryEnsemble >= 0)
        {
            excerpts.Add(new DyadVerifiedMemoryExcerpt(
                "persisted-synaptic-recall",
                $"Recalled numeric population {grounding.MemoryEnsemble}.",
                (float)grounding.MemoryConfidence,
                Tick,
                NeuronalMemoryDecision.Authority));
        }

        return excerpts;
    }

    private const string NeuronalPerceptDecisionAuthority = "DistributedPerceptEnsembleCompetition";


    public NeuronalMotorRuntime GetNeuronalMotorSnapshot()
    {
        lock (_gate)
        {
            return NeuronalMotor;
        }
    }

    public void UpdateNeuronalMotor(NeuronalMotorRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        lock (_gate)
        {
            NeuronalMotor = runtime;
        }
    }

    public void UpdateNeuronalLanguageGrounding(NeuronalLanguageGroundingDecision grounding)
    {
        ArgumentNullException.ThrowIfNull(grounding);
        lock (_gate)
        {
            NeuronalLanguageGrounding = grounding;
        }
    }

    public NeuronalLanguageGroundingDecision GetNeuronalLanguageGroundingSnapshot()
    {
        lock (_gate)
        {
            return NeuronalLanguageGrounding;
        }
    }

    public void UpdateNeuronalCognitionTelemetry(
        NeuronalPerceptDecision perception,
        NeuronalMemoryDecision memory,
        NeuronalAttentionWorkspaceDecision attentionWorkspace,
        NeuronalSleepConsolidationDecision sleepConsolidation,
        NeuronalAffectValuationDecision affectValuation,
        NeuronalExecutiveDecision executive)
    {
        lock (_gate)
        {
            NeuronalPerception = perception;
            NeuronalMemory = memory;
            NeuronalAttentionWorkspace = attentionWorkspace;
            NeuronalSleepConsolidation = sleepConsolidation;
            NeuronalAffectValuation = affectValuation;
            NeuronalExecutive = executive;
        }
    }

    private void InvalidateCompositeSnapshotCachesLocked()
    {
        _cachedCircuitAuditTick = -1;
        _cachedCircuitAuditWarningLimit = 0;
        _cachedCircuitAuditSnapshot = null;
        _cachedConsolidationTelemetryTick = -1;
        _cachedConsolidationTelemetrySnapshot = null;
        _cachedBrainBehaviorTick = -1;
        _cachedBrainBehaviorSnapshot = null;
        _cachedProsodyTelemetryTick = -1;
        _cachedProsodyTelemetrySnapshot = null;
    }



    public object GetCircuitHealthPanelSnapshot(int maxWarnings)
    {
        var clamped = Math.Clamp(maxWarnings, 8, 512);
        return GetCachedCircuitAuditSnapshot(clamped);
    }

    public object GetMetabolicPhysiologySnapshot()
    {
        lock (_gate)
        {
            return new
            {
                MetabolicPhysiology.NeuronalSleepObserved,
                MetabolicPhysiology.AtpBudget,
                MetabolicPhysiology.MaxAtpBudget,
                MetabolicPhysiology.HomeostaticPressure,
                MetabolicPhysiology.MaxHomeostaticPressure,
                MetabolicPhysiology.SleepTicks,
                MetabolicPhysiology.WakeTicks,
                MetabolicPhysiology.SleepEpisodes,
                MetabolicPhysiology.LastTransitionTick,
                Role = "ReadOnlyPhysiologicalTransducer",
                CanAuthorizeSleepState = false,
                CanGateNeuralTraffic = false
            };
        }
    }

    public InputGateRuntime GetInputGatesSnapshot()
    {
        lock (_gate)
        {
            return InputGates;
        }
    }

    public bool IsAvatarVisionEnabled()
    {
        lock (_gate)
        {
            return InputGates.AvatarVisionEnabled;
        }
    }

    public bool IsSpontaneousSpikingEnabled()
    {
        lock (_gate)
        {
            return InputGates.SpontaneousSpikingEnabled;
        }
    }

    public void SetInputGates(InputGateRuntime runtime)
    {
        lock (_gate)
        {
            InputGates = InputGateRuntime.Normalize(runtime);
        }
    }

    public bool EnsureSpontaneousSpikingEnabled(string reason)
    {
        lock (_gate)
        {
            if (InputGates.SpontaneousSpikingEnabled)
            {
                return false;
            }

            InputGates = InputGates with { SpontaneousSpikingEnabled = true };
            AppendLog(_outputLog, $"Input gates auto-restored: spontaneousSpiking=true ({reason}).");
            return true;
        }
    }

    public bool TrySetInputGates(InputGateControlRequest request, out InputGateRuntime runtime, out string? error)
    {
        lock (_gate)
        {
            if (request is null || (request.AvatarVisionEnabled is null && request.SpontaneousSpikingEnabled is null))
            {
                runtime = InputGates;
                error = "At least one setting is required: AvatarVisionEnabled or SpontaneousSpikingEnabled.";
                return false;
            }

            var next = InputGates with
            {
                AvatarVisionEnabled = request.AvatarVisionEnabled ?? InputGates.AvatarVisionEnabled,
                SpontaneousSpikingEnabled = request.SpontaneousSpikingEnabled ?? InputGates.SpontaneousSpikingEnabled
            };
            InputGates = InputGateRuntime.Normalize(next);
            runtime = InputGates;
            error = null;
            return true;
        }
    }

    public MetabolicPhysiologyRuntime GetMetabolicPhysiologyRuntime()
    {
        lock (_gate)
        {
            return MetabolicPhysiology;
        }
    }

    public void UpdateNeuronalVisualAttention(NeuronalVisualAttentionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        lock (_gate)
        {
            VisualAttention = decision;
        }
    }


    public object GetProsodyTelemetrySnapshot()
    {
        lock (_gate)
        {
            return GetCachedProsodyTelemetrySnapshotLocked();
        }
    }

    private object GetCachedProsodyTelemetrySnapshotLocked()
    {
        if (_cachedProsodyTelemetrySnapshot is not null && _cachedProsodyTelemetryTick == Tick)
        {
            return _cachedProsodyTelemetrySnapshot;
        }

        _cachedProsodyTelemetrySnapshot = BuildProsodyTelemetrySnapshotLocked();
        _cachedProsodyTelemetryTick = Tick;
        return _cachedProsodyTelemetrySnapshot;
    }

    private object BuildProsodyTelemetrySnapshotLocked()
    {
        var prosodyModeStates = TransportStats.LanguageBackoffModeStates
            .Where(state => string.Equals(state.Mode, "prosody", StringComparison.OrdinalIgnoreCase))
            .Select(state => new
            {
                state.Mode,
                state.CurrentGraphId,
                state.LastSwitchTick,
                state.LastEvaluationTick,
                state.LastResolutionTick
            })
            .ToList();

        var prosodyGraphs = TransportStats.LanguageBackoffGraphs
            .Where(graph => string.Equals(graph.Mode, "prosody", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(graph => graph.CompositeScore)
            .ThenByDescending(graph => graph.DeliveredSpikes)
            .Take(16)
            .Select(graph => new
            {
                graph.GraphId,
                graph.Mode,
                graph.Description,
                graph.IsCurrent,
                graph.Attempts,
                graph.Resolved,
                graph.DispatchSuccess,
                graph.DispatchErrors,
                graph.DeadPaths,
                graph.DeliveredSpikes,
                graph.ScoreEwma,
                graph.CompositeScore,
                graph.LastTick,
                graph.LastError
            })
            .ToList();

        var prosodyEdges = TransportStats.LanguageBackoffTopEdges
            .Where(edge => string.Equals(edge.Mode, "prosody", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(edge => edge.DispatchSuccess)
            .ThenByDescending(edge => edge.DeliveredSpikes)
            .Take(24)
            .Select(edge => new
            {
                edge.Key,
                edge.Mode,
                edge.GraphId,
                edge.Source,
                edge.Target,
                edge.IsFallback,
                edge.Rank,
                edge.Strategy,
                edge.Attempts,
                edge.Resolved,
                edge.Unavailable,
                edge.DispatchSuccess,
                edge.DispatchErrors,
                edge.DeliveredSpikes,
                edge.LastError
            })
            .ToList();

        return new
        {
            Tick,
            SimulationClockMs,
            Sleep = new
            {
                Authority = NeuronalSleepConsolidationDecision.Authority,
                NeuronalSleepConsolidation.Available,
                NeuronalSleepConsolidation.StateActive,
                NeuronalSleepConsolidation.State,
                NeuronalSleepConsolidation.ReplayActive,
                NeuronalSleepConsolidation.ReplayEnsemble,
                MetabolicPhysiology.NeuronalSleepObserved,
                MetabolicPhysiology.HomeostaticPressure,
                MetabolicPhysiology.AtpBudget,
                MetabolicPhysiology.SleepTicks,
                MetabolicPhysiology.WakeTicks
            },
            NeuronalAffect = new
            {
                Authority = NeuronalAffectValuationDecision.Authority,
                NeuronalAffectValuation.Available,
                NeuronalAffectValuation.Active,
                NeuronalAffectValuation.DominantChannel,
                NeuronalAffectValuation.PositiveValence,
                NeuronalAffectValuation.NegativeValence,
                NeuronalAffectValuation.Arousal,
                NeuronalAffectValuation.Confidence
            },
            LanguageBridge = new
            {
                TransportStats.PerceptionLanguageGenerated,
                TransportStats.PerceptionLanguageDelivered,
                TransportStats.PerceptionLanguageDispatchErrors,
                TransportStats.PerceptionLanguageLastError
            },
            Backoff = new
            {
                TransportStats.LanguageBackoffAttempts,
                TransportStats.LanguageBackoffResolved,
                TransportStats.LanguageBackoffFallbackSelections,
                TransportStats.LanguageBackoffDispatchErrors,
                ModeStates = prosodyModeStates,
                Graphs = prosodyGraphs,
                Edges = prosodyEdges
            }
        };
    }



    public object GetCurriculumSnapshot()
    {
        lock (_gate)
        {
            return Curriculum;
        }
    }

    public bool TrySetCurriculumControl(CurriculumControlRequest request, out CurriculumRuntime runtime, out string? error)
    {
        lock (_gate)
        {
            error = null;
            var enabled = request.Enabled ?? Curriculum.Enabled;
            if (request.StageIndex is not null)
            {
                var requested = request.StageIndex.Value;
                if (requested < 0 || requested >= CurriculumTaskAccumulator.StageNames.Count)
                {
                    runtime = Curriculum;
                    error = $"StageIndex must be between 0 and {CurriculumTaskAccumulator.StageNames.Count - 1}.";
                    return false;
                }

                _curriculumStageIndex = requested;
                _curriculumLastStageTransitionTick = Tick;
            }

            if (request.ResetProgress.GetValueOrDefault(false))
            {
                foreach (var task in _curriculumTasks)
                {
                    task.Reset();
                }
                _curriculumStageIndex = 0;
                _curriculumLastStageTransitionTick = Tick;
            }

            Curriculum = Curriculum with
            {
                Enabled = enabled
            };
            RefreshCurriculumSnapshotLocked(Tick);
            runtime = Curriculum;
            return true;
        }
    }




    private static bool ContainsAny(string value, string first, string second)
        => value.Contains(first, StringComparison.OrdinalIgnoreCase) ||
           value.Contains(second, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, string first, string second, string third)
        => value.Contains(first, StringComparison.OrdinalIgnoreCase) ||
           value.Contains(second, StringComparison.OrdinalIgnoreCase) ||
           value.Contains(third, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, string first, string second, string third, string fourth)
        => value.Contains(first, StringComparison.OrdinalIgnoreCase) ||
           value.Contains(second, StringComparison.OrdinalIgnoreCase) ||
           value.Contains(third, StringComparison.OrdinalIgnoreCase) ||
           value.Contains(fourth, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, string first, string second, string third, string fourth, string fifth)
        => ContainsAny(value, first, second, third, fourth) ||
           value.Contains(fifth, StringComparison.OrdinalIgnoreCase);



    private float GetRecentStructureSpikeSupportLocked(long tick, params StructureId[] structures)
    {
        if (_dispatchSpikeTrace.Count == 0 || structures.Length == 0)
        {
            return 0f;
        }

        var windowStart = Math.Max(0, tick - 16);
        var count = 0;
        foreach (var trace in _dispatchSpikeTrace)
        {
            if (trace.Tick < windowStart)
            {
                continue;
            }

            for (var i = 0; i < structures.Length; i++)
            {
                var structure = structures[i];
                if (trace.SourceStructure == structure || trace.TargetStructure == structure)
                {
                    count++;
                    break;
                }
            }
        }

        return Clamp01(count / 36f);
    }



    internal static string ApplyMotorRecoveryDirective(
        string currentDirective,
        string monitorState,
        float blocked,
        long recoverySequence,
        bool bodyFeedbackFresh = true)
    {
        var directive = string.IsNullOrWhiteSpace(currentDirective)
            ? "motor_idle"
            : currentDirective.Trim();
        if (!bodyFeedbackFresh ||
            !string.Equals(monitorState, "stalled", StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(directive, "rest", "stop", "guard", "immobilize", "idle") ||
            ContainsAny(directive, "turn", "reorient", "about_face", "avoid", "escape"))
        {
            return directive;
        }

        var turnLeft = (recoverySequence & 1L) == 0;
        if (blocked > 0.34f)
        {
            return turnLeft ? "motor_about_face_left" : "motor_about_face_right";
        }

        return turnLeft ? "motor_turn_left" : "motor_turn_right";
    }

    internal static float ResolveMetabolicRateScale(double tickDurationMs)
        => (float)Math.Clamp(
            tickDurationMs / MetabolicReferenceIntervalMs,
            MinMetabolicRateScale,
            4.0);










    private void RefreshCurriculumSnapshotLocked(long tick)
    {
        var stageIndex = Math.Clamp(_curriculumStageIndex, 0, CurriculumTaskAccumulator.StageNames.Count - 1);
        var stageName = CurriculumTaskAccumulator.StageNames[stageIndex];
        var taskSnapshots = _curriculumTasks
            .Select(t => new CurriculumTaskRuntime(
                Name: t.Name,
                StageIndex: t.StageIndex,
                Score: t.ScoreEma,
                Samples: t.SampleCount,
                Successes: t.SuccessCount,
                SuccessRate: t.SuccessRate,
                LastTick: t.LastTick))
            .ToList();
        var stageTasks = _curriculumTasks.Where(t => t.StageIndex == stageIndex).ToList();
        var stageScore = stageTasks.Count == 0 ? 0f : stageTasks.Average(t => t.ScoreEma);
        var stageProgress = stageTasks.Count == 0
            ? 0f
            : stageTasks.Average(t => Math.Clamp((float)t.SampleCount / 160f, 0f, 1f));
        var stageTicks = Math.Max(0, tick - _curriculumLastStageTransitionTick);

        Curriculum = Curriculum with
        {
            StageIndex = stageIndex,
            StageName = stageName,
            LastTransitionTick = _curriculumLastStageTransitionTick,
            StageScore = stageScore,
            StageProgress = stageProgress,
            StageTicks = stageTicks,
            Tasks = taskSnapshots
        };
    }


    private void RestoreCurriculumFromSnapshot(CurriculumRuntime runtime)
    {
        var stageIndex = Math.Clamp(runtime.StageIndex, 0, CurriculumTaskAccumulator.StageNames.Count - 1);
        _curriculumStageIndex = stageIndex;
        _curriculumLastStageTransitionTick = Math.Max(0, runtime.LastTransitionTick);

        foreach (var task in _curriculumTasks)
        {
            task.Reset();
            var snapshot = runtime.Tasks.FirstOrDefault(t =>
                string.Equals(t.Name, task.Name, StringComparison.OrdinalIgnoreCase) &&
                t.StageIndex == task.StageIndex);
            if (snapshot is null)
            {
                continue;
            }

            task.Restore(
                scoreEma: (float)Math.Clamp(snapshot.Score, 0.0, 1.0),
                sampleCount: Math.Max(0, snapshot.Samples),
                successCount: Math.Max(0, snapshot.Successes),
                lastTick: Math.Max(0, snapshot.LastTick));
        }

        Curriculum = runtime with
        {
            Enabled = runtime.Enabled,
            StageIndex = stageIndex,
            StageName = CurriculumTaskAccumulator.StageNames[stageIndex],
            LastTransitionTick = _curriculumLastStageTransitionTick,
            Tasks = _curriculumTasks
                .Select(t => new CurriculumTaskRuntime(
                    t.Name,
                    t.StageIndex,
                    t.ScoreEma,
                    t.SampleCount,
                    t.SuccessCount,
                    t.SuccessRate,
                    t.LastTick))
                .ToList()
        };
    }

    public MetabolicTransitionResult AdvanceMetabolicPhysiology(
        MetabolicTickInput input,
        NeuronalSleepConsolidationDecision neuronalDecision)
    {
        lock (_gate)
        {
            ArgumentNullException.ThrowIfNull(neuronalDecision);

            var runtime = MetabolicPhysiology;
            var rateScale = Math.Clamp(input.HomeostasisRateScale, MinMetabolicRateScale, 4.0f);
            var neuronalSleepObserved =
                neuronalDecision.Available &&
                neuronalDecision.StateActive &&
                neuronalDecision.State != NeuronalSleepState.Wake;
            var enteredSleep = neuronalSleepObserved && !runtime.NeuronalSleepObserved;
            var exitedSleep = !neuronalSleepObserved && runtime.NeuronalSleepObserved;
            var atp = runtime.AtpBudget;
            var pressure = runtime.HomeostaticPressure;
            var sleepTicks = neuronalSleepObserved ? runtime.SleepTicks + 1 : 0;
            var wakeTicks = neuronalSleepObserved ? 0 : runtime.WakeTicks + 1;

            if (neuronalSleepObserved)
            {
                atp += runtime.SleepRecoveryPerTick * rateScale;
                pressure -= runtime.SleepPressureRecoveryPerTick * rateScale;
            }
            else
            {
                var drain =
                    runtime.AwakeBaseDrain +
                    (input.GeneratedSpikes * runtime.GeneratedSpikeDrain) +
                    (input.DrainedSpikes * runtime.InboundDrainPerSpike) +
                    (input.ActivePathways * runtime.ActivePathwayDrain) +
                    (input.SpontaneousGenerated * runtime.SpontaneousDrainPerEvent);
                var pressureRise =
                    runtime.WakePressureBasePerTick +
                    (input.GeneratedSpikes * runtime.WakePressurePerGeneratedSpike) +
                    (input.DrainedSpikes * runtime.WakePressurePerInboundSpike) +
                    (input.ActivePathways * runtime.WakePressurePerActivePathway) +
                    (input.SpontaneousGenerated * runtime.WakePressurePerSpontaneousEvent);

                atp -= Math.Max(0f, drain) * rateScale;
                pressure += Math.Max(0f, pressureRise) * rateScale;
            }

            runtime = runtime with
            {
                NeuronalSleepObserved = neuronalSleepObserved,
                AtpBudget = Math.Clamp(atp, 0f, runtime.MaxAtpBudget),
                HomeostaticPressure = Math.Clamp(pressure, 0f, runtime.MaxHomeostaticPressure),
                SleepTicks = sleepTicks,
                WakeTicks = wakeTicks,
                SleepEpisodes = runtime.SleepEpisodes + (enteredSleep ? 1 : 0),
                LastTransitionTick = enteredSleep || exitedSleep ? Tick : runtime.LastTransitionTick
            };
            MetabolicPhysiology = runtime;

            return new MetabolicTransitionResult(
                runtime.NeuronalSleepObserved,
                enteredSleep,
                exitedSleep,
                runtime.AtpBudget,
                runtime.SleepTicks);
        }
    }

    public BodyStateRuntime UpdateBodyState(
        float forwardVelocity,
        float turnRateDeg,
        float contactLevel,
        float leftMotorDrive,
        float rightMotorDrive)
        => UpdateBodyState(
            forwardVelocity,
            turnRateDeg,
            contactLevel,
            tactileFront: contactLevel,
            tactileLeft: 0f,
            tactileRight: 0f,
            tactileGround: 0f,
            painLevel: contactLevel * 0.35f,
            hunger: 0f,
            health: 1f,
            leftMotorDrive,
            rightMotorDrive);

    public BodyStateRuntime UpdateBodyState(
        float forwardVelocity,
        float turnRateDeg,
        float contactLevel,
        float tactileFront,
        float tactileLeft,
        float tactileRight,
        float tactileGround,
        float painLevel,
        float hunger,
        float health,
        float leftMotorDrive,
        float rightMotorDrive)
    {
        lock (_gate)
        {
            var left = Math.Max(0f, leftMotorDrive);
            var right = Math.Max(0f, rightMotorDrive);
            var asymmetry = (left + right) > 0.01f
                ? Math.Clamp(Math.Abs(left - right) / (left + right), 0f, 1f)
                : 0f;
            BodyState = new BodyStateRuntime(
                ForwardVelocity: forwardVelocity,
                TurnRateDeg: turnRateDeg,
                ContactLevel: Math.Clamp(contactLevel, 0f, 1f),
                TactileFront: Math.Clamp(tactileFront, 0f, 1f),
                TactileLeft: Math.Clamp(tactileLeft, 0f, 1f),
                TactileRight: Math.Clamp(tactileRight, 0f, 1f),
                TactileGround: Math.Clamp(tactileGround, 0f, 1f),
                PainLevel: Math.Clamp(painLevel, 0f, 1f),
                Hunger: Math.Clamp(hunger, 0f, 1f),
                Health: Math.Clamp(health, 0f, 1f),
                LeftMotorDrive: left,
                RightMotorDrive: right,
                MotorAsymmetry: asymmetry,
                LastInputTick: Tick);
            return BodyState;
        }
    }

    public void UpdateServiceTelemetry(StructureId structureId, ServiceRuntimeTelemetry telemetry)
    {
        lock (_gate)
        {
            ServiceTelemetry[structureId] = telemetry;
        }
    }

    public void UpdateTransportStats(TransportRuntimeStats stats)
    {
        lock (_gate)
        {
            TransportStats = stats;
            TotalSpontaneousGenerated += stats.SpontaneousGenerated;
            TotalSpontaneousDelivered += stats.SpontaneousDelivered;
            TotalSpontaneousDispatchErrors += stats.SpontaneousDispatchErrors;
        }
    }

    public void UpdatePerformanceProfile(string profileName)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                PerformanceProfileName = profileName.Trim();
            }

            MetabolicPhysiology = NormalizeMetabolicPhysiology(MetabolicPhysiology);
        }
    }

    public void MarkSnapshot(BrainSnapshot snapshot)
    {
        lock (_gate)
        {
            LastSnapshotTick = snapshot.Tick;
            LastSnapshotSimulationMs = snapshot.TimestampMs;
            LastSnapshotWallClockUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public long GetRestartGeneration()
    {
        lock (_gate)
        {
            return _restartGeneration;
        }
    }

    public long RequestSimulationRestart()
    {
        lock (_gate)
        {
            _restartGeneration++;
            AppendLog(_outputLog, "Simulation restart requested.");
            return _restartGeneration;
        }
    }

    public void ResetForSimulationRestart()
    {
        lock (_gate)
        {
            SimulationClockMs = 0;
            Tick = 0;
            LastSnapshotTick = 0;
            LastSnapshotSimulationMs = 0;
            LastSnapshotWallClockUnixMs = 0;
            TotalSpontaneousGenerated = 0;
            TotalSpontaneousDelivered = 0;
            TotalSpontaneousDispatchErrors = 0;
            MetabolicPhysiology = MetabolicPhysiologyRuntime.Default;
            BodyState = BodyStateRuntime.Default;
            VisualAttention = NeuronalVisualAttentionDecision.Unavailable;
            Curriculum = CurriculumRuntime.Default;
            TransportStats = TransportRuntimeStats.Empty;
            ServiceTelemetry.Clear();
            _dispatchLifetimeOut.Clear();
            _dispatchLifetimeIn.Clear();
            _dispatchLastOutTick.Clear();
            _dispatchLastInTick.Clear();
            _dyadLanguageCandidateReviews.Clear();
            InvalidateCompositeSnapshotCachesLocked();
            _curriculumStageIndex = 0;
            _curriculumLastStageTransitionTick = Tick;
            foreach (var task in _curriculumTasks)
            {
                task.Reset();
            }
            RefreshCurriculumSnapshotLocked(Tick);
            var rhythms = OscillationPhases.Keys.ToList();
            foreach (var rhythm in rhythms)
            {
                OscillationPhases[rhythm] = 0;
            }
            _outputLog.Clear();
            _spikeLog.Clear();
            _dispatchSpikeTrace.Clear();
            AppendLog(_outputLog, $"Simulation reset (generation {_restartGeneration}).");
        }
    }

    public void AppendOutputLog(string message)
    {
        lock (_gate)
        {
            AppendLog(_outputLog, message);
        }
    }

    public void AppendSpikeLog(string message)
    {
        lock (_gate)
        {
            AppendLog(_spikeLog, message);
        }
    }

    public IReadOnlyList<RuntimeLogEntry> GetOutputLog()
    {
        lock (_gate)
        {
            return _outputLog.ToList();
        }
    }

    public IReadOnlyList<RuntimeLogEntry> GetSpikeLog()
    {
        lock (_gate)
        {
            return _spikeLog.ToList();
        }
    }

    public IReadOnlyList<RuntimeLogEntry> GetOutputLogSince(long wallClockUnixMsExclusive, int maxEntries = MaxRuntimeLogEntries)
    {
        lock (_gate)
        {
            return GetRuntimeLogTailSince(_outputLog, wallClockUnixMsExclusive, maxEntries);
        }
    }

    public IReadOnlyList<RuntimeLogEntry> GetSpikeLogSince(long wallClockUnixMsExclusive, int maxEntries = MaxRuntimeLogEntries)
    {
        lock (_gate)
        {
            return GetRuntimeLogTailSince(_spikeLog, wallClockUnixMsExclusive, maxEntries);
        }
    }

    public void RecordDispatchedSpikes(
        long tick,
        double timestampMs,
        string sourceHemisphere,
        string targetHemisphere,
        string targetInstanceKey,
        IReadOnlyList<SpikeMessage> spikes,
        int deliveredCount)
    {
        if (deliveredCount <= 0 || spikes.Count == 0)
        {
            return;
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var limit = Math.Min(deliveredCount, spikes.Count);
        if (limit <= 0)
        {
            return;
        }

        var hasConcreteNeuronIds = false;
        for (var i = 0; i < limit; i++)
        {
            var spike = spikes[i];
            if (spike is null)
            {
                continue;
            }

            var sourceNeuronId = NormalizeNeuronIdForHemisphere(spike.SourceNeuronId, sourceHemisphere);
            var targetNeuronId = NormalizeNeuronIdForHemisphere(spike.TargetNeuronId, targetHemisphere);
            if (string.IsNullOrWhiteSpace(sourceNeuronId) || string.IsNullOrWhiteSpace(targetNeuronId))
            {
                continue;
            }

            if (!sourceNeuronId.Contains("population-", StringComparison.OrdinalIgnoreCase) &&
                !targetNeuronId.Contains("population-", StringComparison.OrdinalIgnoreCase))
            {
                hasConcreteNeuronIds = true;
                break;
            }
        }

        var traces = new List<DispatchedSpikeTrace>(Math.Min(limit, hasConcreteNeuronIds ? limit : MaxPopulationDispatchTracePerBatch));
        var populationTracesRecorded = 0;
        for (var i = 0; i < limit; i++)
        {
            var spike = spikes[i];
            if (spike is null)
            {
                continue;
            }

            var sourceNeuronId = NormalizeNeuronIdForHemisphere(spike.SourceNeuronId, sourceHemisphere);
            var targetNeuronId = NormalizeNeuronIdForHemisphere(spike.TargetNeuronId, targetHemisphere);
            if (string.IsNullOrWhiteSpace(sourceNeuronId) || string.IsNullOrWhiteSpace(targetNeuronId))
            {
                continue;
            }

            var isPopulationTrace =
                sourceNeuronId.Contains("population-", StringComparison.OrdinalIgnoreCase) ||
                targetNeuronId.Contains("population-", StringComparison.OrdinalIgnoreCase);
            if (isPopulationTrace && hasConcreteNeuronIds)
            {
                continue;
            }

            if (isPopulationTrace && populationTracesRecorded >= MaxPopulationDispatchTracePerBatch)
            {
                continue;
            }

            if (isPopulationTrace)
            {
                populationTracesRecorded++;
            }

            traces.Add(new DispatchedSpikeTrace(
                tick,
                timestampMs,
                nowMs,
                spike.SourceStructure,
                sourceHemisphere,
                sourceNeuronId,
                spike.TargetStructure,
                targetHemisphere,
                targetNeuronId,
                spike.Neurotransmitter,
                targetInstanceKey));
        }

        if (traces.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            for (var i = 0; i < traces.Count; i++)
            {
                _dispatchSpikeTrace.Enqueue(traces[i]);
                RecordDispatchTraceStatsLocked(traces[i]);
            }

            while (_dispatchSpikeTrace.Count > MaxDispatchTraceEntries)
            {
                _dispatchSpikeTrace.Dequeue();
            }

            InvalidateCompositeSnapshotCachesLocked();
        }
    }

    public IReadOnlyList<DispatchedSpikeTrace> GetDispatchedSpikesSince(long wallClockUnixMsExclusive, int maxEntries = MaxDispatchTraceEntries)
    {
        lock (_gate)
        {
            return GetDispatchTraceTailSince(_dispatchSpikeTrace, wallClockUnixMsExclusive, maxEntries);
        }
    }

    public IReadOnlyList<DispatchedSpikeTrace> GetRecentDispatchedSpikes(int limit)
    {
        lock (_gate)
        {
            return GetDispatchTraceTailSince(_dispatchSpikeTrace, 0, limit);
        }
    }

    public IReadOnlyList<DispatchedSpikeTrace> GetRecentDispatchedSpikesForTick(long tick, int limit)
    {
        lock (_gate)
        {
            return GetDispatchTraceTailForTick(_dispatchSpikeTrace, tick, limit);
        }
    }

    private static IReadOnlyList<RuntimeLogEntry> GetRuntimeLogTailSince(
        Queue<RuntimeLogEntry> queue,
        long wallClockUnixMsExclusive,
        int maxEntries)
    {
        if (maxEntries <= 0 || queue.Count == 0)
        {
            return [];
        }

        var limit = Math.Min(maxEntries, queue.Count);
        var skip = queue.Count - limit;
        var result = new List<RuntimeLogEntry>(limit);
        var index = 0;
        foreach (var entry in queue)
        {
            if (index++ < skip)
            {
                continue;
            }

            if (wallClockUnixMsExclusive <= 0 || entry.WallClockUnixMs > wallClockUnixMsExclusive)
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static IReadOnlyList<DispatchedSpikeTrace> GetDispatchTraceTailSince(
        Queue<DispatchedSpikeTrace> queue,
        long wallClockUnixMsExclusive,
        int maxEntries)
    {
        if (maxEntries <= 0 || queue.Count == 0)
        {
            return [];
        }

        var limit = Math.Min(maxEntries, queue.Count);
        var skip = queue.Count - limit;
        var result = new List<DispatchedSpikeTrace>(limit);
        var index = 0;
        foreach (var trace in queue)
        {
            if (index++ < skip)
            {
                continue;
            }

            if (wallClockUnixMsExclusive <= 0 || trace.WallClockUnixMs > wallClockUnixMsExclusive)
            {
                result.Add(trace);
            }
        }

        return result;
    }

    private static IReadOnlyList<DispatchedSpikeTrace> GetDispatchTraceTailForTick(
        Queue<DispatchedSpikeTrace> queue,
        long tick,
        int maxEntries)
    {
        if (maxEntries <= 0 || queue.Count == 0)
        {
            return [];
        }

        var limit = Math.Min(maxEntries, queue.Count);
        var skip = queue.Count - limit;
        var result = new List<DispatchedSpikeTrace>(Math.Min(limit, 256));
        var index = 0;
        foreach (var trace in queue)
        {
            if (index++ < skip)
            {
                continue;
            }

            if (trace.Tick == tick)
            {
                result.Add(trace);
            }
        }

        return result;
    }

    private void AdvancePhase(BrainRhythm rhythm, double frequencyHz)
    {
        var phaseIncrement = 2.0 * Math.PI * frequencyHz * (TickDurationMs / 1000.0);
        var next = OscillationPhases[rhythm] + phaseIncrement;
        OscillationPhases[rhythm] = next % (2.0 * Math.PI);
    }

    private object BuildConsolidationTelemetrySnapshot()
    {
        lock (_gate)
        {
            return GetCachedConsolidationTelemetrySnapshotLocked();
        }
    }

    private void RecordDispatchTraceStatsLocked(DispatchedSpikeTrace trace)
    {
        _dispatchLifetimeOut[trace.SourceStructure] = _dispatchLifetimeOut.TryGetValue(trace.SourceStructure, out var outCount)
            ? outCount + 1
            : 1;
        _dispatchLifetimeIn[trace.TargetStructure] = _dispatchLifetimeIn.TryGetValue(trace.TargetStructure, out var inCount)
            ? inCount + 1
            : 1;
        UpdateLastTick(_dispatchLastOutTick, trace.SourceStructure, trace.Tick);
        UpdateLastTick(_dispatchLastInTick, trace.TargetStructure, trace.Tick);
    }

    private void RebuildDispatchTraceStatsLocked()
    {
        _dispatchLifetimeOut.Clear();
        _dispatchLifetimeIn.Clear();
        _dispatchLastOutTick.Clear();
        _dispatchLastInTick.Clear();
        foreach (var trace in _dispatchSpikeTrace)
        {
            RecordDispatchTraceStatsLocked(trace);
        }

        InvalidateCompositeSnapshotCachesLocked();
    }

    private object GetCachedConsolidationTelemetrySnapshotLocked()
    {
        if (_cachedConsolidationTelemetrySnapshot is not null && _cachedConsolidationTelemetryTick == Tick)
        {
            return _cachedConsolidationTelemetrySnapshot;
        }

        _cachedConsolidationTelemetrySnapshot = BuildConsolidationTelemetrySnapshotLocked();
        _cachedConsolidationTelemetryTick = Tick;
        return _cachedConsolidationTelemetrySnapshot;
    }

    private object BuildConsolidationTelemetrySnapshotLocked()
    {
        return new
        {
            Authority = NeuronalSleepConsolidationDecision.Authority,
            NeuronalSleepConsolidation.CircuitObserved,
            NeuronalSleepConsolidation.Available,
            NeuronalSleepConsolidation.StateActive,
            NeuronalSleepConsolidation.State,
            NeuronalSleepConsolidation.StateConfidence,
            NeuronalSleepConsolidation.ReplayActive,
            NeuronalSleepConsolidation.ReplayEnsemble,
            NeuronalSleepConsolidation.ReplayStrength,
            NeuronalSleepConsolidation.SpindleCoupling,
            NeuronalSleepConsolidation.SlowWaveCoupling,
            NeuronalSleepConsolidation.CorticalConsolidationGain
        };
    }



    private object BuildBrainBehaviorSnapshot()
    {
        lock (_gate)
        {
            var grounding = NeuronalLanguageGrounding;
            var groundedLabel = grounding.GroundedLabel == "unlabelled"
                ? string.Empty
                : grounding.GroundedLabel;
            var utterance = grounding.SpeechAuthorized && grounding.Grounded
                ? groundedLabel
                : string.Empty;

            return new
            {
                Tick,
                Authority = "MeasuredNeuronalDecoders",
                Sleep = new
                {
                    Authority = NeuronalSleepConsolidationDecision.Authority,
                    NeuronalSleepConsolidation.Available,
                    NeuronalSleepConsolidation.StateActive,
                    NeuronalSleepConsolidation.State,
                    NeuronalSleepConsolidation.StateConfidence,
                    NeuronalSleepConsolidation.ReplayActive,
                    MetabolicPhysiology.AtpBudget,
                    MetabolicPhysiology.HomeostaticPressure
                },
                Body = BodyState,
                Sensory = new
                {
                    ActiveSource = InferActiveSensorySourceLocked(),
                    InputGates.AvatarVisionEnabled,
                    InputGates.SpontaneousSpikingEnabled
                },
                VisualAttention,
                Language = new
                {
                    Utterance = utterance,
                    Sequence = grounding.SpeechAuthorized ? Tick : 0,
                    LastUpdatedTick = Tick,
                    Source = NeuronalLanguageGroundingDecision.Authority,
                    Grounding = grounding
                },
                Motor = NeuronalMotor
            };
        }
    }


    private string InferActiveSensorySourceLocked()
    {
        var windowStart = Math.Max(0, Tick - 250);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var trace in _dispatchSpikeTrace)
        {
            if (trace.Tick < windowStart)
            {
                continue;
            }

            AddSensorySourceCount(counts, trace.SourceStructure);
            AddSensorySourceCount(counts, trace.TargetStructure);
        }

        if (counts.Count == 0)
        {
            return "quiet";
        }

        var best = counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .First();
        return best.Value > 0 ? best.Key : "quiet";
    }

    private static void AddSensorySourceCount(Dictionary<string, int> counts, StructureId structure)
    {
        var source = structure switch
        {
            StructureId.Retina or StructureId.V1 or StructureId.V2 or StructureId.V4 or StructureId.Mt or StructureId.SuperiorColliculus => "vision",
            StructureId.Cochlea or StructureId.CochlearNucleus or StructureId.SuperiorOlive or StructureId.InferiorColliculus or StructureId.A1 => "hearing",
            StructureId.S1 or StructureId.VestibularNuclei or StructureId.NucleusTractusSolitarius or StructureId.MotorThalamus => "body",
            StructureId.BrocaBa44Ba45 or StructureId.WernickePstgPsts or StructureId.ArcuateFasciculus or StructureId.SupramarginalAngular => "language",
            StructureId.OlfactoryBulb => "olfaction",
            _ => string.Empty
        };

        if (source.Length == 0)
        {
            return;
        }

        counts[source] = counts.TryGetValue(source, out var current) ? current + 1 : 1;
    }

    private sealed record FunctionalCircuitSupportEntry(
        string FunctionKey,
        string DisplayName,
        bool Active,
        float Support,
        string Status,
        string[] RequiredStructures,
        string Evidence,
        string Warning);

    private IReadOnlyList<FunctionalCircuitSupportEntry> BuildFunctionalCircuitSupportSnapshotLocked()
    {
        var tick = Tick;
        var bodySchemaSupport = GetRecentStructureSpikeSupportLocked(
            tick,
            StructureId.S1,
            StructureId.Ppc,
            StructureId.Insula,
            StructureId.VestibularNuclei,
            StructureId.CerebellarVermis);
        var interoceptiveSupport = GetRecentStructureSpikeSupportLocked(
            tick,
            StructureId.NucleusTractusSolitarius,
            StructureId.Hypothalamus,
            StructureId.Insula,
            StructureId.Acc,
            StructureId.Amygdala);
        var affectSupport = GetRecentStructureSpikeSupportLocked(
            tick,
            StructureId.Amygdala,
            StructureId.Insula,
            StructureId.Acc,
            StructureId.Hypothalamus,
            StructureId.OrbitofrontalCortex,
            StructureId.NucleusAccumbens,
            StructureId.Vta,
            StructureId.Habenula,
            StructureId.LocusCoeruleus,
            StructureId.RapheNuclei);
        var cerebellarSupport = GetRecentStructureSpikeSupportLocked(
            tick,
            StructureId.CerebellarGranule,
            StructureId.CerebellarVermis,
            StructureId.CerebellarLobules,
            StructureId.PurkinjeCellLayer,
            StructureId.DeepCerebellarNuclei,
            StructureId.InferiorOlive,
            StructureId.VestibularNuclei,
            StructureId.MotorThalamus,
            StructureId.PremotorCortex,
            StructureId.M1);

        return
        [
            BuildFunctionalCircuitSupportEntry(
                "neuronal_action_selection",
                "Neuronal action selection",
                NeuronalMotor.ActionCircuitObserved,
                Clamp01((float)Math.Max(NeuronalMotor.ActionSelectionConfidence, NeuronalMotor.ActionCircuitCoverage)),
                "decoded basal-ganglia action populations with thalamocortical motor support",
                StructureId.Striatum,
                StructureId.GPe,
                StructureId.GPi,
                StructureId.Stn,
                StructureId.Snr,
                StructureId.NucleusAccumbens,
                StructureId.Vta,
                StructureId.Snc,
                StructureId.Pfc,
                StructureId.OrbitofrontalCortex,
                StructureId.MotorThalamus),
            BuildFunctionalCircuitSupportEntry(
                "neuronal_language_grounding",
                "Neuronal language grounding",
                NeuronalLanguageGrounding.CircuitObserved,
                Clamp01((float)Math.Max(NeuronalLanguageGrounding.GroundingConfidence, NeuronalLanguageGrounding.LanguageCircuitCoverage)),
                NeuronalLanguageGroundingDecision.Authority,
                StructureId.A1,
                StructureId.WernickePstgPsts,
                StructureId.ArcuateFasciculus,
                StructureId.BrocaBa44Ba45,
                StructureId.SupramarginalAngular,
                StructureId.TemporalAssociation,
                StructureId.Pfc,
                StructureId.Sma,
                StructureId.M1),

            BuildFunctionalCircuitSupportEntry(
                "body_schema",
                "Body schema",
                bodySchemaSupport > 0f,
                bodySchemaSupport,
                "S1/PPC/insula/vestibular/cerebellar body ownership loop",
                StructureId.S1,
                StructureId.Ppc,
                StructureId.Insula,
                StructureId.Acc,
                StructureId.NucleusTractusSolitarius,
                StructureId.VestibularNuclei,
                StructureId.CerebellarVermis,
                StructureId.CerebellarLobules,
                StructureId.DeepCerebellarNuclei,
                StructureId.PeriaqueductalGray),
            BuildFunctionalCircuitSupportEntry(
                "interoception",
                "Interoception and need state",
                interoceptiveSupport > 0f,
                interoceptiveSupport,
                "NTS/hypothalamus/insula/ACC/amygdala homeostatic loop",
                StructureId.NucleusTractusSolitarius,
                StructureId.Hypothalamus,
                StructureId.Insula,
                StructureId.Acc,
                StructureId.Amygdala,
                StructureId.Habenula,
                StructureId.NucleusAccumbens,
                StructureId.VentralPallidum,
                StructureId.Vta,
                StructureId.Snc),
            BuildFunctionalCircuitSupportEntry(
                "emotion",
                "Emotion and affect",
                NeuronalAffectValuation.Active,
                Math.Max(affectSupport, Clamp01((float)NeuronalAffectValuation.Confidence)),
                "amygdala/insula/ACC/hypothalamus/OFC neuromodulatory affect loop",
                StructureId.Amygdala,
                StructureId.Insula,
                StructureId.Acc,
                StructureId.Hypothalamus,
                StructureId.OrbitofrontalCortex,
                StructureId.Pfc,
                StructureId.NucleusAccumbens,
                StructureId.Vta,
                StructureId.Habenula,
                StructureId.LocusCoeruleus,
                StructureId.RapheNuclei),
            BuildFunctionalCircuitSupportEntry(
                "neuronal_motor",
                "Neuronal motor control",
                NeuronalMotor.Active,
                Clamp01((float)Math.Max(NeuronalMotor.ConfidenceEma, NeuronalMotor.MotorCircuitCoverage)),
                NeuronalMotor.Evidence,
                StructureId.Pfc,
                StructureId.PremotorCortex,
                StructureId.Sma,
                StructureId.M1,
                StructureId.MotorThalamus,
                StructureId.Striatum,
                StructureId.GPe,
                StructureId.GPi,
                StructureId.Stn,
                StructureId.Snr,
                StructureId.CerebellarVermis,
                StructureId.CerebellarLobules,
                StructureId.DeepCerebellarNuclei,
                StructureId.SpinalCordMotor),

            BuildFunctionalCircuitSupportEntry(
                "cerebellar_prediction",
                "Cerebellar timing and correction",
                cerebellarSupport > 0f,
                cerebellarSupport,
                "granule/Purkinje/deep nuclei/inferior olive loop with vestibular and motor cortex",
                StructureId.CerebellarGranule,
                StructureId.CerebellarVermis,
                StructureId.CerebellarLobules,
                StructureId.PurkinjeCellLayer,
                StructureId.DeepCerebellarNuclei,
                StructureId.InferiorOlive,
                StructureId.VestibularNuclei,
                StructureId.S1,
                StructureId.MotorThalamus,
                StructureId.PremotorCortex,
                StructureId.M1)
        ];
    }

    private static FunctionalCircuitSupportEntry BuildFunctionalCircuitSupportEntry(
        string functionKey,
        string displayName,
        bool active,
        float support,
        string evidence,
        params StructureId[] requiredStructures)
    {
        support = Clamp01(support);
        var status = ResolveFunctionalCircuitStatus(active, support);
        var warning = status switch
        {
            "unsupported" => "active function lacks biological circuit support",
            "weak" => "active function has weak biological circuit support",
            _ => string.Empty
        };

        return new FunctionalCircuitSupportEntry(
            functionKey,
            displayName,
            active,
            support,
            status,
            requiredStructures.Select(static structure => structure.ToString()).ToArray(),
            evidence,
            warning);
    }

    private static string ResolveFunctionalCircuitStatus(bool active, float support)
    {
        if (active && support < 0.10f)
        {
            return "unsupported";
        }

        if (active && support < 0.22f)
        {
            return "weak";
        }

        if (active)
        {
            return "supported";
        }

        return support >= 0.22f ? "primed" : "quiet";
    }

    private object GetCachedCircuitAuditSnapshot(int maxWarnings = 48)
    {
        var warningLimit = Math.Clamp(maxWarnings, 8, 512);
        lock (_gate)
        {
            if (_cachedCircuitAuditSnapshot is not null &&
                _cachedCircuitAuditTick == Tick &&
                _cachedCircuitAuditWarningLimit == warningLimit)
            {
                return _cachedCircuitAuditSnapshot;
            }

            _cachedCircuitAuditSnapshot = BuildCircuitAuditSnapshot(warningLimit);
            _cachedCircuitAuditTick = Tick;
            _cachedCircuitAuditWarningLimit = warningLimit;
            return _cachedCircuitAuditSnapshot;
        }
    }

    private object BuildCircuitAuditSnapshot(int maxWarnings = 48)
    {
        const long recentWindowTicks = 1200;
        var warningLimit = Math.Clamp(maxWarnings, 8, 512);
        lock (_gate)
        {
            var structures = new HashSet<StructureId>(ServiceRegistry.Keys);
            foreach (var pair in ConnectivityMap)
            {
                structures.Add(pair.Key);
                foreach (var connection in pair.Value)
                {
                    structures.Add(connection.Target);
                }
            }

            foreach (var pair in ServiceTelemetry)
            {
                structures.Add(pair.Key);
            }

            var outgoingRoutes = new Dictionary<StructureId, int>();
            var incomingRoutes = new Dictionary<StructureId, int>();
            foreach (var pair in ConnectivityMap)
            {
                outgoingRoutes[pair.Key] = pair.Value.Count;
                foreach (var connection in pair.Value)
                {
                    incomingRoutes[connection.Target] = incomingRoutes.TryGetValue(connection.Target, out var current)
                        ? current + 1
                        : 1;
                }
            }

            var windowStart = Math.Max(0, Tick - recentWindowTicks);
            var recentOut = new Dictionary<StructureId, int>();
            var recentIn = new Dictionary<StructureId, int>();
            foreach (var trace in _dispatchSpikeTrace)
            {
                if (trace.Tick < windowStart)
                {
                    continue;
                }

                recentOut[trace.SourceStructure] = recentOut.TryGetValue(trace.SourceStructure, out var outCount) ? outCount + 1 : 1;
                recentIn[trace.TargetStructure] = recentIn.TryGetValue(trace.TargetStructure, out var inCount) ? inCount + 1 : 1;
            }

            var items = new List<object>(structures.Count);
            var warnings = new List<object>();
            var silentCount = 0;
            var disconnectedCount = 0;
            var receivesInputNoOutputCount = 0;
            var aliveNotParticipatingCount = 0;
            var neverSpikedCount = 0;
            var noRouteCount = 0;
            var registeredDisconnectedCount = 0;
            var connectomeWithoutServiceCount = 0;
            var serviceOfflineCount = 0;
            var serviceBackoffCount = 0;
            var serviceUnhealthyCount = 0;
            var serviceUnknownCount = 0;
            var inhibitedCount = 0;
            var warningCount = 0;
            var noticeCount = 0;
            var okCount = 0;

            foreach (var structure in structures.OrderBy(s => s.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                var incoming = incomingRoutes.TryGetValue(structure, out var incomingCount) ? incomingCount : 0;
                var outgoing = outgoingRoutes.TryGetValue(structure, out var outgoingCount) ? outgoingCount : 0;
                var inboundSpikes = recentIn.TryGetValue(structure, out var inSpikes) ? inSpikes : 0;
                var outboundSpikes = recentOut.TryGetValue(structure, out var outSpikes) ? outSpikes : 0;
                var lifetimeInputSpikes = _dispatchLifetimeIn.TryGetValue(structure, out var totalInputCount) ? totalInputCount : 0;
                var lifetimeOutputSpikes = _dispatchLifetimeOut.TryGetValue(structure, out var totalOutputCount) ? totalOutputCount : 0;
                var lastInputTick = _dispatchLastInTick.TryGetValue(structure, out var lit) ? lit : long.MinValue;
                var lastOutputTick = _dispatchLastOutTick.TryGetValue(structure, out var lot) ? lot : long.MinValue;
                var hasTelemetry = ServiceTelemetry.TryGetValue(structure, out var telemetry);
                var hasRegisteredService = ServiceRegistry.ContainsKey(structure);
                var serviceStatus = telemetry?.LastStatus ?? (hasRegisteredService ? "INIT" : "UNREGISTERED");
                var issueList = new List<string>(6);
                var disconnected = incoming + outgoing == 0;
                var inputRouteMissing = incoming == 0;
                var outputRouteMissing = outgoing == 0;
                var silent = Tick > recentWindowTicks && inboundSpikes + outboundSpikes == 0;
                var neverSpiked = Tick > recentWindowTicks && lifetimeInputSpikes + lifetimeOutputSpikes == 0;
                var receivesInputNoOutput = (inboundSpikes > 0 || lifetimeInputSpikes > 0) && lifetimeOutputSpikes == 0;
                var aliveNotParticipating = string.Equals(serviceStatus, "OK", StringComparison.OrdinalIgnoreCase) && silent;
                var registeredDisconnected = hasRegisteredService && disconnected;
                var connectomeWithoutService = !hasRegisteredService && (incoming + outgoing > 0);
                var purpose = DescribeCircuitPurpose(structure);
                var inputSummary = DescribeCircuitInputs(structure);
                var outputSummary = DescribeCircuitOutputs(structure);
                var activationReason = ResolveCircuitActivationReason(structure, inboundSpikes, outboundSpikes, lastInputTick, lastOutputTick);
                var inhibited = IsCircuitCurrentlyInhibited(structure);
                var serviceHealth = ClassifyCircuitServiceStatus(serviceStatus, hasRegisteredService, hasTelemetry, Tick);
                var silenceCause = ResolveCircuitSilenceCause(
                    structure,
                    incoming,
                    outgoing,
                    inboundSpikes,
                    outboundSpikes,
                    lifetimeInputSpikes,
                    lifetimeOutputSpikes,
                    serviceStatus,
                    serviceHealth.IsHealthy,
                    hasRegisteredService);

                if (disconnected)
                {
                    disconnectedCount++;
                    noRouteCount++;
                    issueList.Add(hasRegisteredService ? "registered but no connectome route" : "no connectome route");
                }
                else
                {
                    if (inputRouteMissing)
                    {
                        issueList.Add("no incoming route");
                    }

                    if (outputRouteMissing)
                    {
                        issueList.Add("no outgoing route");
                    }
                }

                if (silent)
                {
                    silentCount++;
                    issueList.Add("no recent spikes");
                }

                if (neverSpiked)
                {
                    neverSpikedCount++;
                    issueList.Add("never spiked in retained trace");
                }

                if (receivesInputNoOutput)
                {
                    receivesInputNoOutputCount++;
                    issueList.Add("receives input but has never emitted output");
                }

                if (aliveNotParticipating)
                {
                    aliveNotParticipatingCount++;
                    issueList.Add("service alive but not participating");
                }

                if (!serviceHealth.IsHealthy)
                {
                    if (serviceHealth.IsOffline)
                    {
                        serviceOfflineCount++;
                    }

                    if (serviceHealth.IsBackoff)
                    {
                        serviceBackoffCount++;
                    }

                    if (serviceHealth.IsUnhealthy)
                    {
                        serviceUnhealthyCount++;
                    }

                    if (serviceHealth.IsUnknown)
                    {
                        serviceUnknownCount++;
                    }

                    if (!string.IsNullOrWhiteSpace(serviceHealth.Issue))
                    {
                        issueList.Add(serviceHealth.Issue);
                    }
                }

                if (silent && inhibited)
                {
                    inhibitedCount++;
                    issueList.Add("inhibited");
                }

                if (registeredDisconnected)
                {
                    registeredDisconnectedCount++;
                    issueList.Add("registered/visible but disconnected");
                }

                if (connectomeWithoutService)
                {
                    connectomeWithoutServiceCount++;
                    issueList.Add("connectome route has no registered service");
                }

                if (issueList.Count == 0)
                {
                    okCount++;
                }
                else
                {
                    var severity = ResolveCircuitAuditSeverity(
                        receivesInputNoOutput,
                        registeredDisconnected,
                        connectomeWithoutService,
                        serviceHealth.Warn,
                        neverSpiked,
                        silent);
                    if (severity.Equals("warn", StringComparison.OrdinalIgnoreCase))
                    {
                        warningCount++;
                    }
                    else if (severity.Equals("info", StringComparison.OrdinalIgnoreCase))
                    {
                        noticeCount++;
                    }

                    warnings.Add(new
                    {
                        Structure = structure.ToString(),
                        Severity = severity,
                        Issues = issueList.ToArray(),
                        RecentInputSpikes = inboundSpikes,
                        RecentOutputSpikes = outboundSpikes,
                        LifetimeInputSpikes = lifetimeInputSpikes,
                        LifetimeOutputSpikes = lifetimeOutputSpikes,
                        IncomingRoutes = incoming,
                        OutgoingRoutes = outgoing,
                        ServiceStatus = serviceStatus,
                        ServiceState = serviceHealth.State,
                        RegisteredService = hasRegisteredService,
                        Purpose = purpose,
                        Inputs = inputSummary,
                        Outputs = outputSummary,
                        LastActivationReason = activationReason,
                        SilenceCause = silenceCause,
                        Inhibited = inhibited
                    });
                }

                items.Add(new
                {
                    Structure = structure.ToString(),
                    Purpose = purpose,
                    Inputs = inputSummary,
                    Outputs = outputSummary,
                    ServiceStatus = serviceStatus,
                    ServiceState = serviceHealth.State,
                    IncomingRoutes = incoming,
                    OutgoingRoutes = outgoing,
                    RecentInputSpikes = inboundSpikes,
                    RecentOutputSpikes = outboundSpikes,
                    LifetimeInputSpikes = lifetimeInputSpikes,
                    LifetimeOutputSpikes = lifetimeOutputSpikes,
                    LastInputTick = lastInputTick,
                    LastOutputTick = lastOutputTick,
                    LastActivationReason = activationReason,
                    SilenceCause = silenceCause,
                    Inhibited = inhibited,
                    RegisteredService = hasRegisteredService,
                    Issues = issueList.ToArray()
                });
            }

            var functionSupport = BuildFunctionalCircuitSupportSnapshotLocked();
            var activeFunctionCount = functionSupport.Count(entry => entry.Active);
            var weakFunctionCount = functionSupport.Count(entry => entry.Status is "weak");
            var unsupportedFunctionCount = functionSupport.Count(entry => entry.Status is "unsupported");
            var functionSupportMean = functionSupport.Count == 0
                ? 0f
                : Clamp01(functionSupport.Sum(entry => entry.Support) / functionSupport.Count);

            return new
            {
                Summary = new
                {
                    Tick,
                    RecentWindowTicks = recentWindowTicks,
                    StructureCount = structures.Count,
                    OkCount = okCount,
                    WarningCount = warningCount,
                    NoticeCount = noticeCount,
                    SilentCount = silentCount,
                    DisconnectedCount = disconnectedCount,
                    ReceivesInputNoOutputCount = receivesInputNoOutputCount,
                    AliveNotParticipatingCount = aliveNotParticipatingCount,
                    NeverSpikedCount = neverSpikedCount,
                    NoRouteCount = noRouteCount,
                    RegisteredDisconnectedCount = registeredDisconnectedCount,
                    ConnectomeWithoutServiceCount = connectomeWithoutServiceCount,
                    ServiceOfflineCount = serviceOfflineCount,
                    ServiceBackoffCount = serviceBackoffCount,
                    ServiceUnhealthyCount = serviceUnhealthyCount,
                    ServiceUnknownCount = serviceUnknownCount,
                    InhibitedCount = inhibitedCount,
                    FunctionCount = functionSupport.Count,
                    ActiveFunctionCount = activeFunctionCount,
                    WeakFunctionCount = weakFunctionCount,
                    UnsupportedFunctionCount = unsupportedFunctionCount,
                    FunctionSupportMean = functionSupportMean,
                    TransportStats.GeneratedSpikes,
                    TransportStats.RoutedSpikes,
                    TransportStats.DeliveredSpikes,
                    TransportStats.ActivePathways
                },
                Warnings = warnings.Take(warningLimit).ToArray(),
                FunctionSupport = functionSupport,
                Items = items
            };
        }
    }

    private string DescribeCircuitPurpose(StructureId structure)
        => structure switch
        {
            StructureId.Retina or StructureId.V1 or StructureId.V2 or StructureId.V4 or StructureId.Mt => "visual perception and scene features",
            StructureId.SuperiorColliculus => "orienting gaze and salience toward sensory events",
            StructureId.Cochlea or StructureId.CochlearNucleus or StructureId.SuperiorOlive or StructureId.InferiorColliculus or StructureId.A1 => "auditory input and sound localization",
            StructureId.S1 => "somatosensory body surface and contact",
            StructureId.M1 or StructureId.PremotorCortex or StructureId.Sma or StructureId.SpinalCordMotor => "motor planning and movement output",
            StructureId.VestibularNuclei => "balance, head motion, and vestibular body state",
            StructureId.CerebellarGranule or StructureId.PurkinjeCellLayer or StructureId.CerebellarVermis or StructureId.CerebellarLobules or StructureId.DeepCerebellarNuclei or StructureId.InferiorOlive => "cerebellar timing, balance, prediction error, and learned correction",
            StructureId.Thalamus or StructureId.Trn or StructureId.Pulvinar or StructureId.MotorThalamus or StructureId.MediodorsalThalamus or StructureId.IntralaminarThalamus => "thalamic relay, attention gating, and cortical access",
            StructureId.Hypothalamus or StructureId.NucleusTractusSolitarius => "interoception, hunger, tiredness, and homeostatic drive",
            StructureId.Amygdala or StructureId.PeriaqueductalGray or StructureId.Habenula => "threat, aversion, defensive action, and negative prediction",
            StructureId.NucleusAccumbens or StructureId.VentralPallidum or StructureId.Striatum or StructureId.GPe or StructureId.GPi or StructureId.Stn or StructureId.Snr or StructureId.Snc or StructureId.Vta => "basal-ganglia selection, reward, motivation, and action gating",
            StructureId.EntorhinalCortex or StructureId.DentateGyrus or StructureId.CA1 or StructureId.CA2 or StructureId.CA3 or StructureId.Subiculum or StructureId.Presubiculum or StructureId.Parasubiculum or StructureId.ParahippocampalCortex or StructureId.RetrosplenialCortex => "hippocampal memory, context, place, and world map",
            StructureId.BrocaBa44Ba45 or StructureId.WernickePstgPsts or StructureId.ArcuateFasciculus or StructureId.SupramarginalAngular => "English language comprehension, intent, and speech planning",
            StructureId.Pfc or StructureId.OrbitofrontalCortex or StructureId.Acc or StructureId.Ppc or StructureId.PosteriorCingulate => "executive control, planning, attention, and monitoring",
            StructureId.CorpusCallosum => "interhemispheric transfer between paired cortical regions",
            _ => "biological circuit participation"
        };

    private string DescribeCircuitInputs(StructureId structure)
        => structure switch
        {
            StructureId.Retina => "world/avatar vision input",
            StructureId.S1 or StructureId.VestibularNuclei => "body-state facts and motor feedback",
            StructureId.Hypothalamus or StructureId.NucleusTractusSolitarius => "hunger, energy, darkness, shelter, and body physiology",
            StructureId.Amygdala or StructureId.PeriaqueductalGray => "threat, anxiety, pain, and salience",
            StructureId.CerebellarGranule or StructureId.PurkinjeCellLayer or StructureId.CerebellarVermis or StructureId.CerebellarLobules => "vestibular, proprioceptive, motor-copy, and inferior-olive teaching signals",
            StructureId.BrocaBa44Ba45 or StructureId.WernickePstgPsts or StructureId.ArcuateFasciculus => "English tokens, phonetics, auditory/language relay, and prefrontal context",
            _ => "connectome routes and neuromodulated spikes"
        };

    private string DescribeCircuitOutputs(StructureId structure)
        => structure switch
        {
            StructureId.Retina or StructureId.V1 or StructureId.V2 or StructureId.V4 or StructureId.Mt => "visual salience and feature spikes",
            StructureId.S1 or StructureId.VestibularNuclei => "body-schema and balance evidence",
            StructureId.Hypothalamus => "homeostatic drive for hunger, tiredness, and shelter seeking",
            StructureId.Amygdala or StructureId.PeriaqueductalGray => "defensive urgency and fight/flight pressure",
            StructureId.CerebellarGranule or StructureId.PurkinjeCellLayer or StructureId.CerebellarVermis or StructureId.CerebellarLobules or StructureId.DeepCerebellarNuclei => "timing, smoothing, balance correction, and motor prediction",
            StructureId.BrocaBa44Ba45 or StructureId.WernickePstgPsts or StructureId.ArcuateFasciculus => "language intent, narration, and speech planning",
            StructureId.M1 or StructureId.SpinalCordMotor => "brain-owned motor directives",
            _ => "downstream connectome spikes"
        };

    private string ResolveCircuitActivationReason(
        StructureId structure,
        int inboundSpikes,
        int outboundSpikes,
        long lastInputTick,
        long lastOutputTick)
    {
        if (outboundSpikes > 0)
        {
            return $"emitted output spikes in the last 1200 ticks; last output tick {lastOutputTick}";
        }

        if (inboundSpikes > 0)
        {
            return $"received input spikes in the last 1200 ticks; last input tick {lastInputTick}";
        }

        return "no recent activation evidence";
    }

    private string ResolveCircuitSilenceCause(
        StructureId structure,
        int incoming,
        int outgoing,
        int inboundSpikes,
        int outboundSpikes,
        int lifetimeInputSpikes,
        int lifetimeOutputSpikes,
        string serviceStatus,
        bool serviceHealthy,
        bool hasRegisteredService)
    {
        if (inboundSpikes + outboundSpikes > 0)
        {
            return "active";
        }

        if (!hasRegisteredService && incoming + outgoing > 0)
        {
            return "missing service for known connectome route";
        }

        if (incoming + outgoing == 0)
        {
            return "disconnected route";
        }

        if (!serviceHealthy)
        {
            return $"service unavailable: {serviceStatus}";
        }

        if (IsCircuitCurrentlyInhibited(structure))
        {
            return "inhibition";
        }

        if (incoming == 0)
        {
            return "no input route";
        }

        if (outgoing == 0)
        {
            return "no output route";
        }

        if (lifetimeInputSpikes + lifetimeOutputSpikes == 0)
        {
            return "no input";
        }

        return "no recent spikes";
    }

    private bool IsCircuitCurrentlyInhibited(StructureId structure)
    {
        if ((structure is StructureId.Thalamus or StructureId.MotorThalamus or StructureId.Pulvinar or StructureId.MediodorsalThalamus or StructureId.IntralaminarThalamus) &&
            NeuronalAttentionWorkspace.DistractorSuppression > 0.72)
        {
            return true;
        }

        return NeuronalMotor.OutputInhibition > 0.72 && IsMotorOrLanguageCircuit(structure);
    }

    private static bool IsMotorOrLanguageCircuit(StructureId structure)
        => structure is StructureId.M1
            or StructureId.Sma
            or StructureId.PremotorCortex
            or StructureId.SpinalCordMotor
            or StructureId.MotorThalamus
            or StructureId.BrocaBa44Ba45
            or StructureId.WernickePstgPsts
            or StructureId.ArcuateFasciculus
            or StructureId.SupramarginalAngular;

    private static void UpdateLastTick(Dictionary<StructureId, long> ticks, StructureId structure, long tick)
    {
        if (!ticks.TryGetValue(structure, out var existing) || tick > existing)
        {
            ticks[structure] = tick;
        }
    }

    private static string ResolveCircuitAuditSeverity(
        bool receivesInputNoOutput,
        bool registeredDisconnected,
        bool connectomeWithoutService,
        bool unhealthyService,
        bool neverSpiked,
        bool silent)
    {
        if (unhealthyService || receivesInputNoOutput || registeredDisconnected || connectomeWithoutService)
        {
            return "warn";
        }

        if (neverSpiked || silent)
        {
            return "info";
        }

        return "ok";
    }

    private static CircuitServiceAuditState ClassifyCircuitServiceStatus(
        string? status,
        bool hasRegisteredService,
        bool hasTelemetry,
        long tick)
    {
        const long startupGraceTicks = 120;
        var normalized = string.IsNullOrWhiteSpace(status) ? "UNKNOWN" : status.Trim();

        if (!hasRegisteredService && normalized.Equals("UNREGISTERED", StringComparison.OrdinalIgnoreCase))
        {
            return CircuitServiceAuditState.Healthy("unregistered");
        }

        if (normalized.Equals("OK", StringComparison.OrdinalIgnoreCase))
        {
            return CircuitServiceAuditState.Healthy("online");
        }

        if (normalized.Equals("BACKOFF", StringComparison.OrdinalIgnoreCase))
        {
            return new CircuitServiceAuditState(
                IsHealthy: false,
                Warn: true,
                IsOffline: false,
                IsBackoff: true,
                IsUnhealthy: false,
                IsUnknown: false,
                State: "backoff",
                Issue: "service backoff");
        }

        if (hasRegisteredService && (!hasTelemetry || normalized.Equals("INIT", StringComparison.OrdinalIgnoreCase)))
        {
            var initialising = tick <= startupGraceTicks;
            return new CircuitServiceAuditState(
                IsHealthy: false,
                Warn: !initialising,
                IsOffline: !initialising,
                IsBackoff: false,
                IsUnhealthy: false,
                IsUnknown: initialising,
                State: initialising ? "initialising" : "offline",
                Issue: hasTelemetry ? "service initialising" : "registered service has no telemetry");
        }

        if (hasRegisteredService && normalized.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            var initialising = tick <= startupGraceTicks;
            return new CircuitServiceAuditState(
                IsHealthy: false,
                Warn: !initialising,
                IsOffline: !initialising,
                IsBackoff: false,
                IsUnhealthy: false,
                IsUnknown: initialising,
                State: initialising ? "initialising" : "offline",
                Issue: "service status unknown");
        }

        return new CircuitServiceAuditState(
            IsHealthy: false,
            Warn: true,
            IsOffline: false,
            IsBackoff: false,
            IsUnhealthy: true,
            IsUnknown: false,
            State: "unhealthy",
            Issue: $"service {normalized}");
    }

    private sealed record CircuitServiceAuditState(
        bool IsHealthy,
        bool Warn,
        bool IsOffline,
        bool IsBackoff,
        bool IsUnhealthy,
        bool IsUnknown,
        string State,
        string Issue)
    {
        public static CircuitServiceAuditState Healthy(string state)
            => new(true, false, false, false, false, false, state, string.Empty);
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0.0f, 1.0f);
    private static float ClampSigned01(float value) => Math.Clamp(value, -1.0f, 1.0f);

    public object ToDiagnostics(AutoProfileSettings? autoProfile = null)
    {
        lock (_gate)
        {
            return BuildDiagnosticsLocked(autoProfile);
        }
    }


    private object BuildDiagnosticsLocked(AutoProfileSettings? autoProfile) => new
    {
        Tick,
        CognitionAuthority = new
        {
            Authority = NeuronalCognitionAuthorityRuntime.Authority,
            SymbolicScaffoldCanAuthorize = false,
            SemanticMotorInjectionAllowed = false,
            WorldGoalSteeringAllowed = false,
            LegacyLanguageEmissionAllowed = false,
            AuthoritativeEndpoint = "/api/v1/cognition-authority"
        },
        SimulationClockMs,
        TickDurationMs,
        AutoProfile = autoProfile ?? AutoProfileSettings.Default,
        InputGates,
          BodyState = new
          {
              BodyState.ForwardVelocity,
              BodyState.TurnRateDeg,
              BodyState.ContactLevel,
              BodyState.TactileFront,
              BodyState.TactileLeft,
              BodyState.TactileRight,
              BodyState.TactileGround,
              BodyState.PainLevel,
              BodyState.Hunger,
              BodyState.Health,
              BodyState.LeftMotorDrive,
              BodyState.RightMotorDrive,
              BodyState.MotorAsymmetry,
              BodyState.LastInputTick
          },
          Curriculum,
          MetabolicPhysiology = new
        {
            MetabolicPhysiology.NeuronalSleepObserved,
            MetabolicPhysiology.AtpBudget,
            MetabolicPhysiology.MaxAtpBudget,
            MetabolicPhysiology.HomeostaticPressure,
            MetabolicPhysiology.MaxHomeostaticPressure,
            MetabolicPhysiology.SleepTicks,
            MetabolicPhysiology.WakeTicks,
            MetabolicPhysiology.SleepEpisodes,
            MetabolicPhysiology.LastTransitionTick,
            Role = "ReadOnlyPhysiologicalTransducer"
        },
        VisualAttention = new
        {
            Authority = NeuronalVisualAttentionDecision.Authority,
            VisualAttention.Available,
            VisualAttention.Active,
            VisualAttention.LeftFieldDrive,
            VisualAttention.RightFieldDrive,
            VisualAttention.LeftHemisphereTrnSuppression,
            VisualAttention.RightHemisphereTrnSuppression,
            VisualAttention.FocusedField,
            VisualAttention.FocusedHemisphere,
            VisualAttention.FocusConfidence,
            VisualAttention.SelectionMargin,
            VisualAttention.CircuitCoverage,
            VisualAttention.SustainedSelectionTicks,
            VisualAttention.LastSelectionTick,
            CanAcceptAttentionOverrides = false,
            LegacyWinnerEnabled = false
        },
        NeuronalMotor,
        NeuronalLanguageGrounding,
        NeuronalPerception,
        NeuronalMemory,
        NeuronalAttentionWorkspace,
        NeuronalSleepConsolidation,
        NeuronalAffectValuation,
        NeuronalExecutive,
        BrainBehavior = GetCachedBrainBehaviorSnapshot(),
        ConsolidationTelemetry = BuildConsolidationTelemetrySnapshot(),
        CircuitAudit = GetCachedCircuitAuditSnapshot(),
        ProsodyTelemetry = GetProsodyTelemetrySnapshot(),
        LastSnapshotTick,
        LastSnapshotSimulationMs,
        LastSnapshotWallClockUnixMs,
        PerformanceProfileName,
        OscillationPhases,
        ServiceCount = GetExpectedServiceCountLocked(),
        ServiceTelemetry = ServiceTelemetry.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => new
            {
                kvp.Value.LastAckLatencyMs,
                kvp.Value.AckLatencyEwmaMs,
                kvp.Value.ConsecutiveFailures,
                kvp.Value.AttemptCount,
                kvp.Value.SuccessCount,
                kvp.Value.TimeoutFailureCount,
                kvp.Value.NextRetryTimestampMs,
                kvp.Value.LastStatus,
                kvp.Value.LastError,
                kvp.Value.LastTickProcessed,
                kvp.Value.LastUpdateTimestampMs,
                kvp.Value.LatencyLt100MsCount,
                kvp.Value.Latency100To250MsCount,
                kvp.Value.Latency250To500MsCount,
                kvp.Value.Latency500To1000MsCount,
                kvp.Value.LatencyGte1000MsCount
            }),
        TransportStats = new
        {
            TransportStats.Tick,
            TransportStats.ActiveServices,
            TransportStats.SuccessfulAcks,
            TransportStats.DrainCalls,
            TransportStats.DrainedSpikes,
            TransportStats.DispatchedSpikes,
            TransportStats.DroppedByBudget,
            TransportStats.TopQueries,
            TransportStats.SpontaneousGenerated,
            TransportStats.SpontaneousDelivered,
            TransportStats.SpontaneousDispatchErrors,
            TransportStats.SpontaneousLastError,
            TotalSpontaneousGenerated,
            TotalSpontaneousDelivered,
            TotalSpontaneousDispatchErrors,
            TransportStats.ActivePathways,
            TransportStats.DispatchQueueQueuedBatches,
            TransportStats.DispatchQueueQueuedSpikes,
            TransportStats.DispatchQueuePeakBatches,
            TransportStats.DispatchQueuePeakSpikes,
            TransportStats.DispatchQueueDroppedBatches,
            TransportStats.DispatchQueueDroppedSpikes,
            TransportStats.DispatchQueueFlushedBatches,
            TransportStats.DispatchQueueFlushActiveTargets,
            TransportStats.DispatchQueueFlushMaxTargetBurstSpikes,
            TransportStats.DispatchQueueDispatchErrors,
            TransportStats.DispatchQueueLastError,
            TransportStats.GeneratedSpikes,
            TransportStats.RoutedSpikes,
            TransportStats.DeliveredSpikes,
            TransportStats.RouteDroppedNoConnectivity,
            TransportStats.RouteDroppedNoTargets,
            TransportStats.RouteDroppedTargetUnavailable,
            TransportStats.RouteDroppedByBackpressure,
            TransportStats.AdaptivePressure,
            TransportStats.AdaptiveScale,
            TransportStats.EffectiveMaxSpikeDispatchPerServicePerTick,
            TransportStats.EffectiveMaxSpikeDispatchTotalPerTick,
            TransportStats.EffectiveMaxTopQueriesPerTick,
            TransportStats.EffectiveTickAckTimeoutMs,
            TransportStats.EffectiveTickIoTimeoutMs,
            TransportStats.EffectiveTickPublishWaitMs,
            TransportStats.EffectiveTickPublishSettleMs,
            TransportStats.AckLatencyEwmaMs,
            TransportStats.AckLatencyLt100Ms,
            TransportStats.AckLatency100To250Ms,
            TransportStats.AckLatency250To500Ms,
            TransportStats.AckLatency500To1000Ms,
            TransportStats.AckLatencyGte1000Ms,
            TransportStats.TickWallMs,
            TransportStats.TickWallP50Ms,
            TransportStats.TickWallP95Ms,
            TransportStats.TickWallP99Ms,
            TransportStats.DegradeSignal,
            TransportStats.PerceptionLanguageGenerated,
            TransportStats.PerceptionLanguageDelivered,
            TransportStats.PerceptionLanguageDispatchErrors,
            TransportStats.PerceptionLanguageLastError,
            TransportStats.LanguageBackoffAttempts,
            TransportStats.LanguageBackoffResolved,
            TransportStats.LanguageBackoffFallbackSelections,
            TransportStats.LanguageBackoffDispatchErrors,
            TransportStats.LanguageBackoffTopEdges,
            TransportStats.LanguageBackoffGraphs,
            TransportStats.LanguageBackoffModeStates
        }
    };

    public object GetServiceHealthSnapshot()
    {
        lock (_gate)
        {
            return ServiceTelemetry.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => new
                {
                    kvp.Value.LastStatus,
                    kvp.Value.LastError,
                    kvp.Value.LastTickProcessed,
                    kvp.Value.LastUpdateTimestampMs
                });
        }
    }

    public object GetStartupHealth(int maxNonOkDetails = 16)
    {
        var detailsLimit = Math.Clamp(maxNonOkDetails, 1, 256);
        if (!System.Threading.Monitor.TryEnter(_gate, TimeSpan.FromMilliseconds(100)))
        {
            return _lastStartupHealthSnapshot ?? BuildStartupHealthFallback(detailsLimit);
        }

        try
        {
            var snapshot = BuildStartupHealthLocked(detailsLimit, telemetryStale: false);
            _lastStartupHealthSnapshot = snapshot;
            return snapshot;
        }
        finally
        {
            System.Threading.Monitor.Exit(_gate);
        }
    }

    private object GetCachedBrainBehaviorSnapshot()
    {
        lock (_gate)
        {
            if (_cachedBrainBehaviorSnapshot is not null && _cachedBrainBehaviorTick == Tick)
            {
                return _cachedBrainBehaviorSnapshot;
            }

            _cachedBrainBehaviorSnapshot = BuildBrainBehaviorSnapshot();
            _cachedBrainBehaviorTick = Tick;
            return _cachedBrainBehaviorSnapshot;
        }
    }

    public object GetValidationSnapshot(int maxSnapshotAgeTicks = 20, int maxNonOkServices = 2)
    {
        var snapshotAgeLimit = Math.Max(1, maxSnapshotAgeTicks);
        var nonOkLimit = Math.Max(0, maxNonOkServices);
        if (!System.Threading.Monitor.TryEnter(_gate, TimeSpan.FromMilliseconds(100)))
        {
            return _lastValidationSnapshot ?? BuildValidationFallback(snapshotAgeLimit, nonOkLimit);
        }

        try
        {
            var snapshot = BuildValidationSnapshotLocked(snapshotAgeLimit, nonOkLimit, telemetryStale: false);
            _lastValidationSnapshot = snapshot;
            return snapshot;
        }
        finally
        {
            System.Threading.Monitor.Exit(_gate);
        }
    }

    private object BuildStartupHealthLocked(int detailsLimit, bool telemetryStale)
    {
        var nonOkEntries = GetNonOkServiceEntriesLocked();
        var nonOkDetails = nonOkEntries
            .Take(detailsLimit)
            .Select(entry => new
            {
                Structure = entry.Structure.ToString(),
                Status = entry.Status,
                Error = entry.Error
            })
            .ToArray();

        return new
        {
            Tick,
            LastSnapshotTick,
            LastSnapshotSimulationMs,
            LastSnapshotWallClockUnixMs,
            ServiceCount = GetExpectedServiceCountLocked(),
            NonOkCount = nonOkEntries.Count,
            NonOkDetails = nonOkDetails,
            TelemetryStale = telemetryStale
        };
    }

    private object BuildStartupHealthFallback(int detailsLimit)
    {
        _ = detailsLimit;
        return new
        {
            Tick,
            LastSnapshotTick,
            LastSnapshotSimulationMs,
            LastSnapshotWallClockUnixMs,
            ServiceCount = 0,
            NonOkCount = 0,
            NonOkDetails = Array.Empty<object>(),
            TelemetryStale = true
        };
    }

    private object BuildValidationSnapshotLocked(int snapshotAgeLimit, int nonOkLimit, bool telemetryStale)
    {
        var nonOkCount = GetNonOkServiceEntriesLocked().Count;
        var snapshotAgeTicks = LastSnapshotTick > 0 && Tick >= LastSnapshotTick
            ? Tick - LastSnapshotTick
            : long.MaxValue;
        var hasSnapshot = LastSnapshotTick > 0;
        var snapshotFresh = hasSnapshot && snapshotAgeTicks <= snapshotAgeLimit;
        var biological = ComputeBiologicalValidationMetricsLocked();

        var generated = TransportStats.GeneratedSpikes;
        var routed = TransportStats.RoutedSpikes;
        var delivered = TransportStats.DeliveredSpikes;
        var pipelineMonotonic = generated >= 0 && routed >= 0 && delivered >= 0 &&
            (delivered == 0 || routed > 0 || TransportStats.DispatchQueueFlushedBatches > 0);
        var queueHealthy = TransportStats.DispatchQueueDroppedBatches == 0 && TransportStats.DispatchQueueDispatchErrors == 0;
        var servicesHealthy = nonOkCount <= nonOkLimit;
        var sleepBiologyBounds = MetabolicPhysiology.AtpBudget >= 0f && MetabolicPhysiology.AtpBudget <= MetabolicPhysiology.MaxAtpBudget + 0.001f;
        var connectomeCoverage = biological.MissingAsSource.Length == 0 && biological.MissingAsTarget.Length == 0;
        var neurotransmitterCoverage = biological.MissingNeurotransmitters.Length == 0;
        var requiredPathwayCoverage = biological.RequiredPathways.Values.All(v => v);
        var feedbackCoverage = biological.FeedbackProjectionCount >= 6;

        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["servicesHealthy"] = servicesHealthy,
            ["snapshotFresh"] = snapshotFresh,
            ["pipelineMonotonic"] = pipelineMonotonic,
            ["queueHealthy"] = queueHealthy,
            ["sleepBounds"] = sleepBiologyBounds,
            ["connectomeCoverage"] = connectomeCoverage,
            ["neurotransmitterCoverage"] = neurotransmitterCoverage,
            ["requiredPathwayCoverage"] = requiredPathwayCoverage,
            ["feedbackCoverage"] = feedbackCoverage
        };

        return new
        {
            Tick,
            SimulationClockMs,
            ServiceCount = GetExpectedServiceCountLocked(),
            NonOkCount = nonOkCount,
            LastSnapshotTick,
            SnapshotAgeTicks = hasSnapshot ? snapshotAgeTicks : -1,
            SnapshotAgeLimitTicks = snapshotAgeLimit,
            NonOkLimit = nonOkLimit,
            Profile = PerformanceProfileName,
            Checks = checks,
            IsValid = checks.Values.All(v => v),
            TelemetryStale = telemetryStale,
            Biology = new
            {
                biological.StructureCount,
                biological.SourcesWithOutbound,
                biological.TargetsWithInbound,
                biological.ProjectionCount,
                biological.FeedbackProjectionCount,
                MissingAsSource = biological.MissingAsSource,
                MissingAsTarget = biological.MissingAsTarget,
                MissingNeurotransmitters = biological.MissingNeurotransmitters,
                RequiredPathways = biological.RequiredPathways
            },
            Transport = new
            {
                generated,
                routed,
                delivered,
                droppedNoConnectivity = TransportStats.RouteDroppedNoConnectivity,
                droppedNoTarget = TransportStats.RouteDroppedNoTargets,
                droppedUnavailable = TransportStats.RouteDroppedTargetUnavailable,
                droppedBackpressure = TransportStats.RouteDroppedByBackpressure,
                queueDroppedSpikes = TransportStats.DispatchQueueDroppedSpikes,
                queueDroppedBatches = TransportStats.DispatchQueueDroppedBatches,
                queueDispatchErrors = TransportStats.DispatchQueueDispatchErrors,
                ackLatencyEwmaMs = TransportStats.AckLatencyEwmaMs,
                tickWallMs = TransportStats.TickWallMs,
                tickWallP50Ms = TransportStats.TickWallP50Ms,
                tickWallP95Ms = TransportStats.TickWallP95Ms,
                tickWallP99Ms = TransportStats.TickWallP99Ms,
                degradeSignal = TransportStats.DegradeSignal
            },
            Sleep = new
            {
                Authority = NeuronalSleepConsolidationDecision.Authority,
                NeuronalSleepConsolidation.State,
                NeuronalSleepConsolidation.ReplayActive,
                MetabolicPhysiology.AtpBudget,
                MetabolicPhysiology.HomeostaticPressure,
                MetabolicPhysiology.WakeTicks,
                MetabolicPhysiology.SleepTicks
            }
        };
    }

    private object BuildValidationFallback(int snapshotAgeLimit, int nonOkLimit)
    {
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["servicesHealthy"] = false,
            ["snapshotFresh"] = false,
            ["pipelineMonotonic"] = true,
            ["queueHealthy"] = true,
            ["sleepBounds"] = true,
            ["connectomeCoverage"] = true,
            ["neurotransmitterCoverage"] = true,
            ["requiredPathwayCoverage"] = true,
            ["feedbackCoverage"] = true
        };

        return new
        {
            Tick,
            SimulationClockMs,
            ServiceCount = 0,
            NonOkCount = 0,
            LastSnapshotTick,
            SnapshotAgeTicks = -1L,
            SnapshotAgeLimitTicks = snapshotAgeLimit,
            NonOkLimit = nonOkLimit,
            Profile = PerformanceProfileName,
            Checks = checks,
            IsValid = false,
            TelemetryStale = true,
            Biology = new
            {
                StructureCount = 0,
                SourcesWithOutbound = 0,
                TargetsWithInbound = 0,
                ProjectionCount = 0,
                FeedbackProjectionCount = 0,
                MissingAsSource = Array.Empty<string>(),
                MissingAsTarget = Array.Empty<string>(),
                MissingNeurotransmitters = Array.Empty<string>(),
                RequiredPathways = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            },
            Transport = new
            {
                generated = 0L,
                routed = 0L,
                delivered = 0L,
                droppedNoConnectivity = 0L,
                droppedNoTarget = 0L,
                droppedUnavailable = 0L,
                droppedBackpressure = 0L,
                queueDroppedSpikes = 0L,
                queueDroppedBatches = 0L,
                queueDispatchErrors = 0L,
                ackLatencyEwmaMs = 0.0,
                tickWallMs = 0.0,
                tickWallP50Ms = 0.0,
                tickWallP95Ms = 0.0,
                tickWallP99Ms = 0.0,
                degradeSignal = 0.0
            },
            Sleep = new
            {
                isSleeping = false,
                atpBudget = 0f,
                sleepPressure = 0f,
                wakeTicks = 0L,
                sleepTicks = 0L
            }
        };
    }

    public object GetBiologicalConnectomeReport()
    {
        lock (_gate)
        {
            var biological = ComputeBiologicalValidationMetricsLocked();
            var neurotransmitterDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var pathwayClassDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var projectionCount = 0;

            foreach (var pair in ConnectivityMap)
            {
                foreach (var connection in pair.Value)
                {
                    projectionCount++;

                    var nt = connection.Neurotransmitter.ToString();
                    neurotransmitterDistribution[nt] = neurotransmitterDistribution.TryGetValue(nt, out var ntCount)
                        ? ntCount + 1
                        : 1;

                    var cls = ClassifyProjectionClass(connection.ProjectionType);
                    pathwayClassDistribution[cls] = pathwayClassDistribution.TryGetValue(cls, out var clsCount)
                        ? clsCount + 1
                        : 1;
                }
            }

            var nonOkServices = GetNonOkServiceEntriesLocked().Count;
            var routeDrops = new
            {
                NoConnectivity = TransportStats.RouteDroppedNoConnectivity,
                NoTarget = TransportStats.RouteDroppedNoTargets,
                TargetUnavailable = TransportStats.RouteDroppedTargetUnavailable,
                Backpressure = TransportStats.RouteDroppedByBackpressure
            };

            var warnings = new List<string>();
            if (biological.MissingAsSource.Length > 0)
            {
                warnings.Add($"Missing source coverage: {string.Join(", ", biological.MissingAsSource)}");
            }

            if (biological.MissingAsTarget.Length > 0)
            {
                warnings.Add($"Missing target coverage: {string.Join(", ", biological.MissingAsTarget)}");
            }

            if (biological.MissingNeurotransmitters.Length > 0)
            {
                warnings.Add($"Missing neurotransmitter classes: {string.Join(", ", biological.MissingNeurotransmitters)}");
            }

            var missingPathways = biological.RequiredPathways
                .Where(pair => !pair.Value)
                .Select(pair => pair.Key)
                .ToArray();
            if (missingPathways.Length > 0)
            {
                warnings.Add($"Missing required pathway classes: {string.Join(", ", missingPathways)}");
            }

            if (nonOkServices > 0)
            {
                warnings.Add($"Non-OK services: {nonOkServices}");
            }

            var routeDropTotal = routeDrops.NoConnectivity + routeDrops.NoTarget + routeDrops.TargetUnavailable + routeDrops.Backpressure;
            if (routeDropTotal > 0)
            {
                warnings.Add($"Route drops detected: {routeDropTotal}");
            }

            if (TransportStats.DispatchQueueDroppedSpikes > 0 || TransportStats.DispatchQueueDispatchErrors > 0)
            {
                warnings.Add($"Queue pressure: dropped_spikes={TransportStats.DispatchQueueDroppedSpikes}, dispatch_errors={TransportStats.DispatchQueueDispatchErrors}");
            }

            if (TransportStats.DispatchedSpikes > 0 && TransportStats.ActivePathways <= 0)
            {
                warnings.Add("Dispatched spikes present but active_pathways is zero.");
            }

            var driftStatus = warnings.Count == 0 ? "STABLE" : "DRIFT";

            return new
            {
                GeneratedAtTick = Tick,
                Coverage = new
                {
                    StructureCount = biological.StructureCount,
                    SourcesWithOutbound = biological.SourcesWithOutbound,
                    TargetsWithInbound = biological.TargetsWithInbound,
                    ProjectionCount = projectionCount,
                    FeedbackProjectionCount = biological.FeedbackProjectionCount,
                    BidirectionalCoverage = biological.MissingAsSource.Length == 0 && biological.MissingAsTarget.Length == 0,
                    MissingAsSource = biological.MissingAsSource,
                    MissingAsTarget = biological.MissingAsTarget
                },
                NeurotransmitterDistribution = neurotransmitterDistribution
                    .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
                PathwayClassDistribution = pathwayClassDistribution
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
                BiologicalIntegrity = new
                {
                    MissingNeurotransmitters = biological.MissingNeurotransmitters,
                    RequiredPathways = biological.RequiredPathways
                },
                Drift = new
                {
                    Status = driftStatus,
                    NonOkServices = nonOkServices,
                    ActivePathways = TransportStats.ActivePathways,
                    DispatchedSpikes = TransportStats.DispatchedSpikes,
                    RouteDrops = routeDrops,
                    QueueDroppedSpikes = TransportStats.DispatchQueueDroppedSpikes,
                    DispatchErrors = TransportStats.DispatchQueueDispatchErrors,
                    Warnings = warnings
                }
            };
        }
    }

    private BiologicalValidationMetrics ComputeBiologicalValidationMetricsLocked()
    {
        var allStructures = Enum.GetValues<StructureId>();
        var sourceSet = ConnectivityMap
            .Where(kvp => kvp.Value.Count > 0)
            .Select(kvp => kvp.Key)
            .ToHashSet();

        var targetSet = new HashSet<StructureId>();
        var ntSeen = new HashSet<NTEnum>();
        var feedbackProjectionCount = 0;
        var projectionCount = 0;

        var requiredPathways = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["corticothalamic_feedback"] = false,
            ["hippocampal_indexing"] = false,
            ["basal_ganglia_gating"] = false,
            ["cerebellar_error_correction"] = false,
            ["limbic_modulation"] = false,
            ["neuromodulatory_broadcast"] = false
        };

        foreach (var pair in ConnectivityMap)
        {
            foreach (var connection in pair.Value)
            {
                projectionCount++;
                targetSet.Add(connection.Target);
                ntSeen.Add(connection.Neurotransmitter);

                var projectionType = connection.ProjectionType ?? string.Empty;
                var projectionTypeLower = projectionType.ToLowerInvariant();

                if (projectionTypeLower.Contains("feedback", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("recurrent", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("loop", StringComparison.OrdinalIgnoreCase))
                {
                    feedbackProjectionCount++;
                }

                if (projectionTypeLower.Contains("corticothalamic", StringComparison.OrdinalIgnoreCase))
                {
                    requiredPathways["corticothalamic_feedback"] = true;
                }

                if (projectionTypeLower.Contains("hippocampal", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("entorhinal", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("index", StringComparison.OrdinalIgnoreCase))
                {
                    requiredPathways["hippocampal_indexing"] = true;
                }

                if (projectionTypeLower.Contains("striatal", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("basal", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("pallid", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("nigro", StringComparison.OrdinalIgnoreCase))
                {
                    requiredPathways["basal_ganglia_gating"] = true;
                }

                if (projectionTypeLower.Contains("cerebell", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("oliv", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("dcn", StringComparison.OrdinalIgnoreCase))
                {
                    requiredPathways["cerebellar_error_correction"] = true;
                }

                if (projectionTypeLower.Contains("amyg", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("limbic", StringComparison.OrdinalIgnoreCase) ||
                    projectionTypeLower.Contains("acc", StringComparison.OrdinalIgnoreCase))
                {
                    requiredPathways["limbic_modulation"] = true;
                }

                if (connection.Neurotransmitter is NTEnum.DOPAMINE or NTEnum.SEROTONIN or NTEnum.ACETYLCHOLINE or NTEnum.NOREPINEPHRINE ||
                    projectionTypeLower.Contains("neuromod", StringComparison.OrdinalIgnoreCase))
                {
                    requiredPathways["neuromodulatory_broadcast"] = true;
                }
            }
        }

        var missingAsSource = allStructures
            .Where(structure => !sourceSet.Contains(structure))
            .Select(structure => structure.ToString())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var missingAsTarget = allStructures
            .Where(structure => !targetSet.Contains(structure))
            .Select(structure => structure.ToString())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var missingNeurotransmitters = Enum.GetValues<NTEnum>()
            .Where(nt => !ntSeen.Contains(nt))
            .Select(nt => nt.ToString())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        return new BiologicalValidationMetrics(
            StructureCount: allStructures.Length,
            SourcesWithOutbound: sourceSet.Count,
            TargetsWithInbound: targetSet.Count,
            ProjectionCount: projectionCount,
            FeedbackProjectionCount: feedbackProjectionCount,
            MissingAsSource: missingAsSource,
            MissingAsTarget: missingAsTarget,
            MissingNeurotransmitters: missingNeurotransmitters,
            RequiredPathways: requiredPathways);
    }

    public (int TotalServices, int NonOkServices) GetServiceHealthCounts()
    {
        lock (_gate)
        {
            var totalServices = GetExpectedServiceCountLocked();
            var nonOkServices = GetNonOkServiceEntriesLocked().Count;
            return (totalServices, nonOkServices);
        }
    }

    private int GetExpectedServiceCountLocked()
    {
        var expected = 0;
        foreach (var structure in ServiceRegistry.Keys)
        {
            if (!ServiceTelemetry.TryGetValue(structure, out var telemetry) ||
                !ServiceTelemetryAggregation.IsAbsent(telemetry))
            {
                expected++;
            }
        }

        expected += ServiceTelemetry.Count(pair =>
            !ServiceRegistry.ContainsKey(pair.Key) &&
            !ServiceTelemetryAggregation.IsAbsent(pair.Value));
        return expected;
    }

    private List<(StructureId Structure, string Status, string Error)> GetNonOkServiceEntriesLocked()
    {
        var nonOk = new List<(StructureId Structure, string Status, string Error)>();

        foreach (var pair in ServiceRegistry.OrderBy(p => p.Key.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            if (!ServiceTelemetry.TryGetValue(pair.Key, out var telemetry))
            {
                nonOk.Add((pair.Key, "UNKNOWN", "No telemetry reported yet."));
                continue;
            }

            var status = string.IsNullOrWhiteSpace(telemetry.LastStatus) ? "UNKNOWN" : telemetry.LastStatus;
            if (ServiceTelemetryAggregation.IsAbsent(telemetry))
            {
                continue;
            }

            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                nonOk.Add((pair.Key, status, string.IsNullOrWhiteSpace(telemetry.LastError) ? string.Empty : telemetry.LastError));
            }
        }

        foreach (var telemetryEntry in ServiceTelemetry)
        {
            if (ServiceRegistry.ContainsKey(telemetryEntry.Key))
            {
                continue;
            }

            var status = string.IsNullOrWhiteSpace(telemetryEntry.Value.LastStatus) ? "UNKNOWN" : telemetryEntry.Value.LastStatus;
            if (ServiceTelemetryAggregation.IsAbsent(telemetryEntry.Value))
            {
                continue;
            }

            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                nonOk.Add((telemetryEntry.Key, status, string.IsNullOrWhiteSpace(telemetryEntry.Value.LastError) ? string.Empty : telemetryEntry.Value.LastError));
            }
        }

        return nonOk;
    }

    public NetworkStateDocument ExportNetworkState(BrainSnapshot? latestSnapshot = null)
    {
        lock (_gate)
        {
            var exportedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var projectionCount = ConnectivityMap.Sum(pair => pair.Value?.Count ?? 0);
            var document = new NetworkStateDocument
            {
                SchemaVersion = NetworkStateDocument.CurrentSchemaVersion,
                ExportedAtUnixMs = exportedAtUnixMs,
                ExportedTickWallClockUnixMs = exportedAtUnixMs,
                Tick = Tick,
                SimulationClockMs = SimulationClockMs,
                TickDurationMs = TickDurationMs,
                PerformanceProfileName = string.IsNullOrWhiteSpace(PerformanceProfileName) ? "normal" : PerformanceProfileName,
                InputGates = InputGates,
                MetabolicPhysiology = MetabolicPhysiology,
                Curriculum = Curriculum,
                LastSnapshotTick = LastSnapshotTick,
                LastSnapshotSimulationMs = LastSnapshotSimulationMs,
                LastSnapshotWallClockUnixMs = LastSnapshotWallClockUnixMs,
                TotalSpontaneousGenerated = TotalSpontaneousGenerated,
                TotalSpontaneousDelivered = TotalSpontaneousDelivered,
                TotalSpontaneousDispatchErrors = TotalSpontaneousDispatchErrors,
                TransportStats = TransportStats,
                ExportFingerprint = BuildNetworkStateFingerprint(Tick, SimulationClockMs, ServiceRegistry.Count, projectionCount, exportedAtUnixMs),
                LatestSnapshot = latestSnapshot
            };

            foreach (var pair in OscillationPhases)
            {
                document.OscillationPhases[pair.Key.ToString()] = NormalizePhase(pair.Value);
            }

            foreach (var pair in ServiceRegistry)
            {
                document.ServiceRegistry[pair.Key.ToString()] = pair.Value;
            }

            foreach (var pair in ConnectivityMap)
            {
                document.ConnectivityMap[pair.Key.ToString()] = pair.Value.ToList();
            }

            foreach (var pair in ServiceTelemetry)
            {
                document.ServiceTelemetry[pair.Key.ToString()] = pair.Value;
            }

            document.OutputLog.AddRange(_outputLog);
            document.SpikeLog.AddRange(_spikeLog);
            document.DispatchSpikeTrace.AddRange(_dispatchSpikeTrace);
            return document;
        }
    }

    public bool TryImportNetworkState(NetworkStateDocument document, out string? error)
        => TryImportNetworkState(document, out _, out error);

    public bool TryImportNetworkState(NetworkStateDocument document, out NetworkImportReport importReport, out string? error)
    {
        lock (_gate)
        {
            importReport = new NetworkImportReport
            {
                SourceSchemaVersion = document?.SchemaVersion ?? 0,
                ImportedSchemaVersion = document?.SchemaVersion ?? 0
            };
            error = null;
            if (document is null)
            {
                error = "Network state document is required.";
                return false;
            }

            if (!TryMigrateNetworkStateDocument(document, importReport, out error))
            {
                return false;
            }

            if (document.SchemaVersion <= 0 || document.SchemaVersion > NetworkStateDocument.CurrentSchemaVersion)
            {
                error = $"Unsupported schema version {document.SchemaVersion}.";
                return false;
            }

            if (document.ExportedTickWallClockUnixMs <= 0)
            {
                document.ExportedTickWallClockUnixMs = document.ExportedAtUnixMs > 0
                    ? document.ExportedAtUnixMs
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                importReport.DefaultsApplied.Add("exportedTickWallClockUnixMs");
            }

            if (string.IsNullOrWhiteSpace(document.ExportFingerprint))
            {
                document.ExportFingerprint = BuildNetworkStateFingerprint(
                    document.Tick,
                    document.SimulationClockMs,
                    document.ServiceRegistry?.Count ?? 0,
                    document.ConnectivityMap?.Values.Sum(v => v?.Count ?? 0) ?? 0,
                    document.ExportedAtUnixMs);
                importReport.DefaultsApplied.Add("exportFingerprint");
            }

            Tick = Math.Max(0, document.Tick);
            SimulationClockMs = Math.Max(0, document.SimulationClockMs);
            TickDurationMs = ClampFinite(document.TickDurationMs, 0.1, 25.0, 1.0);

            var profileName = string.IsNullOrWhiteSpace(document.PerformanceProfileName)
                ? "normal"
                : document.PerformanceProfileName.Trim();
            PerformanceProfileName = profileName;

            InputGates = InputGateRuntime.Normalize(document.InputGates ?? InputGateRuntime.Default);
            var importedMetabolicPhysiology = document.MetabolicPhysiology ?? MetabolicPhysiologyRuntime.Default;
            var importedCurriculum = document.Curriculum ?? CurriculumRuntime.Default;

            MetabolicPhysiology = NormalizeMetabolicPhysiology(importedMetabolicPhysiology);
            RestoreCurriculumFromSnapshot(importedCurriculum);

            LastSnapshotTick = Math.Max(0, document.LastSnapshotTick);
            LastSnapshotSimulationMs = Math.Max(0, document.LastSnapshotSimulationMs);
            LastSnapshotWallClockUnixMs = Math.Max(0, document.LastSnapshotWallClockUnixMs);

            TotalSpontaneousGenerated = Math.Max(0, document.TotalSpontaneousGenerated);
            TotalSpontaneousDelivered = Math.Max(0, document.TotalSpontaneousDelivered);
            TotalSpontaneousDispatchErrors = Math.Max(0, document.TotalSpontaneousDispatchErrors);
            TransportStats = document.TransportStats ?? TransportRuntimeStats.Empty;

            var rhythms = Enum.GetValues<BrainRhythm>();
            foreach (var rhythm in rhythms)
            {
                OscillationPhases[rhythm] = 0;
            }

            var importedOscillationPhases = document.OscillationPhases ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in importedOscillationPhases)
            {
                if (Enum.TryParse<BrainRhythm>(pair.Key, ignoreCase: true, out var rhythm))
                {
                    OscillationPhases[rhythm] = NormalizePhase(pair.Value);
                }
            }

            var importedServiceRegistry = document.ServiceRegistry ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (importedServiceRegistry.Count > 0)
            {
                ServiceRegistry.Clear();
                foreach (var pair in importedServiceRegistry)
                {
                    if (Enum.TryParse<StructureId>(pair.Key, ignoreCase: true, out var structureId) &&
                        !string.IsNullOrWhiteSpace(pair.Value))
                    {
                        ServiceRegistry[structureId] = pair.Value;
                    }
                }
            }

            var importedConnectivityMap = document.ConnectivityMap ?? new Dictionary<string, List<SynapticConnection>>(StringComparer.OrdinalIgnoreCase);
            if (importedConnectivityMap.Count > 0)
            {
                ConnectivityMap.Clear();
                foreach (var pair in importedConnectivityMap)
                {
                    if (!Enum.TryParse<StructureId>(pair.Key, ignoreCase: true, out var structureId))
                    {
                        continue;
                    }

                    var connections = (pair.Value ?? [])
                        .Where(connection => connection is not null)
                        .ToList();
                    ConnectivityMap[structureId] = connections;
                }
            }

            ServiceTelemetry.Clear();
            var importedServiceTelemetry = document.ServiceTelemetry ?? new Dictionary<string, ServiceRuntimeTelemetry>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in importedServiceTelemetry)
            {
                if (Enum.TryParse<StructureId>(pair.Key, ignoreCase: true, out var structureId))
                {
                    ServiceTelemetry[structureId] = pair.Value;
                }
            }




            ReplaceRuntimeLogQueue(_outputLog, document.OutputLog ?? []);
            ReplaceRuntimeLogQueue(_spikeLog, document.SpikeLog ?? []);
            ReplaceDispatchTraceQueue(_dispatchSpikeTrace, document.DispatchSpikeTrace ?? []);
            RebuildDispatchTraceStatsLocked();
            importReport.ImportedSchemaVersion = document.SchemaVersion;
            importReport.ImportedTick = Tick;
            importReport.ImportedSimulationMs = SimulationClockMs;
            importReport.ImportedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            importReport.ExportFingerprint = document.ExportFingerprint;
            importReport.ExportedTickWallClockUnixMs = document.ExportedTickWallClockUnixMs;

            AppendLog(
                _outputLog,
                $"Network state import applied (schema {document.SchemaVersion}, migrated={(importReport.Migrated ? "yes" : "no")}, defaults={importReport.DefaultsApplied.Count}, warnings={importReport.Warnings.Count}).");
            return true;
        }
    }

    private static bool TryMigrateNetworkStateDocument(NetworkStateDocument document, NetworkImportReport importReport, out string? error)
    {
        error = null;
        if (document.SchemaVersion <= 0)
        {
            error = $"Unsupported schema version {document.SchemaVersion}.";
            return false;
        }

        while (document.SchemaVersion < NetworkStateDocument.CurrentSchemaVersion)
        {
            switch (document.SchemaVersion)
            {
                case 1:
                {
                    MigrateNetworkStateV1ToV2(document, importReport);
                    document.SchemaVersion = 2;
                    importReport.Migrated = true;
                    importReport.MigrationSteps.Add("v1->v2");
                    break;
                }
                case 2:
                {
                    document.MetabolicPhysiology = MetabolicPhysiologyRuntime.Default;
                    document.SchemaVersion = 3;
                    importReport.Migrated = true;
                    importReport.MigrationSteps.Add("v2->v3");
                    importReport.DefaultsApplied.Add("metabolicPhysiology");
                    importReport.Warnings.Add(
                        "Legacy sleep-memory overlay state was discarded; neuronal sleep circuits now hold sole authority.");
                    break;
                }
                default:
                    error = $"No migration path from schema version {document.SchemaVersion} to {NetworkStateDocument.CurrentSchemaVersion}.";
                    return false;
            }
        }

        return true;
    }

    private static void MigrateNetworkStateV1ToV2(NetworkStateDocument document, NetworkImportReport importReport)
    {
        if (document.ExportedTickWallClockUnixMs <= 0)
        {
            document.ExportedTickWallClockUnixMs = document.ExportedAtUnixMs > 0
                ? document.ExportedAtUnixMs
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            importReport.DefaultsApplied.Add("exportedTickWallClockUnixMs");
        }

        if (string.IsNullOrWhiteSpace(document.ExportFingerprint))
        {
            document.ExportFingerprint = BuildNetworkStateFingerprint(
                document.Tick,
                document.SimulationClockMs,
                document.ServiceRegistry?.Count ?? 0,
                document.ConnectivityMap?.Values.Sum(v => v?.Count ?? 0) ?? 0,
                document.ExportedAtUnixMs);
            importReport.DefaultsApplied.Add("exportFingerprint");
        }

        if (document.TransportStats is null)
        {
            document.TransportStats = TransportRuntimeStats.Empty;
            importReport.DefaultsApplied.Add("transportStats");
        }

        importReport.Warnings.Add("Imported legacy network-state schema; defaults were applied for v2 fields.");
    }

    private static string BuildNetworkStateFingerprint(long tick, double simulationClockMs, int serviceCount, int projectionCount, long exportedAtUnixMs)
    {
        var sim = double.IsFinite(simulationClockMs) ? simulationClockMs : 0.0;
        return $"dnne-v3:{Math.Max(0, tick)}:{sim:0.000}:{Math.Max(0, serviceCount)}:{Math.Max(0, projectionCount)}:{Math.Max(0, exportedAtUnixMs)}";
    }

    private sealed record BiologicalValidationMetrics(
        int StructureCount,
        int SourcesWithOutbound,
        int TargetsWithInbound,
        int ProjectionCount,
        int FeedbackProjectionCount,
        string[] MissingAsSource,
        string[] MissingAsTarget,
        string[] MissingNeurotransmitters,
        Dictionary<string, bool> RequiredPathways);

    private static void ReplaceRuntimeLogQueue(Queue<RuntimeLogEntry> queue, List<RuntimeLogEntry> source)
    {
        queue.Clear();
        if (source.Count == 0)
        {
            return;
        }

        var start = Math.Max(0, source.Count - MaxRuntimeLogEntries);
        for (var i = start; i < source.Count; i++)
        {
            queue.Enqueue(source[i]);
        }
    }

    private static void ReplaceDispatchTraceQueue(Queue<DispatchedSpikeTrace> queue, List<DispatchedSpikeTrace> source)
    {
        queue.Clear();
        if (source.Count == 0)
        {
            return;
        }

        var start = Math.Max(0, source.Count - MaxDispatchTraceEntries);
        for (var i = start; i < source.Count; i++)
        {
            queue.Enqueue(source[i]);
        }
    }

    private static double NormalizePhase(double phase)
    {
        if (double.IsNaN(phase) || double.IsInfinity(phase))
        {
            return 0;
        }

        var twoPi = Math.PI * 2.0;
        var normalized = phase % twoPi;
        if (normalized < 0)
        {
            normalized += twoPi;
        }

        return normalized;
    }

    private static double ClampFinite(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static string ClassifyProjectionClass(string projectionType)
    {
        if (string.IsNullOrWhiteSpace(projectionType))
        {
            return "unspecified";
        }

        var p = projectionType.ToLowerInvariant();
        if (p.Contains("dopamin") || p.Contains("seroton") || p.Contains("cholin") || p.Contains("norepineph") || p.Contains("neuromod"))
        {
            return "neuromodulatory";
        }

        if (p.Contains("thalam") || p.Contains("reticular") || p.Contains("pulvinar"))
        {
            return "thalamic";
        }

        if (p.Contains("stri") || p.Contains("pallid") || p.Contains("stn") || p.Contains("snr") || p.Contains("snc") || p.Contains("basal"))
        {
            return "basal_ganglia";
        }

        if (p.Contains("hippocamp") || p.Contains("entorh") || p.Contains("dentate") || p.Contains("perforant") || p.Contains("subiculum") || p.Contains("ca"))
        {
            return "hippocampal";
        }

        if (p.Contains("cerebell") || p.Contains("oliv") || p.Contains("pont"))
        {
            return "cerebellar";
        }

        if (p.Contains("amyg") || p.Contains("limbic") || p.Contains("hypothalam") || p.Contains("accumbens"))
        {
            return "limbic";
        }

        if (p.Contains("motor") || p.Contains("premotor") || p.Contains("corticospinal"))
        {
            return "motor";
        }

        if (p.Contains("callosal") || p.Contains("association") || p.Contains("cortico"))
        {
            return "cortical_association";
        }

        return "misc";
    }

    private static float MoveTowards(float value, float target, float maxDelta)
    {
        var delta = target - value;
        if (Math.Abs(delta) <= maxDelta)
        {
            return target;
        }

        return value + (Math.Sign(delta) * Math.Max(0f, maxDelta));
    }

    private static MetabolicPhysiologyRuntime NormalizeMetabolicPhysiology(MetabolicPhysiologyRuntime runtime)
        => runtime with
        {
            AtpBudget = Math.Clamp(runtime.AtpBudget, 0f, Math.Max(0.0001f, runtime.MaxAtpBudget)),
            HomeostaticPressure = Math.Clamp(
                runtime.HomeostaticPressure,
                0f,
                Math.Max(0.0001f, runtime.MaxHomeostaticPressure)),
            SleepTicks = Math.Max(0, runtime.SleepTicks),
            WakeTicks = Math.Max(0, runtime.WakeTicks),
            SleepEpisodes = Math.Max(0, runtime.SleepEpisodes),
            LastTransitionTick = Math.Max(0, runtime.LastTransitionTick)
        };


    private void AppendLog(Queue<RuntimeLogEntry> queue, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var entry = new RuntimeLogEntry(
            Tick,
            SimulationClockMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            message.Trim());
        queue.Enqueue(entry);
        while (queue.Count > MaxRuntimeLogEntries)
        {
            queue.Dequeue();
        }
    }

    private static string NormalizeNeuronIdForHemisphere(string? neuronId, string hemisphere)
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

        var hemi = string.IsNullOrWhiteSpace(hemisphere) ? "M" : hemisphere.Trim();
        return $"{hemi}:{trimmed}";
    }
}

internal sealed class SnapshotStore : ISnapshotSink, IAsyncDisposable
{
    private readonly ConcurrentQueue<BrainSnapshot> _snapshots = new();
    private readonly int _maxInMemorySnapshots;
    private readonly string _storePath;
    private readonly long _maxFileBytes;
    private readonly int _retainedFiles;
    private readonly int _flushEvery;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private FileStream _writeStream;
    private StreamWriter _writeWriter;
    private BrainSnapshot? _latest;
    private int _pendingWritesSinceFlush;
    private int _snapshotCount;
    private int _disposed;

    public SnapshotStore(IConfiguration configuration)
    {
        _maxInMemorySnapshots = Math.Clamp(configuration.GetValue<int>("SnapshotStore:MaxInMemorySnapshots", 1024), 32, 32_768);
        _maxFileBytes = Math.Clamp(configuration.GetValue<long>("SnapshotStore:MaxFileBytes", 256L * 1024 * 1024), 1024 * 1024, 16L * 1024 * 1024 * 1024);
        _retainedFiles = Math.Clamp(configuration.GetValue<int>("SnapshotStore:RetainedFiles", 4), 1, 32);
        _flushEvery = Math.Clamp(configuration.GetValue<int>("SnapshotStore:FlushEvery", 20), 1, 1000);
        var configuredPath = configuration["SnapshotStore:Path"];
        _storePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeuralResonanceEngine",
                "snapshots",
                "snapshots.ndjson")
            : Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath) ?? AppContext.BaseDirectory);
        (_writeStream, _writeWriter) = OpenWriters();
    }

    private (FileStream Stream, StreamWriter Writer) OpenWriters()
    {
        var stream = new FileStream(
            _storePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 64 * 1024,
            useAsync: true);
        var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 64 * 1024, leaveOpen: true);
        return (stream, writer);
    }

    public async ValueTask AppendAsync(BrainSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var line = JsonSerializer.Serialize(snapshot, DnneJsonContext.Default.BrainSnapshot);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_writeStream.Length >= _maxFileBytes)
            {
                await RotateAsync();
            }
            await _writeWriter.WriteLineAsync(line);
            _pendingWritesSinceFlush++;
            if (_pendingWritesSinceFlush >= _flushEvery)
            {
                await _writeWriter.FlushAsync(CancellationToken.None);
                _pendingWritesSinceFlush = 0;
            }

            _snapshots.Enqueue(snapshot);
            Volatile.Write(ref _latest, snapshot);
            Interlocked.Increment(ref _snapshotCount);
            while (Volatile.Read(ref _snapshotCount) > _maxInMemorySnapshots && _snapshots.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _snapshotCount);
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public IReadOnlyList<BrainSnapshot> GetAll() => _snapshots.ToArray();
    public BrainSnapshot? GetLatest() => Volatile.Read(ref _latest);

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            await CloseWritersAsync();
            try
            {
                File.WriteAllText(_storePath, string.Empty);
                for (var index = 1; index <= _retainedFiles; index++)
                {
                    File.Delete($"{_storePath}.{index}");
                }
            }
            finally
            {
                (_writeStream, _writeWriter) = OpenWriters();
            }

            await _writeWriter.WriteLineAsync($"{{\"event\":\"simulation_restart\",\"utc_ms\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}");
            await _writeWriter.FlushAsync(CancellationToken.None);
            _pendingWritesSinceFlush = 0;

            while (_snapshots.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _snapshotCount);
            }
            Interlocked.Exchange(ref _snapshotCount, 0);
            Volatile.Write(ref _latest, null);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task RotateAsync()
    {
        await CloseWritersAsync();
        try
        {
            File.Delete($"{_storePath}.{_retainedFiles}");
            for (var index = _retainedFiles - 1; index >= 1; index--)
            {
                var source = $"{_storePath}.{index}";
                if (File.Exists(source))
                {
                    File.Move(source, $"{_storePath}.{index + 1}", overwrite: true);
                }
            }
            if (File.Exists(_storePath))
            {
                File.Move(_storePath, $"{_storePath}.1", overwrite: true);
            }
        }
        finally
        {
            (_writeStream, _writeWriter) = OpenWriters();
        }
        _pendingWritesSinceFlush = 0;
    }

    private async ValueTask CloseWritersAsync()
    {
        await _writeWriter.FlushAsync(CancellationToken.None);
        _writeWriter.Dispose();
        await _writeStream.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _writeGate.WaitAsync();
        try
        {
            await CloseWritersAsync();
            _pendingWritesSinceFlush = 0;
        }
        finally
        {
            _writeGate.Release();
        }
    }
}

internal sealed class ServicePublishBuffer
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, PublishedStepMessage>> _messagesByTick = new();
    private long _lastPrunedTick = -1;

    public void Publish(PublishedStepMessage message)
    {
        if (message.Step?.Ack is null || string.IsNullOrWhiteSpace(message.InstanceKey))
        {
            return;
        }

        var tick = message.Step.Ack.Tick;
        if (tick <= 0)
        {
            return;
        }

        var bucket = _messagesByTick.GetOrAdd(
            tick,
            _ => new ConcurrentDictionary<string, PublishedStepMessage>(StringComparer.OrdinalIgnoreCase));
        bucket[message.InstanceKey.Trim()] = message;
        PruneOldTicks(tick);
    }

    public async Task<IReadOnlyDictionary<string, PublishedStepMessage>> WaitForTickAsync(
        long tick,
        IReadOnlyCollection<string> expectedInstanceKeys,
        TimeSpan timeout,
        TimeSpan settleWindow,
        CancellationToken cancellationToken)
    {
        var expected = expectedInstanceKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (expected.Count == 0)
        {
            return new Dictionary<string, PublishedStepMessage>(StringComparer.OrdinalIgnoreCase);
        }

        var deadline = DateTime.UtcNow + timeout;
        var snapshot = new Dictionary<string, PublishedStepMessage>(StringComparer.OrdinalIgnoreCase);
        var startedAt = DateTime.UtcNow;
        var lastGrowthAt = startedAt;
        var previousCount = -1;

        while (!cancellationToken.IsCancellationRequested)
        {
            snapshot.Clear();
            if (_messagesByTick.TryGetValue(tick, out var bucket))
            {
                foreach (var key in expected)
                {
                    if (bucket.TryGetValue(key, out var message))
                    {
                        snapshot[key] = message;
                    }
                }
            }

            var now = DateTime.UtcNow;
            if (snapshot.Count != previousCount)
            {
                previousCount = snapshot.Count;
                lastGrowthAt = now;
            }

            if (snapshot.Count >= expected.Count || now >= deadline)
            {
                return new Dictionary<string, PublishedStepMessage>(snapshot, StringComparer.OrdinalIgnoreCase);
            }

            // Fast path: if no additional publishes arrive for a short settle window, proceed with what we have.
            if (snapshot.Count > 0 && (now - lastGrowthAt) >= settleWindow && (now - startedAt) >= TimeSpan.FromMilliseconds(2))
            {
                return new Dictionary<string, PublishedStepMessage>(snapshot, StringComparer.OrdinalIgnoreCase);
            }

            var remaining = deadline - now;
            var delay = remaining <= TimeSpan.FromMilliseconds(2) ? remaining : TimeSpan.FromMilliseconds(2);
            if (delay <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(delay, cancellationToken);
        }

        return new Dictionary<string, PublishedStepMessage>(snapshot, StringComparer.OrdinalIgnoreCase);
    }

    public void Clear()
    {
        _messagesByTick.Clear();
        Interlocked.Exchange(ref _lastPrunedTick, -1);
    }

    private void PruneOldTicks(long currentTick)
    {
        var lastPruned = Volatile.Read(ref _lastPrunedTick);
        if (currentTick - lastPruned < 64)
        {
            return;
        }

        // Atomically claim the prune window so only one publisher iterates Keys.
        // If another publisher already advanced past our snapshot, back off.
        if (Interlocked.CompareExchange(ref _lastPrunedTick, currentTick, lastPruned) != lastPruned)
        {
            return;
        }

        var pruneBefore = currentTick - 256;
        foreach (var tick in _messagesByTick.Keys)
        {
            if (tick < pruneBefore)
            {
                _messagesByTick.TryRemove(tick, out _);
            }
        }
    }
}

internal sealed class TickCoordinator(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHttpClientFactory clientFactory,
    RuntimeInstanceCatalog runtimeCatalog,
    StructureProcessSupervisor structureSupervisor,
    RuntimePerformanceProfileState performanceProfiles,
    AutoProfileRuntimeState autoProfileState,
    ServicePublishBuffer publishBuffer,
    LanguageBackoffPolicy languageBackoffPolicy,
    NeuronalMotorControlState neuronalMotorControl,
    NeuronalMotorPopulationWindow neuronalMotorPopulationWindow,
    NeuronalPerceptionRuntime neuronalPerception,
    NeuronalMemoryRuntime neuronalMemory,
    NeuronalAttentionWorkspaceRuntime neuronalAttentionWorkspace,
    NeuronalVisualAttentionRuntime neuronalVisualAttention,
    NeuronalSleepConsolidationRuntime neuronalSleepConsolidation,
    NeuronalLanguageGroundingRuntime neuronalLanguageGrounding,
    NeuronalAffectValuationRuntime neuronalAffectValuation,
    NeuronalExecutiveRuntime neuronalExecutive,
    NeuronalCognitionAuthorityRuntime neuronalCognitionAuthority,
    ILogger<TickCoordinator> logger) : BackgroundService
{
    private readonly Random _noiseRandom = new(173);
    private readonly object _noiseGate = new();
    private readonly TransportCapabilityCache _transportCapabilities = new();

    // Bidi-stream sessions for spike transport. Populated only when
    // NRE_USE_GRPC_BIDI_STREAM=1; otherwise the dictionary stays empty and the
    // unary gRPC + HTTP fallback path is unchanged.
    private readonly ConcurrentDictionary<string, GrpcSpikeStreamSession> _streamSessions = new(StringComparer.OrdinalIgnoreCase);

    private enum HttpSingleFallbackMode
    {
        Binary,
        Json
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var state = scope.ServiceProvider.GetRequiredService<SimulationState>();
        var snapshotStore = scope.ServiceProvider.GetRequiredService<SnapshotStore>();

        var tickDurationMs = configuration.GetValue<double>("TickDurationMs", 1.0);
        var wallClockDelayMs = Math.Max(0.0, configuration.GetValue<double>("WallClockDelayMs", tickDurationMs));
        var snapshotEvery = Math.Max(1, configuration.GetValue<int>("SnapshotEveryNTicks", 10));
        var tickTimeoutMs = configuration.GetValue<int>("TickTimeoutMs", 300);
        var maxSpikesPerDispatchRequest = Math.Clamp(configuration.GetValue<int>("MaxSpikesPerDispatchRequest", 192), 16, 8192);
        var degradedModeIgnoreOffline = configuration.GetValue<bool>("DegradedModeIgnoreOffline", true);
        var degradedLogEveryTicks = Math.Max(1, configuration.GetValue<int>("DegradedLogEveryTicks", 100));
        var spontaneousNoiseEnabled = configuration.GetValue<bool>("SpontaneousNoise:Enabled", true);
        var spontaneousNoiseScale = Math.Clamp(configuration.GetValue<double>("SpontaneousNoise:Scale", 1.0), 0.0, 4.0);
        var spontaneousNoiseBenchmarkMode = configuration.GetValue<bool>("SpontaneousNoise:BenchmarkMode", false);
        var spontaneousNoiseForceFallback = configuration.GetValue<bool>("SpontaneousNoise:EnableForcedFallback", false);
        var spontaneousNoiseMaxEventsPerTick = Math.Clamp(configuration.GetValue<int>("SpontaneousNoise:MaxEventsPerTick", 48), 1, 4096);
        const int NeuralStarvationAutoRestoreTicks = 240;
        var perceptionLanguageBridgeEnabled = configuration.GetValue<bool>("PerceptionLanguageBridge:Enabled", true);
        var perceptionLanguageCooldownTicks = Math.Clamp(configuration.GetValue<int>("PerceptionLanguageBridge:CooldownTicks", 14), 1, 10_000);
        var perceptionLanguageMinVisualFocusConfidence = Math.Clamp(configuration.GetValue<double>("PerceptionLanguageBridge:MinVisualFocusConfidence", 0.58), 0.0, 1.0);
        var perceptionLanguageMinAuditoryRateHz = Math.Clamp(configuration.GetValue<double>("PerceptionLanguageBridge:MinAuditoryRateHz", 7.5), 0.0, 200.0);
        var perceptionLanguageBurstPerToken = Math.Clamp(configuration.GetValue<int>("PerceptionLanguageBridge:BurstPerToken", 2), 1, 8);
        var perceptionLanguageMaxTokens = Math.Clamp(configuration.GetValue<int>("PerceptionLanguageBridge:MaxTokens", 4), 1, 16);
        if (spontaneousNoiseBenchmarkMode)
        {
            spontaneousNoiseForceFallback = true;
        }
        var useGrpcSpikeTransport = configuration.GetValue<bool>("UseGrpcSpikeTransport", true);
        var useHttpSpikeTransportFallback = configuration.GetValue<bool>("UseHttpSpikeTransportFallback", true);
        var autoHealEnabled = configuration.GetValue<bool>("ServiceAutoHeal:Enabled", true);
        var autoHealFailureThreshold = Math.Clamp(configuration.GetValue<int>("ServiceAutoHeal:FailureThreshold", 4), 1, 12);
        var autoHealCooldownMs = Math.Clamp(configuration.GetValue<int>("ServiceAutoHeal:CooldownMs", 12000), 1000, 120000);
        var autoHealMaxRestartsPerTick = Math.Clamp(configuration.GetValue<int>("ServiceAutoHeal:MaxRestartsPerTick", 3), 1, 16);
        var autoHealWarmupTicks = Math.Clamp(configuration.GetValue<int>("ServiceAutoHeal:WarmupTicks", 25), 0, 5000);
        var startupWarmupTicks = Math.Clamp(configuration.GetValue<int>("StartupWarmupTicks", 80), 0, 20000);
        var startupWarmupAckTimeoutMs = Math.Clamp(configuration.GetValue<int>("StartupWarmupAckTimeoutMs", 1400), 500, 60000);
        var startupWarmupIoTimeoutMs = Math.Clamp(configuration.GetValue<int>("StartupWarmupIoTimeoutMs", 3200), 1000, 120000);
        var autoProfile = autoProfileState.GetSnapshot();
        var autoProfileGeneration = autoProfileState.Generation;
        var autoProfileEnabled = autoProfile.Enabled;
        var autoProfileAllowRecovery = autoProfile.AllowRecovery;
        var autoProfileWarmupTicks = autoProfile.WarmupTicks;
        var autoProfileManualHoldTicks = autoProfile.ManualHoldTicks;
        var autoProfileDegradeNonOkRatio = autoProfile.DegradeNonOkRatio;
        var autoProfileDegradeAckLatencyMs = autoProfile.DegradeAckLatencyMs;
        var autoProfileDegradeSnapshotAgeTicks = autoProfile.DegradeSnapshotAgeTicks;
        var autoProfileDegradeConsecutiveTicks = autoProfile.DegradeConsecutiveTicks;
        var autoProfileRecoveryNonOkRatio = autoProfile.RecoveryNonOkRatio;
        var autoProfileRecoveryAckLatencyMs = autoProfile.RecoveryAckLatencyMs;
        var autoProfileRecoverySnapshotAgeTicks = autoProfile.RecoverySnapshotAgeTicks;
        var autoProfileRecoveryConsecutiveTicks = autoProfile.RecoveryConsecutiveTicks;
        if (useGrpcSpikeTransport && !useHttpSpikeTransportFallback)
        {
            logger.LogWarning("UseGrpcSpikeTransport=true with HTTP fallback disabled can black-hole spikes on transport incompatibilities; forcing HTTP fallback on.");
            useHttpSpikeTransportFallback = true;
        }
        var restartStructureServicesOnSimRestart = configuration.GetValue<bool>("SimulationRestart:RestartStructureServices", true);

        var tuning = performanceProfiles.GetSnapshot();
        var lastPerfGeneration = performanceProfiles.Generation;
        var tickAckTimeoutMs = tuning.TickAckTimeoutMs;
        var tickIoTimeoutMs = tuning.TickIoTimeoutMs;
        var tickPublishWaitMs = tuning.TickPublishWaitMs;
        var tickPublishSettleMs = tuning.TickPublishSettleMs;
        var maxTickRequestConcurrency = tuning.MaxTickRequestConcurrency;
        var topQueryEveryNTicks = tuning.TopQueryEveryNTicks;
        var maxTopQueriesPerTick = tuning.MaxTopQueriesPerTick;
        snapshotEvery = tuning.SnapshotEveryNTicks;
        var maxSpikeDispatchPerServicePerTick = tuning.MaxSpikeDispatchPerServicePerTick;
        var maxSpikeDispatchTotalPerTick = tuning.MaxSpikeDispatchTotalPerTick;
        var maxDispatchConcurrency = tuning.MaxDispatchConcurrency;
        var useDirectStepFastPath = tuning.UseDirectStepFastPath;
        var effectiveTickAckTimeoutMs = tickAckTimeoutMs;
        var effectiveTickIoTimeoutMs = tickIoTimeoutMs;
        var effectiveTickPublishWaitMs = tickPublishWaitMs;
        var effectiveTickPublishSettleMs = tickPublishSettleMs;
        state.UpdatePerformanceProfile(tuning.ProfileName);

        var configuredInstances = ParseConfiguredServiceInstances(configuration, logger);
        var registry = BuildServiceRegistry(configuration, configuredInstances, logger);

        var connectivityFile = configuration.GetValue<string>("ConnectivityFile") ?? "connectivity\\dnne-connectivity.json";
        var connectivityPath = ResolvePathFromBaseOrAncestors(connectivityFile);
        var connectivityJson = await File.ReadAllTextAsync(connectivityPath, stoppingToken);
        var connectivityRules = JsonSerializer.Deserialize<List<ConnectivityRuleJson>>(
            connectivityJson,
            CachedJsonOptions.CaseInsensitive) ?? [];
        var connectivity = BuildConnectivityMap(connectivityRules, logger);
        var enforceConnectivityCoverage = configuration.GetValue<bool>("Connectivity:EnforceBidirectionalCoverage", true);
        var enforceBiologicalSemantics = configuration.GetValue<bool>("Connectivity:EnforceBiologicalSemantics", true);
        AuditConnectivityCoverage(connectivity, enforceConnectivityCoverage, logger);
        AuditBiologicalSemantics(connectivity, enforceBiologicalSemantics, logger);
        var serviceInstances = BuildServiceInstances(registry, configuredInstances, configuration, logger);
        _transportCapabilities.PruneTo(serviceInstances.Select(instance => instance.InstanceKey));
        var instancesByStructure = serviceInstances
            .GroupBy(i => i.StructureId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var instanceKeysByStructure = serviceInstances
            .GroupBy(i => i.StructureId)
            .ToDictionary(g => g.Key, g => g.Select(i => i.InstanceKey).ToArray());

        state.Configure(tickDurationMs, registry, connectivity);
        state.SetInputGates(new InputGateRuntime(
            AvatarVisionEnabled: true,
            SpontaneousSpikingEnabled: spontaneousNoiseEnabled));
        runtimeCatalog.SetKnownInstances(serviceInstances);
        runtimeCatalog.SetLiveInstances([]);

        var transportClients = InitializeTransportClients(serviceInstances, useGrpcSpikeTransport);
        var clients = transportClients.Clients;
        var grpcChannels = transportClients.GrpcChannels;
        var grpcSpikeTransports = transportClients.GrpcSpikeTransports;
        var serviceHealth = transportClients.ServiceHealth;
        var grpcTransportActive = useGrpcSpikeTransport && grpcSpikeTransports.Count > 0;

        state.AppendOutputLog(
            grpcTransportActive
                ? $"Spike transport: gRPC primary across {grpcSpikeTransports.Count} structure endpoints, HTTP fallback {(useHttpSpikeTransportFallback ? "enabled" : "disabled")}."
                : useGrpcSpikeTransport
                    ? "Spike transport: HTTP primary; gRPC registration skipped for current cleartext structure endpoints, HTTP fallback enabled."
                    : $"Spike transport: HTTP primary, HTTP fallback {(useHttpSpikeTransportFallback ? "enabled" : "disabled")}.");

        var startupResult = await structureSupervisor.EnsureServicesOnlineAsync(serviceInstances, tickTimeoutMs, stoppingToken);
        ApplySupervisorServiceHealthResult(
            startupResult,
            serviceHealth,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            state.Tick,
            "startup");
        var startupLiveInstances = SelectHealthyInstancesFromSupervisorResult(serviceInstances, startupResult);
        runtimeCatalog.SetLiveInstances(startupLiveInstances);
        state.AppendOutputLog(
            $"Structure startup health: healthy={startupResult.Healthy}/{startupResult.Requested}, launched={startupResult.Restarted}, live={startupLiveInstances.Count}.");
        publishBuffer.Clear();
        var dispatchSemaphore = new SemaphoreSlim(maxDispatchConcurrency, maxDispatchConcurrency);
        var tickRequestSemaphore = new SemaphoreSlim(maxTickRequestConcurrency, maxTickRequestConcurrency);
        var autoHealLastRestartByInstance = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        Task<RestartServiceResult>? autoHealRestartTask = null;
        try
        {
            var topQueryCursor = 0;
            var lastSeenRestartGeneration = state.GetRestartGeneration();
            var lastNonOkServiceCount = -1;
            var lastServiceHealthDiskLogTick = long.MinValue;
            var lastLiveCatalogInstanceCount = startupLiveInstances.Count;
            var autoProfileDegradeStreak = 0;
            var autoProfileRecoveryStreak = 0;
            var autoProfileDowngradedFromUltra = false;
            var autoProfileManualHoldUntilTick = 0L;
            long? pendingAutoProfileGeneration = null;
            var tickWallSamples = new Queue<double>(256);
            var lastPerceptionLanguageTick = long.MinValue / 4;
            var lastAutoProfileSignal = "none";
            var spontaneousGateStarvationTicks = 0;
            var tickParticipantCursor = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                var tickWallStopwatch = Stopwatch.StartNew();
                var perfGeneration = performanceProfiles.Generation;
                if (perfGeneration != lastPerfGeneration)
                {
                    var profileChangeWasAuto = pendingAutoProfileGeneration.HasValue && pendingAutoProfileGeneration.Value == perfGeneration;
                    tuning = performanceProfiles.GetSnapshot();
                    lastPerfGeneration = perfGeneration;
                    tickAckTimeoutMs = tuning.TickAckTimeoutMs;
                    tickIoTimeoutMs = tuning.TickIoTimeoutMs;
                    tickPublishWaitMs = tuning.TickPublishWaitMs;
                    tickPublishSettleMs = tuning.TickPublishSettleMs;
                    topQueryEveryNTicks = tuning.TopQueryEveryNTicks;
                    maxTopQueriesPerTick = tuning.MaxTopQueriesPerTick;
                    snapshotEvery = tuning.SnapshotEveryNTicks;
                    maxSpikeDispatchPerServicePerTick = tuning.MaxSpikeDispatchPerServicePerTick;
                    maxSpikeDispatchTotalPerTick = tuning.MaxSpikeDispatchTotalPerTick;
                    useDirectStepFastPath = tuning.UseDirectStepFastPath;
                    effectiveTickAckTimeoutMs = tickAckTimeoutMs;
                    effectiveTickIoTimeoutMs = tickIoTimeoutMs;
                    effectiveTickPublishWaitMs = tickPublishWaitMs;
                    effectiveTickPublishSettleMs = tickPublishSettleMs;
                    state.UpdatePerformanceProfile(tuning.ProfileName);
                    pendingAutoProfileGeneration = null;

                    if (!profileChangeWasAuto)
                    {
                        autoProfileDowngradedFromUltra = false;
                        autoProfileDegradeStreak = 0;
                        autoProfileRecoveryStreak = 0;
                        autoProfileManualHoldUntilTick = Math.Max(autoProfileManualHoldUntilTick, state.Tick + autoProfileManualHoldTicks);
                    }

                    if (tuning.MaxTickRequestConcurrency != maxTickRequestConcurrency)
                    {
                        tickRequestSemaphore.Dispose();
                        maxTickRequestConcurrency = tuning.MaxTickRequestConcurrency;
                        tickRequestSemaphore = new SemaphoreSlim(maxTickRequestConcurrency, maxTickRequestConcurrency);
                    }

                    if (tuning.MaxDispatchConcurrency != maxDispatchConcurrency)
                    {
                        dispatchSemaphore.Dispose();
                        maxDispatchConcurrency = tuning.MaxDispatchConcurrency;
                        dispatchSemaphore = new SemaphoreSlim(maxDispatchConcurrency, maxDispatchConcurrency);
                    }

                    state.AppendOutputLog(
                        $"Performance profile applied: {tuning.ProfileName} " +
                        $"(tickReq={maxTickRequestConcurrency}, dispatch={maxDispatchConcurrency}, " +
                        $"spikes/svc={maxSpikeDispatchPerServicePerTick}, spikes/tick={maxSpikeDispatchTotalPerTick}, " +
                        $"snapshotEvery={snapshotEvery}, directStep={useDirectStepFastPath}).");
                }

                var observedAutoProfileGeneration = autoProfileState.Generation;
                if (observedAutoProfileGeneration != autoProfileGeneration)
                {
                    autoProfile = autoProfileState.GetSnapshot();
                    autoProfileGeneration = observedAutoProfileGeneration;
                    autoProfileEnabled = autoProfile.Enabled;
                    autoProfileAllowRecovery = autoProfile.AllowRecovery;
                    autoProfileWarmupTicks = autoProfile.WarmupTicks;
                    autoProfileManualHoldTicks = autoProfile.ManualHoldTicks;
                    autoProfileDegradeNonOkRatio = autoProfile.DegradeNonOkRatio;
                    autoProfileDegradeAckLatencyMs = autoProfile.DegradeAckLatencyMs;
                    autoProfileDegradeSnapshotAgeTicks = autoProfile.DegradeSnapshotAgeTicks;
                    autoProfileDegradeConsecutiveTicks = autoProfile.DegradeConsecutiveTicks;
                    autoProfileRecoveryNonOkRatio = autoProfile.RecoveryNonOkRatio;
                    autoProfileRecoveryAckLatencyMs = autoProfile.RecoveryAckLatencyMs;
                    autoProfileRecoverySnapshotAgeTicks = autoProfile.RecoverySnapshotAgeTicks;
                    autoProfileRecoveryConsecutiveTicks = autoProfile.RecoveryConsecutiveTicks;
                    autoProfileDegradeStreak = 0;
                    autoProfileRecoveryStreak = 0;
                    state.AppendOutputLog(
                        $"Auto-profile settings applied (generation {autoProfileGeneration}): enabled={autoProfileEnabled}, recovery={autoProfileAllowRecovery}, warmup={autoProfileWarmupTicks}, manualHold={autoProfileManualHoldTicks}.");
                }

                var restartGeneration = state.GetRestartGeneration();
                if (restartGeneration != lastSeenRestartGeneration)
                {
                    lastSeenRestartGeneration = restartGeneration;
                    autoHealRestartTask = await HandleSimulationRestartAsync(
                        state,
                        snapshotStore,
                        runtimeCatalog,
                        serviceInstances,
                        serviceHealth,
                        autoHealLastRestartByInstance,
                        tickTimeoutMs,
                        restartStructureServicesOnSimRestart,
                        stoppingToken);
                    topQueryCursor = 0;
                }

                autoHealRestartTask = await ObserveCompletedAutoHealRestartAsync(
                    autoHealRestartTask,
                    serviceHealth,
                    state);

                var tickSignal = state.AdvanceClockAndCreateTickSignal();
                var healthNowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var activePathways = new ConcurrentDictionary<(StructureId Source, StructureId Target, NTEnum Nt), int>();
                var snapshots = new List<StructureSnapshot>();
                var availableServices = serviceInstances.Where(i => serviceHealth[i.InstanceKey].CanAttempt(healthNowMs)).ToList();
                var previousTransport = state.TransportStats;
                var (healthTotalServices, healthNonOkServices) = state.GetServiceHealthCounts();
                var nonOkRatio = healthTotalServices <= 0
                    ? 0.0
                    : healthNonOkServices / (double)Math.Max(1, healthTotalServices);
                var ackEwmaSamples = availableServices
                    .Select(s => serviceHealth[s.InstanceKey].CreateTelemetry(healthNowMs).AckLatencyEwmaMs)
                    .Where(v => v > 0.001)
                    .ToList();
                var ackLatencyEwmaMs = ackEwmaSamples.Count == 0 ? 0.0 : ackEwmaSamples.Average();
                var snapshotAgeTicks = state.LastSnapshotTick > 0
                    ? Math.Max(0L, tickSignal.Tick - state.LastSnapshotTick)
                    : tickSignal.Tick;
                var autoProfileSignals = new List<string>(4);
                var degradeSignal = false;
                if (nonOkRatio >= autoProfileDegradeNonOkRatio)
                {
                    autoProfileSignals.Add($"nonOkRatio {nonOkRatio:0.000}>={autoProfileDegradeNonOkRatio:0.000}");
                    degradeSignal = true;
                }

                if (ackLatencyEwmaMs > 0.001 && ackLatencyEwmaMs >= autoProfileDegradeAckLatencyMs)
                {
                    autoProfileSignals.Add($"ackEwma {ackLatencyEwmaMs:0.0}>={autoProfileDegradeAckLatencyMs:0.0}ms");
                    degradeSignal = true;
                }

                if (snapshotAgeTicks >= autoProfileDegradeSnapshotAgeTicks)
                {
                    autoProfileSignals.Add($"snapshotAge {snapshotAgeTicks}>={autoProfileDegradeSnapshotAgeTicks}");
                    degradeSignal = true;
                }

                var ackPressure = ackLatencyEwmaMs <= 0.0
                    ? 0.0
                    : Math.Clamp((ackLatencyEwmaMs - (tickAckTimeoutMs * 0.25)) / (tickAckTimeoutMs * 0.85), 0.0, 1.0);
                var queuePressure = DispatchQueueRuntime.ComputePressure(previousTransport);
                if (queuePressure >= 0.18)
                {
                    autoProfileSignals.Add($"queuePressure {queuePressure:0.000}>=0.180");
                    degradeSignal = true;
                }

                var degradeSignalLabel = autoProfileSignals.Count == 0 ? "none" : string.Join("; ", autoProfileSignals);

                if (autoProfileEnabled && tickSignal.Tick >= autoProfileWarmupTicks && tickSignal.Tick >= autoProfileManualHoldUntilTick)
                {
                    if (degradeSignal)
                    {
                        autoProfileDegradeStreak++;
                        autoProfileRecoveryStreak = 0;
                    }
                    else
                    {
                        autoProfileDegradeStreak = 0;
                        if (nonOkRatio <= autoProfileRecoveryNonOkRatio &&
                            (ackLatencyEwmaMs <= autoProfileRecoveryAckLatencyMs || ackLatencyEwmaMs <= 0.001) &&
                            snapshotAgeTicks <= autoProfileRecoverySnapshotAgeTicks)
                        {
                            autoProfileRecoveryStreak++;
                        }
                        else
                        {
                            autoProfileRecoveryStreak = 0;
                        }
                    }

                    if ((tuning.ProfileName.Equals("fast", StringComparison.OrdinalIgnoreCase) ||
                         tuning.ProfileName.Equals("ultra", StringComparison.OrdinalIgnoreCase)) &&
                        autoProfileDegradeStreak >= autoProfileDegradeConsecutiveTicks)
                    {
                        var (generation, settings) = performanceProfiles.ApplyProfile("normal");
                        pendingAutoProfileGeneration = generation;
                        autoProfileDowngradedFromUltra = true;
                        autoProfileDegradeStreak = 0;
                        autoProfileRecoveryStreak = 0;
                        state.AppendOutputLog(
                            $"Auto-profile fallback to normal at tick {tickSignal.Tick}: nonOk={nonOkRatio:P0}, ackEwma={ackLatencyEwmaMs:0.0}ms, snapshotAgeTicks={snapshotAgeTicks}.");
                        logger.LogInformation(
                            "Auto-profile fallback to normal at tick {Tick} (nonOkRatio={NonOkRatio:0.000}, ackEwmaMs={AckEwma:0.0}, snapshotAgeTicks={SnapshotAge}).",
                            tickSignal.Tick,
                            nonOkRatio,
                            ackLatencyEwmaMs,
                            snapshotAgeTicks);
                        tuning = settings;
                    }
                    else if (autoProfileAllowRecovery &&
                             autoProfileDowngradedFromUltra &&
                             tuning.ProfileName.Equals("normal", StringComparison.OrdinalIgnoreCase) &&
                             autoProfileRecoveryStreak >= autoProfileRecoveryConsecutiveTicks)
                    {
                        var (generation, settings) = performanceProfiles.ApplyProfile("fast");
                        pendingAutoProfileGeneration = generation;
                        autoProfileDowngradedFromUltra = false;
                        autoProfileRecoveryStreak = 0;
                        autoProfileDegradeStreak = 0;
                        state.AppendOutputLog(
                            $"Auto-profile recovery to fast at tick {tickSignal.Tick}: nonOk={nonOkRatio:P0}, ackEwma={ackLatencyEwmaMs:0.0}ms, snapshotAgeTicks={snapshotAgeTicks}.");
                        logger.LogInformation(
                            "Auto-profile recovery to fast at tick {Tick} (nonOkRatio={NonOkRatio:0.000}, ackEwmaMs={AckEwma:0.0}, snapshotAgeTicks={SnapshotAge}).",
                            tickSignal.Tick,
                            nonOkRatio,
                            ackLatencyEwmaMs,
                            snapshotAgeTicks);
                        tuning = settings;
                    }
                }

                if (!string.Equals(lastAutoProfileSignal, degradeSignalLabel, StringComparison.Ordinal))
                {
                    lastAutoProfileSignal = degradeSignalLabel;
                    if (!string.Equals(degradeSignalLabel, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        state.AppendOutputLog($"Auto-profile signal @ tick {tickSignal.Tick}: {degradeSignalLabel}.");
                    }
                }
                var adaptivePressure = Math.Clamp((nonOkRatio * 0.52) + (ackPressure * 0.33) + (queuePressure * 0.15), 0.0, 0.92);
                var adaptiveScale = Math.Clamp(1.0 - (adaptivePressure * 0.72), 0.20, 1.0);
                var effectiveMaxSpikeDispatchPerServicePerTick = Math.Max(4, (int)Math.Round(maxSpikeDispatchPerServicePerTick * adaptiveScale));
                var effectiveMaxSpikeDispatchTotalPerTick = Math.Max(16, (int)Math.Round(maxSpikeDispatchTotalPerTick * adaptiveScale));
                var effectiveMaxTopQueriesPerTick = Math.Max(1, (int)Math.Round(maxTopQueriesPerTick * Math.Max(0.30, adaptiveScale)));
                effectiveTickAckTimeoutMs = Math.Clamp(
                    (int)Math.Round(tickAckTimeoutMs * (1.0 + (adaptivePressure * 1.25))),
                    Math.Max(250, tickAckTimeoutMs),
                    15_000);
                effectiveTickIoTimeoutMs = Math.Clamp(
                    (int)Math.Round(tickIoTimeoutMs * (1.0 + (adaptivePressure * 1.50))),
                    Math.Max(500, tickIoTimeoutMs),
                    30_000);
                effectiveTickPublishWaitMs = Math.Clamp(
                    (int)Math.Round(tickPublishWaitMs * (1.0 + (adaptivePressure * 1.10))),
                    Math.Max(15, tickPublishWaitMs),
                    5_000);
                effectiveTickPublishSettleMs = Math.Clamp(
                    (int)Math.Round(tickPublishSettleMs * (1.0 + (adaptivePressure * 0.80))),
                    1,
                    250);
                if (startupWarmupTicks > 0 && tickSignal.Tick <= startupWarmupTicks)
                {
                    effectiveTickAckTimeoutMs = Math.Max(effectiveTickAckTimeoutMs, startupWarmupAckTimeoutMs);
                    effectiveTickIoTimeoutMs = Math.Max(effectiveTickIoTimeoutMs, startupWarmupIoTimeoutMs);
                }
                var tickParticipantSelection = SelectTickParticipants(
                    availableServices,
                    ref tickParticipantCursor,
                    maxTickRequestConcurrency,
                    adaptivePressure,
                    tickSignal.Tick <= Math.Max(1, startupWarmupTicks));
                var activeServices = tickParticipantSelection.Participants;
                if (tickParticipantSelection.Throttled && tickSignal.Tick % 24 == 0)
                {
                    state.AppendOutputLog(
                        $"Tick load shed @ tick {tickSignal.Tick}: ticking {activeServices.Count}/{availableServices.Count} available structures (pressure={adaptivePressure:0.000}).");
                }
                var shouldQueryTop = (tickSignal.Tick % topQueryEveryNTicks) == 0;
                var remainingDispatchBudget = effectiveMaxSpikeDispatchTotalPerTick;
                var drainedSpikeCount = 0;
                var dispatchedSpikeCount = 0;
                var droppedByBudgetCount = 0;
                var drainCallCount = 0;
                var topQueryCount = 0;
                var generatedSpikeCount = 0;
                var routedSpikeCount = 0;
                var routeDroppedNoConnectivityCount = 0;
                var routeDroppedNoTargetsCount = 0;
                var routeDroppedTargetUnavailableCount = 0;
                var topQueryInstanceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (shouldQueryTop && activeServices.Count > 0)
                {
                    var topQueryBudget = Math.Min(effectiveMaxTopQueriesPerTick, activeServices.Count);
                    if (topQueryBudget > 0)
                    {
                        for (var i = 0; i < topQueryBudget; i++)
                        {
                            var idx = (topQueryCursor + i) % activeServices.Count;
                            topQueryInstanceKeys.Add(activeServices[idx].InstanceKey);
                        }

                        topQueryCursor = (topQueryCursor + topQueryBudget) % activeServices.Count;
                    }
                }

                var tickExecution = await ExecuteTickBatchAsync(
                    tickSignal,
                    activeServices,
                    topQueryInstanceKeys,
                    state,
                    clients,
                    serviceHealth,
                    tickRequestSemaphore,
                    healthNowMs,
                    effectiveTickAckTimeoutMs,
                    effectiveTickPublishWaitMs,
                    effectiveTickPublishSettleMs,
                    useDirectStepFastPath,
                    degradedModeIgnoreOffline,
                    degradedLogEveryTicks,
                    stoppingToken);
                topQueryCount = tickExecution.TopQueryCount;
                var successfulSteps = tickExecution.SuccessfulSteps;
                var healthySources = tickExecution.HealthySources;

                var confirmedLiveServices = SelectConfirmedLiveServices(serviceInstances, serviceHealth, healthNowMs);
                lastLiveCatalogInstanceCount = UpdateLiveInstanceCatalog(
                    confirmedLiveServices,
                    runtimeCatalog,
                    state,
                    serviceInstances.Count,
                    lastLiveCatalogInstanceCount);

            var spontaneousStats = SpontaneousInjectionStats.Empty;
            var perceptionLanguageStats = PerceptionLanguageConditioningStats.Empty;
            var (dispatchQueueMaxBatches, dispatchQueueMaxSpikes) = DispatchQueueRuntime.ComputeLimits(
                Math.Max(96, maxDispatchConcurrency * 6),
                Math.Max(1024, effectiveMaxSpikeDispatchTotalPerTick * 8),
                adaptivePressure,
                queuePressure,
                previousTransport.DispatchQueueDroppedBatches,
                previousTransport.DispatchQueueDroppedSpikes,
                healthySources.Count,
                maxGrowthScale: 3.20);
            var dispatchBatchChunkSize = DispatchQueueRuntime.ComputeBatchChunkSize(
                maxSpikesPerDispatchRequest,
                effectiveMaxSpikeDispatchPerServicePerTick,
                effectiveMaxSpikeDispatchTotalPerTick);
            var dispatchQueueByTarget = new ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>>(StringComparer.OrdinalIgnoreCase);
            var dispatchQueueMetrics = new DispatchQueueMetrics();

            var postProcessing = successfulSteps.Select(async stepResult =>
            {
                var structureId = stepResult.Instance.StructureId;
                var ack = stepResult.Step.Ack;
                var sourceHemisphere = stepResult.Instance.Hemisphere;
                var top = (stepResult.Step.TopActiveNeurons ?? Array.Empty<NeuronActivity>())
                    .Select(n => new NeuronActivity($"{sourceHemisphere}:{n.NeuronId}", n.FiringRateHz))
                    .ToList();
                var shouldFetchTopForInstance = topQueryInstanceKeys.Contains(stepResult.Instance.InstanceKey);
                // Tuple-keyed dedupe: many spikes share the same source neuron within
                // a tick. Hashing the (hemisphere, id) tuple lets us deduplicate without
                // building a string per spike; the formatted "hem:id" string is then
                // built only for the unique entries that survive into the top list.
                var drainedNeuronTuples = new HashSet<(string Hemisphere, string Id)>();

                try
                {
                    using var ctsIo = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    ctsIo.CancelAfter(TimeSpan.FromMilliseconds(effectiveTickIoTimeoutMs));

                    if (shouldFetchTopForInstance && top.Count == 0 &&
                        clients.TryGetValue(stepResult.Instance.InstanceKey, out var topClient))
                    {
                        Interlocked.Increment(ref topQueryCount);
                        using var topResponse = await topClient.GetAsync("/api/v1/structure/top?count=8", ctsIo.Token);
                        if (topResponse.IsSuccessStatusCode)
                        {
                            var queriedTop = await topResponse.Content.ReadFromJsonAsync<List<NeuronActivity>>(cancellationToken: ctsIo.Token)
                                ?? [];
                            if (queriedTop.Count > 0)
                            {
                                top = queriedTop
                                    .Select(n => new NeuronActivity($"{sourceHemisphere}:{n.NeuronId}", n.FiringRateHz))
                                    .ToList();
                            }
                        }
                    }

                    var spikes = stepResult.Step.OutboundSpikes ?? Array.Empty<SpikeMessage>();
                    Interlocked.Increment(ref drainCallCount);
                    Interlocked.Add(ref drainedSpikeCount, spikes.Count);
                    Interlocked.Add(ref generatedSpikeCount, spikes.Count);

                    foreach (var drained in spikes)
                    {
                        if (string.IsNullOrWhiteSpace(drained.SourceNeuronId))
                        {
                            continue;
                        }

                        var sourceId = drained.SourceNeuronId.Trim();
                        if (sourceId.StartsWith("population-", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        drainedNeuronTuples.Add((sourceHemisphere, sourceId));
                    }

                    var processedByService = 0;
                    var routedByService = 0;
                    var batchedSpikesByTarget = new Dictionary<string, List<SpikeMessage>>(StringComparer.OrdinalIgnoreCase);
                    var targetResolutionCache = new Dictionary<(StructureId Target, string Projection), IReadOnlyList<ServiceInstance>>();
                    for (var i = 0; i < spikes.Count; i++)
                    {
                        if (processedByService >= effectiveMaxSpikeDispatchPerServicePerTick)
                        {
                            break;
                        }

                        if (Interlocked.Decrement(ref remainingDispatchBudget) < 0)
                        {
                            Interlocked.Increment(ref remainingDispatchBudget);
                            break;
                        }

                        processedByService++;
                        var spike = spikes[i];
                        if (!state.ConnectivityMap.TryGetValue(spike.SourceStructure, out var candidates) || candidates.Count == 0)
                        {
                            Interlocked.Increment(ref routeDroppedNoConnectivityCount);
                            continue;
                        }

                        // Axonal fan-out: propagate the spike along EVERY connectome edge of the
                        // source structure, not just one. Previously routing collapsed to the
                        // single DefaultTarget, leaving divergent projections (e.g. the
                        // basal-ganglia indirect/hyperdirect pathways and cerebellar error inputs)
                        // dead for forward flow. The per-tick dispatch budget still counts source
                        // spikes, so total fan-out volume is bounded by the dispatch-queue caps.
                        var routedAnyTarget = false;
                        var spikeTargetsCerebellarInput = IsCerebellarInputTarget(spike.TargetStructure);
                        foreach (var route in ResolveRoutes(candidates, spike))
                        {
                            // Motor-command copies to cerebellar inputs are delivered as attenuated
                            // efference copies on the feedback channel (forward model), preserving
                            // the behavior of the former dedicated efference-copy path.
                            var asEfferenceCopy = IsMotorOutputStructure(spike.SourceStructure)
                                && IsCerebellarInputTarget(route.Target)
                                && !spikeTargetsCerebellarInput;
                            var builtSpike = asEfferenceCopy
                                ? BuildCerebellarEfferenceCopySpike(spike, route)
                                : RewriteSpikeForRoute(spike, route);

                            var routedSpike = builtSpike;
                            var routeKey = (route.Target, route.ProjectionType ?? string.Empty);
                            if (!targetResolutionCache.TryGetValue(routeKey, out var targetInstances))
                            {
                                targetInstances = ResolveTargetInstances(
                                    route.Target,
                                    sourceHemisphere,
                                    instancesByStructure,
                                    structureId,
                                    route.ProjectionType);
                                targetResolutionCache[routeKey] = targetInstances;
                            }
                            if (targetInstances.Count == 0)
                            {
                                Interlocked.Increment(ref routeDroppedNoTargetsCount);
                                continue;
                            }

                            var acceptedTargets = 0;
                            foreach (var targetInstance in targetInstances)
                            {
                                if (!clients.ContainsKey(targetInstance.InstanceKey))
                                {
                                    continue;
                                }
                                if (!serviceHealth.TryGetValue(targetInstance.InstanceKey, out var targetHealth) ||
                                    !targetHealth.CanAttempt(healthNowMs))
                                {
                                    continue;
                                }

                                if (!batchedSpikesByTarget.TryGetValue(targetInstance.InstanceKey, out var targetBatch))
                                {
                                    targetBatch = [];
                                    batchedSpikesByTarget[targetInstance.InstanceKey] = targetBatch;
                                }

                                targetBatch.Add(routedSpike);
                                acceptedTargets++;
                                Interlocked.Increment(ref routedSpikeCount);
                            }

                            if (acceptedTargets < targetInstances.Count)
                            {
                                Interlocked.Add(ref routeDroppedTargetUnavailableCount, targetInstances.Count - acceptedTargets);
                            }

                            if (acceptedTargets > 0)
                            {
                                routedAnyTarget = true;
                            }
                        }

                        if (routedAnyTarget)
                        {
                            routedByService++;
                        }
                    }

                    if (batchedSpikesByTarget.Count > 0)
                    {
                        foreach (var batch in batchedSpikesByTarget)
                        {
                            if (batch.Value.Count == 0)
                            {
                                continue;
                            }

                            var queued = DispatchQueueRuntime.TryEnqueue(
                                dispatchQueueByTarget,
                                batch.Key,
                                new QueuedDispatchBatch(
                                    stepResult.Instance.InstanceKey,
                                    sourceHemisphere,
                                    ResolveHemisphereFromInstanceKey(batch.Key),
                                    batch.Value),
                                dispatchQueueMetrics,
                                dispatchQueueMaxBatches,
                                dispatchQueueMaxSpikes);
                            if (!queued)
                            {
                                logger.LogDebug(
                                    "Dispatch queue backpressure at tick {Tick}: dropped batch {SourceInstance}->{TargetInstance} ({SpikeCount} spikes)",
                                    tickSignal.Tick,
                                    stepResult.Instance.InstanceKey,
                                    batch.Key,
                                    batch.Value.Count);
                            }
                        }
                    }

                    if (spikes.Count > processedByService)
                    {
                        Interlocked.Add(ref droppedByBudgetCount, spikes.Count - processedByService);
                        logger.LogDebug(
                            "Spike dispatch budget hit for {StructureId}: processed {Processed}/{Total} spikes (routed={Routed})",
                            structureId,
                            processedByService,
                            spikes.Count,
                            routedByService);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Tick post-processing degraded for {ServiceInstance} at tick {Tick}", stepResult.Instance.InstanceKey, tickSignal.Tick);
                }

                if (drainedNeuronTuples.Count > 0)
                {
                    var existing = new HashSet<string>(top.Select(t => t.NeuronId), StringComparer.Ordinal);
                    var fallbackRate = Math.Max(0.5f, ack.MeanFiringRateHz);
                    foreach (var (hem, id) in drainedNeuronTuples)
                    {
                        // String materialized once per unique drained neuron - not per
                        // spike - because the dedupe already happened in the HashSet.
                        var neuronId = string.Concat(hem, ":", id);
                        if (!existing.Add(neuronId))
                        {
                            continue;
                        }

                        top.Add(new NeuronActivity(neuronId, fallbackRate));
                        if (top.Count >= 20)
                        {
                            break;
                        }
                    }
                }

                if (top.Count == 0)
                {
                    top.Add(new NeuronActivity($"{sourceHemisphere}:population-{structureId}", Math.Max(0.01f, ack.MeanFiringRateHz)));
                }

                return new InstanceStructureSnapshot(
                    stepResult.Instance,
                    ack.StructureId,
                    ack.ActiveNeuronCount,
                    ack.MeanFiringRateHz,
                    ack.DominantRhythm,
                    top,
                    ack.LocalNeuromod,
                    ack.SpikeInCount,
                    ack.SpikeOutCount,
                    ack.FeedbackQueueDepth,
                    ack.MicrotubuleDiagnostics,
                    ack.BodySchemaDiagnostics,
                    ack.BasalGangliaDiagnostics,
                    ack.CerebellarDiagnostics,
                    ack.VestibuloReticularDiagnostics,
                    ack.SuperiorColliculusDiagnostics,
                    ack.HippocampalSpatialDiagnostics,
                    ack.SalienceAffectDiagnostics,
                    ack.PrefrontalWorkingMemoryDiagnostics,
                    ack.ThalamicAttentionGateDiagnostics,
                    ack.HypothalamicHomeostasisDiagnostics,
                    ack.SleepWakeArousalDiagnostics,
                    ack.DescendingDefenseDiagnostics,
                    ack.DopamineRewardDiagnostics,
                    ack.SeptohippocampalThetaDiagnostics,
                    ack.SpinalProprioceptiveDiagnostics,
                    ack.OlfactoryLimbicMemoryDiagnostics,
                    ack.AuditoryLanguageMotorDiagnostics,
                    ack.VisualObjectRecognitionDiagnostics,
                    ack.ActionSelectionDiagnostics,
                    ack.PerceptEnsembleDiagnostics,
                    ack.SynapticMemoryDiagnostics,
                    ack.NeuronalAttentionWorkspaceDiagnostics,
                    ack.NeuronalSleepConsolidationDiagnostics);
            });

            var processedSnapshots = await Task.WhenAll(postProcessing);
            var neuronalPercept = neuronalPerception.Update(tickSignal.Tick, processedSnapshots);
            var neuronalMemoryDecision = neuronalMemory.Update(tickSignal.Tick, processedSnapshots);
            var neuronalAttention = neuronalAttentionWorkspace.Update(tickSignal.Tick, processedSnapshots);
            var neuronalVisualAttentionDecision = neuronalVisualAttention.Update(tickSignal.Tick, processedSnapshots);
            state.UpdateNeuronalVisualAttention(neuronalVisualAttentionDecision);
            var neuronalSleep = neuronalSleepConsolidation.Update(tickSignal.Tick, processedSnapshots);
            var neuronalValuation = neuronalAffectValuation.Update(tickSignal.Tick, processedSnapshots);
            var neuronalLanguage = neuronalLanguageGrounding.Update(
                tickSignal.Tick,
                neuronalPercept,
                neuronalPerception.GetSnapshot().LanguageAnnotations,
                neuronalMemoryDecision,
                neuronalAttention,
                neuronalSleep,
                processedSnapshots);
            state.UpdateNeuronalLanguageGrounding(neuronalLanguage);
            var queueFlush = await FlushQueuedDispatchBatchesAsync(
                tickSignal,
                state,
                dispatchSemaphore,
                dispatchQueueByTarget,
                grpcSpikeTransports,
                clients,
                useHttpSpikeTransportFallback,
                activePathways,
                effectiveTickIoTimeoutMs,
                dispatchBatchChunkSize,
                stoppingToken);
            dispatchedSpikeCount += queueFlush.DeliveredSpikes;
            if (queueFlush.DispatchErrors > 0)
            {
                state.AppendOutputLog(
                    $"Tick {tickSignal.Tick}: dispatch queue flush errors={queueFlush.DispatchErrors}. " +
                    $"Last={queueFlush.LastError ?? "n/a"}");
            }

            var aggregatedSnapshots = AggregateInstanceSnapshots(processedSnapshots);
            var previousNeuronalMotor = state.GetNeuronalMotorSnapshot();
            var neuronalControl = neuronalMotorControl.GetSnapshot();
            var neuronalMotorPopulation = neuronalMotorPopulationWindow.UpdateAndGet(
                tickSignal.Tick,
                processedSnapshots,
                neuronalControl.Settings.PopulationSnapshotMaxAgeTicks);
            var neuronalMotor = NeuronalMotorPopulationDecoder.Decode(
                tickSignal.Tick,
                neuronalMotorPopulation,
                neuronalControl,
                previousNeuronalMotor);
            state.UpdateNeuronalMotor(neuronalMotor);
            var neuronalExecutiveDecision = neuronalExecutive.Update(
                tickSignal.Tick,
                processedSnapshots,
                neuronalAttention,
                neuronalMotor);
            state.UpdateNeuronalCognitionTelemetry(
                neuronalPercept,
                neuronalMemoryDecision,
                neuronalAttention,
                neuronalSleep,
                neuronalValuation,
                neuronalExecutiveDecision);
            neuronalCognitionAuthority.Update(
                tickSignal.Tick,
                neuronalPercept,
                neuronalMemoryDecision,
                neuronalAttention,
                neuronalVisualAttentionDecision,
                neuronalSleep,
                neuronalLanguage,
                neuronalValuation,
                neuronalExecutiveDecision,
                neuronalMotor);
            var spontaneousNeuronIdsByStructure = new Dictionary<StructureId, HashSet<string>>();
            var attentionBiasForNoise = NeuronalAttentionWorkspaceDecoder.ToSensoryBias(neuronalAttention);
            var spontaneousSpikingEnabled = state.IsSpontaneousSpikingEnabled();
            if (IsTransportSilent(previousTransport))
            {
                spontaneousGateStarvationTicks++;
            }
            else
            {
                spontaneousGateStarvationTicks = 0;
            }

            if (!spontaneousSpikingEnabled &&
                spontaneousGateStarvationTicks >= NeuralStarvationAutoRestoreTicks &&
                healthySources.Count > 0)
            {
                state.EnsureSpontaneousSpikingEnabled(
                    $"awake neural starvation for {spontaneousGateStarvationTicks} ticks with {healthySources.Count} live sources");
                spontaneousSpikingEnabled = true;
                spontaneousGateStarvationTicks = 0;
            }

            if (spontaneousSpikingEnabled)
            {
                spontaneousStats = await InjectSpontaneousSpikesAsync(
                    tickSignal,
                    state,
                    healthySources,
                    dispatchSemaphore,
                    clients,
                    grpcSpikeTransports,
                    useHttpSpikeTransportFallback,
                    state.ConnectivityMap,
                    instancesByStructure,
                    activePathways,
                    spontaneousNeuronIdsByStructure,
                    spontaneousNoiseScale,
                    spontaneousNoiseMaxEventsPerTick,
                    spontaneousNoiseBenchmarkMode,
                    spontaneousNoiseForceFallback || IsTransportSilent(previousTransport),
                    attentionBiasForNoise,
                    neuronalVisualAttentionDecision,
                    effectiveTickIoTimeoutMs,
                    dispatchBatchChunkSize,
                    stoppingToken);
            }

            if (perceptionLanguageBridgeEnabled &&
                (tickSignal.Tick - lastPerceptionLanguageTick) >= perceptionLanguageCooldownTicks)
            {
                perceptionLanguageStats = await InjectPerceptionLanguageConditioningAsync(
                    tickSignal,
                    state,
                    processedSnapshots,
                    neuronalPercept,
                    dispatchSemaphore,
                    clients,
                    grpcSpikeTransports,
                    useHttpSpikeTransportFallback,
                    activePathways,
                    neuronalVisualAttentionDecision,
                    perceptionLanguageMinVisualFocusConfidence,
                    perceptionLanguageMinAuditoryRateHz,
                    perceptionLanguageBurstPerToken,
                    perceptionLanguageMaxTokens,
                    effectiveTickIoTimeoutMs,
                    dispatchBatchChunkSize,
                    stoppingToken);
                if (perceptionLanguageStats.Generated > 0)
                {
                    lastPerceptionLanguageTick = tickSignal.Tick;
                }
            }

            snapshots.AddRange(MergeSpontaneousNeuronHighlights(aggregatedSnapshots, spontaneousNeuronIdsByStructure));
            foreach (var structureId in registry.Keys)
            {
                if (!instanceKeysByStructure.TryGetValue(structureId, out var instanceKeys) || instanceKeys.Length == 0)
                {
                    continue;
                }

                var telemetry = new List<ServiceRuntimeTelemetry>(instanceKeys.Length);
                foreach (var instanceKey in instanceKeys)
                {
                    telemetry.Add(serviceHealth[instanceKey].CreateTelemetry(healthNowMs));
                }

                state.UpdateServiceTelemetry(structureId, ServiceTelemetryAggregation.Aggregate(telemetry));
            }

            var (_, nonOkServiceCount) = state.GetServiceHealthCounts();
            var serviceHealthChanged = nonOkServiceCount != lastNonOkServiceCount;
            var serviceHealthPersistent = nonOkServiceCount > 0 &&
                tickSignal.Tick - lastServiceHealthDiskLogTick >= 600;
            if (serviceHealthChanged)
            {
                lastNonOkServiceCount = nonOkServiceCount;
                state.AppendOutputLog($"Service health changed: {nonOkServiceCount} non-OK.");
            }

            if (serviceHealthChanged || serviceHealthPersistent)
            {
                lastServiceHealthDiskLogTick = tickSignal.Tick;
                AppendServiceHealthDiskLog(
                    tickSignal.Tick,
                    healthNowMs,
                    serviceInstances,
                    serviceHealth,
                    nonOkServiceCount,
                    serviceHealthChanged ? "count-change" : "persistent");
            }

            autoHealRestartTask = MaybeStartAutoHealRestartAsync(
                autoHealEnabled,
                autoHealRestartTask,
                tickSignal.Tick,
                autoHealWarmupTicks,
                serviceInstances,
                serviceHealth,
                autoHealFailureThreshold,
                autoHealLastRestartByInstance,
                autoHealCooldownMs,
                autoHealMaxRestartsPerTick,
                healthNowMs,
                state,
                stoppingToken);

            if (dispatchedSpikeCount > 0 ||
                spontaneousStats.Delivered > 0 ||
                perceptionLanguageStats.Delivered > 0)
            {
                state.AppendSpikeLog(
                    $"Tick {tickSignal.Tick}: generated={generatedSpikeCount}, routed={routedSpikeCount}, delivered={dispatchedSpikeCount}, spontaneous={spontaneousStats.Delivered}/{spontaneousStats.Generated}, perceptionLang={perceptionLanguageStats.Delivered}/{perceptionLanguageStats.Generated}, pathways={activePathways.Count}");
            }

            var metabolicTransition = state.AdvanceMetabolicPhysiology(new MetabolicTickInput(
                DrainedSpikes: drainedSpikeCount,
                // Metabolic ATP drain tracks neural FIRING (spikes generated), not the
                // transport-layer delivery count. Axonal fan-out replicates each generated
                // spike to all its connectome targets, so dispatchedSpikeCount scales with
                // mean out-degree; feeding it here drained energy ~out-degree-x too fast and
                // forced near-perpetual sleep (frozen avatar). generatedSpikeCount is
                // invariant to fan-out and matches the pre-fan-out dispatched count.
                GeneratedSpikes: generatedSpikeCount,
                ActivePathways: activePathways.Count,
                SpontaneousGenerated: spontaneousStats.Generated,
                // Neural spikes update at millisecond scale; ATP and sleep pressure do not.
                // Integrate homeostatic chemistry against a slow reference interval so a
                // faster neural clock cannot compress a wake/sleep cycle into minutes.
                HomeostasisRateScale: SimulationState.ResolveMetabolicRateScale(tickSignal.TickDurationMs)),
                neuronalSleep);

            if (metabolicTransition.EnteredSleep)
            {
                state.AppendOutputLog(
                    $"Neuronal sleep observed at tick {tickSignal.Tick}: ATP={metabolicTransition.AtpBudget:0.000}.");
            }
            else if (metabolicTransition.ExitedSleep)
            {
                state.AppendOutputLog(
                    $"Neuronal wake observed at tick {tickSignal.Tick}: ATP={metabolicTransition.AtpBudget:0.000}, sleepTicks={metabolicTransition.SleepTicks}.");
            }

            var instanceTelemetryNow = new List<ServiceRuntimeTelemetry>(serviceInstances.Count);
            var ackLatencyEwmaTotalMs = 0.0;
            var ackLatencyEwmaCount = 0;
            var latencyLt100Ms = 0;
            var latency100To250Ms = 0;
            var latency250To500Ms = 0;
            var latency500To1000Ms = 0;
            var latencyGte1000Ms = 0;
            for (var i = 0; i < serviceInstances.Count; i++)
            {
                var telemetry = serviceHealth[serviceInstances[i].InstanceKey].CreateTelemetry(healthNowMs);
                instanceTelemetryNow.Add(telemetry);
                if (telemetry.AckLatencyEwmaMs > 0.001)
                {
                    ackLatencyEwmaTotalMs += telemetry.AckLatencyEwmaMs;
                    ackLatencyEwmaCount++;
                }

                latencyLt100Ms += telemetry.LatencyLt100MsCount;
                latency100To250Ms += telemetry.Latency100To250MsCount;
                latency250To500Ms += telemetry.Latency250To500MsCount;
                latency500To1000Ms += telemetry.Latency500To1000MsCount;
                latencyGte1000Ms += telemetry.LatencyGte1000MsCount;
            }

            var ackLatencyEwmaSummaryMs = ackLatencyEwmaCount == 0
                ? 0.0
                : ackLatencyEwmaTotalMs / ackLatencyEwmaCount;
            tickWallStopwatch.Stop();
            var tickWallMs = tickWallStopwatch.Elapsed.TotalMilliseconds;
            tickWallSamples.Enqueue(tickWallMs);
            while (tickWallSamples.Count > 256)
            {
                tickWallSamples.Dequeue();
            }

            var tickWallSeries = tickWallSamples.ToArray();
            Array.Sort(tickWallSeries);
            var tickWallP50Ms = ComputePercentile(tickWallSeries, 0.50);
            var tickWallP95Ms = ComputePercentile(tickWallSeries, 0.95);
            var tickWallP99Ms = ComputePercentile(tickWallSeries, 0.99);

            var languageBackoffDispatchErrors = queueFlush.DispatchErrors +
                                                spontaneousStats.DispatchErrors +
                                                perceptionLanguageStats.DispatchErrors +
                                                dispatchQueueMetrics.DroppedBatches;
            languageBackoffPolicy.ObserveTick(
                tickSignal.Tick,
                activePathways.Count,
                dispatchedSpikeCount,
                languageBackoffDispatchErrors);
            var languageBackoff = languageBackoffPolicy.GetSnapshot(16);

            state.UpdateTransportStats(new TransportRuntimeStats(
                tickSignal.Tick,
                activeServices.Count,
                successfulSteps.Count,
                drainCallCount,
                drainedSpikeCount,
                dispatchedSpikeCount,
                droppedByBudgetCount,
                topQueryCount,
                spontaneousStats.Generated,
                spontaneousStats.Delivered,
                spontaneousStats.DispatchErrors,
                spontaneousStats.LastError,
                activePathways.Count,
                dispatchQueueMetrics.QueuedBatches,
                dispatchQueueMetrics.QueuedSpikes,
                dispatchQueueMetrics.PeakQueuedBatches,
                dispatchQueueMetrics.PeakQueuedSpikes,
                dispatchQueueMetrics.DroppedBatches,
                dispatchQueueMetrics.DroppedSpikes,
                queueFlush.FlushedBatches,
                queueFlush.ActiveTargets,
                queueFlush.MaxTargetBurstSpikes,
                queueFlush.DispatchErrors,
                queueFlush.LastError,
                generatedSpikeCount,
                routedSpikeCount,
                dispatchedSpikeCount,
                routeDroppedNoConnectivityCount,
                routeDroppedNoTargetsCount,
                routeDroppedTargetUnavailableCount,
                dispatchQueueMetrics.DroppedSpikes,
                adaptivePressure,
                adaptiveScale,
                effectiveMaxSpikeDispatchPerServicePerTick,
                effectiveMaxSpikeDispatchTotalPerTick,
                effectiveMaxTopQueriesPerTick,
                effectiveTickAckTimeoutMs,
                effectiveTickIoTimeoutMs,
                effectiveTickPublishWaitMs,
                effectiveTickPublishSettleMs,
                ackLatencyEwmaSummaryMs,
                latencyLt100Ms,
                latency100To250Ms,
                latency250To500Ms,
                latency500To1000Ms,
                latencyGte1000Ms,
                tickWallMs,
                tickWallP50Ms,
                tickWallP95Ms,
                tickWallP99Ms,
                degradeSignalLabel,
                perceptionLanguageStats.Generated,
                perceptionLanguageStats.Delivered,
                perceptionLanguageStats.DispatchErrors,
                perceptionLanguageStats.LastError,
                languageBackoff.TotalAttempts,
                languageBackoff.TotalResolved,
                languageBackoff.TotalFallbackSelections,
                languageBackoff.TotalDispatchErrors,
                languageBackoff.TopEdges,
                languageBackoff.Graphs,
                languageBackoff.ModeStates));

            if (tickSignal.Tick % snapshotEvery == 0)
            {
                var brainSnapshot = new BrainSnapshot(
                    tickSignal.Tick,
                    tickSignal.TimestampMs,
                    new NeuromodState(),
                    tickSignal.PhaseContext,
                    0f,
                    snapshots,
                    activePathways.Select(x => new ActivePathway(x.Key.Source, x.Key.Target, x.Value, x.Key.Nt)).ToList());

                await snapshotStore.AppendAsync(brainSnapshot, stoppingToken);
                state.MarkSnapshot(brainSnapshot);
                state.AppendOutputLog(
                    $"Snapshot tick {tickSignal.Tick}: structures={snapshots.Count}, activePathways={activePathways.Count}, dispatched={dispatchedSpikeCount}");
            }
            if (wallClockDelayMs > 0.0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(wallClockDelayMs), stoppingToken);
            }
            }
        }
        finally
        {
            dispatchSemaphore.Dispose();
            tickRequestSemaphore.Dispose();
            foreach (var session in _streamSessions.Values)
            {
                try
                {
                    await session.DisposeAsync();
                }
                catch
                {
                }
            }
            _streamSessions.Clear();
            foreach (var channel in grpcChannels.Values)
            {
                channel.Dispose();
            }
        }
    }

    private TransportClientBundle InitializeTransportClients(
        IReadOnlyList<ServiceInstance> serviceInstances,
        bool useGrpcSpikeTransport)
    {
        var clients = serviceInstances.ToDictionary(i => i.InstanceKey, _ => clientFactory.CreateClient("dnne"));
        var grpcChannels = new Dictionary<string, GrpcChannel>(StringComparer.OrdinalIgnoreCase);
        var grpcSpikeTransports = new Dictionary<string, IStructureSpikeTransport>(StringComparer.OrdinalIgnoreCase);
        var serviceHealth = serviceInstances.ToDictionary(i => i.InstanceKey, _ => new ServiceHealth());

        // When NRE_STRUCTURE_SHARED_SECRET is set, attach the header so structure
        // services accept our requests. The same env var on the structure side
        // enables the matching auth middleware.
        foreach (var instance in serviceInstances)
        {
            clients[instance.InstanceKey].BaseAddress = instance.Endpoint;
            clients[instance.InstanceKey].Timeout = Timeout.InfiniteTimeSpan;
            NreStructureSecurity.ApplyClientAuthentication(clients[instance.InstanceKey]);
            if (!useGrpcSpikeTransport || !CanRegisterGrpcSpikeTransport(instance.Endpoint))
            {
                continue;
            }

            var grpcChannel = GrpcChannel.ForAddress(instance.Endpoint, new GrpcChannelOptions
            {
                // Reuse the authenticated target client. A separate handler here
                // silently dropped X-NRE-Auth from every gRPC request.
                HttpClient = clients[instance.InstanceKey]
            });

            grpcChannels[instance.InstanceKey] = grpcChannel;
            var grpcTransport = grpcChannel.CreateGrpcService<IStructureSpikeTransport>();
            grpcSpikeTransports[instance.InstanceKey] = grpcTransport;

            // Opt-in bidi streaming. The session takes over the gRPC dispatch path
            // for this target; the unary transport stays registered so health probes
            // and the existing fallback chain remain functional if the session dies.
            if (string.Equals(Environment.GetEnvironmentVariable("NRE_USE_GRPC_BIDI_STREAM"), "1", StringComparison.Ordinal))
            {
                _streamSessions.TryAdd(
                    instance.InstanceKey,
                    new GrpcSpikeStreamSession(grpcTransport, instance.InstanceKey, logger));
            }
        }

        return new TransportClientBundle(clients, grpcChannels, grpcSpikeTransports, serviceHealth);
    }

    private static bool CanRegisterGrpcSpikeTransport(Uri endpoint)
    {
        // The structure services currently expose mixed HTTP/1.1 + HTTP/2 listeners on the same
        // local cleartext endpoint. Kestrel downgrades those to HTTP/1.1 without TLS/ALPN, so the
        // gRPC client can never establish a usable transport there. Only register gRPC for
        // endpoints that can actually negotiate it.
        return string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplySupervisorServiceHealthResult(
        RestartServiceResult result,
        IReadOnlyDictionary<string, ServiceHealth> serviceHealth,
        double healthNowMs,
        long tick,
        string context)
    {
        foreach (var item in result.Items)
        {
            if (!serviceHealth.TryGetValue(item.InstanceKey, out var health))
            {
                continue;
            }

            if (item.Healthy)
            {
                health.MarkSuccess(healthNowMs, ackLatencyMs: 0, tick);
            }
            else
            {
                var message = string.IsNullOrWhiteSpace(item.Message)
                    ? "service did not become API-healthy"
                    : item.Message;
                health.MarkFailure(healthNowMs, tick, $"{context}: {message}");
            }
        }
    }

    private static List<ServiceInstance> SelectHealthyInstancesFromSupervisorResult(
        IReadOnlyList<ServiceInstance> serviceInstances,
        RestartServiceResult result)
    {
        var healthyKeys = result.Items
            .Where(item => item.Healthy)
            .Select(item => item.InstanceKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (healthyKeys.Count == 0)
        {
            return [];
        }

        return serviceInstances
            .Where(instance => healthyKeys.Contains(instance.InstanceKey))
            .ToList();
    }

    private async Task<Task<RestartServiceResult>?> HandleSimulationRestartAsync(
        SimulationState state,
        SnapshotStore snapshotStore,
        RuntimeInstanceCatalog runtimeCatalog,
        IReadOnlyList<ServiceInstance> serviceInstances,
        IReadOnlyDictionary<string, ServiceHealth> serviceHealth,
        IDictionary<string, double> autoHealLastRestartByInstance,
        int tickTimeoutMs,
        bool restartStructureServicesOnSimRestart,
        CancellationToken stoppingToken)
    {
        foreach (var health in serviceHealth.Values)
        {
            health.Reset();
        }

        publishBuffer.Clear();
        await snapshotStore.ClearAsync(stoppingToken);
        state.ResetForSimulationRestart();
        _transportCapabilities.Clear();
        autoHealLastRestartByInstance.Clear();

        try
        {
            if (restartStructureServicesOnSimRestart)
            {
                var restartResult = await structureSupervisor.RestartServicesAsync(serviceInstances, stoppingToken);
                ApplySupervisorServiceHealthResult(
                    restartResult,
                    serviceHealth,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    state.Tick,
                    "simulation restart");
                runtimeCatalog.SetLiveInstances(SelectHealthyInstancesFromSupervisorResult(serviceInstances, restartResult));
                state.AppendOutputLog(
                    $"Simulation restart applied. Structure services restarted: {restartResult.Restarted}/{restartResult.Requested}; healthy after restart: {restartResult.Healthy}.");
            }
            else
            {
                var startupResult = await structureSupervisor.EnsureServicesOnlineAsync(serviceInstances, tickTimeoutMs, stoppingToken);
                ApplySupervisorServiceHealthResult(
                    startupResult,
                    serviceHealth,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    state.Tick,
                    "simulation restart reprobe");
                runtimeCatalog.SetLiveInstances(SelectHealthyInstancesFromSupervisorResult(serviceInstances, startupResult));
                state.AppendOutputLog(
                    $"Simulation restart applied and services re-probed: healthy={startupResult.Healthy}/{startupResult.Requested}, launched={startupResult.Restarted}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Simulation restart encountered service reset/reprobe failures.");
            state.AppendOutputLog($"Simulation restart warning: service reset/reprobe failed ({ClassifyFailure(ex)}).");
        }

        return null;
    }

    private async Task<Task<RestartServiceResult>?> ObserveCompletedAutoHealRestartAsync(
        Task<RestartServiceResult>? autoHealRestartTask,
        IReadOnlyDictionary<string, ServiceHealth> serviceHealth,
        SimulationState state)
    {
        if (autoHealRestartTask is null || !autoHealRestartTask.IsCompleted)
        {
            return autoHealRestartTask;
        }

        try
        {
            var autoHealResult = await autoHealRestartTask;
            if (autoHealResult.Restarted > 0)
            {
                foreach (var item in autoHealResult.Items)
                {
                    if (item.Healthy && serviceHealth.TryGetValue(item.InstanceKey, out var health))
                    {
                        health.Reset();
                    }
                }

                state.AppendOutputLog(
                    $"Auto-heal complete: requested={autoHealResult.Requested}, restarted={autoHealResult.Restarted}, healthy={autoHealResult.Healthy}.");
                ControlHealthLog.Append(
                    $"auto-heal complete requested={autoHealResult.Requested} restarted={autoHealResult.Restarted} healthy={autoHealResult.Healthy}{Environment.NewLine}" +
                    string.Join(
                        Environment.NewLine,
                        autoHealResult.Items.Select(item =>
                            $"{item.InstanceKey} restarted={item.Restarted} healthy={item.Healthy} message={item.Message}")));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-heal restart task failed.");
            state.AppendOutputLog($"Auto-heal failure: {ClassifyFailure(ex)}");
            ControlHealthLog.Append($"auto-heal failure {ClassifyFailure(ex)}");
        }

        return null;
    }

    private async Task<TickExecutionBatchResult> ExecuteTickBatchAsync(
        TickSignal tickSignal,
        IReadOnlyList<ServiceInstance> activeServices,
        HashSet<string> topQueryInstanceKeys,
        SimulationState state,
        IReadOnlyDictionary<string, HttpClient> clients,
        IReadOnlyDictionary<string, ServiceHealth> serviceHealth,
        SemaphoreSlim tickRequestSemaphore,
        double healthNowMs,
        int effectiveTickAckTimeoutMs,
        int effectiveTickPublishWaitMs,
        int effectiveTickPublishSettleMs,
        bool useDirectStepFastPath,
        bool degradedModeIgnoreOffline,
        int degradedLogEveryTicks,
        CancellationToken stoppingToken)
    {
        var topQueryCount = 0;
        if (!useDirectStepFastPath)
        {
            logger.LogWarning("A legacy profile requested acknowledge-only ticks; using direct structure-step responses instead.");
        }

        var pendingSteps = activeServices.Select(async instance =>
            {
                var client = clients[instance.InstanceKey];
                var stopwatch = Stopwatch.StartNew();
                var tickPermit = false;
                var includeTop = topQueryInstanceKeys.Contains(instance.InstanceKey);

                try
                {
                    await tickRequestSemaphore.WaitAsync(stoppingToken);
                    tickPermit = true;
                    using var ctsAck = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    ctsAck.CancelAfter(TimeSpan.FromMilliseconds(effectiveTickAckTimeoutMs));
                    var stepRequest = new StructureStepRequest(tickSignal, includeTop ? 8 : 0, includeTop);
                    using var stepResponse = await client.PostAsJsonAsync("/api/v1/structure/step", stepRequest, ctsAck.Token);
                    stepResponse.EnsureSuccessStatusCode();
                    var step = await stepResponse.Content.ReadFromJsonAsync<StructureStepResult>(cancellationToken: ctsAck.Token)
                        ?? throw new InvalidOperationException($"Missing step payload from {instance.InstanceKey}");
                    if (step.Ack.Tick != tickSignal.Tick)
                    {
                        throw new InvalidOperationException(
                            $"Mismatched step tick from {instance.InstanceKey}: expected {tickSignal.Tick}, received {step.Ack.Tick}");
                    }

                    if (includeTop)
                    {
                        Interlocked.Increment(ref topQueryCount);
                    }

                    serviceHealth[instance.InstanceKey].MarkSuccess(healthNowMs, stopwatch.Elapsed.TotalMilliseconds, tickSignal.Tick);
                    return new TickStepResult(instance, step);
                }
                catch (Exception ex)
                {
                    serviceHealth[instance.InstanceKey].MarkFailure(healthNowMs, tickSignal.Tick, ClassifyFailure(ex));
                    LogTickExecutionFailure(
                        instance,
                        tickSignal.Tick,
                        serviceHealth,
                        degradedModeIgnoreOffline,
                        degradedLogEveryTicks,
                        ex);
                    return null;
                }
                finally
                {
                    if (tickPermit)
                    {
                        tickRequestSemaphore.Release();
                    }
                }
        });

        var stepResults = await Task.WhenAll(pendingSteps);
        var successfulSteps = stepResults.Where(x => x is not null).Select(x => x!).ToList();
        var healthySources = successfulSteps.Select(x => x.Instance).ToList();
        return new TickExecutionBatchResult(successfulSteps, healthySources, topQueryCount);
    }

    private void LogTickExecutionFailure(
        ServiceInstance instance,
        long tick,
        IReadOnlyDictionary<string, ServiceHealth> serviceHealth,
        bool degradedModeIgnoreOffline,
        int degradedLogEveryTicks,
        Exception ex)
    {
        if (degradedModeIgnoreOffline)
        {
            if (serviceHealth[instance.InstanceKey].ShouldEmitDegradedLog(tick, degradedLogEveryTicks))
            {
                logger.LogInformation(
                    "Degraded mode: {ServiceInstance} timed out at tick {Tick}; retry at {RetryMs:0.0}ms. Last error: {Error}",
                    instance.InstanceKey,
                    tick,
                    serviceHealth[instance.InstanceKey].NextRetryTimestampMs,
                    ex.Message);
            }

            return;
        }

        logger.LogWarning(
            ex,
            "Tick timeout/deadlock for {ServiceInstance} tick {Tick}. Next retry at {RetryMs:0.0}ms",
            instance.InstanceKey,
            tick,
            serviceHealth[instance.InstanceKey].NextRetryTimestampMs);
    }

    private int UpdateLiveInstanceCatalog(
        IReadOnlyList<ServiceInstance> healthySources,
        RuntimeInstanceCatalog runtimeCatalog,
        SimulationState state,
        int totalServiceCount,
        int lastLiveCatalogInstanceCount)
    {
        var liveInstancesForCatalog = new List<ServiceInstance>(healthySources.Count);
        var seenLiveInstanceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < healthySources.Count; i++)
        {
            var instance = healthySources[i];
            if (seenLiveInstanceKeys.Add(instance.InstanceKey))
            {
                liveInstancesForCatalog.Add(instance);
            }
        }

        runtimeCatalog.SetLiveInstances(liveInstancesForCatalog);
        if (liveInstancesForCatalog.Count != lastLiveCatalogInstanceCount)
        {
            state.AppendOutputLog(
                $"Runtime instance registry updated: live_instances={liveInstancesForCatalog.Count}/{totalServiceCount}.");
        }

        return liveInstancesForCatalog.Count;
    }

    private static List<ServiceInstance> SelectConfirmedLiveServices(
        IReadOnlyList<ServiceInstance> serviceInstances,
        IReadOnlyDictionary<string, ServiceHealth> serviceHealth,
        double nowMs)
        => serviceInstances
            .Where(instance =>
                serviceHealth.TryGetValue(instance.InstanceKey, out var health) &&
                string.Equals(health.CreateTelemetry(nowMs).LastStatus, "OK", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static TickParticipantSelection SelectTickParticipants(
        IReadOnlyList<ServiceInstance> availableServices,
        ref int cursor,
        int maxTickRequestConcurrency,
        double adaptivePressure,
        bool startupWarmup)
    {
        if (availableServices.Count == 0)
        {
            return new TickParticipantSelection([], false);
        }

        var baselineMultiplier = startupWarmup ? 2.0 : 3.0;
        var relaxedBudget = Math.Max(1, (int)Math.Round(maxTickRequestConcurrency * baselineMultiplier));
        var pressureRatio = Math.Clamp(adaptivePressure, 0.0, 1.0);
        var pressureScale = 1.0 - (pressureRatio * 0.45);
        var pressureBudget = Math.Max(1, (int)Math.Round(maxTickRequestConcurrency * pressureScale));
        var participantBudget = Math.Clamp(Math.Min(relaxedBudget, Math.Max(maxTickRequestConcurrency, pressureBudget)), 1, availableServices.Count);
        if (participantBudget >= availableServices.Count)
        {
            cursor = 0;
            return new TickParticipantSelection(availableServices.ToList(), false);
        }

        var selected = new List<ServiceInstance>(participantBudget);
        for (var i = 0; i < participantBudget; i++)
        {
            selected.Add(availableServices[(cursor + i) % availableServices.Count]);
        }

        cursor = (cursor + participantBudget) % availableServices.Count;
        return new TickParticipantSelection(selected, true);
    }

    private Task<RestartServiceResult>? MaybeStartAutoHealRestartAsync(
        bool autoHealEnabled,
        Task<RestartServiceResult>? autoHealRestartTask,
        long tick,
        int autoHealWarmupTicks,
        IReadOnlyList<ServiceInstance> serviceInstances,
        IReadOnlyDictionary<string, ServiceHealth> serviceHealth,
        int autoHealFailureThreshold,
        IDictionary<string, double> autoHealLastRestartByInstance,
        int autoHealCooldownMs,
        int autoHealMaxRestartsPerTick,
        double healthNowMs,
        SimulationState state,
        CancellationToken stoppingToken)
    {
        if (!autoHealEnabled || autoHealRestartTask is not null || tick < autoHealWarmupTicks)
        {
            return autoHealRestartTask;
        }

        // Prune restart-history entries for instances that no longer appear in the
        // live catalog so the dictionary cannot grow without bound across reconfigs.
        if (autoHealLastRestartByInstance.Count > serviceInstances.Count)
        {
            var activeKeys = new HashSet<string>(
                serviceInstances.Select(s => s.InstanceKey),
                StringComparer.OrdinalIgnoreCase);
            foreach (var staleKey in autoHealLastRestartByInstance.Keys
                .Where(k => !activeKeys.Contains(k))
                .ToArray())
            {
                autoHealLastRestartByInstance.Remove(staleKey);
            }
        }

        var restartCandidates = serviceInstances
            .Select(instance => new
            {
                Instance = instance,
                Telemetry = serviceHealth[instance.InstanceKey].CreateTelemetry(healthNowMs)
            })
            .Where(x =>
                x.Telemetry.ConsecutiveFailures >= autoHealFailureThreshold &&
                IsAutoHealRestartWorthy(x.Telemetry) &&
                (!autoHealLastRestartByInstance.TryGetValue(x.Instance.InstanceKey, out var lastRestartMs) ||
                 (healthNowMs - lastRestartMs) >= autoHealCooldownMs))
            .OrderByDescending(x => x.Telemetry.ConsecutiveFailures)
            .ThenByDescending(x => x.Telemetry.TimeoutFailureCount)
            .Take(autoHealMaxRestartsPerTick)
            .Select(x => x.Instance)
            .ToList();

        if (restartCandidates.Count == 0)
        {
            return autoHealRestartTask;
        }

        foreach (var candidate in restartCandidates)
        {
            autoHealLastRestartByInstance[candidate.InstanceKey] = healthNowMs;
        }

        state.AppendOutputLog(
            $"Auto-heal triggered at tick {tick}: restarting {restartCandidates.Count} service instance(s).");
        ControlHealthLog.Append(
            $"auto-heal triggered tick={tick} restarting={restartCandidates.Count}{Environment.NewLine}" +
            string.Join(Environment.NewLine, restartCandidates.Select(candidate => $"{candidate.InstanceKey} {candidate.StructureId} {candidate.HemisphereNormalized} endpoint={candidate.Endpoint}")));
        return structureSupervisor.RestartServicesAsync(restartCandidates, stoppingToken);
    }

    private static bool IsAutoHealRestartWorthy(ServiceRuntimeTelemetry telemetry)
    {
        var error = telemetry.LastError ?? string.Empty;
        if (error.Contains("500", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("connection refused", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("project path not found", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("launch failed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("TickAck", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(telemetry.LastStatus, "BACKOFF", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return telemetry.ConsecutiveFailures >= 6;
    }

    private sealed record TransportClientBundle(
        Dictionary<string, HttpClient> Clients,
        Dictionary<string, GrpcChannel> GrpcChannels,
        Dictionary<string, IStructureSpikeTransport> GrpcSpikeTransports,
        Dictionary<string, ServiceHealth> ServiceHealth);

    // Long-lived bidirectional gRPC stream for one target. A producer completes
    // only after the target ACKs its batch. The one in-flight batch survives a
    // reconnect and can be resent safely because structure ingress is idempotent.
    private sealed class GrpcSpikeStreamSession : IAsyncDisposable
    {
        private readonly IStructureSpikeTransport _transport;
        private readonly string _targetInstanceKey;
        private readonly ILogger _logger;
        private readonly Channel<PendingSpikeBatch> _outboundChannel;
        private readonly object _currentGate = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _pumpTask;
        private PendingSpikeBatch? _current;
        private int _disposed;

        public GrpcSpikeStreamSession(IStructureSpikeTransport transport, string targetInstanceKey, ILogger logger)
        {
            _transport = transport;
            _targetInstanceKey = targetInstanceKey;
            _logger = logger;
            _outboundChannel = Channel.CreateBounded<PendingSpikeBatch>(
                new BoundedChannelOptions(64)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });
            _pumpTask = Task.Run(() => PumpAsync(_cts.Token));
        }

        public async ValueTask<SpikeBatchAck> SendAsync(SpikeBatchEnvelope batch, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (string.IsNullOrWhiteSpace(batch.BatchId))
            {
                batch.BatchId = Guid.NewGuid().ToString("N");
            }

            var pending = new PendingSpikeBatch(batch);
            await _outboundChannel.Writer.WriteAsync(pending, cancellationToken).ConfigureAwait(false);
            return await pending.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task PumpAsync(CancellationToken cancellationToken)
        {
            // Reconnect loop: a stream call only ends on remote close or transport
            // error. We re-issue the call and continue draining the channel; any
            // batches enqueued while reconnecting are held by the channel.
            var backoffMs = 250;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var callCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    try
                    {
                        var ackStream = _transport.StreamSpikeBatchesAsync(
                            ReadOutboundAsync(callCts.Token),
                            new CallContext(new Grpc.Core.CallOptions(cancellationToken: callCts.Token)));
                        await foreach (var ack in ackStream.WithCancellation(callCts.Token).ConfigureAwait(false))
                        {
                            PendingSpikeBatch? current;
                            lock (_currentGate)
                            {
                                current = _current;
                            }

                            if (current is null || !string.Equals(current.Envelope.BatchId, ack.BatchId, StringComparison.Ordinal))
                            {
                                throw new InvalidDataException(
                                    $"Spike stream ACK mismatch for {_targetInstanceKey}: expected '{current?.Envelope.BatchId}', received '{ack.BatchId}'.");
                            }

                            current.Completion.TrySetResult(ack);
                        }

                        lock (_currentGate)
                        {
                            if (_current is not null && !_current.Completion.Task.IsCompleted)
                            {
                                throw new IOException($"Spike stream for {_targetInstanceKey} closed before ACK.");
                            }
                        }
                    }
                    finally
                    {
                        callCts.Cancel();
                    }
                    backoffMs = 250;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Spike stream session for {TargetInstance} interrupted; reconnecting in {Backoff}ms", _targetInstanceKey, backoffMs);
                    try
                    {
                        await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    backoffMs = Math.Min(backoffMs * 2, 5000);
                }
            }
        }

        private async IAsyncEnumerable<SpikeBatchEnvelope> ReadOutboundAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                PendingSpikeBatch? pending;
                lock (_currentGate)
                {
                    pending = _current;
                }

                if (pending is null)
                {
                    pending = await _outboundChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    lock (_currentGate)
                    {
                        _current ??= pending;
                        pending = _current;
                    }
                }

                yield return pending.Envelope;
                await pending.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                lock (_currentGate)
                {
                    if (ReferenceEquals(_current, pending))
                    {
                        _current = null;
                    }
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            _outboundChannel.Writer.TryComplete();
            _cts.Cancel();
            try
            {
                await _pumpTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
            }

            var stopped = new OperationCanceledException($"Spike stream session for {_targetInstanceKey} stopped.");
            lock (_currentGate)
            {
                _current?.Completion.TrySetException(stopped);
                _current = null;
            }
            while (_outboundChannel.Reader.TryRead(out var pending))
            {
                pending.Completion.TrySetException(stopped);
            }
            _cts.Dispose();
        }

        private sealed class PendingSpikeBatch(SpikeBatchEnvelope envelope)
        {
            public SpikeBatchEnvelope Envelope { get; } = envelope;
            public TaskCompletionSource<SpikeBatchAck> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }


    private sealed record TickExecutionBatchResult(
        List<TickStepResult> SuccessfulSteps,
        List<ServiceInstance> HealthySources,
        int TopQueryCount);

    private sealed record TickParticipantSelection(
        List<ServiceInstance> Participants,
        bool Throttled);


    private static string NormalizeHemisphere(string? hemisphere)
    {
        if (string.IsNullOrWhiteSpace(hemisphere))
        {
            return "M";
        }

        var token = hemisphere.Trim().ToUpperInvariant();
        return token is "L" or "R" or "M" ? token : "M";
    }

    private static string NormalizeForReplay(string? neuronId)
    {
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return string.Empty;
        }

        var trimmed = neuronId.Trim();
        var separator = trimmed.IndexOf(':');
        if (separator > 0 && separator + 1 < trimmed.Length)
        {
            return trimmed[(separator + 1)..];
        }

        return trimmed;
    }

    private sealed record ConnectivityRuleJson(string? Source, List<SynapticConnectionJson>? Connections);
    private sealed record SynapticConnectionJson(string? Target, string? SynapseId, string? Neurotransmitter, string? ProjectionType);

    private static string ResolvePathFromBaseOrAncestors(string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            if (File.Exists(relativeOrAbsolutePath))
            {
                return relativeOrAbsolutePath;
            }

            throw new DirectoryNotFoundException($"Connectivity file not found: {relativeOrAbsolutePath}");
        }

        var direct = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativeOrAbsolutePath));
        if (File.Exists(direct))
        {
            return direct;
        }

        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            var candidate = Path.Combine(cursor.FullName, relativeOrAbsolutePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            cursor = cursor.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not resolve '{relativeOrAbsolutePath}' from base directory '{AppContext.BaseDirectory}'.");
    }

    private static Dictionary<StructureId, List<SynapticConnection>> BuildConnectivityMap(
        IEnumerable<ConnectivityRuleJson> rules,
        ILogger logger)
    {
        var map = new Dictionary<StructureId, List<SynapticConnection>>();

        foreach (var rule in rules)
        {
            if (!Enum.TryParse<StructureId>(rule.Source, ignoreCase: true, out var source))
            {
                logger.LogWarning("Skipping connectivity row with invalid source '{Source}'", rule.Source);
                continue;
            }

            if (!map.TryGetValue(source, out var list))
            {
                list = new List<SynapticConnection>();
                map[source] = list;
            }

            foreach (var connection in rule.Connections ?? [])
            {
                if (!Enum.TryParse<StructureId>(connection.Target, ignoreCase: true, out var target))
                {
                    logger.LogWarning("Skipping connection from {Source}: invalid target '{Target}'", source, connection.Target);
                    continue;
                }

                if (!Guid.TryParse(connection.SynapseId, out var synapseId))
                {
                    logger.LogWarning("Skipping connection {Source}->{Target}: invalid synapse_id '{SynapseId}'", source, target, connection.SynapseId);
                    continue;
                }

                if (!Enum.TryParse<NTEnum>(connection.Neurotransmitter, ignoreCase: true, out var nt))
                {
                    logger.LogWarning("Skipping connection {Source}->{Target}: invalid neurotransmitter '{Neurotransmitter}'", source, target, connection.Neurotransmitter);
                    continue;
                }

                list.Add(new SynapticConnection(target, synapseId, nt, connection.ProjectionType ?? "unspecified"));
            }
        }

        return map;
    }

    private static void AuditConnectivityCoverage(
        IReadOnlyDictionary<StructureId, List<SynapticConnection>> connectivity,
        bool enforceBidirectionalCoverage,
        ILogger logger)
    {
        var allStructures = Enum.GetValues<StructureId>();
        var sourceSet = connectivity
            .Where(kvp => kvp.Value.Count > 0)
            .Select(kvp => kvp.Key)
            .ToHashSet();
        var targetSet = connectivity
            .SelectMany(kvp => kvp.Value)
            .Select(connection => connection.Target)
            .ToHashSet();

        var missingAsSource = allStructures
            .Where(structure => !sourceSet.Contains(structure))
            .OrderBy(structure => structure.ToString(), StringComparer.Ordinal)
            .ToArray();
        var missingAsTarget = allStructures
            .Where(structure => !targetSet.Contains(structure))
            .OrderBy(structure => structure.ToString(), StringComparer.Ordinal)
            .ToArray();

        if (missingAsSource.Length == 0 && missingAsTarget.Length == 0)
        {
            logger.LogInformation("Connectivity coverage OK: all {Count} structures have explicit source and target participation.", allStructures.Length);
            return;
        }

        logger.LogWarning(
            "Connectivity coverage gap: missing_as_source={MissingSource}; missing_as_target={MissingTarget}",
            missingAsSource.Length == 0 ? "-" : string.Join(", ", missingAsSource),
            missingAsTarget.Length == 0 ? "-" : string.Join(", ", missingAsTarget));

        if (!enforceBidirectionalCoverage)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Connectivity coverage enforcement failed. Missing as source: {(missingAsSource.Length == 0 ? "-" : string.Join(", ", missingAsSource))}; " +
            $"missing as target: {(missingAsTarget.Length == 0 ? "-" : string.Join(", ", missingAsTarget))}. " +
            "Update connectivity/dnne-connectivity.json to include explicit outbound and inbound participation for every StructureId.");
    }

    private static void AuditBiologicalSemantics(
        IReadOnlyDictionary<StructureId, List<SynapticConnection>> connectivity,
        bool enforceBiologicalSemantics,
        ILogger logger)
    {
        var violations = new List<string>();

        static string Describe(StructureId source, SynapticConnection connection)
            => $"{source}->{connection.Target} ({connection.ProjectionType}, {connection.Neurotransmitter})";

        void RequireExclusiveNeurotransmitter(StructureId source, NTEnum required, string rationale)
        {
            if (!connectivity.TryGetValue(source, out var connections))
            {
                return;
            }

            foreach (var connection in connections)
            {
                if (connection.Neurotransmitter != required)
                {
                    violations.Add($"{Describe(source, connection)} violates {rationale}; expected {required}.");
                }
            }
        }

        var inhibitoryOutputSources = new[]
        {
            StructureId.Striatum,
            StructureId.GPe,
            StructureId.GPi,
            StructureId.Snr,
            StructureId.Trn,
            StructureId.PurkinjeCellLayer,
            StructureId.VentralPallidum,
            StructureId.GlobusPallidus
        };

        foreach (var inhibitorySource in inhibitoryOutputSources)
        {
            RequireExclusiveNeurotransmitter(inhibitorySource, NTEnum.GABA, "inhibitory nucleus output rule");
        }

        RequireExclusiveNeurotransmitter(StructureId.LocusCoeruleus, NTEnum.NOREPINEPHRINE, "LC neuromodulator identity rule");
        RequireExclusiveNeurotransmitter(StructureId.RapheNuclei, NTEnum.SEROTONIN, "Raphe neuromodulator identity rule");
        RequireExclusiveNeurotransmitter(StructureId.BasalForebrain, NTEnum.ACETYLCHOLINE, "Basal forebrain neuromodulator identity rule");
        RequireExclusiveNeurotransmitter(StructureId.Snc, NTEnum.DOPAMINE, "SNc dopaminergic output rule");
        RequireExclusiveNeurotransmitter(StructureId.Vta, NTEnum.DOPAMINE, "VTA dopaminergic output rule");

        var trnAllowedTargets = new HashSet<StructureId>
        {
            StructureId.Thalamus,
            StructureId.Pulvinar,
            StructureId.MediodorsalThalamus,
            StructureId.IntralaminarThalamus,
            StructureId.MotorThalamus
        };

        if (connectivity.TryGetValue(StructureId.Trn, out var trnConnections))
        {
            foreach (var connection in trnConnections)
            {
                if (!trnAllowedTargets.Contains(connection.Target))
                {
                    violations.Add($"{Describe(StructureId.Trn, connection)} violates TRN target constraint (thalamic nuclei only).");
                }
            }
        }

        if (violations.Count == 0)
        {
            logger.LogInformation("Biological connectome semantics OK: neurotransmitter identities and inhibitory/reticular constraints validated.");
            return;
        }

        var sample = string.Join(" | ", violations.Take(6));
        logger.LogWarning("Biological connectome semantics violations: {Count}. Sample: {Sample}", violations.Count, sample);

        if (!enforceBiologicalSemantics)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Biological connectome semantics enforcement failed with {violations.Count} violation(s). " +
            $"Sample: {sample}");
    }

    // Axonal fan-out follows every anatomically compatible collateral. Striatal D1
    // and D2 medium spiny neurons are the exception to undifferentiated fan-out:
    // D1 cells project through direct output nuclei, while D2 cells project through GPe.
    internal static IReadOnlyList<SynapticConnection> ResolveRoutes(IReadOnlyList<SynapticConnection> candidates, SpikeMessage spike)
    {
        if (spike.SourceStructure != StructureId.Striatum ||
            !TryParseSourceNeuronIndex(spike.SourceNeuronId, out var neuronIndex))
        {
            return candidates;
        }

        var directPathway = (neuronIndex & 1) == 0;
        return candidates
            .Where(route => IsCompatibleStriatalProjection(route, directPathway))
            .ToArray();
    }

    private static bool IsCompatibleStriatalProjection(SynapticConnection route, bool directPathway)
    {
        if (route.Target == StructureId.GPe)
        {
            return !directPathway;
        }

        if (route.Target is StructureId.GPi or StructureId.Snr or StructureId.Snc)
        {
            return directPathway;
        }

        return true;
    }

    private static bool TryParseSourceNeuronIndex(string? neuronId, out int index)
    {
        index = 0;
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return false;
        }

        var text = neuronId.AsSpan().Trim();
        var start = text.Length - 1;
        while (start >= 0 && char.IsDigit(text[start]))
        {
            start--;
        }

        return start < text.Length - 1 && int.TryParse(text[(start + 1)..], out index);
    }

    private static bool IsMotorOutputStructure(StructureId structure)
        => structure is StructureId.PremotorCortex
            or StructureId.Sma
            or StructureId.M1
            or StructureId.MotorThalamus
            or StructureId.SpinalCordMotor;

    private static bool IsCerebellarInputTarget(StructureId structure)
        => structure is StructureId.InferiorOlive
            or StructureId.CerebellarGranule
            or StructureId.CerebellarVermis
            or StructureId.CerebellarLobules
            or StructureId.Pons;

    private static SpikeMessage BuildCerebellarEfferenceCopySpike(SpikeMessage sourceSpike, SynapticConnection route)
    {
        var baseSpike = RewriteSpikeForRoute(sourceSpike, route);
        return new SpikeMessage
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = baseSpike.TimestampMs,
            SourceStructure = baseSpike.SourceStructure,
            TargetStructure = baseSpike.TargetStructure,
            SourceNeuronId = baseSpike.SourceNeuronId,
            TargetNeuronId = baseSpike.TargetNeuronId,
            SynapseId = baseSpike.SynapseId,
            Neurotransmitter = baseSpike.Neurotransmitter,
            VesicleQuanta = Math.Clamp(baseSpike.VesicleQuanta * 0.86f, 0.05f, 16.0f),
            ReuptakeRate = Math.Clamp(baseSpike.ReuptakeRate * 1.04f, 0.5f, 120f),
            SpikeType = baseSpike.SpikeType,
            IsFeedback = true,
            ModulationContext = baseSpike.ModulationContext
        };
    }

    private static SpikeMessage RewriteSpikeForRoute(SpikeMessage spike, SynapticConnection route)
    {
        return new SpikeMessage
        {
            MessageId = spike.MessageId == Guid.Empty ? Guid.NewGuid() : spike.MessageId,
            TimestampMs = spike.TimestampMs,
            SourceStructure = spike.SourceStructure,
            TargetStructure = route.Target,
            SourceNeuronId = string.IsNullOrWhiteSpace(spike.SourceNeuronId) ? "src-auto" : spike.SourceNeuronId,
            TargetNeuronId = string.IsNullOrWhiteSpace(spike.TargetNeuronId) ? $"auto-{route.Target}" : spike.TargetNeuronId,
            SynapseId = route.SynapseId,
            Neurotransmitter = route.Neurotransmitter,
            VesicleQuanta = Math.Max(0.05f, spike.VesicleQuanta),
            ReuptakeRate = Math.Max(0.5f, spike.ReuptakeRate),
            SpikeType = spike.SpikeType,
            IsFeedback = spike.IsFeedback,
            ModulationContext = spike.ModulationContext
        };
    }

    private static Dictionary<StructureId, string> BuildServiceRegistry(
        IConfiguration configuration,
        IReadOnlyList<ServiceInstance> configuredInstances,
        ILogger logger)
    {
        var registry = new Dictionary<StructureId, string>();
        foreach (var node in configuration.GetSection("ServiceRegistry").GetChildren())
        {
            if (!Enum.TryParse<StructureId>(node.Key, ignoreCase: true, out var structure))
            {
                logger.LogWarning("ServiceRegistry contains unknown structure key '{Key}'.", node.Key);
                continue;
            }

            var endpointRaw = node.Value;
            if (string.IsNullOrWhiteSpace(endpointRaw) || !Uri.TryCreate(endpointRaw, UriKind.Absolute, out var endpoint))
            {
                logger.LogWarning("ServiceRegistry[{Structure}] has invalid endpoint '{Endpoint}'.", structure, endpointRaw);
                continue;
            }

            registry[structure] = endpoint.ToString();
        }

        if (configuredInstances.Count > 0)
        {
            foreach (var group in configuredInstances.GroupBy(i => i.StructureId))
            {
                if (registry.ContainsKey(group.Key))
                {
                    continue;
                }

                var representative = group
                    .OrderByDescending(i => string.Equals(i.Hemisphere, "M", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(i => i.InstanceKey, StringComparer.OrdinalIgnoreCase)
                    .First();
                registry[group.Key] = representative.Endpoint.ToString();
            }
        }

        return registry;
    }

    private static List<ServiceInstance> ParseConfiguredServiceInstances(IConfiguration configuration, ILogger logger)
    {
        var explicitSection = configuration.GetSection("ServiceInstances");
        var children = explicitSection.GetChildren().ToList();
        if (children.Count == 0)
        {
            return [];
        }

        var seenInstanceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var instances = new List<ServiceInstance>();
        foreach (var node in children)
        {
            if (node.Value is not null && !node.GetChildren().Any())
            {
                continue;
            }

            var structureRaw = node["StructureId"] ?? node["Structure"] ?? node["Id"];
            if (!Enum.TryParse<StructureId>(structureRaw, ignoreCase: true, out var structure))
            {
                logger.LogWarning("ServiceInstances entry '{EntryKey}' has invalid StructureId '{StructureId}'.", node.Key, structureRaw);
                continue;
            }

            var endpointRaw = node["Endpoint"] ?? node["Url"] ?? node["BaseUrl"];
            if (string.IsNullOrWhiteSpace(endpointRaw) || !Uri.TryCreate(endpointRaw, UriKind.Absolute, out var endpoint))
            {
                logger.LogWarning("ServiceInstances entry '{EntryKey}' for {Structure} has invalid endpoint '{Endpoint}'.", node.Key, structure, endpointRaw);
                continue;
            }

            var hemisphere = NormalizeHemisphere(node["Hemisphere"]);
            var instanceKey = node["InstanceKey"];
            if (string.IsNullOrWhiteSpace(instanceKey) &&
                !string.IsNullOrWhiteSpace(node.Key) &&
                !int.TryParse(node.Key, out _))
            {
                instanceKey = node.Key;
            }

            if (string.IsNullOrWhiteSpace(instanceKey))
            {
                instanceKey = $"{hemisphere}_{structure}";
            }

            instanceKey = instanceKey.Trim();
            if (!seenInstanceKeys.Add(instanceKey))
            {
                logger.LogWarning("ServiceInstances contains duplicate InstanceKey '{InstanceKey}'.", instanceKey);
                continue;
            }

            instances.Add(new ServiceInstance(structure, instanceKey, hemisphere, endpoint));
        }

        if (instances.Count > 0)
        {
            logger.LogInformation("Loaded {Count} explicit service instances from ServiceInstances.", instances.Count);
        }
        else
        {
            logger.LogWarning("ServiceInstances was provided but no valid entries were parsed. Falling back to ServiceRegistry expansion.");
        }

        return instances;
    }

    private static List<ServiceInstance> BuildServiceInstances(
        IReadOnlyDictionary<StructureId, string> registry,
        IReadOnlyList<ServiceInstance> configuredInstances,
        IConfiguration configuration,
        ILogger logger)
    {
        var hemispheresEnabled = configuration.GetValue<bool>("HemisphereHosting:Enabled", true);
        var rightPortOffset = Math.Clamp(configuration.GetValue<int>("HemisphereHosting:RightPortOffset", 1000), 1, 20000);
        var instances = new List<ServiceInstance>();
        var seenInstanceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configured in configuredInstances
            .OrderBy(i => i.StructureId.ToString(), StringComparer.Ordinal)
            .ThenBy(i => i.InstanceKey, StringComparer.OrdinalIgnoreCase))
        {
            if (!seenInstanceKeys.Add(configured.InstanceKey))
            {
                logger.LogWarning("Skipping duplicate configured instance key '{InstanceKey}'.", configured.InstanceKey);
                continue;
            }

            instances.Add(configured);
        }

        var explicitlyMappedStructures = configuredInstances
            .Select(i => i.StructureId)
            .ToHashSet();

        if (configuration.GetValue<bool>("ServiceInstances:Exclusive", false) &&
            configuredInstances.Count > 0)
        {
            logger.LogInformation("Using {Count} explicit service instances without ServiceRegistry expansion.", instances.Count);
            return instances;
        }

        foreach (var pair in registry.OrderBy(p => p.Key.ToString(), StringComparer.Ordinal))
        {
            if (explicitlyMappedStructures.Contains(pair.Key))
            {
                continue;
            }

            if (!Uri.TryCreate(pair.Value, UriKind.Absolute, out var endpoint))
            {
                logger.LogWarning("Skipping ServiceRegistry[{Structure}] due to invalid endpoint '{Endpoint}'.", pair.Key, pair.Value);
                continue;
            }

            if (!hemispheresEnabled)
            {
                var instance = new ServiceInstance(pair.Key, $"M_{pair.Key}", "M", endpoint);
                if (seenInstanceKeys.Add(instance.InstanceKey))
                {
                    instances.Add(instance);
                }
                continue;
            }

            var left = new ServiceInstance(pair.Key, $"L_{pair.Key}", "L", endpoint);
            var right = new ServiceInstance(pair.Key, $"R_{pair.Key}", "R", WithPort(endpoint, endpoint.Port + rightPortOffset));
            if (seenInstanceKeys.Add(left.InstanceKey))
            {
                instances.Add(left);
            }

            if (seenInstanceKeys.Add(right.InstanceKey))
            {
                instances.Add(right);
            }
        }

        if (instances.Count == 0)
        {
            throw new InvalidOperationException(
                "No valid service instances configured. Provide ServiceInstances entries or valid ServiceRegistry endpoints.");
        }

        return instances;
    }

    private static Uri WithPort(Uri input, int port)
    {
        var builder = new UriBuilder(input) { Port = port };
        return builder.Uri;
    }

    private static IReadOnlyList<ServiceInstance> ResolveTargetInstances(
        StructureId target,
        string sourceHemisphere,
        IReadOnlyDictionary<StructureId, List<ServiceInstance>> instancesByStructure,
        StructureId sourceStructure = default,
        string? projectionType = null)
    {
        if (!instancesByStructure.TryGetValue(target, out var instances) || instances.Count == 0)
        {
            return [];
        }

        var normalizedSourceHemisphere = NormalizeHemisphere(sourceHemisphere);
        var preferContralateralCallosal =
            sourceStructure == StructureId.CorpusCallosum &&
            target != StructureId.CorpusCallosum &&
            IsCallosalProjection(projectionType);

        if (preferContralateralCallosal &&
            normalizedSourceHemisphere is "L" or "R")
        {
            var contralateral = normalizedSourceHemisphere == "L" ? "R" : "L";
            var contralateralTargets = SelectInstancesByHemisphere(instances, contralateral);
            if (contralateralTargets.Count > 0)
            {
                return contralateralTargets;
            }
        }

        if (!string.IsNullOrWhiteSpace(sourceHemisphere) && !sourceHemisphere.Equals("M", StringComparison.OrdinalIgnoreCase))
        {
            var sameHemisphere = SelectInstancesByHemisphere(instances, normalizedSourceHemisphere);
            if (sameHemisphere.Count > 0)
            {
                return sameHemisphere;
            }
        }

        var midline = SelectInstancesByHemisphere(instances, "M");
        if (midline.Count > 0)
        {
            return midline;
        }

        return [instances[0]];
    }

    private static List<ServiceInstance> SelectInstancesByHemisphere(IReadOnlyList<ServiceInstance> instances, string hemisphere)
    {
        var selected = new List<ServiceInstance>(instances.Count);
        for (var i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            if (string.Equals(instance.Hemisphere, hemisphere, StringComparison.OrdinalIgnoreCase))
            {
                selected.Add(instance);
            }
        }

        return selected;
    }

    private static bool IsCallosalProjection(string? projectionType)
    {
        if (string.IsNullOrWhiteSpace(projectionType))
        {
            return false;
        }

        return projectionType.Contains("callosal", StringComparison.OrdinalIgnoreCase) ||
               projectionType.Contains("commissural", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveHemisphereFromInstanceKey(string instanceKey)
    {
        if (string.IsNullOrWhiteSpace(instanceKey))
        {
            return "M";
        }

        var prefix = instanceKey.AsSpan().Trim();
        if (prefix.Length >= 2 && prefix[1] == '_')
        {
            var hemi = char.ToUpperInvariant(prefix[0]);
            if (hemi is 'L' or 'R' or 'M')
            {
                return hemi == 'L' ? "L" : hemi == 'R' ? "R" : "M";
            }
        }

        return "M";
    }

    private static IReadOnlyList<StructureSnapshot> AggregateInstanceSnapshots(IReadOnlyList<InstanceStructureSnapshot> instanceSnapshots)
    {
        if (instanceSnapshots.Count == 0)
        {
            return [];
        }

        var aggregated = new List<StructureSnapshot>();
        foreach (var group in instanceSnapshots.GroupBy(s => s.StructureId))
        {
            var members = group.ToList();
            var top = members
                .SelectMany(m => m.TopActiveNeurons)
                .OrderByDescending(n => n.FiringRateHz)
                .Take(20)
                .ToList();

            if (top.Count == 0)
            {
                top.Add(new NeuronActivity($"population-{group.Key}", Math.Max(0.01f, members.Average(x => x.MeanFiringRateHz))));
            }

            var neuromod = new NeuromodState
            {
                DopamineLevel = (float)members.Average(x => x.NeuromodLocal.DopamineLevel),
                SerotoninLevel = (float)members.Average(x => x.NeuromodLocal.SerotoninLevel),
                AcetylcholineLevel = (float)members.Average(x => x.NeuromodLocal.AcetylcholineLevel),
                NorepinephrineLevel = (float)members.Average(x => x.NeuromodLocal.NorepinephrineLevel)
            };

            var dominantRhythm = members
                .GroupBy(x => x.DominantRhythm)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Average(x => x.MeanFiringRateHz))
                .Select(g => g.Key)
                .FirstOrDefault();

            aggregated.Add(new StructureSnapshot(
                group.Key,
                members.Sum(x => x.ActiveNeuronCount),
                (float)members.Average(x => x.MeanFiringRateHz),
                dominantRhythm,
                top,
                NeuromodState.Clamp(neuromod),
                members.Sum(x => x.SpikeInCount),
                members.Sum(x => x.SpikeOutCount),
                members.Sum(x => x.FeedbackQueueDepth),
                AverageMicrotubuleDiagnostics(members),
                AverageBodySchemaDiagnostics(members),
                AverageBasalGangliaDiagnostics(members),
                AverageCerebellarDiagnostics(members),
                AverageVestibuloReticularDiagnostics(members),
                AverageSuperiorColliculusDiagnostics(members),
                AverageHippocampalSpatialDiagnostics(members),
                AverageSalienceAffectDiagnostics(members),
                AveragePrefrontalWorkingMemoryDiagnostics(members),
                AverageThalamicAttentionGateDiagnostics(members),
                AverageHypothalamicHomeostasisDiagnostics(members),
                AverageSleepWakeArousalDiagnostics(members),
                AverageDescendingDefenseDiagnostics(members),
                AverageDopamineRewardDiagnostics(members),
                AverageSeptohippocampalThetaDiagnostics(members),
                AverageSpinalProprioceptiveDiagnostics(members),
                AverageOlfactoryLimbicMemoryDiagnostics(members),
                AverageAuditoryLanguageMotorDiagnostics(members),
                AverageVisualObjectRecognitionDiagnostics(members),
                AverageActionSelectionDiagnostics(members),
                AveragePerceptEnsembleDiagnostics(members),
                AverageSynapticMemoryDiagnostics(members),
                AverageNeuronalAttentionWorkspaceDiagnostics(members),
                AverageNeuronalSleepConsolidationDiagnostics(members)));
        }

        return EnrichActionSelectionDiagnostics(EnrichVisualObjectRecognitionDiagnostics(
            EnrichAuditoryLanguageMotorDiagnostics(
                EnrichOlfactoryLimbicMemoryDiagnostics(
                    EnrichSpinalProprioceptiveDiagnostics(
                        EnrichSeptohippocampalThetaDiagnostics(
                            EnrichDopamineRewardDiagnostics(
                                EnrichDescendingDefenseDiagnostics(
                                    EnrichSleepWakeArousalDiagnostics(
                                        EnrichHypothalamicHomeostasisDiagnostics(
                                            EnrichThalamicAttentionGateDiagnostics(
                                                EnrichPrefrontalWorkingMemoryDiagnostics(
                                                    EnrichSalienceAffectDiagnostics(
                                                        EnrichHippocampalSpatialMemoryDiagnostics(
                                                            EnrichSuperiorColliculusOrientingDiagnostics(
                                                                EnrichVestibuloReticularPostureDiagnostics(
                                                                    EnrichCerebellarCorrectionDiagnostics(
                                                                        EnrichBasalGangliaActionSelectionDiagnostics(aggregated).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()).ToList()));
    }

    private static IReadOnlyList<StructureSnapshot> MergeSpontaneousNeuronHighlights(
        IReadOnlyList<StructureSnapshot> snapshots,
        IReadOnlyDictionary<StructureId, HashSet<string>> spontaneousNeuronIdsByStructure)
    {
        if (snapshots.Count == 0 || spontaneousNeuronIdsByStructure.Count == 0)
        {
            return snapshots;
        }

        var merged = new List<StructureSnapshot>(snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            if (!spontaneousNeuronIdsByStructure.TryGetValue(snapshot.StructureId, out var ids) || ids.Count == 0)
            {
                merged.Add(snapshot);
                continue;
            }

            var top = snapshot.TopActiveNeurons.ToList();
            var existing = new HashSet<string>(top.Select(x => x.NeuronId), StringComparer.Ordinal);
            var fallbackRate = Math.Max(0.5f, snapshot.MeanFiringRateHz);

            foreach (var neuronId in ids)
            {
                if (!existing.Add(neuronId))
                {
                    continue;
                }

                top.Insert(0, new NeuronActivity(neuronId, fallbackRate + 0.25f));
                if (top.Count >= 20)
                {
                    break;
                }
            }

            merged.Add(new StructureSnapshot(
                snapshot.StructureId,
                snapshot.ActiveNeuronCount,
                snapshot.MeanFiringRateHz,
                snapshot.DominantRhythm,
                top,
                snapshot.NeuromodLocal,
                snapshot.SpikeInCount,
                snapshot.SpikeOutCount,
                snapshot.FeedbackQueueDepth,
                snapshot.MicrotubuleDiagnostics,
                snapshot.BodySchemaDiagnostics,
                snapshot.BasalGangliaDiagnostics,
                snapshot.CerebellarDiagnostics,
                snapshot.VestibuloReticularDiagnostics,
                snapshot.SuperiorColliculusDiagnostics,
                snapshot.HippocampalSpatialDiagnostics,
                snapshot.SalienceAffectDiagnostics,
                snapshot.PrefrontalWorkingMemoryDiagnostics,
                snapshot.ThalamicAttentionGateDiagnostics,
                snapshot.HypothalamicHomeostasisDiagnostics,
                snapshot.SleepWakeArousalDiagnostics,
                snapshot.DescendingDefenseDiagnostics,
                snapshot.DopamineRewardDiagnostics,
                snapshot.SeptohippocampalThetaDiagnostics,
                snapshot.SpinalProprioceptiveDiagnostics,
                snapshot.OlfactoryLimbicMemoryDiagnostics,
                snapshot.AuditoryLanguageMotorDiagnostics,
                snapshot.VisualObjectRecognitionDiagnostics,
                snapshot.ActionSelectionDiagnostics,
                snapshot.PerceptEnsembleDiagnostics,
                snapshot.SynapticMemoryDiagnostics,
                snapshot.NeuronalAttentionWorkspaceDiagnostics,
                snapshot.NeuronalSleepConsolidationDiagnostics));
        }

        return merged;
    }

    private static MicrotubuleDiagnostics? AverageMicrotubuleDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.MicrotubuleDiagnostics)
            .Where(m => m != null)
            .Cast<MicrotubuleDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var enabledCount = diagnostics.Count(d => d.Enabled);
        var experimentalCount = diagnostics.Count(d => d.Experimental);
        var mode = diagnostics
            .GroupBy(d => d.Mode, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "classical";

        return new MicrotubuleDiagnostics(
            mode,
            enabledCount > diagnostics.Count / 2,
            experimentalCount > diagnostics.Count / 2,
            (float)diagnostics.Average(d => d.MeanStability),
            (float)diagnostics.Average(d => d.MeanSpineInvasionEligibility),
            (float)diagnostics.Average(d => d.MeanTransportSupport),
            (float)diagnostics.Average(d => d.MeanOpticalCollectiveBias),
            (float)diagnostics.Average(d => d.MeanRadicalPairSensitivity),
            (float)diagnostics.Average(d => d.MeanPlasticitySupport),
            (float)diagnostics.Average(d => d.MeanTracePersistenceSupport),
            (float)diagnostics.Average(d => d.MeanIntegrationGain),
            (float)diagnostics.Average(d => d.MeanConsolidationSupport));
    }

    private static BodySchemaDiagnostics? AverageBodySchemaDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.BodySchemaDiagnostics)
            .Where(m => m != null)
            .Cast<BodySchemaDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var faceHead = (float)diagnostics.Average(d => d.FaceHeadActivation);
        var handArm = (float)diagnostics.Average(d => d.HandArmActivation);
        var trunk = (float)diagnostics.Average(d => d.TrunkActivation);
        var legFoot = (float)diagnostics.Average(d => d.LegFootActivation);
        var nearBody = (float)diagnostics.Average(d => d.NearBodyActivation);
        var leftPeripersonal = (float)diagnostics.Average(d => d.LeftPeripersonalActivation);
        var rightPeripersonal = (float)diagnostics.Average(d => d.RightPeripersonalActivation);
        var farSpace = (float)diagnostics.Average(d => d.FarSpaceActivation);

        return new BodySchemaDiagnostics(
            SelectDominantBodyZone(faceHead, handArm, trunk, legFoot),
            SelectDominantSpatialZone(diagnostics, nearBody, leftPeripersonal, rightPeripersonal, farSpace),
            faceHead,
            handArm,
            trunk,
            legFoot,
            nearBody,
            leftPeripersonal,
            rightPeripersonal,
            farSpace);
    }

    private static string SelectDominantBodyZone(float faceHead, float handArm, float trunk, float legFoot)
    {
        var best = "FaceHead";
        var bestValue = faceHead;
        if (handArm > bestValue)
        {
            best = "HandArm";
            bestValue = handArm;
        }

        if (trunk > bestValue)
        {
            best = "Trunk";
            bestValue = trunk;
        }

        if (legFoot > bestValue)
        {
            best = "LegFoot";
        }

        return best;
    }

    private static string SelectDominantSpatialZone(
        IReadOnlyList<BodySchemaDiagnostics> diagnostics,
        float nearBody,
        float leftPeripersonal,
        float rightPeripersonal,
        float farSpace)
    {
        if (diagnostics.All(d => string.Equals(d.DominantSpatialZone, "Somatotopic", StringComparison.OrdinalIgnoreCase)))
        {
            return "Somatotopic";
        }

        var best = "NearBody";
        var bestValue = nearBody;
        if (leftPeripersonal > bestValue)
        {
            best = "LeftPeripersonal";
            bestValue = leftPeripersonal;
        }

        if (rightPeripersonal > bestValue)
        {
            best = "RightPeripersonal";
            bestValue = rightPeripersonal;
        }

        if (farSpace > bestValue)
        {
            best = "FarSpace";
        }

        return best;
    }

    private static BasalGangliaDiagnostics? AverageBasalGangliaDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.BasalGangliaDiagnostics)
            .Where(m => m != null)
            .Cast<BasalGangliaDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var direct = (float)diagnostics.Average(d => d.DirectPathwayActivation);
        var indirect = (float)diagnostics.Average(d => d.IndirectPathwayActivation);
        var hyperdirect = (float)diagnostics.Average(d => d.HyperdirectPathwayActivation);
        var output = (float)diagnostics.Average(d => d.OutputNucleusInhibition);
        var disinhibition = (float)diagnostics.Average(d => d.ThalamicDisinhibition);
        var dopamine = (float)diagnostics.Average(d => d.DopamineModulation);
        var bias = direct - Math.Max(indirect, hyperdirect);

        return new BasalGangliaDiagnostics(
            SelectBasalGangliaMode(direct, indirect, hyperdirect, output),
            direct,
            indirect,
            hyperdirect,
            output,
            disinhibition,
            dopamine,
            bias);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichBasalGangliaActionSelectionDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var striatum = GetRate(byId, StructureId.Striatum) + (GetRate(byId, StructureId.NucleusAccumbens) * 0.35f);
        var striatalLocal = GetDiagnostics(byId, StructureId.Striatum);
        var direct = striatalLocal?.DirectPathwayActivation ?? (striatum * 0.55f);
        var indirect = Math.Max(
            striatalLocal?.IndirectPathwayActivation ?? (striatum * 0.45f),
            Math.Max(GetRate(byId, StructureId.GPe), GetRate(byId, StructureId.GlobusPallidus) * 0.75f));
        var hyperdirect = GetRate(byId, StructureId.Stn);
        var output = Math.Max(GetRate(byId, StructureId.GPi), GetRate(byId, StructureId.Snr));
        var dopamine = Math.Clamp(
            Math.Max(
                GetDiagnostics(byId, StructureId.Snc)?.DopamineModulation ?? 0f,
                byId.TryGetValue(StructureId.Snc, out var snc) ? snc.NeuromodLocal.DopamineLevel + (snc.MeanFiringRateHz / 50f) : 0f),
            0f,
            2f);

        direct *= 0.75f + Math.Clamp(dopamine, 0f, 1f);
        indirect *= 1.25f - (Math.Clamp(dopamine, 0f, 1f) * 0.50f);
        var thalamicDisinhibition = Math.Max(0f, direct - (output * 0.50f));
        var bias = direct - Math.Max(indirect, hyperdirect);
        var composite = new BasalGangliaDiagnostics(
            SelectBasalGangliaMode(direct, indirect, hyperdirect, output),
            direct,
            indirect,
            hyperdirect,
            output,
            thalamicDisinhibition,
            dopamine,
            bias);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesBasalGangliaComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { BasalGangliaDiagnostics = composite };
        }

        return snapshots;
    }

    private static ActionSelectionDiagnostics? AverageActionSelectionDiagnostics(
        IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(static member => member.ActionSelectionDiagnostics)
            .Where(static item => item is not null)
            .Cast<ActionSelectionDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return null;
        }

        var channels = new ActionChannelActivity[4];
        for (var channel = 0; channel < channels.Length; channel++)
        {
            var values = diagnostics
                .SelectMany(static item => item.Channels)
                .Where(item => item.ChannelIndex == channel)
                .ToArray();
            channels[channel] = AverageActionChannel(channel, values);
        }

        var (selected, margin) = SelectActionChannel(channels);
        return new ActionSelectionDiagnostics(
            members[0].StructureId,
            channels,
            selected,
            margin,
            (float)diagnostics.Average(static item => item.DopamineModulation));
    }

    private static PerceptEnsembleDiagnostics? AveragePerceptEnsembleDiagnostics(
        IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(static member => member.PerceptEnsembleDiagnostics)
            .Where(static item => item is not null)
            .Cast<PerceptEnsembleDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return null;
        }

        var ensembles = new PerceptEnsembleActivity[8];
        for (var ensemble = 0; ensemble < ensembles.Length; ensemble++)
        {
            var values = diagnostics
                .SelectMany(static item => item.Ensembles)
                .Where(item => item.EnsembleIndex == ensemble)
                .ToArray();
            ensembles[ensemble] = values.Length == 0
                ? new PerceptEnsembleActivity(ensemble, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f)
                : new PerceptEnsembleActivity(
                    ensemble,
                    (float)values.Average(static item => item.VisualFeatureDrive),
                    (float)values.Average(static item => item.MotionConsistency),
                    (float)values.Average(static item => item.AuditoryFeatureDrive),
                    (float)values.Average(static item => item.SomatosensoryFeatureDrive),
                    (float)values.Average(static item => item.RecurrentBinding),
                    (float)values.Average(static item => item.Salience),
                    (float)values.Average(static item => item.Familiarity),
                    (float)values.Average(static item => item.HippocampalIndex),
                    (float)values.Average(static item => item.Novelty),
                    (float)values.Average(static item => item.Confidence));
        }

        var ranked = ensembles
            .OrderByDescending(static item => item.Confidence)
            .ThenBy(static item => item.EnsembleIndex)
            .ToArray();
        var margin = ranked.Length > 1
            ? Math.Max(0f, ranked[0].Confidence - ranked[1].Confidence)
            : 0f;
        return new PerceptEnsembleDiagnostics(
            members[0].StructureId,
            ensembles,
            ranked[0].EnsembleIndex,
            margin,
            (float)diagnostics.Average(static item => item.Persistence));
    }

    private static SynapticMemoryDiagnostics? AverageSynapticMemoryDiagnostics(
        IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(static member => member.SynapticMemoryDiagnostics)
            .Where(static item => item is not null)
            .Cast<SynapticMemoryDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return null;
        }

        var ensembles = new SynapticMemoryEnsembleActivity[8];
        for (var ensemble = 0; ensemble < ensembles.Length; ensemble++)
        {
            var values = diagnostics
                .SelectMany(static item => item.Ensembles)
                .Where(item => item.EnsembleIndex == ensemble)
                .ToArray();
            ensembles[ensemble] = values.Length == 0
                ? new SynapticMemoryEnsembleActivity(ensemble, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0)
                : new SynapticMemoryEnsembleActivity(
                    ensemble,
                    (float)values.Average(static item => item.CueDrive),
                    (float)values.Average(static item => item.EngramStrength),
                    (float)values.Average(static item => item.RecallActivation),
                    (float)values.Average(static item => item.EligibilityTrace),
                    (float)values.Average(static item => item.SynapticTag),
                    (float)values.Average(static item => item.Interference),
                    (float)values.Average(static item => item.Extinction),
                    (float)values.Average(static item => item.Consolidation),
                    values.Sum(static item => item.SupportingSynapses));
        }

        var ranked = ensembles
            .OrderByDescending(static item => item.RecallActivation)
            .ThenBy(static item => item.EnsembleIndex)
            .ToArray();
        var recalled = ranked[0].RecallActivation > 0f ? ranked[0].EnsembleIndex : -1;
        var margin = ranked.Length > 1
            ? Math.Max(0f, ranked[0].RecallActivation - ranked[1].RecallActivation)
            : 0f;
        return new SynapticMemoryDiagnostics(
            members[0].StructureId,
            diagnostics[0].MemoryRole,
            ensembles,
            recalled,
            margin,
            (float)diagnostics.Average(static item => item.HippocampalDependence),
            (float)diagnostics.Average(static item => item.CorticalConsolidation),
            diagnostics.Sum(static item => item.LearnedSynapseCount));
    }

    private static NeuronalAttentionWorkspaceDiagnostics? AverageNeuronalAttentionWorkspaceDiagnostics(
        IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(static member => member.NeuronalAttentionWorkspaceDiagnostics)
            .Where(static item => item is not null)
            .Cast<NeuronalAttentionWorkspaceDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return null;
        }

        var channels = new AttentionWorkspaceChannelActivity[7];
        for (var channel = 0; channel < channels.Length; channel++)
        {
            var values = diagnostics
                .SelectMany(static item => item.Channels)
                .Where(item => item.ChannelIndex == channel)
                .ToArray();
            channels[channel] = values.Length == 0
                ? new AttentionWorkspaceChannelActivity(channel, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f)
                : new AttentionWorkspaceChannelActivity(
                    channel,
                    (float)values.Average(static item => item.SensoryDrive),
                    (float)values.Average(static item => item.PulvinarPriority),
                    (float)values.Average(static item => item.TrnSuppression),
                    (float)values.Average(static item => item.ThalamicRelay),
                    (float)values.Average(static item => item.MediodorsalSupport),
                    (float)values.Average(static item => item.PfcMaintenance),
                    (float)values.Average(static item => item.IntralaminarBroadcast),
                    (float)values.Average(static item => item.CompetitionScore));
        }

        var ranked = channels
            .OrderByDescending(static item => item.CompetitionScore)
            .ThenBy(static item => item.ChannelIndex)
            .ToArray();
        var margin = Math.Max(0f, ranked[0].CompetitionScore - ranked[1].CompetitionScore);
        var maintained = diagnostics
            .SelectMany(static item => item.MaintainedChannels)
            .Distinct()
            .Take(4)
            .ToArray();
        return new NeuronalAttentionWorkspaceDiagnostics(
            members[0].StructureId,
            channels,
            ranked[0].CompetitionScore > 0f && margin > 0f ? ranked[0].ChannelIndex : -1,
            margin,
            maintained,
            (float)diagnostics.Average(static item => item.DistractorSuppression));
    }

    private static NeuronalSleepConsolidationDiagnostics? AverageNeuronalSleepConsolidationDiagnostics(
        IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(static member => member.NeuronalSleepConsolidationDiagnostics)
            .Where(static item => item is not null)
            .Cast<NeuronalSleepConsolidationDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return null;
        }

        var stateChannels = new SleepStateChannelActivity[3];
        for (var channel = 0; channel < stateChannels.Length; channel++)
        {
            var values = diagnostics
                .SelectMany(static item => item.StateChannels)
                .Where(item => item.StateChannel == channel)
                .ToArray();
            stateChannels[channel] = values.Length == 0
                ? new SleepStateChannelActivity(channel, 0f, 0f, 0f, 0f, 0f, 0f, 0f)
                : new SleepStateChannelActivity(
                    channel,
                    (float)values.Average(static item => item.HomeostaticDrive),
                    (float)values.Average(static item => item.WakeDrive),
                    (float)values.Average(static item => item.NremDrive),
                    (float)values.Average(static item => item.RemDrive),
                    (float)values.Average(static item => item.SpindleSynchrony),
                    (float)values.Average(static item => item.SlowWaveSynchrony),
                    (float)values.Average(static item => item.ReplayGate));
        }

        var replayEnsembles = new SleepReplayEnsembleActivity[8];
        for (var ensemble = 0; ensemble < replayEnsembles.Length; ensemble++)
        {
            var values = diagnostics
                .SelectMany(static item => item.ReplayEnsembles)
                .Where(item => item.EnsembleIndex == ensemble)
                .ToArray();
            replayEnsembles[ensemble] = values.Length == 0
                ? new SleepReplayEnsembleActivity(ensemble, 0f, 0f, 0f, 0f, 0f, 0f, 0f)
                : new SleepReplayEnsembleActivity(
                    ensemble,
                    (float)values.Average(static item => item.HippocampalBurst),
                    (float)values.Average(static item => item.SpindleCoupling),
                    (float)values.Average(static item => item.SlowWaveCoupling),
                    (float)values.Average(static item => item.CorticalEcho),
                    (float)values.Average(static item => item.EngramStrength),
                    (float)values.Average(static item => item.Interference),
                    (float)values.Average(static item => item.ConsolidationGain));
        }

        return new NeuronalSleepConsolidationDiagnostics(
            members[0].StructureId,
            stateChannels,
            replayEnsembles);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichActionSelectionDiagnostics(
        IReadOnlyList<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(static snapshot => snapshot.StructureId);
        var channels = new ActionChannelActivity[4];
        for (var channel = 0; channel < channels.Length; channel++)
        {
            var pfc = GetActionChannel(byId, StructureId.Pfc, channel);
            var acc = GetActionChannel(byId, StructureId.Acc, channel);
            var premotor = GetActionChannel(byId, StructureId.PremotorCortex, channel);
            var sma = GetActionChannel(byId, StructureId.Sma, channel);
            var striatum = GetActionChannel(byId, StructureId.Striatum, channel);
            var stn = GetActionChannel(byId, StructureId.Stn, channel);
            var gpi = GetActionChannel(byId, StructureId.GPi, channel);
            var snr = GetActionChannel(byId, StructureId.Snr, channel);
            var motorThalamus = GetActionChannel(byId, StructureId.MotorThalamus, channel);

            var proposal = (pfc.ProposalDrive * 0.30f) +
                (acc.ProposalDrive * 0.10f) +
                (premotor.ProposalDrive * 0.35f) +
                (sma.ProposalDrive * 0.25f);
            var direct = striatum.DirectPathwayActivation;
            var indirect = striatum.IndirectPathwayActivation;
            var hyperdirect = stn.HyperdirectSuppression;
            var output = Math.Max(gpi.OutputNucleusInhibition, snr.OutputNucleusInhibition);
            var thalamic = motorThalamus.ThalamicRelayActivation;
            var learned = Math.Clamp(striatum.LearnedSynapticStrength / 5f, 0f, 1f);
            var eligibility = Math.Clamp(striatum.EligibilityTrace, -1f, 1f);
            var score = (proposal * 0.30f) +
                (direct * 0.32f) +
                (thalamic * 0.18f) +
                (learned * 0.08f) +
                (Math.Max(0f, eligibility) * 0.04f) -
                (indirect * 0.20f) -
                (hyperdirect * 0.28f) -
                (output * 0.42f);
            channels[channel] = new ActionChannelActivity(
                channel,
                proposal,
                direct,
                indirect,
                hyperdirect,
                output,
                thalamic,
                eligibility,
                striatum.LearnedSynapticStrength,
                score);
        }

        var (selected, margin) = SelectActionChannel(channels);
        var dopamine = snapshots
            .Select(static snapshot => snapshot.ActionSelectionDiagnostics?.DopamineModulation)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .DefaultIfEmpty(0f)
            .Average();
        var composite = new ActionSelectionDiagnostics(
            StructureId.Striatum,
            channels,
            selected,
            margin,
            dopamine);

        var enriched = snapshots.ToList();
        for (var i = 0; i < enriched.Count; i++)
        {
            if (CarriesActionSelectionComposite(enriched[i].StructureId))
            {
                enriched[i] = enriched[i] with { ActionSelectionDiagnostics = composite };
            }
        }

        return enriched;
    }

    private static ActionChannelActivity GetActionChannel(
        IReadOnlyDictionary<StructureId, StructureSnapshot> snapshots,
        StructureId structure,
        int channel)
        => snapshots.TryGetValue(structure, out var snapshot)
            ? snapshot.ActionSelectionDiagnostics?.Channels.FirstOrDefault(item => item.ChannelIndex == channel)
                ?? EmptyActionChannel(channel)
            : EmptyActionChannel(channel);

    private static ActionChannelActivity AverageActionChannel(
        int channel,
        IReadOnlyList<ActionChannelActivity> values)
        => values.Count == 0
            ? EmptyActionChannel(channel)
            : new ActionChannelActivity(
                channel,
                (float)values.Average(static item => item.ProposalDrive),
                (float)values.Average(static item => item.DirectPathwayActivation),
                (float)values.Average(static item => item.IndirectPathwayActivation),
                (float)values.Average(static item => item.HyperdirectSuppression),
                (float)values.Average(static item => item.OutputNucleusInhibition),
                (float)values.Average(static item => item.ThalamicRelayActivation),
                (float)values.Average(static item => item.EligibilityTrace),
                (float)values.Average(static item => item.LearnedSynapticStrength),
                (float)values.Average(static item => item.SelectionScore));

    private static ActionChannelActivity EmptyActionChannel(int channel)
        => new(channel, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

    private static (int Selected, float Margin) SelectActionChannel(
        IReadOnlyList<ActionChannelActivity> channels)
    {
        if (channels.Count == 0)
        {
            return (-1, 0f);
        }

        var ordered = channels
            .OrderByDescending(static channel => channel.SelectionScore)
            .ThenBy(static channel => channel.ChannelIndex)
            .Take(2)
            .ToArray();
        var margin = ordered.Length > 1
            ? Math.Max(0f, ordered[0].SelectionScore - ordered[1].SelectionScore)
            : 0f;
        return (ordered[0].ChannelIndex, margin);
    }

    private static bool CarriesActionSelectionComposite(StructureId structure)
        => structure is StructureId.Pfc
            or StructureId.Acc
            or StructureId.PremotorCortex
            or StructureId.Sma
            or StructureId.Striatum
            or StructureId.GPe
            or StructureId.GlobusPallidus
            or StructureId.GPi
            or StructureId.Stn
            or StructureId.Snr
            or StructureId.MotorThalamus;

    private static float GetRate(IReadOnlyDictionary<StructureId, StructureSnapshot> snapshots, StructureId structureId)
        => snapshots.TryGetValue(structureId, out var snapshot) ? snapshot.MeanFiringRateHz : 0f;

    private static BasalGangliaDiagnostics? GetDiagnostics(IReadOnlyDictionary<StructureId, StructureSnapshot> snapshots, StructureId structureId)
        => snapshots.TryGetValue(structureId, out var snapshot) ? snapshot.BasalGangliaDiagnostics : null;

    private static bool CarriesBasalGangliaComposite(StructureId structureId)
        => structureId is StructureId.Striatum
            or StructureId.NucleusAccumbens
            or StructureId.GlobusPallidus
            or StructureId.VentralPallidum
            or StructureId.GPe
            or StructureId.GPi
            or StructureId.Stn
            or StructureId.Snr
            or StructureId.Snc
            or StructureId.MotorThalamus;

    private static string SelectBasalGangliaMode(float direct, float indirect, float hyperdirect, float output)
    {
        var suppressive = Math.Max(indirect, Math.Max(hyperdirect, output));
        if (direct > suppressive * 1.15f && direct > 0.05f)
        {
            return "Go";
        }

        if (hyperdirect > Math.Max(direct, indirect) * 1.10f && hyperdirect > 0.05f)
        {
            return "Stop";
        }

        return "Hold";
    }

    private static CerebellarDiagnostics? AverageCerebellarDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.CerebellarDiagnostics)
            .Where(m => m != null)
            .Cast<CerebellarDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var mossy = (float)diagnostics.Average(d => d.MossyFiberDrive);
        var climbing = (float)diagnostics.Average(d => d.ClimbingFiberError);
        var purkinje = (float)diagnostics.Average(d => d.PurkinjeInhibition);
        var dcn = (float)diagnostics.Average(d => d.DeepNucleusOutput);
        var vermis = (float)diagnostics.Average(d => d.VermisStabilization);
        var gain = (float)diagnostics.Average(d => d.CorrectionGain);
        var error = (float)diagnostics.Average(d => d.PredictionError);

        return new CerebellarDiagnostics(
            SelectCerebellarCorrectionMode(mossy, climbing, purkinje, dcn, gain),
            mossy,
            climbing,
            purkinje,
            dcn,
            vermis,
            gain,
            error);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichCerebellarCorrectionDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var granule = GetRate(byId, StructureId.CerebellarGranule);
        var lobules = GetRate(byId, StructureId.CerebellarLobules);
        var vermisRate = GetRate(byId, StructureId.CerebellarVermis);
        var purkinje = GetRate(byId, StructureId.PurkinjeCellLayer);
        var dcn = GetRate(byId, StructureId.DeepCerebellarNuclei);
        var olive = GetRate(byId, StructureId.InferiorOlive);

        var mossy = granule + (lobules * 0.45f) + (vermisRate * 0.25f);
        var climbing = olive;
        var vermis = vermisRate;
        var deepOutput = dcn + (lobules * 0.20f) + (vermisRate * 0.15f);
        var correctionGain = Math.Max(0f, deepOutput + (climbing * 0.45f) + (vermis * 0.20f) - (purkinje * 0.35f));
        var predictionError = climbing + Math.Max(0f, mossy - purkinje) * 0.10f;
        var composite = new CerebellarDiagnostics(
            SelectCerebellarCorrectionMode(mossy, climbing, purkinje, deepOutput, correctionGain),
            mossy,
            climbing,
            purkinje,
            deepOutput,
            vermis,
            correctionGain,
            predictionError);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesCerebellarComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { CerebellarDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesCerebellarComposite(StructureId structureId)
        => structureId is StructureId.CerebellarGranule
            or StructureId.CerebellarVermis
            or StructureId.CerebellarLobules
            or StructureId.PurkinjeCellLayer
            or StructureId.DeepCerebellarNuclei
            or StructureId.InferiorOlive
            or StructureId.MotorThalamus
            or StructureId.M1;

    private static string SelectCerebellarCorrectionMode(float mossy, float climbing, float purkinje, float dcn, float correctionGain)
    {
        if (climbing > Math.Max(0.20f, mossy * 0.85f) && dcn > purkinje * 1.15f)
        {
            return "Overcorrecting";
        }

        if (climbing > 0.05f || correctionGain > Math.Max(0.08f, purkinje * 0.35f))
        {
            return "Correcting";
        }

        return "Stable";
    }

    private static VestibuloReticularDiagnostics? AverageVestibuloReticularDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.VestibuloReticularDiagnostics)
            .Where(m => m != null)
            .Cast<VestibuloReticularDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var vestibular = (float)diagnostics.Average(d => d.VestibularDrive);
        var reticular = (float)diagnostics.Average(d => d.ReticularArousal);
        var vermis = (float)diagnostics.Average(d => d.VermisBalanceCorrection);
        var spinalTone = (float)diagnostics.Average(d => d.SpinalMotorTone);
        var stability = (float)diagnostics.Average(d => d.PostureStability);
        var error = (float)diagnostics.Average(d => d.BalanceError);

        return new VestibuloReticularDiagnostics(
            SelectVestibuloReticularMode(vestibular, reticular, vermis, spinalTone, error),
            vestibular,
            reticular,
            vermis,
            spinalTone,
            stability,
            error);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichVestibuloReticularPostureDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var vestibular = GetRate(byId, StructureId.VestibularNuclei);
        var reticular = GetRate(byId, StructureId.ReticularFormation);
        var vermis = GetRate(byId, StructureId.CerebellarVermis);
        var spinalTone = GetRate(byId, StructureId.SpinalCordMotor);
        var norepinephrine = byId.TryGetValue(StructureId.ReticularFormation, out var rf)
            ? rf.NeuromodLocal.NorepinephrineLevel
            : 0f;

        var arousal = reticular * (0.80f + Math.Clamp(norepinephrine, 0f, 1f) * 0.55f);
        var balanceError = Math.Max(0f, vestibular - ((vermis * 0.55f) + (spinalTone * 0.25f)));
        var postureStability = Math.Clamp((vermis * 0.35f) + (spinalTone * 0.30f) + (arousal * 0.20f) - (balanceError * 0.25f), 0f, 120f);
        var composite = new VestibuloReticularDiagnostics(
            SelectVestibuloReticularMode(vestibular, arousal, vermis, spinalTone, balanceError),
            vestibular,
            arousal,
            vermis,
            spinalTone,
            postureStability,
            balanceError);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesVestibuloReticularComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { VestibuloReticularDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesVestibuloReticularComposite(StructureId structureId)
        => structureId is StructureId.VestibularNuclei
            or StructureId.ReticularFormation
            or StructureId.CerebellarVermis
            or StructureId.SpinalCordMotor
            or StructureId.CerebellarLobules
            or StructureId.DeepCerebellarNuclei
            or StructureId.M1;

    private static string SelectVestibuloReticularMode(float vestibular, float reticular, float vermis, float spinalTone, float balanceError)
    {
        if (balanceError > Math.Max(0.20f, vermis * 0.75f))
        {
            return "Rebalancing";
        }

        if (reticular > Math.Max(0.18f, spinalTone * 1.20f))
        {
            return "Aroused";
        }

        return "Steady";
    }

    private static SuperiorColliculusDiagnostics? AverageSuperiorColliculusDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.SuperiorColliculusDiagnostics)
            .Where(m => m != null)
            .Cast<SuperiorColliculusDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var visual = (float)diagnostics.Average(d => d.VisualOrientingDrive);
        var auditory = (float)diagnostics.Average(d => d.AuditoryOrientingDrive);
        var nigrotectal = (float)diagnostics.Average(d => d.NigrotectalInhibition);
        var pulvinar = (float)diagnostics.Average(d => d.PulvinarAttention);
        var headEye = (float)diagnostics.Average(d => d.HeadEyeCommand);
        var readiness = (float)diagnostics.Average(d => d.SaccadeReadiness);
        var salience = (float)diagnostics.Average(d => d.SalienceBias);

        return new SuperiorColliculusDiagnostics(
            SelectSuperiorColliculusOrientingMode(readiness, (visual * 0.65f) + (auditory * 0.45f), nigrotectal, headEye),
            visual,
            auditory,
            nigrotectal,
            pulvinar,
            headEye,
            readiness,
            salience);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichSuperiorColliculusOrientingDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var sc = GetRate(byId, StructureId.SuperiorColliculus);
        var visual =
            GetRate(byId, StructureId.Retina) +
            (GetRate(byId, StructureId.V1) * 0.35f) +
            (GetRate(byId, StructureId.Mt) * 0.25f) +
            (sc * 0.55f);
        var auditory =
            GetRate(byId, StructureId.InferiorColliculus) +
            (GetRate(byId, StructureId.A1) * 0.20f);
        var nigrotectal = GetRate(byId, StructureId.Snr);
        var pulvinar = GetRate(byId, StructureId.Pulvinar);
        var headEye =
            (sc * 0.70f) +
            (GetRate(byId, StructureId.PremotorCortex) * 0.30f) +
            (GetRate(byId, StructureId.Pons) * 0.25f) +
            (GetRate(byId, StructureId.M1) * 0.15f);
        var acetylcholine = byId.TryGetValue(StructureId.Pulvinar, out var pulvinarSnapshot)
            ? pulvinarSnapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var norepinephrine = byId.TryGetValue(StructureId.SuperiorColliculus, out var scSnapshot)
            ? scSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var salienceGain = 0.75f +
            (Math.Clamp(acetylcholine, 0f, 1f) * 0.20f) +
            (Math.Clamp(norepinephrine, 0f, 1f) * 0.30f);
        var sensoryDrive = ((visual * 0.65f) + (auditory * 0.45f)) * salienceGain;
        var saccadeReadiness = Math.Max(0f, sensoryDrive + (pulvinar * 0.25f) + (headEye * 0.35f) - (nigrotectal * 0.50f));
        var salienceBias = Math.Max(0f, sensoryDrive + (pulvinar * 0.20f) - (nigrotectal * 0.25f));
        var composite = new SuperiorColliculusDiagnostics(
            SelectSuperiorColliculusOrientingMode(saccadeReadiness, sensoryDrive, nigrotectal, headEye),
            visual,
            auditory,
            nigrotectal,
            pulvinar,
            headEye,
            saccadeReadiness,
            salienceBias);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesSuperiorColliculusComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { SuperiorColliculusDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesSuperiorColliculusComposite(StructureId structureId)
        => structureId is StructureId.Retina
            or StructureId.V1
            or StructureId.Mt
            or StructureId.InferiorColliculus
            or StructureId.SuperiorColliculus
            or StructureId.Snr
            or StructureId.Pulvinar
            or StructureId.Ppc
            or StructureId.PremotorCortex
            or StructureId.Pons
            or StructureId.M1;

    private static string SelectSuperiorColliculusOrientingMode(float saccadeReadiness, float sensoryDrive, float nigrotectal, float headEye)
    {
        if (nigrotectal > Math.Max(sensoryDrive, headEye) * 1.20f && nigrotectal > 0.10f)
        {
            return "Suppressed";
        }

        if (saccadeReadiness > Math.Max(0.20f, nigrotectal * 0.80f) && headEye > 0.05f)
        {
            return "Orienting";
        }

        if (sensoryDrive > 0.08f)
        {
            return "Primed";
        }

        return "Holding";
    }

    private static HippocampalSpatialDiagnostics? AverageHippocampalSpatialDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.HippocampalSpatialDiagnostics)
            .Where(m => m != null)
            .Cast<HippocampalSpatialDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var entorhinal = (float)diagnostics.Average(d => d.EntorhinalGridDrive);
        var dentate = (float)diagnostics.Average(d => d.DentatePatternSeparation);
        var ca3 = (float)diagnostics.Average(d => d.Ca3PatternCompletion);
        var ca1 = (float)diagnostics.Average(d => d.Ca1PlaceIndex);
        var subicular = (float)diagnostics.Average(d => d.SubicularOutput);
        var headDirection = (float)diagnostics.Average(d => d.HeadDirectionAlignment);
        var coherence = (float)diagnostics.Average(d => d.SpatialCoherence);
        var novelty = (float)diagnostics.Average(d => d.NoveltyMismatch);

        return new HippocampalSpatialDiagnostics(
            SelectHippocampalSpatialMode(novelty, coherence, ca3, ca1, subicular),
            entorhinal,
            dentate,
            ca3,
            ca1,
            subicular,
            headDirection,
            coherence,
            novelty);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichHippocampalSpatialMemoryDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var entorhinal = GetRate(byId, StructureId.EntorhinalCortex);
        var dentate = GetRate(byId, StructureId.DentateGyrus);
        var ca3 = GetRate(byId, StructureId.CA3) + (GetRate(byId, StructureId.CA2) * 0.30f);
        var ca1 = GetRate(byId, StructureId.CA1) + (GetRate(byId, StructureId.CA2) * 0.20f);
        var subicular = GetRate(byId, StructureId.Subiculum);
        var headDirection =
            GetRate(byId, StructureId.Presubiculum) +
            (GetRate(byId, StructureId.Parasubiculum) * 0.85f) +
            (GetRate(byId, StructureId.RetrosplenialCortex) * 0.35f) +
            (GetRate(byId, StructureId.VestibularNuclei) * 0.25f);
        var parietalSpatial = GetRate(byId, StructureId.Ppc);
        var acetylcholine = byId.TryGetValue(StructureId.EntorhinalCortex, out var ec)
            ? ec.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var norepinephrine = byId.TryGetValue(StructureId.CA1, out var ca1Snapshot)
            ? ca1Snapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var noveltyGain = 0.80f +
            (Math.Clamp(acetylcholine, 0f, 1f) * 0.30f) +
            (Math.Clamp(norepinephrine, 0f, 1f) * 0.20f);
        var gridDrive = entorhinal + (GetRate(byId, StructureId.Parasubiculum) * 0.30f) + (parietalSpatial * 0.15f);
        var novelty = Math.Max(0f, ((gridDrive + dentate + (parietalSpatial * 0.20f)) * 0.45f * noveltyGain) - ((ca3 + ca1 + subicular) * 0.23f));
        var coherence = Math.Clamp((ca1 * 0.30f) + (subicular * 0.25f) + (headDirection * 0.25f) + (ca3 * 0.15f) - (novelty * 0.20f), 0f, 120f);
        var composite = new HippocampalSpatialDiagnostics(
            SelectHippocampalSpatialMode(novelty, coherence, ca3, ca1, subicular),
            gridDrive,
            dentate,
            ca3,
            ca1,
            subicular,
            headDirection,
            coherence,
            novelty);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesHippocampalSpatialComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { HippocampalSpatialDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesHippocampalSpatialComposite(StructureId structureId)
        => structureId is StructureId.EntorhinalCortex
            or StructureId.DentateGyrus
            or StructureId.CA3
            or StructureId.CA2
            or StructureId.CA1
            or StructureId.Subiculum
            or StructureId.Presubiculum
            or StructureId.Parasubiculum
            or StructureId.RetrosplenialCortex
            or StructureId.ParahippocampalCortex
            or StructureId.Ppc
            or StructureId.VestibularNuclei;

    private static string SelectHippocampalSpatialMode(float novelty, float coherence, float ca3, float ca1, float subicular)
    {
        if (novelty > Math.Max(0.20f, coherence * 0.55f))
        {
            return "Encoding";
        }

        if (ca3 > 0.05f && ca1 > 0.05f && ca3 > novelty * 0.85f)
        {
            return "Recalling";
        }

        if (coherence > Math.Max(0.15f, subicular * 0.35f))
        {
            return "Aligned";
        }

        return "Searching";
    }

    private static SalienceAffectDiagnostics? AverageSalienceAffectDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.SalienceAffectDiagnostics)
            .Where(m => m != null)
            .Cast<SalienceAffectDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var threat = (float)diagnostics.Average(d => d.ThreatSalience);
        var interoception = (float)diagnostics.Average(d => d.InteroceptiveDrive);
        var conflict = (float)diagnostics.Average(d => d.ConflictMonitoring);
        var arousal = (float)diagnostics.Average(d => d.AutonomicArousal);
        var attention = (float)diagnostics.Average(d => d.AttentionGain);
        var defensive = (float)diagnostics.Average(d => d.DefensiveReadiness);
        var control = (float)diagnostics.Average(d => d.ControlBias);
        var affect = (float)diagnostics.Average(d => d.AffectIntensity);

        return new SalienceAffectDiagnostics(
            SelectSalienceAffectMode(threat, interoception, conflict, defensive, control),
            threat,
            interoception,
            conflict,
            arousal,
            attention,
            defensive,
            control,
            affect);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichSalienceAffectDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var amygdala = GetRate(byId, StructureId.Amygdala);
        var insula = GetRate(byId, StructureId.Insula);
        var acc = GetRate(byId, StructureId.Acc);
        var hypothalamus = GetRate(byId, StructureId.Hypothalamus);
        var lc = GetRate(byId, StructureId.LocusCoeruleus);
        var basalForebrain = GetRate(byId, StructureId.BasalForebrain);
        var nacc = GetRate(byId, StructureId.NucleusAccumbens);
        var pfc = GetRate(byId, StructureId.Pfc);
        var pag = GetRate(byId, StructureId.PeriaqueductalGray);
        var norepinephrine = byId.TryGetValue(StructureId.LocusCoeruleus, out var lcSnapshot)
            ? lcSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var acetylcholine = byId.TryGetValue(StructureId.BasalForebrain, out var bfSnapshot)
            ? bfSnapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var neGain = 0.80f + (Math.Clamp(norepinephrine, 0f, 1f) * 0.45f);
        var achGain = 0.85f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.35f);

        var threat = (amygdala * neGain) + (lc * 0.20f) + (nacc * 0.12f);
        var interoception = insula + (hypothalamus * 0.25f);
        var conflict = acc + (insula * 0.25f);
        var arousal = (lc * neGain) + (hypothalamus * 0.70f);
        var attention = basalForebrain * achGain;
        var defensive = pag + (amygdala * 0.55f) + (hypothalamus * 0.18f);
        var controlBias = Math.Max(0f, pfc + (acc * 0.45f) + (attention * 0.30f) - ((threat + defensive) * 0.25f));
        var affect = Math.Max(threat, interoception) + (arousal * 0.35f) + (conflict * 0.25f);
        var composite = new SalienceAffectDiagnostics(
            SelectSalienceAffectMode(threat, interoception, conflict, defensive, controlBias),
            threat,
            interoception,
            conflict,
            arousal,
            attention,
            defensive,
            controlBias,
            affect);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesSalienceAffectComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { SalienceAffectDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesSalienceAffectComposite(StructureId structureId)
        => structureId is StructureId.Amygdala
            or StructureId.Insula
            or StructureId.Acc
            or StructureId.Hypothalamus
            or StructureId.LocusCoeruleus
            or StructureId.BasalForebrain
            or StructureId.NucleusAccumbens
            or StructureId.Pfc
            or StructureId.PeriaqueductalGray;

    private static string SelectSalienceAffectMode(float threat, float interoception, float conflict, float defensive, float controlBias)
    {
        if (defensive > Math.Max(0.20f, controlBias * 1.20f))
        {
            return "Defensive";
        }

        if (threat > Math.Max(interoception, conflict) * 1.15f && threat > 0.08f)
        {
            return "Threat";
        }

        if (interoception > Math.Max(threat, conflict) * 1.10f && interoception > 0.08f)
        {
            return "Interoceptive";
        }

        if (conflict > 0.08f)
        {
            return "Conflict";
        }

        return "Monitoring";
    }

    private static PrefrontalWorkingMemoryDiagnostics? AveragePrefrontalWorkingMemoryDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.PrefrontalWorkingMemoryDiagnostics)
            .Where(m => m != null)
            .Cast<PrefrontalWorkingMemoryDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var pfc = (float)diagnostics.Average(d => d.PfcPersistentActivity);
        var md = (float)diagnostics.Average(d => d.MediodorsalThalamicSupport);
        var frontoparietal = (float)diagnostics.Average(d => d.FrontoparietalContext);
        var semantic = (float)diagnostics.Average(d => d.SemanticContext);
        var striatal = (float)diagnostics.Average(d => d.StriatalGate);
        var acc = (float)diagnostics.Average(d => d.AccControlDemand);
        var topDown = (float)diagnostics.Average(d => d.TopDownBias);
        var stability = (float)diagnostics.Average(d => d.TaskSetStability);

        return new PrefrontalWorkingMemoryDiagnostics(
            SelectPrefrontalWorkingMemoryMode(stability, striatal, acc, topDown),
            pfc,
            md,
            frontoparietal,
            semantic,
            striatal,
            acc,
            topDown,
            stability);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichPrefrontalWorkingMemoryDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var pfcBase = GetRate(byId, StructureId.Pfc);
        var md = GetRate(byId, StructureId.MediodorsalThalamus);
        var ppc = GetRate(byId, StructureId.Ppc);
        var temporal = GetRate(byId, StructureId.TemporalAssociation);
        var striatum = GetRate(byId, StructureId.Striatum);
        var acc = GetRate(byId, StructureId.Acc);
        var ofc = GetRate(byId, StructureId.OrbitofrontalCortex);
        var basalForebrain = GetRate(byId, StructureId.BasalForebrain);
        var lc = GetRate(byId, StructureId.LocusCoeruleus);
        var dopamine = byId.TryGetValue(StructureId.Striatum, out var striatumSnapshot)
            ? striatumSnapshot.NeuromodLocal.DopamineLevel
            : 0f;
        var acetylcholine = byId.TryGetValue(StructureId.BasalForebrain, out var bfSnapshot)
            ? bfSnapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var norepinephrine = byId.TryGetValue(StructureId.LocusCoeruleus, out var lcSnapshot)
            ? lcSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var dopamineGate = 0.80f + (Math.Clamp(dopamine, 0f, 1f) * 0.35f);
        var attentionGain = 0.85f +
            (Math.Clamp(acetylcholine, 0f, 1f) * 0.25f) +
            (Math.Clamp(norepinephrine, 0f, 1f) * 0.20f);

        var pfc = (pfcBase * attentionGain) + (ofc * 0.25f) + (basalForebrain * 0.20f);
        var semantic = temporal + (ofc * 0.25f);
        var frontoparietal = ppc;
        var striatalGate = striatum * dopamineGate;
        var accDemand = acc + (lc * 0.25f);
        var topDown = Math.Max(0f, pfc + (md * 0.35f) + (frontoparietal * 0.25f) + (semantic * 0.20f) - (accDemand * 0.15f));
        var stability = Math.Clamp((pfc * 0.35f) + (md * 0.25f) + (striatalGate * 0.20f) + (frontoparietal * 0.15f) - (accDemand * 0.10f), 0f, 120f);
        var composite = new PrefrontalWorkingMemoryDiagnostics(
            SelectPrefrontalWorkingMemoryMode(stability, striatalGate, accDemand, topDown),
            pfc,
            md,
            frontoparietal,
            semantic,
            striatalGate,
            accDemand,
            topDown,
            stability);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesPrefrontalWorkingMemoryComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { PrefrontalWorkingMemoryDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesPrefrontalWorkingMemoryComposite(StructureId structureId)
        => structureId is StructureId.Pfc
            or StructureId.MediodorsalThalamus
            or StructureId.Ppc
            or StructureId.TemporalAssociation
            or StructureId.Striatum
            or StructureId.Acc
            or StructureId.OrbitofrontalCortex
            or StructureId.BasalForebrain
            or StructureId.LocusCoeruleus;

    private static string SelectPrefrontalWorkingMemoryMode(float stability, float striatalGate, float accDemand, float topDown)
    {
        if (accDemand > Math.Max(stability, topDown) * 0.90f && accDemand > 0.10f)
        {
            return "Updating";
        }

        if (stability > Math.Max(0.20f, accDemand * 1.15f) && striatalGate > 0.05f)
        {
            return "Maintaining";
        }

        if (topDown > 0.08f)
        {
            return "Biasing";
        }

        return "Idle";
    }

    private static ThalamicAttentionGateDiagnostics? AverageThalamicAttentionGateDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.ThalamicAttentionGateDiagnostics)
            .Where(m => m != null)
            .Cast<ThalamicAttentionGateDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var relay = (float)diagnostics.Average(d => d.ThalamocorticalRelay);
        var trn = (float)diagnostics.Average(d => d.TrnInhibitoryGate);
        var pulvinar = (float)diagnostics.Average(d => d.PulvinarSpotlight);
        var md = (float)diagnostics.Average(d => d.MediodorsalAccess);
        var intralaminar = (float)diagnostics.Average(d => d.IntralaminarBroadcast);
        var sensoryGain = (float)diagnostics.Average(d => d.SensoryGain);
        var corticalAccess = (float)diagnostics.Average(d => d.CorticalAccess);
        var selectionBias = (float)diagnostics.Average(d => d.RelaySelectionBias);

        return new ThalamicAttentionGateDiagnostics(
            SelectThalamicAttentionGateMode(relay, trn, pulvinar, intralaminar, corticalAccess),
            relay,
            trn,
            pulvinar,
            md,
            intralaminar,
            sensoryGain,
            corticalAccess,
            selectionBias);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichThalamicAttentionGateDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var thalamus = GetRate(byId, StructureId.Thalamus);
        var motorThalamus = GetRate(byId, StructureId.MotorThalamus);
        var trn = GetRate(byId, StructureId.Trn);
        var pulvinarBase = GetRate(byId, StructureId.Pulvinar);
        var mdBase = GetRate(byId, StructureId.MediodorsalThalamus);
        var intralaminarBase = GetRate(byId, StructureId.IntralaminarThalamus);
        var pfc = GetRate(byId, StructureId.Pfc);
        var ppc = GetRate(byId, StructureId.Ppc);
        var sensoryCortex = (GetRate(byId, StructureId.V1) + GetRate(byId, StructureId.A1) + GetRate(byId, StructureId.S1)) / 3f;
        var basalForebrain = GetRate(byId, StructureId.BasalForebrain);
        var lc = GetRate(byId, StructureId.LocusCoeruleus);
        var acetylcholine = byId.TryGetValue(StructureId.BasalForebrain, out var bfSnapshot)
            ? bfSnapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var norepinephrine = byId.TryGetValue(StructureId.LocusCoeruleus, out var lcSnapshot)
            ? lcSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var achGain = 0.85f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.35f);
        var neGain = 0.90f + (Math.Clamp(norepinephrine, 0f, 1f) * 0.25f);

        var relay = (thalamus * achGain) + (motorThalamus * 0.60f) + (pulvinarBase * 0.25f) + (mdBase * 0.25f) + (intralaminarBase * 0.35f) + (basalForebrain * 0.20f);
        var trnGate = trn * neGain;
        var pulvinar = (pulvinarBase * achGain) + (ppc * 0.15f);
        var mediodorsal = mdBase + (pfc * 0.12f);
        var intralaminar = (intralaminarBase * neGain) + (lc * 0.20f);
        var corticalContext = (pfc * 0.30f) + (ppc * 0.25f) + (sensoryCortex * 0.20f);
        var sensoryGain = Math.Max(0f, (relay * 0.55f) + (pulvinar * 0.30f) + (corticalContext * 0.10f) - (trnGate * 0.25f));
        var corticalAccess = Math.Max(0f, (relay * 0.35f) + (mediodorsal * 0.25f) + (intralaminar * 0.25f) + (pulvinar * 0.20f) + (corticalContext * 0.20f) - (trnGate * 0.20f));
        var selectionBias = Math.Clamp(Math.Max(sensoryGain, corticalAccess) + (pulvinar * 0.18f) + (mediodorsal * 0.12f) - (trnGate * 0.10f), 0f, 120f);
        var composite = new ThalamicAttentionGateDiagnostics(
            SelectThalamicAttentionGateMode(relay, trnGate, pulvinar, intralaminar, corticalAccess),
            relay,
            trnGate,
            pulvinar,
            mediodorsal,
            intralaminar,
            sensoryGain,
            corticalAccess,
            selectionBias);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesThalamicAttentionGateComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { ThalamicAttentionGateDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesThalamicAttentionGateComposite(StructureId structureId)
        => structureId is StructureId.Thalamus
            or StructureId.Trn
            or StructureId.Pulvinar
            or StructureId.MediodorsalThalamus
            or StructureId.IntralaminarThalamus
            or StructureId.MotorThalamus
            or StructureId.Pfc
            or StructureId.Ppc
            or StructureId.V1
            or StructureId.A1
            or StructureId.S1
            or StructureId.BasalForebrain
            or StructureId.LocusCoeruleus;

    private static string SelectThalamicAttentionGateMode(float relay, float trnGate, float pulvinar, float intralaminar, float corticalAccess)
    {
        if (trnGate > Math.Max(relay + pulvinar, corticalAccess) * 0.95f && trnGate > 0.10f)
        {
            return "Suppressed";
        }

        if (pulvinar > Math.Max(0.10f, relay * 0.35f))
        {
            return "Selecting";
        }

        if (intralaminar > Math.Max(0.10f, trnGate * 0.80f))
        {
            return "Broadcasting";
        }

        if (relay > 0.08f || corticalAccess > 0.08f)
        {
            return "Relaying";
        }

        return "Idle";
    }

    private static HypothalamicHomeostasisDiagnostics? AverageHypothalamicHomeostasisDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.HypothalamicHomeostasisDiagnostics)
            .Where(m => m != null)
            .Cast<HypothalamicHomeostasisDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var visceral = (float)diagnostics.Average(d => d.VisceralAfferentDrive);
        var setpoint = (float)diagnostics.Average(d => d.HypothalamicSetpointError);
        var insula = (float)diagnostics.Average(d => d.InsulaBodyFeeling);
        var limbic = (float)diagnostics.Average(d => d.LimbicHomeostaticPressure);
        var autonomic = (float)diagnostics.Average(d => d.AutonomicBrainstemDrive);
        var arousal = (float)diagnostics.Average(d => d.ArousalPressure);
        var comfort = (float)diagnostics.Average(d => d.ComfortDeficit);
        var defensive = (float)diagnostics.Average(d => d.DefensiveBodyCommand);

        return new HypothalamicHomeostasisDiagnostics(
            SelectHypothalamicHomeostasisMode(setpoint, autonomic, arousal, defensive),
            visceral,
            setpoint,
            insula,
            limbic,
            autonomic,
            arousal,
            comfort,
            defensive);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichHypothalamicHomeostasisDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var nts = GetRate(byId, StructureId.NucleusTractusSolitarius);
        var hypothalamus = GetRate(byId, StructureId.Hypothalamus);
        var insula = GetRate(byId, StructureId.Insula);
        var amygdala = GetRate(byId, StructureId.Amygdala);
        var lc = GetRate(byId, StructureId.LocusCoeruleus);
        var raphe = GetRate(byId, StructureId.RapheNuclei);
        var basalForebrain = GetRate(byId, StructureId.BasalForebrain);
        var pons = GetRate(byId, StructureId.Pons);
        var medulla = GetRate(byId, StructureId.Medulla);
        var reticular = GetRate(byId, StructureId.ReticularFormation);
        var pag = GetRate(byId, StructureId.PeriaqueductalGray);
        var norepinephrine = byId.TryGetValue(StructureId.LocusCoeruleus, out var lcSnapshot)
            ? lcSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var acetylcholine = byId.TryGetValue(StructureId.BasalForebrain, out var bfSnapshot)
            ? bfSnapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var serotonin = byId.TryGetValue(StructureId.RapheNuclei, out var rapheSnapshot)
            ? rapheSnapshot.NeuromodLocal.SerotoninLevel
            : 0f;
        var neGain = 0.85f + (Math.Clamp(norepinephrine, 0f, 1f) * 0.35f);
        var achGain = 0.85f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.25f);
        var serotoninBuffer = 1.05f - (Math.Clamp(serotonin, 0f, 1f) * 0.20f);

        var visceral = nts;
        var limbic = amygdala * neGain;
        var setpoint = Math.Max(0f, (hypothalamus * serotoninBuffer) + (visceral * 0.35f) + (insula * 0.25f) + (limbic * 0.20f));
        var brainstemDrive = Math.Max((pons + medulla) * 0.50f, (reticular * 0.45f) + (hypothalamus * 0.35f) + (visceral * 0.25f));
        var arousal = Math.Max((lc * neGain) + (basalForebrain * achGain * 0.35f), (setpoint * 0.25f) + (limbic * 0.25f) + (reticular * 0.35f));
        var comfort = Math.Max((raphe * 0.18f * serotoninBuffer) + (pag * 0.25f), setpoint * 0.25f);
        var defensive = Math.Max(pag + (amygdala * 0.35f), (limbic * 0.35f) + (hypothalamus * 0.20f));
        var composite = new HypothalamicHomeostasisDiagnostics(
            SelectHypothalamicHomeostasisMode(setpoint, brainstemDrive, arousal, defensive),
            visceral,
            setpoint,
            insula,
            limbic,
            brainstemDrive,
            arousal,
            comfort,
            defensive);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesHypothalamicHomeostasisComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { HypothalamicHomeostasisDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesHypothalamicHomeostasisComposite(StructureId structureId)
        => structureId is StructureId.NucleusTractusSolitarius
            or StructureId.Hypothalamus
            or StructureId.Insula
            or StructureId.Amygdala
            or StructureId.LocusCoeruleus
            or StructureId.RapheNuclei
            or StructureId.BasalForebrain
            or StructureId.Pons
            or StructureId.Medulla
            or StructureId.ReticularFormation
            or StructureId.PeriaqueductalGray;

    private static string SelectHypothalamicHomeostasisMode(float error, float autonomic, float arousal, float defensive)
    {
        if (defensive > Math.Max(error, autonomic) * 0.90f && defensive > 0.10f)
        {
            return "Defensive";
        }

        if (autonomic > Math.Max(0.12f, arousal * 1.05f))
        {
            return "Regulating";
        }

        if (arousal > Math.Max(0.10f, error * 0.80f))
        {
            return "Arousing";
        }

        if (error > 0.08f)
        {
            return "Seeking";
        }

        return "Balanced";
    }

    private static SleepWakeArousalDiagnostics? AverageSleepWakeArousalDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.SleepWakeArousalDiagnostics)
            .Where(m => m != null)
            .Cast<SleepWakeArousalDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var sleepPressure = (float)diagnostics.Average(d => d.HypothalamicSleepPressure);
        var reticular = (float)diagnostics.Average(d => d.ReticularActivatingDrive);
        var pontomedullary = (float)diagnostics.Average(d => d.PontomedullaryStateTone);
        var lc = (float)diagnostics.Average(d => d.LocusCoeruleusWakeTone);
        var raphe = (float)diagnostics.Average(d => d.RapheStabilizationTone);
        var basalForebrain = (float)diagnostics.Average(d => d.BasalForebrainWakeDrive);
        var intralaminar = (float)diagnostics.Average(d => d.IntralaminarArousalBroadcast);
        var readiness = (float)diagnostics.Average(d => d.CorticalReadiness);

        return new SleepWakeArousalDiagnostics(
            SelectSleepWakeArousalMode(sleepPressure, reticular, lc, basalForebrain, intralaminar, readiness),
            sleepPressure,
            reticular,
            pontomedullary,
            lc,
            raphe,
            basalForebrain,
            intralaminar,
            readiness);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichSleepWakeArousalDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var hypothalamus = GetRate(byId, StructureId.Hypothalamus);
        var reticular = GetRate(byId, StructureId.ReticularFormation);
        var pons = GetRate(byId, StructureId.Pons);
        var medulla = GetRate(byId, StructureId.Medulla);
        var lc = GetRate(byId, StructureId.LocusCoeruleus);
        var raphe = GetRate(byId, StructureId.RapheNuclei);
        var basalForebrain = GetRate(byId, StructureId.BasalForebrain);
        var intralaminar = GetRate(byId, StructureId.IntralaminarThalamus);
        var norepinephrine = byId.TryGetValue(StructureId.LocusCoeruleus, out var lcSnapshot)
            ? lcSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var acetylcholine = byId.TryGetValue(StructureId.BasalForebrain, out var bfSnapshot)
            ? bfSnapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var serotonin = byId.TryGetValue(StructureId.RapheNuclei, out var rapheSnapshot)
            ? rapheSnapshot.NeuromodLocal.SerotoninLevel
            : 0f;
        var neGain = 0.85f + (Math.Clamp(norepinephrine, 0f, 1f) * 0.40f);
        var achGain = 0.85f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.35f);
        var serotoninGain = 0.85f + (Math.Clamp(serotonin, 0f, 1f) * 0.30f);

        var sleepPressure = hypothalamus * (1.05f - (Math.Clamp(norepinephrine, 0f, 1f) * 0.20f));
        var reticularDrive = reticular * neGain;
        var pontomedullary = (pons + medulla) * 0.50f;
        var lcWake = lc * neGain;
        var rapheTone = raphe * serotoninGain;
        var basalWake = basalForebrain * achGain;
        var intralaminarBroadcast = intralaminar * neGain;
        var readiness = Math.Max(0f,
            (reticularDrive * 0.24f) +
            (lcWake * 0.24f) +
            (basalWake * 0.22f) +
            (intralaminarBroadcast * 0.20f) +
            (pontomedullary * 0.12f) +
            (rapheTone * 0.08f) -
            (sleepPressure * 0.18f));
        var composite = new SleepWakeArousalDiagnostics(
            SelectSleepWakeArousalMode(sleepPressure, reticularDrive, lcWake, basalWake, intralaminarBroadcast, readiness),
            sleepPressure,
            reticularDrive,
            pontomedullary,
            lcWake,
            rapheTone,
            basalWake,
            intralaminarBroadcast,
            readiness);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesSleepWakeArousalComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { SleepWakeArousalDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesSleepWakeArousalComposite(StructureId structureId)
        => structureId is StructureId.Hypothalamus
            or StructureId.ReticularFormation
            or StructureId.Pons
            or StructureId.Medulla
            or StructureId.LocusCoeruleus
            or StructureId.RapheNuclei
            or StructureId.BasalForebrain
            or StructureId.IntralaminarThalamus;

    private static string SelectSleepWakeArousalMode(float sleepPressure, float reticularDrive, float lcWake, float basalWake, float intralaminar, float corticalReadiness)
    {
        var wakeDrive = reticularDrive + lcWake + basalWake + intralaminar;
        if (sleepPressure > Math.Max(0.16f, wakeDrive * 0.95f) && corticalReadiness < sleepPressure * 0.55f)
        {
            return "SleepPressure";
        }

        if (corticalReadiness > Math.Max(0.12f, sleepPressure * 1.20f))
        {
            return "Awake";
        }

        if (Math.Abs(corticalReadiness - sleepPressure) <= Math.Max(0.08f, Math.Max(corticalReadiness, sleepPressure) * 0.20f))
        {
            return "Transition";
        }

        if (wakeDrive > 0.10f)
        {
            return "Drowsy";
        }

        return "Quiescent";
    }

    private static DescendingDefenseDiagnostics? AverageDescendingDefenseDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.DescendingDefenseDiagnostics)
            .Where(m => m != null)
            .Cast<DescendingDefenseDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var amygdala = (float)diagnostics.Average(d => d.AmygdalaThreatDrive);
        var hypothalamus = (float)diagnostics.Average(d => d.HypothalamicDefenseDrive);
        var pag = (float)diagnostics.Average(d => d.PagDefensiveCommand);
        var raphe = (float)diagnostics.Average(d => d.RaphePainModulation);
        var medulla = (float)diagnostics.Average(d => d.MedullaryAutonomicSupport);
        var reticular = (float)diagnostics.Average(d => d.ReticularPatternRelease);
        var spinal = (float)diagnostics.Average(d => d.SpinalWithdrawalDrive);
        var protection = (float)diagnostics.Average(d => d.ProtectionReadiness);

        return new DescendingDefenseDiagnostics(
            SelectDescendingDefenseMode(pag, reticular, spinal, raphe, protection),
            amygdala,
            hypothalamus,
            pag,
            raphe,
            medulla,
            reticular,
            spinal,
            protection);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichDescendingDefenseDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var amygdalaBase = GetRate(byId, StructureId.Amygdala);
        var hypothalamus = GetRate(byId, StructureId.Hypothalamus);
        var pagBase = GetRate(byId, StructureId.PeriaqueductalGray);
        var rapheBase = GetRate(byId, StructureId.RapheNuclei);
        var medulla = GetRate(byId, StructureId.Medulla);
        var reticular = GetRate(byId, StructureId.ReticularFormation);
        var spinal = GetRate(byId, StructureId.SpinalCordMotor);
        var norepinephrine = byId.TryGetValue(StructureId.LocusCoeruleus, out var lcSnapshot)
            ? lcSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var serotonin = byId.TryGetValue(StructureId.RapheNuclei, out var rapheSnapshot)
            ? rapheSnapshot.NeuromodLocal.SerotoninLevel
            : 0f;
        var neGain = 0.85f + (Math.Clamp(norepinephrine, 0f, 1f) * 0.40f);
        var serotoninModulation = 0.85f + (Math.Clamp(serotonin, 0f, 1f) * 0.30f);

        var amygdala = amygdalaBase * neGain;
        var pag = pagBase * neGain;
        var raphe = rapheBase * serotoninModulation;
        var protection = Math.Max(0f,
            (amygdala * 0.25f) +
            (hypothalamus * 0.18f) +
            (pag * 0.30f) +
            (reticular * 0.18f) +
            (spinal * 0.22f) +
            (medulla * 0.10f) -
            (raphe * 0.06f));
        var composite = new DescendingDefenseDiagnostics(
            SelectDescendingDefenseMode(pag, reticular, spinal, raphe, protection),
            amygdala,
            hypothalamus,
            pag,
            raphe,
            medulla,
            reticular,
            spinal,
            protection);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesDescendingDefenseComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { DescendingDefenseDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesDescendingDefenseComposite(StructureId structureId)
        => structureId is StructureId.Amygdala
            or StructureId.Hypothalamus
            or StructureId.PeriaqueductalGray
            or StructureId.RapheNuclei
            or StructureId.Medulla
            or StructureId.ReticularFormation
            or StructureId.SpinalCordMotor;

    private static string SelectDescendingDefenseMode(float pag, float reticular, float spinal, float raphe, float protection)
    {
        if (spinal > Math.Max(0.10f, protection * 0.45f))
        {
            return "Withdrawal";
        }

        if (pag > Math.Max(0.12f, raphe * 1.20f))
        {
            return "Defensive";
        }

        if (reticular > Math.Max(0.10f, spinal * 0.70f))
        {
            return "Patterning";
        }

        if (raphe > Math.Max(0.10f, pag * 0.70f))
        {
            return "Modulating";
        }

        if (protection > 0.08f)
        {
            return "Guarding";
        }

        return "Quiet";
    }

    private static DopamineRewardDiagnostics? AverageDopamineRewardDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.DopamineRewardDiagnostics)
            .Where(m => m != null)
            .Cast<DopamineRewardDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var vta = (float)diagnostics.Average(d => d.VtaPhasicDopamine);
        var snc = (float)diagnostics.Average(d => d.SncActionTeaching);
        var accumbens = (float)diagnostics.Average(d => d.NucleusAccumbensIncentive);
        var striatum = (float)diagnostics.Average(d => d.StriatalActionValue);
        var habenula = (float)diagnostics.Average(d => d.HabenulaNegativePrediction);
        var ofc = (float)diagnostics.Average(d => d.OrbitofrontalExpectedValue);
        var pfc = (float)diagnostics.Average(d => d.PfcGoalBias);
        var rpe = (float)diagnostics.Average(d => d.RewardPredictionError);
        var readiness = (float)diagnostics.Average(d => d.LearningReadiness);

        return new DopamineRewardDiagnostics(
            SelectDopamineRewardMode(vta, snc, accumbens, striatum, habenula, ofc, pfc, rpe, readiness),
            vta,
            snc,
            accumbens,
            striatum,
            habenula,
            ofc,
            pfc,
            rpe,
            readiness);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichDopamineRewardDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var dopamine = byId.Values.Count == 0
            ? 0f
            : (float)byId.Values.Average(s => Math.Clamp(s.NeuromodLocal.DopamineLevel, 0f, 1f));
        var vtaBase = GetRate(byId, StructureId.Vta);
        var sncBase = GetRate(byId, StructureId.Snc);
        var accumbensBase = GetRate(byId, StructureId.NucleusAccumbens);
        var striatumBase = GetRate(byId, StructureId.Striatum);
        var habenulaBase = GetRate(byId, StructureId.Habenula);
        var ofcBase = GetRate(byId, StructureId.OrbitofrontalCortex);
        var pfcBase = GetRate(byId, StructureId.Pfc);
        var rpe = Math.Clamp(
            ((vtaBase + sncBase + accumbensBase) * 0.030f) +
            (ofcBase * 0.015f) +
            (pfcBase * 0.010f) -
            (habenulaBase * 0.045f),
            -1f,
            1f);
        var positiveRpe = Math.Max(0f, rpe);
        var negativeRpe = Math.Max(0f, -rpe);
        var vta = vtaBase * (0.90f + (dopamine * 0.35f) + (positiveRpe * 0.25f));
        var snc = sncBase * (0.90f + (dopamine * 0.30f) + (Math.Abs(rpe) * 0.15f));
        var accumbens = accumbensBase * (0.85f + (dopamine * 0.35f) + (positiveRpe * 0.20f));
        var striatum = striatumBase * (0.85f + (dopamine * 0.35f));
        var habenula = habenulaBase * (0.85f + (negativeRpe * 0.45f));
        var ofc = ofcBase * (0.85f + (dopamine * 0.15f) + (Math.Abs(rpe) * 0.20f));
        var pfc = pfcBase * (0.85f + (dopamine * 0.20f));
        var readiness = Math.Max(0f,
            (vta * 0.24f) +
            (snc * 0.22f) +
            (accumbens * 0.20f) +
            (striatum * 0.18f) +
            (ofc * 0.18f) +
            (pfc * 0.12f) +
            (positiveRpe * 0.25f) -
            (habenula * 0.16f));
        var composite = new DopamineRewardDiagnostics(
            SelectDopamineRewardMode(vta, snc, accumbens, striatum, habenula, ofc, pfc, rpe, readiness),
            vta,
            snc,
            accumbens,
            striatum,
            habenula,
            ofc,
            pfc,
            rpe,
            readiness);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesDopamineRewardComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { DopamineRewardDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesDopamineRewardComposite(StructureId structureId)
        => structureId is StructureId.Vta
            or StructureId.Snc
            or StructureId.NucleusAccumbens
            or StructureId.Striatum
            or StructureId.Habenula
            or StructureId.OrbitofrontalCortex
            or StructureId.Pfc;

    private static string SelectDopamineRewardMode(float vta, float snc, float accumbens, float striatum, float habenula, float ofc, float pfc, float rpe, float readiness)
    {
        if (habenula > Math.Max(0.08f, Math.Max(vta, accumbens) * 0.70f) && rpe < -0.05f)
        {
            return "NegativeTeaching";
        }

        if ((vta + accumbens) > Math.Max(0.12f, habenula * 1.25f) && rpe > 0.05f)
        {
            return "PhasicReward";
        }

        if ((snc + striatum) > Math.Max(0.12f, ofc + pfc))
        {
            return "ActionTeaching";
        }

        if (ofc > Math.Max(0.10f, pfc * 0.85f))
        {
            return "Valuation";
        }

        if (pfc > Math.Max(0.10f, ofc * 0.85f))
        {
            return "GoalBias";
        }

        if (readiness > 0.08f)
        {
            return "TonicLearning";
        }

        return "Quiet";
    }

    private static SeptohippocampalThetaDiagnostics? AverageSeptohippocampalThetaDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.SeptohippocampalThetaDiagnostics)
            .Where(m => m != null)
            .Cast<SeptohippocampalThetaDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var septal = (float)diagnostics.Average(d => d.SeptalThetaDrive);
        var entorhinal = (float)diagnostics.Average(d => d.EntorhinalGridPhase);
        var dentate = (float)diagnostics.Average(d => d.DentateEncodingGate);
        var ca3 = (float)diagnostics.Average(d => d.Ca3SequenceReplay);
        var ca1 = (float)diagnostics.Average(d => d.Ca1PlaceTiming);
        var subicular = (float)diagnostics.Average(d => d.SubicularNavigationOutput);
        var headDirection = (float)diagnostics.Average(d => d.HeadDirectionAlignment);
        var retrosplenial = (float)diagnostics.Average(d => d.RetrosplenialSceneAnchor);
        var vestibular = (float)diagnostics.Average(d => d.VestibularPathIntegration);
        var coherence = (float)diagnostics.Average(d => d.ThetaCoherence);

        return new SeptohippocampalThetaDiagnostics(
            SelectSeptohippocampalThetaMode(septal, entorhinal, ca3, ca1, headDirection, retrosplenial, vestibular, coherence),
            septal,
            entorhinal,
            dentate,
            ca3,
            ca1,
            subicular,
            headDirection,
            retrosplenial,
            vestibular,
            coherence);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichSeptohippocampalThetaDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var acetylcholine = byId.TryGetValue(StructureId.BasalForebrain, out var bfSnapshot)
            ? bfSnapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var achGain = 0.85f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.40f);
        var septal = GetRate(byId, StructureId.BasalForebrain) * achGain;
        var entorhinal = GetRate(byId, StructureId.EntorhinalCortex) * achGain;
        var dentate = GetRate(byId, StructureId.DentateGyrus) * achGain;
        var ca3 = GetRate(byId, StructureId.CA3) + (GetRate(byId, StructureId.CA2) * 0.35f);
        var ca1 = (GetRate(byId, StructureId.CA1) + (GetRate(byId, StructureId.CA2) * 0.25f)) * achGain;
        var subicular = GetRate(byId, StructureId.Subiculum);
        var headDirection =
            GetRate(byId, StructureId.Presubiculum) +
            (GetRate(byId, StructureId.Parasubiculum) * 0.85f) +
            (GetRate(byId, StructureId.Subiculum) * 0.20f) +
            (GetRate(byId, StructureId.RetrosplenialCortex) * 0.30f) +
            (GetRate(byId, StructureId.VestibularNuclei) * 0.35f);
        var retrosplenial = GetRate(byId, StructureId.RetrosplenialCortex);
        var vestibular = GetRate(byId, StructureId.VestibularNuclei);
        var coherence = Math.Max(0f,
            (septal * 0.22f) +
            (entorhinal * 0.18f) +
            (dentate * 0.12f) +
            (ca3 * 0.14f) +
            (ca1 * 0.18f) +
            (subicular * 0.16f) +
            (headDirection * 0.18f) +
            (retrosplenial * 0.14f) +
            (vestibular * 0.12f));
        var composite = new SeptohippocampalThetaDiagnostics(
            SelectSeptohippocampalThetaMode(septal, entorhinal, ca3, ca1, headDirection, retrosplenial, vestibular, coherence),
            septal,
            entorhinal,
            dentate,
            ca3,
            ca1,
            subicular,
            headDirection,
            retrosplenial,
            vestibular,
            coherence);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesSeptohippocampalThetaComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { SeptohippocampalThetaDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesSeptohippocampalThetaComposite(StructureId structureId)
        => structureId is StructureId.BasalForebrain
            or StructureId.EntorhinalCortex
            or StructureId.DentateGyrus
            or StructureId.CA3
            or StructureId.CA2
            or StructureId.CA1
            or StructureId.Subiculum
            or StructureId.Presubiculum
            or StructureId.Parasubiculum
            or StructureId.RetrosplenialCortex
            or StructureId.VestibularNuclei;

    private static string SelectSeptohippocampalThetaMode(float septal, float entorhinal, float ca3, float ca1, float headDirection, float retrosplenial, float vestibular, float coherence)
    {
        if (septal > Math.Max(0.10f, coherence * 0.35f) && entorhinal > 0.05f)
        {
            return "ThetaPacing";
        }

        if ((headDirection + vestibular + retrosplenial) > Math.Max(0.12f, ca1 + ca3))
        {
            return "PathIntegrating";
        }

        if (ca3 > Math.Max(0.10f, entorhinal * 0.80f))
        {
            return "Sequencing";
        }

        if (ca1 > Math.Max(0.10f, ca3 * 0.80f))
        {
            return "PlaceTiming";
        }

        if (coherence > 0.08f)
        {
            return "Synchronized";
        }

        return "Quiet";
    }

    private static SpinalProprioceptiveDiagnostics? AverageSpinalProprioceptiveDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.SpinalProprioceptiveDiagnostics)
            .Where(m => m != null)
            .Cast<SpinalProprioceptiveDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var spinal = (float)diagnostics.Average(d => d.SpinalReflexDrive);
        var s1 = (float)diagnostics.Average(d => d.S1ProprioceptiveMap);
        var m1 = (float)diagnostics.Average(d => d.M1DescendingCommand);
        var cerebellar = (float)diagnostics.Average(d => d.CerebellarMossyFeedback);
        var vestibular = (float)diagnostics.Average(d => d.VestibularBalanceInput);
        var reticular = (float)diagnostics.Average(d => d.ReticularPosturalSet);
        var thalamic = (float)diagnostics.Average(d => d.ThalamicRelayTone);
        var readiness = (float)diagnostics.Average(d => d.ReflexReadiness);
        var coherence = (float)diagnostics.Average(d => d.ProprioceptiveCoherence);

        return new SpinalProprioceptiveDiagnostics(
            SelectSpinalProprioceptiveMode(spinal, s1, m1, cerebellar, vestibular, reticular, thalamic, readiness, coherence),
            spinal,
            s1,
            m1,
            cerebellar,
            vestibular,
            reticular,
            thalamic,
            readiness,
            coherence);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichSpinalProprioceptiveDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var acetylcholine = byId.TryGetValue(StructureId.S1, out var s1Snapshot)
            ? s1Snapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var norepinephrine = byId.TryGetValue(StructureId.ReticularFormation, out var reticularSnapshot)
            ? reticularSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var achGain = 0.90f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.30f);
        var neGain = 0.85f + (Math.Clamp(norepinephrine, 0f, 1f) * 0.35f);
        var spinal = GetRate(byId, StructureId.SpinalCordMotor);
        var s1 = GetRate(byId, StructureId.S1) * achGain;
        var m1 = GetRate(byId, StructureId.M1);
        var cerebellar = GetRate(byId, StructureId.CerebellarGranule);
        var vestibular = GetRate(byId, StructureId.VestibularNuclei);
        var reticular = GetRate(byId, StructureId.ReticularFormation) * neGain;
        var thalamic = (GetRate(byId, StructureId.Thalamus) + (GetRate(byId, StructureId.MotorThalamus) * 0.80f)) * achGain;
        var readiness = Math.Max(0f,
            (spinal * 0.24f) +
            (s1 * 0.18f) +
            (m1 * 0.18f) +
            (cerebellar * 0.18f) +
            (vestibular * 0.14f) +
            (reticular * 0.16f) +
            (thalamic * 0.12f));
        var coherence = Math.Clamp(
            (s1 * 0.22f) +
            (cerebellar * 0.20f) +
            (vestibular * 0.16f) +
            (thalamic * 0.16f) +
            (spinal * 0.12f) +
            (m1 * 0.10f) +
            (reticular * 0.10f),
            0f,
            120f);
        var composite = new SpinalProprioceptiveDiagnostics(
            SelectSpinalProprioceptiveMode(spinal, s1, m1, cerebellar, vestibular, reticular, thalamic, readiness, coherence),
            spinal,
            s1,
            m1,
            cerebellar,
            vestibular,
            reticular,
            thalamic,
            readiness,
            coherence);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesSpinalProprioceptiveComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { SpinalProprioceptiveDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesSpinalProprioceptiveComposite(StructureId structureId)
        => structureId is StructureId.SpinalCordMotor
            or StructureId.S1
            or StructureId.M1
            or StructureId.CerebellarGranule
            or StructureId.VestibularNuclei
            or StructureId.ReticularFormation
            or StructureId.Thalamus
            or StructureId.MotorThalamus;

    private static string SelectSpinalProprioceptiveMode(float spinal, float s1, float m1, float cerebellar, float vestibular, float reticular, float thalamic, float readiness, float coherence)
    {
        if (spinal > Math.Max(0.10f, m1 * 0.75f) && readiness > 0.12f)
        {
            return "Reflexive";
        }

        if ((cerebellar + s1) > Math.Max(0.12f, vestibular + reticular))
        {
            return "Proprioceptive";
        }

        if ((vestibular + reticular) > Math.Max(0.12f, cerebellar))
        {
            return "Postural";
        }

        if (m1 > Math.Max(0.10f, spinal * 0.85f))
        {
            return "Descending";
        }

        if (thalamic > 0.08f)
        {
            return "Relaying";
        }

        if (coherence > 0.08f)
        {
            return "Integrated";
        }

        return "Quiet";
    }

    private static OlfactoryLimbicMemoryDiagnostics? AverageOlfactoryLimbicMemoryDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.OlfactoryLimbicMemoryDiagnostics)
            .Where(m => m != null)
            .Cast<OlfactoryLimbicMemoryDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var olfactory = (float)diagnostics.Average(d => d.OlfactoryCueDrive);
        var temporal = (float)diagnostics.Average(d => d.TemporalPiriformAssociation);
        var amygdala = (float)diagnostics.Average(d => d.AmygdalaAffectiveTag);
        var entorhinal = (float)diagnostics.Average(d => d.EntorhinalMemoryGate);
        var hippocampal = (float)diagnostics.Average(d => d.HippocampalEpisodeIndex);
        var ofc = (float)diagnostics.Average(d => d.OrbitofrontalValenceContext);
        var pfc = (float)diagnostics.Average(d => d.PfcAutobiographicalControl);
        var familiarity = (float)diagnostics.Average(d => d.FamiliaritySignal);
        var coherence = (float)diagnostics.Average(d => d.AutobiographicalCoherence);

        return new OlfactoryLimbicMemoryDiagnostics(
            SelectOlfactoryLimbicMemoryMode(olfactory, temporal, amygdala, entorhinal, hippocampal, ofc, pfc, familiarity, coherence),
            olfactory,
            temporal,
            amygdala,
            entorhinal,
            hippocampal,
            ofc,
            pfc,
            familiarity,
            coherence);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichOlfactoryLimbicMemoryDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var acetylcholine = byId.TryGetValue(StructureId.OlfactoryBulb, out var olfactorySnapshot)
            ? olfactorySnapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var norepinephrine = byId.TryGetValue(StructureId.Amygdala, out var amygdalaSnapshot)
            ? amygdalaSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var dopamine = byId.TryGetValue(StructureId.OrbitofrontalCortex, out var ofcSnapshot)
            ? ofcSnapshot.NeuromodLocal.DopamineLevel
            : 0f;
        var achGain = 0.85f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.35f);
        var neGain = 0.85f + (Math.Clamp(norepinephrine, 0f, 1f) * 0.35f);
        var dopamineGain = 0.85f + (Math.Clamp(dopamine, 0f, 1f) * 0.25f);
        var olfactory = GetRate(byId, StructureId.OlfactoryBulb) * achGain;
        var temporal = GetRate(byId, StructureId.TemporalAssociation) + (GetRate(byId, StructureId.PerirhinalCortex) * 0.35f);
        var familiarity = GetRate(byId, StructureId.PerirhinalCortex) + (GetRate(byId, StructureId.TemporalAssociation) * 0.20f);
        var amygdala = GetRate(byId, StructureId.Amygdala) * neGain;
        var entorhinal = (GetRate(byId, StructureId.EntorhinalCortex) * achGain) + (GetRate(byId, StructureId.ParahippocampalCortex) * 0.25f);
        var hippocampal =
            (GetRate(byId, StructureId.DentateGyrus) * 0.35f) +
            (GetRate(byId, StructureId.CA3) * 0.55f) +
            (GetRate(byId, StructureId.CA2) * 0.25f) +
            (GetRate(byId, StructureId.CA1) * 0.70f) +
            (GetRate(byId, StructureId.Subiculum) * 0.45f) +
            (GetRate(byId, StructureId.ParahippocampalCortex) * 0.45f);
        var ofc = GetRate(byId, StructureId.OrbitofrontalCortex) * dopamineGain;
        var pfc = GetRate(byId, StructureId.Pfc) + (GetRate(byId, StructureId.Subiculum) * 0.15f);
        var coherence = Math.Clamp(
            (olfactory * 0.18f) +
            (temporal * 0.15f) +
            (amygdala * 0.16f) +
            (entorhinal * 0.17f) +
            (hippocampal * 0.20f) +
            (ofc * 0.12f) +
            (pfc * 0.16f) +
            (familiarity * 0.10f),
            0f,
            120f);
        var composite = new OlfactoryLimbicMemoryDiagnostics(
            SelectOlfactoryLimbicMemoryMode(olfactory, temporal, amygdala, entorhinal, hippocampal, ofc, pfc, familiarity, coherence),
            olfactory,
            temporal,
            amygdala,
            entorhinal,
            hippocampal,
            ofc,
            pfc,
            familiarity,
            coherence);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesOlfactoryLimbicMemoryComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { OlfactoryLimbicMemoryDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesOlfactoryLimbicMemoryComposite(StructureId structureId)
        => structureId is StructureId.OlfactoryBulb
            or StructureId.TemporalAssociation
            or StructureId.PerirhinalCortex
            or StructureId.ParahippocampalCortex
            or StructureId.Amygdala
            or StructureId.EntorhinalCortex
            or StructureId.DentateGyrus
            or StructureId.CA3
            or StructureId.CA2
            or StructureId.CA1
            or StructureId.Subiculum
            or StructureId.OrbitofrontalCortex
            or StructureId.Pfc;

    private static string SelectOlfactoryLimbicMemoryMode(float olfactory, float temporal, float amygdala, float entorhinal, float hippocampal, float ofc, float pfc, float familiarity, float coherence)
    {
        if (olfactory > Math.Max(0.10f, temporal * 0.75f) && (amygdala + entorhinal) > 0.08f)
        {
            return "OdorCueing";
        }

        if (amygdala > Math.Max(0.10f, ofc * 0.80f))
        {
            return "AffectiveTagging";
        }

        if (entorhinal > Math.Max(0.10f, hippocampal * 0.70f))
        {
            return "Encoding";
        }

        if (hippocampal > Math.Max(0.10f, entorhinal * 0.80f))
        {
            return "Recalling";
        }

        if (ofc > Math.Max(0.10f, pfc * 0.70f))
        {
            return "Valuating";
        }

        if (pfc > Math.Max(0.10f, hippocampal * 0.70f))
        {
            return "NarrativeControl";
        }

        if (familiarity > 0.08f)
        {
            return "Familiarity";
        }

        if (coherence > 0.08f)
        {
            return "Integrated";
        }

        return "Quiet";
    }

    private static AuditoryLanguageMotorDiagnostics? AverageAuditoryLanguageMotorDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.AuditoryLanguageMotorDiagnostics)
            .Where(m => m != null)
            .Cast<AuditoryLanguageMotorDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var a1 = (float)diagnostics.Average(d => d.A1AuditoryDrive);
        var wernicke = (float)diagnostics.Average(d => d.WernickeComprehension);
        var arcuate = (float)diagnostics.Average(d => d.ArcuatePhonologicalRelay);
        var broca = (float)diagnostics.Average(d => d.BrocaSpeechSequence);
        var premotor = (float)diagnostics.Average(d => d.PremotorArticulationPlan);
        var m1 = (float)diagnostics.Average(d => d.M1SpeechMotorCommand);
        var basalGate = (float)diagnostics.Average(d => d.BasalGangliaSpeechGate);
        var motorThalamic = (float)diagnostics.Average(d => d.MotorThalamicRelay);
        var coherence = (float)diagnostics.Average(d => d.LanguageMotorCoherence);

        return new AuditoryLanguageMotorDiagnostics(
            SelectAuditoryLanguageMotorMode(a1, wernicke, arcuate, broca, premotor, m1, basalGate, motorThalamic, coherence),
            a1,
            wernicke,
            arcuate,
            broca,
            premotor,
            m1,
            basalGate,
            motorThalamic,
            coherence);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichAuditoryLanguageMotorDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var acetylcholine = byId.TryGetValue(StructureId.A1, out var a1Snapshot)
            ? a1Snapshot.NeuromodLocal.AcetylcholineLevel
            : 0f;
        var dopamine = byId.TryGetValue(StructureId.Striatum, out var striatalSnapshot)
            ? striatalSnapshot.NeuromodLocal.DopamineLevel
            : 0f;
        var achGain = 0.85f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.35f);
        var dopamineGain = 0.85f + (Math.Clamp(dopamine, 0f, 1f) * 0.30f);
        var a1 = GetRate(byId, StructureId.A1) * achGain;
        var wernicke = GetRate(byId, StructureId.WernickePstgPsts) * achGain;
        var arcuate = GetRate(byId, StructureId.ArcuateFasciculus);
        var broca = GetRate(byId, StructureId.BrocaBa44Ba45);
        var premotor = GetRate(byId, StructureId.PremotorCortex);
        var m1 = GetRate(byId, StructureId.M1);
        var basalGate =
            (GetRate(byId, StructureId.Striatum) * dopamineGain) +
            (GetRate(byId, StructureId.GPi) * 0.55f) +
            (GetRate(byId, StructureId.Snr) * 0.55f);
        var motorThalamic =
            (GetRate(byId, StructureId.MotorThalamus) * achGain) +
            (GetRate(byId, StructureId.Thalamus) * achGain * 0.35f);
        var coherence = Math.Clamp(
            (a1 * 0.14f) +
            (wernicke * 0.17f) +
            (arcuate * 0.16f) +
            (broca * 0.18f) +
            (premotor * 0.16f) +
            (m1 * 0.14f) +
            (basalGate * 0.12f) +
            (motorThalamic * 0.13f),
            0f,
            120f);
        var composite = new AuditoryLanguageMotorDiagnostics(
            SelectAuditoryLanguageMotorMode(a1, wernicke, arcuate, broca, premotor, m1, basalGate, motorThalamic, coherence),
            a1,
            wernicke,
            arcuate,
            broca,
            premotor,
            m1,
            basalGate,
            motorThalamic,
            coherence);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesAuditoryLanguageMotorComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { AuditoryLanguageMotorDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesAuditoryLanguageMotorComposite(StructureId structureId)
        => structureId is StructureId.A1
            or StructureId.WernickePstgPsts
            or StructureId.ArcuateFasciculus
            or StructureId.BrocaBa44Ba45
            or StructureId.PremotorCortex
            or StructureId.M1
            or StructureId.Striatum
            or StructureId.GPi
            or StructureId.Snr
            or StructureId.Thalamus
            or StructureId.MotorThalamus;

    private static string SelectAuditoryLanguageMotorMode(float a1, float wernicke, float arcuate, float broca, float premotor, float m1, float basalGate, float motorThalamic, float coherence)
    {
        if (a1 > Math.Max(0.10f, wernicke * 0.75f) && wernicke > 0.04f)
        {
            return "AuditoryParsing";
        }

        if (wernicke > Math.Max(0.10f, broca * 0.75f))
        {
            return "Comprehending";
        }

        if (arcuate > Math.Max(0.08f, broca * 0.55f))
        {
            return "PhonologicalRelay";
        }

        if (broca > Math.Max(0.10f, premotor * 0.75f))
        {
            return "SpeechSequencing";
        }

        if ((premotor + m1) > Math.Max(0.12f, broca * 0.85f))
        {
            return "Articulating";
        }

        if ((basalGate + motorThalamic) > Math.Max(0.12f, premotor * 0.65f))
        {
            return "ActionGated";
        }

        if (coherence > 0.08f)
        {
            return "Integrated";
        }

        return "Quiet";
    }

    private static VisualObjectRecognitionDiagnostics? AverageVisualObjectRecognitionDiagnostics(IReadOnlyList<InstanceStructureSnapshot> members)
    {
        var diagnostics = members
            .Select(m => m.VisualObjectRecognitionDiagnostics)
            .Where(m => m != null)
            .Cast<VisualObjectRecognitionDiagnostics>()
            .ToList();
        if (diagnostics.Count == 0)
        {
            return null;
        }

        var v1 = (float)diagnostics.Average(d => d.V1EdgeDrive);
        var v2 = (float)diagnostics.Average(d => d.V2ContourIntegration);
        var v4 = (float)diagnostics.Average(d => d.V4ObjectFeatureBinding);
        var mt = (float)diagnostics.Average(d => d.MtMotionCue);
        var temporal = (float)diagnostics.Average(d => d.TemporalObjectIdentity);
        var perirhinal = (float)diagnostics.Average(d => d.PerirhinalFamiliarity);
        var pulvinar = (float)diagnostics.Average(d => d.PulvinarVisualAttention);
        var thalamic = (float)diagnostics.Average(d => d.ThalamicRelayGain);
        var pfc = (float)diagnostics.Average(d => d.PfcObjectContext);
        var coherence = (float)diagnostics.Average(d => d.ObjectRecognitionCoherence);

        return new VisualObjectRecognitionDiagnostics(
            SelectVisualObjectRecognitionMode(v1, v2, v4, mt, temporal, perirhinal, pulvinar, thalamic, pfc, coherence),
            v1,
            v2,
            v4,
            mt,
            temporal,
            perirhinal,
            pulvinar,
            thalamic,
            pfc,
            coherence);
    }

    private static IReadOnlyList<StructureSnapshot> EnrichVisualObjectRecognitionDiagnostics(List<StructureSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return snapshots;
        }

        var byId = snapshots.ToDictionary(s => s.StructureId);
        var acetylcholine = byId.TryGetValue(StructureId.V1, out var v1Snapshot)
            ? v1Snapshot.NeuromodLocal.AcetylcholineLevel
            : byId.TryGetValue(StructureId.Thalamus, out var thalamusSnapshot)
                ? thalamusSnapshot.NeuromodLocal.AcetylcholineLevel
                : 0f;
        var norepinephrine = byId.TryGetValue(StructureId.Pulvinar, out var pulvinarSnapshot)
            ? pulvinarSnapshot.NeuromodLocal.NorepinephrineLevel
            : 0f;
        var achGain = 0.85f + (Math.Clamp(acetylcholine, 0f, 1f) * 0.35f);
        var neGain = 0.90f + (Math.Clamp(norepinephrine, 0f, 1f) * 0.25f);
        var v1 = GetRate(byId, StructureId.V1) * achGain;
        var v2 = GetRate(byId, StructureId.V2) + (v1 * 0.18f);
        var v4 = GetRate(byId, StructureId.V4) + (v2 * 0.20f);
        var mt = GetRate(byId, StructureId.Mt) + (v2 * 0.12f);
        var temporal = GetRate(byId, StructureId.TemporalAssociation) + (v4 * 0.24f);
        var perirhinal = GetRate(byId, StructureId.PerirhinalCortex) + (temporal * 0.18f);
        var pulvinar = GetRate(byId, StructureId.Pulvinar) * neGain;
        var thalamic = GetRate(byId, StructureId.Thalamus) * achGain;
        var pfc = GetRate(byId, StructureId.Pfc) + (temporal * 0.12f) + (perirhinal * 0.08f);
        var coherence = Math.Clamp(
            (v1 * 0.13f) +
            (v2 * 0.14f) +
            (v4 * 0.18f) +
            (mt * 0.10f) +
            (temporal * 0.18f) +
            (perirhinal * 0.16f) +
            (pulvinar * 0.14f) +
            (thalamic * 0.10f) +
            (pfc * 0.14f),
            0f,
            120f);
        var composite = new VisualObjectRecognitionDiagnostics(
            SelectVisualObjectRecognitionMode(v1, v2, v4, mt, temporal, perirhinal, pulvinar, thalamic, pfc, coherence),
            v1,
            v2,
            v4,
            mt,
            temporal,
            perirhinal,
            pulvinar,
            thalamic,
            pfc,
            coherence);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!CarriesVisualObjectRecognitionComposite(snapshot.StructureId))
            {
                continue;
            }

            snapshots[i] = snapshot with { VisualObjectRecognitionDiagnostics = composite };
        }

        return snapshots;
    }

    private static bool CarriesVisualObjectRecognitionComposite(StructureId structureId)
        => structureId is StructureId.V1
            or StructureId.V2
            or StructureId.V4
            or StructureId.Mt
            or StructureId.TemporalAssociation
            or StructureId.PerirhinalCortex
            or StructureId.Pulvinar
            or StructureId.Thalamus
            or StructureId.Pfc;

    private static string SelectVisualObjectRecognitionMode(float v1, float v2, float v4, float mt, float temporal, float perirhinal, float pulvinar, float thalamic, float pfc, float coherence)
    {
        if (v1 > Math.Max(0.10f, v2 * 0.75f))
        {
            return "EdgeEncoding";
        }

        if ((v2 + v4) > Math.Max(0.12f, temporal * 0.75f))
        {
            return "FeatureBinding";
        }

        if (mt > Math.Max(0.08f, v4 * 0.55f))
        {
            return "MotionCueing";
        }

        if (temporal > Math.Max(0.10f, perirhinal * 0.80f))
        {
            return "ObjectIdentity";
        }

        if (perirhinal > Math.Max(0.10f, temporal * 0.70f))
        {
            return "Familiarity";
        }

        if ((pulvinar + thalamic) > Math.Max(0.12f, v4 * 0.65f))
        {
            return "AttendedRelay";
        }

        if (pfc > Math.Max(0.10f, temporal * 0.65f))
        {
            return "ContextualControl";
        }

        if (coherence > 0.08f)
        {
            return "Integrated";
        }

        return "Quiet";
    }

    private async Task<PerceptionLanguageConditioningStats> InjectPerceptionLanguageConditioningAsync(
        TickSignal tickSignal,
        SimulationState state,
        IReadOnlyList<InstanceStructureSnapshot> processedSnapshots,
        NeuronalPerceptDecision neuronalPercept,
        SemaphoreSlim dispatchSemaphore,
        IReadOnlyDictionary<string, HttpClient> clients,
        IReadOnlyDictionary<string, IStructureSpikeTransport> grpcSpikeTransports,
        bool useHttpSpikeTransportFallback,
        ConcurrentDictionary<(StructureId Source, StructureId Target, NTEnum Nt), int> activePathways,
        NeuronalVisualAttentionDecision visualAttention,
        double minVisualFocusConfidence,
        double minAuditoryRateHz,
        int burstPerToken,
        int maxTokens,
        int tickIoTimeoutMs,
        int maxSpikesPerDispatchRequest,
        CancellationToken stoppingToken)
    {
        if (processedSnapshots.Count == 0)
        {
            return PerceptionLanguageConditioningStats.Empty;
        }

        if (!neuronalPercept.Available || !neuronalPercept.Active || neuronalPercept.DominantEnsemble < 0)
        {
            return PerceptionLanguageConditioningStats.Empty;
        }

        var focusConfidence = Math.Clamp((double)visualAttention.FocusConfidence, 0.0, 1.0);
        if (focusConfidence < minVisualFocusConfidence)
        {
            return PerceptionLanguageConditioningStats.Empty;
        }

        var auditoryRateHz = ComputePerceptionAuditoryRateHz(processedSnapshots);
        if (auditoryRateHz < minAuditoryRateHz)
        {
            return PerceptionLanguageConditioningStats.Empty;
        }

        var (mode, sourceHemisphere) = ResolvePerceptionLanguageRoute(visualAttention, auditoryRateHz);
        var stimulusTargets = GetLanguageStimulusPlanForMode(mode);
        if (stimulusTargets.Count == 0)
        {
            return PerceptionLanguageConditioningStats.Empty;
        }

        var tokens = BuildPerceptionLanguageTokens(
            visualAttention,
            auditoryRateHz,
            neuronalPercept.DominantEnsemble,
            maxTokens);
        if (tokens.Count == 0)
        {
            return PerceptionLanguageConditioningStats.Empty;
        }

        var intensity = Math.Clamp(
            (float)((0.42 * focusConfidence) + (0.58 * Math.Clamp(auditoryRateHz / 45.0, 0.0, 1.0))),
            0.35f,
            1.25f);
        var ioTimeoutMs = Math.Max(250, tickIoTimeoutMs);

        var generated = 0;
        var delivered = 0;
        var dispatchErrors = 0;
        string? lastError = null;
        var errorGate = new object();
        var dispatchTasks = new List<Task>(stimulusTargets.Count);

        foreach (var target in stimulusTargets)
        {
            var resolution = languageBackoffPolicy.Resolve(mode, target, runtimeCatalog, sourceHemisphere, tickSignal.Tick);
            if (!resolution.Resolved || resolution.Target is null || resolution.Instances.Count == 0)
            {
                dispatchErrors++;
                var reason = string.IsNullOrWhiteSpace(resolution.FailureReason)
                    ? "no target instances available"
                    : resolution.FailureReason;
                var syntheticError = new InvalidOperationException(reason);
                languageBackoffPolicy.RecordDispatchResult(resolution.Edge, 0, syntheticError, tickSignal.Tick);
                lock (errorGate)
                {
                    lastError ??= $"{target.SourceStructure}->{target.TargetStructure}: {reason}";
                }

                continue;
            }

            var targetInstance = resolution.Instances
                .OrderBy(i => string.Equals(i.Hemisphere, "L", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(i => i.InstanceKey, StringComparer.OrdinalIgnoreCase)
                .First();
            if (!clients.ContainsKey(targetInstance.InstanceKey))
            {
                continue;
            }

            var targetHemisphere = string.IsNullOrWhiteSpace(targetInstance.Hemisphere)
                ? "M"
                : targetInstance.Hemisphere.Trim().ToUpperInvariant();
            var spikes = BuildLanguageStimulusSpikesForTarget(
                tickSignal.Tick,
                tickSignal.TimestampMs,
                resolution.Target,
                targetHemisphere,
                mode,
                tokens,
                intensity,
                burstPerToken);
            if (spikes.Count == 0)
            {
                continue;
            }

            generated += spikes.Count;
            dispatchTasks.Add(DispatchPerceptionLanguageTargetAsync(
                tickSignal,
                state,
                dispatchSemaphore,
                clients,
                grpcSpikeTransports,
                useHttpSpikeTransportFallback,
                activePathways,
                resolution.Edge,
                targetInstance,
                sourceHemisphere,
                targetHemisphere,
                spikes,
                ioTimeoutMs,
                maxSpikesPerDispatchRequest,
                stoppingToken,
                onDelivered: accepted => Interlocked.Add(ref delivered, accepted),
                onError: error =>
                {
                    Interlocked.Increment(ref dispatchErrors);
                    lock (errorGate)
                    {
                        lastError ??= error;
                    }
                }));
        }

        if (dispatchTasks.Count > 0)
        {
            await Task.WhenAll(dispatchTasks);
        }

        if (generated == 0)
        {
            return PerceptionLanguageConditioningStats.Empty;
        }

        if (dispatchErrors > 0 && (tickSignal.Tick % 64) == 0)
        {
            state.AppendOutputLog(
                $"Perception-language bridge: generated={generated}, delivered={delivered}, errors={dispatchErrors}, last={lastError ?? "n/a"}.");
        }

        return new PerceptionLanguageConditioningStats(generated, delivered, dispatchErrors, lastError);
    }

    private async Task DispatchPerceptionLanguageTargetAsync(
        TickSignal tickSignal,
        SimulationState state,
        SemaphoreSlim dispatchSemaphore,
        IReadOnlyDictionary<string, HttpClient> clients,
        IReadOnlyDictionary<string, IStructureSpikeTransport> grpcSpikeTransports,
        bool useHttpSpikeTransportFallback,
        ConcurrentDictionary<(StructureId Source, StructureId Target, NTEnum Nt), int> activePathways,
        LanguageBackoffEdgeHandle edge,
        ServiceInstance targetInstance,
        string sourceHemisphere,
        string targetHemisphere,
        IReadOnlyList<SpikeMessage> spikes,
        int tickIoTimeoutMs,
        int maxSpikesPerDispatchRequest,
        CancellationToken stoppingToken,
        Action<int> onDelivered,
        Action<string> onError)
    {
        await dispatchSemaphore.WaitAsync(stoppingToken);
        try
        {
            using var ctsIo = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            ctsIo.CancelAfter(TimeSpan.FromMilliseconds(tickIoTimeoutMs));

            var deliveredTotal = 0;
            var chunkSize = Math.Clamp(maxSpikesPerDispatchRequest, 1, 4096);
            for (var offset = 0; offset < spikes.Count; offset += chunkSize)
            {
                var count = Math.Min(chunkSize, spikes.Count - offset);
                if (count <= 0)
                {
                    continue;
                }

                var chunk = new List<SpikeMessage>(count);
                for (var i = 0; i < count; i++)
                {
                    chunk.Add(spikes[offset + i]);
                }

                var deliveredChunk = await SendSpikeBatchToTargetAsync(
                    targetInstance.InstanceKey,
                    chunk,
                    grpcSpikeTransports,
                    clients,
                    useHttpSpikeTransportFallback,
                    ctsIo.Token);
                if (deliveredChunk <= 0)
                {
                    break;
                }

                var acceptedChunk = Math.Min(deliveredChunk, chunk.Count);
                for (var i = 0; i < acceptedChunk; i++)
                {
                    var key = (chunk[i].SourceStructure, chunk[i].TargetStructure, chunk[i].Neurotransmitter);
                    activePathways.AddOrUpdate(key, 1, (_, countValue) => countValue + 1);
                }

                deliveredTotal += acceptedChunk;
                if (acceptedChunk < chunk.Count)
                {
                    break;
                }
            }

            if (deliveredTotal > 0)
            {
                onDelivered(deliveredTotal);
                state.RecordDispatchedSpikes(
                    tickSignal.Tick,
                    tickSignal.TimestampMs,
                    sourceHemisphere,
                    targetHemisphere,
                    targetInstance.InstanceKey,
                    spikes,
                    deliveredTotal);
                languageBackoffPolicy.RecordDispatchResult(edge, deliveredTotal, null, tickSignal.Tick);
                return;
            }

            var noDelivery = $"perception-language dispatch delivered 0 spikes for {targetInstance.InstanceKey}";
            languageBackoffPolicy.RecordDispatchResult(edge, 0, new InvalidOperationException(noDelivery), tickSignal.Tick);
            onError($"{targetInstance.InstanceKey}: {noDelivery}");
        }
        catch (Exception ex)
        {
            languageBackoffPolicy.RecordDispatchResult(edge, 0, ex, tickSignal.Tick);
            onError($"{targetInstance.InstanceKey}: {ClassifyFailure(ex)}");
        }
        finally
        {
            dispatchSemaphore.Release();
        }
    }

    private static IReadOnlyList<string> BuildPerceptionLanguageTokens(
        NeuronalVisualAttentionDecision visualAttention,
        double auditoryRateHz,
        int perceptEnsemble,
        int maxTokens)
    {
        var focusedField = string.IsNullOrWhiteSpace(visualAttention.FocusedField)
            ? "neutral"
            : visualAttention.FocusedField.Trim().ToLowerInvariant();
        var hemisphere = string.IsNullOrWhiteSpace(visualAttention.FocusedHemisphere)
            ? "m"
            : visualAttention.FocusedHemisphere.Trim().ToLowerInvariant();
        var focusBucket = Math.Clamp((int)Math.Round(Math.Clamp(visualAttention.FocusConfidence, 0f, 1f) * 100.0f), 0, 100);
        var auditoryBucket = Math.Clamp((int)Math.Round(auditoryRateHz), 0, 200);

        var tokens = new List<string>(8)
        {
            "perception",
            $"ensemble_{Math.Clamp(perceptEnsemble, 0, 7)}",
            focusedField,
            $"hemi_{hemisphere}",
            $"focus_{focusBucket}",
            $"aud_{auditoryBucket}",
            visualAttention.SustainedSelectionTicks > 1 ? "stable" : "new_selection"
        };

        if (auditoryRateHz >= 18.0)
        {
            tokens.Add("phonemic");
        }
        else
        {
            tokens.Add("ambient");
        }

        return tokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxTokens))
            .ToArray();
    }

    private static double ComputePerceptionAuditoryRateHz(IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var auditoryRates = snapshots
            .Where(s =>
                s.StructureId is StructureId.A1 or StructureId.WernickePstgPsts or StructureId.TemporalAssociation)
            .Select(s => (double)Math.Max(0f, s.MeanFiringRateHz))
            .ToList();
        if (auditoryRates.Count == 0)
        {
            return 0.0;
        }

        return auditoryRates.Average();
    }

    private static (string Mode, string Hemisphere) ResolvePerceptionLanguageRoute(
        NeuronalVisualAttentionDecision visualAttention,
        double auditoryRateHz)
    {
        var hemisphere = NormalizeHemisphereHintForDispatch(visualAttention.FocusedHemisphere);
        var focusedField = (visualAttention.FocusedField ?? string.Empty).Trim().ToLowerInvariant();
        var prosodyCue =
            focusedField.Contains("pitch", StringComparison.Ordinal) ||
            focusedField.Contains("rhythm", StringComparison.Ordinal) ||
            focusedField.Contains("tone", StringComparison.Ordinal) ||
            focusedField.Contains("melody", StringComparison.Ordinal) ||
            focusedField.Contains("contour", StringComparison.Ordinal);

        if (auditoryRateHz >= 18.0 && (prosodyCue || string.Equals(hemisphere, "R", StringComparison.OrdinalIgnoreCase)))
        {
            return ("prosody", hemisphere ?? "R");
        }

        if (auditoryRateHz >= 24.0 && visualAttention.FocusConfidence >= 0.72f)
        {
            return ("emergent", hemisphere ?? "L");
        }

        return ("comprehension", hemisphere ?? "L");
    }

    private static string? NormalizeHemisphereHintForDispatch(string? hemisphere)
    {
        if (string.IsNullOrWhiteSpace(hemisphere))
        {
            return null;
        }

        var trimmed = hemisphere.Trim();
        if (trimmed.Equals("both", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("any", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("*", StringComparison.Ordinal))
        {
            return null;
        }

        if (trimmed.Equals("left", StringComparison.OrdinalIgnoreCase))
        {
            return "L";
        }

        if (trimmed.Equals("right", StringComparison.OrdinalIgnoreCase))
        {
            return "R";
        }

        if (trimmed.Equals("midline", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("middle", StringComparison.OrdinalIgnoreCase))
        {
            return "M";
        }

        if (trimmed.Length == 1)
        {
            var c = char.ToUpperInvariant(trimmed[0]);
            if (c is 'L' or 'R' or 'M')
            {
                return c.ToString();
            }
        }

        return null;
    }

    private static IReadOnlyList<LanguageStimulusTarget> GetLanguageStimulusPlanForMode(string mode) => mode switch
    {
        "english" =>
        [
            new(StructureId.Thalamus, StructureId.A1, "a1_english_phoneme", null, 0.92f),
            new(StructureId.A1, StructureId.WernickePstgPsts, "wernicke_english_lexeme", "L", 1.05f),
            new(StructureId.WernickePstgPsts, StructureId.SupramarginalAngular, "smg_english_phonological", "L", 0.95f),
            new(StructureId.WernickePstgPsts, StructureId.TemporalAssociation, "temporal_english_semantic", "L", 1.00f),
            new(StructureId.TemporalAssociation, StructureId.Pfc, "pfc_english_context", "L", 0.90f),
            new(StructureId.WernickePstgPsts, StructureId.ArcuateFasciculus, "arcuate_english_dorsal", "L", 0.88f),
            new(StructureId.ArcuateFasciculus, StructureId.BrocaBa44Ba45, "broca_english_sequence", "L", 0.82f),
            new(StructureId.BrocaBa44Ba45, StructureId.Sma, "sma_english_inner_speech", "L", 0.70f),
            new(StructureId.Sma, StructureId.M1, "m1_english_articulation", null, 0.62f)
        ],
        "comprehension" =>
        [
            new(StructureId.Thalamus, StructureId.A1, "a1_tonotopic", null, 0.90f),
            new(StructureId.A1, StructureId.WernickePstgPsts, "wernicke_lexical", "L", 1.00f),
            new(StructureId.WernickePstgPsts, StructureId.SupramarginalAngular, "smg_phonological", "L", 0.92f),
            new(StructureId.SupramarginalAngular, StructureId.TemporalAssociation, "temporal_semantic", "L", 0.86f),
            new(StructureId.TemporalAssociation, StructureId.Pfc, "pfc_language_context", "L", 0.78f)
        ],
        "production" =>
        [
            new(StructureId.Pfc, StructureId.BrocaBa44Ba45, "broca_sequence", "L", 1.00f),
            new(StructureId.BrocaBa44Ba45, StructureId.Sma, "sma_speech_sequence", "L", 0.95f),
            new(StructureId.Sma, StructureId.M1, "m1_articulation", null, 0.90f)
        ],
        "prosody" =>
        [
            new(StructureId.Thalamus, StructureId.A1, "a1_tonotopic", null, 0.88f),
            new(StructureId.A1, StructureId.TemporalAssociation, "temporal_prosody", "R", 1.00f),
            new(StructureId.TemporalAssociation, StructureId.SupramarginalAngular, "smg_rhythmic", "R", 0.88f),
            new(StructureId.TemporalAssociation, StructureId.Insula, "insula_affective_prosody", "R", 0.92f),
            new(StructureId.Insula, StructureId.OrbitofrontalCortex, "ofc_valence", "R", 0.86f),
            new(StructureId.TemporalAssociation, StructureId.Pfc, "pfc_prosodic_context", "R", 0.82f),
            new(StructureId.Pfc, StructureId.Sma, "sma_prosodic_motor", "R", 0.76f),
            new(StructureId.Sma, StructureId.M1, "m1_prosodic_articulation", null, 0.70f)
        ],
        "emergent" =>
        [
            new(StructureId.Thalamus, StructureId.A1, "a1_tonotopic", null, 0.88f),
            new(StructureId.A1, StructureId.WernickePstgPsts, "wernicke_lexical", "L", 0.96f),
            new(StructureId.WernickePstgPsts, StructureId.ArcuateFasciculus, "arcuate_dorsal", "L", 1.00f),
            new(StructureId.WernickePstgPsts, StructureId.SupramarginalAngular, "smg_phonological", "L", 0.90f),
            new(StructureId.ArcuateFasciculus, StructureId.BrocaBa44Ba45, "broca_dorsal_input", "L", 1.00f),
            new(StructureId.TemporalAssociation, StructureId.Pfc, "pfc_language_context", null, 0.82f),
            new(StructureId.Pfc, StructureId.BrocaBa44Ba45, "broca_sequence", "L", 0.94f),
            new(StructureId.BrocaBa44Ba45, StructureId.Sma, "sma_speech_sequence", "L", 0.94f),
            new(StructureId.Sma, StructureId.M1, "m1_articulation", null, 0.88f)
        ],
        _ =>
        [
            new(StructureId.Thalamus, StructureId.A1, "a1_tonotopic", null, 0.85f),
            new(StructureId.A1, StructureId.WernickePstgPsts, "wernicke_lexical", "L", 0.95f),
            new(StructureId.WernickePstgPsts, StructureId.ArcuateFasciculus, "arcuate_dorsal", "L", 1.00f),
            new(StructureId.WernickePstgPsts, StructureId.SupramarginalAngular, "smg_phonological", "L", 0.90f),
            new(StructureId.ArcuateFasciculus, StructureId.BrocaBa44Ba45, "broca_dorsal_input", "L", 1.00f),
            new(StructureId.BrocaBa44Ba45, StructureId.Sma, "sma_speech_sequence", "L", 0.95f),
            new(StructureId.Sma, StructureId.M1, "m1_articulation", null, 0.90f)
        ]
    };

    private static List<SpikeMessage> BuildLanguageStimulusSpikesForTarget(
        long tick,
        double timestampMs,
        LanguageStimulusTarget target,
        string hemisphere,
        string mode,
        IReadOnlyList<string> tokens,
        float intensity,
        int burstPerToken)
    {
        var spikes = new List<SpikeMessage>(tokens.Count * burstPerToken);
        const float modeGain = 1f;
        for (var tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
        {
            var token = tokens[tokenIndex];
            var tokenHash = Math.Abs(token.GetHashCode(StringComparison.Ordinal));
            for (var i = 0; i < burstPerToken; i++)
            {
                var channel = (tokenHash + (i * 17) + (tokenIndex * 7)) % 96;
                var syllableHint = (token.Length + i) % 8;
                var vesicle = Math.Clamp((0.52f + (token.Length * 0.03f) + (syllableHint * 0.015f)) * intensity * target.Gain * modeGain, 0.05f, 6.0f);
                var reuptake = ResolveLanguageReuptakeRate(mode, channel);
                var spikeType = ResolveLanguageSpikeType(mode, token, i);

                spikes.Add(new SpikeMessage
                {
                    MessageId = Guid.NewGuid(),
                    TimestampMs = timestampMs,
                    SourceStructure = target.SourceStructure,
                    TargetStructure = target.TargetStructure,
                    SourceNeuronId = $"{hemisphere}:lang_{mode}_{tick}_{tokenIndex}_{i}",
                    TargetNeuronId = BuildLanguageTargetNeuronId(hemisphere, target.TargetNeuronPrefix, mode, token, tokenIndex, channel),
                    SynapseId = Guid.NewGuid(),
                    Neurotransmitter = NTEnum.GLUTAMATE,
                    VesicleQuanta = vesicle,
                    ReuptakeRate = reuptake,
                    SpikeType = spikeType,
                    IsFeedback = false,
                    ModulationContext = null
                });
            }
        }

        return spikes;
    }

    private static SpikeTypeEnum ResolveLanguageSpikeType(string mode, string token, int burstIndex)
    {
        if (mode.Equals("prosody", StringComparison.OrdinalIgnoreCase))
        {
            return burstIndex % 3 == 0 ? SpikeTypeEnum.GRADED : SpikeTypeEnum.ACTION_POTENTIAL;
        }

        if (mode.Equals("english", StringComparison.OrdinalIgnoreCase))
        {
            return burstIndex % 4 == 0 || token.Length >= 7
                ? SpikeTypeEnum.BURST
                : SpikeTypeEnum.ACTION_POTENTIAL;
        }

        return mode.Equals("repetition", StringComparison.OrdinalIgnoreCase) || token.Length >= 8
            ? SpikeTypeEnum.BURST
            : SpikeTypeEnum.ACTION_POTENTIAL;
    }

    private static float ResolveLanguageReuptakeRate(string mode, int channel)
    {
        var baseRate = mode.Equals("prosody", StringComparison.OrdinalIgnoreCase)
            ? 3.2f
            : mode.Equals("english", StringComparison.OrdinalIgnoreCase) ? 2.6f : 2.8f;
        return Math.Clamp(baseRate + (channel * 0.05f), 1.4f, 10.0f);
    }

    private static string BuildLanguageTargetNeuronId(string hemisphere, string prefix, string mode, string token, int tokenIndex, int channel)
    {
        var lexicalKey = Regex.Replace(token.Trim().ToLowerInvariant(), @"[^a-z0-9']+", string.Empty);
        lexicalKey = string.IsNullOrWhiteSpace(lexicalKey) ? "silence" : lexicalKey[..Math.Min(32, lexicalKey.Length)];
        return $"{hemisphere}:{prefix}_lex_{lexicalKey}_tok_{tokenIndex}_cell_{channel}";
    }

    private static double ComputePercentile(IReadOnlyList<double> values, double quantile)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var q = Math.Clamp(quantile, 0.0, 1.0);
        var position = (values.Count - 1) * q;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return values[lower];
        }

        var weight = position - lower;
        return (values[lower] * (1.0 - weight)) + (values[upper] * weight);
    }

    private async Task<SpontaneousInjectionStats> InjectSpontaneousSpikesAsync(
        TickSignal tickSignal,
        SimulationState state,
        IReadOnlyList<ServiceInstance> activeServices,
        SemaphoreSlim dispatchSemaphore,
        IReadOnlyDictionary<string, HttpClient> clients,
        IReadOnlyDictionary<string, IStructureSpikeTransport> grpcSpikeTransports,
        bool useHttpSpikeTransportFallback,
        IReadOnlyDictionary<StructureId, List<SynapticConnection>> connectivity,
        IReadOnlyDictionary<StructureId, List<ServiceInstance>> instancesByStructure,
        ConcurrentDictionary<(StructureId Source, StructureId Target, NTEnum Nt), int> activePathways,
        IDictionary<StructureId, HashSet<string>> spontaneousNeuronIdsByStructure,
        double scale,
        int maxEventsPerTick,
        bool benchmarkMode,
        bool forceFallbackWhenSilent,
        AttentionVector attentionBias,
        NeuronalVisualAttentionDecision visualAttention,
        int tickIoTimeoutMs,
        int maxSpikesPerDispatchRequest,
        CancellationToken stoppingToken)
    {
        if (scale <= 0.0 || activeServices.Count == 0 || maxEventsPerTick <= 0)
        {
            return SpontaneousInjectionStats.Empty;
        }

        var tickDurationSeconds = Math.Max(0.0001, tickSignal.TickDurationMs / 1000.0);
        var dispatchQueue = new ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>>(StringComparer.OrdinalIgnoreCase);
        var dispatchQueueMetrics = new DispatchQueueMetrics();
        var previousTransport = state.TransportStats;
        var queuePressure = DispatchQueueRuntime.ComputePressure(previousTransport);
        var (dispatchQueueMaxBatches, dispatchQueueMaxSpikes) = DispatchQueueRuntime.ComputeLimits(
            Math.Max(64, activeServices.Count * 4),
            Math.Max(512, activeServices.Count * 24),
            previousTransport.AdaptivePressure,
            queuePressure,
            previousTransport.DispatchQueueDroppedBatches,
            previousTransport.DispatchQueueDroppedSpikes,
            activeServices.Count,
            maxGrowthScale: 2.80);

        var generated = QueueScheduledSpontaneousSpikes(
            tickSignal,
            activeServices,
            clients,
            connectivity,
            instancesByStructure,
            spontaneousNeuronIdsByStructure,
            scale,
            tickDurationSeconds,
            maxEventsPerTick,
            attentionBias,
            visualAttention,
            dispatchQueue,
            dispatchQueueMetrics,
            dispatchQueueMaxBatches,
            dispatchQueueMaxSpikes);

        if (generated == 0 && forceFallbackWhenSilent)
        {
            generated += QueueFallbackSpontaneousSpike(
                tickSignal,
                activeServices,
                clients,
                connectivity,
                instancesByStructure,
                spontaneousNeuronIdsByStructure,
                dispatchQueue,
                dispatchQueueMetrics,
                dispatchQueueMaxBatches,
                dispatchQueueMaxSpikes);
        }

        var flush = await FlushQueuedDispatchBatchesAsync(
            tickSignal,
            state,
            dispatchSemaphore,
            dispatchQueue,
            grpcSpikeTransports,
            clients,
                useHttpSpikeTransportFallback,
                activePathways,
                tickIoTimeoutMs,
                maxSpikesPerDispatchRequest,
                stoppingToken);

        var dispatchErrors = flush.DispatchErrors + dispatchQueueMetrics.DroppedBatches;
        var lastError = flush.LastError;
        if (dispatchQueueMetrics.DroppedBatches > 0)
        {
            lastError ??= $"spontaneous queue saturated: dropped {dispatchQueueMetrics.DroppedBatches} batch(es) / {dispatchQueueMetrics.DroppedSpikes} spike(s)";
        }

        return new SpontaneousInjectionStats(generated, flush.DeliveredSpikes, dispatchErrors, lastError);
    }

    private int QueueScheduledSpontaneousSpikes(
        TickSignal tickSignal,
        IReadOnlyList<ServiceInstance> activeServices,
        IReadOnlyDictionary<string, HttpClient> clients,
        IReadOnlyDictionary<StructureId, List<SynapticConnection>> connectivity,
        IReadOnlyDictionary<StructureId, List<ServiceInstance>> instancesByStructure,
        IDictionary<StructureId, HashSet<string>> spontaneousNeuronIdsByStructure,
        double scale,
        double tickDurationSeconds,
        int maxEventsPerTick,
        AttentionVector attentionBias,
        NeuronalVisualAttentionDecision visualAttention,
        ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>> dispatchQueue,
        DispatchQueueMetrics dispatchQueueMetrics,
        int dispatchQueueMaxBatches,
        int dispatchQueueMaxSpikes)
    {
        var generated = 0;
        var remainingEventBudget = maxEventsPerTick;
        var orderedServices = activeServices
            .OrderBy(s => ComputeSpontaneousServiceOrder(s, tickSignal.Tick))
            .ToArray();
        var hasCerebellarSources = orderedServices.Any(s =>
            IsSpontaneousCerebellarStructure(s.StructureId) &&
            HasSpontaneousRoutes(connectivity, s.StructureId));
        var protectedCerebellarBudget = hasCerebellarSources
            ? Math.Clamp(maxEventsPerTick / 6, 2, 12)
            : 0;
        var generatedCerebellar = 0;

        foreach (var sourceInstance in orderedServices)
        {
            if (remainingEventBudget <= 0)
            {
                break;
            }

            var isCerebellarSource = IsSpontaneousCerebellarStructure(sourceInstance.StructureId);
            if (!isCerebellarSource && remainingEventBudget <= protectedCerebellarBudget)
            {
                continue;
            }

            if (!connectivity.TryGetValue(sourceInstance.StructureId, out var routes) || routes.Count == 0)
            {
                continue;
            }

            var profile = GetSpontaneousNoiseProfile(sourceInstance.StructureId);
            var probabilityPerTick = ComputeSpontaneousProbabilityPerTick(
                sourceInstance,
                profile,
                tickDurationSeconds,
                scale,
                attentionBias,
                visualAttention);
            if (NextDouble() >= probabilityPerTick)
            {
                continue;
            }

            var burstCount = NextInt(profile.MinBurstSpikes, profile.MaxBurstSpikes + 1);
            for (var burstIndex = 0; burstIndex < burstCount && remainingEventBudget > 0; burstIndex++)
            {
                var route = routes[NextInt(0, routes.Count)];
                var spike = BuildSpontaneousSpike(tickSignal, sourceInstance, route, profile, burstCount > 1);
                generated++;
                if (isCerebellarSource)
                {
                    generatedCerebellar++;
                }

                remainingEventBudget--;
                QueueSpontaneousSpikeForTargets(
                    tickSignal,
                    sourceInstance,
                    route,
                    spike,
                    clients,
                    instancesByStructure,
                    spontaneousNeuronIdsByStructure,
                    dispatchQueue,
                    dispatchQueueMetrics,
                    dispatchQueueMaxBatches,
                    dispatchQueueMaxSpikes,
                    "Spontaneous");
            }
        }

        if (hasCerebellarSources && generatedCerebellar == 0 && remainingEventBudget > 0)
        {
            generated += QueueFallbackSpontaneousSpike(
                tickSignal,
                orderedServices,
                clients,
                connectivity,
                instancesByStructure,
                spontaneousNeuronIdsByStructure,
                dispatchQueue,
                dispatchQueueMetrics,
                dispatchQueueMaxBatches,
                dispatchQueueMaxSpikes,
                IsCerebellarSpontaneousSource,
                "Cerebellar spontaneous");
        }

        return generated;
    }

    private static bool IsTransportSilent(TransportRuntimeStats stats)
        => stats.GeneratedSpikes <= 0 &&
           stats.RoutedSpikes <= 0 &&
           stats.DeliveredSpikes <= 0 &&
           stats.ActivePathways <= 0 &&
           stats.SpontaneousDelivered <= 0 &&
           stats.PerceptionLanguageDelivered <= 0;

    private int QueueFallbackSpontaneousSpike(
        TickSignal tickSignal,
        IReadOnlyList<ServiceInstance> activeServices,
        IReadOnlyDictionary<string, HttpClient> clients,
        IReadOnlyDictionary<StructureId, List<SynapticConnection>> connectivity,
        IReadOnlyDictionary<StructureId, List<ServiceInstance>> instancesByStructure,
        IDictionary<StructureId, HashSet<string>> spontaneousNeuronIdsByStructure,
        ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>> dispatchQueue,
        DispatchQueueMetrics dispatchQueueMetrics,
        int dispatchQueueMaxBatches,
        int dispatchQueueMaxSpikes,
        Func<ServiceInstance, bool>? sourcePredicate = null,
        string logPrefix = "Fallback spontaneous")
    {
        var fallbackSource = activeServices
            .Where(s =>
                (sourcePredicate is null || sourcePredicate(s)) &&
                connectivity.TryGetValue(s.StructureId, out var routes) &&
                routes.Count > 0)
            .OrderBy(_ => NextDouble())
            .FirstOrDefault();

        if (fallbackSource is null ||
            !connectivity.TryGetValue(fallbackSource.StructureId, out var fallbackRoutes) ||
            fallbackRoutes.Count == 0)
        {
            return 0;
        }

        var profile = GetSpontaneousNoiseProfile(fallbackSource.StructureId);
        var route = fallbackRoutes[NextInt(0, fallbackRoutes.Count)];
        var spike = BuildSpontaneousSpike(tickSignal, fallbackSource, route, profile, burstCluster: false);
        QueueSpontaneousSpikeForTargets(
            tickSignal,
            fallbackSource,
            route,
            spike,
            clients,
            instancesByStructure,
            spontaneousNeuronIdsByStructure,
            dispatchQueue,
            dispatchQueueMetrics,
            dispatchQueueMaxBatches,
            dispatchQueueMaxSpikes,
            logPrefix);
        return 1;
    }

    private static bool IsCerebellarSpontaneousSource(ServiceInstance sourceInstance)
        => IsSpontaneousCerebellarStructure(sourceInstance.StructureId);

    private static bool HasSpontaneousRoutes(
        IReadOnlyDictionary<StructureId, List<SynapticConnection>> connectivity,
        StructureId structureId)
        => connectivity.TryGetValue(structureId, out var routes) && routes.Count > 0;

    private static bool IsSpontaneousCerebellarStructure(StructureId structureId)
        => structureId is StructureId.CerebellarGranule
            or StructureId.CerebellarVermis
            or StructureId.CerebellarLobules
            or StructureId.PurkinjeCellLayer
            or StructureId.DeepCerebellarNuclei
            or StructureId.InferiorOlive;

    private static int ComputeSpontaneousServiceOrder(ServiceInstance sourceInstance, long tick)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in sourceInstance.InstanceKey)
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            hash ^= (uint)tick;
            hash *= 16777619u;
            hash ^= (uint)(tick >> 32);
            return (int)(hash & 0x7fffffffu);
        }
    }

    private double ComputeSpontaneousProbabilityPerTick(
        ServiceInstance sourceInstance,
        SpontaneousNoiseProfile profile,
        double tickDurationSeconds,
        double scale,
        AttentionVector attentionBias,
        NeuronalVisualAttentionDecision visualAttention)
    {
        var lateralizationWeight = GetHemisphereLateralizationWeight(sourceInstance.StructureId, sourceInstance.Hemisphere);
        var attentionWeight = GetAttentionWeightForStructure(sourceInstance.StructureId, attentionBias);
        var visualHemifieldWeight = GetVisualFieldHemisphereWeight(sourceInstance.StructureId, sourceInstance.Hemisphere, visualAttention);
        return Math.Clamp(profile.EventsPerSecond * tickDurationSeconds * scale * lateralizationWeight * attentionWeight * visualHemifieldWeight, 0.0, 1.0);
    }

    private void QueueSpontaneousSpikeForTargets(
        TickSignal tickSignal,
        ServiceInstance sourceInstance,
        SynapticConnection route,
        SpikeMessage spike,
        IReadOnlyDictionary<string, HttpClient> clients,
        IReadOnlyDictionary<StructureId, List<ServiceInstance>> instancesByStructure,
        IDictionary<StructureId, HashSet<string>> spontaneousNeuronIdsByStructure,
        ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>> dispatchQueue,
        DispatchQueueMetrics dispatchQueueMetrics,
        int dispatchQueueMaxBatches,
        int dispatchQueueMaxSpikes,
        string logPrefix)
    {
        foreach (var targetInstance in ResolveTargetInstances(
            route.Target,
            sourceInstance.Hemisphere,
            instancesByStructure,
            sourceInstance.StructureId,
            route.ProjectionType))
        {
            if (!clients.ContainsKey(targetInstance.InstanceKey))
            {
                continue;
            }

            var queued = DispatchQueueRuntime.TryEnqueue(
                dispatchQueue,
                targetInstance.InstanceKey,
                new QueuedDispatchBatch(
                    sourceInstance.InstanceKey,
                    sourceInstance.Hemisphere,
                    targetInstance.Hemisphere,
                    [spike]),
                dispatchQueueMetrics,
                dispatchQueueMaxBatches,
                dispatchQueueMaxSpikes);
            if (!queued)
            {
                logger.LogDebug(
                    "{Prefix} queue saturated at tick {Tick}: dropped {SourceInstance}->{TargetInstance}",
                    logPrefix,
                    tickSignal.Tick,
                    sourceInstance.InstanceKey,
                    targetInstance.InstanceKey);
                continue;
            }

            RecordSpontaneousNeuron(spontaneousNeuronIdsByStructure, spike.SourceStructure, sourceInstance.Hemisphere, spike.SourceNeuronId);
            RecordSpontaneousNeuron(spontaneousNeuronIdsByStructure, spike.TargetStructure, targetInstance.Hemisphere, spike.TargetNeuronId);
        }
    }

    private static void RecordSpontaneousNeuron(
        IDictionary<StructureId, HashSet<string>> collector,
        StructureId structureId,
        string hemisphere,
        string? neuronId)
    {
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return;
        }

        var normalized = neuronId.Trim();
        if (normalized.IndexOf(':') < 0)
        {
            var hemi = string.IsNullOrWhiteSpace(hemisphere) ? "M" : hemisphere.Trim();
            normalized = $"{hemi}:{normalized}";
        }

        if (!collector.TryGetValue(structureId, out var ids))
        {
            ids = new HashSet<string>(StringComparer.Ordinal);
            collector[structureId] = ids;
        }

        ids.Add(normalized);
    }

    private SpikeMessage BuildSpontaneousSpike(
        TickSignal tickSignal,
        ServiceInstance sourceInstance,
        SynapticConnection route,
        SpontaneousNoiseProfile profile,
        bool burstCluster)
    {
        var vesicleRange = Math.Max(0.01f, profile.MaxVesicleQuanta - profile.MinVesicleQuanta);
        var vesicleQuanta = profile.MinVesicleQuanta + ((float)NextDouble() * vesicleRange);
        var isFeedback = (route.ProjectionType ?? string.Empty).Contains("feedback", StringComparison.OrdinalIgnoreCase);
        var spikeType = burstCluster || NextDouble() < profile.BurstProbability
            ? SpikeTypeEnum.BURST
            : SpikeTypeEnum.ACTION_POTENTIAL;

        return new SpikeMessage
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = tickSignal.TimestampMs,
            SourceStructure = sourceInstance.StructureId,
            TargetStructure = route.Target,
            SourceNeuronId = $"spont-{sourceInstance.Hemisphere}-{sourceInstance.StructureId}-{NextInt(0, 4096):0000}",
            TargetNeuronId = $"auto-{route.Target}-{NextInt(0, 256):000}",
            SynapseId = route.SynapseId,
            Neurotransmitter = route.Neurotransmitter,
            VesicleQuanta = Math.Max(0.05f, vesicleQuanta),
            ReuptakeRate = GetReuptakeRateForNt(route.Neurotransmitter),
            SpikeType = spikeType,
            IsFeedback = isFeedback,
            ModulationContext = null
        };
    }

    private static double GetAttentionWeightForStructure(StructureId structureId, AttentionVector attention)
    {
        var a = structureId switch
        {
            StructureId.V1 or StructureId.V2 or StructureId.V4 or StructureId.Mt => attention.Visual,
            StructureId.A1 or StructureId.WernickePstgPsts => attention.Auditory,
            StructureId.S1 or StructureId.Ppc or StructureId.M1 or StructureId.Sma => attention.Somatosensory,
            StructureId.Insula or StructureId.Amygdala or StructureId.Acc or StructureId.Hypothalamus => attention.Interoceptive,
            _ => 0.25f
        };

        return Math.Clamp(0.65 + (a * 1.70), 0.35, 2.10);
    }

    private static double GetHemisphereLateralizationWeight(StructureId structureId, string hemisphere)
    {
        var hemi = string.IsNullOrWhiteSpace(hemisphere)
            ? "M"
            : hemisphere.Trim().ToUpperInvariant();
        if (hemi is not "L" and not "R")
        {
            return 1.0;
        }

        var isLeft = hemi == "L";
        return structureId switch
        {
            StructureId.BrocaBa44Ba45 or StructureId.WernickePstgPsts or StructureId.ArcuateFasciculus or StructureId.SupramarginalAngular
                => isLeft ? 1.30 : 0.75,
            StructureId.Pfc or StructureId.TemporalAssociation
                => isLeft ? 1.15 : 0.90,
            StructureId.Ppc or StructureId.Insula or StructureId.Amygdala
                => isLeft ? 0.88 : 1.18,
            _ => 1.0
        };
    }

    private static double GetVisualFieldHemisphereWeight(
        StructureId structureId,
        string hemisphere,
        NeuronalVisualAttentionDecision visualAttention)
    {
        if (!IsVisualAttentionDrivenStructure(structureId))
        {
            return 1.0;
        }

        var hemi = string.IsNullOrWhiteSpace(hemisphere)
            ? "M"
            : hemisphere.Trim().ToUpperInvariant();
        if (hemi is not "L" and not "R")
        {
            return 1.0;
        }

        if (visualAttention.FocusedHemisphere is not "L" and not "R")
        {
            return 1.0;
        }

        var confidence = Math.Clamp(visualAttention.FocusConfidence, 0f, 1f);
        var boost = 1.10 + (0.55 * confidence);
        var suppress = 0.88 - (0.30 * confidence);
        return string.Equals(hemi, visualAttention.FocusedHemisphere, StringComparison.OrdinalIgnoreCase)
            ? boost
            : Math.Max(0.45, suppress);
    }

    private static bool IsVisualAttentionDrivenStructure(StructureId structureId) => structureId switch
    {
        StructureId.V1 or
        StructureId.V2 or
        StructureId.V4 or
        StructureId.Mt or
        StructureId.Thalamus or
        StructureId.Pulvinar or
        StructureId.Ppc or
        StructureId.Pfc => true,
        _ => false
    };

    private static SpontaneousNoiseProfile GetSpontaneousNoiseProfile(StructureId structureId) => structureId switch
    {
        StructureId.Pfc or StructureId.BrocaBa44Ba45 or StructureId.WernickePstgPsts or StructureId.SupramarginalAngular or StructureId.OrbitofrontalCortex or StructureId.Insula or StructureId.Ppc or StructureId.TemporalAssociation or StructureId.PremotorCortex or StructureId.ParahippocampalCortex or StructureId.PerirhinalCortex or StructureId.PosteriorCingulate or StructureId.RetrosplenialCortex or StructureId.Acc or StructureId.M1 or StructureId.Sma
            => new SpontaneousNoiseProfile(8.5, 2, 5, 1.80f, 4.20f, 0.55),
        StructureId.V1 or StructureId.V2 or StructureId.V4 or StructureId.Mt or StructureId.A1 or StructureId.S1 or StructureId.EntorhinalCortex or StructureId.CorpusCallosum
            => new SpontaneousNoiseProfile(6.8, 1, 4, 1.40f, 3.60f, 0.42),
        StructureId.Thalamus or StructureId.MotorThalamus or StructureId.Trn or StructureId.Pulvinar or StructureId.MediodorsalThalamus or StructureId.IntralaminarThalamus
            => new SpontaneousNoiseProfile(5.4, 1, 3, 1.20f, 3.10f, 0.35),
        StructureId.CerebellarGranule
            => new SpontaneousNoiseProfile(6.2, 1, 4, 1.20f, 3.20f, 0.36),
        StructureId.CerebellarVermis or StructureId.CerebellarLobules
            => new SpontaneousNoiseProfile(5.8, 1, 3, 1.15f, 3.00f, 0.34),
        StructureId.PurkinjeCellLayer
            => new SpontaneousNoiseProfile(6.4, 1, 4, 1.05f, 2.70f, 0.30),
        StructureId.DeepCerebellarNuclei
            => new SpontaneousNoiseProfile(5.6, 1, 3, 1.25f, 3.20f, 0.34),
        StructureId.InferiorOlive or StructureId.Pons
            => new SpontaneousNoiseProfile(5.2, 1, 3, 1.10f, 2.90f, 0.32),
        StructureId.Retina
            => new SpontaneousNoiseProfile(4.4, 1, 2, 1.00f, 2.40f, 0.22),
        StructureId.Cochlea or StructureId.CochlearNucleus or StructureId.SuperiorOlive or StructureId.InferiorColliculus
            => new SpontaneousNoiseProfile(4.8, 1, 3, 1.05f, 2.70f, 0.28),
        StructureId.VestibularNuclei or StructureId.NucleusTractusSolitarius or StructureId.OlfactoryBulb
            => new SpontaneousNoiseProfile(4.6, 1, 3, 1.05f, 2.60f, 0.28),
        StructureId.ArcuateFasciculus
            => new SpontaneousNoiseProfile(4.8, 1, 2, 1.00f, 2.50f, 0.24),
        StructureId.Striatum or StructureId.GlobusPallidus or StructureId.GPe or StructureId.GPi or StructureId.Stn or StructureId.Snr
            => new SpontaneousNoiseProfile(5.0, 1, 3, 1.00f, 2.70f, 0.30),
        StructureId.Snc or StructureId.Vta or StructureId.LocusCoeruleus or StructureId.RapheNuclei or StructureId.BasalForebrain
            => new SpontaneousNoiseProfile(4.4, 1, 2, 0.95f, 2.40f, 0.24),
        StructureId.Hypothalamus or StructureId.Amygdala or StructureId.NucleusAccumbens or StructureId.VentralPallidum
            => new SpontaneousNoiseProfile(4.7, 1, 3, 1.05f, 2.70f, 0.30),
        StructureId.ReticularFormation or StructureId.PeriaqueductalGray or StructureId.Medulla
            => new SpontaneousNoiseProfile(4.5, 1, 3, 1.00f, 2.60f, 0.28),
        StructureId.SpinalCordMotor
            => new SpontaneousNoiseProfile(3.8, 1, 2, 0.90f, 2.10f, 0.20),
        StructureId.CA1 or StructureId.CA2 or StructureId.CA3 or StructureId.DentateGyrus or StructureId.Subiculum or StructureId.Presubiculum or StructureId.Parasubiculum or StructureId.Habenula or StructureId.SuperiorColliculus
            => new SpontaneousNoiseProfile(4.6, 1, 3, 1.10f, 2.80f, 0.30),
        _ => new SpontaneousNoiseProfile(3.2, 1, 3, 0.95f, 2.40f, 0.25)
    };

    private static float GetReuptakeRateForNt(NTEnum neurotransmitter) => neurotransmitter switch
    {
        NTEnum.DOPAMINE => 40f,
        NTEnum.SEROTONIN => 50f,
        NTEnum.ACETYLCHOLINE => 20f,
        NTEnum.NOREPINEPHRINE => 30f,
        NTEnum.GABA => 12f,
        _ => 8f
    };

    private double NextDouble()
    {
        lock (_noiseGate)
        {
            return _noiseRandom.NextDouble();
        }
    }

    private int NextInt(int minInclusive, int maxExclusive)
    {
        lock (_noiseGate)
        {
            return _noiseRandom.Next(minInclusive, maxExclusive);
        }
    }

    private static string ClassifyFailure(Exception ex)
    {
        if (ex is TaskCanceledException)
        {
            return "timeout (no TickAck within timeout window)";
        }

        if (ex is HttpRequestException httpEx)
        {
            if (!string.IsNullOrWhiteSpace(httpEx.Message) &&
                httpEx.Message.Contains("Synchronous operations are disallowed", StringComparison.OrdinalIgnoreCase))
            {
                return "target service is running a stale protocol build (restart/rebuild structure services to pick up async spike decoding)";
            }

            return string.IsNullOrWhiteSpace(httpEx.Message)
                ? "http request failure"
                : $"http request failure ({httpEx.Message})";
        }

        var root = ex.GetBaseException();
        if (root is System.Net.Sockets.SocketException sock)
        {
            return $"socket error ({sock.SocketErrorCode})";
        }

        return string.IsNullOrWhiteSpace(ex.Message)
            ? ex.GetType().Name
            : $"{ex.GetType().Name}: {ex.Message}";
    }

    private async Task<DispatchFlushResult> FlushQueuedDispatchBatchesAsync(
        TickSignal tickSignal,
        SimulationState state,
        SemaphoreSlim dispatchSemaphore,
        ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>> dispatchQueueByTarget,
        IReadOnlyDictionary<string, IStructureSpikeTransport> grpcSpikeTransports,
        IReadOnlyDictionary<string, HttpClient> httpClients,
        bool useHttpSpikeTransportFallback,
        ConcurrentDictionary<(StructureId Source, StructureId Target, NTEnum Nt), int> activePathways,
        int tickIoTimeoutMs,
        int maxSpikesPerDispatchRequest,
        CancellationToken stoppingToken)
    {
        if (dispatchQueueByTarget.IsEmpty)
        {
            return DispatchFlushResult.Empty;
        }

        var deliveredSpikes = 0;
        var dispatchErrors = 0;
        var flushedBatches = 0;
        string? lastError = null;
        var errorGate = new object();
        var flushTargets = DispatchQueueRuntime.DrainTargets(dispatchQueueByTarget, out flushedBatches);

        if (flushTargets.Count == 0)
        {
            return DispatchFlushResult.Empty;
        }

        var activeTargets = flushTargets.Count;
        var maxTargetBurstSpikes = flushTargets[0].MergedSpikes.Count;
        var flushTasks = new Task[flushTargets.Count];

        for (var flushIndex = 0; flushIndex < flushTargets.Count; flushIndex++)
        {
            var flushTarget = flushTargets[flushIndex];
            flushTasks[flushIndex] = FlushTargetBatchAsync(
                flushTarget.TargetInstanceKey,
                flushTarget.SourceBatches,
                flushTarget.MergedSpikes,
                tickSignal,
                state,
                dispatchSemaphore,
                grpcSpikeTransports,
                httpClients,
                useHttpSpikeTransportFallback,
                activePathways,
                tickIoTimeoutMs,
                maxSpikesPerDispatchRequest,
                stoppingToken,
                delivered =>
                {
                    if (delivered > 0)
                    {
                        Interlocked.Add(ref deliveredSpikes, delivered);
                    }
                },
                ex =>
                {
                    Interlocked.Increment(ref dispatchErrors);
                    lock (errorGate)
                    {
                        lastError ??= ex;
                    }
                });
        }

        try
        {
            await Task.WhenAll(flushTasks);
        }
        finally
        {
            DispatchQueueRuntime.ReturnTargets(flushTargets);
        }

        return new DispatchFlushResult(
            flushedBatches,
            deliveredSpikes,
            dispatchErrors,
            lastError,
            activeTargets,
            maxTargetBurstSpikes);
    }

    private async Task FlushTargetBatchAsync(
        string targetInstanceKey,
        IReadOnlyList<QueuedDispatchBatch> sourceBatches,
        IReadOnlyList<SpikeMessage> mergedSpikes,
        TickSignal tickSignal,
        SimulationState state,
        SemaphoreSlim dispatchSemaphore,
        IReadOnlyDictionary<string, IStructureSpikeTransport> grpcSpikeTransports,
        IReadOnlyDictionary<string, HttpClient> httpClients,
        bool useHttpSpikeTransportFallback,
        ConcurrentDictionary<(StructureId Source, StructureId Target, NTEnum Nt), int> activePathways,
        int tickIoTimeoutMs,
        int maxSpikesPerDispatchRequest,
        CancellationToken stoppingToken,
        Action<int> onDelivered,
        Action<string> onDispatchError)
    {
        await dispatchSemaphore.WaitAsync(stoppingToken);
        try
        {
            using var ctsIo = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            ctsIo.CancelAfter(TimeSpan.FromMilliseconds(tickIoTimeoutMs));
            var chunkSize = Math.Clamp(maxSpikesPerDispatchRequest, 1, 4096);
            var deliveredTotal = 0;
            for (var offset = 0; offset < mergedSpikes.Count; offset += chunkSize)
            {
                var count = Math.Min(chunkSize, mergedSpikes.Count - offset);
                if (count <= 0)
                {
                    continue;
                }

                var chunk = ListPool<SpikeMessage>.Rent(count);
                for (var i = 0; i < count; i++)
                {
                    chunk.Add(mergedSpikes[offset + i]);
                }
                try
                {
                    var deliveredChunk = await SendSpikeBatchToTargetAsync(
                        targetInstanceKey,
                        chunk,
                        grpcSpikeTransports,
                        httpClients,
                        useHttpSpikeTransportFallback,
                        ctsIo.Token);
                    if (deliveredChunk <= 0)
                    {
                        break;
                    }

                    var acceptedChunk = Math.Min(deliveredChunk, chunk.Count);
                    for (var i = 0; i < acceptedChunk; i++)
                    {
                        var key = (chunk[i].SourceStructure, chunk[i].TargetStructure, chunk[i].Neurotransmitter);
                        activePathways.AddOrUpdate(key, 1, (_, countValue) => countValue + 1);
                    }

                    deliveredTotal += acceptedChunk;
                    if (acceptedChunk < chunk.Count)
                    {
                        break;
                    }
                }
                finally
                {
                    ListPool<SpikeMessage>.Return(chunk);
                }
            }

            if (deliveredTotal <= 0)
            {
                return;
            }

            var remaining = Math.Min(deliveredTotal, mergedSpikes.Count);
            foreach (var batch in sourceBatches)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var acceptedForBatch = Math.Min(batch.Spikes.Count, remaining);
                state.RecordDispatchedSpikes(
                    tickSignal.Tick,
                    tickSignal.TimestampMs,
                    batch.SourceHemisphere,
                    batch.TargetHemisphere,
                    targetInstanceKey,
                    batch.Spikes,
                    acceptedForBatch);
                remaining -= acceptedForBatch;
            }

            onDelivered(deliveredTotal);
        }
        catch (Exception dispatchEx)
        {
            logger.LogDebug(
                dispatchEx,
                "Batched dispatch failed for {TargetInstance} ({Count} spikes across {BatchCount} source batches)",
                targetInstanceKey,
                mergedSpikes.Count,
                sourceBatches.Count);
            onDispatchError($"{targetInstanceKey}: {ClassifyFailure(dispatchEx)}");
        }
        finally
        {
            dispatchSemaphore.Release();
        }
    }

    private async Task<int> SendSpikeBatchToTargetAsync(
        string targetInstanceKey,
        IReadOnlyList<SpikeMessage> spikes,
        IReadOnlyDictionary<string, IStructureSpikeTransport> grpcSpikeTransports,
        IReadOnlyDictionary<string, HttpClient> httpClients,
        bool useHttpSpikeTransportFallback,
        CancellationToken cancellationToken)
    {
        if (spikes.Count == 0)
        {
            return 0;
        }

        if (spikes.Count > StructureTransportLimits.MaxSpikeBatchCount)
        {
            var delivered = 0;
            for (var offset = 0; offset < spikes.Count; offset += StructureTransportLimits.MaxSpikeBatchCount)
            {
                var count = Math.Min(StructureTransportLimits.MaxSpikeBatchCount, spikes.Count - offset);
                delivered += await SendSpikeBatchToTargetAsync(
                    targetInstanceKey,
                    spikes.Skip(offset).Take(count).ToArray(),
                    grpcSpikeTransports,
                    httpClients,
                    useHttpSpikeTransportFallback,
                    cancellationToken).ConfigureAwait(false);
            }
            return delivered;
        }

        // Streaming path (opt-in via NRE_USE_GRPC_BIDI_STREAM). Delivery is counted
        // only after the target acknowledges the complete batch.
        if (_streamSessions.TryGetValue(targetInstanceKey, out var streamSession))
        {
            try
            {
                var spikeList = spikes as List<SpikeMessage> ?? spikes.ToList();
                var ack = await streamSession.SendAsync(
                    new SpikeBatchEnvelope { Spikes = spikeList, BatchId = Guid.NewGuid().ToString("N") },
                    cancellationToken);
                if (ack.Accepted == spikes.Count && string.IsNullOrWhiteSpace(ack.Error))
                {
                    return spikes.Count;
                }

                throw new HttpRequestException(
                    $"Spike stream accepted {ack.Accepted}/{spikes.Count} spikes for {targetInstanceKey}. {ack.Error}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception streamEx)
            {
                logger.LogDebug(streamEx, "Spike stream enqueue failed for {TargetInstance}; falling back to unary/HTTP", targetInstanceKey);
            }
        }

        var nowMs = Environment.TickCount64;
        if (!_transportCapabilities.ShouldAttemptGrpc(targetInstanceKey, nowMs))
        {
            if (!useHttpSpikeTransportFallback)
            {
                throw new HttpRequestException($"gRPC transport is disabled for {targetInstanceKey} and HTTP fallback is disabled.");
            }
        }
        else if (grpcSpikeTransports.TryGetValue(targetInstanceKey, out var grpcSpikeTransport))
        {
            try
            {
                var spikeList = spikes as List<SpikeMessage> ?? spikes.ToList();
                var grpcAck = await grpcSpikeTransport.PushSpikeBatchAsync(
                    new SpikeBatchEnvelope
                    {
                        Spikes = spikeList,
                        BatchId = Guid.NewGuid().ToString("N")
                    },
                    new CallContext(new Grpc.Core.CallOptions(cancellationToken: cancellationToken)));
                if (grpcAck.Accepted == spikes.Count && string.IsNullOrWhiteSpace(grpcAck.Error))
                {
                    _transportCapabilities.RecordGrpcSuccess(targetInstanceKey);
                    return spikes.Count;
                }

                if (!useHttpSpikeTransportFallback)
                {
                    throw new HttpRequestException(
                        $"gRPC transport accepted {grpcAck.Accepted}/{spikes.Count} spikes for {targetInstanceKey}. {grpcAck.Error}");
                }
            }
            catch (Exception grpcEx)
            {
                var immediateDisable = ShouldDisableGrpcTransportImmediately(grpcEx);
                var failures = _transportCapabilities.RecordGrpcFailure(targetInstanceKey, immediateDisable, nowMs);
                if (failures >= 6)
                {
                    logger.LogWarning(
                        "Pausing gRPC spike transport for {TargetInstance} after {FailureCount} failures; retrying after cooldown and using HTTP meanwhile.",
                        targetInstanceKey,
                        failures);
                }
                else
                {
                    logger.LogDebug(
                        grpcEx,
                        "gRPC spike transport failed for {TargetInstance}; fallback to HTTP (failure {FailureCount}).",
                        targetInstanceKey,
                        failures);
                }

                if (!useHttpSpikeTransportFallback)
                {
                    throw;
                }
            }
        }

        if (!useHttpSpikeTransportFallback)
        {
            throw new HttpRequestException($"No gRPC transport registered for {targetInstanceKey} and HTTP fallback is disabled.");
        }

        if (!httpClients.TryGetValue(targetInstanceKey, out var targetClient))
        {
            throw new HttpRequestException($"No HTTP client registered for {targetInstanceKey}.");
        }

        var batchEndpointUnavailable = _transportCapabilities.IsHttpBatchEndpointUnavailable(targetInstanceKey);
        var preferJsonBatch = _transportCapabilities.PrefersJsonBatch(targetInstanceKey);

        if (batchEndpointUnavailable)
        {
            var fallbackMode = preferJsonBatch ? HttpSingleFallbackMode.Json : HttpSingleFallbackMode.Binary;
            return await SendSpikesIndividuallyAsync(targetInstanceKey, targetClient, spikes, fallbackMode, cancellationToken);
        }

        if (!preferJsonBatch)
        {
            byte[] payload;
            await using (var envelope = new MemoryStream(Math.Max(256, spikes.Count * 128)))
            {
                foreach (var spike in spikes)
                {
                    await SpikeProtocol.send_spike(spike, envelope, cancellationToken);
                }

                payload = envelope.ToArray();
            }

            try
            {
                using var body = new ByteArrayContent(payload);
                body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                using var dispatch = await targetClient.PostAsync("/api/v1/structure/spike-batch", body, cancellationToken);
                if (dispatch.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
                {
                    _transportCapabilities.MarkHttpBatchEndpointUnavailable(targetInstanceKey);
                    return await SendSpikesIndividuallyAsync(targetInstanceKey, targetClient, spikes, HttpSingleFallbackMode.Binary, cancellationToken);
                }

                if (dispatch.StatusCode == HttpStatusCode.UnsupportedMediaType)
                {
                    _transportCapabilities.MarkPreferJsonBatch(targetInstanceKey);
                }
                else
                {
                    await EnsureSuccessWithDetailsAsync(dispatch, cancellationToken);
                    _transportCapabilities.MarkBinaryBatchSuccess(targetInstanceKey);
                    return spikes.Count;
                }
            }
            catch (Exception binaryDispatchEx) when (!cancellationToken.IsCancellationRequested)
            {
                if (ShouldPreferJsonBatch(binaryDispatchEx))
                {
                    _transportCapabilities.MarkPreferJsonBatch(targetInstanceKey);
                }

                logger.LogDebug(
                    binaryDispatchEx,
                    "Binary spike dispatch failed for {TargetInstance}; retrying JSON compatibility path.",
                    targetInstanceKey);
            }
        }

        using var jsonBody = JsonContent.Create(spikes, DnneJsonContext.Default.ListSpikeMessage);
        using var jsonDispatch = await targetClient.PostAsync("/api/v1/structure/spike-batch", jsonBody, cancellationToken);
        if (jsonDispatch.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            _transportCapabilities.MarkHttpBatchEndpointUnavailable(targetInstanceKey);
            return await SendSpikesIndividuallyAsync(targetInstanceKey, targetClient, spikes, HttpSingleFallbackMode.Json, cancellationToken);
        }

        await EnsureSuccessWithDetailsAsync(jsonDispatch, cancellationToken);
        _transportCapabilities.MarkJsonBatchSuccess(targetInstanceKey);
        return spikes.Count;
    }

    private async Task<int> SendSpikesIndividuallyAsync(
        string targetInstanceKey,
        HttpClient targetClient,
        IReadOnlyList<SpikeMessage> spikes,
        HttpSingleFallbackMode fallbackMode,
        CancellationToken cancellationToken)
    {
        var delivered = 0;
        var mode = fallbackMode;
        for (var i = 0; i < spikes.Count; i++)
        {
            if (mode == HttpSingleFallbackMode.Binary)
            {
                try
                {
                    byte[] spikePayload;
                    await using (var singleEnvelope = new MemoryStream(256))
                    {
                        await SpikeProtocol.send_spike(spikes[i], singleEnvelope, cancellationToken);
                        spikePayload = singleEnvelope.ToArray();
                    }

                    using var singleBody = new ByteArrayContent(spikePayload);
                    singleBody.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    using var singleDispatch = await targetClient.PostAsync("/api/v1/structure/spike", singleBody, cancellationToken);
                    await EnsureSuccessWithDetailsAsync(singleDispatch, cancellationToken);
                    delivered++;
                    continue;
                }
                catch (Exception singleBinaryEx) when (!cancellationToken.IsCancellationRequested && ShouldPreferJsonBatch(singleBinaryEx))
                {
                    mode = HttpSingleFallbackMode.Json;
                    _transportCapabilities.MarkPreferJsonBatch(targetInstanceKey);
                }
            }

            using var singleJsonBody = JsonContent.Create(spikes[i], DnneJsonContext.Default.SpikeMessage);
            using var singleJsonDispatch = await targetClient.PostAsync("/api/v1/structure/spike", singleJsonBody, cancellationToken);
            await EnsureSuccessWithDetailsAsync(singleJsonDispatch, cancellationToken);
            delivered++;
        }

        return delivered;
    }

    private static bool ShouldPreferJsonBatch(Exception ex)
    {
        var message = ex.GetBaseException().Message ?? ex.Message ?? string.Empty;
        return message.Contains("Synchronous operations are disallowed", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Unsupported Media Type", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UnsupportedMediaType", StringComparison.OrdinalIgnoreCase)
               || message.Contains("415", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldDisableGrpcTransportImmediately(Exception ex)
    {
        foreach (var current in EnumerateExceptionChain(ex))
        {
            if (current is NotSupportedException)
            {
                return true;
            }

            var message = current.Message ?? string.Empty;
            if (message.Length == 0)
            {
                continue;
            }

            if (message.Contains("ResponseEnded", StringComparison.OrdinalIgnoreCase)
                || message.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase)
                || message.Contains("HTTP/2", StringComparison.OrdinalIgnoreCase)
                || message.Contains("HTTP2", StringComparison.OrdinalIgnoreCase)
                || message.Contains("content-type", StringComparison.OrdinalIgnoreCase)
                || message.Contains("grpc-status", StringComparison.OrdinalIgnoreCase)
                || message.Contains("gRPC call", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Bad gRPC response", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Exception> EnumerateExceptionChain(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static async Task EnsureSuccessWithDetailsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string details;
        try
        {
            details = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        }
        catch
        {
            details = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(details))
        {
            details = response.ReasonPhrase ?? "request failed";
        }

        if (details.Length > 320)
        {
            details = $"{details[..320]}...";
        }

        throw new HttpRequestException($"Response status code {(int)response.StatusCode} ({response.StatusCode}). {details}");
    }

    private static void AppendServiceHealthDiskLog(
        long tick,
        double nowMs,
        IReadOnlyList<ServiceInstance> serviceInstances,
        IReadOnlyDictionary<string, ServiceHealth> serviceHealth,
        int nonOkServiceCount,
        string reason)
    {
        try
        {
            var details = serviceInstances
                .Select(instance =>
                {
                    if (!serviceHealth.TryGetValue(instance.InstanceKey, out var health))
                    {
                        return new
                        {
                            Instance = instance,
                            Telemetry = (ServiceRuntimeTelemetry?)null,
                            Status = "MISSING_HEALTH"
                        };
                    }

                    var telemetry = health.CreateTelemetry(nowMs);
                    return new
                    {
                        Instance = instance,
                        Telemetry = (ServiceRuntimeTelemetry?)telemetry,
                        Status = string.IsNullOrWhiteSpace(telemetry.LastStatus) ? "UNKNOWN" : telemetry.LastStatus
                    };
                })
                .Where(item => !string.Equals(item.Status, "OK", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Telemetry?.ConsecutiveFailures ?? int.MaxValue)
                .ThenBy(item => item.Instance.StructureId.ToString(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Instance.InstanceKey, StringComparer.OrdinalIgnoreCase)
                .Take(32)
                .Select(item =>
                {
                    var telemetry = item.Telemetry;
                    if (telemetry is null)
                    {
                        return $"{item.Instance.InstanceKey} {item.Instance.StructureId} {item.Instance.HemisphereNormalized} status={item.Status} endpoint={item.Instance.Endpoint}";
                    }

                    var nextRetryInMs = Math.Max(0, telemetry.NextRetryTimestampMs - nowMs);
                    var error = string.IsNullOrWhiteSpace(telemetry.LastError)
                        ? "none"
                        : telemetry.LastError.Replace(Environment.NewLine, " ");
                    if (error.Length > 180)
                    {
                        error = $"{error[..180]}...";
                    }

                    return
                        $"{item.Instance.InstanceKey} {item.Instance.StructureId} {item.Instance.HemisphereNormalized} " +
                        $"status={item.Status} failures={telemetry.ConsecutiveFailures} attempts={telemetry.AttemptCount} " +
                        $"success={telemetry.SuccessCount} timeouts={telemetry.TimeoutFailureCount} " +
                        $"ackEwmaMs={telemetry.AckLatencyEwmaMs:0.0} nextRetryInMs={nextRetryInMs:0} " +
                        $"lastTick={telemetry.LastTickProcessed} endpoint={item.Instance.Endpoint} error={error}";
                })
                .ToArray();

            var detailText = details.Length == 0
                ? "non-OK instances: none in instance registry"
                : string.Join(Environment.NewLine, details);
            ControlHealthLog.Append(
                $"tick={tick} reason={reason} nonOk={nonOkServiceCount}/{serviceInstances.Count}{Environment.NewLine}{detailText}");
        }
        catch
        {
            // Diagnostics must stay best-effort only.
        }
    }

    private sealed record SpontaneousNoiseProfile(
        double EventsPerSecond,
        int MinBurstSpikes,
        int MaxBurstSpikes,
        float MinVesicleQuanta,
        float MaxVesicleQuanta,
        double BurstProbability);

    private sealed class ServiceHealth
    {
        private readonly object _gate = new();
        private int _failureCount;
        private int _attemptCount;
        private int _successCount;
        private int _timeoutFailureCount;
        private double _lastAckLatencyMs;
        private double _ackLatencyEwmaMs;
        private int _latencyLt100MsCount;
        private int _latency100To250MsCount;
        private int _latency250To500MsCount;
        private int _latency500To1000MsCount;
        private int _latencyGte1000MsCount;
        private string _lastStatus = "INIT";
        private string _lastError = string.Empty;
        private long _lastTickProcessed;
        private double _lastUpdateTimestampMs;
        private long _lastDegradedLogTick = long.MinValue;

        public double NextRetryTimestampMs { get; private set; }

        public bool CanAttempt(double nowMs)
        {
            lock (_gate)
            {
                return nowMs >= NextRetryTimestampMs;
            }
        }

        public void MarkSuccess(double nowMs, double ackLatencyMs, long tick)
        {
            lock (_gate)
            {
                _failureCount = 0;
                _attemptCount++;
                _successCount++;
                NextRetryTimestampMs = 0;
                _lastAckLatencyMs = ackLatencyMs;
                _ackLatencyEwmaMs = _ackLatencyEwmaMs <= 0.001
                    ? ackLatencyMs
                    : ((_ackLatencyEwmaMs * 0.78) + (ackLatencyMs * 0.22));
                if (ackLatencyMs < 100)
                {
                    _latencyLt100MsCount++;
                }
                else if (ackLatencyMs < 250)
                {
                    _latency100To250MsCount++;
                }
                else if (ackLatencyMs < 500)
                {
                    _latency250To500MsCount++;
                }
                else if (ackLatencyMs < 1000)
                {
                    _latency500To1000MsCount++;
                }
                else
                {
                    _latencyGte1000MsCount++;
                }
                _lastStatus = "OK";
                _lastError = string.Empty;
                _lastTickProcessed = tick;
                _lastUpdateTimestampMs = nowMs;
            }
        }

        public void MarkFailure(double nowMs, long tick, string error)
        {
            lock (_gate)
            {
                _failureCount = Math.Min(_failureCount + 1, 8);
                _attemptCount++;
                if (error.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    _timeoutFailureCount++;
                }
                var maxBackoffMs = _successCount == 0 && _failureCount >= ServiceTelemetryAggregation.AbsentFailureThreshold
                    ? 30_000
                    : 5_000;
                var backoffMs = Math.Min(maxBackoffMs, 100 * Math.Pow(2, _failureCount));
                NextRetryTimestampMs = nowMs + backoffMs;
                _lastStatus = "DEGRADED";
                _lastError = error;
                _lastTickProcessed = tick;
                _lastUpdateTimestampMs = nowMs;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _failureCount = 0;
                _attemptCount = 0;
                _successCount = 0;
                _timeoutFailureCount = 0;
                NextRetryTimestampMs = 0;
                _lastAckLatencyMs = 0;
                _ackLatencyEwmaMs = 0;
                _latencyLt100MsCount = 0;
                _latency100To250MsCount = 0;
                _latency250To500MsCount = 0;
                _latency500To1000MsCount = 0;
                _latencyGte1000MsCount = 0;
                _lastStatus = "INIT";
                _lastError = string.Empty;
                _lastTickProcessed = 0;
                _lastUpdateTimestampMs = 0;
                _lastDegradedLogTick = long.MinValue;
            }
        }

        public ServiceRuntimeTelemetry CreateTelemetry(double nowMs)
        {
            lock (_gate)
            {
                var status = nowMs >= NextRetryTimestampMs ? _lastStatus : "BACKOFF";
                return new ServiceRuntimeTelemetry(
                    _lastAckLatencyMs,
                    _ackLatencyEwmaMs,
                    _failureCount,
                    _attemptCount,
                    _successCount,
                    _timeoutFailureCount,
                    NextRetryTimestampMs,
                    status,
                    _lastError,
                    _lastTickProcessed,
                    _lastUpdateTimestampMs,
                    _latencyLt100MsCount,
                    _latency100To250MsCount,
                    _latency250To500MsCount,
                    _latency500To1000MsCount,
                    _latencyGte1000MsCount);
            }
        }

        public bool ShouldEmitDegradedLog(long tick, int everyTicks)
        {
            lock (_gate)
            {
                if (tick - _lastDegradedLogTick < everyTicks)
                {
                    return false;
                }

                _lastDegradedLogTick = tick;
                return true;
            }
        }
    }
}

internal sealed record ServiceRuntimeTelemetry(
    double LastAckLatencyMs,
    double AckLatencyEwmaMs,
    int ConsecutiveFailures,
    int AttemptCount,
    int SuccessCount,
    int TimeoutFailureCount,
    double NextRetryTimestampMs,
    string LastStatus,
    string LastError,
    long LastTickProcessed,
    double LastUpdateTimestampMs,
    int LatencyLt100MsCount,
    int Latency100To250MsCount,
    int Latency250To500MsCount,
    int Latency500To1000MsCount,
    int LatencyGte1000MsCount);

internal static class ServiceTelemetryAggregation
{
    internal const string AbsentStatus = "ABSENT";
    internal const int AbsentFailureThreshold = 8;

    internal static ServiceRuntimeTelemetry Aggregate(IReadOnlyList<ServiceRuntimeTelemetry> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);
        if (instances.Count == 0)
        {
            throw new ArgumentException("At least one service instance is required.", nameof(instances));
        }

        var healthy = instances
            .Where(telemetry => string.Equals(telemetry.LastStatus, "OK", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(telemetry => telemetry.LastUpdateTimestampMs)
            .ThenByDescending(telemetry => telemetry.SuccessCount)
            .FirstOrDefault();
        if (healthy is not null)
        {
            return healthy;
        }

        if (instances.All(IsNeverDiscovered))
        {
            var latest = instances.OrderByDescending(telemetry => telemetry.LastUpdateTimestampMs).First();
            return latest with
            {
                LastStatus = AbsentStatus,
                LastError = "No deployed instance was discovered after bounded startup probes."
            };
        }

        return instances
            .OrderByDescending(telemetry => telemetry.ConsecutiveFailures)
            .ThenByDescending(telemetry => !string.Equals(telemetry.LastStatus, "OK", StringComparison.OrdinalIgnoreCase))
            .First();
    }

    internal static bool IsAbsent(ServiceRuntimeTelemetry telemetry)
        => string.Equals(telemetry.LastStatus, AbsentStatus, StringComparison.OrdinalIgnoreCase);

    private static bool IsNeverDiscovered(ServiceRuntimeTelemetry telemetry)
        => telemetry.SuccessCount == 0 &&
           telemetry.AttemptCount >= AbsentFailureThreshold &&
           telemetry.ConsecutiveFailures >= AbsentFailureThreshold;
}

internal sealed record TransportRuntimeStats(
    long Tick,
    int ActiveServices,
    int SuccessfulAcks,
    int DrainCalls,
    int DrainedSpikes,
    int DispatchedSpikes,
    int DroppedByBudget,
    int TopQueries,
    int SpontaneousGenerated,
    int SpontaneousDelivered,
    int SpontaneousDispatchErrors,
    string? SpontaneousLastError,
    int ActivePathways,
    int DispatchQueueQueuedBatches,
    int DispatchQueueQueuedSpikes,
    int DispatchQueuePeakBatches,
    int DispatchQueuePeakSpikes,
    int DispatchQueueDroppedBatches,
    int DispatchQueueDroppedSpikes,
    int DispatchQueueFlushedBatches,
    int DispatchQueueFlushActiveTargets,
    int DispatchQueueFlushMaxTargetBurstSpikes,
    int DispatchQueueDispatchErrors,
    string? DispatchQueueLastError,
    int GeneratedSpikes,
    int RoutedSpikes,
    int DeliveredSpikes,
    int RouteDroppedNoConnectivity,
    int RouteDroppedNoTargets,
    int RouteDroppedTargetUnavailable,
    int RouteDroppedByBackpressure,
    double AdaptivePressure,
    double AdaptiveScale,
    int EffectiveMaxSpikeDispatchPerServicePerTick,
    int EffectiveMaxSpikeDispatchTotalPerTick,
    int EffectiveMaxTopQueriesPerTick,
    int EffectiveTickAckTimeoutMs,
    int EffectiveTickIoTimeoutMs,
    int EffectiveTickPublishWaitMs,
    int EffectiveTickPublishSettleMs,
    double AckLatencyEwmaMs,
    int AckLatencyLt100Ms,
    int AckLatency100To250Ms,
    int AckLatency250To500Ms,
    int AckLatency500To1000Ms,
    int AckLatencyGte1000Ms,
    double TickWallMs,
    double TickWallP50Ms,
    double TickWallP95Ms,
    double TickWallP99Ms,
    string DegradeSignal,
    int PerceptionLanguageGenerated,
    int PerceptionLanguageDelivered,
    int PerceptionLanguageDispatchErrors,
    string? PerceptionLanguageLastError,
    long LanguageBackoffAttempts,
    long LanguageBackoffResolved,
    long LanguageBackoffFallbackSelections,
    long LanguageBackoffDispatchErrors,
    IReadOnlyList<LanguageBackoffEdgeSnapshot> LanguageBackoffTopEdges,
    IReadOnlyList<LanguageBackoffGraphSnapshot> LanguageBackoffGraphs,
    IReadOnlyList<LanguageBackoffModeStateSnapshot> LanguageBackoffModeStates)
{
    public static TransportRuntimeStats Empty { get; } = new(
        Tick: 0,
        ActiveServices: 0,
        SuccessfulAcks: 0,
        DrainCalls: 0,
        DrainedSpikes: 0,
        DispatchedSpikes: 0,
        DroppedByBudget: 0,
        TopQueries: 0,
        SpontaneousGenerated: 0,
        SpontaneousDelivered: 0,
        SpontaneousDispatchErrors: 0,
        SpontaneousLastError: null,
        ActivePathways: 0,
        DispatchQueueQueuedBatches: 0,
        DispatchQueueQueuedSpikes: 0,
        DispatchQueuePeakBatches: 0,
        DispatchQueuePeakSpikes: 0,
        DispatchQueueDroppedBatches: 0,
        DispatchQueueDroppedSpikes: 0,
        DispatchQueueFlushedBatches: 0,
        DispatchQueueFlushActiveTargets: 0,
        DispatchQueueFlushMaxTargetBurstSpikes: 0,
        DispatchQueueDispatchErrors: 0,
        DispatchQueueLastError: null,
        GeneratedSpikes: 0,
        RoutedSpikes: 0,
        DeliveredSpikes: 0,
        RouteDroppedNoConnectivity: 0,
        RouteDroppedNoTargets: 0,
        RouteDroppedTargetUnavailable: 0,
        RouteDroppedByBackpressure: 0,
        AdaptivePressure: 0.0,
        AdaptiveScale: 1.0,
        EffectiveMaxSpikeDispatchPerServicePerTick: 0,
        EffectiveMaxSpikeDispatchTotalPerTick: 0,
        EffectiveMaxTopQueriesPerTick: 0,
        EffectiveTickAckTimeoutMs: 0,
        EffectiveTickIoTimeoutMs: 0,
        EffectiveTickPublishWaitMs: 0,
        EffectiveTickPublishSettleMs: 0,
        AckLatencyEwmaMs: 0.0,
        AckLatencyLt100Ms: 0,
        AckLatency100To250Ms: 0,
        AckLatency250To500Ms: 0,
        AckLatency500To1000Ms: 0,
        AckLatencyGte1000Ms: 0,
        TickWallMs: 0.0,
        TickWallP50Ms: 0.0,
        TickWallP95Ms: 0.0,
        TickWallP99Ms: 0.0,
        DegradeSignal: "none",
        PerceptionLanguageGenerated: 0,
        PerceptionLanguageDelivered: 0,
        PerceptionLanguageDispatchErrors: 0,
        PerceptionLanguageLastError: null,
        LanguageBackoffAttempts: 0,
        LanguageBackoffResolved: 0,
        LanguageBackoffFallbackSelections: 0,
        LanguageBackoffDispatchErrors: 0,
        LanguageBackoffTopEdges: Array.Empty<LanguageBackoffEdgeSnapshot>(),
        LanguageBackoffGraphs: Array.Empty<LanguageBackoffGraphSnapshot>(),
        LanguageBackoffModeStates: Array.Empty<LanguageBackoffModeStateSnapshot>());
}

internal sealed record PerceptionLanguageConditioningStats(int Generated, int Delivered, int DispatchErrors, string? LastError)
{
    public static PerceptionLanguageConditioningStats Empty { get; } = new(0, 0, 0, null);
}

internal sealed record SpontaneousInjectionStats(int Generated, int Delivered, int DispatchErrors, string? LastError)
{
    public static SpontaneousInjectionStats Empty { get; } = new(0, 0, 0, null);
}

internal sealed record MetabolicTickInput(
    int DrainedSpikes,
    int GeneratedSpikes,
    int ActivePathways,
    int SpontaneousGenerated,
    float HomeostasisRateScale = 1.0f);

internal sealed record MetabolicTransitionResult(
    bool NeuronalSleepObserved,
    bool EnteredSleep,
    bool ExitedSleep,
    float AtpBudget,
    int SleepTicks);

internal sealed record BodyStateRuntime(
    float ForwardVelocity,
    float TurnRateDeg,
    float ContactLevel,
    float TactileFront,
    float TactileLeft,
    float TactileRight,
    float TactileGround,
    float PainLevel,
    float Hunger,
    float Health,
    float LeftMotorDrive,
    float RightMotorDrive,
    float MotorAsymmetry,
    long LastInputTick)
{
    public static BodyStateRuntime Default { get; } = new(
        ForwardVelocity: 0f,
        TurnRateDeg: 0f,
        ContactLevel: 0f,
        TactileFront: 0f,
        TactileLeft: 0f,
        TactileRight: 0f,
        TactileGround: 0f,
        PainLevel: 0f,
        Hunger: 0f,
        Health: 1f,
        LeftMotorDrive: 0f,
        RightMotorDrive: 0f,
        MotorAsymmetry: 0f,
        LastInputTick: long.MinValue);
}



internal sealed record DispatchedSpikeTrace(
    long Tick,
    double TimestampMs,
    long WallClockUnixMs,
    StructureId SourceStructure,
    string SourceHemisphere,
    string SourceNeuronId,
    StructureId TargetStructure,
    string TargetHemisphere,
    string TargetNeuronId,
    NTEnum Neurotransmitter,
    string TargetInstanceKey);


internal sealed record EmbodiedAttentionSpotlightRuntime(
    bool Active,
    string FocusKey,
    string FocusLabel,
    string FocusCategory,
    string DominantNeed,
    string BodyRegion,
    string TargetObjectId,
    string TargetHemisphere,
    string SelectedChannel,
    string GoalKey,
    float Salience,
    float BodyBinding,
    float NeedBinding,
    float ObjectBinding,
    float MotorReadiness,
    float MemoryBinding,
    float CircuitSupport,
    float ObjectCircuitEvidence,
    float BodyCircuitEvidence,
    float NeedCircuitEvidence,
    float MotorCircuitEvidence,
    float Confidence,
    long LastUpdatedTick,
    long Sequence,
    string Evidence)
{
    public static EmbodiedAttentionSpotlightRuntime Default { get; } = new(
        Active: false,
        FocusKey: "environment",
        FocusLabel: "environment",
        FocusCategory: "observation",
        DominantNeed: "observation",
        BodyRegion: "whole body",
        TargetObjectId: "none",
        TargetHemisphere: "M",
        SelectedChannel: "visual",
        GoalKey: "Observe",
        Salience: 0f,
        BodyBinding: 0f,
        NeedBinding: 0f,
        ObjectBinding: 0f,
        MotorReadiness: 0f,
        MemoryBinding: 0f,
        CircuitSupport: 0f,
        ObjectCircuitEvidence: 0f,
        BodyCircuitEvidence: 0f,
        NeedCircuitEvidence: 0f,
        MotorCircuitEvidence: 0f,
        Confidence: 0f,
        LastUpdatedTick: 0,
        Sequence: 0,
        Evidence: "quiet embodied monitoring");

    public static EmbodiedAttentionSpotlightRuntime Normalize(EmbodiedAttentionSpotlightRuntime? value)
    {
        if (value is null)
        {
            return Default;
        }

        return value with
        {
            FocusKey = NormalizeSpotlightText(value.FocusKey, Default.FocusKey),
            FocusLabel = NormalizeSpotlightText(value.FocusLabel, Default.FocusLabel),
            FocusCategory = NormalizeSpotlightText(value.FocusCategory, Default.FocusCategory),
            DominantNeed = NormalizeSpotlightText(value.DominantNeed, Default.DominantNeed),
            BodyRegion = NormalizeSpotlightText(value.BodyRegion, Default.BodyRegion),
            TargetObjectId = NormalizeSpotlightText(value.TargetObjectId, "none"),
            TargetHemisphere = NormalizeSpotlightHemisphere(value.TargetHemisphere),
            SelectedChannel = NormalizeSpotlightText(value.SelectedChannel, Default.SelectedChannel),
            GoalKey = NormalizeSpotlightText(value.GoalKey, Default.GoalKey),
            Salience = Math.Clamp(value.Salience, 0f, 1f),
            BodyBinding = Math.Clamp(value.BodyBinding, 0f, 1f),
            NeedBinding = Math.Clamp(value.NeedBinding, 0f, 1f),
            ObjectBinding = Math.Clamp(value.ObjectBinding, 0f, 1f),
            MotorReadiness = Math.Clamp(value.MotorReadiness, 0f, 1f),
            MemoryBinding = Math.Clamp(value.MemoryBinding, 0f, 1f),
            CircuitSupport = Math.Clamp(value.CircuitSupport, 0f, 1f),
            ObjectCircuitEvidence = Math.Clamp(value.ObjectCircuitEvidence, 0f, 1f),
            BodyCircuitEvidence = Math.Clamp(value.BodyCircuitEvidence, 0f, 1f),
            NeedCircuitEvidence = Math.Clamp(value.NeedCircuitEvidence, 0f, 1f),
            MotorCircuitEvidence = Math.Clamp(value.MotorCircuitEvidence, 0f, 1f),
            Confidence = Math.Clamp(value.Confidence, 0f, 1f),
            LastUpdatedTick = Math.Max(0, value.LastUpdatedTick),
            Sequence = Math.Max(0, value.Sequence),
            Evidence = NormalizeSpotlightText(value.Evidence, Default.Evidence)
        };
    }

    private static string NormalizeSpotlightText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeSpotlightHemisphere(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "M" : value.Trim().ToUpperInvariant();
        return normalized is "L" or "R" or "M" ? normalized : "M";
    }
}


internal sealed class LanguageBackoffPolicy
{
    private const int GraphEvaluationIntervalTicks = 160;
    private const int GraphMinHoldTicks = 320;
    private const int GraphDormantCutoffTicks = 1200;
    private const double GraphSwitchHysteresis = 0.12;

    private static readonly IReadOnlyDictionary<StructureId, StructureId[]> FallbackTargetsByTarget = new Dictionary<StructureId, StructureId[]>
    {
        [StructureId.A1] = [StructureId.TemporalAssociation, StructureId.WernickePstgPsts],
        [StructureId.WernickePstgPsts] = [StructureId.TemporalAssociation, StructureId.SupramarginalAngular],
        [StructureId.ArcuateFasciculus] = [StructureId.BrocaBa44Ba45, StructureId.SupramarginalAngular],
        [StructureId.SupramarginalAngular] = [StructureId.TemporalAssociation, StructureId.Ppc],
        [StructureId.BrocaBa44Ba45] = [StructureId.PremotorCortex, StructureId.Pfc],
        [StructureId.Sma] = [StructureId.PremotorCortex, StructureId.M1],
        [StructureId.M1] = [StructureId.PremotorCortex, StructureId.Sma],
        [StructureId.TemporalAssociation] = [StructureId.Pfc, StructureId.WernickePstgPsts],
        [StructureId.Pfc] = [StructureId.OrbitofrontalCortex, StructureId.Acc],
        [StructureId.Thalamus] = [StructureId.MotorThalamus, StructureId.Pulvinar]
    };

    private static readonly IReadOnlyDictionary<StructureId, string> DefaultTargetPrefixes = new Dictionary<StructureId, string>
    {
        [StructureId.A1] = "a1_tonotopic",
        [StructureId.WernickePstgPsts] = "wernicke_lexical",
        [StructureId.SupramarginalAngular] = "smg_phonological",
        [StructureId.ArcuateFasciculus] = "arcuate_dorsal",
        [StructureId.BrocaBa44Ba45] = "broca_sequence",
        [StructureId.TemporalAssociation] = "temporal_semantic",
        [StructureId.Pfc] = "pfc_language_context",
        [StructureId.PremotorCortex] = "premotor_plan",
        [StructureId.Sma] = "sma_speech_sequence",
        [StructureId.M1] = "m1_articulation",
        [StructureId.OrbitofrontalCortex] = "ofc_context",
        [StructureId.Acc] = "acc_monitor"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<LanguageConditioningGraph>> GraphRegistryByMode = BuildGraphRegistry();

    private readonly object _gate = new();
    private readonly Dictionary<string, LanguageBackoffEdgeAccumulator> _edgeStats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LanguageBackoffGraphAccumulator> _graphStats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LanguageBackoffModeState> _modeStates = new(StringComparer.OrdinalIgnoreCase);
    private long _totalAttempts;
    private long _totalResolved;
    private long _totalFallbackSelections;
    private long _totalDispatchErrors;
    private long _epoch;

    public LanguageBackoffResolution Resolve(
        string mode,
        LanguageStimulusTarget primary,
        RuntimeInstanceCatalog catalog,
        string? hemisphereHint,
        long tick)
    {
        var normalizedMode = NormalizeMode(mode);
        var preferredHemisphere = hemisphereHint ?? primary.PreferredHemisphere;
        List<CandidateRoute> candidates;
        LanguageBackoffModeState modeState;
        LanguageConditioningGraph graph;
        lock (_gate)
        {
            modeState = GetOrCreateModeState(normalizedMode, tick);
            MaybeRebalanceMode(modeState, tick);
            modeState.LastResolutionTick = tick;
            graph = GetGraphDefinition(modeState.Mode, modeState.CurrentGraphId);
            candidates = BuildCandidates(modeState.Mode, primary, graph).ToList();
        }

        if (candidates.Count == 0)
        {
            var synthetic = new LanguageBackoffEdgeHandle(
                Key: $"{normalizedMode}:{primary.SourceStructure}->{primary.TargetStructure}:r0:primary:{modeState.CurrentGraphId}",
                Mode: normalizedMode,
                GraphId: modeState.CurrentGraphId,
                Source: primary.SourceStructure,
                Target: primary.TargetStructure,
                IsFallback: false,
                Rank: 0,
                Strategy: "primary");
            return new LanguageBackoffResolution(false, null, Array.Empty<ServiceInstance>(), synthetic, "no backoff candidates generated");
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i].Edge;
            var instances = catalog.GetByStructure(candidate.Target, preferredHemisphere).ToList();
            if (instances.Count == 0 && preferredHemisphere is not null)
            {
                instances = catalog.GetByStructure(candidate.Target, null).ToList();
            }

            RecordAttempt(candidate, instances.Count > 0, tick);
            if (instances.Count == 0)
            {
                continue;
            }

            var gainScale = candidates[i].GainScale;
            var resolvedTarget = new LanguageStimulusTarget(
                primary.SourceStructure,
                candidate.Target,
                ResolveTargetPrefix(candidate.Target, primary.TargetNeuronPrefix),
                preferredHemisphere,
                primary.Gain * gainScale);
            return new LanguageBackoffResolution(
                true,
                resolvedTarget,
                instances,
                candidate,
                null);
        }

        var inspected = string.Join(", ", candidates.Select(c => $"{c.Edge.Target}[r{c.Edge.Rank}]"));
        return new LanguageBackoffResolution(
            false,
            null,
            Array.Empty<ServiceInstance>(),
            candidates[0].Edge,
            $"no active instances for candidates: {inspected}");
    }

    public void RecordDispatchResult(LanguageBackoffEdgeHandle edge, int deliveredSpikes, Exception? error, long tick)
    {
        lock (_gate)
        {
            var accumulator = GetOrCreate(edge);
            accumulator.LastEpoch = ++_epoch;
            if (deliveredSpikes > 0)
            {
                accumulator.DispatchSuccessCount++;
                accumulator.DeliveredSpikes += deliveredSpikes;
            }

            var graphAccumulator = GetOrCreateGraphAccumulator(edge.Mode, edge.GraphId);
            if (deliveredSpikes > 0)
            {
                graphAccumulator.DispatchSuccessCount++;
                graphAccumulator.DeliveredSpikes += deliveredSpikes;
            }

            if (error is not null)
            {
                accumulator.DispatchErrorCount++;
                accumulator.LastError = $"{error.GetType().Name}: {error.Message}";
                graphAccumulator.DispatchErrorCount++;
                graphAccumulator.LastError = accumulator.LastError;
                _totalDispatchErrors++;
            }

            if (deliveredSpikes <= 0 && error is null)
            {
                graphAccumulator.DeadPathCount++;
            }

            var reward = ComputeDispatchReward(edge, deliveredSpikes, error);
            graphAccumulator.ApplyReward(reward, tick);
        }
    }

    public void ObserveTick(long tick, int activePathways, int dispatchedSpikes, int dispatchErrors)
    {
        lock (_gate)
        {
            foreach (var modeState in _modeStates.Values)
            {
                if (tick - modeState.LastResolutionTick > GraphDormantCutoffTicks)
                {
                    continue;
                }

                var graphAccumulator = GetOrCreateGraphAccumulator(modeState.Mode, modeState.CurrentGraphId);
                var sharedReward = Math.Clamp(
                    (activePathways * 0.015) +
                    (dispatchedSpikes * 0.0015) -
                    (dispatchErrors * 0.15),
                    -1.5,
                    1.5);
                graphAccumulator.ApplyReward(sharedReward * 0.25, tick);
                MaybeRebalanceMode(modeState, tick);
            }
        }
    }

    public LanguageBackoffSnapshot GetSnapshot(int maxEdges)
    {
        var limit = Math.Clamp(maxEdges, 1, 256);
        lock (_gate)
        {
            var topEdges = _edgeStats.Values
                .OrderByDescending(e => e.DeliveredSpikes)
                .ThenByDescending(e => e.DispatchSuccessCount)
                .ThenByDescending(e => e.ResolvedCount)
                .ThenBy(e => e.Handle.Key, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(e => new LanguageBackoffEdgeSnapshot(
                    e.Handle.Key,
                    e.Handle.Mode,
                    e.Handle.GraphId,
                    e.Handle.Source.ToString(),
                    e.Handle.Target.ToString(),
                    e.Handle.IsFallback,
                    e.Handle.Rank,
                    e.Handle.Strategy,
                    e.AttemptCount,
                    e.ResolvedCount,
                    e.UnavailableCount,
                    e.DispatchSuccessCount,
                    e.DispatchErrorCount,
                    e.DeliveredSpikes,
                    e.LastError))
                .ToArray();

            var modeStateByMode = _modeStates.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            var graphSnapshots = _graphStats.Values
                .OrderByDescending(g => g.CompositeScore)
                .ThenByDescending(g => g.DeliveredSpikes)
                .ThenBy(g => g.GraphId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new LanguageBackoffGraphSnapshot(
                    g.GraphId,
                    g.Mode,
                    g.Description,
                    modeStateByMode.TryGetValue(g.Mode, out var modeState) &&
                    string.Equals(modeState.CurrentGraphId, g.GraphId, StringComparison.OrdinalIgnoreCase),
                    g.AttemptCount,
                    g.ResolvedCount,
                    g.DispatchSuccessCount,
                    g.DispatchErrorCount,
                    g.DeadPathCount,
                    g.DeliveredSpikes,
                    g.ScoreEwma,
                    g.CompositeScore,
                    g.LastTick,
                    g.LastError))
                .ToArray();
            var modeStates = _modeStates.Values
                .OrderBy(m => m.Mode, StringComparer.OrdinalIgnoreCase)
                .Select(m => new LanguageBackoffModeStateSnapshot(
                    m.Mode,
                    m.CurrentGraphId,
                    m.LastSwitchTick,
                    m.LastEvaluationTick,
                    m.LastResolutionTick))
                .ToArray();

            return new LanguageBackoffSnapshot(
                _totalAttempts,
                _totalResolved,
                _totalFallbackSelections,
                _totalDispatchErrors,
                topEdges,
                graphSnapshots,
                modeStates);
        }
    }

    private IEnumerable<CandidateRoute> BuildCandidates(string mode, LanguageStimulusTarget primary, LanguageConditioningGraph graph)
    {
        var rank = 0;
        var primaryEdge = new LanguageBackoffEdgeHandle(
            Key: $"{mode}:{primary.SourceStructure}->{primary.TargetStructure}:r{rank}:primary:{graph.GraphId}",
            Mode: mode,
            GraphId: graph.GraphId,
            Source: primary.SourceStructure,
            Target: primary.TargetStructure,
            IsFallback: false,
            Rank: rank,
            Strategy: graph.Strategy);
        yield return new CandidateRoute(primaryEdge, graph.PrimaryGain);

        var fallbacks = ResolveFallbackTargets(primary.TargetStructure, graph);
        if (fallbacks.Length == 0)
        {
            yield break;
        }

        for (var i = 0; i < fallbacks.Length; i++)
        {
            var fallback = fallbacks[i];
            if (fallback == primary.TargetStructure)
            {
                continue;
            }

            rank++;
            var key = $"{mode}:{primary.SourceStructure}->{fallback}:r{rank}:fallback:{graph.GraphId}";
            var edge = new LanguageBackoffEdgeHandle(
                Key: key,
                Mode: mode,
                GraphId: graph.GraphId,
                Source: primary.SourceStructure,
                Target: fallback,
                IsFallback: true,
                Rank: rank,
                Strategy: graph.Strategy);
            var gainScale = MathF.Max(0.50f, graph.FallbackBaseGain - (graph.FallbackRankDecay * rank));
            yield return new CandidateRoute(edge, gainScale);
        }
    }

    private static StructureId[] ResolveFallbackTargets(StructureId target, LanguageConditioningGraph graph)
    {
        if (graph.FallbackOverrides.TryGetValue(target, out var overrides) && overrides.Length > 0)
        {
            return overrides;
        }

        return FallbackTargetsByTarget.TryGetValue(target, out var defaults) ? defaults : Array.Empty<StructureId>();
    }

    private static string ResolveTargetPrefix(StructureId target, string fallbackPrefix)
        => DefaultTargetPrefixes.TryGetValue(target, out var prefix) ? prefix : fallbackPrefix;

    private void RecordAttempt(LanguageBackoffEdgeHandle edge, bool resolved, long tick)
    {
        lock (_gate)
        {
            var accumulator = GetOrCreate(edge);
            accumulator.LastEpoch = ++_epoch;
            accumulator.AttemptCount++;
            _totalAttempts++;
            var graphAccumulator = GetOrCreateGraphAccumulator(edge.Mode, edge.GraphId);
            graphAccumulator.AttemptCount++;
            graphAccumulator.LastTick = tick;
            if (resolved)
            {
                accumulator.ResolvedCount++;
                _totalResolved++;
                graphAccumulator.ResolvedCount++;
                graphAccumulator.SelectionCount++;
                if (edge.IsFallback)
                {
                    _totalFallbackSelections++;
                }
            }
            else
            {
                accumulator.UnavailableCount++;
                graphAccumulator.DeadPathCount++;
            }
        }
    }

    private void MaybeRebalanceMode(LanguageBackoffModeState modeState, long tick)
    {
        if (tick - modeState.LastEvaluationTick < GraphEvaluationIntervalTicks)
        {
            return;
        }

        modeState.LastEvaluationTick = tick;
        var graphs = GetGraphDefinitions(modeState.Mode);
        if (graphs.Count <= 1)
        {
            return;
        }

        var currentGraph = GetOrCreateGraphAccumulator(modeState.Mode, modeState.CurrentGraphId);
        var currentScore = currentGraph.CompositeScore + 0.04; // small stickiness

        LanguageBackoffGraphAccumulator? bestGraph = null;
        for (var i = 0; i < graphs.Count; i++)
        {
            var candidate = GetOrCreateGraphAccumulator(modeState.Mode, graphs[i].GraphId);
            if (bestGraph is null || candidate.CompositeScore > bestGraph.CompositeScore)
            {
                bestGraph = candidate;
            }
        }

        if (bestGraph is null || string.Equals(bestGraph.GraphId, modeState.CurrentGraphId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (tick - modeState.LastSwitchTick < GraphMinHoldTicks)
        {
            return;
        }

        if (bestGraph.CompositeScore <= currentScore + GraphSwitchHysteresis)
        {
            return;
        }

        modeState.CurrentGraphId = bestGraph.GraphId;
        modeState.LastSwitchTick = tick;
    }

    private LanguageBackoffModeState GetOrCreateModeState(string mode, long tick)
    {
        if (_modeStates.TryGetValue(mode, out var existing))
        {
            return existing;
        }

        var graphs = GetGraphDefinitions(mode);
        var initialGraphId = graphs.Count == 0 ? "default" : graphs[0].GraphId;
        var created = new LanguageBackoffModeState(mode, initialGraphId, tick, tick, tick);
        _modeStates[mode] = created;
        return created;
    }

    private static string NormalizeMode(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "repetition";
        }

        var normalized = mode.Trim().ToLowerInvariant();
        return GraphRegistryByMode.ContainsKey(normalized) ? normalized : "repetition";
    }

    private static IReadOnlyList<LanguageConditioningGraph> GetGraphDefinitions(string mode)
        => GraphRegistryByMode.TryGetValue(mode, out var graphs)
            ? graphs
            : GraphRegistryByMode["repetition"];

    private static LanguageConditioningGraph GetGraphDefinition(string mode, string graphId)
    {
        var graphs = GetGraphDefinitions(mode);
        for (var i = 0; i < graphs.Count; i++)
        {
            if (string.Equals(graphs[i].GraphId, graphId, StringComparison.OrdinalIgnoreCase))
            {
                return graphs[i];
            }
        }

        return graphs[0];
    }

    private LanguageBackoffGraphAccumulator GetOrCreateGraphAccumulator(string mode, string graphId)
    {
        var key = $"{mode}:{graphId}";
        if (_graphStats.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var graph = GetGraphDefinition(mode, graphId);
        var created = new LanguageBackoffGraphAccumulator(graph);
        _graphStats[key] = created;
        return created;
    }

    private static double ComputeDispatchReward(LanguageBackoffEdgeHandle edge, int deliveredSpikes, Exception? error)
    {
        var deliveredReward = Math.Min(3.0, deliveredSpikes / 24.0);
        var fallbackPenalty = edge.IsFallback ? (0.04 * Math.Max(1, edge.Rank)) : 0.0;
        var errorPenalty = error is null ? 0.0 : 1.2;
        return Math.Clamp(deliveredReward - fallbackPenalty - errorPenalty, -2.0, 3.0);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<LanguageConditioningGraph>> BuildGraphRegistry()
    {
        static Dictionary<StructureId, StructureId[]> D(params (StructureId Target, StructureId[] Fallback)[] entries)
            => entries.ToDictionary(x => x.Target, x => x.Fallback);

        return new Dictionary<string, IReadOnlyList<LanguageConditioningGraph>>(StringComparer.OrdinalIgnoreCase)
        {
            ["repetition"] =
            [
                new("rep_stable", "repetition", "Stable direct-plus-local fallback", "stable", 1.00f, 0.92f, 0.08f, new Dictionary<StructureId, StructureId[]>()),
                new("rep_semantic_bias", "repetition", "Semantic fallback preference", "semantic_bias", 0.98f, 0.88f, 0.07f, D(
                    (StructureId.WernickePstgPsts, [StructureId.TemporalAssociation, StructureId.SupramarginalAngular, StructureId.Pfc]),
                    (StructureId.TemporalAssociation, [StructureId.Pfc, StructureId.WernickePstgPsts]),
                    (StructureId.A1, [StructureId.WernickePstgPsts, StructureId.TemporalAssociation]))),
                new("rep_motor_bias", "repetition", "Motor-articulation fallback preference", "motor_bias", 0.97f, 0.90f, 0.06f, D(
                    (StructureId.BrocaBa44Ba45, [StructureId.PremotorCortex, StructureId.Pfc]),
                    (StructureId.Sma, [StructureId.PremotorCortex, StructureId.M1]),
                    (StructureId.M1, [StructureId.Sma, StructureId.PremotorCortex])))
            ],
            ["english"] =
            [
                new("eng_lexical_semantic", "english", "English lexical-to-semantic route", "english_semantic", 1.00f, 0.92f, 0.06f, D(
                    (StructureId.WernickePstgPsts, [StructureId.TemporalAssociation, StructureId.SupramarginalAngular, StructureId.Pfc]),
                    (StructureId.TemporalAssociation, [StructureId.Pfc, StructureId.WernickePstgPsts, StructureId.OrbitofrontalCortex]),
                    (StructureId.Pfc, [StructureId.TemporalAssociation, StructureId.Acc]))),
                new("eng_dorsal_rehearsal", "english", "English dorsal rehearsal route", "english_dorsal", 0.98f, 0.90f, 0.06f, D(
                    (StructureId.ArcuateFasciculus, [StructureId.WernickePstgPsts, StructureId.BrocaBa44Ba45]),
                    (StructureId.BrocaBa44Ba45, [StructureId.PremotorCortex, StructureId.Pfc]),
                    (StructureId.Sma, [StructureId.PremotorCortex, StructureId.M1]))),
                new("eng_auditory_resilient", "english", "English auditory fallback route", "english_resilient", 0.97f, 0.88f, 0.07f, D(
                    (StructureId.A1, [StructureId.WernickePstgPsts, StructureId.TemporalAssociation]),
                    (StructureId.Thalamus, [StructureId.Pulvinar, StructureId.A1]),
                    (StructureId.SupramarginalAngular, [StructureId.TemporalAssociation, StructureId.Ppc])))
            ],
            ["comprehension"] =
            [
                new("comp_dorsal", "comprehension", "Dorsal lexical chain", "dorsal", 1.00f, 0.92f, 0.08f, new Dictionary<StructureId, StructureId[]>()),
                new("comp_semantic_heavy", "comprehension", "Semantic contextual integration", "semantic_heavy", 0.99f, 0.90f, 0.06f, D(
                    (StructureId.WernickePstgPsts, [StructureId.TemporalAssociation, StructureId.SupramarginalAngular]),
                    (StructureId.SupramarginalAngular, [StructureId.TemporalAssociation, StructureId.Ppc]),
                    (StructureId.Pfc, [StructureId.OrbitofrontalCortex, StructureId.Acc]))),
                new("comp_thalamic_resilient", "comprehension", "Thalamic-resilient route", "thalamic_resilient", 0.97f, 0.88f, 0.07f, D(
                    (StructureId.A1, [StructureId.TemporalAssociation, StructureId.WernickePstgPsts]),
                    (StructureId.Thalamus, [StructureId.Pulvinar, StructureId.MotorThalamus])))
            ],
            ["production"] =
            [
                new("prod_motor_chain", "production", "Broca-SMA-M1 motor chain", "motor_chain", 1.00f, 0.94f, 0.06f, new Dictionary<StructureId, StructureId[]>()),
                new("prod_planning_bias", "production", "Planning-heavy production", "planning_bias", 0.99f, 0.92f, 0.05f, D(
                    (StructureId.BrocaBa44Ba45, [StructureId.PremotorCortex, StructureId.Pfc]),
                    (StructureId.Sma, [StructureId.PremotorCortex, StructureId.M1]))),
                new("prod_resilient", "production", "Resilient articulation fallback", "resilient", 0.96f, 0.90f, 0.06f, D(
                    (StructureId.M1, [StructureId.Sma, StructureId.PremotorCortex, StructureId.Acc])))
            ],
            ["prosody"] =
            [
                new("prosody_affective", "prosody", "Right-lateralized affective prosody route", "affective", 1.00f, 0.93f, 0.07f, D(
                    (StructureId.TemporalAssociation, [StructureId.Insula, StructureId.SupramarginalAngular, StructureId.Pfc]),
                    (StructureId.Insula, [StructureId.OrbitofrontalCortex, StructureId.Pfc]),
                    (StructureId.OrbitofrontalCortex, [StructureId.Pfc, StructureId.Acc]))),
                new("prosody_rhythmic_motor", "prosody", "Rhythm-to-motor prosody coupling", "rhythmic_motor", 0.99f, 0.92f, 0.06f, D(
                    (StructureId.SupramarginalAngular, [StructureId.TemporalAssociation, StructureId.Ppc]),
                    (StructureId.Pfc, [StructureId.Sma, StructureId.PremotorCortex]),
                    (StructureId.Sma, [StructureId.PremotorCortex, StructureId.M1]))),
                new("prosody_resilient", "prosody", "Resilient auditory prosody fallback", "resilient", 0.97f, 0.90f, 0.07f, D(
                    (StructureId.A1, [StructureId.TemporalAssociation, StructureId.WernickePstgPsts]),
                    (StructureId.Thalamus, [StructureId.Pulvinar, StructureId.MotorThalamus]),
                    (StructureId.TemporalAssociation, [StructureId.SupramarginalAngular, StructureId.Pfc])))
            ],
            ["emergent"] =
            [
                new("emergent_balanced", "emergent", "Balanced semantic-motor emergence", "balanced", 1.00f, 0.93f, 0.06f, new Dictionary<StructureId, StructureId[]>()),
                new("emergent_semantic", "emergent", "Semantic exploratory emergence", "semantic_explore", 0.99f, 0.90f, 0.07f, D(
                    (StructureId.WernickePstgPsts, [StructureId.TemporalAssociation, StructureId.SupramarginalAngular, StructureId.Pfc]),
                    (StructureId.TemporalAssociation, [StructureId.Pfc, StructureId.WernickePstgPsts]),
                    (StructureId.Pfc, [StructureId.OrbitofrontalCortex, StructureId.Acc]))),
                new("emergent_motor", "emergent", "Motor expressive emergence", "motor_express", 0.98f, 0.92f, 0.06f, D(
                    (StructureId.BrocaBa44Ba45, [StructureId.PremotorCortex, StructureId.Pfc]),
                    (StructureId.Sma, [StructureId.PremotorCortex, StructureId.M1]),
                    (StructureId.M1, [StructureId.Sma, StructureId.PremotorCortex])))
            ]
        };
    }

    private LanguageBackoffEdgeAccumulator GetOrCreate(LanguageBackoffEdgeHandle edge)
    {
        if (_edgeStats.TryGetValue(edge.Key, out var existing))
        {
            return existing;
        }

        var created = new LanguageBackoffEdgeAccumulator(edge);
        _edgeStats[edge.Key] = created;
        return created;
    }

    private sealed class LanguageBackoffEdgeAccumulator(LanguageBackoffEdgeHandle handle)
    {
        public LanguageBackoffEdgeHandle Handle { get; } = handle;
        public long AttemptCount;
        public long ResolvedCount;
        public long UnavailableCount;
        public long DispatchSuccessCount;
        public long DispatchErrorCount;
        public long DeliveredSpikes;
        public string? LastError;
        public long LastEpoch;
    }

    private sealed class LanguageBackoffGraphAccumulator(LanguageConditioningGraph graph)
    {
        public string GraphId { get; } = graph.GraphId;
        public string Mode { get; } = graph.Mode;
        public string Description { get; } = graph.Description;
        public long AttemptCount;
        public long ResolvedCount;
        public long DispatchSuccessCount;
        public long DispatchErrorCount;
        public long DeadPathCount;
        public long DeliveredSpikes;
        public long SelectionCount;
        public double ScoreEwma;
        public double LastReward;
        public long LastTick;
        public string? LastError;

        public double CompositeScore
        {
            get
            {
                var resolveRate = AttemptCount > 0 ? ResolvedCount / (double)AttemptCount : 0.0;
                var dispatchTotal = DispatchSuccessCount + DispatchErrorCount;
                var dispatchErrorRate = dispatchTotal > 0 ? DispatchErrorCount / (double)dispatchTotal : 0.0;
                var deadPathRate = AttemptCount > 0 ? DeadPathCount / (double)AttemptCount : 0.0;
                var deliveredNorm = Math.Log10(1.0 + DeliveredSpikes);
                return (ScoreEwma * 1.35) +
                       (resolveRate * 0.90) +
                       (deliveredNorm * 0.30) -
                       (dispatchErrorRate * 1.40) -
                       (deadPathRate * 0.80);
            }
        }

        public void ApplyReward(double reward, long tick)
        {
            ScoreEwma = ScoreEwma == 0.0
                ? reward
                : ((ScoreEwma * 0.92) + (reward * 0.08));
            LastReward = reward;
            LastTick = tick;
        }
    }

    private sealed class LanguageBackoffModeState(string mode, string currentGraphId, long lastSwitchTick, long lastEvaluationTick, long lastResolutionTick)
    {
        public string Mode { get; } = mode;
        public string CurrentGraphId { get; set; } = currentGraphId;
        public long LastSwitchTick { get; set; } = lastSwitchTick;
        public long LastEvaluationTick { get; set; } = lastEvaluationTick;
        public long LastResolutionTick { get; set; } = lastResolutionTick;
    }

    private sealed record CandidateRoute(LanguageBackoffEdgeHandle Edge, float GainScale);
    private sealed record LanguageConditioningGraph(
        string GraphId,
        string Mode,
        string Description,
        string Strategy,
        float PrimaryGain,
        float FallbackBaseGain,
        float FallbackRankDecay,
        IReadOnlyDictionary<StructureId, StructureId[]> FallbackOverrides);
}

internal sealed class PhoneticLanguageEngine
{
    private static readonly string[] Onsets =
    [
        "b", "d", "g", "k", "t", "p", "m", "n", "l", "r", "s", "z", "sh", "ch", "f", "v", "th", "dh", "h", "y", "w", "kr", "gr", "tr", "dr", "pl", "kl"
    ];

    private static readonly string[] Nuclei =
    [
        "a", "e", "i", "o", "u", "ae", "ai", "au", "ei", "ia", "io", "oa", "ou", "uu", "oi", "ea"
    ];

    private static readonly string[] Codas =
    [
        "", "", "", "n", "m", "s", "r", "l", "k", "t", "d", "ng", "sh", "th", "x"
    ];

    private static readonly string[] ComplexOnsets =
    [
        "kr", "gr", "tr", "dr", "pl", "kl"
    ];

    private static readonly string[] SimpleOnsets = Onsets
        .Where(x => !ComplexOnsets.Contains(x, StringComparer.OrdinalIgnoreCase))
        .ToArray();

    private static readonly Dictionary<string, string[]> TemplatesByMode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["repetition"] = ["CV", "CVC", "CV", "CVC", "VC"],
        ["comprehension"] = ["CV", "CVC", "CV", "VC"],
        ["production"] = ["CVC", "CCVC", "CVC", "CCV"],
        ["emergent"] = ["CV", "CVC", "CCV", "CCVC", "VC"]
    };

    private static readonly HashSet<string> DisallowedOnsetNucleus = new(StringComparer.OrdinalIgnoreCase)
    {
        "h|uu",
        "w|i",
        "y|uu",
        "kr|ia",
        "gr|ia",
        "dh|uu"
    };

    private static readonly HashSet<string> DisallowedNucleusCoda = new(StringComparer.OrdinalIgnoreCase)
    {
        "uu|x",
        "ia|ng",
        "io|ng",
        "oi|x",
        "ea|x"
    };

    private static readonly HashSet<string> DisallowedOnsetCoda = new(StringComparer.OrdinalIgnoreCase)
    {
        "sh|sh",
        "th|th",
        "dh|th",
        "kr|r",
        "gr|r",
        "tr|r",
        "dr|r"
    };

    private static readonly Dictionary<string, SegmentFeatureRow> FeatureMatrix = BuildFeatureMatrix();
    private const int MaxSyllableSelectionAttempts = 24;

    private readonly object _gate = new();
    private readonly Random _random = new(701_903);
    private readonly Dictionary<string, PhoneticLexemeState> _lexicon = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _surfaceForms = new(StringComparer.OrdinalIgnoreCase);

    public string[] CreateEmergentSemanticSeeds(int tokenCount, long tick)
    {
        var count = Math.Clamp(tokenCount, 1, 24);
        var seeds = new string[count];
        for (var i = 0; i < count; i++)
        {
            var cycle = (tick + 1 + (i * 13)) % 997;
            seeds[i] = $"seed_{cycle}_{i}";
        }

        return seeds;
    }

    public PhoneticLexicalization Lexicalize(IReadOnlyList<string> semanticTokens, string mode, long tick, float noveltyBias)
    {
        if (semanticTokens.Count == 0)
        {
            return new PhoneticLexicalization(string.Empty, Array.Empty<string>(), Array.Empty<string>(), 0, 0);
        }

        var created = 0;
        var reused = 0;
        var surfaceTokens = new List<string>(semanticTokens.Count);
        var phonemeTokens = new List<string>(semanticTokens.Count);
        var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "repetition" : mode.Trim().ToLowerInvariant();
        var clampedNovelty = Math.Clamp(noveltyBias, 0.0f, 1.0f);

        lock (_gate)
        {
            for (var i = 0; i < semanticTokens.Count; i++)
            {
                var semantic = NormalizeSemanticToken(semanticTokens[i], i);
                if (!_lexicon.TryGetValue(semantic, out var lexeme))
                {
                    lexeme = CreateLexeme(semantic, i, tick, normalizedMode);
                    _lexicon[semantic] = lexeme;
                    _surfaceForms.Add(lexeme.SurfaceForm);
                    created++;
                }
                else
                {
                    var remapProbability = normalizedMode == "emergent"
                        ? (0.08f + (clampedNovelty * 0.28f))
                        : (0.01f + (clampedNovelty * 0.06f));
                    var shouldRemap = _random.NextDouble() < remapProbability && lexeme.UsageCount > 1;
                    if (shouldRemap)
                    {
                        _surfaceForms.Remove(lexeme.SurfaceForm);
                        var replacement = CreateLexeme(semantic, i, tick, normalizedMode);
                        lexeme.SurfaceForm = replacement.SurfaceForm;
                        lexeme.PhonemeForm = replacement.PhonemeForm;
                        lexeme.Factors = replacement.Factors;
                        _surfaceForms.Add(lexeme.SurfaceForm);
                        created++;
                    }
                    else
                    {
                        reused++;
                    }
                }

                lexeme.UsageCount++;
                lexeme.LastTick = tick;
                lexeme.Strength = Math.Clamp(lexeme.Strength + 0.06f, 0.10f, 8.0f);
                surfaceTokens.Add(lexeme.SurfaceForm);
                phonemeTokens.Add(lexeme.PhonemeForm);
            }

            // Mild decay for inactive lexemes keeps the inventory adaptive.
            foreach (var lexeme in _lexicon.Values)
            {
                if (tick - lexeme.LastTick < 256)
                {
                    continue;
                }

                lexeme.Strength = Math.Max(0.08f, lexeme.Strength * 0.997f);
            }
        }

        return new PhoneticLexicalization(
            Utterance: string.Join(' ', surfaceTokens),
            SurfaceTokens: surfaceTokens,
            PhonemeTokens: phonemeTokens,
            CreatedLexemes: created,
            ReusedLexemes: reused);
    }

    public object GetSnapshot(int maxLexemes)
    {
        var limit = Math.Clamp(maxLexemes, 1, 512);
        lock (_gate)
        {
            var lexemes = _lexicon.Values
                .OrderByDescending(x => x.UsageCount)
                .ThenByDescending(x => x.Strength)
                .Take(limit)
                .Select(x => new PhoneticLexemeSnapshot(
                    x.SemanticToken,
                    x.SurfaceForm,
                    x.PhonemeForm,
                    x.UsageCount,
                    x.Strength,
                    x.LastTick,
                    x.Factors))
                .ToArray();

            return new
            {
                LexiconSize = _lexicon.Count,
                Inventory = new
                {
                    Onsets,
                    SimpleOnsets,
                    ComplexOnsets,
                    Nuclei,
                    Codas
                },
                FeatureMatrix = FeatureMatrix
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new
                    {
                        Segment = x.Key,
                        x.Value.SegmentType,
                        x.Value.Place,
                        x.Value.Manner,
                        x.Value.Voicing,
                        x.Value.Sonority
                    })
                    .ToArray(),
                Phonotactics = new
                {
                    TemplatesByMode,
                    DisallowedOnsetNucleus = DisallowedOnsetNucleus.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                    DisallowedNucleusCoda = DisallowedNucleusCoda.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                    DisallowedOnsetCoda = DisallowedOnsetCoda.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                    MaxSyllableSelectionAttempts
                },
                Lexemes = lexemes
            };
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _lexicon.Clear();
            _surfaceForms.Clear();
        }
    }

    private PhoneticLexemeState CreateLexeme(string semanticToken, int tokenIndex, long tick, string mode)
    {
        var syllableCount = mode switch
        {
            "production" => 3,
            "comprehension" => 2,
            "emergent" => _random.Next(2, 5),
            _ => _random.Next(1, 4)
        };

        var fragments = new List<string>(syllableCount);
        var phones = new List<string>(syllableCount * 3);
        for (var i = 0; i < syllableCount; i++)
        {
            var syllable = CreateSyllable(mode, i);
            var onset = syllable.Onset;
            var nucleus = syllable.Nucleus;
            var coda = syllable.Coda;
            var surface = syllable.Surface;
            fragments.Add(surface);

            if (!string.IsNullOrEmpty(onset))
            {
                phones.Add(onset);
            }

            phones.Add(nucleus);
            if (!string.IsNullOrEmpty(coda))
            {
                phones.Add(coda);
            }
        }

        var candidate = string.Concat(fragments);
        if (candidate.Length > 12)
        {
            candidate = candidate[..12];
        }

        var attempt = 0;
        while (_surfaceForms.Contains(candidate))
        {
            attempt++;
            candidate = $"{candidate[..Math.Min(candidate.Length, 8)]}{(char)('a' + ((tokenIndex + attempt) % 26))}";
            if (candidate.Length > 12)
            {
                candidate = candidate[..12];
            }
        }

        return new PhoneticLexemeState
        {
            SemanticToken = semanticToken,
            SurfaceForm = candidate,
            PhonemeForm = string.Join(' ', phones),
            Factors = BuildFactorVector(mode, phones, syllableCount, semanticToken),
            UsageCount = 0,
            Strength = 0.85f,
            LastTick = tick
        };
    }

    private (string Surface, string Onset, string Nucleus, string Coda) CreateSyllable(string mode, int syllableIndex)
    {
        var templates = GetTemplatesForMode(mode);
        for (var attempt = 0; attempt < MaxSyllableSelectionAttempts; attempt++)
        {
            var template = Pick(templates);
            var onset = ResolveOnset(template);
            var nucleus = Pick(Nuclei);
            var coda = ResolveCoda(template);
            if (!IsAllowedSyllable(onset, nucleus, coda))
            {
                continue;
            }

            return ($"{onset}{nucleus}{coda}", onset, nucleus, coda);
        }

        var fallbackOnset = string.IsNullOrWhiteSpace(mode) ? Pick(SimpleOnsets) : Pick(Onsets);
        var fallbackNucleus = Pick(Nuclei);
        var fallbackCoda = Pick(Codas);
        return ($"{fallbackOnset}{fallbackNucleus}{fallbackCoda}", fallbackOnset, fallbackNucleus, fallbackCoda);
    }

    private static string[] GetTemplatesForMode(string mode)
    {
        if (TemplatesByMode.TryGetValue(mode, out var templates))
        {
            return templates;
        }

        return TemplatesByMode["repetition"];
    }

    private string ResolveOnset(string template)
    {
        if (template.StartsWith("V", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (template.StartsWith("CC", StringComparison.OrdinalIgnoreCase))
        {
            return Pick(ComplexOnsets);
        }

        return Pick(SimpleOnsets);
    }

    private string ResolveCoda(string template)
    {
        if (!template.EndsWith("C", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return Pick(Codas);
    }

    private static bool IsAllowedSyllable(string onset, string nucleus, string coda)
    {
        if (string.IsNullOrEmpty(nucleus))
        {
            return false;
        }

        if (DisallowedOnsetNucleus.Contains($"{onset}|{nucleus}"))
        {
            return false;
        }

        if (DisallowedNucleusCoda.Contains($"{nucleus}|{coda}"))
        {
            return false;
        }

        if (DisallowedOnsetCoda.Contains($"{onset}|{coda}"))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(coda) && string.Equals(onset, coda, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var onsetSonority = GetSonority(onset);
        var nucleusSonority = GetSonority(nucleus);
        var codaSonority = GetSonority(coda);
        if (onsetSonority >= nucleusSonority)
        {
            return false;
        }

        return codaSonority <= nucleusSonority;
    }

    private static int GetSonority(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return 0;
        }

        return FeatureMatrix.TryGetValue(segment, out var features)
            ? features.Sonority
            : 0;
    }

    private static LanguageFactorVector BuildFactorVector(string mode, IReadOnlyList<string> phones, int syllableCount, string semanticToken)
    {
        var onset = phones.FirstOrDefault(p => !IsVowelSegment(p)) ?? string.Empty;
        var nucleus = phones.FirstOrDefault(IsVowelSegment) ?? string.Empty;
        var coda = phones.LastOrDefault(p => !IsVowelSegment(p)) ?? string.Empty;

        var rises = 0;
        var falls = 0;
        for (var i = 1; i < phones.Count; i++)
        {
            var delta = GetSonority(phones[i]) - GetSonority(phones[i - 1]);
            if (delta > 0)
            {
                rises++;
            }
            else if (delta < 0)
            {
                falls++;
            }
        }

        var sonorityShape = rises == falls
            ? "balanced"
            : rises > falls
                ? "rising"
                : "falling";
        var semanticClusterId = Math.Abs(semanticToken.GetHashCode(StringComparison.Ordinal)) % 97;

        return new LanguageFactorVector(
            Mode: mode,
            SyllableCount: syllableCount,
            PhoneCount: phones.Count,
            OnsetProfile: SegmentProfile(onset),
            NucleusProfile: SegmentProfile(nucleus),
            CodaProfile: SegmentProfile(coda),
            SonorityShape: sonorityShape,
            SemanticClusterId: semanticClusterId);
    }

    private static bool IsVowelSegment(string segment)
        => FeatureMatrix.TryGetValue(segment, out var features) &&
           (string.Equals(features.SegmentType, "vowel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(features.SegmentType, "diphthong", StringComparison.OrdinalIgnoreCase));

    private static string SegmentProfile(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "none";
        }

        if (!FeatureMatrix.TryGetValue(segment, out var features))
        {
            return "unknown";
        }

        return $"{features.SegmentType}:{features.Manner}:{features.Place}";
    }

    private static Dictionary<string, SegmentFeatureRow> BuildFeatureMatrix() => new(StringComparer.OrdinalIgnoreCase)
    {
        // Stops
        ["p"] = new("consonant", "bilabial", "stop", "voiceless", 1),
        ["b"] = new("consonant", "bilabial", "stop", "voiced", 1),
        ["t"] = new("consonant", "alveolar", "stop", "voiceless", 1),
        ["d"] = new("consonant", "alveolar", "stop", "voiced", 1),
        ["k"] = new("consonant", "velar", "stop", "voiceless", 1),
        ["g"] = new("consonant", "velar", "stop", "voiced", 1),
        // Fricatives / affricates
        ["f"] = new("consonant", "labiodental", "fricative", "voiceless", 2),
        ["v"] = new("consonant", "labiodental", "fricative", "voiced", 2),
        ["s"] = new("consonant", "alveolar", "fricative", "voiceless", 2),
        ["z"] = new("consonant", "alveolar", "fricative", "voiced", 2),
        ["sh"] = new("consonant", "postalveolar", "fricative", "voiceless", 2),
        ["th"] = new("consonant", "dental", "fricative", "voiceless", 2),
        ["dh"] = new("consonant", "dental", "fricative", "voiced", 2),
        ["h"] = new("consonant", "glottal", "fricative", "voiceless", 2),
        ["ch"] = new("consonant", "postalveolar", "affricate", "voiceless", 2),
        ["x"] = new("consonant", "dorsal", "cluster_fricative", "voiceless", 2),
        // Nasals and liquids
        ["m"] = new("consonant", "bilabial", "nasal", "voiced", 4),
        ["n"] = new("consonant", "alveolar", "nasal", "voiced", 4),
        ["ng"] = new("consonant", "velar", "nasal", "voiced", 4),
        ["l"] = new("consonant", "alveolar", "liquid", "voiced", 5),
        ["r"] = new("consonant", "alveolar", "liquid", "voiced", 5),
        // Glides
        ["y"] = new("consonant", "palatal", "glide", "voiced", 6),
        ["w"] = new("consonant", "labiovelar", "glide", "voiced", 6),
        // Cluster onsets tracked explicitly as macro-segments
        ["kr"] = new("cluster", "mixed", "stop_liquid", "voiceless", 3),
        ["gr"] = new("cluster", "mixed", "stop_liquid", "voiced", 3),
        ["tr"] = new("cluster", "mixed", "stop_liquid", "voiceless", 3),
        ["dr"] = new("cluster", "mixed", "stop_liquid", "voiced", 3),
        ["pl"] = new("cluster", "mixed", "stop_liquid", "voiceless", 3),
        ["kl"] = new("cluster", "mixed", "stop_liquid", "voiceless", 3),
        // Vowels
        ["a"] = new("vowel", "central", "open", "voiced", 9),
        ["e"] = new("vowel", "front", "close_mid", "voiced", 9),
        ["i"] = new("vowel", "front", "close", "voiced", 9),
        ["o"] = new("vowel", "back", "close_mid", "voiced", 9),
        ["u"] = new("vowel", "back", "close", "voiced", 9),
        ["ae"] = new("vowel", "front", "near_open", "voiced", 10),
        ["ai"] = new("diphthong", "front", "open_to_close", "voiced", 10),
        ["au"] = new("diphthong", "back", "open_to_close", "voiced", 10),
        ["ei"] = new("diphthong", "front", "mid_to_close", "voiced", 10),
        ["ia"] = new("diphthong", "front", "close_to_open", "voiced", 10),
        ["io"] = new("diphthong", "front_back", "close_to_mid", "voiced", 10),
        ["oa"] = new("diphthong", "back", "mid_to_open", "voiced", 10),
        ["ou"] = new("diphthong", "back", "mid_to_close", "voiced", 10),
        ["uu"] = new("vowel", "back", "long_close", "voiced", 10),
        ["oi"] = new("diphthong", "back_front", "mid_to_close", "voiced", 10),
        ["ea"] = new("diphthong", "front_central", "mid_to_open", "voiced", 10)
    };

    private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    private static string NormalizeSemanticToken(string token, int fallbackIndex)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return $"unknown_{fallbackIndex}";
        }

        var normalized = token.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9_']+", string.Empty);
        return string.IsNullOrWhiteSpace(normalized) ? $"unknown_{fallbackIndex}" : normalized;
    }

    private sealed record SegmentFeatureRow(string SegmentType, string Place, string Manner, string Voicing, int Sonority);
}

internal sealed class PhoneticLexemeState
{
    public string SemanticToken { get; set; } = string.Empty;
    public string SurfaceForm { get; set; } = string.Empty;
    public string PhonemeForm { get; set; } = string.Empty;
    public LanguageFactorVector Factors { get; set; } = LanguageFactorVector.Default;
    public int UsageCount { get; set; }
    public float Strength { get; set; }
    public long LastTick { get; set; }
}

internal sealed record RuntimeLogEntry(long Tick, double TimestampMs, long WallClockUnixMs, string Message);
internal sealed record PerformanceProfileRequest(string? Profile, bool? RestartSimulation);
internal sealed record AutoProfileControlRequest(
    bool? Enabled,
    bool? AllowRecovery,
    int? WarmupTicks,
    int? ManualHoldTicks,
    double? DegradeNonOkRatio,
    double? DegradeAckLatencyMs,
    long? DegradeSnapshotAgeTicks,
    int? DegradeConsecutiveTicks,
    double? RecoveryNonOkRatio,
    double? RecoveryAckLatencyMs,
    long? RecoverySnapshotAgeTicks,
    int? RecoveryConsecutiveTicks);
internal sealed record CollisionInputRequest(
    string? Pattern,
    float? Intensity,
    int? BurstCount,
    string? TargetStructure,
    string? SourceStructure,
    string? Hemisphere,
    bool? IsFeedback);
internal sealed record LanguageInputRequest(string? Text, string? Mode, float? Intensity, int? BurstPerToken, string? Hemisphere, int? TokenCount, float? NoveltyBias);
internal sealed record PhoneticGenerationRequest(int? TokenCount, string? Mode, float? NoveltyBias, string? SeedText);
internal sealed record RestartServiceRequest(string? StructureId, string? Hemisphere, string? InstanceKey);
internal sealed record CurriculumControlRequest(
    bool? Enabled,
    int? StageIndex,
    bool? ResetProgress);
internal sealed record CurriculumTaskRuntime(
    string Name,
    int StageIndex,
    float Score,
    long Samples,
    long Successes,
    float SuccessRate,
    long LastTick);
internal sealed record CurriculumRuntime(
    bool Enabled,
    int StageIndex,
    string StageName,
    long LastTransitionTick,
    float StageScore,
    float StageProgress,
    long StageTicks,
    IReadOnlyList<CurriculumTaskRuntime> Tasks)
{
    public static CurriculumRuntime Default { get; } = new(
        Enabled: true,
        StageIndex: 0,
        StageName: CurriculumTaskAccumulator.StageNames[0],
        LastTransitionTick: 0,
        StageScore: 0f,
        StageProgress: 0f,
        StageTicks: 0,
        Tasks: []);
}
internal sealed class CurriculumTaskAccumulator
{
    public static IReadOnlyList<string> StageNames { get; } =
    [
        "perceptual_bootstrap",
        "sensorimotor_grounding",
        "language_composition",
        "abstraction_transfer"
    ];

    public string Name { get; }
    public int StageIndex { get; }
    public float ScoreEma { get; private set; }
    public long SampleCount { get; private set; }
    public long SuccessCount { get; private set; }
    public long LastTick { get; private set; }
    public float SuccessRate => SampleCount <= 0 ? 0f : (float)SuccessCount / SampleCount;

    public CurriculumTaskAccumulator(string name, int stageIndex)
    {
        Name = name;
        StageIndex = stageIndex;
    }

    public void Observe(float score, long tick)
    {
        score = Math.Clamp(score, 0f, 1f);
        if (SampleCount <= 0)
        {
            ScoreEma = score;
        }
        else
        {
            const float alpha = 0.10f;
            ScoreEma = (ScoreEma * (1f - alpha)) + (score * alpha);
        }

        SampleCount++;
        if (score >= 0.62f)
        {
            SuccessCount++;
        }

        LastTick = Math.Max(0, tick);
    }

    public void Restore(float scoreEma, long sampleCount, long successCount, long lastTick)
    {
        ScoreEma = Math.Clamp(scoreEma, 0f, 1f);
        SampleCount = Math.Max(0, sampleCount);
        SuccessCount = Math.Clamp(successCount, 0, SampleCount);
        LastTick = Math.Max(0, lastTick);
    }

    public void Reset()
    {
        ScoreEma = 0f;
        SampleCount = 0;
        SuccessCount = 0;
        LastTick = 0;
    }

    public static List<CurriculumTaskAccumulator> CreateDefaults() =>
    [
        new CurriculumTaskAccumulator("sensory_discrimination", 0),
        new CurriculumTaskAccumulator("feature_binding", 0),
        new CurriculumTaskAccumulator("action_outcome_association", 1),
        new CurriculumTaskAccumulator("working_memory_stability", 1),
        new CurriculumTaskAccumulator("language_pathway_composition", 2),
        new CurriculumTaskAccumulator("semantic_disambiguation", 2),
        new CurriculumTaskAccumulator("cross_context_transfer", 3),
        new CurriculumTaskAccumulator("counterfactual_generalization", 3)
    ];
}
internal sealed class NetworkImportReport
{
    public int SourceSchemaVersion { get; set; }
    public int ImportedSchemaVersion { get; set; }
    public bool Migrated { get; set; }
    public List<string> MigrationSteps { get; set; } = [];
    public List<string> DefaultsApplied { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public long ImportedAtUnixMs { get; set; }
    public long ImportedTick { get; set; }
    public double ImportedSimulationMs { get; set; }
    public long ExportedTickWallClockUnixMs { get; set; }
    public string ExportFingerprint { get; set; } = string.Empty;
}
internal sealed class NetworkStateDocument
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public long ExportedAtUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long ExportedTickWallClockUnixMs { get; set; }
    public string ExportFingerprint { get; set; } = string.Empty;
    public long Tick { get; set; }
    public double SimulationClockMs { get; set; }
    public double TickDurationMs { get; set; } = 1.0;
    public Dictionary<string, double> OscillationPhases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string PerformanceProfileName { get; set; } = "normal";
    public InputGateRuntime? InputGates { get; set; } = InputGateRuntime.Default;
    public MetabolicPhysiologyRuntime? MetabolicPhysiology { get; set; } = MetabolicPhysiologyRuntime.Default;
    public CurriculumRuntime Curriculum { get; set; } = CurriculumRuntime.Default;
    public long LastSnapshotTick { get; set; }
    public double LastSnapshotSimulationMs { get; set; }
    public long LastSnapshotWallClockUnixMs { get; set; }
    public long TotalSpontaneousGenerated { get; set; }
    public long TotalSpontaneousDelivered { get; set; }
    public long TotalSpontaneousDispatchErrors { get; set; }
    public Dictionary<string, string> ServiceRegistry { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<SynapticConnection>> ConnectivityMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ServiceRuntimeTelemetry> ServiceTelemetry { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public TransportRuntimeStats? TransportStats { get; set; } = TransportRuntimeStats.Empty;
    public List<RuntimeLogEntry> OutputLog { get; set; } = [];
    public List<RuntimeLogEntry> SpikeLog { get; set; } = [];
    public List<DispatchedSpikeTrace> DispatchSpikeTrace { get; set; } = [];
    public BrainSnapshot? LatestSnapshot { get; set; }
}
internal sealed record RestartServiceItem(string InstanceKey, bool Restarted, bool Healthy, string Message);
internal sealed record RestartServiceResult(int Requested, int Restarted, int Healthy, IReadOnlyList<RestartServiceItem> Items);
internal sealed record StimulusDispatchResult(int GeneratedSpikes, int DeliveredSpikes, IReadOnlyList<string> Errors);
internal sealed record LanguageStimulusTarget(
    StructureId SourceStructure,
    StructureId TargetStructure,
    string TargetNeuronPrefix,
    string? PreferredHemisphere,
    float Gain);
internal sealed record LanguageBackoffEdgeHandle(
    string Key,
    string Mode,
    string GraphId,
    StructureId Source,
    StructureId Target,
    bool IsFallback,
    int Rank,
    string Strategy);
internal sealed record LanguageBackoffResolution(
    bool Resolved,
    LanguageStimulusTarget? Target,
    IReadOnlyList<ServiceInstance> Instances,
    LanguageBackoffEdgeHandle Edge,
    string? FailureReason);
internal sealed record LanguageBackoffEdgeSnapshot(
    string Key,
    string Mode,
    string GraphId,
    string Source,
    string Target,
    bool IsFallback,
    int Rank,
    string Strategy,
    long Attempts,
    long Resolved,
    long Unavailable,
    long DispatchSuccess,
    long DispatchErrors,
    long DeliveredSpikes,
    string? LastError);
internal sealed record LanguageBackoffGraphSnapshot(
    string GraphId,
    string Mode,
    string Description,
    bool IsCurrent,
    long Attempts,
    long Resolved,
    long DispatchSuccess,
    long DispatchErrors,
    long DeadPaths,
    long DeliveredSpikes,
    double ScoreEwma,
    double CompositeScore,
    long LastTick,
    string? LastError);
internal sealed record LanguageBackoffModeStateSnapshot(
    string Mode,
    string CurrentGraphId,
    long LastSwitchTick,
    long LastEvaluationTick,
    long LastResolutionTick);
internal sealed record LanguageBackoffSnapshot(
    long TotalAttempts,
    long TotalResolved,
    long TotalFallbackSelections,
    long TotalDispatchErrors,
    IReadOnlyList<LanguageBackoffEdgeSnapshot> TopEdges,
    IReadOnlyList<LanguageBackoffGraphSnapshot> Graphs,
    IReadOnlyList<LanguageBackoffModeStateSnapshot> ModeStates);
internal sealed record PhoneticLexicalization(
    string Utterance,
    IReadOnlyList<string> SurfaceTokens,
    IReadOnlyList<string> PhonemeTokens,
    int CreatedLexemes,
    int ReusedLexemes);
internal sealed record PhoneticLexemeSnapshot(
    string SemanticToken,
    string SurfaceForm,
    string PhonemeForm,
    int UsageCount,
    float Strength,
    long LastTick,
    LanguageFactorVector Factors);
internal sealed record LanguageFactorVector(
    string Mode,
    int SyllableCount,
    int PhoneCount,
    string OnsetProfile,
    string NucleusProfile,
    string CodaProfile,
    string SonorityShape,
    int SemanticClusterId)
{
    public static LanguageFactorVector Default { get; } = new(
        "repetition",
        1,
        0,
        "none",
        "none",
        "none",
        "balanced",
        0);
}
internal sealed record EnglishLexeme(
    string Surface,
    string PhonemeForm,
    string SemanticClass);





























internal static class EnglishLanguageLexicon
{
    private static readonly IReadOnlyDictionary<string, EnglishLexeme> Lexicon =
        new Dictionary<string, EnglishLexeme>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = new("a", "ah", "function"),
            ["an"] = new("an", "ae n", "function"),
            ["and"] = new("and", "ae n d", "function"),
            ["the"] = new("the", "dh ah", "function"),
            ["to"] = new("to", "t uw", "function"),
            ["of"] = new("of", "ah v", "function"),
            ["in"] = new("in", "ih n", "function"),
            ["on"] = new("on", "aa n", "function"),
            ["with"] = new("with", "w ih th", "function"),
            ["is"] = new("is", "ih z", "function"),
            ["are"] = new("are", "aa r", "function"),
            ["please"] = new("please", "p l iy z", "social"),
            ["what"] = new("what", "w ah t", "question"),
            ["where"] = new("where", "w eh r", "question"),
            ["when"] = new("when", "w eh n", "question"),
            ["why"] = new("why", "w ay", "question"),
            ["how"] = new("how", "h aw", "question"),
            ["who"] = new("who", "h uw", "question"),
            ["which"] = new("which", "w ih ch", "question"),
            ["hello"] = new("hello", "h eh l ow", "social"),
            ["world"] = new("world", "w er l d", "place"),
            ["yes"] = new("yes", "y eh s", "social"),
            ["no"] = new("no", "n ow", "social"),
            ["stop"] = new("stop", "s t aa p", "command"),
            ["wait"] = new("wait", "w ey t", "command"),
            ["stay"] = new("stay", "s t ey", "command"),
            ["hold"] = new("hold", "h ow l d", "command"),
            ["pause"] = new("pause", "p ao z", "command"),
            ["resume"] = new("resume", "r ih z uw m", "command"),
            ["go"] = new("go", "g ow", "command"),
            ["move"] = new("move", "m uw v", "motor"),
            ["run"] = new("run", "r ah n", "motor"),
            ["turn"] = new("turn", "t er n", "motor"),
            ["left"] = new("left", "l eh f t", "direction"),
            ["right"] = new("right", "r ay t", "direction"),
            ["forward"] = new("forward", "f ao r w er d", "direction"),
            ["back"] = new("back", "b ae k", "direction"),
            ["retreat"] = new("retreat", "r iy t r iy t", "direction"),
            ["look"] = new("look", "l uh k", "vision"),
            ["see"] = new("see", "s iy", "vision"),
            ["hear"] = new("hear", "h iy r", "auditory"),
            ["find"] = new("find", "f ay n d", "command"),
            ["seek"] = new("seek", "s iy k", "command"),
            ["get"] = new("get", "g eh t", "command"),
            ["take"] = new("take", "t ey k", "command"),
            ["collect"] = new("collect", "k ah l eh k t", "command"),
            ["eat"] = new("eat", "iy t", "need"),
            ["drink"] = new("drink", "d r ih ng k", "need"),
            ["avoid"] = new("avoid", "ah v oy d", "threat"),
            ["escape"] = new("escape", "ih s k ey p", "threat"),
            ["hide"] = new("hide", "h ay d", "safety"),
            ["use"] = new("use", "y uw z", "command"),
            ["food"] = new("food", "f uw d", "need"),
            ["water"] = new("water", "w ao t er", "need"),
            ["hungry"] = new("hungry", "h ah n g r iy", "need"),
            ["hunger"] = new("hunger", "h ah n g er", "need"),
            ["tired"] = new("tired", "t ay er d", "sleep"),
            ["sleep"] = new("sleep", "s l iy p", "sleep"),
            ["shelter"] = new("shelter", "sh eh l t er", "safety"),
            ["safe"] = new("safe", "s ey f", "safety"),
            ["home"] = new("home", "h ow m", "safety"),
            ["rest"] = new("rest", "r eh s t", "sleep"),
            ["danger"] = new("danger", "d ey n j er", "threat"),
            ["predator"] = new("predator", "p r eh d ah t er", "threat"),
            ["bear"] = new("bear", "b eh r", "threat"),
            ["fear"] = new("fear", "f iy r", "limbic"),
            ["fight"] = new("fight", "f ay t", "limbic"),
            ["attack"] = new("attack", "ah t ae k", "limbic"),
            ["approach"] = new("approach", "ah p r ow ch", "limbic"),
            ["flight"] = new("flight", "f l ay t", "limbic"),
            ["weapon"] = new("weapon", "w eh p ah n", "tool"),
            ["tool"] = new("tool", "t uw l", "tool"),
            ["arm"] = new("arm", "aa r m", "tool"),
            ["short"] = new("short", "sh ao r t", "attribute"),
            ["long"] = new("long", "l ao ng", "attribute"),
            ["day"] = new("day", "d ey", "time"),
            ["night"] = new("night", "n ay t", "time"),
            ["dark"] = new("dark", "d aa r k", "time"),
            ["light"] = new("light", "l ay t", "time"),
            ["happy"] = new("happy", "h ae p iy", "affect"),
            ["sad"] = new("sad", "s ae d", "affect"),
            ["pain"] = new("pain", "p ey n", "body"),
            ["body"] = new("body", "b aa d iy", "body"),
            ["avatar"] = new("avatar", "ae v ah t aa r", "self"),
            ["brain"] = new("brain", "b r ey n", "self"),
            ["language"] = new("language", "l ae ng g w ih j", "language"),
            ["english"] = new("english", "ih ng g l ih sh", "language"),
            ["repeat"] = new("repeat", "r ih p iy t", "language"),
            ["remember"] = new("remember", "r ih m eh m b er", "memory"),
            ["recall"] = new("recall", "r iy k ao l", "memory"),
            ["memory"] = new("memory", "m eh m er iy", "memory"),
            ["think"] = new("think", "th ih ng k", "inner"),
            ["imagine"] = new("imagine", "ih m ae j ih n", "inner"),
            ["say"] = new("say", "s ey", "language")
        };

    public static PhoneticLexicalization Lexicalize(IReadOnlyList<string> sourceTokens)
    {
        if (sourceTokens.Count == 0)
        {
            return new PhoneticLexicalization(string.Empty, Array.Empty<string>(), Array.Empty<string>(), 0, 0);
        }

        var surfaceTokens = new List<string>(sourceTokens.Count);
        var phonemeTokens = new List<string>(sourceTokens.Count);
        for (var i = 0; i < sourceTokens.Count; i++)
        {
            var lexeme = Resolve(sourceTokens[i]);
            surfaceTokens.Add(lexeme.Surface);
            phonemeTokens.Add(lexeme.PhonemeForm);
        }

        return new PhoneticLexicalization(
            Utterance: string.Join(' ', surfaceTokens),
            SurfaceTokens: surfaceTokens,
            PhonemeTokens: phonemeTokens,
            CreatedLexemes: 0,
            ReusedLexemes: surfaceTokens.Count);
    }


    private static EnglishLexeme Resolve(string token)
    {
        var surface = NormalizeSurface(token);
        if (Lexicon.TryGetValue(surface, out var lexeme))
        {
            return lexeme;
        }

        return new EnglishLexeme(surface, GuessPhonemes(surface), GuessSemanticClass(surface));
    }

    private static string NormalizeSurface(string token)
    {
        var normalized = Regex.Replace(token.Trim().ToLowerInvariant(), @"[^a-z0-9_']+", string.Empty);
        return string.IsNullOrWhiteSpace(normalized) ? "silence" : normalized;
    }

    private static string GuessSemanticClass(string surface)
    {
        if (surface.EndsWith("ing", StringComparison.OrdinalIgnoreCase) ||
            surface.EndsWith("ed", StringComparison.OrdinalIgnoreCase))
        {
            return "action";
        }

        if (surface.EndsWith("ly", StringComparison.OrdinalIgnoreCase))
        {
            return "attribute";
        }

        return surface.Length <= 2 ? "function" : "object";
    }


    private static string GuessPhonemes(string surface)
    {
        var cleaned = Regex.Replace(surface.ToLowerInvariant(), @"[^a-z]+", string.Empty);
        if (cleaned.Length == 0)
        {
            return "sil";
        }

        var phonemes = new List<string>(cleaned.Length);
        for (var i = 0; i < cleaned.Length;)
        {
            if (i + 1 < cleaned.Length && TryMapDigraph(cleaned.Substring(i, 2), out var digraph))
            {
                phonemes.Add(digraph);
                i += 2;
                continue;
            }

            phonemes.Add(cleaned[i] switch
            {
                'a' => "ae",
                'b' => "b",
                'c' => "k",
                'd' => "d",
                'e' => "eh",
                'f' => "f",
                'g' => "g",
                'h' => "h",
                'i' => "ih",
                'j' => "j",
                'k' => "k",
                'l' => "l",
                'm' => "m",
                'n' => "n",
                'o' => "ow",
                'p' => "p",
                'q' => "k",
                'r' => "r",
                's' => "s",
                't' => "t",
                'u' => "uh",
                'v' => "v",
                'w' => "w",
                'x' => "k s",
                'y' => "iy",
                'z' => "z",
                _ => "sil"
            });
            i++;
        }

        return string.Join(' ', phonemes);
    }

    private static bool TryMapDigraph(string text, out string phoneme)
    {
        phoneme = text switch
        {
            "th" => "th",
            "sh" => "sh",
            "ch" => "ch",
            "ng" => "ng",
            "ph" => "f",
            "wh" => "w",
            "ee" => "iy",
            "ea" => "iy",
            "oo" => "uw",
            "ou" => "aw",
            "ow" => "aw",
            "ai" => "ey",
            "ay" => "ey",
            "oi" => "oy",
            "oy" => "oy",
            "er" => "er",
            "or" => "ao r",
            "ar" => "aa r",
            _ => string.Empty
        };
        return phoneme.Length > 0;
    }
}
internal sealed record MetabolicPhysiologyRuntime(
    bool NeuronalSleepObserved,
    float AtpBudget,
    float MaxAtpBudget,
    float HomeostaticPressure,
    float MaxHomeostaticPressure,
    float WakePressureBasePerTick,
    float WakePressurePerGeneratedSpike,
    float WakePressurePerInboundSpike,
    float WakePressurePerActivePathway,
    float WakePressurePerSpontaneousEvent,
    float SleepPressureRecoveryPerTick,
    float AwakeBaseDrain,
    float GeneratedSpikeDrain,
    float InboundDrainPerSpike,
    float ActivePathwayDrain,
    float SpontaneousDrainPerEvent,
    float SleepRecoveryPerTick,
    int SleepTicks,
    int WakeTicks,
    long SleepEpisodes,
    long LastTransitionTick)
{
    public static MetabolicPhysiologyRuntime Default { get; } = new(
        NeuronalSleepObserved: false,
        AtpBudget: 1.0f,
        MaxAtpBudget: 1.0f,
        HomeostaticPressure: 0.12f,
        MaxHomeostaticPressure: 1.0f,
        WakePressureBasePerTick: 0.0012f,
        WakePressurePerGeneratedSpike: 0.000030f,
        WakePressurePerInboundSpike: 0.000025f,
        WakePressurePerActivePathway: 0.000050f,
        WakePressurePerSpontaneousEvent: 0.000080f,
        SleepPressureRecoveryPerTick: 0.0055f,
        AwakeBaseDrain: 0.0009f,
        GeneratedSpikeDrain: 0.00007f,
        InboundDrainPerSpike: 0.00005f,
        ActivePathwayDrain: 0.00006f,
        SpontaneousDrainPerEvent: 0.00008f,
        SleepRecoveryPerTick: 0.0062f,
        SleepTicks: 0,
        WakeTicks: 0,
        SleepEpisodes: 0,
        LastTransitionTick: 0);
}

internal sealed record ServiceInstance(StructureId StructureId, string InstanceKey, string Hemisphere, Uri Endpoint)
{
    // Cached once at construction so per-dispatch hot paths avoid the
    // string.IsNullOrWhiteSpace(...) ? "M" : .ToUpperInvariant() allocation.
    public string HemisphereNormalized { get; } =
        string.IsNullOrWhiteSpace(Hemisphere) ? "M" : Hemisphere.ToUpperInvariant();
}
internal sealed record TickStepResult(ServiceInstance Instance, StructureStepResult Step);
internal sealed record InstanceStructureSnapshot(
    ServiceInstance Instance,
    StructureId StructureId,
    int ActiveNeuronCount,
    float MeanFiringRateHz,
    BrainRhythm DominantRhythm,
    IReadOnlyList<NeuronActivity> TopActiveNeurons,
    NeuromodState NeuromodLocal,
    int SpikeInCount,
    int SpikeOutCount,
    int FeedbackQueueDepth,
    MicrotubuleDiagnostics? MicrotubuleDiagnostics = null,
    BodySchemaDiagnostics? BodySchemaDiagnostics = null,
    BasalGangliaDiagnostics? BasalGangliaDiagnostics = null,
    CerebellarDiagnostics? CerebellarDiagnostics = null,
    VestibuloReticularDiagnostics? VestibuloReticularDiagnostics = null,
    SuperiorColliculusDiagnostics? SuperiorColliculusDiagnostics = null,
    HippocampalSpatialDiagnostics? HippocampalSpatialDiagnostics = null,
    SalienceAffectDiagnostics? SalienceAffectDiagnostics = null,
    PrefrontalWorkingMemoryDiagnostics? PrefrontalWorkingMemoryDiagnostics = null,
    ThalamicAttentionGateDiagnostics? ThalamicAttentionGateDiagnostics = null,
    HypothalamicHomeostasisDiagnostics? HypothalamicHomeostasisDiagnostics = null,
    SleepWakeArousalDiagnostics? SleepWakeArousalDiagnostics = null,
    DescendingDefenseDiagnostics? DescendingDefenseDiagnostics = null,
    DopamineRewardDiagnostics? DopamineRewardDiagnostics = null,
    SeptohippocampalThetaDiagnostics? SeptohippocampalThetaDiagnostics = null,
    SpinalProprioceptiveDiagnostics? SpinalProprioceptiveDiagnostics = null,
    OlfactoryLimbicMemoryDiagnostics? OlfactoryLimbicMemoryDiagnostics = null,
    AuditoryLanguageMotorDiagnostics? AuditoryLanguageMotorDiagnostics = null,
    VisualObjectRecognitionDiagnostics? VisualObjectRecognitionDiagnostics = null,
    ActionSelectionDiagnostics? ActionSelectionDiagnostics = null,
    PerceptEnsembleDiagnostics? PerceptEnsembleDiagnostics = null,
    SynapticMemoryDiagnostics? SynapticMemoryDiagnostics = null,
    NeuronalAttentionWorkspaceDiagnostics? NeuronalAttentionWorkspaceDiagnostics = null,
    NeuronalSleepConsolidationDiagnostics? NeuronalSleepConsolidationDiagnostics = null);

internal sealed class AdminInputRestartGate
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _lastRestartAtMs = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ServiceInstance> SelectRestartCandidates(
        IReadOnlyList<ServiceInstance> instances,
        int cooldownMs)
    {
        if (instances.Count == 0)
        {
            return [];
        }

        var normalizedCooldownMs = Math.Clamp(cooldownMs, 250, 120000);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var restartCandidates = new List<ServiceInstance>(instances.Count);

        lock (_gate)
        {
            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (_lastRestartAtMs.TryGetValue(instance.InstanceKey, out var lastRestartAtMs) &&
                    nowMs - lastRestartAtMs < normalizedCooldownMs)
                {
                    continue;
                }

                _lastRestartAtMs[instance.InstanceKey] = nowMs;
                restartCandidates.Add(instance);
            }
        }

        return restartCandidates;
    }
}

internal sealed class RuntimeInstanceCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ServiceInstance> _knownByInstanceKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<StructureId, List<ServiceInstance>> _knownByStructure = new();
    private readonly HashSet<string> _liveInstanceKeys = new(StringComparer.OrdinalIgnoreCase);

    public void SetKnownInstances(IEnumerable<ServiceInstance> instances)
    {
        lock (_gate)
        {
            _knownByInstanceKey.Clear();
            _knownByStructure.Clear();
            foreach (var instance in instances)
            {
                _knownByInstanceKey[instance.InstanceKey] = instance;
                if (!_knownByStructure.TryGetValue(instance.StructureId, out var list))
                {
                    list = [];
                    _knownByStructure[instance.StructureId] = list;
                }

                list.Add(instance);
            }
        }
    }

    public void SetLiveInstances(IEnumerable<ServiceInstance> instances)
    {
        lock (_gate)
        {
            _liveInstanceKeys.Clear();
            foreach (var instance in instances)
            {
                _liveInstanceKeys.Add(instance.InstanceKey);
                if (_knownByInstanceKey.ContainsKey(instance.InstanceKey))
                {
                    continue;
                }

                _knownByInstanceKey[instance.InstanceKey] = instance;
                if (!_knownByStructure.TryGetValue(instance.StructureId, out var list))
                {
                    list = [];
                    _knownByStructure[instance.StructureId] = list;
                }

                list.Add(instance);
            }
        }
    }

    public void SetInstances(IEnumerable<ServiceInstance> instances)
    {
        var snapshot = instances.ToList();
        SetKnownInstances(snapshot);
        SetLiveInstances(snapshot);
    }

    public bool TryGetByInstanceKey(string instanceKey, out ServiceInstance instance)
    {
        lock (_gate)
        {
            return _knownByInstanceKey.TryGetValue(instanceKey, out instance!);
        }
    }

    public IReadOnlyList<ServiceInstance> GetByStructure(StructureId structureId, string? hemisphere)
    {
        lock (_gate)
        {
            if (!_knownByStructure.TryGetValue(structureId, out var list) || list.Count == 0)
            {
                return [];
            }

            var filtered = FilterHemisphere(list, hemisphere);
            if (filtered.Count == 0)
            {
                return [];
            }

            var live = new List<ServiceInstance>(filtered.Count);
            for (var i = 0; i < filtered.Count; i++)
            {
                var instance = filtered[i];
                if (_liveInstanceKeys.Contains(instance.InstanceKey))
                {
                    live.Add(instance);
                }
            }

            return live;
        }
    }

    public IReadOnlyList<ServiceInstance> GetByStructureWithKnownFallback(StructureId structureId, string? hemisphere)
    {
        lock (_gate)
        {
            if (!_knownByStructure.TryGetValue(structureId, out var list) || list.Count == 0)
            {
                return [];
            }

            var filtered = FilterHemisphere(list, hemisphere);
            if (filtered.Count == 0)
            {
                return [];
            }

            var live = new List<ServiceInstance>(filtered.Count);
            for (var i = 0; i < filtered.Count; i++)
            {
                var instance = filtered[i];
                if (_liveInstanceKeys.Contains(instance.InstanceKey))
                {
                    live.Add(instance);
                }
            }

            if (live.Count > 0)
            {
                return live;
            }

            return filtered;
        }
    }

    private static List<ServiceInstance> FilterHemisphere(List<ServiceInstance> list, string? hemisphere)
    {
        if (!TryNormalizeHemisphere(hemisphere, out var normalizedHemisphere))
        {
            return new List<ServiceInstance>(list);
        }

        var filtered = new List<ServiceInstance>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var instance = list[i];
            if (string.Equals(instance.Hemisphere, normalizedHemisphere, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(instance);
            }
        }

        return filtered;
    }

    private static bool TryNormalizeHemisphere(string? hemisphere, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(hemisphere))
        {
            return false;
        }

        var hemi = hemisphere.Trim();
        if (hemi.Equals("both", StringComparison.OrdinalIgnoreCase) ||
            hemi.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            hemi.Equals("any", StringComparison.OrdinalIgnoreCase) ||
            hemi.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
            hemi.Equals("*", StringComparison.Ordinal))
        {
            return false;
        }

        if (hemi.Equals("left", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "L";
            return true;
        }

        if (hemi.Equals("right", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "R";
            return true;
        }

        if (hemi.Equals("midline", StringComparison.OrdinalIgnoreCase) ||
            hemi.Equals("middle", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "M";
            return true;
        }

        if (hemi.Length == 1)
        {
            var value = char.ToUpperInvariant(hemi[0]);
            if (value is 'L' or 'R' or 'M')
            {
                normalized = value == 'L' ? "L" : value == 'R' ? "R" : "M";
                return true;
            }
        }

        // Unknown hint: default to all hemispheres for resilience.
        return false;
    }
}



internal sealed class StructureProcessSupervisor(IConfiguration configuration, ILogger<StructureProcessSupervisor> logger) : IAsyncDisposable
{
    private readonly Dictionary<string, ManagedStructureProcess> _spawnedByInstance = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private static readonly ConcurrentDictionary<string, object> BuildLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient HealthProbeClient = new(new SocketsHttpHandler
    {
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromMilliseconds(600),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        MaxConnectionsPerServer = 128,
        AutomaticDecompression = DecompressionMethods.None
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private static readonly IReadOnlyDictionary<StructureId, string> ProjectDirectoryByStructure = new Dictionary<StructureId, string>
    {
        [StructureId.V1] = "V1",
        [StructureId.Retina] = "Retina",
        [StructureId.A1] = "A1",
        [StructureId.Cochlea] = "Cochlea",
        [StructureId.CochlearNucleus] = "CochlearNucleus",
        [StructureId.SuperiorOlive] = "SuperiorOlive",
        [StructureId.InferiorColliculus] = "InferiorColliculus",
        [StructureId.S1] = "S1",
        [StructureId.VestibularNuclei] = "VestibularNuclei",
        [StructureId.NucleusTractusSolitarius] = "NucleusTractusSolitarius",
        [StructureId.OlfactoryBulb] = "OlfactoryBulb",
        [StructureId.CorpusCallosum] = "CorpusCallosum",
        [StructureId.Thalamus] = "Thalamus",
        [StructureId.Trn] = "TRN",
        [StructureId.Pulvinar] = "Pulvinar",
        [StructureId.MediodorsalThalamus] = "MediodorsalThalamus",
        [StructureId.IntralaminarThalamus] = "IntralaminarThalamus",
        [StructureId.EntorhinalCortex] = "EntorhinalCortex",
        [StructureId.DentateGyrus] = "Hippocampus.DG",
        [StructureId.CA3] = "Hippocampus.CA3",
        [StructureId.CA2] = "Hippocampus.CA2",
        [StructureId.CA1] = "Hippocampus.CA1",
        [StructureId.Subiculum] = "Subiculum",
        [StructureId.Presubiculum] = "Presubiculum",
        [StructureId.Parasubiculum] = "Parasubiculum",
        [StructureId.Pfc] = "PFC",
        [StructureId.BrocaBa44Ba45] = "BrocaBa44Ba45",
        [StructureId.WernickePstgPsts] = "WernickePstgPsts",
        [StructureId.ArcuateFasciculus] = "ArcuateFasciculus",
        [StructureId.SupramarginalAngular] = "SupramarginalAngular",
        [StructureId.OrbitofrontalCortex] = "OrbitofrontalCortex",
        [StructureId.Insula] = "Insula",
        [StructureId.Ppc] = "PPC",
        [StructureId.TemporalAssociation] = "TemporalAssociation",
        [StructureId.Striatum] = "Striatum",
        [StructureId.GlobusPallidus] = "GlobusPallidus",
        [StructureId.GPe] = "GPe",
        [StructureId.GPi] = "GPi",
        [StructureId.Stn] = "STN",
        [StructureId.Snr] = "SNr",
        [StructureId.Snc] = "SNc",
        [StructureId.Hypothalamus] = "Hypothalamus",
        [StructureId.Amygdala] = "Amygdala",
        [StructureId.Acc] = "ACC",
        [StructureId.CerebellarGranule] = "Cerebellum.GranuleCellLayer",
        [StructureId.CerebellarVermis] = "Cerebellum.Vermis",
        [StructureId.CerebellarLobules] = "Cerebellum.Lobules",
        [StructureId.PurkinjeCellLayer] = "Cerebellum.PurkinjeCellLayer",
        [StructureId.DeepCerebellarNuclei] = "Cerebellum.DCN",
        [StructureId.InferiorOlive] = "InferiorOlive",
        [StructureId.ReticularFormation] = "ReticularFormation",
        [StructureId.PeriaqueductalGray] = "PeriaqueductalGray",
        [StructureId.Pons] = "Pons",
        [StructureId.Medulla] = "Medulla",
        [StructureId.SpinalCordMotor] = "SpinalCordMotor",
        [StructureId.LocusCoeruleus] = "LocusCoeruleus",
        [StructureId.RapheNuclei] = "RapheNuclei",
        [StructureId.BasalForebrain] = "BasalForebrainCholinergic",
        [StructureId.Vta] = "VTA",
        [StructureId.M1] = "M1",
        [StructureId.Sma] = "SMA",
        [StructureId.V2] = "V2",
        [StructureId.V4] = "V4",
        [StructureId.Mt] = "MT",
        [StructureId.PremotorCortex] = "PremotorCortex",
        [StructureId.ParahippocampalCortex] = "ParahippocampalCortex",
        [StructureId.PerirhinalCortex] = "PerirhinalCortex",
        [StructureId.PosteriorCingulate] = "PosteriorCingulate",
        [StructureId.RetrosplenialCortex] = "RetrosplenialCortex",
        [StructureId.NucleusAccumbens] = "NucleusAccumbens",
        [StructureId.VentralPallidum] = "VentralPallidum",
        [StructureId.Habenula] = "Habenula",
        [StructureId.MotorThalamus] = "MotorThalamus",
        [StructureId.SuperiorColliculus] = "SuperiorColliculus",
        [StructureId.V3] = "V3",
        [StructureId.AuditoryAssociationCortex] = "AuditoryAssociationCortex",
        [StructureId.SecondarySomatosensoryCortex] = "SecondarySomatosensoryCortex",
        [StructureId.InferotemporalCortex] = "InferotemporalCortex",
        [StructureId.FusiformGyrus] = "FusiformGyrus",
        [StructureId.TemporalPole] = "TemporalPole",
        [StructureId.TemporoparietalJunction] = "TemporoparietalJunction",
        [StructureId.Precuneus] = "Precuneus",
        [StructureId.MidcingulateCortex] = "MidcingulateCortex",
        [StructureId.DorsomedialPrefrontalCortex] = "DorsomedialPrefrontalCortex",
        [StructureId.VentromedialPrefrontalCortex] = "VentromedialPrefrontalCortex",
        [StructureId.FrontalEyeFields] = "FrontalEyeFields"
    };

    public async Task<RestartServiceResult> EnsureServicesOnlineAsync(IReadOnlyList<ServiceInstance> instances, int tickTimeoutMs, CancellationToken cancellationToken)
    {
        if (instances.Count == 0)
        {
            return new RestartServiceResult(0, 0, 0, []);
        }

        var autoStartEnabled = configuration.GetValue<bool>("StructureProcessHost:AutoStartEnabled", true);
        var startupTimeoutMs = Math.Clamp(configuration.GetValue<int>("StructureProcessHost:StartupTimeoutMs", 20000), 2000, 120000);
        var probeTimeoutMs = Math.Clamp(configuration.GetValue<int>("StructureProcessHost:HealthProbeTimeoutMs", Math.Max(250, tickTimeoutMs / 3)), 150, 5000);
        var launchParallelism = Math.Clamp(configuration.GetValue<int>("StructureProcessHost:LaunchParallelism", 8), 1, 64);
        var root = ResolveRepositoryRoot();
        var launchedKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var launchMessages = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var initialChecks = await Task.WhenAll(instances.Select(async instance =>
            new
            {
                Instance = instance,
                IsHealthy = await IsHealthyAsync(instance.Endpoint, probeTimeoutMs, cancellationToken)
            }));

        var offline = initialChecks
            .Where(x => !x.IsHealthy)
            .ToDictionary(x => x.Instance.InstanceKey, x => x.Instance, StringComparer.OrdinalIgnoreCase);

        if (offline.Count == 0)
        {
            ControlHealthLog.Append($"structure autostart check: all {instances.Count} services API-healthy");
            return BuildSupervisorProbeResult(instances, offline, launchedKeys, launchMessages);
        }

        logger.LogInformation("Structure autostart: {Count} services offline. Attempting launch...", offline.Count);
        ControlHealthLog.Append(
            $"structure autostart check: offline={offline.Count}/{instances.Count}{Environment.NewLine}" +
            string.Join(Environment.NewLine, offline.Values.Select(instance => $"{instance.InstanceKey} {instance.StructureId} {instance.HemisphereNormalized} endpoint={instance.Endpoint}")));

        if (!autoStartEnabled)
        {
            foreach (var instance in offline.Values)
            {
                launchMessages.TryAdd(instance.InstanceKey, "autostart disabled; endpoint is not API-healthy");
            }

            return BuildSupervisorProbeResult(instances, offline, launchedKeys, launchMessages);
        }

        var localLaunchTargets = offline.Values
            .Where(i => IsLocallyManagedEndpoint(i.Endpoint))
            .ToList();
        var remoteUnmanagedTargets = offline.Values
            .Where(i => !IsLocallyManagedEndpoint(i.Endpoint))
            .ToList();

        if (remoteUnmanagedTargets.Count > 0)
        {
            logger.LogInformation(
                "Structure autostart: skipping unmanaged remote endpoints: {Endpoints}",
                string.Join(", ", remoteUnmanagedTargets.Select(x => $"{x.InstanceKey}@{x.Endpoint}")));
            foreach (var instance in remoteUnmanagedTargets)
            {
                launchMessages.TryAdd(instance.InstanceKey, "remote endpoint unmanaged by local supervisor");
            }
        }

        await Parallel.ForEachAsync(
            localLaunchTargets,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = launchParallelism
            },
            async (pair, loopCancellationToken) =>
            {
                if (!TryResolveProjectPath(root, pair.StructureId, out var projectPath))
                {
                    logger.LogWarning("Structure autostart: unable to resolve project path for {ServiceInstance}.", pair.InstanceKey);
                    launchMessages.TryAdd(pair.InstanceKey, "project path not found");
                    return;
                }

                await StopTrackedProcessAsync(pair.InstanceKey, loopCancellationToken).ConfigureAwait(false);
                var started = TryLaunch(pair, projectPath);
                if (!started)
                {
                    logger.LogWarning("Structure autostart: failed to launch {ServiceInstance} on {Endpoint}.", pair.InstanceKey, pair.Endpoint);
                    launchMessages.TryAdd(pair.InstanceKey, "launch failed");
                }
                else
                {
                    launchedKeys.TryAdd(pair.InstanceKey, 0);
                }

            });

        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(startupTimeoutMs);
        while (offline.Count > 0 && DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var nowHealthy = await Task.WhenAll(offline.Select(async pair =>
                new
                {
                    pair.Key,
                    IsHealthy = await IsHealthyAsync(pair.Value.Endpoint, probeTimeoutMs, cancellationToken)
                }));

            foreach (var entry in nowHealthy.Where(x => x.IsHealthy))
            {
                offline.Remove(entry.Key);
            }

            if (offline.Count == 0)
            {
                break;
            }

            await Task.Delay(250, cancellationToken);
        }

        if (offline.Count > 0)
        {
            logger.LogWarning(
                "Structure autostart incomplete. Still offline: {Offline}",
                string.Join(", ", offline.Values.Select(x => $"{x.InstanceKey}@{x.Endpoint}")));
            foreach (var instance in offline.Values)
            {
                launchMessages.TryAdd(instance.InstanceKey, "health probe timeout");
            }

            ControlHealthLog.Append(
                $"structure autostart incomplete: offline={offline.Count}/{instances.Count}{Environment.NewLine}" +
                string.Join(Environment.NewLine, offline.Values.Select(instance => $"{instance.InstanceKey} {instance.StructureId} {instance.HemisphereNormalized} endpoint={instance.Endpoint}")));
        }
        else
        {
            logger.LogInformation("Structure autostart: all services are online.");
            ControlHealthLog.Append($"structure autostart complete: all {instances.Count} services API-healthy");
        }

        return BuildSupervisorProbeResult(instances, offline, launchedKeys, launchMessages);
    }

    private static RestartServiceResult BuildSupervisorProbeResult(
        IReadOnlyList<ServiceInstance> instances,
        IReadOnlyDictionary<string, ServiceInstance> offline,
        IReadOnlyDictionary<string, byte> launchedKeys,
        IReadOnlyDictionary<string, string> messages)
    {
        var items = instances
            .Select(instance =>
            {
                var healthy = !offline.ContainsKey(instance.InstanceKey);
                var restarted = launchedKeys.ContainsKey(instance.InstanceKey);
                var message = healthy
                    ? "online"
                    : messages.TryGetValue(instance.InstanceKey, out var reason)
                        ? reason
                        : "endpoint is not API-healthy";
                return new RestartServiceItem(instance.InstanceKey, restarted, healthy, message);
            })
            .OrderBy(item => item.InstanceKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RestartServiceResult(
            instances.Count,
            items.Count(item => item.Restarted),
            items.Count(item => item.Healthy),
            items);
    }

    public async Task<RestartServiceResult> RestartServicesAsync(IReadOnlyList<ServiceInstance> instances, CancellationToken cancellationToken)
    {
        if (instances.Count == 0)
        {
            return new RestartServiceResult(0, 0, 0, []);
        }

        var startupTimeoutMs = Math.Clamp(configuration.GetValue<int>("StructureProcessHost:StartupTimeoutMs", 20000), 2000, 120000);
        var probeTimeoutMs = Math.Clamp(configuration.GetValue<int>("StructureProcessHost:HealthProbeTimeoutMs", 500), 150, 5000);
        var restartParallelism = Math.Clamp(configuration.GetValue<int>("StructureProcessHost:RestartParallelism", 10), 1, 64);
        var root = ResolveRepositoryRoot();
        var items = new ConcurrentBag<RestartServiceItem>();
        var restarted = 0;
        var healthy = 0;

        await Parallel.ForEachAsync(
            instances,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = restartParallelism
            },
            async (instance, ct) =>
            {
                if (!IsLocallyManagedEndpoint(instance.Endpoint))
                {
                    var remoteHealthy = await IsHealthyAsync(instance.Endpoint, probeTimeoutMs, ct);
                    if (remoteHealthy)
                    {
                        Interlocked.Increment(ref healthy);
                    }

                    items.Add(new RestartServiceItem(
                        instance.InstanceKey,
                        Restarted: false,
                        Healthy: remoteHealthy,
                        Message: "remote endpoint unmanaged by local supervisor"));
                    return;
                }

                if (!TryResolveProjectPath(root, instance.StructureId, out var projectPath))
                {
                    items.Add(new RestartServiceItem(instance.InstanceKey, false, false, "project path not found"));
                    return;
                }

                await StopTrackedProcessAsync(instance.InstanceKey, ct).ConfigureAwait(false);
                var started = TryLaunch(instance, projectPath);
                if (!started)
                {
                    items.Add(new RestartServiceItem(instance.InstanceKey, false, false, "launch failed"));
                    return;
                }

                Interlocked.Increment(ref restarted);
                var ok = await WaitForHealthyAsync(instance.Endpoint, probeTimeoutMs, startupTimeoutMs, ct);
                if (ok)
                {
                    Interlocked.Increment(ref healthy);
                    items.Add(new RestartServiceItem(instance.InstanceKey, true, true, "online"));
                }
                else
                {
                    items.Add(new RestartServiceItem(instance.InstanceKey, true, false, "launch started, health probe timeout"));
                }
            });

        var orderedItems = items.OrderBy(i => i.InstanceKey, StringComparer.OrdinalIgnoreCase).ToList();
        return new RestartServiceResult(instances.Count, restarted, healthy, orderedItems);
    }

    private bool TryLaunch(ServiceInstance instance, string projectPath)
    {
        try
        {
            var projectDir = Path.GetDirectoryName(projectPath) ?? ".";
            var assemblyName = GetProjectAssemblyName(projectPath);
            var buildConfiguration = NormalizeBuildConfiguration(configuration["StructureProcessHost:Configuration"]);
            var dllPath = Path.Combine(projectDir, "bin", buildConfiguration, "net8.0", $"{assemblyName}.dll");
            if (ShouldRefreshProjectOutput(projectPath, dllPath))
            {
                var buildLock = BuildLocks.GetOrAdd(projectPath, _ => new object());
                lock (buildLock)
                {
                    if (ShouldRefreshProjectOutput(projectPath, dllPath) &&
                        !TryBuildProject(projectPath, dllPath, instance, buildConfiguration))
                    {
                        return false;
                    }
                }
            }

            ProcessStartInfo startInfo;
            if (File.Exists(dllPath))
            {
                startInfo = new ProcessStartInfo("dotnet", $"\"{dllPath}\"");
            }
            else
            {
                startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" -c {buildConfiguration} --no-launch-profile");
            }

            startInfo.WorkingDirectory = projectDir;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.Environment["PORT"] = instance.Endpoint.Port.ToString();
            startInfo.Environment["ASPNETCORE_URLS"] = $"http://localhost:{instance.Endpoint.Port}";
            startInfo.Environment["HEMISPHERE"] = instance.Hemisphere;
            startInfo.Environment["SERVICE_INSTANCE"] = instance.InstanceKey;
            var controlPublishUrl = configuration.GetValue<string>("ControlPublishUrl");
            if (string.IsNullOrWhiteSpace(controlPublishUrl))
            {
                controlPublishUrl = "http://localhost:5080/api/v1/publish/step";
            }
            startInfo.Environment["CONTROL_PUBLISH_URL"] = controlPublishUrl;

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    logger.LogDebug("[{ServiceInstance}] {Line}", instance.InstanceKey, e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    logger.LogWarning("[{ServiceInstance}] {Line}", instance.InstanceKey, e.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (_gate)
            {
                if (_spawnedByInstance.TryGetValue(instance.InstanceKey, out var previous))
                {
                    logger.LogWarning(
                        "Structure autostart: refused duplicate launch for {ServiceInstance}; tracked process {ProcessId} remains authoritative.",
                        instance.InstanceKey,
                        previous.Process.Id);
                    TryTerminateProcess(process);
                    return false;
                }

                _spawnedByInstance[instance.InstanceKey] = new ManagedStructureProcess(process, instance.Endpoint);
            }

            logger.LogInformation("Structure autostart: launched {ServiceInstance} at {Endpoint}.", instance.InstanceKey, instance.Endpoint);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Structure autostart: exception while launching {ServiceInstance}.", instance.InstanceKey);
            return false;
        }
    }

    private bool ShouldRefreshProjectOutput(string projectPath, string dllPath)
    {
        if (!configuration.GetValue<bool>("StructureProcessHost:RefreshStaleBuilds", true))
        {
            return false;
        }

        if (!File.Exists(dllPath))
        {
            return true;
        }

        var outputTimestamp = File.GetLastWriteTimeUtc(dllPath);
        return EnumerateProjectInputs(projectPath).Any(path =>
        {
            try
            {
                return File.GetLastWriteTimeUtc(path) > outputTimestamp;
            }
            catch
            {
                return false;
            }
        });
    }

    private bool TryBuildProject(string projectPath, string dllPath, ServiceInstance instance, string buildConfiguration)
    {
        var projectDir = Path.GetDirectoryName(projectPath) ?? ".";
        var timeoutMs = Math.Clamp(configuration.GetValue<int>("StructureProcessHost:BuildTimeoutMs", 120000), 10000, 600000);
        var outputLines = new ConcurrentQueue<string>();

        try
        {
            logger.LogInformation("Structure autostart: refreshing stale output for {ServiceInstance}.", instance.InstanceKey);
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("dotnet", $"build \"{projectPath}\" -c {buildConfiguration} --nologo --verbosity minimal")
            {
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.OutputDataReceived += (_, e) => EnqueueBuildLogLine(outputLines, e.Data);
            process.ErrorDataReceived += (_, e) => EnqueueBuildLogLine(outputLines, e.Data);

            if (!process.Start())
            {
                logger.LogWarning("Structure autostart: build process failed to start for {ServiceInstance}.", instance.InstanceKey);
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(timeoutMs))
            {
                TryTerminateProcess(process);
                logger.LogWarning("Structure autostart: build timed out for {ServiceInstance} after {TimeoutMs} ms.", instance.InstanceKey, timeoutMs);
                return false;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                logger.LogWarning(
                    "Structure autostart: build failed for {ServiceInstance} with exit code {ExitCode}. Output: {Output}",
                    instance.InstanceKey,
                    process.ExitCode,
                    string.Join(" | ", outputLines));
                return false;
            }

            if (!File.Exists(dllPath))
            {
                logger.LogWarning("Structure autostart: build succeeded but output was not found for {ServiceInstance}: {DllPath}", instance.InstanceKey, dllPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Structure autostart: exception while building {ServiceInstance}.", instance.InstanceKey);
            return false;
        }
    }

    private static void EnqueueBuildLogLine(ConcurrentQueue<string> outputLines, string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || outputLines.Count >= 40)
        {
            return;
        }

        outputLines.Enqueue(line.Trim());
    }

    private static IEnumerable<string> EnumerateProjectInputs(string projectPath)
    {
        return EnumerateProjectInputs(projectPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateProjectInputs(string projectPath, HashSet<string> visitedProjects)
    {
        projectPath = Path.GetFullPath(projectPath);
        if (!visitedProjects.Add(projectPath))
        {
            yield break;
        }

        var projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
        {
            yield break;
        }

        yield return projectPath;

        var repositoryRoot = ResolveRepositoryRoot();
        foreach (var commonInput in new[]
                 {
                     Path.Combine(repositoryRoot, "Directory.Build.props"),
                     Path.Combine(repositoryRoot, "Directory.Build.targets"),
                     Path.Combine(repositoryRoot, "global.json")
                 })
        {
            if (File.Exists(commonInput))
            {
                yield return commonInput;
            }
        }

        foreach (var path in Directory.EnumerateFiles(projectDir, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(projectDir, path);
            if (relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(path);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }

        foreach (var referencePath in EnumerateProjectReferencePaths(projectPath, projectDir))
        {
            foreach (var input in EnumerateProjectInputs(referencePath, visitedProjects))
            {
                yield return input;
            }
        }
    }

    private static IEnumerable<string> EnumerateProjectReferencePaths(string projectPath, string projectDir)
    {
        string projectText;
        try
        {
            projectText = File.ReadAllText(projectPath);
        }
        catch
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(projectText, @"<ProjectReference\s+Include=""(?<path>[^""]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var referencePath = match.Groups["path"].Value.Trim();
            if (string.IsNullOrWhiteSpace(referencePath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(projectDir, referencePath));
            if (File.Exists(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    private static string GetProjectAssemblyName(string projectPath)
    {
        try
        {
            var projectText = File.ReadAllText(projectPath);
            var match = Regex.Match(
                projectText,
                @"<AssemblyName>\s*(?<assembly>[^<]+?)\s*</AssemblyName>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                var assemblyName = match.Groups["assembly"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    return assemblyName;
                }
            }
        }
        catch
        {
            // Fall back to the project filename when the project file cannot be inspected.
        }

        return Path.GetFileNameWithoutExtension(projectPath);
    }

    private async Task<bool> WaitForHealthyAsync(Uri endpoint, int probeTimeoutMs, int startupTimeoutMs, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(startupTimeoutMs);
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await IsHealthyAsync(endpoint, probeTimeoutMs, cancellationToken))
            {
                return true;
            }

            await Task.Delay(200, cancellationToken);
        }

        return false;
    }

    private async Task StopTrackedProcessAsync(string instanceKey, CancellationToken cancellationToken)
    {
        ManagedStructureProcess? managed;
        lock (_gate)
        {
            if (!_spawnedByInstance.TryGetValue(instanceKey, out managed))
            {
                return;
            }

            _spawnedByInstance.Remove(instanceKey);
        }

        await StopManagedProcessAsync(managed, cancellationToken).ConfigureAwait(false);
    }

    private static async Task StopManagedProcessAsync(ManagedStructureProcess managed, CancellationToken cancellationToken)
    {
        try
        {
            if (managed.Process.HasExited)
            {
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(managed.Endpoint, "/api/v1/structure/shutdown"));
            NreStructureSecurity.ApplyRequestAuthentication(request, NreStructureSecurity.ResolveSharedSecret());
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                using var response = await HealthProbeClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestTimeout.Token).ConfigureAwait(false);
            }
            catch
            {
                // The host may already be exiting or may predate the shutdown endpoint.
            }

            using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try
            {
                await managed.Process.WaitForExitAsync(exitTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryTerminateProcess(managed.Process, dispose: false);
            }
        }
        finally
        {
            managed.Process.Dispose();
        }
    }

    private static void TryTerminateProcess(Process? process, bool dispose = true)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // Best-effort shutdown.
        }
        finally
        {
            if (dispose)
            {
                process.Dispose();
            }
        }
    }

    private static async Task<bool> IsHealthyAsync(Uri endpoint, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            using var response = await HealthProbeClient.GetAsync(
                new Uri(endpoint, "/health"),
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
        }
        catch
        {
            // A structure is only considered online when its API responds.
            // A bare open TCP port can still be a warming or failed host.
        }

        return false;
    }

    private static bool TryResolveProjectPath(string repositoryRoot, StructureId structureId, out string projectPath)
    {
        projectPath = string.Empty;
        if (!ProjectDirectoryByStructure.TryGetValue(structureId, out var folder))
        {
            return false;
        }

        var structureDir = Path.Combine(repositoryRoot, "Structures", folder);
        if (!Directory.Exists(structureDir))
        {
            return false;
        }

        var csproj = Directory.GetFiles(structureDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(csproj))
        {
            return false;
        }

        projectPath = csproj;
        return true;
    }

    private static string ResolveRepositoryRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            if (Directory.Exists(Path.Combine(cursor.FullName, "Structures")))
            {
                return cursor.FullName;
            }

            cursor = cursor.Parent;
        }

        return AppContext.BaseDirectory;
    }

    public async ValueTask DisposeAsync()
    {
        List<ManagedStructureProcess> processes;
        lock (_gate)
        {
            processes = _spawnedByInstance.Values.ToList();
            _spawnedByInstance.Clear();
        }

        foreach (var process in processes)
        {
            await StopManagedProcessAsync(process, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static string NormalizeBuildConfiguration(string? configured) =>
        string.Equals(configured, "Debug", StringComparison.OrdinalIgnoreCase) ? "Debug" : "Release";

    private sealed record ManagedStructureProcess(Process Process, Uri Endpoint);

    private static bool IsLocallyManagedEndpoint(Uri endpoint)
    {
        if (endpoint.IsLoopback)
        {
            return true;
        }

        var host = endpoint.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var dnsHostName = Dns.GetHostName();
            if (host.Equals(dnsHostName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
            // best effort only
        }

        return false;
    }
}

internal sealed class DialogueTurnManager
{
    private readonly object _gate = new();
    private long _sequence;
    private DialogueTurnSnapshot _snapshot = DialogueTurnSnapshot.Empty;

    public DialogueTurnSnapshot ObserveInput(
        string mode,
        string text,
        IReadOnlyList<string> brainTokens,
        long tick)
    {
        lock (_gate)
        {
            _sequence++;
            var tokenCount = brainTokens?.Count ?? 0;
            var confidence = tokenCount > 0 ? 1.0f : 0.0f;

            _snapshot = _snapshot with
            {
                Sequence = _sequence,
                Phase = "sensory-ingress",
                Mode = string.IsNullOrWhiteSpace(mode) ? "auto" : mode,
                LastUserText = text ?? string.Empty,
                LastIntent = "uninterpreted",
                LastMood = "none",
                TokenCount = tokenCount,
                Confidence = confidence,
                PendingClarification = false,
                ClarificationPrompt = string.Empty,
                LastGeneratedSpikes = 0,
                LastDeliveredSpikes = 0,
                LastErrorCount = 0,
                LastTick = tick
            };
            return _snapshot;
        }
    }

    public DialogueTurnSnapshot ObservePaused(string text, string mode, long tick, string reason)
    {
        lock (_gate)
        {
            _sequence++;
            _snapshot = _snapshot with
            {
                Sequence = _sequence,
                Phase = "paused",
                Mode = string.IsNullOrWhiteSpace(mode) ? "auto" : mode,
                LastUserText = text ?? string.Empty,
                LastIntent = "paused",
                LastMood = reason,
                TokenCount = 0,
                Confidence = 0f,
                PendingClarification = false,
                ClarificationPrompt = string.Empty,
                LastGeneratedSpikes = 0,
                LastDeliveredSpikes = 0,
                LastErrorCount = 0,
                PausedCount = _snapshot.PausedCount + 1,
                LastTick = tick
            };
            return _snapshot;
        }
    }

    public DialogueTurnSnapshot RecordDelivery(DialogueTurnSnapshot turn, int generatedSpikes, int deliveredSpikes, IReadOnlyCollection<string> errors, long tick)
    {
        lock (_gate)
        {
            var errorCount = errors?.Count ?? 0;
            var successful = deliveredSpikes > 0 && errorCount == 0;
            var partial = deliveredSpikes > 0 && errorCount > 0;
            var phase = successful ? "acting" : partial ? "repairing" : "blocked";
            var repairCount = _snapshot.RepairCount + (successful ? 0 : 1);
            var clarification = _snapshot.PendingClarification;
            var prompt = _snapshot.ClarificationPrompt;
            if (!successful && string.IsNullOrWhiteSpace(prompt))
            {
                clarification = true;
                prompt = "I could not route that fully; please repeat or simplify the command.";
            }

            _snapshot = turn with
            {
                Phase = phase,
                PendingClarification = clarification,
                ClarificationPrompt = prompt,
                LastGeneratedSpikes = generatedSpikes,
                LastDeliveredSpikes = deliveredSpikes,
                LastErrorCount = errorCount,
                SuccessfulTurnCount = _snapshot.SuccessfulTurnCount + (successful ? 1 : 0),
                RepairCount = repairCount,
                LastTick = tick
            };
            return _snapshot;
        }
    }

    public DialogueTurnSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _sequence = 0;
            _snapshot = DialogueTurnSnapshot.Empty;
        }
    }


}

internal readonly record struct DialogueTurnSnapshot(
    long Sequence,
    string Phase,
    string Mode,
    string LastUserText,
    string LastIntent,
    string LastMood,
    int TokenCount,
    float Confidence,
    bool PendingClarification,
    string ClarificationPrompt,
    int LastGeneratedSpikes,
    int LastDeliveredSpikes,
    int LastErrorCount,
    int SuccessfulTurnCount,
    int RepairCount,
    int PausedCount,
    long LastTick)
{
    public static DialogueTurnSnapshot Empty { get; } = new(
        0,
        "idle",
        "none",
        string.Empty,
        "none",
        "none",
        0,
        0f,
        false,
        string.Empty,
        0,
        0,
        0,
        0,
        0,
        0,
        0);
}


