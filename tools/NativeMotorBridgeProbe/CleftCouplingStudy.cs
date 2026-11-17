using System.Text;
using System.Text.Json;
using NRE.SimAvatar;

internal static class CleftCouplingStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal sealed record EdgeCase(int Index, AvatarCleftMaterial Material, AvatarCleftEdgeGeometry Geometry,
        AvatarCleftSite Left, AvatarCleftSite Right);
    internal sealed record ChargeCase(int Index, AvatarCleftCapacitances Capacitances,
        AvatarCleftPortCurrents Ports, AvatarCleftPotentials Potentials);
    internal sealed record Protocol(string Version, string EdgeVersion, string ChargeVersion,
        EdgeCase[] EdgeCases, ChargeCase[] ChargeCases, int ExpectedEdges, int ExpectedCharges);

    internal static void Run(string input, string output)
    {
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Frozen absolute input and NEW output required.");
        var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input), Json);
        if (spec is null || spec.Version != "cleft-constitutive-comparison-v1" || spec.EdgeVersion != AvatarCleftEdgeKernel.Version ||
            spec.ChargeVersion != AvatarCleftChargeBalance.Version || spec.ExpectedEdges != 468 || spec.ExpectedCharges != 60 ||
            spec.EdgeCases.Length != 468 || spec.ChargeCases.Length != 60)
            throw new ArgumentException("Unexpected cleft constitutive protocol.");
        Directory.CreateDirectory(output); WriteNew(Path.Combine(output,"input.json"),spec);
        using (var writer = Writer(Path.Combine(output,"edges.jsonl")))
        foreach (var row in spec.EdgeCases)
        {
            var kernel = new AvatarCleftEdgeKernel(row.Material,row.Geometry);
            writer.WriteLine(JsonSerializer.Serialize(new { input = row, kernel.Fingerprint,
                evaluation = kernel.Evaluate(row.Left,row.Right) },Json));
        }
        using (var writer = Writer(Path.Combine(output,"charges.jsonl")))
        foreach (var row in spec.ChargeCases)
        {
            var kernel = new AvatarCleftChargeBalance(row.Capacitances);
            var rates = kernel.Solve(row.Ports);
            writer.WriteLine(JsonSerializer.Serialize(new { input = row, kernel.Fingerprint, evaluation = new {
                rates, chargeRates = kernel.ChargeRates(rates), charges = kernel.Charges(row.Potentials),
                storedEnergyAttojoules = kernel.StoredEnergyAttojoules(row.Potentials),
                storedEnergyRateFw = kernel.StoredEnergyRateFw(row.Potentials,rates) } },Json));
        }
        WriteNew(Path.Combine(output,"results.json"),new { completed = true, edgeRows = 468, chargeRows = 60,
            timeIntegrated = false, spatialGeometryCalibrated = false, physiologyQualified = false, avatarRecoveryAssessed = false });
        Console.WriteLine("468 constitutive edge cases and 60 capacitance cases retained; no time integration or sensory firing.");
    }

    private static StreamWriter Writer(string path) => new(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),
        new UTF8Encoding(false)) { AutoFlush = true };
    private static void WriteNew(string path, object value)
    {
        using var stream = new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(stream,value,Json);
    }
}
