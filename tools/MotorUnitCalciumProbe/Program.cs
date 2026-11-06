using System.Globalization;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]) || Directory.Exists(args[0]))
    throw new ArgumentException("Supply a new absolute output directory. This probe never accesses old saves.");
Directory.CreateDirectory(args[0]);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
void Save(string name, object value) => File.WriteAllText(Path.Combine(args[0], name), JsonSerializer.Serialize(value, json));
double[] steps = [.125, .0625, .03125];
var protocols = new Dictionary<string, double[]> { ["quiet"] = [], ["single"] = [1001.125], ["pair"] = [1001.125, 1051.125] };
foreach (int hz in new[] { 5, 10, 20, 40 }) protocols["train-" + hz] = Enumerable.Range(0, 3 * hz).Select(i => 1000d + i * 1000d / hz).ToArray();
Save("protocol.json", new { version = AvatarMotorUnitCalcium.Version, stepsMs = steps, sampleMs = .25, durationMs = 6000,
    parameters = MotorUnitCalciumParameters.NagamoriSlowUnit1, referenceRevision = "c317be803f01965a1ca8fc4cbad1ba9156433af8",
    referenceRowOneBased = 1, referenceLength = 1, referenceYield = 1, referenceSag = 1,
    endogenousSpikes = false, diagnosticArrivals = true, dynamicMuscleAssessed = false, protocols });
var cases = new List<object>();
try
{
    foreach (var (name, times) in protocols)
    foreach (double h in steps)
    {
        var clock = new MotorAxonClock(Guid.NewGuid(), 0, 0);
        var contact = MotorAxonContact.Create("isolated-spinal", "fixture-alpha-1", Guid.NewGuid(), "fixture-TA-unit-1", 0);
        var cell = new AvatarMotorUnitCalcium(MotorUnitCalciumParameters.NagamoriSlowUnit1, contact, clock);
        string key = name + "-h" + h.ToString(CultureInfo.InvariantCulture);
        Console.WriteLine($"CASE {cases.Count + 1}/21: {key}");
        using var trace = new StreamWriter(Path.Combine(args[0], key + ".csv"));
        trace.WriteLine("timeMs,releaseDrive,freeCalcium,boundCalcium,storeCalcium,activation,forceNewtons,released,recovered,netBound,ledgerError");
        void Sample() => trace.WriteLine(string.Join(',', new[] { cell.TimeMs, cell.ReleaseDrive, cell.FreeCalcium,
            cell.BoundCalcium, cell.StoreCalcium, cell.Activation, cell.IsometricForceNewtons,
            cell.ReleasedCalcium, cell.RecoveredCalcium, cell.NetBoundCalcium, cell.MaximumCalciumLedgerError }
            .Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
        Sample(); int nextArrival = 0;
        for (int sample = 1; sample <= 24000; ++sample)
        {
            double target = sample * .25;
            while (cell.TimeMs < target)
            {
                double stop = Math.Min(target, cell.TimeMs + h);
                if (nextArrival < times.Length) stop = Math.Min(stop, times[nextArrival]);
                if (stop > cell.TimeMs) cell.Advance(stop - cell.TimeMs);
                if (nextArrival < times.Length && times[nextArrival] == cell.TimeMs)
                {
                    cell.Receive(new(clock.SessionId, contact.SourceInstanceKey, Guid.NewGuid(), nextArrival + 1,
                        contact.SourceNeuronId, contact.ProjectionId, Guid.NewGuid(), contact.ContactId, contact.MotorUnitId,
                        cell.TimeMs, cell.TimeMs));
                    nextArrival++;
                }
            }
            Sample();
        }
        cases.Add(new { key, protocol = name, stepMs = h, arrivalsMs = times, cell.Arrivals,
            cell.MaximumCalciumLedgerError, finalActivation = cell.Activation });
    }
    Save("results.json", new { completed = true, cases, physiologyQualified = false, neuronalSourceQualified = false,
        neuromuscularJunctionQualified = false, avatarRecoveryAssessed = false });
}
catch (Exception error) { Save("FAILED.json", new { error = error.ToString() }); throw; }
