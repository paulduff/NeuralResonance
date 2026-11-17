using System.Text;
using System.Text.Json;
using NRE.SimAvatar;

internal static class CleftOpenStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal sealed record OpenCase(int Index, CleftCouplingStudy.SpatialCase Spatial,
        AvatarCleftPerilymph Reservoir, AvatarCleftApexFace[] Faces);
    internal sealed record Protocol(string Version, string OpenVersion, string SpatialVersion,
        OpenCase[] Cases, int ExpectedCases, int ExpectedClosedCases);

    internal static void Run(string closedInput, string input, string output)
    {
        if (!Path.IsPathFullyQualified(closedInput) || !Path.IsPathFullyQualified(input) ||
            !Path.IsPathFullyQualified(output) || Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Frozen absolute inputs and NEW output required.");
        var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input),Json);
        if (spec is null || spec.Version != "cleft-open-comparison-v1" || spec.OpenVersion != AvatarCleftOpenCoupling.Version ||
            spec.SpatialVersion != AvatarCleftSpatialCoupling.Version || spec.ExpectedCases != 192 || spec.ExpectedClosedCases != 624 || spec.Cases.Length != 192)
            throw new ArgumentException("Unexpected open-cleft protocol.");
        Directory.CreateDirectory(output); WriteNew(Path.Combine(output,"input.json"),spec);
        // The preserved 468/60/96 input set is reevaluated for exact closed
        // regression; its original source/output files remain read-only.
        CleftCouplingStudy.Run(closedInput,Path.Combine(output,"closed"));
        using (var stream = new FileStream(Path.Combine(output,"open.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))
        using (var writer = new StreamWriter(stream,new UTF8Encoding(false)) { AutoFlush = true })
        foreach (var row in spec.Cases)
        {
            var s = row.Spatial;
            var spatial = new AvatarCleftSpatialCoupling(s.HairCellId,s.HairApicalPf,s.HairPerilymphPf,s.Material,s.Compartments,s.Connections);
            var kernel = new AvatarCleftOpenCoupling(spatial,row.Reservoir,row.Faces);
            writer.WriteLine(JsonSerializer.Serialize(new { input = row, kernel.Fingerprint, spatialFingerprint = spatial.Fingerprint,
                evaluation = kernel.Evaluate(s.HairMv,s.EndolymphMv,s.PerilymphMv,s.HairToEndolymphPa,s.HairToPerilymphPa,s.States,s.MembranePorts) },Json));
        }
        WriteNew(Path.Combine(output,"results.json"),new { completed = true, closedRows = 624, openRows = 192,
            timeIntegrated = false, spatialGeometryCalibrated = false, physiologyQualified = false, avatarRecoveryAssessed = false });
        Console.WriteLine("624 closed regression and 192 open-boundary evaluations retained. Independent agreement remains a separate check.");
    }

    private static void WriteNew(string path, object value)
    {
        using var stream = new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(stream,value,Json);
    }
}
