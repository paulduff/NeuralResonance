using System.Text;
using System.Text.Json;
using NRE.SimAvatar;

internal static class CleftMembraneStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal sealed record Topology(string HairCellId, double HairApicalPf, double HairPerilymphPf,
        AvatarCleftMaterial Material, AvatarCleftCompartment[] Compartments, AvatarCleftConnection[] Connections,
        AvatarCleftPerilymph Reservoir, AvatarCleftApexFace[] Faces);
    internal sealed record Case(int Index, string Key, AvatarHairCellParameters HairParameters,
        AvatarCalyxParameters CalyxParameters, Topology Topology, double HairMv, double PerilymphMv,
        double BundleDisplacementNanometers, AvatarCleftCompartmentState[] States, AvatarCleftLocalMembraneInput[] Inputs);
    internal sealed record Protocol(string Version, string KernelVersion, Case[] Cases,
        int ExpectedCases, int ExpectedClosedRows, int ExpectedOpenRows);

    internal static void Run(string closedInput, string openInput, string input, string output)
    {
        if (new[] { closedInput, openInput, input, output }.Any(p => !Path.IsPathFullyQualified(p)) ||
            Directory.Exists(output) || File.Exists(output)) throw new ArgumentException("Frozen absolute inputs and NEW output required.");
        var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input),Json);
        if (spec is null || spec.Version != "coupled-cleft-membrane-derivative-comparison-v1" ||
            spec.KernelVersion != AvatarVestibularCleftMembranes.Version || spec.ExpectedCases != 216 ||
            spec.ExpectedClosedRows != 624 || spec.ExpectedOpenRows != 192 || spec.Cases.Length != 216)
            throw new ArgumentException("Unexpected coupled membrane protocol.");
        Directory.CreateDirectory(output); WriteNew(Path.Combine(output,"input.json"),spec);
        CleftOpenStudy.Run(closedInput,openInput,Path.Combine(output,"regression"));
        using (var stream = new FileStream(Path.Combine(output,"membranes.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))
        using (var writer = new StreamWriter(stream,new UTF8Encoding(false)) { AutoFlush = true })
        foreach (var row in spec.Cases)
        {
            var t = row.Topology;
            var spatial = new AvatarCleftSpatialCoupling(t.HairCellId,t.HairApicalPf,t.HairPerilymphPf,t.Material,t.Compartments,t.Connections);
            var opening = new AvatarCleftOpenCoupling(spatial,t.Reservoir,t.Faces);
            var kernel = new AvatarVestibularCleftMembranes(opening,row.HairParameters,row.CalyxParameters);
            writer.WriteLine(JsonSerializer.Serialize(new { input = row, kernel.Fingerprint,
                evaluation = kernel.Evaluate(row.HairMv,row.PerilymphMv,row.BundleDisplacementNanometers,row.States,row.Inputs) },Json));
        }
        WriteNew(Path.Combine(output,"results.json"),new { completed = true, closedRows = 624, openRows = 192, membraneRows = 216,
            timeIntegrated = false, spatialGeometryCalibrated = false, afferentFiringQualified = false,
            physiologyQualified = false, avatarRecoveryAssessed = false });
        Console.WriteLine("624 closed and 192 open regression records plus 216 coupled membrane derivatives retained; no time integration or sensory firing.");
    }

    private static void WriteNew(string path, object value)
    {
        using var stream = new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(stream,value,Json);
    }
}
