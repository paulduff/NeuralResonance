using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal static class AdminTelemetryRoutes
{
    public static WebApplication MapAdminTelemetryRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/admin/language/prosody-telemetry", GetProsodyTelemetry);
        app.MapGet("/api/v1/admin/startup-health", GetStartupHealth);
        app.MapGet("/api/v1/admin/validation", GetValidation);
        app.MapGet("/api/v1/admin/runtime/http-profile", GetHttpProfile);
        app.MapGet("/api/v1/connectome/biological-report", GetBiologicalReport);
        app.MapGet("/api/v1/transport/spikes/recent", GetRecentTransportSpikes);
        app.MapGet("/api/v1/transport/stats", GetTransportStats);
        app.MapGet("/api/v1/performance/snapshot", GetPerformanceSnapshot);

        return app;
    }

    internal static IResult GetProsodyTelemetry(SimulationState state)
        => Results.Ok(state.GetProsodyTelemetrySnapshot());

    internal static IResult GetStartupHealth(SimulationState state, int? maxNonOkDetails)
        => Results.Ok(state.GetStartupHealth(Math.Clamp(maxNonOkDetails ?? 16, 1, 256)));

    internal static IResult GetValidation(SimulationState state, int? maxSnapshotAgeTicks, int? maxNonOkServices)
    {
        var snapshotAgeLimit = Math.Clamp(maxSnapshotAgeTicks ?? 20, 1, 10_000);
        var nonOkLimit = Math.Clamp(maxNonOkServices ?? 2, 0, 256);
        return Results.Ok(state.GetValidationSnapshot(snapshotAgeLimit, nonOkLimit));
    }

    internal static IResult GetHttpProfile(HttpRequestProfiler profiler, int? maxEndpoints, int? maxRecentSlow)
        => Results.Ok(profiler.GetSnapshot(maxEndpoints ?? 24, maxRecentSlow ?? 24));

    internal static IResult GetBiologicalReport(SimulationState state)
        => Results.Ok(state.GetBiologicalConnectomeReport());

    internal static IResult GetRecentTransportSpikes(SimulationState state, int? limit)
    {
        var requested = limit.GetValueOrDefault(256);
        var clamped = Math.Clamp(requested, 1, 1024);
        return Results.Ok(state.GetRecentDispatchedSpikes(clamped));
    }

    internal static IResult GetTransportStats(SimulationState state, InputIngressRuntime ingress)
        => Results.Ok(BuildPerformanceSnapshot(state, ingress));

    internal static IResult GetPerformanceSnapshot(SimulationState state, InputIngressRuntime ingress)
        => Results.Ok(BuildPerformanceSnapshot(state, ingress));

    private static object BuildPerformanceSnapshot(SimulationState state, InputIngressRuntime ingress)
    {
        var transport = state.TransportStats;
        var ingressSnapshot = ingress.GetSnapshot();
        var (totalServices, nonOkServices) = state.GetServiceHealthCounts();
        var snapshotAgeTicks = state.LastSnapshotTick > 0 && state.Tick >= state.LastSnapshotTick
            ? state.Tick - state.LastSnapshotTick
            : -1L;

        return new
        {
            tick = state.Tick,
            simulationMs = state.SimulationClockMs,
            simulationClockMs = state.SimulationClockMs,
            performanceProfileName = state.PerformanceProfileName,
            lastSnapshotTick = state.LastSnapshotTick,
            lastSnapshotSimulationMs = state.LastSnapshotSimulationMs,
            lastSnapshotWallClockUnixMs = state.LastSnapshotWallClockUnixMs,
            snapshotAgeTicks,
            serviceCount = totalServices,
            nonOkCount = nonOkServices,
            transport = new
            {
                tick = transport.Tick,
                activeServices = transport.ActiveServices,
                successfulAcks = transport.SuccessfulAcks,
                drainCalls = transport.DrainCalls,
                drainedSpikes = transport.DrainedSpikes,
                dispatchedSpikes = transport.DispatchedSpikes,
                droppedByBudget = transport.DroppedByBudget,
                topQueries = transport.TopQueries,
                spontaneousGenerated = transport.SpontaneousGenerated,
                spontaneousDelivered = transport.SpontaneousDelivered,
                spontaneousDispatchErrors = transport.SpontaneousDispatchErrors,
                spontaneousLastError = transport.SpontaneousLastError,
                totalSpontaneousGenerated = state.TotalSpontaneousGenerated,
                totalSpontaneousDelivered = state.TotalSpontaneousDelivered,
                totalSpontaneousDispatchErrors = state.TotalSpontaneousDispatchErrors,
                activePathways = transport.ActivePathways,
                dispatchQueueQueuedBatches = transport.DispatchQueueQueuedBatches,
                dispatchQueueQueuedSpikes = transport.DispatchQueueQueuedSpikes,
                dispatchQueuePeakBatches = transport.DispatchQueuePeakBatches,
                dispatchQueuePeakSpikes = transport.DispatchQueuePeakSpikes,
                dispatchQueueDroppedBatches = transport.DispatchQueueDroppedBatches,
                dispatchQueueDroppedSpikes = transport.DispatchQueueDroppedSpikes,
                dispatchQueueFlushedBatches = transport.DispatchQueueFlushedBatches,
                dispatchQueueFlushActiveTargets = transport.DispatchQueueFlushActiveTargets,
                dispatchQueueFlushMaxTargetBurstSpikes = transport.DispatchQueueFlushMaxTargetBurstSpikes,
                dispatchQueueDispatchErrors = transport.DispatchQueueDispatchErrors,
                dispatchQueueLastError = transport.DispatchQueueLastError,
                generatedSpikes = transport.GeneratedSpikes,
                routedSpikes = transport.RoutedSpikes,
                deliveredSpikes = transport.DeliveredSpikes,
                routeDroppedNoConnectivity = transport.RouteDroppedNoConnectivity,
                routeDroppedNoTargets = transport.RouteDroppedNoTargets,
                routeDroppedTargetUnavailable = transport.RouteDroppedTargetUnavailable,
                routeDroppedByBackpressure = transport.RouteDroppedByBackpressure,
                adaptivePressure = transport.AdaptivePressure,
                adaptiveScale = transport.AdaptiveScale,
                effectiveMaxSpikeDispatchPerServicePerTick = transport.EffectiveMaxSpikeDispatchPerServicePerTick,
                effectiveMaxSpikeDispatchTotalPerTick = transport.EffectiveMaxSpikeDispatchTotalPerTick,
                effectiveMaxTopQueriesPerTick = transport.EffectiveMaxTopQueriesPerTick,
                effectiveTickAckTimeoutMs = transport.EffectiveTickAckTimeoutMs,
                effectiveTickIoTimeoutMs = transport.EffectiveTickIoTimeoutMs,
                effectiveTickPublishWaitMs = transport.EffectiveTickPublishWaitMs,
                effectiveTickPublishSettleMs = transport.EffectiveTickPublishSettleMs,
                ackLatencyEwmaMs = transport.AckLatencyEwmaMs,
                tickWallMs = transport.TickWallMs,
                tickWallP50Ms = transport.TickWallP50Ms,
                tickWallP95Ms = transport.TickWallP95Ms,
                tickWallP99Ms = transport.TickWallP99Ms,
                degradeSignal = transport.DegradeSignal,
                sleepReplayStage = transport.SleepReplayStage,
                sleepInhibitoryScale = transport.SleepInhibitoryScale,
                sleepExcitatoryScale = transport.SleepExcitatoryScale,
                perceptionLanguageGenerated = transport.PerceptionLanguageGenerated,
                perceptionLanguageDelivered = transport.PerceptionLanguageDelivered,
                perceptionLanguageDispatchErrors = transport.PerceptionLanguageDispatchErrors
            },
            transportStats = new
            {
                tick = transport.Tick,
                activeServices = transport.ActiveServices,
                successfulAcks = transport.SuccessfulAcks,
                drainCalls = transport.DrainCalls,
                drainedSpikes = transport.DrainedSpikes,
                dispatchedSpikes = transport.DispatchedSpikes,
                droppedByBudget = transport.DroppedByBudget,
                topQueries = transport.TopQueries,
                spontaneousGenerated = transport.SpontaneousGenerated,
                spontaneousDelivered = transport.SpontaneousDelivered,
                spontaneousDispatchErrors = transport.SpontaneousDispatchErrors,
                spontaneousLastError = transport.SpontaneousLastError,
                totalSpontaneousGenerated = state.TotalSpontaneousGenerated,
                totalSpontaneousDelivered = state.TotalSpontaneousDelivered,
                totalSpontaneousDispatchErrors = state.TotalSpontaneousDispatchErrors,
                activePathways = transport.ActivePathways,
                dispatchQueueDroppedBatches = transport.DispatchQueueDroppedBatches,
                dispatchQueueDroppedSpikes = transport.DispatchQueueDroppedSpikes,
                dispatchQueueDispatchErrors = transport.DispatchQueueDispatchErrors,
                generatedSpikes = transport.GeneratedSpikes,
                routedSpikes = transport.RoutedSpikes,
                deliveredSpikes = transport.DeliveredSpikes,
                adaptivePressure = transport.AdaptivePressure,
                adaptiveScale = transport.AdaptiveScale,
                ackLatencyEwmaMs = transport.AckLatencyEwmaMs,
                tickWallMs = transport.TickWallMs,
                tickWallP50Ms = transport.TickWallP50Ms,
                tickWallP95Ms = transport.TickWallP95Ms,
                tickWallP99Ms = transport.TickWallP99Ms,
                degradeSignal = transport.DegradeSignal
            },
            services = new
            {
                total = totalServices,
                nonOk = nonOkServices
            },
            inputIngress = ingressSnapshot
        };
    }
}
