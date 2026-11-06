using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class ActiveStudy
{
    internal sealed record Segment(double UntilMs, double SomaCurrentPa, double DendriteExcitatoryNs, double DendriteInhibitoryNs);
    internal sealed record Case(string Key, string Block, Segment[] Segments);
    internal sealed record Protocol(string Version, double DurationMs, double SampleMs, double InitialVoltageMv,
        double[] StepsMs, double ThresholdMv, string SourceRevision, string Cell, Case[] Cases);

    public static void Run(string input, string output)
    {
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output))
            throw new ArgumentException("Supply an absolute input file and new absolute output directory.");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        byte[] raw = File.ReadAllBytes(input);
        var spec = JsonSerializer.Deserialize<Protocol>(raw, json) ?? throw new ArgumentException("Missing active protocol.");
        if (spec.Version != "motoneuron-active-comparison-v1" || spec.DurationMs != 6000 || spec.SampleMs != .1 ||
            spec.InitialVoltageMv != -65 || spec.ThresholdMv != MotoneuronReferenceCell.SpikeThresholdMv ||
            spec.SourceRevision != "0d11354490fad5fcd940d016a8d69b000693fd81" || spec.Cell != "FivecomptcMG_0" ||
            spec.StepsMs is null || !spec.StepsMs.SequenceEqual(new[] { .0125, .00625, .003125 }) ||
            spec.Cases is null || spec.Cases.Length != 10 || spec.Cases.Select(x => x.Key).Distinct().Count() != 10)
            throw new ArgumentException("Unexpected isolated active protocol.");
        foreach (var scenario in spec.Cases)
        {
            if (!Regex.IsMatch(scenario.Key, "^[a-z0-9-]+$") || scenario.Segments is null || scenario.Segments.Length == 0 ||
                !Enum.TryParse<MotoneuronDiagnosticBlock>(scenario.Block, out var block) ||
                !Enum.IsDefined(block) || block == MotoneuronDiagnosticBlock.Passive)
                throw new ArgumentException("Invalid isolated case.");
            double previous = 0;
            foreach (var s in scenario.Segments)
            {
                if (!double.IsFinite(s.UntilMs) || s.UntilMs <= previous || s.UntilMs > spec.DurationMs ||
                    Math.Abs(s.UntilMs / spec.SampleMs - Math.Round(s.UntilMs / spec.SampleMs)) > 1e-8 ||
                    !double.IsFinite(s.SomaCurrentPa) || !double.IsFinite(s.DendriteExcitatoryNs) || s.DendriteExcitatoryNs < 0 ||
                    !double.IsFinite(s.DendriteInhibitoryNs) || s.DendriteInhibitoryNs < 0)
                    throw new ArgumentException("Invalid piecewise constant physical input.");
                previous = s.UntilMs;
            }
            if (previous != spec.DurationMs) throw new ArgumentException("Incomplete time domain.");
        }
        Directory.CreateDirectory(output);
        void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json));
        var parameters = MotoneuronReferenceParameters.CatLowThreshold;
        var geometry = new MotoneuronReferenceCell(parameters);
        Save("protocol.json", new { inputSha256 = Convert.ToHexString(SHA256.HashData(raw)), specification = spec, parameters,
            version = MotoneuronReferenceCell.Version, areasUm2 = geometry.CopyAreasUm2(), capacitancesPf = geometry.CopyCapacitancesPf(),
            couplingNs = geometry.CopyCouplingNs(), nativeNeuronExecuted = false, liveBrainAccessed = false });
        var cases = new List<object>(); int successful = 0;
        foreach (var scenario in spec.Cases)
        foreach (double step in spec.StepsMs)
        {
            string key = scenario.Key + "-h" + step.ToString(CultureInfo.InvariantCulture);
            Console.WriteLine(key);
            var cell = new MotoneuronReferenceCell(parameters, spec.InitialVoltageMv, Enum.Parse<MotoneuronDiagnosticBlock>(scenario.Block));
            try
            {
                using var trace = new StreamWriter(Path.Combine(output, key + ".csv"));
                trace.WriteLine("timeMs," + string.Join(',', Enumerable.Range(0, 30).Select(i => "state" + i)));
                void Sample() => trace.WriteLine(string.Join(',', new[] { cell.TimeMs }.Concat(cell.CopyState()).Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
                int segment = 0; Sample();
                for (int sample = 1; sample <= 60000; ++sample)
                {
                    double until = sample * spec.SampleMs;
                    while (cell.TimeMs < until)
                    {
                        while (segment + 1 < scenario.Segments.Length && cell.TimeMs >= scenario.Segments[segment].UntilMs) ++segment;
                        var command = scenario.Segments[segment];
                        double dt = Math.Min(step, Math.Min(until, command.UntilMs) - cell.TimeMs);
                        cell.Advance(dt, command.SomaCurrentPa, command.DendriteExcitatoryNs, command.DendriteInhibitoryNs);
                    }
                    Sample();
                }
                cases.Add(new { key, scenario = scenario.Key, stepMs = step, completed = true, cell.TimeMs,
                    cell.MaximumCalciumLedgerErrorMm, cell.MaximumChargeLedgerErrorFc, crossingsMs = cell.CopyCrossings() });
                ++successful;
            }
            catch (Exception error)
            {
                // A failed coarse condition must not suppress finer evidence.
                // Preserve its partial trace and fault; never reset/retry the cell.
                cases.Add(new { key, scenario = scenario.Key, stepMs = step, completed = false,
                    cell.TimeMs, error = error.ToString(), crossingsMs = cell.CopyCrossings() });
                Console.WriteLine("CONDITION FAILED: " + error.Message);
            }
            Save("results.json", new { completed = false, successfulCases = successful, cases });
        }
        Save("results.json", new { completed = true, successfulCases = successful, expectedCases = 30, cases,
            numericalCriteriaAssessed = false, activeMotorSourceQualified = false, avatarRecoveryAssessed = false });
    }
}
