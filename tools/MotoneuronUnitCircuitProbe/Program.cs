using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

// An isolated somatic-crossing-to-isometric-unit experiment. Observed membrane
// crossings become messages once; neither current nor a population rate is a
// spike generator. Fixed conduction and perfect endplate transmission remain
// abstractions. This does not implement an active axon or live spinal authority.
if (args.Length != 2 || args.Any(x => !Path.IsPathFullyQualified(x)) || Directory.Exists(args[1]))
    throw new ArgumentException("Supply an absolute verified reference folder and a new absolute output folder.");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var compact = new JsonSerializerOptions(JsonSerializerDefaults.Web);
string reference = args[0], output = args[1];
using var qualification = JsonDocument.Parse(File.ReadAllText(Path.Combine(reference, "boundary-analysis.json")));
foreach (string key in new[] { "completed", "numericalCriteriaPassed", "originalControlBytesReproduced", "originalAnalysisReproduced" })
    if (!qualification.RootElement.GetProperty(key).GetBoolean()) throw new ArgumentException("Source comparison must pass first.");
if (qualification.RootElement.GetProperty("originalBoundsChanged").GetBoolean()) throw new ArgumentException("Source bounds changed.");
var spec = JsonSerializer.Deserialize<ActiveStudy.Protocol>(File.ReadAllText(Path.Combine(reference, "boundary-input.json")), json)
    ?? throw new ArgumentException("Missing source protocol.");
if (spec.Version != "motoneuron-boundary-comparison-v1" || spec.DurationMs != 6000 || spec.SampleMs != .1 ||
    spec.InitialVoltageMv != -65 || spec.ThresholdMv != MotoneuronReferenceCell.SpikeThresholdMv ||
    spec.StateAccumulation != "Compensated" || spec.Cell != "FivecomptcMG_0" ||
    spec.SourceRevision != "0d11354490fad5fcd940d016a8d69b000693fd81" ||
    !spec.StepsMs.SequenceEqual(new[] { .0125, .00625, .003125 }))
    throw new ArgumentException("Unexpected source protocol.");
var active = spec.Cases.Single(c => c.Key == "soma-6000");
var quiet = spec.Cases.Single(c => c.Key == "quiet");
if (active.Block != "None" || quiet.Block != "None" ||
    !active.Segments.SequenceEqual(new[] { new ActiveStudy.Segment(1000, 0, 0, 0), new(3000, 6000, 0, 0), new(6000, 0, 0, 0) }) ||
    !quiet.Segments.SequenceEqual(new[] { new ActiveStudy.Segment(6000, 0, 0, 0) }))
    throw new ArgumentException("Source inputs differ from the prospective experiment.");
using var oldResults = JsonDocument.Parse(File.ReadAllText(Path.Combine(reference, "source-results.json")));
if (!oldResults.RootElement.GetProperty("completed").GetBoolean()) throw new ArgumentException("Incomplete source baseline.");
Directory.CreateDirectory(output);
void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json));
string Digest(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
Guid run = Guid.NewGuid();
const string instance = "isolated-motoneuron-reference", neuron = "FivecomptcMG_0", unit = "isolated-slow-unit-1";
Guid projection = StableNeuralIdentity.Create("motoneuron-unit-reference-projection", instance, neuron, unit);
Guid synapse = StableNeuralIdentity.Create("motoneuron-unit-reference-outbound", instance, neuron, projection);
const double delayMs = 2.0625, neuronStepMs = .003125, sampleMs = .25;
var conditions = new[] {
    new Condition("packet-1", "soma-6000", 1, 0, false, false),
    new Condition("packet-10", "soma-6000", 10, 0, false, false),
    new Condition("packet-50", "soma-6000", 50, 0, false, false),
    new Condition("retry-10", "soma-6000", 10, 0, true, false),
    new Condition("refined-1", "soma-6000", 10, 1, false, false),
    new Condition("refined-2", "soma-6000", 10, 2, false, false),
    new Condition("quiet", "quiet", 10, 0, false, false),
    new Condition("disconnected", "soma-6000", 10, 0, false, true)
};
Save("protocol.json", new { version = "somatic-motor-unit-circuit-v1", run, instance, neuron, unit, projection, synapse,
    delayMs, neuronStepMs, sourceSampleMs = spec.SampleMs, sampleMs, durationMs = spec.DurationMs, conditions,
    parameters = MotorUnitCalciumParameters.NagamoriSlowUnit1, neuronParameters = MotoneuronReferenceParameters.CatLowThreshold,
    calciumVersion = AvatarMotorUnitCalcium.Version, plantVersion = AvatarIsometricMotorUnitPlant.Version,
    sourceClock = "endpoint-difference", sourceBaselineAnalysisSha256 = Digest(Path.Combine(reference, "boundary-analysis.json")),
    sourceOrigin = "observed-somatic-threshold-crossing", fixedDelayAbstraction = true, idealEndplateTransmission = true,
    referenceBath = true, fixedOptimalLength = true, nativeNeuronExecuted = false, liveBrainAccessed = false });
var results = new List<object>();
int successful = 0;
foreach (var condition in conditions)
{
    Console.WriteLine($"CASE {results.Count + 1}/{conditions.Length}: {condition.Key}");
    string folder = Path.Combine(output, condition.Key); Directory.CreateDirectory(folder);
    var scenario = condition.Source == "quiet" ? quiet : active;
    var cell = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold, spec.InitialVoltageMv,
        MotoneuronDiagnosticBlock.None, MotoneuronAccumulation.Compensated);
    var clock = new MotorAxonClock(Guid.NewGuid(), 0, 0);
    var contact = MotorAxonContact.Create(instance, neuron, projection, unit, delayMs);
    var anatomy = new MotorAxonAnatomy([contact]);
    var plant = new AvatarIsometricMotorUnitPlant(anatomy, clock,
        new Dictionary<string, MotorUnitCalciumParameters> { [unit] = MotorUnitCalciumParameters.NagamoriSlowUnit1 }, condition.Refinement);
    var sender = new MotorAxonReplayBuffer(anatomy, clock, instance);
    var emitted = new List<SpikeMessage>();
    var delivered = new List<AvatarMotorAxonArrival>();
    long calls = 0, published = 0, replays = 0, delayedReceipts = 0, blockedBodyAdvances = 0;
    Int128 supplied = 0;
    try
    {
        using (var sourceTrace = new StreamWriter(Path.Combine(folder, "source.csv")))
        using (var muscleTrace = new StreamWriter(Path.Combine(folder, "muscle.csv")))
        using (var packets = new StreamWriter(Path.Combine(folder, "packets.jsonl")))
        using (var emissions = new StreamWriter(Path.Combine(folder, "emissions.jsonl")))
        using (var arrivals = new StreamWriter(Path.Combine(folder, "arrivals.jsonl")))
        {
            sourceTrace.WriteLine("timeMs," + string.Join(',', Enumerable.Range(0, 30).Select(i => "state" + i)));
            muscleTrace.WriteLine("timeMs,releaseDrive,freeCalcium,boundCalcium,storeCalcium,activation,forceNewtons,released,recovered,netBound,ledgerError,arrivals");
            void SourceSample() => sourceTrace.WriteLine(string.Join(',', new[] { cell.TimeMs }.Concat(cell.CopyState()).Select(x => x.ToString("R"))));
            void MuscleSample()
            {
                var state = plant.Capture().Units.Single();
                muscleTrace.WriteLine(string.Join(',', new[] { state.TimeMs, state.ReleaseDrive, state.FreeCalcium, state.BoundCalcium,
                    MotorUnitCalciumParameters.NagamoriSlowUnit1.TotalCalcium - state.FreeCalcium - state.BoundCalcium,
                    state.Activation, state.ForceNewtons, state.ReleasedCalcium, state.RecoveredCalcium, state.NetBoundCalcium,
                    state.MaximumCalciumLedgerError, state.Arrivals }.Select(x => x.ToString("R"))));
            }
            SourceSample(); MuscleSample();
            int segment = 0, bodySample = 1, packetSamples = condition.PacketMs * 10;
            for (int sample = 1; sample <= 60000; ++sample)
            {
                double until = sample * spec.SampleMs;
                while (cell.TimeMs < until)
                {
                    while (segment + 1 < scenario.Segments.Length && cell.TimeMs >= scenario.Segments[segment].UntilMs) ++segment;
                    var command = scenario.Segments[segment];
                    double dt = ClockAudit.BoundaryDuration(cell.TimeMs, Math.Min(until, command.UntilMs), neuronStepMs);
                    cell.Advance(dt, command.SomaCurrentPa, command.DendriteExcitatoryNs, command.DendriteInhibitoryNs);
                    supplied = checked(supplied + ClockAudit.Units(dt)); ++calls;
                    if (supplied != ClockAudit.Units(cell.TimeMs)) throw new InvalidOperationException("Source clock lost integrated duration.");
                }
                SourceSample();
                if (sample % packetSamples != 0) continue;
                // Harvest the model's actual saved crossings, retaining fractional
                // firing times. Publication cadence never splits its solver grid.
                double[] crossings = cell.CopyCrossings();
                var outgoing = new List<SpikeMessage>();
                for (int i = emitted.Count; i < crossings.Length; ++i)
                {
                    var spike = new SpikeMessage { MessageId = StableNeuralIdentity.Create("observed-motor-crossing", run, scenario.Key, i + 1),
                        TimestampMs = crossings[i], SourceStructure = StructureId.SpinalCordMotor, SourceInstanceKey = instance,
                        SourceNeuronId = neuron, ProjectionId = projection, SynapseId = synapse, Neurotransmitter = NTEnum.ACETYLCHOLINE };
                    emitted.Add(spike);
                    emissions.WriteLine(JsonSerializer.Serialize(new { ordinal = i + 1, spike, intentionallyDisconnected = condition.Disconnected }, compact));
                    if (!condition.Disconnected) outgoing.Add(spike);
                }
                // The disconnection is a declared experimental cut. Empty
                // watermarks mean no output on that cut projection, not no firing.
                var batch = sender.Publish(outgoing, cell.TimeMs); ++published;
                if (condition.Retry && published % 7 == 0)
                {
                    var before = plant.Capture(); bool blocked = false;
                    try { plant.AdvanceToTick(plant.CompletedTick + 1); }
                    catch (ArgumentOutOfRangeException) { blocked = true; }
                    if (!blocked || JsonSerializer.Serialize(before, compact) != JsonSerializer.Serialize(plant.Capture(), compact))
                        throw new InvalidOperationException("Body advanced across missing source completeness.");
                    ++blockedBodyAdvances;
                }
                var receipt = plant.AcceptWithAcknowledgement(batch);
                packets.WriteLine(JsonSerializer.Serialize(new { action = "accept", batch, receipt }, compact));
                if (condition.Retry && published % 7 == 0)
                {
                    ++delayedReceipts;
                    var retained = sender.Unacknowledged();
                    if (retained.Count != 1 || !ReferenceEquals(retained[0], batch)) throw new InvalidOperationException("Retry lost original packet.");
                    var before = plant.Capture();
                    var retried = plant.AcceptWithAcknowledgement(retained[0]);
                    packets.WriteLine(JsonSerializer.Serialize(new { action = "retry", batch = retained[0], receipt = retried }, compact));
                    if (retried != receipt ||
                        JsonSerializer.Serialize(before, compact) != JsonSerializer.Serialize(plant.Capture(), compact))
                        throw new InvalidOperationException("Retry changed accepted state.");
                    receipt = retried; ++replays;
                }
                sender.Acknowledge(receipt);
                while (bodySample * sampleMs <= plant.Capture().CompleteThroughBodyMs)
                {
                    long tick = (long)(bodySample * sampleMs / plant.StepMs);
                    var step = plant.AdvanceToTick(tick);
                    foreach (var arrival in step.Arrivals) { delivered.Add(arrival); arrivals.WriteLine(JsonSerializer.Serialize(arrival, compact)); }
                    MuscleSample(); ++bodySample;
                }
            }
        }
        var expected = oldResults.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("key").GetString() == scenario.Key + "-h0.003125");
        if (!expected.GetProperty("completed").GetBoolean() || cell.TimeMs != 6000 ||
            !cell.CopyCrossings().SequenceEqual(expected.GetProperty("crossingsMs").EnumerateArray().Select(x => x.GetDouble())) ||
            calls != expected.GetProperty("calls").GetInt64() || supplied.ToString() != expected.GetProperty("suppliedUnits").GetString() ||
            Digest(Path.Combine(folder, "source.csv")) != Digest(Path.Combine(reference, scenario.Key + "-h0.003125.csv")))
            throw new InvalidOperationException("Connected execution changed the qualified source trace.");
        var final = plant.Capture();
        long sent = condition.Disconnected ? 0 : emitted.Count;
        long due = condition.Disconnected ? 0 : emitted.Count(s => clock.ToBodyMs(s.TimestampMs) + delayMs <= 6000);
        if (final.BodyTimeMs != 6000 || final.AcceptedEvents != sent || final.DeliveredEvents != due ||
            final.PendingEvents != sent - due || final.Units.Single().Arrivals != due ||
            sender.PendingBatches != 0 || sender.PendingEvents != 0 || final.Faulted)
            throw new InvalidOperationException("Incomplete or duplicated motor event accounting.");
        // Never advance the neuron/body beyond the declared horizon to manufacture
        // a quiet tail. Preserve every accepted later arrival as pending evidence.
        var pending = condition.Disconnected ? Array.Empty<SpikeMessage>() : emitted.Where(s => clock.ToBodyMs(s.TimestampMs) + delayMs > 6000).ToArray();
        results.Add(new { key = condition.Key, completed = true, clock, contact, calls, suppliedUnits = supplied.ToString(),
            sourceCrossingsMs = cell.CopyCrossings(), emittedEvents = emitted.Count, intentionallyDisconnected = condition.Disconnected ? emitted.Count : 0,
            published, replays, delayedReceipts, blockedBodyAdvances, sourceBytesReproduced = true, final, pending });
        ++successful;
    }
    catch (Exception error)
    {
        results.Add(new { key = condition.Key, completed = false, error = error.ToString(), cell.TimeMs,
            calls, emittedEvents = emitted.Count, deliveredEvents = delivered.Count, final = plant.Capture() });
        Console.WriteLine("CONDITION FAILED: " + error.Message);
    }
    Save("results.json", new { completed = false, successfulCases = successful, cases = results });
}
Save("results.json", new { completed = true, successfulCases = successful, expectedCases = conditions.Length, cases = results,
    numericalCriteriaAssessed = false, axonalPropagationQualified = false, neuromuscularJunctionQualified = false,
    physiologicalCalibrationComplete = false, liveMotorAuthorityQualified = false, avatarRecoveryAssessed = false });
if (successful != conditions.Length) throw new InvalidOperationException("Circuit conditions incomplete; preserve failed traces.");

internal sealed record Condition(string Key, string Source, int PacketMs, int Refinement, bool Retry, bool Disconnected);
