using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NeuralResonanceEngine.Shared.Contracts;

internal static class DyadLanguageRoutes
{
    public static WebApplication MapDyadLanguageRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/dyad/language/candidates", PostCandidate);
        app.MapGet("/api/v1/dyad/language/reviews", GetReviews);
        app.MapGet("/api/v1/dyad/language/adapter-training", GetAdapterTraining);

        return app;
    }

    internal static IResult PostCandidate(DyadLanguageCandidateRequest request, SimulationState state)
    {
        if (!DyadLanguageContract.TryNormalize(request, out var proposal, out var error) || proposal is null)
        {
            return Results.BadRequest(new { Error = error ?? "Invalid Dyad language candidate." });
        }

        var review = state.ReviewDyadLanguageCandidate(proposal);
        state.AppendOutputLog(
            $"Dyad language candidate reviewed: session={proposal.SessionId}, turn={proposal.TurnId}, " +
            $"kind={proposal.CandidateKind}, decision={review.Decision}, sequence={review.ReviewSequence}.");
        return Results.Ok(review);
    }

    internal static IResult GetReviews(SimulationState state, int? limit)
        => Results.Ok(state.GetDyadLanguageCandidateReviews(Math.Clamp(limit ?? 32, 1, 256)));

    internal static IResult GetAdapterTraining(SimulationState state, int? limit)
    {
        ArgumentNullException.ThrowIfNull(state);
        var reviews = state.GetDyadLanguageCandidateReviews(Math.Clamp(limit ?? 256, 1, 256));
        return Results.Ok(DyadPopulationLanguageTrainingContract.CreateDataset(reviews));
    }
}
