using System.Text.Json;
using NeuralResonanceEngine.Protocol;

internal static class ContactNumericsStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static void Run(string inputDirectory, string outputDirectory)
    {
        if (!Path.IsPathFullyQualified(inputDirectory) || !Path.IsPathFullyQualified(outputDirectory) ||
            !Directory.Exists(inputDirectory) || Directory.Exists(outputDirectory)) throw new ArgumentException("Fresh absolute output required.");
        Directory.CreateDirectory(outputDirectory);
        void Save(string name, object value) => File.WriteAllText(Path.Combine(outputDirectory, name), JsonSerializer.Serialize(value, Json));
        var replays = new List<object>(); var cadence = new List<object>();
        // Declared before execution. These qualify this numerical fixture only.
        const double maximumError = 3e-8, finestError = 1e-9, refinementFloor = 1e-11;
        Save("protocol.json", new { version = CorticalContactNumerics.Version, maximumAbsoluteWeightError = maximumError,
            finestAbsoluteWeightError = finestError, refinementRatio = 1.1, refinementRoundoffFloor = refinementFloor,
            durationMs = 1000, stepsMs = new[] { 4d, 2, 1, .5, .25, .125, .0625, .03125, .015625 },
            parametersRetainLegacyFloatValues = true, liveBrainAccessed = false, physiologicalCalibrationComplete = false });
        try
        {
            foreach (string name in new[] { "continuous-colocated", "continuous-neighbor", "continuous-simultaneous", "continuous-unmodulated" })
            {
                var events = File.ReadLines(Path.Combine(inputDirectory, name + ".jsonl"))
                    .Select(line => JsonSerializer.Deserialize<MemoryLearningEvidence>(line, Json) ?? throw new InvalidOperationException("Missing event.")).ToArray();
                if (events.Length == 0) throw new InvalidOperationException("No reviewed events.");
                foreach (string mode in new[] { "RecordedSignals", "NoTagCapture", "NoDopamine", "NoAcetylcholine" })
                {
                    Console.WriteLine(name + " / double / " + mode);
                    var cell = new CorticalContactNumerics(CorticalContactNumerics.FromLegacy(events[0].Before), true);
                    var changes = new Dictionary<string, double> { ["pre"] = 0, ["post"] = 0, ["modulation"] = 0 };
                    int lostUpdates = 0; double lostCharge = 0;
                    using var output = new StreamWriter(Path.Combine(outputDirectory, name + "-" + mode + ".jsonl"));
                    var compact = new JsonSerializerOptions(Json) { WriteIndented = false };
                    for (int i = 0; i < events.Length; ++i)
                    {
                        var e = events[i]; var before = cell.State;
                        if (e.Before.ContactId != before.ContactId) throw new InvalidOperationException("Contact identity changed.");
                        if (e.Kind is "pre" or "post") cell.Spike(e.Kind == "pre", e.TimestampMs);
                        else if (e.Kind == "modulation") cell.Modulate(e.TimestampMs, e.ElapsedMs,
                            mode == "NoDopamine" ? 0 : e.Dopamine, mode == "NoAcetylcholine" ? 0 : e.Acetylcholine,
                            e.Activity, mode != "NoTagCapture");
                        else throw new InvalidOperationException("Unknown learning operation.");
                        var after = cell.State; double change = after.Weight - before.Weight;
                        changes[e.Kind] += change;
                        double charged = after.ChargedPlasticity - before.ChargedPlasticity;
                        if (charged > 0 && change == 0) { ++lostUpdates; lostCharge += charged; }
                        output.WriteLine(JsonSerializer.Serialize(new { index = i, e.Kind, e.MessageId, e.TimestampMs,
                            e.ElapsedMs, e.Activity, e.Teaching, recordedDopamine = e.Dopamine,
                            recordedAcetylcholine = e.Acetylcholine, intervention = mode, before, after }, compact));
                    }
                    replays.Add(new { key = name, intervention = mode, eventCount = events.Length,
                        initial = CorticalContactNumerics.FromLegacy(events[0].Before), final = cell.State,
                        weightChangeByKind = changes, lostUpdates, lostCharge, spikesAndActivityHeldFixed = true });
                }
            }
            var identity = StableNeuralIdentity.Create("cortical-double-numerical-fixture");
            CorticalContactState Initial() => new(identity, 0, 1, 1, 0, 0, 0, 0, .2f, 0,
                PlasticityRules.InitialPlasticityBudgetQuanta, -1, 0);
            var guard = new CorticalContactNumerics(Initial(), false);
            guard.Spike(true, 10); guard.Spike(false, 20); var accepted = guard.State;
            foreach (var invalid in new Action[] { () => guard.Spike(true, 19),
                () => guard.Modulate(21, -1, 1, 1, 0), () => guard.Modulate(21, 1, double.NaN, 1, 0) })
            {
                bool rejected = false;
                try { invalid(); } catch (ArgumentException) { rejected = true; }
                if (!rejected || guard.State != accepted) throw new InvalidOperationException("Invalid input changed accepted state.");
            }
            bool allPassed = true;
            foreach (string pairing in new[] { "pre-post", "post-pre", "unpaired" })
            {
                double previousError = double.PositiveInfinity;
                foreach (double step in new[] { 4d, 2, 1, .5, .25, .125, .0625, .03125, .015625 })
                {
                    var cell = new CorticalContactNumerics(pairing == "unpaired" ? Initial() with { TimeMs = 20 } : Initial(), false);
                    if (pairing != "unpaired") { cell.Spike(pairing == "pre-post", 10); cell.Spike(pairing != "pre-post", 20); }
                    var initial = cell.State;
                    int count = checked((int)(1000 / step));
                    double maximumChargedWithoutWeight = 0;
                    for (int i = 1; i <= count; ++i)
                    {
                        var before = cell.State;
                        cell.Modulate(20 + i * step, step, 1, 1, 0);
                        if (cell.State.Weight == before.Weight)
                            maximumChargedWithoutWeight = Math.Max(maximumChargedWithoutWeight, cell.State.ChargedPlasticity - before.ChargedPlasticity);
                    }
                    // Closed form of the unclipped continuous equations, with
                    // the exact declared float parameter VALUES promoted to double.
                    double capture = (double).35f + (double).65f;
                    double integral = (double).35f * capture * PlasticityRules.RestoredSpikeDensityLearningScale *
                        initial.Tag * 8 * (1 - Math.Exp(-1d / 8)) / 5;
                    double reference = initial.Tag >= 0 ? 5 - (5 - initial.Weight) * Math.Exp(-integral)
                        : initial.Weight * Math.Exp(integral);
                    double error = Math.Abs(cell.State.Weight - reference);
                    bool direction = pairing == "pre-post" ? cell.State.Weight > 1 : pairing == "post-pre" ? cell.State.Weight < 1 : cell.State.Weight == 1;
                    bool passed = error <= maximumError && (step != .015625 || error <= finestError) &&
                        error <= Math.Max(refinementFloor, 1.1 * previousError) && direction && maximumChargedWithoutWeight == 0 &&
                        cell.State.Budget > 0 && cell.State.Weight > .05 && cell.State.Weight < 5;
                    allPassed &= passed; previousError = error;
                    cadence.Add(new { pairing, stepMs = step, initial, final = cell.State, continuousLimit = reference,
                        absoluteError = error, maximumChargedWithoutWeight, directionPassed = direction, passed });
                }
            }
            Save("results.json", new { completed = true, version = CorticalContactNumerics.Version, replayCases = replays,
                cadenceCases = cadence, invalidInputAtomicityPassed = true, numericalCriteriaPassed = allPassed,
                biologicalConsolidationQualified = false, liveBrainAccessed = false, avatarRecoveryAssessed = false });
            if (!allPassed) throw new InvalidOperationException("Contact numerical criteria failed; preserve the original bounds and inspect evidence.");
            Console.WriteLine("Isolated contact numerical checks passed. Receptor biology, consolidation and live integration remain unqualified.");
        }
        catch (Exception error)
        {
            Save("FAILED.json", new { error = error.ToString(), replayCases = replays, cadenceCases = cadence });
            throw;
        }
    }
}
