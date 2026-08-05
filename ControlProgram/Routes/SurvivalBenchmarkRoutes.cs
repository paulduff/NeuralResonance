using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal static class SurvivalBenchmarkRoutes
{
    public static WebApplication MapSurvivalBenchmarkRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost("/api/v1/admin/benchmarks/survival/run", PostRun);
        app.MapPost("/api/v1/admin/benchmarks/survival/dyad-replay", PostDyadReplay);
        return app;
    }

    internal static async Task<IResult> PostRun(
        SurvivalBenchmarkRequest? request,
        SimulationState state,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Results.BadRequest(new { Error = "Benchmark request payload is required." });
        }

        if (!DeterministicSurvivalBenchmark.TryNormalize(request, out var normalized, out var error) || normalized is null)
        {
            return Results.BadRequest(new { Error = error ?? "Invalid survival benchmark request." });
        }

        // The benchmark works on a private imported state. It never advances or mutates
        // the currently running control-program state.
        var suppliedState = normalized.InitialBrainState is not null;
        var initialState = normalized.InitialBrainState ?? state.ExportNetworkState();
        try
        {
            var result = await Task.Run(
                () => DeterministicSurvivalBenchmark.Run(
                    normalized,
                    initialState,
                    suppliedState ? "request-snapshot" : "control-state-snapshot",
                    cancellationToken),
                cancellationToken);
            return Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { Error = exception.Message });
        }
    }

    internal static async Task<IResult> PostDyadReplay(
        SurvivalBenchmarkDyadReplayRequest? request,
        IEntityLanguageClient entityClient,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { Error = "Dyad replay request payload is required." });
        }

        if (!SurvivalBenchmarkDyadReplay.TryNormalize(request, out var parameters, out var error) || parameters is null)
        {
            return Results.BadRequest(new { Error = error ?? "Invalid Dyad replay request." });
        }

        try
        {
            var result = await SurvivalBenchmarkDyadReplay.EvaluateAsync(parameters, entityClient, cancellationToken);
            return Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { Error = exception.Message });
        }
    }
}
