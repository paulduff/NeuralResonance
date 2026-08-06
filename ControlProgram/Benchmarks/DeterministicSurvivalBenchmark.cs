using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record SurvivalBenchmarkRequest(
    int? Seed = null,
    int? Steps = null,
    IReadOnlyList<string>? Policies = null,
    NetworkStateDocument? InitialBrainState = null);

internal sealed record SurvivalBenchmarkResult(
    string ProtocolVersion,
    string BenchmarkName,
    int Seed,
    int RequestedSteps,
    SurvivalBenchmarkBrainSnapshotDescriptor InitialBrainSnapshot,
    NetworkStateDocument InitialBrainState,
    SurvivalBenchmarkWorldSnapshot InitialWorld,
    IReadOnlyList<SurvivalBenchmarkEpisode> Episodes)
{
    public string ArtifactFingerprint { get; init; } = string.Empty;
}

internal sealed record SurvivalBenchmarkBrainSnapshotDescriptor(
    string Source,
    int SchemaVersion,
    long Tick,
    double SimulationClockMs,
    double TickDurationMs,
    string StateFingerprint);

internal sealed record SurvivalBenchmarkEpisode(
    string Policy,
    string PolicyKind,
    int Seed,
    int StepsExecuted,
    bool Success,
    string TerminalCondition,
    SurvivalBenchmarkMetrics Metrics,
    SurvivalBenchmarkWorldSnapshot FinalWorld,
    SurvivalBenchmarkBrainSnapshotDescriptor FinalBrainSnapshot,
    IReadOnlyList<SurvivalBenchmarkStepRecord> Steps);

internal sealed record SurvivalBenchmarkMetrics(
    int FoodCollected,
    int ShelterVisits,
    int ThreatContacts,
    int UniqueCellsVisited,
    float TotalDamage,
    float MeanHealth,
    float MeanHunger,
    float MeanThreat,
    float MeanIntentConfidence,
    int IntentDrivenActions,
    int FallbackActions);

internal sealed record SurvivalBenchmarkStepRecord(
    int Step,
    long BrainTick,
    SurvivalBenchmarkWorldSnapshot WorldBeforeAction,
    string PolicyAction,
    string DnneMotorDirective,
    string DnneGoalKey,
    string DnneActionKey,
    float DnneIntentConfidence,
    bool UsedDnneIntent,
    float LeftMotorDrive,
    float RightMotorDrive,
    float ForwardVelocity,
    float TurnRateDeg,
    float ContactLevel,
    float SatietyRelief,
    float SafetyRelief,
    float PainLevel,
    float DamageLevel,
    float Progress,
    float EffortCost,
    float Novelty,
    float RewardPredictionError,
    SurvivalBenchmarkWorldSnapshot WorldAfterAction,
    string EventSummary);

internal sealed record SurvivalBenchmarkWorldSnapshot(
    int Step,
    int WorldSize,
    int AgentX,
    int AgentY,
    int FoodX,
    int FoodY,
    int ShelterX,
    int ShelterY,
    int ThreatX,
    int ThreatY,
    float Health,
    float Hunger,
    float ThreatLevel,
    bool InShelter,
    bool FoodAvailable,
    int FoodCollected,
    int ShelterVisits,
    int ThreatContacts,
    int UniqueCellsVisited);

internal static class DeterministicSurvivalBenchmark
{
    internal const string ProtocolVersion = "dnne.survival-benchmark.v1";
    private const int DefaultSeed = 317;
    private const int DefaultSteps = 240;
    private const int MaximumSteps = 2_000;
    private static readonly string[] DefaultPolicies =
    [
        "control-state-intent",
        "rule-safety",
        "deterministic-random",
        "no-learning-stationary"
    ];

    public static bool TryNormalize(
        SurvivalBenchmarkRequest request,
        out SurvivalBenchmarkRequest? normalized,
        out string? error)
    {
        normalized = null;
        error = null;
        var steps = request.Steps ?? DefaultSteps;
        if (steps is < 1 or > MaximumSteps)
        {
            error = $"steps must be between 1 and {MaximumSteps}.";
            return false;
        }

        var seed = request.Seed ?? DefaultSeed;
        var requestedPolicies = request.Policies is { Count: > 0 }
            ? request.Policies
            : DefaultPolicies;
        var policies = new List<string>(requestedPolicies.Count);
        foreach (var requestedPolicy in requestedPolicies)
        {
            var policy = NormalizePolicy(requestedPolicy);
            if (policy is null)
            {
                error = $"Unknown survival benchmark policy '{requestedPolicy}'. Supported policies: {string.Join(", ", DefaultPolicies)}.";
                return false;
            }

            if (!policies.Contains(policy, StringComparer.OrdinalIgnoreCase))
            {
                policies.Add(policy);
            }
        }

        if (policies.Count == 0)
        {
            error = "At least one benchmark policy is required.";
            return false;
        }

        normalized = new SurvivalBenchmarkRequest(seed, steps, policies, request.InitialBrainState);
        return true;
    }

    public static SurvivalBenchmarkResult Run(
        SurvivalBenchmarkRequest request,
        NetworkStateDocument initialBrainState,
        string initialStateSource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(initialBrainState);
        if (!TryNormalize(request, out var normalized, out var error) || normalized is null)
        {
            throw new InvalidOperationException(error ?? "Invalid survival benchmark request.");
        }

        var seed = normalized.Seed.GetValueOrDefault(DefaultSeed);
        var steps = normalized.Steps.GetValueOrDefault(DefaultSteps);
        var policies = normalized.Policies ?? DefaultPolicies;
        var initial = CloneState(initialBrainState);
        var worldLayout = SurvivalWorldLayout.Create(seed);
        var episodes = new List<SurvivalBenchmarkEpisode>(policies.Count);
        foreach (var policy in policies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            episodes.Add(RunEpisode(policy, seed, steps, worldLayout, initial, cancellationToken));
        }
        var descriptor = DescribeBrainSnapshot(initial, initialStateSource);
        var initialWorld = new SurvivalWorld(worldLayout).Snapshot();
        var result = new SurvivalBenchmarkResult(
            ProtocolVersion,
            "deterministic-survival-grid",
            seed,
            steps,
            descriptor,
            initial,
            initialWorld,
            episodes);
        return result with { ArtifactFingerprint = BuildArtifactFingerprint(result) };
    }

    private static SurvivalBenchmarkEpisode RunEpisode(
        string policyName,
        int seed,
        int maxSteps,
        SurvivalWorldLayout layout,
        NetworkStateDocument initialState,
        CancellationToken cancellationToken)
    {
        var state = CreateIsolatedState(initialState);
        var world = new SurvivalWorld(layout);
        var policy = CreatePolicy(policyName, seed);
        var records = new List<SurvivalBenchmarkStepRecord>(maxSteps);
        var healthTotal = 0f;
        var hungerTotal = 0f;
        var threatTotal = 0f;
        var intentTotal = 0f;
        var intentDrivenActions = 0;
        var fallbackActions = 0;
        var totalDamage = 0f;

        for (var step = 1; step <= maxSteps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = world.Snapshot();
            state.AdvanceClockAndCreateTickSignal();
            if (policy.LearningEnabled)
            {
                ApplyWorldObservation(state, before, default, step);
                state.ObserveCognitiveRuntime(
                    state.Tick,
                    dispatchedSpikes: 0,
                    activePathwayCount: 0,
                    rewardPredictionError: 0f,
                    dominantPathway: null);
            }

            var intent = state.GetIntentionalActionLoopSnapshot();
            var decision = policy.Decide(before, intent, step);
            var outcome = world.Apply(decision.Action);
            var after = world.Snapshot();
            if (policy.LearningEnabled)
            {
                ApplyWorldObservation(state, after, outcome, step);
                state.ObserveCognitiveRuntime(
                    state.Tick,
                    dispatchedSpikes: 0,
                    activePathwayCount: 0,
                    rewardPredictionError: outcome.RewardPredictionError,
                    dominantPathway: null);
            }

            healthTotal += after.Health;
            hungerTotal += after.Hunger;
            threatTotal += after.ThreatLevel;
            intentTotal += intent.Confidence;
            totalDamage += outcome.DamageLevel;
            if (decision.UsedDnneIntent)
            {
                intentDrivenActions++;
            }
            else
            {
                fallbackActions++;
            }

            records.Add(new SurvivalBenchmarkStepRecord(
                step,
                state.Tick,
                before,
                decision.Action.Name,
                intent.MotorDirective,
                intent.GoalKey,
                intent.ActionKey,
                intent.Confidence,
                decision.UsedDnneIntent,
                decision.LeftMotorDrive,
                decision.RightMotorDrive,
                outcome.ForwardVelocity,
                outcome.TurnRateDeg,
                outcome.ContactLevel,
                outcome.SatietyRelief,
                outcome.SafetyRelief,
                outcome.PainLevel,
                outcome.DamageLevel,
                outcome.Progress,
                outcome.EffortCost,
                outcome.Novelty,
                outcome.RewardPredictionError,
                after,
                outcome.EventSummary));

            if (outcome.Terminal)
            {
                break;
            }
        }

        world.CompleteHorizon();
        var finalWorld = world.Snapshot();
        var executed = records.Count;
        var metrics = new SurvivalBenchmarkMetrics(
            finalWorld.FoodCollected,
            finalWorld.ShelterVisits,
            finalWorld.ThreatContacts,
            finalWorld.UniqueCellsVisited,
            totalDamage,
            DivideOrZero(healthTotal, executed),
            DivideOrZero(hungerTotal, executed),
            DivideOrZero(threatTotal, executed),
            DivideOrZero(intentTotal, executed),
            intentDrivenActions,
            fallbackActions);
        var finalBrainState = state.ExportNetworkState();
        return new SurvivalBenchmarkEpisode(
            policy.Name,
            policy.Kind,
            seed,
            executed,
            world.IsSuccessful,
            world.TerminalCondition,
            metrics,
            finalWorld,
            DescribeBrainSnapshot(finalBrainState, "isolated-episode-final"),
            records);
    }

    internal static void ApplyWorldObservation(
        SimulationState state,
        SurvivalBenchmarkWorldSnapshot world,
        SurvivalTransition outcome,
        int step)
    {
        var foodDistance = Distance(world.AgentX, world.AgentY, world.FoodX, world.FoodY);
        var shelterDistance = Distance(world.AgentX, world.AgentY, world.ShelterX, world.ShelterY);
        var threatDistance = Distance(world.AgentX, world.AgentY, world.ThreatX, world.ThreatY);
        RegisterVisibleObject(state, "food", "food source", foodDistance, world.FoodAvailable, step);
        RegisterVisibleObject(state, "shelter", "safe shelter", shelterDistance, true, step);
        RegisterVisibleObject(state, "threat", "moving predator threat", threatDistance, true, step);

        var darkness = ((step / 40) % 2) == 1 ? 0.72f : 0.18f;
        var shelterNeed = Math.Clamp((world.ThreatLevel * 0.62f) + ((1f - world.Health) * 0.30f), 0f, 1f);
        var anxiety = Math.Clamp((world.ThreatLevel * 0.68f) + ((1f - world.Health) * 0.18f), 0f, 1f);
        state.UpdateNeuromod(
            new NeuromodState
            {
                DopamineLevel = Math.Clamp(0.32f + outcome.SatietyRelief + (outcome.Progress * 0.16f), 0f, 1f),
                SerotoninLevel = Math.Clamp(0.46f + (world.InShelter ? 0.16f : 0f) - (world.Hunger * 0.12f), 0f, 1f),
                AcetylcholineLevel = Math.Clamp(0.32f + (world.ThreatLevel * 0.30f) + (outcome.Progress * 0.12f), 0f, 1f),
                NorepinephrineLevel = Math.Clamp(0.16f + (world.ThreatLevel * 0.72f) + outcome.PainLevel, 0f, 1f)
            },
            outcome.RewardPredictionError);
        state.UpdateEnvironmentalState(
            darkness,
            shelterNeed,
            anxiety,
            world.Hunger,
            world.ThreatLevel,
            world.InShelter ? 1f : 0f,
            world.Health,
            world.InShelter ? 0.92f : 0.12f);
        state.UpdateBodyState(
            outcome.ForwardVelocity,
            outcome.TurnRateDeg,
            outcome.ContactLevel,
            tactileFront: outcome.ContactLevel,
            tactileLeft: outcome.ContactLevel * 0.5f,
            tactileRight: outcome.ContactLevel * 0.5f,
            tactileGround: outcome.ForwardVelocity > 0.01f ? 0.18f : 0.05f,
            painLevel: outcome.PainLevel,
            urgency: world.ThreatLevel,
            leftMotorDrive: outcome.LeftMotorDrive,
            rightMotorDrive: outcome.RightMotorDrive);
        state.UpdateOutcomeState(
            outcome.SatietyRelief,
            outcome.SafetyRelief,
            outcome.PainLevel,
            outcome.DamageLevel,
            world.InShelter ? 0.82f : 0f,
            outcome.Progress,
            outcome.EffortCost,
            outcome.Novelty,
            socialApproval: 0f);
    }

    private static void RegisterVisibleObject(
        SimulationState state,
        string key,
        string label,
        int distance,
        bool available,
        int step)
    {
        if (!available || distance > 5)
        {
            return;
        }

        var salience = Math.Clamp(1f - (distance / 6f), 0.18f, 1f);
        state.RegisterObjectObservation(
            $"benchmark:{key}",
            label,
            hemisphere: (step & 1) == 0 ? "L" : "R",
            salience,
            confidence: 0.92f,
            intensity: 0.6f + salience,
            deliveredSpikes: 18 + (int)Math.Round(salience * 36));
    }

    internal static SimulationState CreateIsolatedState(NetworkStateDocument initialState)
    {
        var state = new SimulationState();
        state.Configure(
            Math.Max(0.1, initialState.TickDurationMs),
            new Dictionary<StructureId, string>(),
            new Dictionary<StructureId, List<SynapticConnection>>());
        var copy = CloneState(initialState);
        if (!state.TryImportNetworkState(copy, out _, out var error))
        {
            throw new InvalidOperationException($"Unable to import the benchmark brain snapshot: {error ?? "unknown error"}");
        }

        return state;
    }

    internal static void ReplayRecordedStep(SimulationState state, SurvivalBenchmarkStepRecord record, bool learningEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(record);

        state.AdvanceClockAndCreateTickSignal();
        if (state.Tick != record.BrainTick)
        {
            throw new InvalidOperationException(
                $"Replay tick mismatch at benchmark step {record.Step}: expected {record.BrainTick}, observed {state.Tick}.");
        }

        if (!learningEnabled)
        {
            _ = state.GetIntentionalActionLoopSnapshot();
            return;
        }

        ApplyWorldObservation(state, record.WorldBeforeAction, SurvivalTransition.None, record.Step);
        state.ObserveCognitiveRuntime(
            state.Tick,
            dispatchedSpikes: 0,
            activePathwayCount: 0,
            rewardPredictionError: 0f,
            dominantPathway: null);

        // The recorded run resolves this loop after its pre-action observation.
        // The getter advances derived intent state even though replay does not
        // re-select or execute an action from it.
        _ = state.GetIntentionalActionLoopSnapshot();

        var outcome = new SurvivalTransition(
            record.SatietyRelief,
            record.SafetyRelief,
            record.PainLevel,
            record.DamageLevel,
            record.Progress,
            record.EffortCost,
            record.Novelty,
            record.RewardPredictionError,
            record.ForwardVelocity,
            record.TurnRateDeg,
            record.ContactLevel,
            record.LeftMotorDrive,
            record.RightMotorDrive,
            Terminal: record.WorldAfterAction.Health <= 0.02f,
            record.EventSummary);
        ApplyWorldObservation(state, record.WorldAfterAction, outcome, record.Step);
        state.ObserveCognitiveRuntime(
            state.Tick,
            dispatchedSpikes: 0,
            activePathwayCount: 0,
            rewardPredictionError: outcome.RewardPredictionError,
            dominantPathway: null);
    }

    private static NetworkStateDocument CloneState(NetworkStateDocument source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<NetworkStateDocument>(json)
            ?? throw new InvalidOperationException("Unable to clone the benchmark brain snapshot.");
    }

    private static SurvivalBenchmarkBrainSnapshotDescriptor DescribeBrainSnapshot(NetworkStateDocument state, string source)
        => new(
            source,
            state.SchemaVersion,
            state.Tick,
            state.SimulationClockMs,
            state.TickDurationMs,
            BuildBenchmarkStateFingerprint(state));

    internal static string BuildBenchmarkStateFingerprint(NetworkStateDocument state)
    {
        var stable = CloneState(state);
        stable.ExportedAtUnixMs = 0;
        stable.ExportedTickWallClockUnixMs = 0;
        stable.ExportFingerprint = string.Empty;
        var node = JsonSerializer.SerializeToNode(stable)
            ?? throw new InvalidOperationException("Unable to serialize benchmark state for fingerprinting.");
        RemoveTransientTimestamps(node);
        return Sha256(JsonSerializer.Serialize(Canonicalize(node)));
    }

    internal static string BuildArtifactFingerprint(SurvivalBenchmarkResult result)
        => Sha256(JsonSerializer.Serialize(result with { ArtifactFingerprint = string.Empty }));

    private static string Sha256(string value)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void RemoveTransientTimestamps(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Key.Contains("WallClock", StringComparison.OrdinalIgnoreCase) ||
                    property.Key.EndsWith("AtUtc", StringComparison.OrdinalIgnoreCase) ||
                    property.Key.EndsWith("AtUnixMs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Key, "ExportFingerprint", StringComparison.OrdinalIgnoreCase))
                {
                    obj.Remove(property.Key);
                    continue;
                }

                if (property.Value is not null)
                {
                    RemoveTransientTimestamps(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    RemoveTransientTimestamps(item);
                }
            }
        }
    }

    private static JsonNode Canonicalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var canonical = new JsonObject();
            foreach (var property in obj.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                canonical[property.Key] = property.Value is null ? null : Canonicalize(property.Value);
            }
            return canonical;
        }

        if (node is JsonArray array)
        {
            var canonical = new JsonArray();
            foreach (var item in array)
            {
                canonical.Add(item is null ? null : Canonicalize(item));
            }
            return canonical;
        }

        return node.DeepClone();
    }

    private static ISurvivalBenchmarkPolicy CreatePolicy(string policyName, int seed)
        => policyName switch
        {
            "control-state-intent" => new DnneIntentSurvivalPolicy(),
            "rule-safety" => new RuleSafetySurvivalPolicy(),
            "deterministic-random" => new DeterministicRandomSurvivalPolicy(seed),
            "no-learning-stationary" => new StationarySurvivalPolicy(),
            _ => throw new InvalidOperationException($"Unknown survival benchmark policy '{policyName}'.")
        };

    private static string? NormalizePolicy(string? policy)
    {
        var value = policy?.Trim().ToLowerInvariant();
        return value switch
        {
            "current" or "dnne" or "current-dnne-intent" or "control-state-intent" => "control-state-intent",
            "rule" or "rule-safety" => "rule-safety",
            "random" or "deterministic-random" => "deterministic-random",
            "stationary" or "no-learning" or "no-learning-stationary" => "no-learning-stationary",
            _ => null
        };
    }

    private static float DivideOrZero(float total, int count)
        => count <= 0 ? 0f : total / count;

    private static int Distance(int leftX, int leftY, int rightX, int rightY)
        => Math.Abs(leftX - rightX) + Math.Abs(leftY - rightY);

    private interface ISurvivalBenchmarkPolicy
    {
        string Name { get; }
        string Kind { get; }
        bool LearningEnabled => true;
        SurvivalPolicyDecision Decide(SurvivalBenchmarkWorldSnapshot world, IntentionalActionLoopRuntime intent, int step);
    }

    private sealed class DnneIntentSurvivalPolicy : ISurvivalBenchmarkPolicy
    {
        public string Name => "control-state-intent";
        public string Kind => "control-state-intent";

        public SurvivalPolicyDecision Decide(SurvivalBenchmarkWorldSnapshot world, IntentionalActionLoopRuntime intent, int step)
        {
            var directive = intent.MotorDirective.ToLowerInvariant();
            if (intent.Active && intent.Readiness >= 0.18f)
            {
                if (directive.Contains("seek_food", StringComparison.Ordinal))
                {
                    return Toward(world, world.FoodX, world.FoodY, "seek-food", usedDnneIntent: true);
                }

                if (directive.Contains("seek_shelter", StringComparison.Ordinal) ||
                    directive.Contains("guard_body", StringComparison.Ordinal))
                {
                    return Toward(world, world.ShelterX, world.ShelterY, "seek-shelter", usedDnneIntent: true);
                }

                if (directive.Contains("escape", StringComparison.Ordinal) || directive.Contains("reorient", StringComparison.Ordinal))
                {
                    return AwayFrom(world, world.ThreatX, world.ThreatY, "escape-threat", usedDnneIntent: true);
                }

                if (directive.Contains("rest", StringComparison.Ordinal))
                {
                    return SurvivalPolicyDecision.Wait("rest", usedDnneIntent: true);
                }
            }

            return Explore(step, "dnne-orienting-fallback");
        }
    }

    private sealed class RuleSafetySurvivalPolicy : ISurvivalBenchmarkPolicy
    {
        public string Name => "rule-safety";
        public string Kind => "hand-authored-baseline";

        public SurvivalPolicyDecision Decide(SurvivalBenchmarkWorldSnapshot world, IntentionalActionLoopRuntime intent, int step)
        {
            var threatDistance = Distance(world.AgentX, world.AgentY, world.ThreatX, world.ThreatY);
            if (!world.InShelter && threatDistance <= 3)
            {
                return Toward(world, world.ShelterX, world.ShelterY, "rule-seek-shelter", usedDnneIntent: false);
            }

            if (world.Hunger >= 0.34f && world.FoodAvailable)
            {
                return Toward(world, world.FoodX, world.FoodY, "rule-seek-food", usedDnneIntent: false);
            }

            return Explore(step, "rule-patrol");
        }
    }

    private sealed class DeterministicRandomSurvivalPolicy : ISurvivalBenchmarkPolicy
    {
        private readonly DeterministicRandom _random;

        public DeterministicRandomSurvivalPolicy(int seed)
        {
            _random = new DeterministicRandom((uint)(seed ^ 0x4D595DF4));
        }

        public string Name => "deterministic-random";
        public string Kind => "random-baseline";

        public SurvivalPolicyDecision Decide(SurvivalBenchmarkWorldSnapshot world, IntentionalActionLoopRuntime intent, int step)
            => Direction(_random.NextInt(4), "random-walk", usedDnneIntent: false);
    }

    private sealed class StationarySurvivalPolicy : ISurvivalBenchmarkPolicy
    {
        public string Name => "no-learning-stationary";
        public string Kind => "no-learning-baseline";
        public bool LearningEnabled => false;

        public SurvivalPolicyDecision Decide(SurvivalBenchmarkWorldSnapshot world, IntentionalActionLoopRuntime intent, int step)
            => SurvivalPolicyDecision.Wait("stationary", usedDnneIntent: false);
    }

    private static SurvivalPolicyDecision Toward(
        SurvivalBenchmarkWorldSnapshot world,
        int targetX,
        int targetY,
        string label,
        bool usedDnneIntent)
    {
        var xDelta = targetX - world.AgentX;
        var yDelta = targetY - world.AgentY;
        if (Math.Abs(xDelta) >= Math.Abs(yDelta) && xDelta != 0)
        {
            return new SurvivalPolicyDecision(new SurvivalAction(Math.Sign(xDelta), 0, label), usedDnneIntent);
        }

        if (yDelta != 0)
        {
            return new SurvivalPolicyDecision(new SurvivalAction(0, Math.Sign(yDelta), label), usedDnneIntent);
        }

        return SurvivalPolicyDecision.Wait(label, usedDnneIntent);
    }

    private static SurvivalPolicyDecision AwayFrom(
        SurvivalBenchmarkWorldSnapshot world,
        int threatX,
        int threatY,
        string label,
        bool usedDnneIntent)
    {
        var xDelta = world.AgentX - threatX;
        var yDelta = world.AgentY - threatY;
        if (Math.Abs(xDelta) >= Math.Abs(yDelta) && xDelta != 0)
        {
            return new SurvivalPolicyDecision(new SurvivalAction(Math.Sign(xDelta), 0, label), usedDnneIntent);
        }

        if (yDelta != 0)
        {
            return new SurvivalPolicyDecision(new SurvivalAction(0, Math.Sign(yDelta), label), usedDnneIntent);
        }

        return Direction(0, label, usedDnneIntent);
    }

    private static SurvivalPolicyDecision Explore(int step, string label)
        => Direction((step - 1) % 4, label, usedDnneIntent: false);

    private static SurvivalPolicyDecision Direction(int direction, string label, bool usedDnneIntent)
        => direction switch
        {
            0 => new SurvivalPolicyDecision(new SurvivalAction(1, 0, label), usedDnneIntent),
            1 => new SurvivalPolicyDecision(new SurvivalAction(0, 1, label), usedDnneIntent),
            2 => new SurvivalPolicyDecision(new SurvivalAction(-1, 0, label), usedDnneIntent),
            _ => new SurvivalPolicyDecision(new SurvivalAction(0, -1, label), usedDnneIntent)
        };

    private readonly record struct SurvivalPolicyDecision(SurvivalAction Action, bool UsedDnneIntent)
    {
        public float LeftMotorDrive => Action.IsMovement ? 0.72f : 0.08f;
        public float RightMotorDrive => Action.IsMovement ? 0.72f : 0.08f;

        public static SurvivalPolicyDecision Wait(string label, bool usedDnneIntent)
            => new(new SurvivalAction(0, 0, label), usedDnneIntent);
    }

    private readonly record struct SurvivalAction(int DeltaX, int DeltaY, string Name)
    {
        public bool IsMovement => DeltaX != 0 || DeltaY != 0;
    }

    internal readonly record struct SurvivalTransition(
        float SatietyRelief,
        float SafetyRelief,
        float PainLevel,
        float DamageLevel,
        float Progress,
        float EffortCost,
        float Novelty,
        float RewardPredictionError,
        float ForwardVelocity,
        float TurnRateDeg,
        float ContactLevel,
        float LeftMotorDrive,
        float RightMotorDrive,
        bool Terminal,
        string EventSummary)
    {
        public static SurvivalTransition None { get; } = new(
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false, "initial observation");
    }

    private sealed class SurvivalWorld
    {
        private readonly SurvivalWorldLayout _layout;
        private readonly HashSet<(int X, int Y)> _visited = [];
        private int _agentX;
        private int _agentY;
        private int _foodX;
        private int _foodY;
        private int _threatX;
        private int _threatY;
        private float _health = 0.90f;
        private float _hunger = 0.34f;
        private int _step;
        private bool _foodAvailable = true;

        public SurvivalWorld(SurvivalWorldLayout layout)
        {
            _layout = layout;
            _agentX = layout.StartX;
            _agentY = layout.StartY;
            _foodX = layout.FoodX;
            _foodY = layout.FoodY;
            _threatX = layout.ThreatX;
            _threatY = layout.ThreatY;
            _visited.Add((_agentX, _agentY));
        }

        public bool IsSuccessful { get; private set; }
        public string TerminalCondition { get; private set; } = "running";
        public int FoodCollected { get; private set; }
        public int ShelterVisits { get; private set; }
        public int ThreatContacts { get; private set; }

        public void CompleteHorizon()
        {
            if (!string.Equals(TerminalCondition, "running", StringComparison.Ordinal))
            {
                return;
            }

            TerminalCondition = "survived-fixed-horizon";
            IsSuccessful = true;
        }

        public SurvivalTransition Apply(SurvivalAction action)
        {
            _step++;
            var foodWasAvailable = _foodAvailable;
            var oldFoodDistance = Distance(_agentX, _agentY, _foodX, _foodY);
            var oldShelterDistance = Distance(_agentX, _agentY, _layout.ShelterX, _layout.ShelterY);
            var wasInShelter = _agentX == _layout.ShelterX && _agentY == _layout.ShelterY;
            _agentX = Math.Clamp(_agentX + action.DeltaX, 0, _layout.WorldSize - 1);
            _agentY = Math.Clamp(_agentY + action.DeltaY, 0, _layout.WorldSize - 1);
            var isNovelCell = _visited.Add((_agentX, _agentY));
            var inShelter = _agentX == _layout.ShelterX && _agentY == _layout.ShelterY;
            var moved = action.IsMovement;
            var ateFood = _foodAvailable && _agentX == _foodX && _agentY == _foodY;
            if (ateFood)
            {
                FoodCollected++;
                _hunger = Math.Max(0.02f, _hunger - 0.58f);
                _foodAvailable = false;
            }

            MoveThreat(inShelter);
            var threatDistance = Distance(_agentX, _agentY, _threatX, _threatY);
            var threatLevel = ThreatLevel(threatDistance, inShelter);
            if (threatDistance == 0 && !inShelter)
            {
                ThreatContacts++;
            }

            _hunger = Math.Clamp(_hunger + 0.010f, 0f, 1f);
            var damage = Math.Clamp(
                (threatLevel * (inShelter ? 0.004f : 0.070f)) +
                (_hunger > 0.86f ? (_hunger - 0.86f) * 0.085f : 0f),
                0f,
                0.16f);
            if (inShelter)
            {
                if (!wasInShelter)
                {
                    ShelterVisits++;
                }

                _health = Math.Min(1f, _health + 0.010f);
            }

            _health = Math.Clamp(_health - damage, 0f, 1f);
            var newFoodDistance = Distance(_agentX, _agentY, _foodX, _foodY);
            var newShelterDistance = Distance(_agentX, _agentY, _layout.ShelterX, _layout.ShelterY);
            var progress = ateFood || inShelter
                ? 0.90f
                : foodWasAvailable && newFoodDistance < oldFoodDistance
                    ? 0.24f
                    : newShelterDistance < oldShelterDistance ? 0.16f : 0.04f;
            var safetyRelief = inShelter ? 0.72f : Math.Clamp(0.20f - (threatLevel * 0.18f), 0f, 1f);
            var pain = Math.Clamp(damage * 6.5f, 0f, 1f);
            var novelty = isNovelCell ? 0.28f : 0.08f;
            var terminal = _health <= 0.02f;
            if (terminal)
            {
                TerminalCondition = "death-health-depleted";
                IsSuccessful = false;
            }

            var eventSummary = ateFood
                ? "food collected"
                : inShelter
                    ? "shelter occupied"
                    : threatDistance == 0 && !inShelter
                        ? "threat contact"
                        : moved
                            ? "movement"
                            : "wait";
            return new SurvivalTransition(
                SatietyRelief: ateFood ? 0.92f : 0f,
                SafetyRelief: safetyRelief,
                PainLevel: pain,
                DamageLevel: damage,
                Progress: progress,
                EffortCost: moved ? 0.14f : 0.02f,
                Novelty: novelty,
                RewardPredictionError: Math.Clamp((ateFood ? 0.64f : 0f) + (safetyRelief * 0.20f) - (pain * 0.72f), -1f, 1f),
                ForwardVelocity: moved ? 1f : 0f,
                TurnRateDeg: moved && action.DeltaY != 0 ? 90f : 0f,
                ContactLevel: threatDistance == 0 && !inShelter ? 1f : 0f,
                LeftMotorDrive: moved ? 0.72f : 0.08f,
                RightMotorDrive: moved ? 0.72f : 0.08f,
                Terminal: terminal,
                EventSummary: eventSummary);
        }

        public SurvivalBenchmarkWorldSnapshot Snapshot()
        {
            var inShelter = _agentX == _layout.ShelterX && _agentY == _layout.ShelterY;
            var threatDistance = Distance(_agentX, _agentY, _threatX, _threatY);
            return new SurvivalBenchmarkWorldSnapshot(
                _step,
                _layout.WorldSize,
                _agentX,
                _agentY,
                _foodX,
                _foodY,
                _layout.ShelterX,
                _layout.ShelterY,
                _threatX,
                _threatY,
                _health,
                _hunger,
                ThreatLevel(threatDistance, inShelter),
                inShelter,
                _foodAvailable,
                FoodCollected,
                ShelterVisits,
                ThreatContacts,
                _visited.Count);
        }

        private void MoveThreat(bool agentInShelter)
        {
            if (agentInShelter)
            {
                if (_threatX != 0)
                {
                    _threatX--;
                }
                else if (_threatY != 0)
                {
                    _threatY--;
                }

                return;
            }

            var xDelta = _agentX - _threatX;
            var yDelta = _agentY - _threatY;
            if (Math.Abs(xDelta) >= Math.Abs(yDelta) && xDelta != 0)
            {
                _threatX += Math.Sign(xDelta);
            }
            else if (yDelta != 0)
            {
                _threatY += Math.Sign(yDelta);
            }
        }

        private static float ThreatLevel(int distance, bool inShelter)
        {
            if (inShelter)
            {
                return 0.04f;
            }

            return distance switch
            {
                <= 0 => 1f,
                1 => 0.78f,
                2 => 0.48f,
                3 => 0.22f,
                _ => 0.06f
            };
        }
    }

    private readonly record struct SurvivalWorldLayout(
        int WorldSize,
        int StartX,
        int StartY,
        int FoodX,
        int FoodY,
        int ShelterX,
        int ShelterY,
        int ThreatX,
        int ThreatY)
    {
        public static SurvivalWorldLayout Create(int seed)
        {
            var random = new DeterministicRandom((uint)seed);
            const int worldSize = 11;
            var startX = worldSize / 2;
            var startY = worldSize / 2;
            var food = NextDistinctPosition(random, worldSize, (startX, startY));
            var shelter = NextDistinctPosition(random, worldSize, (startX, startY), food);
            var threat = NextDistinctPosition(random, worldSize, (startX, startY), food, shelter);
            return new SurvivalWorldLayout(
                worldSize,
                startX,
                startY,
                food.X,
                food.Y,
                shelter.X,
                shelter.Y,
                threat.X,
                threat.Y);
        }

        private static (int X, int Y) NextDistinctPosition(
            DeterministicRandom random,
            int size,
            params (int X, int Y)[] excluded)
        {
            while (true)
            {
                var position = (random.NextInt(size), random.NextInt(size));
                if (!excluded.Contains(position))
                {
                    return position;
                }
            }
        }
    }

    private sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 0xA341316Cu : seed;
        }

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            }

            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)exclusiveMaximum);
        }
    }
}
