using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

var directory=Path.GetFullPath(args[0]);
bool projectSensorFix=args.Contains("--project-sensor-fix");
var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
int count=0,invalid=0,pressureMismatch=0,missingMuscles=0;
int motorUnitSamples=0,invalidMotorUnitStates=0;
double maximumMotorUnitConservationError=0;
float maximumRelativeTrunk=0;
var errors=new List<string>();
var expectedPoolTraces=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var expectedExcitationTraces=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
string reportPath=Path.Combine(directory,"balance-report.json");
if(File.Exists(reportPath)){
    using var report=JsonDocument.Parse(File.ReadAllText(reportPath));
    foreach(var c in report.RootElement.GetProperty("cases").EnumerateArray()){
        if(c.TryGetProperty("fatiguePolicy",out var policy)&&policy.GetInt32() is 3 or 4 or 5 or 6 or 7)
            expectedPoolTraces.Add(c.GetProperty("name").GetString()+"-trace.jsonl");
        if(c.TryGetProperty("canonicalActivationTiming",out var timing)&&timing.GetBoolean())
            expectedExcitationTraces.Add(c.GetProperty("name").GetString()+"-trace.jsonl");
    }
}
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
        bool hasPools=document.RootElement.TryGetProperty("motorUnitPools",out var pools)&&pools.ValueKind==JsonValueKind.Array;
        if(expectedPoolTraces.Contains(Path.GetFileName(path))&&(!hasPools||pools.GetArrayLength()==0)){
            invalidMotorUnitStates++;errors.Add(Path.GetFileName(path)+" at "+request.Sequence+": missing motor-unit pools");
        }
        if(hasPools&&pools.GetArrayLength()>0){
            motorUnitSamples++;
            if(pools.GetArrayLength()!=38)invalidMotorUnitStates++;
            var seenPools=new HashSet<(string,string)>();
            foreach(var pool in pools.EnumerateArray()){
                string name=pool.GetProperty("name").GetString()!,side=pool.GetProperty("side").GetString()!;
                double resting=pool.GetProperty("restingFraction").GetDouble(),active=pool.GetProperty("activeFraction").GetDouble(),
                    fatigued=pool.GetProperty("fatiguedFraction").GetDouble(),requested=pool.GetProperty("requestedActivation").GetDouble();
                double conservation=Math.Abs(resting+active+fatigued-1);
                maximumMotorUnitConservationError=Math.Max(maximumMotorUnitConservationError,conservation);
                var fiber=a.Musculoskeletal!.Muscles.SingleOrDefault(m=>m.Name==name&&m.Side==side);
                bool hasExcitation=pool.TryGetProperty("excitation",out var excitation);
                if((expectedExcitationTraces.Contains(Path.GetFileName(path))&&!hasExcitation)||
                    (hasExcitation&&(!excitation.TryGetDouble(out var raw)||!double.IsFinite(raw)||raw<0||raw>1)))
                    invalidMotorUnitStates++;
                if(!seenPools.Add((name,side))||fiber is null||conservation>1e-6||
                    new[]{resting,active,fatigued,requested}.Any(v=>!double.IsFinite(v)||v<0||v>1)||
                    Math.Abs(fiber.Activation-active)>1e-6||Math.Abs(fiber.FatigueFraction-fatigued)>1e-6)
                    invalidMotorUnitStates++;
            }
        }
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
var result=new {samples=count,invalid,pressureMismatch,missingMuscles,maximumRelativeTrunk,
    motorUnitSamples,invalidMotorUnitStates,maximumMotorUnitConservationError,errors,sensorFixProjected=projectSensorFix,liveDeliveryAssessed=false};
string output=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});
File.WriteAllText(Path.Combine(directory,projectSensorFix?"offline-body-projection-validation.json":"offline-body-validation.json"),output);
Console.WriteLine(output);
return invalid==0&&pressureMismatch==0&&missingMuscles==0&&invalidMotorUnitStates==0&&count>0?0:1;
