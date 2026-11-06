using System.Globalization;
using System.Text.Json;

if (args.Length == 3 && args[0] == "--active")
{
    ActiveStudy.Run(args[1], args[2]);
    return;
}

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]) || Directory.Exists(args[0]))
    throw new ArgumentException("Supply a new absolute output directory; old saves are never loaded.");
Directory.CreateDirectory(args[0]);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
void Save(string name, object value) => File.WriteAllText(Path.Combine(args[0], name), JsonSerializer.Serialize(value, json));
var parameters = MotoneuronReferenceParameters.CatLowThreshold;
double[] times = [0, .0125, .125, 1, 10, 100, 1000];
var clamps = new List<object>();
try
{
    foreach (double temperature in new[] { 24d, 37 })
    foreach (double voltage in new[] { -100d, -80, -65, -44, -29, -20, 0, 40 })
    foreach (double calcium in new[] { .0001, .0002, .001 })
    foreach (double availability in new[] { 0d, 1 })
    {
        var channels = parameters.Channels with { TemperatureCelsius = temperature,
            TransientSodiumAvailability = availability, PersistentSodiumAvailability = availability };
        var initial = new MotoneuronGateRate[12];
        MotoneuronChannelKinetics.Soma(-65, .0001, channels, initial);
        MotoneuronChannelKinetics.Dendrite(-65, channels, initial.AsSpan(9));
        var rates = new MotoneuronGateRate[12];
        MotoneuronChannelKinetics.Soma(voltage, calcium, channels, rates);
        MotoneuronChannelKinetics.Dendrite(voltage, channels, rates.AsSpan(9));
        double[] gates = initial.Select(r => r.Equilibrium).ToArray();
        var samples = new List<object>(); double previous = 0;
        foreach (double time in times)
        {
            for (int i = 0; i < gates.Length; ++i) gates[i] = MotoneuronChannelKinetics.Relax(gates[i], rates[i], time - previous);
            samples.Add(new { timeMs = time, gates = (double[])gates.Clone() }); previous = time;
        }
        clamps.Add(new { temperature, voltage, calcium, availability, rates, samples });
    }
    Save("clamps.json", new { cases = clamps, timesMs = times, caseCount = clamps.Count });
    Console.WriteLine($"Captured {clamps.Count} voltage/calcium clamps.");
    var geometry = new MotoneuronReferenceCell(parameters);
    Save("protocol.json", new { version = MotoneuronReferenceCell.Version,
        sourceRevision = "0d11354490fad5fcd940d016a8d69b000693fd81", parameters,
        areasUm2 = geometry.CopyAreasUm2(), capacitancesPf = geometry.CopyCapacitancesPf(),
        couplingNs = geometry.CopyCouplingNs(),
        somaticCalciumReversalMv = MotoneuronChannelKinetics.SomaticCalciumReversalMv,
        sodiumReversalMv = MotoneuronChannelKinetics.SodiumReversalMv,
        faraday = MotoneuronChannelKinetics.FaradayCoulombsPerMole,
        initialVoltageMv = -71, durationMs = 100, sampleMs = .25,
        currentsPa = new[] { -100d, 100, 200 }, stepsMs = new[] { .0125, .00625, .003125 },
        block = "Passive", activeFiringAssessed = false, liveBrainAccessed = false });
    var cases = new List<object>();
    foreach (double current in new[] { -100d, 100, 200 })
    foreach (double h in new[] { .0125, .00625, .003125 })
    {
        string key = "passive-i" + current.ToString(CultureInfo.InvariantCulture) + "-h" + h.ToString(CultureInfo.InvariantCulture);
        Console.WriteLine(key);
        var cell = new MotoneuronReferenceCell(parameters, -71, MotoneuronDiagnosticBlock.Passive);
        using var trace = new StreamWriter(Path.Combine(args[0], key + ".csv"));
        trace.WriteLine("timeMs," + string.Join(',', Enumerable.Range(0, 30).Select(i => "state" + i)));
        void Sample() => trace.WriteLine(string.Join(',', new[] { cell.TimeMs }.Concat(cell.CopyState()).Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
        Sample();
        for (int sample = 1; sample <= 400; ++sample)
        {
            double until = sample * .25;
            while (cell.TimeMs < until) cell.Advance(Math.Min(h, until - cell.TimeMs), current);
            Sample();
        }
        cases.Add(new { key, currentPa = current, stepMs = h, cell.MaximumChargeLedgerErrorFc, cell.MaximumCalciumLedgerErrorMm, crossings = cell.CopyCrossings() });
    }
    Save("results.json", new { completed = true, cases, activeFiringQualified = false, neuronalMotorSourceQualified = false, avatarRecoveryAssessed = false });
}
catch (Exception error) { Save("FAILED.json", new { error = error.ToString() }); throw; }
