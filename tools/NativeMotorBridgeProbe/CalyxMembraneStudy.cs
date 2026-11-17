using System.Text;
using System.Text.Json;
using NRE.SimAvatar;

internal static class CalyxMembraneStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal sealed record Variant(string Key, AvatarCalyxParameters Parameters);
    internal sealed record Protocol(string Version, string KernelVersion, AvatarCalyxParameters Parameters,
        Variant[] Variants, double[] VoltagesMv, double[] CleftPotentialsMv, double[] CleftPotassiumMm,
        double[] CleftSodiumMm, string[] GatePresets, double[][] ClampVoltagesMv,
        double[] ClampDurationsMs, double ClampSubdivisionMs);

    internal static void Run(string input, string output)
    {
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Frozen absolute input and new output required.");
        var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input), Json);
        if (spec is null || spec.Version != "calyx-membrane-comparison-v1" || spec.KernelVersion != AvatarCalyxKernel.Version ||
            spec.Parameters != AvatarCalyxParameters.DepositedReference || spec.Variants.Length != 6 ||
            !spec.GatePresets.SequenceEqual(new[] { "zero", "one", "mixed", "conditioned" }) || spec.ClampSubdivisionMs != .125)
            throw new ArgumentException("Unexpected calyx protocol.");
        Directory.CreateDirectory(output); WriteNew(Path.Combine(output, "input.json"), spec);
        int arithmetic = 0;
        using (var writer = Writer(Path.Combine(output, "arithmetic.jsonl")))
        foreach (var variant in spec.Variants)
        {
            var kernel = new AvatarCalyxKernel(variant.Parameters);
            foreach (double v in spec.VoltagesMv)
            foreach (double cleft in spec.CleftPotentialsMv)
            foreach (double potassium in spec.CleftPotassiumMm)
            foreach (double sodium in spec.CleftSodiumMm)
            foreach (string preset in spec.GatePresets)
            {
                var gates = Gates(preset, v, cleft);
                writer.WriteLine(JsonSerializer.Serialize(new { index = arithmetic++, variant = variant.Key,
                    voltageMv = v, cleftPotentialMv = cleft, cleftPotassiumMm = potassium, cleftSodiumMm = sodium,
                    preset, gates, kernel.Fingerprint, kernel.InnerCapacitancePf, kernel.OuterCapacitancePf,
                    evaluation = kernel.Evaluate(v, cleft, potassium, sodium, gates) }, Json));
            }
        }
        int clamps = 0;
        using (var writer = Writer(Path.Combine(output, "clamps.jsonl")))
        foreach (var voltages in spec.ClampVoltagesMv)
        foreach (string preset in spec.GatePresets)
        foreach (double elapsed in spec.ClampDurationsMs)
        {
            var gates = Gates(preset, voltages[0], voltages[1]); var split = gates;
            long count = checked((long)(elapsed / spec.ClampSubdivisionMs));
            if (count * spec.ClampSubdivisionMs != elapsed) throw new ArgumentException("Clamp subdivision differs.");
            for (long i = 0; i < count; i++)
                split = AvatarCalyxKernel.RelaxVoltageClamp(split, voltages[0], voltages[1], spec.ClampSubdivisionMs);
            writer.WriteLine(JsonSerializer.Serialize(new { index = clamps++, voltageMv = voltages[0], cleftPotentialMv = voltages[1],
                preset, elapsedMs = elapsed, initial = gates, actual = AvatarCalyxKernel.RelaxVoltageClamp(gates, voltages[0], voltages[1], elapsed),
                subdivided = split, subdivisionSteps = count }, Json));
        }
        WriteNew(Path.Combine(output, "results.json"), new { completed = arithmetic == 15984 && clamps == 224,
            arithmeticRows = arithmetic, clampRows = clamps, freeMembraneIntegrated = false, cleftDynamicsIntegrated = false,
            afferentFiringQualified = false, liveAvatarSelected = false, physiologyQualified = false, avatarRecoveryAssessed = false });
        if (arithmetic != 15984 || clamps != 224) throw new InvalidOperationException("Calyx capture incomplete.");
        Console.WriteLine("15984 native membrane-current cases and 224 voltage-clamp records retained. No free membrane or afferent integration.");
    }

    private static AvatarCalyxGates Gates(string preset, double v, double cleft) => preset switch
    {
        "zero" => new(0,0,0,0), "one" => new(1,1,1,1), "mixed" => new(.2,.3,.4,.5),
        "conditioned" => AvatarCalyxKernel.VoltageConditionedGates(v,cleft),
        _ => throw new ArgumentException("Unknown gate fixture.")
    };
    private static StreamWriter Writer(string path) => new(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),
        new UTF8Encoding(false)) { AutoFlush = true };
    private static void WriteNew(string path, object value)
    {
        using var stream = new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(stream,value,Json);
    }
}
