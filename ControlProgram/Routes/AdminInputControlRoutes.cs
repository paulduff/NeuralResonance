using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NeuralResonanceEngine.Shared.Contracts;

internal static class AdminInputControlRoutes
{
    public static WebApplication MapAdminInputControlRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/admin/input-gates", GetInputGates);
        app.MapPost("/api/v1/admin/input-gates", SetInputGates);

        return app;
    }

    internal static IResult GetInputGates(SimulationState state)
    {
        return Results.Ok(state.GetInputGatesSnapshot());
    }

    internal static IResult SetInputGates(InputGateControlRequest request, SimulationState state)
    {
        if (!state.TrySetInputGates(request, out var runtime, out var error))
        {
            return Results.BadRequest(new
            {
                Error = error ?? "At least one setting is required: AvatarVisionEnabled or SpontaneousSpikingEnabled."
            });
        }

        state.AppendOutputLog(
            $"Input gates updated: avatarVision={runtime.AvatarVisionEnabled}, spontaneousSpiking={runtime.SpontaneousSpikingEnabled}.");
        return Results.Ok(new
        {
            Applied = true,
            InputGates = runtime
        });
    }

}
