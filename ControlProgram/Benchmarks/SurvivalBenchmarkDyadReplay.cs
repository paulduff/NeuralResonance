using NeuralResonanceEngine.Shared.Contracts;

internal sealed record SurvivalBenchmarkDyadReplayRequest(
    SurvivalBenchmarkResult? Artifact,
    string? Policy = null,
    int? SampleEverySteps = null,
    int? MaxSamples = null,
    string? SessionId = null,
    string? CandidateKind = null);

internal sealed record SurvivalBenchmarkDyadReplayParameters(
    SurvivalBenchmarkResult Artifact,
    SurvivalBenchmarkEpisode Episode,
    string Policy,
    int SampleEverySteps,
    int MaxSamples,
    string SessionId,
    string CandidateKind);

internal sealed record SurvivalBenchmarkDyadReplayResult(
    string ProtocolVersion,
    string BenchmarkProtocolVersion,
    string BenchmarkName,
    string Policy,
    string SessionId,
    int StepsReplayed,
    bool ReplayVerified,
    string ReplayEvidence,
    IReadOnlyList<SurvivalBenchmarkDyadReplayTurn> Turns);

internal sealed record SurvivalBenchmarkDyadReplayTurn(
    int Step,
    long BrainTick,
    string RecordedPolicyAction,
    DyadEntityPromptSnapshot Prompt,
    bool EntityAvailable,
    bool UsedFallback,
    string Origin,
    string Text,
    string Detail,
    string EntityVersion,
    string EntityConfiguration,
    IReadOnlyList<string> SourceReferences,
    DyadLanguageCandidateResponse? Review);

internal static class SurvivalBenchmarkDyadReplay
{
    internal const string ProtocolVersion = "dyad.survival-replay.v1";
    private const int DefaultSampleEverySteps = 24;
    private const int DefaultMaxSamples = 4;

    public static bool TryNormalize(
        SurvivalBenchmarkDyadReplayRequest request,
        out SurvivalBenchmarkDyadReplayParameters? parameters,
        out string? error)
    {
        parameters = null;
        error = null;
        if (request.Artifact is null)
        {
            error = "A completed survival benchmark artifact is required.";
            return false;
        }

        var artifact = request.Artifact;
        if (!string.Equals(artifact.ProtocolVersion, DeterministicSurvivalBenchmark.ProtocolVersion, StringComparison.Ordinal))
        {
            error = $"Unsupported benchmark protocol '{artifact.ProtocolVersion}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(artifact.ArtifactFingerprint) ||
            !string.Equals(
                artifact.ArtifactFingerprint,
                DeterministicSurvivalBenchmark.BuildArtifactFingerprint(artifact),
                StringComparison.Ordinal))
        {
            error = "The benchmark artifact fingerprint is missing or invalid.";
            return false;
        }

        if (artifact.InitialBrainState is null || artifact.Episodes is null || artifact.Episodes.Count == 0)
        {
            error = "The benchmark artifact must include an initial brain state and at least one episode.";
            return false;
        }

        var policy = string.IsNullOrWhiteSpace(request.Policy)
            ? "control-state-intent"
            : request.Policy.Trim();
        if (string.Equals(policy, "current-dnne-intent", StringComparison.OrdinalIgnoreCase))
        {
            policy = "control-state-intent";
        }
        var episode = artifact.Episodes.FirstOrDefault(candidate =>
            string.Equals(candidate.Policy, policy, StringComparison.OrdinalIgnoreCase));
        if (episode is null)
        {
            error = $"The artifact has no episode for policy '{policy}'.";
            return false;
        }

        if (episode.Steps is null || episode.Steps.Count == 0)
        {
            error = $"The '{episode.Policy}' episode has no recorded steps to replay.";
            return false;
        }

		for (var index = 0; index < episode.Steps.Count; index++)
		{
			var step = episode.Steps[index];
			if (step.Step != index + 1)
			{
				error = $"Benchmark step sequence is invalid at index {index}.";
				return false;
			}
			if (index > 0 && episode.Steps[index - 1].WorldAfterAction != step.WorldBeforeAction)
			{
				error = $"Benchmark world-state chain is broken before step {step.Step}.";
				return false;
			}
		}

        var sampleEverySteps = request.SampleEverySteps ?? DefaultSampleEverySteps;
        if (sampleEverySteps is < 1 or > 512)
        {
            error = "sampleEverySteps must be between 1 and 512.";
            return false;
        }

        var maxSamples = request.MaxSamples ?? DefaultMaxSamples;
        if (maxSamples is < 1 or > 12)
        {
            error = "maxSamples must be between 1 and 12.";
            return false;
        }

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? $"survival-{artifact.Seed}-{episode.Policy}"
            : request.SessionId.Trim();
        if (sessionId.Length > 128)
        {
            error = "sessionId must not exceed 128 characters.";
            return false;
        }

        var candidateKind = string.IsNullOrWhiteSpace(request.CandidateKind)
            ? "interpretation"
            : request.CandidateKind.Trim();
        var probe = new DyadEntityGenerationRequest(
            DyadLanguageContract.ProtocolVersion,
            sessionId,
            "survival-replay-probe",
            candidateKind,
            "read-only recorded survival replay");
        if (!DyadLanguageContract.TryNormalizeGeneration(probe, out var normalizedGeneration, out error) || normalizedGeneration is null)
        {
            return false;
        }

        parameters = new SurvivalBenchmarkDyadReplayParameters(
            artifact,
            episode,
            episode.Policy,
            sampleEverySteps,
            maxSamples,
            normalizedGeneration.SessionId,
            normalizedGeneration.CandidateKind);
        return true;
    }

    public static async Task<SurvivalBenchmarkDyadReplayResult> EvaluateAsync(
        SurvivalBenchmarkDyadReplayParameters parameters,
        IEntityLanguageClient entityClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(entityClient);

        var replayState = DeterministicSurvivalBenchmark.CreateIsolatedState(parameters.Artifact.InitialBrainState);
        var selectedSteps = SelectSteps(
            parameters.Episode.Steps,
            parameters.SampleEverySteps,
            parameters.MaxSamples);
        var selectedNumbers = selectedSteps.Select(step => step.Step).ToHashSet();
        var turns = new List<SurvivalBenchmarkDyadReplayTurn>(selectedSteps.Count);

        foreach (var record in parameters.Episode.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeterministicSurvivalBenchmark.ReplayRecordedStep(
                replayState,
                record,
                !string.Equals(parameters.Policy, "no-learning-stationary", StringComparison.OrdinalIgnoreCase));
            if (!selectedNumbers.Contains(record.Step))
            {
                continue;
            }

            var generation = new DyadEntityGenerationParameters(
                DyadLanguageContract.ProtocolVersion,
                parameters.SessionId,
                $"step-{record.Step}",
                parameters.CandidateKind,
                $"read-only survival replay at recorded step {record.Step}; action already happened and cannot be changed");
            var prompt = replayState.CreateDyadEntityPrompt(generation);
            var entity = await entityClient.GenerateAsync(prompt, cancellationToken);
            turns.Add(CreateTurn(replayState, record, generation, prompt, entity));
        }

        var finalTick = parameters.Episode.Steps[^1].BrainTick;
        var replayFingerprint = DeterministicSurvivalBenchmark.BuildBenchmarkStateFingerprint(replayState.ExportNetworkState());
        var expectedFingerprint = parameters.Episode.FinalBrainSnapshot.StateFingerprint;
        var replayVerified = replayState.Tick == finalTick &&
                             string.Equals(replayFingerprint, expectedFingerprint, StringComparison.Ordinal);
        return new SurvivalBenchmarkDyadReplayResult(
            ProtocolVersion,
            parameters.Artifact.ProtocolVersion,
            parameters.Artifact.BenchmarkName,
            parameters.Policy,
            parameters.SessionId,
            parameters.Episode.Steps.Count,
            replayVerified,
            replayVerified
                ? "Recorded feedback replayed into a private DNNE state and matched the complete final-state fingerprint."
                : $"Replay mismatch: tick {replayState.Tick}/{finalTick}; fingerprint {replayFingerprint}/{expectedFingerprint}.",
            turns);
    }

    private static SurvivalBenchmarkDyadReplayTurn CreateTurn(
        SimulationState replayState,
        SurvivalBenchmarkStepRecord record,
        DyadEntityGenerationParameters generation,
        DyadEntityPromptSnapshot prompt,
        EntityLanguageCandidateResult entity)
    {
        if (!entity.IsAvailable)
        {
            return CreateFallback(record, prompt, entity.Detail);
        }

        var candidate = new DyadLanguageCandidateRequest(
            DyadLanguageContract.ProtocolVersion,
            generation.SessionId,
            generation.TurnId,
            entity.EntityVersion,
            entity.EntityConfiguration,
            prompt.PromptFingerprint,
            prompt.PromptText,
            generation.CandidateKind,
            entity.CandidateText,
            entity.SourceReferences);
        if (!DyadLanguageContract.TryNormalize(candidate, out var proposal, out var error) || proposal is null)
        {
            return CreateFallback(
                record,
                prompt,
                $"Entity candidate failed DNNE contract validation: {error ?? "unknown error"}");
        }

        var review = replayState.ReviewDyadLanguageCandidate(proposal);
        var emitted = review.Decision == DyadLanguageCandidateDecision.AcceptedForEmission;
        return new SurvivalBenchmarkDyadReplayTurn(
            record.Step,
            record.BrainTick,
            record.PolicyAction,
            prompt,
            EntityAvailable: true,
            UsedFallback: false,
            Origin: emitted ? "entity" : "entity-deferred",
            Text: emitted ? proposal.CandidateText : string.Empty,
            Detail: emitted ? entity.Detail : review.DecisionReason,
            proposal.EntityVersion,
            proposal.EntityConfiguration,
            proposal.SourceReferences,
            review);
    }

    private static SurvivalBenchmarkDyadReplayTurn CreateFallback(
        SurvivalBenchmarkStepRecord record,
        DyadEntityPromptSnapshot prompt,
        string detail)
    {
        var emitted = !prompt.Grounding.IsSleeping &&
                      prompt.Grounding.SpeechEligible &&
                      string.Equals(prompt.Grounding.SpeechMode, "speakable", StringComparison.OrdinalIgnoreCase) &&
                      !string.IsNullOrWhiteSpace(prompt.FallbackText);
        return new(
            record.Step,
            record.BrainTick,
            record.PolicyAction,
            prompt,
            EntityAvailable: false,
            UsedFallback: true,
            Origin: emitted ? "dnne-fallback" : "dnne-deferred",
            Text: emitted ? prompt.FallbackText : string.Empty,
            Detail: string.IsNullOrWhiteSpace(detail) ? "Entity candidate is unavailable." : detail,
            EntityVersion: "unavailable",
            EntityConfiguration: "unavailable",
            SourceReferences: Array.Empty<string>(),
            Review: null);
    }

    private static IReadOnlyList<SurvivalBenchmarkStepRecord> SelectSteps(
        IReadOnlyList<SurvivalBenchmarkStepRecord> records,
        int sampleEverySteps,
        int maxSamples)
    {
        var selected = new List<SurvivalBenchmarkStepRecord>();
        void Add(SurvivalBenchmarkStepRecord record)
        {
            if (!selected.Any(existing => existing.Step == record.Step))
            {
                selected.Add(record);
            }
        }

        Add(records[0]);
        foreach (var record in records)
        {
            if (record.Step % sampleEverySteps == 0)
            {
                Add(record);
            }
        }

        Add(records[^1]);
        selected.Sort(static (left, right) => left.Step.CompareTo(right.Step));
        if (selected.Count <= maxSamples)
        {
            return selected;
        }

        var bounded = selected.Take(Math.Max(0, maxSamples - 1)).ToList();
        var terminal = selected[^1];
        if (!bounded.Any(record => record.Step == terminal.Step))
        {
            bounded.Add(terminal);
        }

        return bounded;
    }
}
