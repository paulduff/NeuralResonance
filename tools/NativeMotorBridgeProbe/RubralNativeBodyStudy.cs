using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static Nre.UnityWorld.MuscleHingeWire;
using Path=System.IO.Path;

// Fresh actual DNNE source -> original receptor motor crossings -> existing
// calcium/muscle/native hinge. Open loop, no live host or movement controller.
internal static class RubralNativeBodyStudy
{
    internal const string Version="rubral-neuronal-native-hinge-v1";
    private const int SourceFrames=12000,WarmFrames=8000,MotorSamples=15000,Steps=7999;
    private const double NeuralEpochMs=1000;
    private sealed record Condition(string Key,string ReferenceKey,float Quanta,bool Disconnected,int ActiveMuscle,int PacketFrames,bool Retry);
    private static readonly Condition[] Conditions=[
        new("quiet","no-evoked-drive",0,false,0,17,false),
        new("unit-left","drive-1",1,false,0,17,false),
        new("strong-left","drive-4",4,false,0,17,false),
        new("strong-right","drive-4",4,false,1,17,false),
        new("strong-cut","drive-4",4,true,0,17,false),
        new("strong-fast","drive-4",4,false,0,1,false),
        new("strong-retry","drive-4",4,false,0,17,true)];
    private static string Bytes(object value)=>JsonSerializer.Serialize(value);
    private static string BodyBytes(object value)=>JsonSerializer.Serialize(value,NativeHingeBody.Json);

    internal static void Run(string player,string reference,string sourceGate,string output)
    {
        Require(new[]{player,reference,sourceGate,output}.All(Path.IsPathFullyQualified)&&File.Exists(player)&&
            Directory.Exists(reference)&&Directory.Exists(sourceGate)&&!Directory.Exists(output),"Require preserved references, qualified source gate and fresh output.");
        using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(sourceGate,"analysis.json")));
        var proof=gate.RootElement;
        Require(proof.GetProperty("completed").GetBoolean()&&proof.GetProperty("bodyEpochSourceGatePassed").GetBoolean()&&
            proof.GetProperty("sourceFrames").GetInt32()==120000&&proof.GetProperty("motorTrajectoryRows").GetInt32()==150010&&
            proof.GetProperty("cases").GetArrayLength()==10&&proof.GetProperty("tests").GetProperty("passedCases").GetInt32()==161,
            "Review and preserve the full source/body-clock gate before native coupling.");
        var spec=JsonSerializer.Deserialize<Parameters>(File.ReadAllText(Path.Combine(reference,"muscle-input.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Require(spec is not null&&spec.CandidateVersion==AvatarCausalMotorPlant.Version&&spec.MuscleVersion==AvatarDynamicMuscle.Version&&
            spec.ParameterRows[0].Calcium==MotorUnitCalciumParameters.NagamoriSlowUnit1,"Require the qualified existing muscle parameters.");
        double end=Steps*StepMsAt(1);
        // Native float step size is preserved exactly. The next full step lies
        // beyond the proven 500 ms body horizon; never invent neuronal silence.
        Require(end<500&&(Steps+1)*StepMsAt(1)>500,"Native/source horizon changed; re-declare coverage before executing.");
        Directory.CreateDirectory(output);
        Save(Path.Combine(output,"protocol.json"),new{version=Version,circuitVersion=RubralMotorCircuit.Version,conditions=Conditions,
            neuralEpochMs=NeuralEpochMs,physicalEpochMs=0,sourceFrames=SourceFrames,motorSamples=MotorSamples,warmFrames=WarmFrames,
            steps=Steps,physicsResolution=1,muscleSubsteps=2,durationMs=end,neuronalHorizonMs=1500,
            neuronalModelExecuted=true,presynapticInputIsFixture=true,oldRecordsDriveModel=false,nativePhysicsExecuted=true,
            liveBrainAccessed=false,sensoryFeedbackImplemented=false,physiologyQualified=false,avatarRecoveryAssessed=false,
            minimumStrongResponseRadians=1e-7,quietAndMirrorBoundRadians=1e-5});
        WirePreflight.Run(player,reference,output);
        var rows=new List<object>();var angles=new Dictionary<string,double>();
        foreach(var condition in Conditions)
        {
            Console.WriteLine("RUBRAL NEURONS TO NATIVE MUSCLES "+condition.Key);
            string folder=Path.Combine(output,condition.Key);Directory.CreateDirectory(folder);Guid session=Guid.NewGuid();
            using var circuit=new RubralMotorCircuit(session,Path.GetFullPath(Path.Combine(folder,"source-state")),condition.Quanta,condition.Disconnected);
            var quietIdentity=new ObservedMotorIdentity("rubral-native-quiet","FivecomptcMG-quiet",
                StableNeuralIdentity.Create(Version,"quiet-projection"),StableNeuralIdentity.Create(Version,"quiet-synapse"));
            var quiet=new ObservedMotorSource(session,quietIdentity,false);
            var activeContact=MotorAxonContact.Create(circuit.Identity.Instance,circuit.Identity.Neuron,circuit.Identity.Projection,"rubral-native-unit",2.0625);
            var quietContact=MotorAxonContact.Create(quietIdentity.Instance,quietIdentity.Neuron,quietIdentity.Projection,"rubral-native-quiet-unit",2.0625);
            var anatomy=new MotorAxonAnatomy([activeContact,quietContact]);var row=spec.ParameterRows[0];
            var definitions=Enumerable.Range(0,2).Select(i=>new CausalMuscleDefinition("m"+i,spec.Geometry,
                [new SlowMotorUnitDefinition(i==condition.ActiveMuscle?activeContact:quietContact,row.Calcium,row.LengthGainAboveOptimal,row.LengthGainBelowOptimal)],spec.InitialFiberLengthMeters)).ToArray();
            var motorClock=new MotorAxonClock(session,NeuralEpochMs,0);ObservedMotorLane quietLane=null;
            NativeHingeBody body=null;AvatarCausalMotorPlant plant=null;Process process=null;
            using var recording=new RubralCircuitTrace(Path.Combine(folder,"neuronal"),Path.Combine(reference,"rubral/study",condition.ReferenceKey),condition.Disconnected,circuit);
            using var quietTrace=new StreamWriter(Path.Combine(folder,"quiet-neuron.csv"));
            using var quietReference=new StreamReader(Path.Combine(reference,"quiet-h0.003125.csv"));
            int quietRows=0;string header="timeMs,"+string.Join(',',Enumerable.Range(0,30).Select(i=>"state"+i));
            Require(header==quietReference.ReadLine(),"Quiet reference columns changed.");quietTrace.WriteLine(header);
            void RecordQuiet(ObservedMotorSnapshot snapshot)
            {
                string line=string.Join(',',new[]{snapshot.TimeMs}.Concat(snapshot.State).Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
                Require(line==quietReference.ReadLine(),"Body scheduling changed the original quiet neuron.");quietTrace.WriteLine(line);++quietRows;
            }
            RecordQuiet(quiet.Capture());
            string Neurons()=>Bytes(new{source=circuit.CaptureSource(),motor=circuit.CaptureMotor(),quiet=quiet.Capture()});
            try
            {
                for(int frame=1;frame<=WarmFrames;++frame)
                {
                    var current=circuit.AdvanceOneSourceSample();recording.Record(current);
                    foreach(var sample in current.MotorSamples){var other=quiet.AdvanceOneSample();Require(other.TimeMs==sample.Cell.TimeMs,"Quiet source clock drifted.");RecordQuiet(other);}
                }
                var primedSource=circuit.CaptureSource();var primedMotor=circuit.CaptureMotor();var primedQuiet=quiet.Capture();
                Require(primedMotor.Cell.TimeMs==NeuralEpochMs&&primedMotor.Emissions.Count==0&&primedQuiet.TimeMs==NeuralEpochMs&&
                    primedQuiet.Emissions.Count==0&&primedQuiet.CrossingsMs.Count==0&&!primedQuiet.Faulted,"Require retained quiet prehistory.");
                string held=Neurons();circuit.AttachMotorOutput(anatomy,motorClock);quietLane=new(quiet,anatomy,motorClock,maximumBatches:1);
                Require(held==Neurons(),"Body-clock binding reset neuronal state.");
                string bridge=Path.Combine(folder,"bridge");
                var start=new ProcessStartInfo(player){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(player)};
                foreach(string arg in new[]{"-batchmode","-nographics","-logFile",Path.Combine(folder,"player.log"),"--motor-bridge",bridge,
                    "--motor-session",session.ToString("N"),"--motor-rest-length-bits",Encode(spec.RestPathLengthMeters),"--motor-resolution","1"})start.ArgumentList.Add(arg);
                process=Process.Start(start)??throw new InvalidOperationException("Native player did not start.");
                body=new(bridge,session,process,exerciseReadLock:condition.Key=="quiet",physicsResolution:1);
                var clock=new PhysicalSampleClock(session,"native-rubral-hinge","muscle-paths",0,StepMsAt(1),0,NeuralEpochMs);
                plant=new(clock,body,definitions,2);body.Bind(plant.Fingerprint);
                var initial=plant.CaptureInitial();
                Save(Path.Combine(folder,"initial.json"),new{scenario=condition.Key,condition,session,clock,motorClock,definitions,
                    anatomy=anatomy.Contacts,anatomyFingerprint=anatomy.Fingerprint,activeContact,quietContact,quietIdentity,
                    plantFingerprint=plant.Fingerprint,bodyFingerprint=body.Fingerprint,initial,muscles=plant.CaptureMuscles(),native=body.Last,
                    primedSource,primedMotor,primedQuiet,muscleSubsteps=2,actualSteps=Steps,syntheticMotorEvents=false,neuronalModelExecuted=true});
                Require(!plant.State.Running&&plant.TryAdvance().Outcome==CausalMotorAdvance.Paused&&body.CompletedPhysicsStep==0,"World must begin paused.");
                plant.Resume();int published=0,retries=0,blocked=0,waiting=0;var delivered=new List<AvatarMotorAxonArrival>();
                using(var steps=new StreamWriter(Path.Combine(folder,"steps.jsonl")))
                using(var packets=new StreamWriter(new GZipStream(new FileStream(Path.Combine(folder,"packets.jsonl.gz"),FileMode.CreateNew),CompressionLevel.Fastest),new UTF8Encoding(false)))
                {
                    for(int frame=WarmFrames+1;frame<=SourceFrames;++frame)
                    {
                        Require(plant.State.Running&&!plant.State.Faulted,"Neurons cannot continue a paused/faulted body epoch.");
                        var current=circuit.AdvanceOneSourceSample();recording.Record(current);
                        foreach(var sample in current.MotorSamples){var other=quietLane.AdvanceOneSample();Require(other.TimeMs==sample.Cell.TimeMs,"Quiet source clock drifted.");RecordQuiet(other);}
                        if((frame-WarmFrames)%condition.PacketFrames!=0&&frame!=SourceFrames)continue;
                        var batches=new[]{circuit.PublishCompleted(),quietLane.PublishCompleted()};++published;
                        Require(batches[0].CompleteThroughNeuralMs==batches[1].CompleteThroughNeuralMs,"Both original motor sources must complete the same interval.");
                        held=Neurons();long beforeStep=body.CompletedPhysicsStep;
                        Require(plant.TryAdvance().Outcome==CausalMotorAdvance.WaitingForNeuronalCompleteness&&body.CompletedPhysicsStep==beforeStep,"Body advanced before original source completeness.");++waiting;
                        bool retry=condition.Retry&&(published%7==0||frame==SourceFrames);
                        for(int source=0;source<2;++source)
                        {
                            if(retry)
                            {
                                bool rejected=false;
                                try{if(source==0)circuit.AdvanceOneSourceSample();else quietLane.AdvanceOneSample();}catch(InvalidOperationException){rejected=true;}
                                Require(rejected&&held==Neurons(),"Backpressure changed a source neuron.");++blocked;
                            }
                            var batch=batches[source];var receipt=plant.Accept(batch);
                            packets.WriteLine(Bytes(new{action="accept",sourceIndex=source,batch,canonicalBatchJson=Bytes(batch),receipt,
                                bodyStepAtReceipt=body.CompletedPhysicsStep,completeThroughBodyMs=plant.State.CompleteThroughBodyMs}));
                            if(retry)
                            {
                                var retained=source==0?circuit.Unacknowledged():quietLane.Unacknowledged();
                                string before=BodyBytes(new{state=plant.State,muscles=plant.CaptureMuscles()});
                                Require(retained.Count==1&&ReferenceEquals(retained[0],batch),"Original output was not retained.");
                                var again=plant.Accept(retained[0]);Require(again==receipt&&before==BodyBytes(new{state=plant.State,muscles=plant.CaptureMuscles()}),"Retry changed body delivery.");
                                packets.WriteLine(Bytes(new{action="retry",sourceIndex=source,batch,canonicalBatchJson=Bytes(batch),receipt=again,
                                    bodyStepAtReceipt=body.CompletedPhysicsStep,completeThroughBodyMs=plant.State.CompleteThroughBodyMs}));++retries;
                            }
                            if(source==0){circuit.Acknowledge(receipt);if(retry)circuit.Acknowledge(receipt);}
                            else{quietLane.Acknowledge(receipt);if(retry)quietLane.Acknowledge(receipt);}
                            if(source==0){Require(plant.TryAdvance().Outcome==CausalMotorAdvance.WaitingForNeuronalCompleteness&&body.CompletedPhysicsStep==beforeStep,"One source completed both motor routes.");++waiting;}
                        }
                        Require(held==Neurons(),"Body acceptance changed neuronal state.");
                        while(plant.State.CompletedPhysicsStep<Steps&&clock.AtStep(plant.State.CompletedPhysicsStep+1).PhysicalMs<=plant.State.CompleteThroughBodyMs)
                        {
                            var advance=plant.TryAdvance();Require(advance.Outcome==CausalMotorAdvance.Advanced&&advance.Step is not null,"Missing owned physical step.");
                            delivered.AddRange(advance.Step.Arrivals);steps.WriteLine(BodyBytes(new{step=advance.Step,native=body.Last,sourceCompleteThroughBodyMs=plant.State.CompleteThroughBodyMs}));
                        }
                    }
                }
                recording.Finish();quietTrace.Flush();
                var finalMotor=circuit.CaptureMotor();var finalQuiet=quiet.Capture();
                Require(quietRows==MotorSamples+1&&finalQuiet.CompletedSamples==MotorSamples&&finalQuiet.Emissions.Count==0&&finalQuiet.CrossingsMs.Count==0&&
                    !finalQuiet.Faulted&&finalMotor.CompletedSamples==MotorSamples&&!circuit.Faulted,"Incomplete/changed neuronal epoch.");
                var expected=finalMotor.Emissions.Select(e=>new AvatarMotorAxonArrival(session,e.SourceInstanceKey,e.MessageId,e.Ordinal,e.SourceNeuronId,e.ProjectionId,
                    e.SourceSynapseId,activeContact.ContactId,activeContact.MotorUnitId,e.FiredAtNeuralMs,motorClock.ToBodyMs(e.FiredAtNeuralMs)+activeContact.ConductionDelayMs))
                    .Where(a=>a.ArrivalAtBodyMs<=end).OrderBy(a=>a.ArrivalAtBodyMs).ThenBy(a=>a.Ordinal).ToArray();
                Require(delivered.SequenceEqual(expected)&&plant.State.CompletedPhysicsStep==Steps&&plant.State.BodyTimeMs==end&&
                    plant.State.AcceptedEvents==finalMotor.Emissions.Count&&plant.State.DeliveredEvents==delivered.Count&&
                    plant.State.PendingEvents==finalMotor.Emissions.Count-expected.Length&&circuit.Unacknowledged().Count==0&&
                    quietLane.Unacknowledged().Count==0&&quietLane.UnpublishedEmissions==0,"Incomplete neuronal/muscle/body accounting.");
                Require(waiting==2*published&&blocked==retries&&retries==(condition.Retry?2*((published/7)+(published%7==0?0:1)):0),"Incomplete retry/wait controls.");
                plant.Pause();var final=body.Last;
                Require(plant.TryAdvance().Outcome==CausalMotorAdvance.Paused&&body.CaptureGeometry().PhysicsStep==Steps&&body.Last.stateHash==final.stateHash,"Paused body moved.");
                using var state=JsonDocument.Parse(final.stateJson);double angle=state.RootElement.GetProperty("angle").GetDouble();angles.Add(condition.Key,angle);
                Save(Path.Combine(folder,"final.json"),new{plant=plant.State,muscles=plant.CaptureMuscles(),native=final,angle,arrivals=delivered.Count,
                    source=circuit.CaptureSource(),motor=finalMotor,quiet=finalQuiet,motorClock,published,retries,blocked,waiting,
                    originalRubralObservationsPreserved=true,originalMotorTrajectoryPreserved=true,quietPrefixPreserved=true,quietPrefixRows=quietRows});
                body.Stop();Require(process.WaitForExit(30000)&&process.ExitCode==0,"Native player did not stop cleanly.");
                rows.Add(new{condition=condition.Key,completed=true,physicsSteps=Steps,motorEvents=finalMotor.Emissions.Count,arrivals=delivered.Count,angle,published,retries});
            }
            catch(Exception error)
            {
                Save(Path.Combine(folder,"failure.json"),new{error=error.ToString(),source=circuit.CaptureSource(),motor=circuit.CaptureMotor(),
                    quiet=quiet.Capture(),pendingSource=circuit.PendingSource(),plant=plant?.State,native=body?.Last});throw;
            }
            finally{if(process is not null){if(!process.HasExited){process.Kill(true);process.WaitForExit();}process.Dispose();}}
        }
        bool movement=Math.Abs(angles["quiet"])<=1e-5&&Math.Abs(angles["strong-cut"])<=1e-5&&angles["strong-left"]>1e-7&&
            angles["strong-right"]< -1e-7&&Math.Abs(angles["strong-left"]+angles["strong-right"])<=1e-5;
        Save(Path.Combine(output,"results.json"),new{completed=true,sourceBodyAccountingPassed=true,observedNeuronalMuscleMovement=movement,
            cases=rows,actualPhysicsSteps=Conditions.Length*Steps,neuronalModelExecuted=true,nativePhysicsExecuted=true,liveBrainAccessed=false,
            coupledTrajectoryAccuracyQualified=false,physiologyQualified=false,avatarRecoveryAssessed=false});
        Require(movement,"Original qualitative movement bounds failed; preserve full completed accounting and motion.");
    }
    private static void Save(string path,object value)=>NativeHingeBody.Write(path,value);
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
