using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;

internal static class PosturalCellProbe
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    static readonly FieldInfo Ais = typeof(ModelNeuron).GetField("_aisVoltage", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly (StructureId Id, string Model)[] Profiles =
    [ (StructureId.VestibularNuclei, "LIF"), (StructureId.CerebellarVermis, "HH"), (StructureId.FastigialNucleus, "Izhikevich") ];
    static readonly (string Name, int GlutamatePeriodMs, int GabaPeriodMs, float Quanta)[] Inputs =
    [ ("none", 0, 0, 1), ("glutamate-20hz", 50, 0, 1), ("glutamate-100hz", 10, 0, 1),
      ("glutamate-100hz-q3", 10, 0, 3), ("gaba-100hz", 0, 10, 1), ("mixed-100hz", 10, 10, 1) ];

    static void Save(string directory, string name, object value) =>
        File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, Json));

    static int Main(string[] args)
    {
        if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] != "--smoke")
            throw new ArgumentException("Arguments: new-output-directory [--smoke]");
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new InvalidOperationException("Use a new output directory.");
        Directory.CreateDirectory(output);
        bool smoke = args.Contains("--smoke");
        int durationMs = smoke ? 200 : 8000, count = smoke ? 4 : 32;
        string? oldMicrotubules = Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
        // Match the default classical cell mode explicitly; no experimental quantum terms.
        Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", "classical");
        try
        {
            Save(output, "protocol.json", new
            {
                Study = "postural-cell-response", Smoke = smoke, DurationMilliseconds = durationMs,
                SampledCellsPerCondition = count, CaseCount = 72, IntegrationStepsMilliseconds = new[] { 1.0, .25 },
                Profiles = Profiles.Select(p => new { p.Id, p.Model }), Inputs = Inputs.Select(p => new
                    { p.Name, p.GlutamatePeriodMs, p.GabaPeriodMs, p.Quanta }),
                Phases = "First quarter no external input; middle half scheduled receptor events; final quarter no external input.",
                CellIndices = "(sampleIndex * 37) modulo production population size, unique within each sample",
                Pacemaker = "Production IntrinsicCircuitDriveTopology on/off, paired in each condition. Intrinsic cell tonic/homeostatic dynamics remain enabled in both.",
                Clock = "Identical physical event times at both integration steps. Circuit clock is held at ceil(end-of-step ms), matching a 1 ms host clock with substeps.",
                InputScope = "Direct ModelNeuron.Integrate receptor events on each sampled cell. Effective quanta are imposed; no routing, synaptic contact plasticity, intercell connections or body controller.",
                Microtubules = "classical", LiveBrainAccessed = false, OldWeightsLoaded = false,
                BiologicalEquivalenceQualified = false, PhysicalRecoveryAssessed = false,
                NumericalCaution = "Finite outputs follow production clamps. Boundary occupancy is recorded; raw pre-clamp failures cannot be excluded by these observations."
            });
            var results = new List<object>();
            foreach (var profile in Profiles)
            foreach (var pacemaker in new[] { true, false })
            foreach (var dt in new[] { 1.0, .25 })
            foreach (var input in Inputs)
            {
                var circuit = StructureCircuitProfile.For(profile.Id);
                var cells = Enumerable.Range(0, count).Select(i => new ModelNeuron(i * 37 % circuit.NeuronCount, profile.Model, circuit)).ToArray();
                if (cells.Select(c => c.Index).Distinct().Count() != count) throw new InvalidOperationException("Duplicate sampled cell");
                var states = new[] { "initial", "stimulus", "return" }.ToDictionary(phase => phase,
                    _ => cells.Select(c => new CellStats(c.Index)).ToArray());
                var endSteps = (int)(durationMs / dt);
                long glutamateEvents = 0, gabaEvents = 0;
                var excitatory = new SpikeMessage { SourceStructure = StructureId.VestibularAfferents, TargetStructure = profile.Id,
                    SourceNeuronId = "imposed-diagnostic-input", SourceInstanceKey = "isolated-cell-probe", Neurotransmitter = NTEnum.GLUTAMATE, VesicleQuanta = input.Quanta };
                var inhibitory = new SpikeMessage { SourceStructure = StructureId.PurkinjeCellLayer, TargetStructure = profile.Id,
                    SourceNeuronId = "imposed-diagnostic-input", SourceInstanceKey = "isolated-cell-probe", Neurotransmitter = NTEnum.GABA, VesicleQuanta = 1 };
                // These input source names label diagnostic receptor stimuli, not claimed anatomical projections.
                for (int step = 0; step < endSteps; step++)
                {
                    var start = step * dt;
                    string phase = start < durationMs / 4 ? "initial" : start < durationMs * 3 / 4 ? "stimulus" : "return";
                    bool glutamate = phase == "stimulus" && input.GlutamatePeriodMs > 0 && (start - durationMs / 4) % input.GlutamatePeriodMs == 0;
                    bool gaba = phase == "stimulus" && input.GabaPeriodMs > 0 && (start - durationMs / 4) % input.GabaPeriodMs == 0;
                    long clock = (long)Math.Ceiling(start + dt);
                    for (int i = 0; i < count; i++)
                    {
                        var cell = cells[i];
                        if (glutamate) { excitatory.TimestampMs = start; cell.Integrate(excitatory, dt); glutamateEvents++; }
                        if (gaba) { inhibitory.TimestampMs = start; cell.Integrate(inhibitory, dt); gabaEvents++; }
                        if (pacemaker)
                        {
                            IntrinsicCircuitDriveTopology.Resolve(profile.Id, cell.Index, clock, out var excitation, out var inhibition);
                            cell.IntegratePacemakerDrive(excitation, inhibition);
                        }
                        cell.Step(dt);
                        states[phase][i].Add(cell, dt, start + dt);
                    }
                }
                var name = $"{profile.Id}-{(pacemaker ? "pacemaker" : "no-pacemaker")}-{(dt == 1 ? "1ms" : "025ms")}-{input.Name}";
                var result = new { Case = name, Structure = profile.Id, profile.Model, Pacemaker = pacemaker,
                    StepMilliseconds = dt, Input = input.Name, DurationMilliseconds = durationMs,
                    GlutamateEvents = glutamateEvents, GabaEvents = gabaEvents, Phases = states };
                Save(output, name + ".json", result);
                results.Add(new { Case = name, Structure = profile.Id, Pacemaker = pacemaker, StepMilliseconds = dt, Input = input.Name });
                Console.WriteLine($"{results.Count}/72 complete: {name}");
            }
            Save(output, "results.json", new { Completed = true, Cases = results, LiveBrainAccessed = false,
                BiologicalEquivalenceQualified = false, PhysicalRecoveryAssessed = false });
            return 0;
        }
        catch (Exception error) { Save(output, "FAILED.json", new { Error = error.ToString() }); return 1; }
        finally { Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", oldMicrotubules); }
    }

    sealed class CellStats(int index)
    {
        public int Index { get; } = index;
        public int Samples { get; private set; }
        public int Spikes { get; private set; }
        public double ElapsedMilliseconds { get; private set; }
        public double RateHz => Spikes * 1000 / ElapsedMilliseconds;
        public double MinimumSomaMillivolts { get; private set; } = double.MaxValue;
        public double MaximumSomaMillivolts { get; private set; } = double.MinValue;
        public double MinimumAisMillivolts { get; private set; } = double.MaxValue;
        public double MaximumAisMillivolts { get; private set; } = double.MinValue;
        public double MeanNetSynapticCurrent => currentTimeIntegral / ElapsedMilliseconds;
        public int AisBoundarySamples { get; private set; }
        public double AisBoundaryMilliseconds { get; private set; }
        public List<double> SpikeTimesMilliseconds { get; } = [];
        double currentTimeIntegral;
        public void Add(ModelNeuron cell, double dt, double time)
        {
            var soma = cell.MembranePotentialMillivolts;
            var ais = Convert.ToDouble(Ais.GetValue(cell));
            if (!double.IsFinite(ais) || !float.IsFinite(soma) || !float.IsFinite(cell.NetSynapticCurrent))
                throw new InvalidOperationException("Non-finite post-clamp cell observation");
            Samples++; ElapsedMilliseconds += dt;
            if (cell.IsActive) { Spikes++; SpikeTimesMilliseconds.Add(time); }
            MinimumSomaMillivolts = Math.Min(MinimumSomaMillivolts, soma);
            MaximumSomaMillivolts = Math.Max(MaximumSomaMillivolts, soma);
            MinimumAisMillivolts = Math.Min(MinimumAisMillivolts, ais);
            MaximumAisMillivolts = Math.Max(MaximumAisMillivolts, ais);
            currentTimeIntegral += cell.NetSynapticCurrent * dt;
            if (ais <= -90 || ais >= 35) { AisBoundarySamples++; AisBoundaryMilliseconds += dt; }
        }
    }
}
