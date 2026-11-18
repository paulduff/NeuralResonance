using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

internal static class CanalSkullStudy
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    internal sealed record Row(string SourceFile,string SourceSha256,string SensorId,string AdapterFingerprint,AvatarSkullKinematicReceipt Receipt);
    internal sealed record Protocol(string Version,PhysicalSampleClock Clock,int ExpectedEndpoints,Row[] Records);

    internal static void Run(string input,string output)
    {
        if(!Path.IsPathFullyQualified(input)||!Path.IsPathFullyQualified(output)||Directory.Exists(output)||File.Exists(output))
            throw new ArgumentException("Absolute frozen input and new output required.");
        var p=JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input),Json)??throw new ArgumentException("Missing frame input.");
        if(p.Version!="retained-native-skull-prime-input-v1"||p.ExpectedEndpoints!=201||p.Records.Length!=603||p.Clock.InitialPhysicsStep!=0)
            throw new ArgumentException("Incomplete original head record set.");
        p.Clock.Validate();Directory.CreateDirectory(output);Write(Path.Combine(output,"input.json"),p);
        var basis=AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1();Write(Path.Combine(output,"basis.json"),basis);
        var owners=new Dictionary<string,AvatarCanalSkullInput>();var samples=new List<object>();int projected=0;
        for(int i=0;i<p.Records.Length;i++)
        {
            var row=p.Records[i];long step=i/3;string sensor=new[]{"centre","left","right"}[i%3];
            if(row.SensorId!=sensor||row.Receipt.PhysicsStep!=step)throw new ArgumentException("Original step/sensor order differs.");
            var observation=JsonSerializer.Deserialize<AvatarSkullObservation>(row.Receipt.RawObservationJson,Json)
                ??throw new ArgumentException("Missing original observation.");
            if(observation.PhysicsStep!=step||observation.SensorId!=sensor||
                PhysicalSampleProtocol.Digest(row.Receipt.RawObservationJson)!=row.Receipt.RawObservationSha256)
                throw new ArgumentException("Original observation identity or bytes differ.");
            if(step==0)owners.Add(sensor,new(p.Clock,observation.HeadBodyId,sensor,observation.LocalPointMeters,basis));
            var owner=owners[sensor];var result=owner.Consume(observation);
            if(!ReferenceEquals(result,owner.Consume(observation)))throw new InvalidOperationException("Angular retry changed the receipt.");
            if(JsonSerializer.Serialize(result.Skull,Json)!=JsonSerializer.Serialize(row.Receipt,Json))
                throw new InvalidOperationException("Existing skull receipt replay differs.");
            if(result.Projection is not null)projected++;
            samples.Add(new {step,sensorId=sensor,row.SourceFile,row.SourceSha256,receipt=result});
            // Keep the accepted prefix if a later point is rejected.
            Write(Path.Combine(output,"sample-"+i.ToString("D6")+".json"),samples[^1]);
        }
        if(projected!=600||owners.Values.Any(o=>o.Faulted))throw new InvalidOperationException("Incomplete angular reconstruction.");
        Write(Path.Combine(output,"results.json"),new {completed=true,retainedSamples=603,angularSamples=600,
            originalSkullReceiptReplayExact=true,sourceDirectionsNormalized=false,mechanicsIntegrated=false,
            newNativePhysicsExecuted=false,anatomicalCalibrationQualified=false,afferentFiringQualified=false,avatarBalanceAssessed=false});
        Console.WriteLine("603 original head receipts replay exactly; 600 causal angular inputs retained. No new physics or organ integration.");
    }
    private static void Write(string path,object value)
    { using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(stream,value,Json); }
}
