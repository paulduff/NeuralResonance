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
    internal sealed record SpatialCase(int Index, string HairCellId, double HairApicalPf, double HairPerilymphPf,
        AvatarCleftMaterial Material, AvatarCleftCompartment[] Compartments, AvatarCleftConnection[] Connections,
        AvatarCleftCompartmentState[] States, AvatarCleftMembranePort[] MembranePorts,
        double HairMv, double EndolymphMv, double PerilymphMv, double HairToEndolymphPa, double HairToPerilymphPa);
    internal sealed record Protocol(string Version, string EdgeVersion, string ChargeVersion, string SpatialVersion,
        EdgeCase[] EdgeCases, ChargeCase[] ChargeCases, SpatialCase[] SpatialCases, int ExpectedEdges, int ExpectedCharges, int ExpectedSpatial);

    internal static void Run(string input, string output)
    {
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Frozen absolute input and NEW output required.");
        var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input), Json);
        if (spec is null || spec.Version != "cleft-constitutive-comparison-v2" || spec.EdgeVersion != AvatarCleftEdgeKernel.Version ||
            spec.ChargeVersion != AvatarCleftChargeBalance.Version || spec.ExpectedEdges != 468 || spec.ExpectedCharges != 60 ||
            spec.SpatialVersion != AvatarCleftSpatialCoupling.Version || spec.ExpectedSpatial != 96 ||
            spec.EdgeCases.Length != 468 || spec.ChargeCases.Length != 60 || spec.SpatialCases.Length != 96)
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
        using (var writer = Writer(Path.Combine(output,"spatial.jsonl")))
        foreach (var row in spec.SpatialCases)
        {
            var kernel = new AvatarCleftSpatialCoupling(row.HairCellId,row.HairApicalPf,row.HairPerilymphPf,
                row.Material,row.Compartments,row.Connections);
            writer.WriteLine(JsonSerializer.Serialize(new { input = row, kernel.Fingerprint, evaluation = kernel.Evaluate(
                row.HairMv,row.EndolymphMv,row.PerilymphMv,row.HairToEndolymphPa,row.HairToPerilymphPa,row.States,row.MembranePorts) },Json));
        }
        WriteNew(Path.Combine(output,"results.json"),new { completed = true, edgeRows = 468, chargeRows = 60, spatialRows = 96,
            timeIntegrated = false, spatialGeometryCalibrated = false, physiologyQualified = false, avatarRecoveryAssessed = false });
        Console.WriteLine("468 edge, 60 local capacitance and 96 spatial coupling cases retained; no time integration or sensory firing.");
    }

    private static StreamWriter Writer(string path) => new(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),
        new UTF8Encoding(false)) { AutoFlush = true };
    private static void WriteNew(string path, object value)
    {
        using var stream = new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(stream,value,Json);
    }
}
