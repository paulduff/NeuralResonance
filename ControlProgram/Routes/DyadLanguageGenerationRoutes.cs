using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NeuralResonanceEngine.Shared.Contracts;

internal static class DyadLanguageGenerationRoutes
{
    public static WebApplication MapDyadLanguageGenerationRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost("/api/v1/dyad/language/generate", PostGenerate);
        return app;
    }

    internal static async Task<IResult> PostGenerate(
        DyadEntityGenerationRequest request,
        SimulationState state,
        IEntityLanguageClient entityClient,
        CancellationToken cancellationToken)
    {
        if (!DyadLanguageContract.TryNormalizeGeneration(request, out var parameters, out var error) || parameters is null)
        {
            return Results.BadRequest(new { Error = error ?? "Invalid Dyad Entity generation request." });
        }

        var prompt = state.CreateDyadEntityPrompt(parameters);
        if (prompt.Grounding.IsSleeping)
        {
            return CreateDeferred(
                state,
                parameters,
                "DNNE is sleeping, so Entity was not called.");
        }

        var entity = await entityClient.GenerateAsync(prompt, cancellationToken);
        if (!entity.IsAvailable)
        {
            return CreateDeferred(state, parameters, entity.Detail);
        }

        var candidateRequest = new DyadLanguageCandidateRequest(
            DyadLanguageContract.ProtocolVersion,
            parameters.SessionId,
            parameters.TurnId,
            entity.EntityVersion,
            entity.EntityConfiguration,
            prompt.PromptFingerprint,
            prompt.PromptText,
            parameters.CandidateKind,
            entity.CandidateText,
            entity.SourceReferences);
        if (!DyadLanguageContract.TryNormalize(candidateRequest, out var proposal, out var candidateError) || proposal is null)
        {
            return CreateDeferred(state, parameters, $"Entity candidate failed DNNE contract validation: {candidateError}");
        }

        var review = state.ReviewDyadLanguageCandidate(proposal);
        state.AppendOutputLog(
            $"Dyad Entity candidate reviewed: session={parameters.SessionId}, turn={parameters.TurnId}, " +
            $"decision={review.Decision}, sequence={review.ReviewSequence}.");
        var emitted = review.Decision == DyadLanguageCandidateDecision.AcceptedForEmission;
        return Results.Ok(new DyadEntityGenerationResponse(
            DyadLanguageContract.ProtocolVersion,
            parameters.SessionId,
            parameters.TurnId,
            EntityAvailable: true,
            Origin: emitted ? "entity" : "entity-deferred",
            Text: emitted ? proposal.CandidateText : string.Empty,
            Detail: emitted ? entity.Detail : review.DecisionReason,
            Review: review,
            Emitted: emitted,
            CandidateText: proposal.CandidateText));
    }

    private static IResult CreateDeferred(
        SimulationState state,
        DyadEntityGenerationParameters parameters,
        string detail)
    {
        state.AppendOutputLog(
            $"Dyad Entity deferred: session={parameters.SessionId}, turn={parameters.TurnId}, detail={detail}");
        return Results.Ok(new DyadEntityGenerationResponse(
            DyadLanguageContract.ProtocolVersion,
            parameters.SessionId,
            parameters.TurnId,
            EntityAvailable: false,
            Origin: "entity-deferred",
            Text: string.Empty,
            Detail: detail,
            Review: null,
            Emitted: false,
            CandidateText: string.Empty));
    }
}
