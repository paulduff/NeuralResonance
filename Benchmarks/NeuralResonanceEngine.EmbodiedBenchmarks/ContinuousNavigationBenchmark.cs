using System.Diagnostics;
using NeuralResonanceEngine.ControlProgram.Services;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

internal sealed record ContinuousNavigationScenarioResult(
    int Seed,
    string LayoutFingerprint,
    bool Passed,
    bool GoalReached,
    int ShortestPathCells,
    int CellTransitions,
    int Decisions,
    int Backtracks,
    int ExploredPlaces,
    int LearnedEdges,
    int AvatarPlaceMemories,
    int Collisions,
    int BrainMotorSpikes,
    int AvatarMotorEvents,
    int BodyMessages,
    int OutcomeMessages,
    int ObjectMessages,
    double PathEfficiency,
    double DistanceEfficiency,
    double DistanceTravelled,
    string LastEvidence,
    IReadOnlyList<string> Criteria);

internal sealed record ContinuousNavigationResult(
    string ProtocolVersion,
    bool Passed,
    double GeneralizationScore,
    double MeanPathEfficiency,
    double MeanDistanceEfficiency,
    int TotalCollisions,
    int TotalBrainMotorSpikes,
    int TotalAvatarMotorEvents,
    IReadOnlyList<ContinuousNavigationScenarioResult> Scenarios,
    IReadOnlyList<string> Criteria);

internal static class ContinuousNavigationBenchmark
{
    internal const string ProtocolVersion = "dnne.continuous-navigation.v1";
    private static readonly int[] DefaultSeeds = [317, 911, 2027];
    private const int MotorBurstCount = 16;
    private static readonly AvatarKinematicsOptions NavigationKinematics = new(
        MaxMotorDrive: 240.0,
        ForwardSpeedCoefficient: 0.0125,
        TurnSpeedCoefficient: 3.2,
        MinForwardSpeed: -1.6,
        MaxForwardSpeed: 3.2,
        MaxTurnRateDeg: 220.0,
        AllowSignedMotorDrive: true,
        InPlaceTurnCancelsForwardDrive: true);
    private static readonly AvatarBodyStateProfile BodyProfile = new(
        MaxForwardSpeed: 3.2,
        MaxTurnRateDeg: 220.0,
        BaseIntensity: 0.20,
        MotionIntensityWeight: 0.62,
        TurnIntensityWeight: 0.28,
        ContactIntensityWeight: 0.58,
        BaseBurstCount: 6,
        MotionBurstWeight: 10,
        TurnBurstWeight: 6,
        ContactBurstWeight: 10);

    public static ContinuousNavigationResult Run(IReadOnlyList<int>? seeds = null)
    {
        int[] selectedSeeds = (seeds is null || seeds.Count == 0 ? DefaultSeeds : seeds)
            .Distinct()
            .ToArray();
        ContinuousNavigationScenarioResult[] scenarios = selectedSeeds
            .Select(RunScenario)
            .ToArray();
        bool uniqueWorlds = scenarios.Select(static scenario => scenario.LayoutFingerprint).Distinct().Count() == scenarios.Length;
        bool allReached = scenarios.All(static scenario => scenario.GoalReached);
        bool allBoundaries = scenarios.All(static scenario =>
            scenario.BrainMotorSpikes > 0 && scenario.AvatarMotorEvents > 0 &&
            scenario.BodyMessages > 0 && scenario.OutcomeMessages > 0 && scenario.ObjectMessages > 0);
        bool learnedPlaces = scenarios.All(static scenario => scenario.ExploredPlaces > 1 && scenario.AvatarPlaceMemories > 1);
        bool collisionControl = scenarios.All(static scenario => scenario.Collisions <= 2);
        double meanPathEfficiency = scenarios.Average(static scenario => scenario.PathEfficiency);
        double meanDistanceEfficiency = scenarios.Average(static scenario => scenario.DistanceEfficiency);
        bool efficientEnough = meanPathEfficiency >= 0.20 && meanDistanceEfficiency >= 0.18;
        var criteria = new Dictionary<string, bool>
        {
            ["all unseen seeded mazes reach the goal"] = allReached,
            ["each seed produces a distinct hidden maze"] = uniqueWorlds,
            ["brain-avatar-world feedback crosses every boundary"] = allBoundaries,
            ["hippocampal and avatar place maps grow"] = learnedPlaces,
            ["local wall probes prevent repeated collisions"] = collisionControl,
            ["navigation remains above the generalization efficiency floor"] = efficientEnough
        };

        return new ContinuousNavigationResult(
            ProtocolVersion,
            criteria.Values.All(static passed => passed) && scenarios.All(static scenario => scenario.Passed),
            scenarios.Count(static scenario => scenario.Passed) / (double)scenarios.Length,
            meanPathEfficiency,
            meanDistanceEfficiency,
            scenarios.Sum(static scenario => scenario.Collisions),
            scenarios.Sum(static scenario => scenario.BrainMotorSpikes),
            scenarios.Sum(static scenario => scenario.AvatarMotorEvents),
            scenarios,
            criteria.Select(static pair => $"{pair.Key}: {(pair.Value ? "PASS" : "FAIL")}").ToArray());
    }

    public static string RenderMarkdown(ContinuousNavigationResult result)
    {
        var lines = new List<string>
        {
            "# DNNE Continuous Navigation Benchmark",
            string.Empty,
            $"- Protocol: `{result.ProtocolVersion}`",
            $"- Status: **{(result.Passed ? "PASS" : "FAIL")}**",
            $"- Generalization: `{result.GeneralizationScore:P1}`",
            $"- Mean path efficiency: `{result.MeanPathEfficiency:P1}`",
            $"- Mean distance efficiency: `{result.MeanDistanceEfficiency:P1}`",
            $"- Total collisions: `{result.TotalCollisions}`",
            $"- Brain motor spikes: `{result.TotalBrainMotorSpikes}`",
            $"- Avatar motor events: `{result.TotalAvatarMotorEvents}`",
            string.Empty,
            "## Unseen Mazes",
            string.Empty,
            "| Seed | Fingerprint | Goal | Shortest | Actual | Efficiency | Backtracks | Places | Collisions |",
            "| ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |"
        };
        lines.AddRange(result.Scenarios.Select(static scenario =>
            $"| {scenario.Seed} | `{scenario.LayoutFingerprint}` | {(scenario.GoalReached ? "reached" : "missed")} | {scenario.ShortestPathCells} | {scenario.CellTransitions} | {scenario.PathEfficiency:P1} | {scenario.Backtracks} | {scenario.ExploredPlaces} | {scenario.Collisions} |"));
        lines.AddRange(
        [
            string.Empty,
            "## Boundary Traffic",
            string.Empty
        ]);
        foreach (ContinuousNavigationScenarioResult scenario in result.Scenarios)
        {
            lines.Add($"- seed `{scenario.Seed}`: {scenario.BrainMotorSpikes} spikes -> {scenario.AvatarMotorEvents} motor events; {scenario.BodyMessages} body, {scenario.OutcomeMessages} outcome, {scenario.ObjectMessages} object messages; {scenario.AvatarPlaceMemories} avatar place memories.");
        }

        lines.AddRange(
        [
            string.Empty,
            "## Criteria",
            string.Empty
        ]);
        lines.AddRange(result.Criteria.Select(static criterion => $"- {criterion}"));
        lines.Add(string.Empty);
        lines.Add("The navigator receives no maze layout or shortest path. It incrementally binds locally observed places and open edges, uses goal-relative heading as a bias, and replays parent place sequences to escape dead ends. Motion is generated as M1 directives, integrated by the production avatar, and applied through continuous collision physics.");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static ContinuousNavigationScenarioResult RunScenario(int seed)
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 10.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        using var avatar = new AvatarService(
            new AvatarNervousSystemOptions(NavigationKinematics, IdleMotorFallbackTicks: int.MaxValue),
            $"NRE.NavigationBenchmark.Avatar.{seed}",
            new AvatarServiceClockOptions(Enabled: false));
        var world = new AvatarMazeEnvironment(new AvatarMazeOptions(Seed: seed));
        var navigator = new HippocampalNavigationRuntime();
        AvatarMazeTransition? feedbackTransition = null;
        int brainMotorSpikes = 0;
        int avatarMotorEvents = 0;
        int bodyMessages = 0;
        int outcomeMessages = 0;
        int objectMessages = 0;
        string lastEvidence = "none";
        int maxDecisions = (world.WalkableCellCount * 2) + 8;

        while (!world.GoalReached && navigator.DecisionCount < maxDecisions)
        {
            NavigationFeedbackTraffic traffic = DeliverNavigationFeedback(avatar, state, world, feedbackTransition);
            bodyMessages += traffic.BodyMessages;
            outcomeMessages += traffic.OutcomeMessages;
            objectMessages += traffic.ObjectMessages;

            AvatarMazeObservation sensory = world.Observe();
            HippocampalNavigationDecision decision = navigator.Decide(new HippocampalNavigationObservation(
                sensory.Row,
                sensory.Column,
                sensory.HeadingQuarter,
                sensory.ForwardOpen,
                sensory.LeftOpen,
                sensory.RightOpen,
                sensory.RearOpen,
                sensory.GoalRow,
                sensory.GoalColumn,
                sensory.GoalBearingDeg,
                sensory.DistanceToGoal,
                sensory.CollisionCount,
                sensory.GoalReached));
            lastEvidence = decision.Evidence;

            AvatarMazeTransition transition = ExecuteDecision(
                avatar,
                world,
                decision,
                ref brainMotorSpikes,
                ref avatarMotorEvents);
            feedbackTransition = transition;
            if (transition.Collision)
            {
                break;
            }
        }

        if (feedbackTransition is AvatarMazeTransition finalTransition)
        {
            NavigationFeedbackTraffic traffic = DeliverNavigationFeedback(avatar, state, world, finalTransition);
            bodyMessages += traffic.BodyMessages;
            outcomeMessages += traffic.OutcomeMessages;
            objectMessages += traffic.ObjectMessages;
        }

        int transitions = Math.Max(1, world.TransitionCount);
        double shortestDistance = world.ShortestPathCells * world.CellSize;
        double pathEfficiency = Math.Clamp(world.ShortestPathCells / (double)transitions, 0.0, 1.0);
        double distanceEfficiency = Math.Clamp(shortestDistance / Math.Max(world.CellSize, world.TotalDistanceTravelled), 0.0, 1.0);
        int avatarPlaceMemories = avatar.PlaceMemories.Count;
        var scenarioCriteria = new Dictionary<string, bool>
        {
            ["goal reached"] = world.GoalReached,
            ["decision bound respected"] = navigator.DecisionCount <= maxDecisions,
            ["motor boundary active"] = brainMotorSpikes > 0 && avatarMotorEvents > 0,
            ["sensory feedback active"] = bodyMessages > 0 && outcomeMessages > 0 && objectMessages > 0,
            ["place map formed"] = navigator.ExploredPlaceCount > 1 && avatarPlaceMemories > 1,
            ["collision budget respected"] = world.CollisionCount <= 2
        };

        return new ContinuousNavigationScenarioResult(
            seed,
            world.LayoutFingerprint(),
            scenarioCriteria.Values.All(static passed => passed),
            world.GoalReached,
            world.ShortestPathCells,
            world.TransitionCount,
            navigator.DecisionCount,
            navigator.BacktrackCount,
            navigator.ExploredPlaceCount,
            navigator.LearnedEdgeCount,
            avatarPlaceMemories,
            world.CollisionCount,
            brainMotorSpikes,
            avatarMotorEvents,
            bodyMessages,
            outcomeMessages,
            objectMessages,
            pathEfficiency,
            distanceEfficiency,
            world.TotalDistanceTravelled,
            lastEvidence,
            scenarioCriteria.Select(static pair => $"{pair.Key}: {(pair.Value ? "PASS" : "FAIL")}").ToArray());
    }

    private static AvatarMazeTransition ExecuteDecision(
        AvatarService avatar,
        AvatarMazeEnvironment world,
        HippocampalNavigationDecision decision,
        ref int brainMotorSpikes,
        ref int avatarMotorEvents)
    {
        AvatarMazeTransition? aggregate = null;
        if (!string.Equals(decision.TurnDirective, "motor_forward", StringComparison.Ordinal))
        {
            AvatarActionOutput turn = DeliverMotorDirective(avatar, decision.TurnDirective, ref brainMotorSpikes, ref avatarMotorEvents);
            double targetHeading = AvatarMazeEnvironment.HeadingDegreesForQuarter(decision.TargetHeadingQuarter);
            double error = NormalizeSignedDegrees(targetHeading - world.HeadingDeg);
            if (Math.Abs(turn.Movement.TurnRateDeg) < 0.001 || Math.Sign(error) != Math.Sign(turn.Movement.TurnRateDeg))
            {
                throw new InvalidOperationException($"Motor directive {decision.TurnDirective} produced turn {turn.Movement.TurnRateDeg:F3} for heading error {error:F3}.");
            }

            double turnDuration = Math.Abs(error / turn.Movement.TurnRateDeg);
            aggregate = world.Advance(turn.Movement, turnDuration);
        }

        AvatarActionOutput forward = DeliverMotorDirective(avatar, decision.ForwardDirective, ref brainMotorSpikes, ref avatarMotorEvents);
        if (forward.Movement.ForwardSpeed <= 0.001)
        {
            throw new InvalidOperationException($"Motor directive {decision.ForwardDirective} produced no forward motion.");
        }

        double targetX = decision.TargetColumn * world.CellSize;
        double targetZ = decision.TargetRow * world.CellSize;
        bool enteredNewCell = false;
        double totalProgress = 0.0;
        for (int substep = 0; substep < 80; substep++)
        {
            double distance = Math.Sqrt(DistanceSquared(world.X, world.Z, targetX, targetZ));
            if (distance <= 0.012)
            {
                break;
            }

            double dt = Math.Min(0.04, distance / forward.Movement.ForwardSpeed);
            AvatarMazeTransition step = world.Advance(forward.Movement, dt);
            enteredNewCell |= step.EnteredNewCell;
            totalProgress += step.Progress;
            aggregate = step;
            if (step.Collision)
            {
                break;
            }
        }

        AvatarMazeTransition result = aggregate ?? throw new InvalidOperationException("Navigation decision produced no world transition.");
        return result with
        {
            EnteredNewCell = enteredNewCell,
            Progress = Math.Clamp(totalProgress, -1.0, 1.0)
        };
    }

    private static AvatarActionOutput DeliverMotorDirective(
        AvatarService avatar,
        string directive,
        ref int brainMotorSpikes,
        ref int avatarMotorEvents)
    {
        long expected = avatar.EnqueuedCommands;
        avatar.PostResetMotor();
        WaitForCommands(avatar, ++expected);
        int spikeBase = brainMotorSpikes;
        AvatarDispatchSpike[] spikes = Enumerable.Range(0, MotorBurstCount)
            .Select(index => new AvatarDispatchSpike(
                "M1",
                (index & 1) == 0 ? "L" : "R",
                2_000 + spikeBase + index,
                $"{directive}_{spikeBase + index}"))
            .ToArray();
        avatar.PostBrainSignals(
            spikes,
            new AvatarNervousSystemBodyState(
                IsSleeping: false,
                Hunger: 0.32,
                Threat: 0.02,
                Health: 1.0,
                SecondsSinceProgress: 0.0,
                NoProgressTimeoutSeconds: 4.0));
        WaitForCommands(avatar, ++expected);
        AvatarActionOutput output = avatar.PublishActionOutput();
        brainMotorSpikes += spikes.Length;
        avatarMotorEvents += avatar.LatestSignal.MotorEvents;
        return output;
    }

    private static NavigationFeedbackTraffic DeliverNavigationFeedback(
        AvatarService avatar,
        SimulationState state,
        AvatarMazeEnvironment world,
        AvatarMazeTransition? transition)
    {
        long expected = avatar.EnqueuedCommands;
        avatar.PostBodyInput(world.CreateBodyTelemetry(transition), BodyProfile);
        expected++;
        if (transition is AvatarMazeTransition value)
        {
            avatar.PostOutcome(world.CreateOutcomeTelemetry(value));
            expected++;
        }

        avatar.PostPlaceObservations([world.CreatePlaceObservation()]);
        expected++;
        avatar.PostObjectCandidates([world.CreateGoalObservation()], maxObservations: 1);
        expected++;
        WaitForCommands(avatar, expected);

        int bodyMessages = 0;
        int outcomeMessages = 0;
        int objectMessages = 0;
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
        }

        while (avatar.TryDequeueOutcome(out AvatarOutcomeTelemetry outcome))
        {
            outcomeMessages++;
            OutcomeInputRequest request = AvatarOutcomeInputFactory.CreateRequest(outcome);
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

        float rewardPredictionError = transition switch
        {
            { GoalReached: true } => 0.92f,
            { Collision: true } => -0.48f,
            { Progress: > 0.0 } progressTransition => Math.Clamp((float)progressTransition.Progress * 0.24f, 0.02f, 0.24f),
            _ => 0f
        };
        state.UpdateNeuromod(
            new NeuromodState
            {
                DopamineLevel = Math.Clamp(0.34f + Math.Max(0f, rewardPredictionError) * 0.48f, 0f, 1f),
                SerotoninLevel = world.GoalReached ? 0.76f : 0.42f,
                AcetylcholineLevel = 0.58f,
                NorepinephrineLevel = transition?.Collision == true ? 0.72f : 0.28f
            },
            rewardPredictionError,
            new AttentionVector(Visual: 0.72f, Auditory: 0.12f, Somatosensory: 0.48f, Interoceptive: 0.22f));
        for (int tick = 0; tick < 2; tick++)
        {
            state.AdvanceClockAndCreateTickSignal();
            state.ObserveCognitiveRuntime(
                state.Tick,
                dispatchedSpikes: MotorBurstCount,
                activePathwayCount: 7,
                rewardPredictionError: tick == 0 ? rewardPredictionError : 0f,
                dominantPathway: null);
        }

        return new NavigationFeedbackTraffic(bodyMessages, outcomeMessages, objectMessages);
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

    private static double NormalizeSignedDegrees(double value)
    {
        double wrapped = ((value + 540.0) % 360.0) - 180.0;
        return wrapped == -180.0 ? 180.0 : wrapped;
    }

    private static double DistanceSquared(double x1, double z1, double x2, double z2)
    {
        double dx = x2 - x1;
        double dz = z2 - z1;
        return (dx * dx) + (dz * dz);
    }

    private readonly record struct NavigationFeedbackTraffic(int BodyMessages, int OutcomeMessages, int ObjectMessages);
}
