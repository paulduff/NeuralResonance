#nullable enable
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

internal static class CanalBundleStudy
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    internal static void Run(string input,string output)
    {
        if(!Path.IsPathFullyQualified(input)||!Path.IsPathFullyQualified(output)||Directory.Exists(output)||File.Exists(output))
            throw new ArgumentException("Absolute frozen input and new output required.");
        using var document=JsonDocument.Parse(File.ReadAllText(input));var p=document.RootElement;
        if(p.GetProperty("version").GetString()!="retained-head-spatial-bundle-v1"||
            p.GetProperty("ownerVersion").GetString()!=AvatarClockedGradedCanal.Version||
            p.GetProperty("bridgeVersion").GetString()!=AvatarCanalBundleInput.Version||
            p.GetProperty("denseVersion").GetString()!=AvatarCanalDensePoint.Version||
            p.GetProperty("bindingVersion").GetString()!=AvatarCupulaMechanicalBinding.Version||
            p.GetProperty("scheduleVersion").GetString()!=AvatarCanalStepSchedule.Version||
            p.GetProperty("baseTicksPerInterval").GetInt32()!=65536||
            !p.GetProperty("baseWidthsTicks").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(AvatarCanalStepSchedule.BoundaryRefined(1).StepWidthsTicks)||
            !p.GetProperty("refinements").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(new[]{1,2,4})||
            !p.GetProperty("initial").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(new double[12]))
            throw new ArgumentException("Unexpected clock protocol.");
        var frame=p.GetProperty("frame").Deserialize<CanalSkullStudy.Protocol>(Json)??throw new ArgumentException("Missing head input.");
        if(frame.ExpectedEndpoints!=201||frame.Records.Length!=603||frame.Clock.InitialPhysicsStep!=0)throw new ArgumentException("Incomplete head input.");
        var selected=frame.Records.Where(r=>r.SensorId=="centre").ToArray();
        if(selected.Length!=201||selected.Where((r,i)=>r.Receipt.PhysicsStep!=i).Any())throw new ArgumentException("Head order changed.");
        Directory.CreateDirectory(output);Write(Path.Combine(output,"input.json"),p);
        var parameters=AvatarCoupledCanalParameters.PublishedRightLabyrinth1();var basis=AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1();
        Write(Path.Combine(output,"parameters.json"),parameters);Write(Path.Combine(output,"basis.json"),basis);
        var g=p.GetProperty("geometry");var a=p.GetProperty("attachment");
        var field=AvatarCupulaMechanicalBinding.EquivalentCircularField(parameters,g.GetProperty("organId").GetString()!,
            (AvatarCupulaCanal)g.GetProperty("canal").GetInt32(),g.GetProperty("source").GetString()!,g.GetProperty("sourceSha256").GetString()!,
            g.GetProperty("shearModulusPascal").GetDouble(),g.GetProperty("thicknessOverRadius").GetDouble());
        var attachment=new AvatarCupulaBundleAttachment(a.GetProperty("hairCellId").GetGuid(),field.OrganId,field.Canal,
            a.GetProperty("source").GetString()!,a.GetProperty("rootXOverRadius").GetDouble(),a.GetProperty("bundleLengthMeters").GetDouble(),a.GetProperty("excitatoryPolarity").GetInt32());
        Write(Path.Combine(output,"geometry.json"),new {field,attachment,stiffness=AvatarCupulaMechanicalBinding.Match(parameters,field)});
        var reports=new List<object>();bool complete=true;
        foreach(int n in new[]{1,2,4})
        {
            string folder=Path.Combine(output,"r"+n);Directory.CreateDirectory(folder);
            var schedule=AvatarCanalStepSchedule.BoundaryRefined(n);
            var first=selected[0].Receipt.RawObservationJson;var raw=JsonSerializer.Deserialize<AvatarSkullObservation>(first,Json)!;
            var owner=new AvatarCanalBundleInput(frame.Clock,raw.HeadBodyId,raw.SensorId,raw.LocalPointMeters,basis,parameters,new(default,default),schedule,field,attachment);
            var query=p.GetProperty("queryReferenceTicks").GetProperty(n.ToString()).EnumerateArray().Select(v=>v.GetInt64()).ToArray();
            if(query.Length==0||query[^1]!=262144||query.Any(t=>t<=0||t>262144||t%(4/n)!=0)||!query.SequenceEqual(query.Distinct().Order()))
                throw new ArgumentException("Invalid declared integer query grid.");
            Write(Path.Combine(folder,"initial.json"),new {refinement=n,schedule,owner.Fingerprint,clock=frame.Clock,initial=owner.Capture()});
            int intervals=0,receipts=0;long steps=0,denseSamples=0;string? error=null;
            Console.WriteLine("CANAL TO BUNDLE r"+n+"; 201 original endpoints, 199 integrated intervals");
            try
            {
                using var stream=new FileStream(Path.Combine(folder,"trajectory.csv.gz"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                using var gzip=new GZipStream(stream,CompressionLevel.Fastest);
                using var writer=new StreamWriter(gzip,new UTF8Encoding(false));
                writer.WriteLine("index,physicalMs,neuralMs,intervalStep,"+string.Join(',',Enumerable.Range(0,12).Select(i=>"state"+i))+","+
                    string.Join(',',Enumerable.Range(0,18).Select(i=>"stage1_"+i))+","+string.Join(',',Enumerable.Range(0,18).Select(i=>"stage2_"+i))+
                    ",energyBefore,energyAfter,inputWork,physicalDamping,numericalDamping,energyResidual,backwardError");
                using var denseStream=new FileStream(Path.Combine(folder,"bundle.csv.gz"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                using var denseGzip=new GZipStream(denseStream,CompressionLevel.Fastest);
                using var dense=new StreamWriter(denseGzip,new UTF8Encoding(false));
                dense.WriteLine("index,physicalMs,neuralMs,intervalStep,"+string.Join(',',Enumerable.Range(0,12).Select(i=>"state"+i))+
                    ",solidVolume,solidFlow,meanDisplacement,shape,rootShear,bundleNm,bundleVelocityNmPerSecond");
                foreach(var row in selected)
                {
                    var observation=JsonSerializer.Deserialize<AvatarSkullObservation>(row.Receipt.RawObservationJson,Json)!;
                    if(PhysicalSampleProtocol.Digest(row.Receipt.RawObservationJson)!=row.Receipt.RawObservationSha256)throw new ArgumentException("Original bytes changed.");
                    var bridge=owner.Consume(observation);var receipt=bridge.Mechanics;receipts++;
                    if(!ReferenceEquals(bridge,owner.Consume(observation))||JsonSerializer.Serialize(receipt.Input.Skull,Json)!=JsonSerializer.Serialize(row.Receipt,Json))
                        throw new InvalidOperationException("Original input replay or latest retry changed.");
                    var interval=receipt.CompletedInterval;
                    if(interval is null)
                    {
                        if(observation.PhysicsStep>1||receipt.Mechanics.IntegratedSubsteps!=0)throw new InvalidOperationException("Missing interval.");
                    }
                    else
                    {
                        if(interval.Substeps.Count!=schedule.Count||interval.HeldInput.Skull.PhysicsStep!=observation.PhysicsStep-1)throw new InvalidOperationException("Causal substep coverage changed.");
                        foreach(var s in interval.Substeps)
                        {
                            var t=s.Transition;
                            double[] values=[..t.After.VolumeMeters3.ToArray(),..t.After.FlowMeters3PerSecond.ToArray(),
                                ..t.FirstStage.Point.VolumeMeters3.ToArray(),..t.FirstStage.Point.FlowMeters3PerSecond.ToArray(),..t.FirstStage.AccelerationMeters3PerSecond2.ToArray(),
                                ..t.LastStage.Point.VolumeMeters3.ToArray(),..t.LastStage.Point.FlowMeters3PerSecond.ToArray(),..t.LastStage.AccelerationMeters3PerSecond2.ToArray(),
                                t.EnergyBeforeJoules,t.EnergyAfterJoules,t.InputWorkJoules,t.PhysicalDampingJoules,t.NumericalDampingJoules,t.EnergyIdentityResidualJoules,t.MaximumStageBackwardError];
                            if(!values.All(double.IsFinite))throw new ArithmeticException("Nonfinite retained mechanics.");
                            writer.WriteLine(string.Join(',',new[]{s.ClockSubstepIndex.ToString(),s.PhysicalTimeMs.ToString("R",CultureInfo.InvariantCulture),
                                s.NeuralTimeMs.ToString("R",CultureInfo.InvariantCulture),observation.PhysicsStep.ToString()}.Concat(values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)))));
                            steps++;
                        }
                        var trace=bridge.CompletedTrace??throw new InvalidOperationException("Missing spatial trace.");
                        if(trace.MechanicalIntervalFingerprint!=interval.Fingerprint||trace.Knots.Count!=schedule.Count+1)
                            throw new InvalidOperationException("Lost accepted knots.");
                        foreach(long local in query)
                        {
                            long tick=checked(trace.FirstTick+local/(4/n));var sample=trace.AtTick(tick);var b=sample.Bundle;
                            double[] values=[..sample.Mechanics.VolumeMeters3.ToArray(),..sample.Mechanics.FlowMeters3PerSecond.ToArray(),
                                b.SolidVolumeCubicMeters,b.SolidFlowCubicMetersPerSecond,b.MeanCupulaDisplacementMeters,b.TipShapeFactor,
                                b.RootShearGradientY,b.BundleDisplacementNanometers,b.BundleVelocityNanometersPerSecond];
                            if(!values.All(double.IsFinite))throw new ArithmeticException("Nonfinite spatial output.");
                            dense.WriteLine(string.Join(',',new[]{tick.ToString(),sample.PhysicalTimeMs.ToString("R",CultureInfo.InvariantCulture),
                                sample.NeuralTimeMs.ToString("R",CultureInfo.InvariantCulture),observation.PhysicsStep.ToString()}.Concat(values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)))));
                            denseSamples++;
                        }
                        Write(Path.Combine(folder,"trace-"+observation.PhysicsStep.ToString("D3")+".json"),new {trace.Fingerprint,trace.MechanicalIntervalFingerprint,
                            trace.FirstTick,trace.LastTick,trace.TicksPerPhysicalInterval,knots=trace.Knots.Count,queryCount=query.Length,
                            initial=trace.Knots[0],final=trace.Knots[^1]});
                        intervals++;owner.AcknowledgeTrace(trace.Fingerprint);owner.AcknowledgeTrace(trace.Fingerprint);
                        if(owner.Capture().PendingTrace is not null||owner.Capture().Mechanics.PendingInterval is not null)throw new InvalidOperationException("Acknowledged handoff retained.");
                    }
                    Write(Path.Combine(folder,"endpoint-"+observation.PhysicsStep.ToString("D3")+".json"),new {
                        row.SourceFile,row.SourceSha256,input=receipt.Input,mechanics=receipt.Mechanics,
                        completedInterval=interval is null?null:new {interval.Fingerprint,heldStep=interval.HeldInput.Skull.PhysicsStep,
                            closingStep=interval.ClosingInput.Skull.PhysicsStep,substeps=interval.Substeps.Count},acknowledged=interval is not null});
                }
            }
            catch(Exception exception){error=exception.ToString();complete=false;Write(Path.Combine(folder,"failure.json"),new {error,state=owner.Capture()});Console.WriteLine(error);}
            var state=owner.Capture();bool passed=error is null&&state.Failure is null&&!state.Mechanics.Faulted&&state.PendingTrace is null&&
                state.Mechanics.PendingInterval is null&&receipts==201&&intervals==199&&steps==199L*schedule.Count&&denseSamples==199L*query.Length;
            complete &= passed;
            var report=new {refinement=n,completed=passed,receipts,intervals,steps,denseSamples,state,error};Write(Path.Combine(folder,"final.json"),report);reports.Add(report);
        }
        Write(Path.Combine(output,"results.json"),new {completed=complete,runs=reports,newNativePhysicsExecuted=false,denseNumericalQualificationPending=true,connectedSensoryDynamicsExecuted=false,
            anatomicalCalibrationQualified=false,afferentFiringQualified=false,avatarBalanceAssessed=false});
        if(!complete)throw new InvalidOperationException("Clock capture incomplete; all attempted prefixes preserved.");
    }
    private static void Write(string path,object value)
    {using var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);JsonSerializer.Serialize(f,value,Json);}
}
