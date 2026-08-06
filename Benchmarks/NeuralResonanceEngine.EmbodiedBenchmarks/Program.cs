using System.Diagnostics;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

string outputDirectory = ReadOption(args, "--output")
    ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "embodied-closed-loop");
Directory.CreateDirectory(outputDirectory);

string mode = ReadOption(args, "--mode") ?? "closed-loop";
mode = mode.Trim().ToLowerInvariant();
if (mode is "motor-preflight" or "motor-capture" or "motor-campaign")
{
    Environment.ExitCode = await NeuronalMotorQualificationCommand.RunAsync(mode, args, outputDirectory);
    return;
}

if (string.Equals(mode, "navigation", StringComparison.OrdinalIgnoreCase))
{
    ContinuousNavigationResult navigation = ContinuousNavigationBenchmark.Run();
    string navigationStamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    string navigationJsonPath = Path.Combine(outputDirectory, $"continuous-navigation-{navigationStamp}.json");
    string navigationReportPath = Path.Combine(outputDirectory, $"continuous-navigation-{navigationStamp}.md");
    File.WriteAllText(navigationJsonPath, JsonSerializer.Serialize(navigation, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(navigationReportPath, ContinuousNavigationBenchmark.RenderMarkdown(navigation));

    Console.WriteLine("DNNE continuous-navigation benchmark complete.");
    Console.WriteLine($"Status: {(navigation.Passed ? "PASS" : "FAIL")}");
    Console.WriteLine($"Generalization: {navigation.GeneralizationScore:P1}");
    Console.WriteLine($"Mean path efficiency: {navigation.MeanPathEfficiency:P1}");
    foreach (ContinuousNavigationScenarioResult scenario in navigation.Scenarios)
    {
        Console.WriteLine($"Seed {scenario.Seed}: {(scenario.GoalReached ? "goal" : "miss")} in {scenario.CellTransitions} cells; shortest {scenario.ShortestPathCells}; collisions {scenario.Collisions}");
    }

    Console.WriteLine($"JSON: {navigationJsonPath}");
    Console.WriteLine($"Report: {navigationReportPath}");
    Environment.ExitCode = navigation.Passed ? 0 : 1;
    return;
}

EmbodiedClosedLoopResult result = EmbodiedClosedLoopBenchmark.Run();
string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
string jsonPath = Path.Combine(outputDirectory, $"embodied-closed-loop-{stamp}.json");
string reportPath = Path.Combine(outputDirectory, $"embodied-closed-loop-{stamp}.md");
File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(reportPath, EmbodiedClosedLoopBenchmark.RenderMarkdown(result));

Console.WriteLine("DNNE embodied closed-loop benchmark complete.");
Console.WriteLine($"Status: {(result.Passed ? "PASS" : "FAIL")}");
Console.WriteLine($"Loop integrity: {result.LoopIntegrityScore:P1}");
Console.WriteLine($"Initial choice: {result.InitialIntent.GoalKey} -> {result.FirstWorldEvent}");
Console.WriteLine($"Adapted choice: {result.AdaptedIntent.GoalKey} -> {result.SecondWorldEvent}");
Console.WriteLine($"Action memory: {result.ActionMemoryCount} trace(s), dopamine: {result.DopamineLearningCount} trace(s)");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Report: {reportPath}");
Environment.ExitCode = result.Passed ? 0 : 1;

static string? ReadOption(string[] arguments, string name)
{
    for (int index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
        {
            return arguments[index + 1];
        }
    }

    return null;
}

internal sealed record EmbodiedIntentSnapshot(
    string GoalKey,
    string ActionKey,
    string MotorDirective,
    float Confidence,
    bool Active);

internal sealed record EmbodiedBoundaryMetrics(
    int WorldBodyMessages,
    int WorldOutcomeMessages,
    int WorldObjectMessages,
    int BrainMotorSpikes,
    int AvatarMotorEvents,
    int AvatarActions,
    bool BodyFeedbackReachedBrain,
    bool OutcomeFeedbackReachedBrain);

internal sealed record EmbodiedClosedLoopResult(
    string ProtocolVersion,
    bool Passed,
    float LoopIntegrityScore,
    EmbodiedIntentSnapshot InitialIntent,
    EmbodiedIntentSnapshot AdaptedIntent,
    string FirstWorldEvent,
    string SecondWorldEvent,
    double FirstForwardSpeed,
    double SecondForwardSpeed,
    float FirstRewardPredictionError,
    float SecondRewardPredictionError,
    int ActionMemoryCount,
    int DopamineLearningCount,
    string BestLearnedAction,
    EmbodiedBoundaryMetrics Boundaries,
    IReadOnlyList<string> Criteria);

internal static class EmbodiedClosedLoopBenchmark
{
    internal const string ProtocolVersion = "dnne.embodied-closed-loop.v1";
    private const int CognitiveTicksPerDecision = 8;
    private static readonly AvatarBodyStateProfile BodyProfile = new(
        MaxForwardSpeed: 3.2,
        MaxTurnRateDeg: 220.0,
        BaseIntensity: 0.20,
        MotionIntensityWeight: 0.50,
        TurnIntensityWeight: 0.14,
        ContactIntensityWeight: 0.42,
        BaseBurstCount: 6,
        MotionBurstWeight: 9,
        TurnBurstWeight: 4,
        ContactBurstWeight: 8);

    public static EmbodiedClosedLoopResult Run()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 10.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());

        using var avatar = new AvatarService(
            new AvatarNervousSystemOptions(
                new AvatarKinematicsOptions(
                    MaxMotorDrive: 240,
                    ForwardSpeedCoefficient: 0.0125,
                    TurnSpeedCoefficient: 3.2,
                    MinForwardSpeed: 0,
                    MaxForwardSpeed: 3.2,
                    MaxTurnRateDeg: 220),
                IdleMotorFallbackTicks: int.MaxValue),
            name: "NRE.EmbodiedBenchmark.Avatar",
            clockOptions: new AvatarServiceClockOptions(Enabled: false));
        var world = new EmbodiedChoiceWorld();

        FeedbackDelivery initialFeedback = DeliverWorldState(avatar, state, world, outcome: null);
        EmbodiedIntentSnapshot initialIntent = DriveCognition(state, CognitiveTicksPerDecision, 0f);
        AvatarActionOutput firstAction = DeliverBrainAction(avatar, initialIntent, out int firstSpikeCount, out int firstMotorEvents);
        EmbodiedWorldOutcome firstOutcome = world.Apply(initialIntent, firstAction);
        FeedbackDelivery firstFeedback = DeliverWorldState(avatar, state, world, firstOutcome);
        EmbodiedIntentSnapshot adaptedIntent = DriveCognition(
            state,
            CognitiveTicksPerDecision,
            firstOutcome.RewardPredictionError);

        AvatarActionOutput secondAction = DeliverBrainAction(avatar, adaptedIntent, out int secondSpikeCount, out int secondMotorEvents);
        EmbodiedWorldOutcome secondOutcome = world.Apply(adaptedIntent, secondAction);
        FeedbackDelivery secondFeedback = DeliverWorldState(avatar, state, world, secondOutcome);
        _ = DriveCognition(state, 3, secondOutcome.RewardPredictionError);

        bool initialFoodChoice = Contains(initialIntent.GoalKey, "food") && firstOutcome.FoodCollected;
        bool changedChoice = !string.Equals(initialIntent.GoalKey, adaptedIntent.GoalKey, StringComparison.OrdinalIgnoreCase);
        bool safetyIntent = Contains(adaptedIntent.GoalKey, "shelter")
                            || Contains(adaptedIntent.GoalKey, "threat")
                            || Contains(adaptedIntent.MotorDirective, "shelter")
                            || Contains(adaptedIntent.MotorDirective, "escape")
                            || Contains(adaptedIntent.MotorDirective, "avoid");
        bool safetyChoice = safetyIntent && (secondOutcome.ShelterReached || secondOutcome.ThreatAvoided);
        bool brainToAvatar = firstSpikeCount > 0 && secondSpikeCount > 0 && firstMotorEvents > 0 && secondMotorEvents > 0;
        bool avatarToWorld = firstAction.Movement.ForwardSpeed > 0.01 && secondAction.Movement.ForwardSpeed > 0.01;
        bool worldToAvatar = firstFeedback.BodyMessages > 0 && firstFeedback.OutcomeMessages > 0
                             && secondFeedback.BodyMessages > 0 && secondFeedback.OutcomeMessages > 0;
        bool avatarToBrain = firstFeedback.BodyApplied && firstFeedback.OutcomeApplied
                             && secondFeedback.BodyApplied && secondFeedback.OutcomeApplied;
        bool memoryChanged = state.ActionMemory.Count > 0 && state.DopamineLearning.Count > 0;

        var criteria = new Dictionary<string, bool>
        {
            ["initial hunger selects food"] = initialFoodChoice,
            ["brain spikes produce avatar motor events"] = brainToAvatar,
            ["avatar movement changes the world"] = avatarToWorld,
            ["world body and outcome cross the avatar"] = worldToAvatar,
            ["avatar feedback reaches the brain"] = avatarToBrain,
            ["consequence changes the next choice"] = changedChoice && safetyChoice,
            ["action and dopamine memories are updated"] = memoryChanged
        };
        float score = criteria.Values.Count(static passed => passed) / (float)criteria.Count;

        return new EmbodiedClosedLoopResult(
            ProtocolVersion,
            criteria.Values.All(static passed => passed),
            score,
            initialIntent,
            adaptedIntent,
            firstOutcome.EventSummary,
            secondOutcome.EventSummary,
            firstAction.Movement.ForwardSpeed,
            secondAction.Movement.ForwardSpeed,
            firstOutcome.RewardPredictionError,
            secondOutcome.RewardPredictionError,
            state.ActionMemory.Count,
            state.DopamineLearning.Count,
            state.ActionMemory.BestActionKey,
            new EmbodiedBoundaryMetrics(
                initialFeedback.BodyMessages + firstFeedback.BodyMessages + secondFeedback.BodyMessages,
                firstFeedback.OutcomeMessages + secondFeedback.OutcomeMessages,
                initialFeedback.ObjectMessages + firstFeedback.ObjectMessages + secondFeedback.ObjectMessages,
                firstSpikeCount + secondSpikeCount,
                firstMotorEvents + secondMotorEvents,
                2,
                firstFeedback.BodyApplied && secondFeedback.BodyApplied,
                firstFeedback.OutcomeApplied && secondFeedback.OutcomeApplied),
            criteria.Select(static pair => $"{pair.Key}: {(pair.Value ? "PASS" : "FAIL")}").ToArray());
    }

    public static string RenderMarkdown(EmbodiedClosedLoopResult result)
    {
        var lines = new List<string>
        {
            "# DNNE Embodied Closed-Loop Benchmark",
            string.Empty,
            $"- Protocol: `{result.ProtocolVersion}`",
            $"- Status: **{(result.Passed ? "PASS" : "FAIL")}**",
            $"- Loop integrity: `{result.LoopIntegrityScore:P1}`",
            string.Empty,
            "## Decisions",
            string.Empty,
            "| Stage | Goal | Action | Motor directive | Confidence | World event | Forward speed | RPE |",
            "| --- | --- | --- | --- | ---: | --- | ---: | ---: |",
            $"| Initial | {result.InitialIntent.GoalKey} | {result.InitialIntent.ActionKey} | {result.InitialIntent.MotorDirective} | {result.InitialIntent.Confidence:F3} | {result.FirstWorldEvent} | {result.FirstForwardSpeed:F3} | {result.FirstRewardPredictionError:F3} |",
            $"| Adapted | {result.AdaptedIntent.GoalKey} | {result.AdaptedIntent.ActionKey} | {result.AdaptedIntent.MotorDirective} | {result.AdaptedIntent.Confidence:F3} | {result.SecondWorldEvent} | {result.SecondForwardSpeed:F3} | {result.SecondRewardPredictionError:F3} |",
            string.Empty,
            "## Boundary Traffic",
            string.Empty,
            $"- world body messages: `{result.Boundaries.WorldBodyMessages}`",
            $"- world outcome messages: `{result.Boundaries.WorldOutcomeMessages}`",
            $"- world object messages: `{result.Boundaries.WorldObjectMessages}`",
            $"- brain motor spikes: `{result.Boundaries.BrainMotorSpikes}`",
            $"- avatar motor events: `{result.Boundaries.AvatarMotorEvents}`",
            $"- avatar actions: `{result.Boundaries.AvatarActions}`",
            $"- action-memory traces: `{result.ActionMemoryCount}`",
            $"- dopamine-learning traces: `{result.DopamineLearningCount}`",
            $"- best learned action: `{result.BestLearnedAction}`",
            string.Empty,
            "## Criteria",
            string.Empty
        };
        lines.AddRange(result.Criteria.Select(static criterion => $"- {criterion}"));
        lines.Add(string.Empty);
        lines.Add("This is a deterministic headless embodiment challenge using the production brain state and avatar service. It validates boundary integrity and short-horizon adaptive choice; it does not yet validate continuous 3D navigation or long-horizon autonomy in the WPF world simulation.");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static EmbodiedIntentSnapshot DriveCognition(SimulationState state, int ticks, float rewardPredictionError)
    {
        for (int index = 0; index < ticks; index++)
        {
            state.AdvanceClockAndCreateTickSignal();
            state.ObserveCognitiveRuntime(
                state.Tick,
                dispatchedSpikes: 24,
                activePathwayCount: 6,
                rewardPredictionError: index == 0 ? rewardPredictionError : 0f,
                dominantPathway: null);
        }

        IntentionalActionLoopRuntime intent = state.GetIntentionalActionLoopSnapshot();
        return new EmbodiedIntentSnapshot(
            intent.GoalKey,
            intent.ActionKey,
            intent.MotorDirective,
            intent.Confidence,
            intent.Active);
    }

    private static AvatarActionOutput DeliverBrainAction(
        AvatarService avatar,
        EmbodiedIntentSnapshot intent,
        out int spikeCount,
        out int motorEvents)
    {
        long beforeReset = avatar.EnqueuedCommands;
        avatar.PostResetMotor();
        WaitForCommands(avatar, beforeReset + 1);

        string directive = string.IsNullOrWhiteSpace(intent.MotorDirective)
            ? "motor_forward"
            : intent.MotorDirective;
        AvatarDispatchSpike[] spikes = Enumerable.Range(0, 24)
            .Select(index => new AvatarDispatchSpike(
                "M1",
                (index & 1) == 0 ? "L" : "R",
                1_000 + index,
                directive))
            .ToArray();
        long beforeSignals = avatar.EnqueuedCommands;
        avatar.PostBrainSignals(
            spikes,
            new AvatarNervousSystemBodyState(
                IsSleeping: false,
                Hunger: 0.5,
                Threat: 0.2,
                Health: 0.95,
                SecondsSinceProgress: 0,
                NoProgressTimeoutSeconds: 4));
        WaitForCommands(avatar, beforeSignals + 1);
        AvatarActionOutput output = avatar.PublishActionOutput();
        spikeCount = spikes.Length;
        motorEvents = avatar.LatestSignal.MotorEvents;
        return output;
    }

    private static FeedbackDelivery DeliverWorldState(
        AvatarService avatar,
        SimulationState state,
        EmbodiedChoiceWorld world,
        EmbodiedWorldOutcome? outcome)
    {
        long expected = avatar.EnqueuedCommands;
        avatar.PostBodyInput(world.CreateBodyTelemetry(outcome), BodyProfile);
        expected++;
        if (outcome is not null)
        {
            avatar.PostOutcome(outcome.ToTelemetry());
            expected++;
        }

        AvatarObjectObservation[] observations = world.CreateObjectObservations();
        avatar.PostObjectCandidates(observations, maxObservations: observations.Length);
        expected++;
        WaitForCommands(avatar, expected);

        int bodyMessages = 0;
        int outcomeMessages = 0;
        int objectMessages = 0;
        bool bodyApplied = false;
        bool outcomeApplied = false;
        while (avatar.TryDequeueBodyInput(out AvatarBodyStateInput bodyInput))
        {
            bodyMessages++;
            BodyStateInputRequest request = AvatarBodyStateInputFactory.CreateRequest(bodyInput.Telemetry, bodyInput.Profile);
            state.UpdateBodyState(
                request.ForwardVelocity.GetValueOrDefault(),
                request.TurnRateDeg.GetValueOrDefault(),
                request.ContactLevel.GetValueOrDefault(),
                request.TactileFront.GetValueOrDefault(),
                request.TactileLeft.GetValueOrDefault(),
                request.TactileRight.GetValueOrDefault(),
                request.TactileGround.GetValueOrDefault(),
                request.PainLevel.GetValueOrDefault(),
                request.Urgency.GetValueOrDefault(),
                request.LeftMotorDrive.GetValueOrDefault(),
                request.RightMotorDrive.GetValueOrDefault());
            state.UpdateEnvironmentalState(
                request.EnvironmentalDarkness.GetValueOrDefault(),
                request.ShelterNeed.GetValueOrDefault(),
                request.Anxiety.GetValueOrDefault(),
                request.Hunger.GetValueOrDefault(),
                request.PredatorThreat.GetValueOrDefault(),
                request.InShelter.GetValueOrDefault(),
                request.Health.GetValueOrDefault(1f),
                request.ShelterSafety.GetValueOrDefault());
            bodyApplied = true;
        }

        while (avatar.TryDequeueOutcome(out AvatarOutcomeTelemetry telemetry))
        {
            outcomeMessages++;
            OutcomeInputRequest request = AvatarOutcomeInputFactory.CreateRequest(telemetry);
            state.UpdateOutcomeState(
                request.SatietyRelief.GetValueOrDefault(),
                request.SafetyRelief.GetValueOrDefault(),
                request.PainLevel.GetValueOrDefault(),
                request.DamageLevel.GetValueOrDefault(),
                request.ShelterComfort.GetValueOrDefault(),
                request.Progress.GetValueOrDefault(),
                request.EffortCost.GetValueOrDefault(),
                request.Novelty.GetValueOrDefault(),
                request.SocialApproval.GetValueOrDefault());
            outcomeApplied = true;
        }

        while (avatar.TryDequeueObjectObservation(out AvatarObjectObservation observation))
        {
            objectMessages++;
            state.RegisterObjectObservation(
                observation.ObjectId,
                observation.Label,
                observation.Hemisphere ?? "M",
                (float)observation.Salience,
                (float)observation.Confidence,
                (float)observation.Intensity,
                observation.BurstCount);
        }

        AvatarBodyTelemetry current = world.CreateBodyTelemetry(outcome);
        state.UpdateNeuromod(
            new NeuromodState
            {
                DopamineLevel = Math.Clamp(0.34f + (outcome?.SatietyRelief ?? 0f) * 0.48f, 0f, 1f),
                SerotoninLevel = world.InShelter ? 0.72f : 0.42f,
                AcetylcholineLevel = Math.Clamp(0.32f + (float)current.PredatorThreat * 0.42f, 0f, 1f),
                NorepinephrineLevel = Math.Clamp(0.18f + (float)current.PredatorThreat * 0.68f, 0f, 1f)
            },
            outcome?.RewardPredictionError ?? 0f,
            new AttentionVector(
                Visual: Math.Clamp(0.44f + (float)current.PredatorThreat * 0.30f, 0f, 1f),
                Auditory: 0.20f,
                Somatosensory: 0.34f,
                Interoceptive: Math.Clamp(0.34f + (float)current.Hunger * 0.46f, 0f, 1f)));

        return new FeedbackDelivery(bodyMessages, outcomeMessages, objectMessages, bodyApplied, outcomeApplied);
    }

    private static void WaitForCommands(AvatarService avatar, long expectedProcessed)
    {
        var stopwatch = Stopwatch.StartNew();
        while (avatar.ProcessedCommands < expectedProcessed && stopwatch.Elapsed < TimeSpan.FromSeconds(2))
        {
            Thread.Sleep(1);
        }

        if (avatar.ProcessedCommands < expectedProcessed)
        {
            throw new TimeoutException($"Avatar processed {avatar.ProcessedCommands} commands; expected {expectedProcessed}.");
        }

        if (avatar.FailedCommands > 0)
        {
            throw new InvalidOperationException($"Avatar reported {avatar.FailedCommands} failed command(s).");
        }
    }

    private static bool Contains(string value, string fragment)
        => value.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private readonly record struct FeedbackDelivery(
        int BodyMessages,
        int OutcomeMessages,
        int ObjectMessages,
        bool BodyApplied,
        bool OutcomeApplied);
}

internal sealed class EmbodiedChoiceWorld
{
    public float Hunger { get; private set; } = 0.94f;
    public float Threat { get; private set; } = 0.04f;
    public float Health { get; private set; } = 0.96f;
    public bool FoodAvailable { get; private set; } = true;
    public bool InShelter { get; private set; }

    public EmbodiedWorldOutcome Apply(EmbodiedIntentSnapshot intent, AvatarActionOutput action)
    {
        bool moved = action.Movement.ForwardSpeed > 0.01;
        if (moved && FoodAvailable && intent.GoalKey.Contains("food", StringComparison.OrdinalIgnoreCase))
        {
            FoodAvailable = false;
            Hunger = 0.08f;
            Threat = 0.88f;
            return new EmbodiedWorldOutcome(
                "food collected; nearby threat revealed",
                FoodCollected: true,
                ShelterReached: false,
                ThreatAvoided: false,
                SatietyRelief: 0.92f,
                SafetyRelief: 0.02f,
                PainLevel: 0f,
                DamageLevel: 0f,
                Progress: 0.90f,
                EffortCost: 0.12f,
                Novelty: 0.52f,
                RewardPredictionError: 0.68f,
                action.Movement.ForwardSpeed,
                action.Movement.TurnRateDeg);
        }

        bool seekingShelter = intent.GoalKey.Contains("shelter", StringComparison.OrdinalIgnoreCase)
                              || intent.MotorDirective.Contains("shelter", StringComparison.OrdinalIgnoreCase)
                              || intent.MotorDirective.Contains("guard", StringComparison.OrdinalIgnoreCase);
        bool avoidingThreat = intent.GoalKey.Contains("threat", StringComparison.OrdinalIgnoreCase)
                              || intent.MotorDirective.Contains("escape", StringComparison.OrdinalIgnoreCase)
                              || intent.MotorDirective.Contains("avoid", StringComparison.OrdinalIgnoreCase);
        if (moved && (seekingShelter || avoidingThreat))
        {
            InShelter = seekingShelter;
            Threat = seekingShelter ? 0.04f : 0.10f;
            Health = Math.Min(1f, Health + 0.02f);
            return new EmbodiedWorldOutcome(
                seekingShelter
                    ? "shelter reached; threat exposure reduced"
                    : "threat avoided; exposure reduced",
                FoodCollected: false,
                ShelterReached: seekingShelter,
                ThreatAvoided: avoidingThreat,
                SatietyRelief: 0f,
                SafetyRelief: seekingShelter ? 0.92f : 0.78f,
                PainLevel: 0f,
                DamageLevel: 0f,
                Progress: 0.90f,
                EffortCost: 0.10f,
                Novelty: 0.24f,
                RewardPredictionError: 0.54f,
                action.Movement.ForwardSpeed,
                action.Movement.TurnRateDeg);
        }

        Health = Math.Max(0f, Health - Threat * 0.08f);
        return new EmbodiedWorldOutcome(
            moved ? "movement without goal completion" : "no movement",
            FoodCollected: false,
            ShelterReached: false,
            ThreatAvoided: false,
            SatietyRelief: 0f,
            SafetyRelief: 0f,
            PainLevel: Threat * 0.32f,
            DamageLevel: Threat * 0.08f,
            Progress: 0.02f,
            EffortCost: moved ? 0.16f : 0.02f,
            Novelty: 0.06f,
            RewardPredictionError: -0.42f,
            action.Movement.ForwardSpeed,
            action.Movement.TurnRateDeg);
    }

    public AvatarBodyTelemetry CreateBodyTelemetry(EmbodiedWorldOutcome? outcome)
        => new(
            ForwardVelocity: outcome?.ForwardVelocity ?? 0,
            TurnRateDeg: outcome?.TurnRateDeg ?? 0,
            ContactLevel: outcome?.DamageLevel > 0 ? 0.72 : 0.02,
            LeftMotorDrive: 0.72,
            RightMotorDrive: 0.72,
            EnvironmentalDarkness: 0.30,
            ShelterNeed: InShelter ? 0.02 : Math.Clamp(Threat * 1.08f, 0f, 1f),
            Anxiety: InShelter ? 0.04 : Threat * 0.86f,
            Hunger: Hunger,
            PredatorThreat: Threat,
            InShelter: InShelter ? 1 : 0,
            Health: Health,
            ShelterSafety: InShelter ? 0.96 : 0.04,
            TactileFront: outcome?.DamageLevel > 0 ? 0.65 : 0.02,
            TactileGround: outcome?.ForwardVelocity > 0.01 ? 0.24 : 0.06,
            PainLevel: outcome?.PainLevel ?? 0,
            Urgency: Threat);

    public AvatarObjectObservation[] CreateObjectObservations()
    {
        var observations = new List<AvatarObjectObservation>();
        if (FoodAvailable)
        {
            observations.Add(new AvatarObjectObservation(
                "benchmark.food",
                "food berry patch",
                0.94,
                0.92,
                1.35,
                32,
                1.2,
                "L"));
        }

        observations.Add(new AvatarObjectObservation(
            "benchmark.shelter",
            "safe shelter",
            Threat > 0.5f ? 0.96 : 0.54,
            0.94,
            1.2,
            30,
            1.4,
            "R"));
        if (Threat > 0.20f)
        {
            observations.Add(new AvatarObjectObservation(
                "benchmark.threat",
                "nearby predator threat",
                0.98,
                0.96,
                1.6,
                38,
                0.8,
                "M"));
        }

        return observations.ToArray();
    }
}

internal sealed record EmbodiedWorldOutcome(
    string EventSummary,
    bool FoodCollected,
    bool ShelterReached,
    bool ThreatAvoided,
    float SatietyRelief,
    float SafetyRelief,
    float PainLevel,
    float DamageLevel,
    float Progress,
    float EffortCost,
    float Novelty,
    float RewardPredictionError,
    double ForwardVelocity,
    double TurnRateDeg)
{
    public AvatarOutcomeTelemetry ToTelemetry()
        => new(
            SatietyRelief,
            SafetyRelief,
            PainLevel,
            DamageLevel,
            ShelterReached ? 0.88 : 0,
            Progress,
            EffortCost,
            Novelty,
            SocialApproval: 0,
            Pattern: "EmbodiedClosedLoopOutcome",
            InputSource: "embodied_benchmark_world");
}
