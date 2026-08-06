using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal static class NeuronalMotorQualificationProtocol
{
    public const string Version = "dnne.neuronal-motor-qualification.v1";
    public const string LiveEvidenceSource = "LiveStructureServices";
    public const string OfflineEvidenceSource = "OfflineCausalPreflight";
    public const string TrainingSplit = "training";
    public const string HeldOutSplit = "held-out";
}

internal sealed record NeuronalMotorQualificationScenario(
    string ScenarioId,
    string Split,
    int Seed,
    string ExpectedMode,
    string EvidenceSource,
    string LayoutFingerprint);

internal sealed record NeuronalMotorQualificationSample(
    DateTimeOffset CapturedAtUtc,
    long Tick,
    long StateTick,
    long Sequence,
    string Mode,
    bool Active,
    bool Sleeping,
    bool PromotionReady,
    double LeftDrive,
    double RightDrive,
    double MotorCircuitCoverage,
    double Confidence,
    double ConfidenceEma,
    double Agreement,
    double AgreementEma,
    long EvaluationSamples,
    long ActiveEvaluationSamples,
    int QualifiedConsecutiveTicks,
    bool ActionCircuitObserved,
    double ActionSelectionConfidence,
    double ActionCircuitCoverage,
    double ActionSelectionMargin,
    long BodyInputTick,
    double ForwardVelocity,
    double TurnRateDeg,
    long OutcomeInputTick,
    double OutcomeProgress,
    double OutcomeDamage,
    bool SymbolicScaffoldCanAuthorize,
    bool SemanticMotorInjectionAllowed,
    bool WorldGoalSteeringAllowed);

internal sealed record NeuronalMotorCausalCheck(
    string Name,
    bool Passed,
    string Evidence);

internal sealed record NeuronalMotorCausalPreflightResult(
    string ProtocolVersion,
    string EvidenceSource,
    bool Passed,
    IReadOnlyList<NeuronalMotorCausalCheck> Checks);

internal sealed record NeuronalMotorQualificationCapture(
    string ProtocolVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    NeuronalMotorQualificationScenario Scenario,
    NeuronalMotorControlSettings Settings,
    IReadOnlyList<NeuronalMotorQualificationSample> Samples,
    NeuronalMotorCausalPreflightResult CausalPreflight,
    IReadOnlyList<string> CollectionErrors);

internal sealed record NeuronalMotorQualificationCriterion(
    string Name,
    bool Passed,
    string Evidence);

internal sealed record NeuronalMotorQualificationMetrics(
    int DistinctRuntimeSamples,
    long ActiveEvaluationSampleDelta,
    long EvaluationSampleDelta,
    int DistinctBodyFeedbackTicks,
    int DistinctOutcomeFeedbackTicks,
    int MovingFeedbackSamples,
    long MaximumStateTickSkew,
    double MeanMotorCoverage,
    double MinimumMotorCoverage,
    double FinalConfidenceEma,
    double FinalAgreementEma,
    int MaximumQualifiedStreak,
    double ActionCircuitObservationRate,
    int SleepSamples,
    int ActiveSleepViolations);

internal sealed record NeuronalMotorQualificationReport(
    string ProtocolVersion,
    DateTimeOffset EvaluatedAtUtc,
    NeuronalMotorQualificationScenario Scenario,
    bool Passed,
    bool EligibleForPrimaryAuthority,
    string Decision,
    string CircuitSizingRecommendation,
    string CaptureFingerprint,
    NeuronalMotorQualificationMetrics Metrics,
    IReadOnlyList<NeuronalMotorQualificationCriterion> Criteria);

internal sealed record NeuronalMotorQualificationCampaignReport(
    string ProtocolVersion,
    DateTimeOffset EvaluatedAtUtc,
    string Phase,
    bool Passed,
    bool EligibleForPrimaryAuthority,
    string Decision,
    int TrainingScenarios,
    int HeldOutScenarios,
    int DistinctSeeds,
    IReadOnlyList<string> ScenarioFingerprints,
    IReadOnlyList<NeuronalMotorQualificationCriterion> Criteria);

internal static class NeuronalMotorQualificationEvaluator
{
    private const double MovementEpsilon = 0.01;

    public static NeuronalMotorQualificationReport Evaluate(NeuronalMotorQualificationCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var samples = capture.Samples
            .GroupBy(static sample => sample.Sequence)
            .Select(static group => group.OrderBy(static sample => sample.CapturedAtUtc).Last())
            .OrderBy(static sample => sample.Sequence)
            .ToArray();
        var first = samples.FirstOrDefault();
        var last = samples.LastOrDefault();
        var settings = capture.Settings;
        var minimumObservedSamples = Math.Clamp(settings.PromotionMinimumSamples / 10, 25, 100);
        var activeSampleDelta = CounterDelta(first?.ActiveEvaluationSamples, last?.ActiveEvaluationSamples);
        var evaluationSampleDelta = CounterDelta(first?.EvaluationSamples, last?.EvaluationSamples);
        var bodyTicks = samples
            .Select(static sample => sample.BodyInputTick)
            .Where(static tick => tick >= 0)
            .Distinct()
            .Count();
        var outcomeTicks = samples
            .Select(static sample => sample.OutcomeInputTick)
            .Where(static tick => tick >= 0)
            .Distinct()
            .Count();
        var movingSamples = samples.Count(static sample =>
            Math.Abs(sample.ForwardVelocity) >= MovementEpsilon ||
            Math.Abs(sample.TurnRateDeg) >= MovementEpsilon);
        var maximumStateTickSkew = samples.Length == 0
            ? long.MaxValue
            : samples.Max(static sample => AbsoluteDifference(sample.Tick, sample.StateTick));
        var maximumAllowedTickSkew = Math.Clamp(settings.PopulationSnapshotMaxAgeTicks / 4, 4, 24);
        var activeSamples = samples.Where(static sample => sample.Active).ToArray();
        var actionObserved = activeSamples.Count(static sample => sample.ActionCircuitObserved);
        var actionObservationRate = activeSamples.Length == 0
            ? 0.0
            : actionObserved / (double)activeSamples.Length;
        var sleepSamples = samples.Count(static sample => sample.Sleeping);
        var activeSleepViolations = samples.Count(static sample =>
            sample.Sleeping &&
            (sample.Active || Math.Abs(sample.LeftDrive) >= MovementEpsilon || Math.Abs(sample.RightDrive) >= MovementEpsilon));
        var meanCoverage = samples.Length == 0 ? 0.0 : samples.Average(static sample => sample.MotorCircuitCoverage);
        var minimumCoverage = samples.Length == 0 ? 0.0 : samples.Min(static sample => sample.MotorCircuitCoverage);
        var maxQualifiedStreak = samples.Length == 0 ? 0 : samples.Max(static sample => sample.QualifiedConsecutiveTicks);
        var expectedMode = NormalizeMode(capture.Scenario.ExpectedMode);
        var split = NormalizeSplit(capture.Scenario.Split);

        var criteria = new List<NeuronalMotorQualificationCriterion>
        {
            Criterion(
                "protocol is recognized",
                string.Equals(capture.ProtocolVersion, NeuronalMotorQualificationProtocol.Version, StringComparison.Ordinal),
                capture.ProtocolVersion),
            Criterion(
                "evidence comes from live structure services",
                string.Equals(capture.Scenario.EvidenceSource, NeuronalMotorQualificationProtocol.LiveEvidenceSource, StringComparison.Ordinal),
                capture.Scenario.EvidenceSource),
            Criterion(
                "scenario split is declared",
                split is NeuronalMotorQualificationProtocol.TrainingSplit or NeuronalMotorQualificationProtocol.HeldOutSplit,
                string.IsNullOrWhiteSpace(split) ? "missing" : split),
            Criterion(
                "world layout is fingerprinted",
                IsVerifiedFingerprint(capture.Scenario.LayoutFingerprint),
                string.IsNullOrWhiteSpace(capture.Scenario.LayoutFingerprint) ? "missing" : capture.Scenario.LayoutFingerprint),
            Criterion(
                "collection completed without errors",
                capture.CollectionErrors.Count == 0,
                capture.CollectionErrors.Count == 0 ? "none" : string.Join(" | ", capture.CollectionErrors)),
            Criterion(
                "runtime remained in the declared phase",
                samples.Length > 0 && samples.All(sample => string.Equals(NormalizeMode(sample.Mode), expectedMode, StringComparison.Ordinal)),
                $"expected={expectedMode}; observed={string.Join(',', samples.Select(static sample => sample.Mode).Distinct(StringComparer.OrdinalIgnoreCase))}"),
            Criterion(
                "enough distinct runtime observations were captured",
                samples.Length >= minimumObservedSamples,
                $"observed={samples.Length}; required={minimumObservedSamples}"),
            Criterion(
                "motor and embodied state samples are temporally aligned",
                maximumStateTickSkew <= maximumAllowedTickSkew,
                $"maximum tick skew={maximumStateTickSkew}; allowed={maximumAllowedTickSkew}"),
            Criterion(
                "active evaluation sample gate was earned during this capture",
                activeSampleDelta >= settings.PromotionMinimumSamples,
                $"delta={activeSampleDelta}; required={settings.PromotionMinimumSamples}"),
            Criterion(
                "bilateral motor coverage meets the promotion floor",
                last is not null && last.MotorCircuitCoverage >= settings.PromotionMinimumCoverage,
                $"final={last?.MotorCircuitCoverage ?? 0.0:0.000}; required={settings.PromotionMinimumCoverage:0.000}"),
            Criterion(
                "confidence EMA meets the promotion floor",
                last is not null && last.ConfidenceEma >= settings.PromotionMinimumConfidence,
                $"final={last?.ConfidenceEma ?? 0.0:0.000}; required={settings.PromotionMinimumConfidence:0.000}"),
            Criterion(
                "agreement EMA meets the migration floor",
                last is not null && last.AgreementEma >= settings.PromotionMinimumAgreement,
                $"final={last?.AgreementEma ?? 0.0:0.000}; required={settings.PromotionMinimumAgreement:0.000}"),
            Criterion(
                "qualified streak is sustained",
                maxQualifiedStreak >= settings.PromotionConsecutiveTicks,
                $"maximum={maxQualifiedStreak}; required={settings.PromotionConsecutiveTicks}"),
            Criterion(
                "runtime promotion gate reports ready",
                last?.PromotionReady == true,
                $"final={last?.PromotionReady ?? false}"),
            Criterion(
                "action-selection circuit is observed on active samples",
                activeSamples.Length > 0 && actionObservationRate >= 0.80,
                $"observed={actionObserved}/{activeSamples.Length} ({actionObservationRate:P1})"),
            Criterion(
                "body feedback advances",
                bodyTicks >= 2,
                $"distinct body input ticks={bodyTicks}"),
            Criterion(
                "world outcome feedback advances",
                outcomeTicks >= 2,
                $"distinct outcome input ticks={outcomeTicks}"),
            Criterion(
                "embodied movement is observed",
                movingSamples > 0,
                $"moving feedback samples={movingSamples}"),
            Criterion(
                "sleep suppresses motor authority",
                activeSleepViolations == 0,
                $"sleep samples={sleepSamples}; active violations={activeSleepViolations}"),
            Criterion(
                "symbolic authority remains disabled",
                samples.Length > 0 && samples.All(static sample =>
                    !sample.SymbolicScaffoldCanAuthorize &&
                    !sample.SemanticMotorInjectionAllowed &&
                    !sample.WorldGoalSteeringAllowed),
                "symbolic scaffold, semantic motor injection, and world-goal steering must remain false"),
            Criterion(
                "offline causal preflight passes",
                capture.CausalPreflight.Passed && capture.CausalPreflight.Checks.All(static check => check.Passed),
                $"{capture.CausalPreflight.Checks.Count(static check => check.Passed)}/{capture.CausalPreflight.Checks.Count} checks passed")
        };

        var passed = criteria.All(static criterion => criterion.Passed);
        const bool eligibleForPrimary = false;
        var decision = passed
            ? string.Equals(expectedMode, NeuronalMotorModes.Assist, StringComparison.Ordinal)
                ? "ScenarioQualifiedForPrimaryCampaign"
                : "ScenarioQualifiedForAssistCampaign"
            : "ScenarioRejected";
        var sizingRecommendation = ResolveSizingRecommendation(last, meanCoverage, settings);
        var metrics = new NeuronalMotorQualificationMetrics(
            samples.Length,
            activeSampleDelta,
            evaluationSampleDelta,
            bodyTicks,
            outcomeTicks,
            movingSamples,
            maximumStateTickSkew,
            meanCoverage,
            minimumCoverage,
            last?.ConfidenceEma ?? 0.0,
            last?.AgreementEma ?? 0.0,
            maxQualifiedStreak,
            actionObservationRate,
            sleepSamples,
            activeSleepViolations);

        return new NeuronalMotorQualificationReport(
            NeuronalMotorQualificationProtocol.Version,
            DateTimeOffset.UtcNow,
            capture.Scenario with { Split = split, ExpectedMode = expectedMode },
            passed,
            eligibleForPrimary,
            decision,
            sizingRecommendation,
            Fingerprint(capture),
            metrics,
            criteria);
    }

    public static NeuronalMotorQualificationCampaignReport EvaluateCampaign(
        string phase,
        IReadOnlyList<NeuronalMotorQualificationReport> reports,
        int minimumTrainingScenarios = 3,
        int minimumHeldOutScenarios = 3)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var normalizedPhase = NormalizeMode(phase);
        if (normalizedPhase is not (NeuronalMotorModes.Shadow or NeuronalMotorModes.Assist))
        {
            normalizedPhase = string.Empty;
        }

        var phaseReports = reports
            .Where(report => string.Equals(NormalizeMode(report.Scenario.ExpectedMode), normalizedPhase, StringComparison.Ordinal))
            .ToArray();
        var training = phaseReports
            .Where(static report => string.Equals(report.Scenario.Split, NeuronalMotorQualificationProtocol.TrainingSplit, StringComparison.Ordinal))
            .ToArray();
        var heldOut = phaseReports
            .Where(static report => string.Equals(report.Scenario.Split, NeuronalMotorQualificationProtocol.HeldOutSplit, StringComparison.Ordinal))
            .ToArray();
        var distinctSeeds = phaseReports.Select(static report => report.Scenario.Seed).Distinct().Count();
        var distinctLayouts = phaseReports
            .Select(static report => report.Scenario.LayoutFingerprint)
            .Where(IsVerifiedFingerprint)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var criteria = new List<NeuronalMotorQualificationCriterion>
        {
            Criterion(
                "campaign phase is Shadow or Assist",
                normalizedPhase.Length > 0,
                normalizedPhase.Length == 0 ? phase : normalizedPhase),
            Criterion(
                "campaign phase reports are available",
                phaseReports.Length > 0,
                $"phase reports={phaseReports.Length}; reports scanned={reports.Count}"),
            Criterion(
                "minimum training scenarios pass",
                training.Length >= minimumTrainingScenarios && training.All(static report => report.Passed),
                $"passing={training.Count(static report => report.Passed)}/{training.Length}; required={minimumTrainingScenarios}"),
            Criterion(
                "minimum held-out scenarios pass",
                heldOut.Length >= minimumHeldOutScenarios && heldOut.All(static report => report.Passed),
                $"passing={heldOut.Count(static report => report.Passed)}/{heldOut.Length}; required={minimumHeldOutScenarios}"),
            Criterion(
                "seeds are distinct across the campaign",
                distinctSeeds == phaseReports.Length,
                $"distinct={distinctSeeds}; scenarios={phaseReports.Length}"),
            Criterion(
                "layouts are distinct and fingerprinted",
                distinctLayouts == phaseReports.Length,
                $"distinct verified layouts={distinctLayouts}; scenarios={phaseReports.Length}"),
            Criterion(
                "every scenario used live evidence",
                phaseReports.Length > 0 && phaseReports.All(static report =>
                    string.Equals(report.Scenario.EvidenceSource, NeuronalMotorQualificationProtocol.LiveEvidenceSource, StringComparison.Ordinal)),
                $"live={phaseReports.Count(static report => string.Equals(report.Scenario.EvidenceSource, NeuronalMotorQualificationProtocol.LiveEvidenceSource, StringComparison.Ordinal))}/{phaseReports.Length}"),
            Criterion(
                "every scenario preserves symbolic-authority isolation",
                phaseReports.Length > 0 && phaseReports.All(static report =>
                    report.Criteria.Any(static criterion => criterion.Name == "symbolic authority remains disabled" && criterion.Passed)),
                "all scenario authority criteria must pass")
        };

        var passed = criteria.All(static criterion => criterion.Passed);
        var eligibleForPrimary = passed && string.Equals(normalizedPhase, NeuronalMotorModes.Assist, StringComparison.Ordinal);
        var decision = !passed
            ? "CampaignRejected"
            : eligibleForPrimary
                ? "ReadyForGuardedPrimary"
                : "ReadyForAssist";

        return new NeuronalMotorQualificationCampaignReport(
            NeuronalMotorQualificationProtocol.Version,
            DateTimeOffset.UtcNow,
            normalizedPhase,
            passed,
            eligibleForPrimary,
            decision,
            training.Length,
            heldOut.Length,
            distinctSeeds,
            phaseReports.Select(static report => report.CaptureFingerprint).ToArray(),
            criteria);
    }

    public static string RenderMarkdown(NeuronalMotorQualificationReport report)
    {
        var lines = new List<string>
        {
            "# DNNE Neuronal Motor Scenario Qualification",
            string.Empty,
            $"- Protocol: `{report.ProtocolVersion}`",
            $"- Scenario: `{report.Scenario.ScenarioId}`",
            $"- Split / seed: `{report.Scenario.Split}` / `{report.Scenario.Seed}`",
            $"- Mode: `{report.Scenario.ExpectedMode}`",
            $"- Evidence: `{report.Scenario.EvidenceSource}`",
            $"- Status: **{(report.Passed ? "PASS" : "FAIL")}**",
            $"- Decision: **{report.Decision}**",
            $"- Primary authority eligible: `{report.EligibleForPrimaryAuthority}`",
            $"- Capture fingerprint: `{report.CaptureFingerprint}`",
            $"- Circuit sizing: {report.CircuitSizingRecommendation}",
            string.Empty,
            "## Metrics",
            string.Empty,
            $"- Distinct runtime samples: `{report.Metrics.DistinctRuntimeSamples}`",
            $"- Active evaluation delta: `{report.Metrics.ActiveEvaluationSampleDelta}`",
            $"- Maximum motor/state tick skew: `{report.Metrics.MaximumStateTickSkew}`",
            $"- Mean / minimum coverage: `{report.Metrics.MeanMotorCoverage:0.000}` / `{report.Metrics.MinimumMotorCoverage:0.000}`",
            $"- Final confidence / agreement EMA: `{report.Metrics.FinalConfidenceEma:0.000}` / `{report.Metrics.FinalAgreementEma:0.000}`",
            $"- Maximum qualified streak: `{report.Metrics.MaximumQualifiedStreak}`",
            $"- Action-circuit observation rate: `{report.Metrics.ActionCircuitObservationRate:P1}`",
            $"- Body / outcome feedback ticks: `{report.Metrics.DistinctBodyFeedbackTicks}` / `{report.Metrics.DistinctOutcomeFeedbackTicks}`",
            string.Empty,
            "## Criteria",
            string.Empty
        };
        lines.AddRange(report.Criteria.Select(static criterion =>
            $"- {(criterion.Passed ? "PASS" : "FAIL")}: {criterion.Name} ({criterion.Evidence})"));
        lines.AddRange(
        [
            string.Empty,
            "This report never changes motor mode. Promotion remains an explicit, logged operator action after a complete campaign passes.",
            string.Empty
        ]);
        return string.Join(Environment.NewLine, lines);
    }

    public static string RenderMarkdown(NeuronalMotorQualificationCampaignReport report)
    {
        var lines = new List<string>
        {
            "# DNNE Neuronal Motor Qualification Campaign",
            string.Empty,
            $"- Protocol: `{report.ProtocolVersion}`",
            $"- Phase: `{report.Phase}`",
            $"- Status: **{(report.Passed ? "PASS" : "FAIL")}**",
            $"- Decision: **{report.Decision}**",
            $"- Primary authority eligible: `{report.EligibleForPrimaryAuthority}`",
            $"- Training / held-out scenarios: `{report.TrainingScenarios}` / `{report.HeldOutScenarios}`",
            $"- Distinct seeds: `{report.DistinctSeeds}`",
            string.Empty,
            "## Criteria",
            string.Empty
        };
        lines.AddRange(report.Criteria.Select(static criterion =>
            $"- {(criterion.Passed ? "PASS" : "FAIL")}: {criterion.Name} ({criterion.Evidence})"));
        lines.AddRange(
        [
            string.Empty,
            report.EligibleForPrimaryAuthority
                ? "The evidence permits a guarded Primary canary; it does not change runtime authority automatically."
                : "Primary remains locked. Resolve failed criteria or complete the next campaign phase.",
            string.Empty
        ]);
        return string.Join(Environment.NewLine, lines);
    }

    private static NeuronalMotorQualificationCriterion Criterion(string name, bool passed, string evidence)
        => new(name, passed, evidence);

    private static long CounterDelta(long? first, long? last)
        => first is null || last is null || last < first ? 0 : last.Value - first.Value;

    private static long AbsoluteDifference(long left, long right)
        => left >= right ? left - right : right - left;

    private static string NormalizeMode(string? mode)
        => NeuronalMotorModes.TryNormalize(mode, out var normalized) ? normalized : string.Empty;

    private static string NormalizeSplit(string? split)
    {
        if (string.Equals(split, NeuronalMotorQualificationProtocol.TrainingSplit, StringComparison.OrdinalIgnoreCase))
        {
            return NeuronalMotorQualificationProtocol.TrainingSplit;
        }

        if (string.Equals(split, NeuronalMotorQualificationProtocol.HeldOutSplit, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(split, "heldout", StringComparison.OrdinalIgnoreCase))
        {
            return NeuronalMotorQualificationProtocol.HeldOutSplit;
        }

        return string.Empty;
    }

    private static bool IsVerifiedFingerprint(string? fingerprint)
        => !string.IsNullOrWhiteSpace(fingerprint) &&
           !fingerprint.StartsWith("unverified", StringComparison.OrdinalIgnoreCase);

    private static string ResolveSizingRecommendation(
        NeuronalMotorQualificationSample? last,
        double meanCoverage,
        NeuronalMotorControlSettings settings)
    {
        if (last is null)
        {
            return "No sizing decision: no live samples were captured.";
        }

        if (meanCoverage < settings.PromotionMinimumCoverage)
        {
            return "Repair missing population services, hemispheric coverage, or transport freshness before changing neuron counts.";
        }

        if (last.ConfidenceEma < settings.PromotionMinimumConfidence && meanCoverage >= 0.95)
        {
            return "Coverage is healthy but confidence is weak; inspect firing-rate separation, synaptic gain, saturation, and only then test targeted population resizing.";
        }

        if (last.ActionCircuitObserved && last.ActionCircuitCoverage < settings.PromotionMinimumCoverage)
        {
            return "Action lanes are under-covered; inspect lane allocation and collision telemetry before resizing the affected action-selection population.";
        }

        return "No population resize is justified by this capture.";
    }

    private static string Fingerprint<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

internal static class NeuronalMotorCausalPreflight
{
    public static NeuronalMotorCausalPreflightResult Run(NeuronalMotorControlSettings settings)
    {
        var deterministicSettings = NeuronalMotorControlSettings.Normalize(settings with
        {
            Mode = NeuronalMotorModes.Shadow,
            SmoothingAlpha = 1.0,
            MinimumCircuitCoverage = Math.Min(settings.MinimumCircuitCoverage, 0.45),
            MinimumOutputConfidence = Math.Min(settings.MinimumOutputConfidence, 0.35)
        });
        var control = new NeuronalMotorControlSnapshot(0, deterministicSettings);
        var balanced = CreateMotorCircuit(24.0f, 24.0f, 0.0f, 1.0f);
        var forwardReference = Reference("motor_forward");
        var leftReference = Reference("motor_turn_left");
        var baseline = Decode(balanced, forwardReference, false, control);
        var rightBiased = Decode(CreateMotorCircuit(5.0f, 24.0f, 0.0f, 1.0f), forwardReference, false, control);
        var inhibited = Decode(CreateMotorCircuit(24.0f, 24.0f, 1.0f, 0.0f), forwardReference, false, control);
        var sleeping = Decode(balanced, forwardReference, true, control);
        var alteredReference = Decode(balanced, leftReference, false, control);
        var ablated = Decode(
            balanced.Where(static snapshot => snapshot.Instance.HemisphereNormalized != "R").ToArray(),
            forwardReference,
            false,
            control);

        var checks = new[]
        {
            Check(
                "bilateral populations produce active forward drive",
                baseline.Active && baseline.ForwardDrive > 0.05 && Math.Abs(baseline.TurnDrive) < 0.01,
                $"active={baseline.Active}; forward={baseline.ForwardDrive:0.000}; turn={baseline.TurnDrive:0.000}"),
            Check(
                "lateralized firing produces differential steering",
                rightBiased.RightDrive > rightBiased.LeftDrive && rightBiased.TurnDrive > 0.05,
                $"left={rightBiased.LeftDrive:0.000}; right={rightBiased.RightDrive:0.000}; turn={rightBiased.TurnDrive:0.000}"),
            Check(
                "basal-ganglia output inhibition suppresses drive",
                inhibited.ForwardDrive < baseline.ForwardDrive * 0.60,
                $"baseline={baseline.ForwardDrive:0.000}; inhibited={inhibited.ForwardDrive:0.000}"),
            Check(
                "hemisphere ablation removes authority",
                !ablated.Active && ablated.MotorCircuitCoverage < baseline.MotorCircuitCoverage,
                $"active={ablated.Active}; coverage={ablated.MotorCircuitCoverage:0.000}"),
            Check(
                "sleep silences motor output",
                !sleeping.Active && Math.Abs(sleeping.LeftDrive) < 0.001 && Math.Abs(sleeping.RightDrive) < 0.001,
                $"active={sleeping.Active}; left={sleeping.LeftDrive:0.000}; right={sleeping.RightDrive:0.000}"),
            Check(
                "symbolic reference cannot change neuronal output",
                Math.Abs(baseline.LeftDrive - alteredReference.LeftDrive) < 0.000001 &&
                Math.Abs(baseline.RightDrive - alteredReference.RightDrive) < 0.000001,
                $"forward=({baseline.LeftDrive:0.000},{baseline.RightDrive:0.000}); altered=({alteredReference.LeftDrive:0.000},{alteredReference.RightDrive:0.000})")
        };
        return new NeuronalMotorCausalPreflightResult(
            NeuronalMotorQualificationProtocol.Version,
            NeuronalMotorQualificationProtocol.OfflineEvidenceSource,
            checks.All(static check => check.Passed),
            checks);
    }

    private static NeuronalMotorRuntime Decode(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        IntentionalActionLoopRuntime reference,
        bool sleeping,
        NeuronalMotorControlSnapshot control)
        => NeuronalMotorPopulationDecoder.Decode(
            1,
            snapshots,
            reference,
            sleeping,
            control,
            NeuronalMotorRuntime.Default);

    private static NeuronalMotorCausalCheck Check(string name, bool passed, string evidence)
        => new(name, passed, evidence);

    private static IntentionalActionLoopRuntime Reference(string directive)
        => IntentionalActionLoopRuntime.Default with
        {
            Active = true,
            MotorDirective = directive,
            Commitment = 1.0f,
            Readiness = 1.0f,
            Confidence = 1.0f,
            Inhibition = 0.0f
        };

    private static IReadOnlyList<InstanceStructureSnapshot> CreateMotorCircuit(
        float leftRate,
        float rightRate,
        float outputInhibition,
        float thalamicDisinhibition)
    {
        var snapshots = new List<InstanceStructureSnapshot>();
        StructureId[] structures =
        [
            StructureId.PremotorCortex,
            StructureId.Sma,
            StructureId.M1,
            StructureId.MotorThalamus,
            StructureId.ReticularFormation,
            StructureId.SpinalCordMotor
        ];
        foreach (var structure in structures)
        {
            snapshots.Add(Snapshot(structure, "L", leftRate));
            snapshots.Add(Snapshot(structure, "R", rightRate));
        }

        snapshots.Add(Snapshot(
            StructureId.GPi,
            "L",
            8.0f,
            basalGanglia: new BasalGangliaDiagnostics(
                "selection",
                thalamicDisinhibition,
                outputInhibition,
                outputInhibition,
                outputInhibition,
                thalamicDisinhibition,
                0.5f,
                thalamicDisinhibition)));
        snapshots.Add(Snapshot(
            StructureId.DeepCerebellarNuclei,
            "M",
            10.0f,
            cerebellar: new CerebellarDiagnostics(
                "stable",
                0.7f,
                0.1f,
                0.3f,
                0.8f,
                0.8f,
                0.8f,
                0.1f)));
        snapshots.Add(Snapshot(
            StructureId.VestibularNuclei,
            "M",
            10.0f,
            postural: new VestibuloReticularDiagnostics(
                "stable",
                0.7f,
                0.7f,
                0.8f,
                0.8f,
                0.9f,
                0.1f)));
        return snapshots;
    }

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        string hemisphere,
        float rate,
        BasalGangliaDiagnostics? basalGanglia = null,
        CerebellarDiagnostics? cerebellar = null,
        VestibuloReticularDiagnostics? postural = null)
    {
        var instance = new ServiceInstance(
            structure,
            $"{structure}-{hemisphere}",
            hemisphere,
            new Uri($"http://localhost:{5000 + (int)structure}"));
        return new InstanceStructureSnapshot(
            instance,
            structure,
            rate > 0.0f ? 8 : 0,
            rate,
            BrainRhythm.BETA,
            [],
            new NeuromodState(),
            0,
            0,
            0,
            BasalGangliaDiagnostics: basalGanglia,
            CerebellarDiagnostics: cerebellar,
            VestibuloReticularDiagnostics: postural);
    }
}
