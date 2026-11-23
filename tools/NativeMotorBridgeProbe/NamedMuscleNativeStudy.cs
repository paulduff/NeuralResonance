#nullable enable
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static Nre.UnityWorld.MuscleHingeWire;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;
using Path = System.IO.Path;

// Prescribed mechanics only. Original mapped names/anchors pass through the
// actual native force owner; no neural signal or muscle activation is invented.
internal static class NamedMuscleNativeStudy
{
    internal const string Version="native-named-lower-leg-study-v1";
    private const int FinalStep=8;
    private sealed record Condition(string Name,string Muscle,int InspectionsPerEndpoint);
    internal static void Run(string player,string mappingFile,string inputFile,string output)
    {
        Require(new[]{player,mappingFile,inputFile,output}.All(Path.IsPathFullyQualified) && File.Exists(player) &&
            File.Exists(mappingFile) && File.Exists(inputFile) && !Directory.Exists(output),"Require preserved mapped input, isolated player and a fresh output.");
        using var mapping=JsonDocument.Parse(File.ReadAllText(mappingFile));using var input=JsonDocument.Parse(File.ReadAllText(inputFile));
        Require(mapping.RootElement.GetProperty("version").GetString()=="declared-lower-leg-path-candidate-v1" &&
            !mapping.RootElement.GetProperty("runtimeSelected").GetBoolean() && mapping.RootElement.GetProperty("footScaleIsDeclaredShinRatio").GetBoolean(),"Use the explicit retained anatomical candidate.");
        var paths=mapping.RootElement.GetProperty("paths").EnumerateArray().Select(r=>r.GetProperty("path")).OrderBy(p=>p.GetProperty("MuscleId").GetString(),StringComparer.Ordinal).ToArray();
        Require(paths.Length==10 && paths.Sum(p=>p.GetProperty("Anchors").GetArrayLength())==44,"Require all ten retained paths and 44 points.");
        using var original=JsonDocument.Parse(input.RootElement.GetProperty("originalNativeEndpointJson").GetString()!);
        using var geometry=JsonDocument.Parse(original.RootElement.GetProperty("geometryJson").GetString()!);
        Require(paths.All(p=>p.GetProperty("BodyGeometryFingerprint").GetString()==original.RootElement.GetProperty("bodyFingerprint").GetString()),"Mapped source body differs.");
        var cases=new[]{new Condition("quiet","",0)}.Concat(paths.Select(p=>new Condition(p.GetProperty("MuscleId").GetString()!,p.GetProperty("MuscleId").GetString()!,0)))
            .Append(new Condition("quiet-inspected","",2)).ToArray();
        Directory.CreateDirectory(output);
        Write(Path.Combine(output,"protocol.json"),new{Version,Cases=cases,PrescribedTensionNewtons=40,PassivePreludeSteps=1,
            FootScaleIsDeclaredShinRatio=true,AnatomicalFitQualified=false,NeuronalSourceExecuted=false,MuscleCellExecuted=false});
        foreach(var condition in cases)RunCase(player,output,paths,original.RootElement,geometry.RootElement,condition);
        Write(Path.Combine(output,"result.json"),new{Version,Completed=true,NativeCases=12,PhysicsSteps=96,PhysicalEndpoints=108,HeadReceipts=324,
            ExtraInspections=18,IndependentSavedComparisonRequired=true,NeuronalSourceExecuted=false,MuscleCellExecuted=false,AvatarStandingQualified=false});
    }
    private static void RunCase(string player,string output,JsonElement[] paths,JsonElement original,JsonElement originalGeometry,Condition condition)
    {
        Console.WriteLine("ACTUAL NAMED LOWER-LEG PATH "+condition.Name);string directory=Path.Combine(output,condition.Name);Directory.CreateDirectory(directory);
        Guid session=Guid.NewGuid();
        var config=new Wire.Configuration{protocol=Wire.NamedMuscleProtocol,sessionId=session.ToString("N"),
            stepSeconds=(double)originalGeometry.GetProperty("stepSeconds").GetSingle(),passivePreludeSteps=1,mechanicsMode="no-joint-friction",
            declarations=paths.Select(p=>new Wire.Declaration{muscleId=p.GetProperty("MuscleId").GetString()!,provenance=p.GetProperty("Provenance").GetString()!,
                anchors=p.GetProperty("Anchors").EnumerateArray().Select(a=>new Wire.Anchor{id=a.GetProperty("AttachmentId").GetString()!,bodyId=a.GetProperty("BodyId").GetString()!,
                    x=a.GetProperty("LocalPositionMeters").GetProperty("X").GetDouble(),y=a.GetProperty("LocalPositionMeters").GetProperty("Y").GetDouble(),
                    z=a.GetProperty("LocalPositionMeters").GetProperty("Z").GetDouble()}).ToArray()}).ToArray()};
        Wire.Validate(config);string configPath=Path.Combine(directory,"input.json");Write(configPath,config);
        var start=new ProcessStartInfo(player){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(player)};
        foreach(string a in new[]{"-batchmode","-nographics","-logFile",Path.Combine(directory,"player.log"),"--neuronal-muscle-qualify","1",
            "--tendon-bridge",session.ToString("N"),"--tendon-config",configPath,"--output",directory})start.ArgumentList.Add(a);
        using var process=Process.Start(start)??throw new InvalidOperationException("Named muscle player did not start.");
        NativeArticulatedBody? body=null;NativeArticulatedHeadInput? head=null;int endpoints=0,inspections=0;
        using var acknowledged=Lines(Path.Combine(directory,"acknowledged.jsonl"));using var heads=Lines(Path.Combine(directory,"heads.jsonl"));using var inspected=Lines(Path.Combine(directory,"inspections.jsonl"));
        try
        {
            body=new NativeArticulatedBody(File.ReadAllText(configPath),new FileArticulatedTendonExchange(Path.Combine(directory,"tendon-bridge"),process));
            using var actualGeometry=JsonDocument.Parse(body.Last.geometryJson);
            Require(actualGeometry.RootElement.GetProperty("links").GetRawText()==originalGeometry.GetProperty("links").GetRawText() &&
                actualGeometry.RootElement.GetProperty("gravity").GetRawText()==originalGeometry.GetProperty("gravity").GetRawText() &&
                body.Last.stateJson==original.GetProperty("stateJson").GetString(),"Original bone geometry and initial pose must match before applying mapped forces.");
            var clock=new PhysicalSampleClock(session,"named-avatar-native","avatar_unity_body",0,body.PhysicsStepMs,0,1000);
            head=new NativeArticulatedHeadInput(body,clock);Write(Path.Combine(directory,"clock.json"),new{Clock=clock,clock.Fingerprint,HeadInputFingerprint=head.Fingerprint});Record();
            var attachment=body.InitializePassiveInterval(clock);Write(Path.Combine(directory,"attachment.json"),new{Attachment=attachment,OriginalEvidenceJson=body.InitializationEvidenceJson});Record();
            string plant=Wire.Digest(JsonSerializer.Serialize(new{Version,Condition=condition,Clock=clock.Fingerprint,attachment.InitializationEvidenceSha256,
                Kind="prescribed named-path tension; no neuronal or muscle-cell source"}));body.Bind(plant);
            for(long step=1;step<FinalStep;step++)
            {
                body.AdvanceOneStep(new MotorMechanicalEffort(Guid.NewGuid(),session,body.Fingerprint,plant,step,clock.AtStep(step).PhysicalMs,clock.AtStep(step+1).PhysicalMs,
                    config.declarations.Select(d=>new TendonTension(d.muscleId,d.muscleId==condition.Muscle?40:0)).ToArray()));Record();
            }
            body.Stop();Require(ReferenceEquals(head.Last,head.ReadAcknowledgedEndpoint()),"Stop invented a head sample.");
            Require(process.WaitForExit(30000) && process.ExitCode==0,"Native player did not stop cleanly.");
            Require(endpoints==9 && inspections==condition.InspectionsPerEndpoint*9 && body.CompletedPhysicsStep==8,"Incomplete named physical sequence.");
            Write(Path.Combine(directory,"result.json"),new{Version,Completed=true,Condition=condition,Endpoints=endpoints,Inspections=inspections,
                PhysicsSteps=8,HeadReceipts=endpoints*3,FinalSequence=body.Last.sequence,PrescribedForceOnly=true,AvatarStandingQualified=false});
            void Record()
            {
                var reply=body.Last;acknowledged.WriteLine(JsonSerializer.Serialize(reply,NativeArticulatedBody.Json));
                var frame=head.ReadAcknowledgedEndpoint();heads.WriteLine(JsonSerializer.Serialize(frame,NativeArticulatedBody.Json));endpoints++;
                Require(frame.PhysicsStep==endpoints-1 && frame.Samples.Count==3 && frame.Samples.All(s=>(frame.PhysicsStep==0)==(s.Kinematics.VestibularInput==null)),"Head endpoint ownership changed.");
                for(int i=0;i<condition.InspectionsPerEndpoint;i++){body.CaptureGeometry();inspected.WriteLine(JsonSerializer.Serialize(body.Last,NativeArticulatedBody.Json));inspections++;
                    Require(ReferenceEquals(frame,head.ReadAcknowledgedEndpoint()),"Inspection invented a head sample.");}
            }
        }
        catch(Exception error)
        {
            Write(Path.Combine(directory,"failure.json"),new{Error=error.ToString(),LastAcknowledged=body?.Last,LastCompleteHeadFrame=head?.Last,
                head?.AttemptedReplyJson,head?.PartialSamples,HeadFailure=head?.Failure,Endpoints=endpoints,Inspections=inspections,CommandResent=false});throw;
        }
        finally{if(!process.HasExited){process.Kill(entireProcessTree:true);process.WaitForExit();}}
    }
    private static StreamWriter Lines(string path)=>new(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false,true)){AutoFlush=true};
    private static void Write<T>(string path,T value)=>NativeHingeBody.Write(path,value);
}
