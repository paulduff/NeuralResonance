using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

// Qualify the new body-epoch owner against the complete, already reviewed
// ten-input recordings. Old records are comparisons, never driving inputs.
internal static class RubralMotorCircuitStudy
{
    internal const string Version="rubral-body-epoch-source-gate-v1";
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static string Bytes(object value)=>JsonSerializer.Serialize(value);
    private static void Save(string path,object value)
    {
        using(var stream=new FileStream(path+".tmp",FileMode.CreateNew))JsonSerializer.Serialize(stream,value,Json);
        File.Move(path+".tmp",path);
    }
    private static StreamWriter Compressed(string path)=>new(new GZipStream(new FileStream(path,FileMode.CreateNew),CompressionLevel.Fastest),new UTF8Encoding(false));
    private static StreamReader ReadCompressed(string path)=>new(new GZipStream(File.OpenRead(path),CompressionMode.Decompress));
    private static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}

    internal static void Run(string reference,string output)
    {
        Require(Path.IsPathFullyQualified(reference)&&Directory.Exists(reference)&&Path.IsPathFullyQualified(output)&&!Directory.Exists(output),
            "Require the frozen reviewed reference and a fresh source-gate output.");
        Directory.CreateDirectory(output);
        Save(Path.Combine(output,"protocol.json"),new{version=Version,circuitVersion=RubralMotorCircuit.Version,
            bodyNeuralEpochMs=1000.0,bodyEpochMs=0.0,sourceFrames=12000,motorSamples=15000,warmFrames=8000,
            publicationEverySourceFrames=new[]{1,17},fixtureInputRetained=true,oldRecordsDriveModel=false,
            neuronalModelExecuted=true,nativePhysicsExecuted=false,liveBrainAccessed=false,avatarRecoveryAssessed=false});
        var results=new List<object>();
        foreach(var (key,quanta) in new[]{("no-evoked-drive",0f),("drive-quarter",.25f),("drive-1",1f),("drive-4",4f)})
        foreach(bool disconnected in key=="drive-4"?new[]{false,true}:new[]{false})
        foreach(int cadence in new[]{1,17})
        {
            string name=key+(disconnected?"-cut":"")+"-packets"+cadence;Console.WriteLine("RUBRAL BODY-EPOCH SOURCE "+name);
            string folder=Path.Combine(output,name),baseline=Path.Combine(reference,"study",key);Directory.CreateDirectory(folder);
            string baselineTrajectory=Path.Combine(baseline,disconnected?"contacts-disabled":"packets1","trajectory.csv");
            Guid session=Guid.NewGuid();using var circuit=new RubralMotorCircuit(session,Path.GetFullPath(Path.Combine(folder,"source-state")),quanta,disconnected);
            File.WriteAllText(Path.Combine(folder,"input.json"),circuit.OriginalInputJson);
            File.WriteAllText(Path.Combine(folder,"input-anatomy.json"),circuit.InputAnatomyJson);
            File.WriteAllText(Path.Combine(folder,"topology.json"),circuit.TopologyJson);
            var contact=MotorAxonContact.Create(circuit.Identity.Instance,circuit.Identity.Neuron,circuit.Identity.Projection,"owned-rubral-unit",2.0625);
            var anatomy=new MotorAxonAnatomy([contact]);var clock=new MotorAxonClock(session,1000,0);
            var receiver=new AvatarMotorAxonReceiver(anatomy,clock);
            Save(Path.Combine(folder,"initial.json"),new{name,key,quanta,cadence,disconnected,session,anatomy=anatomy.Contacts,
                anatomyFingerprint=anatomy.Fingerprint,clock,circuitFingerprint=circuit.ConfigurationFingerprint,motor=circuit.CaptureMotor()});
            long rows=0,received=0,batches=0,blocked=0,retries=0;double published=1000;
            using var originalFrames=ReadCompressed(Path.Combine(baseline,"source-frames.jsonl.gz"));
            using var originalTrajectory=new StreamReader(baselineTrajectory);
            try
            {
                using(var frames=Compressed(Path.Combine(folder,"source-frames.jsonl.gz")))
                using(var admissions=Compressed(Path.Combine(folder,"admissions.jsonl.gz")))
                using(var packets=Compressed(Path.Combine(folder,"packets.jsonl.gz")))
                using(var trajectory=new StreamWriter(Path.Combine(folder,"trajectory.csv")))
                {
                    var initial=circuit.CaptureMotor();string header=string.Join(',',new[]{"timeMs"}.Concat(Enumerable.Range(0,30).Select(i=>"state"+i))
                        .Concat(initial.Cell.Contacts.Select(c=>"receptor_"+c.ContactId)).Concat(new[]{"integrationSteps","deliveredEvents","crossings"}));
                    Require(header==originalTrajectory.ReadLine(),"Original motor contact columns changed.");trajectory.WriteLine(header);
                    void Record(ReceptorMotorSourceState state)
                    {
                        var c=state.Cell;var values=new[]{c.TimeMs}.Concat(c.MembraneState).Concat(c.Contacts.Select(r=>r.Value.OpenFraction))
                            .Concat(new double[]{c.IntegrationSteps,c.DeliveredEvents,c.Crossings.Length});
                        string line=string.Join(',',values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
                        Require(line==originalTrajectory.ReadLine(),"Body-epoch attachment changed a recorded motor sample at "+c.TimeMs);
                        trajectory.WriteLine(line);++rows;
                    }
                    Record(initial);
                    for(int step=1;step<=12000;++step)
                    {
                        var frame=circuit.AdvanceOneSourceSample();var payload=IsolatedRubralSource.Decode(frame.OriginalSource);
                        frames.WriteLine(Bytes(frame.OriginalSource));
                        admissions.WriteLine(Bytes(new{sourceSequence=step,frame.Admissions,frame.SourceAcknowledgement}));
                        string line=originalFrames.ReadLine();Require(line!=null,"Missing original source endpoint.");
                        var previousFrame=JsonSerializer.Deserialize<RubralSourceFrame>(line);
                        var previous=IsolatedRubralSource.Decode(previousFrame);
                        Require(Bytes(payload.Observation)==Bytes(previous.Observation)&&Bytes(payload.Energy)==Bytes(previous.Energy)&&
                            payload.OriginalEvents.Select(e=>(e.SourceNeuronId,e.TimestampMs)).SequenceEqual(previous.OriginalEvents.Select(e=>(e.SourceNeuronId,e.TimestampMs))),
                            "New owner changed original source activity.");
                        foreach(var sample in frame.MotorSamples)Record(sample);
                        if(step==8000)
                        {
                            string before=Bytes(circuit.CaptureMotor());Require(circuit.CaptureMotor().Cell.TimeMs==1000&&circuit.CaptureMotor().Emissions.Count==0,"Prehistory is not quiet and complete.");
                            circuit.AttachMotorOutput(anatomy,clock);Require(before==Bytes(circuit.CaptureMotor()),"Body attachment reset the neuron.");
                            Save(Path.Combine(folder,"primed.json"),new{source=circuit.CaptureSource(),motor=circuit.CaptureMotor(),clock});
                        }
                        if(step<=8000||((step-8000)%cadence!=0&&step!=12000))continue;
                        var batch=circuit.PublishCompleted();++batches;string held=Bytes(new{source=circuit.CaptureSource(),motor=circuit.CaptureMotor()});
                        bool hold=batches%113==0||step==12000;
                        if(hold)
                        {
                            bool rejected=false;try{circuit.AdvanceOneSourceSample();}catch(InvalidOperationException){rejected=true;}
                            Require(rejected&&held==Bytes(new{source=circuit.CaptureSource(),motor=circuit.CaptureMotor()})&&ReferenceEquals(circuit.Unacknowledged()[0],batch),
                                "Unacknowledged output advanced the neuronal circuit.");++blocked;
                        }
                        var receipt=receiver.AcceptWithAcknowledgement(batch);bool replay=batches%37==0||step==12000;
                        if(replay){Require(receipt==receiver.AcceptWithAcknowledgement(batch),"Motor receiver retry changed.");++retries;}
                        circuit.Acknowledge(receipt);if(replay)circuit.Acknowledge(receipt);
                        Require(held==Bytes(new{source=circuit.CaptureSource(),motor=circuit.CaptureMotor()}),"Delivery changed neuronal state.");
                        published=batch.CompleteThroughNeuralMs;var arrivals=receiver.AdvanceTo(clock.ToBodyMs(published));received+=arrivals.Count;
                        packets.WriteLine(Bytes(new{batch,canonicalBatchJson=Bytes(batch),receipt,replayed=replay,blocked=hold,arrivals}));
                    }
                    Require(originalFrames.ReadLine()==null&&originalTrajectory.ReadLine()==null&&rows==15001,"Truncated original/candidate trajectory.");
                }
                var state=circuit.CaptureMotor();Require(published==1500&&circuit.CaptureSource().CompletedSamples==12000&&state.CompletedSamples==15000,
                    "Incomplete circuit endpoint.");
                Require(circuit.Unacknowledged().Count==0&&!circuit.Faulted&&receiver.CompleteThroughBodyMs==500&&
                    received+receiver.PendingEvents==state.Emissions.Count,"Incomplete source/output accounting.");
                Require(Hash(Path.Combine(folder,"trajectory.csv"))==Hash(baselineTrajectory),"Motor trace bytes differ.");
                Save(Path.Combine(folder,"final.json"),new{source=circuit.CaptureSource(),motor=state,received,
                    receiverPending=receiver.PendingEvents,receiverCompleteThroughMs=receiver.CompleteThroughBodyMs,batches,blocked,retries,
                    motorTrajectoryExactlyPreserved=true,sourceObservationsExactlyPreserved=true});
                results.Add(new{name,key,cadence,disconnected,completed=true,sourceFrames=12000,motorSamples=15000,motorEvents=state.Emissions.Count,received,batches,blocked,retries});
            }
            catch(Exception error){Save(Path.Combine(folder,"failure.json"),new{error=error.ToString(),source=circuit.CaptureSource(),
                motor=circuit.CaptureMotor(),pending=circuit.PendingSource()});throw;}
        }
        Save(Path.Combine(output,"results.json"),new{completed=true,bodyEpochSourceGatePassed=true,cases=results,
            sourceFrames=120000,motorTrajectoryRows=150010,nativePhysicsExecuted=false,physiologyQualified=false,avatarRecoveryAssessed=false});
    }
}
