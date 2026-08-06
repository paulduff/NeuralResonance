using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NeuralResonanceEngine.ControlProgram.Services;
using NeuralResonanceEngine.Shared.Contracts;

internal static class NavigationRoutes
{
    public static WebApplication MapNavigationRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost("/api/v1/navigation/decision", PostDecision);
        return app;
    }

    internal static IResult PostDecision(
        HippocampalNavigationControlRequest? request,
        HippocampalNavigationSessionManager sessions)
    {
        if (request is null)
        {
            return Results.BadRequest(new { Error = "Navigation request payload is required." });
        }

        HippocampalNavigationObservation observation = request.Observation;
        if (observation.Row < 0 || observation.Column < 0 ||
            observation.GoalRow < 0 || observation.GoalColumn < 0)
        {
            return Results.BadRequest(new { Error = "Navigation cell coordinates must be non-negative." });
        }

        if (!double.IsFinite(request.HeadingDeg) ||
            !double.IsFinite(request.CellOffsetX) ||
            !double.IsFinite(request.CellOffsetZ) ||
            !double.IsFinite(observation.GoalBearingDeg) ||
            !double.IsFinite(observation.DistanceToGoal) ||
            observation.DistanceToGoal < 0.0)
        {
            return Results.BadRequest(new { Error = "Navigation heading, cell offsets, bearing, and distance must be finite; distance cannot be negative." });
        }

        if (Math.Abs(request.CellOffsetX) > 0.75 || Math.Abs(request.CellOffsetZ) > 0.75)
        {
            return Results.BadRequest(new { Error = "Navigation cell offsets must remain within the observed cell." });
        }

        bool atGoal = observation.GoalReached ||
                      (observation.Row == observation.GoalRow && observation.Column == observation.GoalColumn);
        bool hasOpenExit = observation.ForwardOpen || observation.LeftOpen || observation.RightOpen || observation.RearOpen;
        if (!atGoal && !hasOpenExit)
        {
            return Results.BadRequest(new { Error = "A non-goal navigation observation must expose at least one traversable exit." });
        }

        return Results.Json(new
        {
            Error = "The goal-directed spatial navigator is retired from runtime authority. Use sensory input and neuronal motor output.",
            Authority = "LegacyOfflineBenchmark",
            CanAuthorizeMovement = false,
            AuthoritativeEndpoint = "/api/v1/neuronal-motor"
        }, statusCode: StatusCodes.Status410Gone);
    }
}
