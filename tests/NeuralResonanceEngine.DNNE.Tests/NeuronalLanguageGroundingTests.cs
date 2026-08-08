using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalLanguageGroundingTests
{
    [Fact]
    public void CompleteCircuitGroundsNumericReferenceAndAuthorizesSpeech()
    {
        var decision = Decode(
            perceptEnsemble: 3,
            memoryEnsemble: 3);

        Assert.True(decision.CircuitObserved);
        Assert.True(decision.Available);
        Assert.True(decision.Grounded);
        Assert.True(decision.SpeechAuthorized);
        Assert.Equal(3, decision.PerceptEnsemble);
        Assert.Equal(3, decision.MemoryEnsemble);
        Assert.Equal(NeuronalLanguageGroundingDecoder.LanguageAttentionChannel, decision.AttentionChannel);
        Assert.InRange(decision.GroundingConfidence, 0.20, 1.0);
        Assert.InRange(decision.Uncertainty, 0.0, 0.70);
        Assert.Contains(decision.Sources, static source => source.SourceId == "neuronal-percept-ensemble");
        Assert.Contains(decision.Sources, static source => source.SourceId == "persisted-synaptic-recall");
        Assert.DoesNotContain(decision.Sources, static source => source.SourceId.Contains("annotation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GroundingDecisionHasNoSemanticLabelSurface()
    {
        var propertyNames = typeof(NeuronalLanguageGroundingDecision)
            .GetProperties()
            .Select(static property => property.Name)
            .ToArray();

        Assert.DoesNotContain("GroundedLabel", propertyNames);
        Assert.DoesNotContain(propertyNames, name => name.Contains("Object", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Semantic", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(StructureId.WernickePstgPsts)]
    [InlineData(StructureId.ArcuateFasciculus)]
    public void ComprehensionLesionRemovesGroundingAuthority(StructureId lesion)
    {
        var decision = Decode(
            snapshots: CreateLanguageCircuit().Where(snapshot => snapshot.StructureId != lesion).ToArray());

        Assert.True(decision.CircuitObserved);
        Assert.False(decision.Available);
        Assert.False(decision.Grounded);
        Assert.False(decision.SpeechAuthorized);
    }

    [Theory]
    [InlineData(StructureId.BrocaBa44Ba45)]
    [InlineData(StructureId.PremotorCortex)]
    [InlineData(StructureId.MotorThalamus)]
    public void ExpressionLesionPreservesReferenceButClosesSpeech(StructureId lesion)
    {
        var decision = Decode(
            snapshots: CreateLanguageCircuit().Where(snapshot => snapshot.StructureId != lesion).ToArray());

        Assert.True(decision.Available);
        Assert.True(decision.Grounded);
        Assert.False(decision.SpeechAuthorized);
    }

    [Fact]
    public void NonLanguageAttentionOrSleepClosesSpeech()
    {
        var visualAttention = Decode(attentionChannel: 0);
        var sleeping = Decode(sleeping: true);

        Assert.True(visualAttention.Grounded);
        Assert.False(visualAttention.SpeechAuthorized);
        Assert.True(sleeping.Grounded);
        Assert.True(sleeping.IsSleeping);
        Assert.False(sleeping.SpeechAuthorized);
    }

    [Fact]
    public void ConflictingRecallRaisesUncertaintyAndPreventsEmission()
    {
        var agreeing = Decode(perceptEnsemble: 4, memoryEnsemble: 4);
        var conflicting = Decode(perceptEnsemble: 4, memoryEnsemble: 7);

        Assert.True(conflicting.Uncertainty > agreeing.Uncertainty);
        Assert.True(conflicting.GroundingConfidence < agreeing.GroundingConfidence);
        Assert.False(conflicting.SpeechAuthorized);
    }

    [Fact]
    public void IssuedGroundedCandidateCanEmitWithoutChangingMotorState()
    {
        var state = CreateState();
        var motorBefore = state.GetNeuronalMotorSnapshot();
        state.UpdateNeuronalLanguageGrounding(Decode());
        var parameters = new DyadEntityGenerationParameters(
            DyadLanguageContract.ProtocolVersion,
            "grounded-session",
            "turn-1",
            "utterance",
            "report current neuronal reference");
        var prompt = state.CreateDyadEntityPrompt(parameters);
        var request = new DyadLanguageCandidateRequest(
            DyadLanguageContract.ProtocolVersion,
            parameters.SessionId,
            parameters.TurnId,
            "entity-test",
            "test",
            prompt.PromptFingerprint,
            prompt.PromptText,
            parameters.CandidateKind,
            "I can report the grounded reference.",
            []);
        Assert.True(DyadLanguageContract.TryNormalize(request, out var proposal, out var error), error);

        var review = state.ReviewDyadLanguageCandidate(proposal!);

        Assert.Equal(DyadLanguageCandidateDecision.AcceptedForEmission, review.Decision);
        Assert.True(review.Grounding.NeuronalGrounded);
        Assert.True(review.Grounding.NeuronalSpeechAuthorized);
        Assert.Equal(NeuronalLanguageGroundingDecision.Authority, review.Grounding.Authority);
        Assert.Equal(motorBefore, state.GetNeuronalMotorSnapshot());
    }

    [Fact]
    public void IncompleteObservedCircuitCannotUseLegacyLanguageTelemetry()
    {
        var state = CreateState();
        var incomplete = Decode(
            snapshots: [CreateLanguageCircuit().Single(static snapshot => snapshot.StructureId == StructureId.A1)]);
        state.UpdateNeuronalLanguageGrounding(incomplete);

        var prompt = state.CreateDyadEntityPrompt(new DyadEntityGenerationParameters(
            DyadLanguageContract.ProtocolVersion,
            "incomplete-session",
            "turn-1",
            "utterance",
            "test incomplete circuit"));

        Assert.True(prompt.Grounding.NeuronalCircuitObserved);
        Assert.False(prompt.Grounding.NeuronalGroundingAvailable);
        Assert.False(prompt.Grounding.NeuronalSpeechAuthorized);
        Assert.DoesNotContain(
            prompt.Grounding.Sources,
            static source => source.SourceId == "prefrontal-working-memory");
    }

    [Fact]
    public void CandidateCannotBorrowGroundingAcquiredAfterItsPromptWasIssued()
    {
        var state = CreateState();
        var incomplete = Decode(
            snapshots: [CreateLanguageCircuit().Single(static snapshot => snapshot.StructureId == StructureId.A1)]);
        state.UpdateNeuronalLanguageGrounding(incomplete);
        var parameters = new DyadEntityGenerationParameters(
            DyadLanguageContract.ProtocolVersion,
            "stale-grounding-session",
            "turn-1",
            "utterance",
            "test prompt binding");
        var prompt = state.CreateDyadEntityPrompt(parameters);

        state.UpdateNeuronalLanguageGrounding(Decode());
        var request = new DyadLanguageCandidateRequest(
            DyadLanguageContract.ProtocolVersion,
            parameters.SessionId,
            parameters.TurnId,
            "entity-test",
            "test",
            prompt.PromptFingerprint,
            prompt.PromptText,
            parameters.CandidateKind,
            "This must remain deferred.",
            []);
        Assert.True(DyadLanguageContract.TryNormalize(request, out var proposal, out var error), error);

        var review = state.ReviewDyadLanguageCandidate(proposal!);

        Assert.Equal(DyadLanguageCandidateDecision.Deferred, review.Decision);
        Assert.False(review.Grounding.NeuronalGroundingAvailable);
        Assert.False(review.Grounding.NeuronalSpeechAuthorized);
    }

    private static NeuronalLanguageGroundingDecision Decode(
        int perceptEnsemble = 3,
        int memoryEnsemble = 3,
        int attentionChannel = NeuronalLanguageGroundingDecoder.LanguageAttentionChannel,
        bool sleeping = false,
        IReadOnlyList<InstanceStructureSnapshot>? snapshots = null)
    {
        var percept = new NeuronalPerceptDecision(
            true,
            true,
            perceptEnsemble,
            0.25,
            0.82,
            1.0,
            0.72,
            0.12,
            []);
        var memory = new NeuronalMemoryDecision(
            true,
            true,
            memoryEnsemble,
            0.74,
            0.20,
            0.80,
            0.76,
            0.03,
            0.01,
            0.62,
            0.38,
            48,
            true,
            []);
        var scores = Enumerable.Range(0, 7)
            .Select(channel => channel == attentionChannel ? 0.88 : 0.08)
            .ToArray();
        var attention = new NeuronalAttentionWorkspaceDecision(
            true,
            true,
            attentionChannel,
            0.80,
            scores,
            [attentionChannel],
            1,
            true,
            attentionChannel,
            0.70,
            1.0,
            []);
        var sleep = sleeping
            ? new NeuronalSleepConsolidationDecision(
                true,
                true,
                true,
                NeuronalSleepState.Nrem,
                0.90,
                [0.05, 0.90, 0.05],
                1.0,
                false,
                -1,
                0.0,
                0.0,
                1.0,
                0.8,
                0.8,
                0.0,
                [])
            : NeuronalSleepConsolidationDecision.Unavailable;
        return NeuronalLanguageGroundingDecoder.Decode(
            42,
            percept,
            memory,
            attention,
            sleep,
            snapshots ?? CreateLanguageCircuit());
    }

    private static IReadOnlyList<InstanceStructureSnapshot> CreateLanguageCircuit()
        =>
        [
            LanguageSnapshot(StructureId.A1),
            LanguageSnapshot(StructureId.WernickePstgPsts),
            LanguageSnapshot(StructureId.ArcuateFasciculus),
            LanguageSnapshot(StructureId.BrocaBa44Ba45),
            LanguageSnapshot(StructureId.PremotorCortex),
            LanguageSnapshot(StructureId.M1),
            LanguageSnapshot(StructureId.Striatum),
            LanguageSnapshot(StructureId.MotorThalamus)
        ];

    private static InstanceStructureSnapshot LanguageSnapshot(StructureId structure, float drive = 12f)
    {
        var diagnostic = new AuditoryLanguageMotorDiagnostics(
            "Integrated",
            drive,
            drive,
            drive,
            drive,
            drive,
            drive,
            drive,
            drive,
            drive);
        return new InstanceStructureSnapshot(
            new ServiceInstance(structure, $"{structure}-M", "M", new Uri("http://localhost")),
            structure,
            64,
            drive,
            BrainRhythm.GAMMA,
            [],
            new NeuromodState(),
            0,
            0,
            0,
            AuditoryLanguageMotorDiagnostics: diagnostic);
    }

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        state.AdvanceClockAndCreateTickSignal();
        return state;
    }
}
