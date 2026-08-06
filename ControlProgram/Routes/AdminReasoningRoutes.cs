using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal static class AdminReasoningRoutes
{
    public static WebApplication MapAdminReasoningRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/admin/reasoning/schemas", GetSchemas);
        app.MapGet("/api/v1/admin/reasoning/world-model", GetWorldModel);
        app.MapPost("/api/v1/admin/reasoning/counterfactual", PostCounterfactual);
        app.MapGet("/api/v1/admin/reasoning/curriculum", GetCurriculum);
        app.MapPost("/api/v1/admin/reasoning/curriculum", PostCurriculum);
        app.MapGet("/api/v1/admin/reasoning/consolidation", GetConsolidation);
        app.MapPost("/api/v1/admin/reasoning/consolidation", PostConsolidation);

        return app;
    }

    internal static IResult GetSchemas(SimulationState state, int? limit)
        => Results.Ok(state.GetRelationalSchemaSnapshot(limit ?? 64));

    internal static IResult GetWorldModel(SimulationState state, int? limit)
        => Results.Ok(state.GetWorldModelSnapshot(limit ?? 64));

    internal static IResult PostCounterfactual(WorldModelCounterfactualRequest request, SimulationState state)
    {
        if (request is null)
        {
            return Results.BadRequest(new { Error = "Request payload is required." });
        }

        return Results.Ok(state.EvaluateCounterfactual(request));
    }

    internal static IResult GetCurriculum(SimulationState state)
        => Results.Ok(state.GetCurriculumSnapshot());

    internal static IResult PostCurriculum(CurriculumControlRequest request, SimulationState state)
    {
        if (request is null)
        {
            return Results.BadRequest(new { Error = "Request payload is required." });
        }

        if (!state.TrySetCurriculumControl(request, out var runtime, out var error))
        {
            return Results.BadRequest(new { Error = error ?? "Unable to update curriculum settings." });
        }

        return Results.Ok(new
        {
            Applied = true,
            Curriculum = runtime
        });
    }

    internal static IResult GetConsolidation(SimulationState state)
        => Results.Ok(state.GetConsolidationControlSnapshot());

    internal static IResult PostConsolidation(ConsolidationControlRequest request, SimulationState state)
    {
        if (request is null)
        {
            return Results.BadRequest(new { Error = "Request payload is required." });
        }

        if (!state.TrySetConsolidationControl(request, out var settings, out var error))
        {
            return Results.BadRequest(new { Error = error ?? "Unable to update consolidation settings." });
        }

        return Results.Ok(new
        {
            Applied = true,
            Consolidation = settings
        });
    }
}
