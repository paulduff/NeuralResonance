using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;

// Numerical fixture only. Execute in a frozen visible PowerShell comparison,
// after the component/source regression gate. This does not start Unity.
internal static class ReceptorMembraneStudy
{
    private sealed record Contact(string Site, int Compartment, string Kind, double MaximumConductanceNs,
        double AxonalDelayMs, double[] SourceTimesMs);
    private sealed record Case(string Key, Contact[] Contacts);
    private sealed record Specification(string Version, double DurationMs, double SampleMs, double InitialVoltageMv,
        double[] StepsMs, string StateAccumulation, string MotorSourceRevision, string ReceptorSourceRevision,
        string Cell, double PulseConcentrationMm, double PulseDurationMs, double ThresholdMv, Case[] Cases);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal static MotoneuronCrossingInterpolation CrossingMode(string input)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(input));
        if (!document.RootElement.TryGetProperty("crossingInterpolation", out var mode))
            return MotoneuronCrossingInterpolation.Linear;
        Require(mode.ValueKind == JsonValueKind.String && mode.GetString() == "CubicHermite",
            "Only the explicit CubicHermite extension or original absent-field linear protocol is supported.");
        return MotoneuronCrossingInterpolation.CubicHermite;
    }

    internal static string CellVersion(MotoneuronCrossingInterpolation mode) =>
        MotoneuronReferenceCell.CompensatedVersion +
        (mode == MotoneuronCrossingInterpolation.CubicHermite ? MotoneuronCrossing.CubicVersionSuffix : "") +
        "+point-receptors-stage-evaluated-v1";

    internal static void Run(string input, string output)
    {
        Require(Path.IsPathFullyQualified(input) && File.Exists(input) && Path.IsPathFullyQualified(output) && !Directory.Exists(output),
            "Require a frozen absolute input and new output folder.");
        byte[] bytes = File.ReadAllBytes(input); string inputHash = Convert.ToHexString(SHA256.HashData(bytes));
        var crossing = CrossingMode(input);
        var spec = JsonSerializer.Deserialize<Specification>(bytes, Json);
        Require(spec is not null && spec.Version == "receptor-membrane-comparison-v1" && spec.DurationMs == 1500 && spec.SampleMs == .1 &&
            spec.InitialVoltageMv == -65 && spec.StateAccumulation == "Compensated" && spec.Cell == "FivecomptcMG_0" &&
            spec.MotorSourceRevision == "0d11354490fad5fcd940d016a8d69b000693fd81" &&
            spec.ReceptorSourceRevision == "937ed5d0d04519d4cd7af474a0f6c53a6e437e06" &&
            spec.PulseConcentrationMm == MotorReceptorContact.PulseConcentrationMm && spec.PulseDurationMs == MotorReceptorContact.PulseDurationMs &&
            spec.ThresholdMv == MotoneuronReferenceCell.SpikeThresholdMv && spec.StepsMs.SequenceEqual(new[] { .0125, .00625, .003125 }) &&
            spec.Cases.Select(c => c.Key).SequenceEqual(new[] { "quiet", "soma-excitation", "local-excitation", "distributed-excitation", "distributed-inhibited", "inhibition-only" }),
            "Require the prospective receptor/motor comparison specification.");
        Directory.CreateDirectory(output); var outcomes = new List<object>();
        Save(Path.Combine(output, "protocol.json"), new { inputSha256 = inputHash, specification = spec,
            motorParameters = MotoneuronReferenceParameters.CatLowThreshold,
            candidateVersion = CellVersion(crossing),
            contactVersion = MotorReceptorContact.Version, presynapticEventsAreFixtures = true,
            postsynapticNeuronExecutionRequested = true,
            presynapticNeuronsExecuted = false, nativePhysicsExecuted = false, liveBrainAccessed = false,
            physiologicalCalibrationComplete = false, avatarRecoveryAssessed = false });
        try
        {
            foreach (var scenario in spec.Cases)
            foreach (double maximum in spec.StepsMs)
            {
                string key = scenario.Key + "-h" + maximum.ToString("R", CultureInfo.InvariantCulture);
                Console.WriteLine("RECEPTOR MOTOR " + key);
                string folder = Path.Combine(output, key); Directory.CreateDirectory(folder);
                Guid session = StableNeuralIdentity.Create(spec.Version, inputHash, scenario.Key);
                Require(scenario.Contacts.Length > 0 && scenario.Contacts.Select(c => c.Site).Distinct().Count() == scenario.Contacts.Length,
                    "Every physical contact requires a distinct site.");
                var definitions = scenario.Contacts.Select(c =>
                {
                    Require(c.Kind is "AMPA" or "GABAA" && c.AxonalDelayMs == .125 && c.SourceTimesMs.All(t => double.IsFinite(t) && t >= 0 && t < spec.DurationMs),
                        "Invalid declared receptor input.");
                    string source = "fixture-" + c.Kind;
                    return new MotorReceptorDefinition(source, StructureId.PremotorCortex, source + "-cell",
                        StableNeuralIdentity.Create(spec.Version, "synapse", source), StableNeuralIdentity.Create(spec.Version, "projection", source),
                        "isolated-spinal", spec.Cell, 0, c.Site, c.Compartment,
                        c.Kind == "AMPA" ? MotorReceptorKind.AmpaReference : MotorReceptorKind.GabaAReference,
                        c.MaximumConductanceNs, c.AxonalDelayMs);
                }).ToArray();
                var cell = new ReceptorDrivenMotoneuron(MotoneuronReferenceParameters.CatLowThreshold, definitions, session,
                    spec.InitialVoltageMv, crossingInterpolation: crossing);
                var admissions = new List<object>(); var boundaries = new SortedSet<double> { spec.DurationMs };
                long expectedEvents = 0;
                for (int i = 0; i < definitions.Length; ++i)
                {
                    var d = definitions[i]; var c = scenario.Contacts[i];
                    var events = c.SourceTimesMs.Select(t => new SpikeMessage
                    {
                        MessageId = StableNeuralIdentity.Create(spec.Version, session, c.Kind, t.ToString("R", CultureInfo.InvariantCulture)),
                        TimestampMs = t, SourceInstanceKey = d.SourceInstance, SourceStructure = d.SourceStructure,
                        SourceNeuronId = d.SourceNeuron, SynapseId = d.SourceSynapse, ProjectionId = d.Projection,
                        TargetStructure = StructureId.SpinalCordMotor, TargetNeuronId = d.TargetNeuron, Neurotransmitter = d.Transmitter,
                        VesicleQuanta = 1
                    }).ToArray();
                    var receipt = cell.Admit(session, d.ContactId, 1, spec.DurationMs, events);
                    admissions.Add(new { site = c.Site, definition = d, originalEvents = events, receipt });
                    expectedEvents += events.Length;
                    foreach (var e in events)
                    {
                        double arrival = e.TimestampMs + d.AxonalDelayMs;
                        boundaries.Add(arrival); boundaries.Add(arrival + MotorReceptorContact.PulseDurationMs);
                    }
                }
                Save(Path.Combine(folder, "inputs.json"), new { scenario, session, maximumStepMs = maximum, admissions });
                var state = cell.Capture(); var contactOrder = state.Contacts.Select(c => c.ContactId).ToArray();
                Require(state.MembraneState.Length == 30 && state.TimeMs == 0 && state.DeliveredEvents == 0 && !state.Faulted, "Fresh initial state required.");
                string csv = Path.Combine(folder, "trajectory.csv"); Int128 supplied = 0; long rows = 0;
                using (var writer = new StreamWriter(csv))
                {
                    writer.WriteLine(string.Join(',', new[] { "timeMs", "dtMs" }.Concat(Enumerable.Range(0, 30).Select(i => "state" + i))
                        .Concat(contactOrder.Select(id => "receptor_" + id.ToString("D")))));
                    void Record(double dt)
                    {
                        Require(state.Contacts.Select(c => c.ContactId).SequenceEqual(contactOrder), "Contact ordering changed.");
                        writer.WriteLine(string.Join(',', new[] { state.TimeMs, dt }.Concat(state.MembraneState)
                            .Concat(state.Contacts.Select(c => c.Value.OpenFraction)).Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                        ++rows;
                    }
                    Record(0);
                    double[] transitions = boundaries.ToArray(); int transition = 0;
                    int samples = checked((int)(spec.DurationMs / spec.SampleMs));
                    Require(samples * spec.SampleMs == spec.DurationMs, "Unrepresentable output horizon.");
                    for (int sample = 1; sample <= samples; ++sample)
                    {
                        double observation = sample * spec.SampleMs;
                        while (state.TimeMs < observation)
                        {
                            while (transition < transitions.Length && transitions[transition] <= state.TimeMs) ++transition;
                            double boundary = Math.Min(observation, transition < transitions.Length ? transitions[transition] : spec.DurationMs);
                            double dt = ReceptorDrivenMotoneuron.BoundaryDuration(state.TimeMs, boundary, maximum);
                            double next = state.TimeMs + dt; long previousSteps = state.IntegrationSteps;
                            cell.AdvanceTo(next, maximum); state = cell.Capture();
                            supplied = checked(supplied + ClockAudit.Units(dt));
                            Require(state.TimeMs == next && state.IntegrationSteps == previousSteps + 1 && supplied == ClockAudit.Units(next) && !state.Faulted,
                                "Every accepted membrane step and its exact supplied duration must be retained.");
                            Record(dt);
                        }
                        Require(state.TimeMs == observation, "Observation endpoint changed.");
                    }
                }
                Require(state.TimeMs == spec.DurationMs && state.AdmittedEvents == expectedEvents && state.DeliveredEvents == expectedEvents &&
                    state.Contacts.Sum(c => (long)c.AcceptedEvents) == expectedEvents && rows == state.IntegrationSteps + 1,
                    "Incomplete original-event or accepted-step accounting.");
                Save(Path.Combine(folder, "final.json"), new { state, rows, suppliedUnits = supplied.ToString(), scalePower = 80, contactOrder });
                using var stream = File.OpenRead(csv);
                outcomes.Add(new { key, scenario = scenario.Key, maximumStepMs = maximum, completed = true, rows,
                    deliveredEvents = state.DeliveredEvents, crossings = state.Crossings.Length, trajectorySha256 = Convert.ToHexString(SHA256.HashData(stream)) });
                Save(Path.Combine(output, "progress.json"), outcomes);
            }
            Save(Path.Combine(output, "results.json"), new { completed = true, expectedConditions = 18, cases = outcomes,
                postsynapticNeuronExecuted = true,
                numericalAccuracyQualified = false, presynapticEventsAreFixtures = true, presynapticNeuronsExecuted = false,
                nativePhysicsExecuted = false, physiologicalCalibrationComplete = false, avatarRecoveryAssessed = false });
        }
        catch (Exception error)
        {
            Save(Path.Combine(output, "FAILED.json"), new { error = error.ToString(), completedCases = outcomes.Count, numericalAccuracyQualified = false });
            throw;
        }
    }
}
