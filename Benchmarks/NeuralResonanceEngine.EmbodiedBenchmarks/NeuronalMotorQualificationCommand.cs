using System.Net.Http.Json;
using System.Text.Json;

internal sealed record NeuronalMotorApiEnvelope(
    NeuronalMotorControlSnapshot Control,
    NeuronalMotorRuntime Runtime);

internal sealed record NeuronalMotorQualificationBodyState(
    double ForwardVelocity,
    double TurnRateDeg,
    long LastInputTick);

internal sealed record NeuronalMotorQualificationOutcomeState(
    double Progress,
    double DamageLevel,
    long LastInputTick);

internal sealed record NeuronalMotorQualificationAuthorityState(
    bool SymbolicScaffoldCanAuthorize,
    bool SemanticMotorInjectionAllowed,
    bool WorldGoalSteeringAllowed);

internal sealed record NeuronalMotorQualificationStateEnvelope(
    long Tick,
    NeuronalMotorQualificationBodyState BodyState,
    NeuronalMotorQualificationOutcomeState OutcomeState,
    NeuronalMotorQualificationAuthorityState CognitionAuthority);

internal sealed record NeuronalMotorLiveCaptureOptions(
    Uri ApiBaseUrl,
    string ScenarioId,
    string Split,
    int Seed,
    string ExpectedMode,
    string LayoutFingerprint,
    TimeSpan MaximumDuration,
    TimeSpan PollInterval);

internal static class NeuronalMotorQualificationCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string mode, string[] args, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        return mode.ToLowerInvariant() switch
        {
            "motor-preflight" => await RunPreflightAsync(outputDirectory),
            "motor-capture" => await RunCaptureAsync(args, outputDirectory),
            "motor-campaign" => await RunCampaignAsync(args, outputDirectory),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown neuronal motor qualification mode.")
        };
    }

    private static async Task<int> RunPreflightAsync(string outputDirectory)
    {
        var settings = DefaultSettings();
        var preflight = NeuronalMotorCausalPreflight.Run(settings);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var jsonPath = Path.Combine(outputDirectory, $"neuronal-motor-preflight-{stamp}.json");
        var markdownPath = Path.Combine(outputDirectory, $"neuronal-motor-preflight-{stamp}.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(preflight, JsonOptions));
        await File.WriteAllTextAsync(markdownPath, RenderPreflight(preflight));

        Console.WriteLine("DNNE neuronal motor causal preflight complete.");
        Console.WriteLine($"Status: {(preflight.Passed ? "PASS" : "FAIL")}");
        foreach (var check in preflight.Checks)
        {
            Console.WriteLine($"{(check.Passed ? "PASS" : "FAIL")}: {check.Name} ({check.Evidence})");
        }

        Console.WriteLine("Authority: offline preflight cannot unlock Assist or Primary.");
        Console.WriteLine($"JSON: {jsonPath}");
        Console.WriteLine($"Report: {markdownPath}");
        return preflight.Passed ? 0 : 1;
    }

    private static async Task<int> RunCaptureAsync(string[] args, string outputDirectory)
    {
        var apiValue = ReadOption(args, "--api") ?? "http://localhost:5080";
        if (!Uri.TryCreate(apiValue, UriKind.Absolute, out var apiBaseUrl))
        {
            Console.Error.WriteLine($"Invalid --api URL '{apiValue}'.");
            return 2;
        }

        var scenarioId = ReadOption(args, "--scenario") ?? string.Empty;
        var split = ReadOption(args, "--split") ?? string.Empty;
        var expectedMode = ReadOption(args, "--expected-mode") ?? NeuronalMotorModes.Shadow;
        var layoutFingerprint = ReadOption(args, "--layout-fingerprint") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(scenarioId) ||
            string.IsNullOrWhiteSpace(split) ||
            string.IsNullOrWhiteSpace(layoutFingerprint))
        {
            Console.Error.WriteLine("motor-capture requires --scenario, --split, and --layout-fingerprint.");
            return 2;
        }

        if (!NeuronalMotorModes.TryNormalize(expectedMode, out expectedMode) ||
            expectedMode is not (NeuronalMotorModes.Shadow or NeuronalMotorModes.Assist))
        {
            Console.Error.WriteLine("--expected-mode must be Shadow or Assist. Primary is never entered by the collector.");
            return 2;
        }

        var seed = ReadIntOption(args, "--seed", 317, int.MinValue, int.MaxValue);
        var maxSeconds = ReadIntOption(args, "--max-seconds", 900, 5, 86_400);
        var pollMs = ReadIntOption(args, "--poll-ms", 100, 25, 10_000);
        var options = new NeuronalMotorLiveCaptureOptions(
            apiBaseUrl,
            scenarioId,
            split,
            seed,
            expectedMode,
            layoutFingerprint,
            TimeSpan.FromSeconds(maxSeconds),
            TimeSpan.FromMilliseconds(pollMs));

        NeuronalMotorQualificationCapture capture;
        try
        {
            capture = await CollectLiveAsync(options, CancellationToken.None);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            Console.Error.WriteLine($"Live neuronal motor capture failed: {exception.Message}");
            return 2;
        }

        var report = NeuronalMotorQualificationEvaluator.Evaluate(capture);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var safeScenario = SanitizeFileName(scenarioId);
        var capturePath = Path.Combine(outputDirectory, $"neuronal-motor-capture-{safeScenario}-{stamp}.json");
        var reportPath = Path.Combine(outputDirectory, $"neuronal-motor-scenario-{safeScenario}-{stamp}.json");
        var markdownPath = Path.Combine(outputDirectory, $"neuronal-motor-scenario-{safeScenario}-{stamp}.md");
        await File.WriteAllTextAsync(capturePath, JsonSerializer.Serialize(capture, JsonOptions));
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        await File.WriteAllTextAsync(markdownPath, NeuronalMotorQualificationEvaluator.RenderMarkdown(report));

        Console.WriteLine("DNNE live neuronal motor scenario capture complete.");
        Console.WriteLine($"Status: {(report.Passed ? "PASS" : "FAIL")}");
        Console.WriteLine($"Decision: {report.Decision}");
        Console.WriteLine($"Samples: {report.Metrics.DistinctRuntimeSamples}; active evaluation delta: {report.Metrics.ActiveEvaluationSampleDelta}");
        Console.WriteLine($"Coverage: {report.Metrics.MeanMotorCoverage:0.000}; confidence: {report.Metrics.FinalConfidenceEma:0.000}; agreement: {report.Metrics.FinalAgreementEma:0.000}");
        Console.WriteLine($"Capture: {capturePath}");
        Console.WriteLine($"JSON: {reportPath}");
        Console.WriteLine($"Report: {markdownPath}");
        return report.Passed ? 0 : 1;
    }

    private static async Task<int> RunCampaignAsync(string[] args, string outputDirectory)
    {
        var inputDirectory = ReadOption(args, "--input") ?? outputDirectory;
        var phase = ReadOption(args, "--phase") ?? NeuronalMotorModes.Shadow;
        var minimumTraining = ReadIntOption(args, "--minimum-training", 3, 1, 100);
        var minimumHeldOut = ReadIntOption(args, "--minimum-held-out", 3, 1, 100);
        if (!Directory.Exists(inputDirectory))
        {
            Console.Error.WriteLine($"Campaign input directory does not exist: {inputDirectory}");
            return 2;
        }

        var reports = new List<NeuronalMotorQualificationReport>();
        foreach (var path in Directory.EnumerateFiles(inputDirectory, "neuronal-motor-scenario-*.json", SearchOption.AllDirectories))
        {
            var json = await File.ReadAllTextAsync(path);
            var report = JsonSerializer.Deserialize<NeuronalMotorQualificationReport>(json, JsonOptions);
            if (report is not null && string.Equals(report.ProtocolVersion, NeuronalMotorQualificationProtocol.Version, StringComparison.Ordinal))
            {
                reports.Add(report);
            }
        }

        if (reports.Count == 0)
        {
            Console.Error.WriteLine($"No neuronal motor scenario reports were found under {inputDirectory}.");
            return 2;
        }

        var campaign = NeuronalMotorQualificationEvaluator.EvaluateCampaign(
            phase,
            reports,
            minimumTraining,
            minimumHeldOut);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var normalizedPhase = SanitizeFileName(campaign.Phase.Length == 0 ? phase : campaign.Phase);
        var jsonPath = Path.Combine(outputDirectory, $"neuronal-motor-campaign-{normalizedPhase}-{stamp}.json");
        var markdownPath = Path.Combine(outputDirectory, $"neuronal-motor-campaign-{normalizedPhase}-{stamp}.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(campaign, JsonOptions));
        await File.WriteAllTextAsync(markdownPath, NeuronalMotorQualificationEvaluator.RenderMarkdown(campaign));

        Console.WriteLine("DNNE neuronal motor qualification campaign complete.");
        Console.WriteLine($"Status: {(campaign.Passed ? "PASS" : "FAIL")}");
        Console.WriteLine($"Decision: {campaign.Decision}");
        Console.WriteLine($"Training: {campaign.TrainingScenarios}; held-out: {campaign.HeldOutScenarios}; distinct seeds: {campaign.DistinctSeeds}");
        Console.WriteLine($"JSON: {jsonPath}");
        Console.WriteLine($"Report: {markdownPath}");
        return campaign.Passed ? 0 : 1;
    }

    internal static async Task<NeuronalMotorQualificationCapture> CollectLiveAsync(
        NeuronalMotorLiveCaptureOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var client = new HttpClient
        {
            BaseAddress = EnsureTrailingSlash(options.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        var startedAt = DateTimeOffset.UtcNow;
        var deadline = startedAt + options.MaximumDuration;
        var samples = new List<NeuronalMotorQualificationSample>();
        var errors = new List<string>();
        var settings = DefaultSettings();
        long lastSequence = long.MinValue;
        var consecutiveErrors = 0;

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var motorTask = client.GetFromJsonAsync<NeuronalMotorApiEnvelope>(
                    "api/v1/neuronal-motor",
                    JsonOptions,
                    cancellationToken);
                var stateTask = client.GetFromJsonAsync<NeuronalMotorQualificationStateEnvelope>(
                    "api/v1/state",
                    JsonOptions,
                    cancellationToken);
                await Task.WhenAll(motorTask, stateTask);
                var motor = await motorTask ?? throw new JsonException("The neuronal motor endpoint returned no body.");
                var state = await stateTask ?? throw new JsonException("The state endpoint returned no body.");
                settings = motor.Control.Settings;
                consecutiveErrors = 0;

                if (motor.Runtime.Sequence != lastSequence)
                {
                    samples.Add(ToSample(motor.Runtime, state));
                    lastSequence = motor.Runtime.Sequence;
                }

                if (HasEnoughLiveEvidence(samples, settings))
                {
                    break;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                consecutiveErrors++;
                var message = $"{DateTimeOffset.UtcNow:O} {exception.GetType().Name}: {exception.Message}";
                if (errors.Count == 0 || !string.Equals(errors[^1], message, StringComparison.Ordinal))
                {
                    errors.Add(message);
                }

                if (consecutiveErrors >= 10)
                {
                    break;
                }
            }

            await Task.Delay(options.PollInterval, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var scenario = new NeuronalMotorQualificationScenario(
            options.ScenarioId,
            options.Split,
            options.Seed,
            options.ExpectedMode,
            NeuronalMotorQualificationProtocol.LiveEvidenceSource,
            options.LayoutFingerprint);
        return new NeuronalMotorQualificationCapture(
            NeuronalMotorQualificationProtocol.Version,
            startedAt,
            DateTimeOffset.UtcNow,
            scenario,
            settings,
            samples,
            NeuronalMotorCausalPreflight.Run(settings),
            errors);
    }

    private static bool HasEnoughLiveEvidence(
        IReadOnlyList<NeuronalMotorQualificationSample> samples,
        NeuronalMotorControlSettings settings)
    {
        if (samples.Count < Math.Clamp(settings.PromotionMinimumSamples / 10, 25, 100))
        {
            return false;
        }

        var first = samples[0];
        var last = samples[^1];
        return last.PromotionReady &&
               last.ActiveEvaluationSamples - first.ActiveEvaluationSamples >= settings.PromotionMinimumSamples &&
               samples.Select(static sample => sample.BodyInputTick).Distinct().Count() >= 2 &&
               samples.Select(static sample => sample.OutcomeInputTick).Distinct().Count() >= 2;
    }

    private static NeuronalMotorQualificationSample ToSample(
        NeuronalMotorRuntime runtime,
        NeuronalMotorQualificationStateEnvelope state)
        => new(
            DateTimeOffset.UtcNow,
            runtime.Tick,
            state.Tick,
            runtime.Sequence,
            runtime.Mode,
            runtime.Active,
            runtime.Sleeping,
            runtime.PromotionReady,
            runtime.LeftDrive,
            runtime.RightDrive,
            runtime.MotorCircuitCoverage,
            runtime.Confidence,
            runtime.ConfidenceEma,
            runtime.Agreement,
            runtime.AgreementEma,
            runtime.EvaluationSamples,
            runtime.ActiveEvaluationSamples,
            runtime.QualifiedConsecutiveTicks,
            runtime.ActionCircuitObserved,
            runtime.ActionSelectionConfidence,
            runtime.ActionCircuitCoverage,
            runtime.ActionSelectionMargin,
            state.BodyState.LastInputTick,
            state.BodyState.ForwardVelocity,
            state.BodyState.TurnRateDeg,
            state.OutcomeState.LastInputTick,
            state.OutcomeState.Progress,
            state.OutcomeState.DamageLevel,
            state.CognitionAuthority.SymbolicScaffoldCanAuthorize,
            state.CognitionAuthority.SemanticMotorInjectionAllowed,
            state.CognitionAuthority.WorldGoalSteeringAllowed);

    private static NeuronalMotorControlSettings DefaultSettings()
        => NeuronalMotorControlSettings.Normalize(new NeuronalMotorControlSettings(
            NeuronalMotorModes.Shadow,
            1.5,
            25.0,
            0.2,
            96,
            0.45,
            0.45,
            12,
            1200,
            0.72,
            0.62,
            0.80,
            600));

    private static Uri EnsureTrailingSlash(Uri value)
    {
        var text = value.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? value.AbsoluteUri
            : value.AbsoluteUri + "/";
        return new Uri(text, UriKind.Absolute);
    }

    private static string? ReadOption(string[] arguments, string name)
    {
        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    private static int ReadIntOption(string[] arguments, string name, int fallback, int minimum, int maximum)
    {
        var value = ReadOption(arguments, name);
        return int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : character)
            .ToArray());
    }

    private static string RenderPreflight(NeuronalMotorCausalPreflightResult preflight)
    {
        var lines = new List<string>
        {
            "# DNNE Neuronal Motor Causal Preflight",
            string.Empty,
            $"- Protocol: `{preflight.ProtocolVersion}`",
            $"- Evidence: `{preflight.EvidenceSource}`",
            $"- Status: **{(preflight.Passed ? "PASS" : "FAIL")}**",
            string.Empty,
            "## Checks",
            string.Empty
        };
        lines.AddRange(preflight.Checks.Select(static check =>
            $"- {(check.Passed ? "PASS" : "FAIL")}: {check.Name} ({check.Evidence})"));
        lines.AddRange(
        [
            string.Empty,
            "This is an offline causal preflight. It cannot qualify DNNE for Assist or Primary authority.",
            string.Empty
        ]);
        return string.Join(Environment.NewLine, lines);
    }
}
