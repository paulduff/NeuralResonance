using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static Nre.UnityWorld.MuscleHingeWire;

// A reference neuron drives actual native point forces. This isolated open-loop
// circuit is not a live spinal population, sensory loop or human calibration.
internal static class ObservedMotorBodyStudy
{
    internal const string Version = "observed-neuronal-native-hinge-v1";
    internal const double NeuralEpochMs = 1000;
    internal const int Steps = 2048, WarmSamples = 10000, FinalSourceSample = 11281;
    private sealed record Condition(string Key, bool Left, bool Right, int PacketSamples, bool Retry);
    private static readonly Condition[] Conditions = [new("quiet",false,false,100,false),new("left",true,false,100,false),
        new("right",false,true,100,false),new("balanced",true,true,100,false),new("left-fast",true,false,10,false),
        new("left-slow",true,false,500,false),new("left-retry",true,false,100,true)];
    private static string EncodeState(object value) => JsonSerializer.Serialize(value, NativeHingeBody.Json);

    internal static void Run(string player, string reference, string gate, string output)
    {
        Require(new[]{player,reference,gate,output}.All(System.IO.Path.IsPathFullyQualified) && File.Exists(player) &&
            Directory.Exists(reference) && Directory.Exists(gate) && !Directory.Exists(output),"Require absolute qualified inputs and fresh output.");
        using var qualification=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(gate,"source-analysis.json")));
        var passed=qualification.RootElement;
        Require(passed.GetProperty("completed").GetBoolean() && passed.GetProperty("sourceAdapterCriteriaPassed").GetBoolean() &&
            passed.GetProperty("activeCadenceEventIdentityEqual").GetBoolean() && passed.GetProperty("cases").GetArrayLength()==5,
            "The current binary must pass the complete source gate before native coupling.");
        var spec=JsonSerializer.Deserialize<Parameters>(File.ReadAllText(System.IO.Path.Combine(reference,"muscle-input.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Require(spec!=null && spec.CandidateVersion==AvatarCausalMotorPlant.Version && spec.MuscleVersion==AvatarDynamicMuscle.Version &&
            spec.ParameterRows[0].Calcium==MotorUnitCalciumParameters.NagamoriSlowUnit1,"Require the qualified Avatar muscle model.");
        // Parameters includes only the required subset of the larger source file.
        double end=Steps*StepMsAt(1);
        Require((FinalSourceSample-1)*ObservedMotorSource.SampleMs<NeuralEpochMs+end &&
            FinalSourceSample*ObservedMotorSource.SampleMs>=NeuralEpochMs+end,"Invalid source coverage horizon.");
        Directory.CreateDirectory(output);Guid evidence=Guid.NewGuid();
        var identities=Enumerable.Range(0,2).Select(i=>new ObservedMotorIdentity("observed-native-"+i,"FivecomptcMG_0-"+i,
            StableNeuralIdentity.Create("observed-native-projection",i),StableNeuralIdentity.Create("observed-native-synapse",i))).ToArray();
        var contacts=identities.Select((identity,i)=>MotorAxonContact.Create(identity.Instance,identity.Neuron,identity.Projection,"observed-unit-"+i,2.0625)).ToArray();
        var anatomy=new MotorAxonAnatomy(contacts);var row=spec.ParameterRows[0];
        var definitions=contacts.Select((c,i)=>new CausalMuscleDefinition("m"+i,spec.Geometry,
            [new SlowMotorUnitDefinition(c,row.Calcium,row.LengthGainAboveOptimal,row.LengthGainBelowOptimal)],spec.InitialFiberLengthMeters)).ToArray();
        Save(System.IO.Path.Combine(output,"protocol.json"),new{version=Version,evidence,identities,contacts,conditions=Conditions,
            neuralEpochMs=NeuralEpochMs,physicalEpochMs=0,physicsResolution=1,muscleSubsteps=2,steps=Steps,durationMs=end,
            warmSamples=WarmSamples,finalSourceSample=FinalSourceSample,sourceVersion=ObservedMotorSource.Version,
            neuronalModelExecuted=true,liveBrainAccessed=false,sensoryFeedbackImplemented=false,physiologyQualified=false,avatarRecoveryAssessed=false});
        WirePreflight.Run(player,reference,output);
        var results=new List<object>();var angles=new Dictionary<string,double>();
        foreach(var condition in Conditions)
        {
            Console.WriteLine("OBSERVED NEURON TO NATIVE BODY "+condition.Key);
            string folder=System.IO.Path.Combine(output,condition.Key);Directory.CreateDirectory(folder);
            var sources=identities.Select((identity,i)=>new ObservedMotorSource(evidence,identity,i==0?condition.Left:condition.Right)).ToArray();
            var traces=Enumerable.Range(0,2).Select(i=>new StreamWriter(System.IO.Path.Combine(folder,"neuron-"+i+".csv"))).ToArray();
            ObservedMotorLane[] lanes=null;Process process=null;
            try
            {
                for(int i=0;i<2;++i){traces[i].WriteLine("timeMs,"+string.Join(',',Enumerable.Range(0,30).Select(n=>"state"+n)));Sample(traces[i],sources[i].Capture());}
                for(int sample=1;sample<=WarmSamples;++sample)
                    for(int i=0;i<2;++i)Sample(traces[i],sources[i].AdvanceOneSample());
                var primed=sources.Select(s=>s.Capture()).ToArray();
                Require(primed.All(s=>s.TimeMs==NeuralEpochMs && s.Emissions.Count==0 && s.CrossingsMs.Count==0 && !s.Faulted),"Prehistory must be quiet and complete, never discarded.");
                Guid session=Guid.NewGuid();string bridge=System.IO.Path.Combine(folder,"bridge");
                var start=new ProcessStartInfo(player){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=System.IO.Path.GetDirectoryName(player)};
                foreach(string arg in new[]{"-batchmode","-nographics","-logFile",System.IO.Path.Combine(folder,"player.log"),"--motor-bridge",bridge,
                    "--motor-session",session.ToString("N"),"--motor-rest-length-bits",Encode(spec.RestPathLengthMeters),"--motor-resolution","1"})start.ArgumentList.Add(arg);
                process=Process.Start(start)??throw new InvalidOperationException("Native player did not start.");
                var body=new NativeHingeBody(bridge,session,process,exerciseReadLock:condition.Key=="quiet",physicsResolution:1);
                var clock=new PhysicalSampleClock(session,"native-observed-hinge","muscle-paths",0,StepMsAt(1),0,NeuralEpochMs);
                var motorClock=new MotorAxonClock(session,NeuralEpochMs,0);
                var plant=new AvatarCausalMotorPlant(clock,body,definitions,2);body.Bind(plant.Fingerprint);
                var initial=plant.CaptureInitial();var initialMuscles=plant.CaptureMuscles();
                lanes=sources.Select(s=>new ObservedMotorLane(s,anatomy,motorClock,maximumBatches:1)).ToArray();
                Require(EncodeState(primed)==EncodeState(lanes.Select(l=>l.CaptureSource()).ToArray()),"Binding reset source state.");
                Save(System.IO.Path.Combine(folder,"initial.json"),new{scenario=condition.Key,clock,definitions,plantFingerprint=plant.Fingerprint,
                    bodyFingerprint=body.Fingerprint,initial,muscles=initialMuscles,native=body.Last,muscleSubsteps=2,actualSteps=Steps,
                    primedSources=primed,syntheticMotorEvents=false,neuronalModelExecuted=true});
                Require(!plant.State.Running && plant.TryAdvance().Outcome==CausalMotorAdvance.Paused && body.CompletedPhysicsStep==0,"World must begin paused.");
                plant.Resume();int published=0,retries=0,blockedSource=0,waiting=0;var delivered=new List<AvatarMotorAxonArrival>();
                using(var trace=new StreamWriter(System.IO.Path.Combine(folder,"steps.jsonl")))
                using(var packets=new StreamWriter(System.IO.Path.Combine(folder,"packets.jsonl")))
                {
                    for(int sample=WarmSamples+1;sample<=FinalSourceSample;++sample)
                    {
                        Require(plant.State.Running && !plant.State.Faulted,"Source cannot continue a paused or failed body epoch.");
                        for(int i=0;i<2;++i)Sample(traces[i],lanes[i].AdvanceOneSample());
                        if((sample-WarmSamples)%condition.PacketSamples!=0 && sample!=FinalSourceSample)continue;
                        var batch=lanes.Select(l=>l.PublishCompleted()).ToArray();++published;
                        var beforeSources=EncodeState(lanes.Select(l=>l.CaptureSource()).ToArray());
                        long beforeStep=body.CompletedPhysicsStep;
                        Require(plant.TryAdvance().Outcome==CausalMotorAdvance.WaitingForNeuronalCompleteness && body.CompletedPhysicsStep==beforeStep,"Body advanced without source completeness.");++waiting;
                        for(int i=0;i<2;++i)
                        {
                            if(condition.Retry && published%7==0)
                            {
                                bool rejected=false;try{lanes[i].AdvanceOneSample();}catch(InvalidOperationException){rejected=true;}
                                Require(rejected && beforeSources==EncodeState(lanes.Select(l=>l.CaptureSource()).ToArray()),"Backpressure advanced a neuron.");++blockedSource;
                            }
                            var receipt=plant.Accept(batch[i]);
                            packets.WriteLine(EncodeState(new{action="accept",sourceIndex=i,batch=batch[i],receipt,
                                bodyStepAtReceipt=body.CompletedPhysicsStep,completeThroughBodyMs=plant.State.CompleteThroughBodyMs}));
                            if(condition.Retry && published%7==0)
                            {
                                var retained=lanes[i].Unacknowledged();var acceptedState=EncodeState(new{plant=plant.State,muscles=plant.CaptureMuscles()});
                                Require(retained.Count==1 && ReferenceEquals(retained[0],batch[i]),"Lost original retained batch.");
                                var again=plant.Accept(retained[0]);
                                Require(again==receipt && acceptedState==EncodeState(new{plant=plant.State,muscles=plant.CaptureMuscles()}),"Retry duplicated a body event.");
                                packets.WriteLine(EncodeState(new{action="retry",sourceIndex=i,batch=retained[0],receipt=again,
                                    bodyStepAtReceipt=body.CompletedPhysicsStep,completeThroughBodyMs=plant.State.CompleteThroughBodyMs}));++retries;
                            }
                            lanes[i].Acknowledge(receipt);
                            if(i==0){Require(plant.TryAdvance().Outcome==CausalMotorAdvance.WaitingForNeuronalCompleteness && body.CompletedPhysicsStep==beforeStep,"One source incorrectly completed both routes.");++waiting;}
                        }
                        Require(beforeSources==EncodeState(lanes.Select(l=>l.CaptureSource()).ToArray()),"Publication or delivery changed the neurons.");
                        while(plant.State.CompletedPhysicsStep<Steps && clock.AtStep(plant.State.CompletedPhysicsStep+1).PhysicalMs<=plant.State.CompleteThroughBodyMs)
                        {
                            var advance=plant.TryAdvance();Require(advance.Outcome==CausalMotorAdvance.Advanced && advance.Step!=null,"Missing owned body step.");
                            delivered.AddRange(advance.Step.Arrivals);trace.WriteLine(EncodeState(new{step=advance.Step,native=body.Last,
                                sourceCompleteThroughBodyMs=plant.State.CompleteThroughBodyMs}));
                        }
                    }
                }
                for(int i=0;i<2;++i){traces[i].Dispose();VerifyPrefix(System.IO.Path.Combine(folder,"neuron-"+i+".csv"),
                    System.IO.Path.Combine(reference,lanes[i].CaptureSource().Source+"-h0.003125.csv"));}
                plant.Pause();var final=body.Last;var states=lanes.Select(l=>l.CaptureSource()).ToArray();
                var expected=states.SelectMany((s,i)=>s.Emissions.Select(e=>new AvatarMotorAxonArrival(session,e.SourceInstanceKey,e.MessageId,e.Ordinal,
                    e.SourceNeuronId,e.ProjectionId,e.SourceSynapseId,contacts[i].ContactId,contacts[i].MotorUnitId,e.FiredAtNeuralMs,
                    motorClock.ToBodyMs(e.FiredAtNeuralMs)+contacts[i].ConductionDelayMs))).Where(a=>a.ArrivalAtBodyMs<=end)
                    .OrderBy(a=>a.ArrivalAtBodyMs).ThenBy(a=>a.SourceInstanceKey,StringComparer.Ordinal).ThenBy(a=>a.Ordinal).ToArray();
                Require(delivered.SequenceEqual(expected) && plant.State.CompletedPhysicsStep==Steps && plant.State.BodyTimeMs==end &&
                    plant.State.AcceptedEvents==states.Sum(s=>s.Emissions.Count) && plant.State.DeliveredEvents==delivered.Count &&
                    plant.State.PendingEvents==states.Sum(s=>s.Emissions.Count)-expected.Length && lanes.All(l=>l.UnpublishedEmissions==0 && l.Unacknowledged().Count==0),"Incomplete causal source/body accounting.");
                Require(waiting==2*published && retries==(condition.Retry?2*(published/7):0) && blockedSource==retries,"Incomplete wait/retry controls.");
                Require(plant.TryAdvance().Outcome==CausalMotorAdvance.Paused && body.CaptureGeometry().PhysicsStep==Steps && body.Last.stateHash==final.stateHash,"Paused native body moved.");
                using var state=JsonDocument.Parse(final.stateJson);double angle=state.RootElement.GetProperty("angle").GetDouble();angles.Add(condition.Key,angle);
                Save(System.IO.Path.Combine(folder,"final.json"),new{plant=plant.State,muscles=plant.CaptureMuscles(),native=final,arrivals=delivered.Count,
                    angle,sources=states,motorClock,published,retries,blockedSource,waiting,sourcePrefixRows=FinalSourceSample+1,sourcePrefixExactlyReproduced=true});
                body.Stop();Require(process.WaitForExit(30000) && process.ExitCode==0,"Native player did not stop cleanly.");
                results.Add(new{condition=condition.Key,passed=true,physicsSteps=Steps,arrivals=delivered.Count,angle,published,retries});
            }
            catch(Exception error){Save(System.IO.Path.Combine(folder,"failure.json"),new{error=error.ToString(),sources=sources.Select(s=>s.Capture()).ToArray()});throw;}
            finally{foreach(var trace in traces)trace.Dispose();if(process!=null){if(!process.HasExited){process.Kill(true);process.WaitForExit();}process.Dispose();}}
        }
        Require(Math.Abs(angles["quiet"])<=1e-5 && Math.Abs(angles["balanced"])<=1e-5 && angles["left"]>1e-7 &&
            angles["right"]< -1e-7 && Math.Abs(angles["left"]+angles["right"])<=1e-5,"Original qualitative movement bounds failed.");
        Save(System.IO.Path.Combine(output,"results.json"),new{completed=true,sourceBodyCriteriaPassed=true,cases=results,actualPhysicsSteps=7*Steps,
            neuronalModelExecuted=true,nativePhysicsExecuted=true,liveBrainAccessed=false,coupledTrajectoryAccuracyQualified=false,
            physiologyQualified=false,avatarRecoveryAssessed=false});
    }

    private static void Sample(StreamWriter writer,ObservedMotorSnapshot s)=>writer.WriteLine(string.Join(',',new[]{s.TimeMs}.Concat(s.State).Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
    private static void VerifyPrefix(string actual,string reference)
    {
        using var candidate=new StreamReader(actual);using var baseline=new StreamReader(reference);int lines=0;string line;
        while((line=candidate.ReadLine())!=null){Require(line==baseline.ReadLine(),"Native scheduling changed the original neuronal trace.");++lines;}
        Require(lines==FinalSourceSample+2,"Truncated source prehistory or trial trace.");
    }
    private static void Save(string path,object value)=>NativeHingeBody.Write(path,value);
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
