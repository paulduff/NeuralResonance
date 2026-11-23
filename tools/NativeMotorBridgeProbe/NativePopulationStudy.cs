#nullable enable
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static Nre.UnityWorld.MuscleHingeWire;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;
using Path = System.IO.Path;

// Explicit diagnostic events, NOT observed neuronal output or recruitment.
// Original muscle equations and native force owner; no live brain selection.
internal static class NativePopulationStudy
{
    internal const string Version="native-full-slow-population-v1";
    internal sealed record Condition(string Name,bool Left,bool Right,bool Cut,bool Retry,int Substeps);
    internal sealed record Protocol(string Version,int BodySteps,Condition[] Cases);
    internal static readonly Condition[] Cases = [new("quiet",false,false,false,false,160),
        new("left",true,false,false,false,160),new("right",false,true,false,false,160),
        new("both",true,true,false,false,160),new("both-cut",true,true,true,false,160),
        new("both-retry",true,true,false,true,160),new("quiet-finer",false,false,false,false,320),
        new("both-finer",true,true,false,false,320)];
    private static readonly JsonSerializerOptions Json = Options();
    private static JsonSerializerOptions Options(){var o=new JsonSerializerOptions(JsonSerializerDefaults.Web){IncludeFields=true};o.Converters.Add(new JsonStringEnumConverter());return o;}
    private static string Serialize(object o)=>JsonSerializer.Serialize(o,Json);
    internal static void Run(string player,string inputs,string output)
    {
        Require(new[]{player,inputs,output}.All(Path.IsPathFullyQualified)&&File.Exists(player)&&Directory.Exists(inputs)&&!Directory.Exists(output),"Require retained input/player and fresh output.");
        var protocol=JsonSerializer.Deserialize<Protocol>(File.ReadAllText(Path.Combine(inputs,"protocol.json")),Json)!;
        var population=JsonSerializer.Deserialize<MusclePopulationDynamicsStudy.Input>(File.ReadAllText(Path.Combine(inputs,"population.json")),Json)!;
        Require(protocol.Version==Version&&protocol.BodySteps==40&&protocol.Cases.SequenceEqual(Cases),"Unexpected prospective native inventory.");
        Require(population.Version=="named-ta-full-slow-population-dynamics-v1"&&population.ParameterRows.Length==147&&
            population.ParameterRows.Select(r=>r.SourceRow).SequenceEqual(Enumerable.Range(1,147))&&
            population.ParameterSha256=="01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd","Retain all original slow rows.");
        using var mapping=JsonDocument.Parse(File.ReadAllText(Path.Combine(inputs,"mapping.json")));
        var paths=mapping.RootElement.GetProperty("paths").EnumerateArray().Select(r=>r.GetProperty("path"))
            .Where(p=>p.GetProperty("MuscleId").GetString() is "tibant_l" or "tibant_r").OrderBy(p=>p.GetProperty("MuscleId").GetString(),StringComparer.Ordinal).ToArray();
        Require(paths.Length==2&&paths.Sum(p=>p.GetProperty("Anchors").GetArrayLength())==8,"Require both four-point TA paths.");
        using var binding=JsonDocument.Parse(File.ReadAllText(Path.Combine(inputs,"binding.json")));
        using var original=JsonDocument.Parse(binding.RootElement.GetProperty("originalNativeEndpointJson").GetString()!);
        using var originalGeometry=JsonDocument.Parse(original.RootElement.GetProperty("geometryJson").GetString()!);
        var quiet=File.ReadLines(Path.Combine(inputs,"quiet.jsonl")).Take(2).Select(s=>JsonDocument.Parse(s)).ToArray();
        Require(quiet.Length==2,"Retain original initial and prelude endpoints.");
        Directory.CreateDirectory(output);Write(Path.Combine(output,"protocol.json"),protocol);int completed=0;
        foreach(var condition in Cases)
        {
            try {RunCase(player,output,paths,population,originalGeometry.RootElement,quiet,condition);completed++;}
            catch(Exception error){Console.WriteLine("CASE FAILED "+condition.Name+": "+error);}
        }
        foreach(var q in quiet)q.Dispose();
        Write(Path.Combine(output,"result.json"),new{Version,CompletedCases=completed,ExpectedCases=8,Completed=completed==8,
            PrescribedDiagnosticEvents=true,UnitsPerMuscle=147,IndependentCoupledAccuracyQualified=false,NeuronalAuthorityQualified=false,AvatarStandingQualified=false});
        Require(completed==8,"Incomplete native population cases; partial traces retained.");
    }
    private static void RunCase(string player,string output,JsonElement[] paths,MusclePopulationDynamicsStudy.Input population,
        JsonElement originalGeometry,JsonDocument[] quiet,Condition condition)
    {
        Console.WriteLine("NATIVE FULL SLOW POPULATIONS "+condition.Name);
        string folder=Path.Combine(output,condition.Name);Directory.CreateDirectory(folder);Guid session=Guid.NewGuid();
        var config=new Wire.Configuration{protocol=Wire.NamedMuscleProtocol,sessionId=session.ToString("N"),
            stepSeconds=(double)originalGeometry.GetProperty("stepSeconds").GetSingle(),passivePreludeSteps=1,mechanicsMode="no-joint-friction",
            declarations=paths.Select(p=>new Wire.Declaration{muscleId=p.GetProperty("MuscleId").GetString()!,provenance=p.GetProperty("Provenance").GetString()!,
                anchors=p.GetProperty("Anchors").EnumerateArray().Select(a=>new Wire.Anchor{id=a.GetProperty("AttachmentId").GetString()!,bodyId=a.GetProperty("BodyId").GetString()!,
                    x=a.GetProperty("LocalPositionMeters").GetProperty("X").GetDouble(),y=a.GetProperty("LocalPositionMeters").GetProperty("Y").GetDouble(),
                    z=a.GetProperty("LocalPositionMeters").GetProperty("Z").GetDouble()}).ToArray()}).ToArray()};
        Wire.Validate(config);string configPath=Path.Combine(folder,"input.json");Write(configPath,config);
        var start=new ProcessStartInfo(player){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(player)};
        foreach(string a in new[]{"-batchmode","-nographics","-logFile",Path.Combine(folder,"player.log"),"--neuronal-muscle-qualify","1",
            "--tendon-bridge",session.ToString("N"),"--tendon-config",configPath,"--output",folder})start.ArgumentList.Add(a);
        using var process=Process.Start(start)??throw new InvalidOperationException("Native player did not start.");
        NativeArticulatedBody? body=null;NativeArticulatedHeadInput? head=null;AvatarCausalMotorPlant? plant=null;
        int endpoints=0,closures=0,packets=0,retries=0,events=0,waitChecks=0;
        using var acknowledged=Lines(Path.Combine(folder,"acknowledged.jsonl"),false);
        using var heads=Lines(Path.Combine(folder,"heads.jsonl"),false);
        using var steps=Lines(Path.Combine(folder,"steps.jsonl.gz"),true);
        using var source=Lines(Path.Combine(folder,"packets.jsonl.gz"),true);
        try
        {
            body=new NativeArticulatedBody(File.ReadAllText(configPath),new FileArticulatedTendonExchange(Path.Combine(folder,"tendon-bridge"),process));
            using var actualGeometry=JsonDocument.Parse(body.Last.geometryJson);
            Require(actualGeometry.RootElement.GetProperty("links").GetRawText()==originalGeometry.GetProperty("links").GetRawText()&&
                actualGeometry.RootElement.GetProperty("gravity").GetRawText()==originalGeometry.GetProperty("gravity").GetRawText()&&
                body.Last.stateJson==quiet[0].RootElement.GetProperty("stateJson").GetString(),"Original full-body geometry and initial pose changed.");
            var clock=new PhysicalSampleClock(session,"native-population","avatar_unity_body",0,body.PhysicsStepMs,0,0);
            head=new NativeArticulatedHeadInput(body,clock);Record();
            var attachment=body.InitializePassiveInterval(clock);Record();
            Require(body.Last.stateJson==quiet[1].RootElement.GetProperty("stateJson").GetString(),"Original quiet prelude changed.");
            var motorClock=new MotorAxonClock(session,attachment.NeuralEpochMs,attachment.Frame.PhysicalTimeMs);
            var scenarios=new[]{population.Cases.Single(c=>c.Key=="left-mixed"),population.Cases.Single(c=>c.Key=="right-mixed")};
            var definitions=scenarios.Select((s,i)=>new CausalMuscleDefinition(i==0?"tibant_l":"tibant_r",s.Geometry,
                population.ParameterRows.Select(r=>new SlowMotorUnitDefinition(MotorAxonContact.Create("diagnostic-ta-"+i+"-"+r.SourceRow.ToString("D3"),
                    "fixture-"+r.SourceRow.ToString("D3"),StableNeuralIdentity.Create(Version,"projection",i,r.SourceRow),
                    (i==0?"left-":"right-")+r.SourceRow.ToString("D3"),0),r.Calcium,r.LengthGainAboveOptimal,r.LengthGainBelowOptimal)).ToArray(),s.InitialFiberLengthMeters)).ToArray();
            for(int i=0;i<2;i++)Require(scenarios[i].InitialTimeMs==attachment.Frame.PhysicalTimeMs&&
                scenarios[i].InitialMusculotendonLengthMeters==attachment.Frame.Paths.Single(p=>p.MuscleId==definitions[i].MuscleId).LengthMeters,
                "Saved initial length/time differs from actual native prelude.");
            var anatomy=new MotorAxonAnatomy(definitions.SelectMany(d=>d.Units).Select(u=>u.Contact));
            var contacts=definitions.SelectMany(d=>d.Units).Select(u=>u.Contact).ToArray();
            var publishers=contacts.Select(c=>new MotorAxonPublisher(anatomy,motorClock,c.SourceInstanceKey)).ToArray();
            var fixtures=contacts.Select((c,index)=>scenarios[index/147].Events.Where(e=>e.UnitIndex==index%147).Select((e,n)=>new SpikeMessage{
                MessageId=StableNeuralIdentity.Create(Version,"diagnostic-event",index,n),TimestampMs=motorClock.NeuralEpochMs+e.OffsetMs,
                SourceStructure=StructureId.SpinalCordMotor,SourceInstanceKey=c.SourceInstanceKey,SourceNeuronId=c.SourceNeuronId,
                ProjectionId=c.ProjectionId,SynapseId=StableNeuralIdentity.Create(Version,"diagnostic-synapse",index),Neurotransmitter=NTEnum.ACETYLCHOLINE}).ToArray()).ToArray();
            Write(Path.Combine(folder,"declaration.json"),new{Version,Condition=condition,Clock=clock,MotorClock=motorClock,Definitions=definitions,
                AnatomyFingerprint=anatomy.Fingerprint,Fixtures=fixtures,Attachment=attachment,OriginalEvidenceJson=body.InitializationEvidenceJson,
                PrescribedDiagnosticEvents=true,NeuronalModelExecuted=false});
            plant=new(clock,body,definitions,condition.Substeps,schedule:CausalMotorSchedule.BodyThenMotor,attachment:attachment);
            body.Bind(plant.Fingerprint);plant.CaptureInitial();Write(Path.Combine(folder,"initial.json"),new{Muscles=plant.CaptureMuscles(),State=plant.State});
            plant.Resume();double previous=motorClock.NeuralEpochMs;
            for(int n=0;n<40;n++)
            {
                string before=Serialize(plant.CaptureMuscles());var pending=plant.TryAdvanceBody();
                Require(pending.Outcome==CausalBodyAdvance.Advanced&&pending.Step!=null,"Native body must advance once.");Record();
                Require(plant.TryAdvanceBody().Outcome==CausalBodyAdvance.AlreadyPending,"Second body advance must remain pending.");
                Require(plant.TryCompleteMotorInterval().Outcome==CausalMotorAdvance.WaitingForNeuronalCompleteness&&Serialize(plant.CaptureMuscles())==before,"Missing source must not advance muscles.");waitChecks++;
                double through=motorClock.NeuralEpochMs+(pending.Step!.After.PhysicalTimeMs-motorClock.BodyEpochMs);
                for(int i=0;i<contacts.Length;i++)
                {
                    bool selected=i<147?condition.Left:condition.Right;
                    var selectedEvents=selected?fixtures[i].Where(e=>e.TimestampMs>previous&&e.TimestampMs<=through).ToArray():[];
                    var emitted=condition.Cut?Array.Empty<SpikeMessage>():selectedEvents;
                    var batch=publishers[i].Publish(emitted,through);var receipt=plant.Accept(batch);packets++;events+=emitted.Length;
                    source.WriteLine(Serialize(new{Interval=n,Source=i,Selected=selectedEvents,Batch=batch,Receipt=receipt,Retry=condition.Retry}));
                    if(condition.Retry){var state=plant.State;Require(plant.Accept(batch)==receipt&&plant.State==state,"A repeated packet changed accepted/delivered state.");retries++;}
                    if(i==contacts.Length-2){Require(plant.TryCompleteMotorInterval().Outcome==CausalMotorAdvance.WaitingForNeuronalCompleteness&&Serialize(plant.CaptureMuscles())==before,"Final source must also close the interval.");waitChecks++;}
                }
                var complete=plant.TryCompleteMotorInterval();
                Require(complete.Outcome==CausalMotorAdvance.Advanced&&complete.Step!=null,"All sources must close exactly one interval.");
                steps.WriteLine(Serialize(new{Step=complete.Step,State=plant.State}));closures++;previous=through;
                Require(plant.State.AcceptedEvents==events&&plant.State.DeliveredEvents==events&&plant.State.PendingEvents==0,"Original diagnostic arrivals must be consumed exactly once.");
            }
            plant.Pause();body.Stop();Require(process.WaitForExit(30000)&&process.ExitCode==0,"Native player did not stop cleanly.");
            Require(endpoints==42&&closures==40&&packets==11760&&waitChecks==80&&events==(condition.Cut?0:(condition.Left?588:0)+(condition.Right?588:0)),"Incomplete native/source inventory.");
            Write(Path.Combine(folder,"result.json"),new{Version,Completed=true,Condition=condition,Endpoints=endpoints,Closures=closures,Packets=packets,
                Retries=retries,Events=events,WaitChecks=waitChecks,HeadReceipts=endpoints*3,FinalState=plant.State,NeuronalAuthorityQualified=false,AvatarStandingQualified=false});
            void Record(){acknowledged.WriteLine(JsonSerializer.Serialize(body.Last,NativeArticulatedBody.Json));
                var frame=head.ReadAcknowledgedEndpoint();heads.WriteLine(Serialize(frame));Require(frame.PhysicsStep==endpoints&&frame.Samples.Count==3,"Head sample ownership differs.");endpoints++;}
        }
        catch(Exception error){Write(Path.Combine(folder,"failure.json"),new{Error=error.ToString(),LastAcknowledged=body?.Last,
            LastHead=head?.Last,Plant=plant?.State,Muscles=plant?.CaptureMuscles(),Endpoints=endpoints,Closures=closures,Packets=packets,Events=events});throw;}
        finally{if(!process.HasExited){process.Kill(entireProcessTree:true);process.WaitForExit();}}
    }
    private static StreamWriter Lines(string path,bool gzip){Stream stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        if(gzip)stream=new GZipStream(stream,CompressionLevel.Fastest);return new(stream,new UTF8Encoding(false,true)){AutoFlush=true};}
    private static void Write<T>(string path,T value){string temporary=path+".tmp";
        using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))JsonSerializer.Serialize(stream,value,Json);
        File.Move(temporary,path);}
}
