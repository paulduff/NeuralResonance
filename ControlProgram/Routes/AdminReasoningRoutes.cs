using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal static class AdminReasoningRoutes
{
    public static WebApplication MapAdminReasoningRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/admin/reasoning/curriculum", GetCurriculum);
        app.MapPost("/api/v1/admin/reasoning/curriculum", PostCurriculum);

        return app;
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

}
