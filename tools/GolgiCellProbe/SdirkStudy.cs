using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

internal static class SdirkStudy
{
    internal sealed record Condition(string Key, string Protocol, string Block, double CurrentPa, double StepMs,
        double NonlinearToleranceScale, double DurationMs, double InjectionStartMs, double InjectionEndMs, bool Electrode);
    internal sealed record Input(string Version, string CandidateVersion, double TemperatureCelsius, double InitialVoltageMv,
        double SampleMs, double ThresholdMv, Condition[] Cases);

    public static void Run(string input, string output, bool thirdOrder = false, bool finestExtension = false)
    {
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output))
            throw new ArgumentException("Require absolute input and a new output directory.");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        byte[] raw = File.ReadAllBytes(input);
        var request = JsonSerializer.Deserialize<Input>(raw, json) ?? throw new ArgumentException("Missing protocol.");
        if (finestExtension && !thirdOrder) throw new ArgumentException("The finer extension requires third order.");
        int caseCount = finestExtension ? 14 : thirdOrder ? 38 : 30;
        double[] steps = finestExtension ? [.0009765625] : thirdOrder ? [.015625, .0078125, .00390625, .001953125] : [.015625, .0078125, .00390625];
        string version = finestExtension ? "golgi-sdirk3-finer-comparison-v1" : thirdOrder ? "golgi-sdirk3-reference-comparison-v1" : "golgi-sdirk-reference-comparison-v1";
        if (request.Version != version ||
            request.CandidateVersion != (thirdOrder ? GolgiSdirkCell.ThirdOrderVersion : GolgiSdirkCell.Version) ||
            request.TemperatureCelsius != 23 || request.InitialVoltageMv != -60 || request.SampleMs != .25 ||
            request.ThresholdMv != GolgiReferenceCell.SpikeThresholdMv || request.Cases is null || request.Cases.Length != caseCount ||
            request.Cases.Select(c => c.Key).Distinct().Count() != caseCount)
            throw new ArgumentException("Unexpected coupled-cell protocol.");
        foreach (var c in request.Cases)
        {
            bool passive = c.Block == nameof(GolgiDiagnosticBlock.Passive);
            if (!System.Text.RegularExpressions.Regex.IsMatch(c.Key, "^[a-z0-9.-]+$") ||
                !Enum.TryParse<GolgiDiagnosticBlock>(c.Block, out var block) || !Enum.IsDefined(block) ||
                !steps.Contains(c.StepMs) || c.NonlinearToleranceScale is not (1 or .125) ||
                c.NonlinearToleranceScale == .125 && (passive || c.StepMs != steps[^1]) ||
                c.DurationMs != (passive ? 100 : 6000) || c.InjectionStartMs != (passive ? 20 : 2000) ||
                c.InjectionEndMs != (passive ? 60 : 3000) || !double.IsFinite(c.CurrentPa) || (!passive && c.Electrode))
                throw new ArgumentException("Invalid fixed condition.");
        }
        if (finestExtension)
        {
            var expected = new List<Condition>();
            (string Name, string Block, double Current)[] protocols =
            [ ("quiet", "None", 0), ("depolarize", "None", 50), ("hyperpolarize", "None", -50),
              ("no-nap", "WithoutPersistentSodium", 0), ("no-hcn", "WithoutHcn", 0), ("no-sk", "WithoutSk", 0),
              ("passive-soma", "Passive", 25), ("passive-electrode", "Passive", 25) ];
            foreach (var p in protocols)
            {
                bool passive = p.Block == "Passive";
                foreach (double scale in passive ? new double[] { 1 } : new double[] { 1, .125 })
                    expected.Add(new(p.Name + "-h0.0009765625-tol" + scale.ToString("G", CultureInfo.InvariantCulture),
                        p.Name, p.Block, p.Current, .0009765625, scale, passive ? 100 : 6000,
                        passive ? 20 : 2000, passive ? 60 : 3000, p.Name == "passive-electrode"));
            }
            if (!request.Cases.SequenceEqual(expected)) throw new ArgumentException("Finer extension must retain every fixed protocol in order.");
        }
        Directory.CreateDirectory(output);
        void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json));
        Save("protocol.json", new { inputSha256 = Convert.ToHexString(SHA256.HashData(raw)), specification = request,
            gamma = thirdOrder ? GolgiSdirkCell.ThirdGamma : GolgiSdirkCell.Gamma,
            cable = new GolgiCable().CopyNodes(), gateOrder = Enum.GetNames<GolgiGate>(),
            rowDoubles = 162, state = "timeMs; shifted V[135]; gates[18]; SK[6]; high/low Ca micromolar", binary = "little-endian float64",
            exactCheckpoint = false, liveBrainAccessed = false, originalControlReused = true });
        var outcomes = new List<object>(); int successful = 0;
        foreach (var condition in request.Cases)
        {
            Console.WriteLine($"CASE {outcomes.Count + 1}/{caseCount}: {condition.Key}");
            var cell = new GolgiSdirkCell(request.TemperatureCelsius, request.InitialVoltageMv,
                Enum.Parse<GolgiDiagnosticBlock>(condition.Block), condition.NonlinearToleranceScale,
                thirdOrder ? GolgiSdirkOrder.Third : GolgiSdirkOrder.Second);
            int samples = 0; string? failure = null;
            try
            {
                using var binary = new BinaryWriter(new FileStream(Path.Combine(output, condition.Key + ".bin"), FileMode.CreateNew));
                using var accounting = new StreamWriter(new FileStream(Path.Combine(output, condition.Key + "-accounting.csv"), FileMode.CreateNew));
                accounting.WriteLine("timeMs,chargeFc,highEnteredMm,highRemovedMm,lowEnteredMm,lowRemovedMm,maxChargeErrorFc,maxCalciumErrorMm,maxKirchhoffPa,maxGateStagePerMs,maxCalciumStageMmPerMs,maxSkMassError,maxScaledNonlinearResidual,newtonEvaluations,maxNewtonIterations");
                void Sample()
                {
                    Span<double> state = stackalloc double[161]; cell.CopyPhysicalTrace(state);
                    binary.Write(cell.TimeMs); foreach (double value in state) binary.Write(value);
                    double[] row = [cell.TimeMs, cell.NetMembraneChargeFc, cell.HighCalciumEnteredMm, cell.HighCalciumRemovedMm,
                        cell.LowCalciumEnteredMm, cell.LowCalciumRemovedMm, cell.MaximumChargeBalanceErrorFc, cell.MaximumCalciumBalanceErrorMm,
                        cell.MaximumKirchhoffResidualPa, cell.MaximumGateStageResidualPerMs, cell.MaximumCalciumStageResidualMmPerMs,
                        cell.MaximumSkMassError, cell.MaximumScaledNonlinearResidual, cell.NonlinearEvaluations, cell.MaximumNewtonIterations];
                    accounting.WriteLine(string.Join(',', row.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                    ++samples;
                }
                Sample(); int sampleEvery = (int)(request.SampleMs / condition.StepMs);
                for (int tick = 0; tick < (int)(condition.DurationMs / condition.StepMs); ++tick)
                {
                    double time = tick * condition.StepMs;
                    double drive = time >= condition.InjectionStartMs && time < condition.InjectionEndMs ? condition.CurrentPa : 0;
                    cell.Advance(condition.StepMs, condition.Electrode ? 0 : drive, condition.Electrode ? drive : 0);
                    if ((tick + 1) % sampleEvery == 0) Sample();
                }
                if (cell.TimeMs != condition.DurationMs || samples != condition.DurationMs / request.SampleMs + 1)
                    throw new InvalidOperationException("Incomplete physical time or sample grid.");
                ++successful;
            }
            catch (Exception error)
            {
                failure = error.ToString(); Console.WriteLine("CONDITION FAILED: " + error.Message);
            }
            outcomes.Add(new { condition.Key, completed = failure is null, condition, samplesSaved = samples,
                cell.TimeMs, cell.Faulted, error = failure, crossingsMs = cell.CopySomaticCrossings(),
                cell.MaximumChargeBalanceErrorFc, cell.MaximumCalciumBalanceErrorMm, cell.MaximumKirchhoffResidualPa,
                cell.MaximumGateStageResidualPerMs, cell.MaximumCalciumStageResidualMmPerMs, cell.MaximumSkMassError,
                cell.MaximumScaledNonlinearResidual, cell.NonlinearEvaluations, cell.MaximumNewtonIterations });
            Save("results.json", new { completed = false, successfulCases = successful, cases = outcomes });
        }
        Save("results.json", new { completed = true, successfulCases = successful, allConditionsCompleted = successful == caseCount,
            cases = outcomes, numericalCriteriaAssessed = false, physiologyQualified = false, avatarRecoveryAssessed = false });
        Console.WriteLine("All conditions attempted; failed traces are retained. Numerical analysis follows.");
    }
}
