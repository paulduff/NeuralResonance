using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length == 3 && args[0] == "--sdirk")
{
    SdirkStudy.Run(args[1], args[2]);
    return 0;
}

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]) || Directory.Exists(args[0]))
    throw new ArgumentException("Supply a new absolute output directory; no old brain state is loaded.");
string output = args[0]; Directory.CreateDirectory(output);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
void Save(string name, object data) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(data, json));
try
{
    var cases = new List<object>();
    double[] steps = [.015625, .0078125, .00390625];
    Save("protocol.json", new
    {
        study = "golgi-coupled-reference", candidateVersion = GolgiReferenceCell.Version, temperatureCelsius = 23,
        referenceRevision = "2ffaeca558235d61222240fc74247cab1cd02fe7", stepsMs = steps, sampleMs = .25,
        activeDurationMs = 6000, passiveDurationMs = 100, caseCount = 24,
        initialVoltageMv = -60, calciumMm = GolgiChannelKinetics.RestingCalciumMm,
        thresholdMv = GolgiReferenceCell.SpikeThresholdMv, somaIndex = 0,
        cable = new GolgiCable().CopyNodes(), gateOrder = Enum.GetNames<GolgiGate>(),
        interpretation = "Somatic crossings are measured, never imposed. Axon is passive. Explicit reference bath; no ATP, perfusion, learning, synapses, brain host or Avatar. First-order coupling needs refinement and an active-cell reference."
    });
    var protocols = new[]
    {
        ("quiet", GolgiDiagnosticBlock.None, 0d, false, 6000),
        ("depolarize", GolgiDiagnosticBlock.None, 50d, false, 6000),
        ("hyperpolarize", GolgiDiagnosticBlock.None, -50d, false, 6000),
        ("no-nap", GolgiDiagnosticBlock.WithoutPersistentSodium, 0d, false, 6000),
        ("no-hcn", GolgiDiagnosticBlock.WithoutHcn, 0d, false, 6000),
        ("no-sk", GolgiDiagnosticBlock.WithoutSk, 0d, false, 6000),
        ("passive-soma", GolgiDiagnosticBlock.Passive, 25d, false, 100),
        ("passive-electrode", GolgiDiagnosticBlock.Passive, 25d, true, 100)
    };
    foreach (var (name, block, current, electrode, duration) in protocols)
    foreach (var step in steps)
    {
        string key = name + "-h" + step.ToString(CultureInfo.InvariantCulture);
        Console.WriteLine($"CASE {cases.Count+1}/24: {key}");
        var cell = new GolgiReferenceCell(block: block);
        using var trace = new StreamWriter(Path.Combine(output, key+".csv"));
        trace.WriteLine("timeMs,somaMv,electrodeMv,highCaMm,lowCaMm,highEntered,highRemoved,lowEntered,lowRemoved,chargeErrorFc,calciumErrorMm,kirchhoffResidualPa,skMassError");
        using var cable = block == GolgiDiagnosticBlock.Passive ? new StreamWriter(Path.Combine(output, key+"-cable.csv")) : null;
        cable?.WriteLine("timeMs,"+string.Join(',',Enumerable.Range(0,135).Select(i=>"v"+i)));
        var initial = new { voltages = cell.CopyVoltages(), gates = cell.CopyGates(), sk = cell.CopySkOccupancy() };
        void Sample()
        {
            var row = new[] { cell.TimeMs, cell.SomaVoltageMv, cell.ElectrodeVoltageMv, cell.HighCalciumMm, cell.LowCalciumMm,
                cell.HighCalciumEnteredMm, cell.HighCalciumRemovedMm, cell.LowCalciumEnteredMm, cell.LowCalciumRemovedMm,
                cell.MaximumChargeBalanceErrorFc, cell.MaximumCalciumBalanceErrorMm, cell.MaximumKirchhoffResidualPa,
                Math.Abs(cell.CopySkOccupancy().Sum()-1) };
            trace.WriteLine(string.Join(',',row.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));
            cable?.WriteLine(cell.TimeMs.ToString("R",CultureInfo.InvariantCulture)+","+
                string.Join(',',cell.CopyVoltages().Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));
        }
        Sample();
        double start = block == GolgiDiagnosticBlock.Passive ? 20 : 2000, end = block == GolgiDiagnosticBlock.Passive ? 60 : 3000;
        int sampleEvery = (int)(.25/step);
        for (int index=0; index<(int)(duration/step); ++index)
        {
            double injected = index*step>=start && index*step<end ? current : 0;
            cell.Advance(step, electrode ? 0 : injected, electrode ? injected : 0);
            if ((index+1)%sampleEvery==0) Sample();
        }
        cases.Add(new
        {
            key, protocol = name, block, stepMs = step, durationMs = duration, injectionPa = current,
            injectionStartMs = start, injectionEndMs = end, electrode, initial,
            final = new { voltages = cell.CopyVoltages(), gates = cell.CopyGates(), sk = cell.CopySkOccupancy() },
            somaticCrossingsMs = cell.CopySomaticCrossings(), cell.MinimumVoltageMv, cell.MaximumVoltageMv,
            cell.MaximumChargeBalanceErrorFc, cell.MaximumCalciumBalanceErrorMm, cell.MaximumKirchhoffResidualPa
        });
    }
    Save("results.json", new { completed = true, cases, physiologyQualified = false, activeReferenceCompared = false,
        synapticCircuitAssessed = false, axonalPropagationQualified = false, avatarRecoveryAssessed = false });
    Console.WriteLine("Capture complete. Passive cable comparison and numerical refinement analysis follow; active-cell physiology remains unqualified.");
}
catch (Exception exception)
{
    Save("FAILED.json", new { error = exception.ToString() }); Console.Error.WriteLine(exception); return 1;
}
return 0;
