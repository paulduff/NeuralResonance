using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

if (args.Length != 3 || args.Any(x => !Path.IsPathFullyQualified(x)) || Directory.Exists(args[2]))
    throw new ArgumentException("Supply absolute protocol, recorded arrivals, and NEW output paths.");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
json.Converters.Add(new JsonStringEnumConverter());
var compact = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(args[0]), json) ?? throw new ArgumentException("Missing protocol.");
string Digest(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }
if (spec.Version != "dynamic-muscle-comparison-v1" || spec.CandidateVersion != AvatarDynamicMuscle.Version ||
    !spec.StepsMs.SequenceEqual(new[] { .125, .0625, .03125 }) || spec.SampleMs != .25 || spec.Cases.Length != 11 ||
    spec.RecordedArrivalsSha256 != Digest(args[1]) || spec.ParameterRows[0].Calcium != MotorUnitCalciumParameters.NagamoriSlowUnit1)
    throw new ArgumentException("Unexpected numerical comparison inputs.");
var recorded = File.ReadLines(args[1]).Select(line => JsonSerializer.Deserialize<AvatarMotorAxonArrival>(line, compact)!).ToArray();
if (recorded.Length != 28) throw new ArgumentException("Require all recorded arrivals.");
Directory.CreateDirectory(args[2]);
void Save(string path, object value) => File.WriteAllText(Path.Combine(args[2], path), JsonSerializer.Serialize(value, json));
Save("protocol.json", JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(args[0])));
var results = new List<object>(); int successful = 0;
foreach (var scenario in spec.Cases)
foreach (double step in spec.StepsMs)
{
    string key = scenario.Key + "-h" + step.ToString("R");
    Console.WriteLine($"CASE {results.Count + 1}/33: {key}");
    var delivered = new List<AvatarMotorAxonArrival>();
    AvatarDynamicMuscle? muscle = null;
    try
    {
        var definitions = new List<SlowMotorUnitDefinition>();
        var clock = new MotorAxonClock(scenario.ReplayOriginalArrivals ? recorded[0].SessionId
            : StableNeuralIdentity.Create("dynamic-muscle-study-session", scenario.Key), 0, 0);
        for (int i = 0; i < scenario.UnitRows.Length; i++)
        {
            var row = spec.ParameterRows.Single(p => p.Row == scenario.UnitRows[i]);
            var contact = scenario.ReplayOriginalArrivals
                ? MotorAxonContact.Create(recorded[0].SourceInstanceKey, recorded[0].SourceNeuronId, recorded[0].ProjectionId, recorded[0].MotorUnitId, 2.0625)
                : MotorAxonContact.Create("dynamic-muscle-fixture", "cell-" + i,
                    StableNeuralIdentity.Create("dynamic-muscle-projection", i), "unit-" + i, 0);
            definitions.Add(new(contact, row.Calcium, row.LengthGainAboveOptimal, row.LengthGainBelowOptimal));
        }
        muscle = new AvatarDynamicMuscle(spec.Geometry, definitions, clock, scenario.InitialFiberLengthMeters,
            scenario.InitialMusculotendonLengthMeters, boundary: scenario.Boundary);
        var arrivals = new List<AvatarMotorAxonArrival>(); var ordinals = new long[definitions.Count];
        for (int index = 0; index < scenario.Events.Length; index++)
        {
            var ev = scenario.Events[index]; var contact = definitions[ev.UnitIndex].Contact; long ordinal = ++ordinals[ev.UnitIndex];
            var arrival = scenario.ReplayOriginalArrivals ? recorded[index] : new AvatarMotorAxonArrival(clock.SessionId,
                contact.SourceInstanceKey, StableNeuralIdentity.Create("dynamic-muscle-fixture-event", scenario.Key, ev.UnitIndex, ordinal),
                ordinal, contact.SourceNeuronId, contact.ProjectionId, StableNeuralIdentity.Create("dynamic-muscle-fixture-synapse", ev.UnitIndex),
                contact.ContactId, contact.MotorUnitId, ev.ArrivalAtMs, ev.ArrivalAtMs);
            if (arrival.ArrivalAtBodyMs != ev.ArrivalAtMs || arrival.ContactId != contact.ContactId)
                throw new InvalidOperationException("Declared event changed from the recorded identity or time.");
            arrivals.Add(arrival);
        }
        Save(key + "-inputs.json", new { clock, definitions, arrivals, scenario, stepMs = step,
            recordedSomaticEventsReplayed = scenario.ReplayOriginalArrivals, neuronalModelExecuted = false });
        double LengthAt(double time)
        {
            var knots = scenario.LengthKnots; int segment = 0;
            while (segment + 1 < knots.Length - 1 && time > knots[segment + 1].TimeMs) segment++;
            var a = knots[segment]; var b = knots[segment + 1];
            return scenario.InitialMusculotendonLengthMeters + a.OffsetMeters +
                (b.OffsetMeters - a.OffsetMeters) * ((time - a.TimeMs) / (b.TimeMs - a.TimeMs));
        }
        void AdvanceTo(double end)
        {
            while (muscle.TimeMs < end)
            {
                double boundary = end;
                foreach (var knot in scenario.LengthKnots)
                    if (knot.TimeMs > muscle.TimeMs && knot.TimeMs < boundary) boundary = knot.TimeMs;
                var before = muscle.Capture();
                muscle.AdvancePrescribed(new(before.TimeMs, boundary, before.MusculotendonLengthMeters, LengthAt(boundary)));
            }
        }
        using var trace = new StreamWriter(Path.Combine(args[2], key + ".csv"));
        using var eventLog = new StreamWriter(Path.Combine(args[2], key + "-arrivals.jsonl"));
        string[] core = ["timeMs", "totalLengthMeters", "fiberLengthMeters", "fiberVelocityMetersPerSecond", "yieldFraction",
            "activeFiberForceNewtons", "passiveFiberForceNewtons", "tendonAxialForceNewtons", "projectedTendonForceNewtons",
            "projectedFiberForceNewtons", "geometricForceNewtons", "accelerationMetersPerSecondSquared", "impulseNewtonSeconds",
            "workJoules", "momentumLedger", "kineticLedger"];
        string[] unitNames = ["releaseDrive", "freeCalcium", "boundCalcium", "activation", "released", "recovered", "netBound", "calciumLedger", "arrivals"];
        trace.WriteLine(string.Join(',', core.Concat(definitions.SelectMany((_, i) => unitNames.Select(n => "u" + i + "_" + n)))));
        void Sample()
        {
            var s = muscle.Capture(); var f = s.Forces;
            var values = new List<double> { s.TimeMs, s.MusculotendonLengthMeters, s.FiberLengthMeters, s.FiberVelocityMetersPerSecond,
                s.Yield, f.ActiveFiberForceNewtons, f.PassiveFiberForceNewtons, f.TendonAxialForceNewtons, f.ProjectedTendonForceNewtons,
                f.ProjectedFiberForceNewtons, f.GeometricForceNewtons, f.AccelerationMetersPerSecondSquared,
                s.IntegratedNetImpulseNewtonSeconds, s.IntegratedNetFiberWorkJoules, s.MaximumMomentumLedgerError, s.MaximumKineticLedgerError };
            foreach (var u in s.Units) values.AddRange([u.ReleaseDrive, u.FreeCalcium, u.BoundCalcium, u.Activation,
                u.ReleasedCalcium, u.RecoveredCalcium, u.NetBoundCalcium, u.MaximumCalciumLedgerError, u.Arrivals]);
            trace.WriteLine(string.Join(',', values.Select(v => v.ToString("R"))));
        }
        int next = 0;
        void ReceiveThrough(double time)
        {
            while (next < arrivals.Count && arrivals[next].ArrivalAtBodyMs <= time)
            {
                var arrival = arrivals[next++]; AdvanceTo(arrival.ArrivalAtBodyMs); muscle.Receive(arrival);
                delivered.Add(arrival); eventLog.WriteLine(JsonSerializer.Serialize(arrival, compact));
            }
        }
        ReceiveThrough(0); Sample();
        long ticks = checked((long)(scenario.DurationMs / step));
        if (ticks * step != scenario.DurationMs) throw new InvalidOperationException("Nonintegral declared endpoint grid.");
        for (long tick = 1; tick <= ticks; tick++)
        {
            double end = tick * step; ReceiveThrough(end); AdvanceTo(end);
            if (tick % (long)(spec.SampleMs / step) == 0) Sample();
        }
        if (next != arrivals.Count || !delivered.SequenceEqual(arrivals)) throw new InvalidOperationException("Incomplete or changed delivery.");
        Save(key + "-final.json", muscle.Capture());
        results.Add(new { key, completed = true, scenario = scenario.Key, stepMs = step, delivered = delivered.Count, sampleCount = (long)(scenario.DurationMs / spec.SampleMs) + 1 });
        successful++;
    }
    catch (Exception error)
    {
        if (muscle is not null) Save(key + "-failure-state.json", muscle.Capture());
        results.Add(new { key, completed = false, error = error.ToString(), delivered = delivered.Count });
        Console.WriteLine(error);
    }
    Save("results.json", new { completed = results.Count == 33, successfulCases = successful, cases = results,
        neuronalModelExecuted = false, liveBrainAccessed = false, physiologicalCalibrationComplete = false, avatarRecoveryAssessed = false });
}
if (successful != 33) throw new InvalidOperationException("Some dynamic muscle trajectories failed; preserve all partial evidence.");

internal sealed record ParameterRow(int Row, MotorUnitCalciumParameters Calcium, double LengthGainAboveOptimal, double LengthGainBelowOptimal);
internal sealed record EventInput(int UnitIndex, double ArrivalAtMs);
internal sealed record LengthKnot(double TimeMs, double OffsetMeters);
internal sealed record Scenario(string Key, FiberBoundary Boundary, double DurationMs, int[] UnitRows,
    double InitialFiberLengthMeters, double InitialMusculotendonLengthMeters, LengthKnot[] LengthKnots, EventInput[] Events, bool ReplayOriginalArrivals);
internal sealed record Protocol(string Version, string CandidateVersion, DynamicMuscleGeometry Geometry,
    ParameterRow[] ParameterRows, double[] StepsMs, double SampleMs, Scenario[] Cases, string RecordedArrivalsSha256);
