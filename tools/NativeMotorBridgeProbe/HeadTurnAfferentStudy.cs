#nullable enable
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

internal static class HeadTurnAfferentStudy
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions FailureJson=new(JsonSerializerDefaults.Web){NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
    internal static void Run(string input,string output)
    {
        if(!Path.IsPathFullyQualified(input)||!Path.IsPathFullyQualified(output)||Directory.Exists(output)||File.Exists(output))throw new ArgumentException("Frozen input and new output required.");
        using var document=JsonDocument.Parse(File.ReadAllText(input));var p=document.RootElement;var bundle=p.GetProperty("bundle");
        const AvatarCanalForcingPolicy forcingPolicy=AvatarCanalForcingPolicy.MidpointHeld;
        const int physicalIntervals=18;
        var conditions=new[]{"quiet","yaw-positive","yaw-negative"};
        var counts=new[]{16384,32768};
        if(p.GetProperty("version").GetString()!="prescribed-head-midpoint-afferent-v1"||
            p.GetProperty("forcingPolicy").GetString()!="MidpointHeld"||p.GetProperty("activationPolicy").GetString()!="EndpointHold"||
            p.GetProperty("physicalIntervals").GetInt32()!=physicalIntervals||p.GetProperty("expectedRuns").GetInt32()!=6||
            p.GetProperty("expectedSourceSteps").GetInt64()!=2654208||p.GetProperty("mechanicalRefinement").GetInt32()!=4||
            p.GetProperty("maximumPacketSteps").GetInt32()!=512||
            !p.GetProperty("sourceStepsPerInterval").EnumerateArray().Select(x=>x.GetInt32()).SequenceEqual(counts)||
            !p.GetProperty("conditions").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(conditions))
            throw new ArgumentException("Unexpected prospective head-turn protocol.");
        var motion=p.GetProperty("motion");
        if(motion.GetProperty("angleRadians").GetDouble()!=.1||motion.GetProperty("startOffsetSteps").GetInt32()!=2||
            motion.GetProperty("turnSteps").GetInt32()!=10||motion.GetProperty("physicsStepMs").GetDouble()!=10||
            motion.GetProperty("axis").GetString()!="Yaw"||motion.GetProperty("neuralEpochMs").GetDouble()!=1000)
            throw new ArgumentException("Freeze the diagnostic motion before execution; no parameter search.");
        var fixture=p.GetProperty("fixture").Deserialize<AfferentNumericalStudy.JointCase>(Json)!;
        var settings=p.GetProperty("nonlinearSettings").Deserialize<AfferentLoadedRestStudy.Settings>(Json)!;
        Directory.CreateDirectory(output);Write(Path.Combine(output,"input.json"),p);
        var reports=new List<object>();bool all=true;
        foreach(string condition in conditions)foreach(int count in counts)
        {
            string name=condition+"-n"+count;string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            AvatarCanalAfferentInput? owner=null;AvatarVestibularAfferentDynamics? cell=null;AvatarCanalBundleInput? mechanics=null;
            long steps=0,events=0;int packets=0,intervals=0;string? failure=null;
            Console.WriteLine("PRESCRIBED HEAD / SENSORY NERVE "+name+"; 18 physical intervals");
            try
            {
                var clock=new PhysicalSampleClock(p.GetProperty("sessions").GetProperty(condition).GetGuid(),
                    "prescribed-head","diagnostic-quintic-yaw",0,10,0,1000);
                var binding=new AvatarCanalAfferentClock(clock,262144,count);
                var zero=new AvatarSkullVector(0,0,0);
                var first=new AvatarSkullObservation(0,PhysicalSampleProtocol.Identity(clock,0),"head","centre",zero,
                    zero,new(1,0,0),new(0,1,0),new(0,0,1),zero,zero,zero,new(0,-9.80665,0));
                double angle=condition=="quiet"?0:condition=="yaw-positive"?.1:-.1;
                var turn=new PrescribedHeadTurn(clock,first,PrescribedHeadAxis.Yaw,angle,2,10);
                var samples=Enumerable.Range(0,physicalIntervals+2).Select(i=>turn.AtStep(i)).ToArray();
                var observations=samples.Select(s=>s.Observation).ToArray();
                Write(Path.Combine(folder,"head-input.json"),new {condition,clock,observations,samples,turn.Fingerprint,
                    prescribedDiagnosticInput=true,nativeHeadMeasurements=false});
                var parameters=AvatarCoupledCanalParameters.PublishedRightLabyrinth1();var g=bundle.GetProperty("geometry");var a=bundle.GetProperty("attachment");
                var field=AvatarCupulaMechanicalBinding.EquivalentCircularField(parameters,g.GetProperty("organId").GetString()!,
                    (AvatarCupulaCanal)g.GetProperty("canal").GetInt32(),g.GetProperty("source").GetString()!,g.GetProperty("sourceSha256").GetString()!,
                    g.GetProperty("shearModulusPascal").GetDouble(),g.GetProperty("thicknessOverRadius").GetDouble());
                var attachment=new AvatarCupulaBundleAttachment(a.GetProperty("hairCellId").GetGuid(),field.OrganId,field.Canal,
                    a.GetProperty("source").GetString()!,a.GetProperty("rootXOverRadius").GetDouble(),a.GetProperty("bundleLengthMeters").GetDouble(),a.GetProperty("excitatoryPolarity").GetInt32());
                mechanics=new(clock,first.HeadBodyId,first.SensorId,first.LocalPointMeters,AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1(),parameters,
                    new(default,default),AvatarCanalStepSchedule.BoundaryRefined(4),field,attachment);
                mechanics.Consume(observations[0]);mechanics.Consume(observations[1]);
                var f=fixture.Sensory;var t=f.Topology;
                if(f.Inputs.Any(x=>x.AxonOutgoingPa!=0))throw new ArgumentException("The actual cable owns all calyx loading.");
                var spatial=new AvatarCleftSpatialCoupling(t.HairCellId,t.HairApicalPf,t.HairPerilymphPf,t.Material,t.Compartments,t.Connections);
                var sensory=new AvatarVestibularCleftMembranes(new(spatial,t.Reservoir,t.Faces),f.HairParameters,f.CalyxParameters);
                var cable=new AvatarAfferentCable(fixture.CableTopology.Nodes,fixture.CableTopology.Edges,fixture.CableTopology.Terminals,AvatarSodiumActivationContinuation.EndpointHold);
                var kernel=new AvatarVestibularCleftAfferent(sensory,cable,fixture.Attachments);
                cell=new(clock.SessionId,attachment.HairCellId,kernel,new(binding.SourceStepMs,settings.VoltageAbsoluteToleranceMv,settings.IonAbsoluteToleranceMm,
                    settings.GateAbsoluteTolerance,settings.RelativeTolerance,settings.MaximumNewtonIterations,settings.MaximumLineSearchHalvings),
                    f.HairMv,f.PerilymphMv,f.States,f.Inputs.Select(x=>new AvatarCleftGateState(x.Id,x.HairGates,x.CalyxGates)).ToArray(),fixture.CableStates,binding.SourceEpochMs);
                owner=new(mechanics,cell,new("isolated-canal-afferent",attachment.HairCellId.ToString("D"),p.GetProperty("observationNode").GetString()!,p.GetProperty("thresholdMv").GetDouble()),binding,512,forcingPolicy);
                Write(Path.Combine(folder,"sampling.json"),new {policy=owner.ForcingPolicy.ToString(),
                    version=AvatarCanalAfferentInput.MidpointVersion,ownerFingerprint=owner.Fingerprint,
                    mechanicalTicksPerSourceStep=binding.MechanicalTicksPerSourceStep,offsetTicks=binding.MechanicalTicksPerSourceStep/2});
                var initial=cell.Capture();Write(Path.Combine(folder,"initial.json"),new {condition,count,binding,field,attachment,owner.Fingerprint,source=owner.Source,neuronal=initial,mechanical=mechanics.Capture()});
                using var file=new FileStream(Path.Combine(folder,"trajectory.csv.gz"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                using var gzip=new GZipStream(file,CompressionLevel.Fastest);using var rows=new StreamWriter(gzip,new UTF8Encoding(false));
                using var packetFile=new FileStream(Path.Combine(folder,"packets.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                using var packetWriter=new StreamWriter(packetFile,new UTF8Encoding(false)){AutoFlush=true};
                using var eventFile=new FileStream(Path.Combine(folder,"events.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                using var eventWriter=new StreamWriter(eventFile,new UTF8Encoding(false)){AutoFlush=true};
                rows.WriteLine(string.Join(',',new[]{"step","timeMs","heldBundleNm","mechanicalStartTick","mechanicalEndTick","mechanicalEndNeuralMs","representationDifferenceMs",
                    "startMv","endMv","startSlopeMvPerMs","endSlopeMvPerMs"}.Concat(initial.StateLabels).Concat(initial.StateLabels.Select(x=>"stateError/"+x))
                    .Concat(initial.LedgerLabels).Concat(initial.LedgerLabels.Select(x=>"ledgerError/"+x)).Concat(new[]{"maximumChargeResidualFc","maximumIonResidualAttomoles",
                        "maximumCableChargeResidualFc","maximumInterfaceChargeResidualFc","nonlinearEvaluations","maximumNewtonIterations","maximumAcceptedScaledResidual"})));
                for(int head=2;head<physicalIntervals+2;head++)
                {
                    var released=owner.ConsumeHead(observations[head]);var trace=released.CompletedTrace!;
                    Write(Path.Combine(folder,"mechanical-"+head.ToString("D2")+".json"),released);
                    for(int local=0;local<count;local++)
                    {
                        var receipt=owner.AdvanceNext(steps);steps++;var state=cell.Capture();var v=receipt.Observation.Interval;
                        if(state.CompletedSteps!=steps||state.TimeMs!=receipt.End.SourceNeuralMs||state.Failure is not null)throw new InvalidOperationException("Source state/clock mismatch.");
                        if(receipt.Observation.Event is not null){events++;eventWriter.WriteLine(JsonSerializer.Serialize(receipt.Observation,Json));}
                        var values=new List<double>{steps,state.TimeMs,receipt.HeldBundle.Bundle.BundleDisplacementNanometers,receipt.Start.MechanicalTick,receipt.End.MechanicalTick,
                            receipt.End.MechanicalNeuralMs,receipt.End.RepresentationDifferenceMs,v.StartMv,v.EndMv,v.StartSlopeMvPerMs,v.EndSlopeMvPerMs};
                        values.AddRange(state.Values);values.AddRange(state.AdditionResiduals);values.AddRange(state.Ledgers);values.AddRange(state.LedgerAdditionResiduals);
                        values.AddRange(new[]{state.MaximumChargeResidualFc,state.MaximumIonResidualAttomoles,state.MaximumCableChargeResidualFc,state.MaximumInterfaceChargeResidualFc,
                            state.NonlinearEvaluations,(double)state.MaximumNewtonIterations,state.MaximumAcceptedScaledResidual});
                        if(!values.All(double.IsFinite))throw new ArithmeticException("Nonfinite retained source state.");
                        rows.WriteLine(string.Join(',',values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));
                        if((local+1)%512==0)
                        {
                            var packet=owner.Seal();packetWriter.WriteLine(JsonSerializer.Serialize(packet,Json));owner.AcknowledgePacket(packet.Fingerprint);packets++;
                        }
                    }
                    intervals++;rows.Flush();
                    if(mechanics.Capture().PendingTrace is not null)throw new InvalidOperationException("Completed sensory interval was not acknowledged.");
                    Console.WriteLine(name+" physical interval "+intervals+" / 18; source steps "+steps+"; observed events "+events);
                }
            }
            catch(Exception error)
            {
                failure=error.ToString();all=false;string? prefixError=null;
                // Retain a valid partial observed packet as well as the complete
                // neuronal prefix when a later numerical/observation step fails.
                try
                {
                    var state=owner?.Capture();
                    if(state is not null&&state.CompletedSteps>state.AcknowledgedSteps)
                    {
                        var prefix=owner!.Seal();File.AppendAllText(Path.Combine(folder,"packets.jsonl"),JsonSerializer.Serialize(prefix,Json)+Environment.NewLine,new UTF8Encoding(false));packets++;
                    }
                }
                catch(Exception preservation){prefixError=preservation.ToString();}
                Write(Path.Combine(folder,"failure.json"),new {failure,prefixError,state=owner?.Capture(),neuronal=cell?.Capture(),mechanical=mechanics?.Capture()},FailureJson);
                Console.WriteLine("CONNECTED CASE FAILED "+name+": "+error.Message);
            }
            bool completed=failure is null&&steps==(long)physicalIntervals*count&&intervals==physicalIntervals&&packets==physicalIntervals*count/512;
            all&=completed;var report=new {condition,count,completed,steps,intervals,packets,events,failure,state=owner?.Capture()};
            Write(Path.Combine(folder,"final.json"),report,FailureJson);reports.Add(report);
        }
        Write(Path.Combine(output,"results.json"),new {completed=all,runs=reports,prescribedDiagnosticMotion=true,newNativePhysicsExecuted=false,liveBrainExecuted=false,
            independentConnectedAccuracyQualified=false,physiologicalCalibrationQualified=false,afferentFiringQualified=false,avatarBalanceAssessed=false},FailureJson);
        if(!all)throw new InvalidOperationException("Some connected cases failed; all accepted mechanical/neuronal prefixes retained.");
    }
    private static void Write(string path,object value,JsonSerializerOptions? options=null)
    {using var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);JsonSerializer.Serialize(f,value,options??Json);}
}
