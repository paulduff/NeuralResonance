using System.Text.Json;

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]) || Directory.Exists(args[0]))
    throw new ArgumentException("Supply a new absolute output directory; this probe never loads brain saves.");
string output = args[0];
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
double[] voltages = [-100, -80, -60, -40, 0, 30], calcium = [.00005, .001, .01], temperatures = [23, 37];
double[] times = [0, .125, 1, 5, 25, 100, 250];
var cases = new List<object>();
foreach (double temperature in temperatures)
foreach (double voltage in voltages)
foreach (double ca in calcium)
{
    var cell = new GolgiChannelKinetics(temperatureCelsius: temperature);
    var samples = new List<object>();
    double previous = 0;
    foreach (double time in times)
    {
        cell.AdvanceVoltageClamp(voltage, ca, time - previous);
        var currents = cell.Currents(voltage, ca, ca);
        samples.Add(new { timeMs = time, gates = cell.CopyGates(), sk = cell.CopySkOccupancy(), currents });
        previous = time;
    }
    cases.Add(new { voltageMv = voltage, calciumMm = ca, temperatureCelsius = temperature, samples });
    Console.WriteLine($"Captured clamp {cases.Count}/36: {voltage} mV, Ca={ca} mM, {temperature} C");
}
File.WriteAllText(Path.Combine(output, "channels.json"), JsonSerializer.Serialize(new
{
    completed = true, candidateVersion = GolgiChannelKinetics.CandidateVersion,
    sourceRevision = "2ffaeca558235d61222240fc74247cab1cd02fe7", caseCount = cases.Count,
    initialVoltageMv = -60, initialCalciumMm = GolgiChannelKinetics.RestingCalciumMm,
    gateOrder = Enum.GetNames<GolgiGate>(), timesMs = times, cases,
    liveBrainAccessed = false, oldSavesLoaded = false, freeMembraneAssessed = false,
    synapticCircuitAssessed = false, physicalRecoveryAssessed = false
}, json));
Console.WriteLine("Channel capture complete. Independent SK2 matrix validation follows; free-cell firing and circuit function remain unassessed.");
