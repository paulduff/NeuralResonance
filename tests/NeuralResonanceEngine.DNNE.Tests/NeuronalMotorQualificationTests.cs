namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalMotorQualificationTests
{
    [Fact]
    public void CausalPreflightExercisesMotorLesionsAndAuthorityIsolation()
    {
        var result = NeuronalMotorCausalPreflight.Run(CreateSettings());

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Checks.Select(static check => check.Evidence)));
        Assert.Equal(NeuronalMotorQualificationProtocol.OfflineEvidenceSource, result.EvidenceSource);
        Assert.Contains(result.Checks, static check => check.Name.Contains("ablation", StringComparison.OrdinalIgnoreCase) && check.Passed);
        Assert.Contains(result.Checks, static check => check.Name.Contains("symbolic reference", StringComparison.OrdinalIgnoreCase) && check.Passed);
    }

    [Fact]
    public void LiveShadowScenarioPassesWithoutBecomingPrimaryEligible()
    {
        var report = NeuronalMotorQualificationEvaluator.Evaluate(CreateCapture(317, "training", "Shadow"));

        Assert.True(report.Passed, FailedCriteria(report));
        Assert.False(report.EligibleForPrimaryAuthority);
        Assert.Equal("ScenarioQualifiedForAssistCampaign", report.Decision);
        Assert.Equal("No population resize is justified by this capture.", report.CircuitSizingRecommendation);
    }

    [Fact]
    public void OfflineEvidenceCanNeverQualifyAuthority()
    {
        var capture = CreateCapture(317, "training", "Shadow") with
        {
            Scenario = CreateCapture(317, "training", "Shadow").Scenario with
            {
                EvidenceSource = NeuronalMotorQualificationProtocol.OfflineEvidenceSource
            }
        };

        var report = NeuronalMotorQualificationEvaluator.Evaluate(capture);

        Assert.False(report.Passed);
        Assert.False(report.EligibleForPrimaryAuthority);
        Assert.Contains(report.Criteria, static criterion =>
            criterion.Name == "evidence comes from live structure services" && !criterion.Passed);
    }

    [Fact]
    public void MissingWorldFeedbackRejectsOtherwiseQualifiedScenario()
    {
        var capture = CreateCapture(317, "training", "Shadow");
        var samples = capture.Samples
            .Select(static sample => sample with
            {
                BodyInputTick = 10,
                OutcomeInputTick = 10,
                ForwardVelocity = 0.0,
                TurnRateDeg = 0.0
            })
            .ToArray();

        var report = NeuronalMotorQualificationEvaluator.Evaluate(capture with { Samples = samples });

        Assert.False(report.Passed);
        Assert.Contains(report.Criteria, static criterion => criterion.Name == "body feedback advances" && !criterion.Passed);
        Assert.Contains(report.Criteria, static criterion => criterion.Name == "world outcome feedback advances" && !criterion.Passed);
        Assert.Contains(report.Criteria, static criterion => criterion.Name == "embodied movement is observed" && !criterion.Passed);
    }

    [Fact]
    public void SixSeedShadowCampaignAdvancesOnlyToAssist()
    {
        var reports = CreateCampaignReports("Shadow");

        var campaign = NeuronalMotorQualificationEvaluator.EvaluateCampaign("Shadow", reports);

        Assert.True(campaign.Passed, FailedCriteria(campaign));
        Assert.False(campaign.EligibleForPrimaryAuthority);
        Assert.Equal("ReadyForAssist", campaign.Decision);
        Assert.Equal(3, campaign.TrainingScenarios);
        Assert.Equal(3, campaign.HeldOutScenarios);
    }

    [Fact]
    public void SixSeedAssistCampaignCanBecomePrimaryEligible()
    {
        var reports = CreateCampaignReports("Assist");

        Assert.All(reports, static report => Assert.False(report.EligibleForPrimaryAuthority));

        var campaign = NeuronalMotorQualificationEvaluator.EvaluateCampaign("Assist", reports);

        Assert.True(campaign.Passed, FailedCriteria(campaign));
        Assert.True(campaign.EligibleForPrimaryAuthority);
        Assert.Equal("ReadyForGuardedPrimary", campaign.Decision);
    }

    [Fact]
    public void ReusingASeedRejectsCampaignGeneralizationClaim()
    {
        var reports = CreateCampaignReports("Shadow").ToArray();
        reports[^1] = reports[^1] with
        {
            Scenario = reports[^1].Scenario with { Seed = reports[0].Scenario.Seed }
        };

        var campaign = NeuronalMotorQualificationEvaluator.EvaluateCampaign("Shadow", reports);

        Assert.False(campaign.Passed);
        Assert.Contains(campaign.Criteria, static criterion =>
            criterion.Name == "seeds are distinct across the campaign" && !criterion.Passed);
    }

    private static IReadOnlyList<NeuronalMotorQualificationReport> CreateCampaignReports(string mode)
    {
        var reports = new List<NeuronalMotorQualificationReport>();
        foreach (var seed in new[] { 317, 911, 2027 })
        {
            reports.Add(NeuronalMotorQualificationEvaluator.Evaluate(CreateCapture(seed, "training", mode)));
        }

        foreach (var seed in new[] { 4049, 5051, 6067 })
        {
            reports.Add(NeuronalMotorQualificationEvaluator.Evaluate(CreateCapture(seed, "held-out", mode)));
        }

        return reports;
    }

    private static NeuronalMotorQualificationCapture CreateCapture(int seed, string split, string mode)
    {
        var settings = CreateSettings() with { Mode = mode };
        var samples = Enumerable.Range(0, 80)
            .Select(index => new NeuronalMotorQualificationSample(
                DateTimeOffset.UnixEpoch.AddMilliseconds(index * 100),
                Tick: index + 1,
                StateTick: index + 1,
                Sequence: index + 1,
                Mode: mode,
                Active: true,
                Sleeping: false,
                PromotionReady: index >= 50,
                LeftDrive: 0.72,
                RightDrive: 0.72,
                MotorCircuitCoverage: 1.0,
                Confidence: 0.82,
                ConfidenceEma: 0.80,
                Agreement: 0.84,
                AgreementEma: 0.82,
                EvaluationSamples: index + 1,
                ActiveEvaluationSamples: index + 1,
                QualifiedConsecutiveTicks: index + 1,
                ActionCircuitObserved: true,
                ActionSelectionConfidence: 0.82,
                ActionCircuitCoverage: 1.0,
                ActionSelectionMargin: 0.34,
                BodyInputTick: index,
                ForwardVelocity: 0.62,
                TurnRateDeg: 0.0,
                OutcomeInputTick: index / 8,
                OutcomeProgress: 0.24,
                OutcomeDamage: 0.0,
                SymbolicScaffoldCanAuthorize: false,
                SemanticMotorInjectionAllowed: false,
                WorldGoalSteeringAllowed: false))
            .ToArray();
        var scenario = new NeuronalMotorQualificationScenario(
            $"maze-{split}-{seed}",
            split,
            seed,
            mode,
            NeuronalMotorQualificationProtocol.LiveEvidenceSource,
            $"sha256:layout-{seed}");
        return new NeuronalMotorQualificationCapture(
            NeuronalMotorQualificationProtocol.Version,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(8),
            scenario,
            settings,
            samples,
            NeuronalMotorCausalPreflight.Run(settings),
            []);
    }

    private static NeuronalMotorControlSettings CreateSettings()
        => NeuronalMotorControlSettings.Normalize(new NeuronalMotorControlSettings(
            Mode: "Shadow",
            BaselineRateHz: 1.5,
            SaturationRateHz: 25.0,
            SmoothingAlpha: 1.0,
            PopulationSnapshotMaxAgeTicks: 96,
            MinimumCircuitCoverage: 0.45,
            MinimumOutputConfidence: 0.35,
            MaxPopulationEventsPerSide: 12,
            PromotionMinimumSamples: 50,
            PromotionMinimumAgreement: 0.70,
            PromotionMinimumConfidence: 0.55,
            PromotionMinimumCoverage: 0.95,
            PromotionConsecutiveTicks: 10));

    private static string FailedCriteria(NeuronalMotorQualificationReport report)
        => string.Join(Environment.NewLine, report.Criteria.Where(static criterion => !criterion.Passed).Select(static criterion => criterion.Evidence));

    private static string FailedCriteria(NeuronalMotorQualificationCampaignReport report)
        => string.Join(Environment.NewLine, report.Criteria.Where(static criterion => !criterion.Passed).Select(static criterion => criterion.Evidence));
}
