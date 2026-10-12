using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

var directory=Path.GetFullPath(args[0]);
bool projectSensorFix=args.Contains("--project-sensor-fix");
var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
int count=0,invalid=0,pressureMismatch=0,missingMuscles=0;
float maximumRelativeTrunk=0;
var errors=new List<string>();
foreach(var path in Directory.EnumerateFiles(directory,"*-trace.jsonl")) {
    var transducer=new PhysicalBodyTransducerRuntime();
    foreach(string line in File.ReadLines(path)) {
        count++;using var document=JsonDocument.Parse(line);
        var request=document.RootElement.GetProperty("body").Deserialize<PhysicalBodyFrameRequest>(options)!;
        if(projectSensorFix){var projected=request.Articulation!;request=request with {Articulation=projected with {
            LeftHandLoadNewtons=Nre.UnityWorld.BodySensorMath.HandReceptorLoad(projected.LeftHandLoadNewtons),
            RightHandLoadNewtons=Nre.UnityWorld.BodySensorMath.HandReceptorLoad(projected.RightHandLoadNewtons),
            Musculoskeletal=projected.Musculoskeletal! with {BalanceError=Nre.UnityWorld.BodySensorMath.NormalizedSupportError(projected.Musculoskeletal!.Balance!.SupportMarginMeters)}}};}
        if(!PhysicalBodyFrameDescriptor.TryCreate(request,out var descriptor,out var error)) {
            invalid++;errors.Add(Path.GetFileName(path)+" at "+request.Sequence+": "+error);continue;
        }
        var a=descriptor!.Articulation;
        maximumRelativeTrunk=Math.Max(maximumRelativeTrunk,Math.Max(Math.Abs(a.TrunkPitchRadians),Math.Abs(a.TrunkRollRadians)));
        if(a.Musculoskeletal!.Muscles.Count!=38)missingMuscles++;
        foreach(var foot in new[]{(a.LeftFootPressure,a.LeftFootLoadNewtons),(a.RightFootPressure,a.RightFootLoadNewtons)}) {
            var p=foot.Item1;
            if(p is null){pressureMismatch++;continue;}
            float total=p.HeelMedialLoadNewtons+p.HeelLateralLoadNewtons+p.ForefootMedialLoadNewtons+p.ForefootLateralLoadNewtons;
            if(total>0&&Math.Abs(total-foot.Item2)>.01f)pressureMismatch++;
        }
        transducer.Transduce(descriptor,count,request.TimestampMs);
    }
}
var result=new {samples=count,invalid,pressureMismatch,missingMuscles,maximumRelativeTrunk,errors,sensorFixProjected=projectSensorFix,liveDeliveryAssessed=false};
string output=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});
File.WriteAllText(Path.Combine(directory,projectSensorFix?"offline-body-projection-validation.json":"offline-body-validation.json"),output);
Console.WriteLine(output);
return invalid==0&&pressureMismatch==0&&missingMuscles==0&&count>0?0:1;
