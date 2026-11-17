using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

internal static class VestibularHairCellStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    internal sealed record Change(double TimeMs, double DisplacementNanometers);
    internal sealed record Condition(string Key, Change[] Changes);
    internal sealed record Protocol(string Version, string CandidateVersion, Guid SessionId,
        AvatarHairCellParameters Parameters, AvatarHairCellBath Bath, double InitialVoltageMv,
        double DurationMs, double SamplePeriodMs, double[] StepsMs, Condition[] Cases);

    internal static void Run(string inputPath, string output)
    {
        Require(Path.IsPathFullyQualified(inputPath) && Path.IsPathFullyQualified(output) &&
            !File.Exists(output) && !Directory.Exists(output), "Require frozen absolute input and new output.");
        var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(inputPath), Json);
        Require(spec is not null && spec.Version == "vestibular-hair-cell-comparison-v1" &&
            spec.CandidateVersion == AvatarVestibularHairCell.Version && spec.SessionId != Guid.Empty &&
            spec.Parameters == AvatarHairCellParameters.TypeIReference && spec.Bath == AvatarHairCellBath.Reference &&
            spec.InitialVoltageMv == -75 && spec.DurationMs == 1000 && spec.SamplePeriodMs == .125 &&
            spec.StepsMs.SequenceEqual(new[] { .03125, .015625, .0078125 }) &&
            spec.Cases.Select(c => c.Key).SequenceEqual(new[] { "quiet", "positive", "negative", "strong", "reversal", "paired" }),
            "Unexpected reduced hair-cell protocol.");
        foreach (var condition in spec.Cases)
            Require(condition.Changes.Length > 0 && condition.Changes[0].TimeMs == 0 &&
                condition.Changes.All(c => double.IsFinite(c.DisplacementNanometers) && c.TimeMs >= 0 && c.TimeMs < spec.DurationMs &&
                    c.TimeMs / spec.SamplePeriodMs == Math.Truncate(c.TimeMs / spec.SamplePeriodMs)) &&
                condition.Changes.Zip(condition.Changes.Skip(1)).All(p => p.First.TimeMs < p.Second.TimeMs),
                "Explicit ordered held displacement changes must lie on recording boundaries.");
        Directory.CreateDirectory(output);
        WriteNew(Path.Combine(output, "input.json"), spec);
        int arithmeticRows = ChannelArithmetic(spec, Path.Combine(output, "channel-arithmetic.jsonl"));
        var results = new List<object>(); bool passed = true;
        foreach (var condition in spec.Cases)
        foreach (double step in spec.StepsMs)
        {
            string key = condition.Key + "-h" + step.ToString("R", CultureInfo.InvariantCulture);
            Console.WriteLine("HAIR CELL " + key);
            string folder = Path.Combine(output, key); Directory.CreateDirectory(folder);
            var kernel = new AvatarHairCellKernel(spec.Parameters, spec.Bath);
            var cell = new AvatarVestibularHairCell(spec.SessionId,
                StableNeuralIdentity.Create("type-I-hair-cell-fixture-v1", key), kernel, spec.InitialVoltageMv, step);
            WriteNew(Path.Combine(folder, "initial.json"), cell.Capture());
            int samples = 0; string error = null;
            try
            {
                using var stream = new FileStream(Path.Combine(folder, "trajectory.csv"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                writer.WriteLine("timeMs,bundleDisplacementNm,previousIntervalBundleDisplacementNm,voltageMv,kLow,ca,hcn1,metChargeFc,kLowChargeFc,calciumChargeFc,hcnKChargeFc,hcnNaChargeFc,pumpKChargeFc,pumpNaChargeFc,metPa,kLowPa,calciumPa,hcnKPa,hcnNaPa,pumpKPa,pumpNaPa");
                int count = checked((int)(spec.DurationMs / spec.SamplePeriodMs));
                long substeps = checked((long)(spec.SamplePeriodMs / step));
                Require(substeps * step == spec.SamplePeriodMs, "Recording interval must divide into original solver steps.");
                for (int i = 0; i <= count; i++)
                {
                    double time = i * spec.SamplePeriodMs;
                    double held = At(condition, i == 0 ? 0 : time - spec.SamplePeriodMs);
                    if (i > 0) cell.AdvanceHeldDisplacement(substeps, held);
                    var state = cell.Capture(); double available = At(condition, time);
                    Require(state.TimeMs == time && state.CompletedSteps == i * substeps && !state.Faulted, "Cell clock/coverage changed.");
                    var currents = kernel.Currents(state.Values[0], state.Values[1], state.Values[2], state.Values[3], available);
                    double[] row = [time, available, held, .. state.Values, currents.MetPa, currents.PotassiumLowPa,
                        currents.CalciumPa, currents.HcnPotassiumPa, currents.HcnSodiumPa, currents.PumpPotassiumPa, currents.PumpSodiumPa];
                    writer.WriteLine(string.Join(',', row.Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
                    samples++;
                }
            }
            catch (Exception exception) { passed = false; error = exception.ToString(); Console.WriteLine(error); }
            var final = cell.Capture();
            var result = new { key, condition, stepMs = step, samples, completed = error is null, error,
                finalState = final, recordedInputIsPrescribedDisplacement = true, afferentMembraneIntegrated = false };
            WriteNew(Path.Combine(folder, "result.json"), result); results.Add(result);
        }
        WriteNew(Path.Combine(output, "results.json"), new { completed = passed, cases = results, arithmeticRows,
            hairCellMembraneIntegrated = true, fixedBathReduction = true, afferentMembraneIntegrated = false,
            liveBrainAccessed = false, nativeBodyExecuted = false, physiologyQualified = false, avatarRecoveryAssessed = false });
        Require(passed && results.Count == 18 && arithmeticRows == 7260, "Hair-cell capture incomplete; preserve the failed prefixes.");
    }

    private static int ChannelArithmetic(Protocol spec, string path)
    {
        var baths = new[] { spec.Bath, spec.Bath with { PotassiumOutsideMm = 20 },
            spec.Bath with { BasolateralOutsidePotentialMv = 9 } };
        int count = 0;
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        for (int bathIndex = 0; bathIndex < baths.Length; bathIndex++)
        {
            var kernel = new AvatarHairCellKernel(spec.Parameters, baths[bathIndex]);
            for (int vi = 0; vi <= 120; vi++)
            foreach (double displacement in new[] { -1000d, -300, 0, 300, 1000 })
            {
                double voltage = -120 + vi * 1.5;
                var rates = AvatarHairCellKernel.Rates(voltage - baths[bathIndex].BasolateralOutsidePotentialMv);
                double[][] presets = [[0, 0, 0], [1, 1, 1], [.2, .3, .4],
                    [rates.PotassiumLow.Equilibrium, rates.Calcium.Equilibrium, rates.Hcn1.Equilibrium]];
                for (int gatePreset = 0; gatePreset < presets.Length; gatePreset++)
                {
                    var gates = presets[gatePreset];
                    writer.WriteLine(JsonSerializer.Serialize(new { index = count, bathIndex, gatePreset,
                        voltageMv = voltage, displacementNanometers = displacement, gates, rates,
                        metOpenProbability = AvatarHairCellKernel.MetOpenProbability(displacement),
                        currents = kernel.Currents(voltage, gates[0], gates[1], gates[2], displacement),
                        kernel.CapacitancePf, kernel.PotassiumReversalMv, kernel.SodiumReversalMv, kernel.CalciumReversalMv,
                        kernel.PumpTurnoverChargePa, kernel.Fingerprint }, Json));
                    count++;
                }
            }
        }
        return count;
    }

    private static double At(Condition condition, double time) => condition.Changes.Last(c => c.TimeMs <= time).DisplacementNanometers;
    private static void WriteNew(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, value, Json);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
