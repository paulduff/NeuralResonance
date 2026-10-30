using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;

// Deliberately artificial interventions in fresh, disconnected diagnostic cells.
// No switches or overrides are added to the production model or running services.
internal static class CellSupportProbe
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);
    static readonly FieldInfo Reserve = Field("_metabolicReserve"), Fatigue = Field("_metabolicFatigueTrace"),
        Potassium = Field("_astrocytePotassiumLoad"), Lactate = Field("_astrocyteLactateSupport");
    static readonly StructureId[] Profiles = [StructureId.CerebellarVermis, StructureId.PurkinjeCellLayer, StructureId.InferiorOlive];
    static readonly string[] Controls = ["native", "hold-reserve-fatigue", "hold-potassium", "hold-both"];
    static readonly string[] Phases = ["initial-quiet", "excitation-window", "middle-quiet", "inhibition-window", "returned-quiet"];
    static FieldInfo Field(string name) => typeof(ModelNeuron).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Diagnostic field unavailable: " + name);
    static double Value(FieldInfo field, ModelNeuron cell) => (double)field.GetValue(cell)!;
    static void Save(string file, object value) => File.WriteAllText(file, JsonSerializer.Serialize(value, Json));

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args.Skip(1).Any(a => a is not ("--support-controls" or "--smoke")))
            throw new ArgumentException("Arguments: new-output-directory --support-controls [--smoke]");
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new InvalidOperationException("Use a new isolated output directory.");
        Directory.CreateDirectory(output);
        var smoke = args.Contains("--smoke");
        int duration = smoke ? 240 : 24000, count = smoke ? 2 : 32, traceInterval = smoke ? 10 : 100;
        double[] steps = [.25, .125];
        var oldMicrotubules = Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", "classical");
            Save(Path.Combine(output, "protocol.json"), new
            {
                Study = "cell-support-controls", Smoke = smoke, DurationMilliseconds = duration,
                SampledCellsPerCondition = count, CaseCount = Profiles.Length * Controls.Length * steps.Length * 2,
                Profiles, Controls, IntegrationStepsMilliseconds = steps, Inputs = new[] { "quiet", "challenge" },
                Phases, PhaseEndsMilliseconds = new[] { duration / 3, duration / 2, duration * 2 / 3, duration * 5 / 6, duration },
                ExcitationPeriodMilliseconds = 50, InhibitionPeriodMilliseconds = 10, Quanta = 1,
                TraceIntervalMilliseconds = traceInterval, ModelDynamicsVersion = ModelNeuron.DynamicsVersion,
                PacemakerClockVersion = IntrinsicCircuitDriveTopology.ClockVersion, Microtubules = "classical",
                CellIndices = "sampleIndex * 37 modulo production population size",
                Intervention = "Before each Step: optionally set reserve=1 and fatigue=0, and independently potassium load=0. Ordinary in-step dynamics, including new potassium accumulation, remain. Native cells are never written. Reflection is confined to this diagnostic.",
                InputScope = "Quiet throughout, or 20 Hz glutamate during second phase and 100 Hz GABA during fourth phase; direct imposed receptor events, not anatomical connectivity.",
                Scope = "Fresh disconnected HH cells with production pacemaker. Artificial causal controls, not candidate biology, training, a replacement energy model or an anatomical fit.",
                Clock = "Held ceil(end-of-step biological ms) pacemaker; identical physical receptor event times at both steps.",
                LiveBrainAccessed = false, OldWeightsLoaded = false, BiologicalEquivalenceQualified = false, PhysicalRecoveryAssessed = false
            });
            var results = new List<object>();
            foreach (var profile in Profiles)
            foreach (var control in Controls)
            foreach (var dt in steps)
            foreach (var input in new[] { "quiet", "challenge" })
            {
                var circuit = StructureCircuitProfile.For(profile);
                var cells = Enumerable.Range(0, count).Select(i => new ModelNeuron(i * 37 % circuit.NeuronCount, "HH", circuit)).ToArray();
                if (cells.Select(c => c.Index).Distinct().Count() != count) throw new InvalidOperationException("Duplicate cell indices");
                var states = Phases.ToDictionary(p => p, _ => cells.Select(c => new Stats(c.Index)).ToArray());
                var name = $"{profile}-{control}-{dt.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "")}ms-{input}";
                using var trace = new StreamWriter(Path.Combine(output, name + ".jsonl"));
                long glutamateEvents = 0, gabaEvents = 0, reserveApplications = 0, potassiumApplications = 0;
                bool holdReserve = control is "hold-reserve-fatigue" or "hold-both";
                bool holdPotassium = control is "hold-potassium" or "hold-both";
                var excitation = new SpikeMessage { SourceStructure = StructureId.VestibularAfferents, TargetStructure = profile,
                    SourceInstanceKey = "isolated-support-probe", SourceNeuronId = "imposed-input", Neurotransmitter = NTEnum.GLUTAMATE, VesicleQuanta = 1 };
                var inhibition = new SpikeMessage { SourceStructure = StructureId.PurkinjeCellLayer, TargetStructure = profile,
                    SourceInstanceKey = "isolated-support-probe", SourceNeuronId = "imposed-input", Neurotransmitter = NTEnum.GABA, VesicleQuanta = 1 };
                int Phase(double t) => t < duration / 3 ? 0 : t < duration / 2 ? 1 : t < duration * 2 / 3 ? 2 : t < duration * 5 / 6 ? 3 : 4;
                for (int step = 0; step < duration / dt; step++)
                {
                    var start = step * dt; var end = start + dt; int phase = Phase(start);
                    bool glutamate = input == "challenge" && phase == 1 && (start - duration / 3) % 50 == 0;
                    bool gaba = input == "challenge" && phase == 3 && (start - duration * 2 / 3) % 10 == 0;
                    for (int i = 0; i < cells.Length; i++)
                    {
                        var cell = cells[i];
                        if (glutamate) { excitation.TimestampMs = start; cell.Integrate(excitation, dt); glutamateEvents++; }
                        if (gaba) { inhibition.TimestampMs = start; cell.Integrate(inhibition, dt); gabaEvents++; }
                        if (holdReserve)
                        {
                            Reserve.SetValue(cell, 1d); Fatigue.SetValue(cell, 0d); reserveApplications++;
                            if (Value(Reserve, cell) != 1 || Value(Fatigue, cell) != 0) throw new InvalidOperationException("Reserve intervention failed");
                        }
                        if (holdPotassium)
                        {
                            Potassium.SetValue(cell, 0d); potassiumApplications++;
                            if (Value(Potassium, cell) != 0) throw new InvalidOperationException("Potassium intervention failed");
                        }
                        IntrinsicCircuitDriveTopology.Resolve(profile, cell.Index, (long)Math.Ceiling(end), out var exc, out var inh);
                        cell.IntegratePacemakerDrive(exc, inh);
                        cell.Step(dt);
                        states[Phases[phase]][i].Add(cell, dt, end);
                    }
                    if (end % traceInterval == 0)
                        trace.WriteLine(JsonSerializer.Serialize(new { TimeMilliseconds = end, Phase = Phases[phase], Cells = cells.Select(c => new
                        {
                            c.Index, c.MembranePotentialMillivolts, c.NetSynapticCurrent, c.FiringRateHz,
                            Reserve = Value(Reserve, c), Fatigue = Value(Fatigue, c), Potassium = Value(Potassium, c), Lactate = Value(Lactate, c)
                        }) }, Compact));
                }
                Save(Path.Combine(output, name + ".json"), new { Case = name, Structure = profile, Control = control, Input = input,
                    StepMilliseconds = dt, DurationMilliseconds = duration, GlutamateEvents = glutamateEvents, GabaEvents = gabaEvents,
                    ReserveApplications = reserveApplications, PotassiumApplications = potassiumApplications, Phases = states });
                results.Add(new { Case = name, Structure = profile, Control = control, Input = input, StepMilliseconds = dt });
                Console.WriteLine($"{results.Count}/48 complete: {name}");
            }
            Save(Path.Combine(output, "results.json"), new { Completed = true, Cases = results, LiveBrainAccessed = false,
                BiologicalEquivalenceQualified = false, PhysicalRecoveryAssessed = false });
            return 0;
        }
        catch (Exception e) { Save(Path.Combine(output, "FAILED.json"), new { Error = e.ToString() }); return 1; }
        finally { Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", oldMicrotubules); }
    }

    sealed class Stats(int index)
    {
        public int Index { get; } = index;
        public int Samples { get; private set; }
        public double ElapsedMilliseconds { get; private set; }
        public List<double> SpikeTimesMilliseconds { get; } = [];
        public double RateHz => SpikeTimesMilliseconds.Count * 1000 / ElapsedMilliseconds;
        public double MinimumReserve { get; private set; } = 1;
        public double LastReserve { get; private set; }
        public double MaximumFatigue { get; private set; }
        public double MaximumPotassium { get; private set; }
        public double ReserveFloorMilliseconds { get; private set; }
        public double? FirstReserveFloorTimeMilliseconds { get; private set; }
        public void Add(ModelNeuron cell, double dt, double end)
        {
            var reserve = Value(Reserve, cell); var fatigue = Value(Fatigue, cell); var potassium = Value(Potassium, cell);
            if (!double.IsFinite(reserve) || !double.IsFinite(fatigue) || !double.IsFinite(potassium) ||
                !float.IsFinite(cell.MembranePotentialMillivolts) || !float.IsFinite(cell.NetSynapticCurrent))
                throw new InvalidOperationException("Non-finite diagnostic state");
            Samples++; ElapsedMilliseconds += dt;
            if (cell.IsActive) SpikeTimesMilliseconds.Add(end);
            MinimumReserve = Math.Min(MinimumReserve, reserve); LastReserve = reserve;
            MaximumFatigue = Math.Max(MaximumFatigue, fatigue); MaximumPotassium = Math.Max(MaximumPotassium, potassium);
            if (reserve <= .200001) { ReserveFloorMilliseconds += dt; FirstReserveFloorTimeMilliseconds ??= end; }
        }
    }
}
