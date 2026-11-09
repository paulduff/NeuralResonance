using System.Security.Cryptography;
using System.Text.Json;

internal static class PrecisionStudy
{
    internal sealed record Point(string Key, string Scenario, string Trace, int SampleIndex, double TimeMs,
        string Block, double CurrentPa, double ExcitationNs, double InhibitionNs, double[] State);
    internal sealed record Input(string Version, Point[] Points);

    public static void Run(string input, string output)
    {
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output))
            throw new ArgumentException("Require an absolute input and new output directory.");
        byte[] raw = File.ReadAllBytes(input);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var request = JsonSerializer.Deserialize<Input>(raw, json) ?? throw new ArgumentException("Missing precision input.");
        if (request.Version != "motoneuron-fixed-state-precision-v1" || request.Points is null ||
            request.Points.Length is < 1 or > 2000 || request.Points.Select(p => p.Key).Distinct().Count() != request.Points.Length)
            throw new ArgumentException("Invalid bounded fixed-state request.");
        var parameters = MotoneuronReferenceParameters.CatLowThreshold;
        var rows = new List<object>();
        foreach (var point in request.Points)
        {
            if (point.State is null || !Enum.TryParse<MotoneuronDiagnosticBlock>(point.Block, out var block) ||
                !Enum.IsDefined(block) || block == MotoneuronDiagnosticBlock.Passive ||
                point.SampleIndex is < 0 or > 60000 || point.TimeMs != point.SampleIndex * .1)
                throw new ArgumentException("Invalid point identity or clock.");
            var cell = new MotoneuronReferenceCell(parameters, block: block, accumulation: MotoneuronAccumulation.Compensated);
            var before = cell.CopyState();
            double[]? derivative = null;
            string? rejection = null;
            try { derivative = cell.EvaluateDerivativeForDiagnosis(point.State, point.CurrentPa, point.ExcitationNs, point.InhibitionNs); }
            catch (InvalidOperationException error) { rejection = error.Message; }
            if (!before.SequenceEqual(cell.CopyState()) || cell.TimeMs != 0 || cell.CopyCrossings().Length != 0 ||
                cell.MaximumChargeLedgerErrorFc != 0 || cell.MaximumCalciumLedgerErrorMm != 0)
                throw new InvalidOperationException("Fixed-state evaluation mutated the cell.");
            rows.Add(new { point.Key, derivative, rejection });
        }
        var geometry = new MotoneuronReferenceCell(parameters);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "derivatives.json"), JsonSerializer.Serialize(new {
            completed = true, inputSha256 = Convert.ToHexString(SHA256.HashData(raw)), parameters,
            areasUm2 = geometry.CopyAreasUm2(), capacitancesPf = geometry.CopyCapacitancesPf(), couplingNs = geometry.CopyCouplingNs(),
            constants = new { pi = Math.PI, reversalMv = MotoneuronChannelKinetics.SomaticCalciumReversalMv,
                restingCalciumMm = MotoneuronChannelKinetics.RestingCalciumMm,
                faraday = MotoneuronChannelKinetics.FaradayCoulombsPerMole, depthUm = MotoneuronChannelKinetics.CalciumShellDepthUm },
            points = rows, stateAdvanced = false, exactCheckpointReplay = false, numericalCriteriaPassed = false,
            physiologyQualified = false, avatarRecoveryAssessed = false }, json));
        Console.WriteLine($"Evaluated {rows.Count} fixed states without advancing or modifying a neuron.");
    }
}
