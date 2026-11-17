using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

// Full active-cell source replay, only in the frozen visible comparison window.
// The earlier raw recorder provides the comparison observations, never the
// state or firing events that drive this new cell or its Avatar receiver.
internal static class ReceptorMotorOutputStudy
{
    private sealed record Specification(string Version, string SourceVersion, int BaselineConditions, int SourceConditions,
        double SampleMs, double DurationMs, int[] PublicationEverySamples, int RetryEveryBatches,
        int BackpressureEveryBatches, string MotorUnit, double ConductionDelayMs);
    private sealed record Admission(string Site, MotorReceptorDefinition Definition, SpikeMessage[] OriginalEvents,
        MotorReceptorBatchReceipt Receipt);
    private sealed record Inputs(Guid Session, double MaximumStepMs, Admission[] Admissions);
    private sealed record BaselineFinal(ReceptorDrivenMotorState State, long Rows, Guid[] ContactOrder);
    private sealed record Observation(double TimeMs, double[] Values, long Steps);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions LineJson = new(JsonSerializerDefaults.Web);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
    private static string Bytes(object value) => JsonSerializer.Serialize(value, LineJson);
    private static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static bool EqualBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    // Read saved comparison points only; no state is installed into the source.
    private static Observation[] Observations(string path, int count, double sampleMs, Guid[] order, long expectedRows)
    {
        using var reader = new StreamReader(path);
        string[] expected = new[] { "timeMs", "dtMs" }.Concat(Enumerable.Range(0, 30).Select(i => "state" + i))
            .Concat(order.Select(id => "receptor_" + id.ToString("D"))).ToArray();
        Require(reader.ReadLine() == string.Join(',', expected), "Unexpected baseline trajectory columns.");
        var samples = new List<Observation>(); long rows = 0; string line;
        while ((line = reader.ReadLine()) is not null)
        {
            double time = double.Parse(line.AsSpan(0, line.IndexOf(',')), CultureInfo.InvariantCulture);
            if (samples.Count <= count)
            {
                double target = samples.Count * sampleMs;
                Require(time <= target, "Baseline omitted a required observation.");
                if (time == target)
                {
                    var values = line.Split(',').Skip(2).Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    Require(values.Length == 30 + order.Length && values.All(double.IsFinite), "Incomplete baseline state.");
                    samples.Add(new(time, values, rows));
                }
            }
            ++rows;
        }
        Require(rows == expectedRows && samples.Count == count + 1, "Incomplete baseline observations.");
        return samples.ToArray();
    }

    internal static void Run(string membraneInput, string outputInput, string baseline, string output)
    {
        Require(new[] { membraneInput, outputInput, baseline, output }.All(Path.IsPathFullyQualified) &&
            File.Exists(membraneInput) && File.Exists(outputInput) && Directory.Exists(baseline) &&
            !Directory.Exists(output) && !File.Exists(output), "Require frozen absolute inputs and a new output directory.");
        var spec = Read<Specification>(outputInput);
        var crossing = ReceptorMembraneStudy.CrossingMode(membraneInput);
        string sourceVersion = crossing == MotoneuronCrossingInterpolation.CubicHermite ? ReceptorMotorSource.CubicVersion : ReceptorMotorSource.Version;
        Require(spec is not null && spec.Version == "receptor-motor-output-comparison-v1" && spec.SourceVersion == sourceVersion &&
            spec.BaselineConditions == 18 && spec.SourceConditions == 36 && spec.SampleMs == .1 && spec.DurationMs == 1500 &&
            spec.PublicationEverySamples.SequenceEqual(new[] { 1, 17 }) && spec.RetryEveryBatches == 37 &&
            spec.BackpressureEveryBatches == 113 && spec.MotorUnit == "fixture-motor-unit" && spec.ConductionDelayMs == 2.0625,
            "Require the declared active source/output matrix.");
        using var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(baseline, "protocol.json")));
        using var results = JsonDocument.Parse(File.ReadAllText(Path.Combine(baseline, "results.json")));
        Require(meta.RootElement.GetProperty("inputSha256").GetString() == Hash(membraneInput) &&
            meta.RootElement.GetProperty("candidateVersion").GetString() == ReceptorMembraneStudy.CellVersion(crossing) &&
            results.RootElement.GetProperty("completed").GetBoolean() && results.RootElement.GetProperty("cases").GetArrayLength() == 18,
            "Require the completed raw candidate recorder from these exact inputs.");
        Directory.CreateDirectory(output); var outcomes = new List<object>(); bool excitedCrossings = false;
        Save(Path.Combine(output, "protocol.json"), new { specification = spec, inputSha256 = Hash(outputInput),
            membraneInputSha256 = Hash(membraneInput), baselineResultsSha256 = Hash(Path.Combine(baseline, "results.json")),
            presynapticEventsAreFixtures = true, presynapticNeuronsExecuted = false,
            postsynapticNeuronExecutionRequested = true, actualAvatarReceiverRequested = true,
            nativePhysicsExecuted = false, muscleDynamicsExecuted = false, physiologicalCalibrationComplete = false, avatarRecoveryAssessed = false });
        try
        {
            int count = checked((int)(spec.DurationMs / spec.SampleMs));
            Require(count == 15000 && count * spec.SampleMs == spec.DurationMs, "Changed observation horizon.");
            foreach (var entry in results.RootElement.GetProperty("cases").EnumerateArray())
            {
                string baselineKey = entry.GetProperty("key").GetString();
                string scenario = entry.GetProperty("scenario").GetString();
                Require(Path.GetFileName(baselineKey) == baselineKey && !baselineKey.Contains(".."), "Invalid baseline key.");
                string original = Path.Combine(baseline, baselineKey);
                Require(Hash(Path.Combine(original, "trajectory.csv")) == entry.GetProperty("trajectorySha256").GetString(), "Baseline trace changed.");
                var inputs = Read<Inputs>(Path.Combine(original, "inputs.json")); var final = Read<BaselineFinal>(Path.Combine(original, "final.json"));
                Require(inputs.MaximumStepMs == entry.GetProperty("maximumStepMs").GetDouble() && inputs.Admissions.Length > 0 &&
                    final.State.SessionId == inputs.Session && !final.State.Faulted, "Invalid baseline input or final state.");
                var observations = Observations(Path.Combine(original, "trajectory.csv"), count, spec.SampleMs, final.ContactOrder, final.Rows);
                foreach (int cadence in spec.PublicationEverySamples)
                {
                    string key = baselineKey + "-packets" + cadence; Console.WriteLine("ACTIVE RECEPTOR MOTOR OUTPUT " + key);
                    string folder = Path.Combine(output, key); Directory.CreateDirectory(folder);
                    var target = inputs.Admissions[0].Definition;
                    var identity = new ReceptorMotorIdentity(target.TargetInstance, target.TargetNeuron,
                        StableNeuralIdentity.Create(sourceVersion, "projection", inputs.Session, target.TargetInstance, target.TargetNeuron),
                        StableNeuralIdentity.Create(sourceVersion, "synapse", inputs.Session, target.TargetInstance, target.TargetNeuron));
                    var anatomy = new MotorAxonAnatomy([MotorAxonContact.Create(identity.Instance, identity.Neuron, identity.Projection,
                        spec.MotorUnit, spec.ConductionDelayMs)]);
                    var clock = new MotorAxonClock(inputs.Session, 0, 0);
                    var source = new ReceptorMotorSource(MotoneuronReferenceParameters.CatLowThreshold,
                        inputs.Admissions.Select(a => a.Definition).ToArray(), inputs.Session, identity, inputs.MaximumStepMs,
                        crossingInterpolation: crossing);
                    var lane = new ReceptorMotorLane(source, anatomy, clock, maximumBatches: 1);
                    var receiver = new AvatarMotorAxonReceiver(anatomy, clock);
                    foreach (var admission in inputs.Admissions)
                        Require(lane.Admit(inputs.Session, admission.Receipt.ContactId, 1, spec.DurationMs, admission.OriginalEvents) == admission.Receipt,
                            "Reconstructed input differs from its original retained receipt.");
                    Save(Path.Combine(folder, "initial.json"), new { baselineKey, publicationEverySamples = cadence, identity, clock,
                        contacts = anatomy.Contacts, anatomyFingerprint = anatomy.Fingerprint, source = lane.CaptureSource(),
                        baselineInputsSha256 = Hash(Path.Combine(original, "inputs.json")) });
                    long batches = 0, retries = 0, blocked = 0, delivered = 0;
                    using (var trajectory = new StreamWriter(Path.Combine(folder, "trajectory.csv")))
                    using (var transport = new StreamWriter(Path.Combine(folder, "transport.jsonl")))
                    {
                        trajectory.WriteLine(string.Join(',', new[] { "timeMs" }.Concat(Enumerable.Range(0, 30).Select(i => "state" + i))
                            .Concat(final.ContactOrder.Select(id => "receptor_" + id.ToString("D")))
                            .Concat(new[] { "integrationSteps", "admittedEvents", "deliveredEvents", "observedCrossings" })));
                        void Record(int sample, ReceptorMotorSourceState state)
                        {
                            var cell = state.Cell; var expected = observations[sample];
                            var values = cell.MembraneState.Concat(cell.Contacts.Select(c => c.Value.OpenFraction)).ToArray();
                            bool matches = !state.Faulted && state.DiagnosticBlock == MotoneuronDiagnosticBlock.None && state.CompletedSamples == sample &&
                                cell.TimeMs == expected.TimeMs && cell.IntegrationSteps == expected.Steps &&
                                cell.Contacts.Select(c => c.ContactId).SequenceEqual(final.ContactOrder) &&
                                values.Length == expected.Values.Length && values.Zip(expected.Values).All(p => EqualBits(p.First, p.Second));
                            trajectory.WriteLine(string.Join(',', new[] { cell.TimeMs }.Concat(values)
                                .Concat(new double[] { cell.IntegrationSteps, cell.AdmittedEvents, cell.DeliveredEvents, cell.Crossings.Length })
                                .Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                            if (!matches) Save(Path.Combine(folder, "OBSERVATION_MISMATCH.json"), new { sample, expected, actual = state });
                            Require(matches, "Source observation differs from the raw candidate at the same neural endpoint; saved the mismatch.");
                        }
                        Record(0, lane.CaptureSource());
                        for (int sample = 1; sample <= count; ++sample)
                        {
                            var state = lane.AdvanceOneSample(); Record(sample, state);
                            if (sample % cadence != 0 && sample != count) continue;
                            string before = Bytes(state); var batch = lane.PublishCompleted(); ++batches;
                            bool checkBlocked = batches % spec.BackpressureEveryBatches == 0 || sample == count;
                            if (checkBlocked)
                            {
                                bool rejected = false;
                                try { lane.AdvanceOneSample(); } catch (InvalidOperationException) { rejected = true; }
                                Require(rejected && Bytes(lane.CaptureSource()) == before, "Backpressure altered neuronal state."); ++blocked;
                            }
                            var receipt = receiver.AcceptWithAcknowledgement(batch);
                            bool replayed = batches % spec.RetryEveryBatches == 0 || sample == count;
                            if (replayed)
                            {
                                Require(ReferenceEquals(lane.Unacknowledged().Single(), batch) &&
                                    receiver.AcceptWithAcknowledgement(batch) == receipt, "Retry changed the original batch or receipt."); ++retries;
                            }
                            lane.Acknowledge(receipt); if (replayed) lane.Acknowledge(receipt);
                            Require(Bytes(lane.CaptureSource()) == before && lane.Unacknowledged().Count == 0,
                                "Publication/acknowledgement changed neuronal state or lost a retained batch.");
                            var arrivals = receiver.AdvanceTo(batch.CompleteThroughNeuralMs); delivered += arrivals.Count;
                            transport.WriteLine(Bytes(new { sample, batch, canonicalBatchJson = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(batch)),
                                receipt, replayed, blockedAdvanceChecked = checkBlocked, arrivals,
                                receiverBodyTimeMs = receiver.BodyTimeMs, receiverCompleteThroughBodyMs = receiver.CompleteThroughBodyMs,
                                receiverPendingEvents = receiver.PendingEvents, receiverDeliveredEvents = receiver.DeliveredEvents }));
                        }
                    }
                    var last = lane.CaptureSource();
                    Save(Path.Combine(folder, "final.json"), new { source = last, batches, retries, blockedAdvanceChecks = blocked,
                        receiverBodyTimeMs = receiver.BodyTimeMs, receiverCompleteThroughBodyMs = receiver.CompleteThroughBodyMs,
                        receiverPendingEvents = receiver.PendingEvents, receiverDeliveredEvents = receiver.DeliveredEvents,
                        publishedThroughNeuralMs = lane.PublishedThroughNeuralMs, unpublishedEmissions = lane.UnpublishedEmissions });
                    Require(Bytes(last.Cell) == Bytes(final.State) && last.Cell.Crossings.SequenceEqual(last.Emissions.Select(e => e.FiredAtNeuralMs)) &&
                        lane.UnpublishedEmissions == 0 && lane.PublishedThroughNeuralMs == spec.DurationMs &&
                        delivered == receiver.DeliveredEvents && delivered + receiver.PendingEvents == last.Emissions.Count,
                        "Incomplete source or receiver accounting; retain any arrivals beyond the declared horizon.");
                    if (scenario == "quiet") Require(last.Emissions.Count == 0, "Quiet source emitted unexpected crossings.");
                    if (scenario is "soma-excitation" or "local-excitation" or "distributed-excitation")
                        excitedCrossings |= last.Emissions.Count > 0;
                    outcomes.Add(new { key, baselineKey, publicationEverySamples = cadence, completed = true, observationPoints = count + 1,
                        crossings = last.Emissions.Count, batches, retries, blockedAdvanceChecks = blocked,
                        trajectorySha256 = Hash(Path.Combine(folder, "trajectory.csv")), transportSha256 = Hash(Path.Combine(folder, "transport.jsonl")) });
                    Save(Path.Combine(output, "progress.json"), outcomes);
                }
            }
            Require(outcomes.Count == 36 && excitedCrossings, "Incomplete active source comparison or no excited crossing train.");
            Save(Path.Combine(output, "results.json"), new { completed = true, exactObservedStateAndDeliveryChecksPassed = true, cases = outcomes,
                quietHasNoCrossings = true, nontrivialExcitedCrossings = excitedCrossings,
                postsynapticNeuronExecuted = true, actualAvatarReceiverExecuted = true, presynapticEventsAreFixtures = true,
                presynapticNeuronsExecuted = false, nativePhysicsExecuted = false, muscleDynamicsExecuted = false,
                independentNumericalAccuracyQualified = false, physiologicalCalibrationComplete = false, avatarRecoveryAssessed = false });
        }
        catch (Exception error)
        {
            Save(Path.Combine(output, "FAILED.json"), new { error = error.ToString(), completedCases = outcomes.Count,
                exactObservedStateAndDeliveryChecksPassed = false }); throw;
        }
    }
}
