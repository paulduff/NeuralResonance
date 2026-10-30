using System.Text.Json;

// Short characterization of the reference membrane alone. No energy regulation,
// cell wrappers, learned weights, brain services or physiological acceptance.
internal static class ReferenceRecoveryProbe
{
    public static int Run(string[] args)
    {
        if (args.Length != 2 || args[1] != "--reference-recovery")
            throw new ArgumentException("Arguments: new-output-directory --reference-recovery");
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new InvalidOperationException("Use a new output directory.");
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json));
        try
        {
            Save("protocol.json", new
            {
                Study = "reference-hh-recovery", DurationMilliseconds = 4000, IntegrationStepsMilliseconds = new[] { .25, .125 },
                BiasCurrentMicroampPerSquareCentimeter = new[] { 8, 10, 12 },
                Inhibition = "No input, abrupt release or slow release: GABA-A conductance 0.8 mS/cm^2 at 500-700 ms, reversal -80 mV. Slow release decays exponentially with tau=120 ms after 700 ms; not a fitted receptor waveform.",
                ProbePulse = "Both arms receive an additional 20 microamp/cm^2 at 3000-3002 ms; diagnostic perturbation only.",
                Scope = "Bare classical HH reference at 6.3 C; no ModelNeuron support/compartment wrapper. Not a Purkinje or olive fit.",
                LiveBrainAccessed = false, PhysicalRecoveryAssessed = false, BiologicalEquivalenceQualified = false
            });
            var results = new List<object>();
            foreach (double current in new[] { 8d, 10d, 12d })
            foreach (string inhibition in new[] { "none", "abrupt", "slow" })
            foreach (double dt in new[] { .25, .125 })
            {
                var membrane = new HodgkinHuxleyMembrane();
                string[] phases = ["initial", "inhibition-window", "recovery", "probe-pulse", "after-pulse"];
                var spikes = phases.ToDictionary(p => p, _ => new List<double>());
                var trace = new List<object>();
                for (int step = 0; step < 4000 / dt; step++)
                {
                    double start = step * dt, end = start + dt;
                    int phase = start < 500 ? 0 : start < 700 ? 1 : start < 3000 ? 2 : start < 3002 ? 3 : 4;
                    double conductance = inhibition == "none" || start < 500 ? 0 : start < 700 ? .8 :
                        inhibition == "slow" ? .8 * Math.Exp(-(start - 700) / 120) : 0;
                    if (membrane.Advance(dt, current + (phase == 3 ? 20 : 0), gabaAConductance: conductance))
                        spikes[phases[phase]].Add(end);
                    if (!double.IsFinite(membrane.Voltage) || new[] { membrane.M, membrane.H, membrane.N }.Any(g => !double.IsFinite(g) || g < 0 || g > 1))
                        throw new InvalidOperationException("Invalid reference membrane state");
                    if (end % 1 == 0) trace.Add(new { TimeMilliseconds = end, membrane.Voltage, membrane.M, membrane.H, membrane.N });
                }
                var name = $"current-{current}-{inhibition}-{(dt == .25 ? "025" : "0125")}ms";
                Save(name + ".json", new { Current = current, Inhibition = inhibition, StepMilliseconds = dt, Spikes = spikes, Trace = trace });
                results.Add(new { Case = name, Current = current, Inhibition = inhibition, StepMilliseconds = dt,
                    Counts = spikes.ToDictionary(p => p.Key, p => p.Value.Count) });
            }
            Save("results.json", new { Completed = true, CaseCount = results.Count, Results = results,
                BiologicalEquivalenceQualified = false, PhysicalRecoveryAssessed = false });
            Console.WriteLine("18 short reference-membrane cases complete. This characterizes the reference, not biological suitability.");
            return 0;
        }
        catch (Exception e) { Save("FAILED.json", new { Error = e.ToString() }); return 1; }
    }
}
