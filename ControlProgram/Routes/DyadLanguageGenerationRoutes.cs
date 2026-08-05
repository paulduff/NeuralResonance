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
            return CreateFallback(
                state,
                parameters,
                prompt,
                "DNNE is sleeping, so Entity was not called.");
        }

        var entity = await entityClient.GenerateAsync(prompt, cancellationToken);
        if (!entity.IsAvailable)
        {
            return CreateFallback(state, parameters, prompt, entity.Detail);
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
            return CreateFallback(state, parameters, prompt, $"Entity candidate failed DNNE contract validation: {candidateError}");
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
            UsedFallback: false,
            Origin: emitted ? "entity" : "entity-deferred",
            Text: emitted ? proposal.CandidateText : string.Empty,
            Detail: emitted ? entity.Detail : review.DecisionReason,
            Review: review)
        {
            Emitted = emitted,
            CandidateText = proposal.CandidateText
        });
    }

    private static IResult CreateFallback(
        SimulationState state,
        DyadEntityGenerationParameters parameters,
        DyadEntityPromptSnapshot prompt,
        string detail)
    {
        state.AppendOutputLog(
            $"Dyad Entity fallback: session={parameters.SessionId}, turn={parameters.TurnId}, detail={detail}");
        var emitted = !prompt.Grounding.IsSleeping &&
                      prompt.Grounding.SpeechEligible &&
                      string.Equals(prompt.Grounding.SpeechMode, "speakable", StringComparison.OrdinalIgnoreCase) &&
                      !string.IsNullOrWhiteSpace(prompt.FallbackText);
        return Results.Ok(new DyadEntityGenerationResponse(
            DyadLanguageContract.ProtocolVersion,
            parameters.SessionId,
            parameters.TurnId,
            EntityAvailable: false,
            UsedFallback: true,
            Origin: emitted ? "dnne-fallback" : "dnne-deferred",
            Text: emitted ? prompt.FallbackText : string.Empty,
            Detail: detail,
            Review: null)
        {
            Emitted = emitted,
            CandidateText = prompt.FallbackText
        });
    }
}
