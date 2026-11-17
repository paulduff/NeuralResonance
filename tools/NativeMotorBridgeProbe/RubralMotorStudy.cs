using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

// Fresh actual DNNE source -> original events -> receptor motor cell -> Avatar
// receiver. No live service, body physics, muscle dynamics or sensory feedback.
internal static class RubralMotorStudy
{
    internal const string Version = "isolated-rubral-receptor-motor-study-v1";
    private const double DurationMs = 1500;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    private static string Bytes<T>(T value) => JsonSerializer.Serialize(value);
    private static string Hash(string path) { using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)); }
    private static StreamWriter Compressed(string path) => new(new GZipStream(new FileStream(path,FileMode.CreateNew),CompressionLevel.Fastest),new UTF8Encoding(false));

    private static SpikeMessage[] Input(float quanta)
    {
        if(quanta==0) return [];
        return Enumerable.Range(0,20).SelectMany(pulse=>Enumerable.Range(0,256).Select(cell=>new SpikeMessage
        {
            MessageId=StableNeuralIdentity.Create(Version,"input",pulse,cell),TimestampMs=1000.125+10*pulse,
            SourceStructure=StructureId.M1,TargetStructure=StructureId.RedNucleus,SourceInstanceKey="isolated-rubral-drive-fixture",
            SourceNeuronId=$"fixture-{cell:000}",TargetNeuronId=$"n-{cell:000}",
            SynapseId=StableNeuralIdentity.Create(Version,"input-contact",cell),
            ProjectionId=StableNeuralIdentity.Create(Version,"input-projection",cell),
            Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=quanta,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL,
            ModulationContext=new NeuromodState(),IsSyntheticBackground=false
        })).ToArray();
    }

    internal static void Run(string output, bool declaredInputs = false, bool tenInputs = false)
    {
        Require(!tenInputs || declaredInputs, "Ten-input comparison requires declared input anatomy.");
        Require(!Directory.Exists(output),"Preserve earlier study results.");Directory.CreateDirectory(output);
        Save(Path.Combine(output,"protocol.json"),new {version=tenInputs?"isolated-rubral-ten-input-study-v1":declaredInputs?"isolated-rubral-declared-input-study-v1":Version,
            inputContactsDeclared=declaredInputs,sourceVersion=IsolatedRubralSource.Version,
            sourceStructure=StructureId.RedNucleus,sourceModel="Izhikevich",sourcePlasticity="STDP",sourceCells=256,
            sourceSampleMs=IsolatedRubralSource.SampleMs,durationMs=DurationMs,
            cases=new[]{new{key="no-evoked-drive",quanta=0f},new{key="drive-quarter",quanta=.25f},new{key="drive-1",quanta=1f},new{key="drive-4",quanta=4f}},
            inputSourceIsFixture=true,fixturePulses=20,fixtureOnsetMs=1000.125,fixtureIntervalMs=10.0,
            motorSourceVersion=ReceptorMotorSource.CubicVersion,motorMaximumStepMs=.003125,motorSampleMs=.1,
            targetSelection="lexicographically first existing primary spinal target, before observing activity",
            inputConductanceNs=50.0,inputDelayMs=.125,outputDelayMs=2.0625,
            branches=tenInputs?new[]{"packets1","packets17","contacts-disabled","single-contact"}:new[]{"packets1","packets17","contacts-disabled"},
            sourceTimingConvention=IsolatedRubralSource.TimingConvention,
            contactSemantics="original payload retained; fixed reference conductance/unit pulse; source quanta/modulation not mapped to efficacy",
            presynapticNeuronsExecuted=true,liveBrainAccessed=false,nativePhysicsExecuted=false,muscleDynamicsExecuted=false,
            upstreamPhysiologyQualified=false,learnedContactEfficacyTransferred=false,avatarRecoveryAssessed=false});
        var outcomes=new List<object>();bool demonstrated=false;
        foreach(var (key,quanta) in new[]{("no-evoked-drive",0f),("drive-quarter",.25f),("drive-1",1f),("drive-4",4f)})
        {
            Console.WriteLine("ACTUAL DNNE RUBRAL SOURCE "+key);
            string folder=Path.Combine(output,key);Directory.CreateDirectory(folder);Guid session=Guid.NewGuid();
            // Each condition has separate empty storage/session but identical
            // anatomical/input identities. Quantal drive is the declared change.
            string instance="isolated-rubral-study";var inputs=Input(quanta);
            Save(Path.Combine(folder,"input.json"),new{session,instance,quanta,originalEvents=inputs});
            // Declare the same 256 contacts even in the quiet condition. The
            // existing fixture identities, stimulation and output target stay fixed.
            var anatomy=declaredInputs?new IsolatedSynapticTargets(Input(1).Take(256)
                .Select((message,index)=>IsolatedSynapticTarget.From(message,index)),256):null;
            // Fixed before observing any activity: original n-081 and its next
            // nine indexed neighbours converge on the original spinal cell 2.
            var projection=tenInputs?new IsolatedRubralProjection(Enumerable.Range(81,10),2):null;
            using var source=new IsolatedRubralSource(session,instance,Path.GetFullPath(Path.Combine(folder,"source-state")),DurationMs,inputs,anatomy,projection);
            if(projection is not null) Save(Path.Combine(folder,"output-anatomy.json"),new{version=IsolatedRubralProjection.Version,
                fingerprint=projection.Fingerprint,sources=projection.Sources,targetIndex=projection.TargetIndex,
                selection="n-081 through n-090 fixed before neuronal execution; no activity-based selection"});
            if(anatomy is not null) Save(Path.Combine(folder,"input-anatomy.json"),new{version=IsolatedSynapticTargets.Version,
                fingerprint=source.InputTargetFingerprint,entries=anatomy.Entries,actualDeliveryVerificationRequired=true});
            string target=source.Routes.Select(r=>r.TargetNeuron).Order(StringComparer.Ordinal).First();
            var selected=source.Routes.Where(r=>r.TargetNeuron==target).OrderBy(r=>r.SourceIndex).ToArray();
            Require(selected.Length>0,"No declared rubral recipient.");
            if(tenInputs) Require(target=="auto-SpinalCordMotor-002"&&selected.Select(r=>r.SourceIndex).SequenceEqual(Enumerable.Range(81,10)),"Ten-input anatomy changed.");
            var identity=new ReceptorMotorIdentity("isolated-rubral-motor",target,
                StableNeuralIdentity.Create(Version,session,"motor-projection"),StableNeuralIdentity.Create(Version,session,"motor-synapse"));
            var contacts=selected.Select(r=>new MotorReceptorDefinition(r.SourceInstance,StructureId.RedNucleus,r.SourceNeuron,r.Synapse,r.Projection,
                identity.Instance,identity.Neuron,r.TargetIndex,"rubral-"+r.SourceNeuron,1+r.SourceIndex%4,MotorReceptorKind.AmpaReference,50,.125)).ToArray();
            Save(Path.Combine(folder,"topology.json"),new{allRoutes=source.Routes,selected,identity,contacts,contactIds=contacts.Select(c=>c.ContactId).ToArray()});
            using var fast=new Branch(folder,"packets1",1,false,session,identity,contacts);
            using var slow=new Branch(folder,"packets17",17,false,session,identity,contacts);
            using var disabled=new Branch(folder,"contacts-disabled",17,true,session,identity,contacts);
            using var single=tenInputs?new Branch(folder,"single-contact",17,false,session,identity,contacts.Where(c=>c.SourceNeuron=="n-081").ToArray()):null;
            var branches=tenInputs?new[]{fast,slow,disabled,single}:new[]{fast,slow,disabled};long sourceEvents=0,selectedEvents=0,blocked=0,deliveredFixtures=0;
            try
            {
                using(var frames=Compressed(Path.Combine(folder,"source-frames.jsonl.gz")))
                {
                    for(int step=1;step<=12000;step++)
                    {
                        var frame=source.AdvanceOneSample();var payload=IsolatedRubralSource.Decode(frame);
                        frames.WriteLine(Bytes(frame));sourceEvents+=payload.FiredCount;
                        selectedEvents+=payload.OriginalEvents.Count(e=>e.TargetNeuronId==target);
                        deliveredFixtures+=payload.InputDeliveries?.Count??0;
                        if(step%113==0||step==12000)
                        {
                            string before=Bytes(source.Capture());bool rejected=false;
                            try{source.AdvanceOneSample();}catch(InvalidOperationException){rejected=true;}
                            Require(rejected&&Bytes(source.Capture())==before&&ReferenceEquals(source.Pending(),frame),"Source advanced through unacknowledged output.");blocked++;
                        }
                        foreach(var branch in branches) branch.Accept(frame,step==12000);
                        var ack=source.Acknowledge(session,frame.Sequence,frame.Sha256);
                        Require(ack==source.Acknowledge(session,frame.Sequence,frame.Sha256),"Source acknowledgement replay changed.");
                    }
                }
                foreach(var branch in branches) branch.Finish();
                Require(Hash(fast.TrajectoryPath)==Hash(slow.TrajectoryPath),"Input packet cadence changed the physical motor trajectory.");
                var a=fast.Lane.CaptureSource();var b=slow.Lane.CaptureSource();var cut=disabled.Lane.CaptureSource();
                Require(Bytes(a.Emissions)==Bytes(b.Emissions)&&a.Cell.MembraneState.SequenceEqual(b.Cell.MembraneState),"Motor firing differs across identical original input events.");
                Require(cut.Emissions.Count==0&&cut.Cell.AdmittedEvents==0,"Contact-disabled branch generated a motor event.");
                Require(a.Cell.AdmittedEvents==selectedEvents&&b.Cell.AdmittedEvents==selectedEvents,"Selected source events were lost or fabricated.");
                bool response=selectedEvents>0&&a.Emissions.Count>0;demonstrated|=response;
                Require(!declaredInputs||deliveredFixtures==inputs.Length,"Not every declared fixture event actually reached its receiving neuron.");
                var result=new{key,completed=true,sourceEvents,selectedEvents,sourceFrames=12000,sourceBackpressureChecks=blocked,
                    declaredInputContacts=declaredInputs,deliveredFixtureEvents=deliveredFixtures,
                    source=source.Capture(),motorEvents=a.Emissions.Count,disabledMotorEvents=cut.Emissions.Count,
                    trajectoryHash=Hash(fast.TrajectoryPath),packetCadenceExact=true,observedNeuronalMotorResponse=response,
                    receiverDeliveryExact=fast.Received==slow.Received&&fast.Pending==slow.Pending&&fast.Received+fast.Pending==a.Emissions.Count,
                    upstreamNumericalAccuracyQualified=false,upstreamPhysiologyQualified=false,learnedContactEfficacyTransferred=false,
                    movingBodyCouplingQualified=false,avatarRecoveryAssessed=false};
                Require(result.receiverDeliveryExact,"Receiver delivery/pending tail did not account for every neuronal output.");
                Save(Path.Combine(folder,"result.json"),result);outcomes.Add(result);
            }
            catch(Exception error)
            {
                Save(Path.Combine(folder,"failure.json"),new{error=error.ToString(),source=source.Capture(),pending=source.Pending(),
                    pendingInputDeliveries=source.PendingInputDeliveries(),
                    motor=branches.Select(b=>new{b.Name,state=b.Lane.CaptureSource()}).ToArray()});throw;
            }
        }
        Save(Path.Combine(output,"results.json"),new{completed=true,cases=outcomes,componentCausalityChecksPassed=true,
            observedNeuronalMotorResponse=demonstrated,presynapticNeuronsExecuted=true,presynapticInputIsFixture=true,
            upstreamNumericalAccuracyQualified=false,upstreamPhysiologyQualified=false,learnedContactEfficacyTransferred=false,
            liveBrainAccessed=false,nativePhysicsExecuted=false,muscleDynamicsExecuted=false,avatarRecoveryAssessed=false});
    }

    private sealed class Branch:IDisposable
    {
        internal string Name{get;}
        internal string TrajectoryPath{get;}
        internal ReceptorMotorLane Lane{get;}
        internal long Received{get;private set;}
        internal int Pending=>_receiver.PendingEvents;
        private readonly string _folder;private readonly int _cadence;private readonly bool _disabled;
        private readonly Guid _session;private readonly MotorReceptorDefinition[] _contacts;
        private readonly AvatarMotorAxonReceiver _receiver;
        private readonly List<RubralSourceFrame> _frames=[];
        private readonly StreamWriter _trajectory,_transport,_admissions;
        private long _sequence,_outputBatches,_retries,_blocked,_sample;
        private bool _finished;
        internal Branch(string root,string name,int cadence,bool disabled,Guid session,ReceptorMotorIdentity identity,MotorReceptorDefinition[] contacts)
        {
            Name=name;_cadence=cadence;_disabled=disabled;_session=session;_contacts=contacts;
            _folder=Path.Combine(root,name);Directory.CreateDirectory(_folder);
            var anatomy=new MotorAxonAnatomy([MotorAxonContact.Create(identity.Instance,identity.Neuron,identity.Projection,"observed-rubral-unit",2.0625)]);
            var clock=new MotorAxonClock(session,0,0);
            var motor=new ReceptorMotorSource(MotoneuronReferenceParameters.CatLowThreshold,contacts,session,identity,
                crossingInterpolation:MotoneuronCrossingInterpolation.CubicHermite);
            Lane=new(motor,anatomy,clock,maximumBatches:1);_receiver=new(anatomy,clock);
            Save(Path.Combine(_folder,"initial.json"),new{cadence,disabled,anatomy=anatomy.Contacts,anatomyFingerprint=anatomy.Fingerprint,clock,source=Lane.CaptureSource()});
            TrajectoryPath=Path.Combine(_folder,"trajectory.csv");_trajectory=new(TrajectoryPath);
            _transport=Compressed(Path.Combine(_folder,"motor-transport.jsonl.gz"));_admissions=Compressed(Path.Combine(_folder,"receptor-admissions.jsonl.gz"));
            var state=Lane.CaptureSource();
            _trajectory.WriteLine(string.Join(',',new[]{"timeMs"}.Concat(Enumerable.Range(0,30).Select(i=>"state"+i))
                .Concat(state.Cell.Contacts.Select(c=>"receptor_"+c.ContactId)).Concat(new[]{"integrationSteps","deliveredEvents","crossings"})));
            Record(state);
            string before=Bytes(state);bool rejected=false;
            try{Lane.AdvanceOneSample();}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&Bytes(Lane.CaptureSource())==before,"Missing presynaptic completeness was treated as silence.");
        }
        internal void Accept(RubralSourceFrame frame,bool last)
        {
            Require(!_finished&&_frames.Count<_cadence,"Input buffering exceeded its declared cadence.");_frames.Add(frame);
            if(_frames.Count<_cadence&&!last)return;
            double through=frame.CompleteThroughMs;long sequence=++_sequence;
            var original=_frames.SelectMany(f=>IsolatedRubralSource.Decode(f).OriginalEvents).ToArray();
            foreach(var contact in _contacts)
            {
                var selected=original.Where(e=>e.SourceNeuronId==contact.SourceNeuron&&e.TargetNeuronId==contact.TargetNeuron).ToArray();
                var delivered=_disabled?Array.Empty<SpikeMessage>():selected;
                var receipt=Lane.Admit(_session,contact.ContactId,sequence,through,delivered);
                bool replay=sequence%37==0||last;
                if(replay)Require(receipt==Lane.Admit(_session,contact.ContactId,sequence,through,delivered),"Contact input replay changed.");
                string canonicalInputJson=Bytes(new{SessionId=_session,ContactId=contact.ContactId,Sequence=sequence,
                    CompleteThroughSourceMs=through,OriginalEventHashes=delivered.Select(e=>IsolatedRubralSource.Hash(Bytes(e))).ToArray()});
                Require(IsolatedRubralSource.Hash(canonicalInputJson)==receipt.Sha256,"Original contact payload fingerprint changed.");
                _admissions.WriteLine(Bytes(new{receipt,replayed=replay,sourceSequences=_frames.Select(f=>f.Sequence).ToArray(),
                    selectedOriginalJson=selected.Select(e=>Bytes(e)).ToArray(),canonicalInputJson,
                    selectedOriginalIds=selected.Select(e=>e.MessageId).ToArray(),deliveredOriginalIds=delivered.Select(e=>e.MessageId).ToArray(),contactDisabled=_disabled}));
            }
            // Completed source time is conservative here: do not advance an
            // extra conduction delay merely because the receiver permits it.
            while((_sample+1)*ReceptorMotorSource.DefaultSampleMs<=through)
            {
                var state=Lane.AdvanceOneSample();_sample++;Record(state);
                if(_sample%_cadence!=0&&state.Cell.TimeMs!=DurationMs)continue;
                var batch=Lane.PublishCompleted();_outputBatches++;string before=Bytes(Lane.CaptureSource());
                bool blocked=false;
                if(_outputBatches%113==0||state.Cell.TimeMs==DurationMs)
                {
                    try{Lane.AdvanceOneSample();}catch(InvalidOperationException){blocked=true;}
                    Require(blocked&&Bytes(Lane.CaptureSource())==before,"Motor backpressure changed neuronal state.");_blocked++;
                }
                var receipt=_receiver.AcceptWithAcknowledgement(batch);bool replay=_outputBatches%37==0||state.Cell.TimeMs==DurationMs;
                if(replay){Require(receipt==_receiver.AcceptWithAcknowledgement(batch),"Motor replay changed receiver receipt.");_retries++;}
                Lane.Acknowledge(receipt);if(replay)Lane.Acknowledge(receipt);
                Require(Bytes(Lane.CaptureSource())==before,"Motor publication changed neuronal state.");
                var arrivals=_receiver.AdvanceTo(batch.CompleteThroughNeuralMs);Received+=arrivals.Count;
                _transport.WriteLine(Bytes(new{batch,canonicalBatchJson=Bytes(batch),receipt,replayed=replay,blocked,arrivals}));
            }
            _frames.Clear();
        }
        private void Record(ReceptorMotorSourceState state)
        {
            var c=state.Cell;
            var values=new[]{c.TimeMs}.Concat(c.MembraneState).Concat(c.Contacts.Select(r=>r.Value.OpenFraction))
                .Concat(new double[]{c.IntegrationSteps,c.DeliveredEvents,c.Crossings.Length});
            _trajectory.WriteLine(string.Join(',',values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));
        }
        internal void Finish()
        {
            Require(!_finished&&_frames.Count==0&&_sample==15000&&Lane.Unacknowledged().Count==0&&Lane.UnpublishedEmissions==0,"Incomplete neuronal branch.");
            var tail=_receiver.AdvanceTo(_receiver.CompleteThroughBodyMs);Received+=tail.Count;
            // Axonal conduction may carry a final discharge beyond the proven
            // source horizon. Retain it; never invent future neuronal silence
            // merely to empty the receiver queue.
            var pending=Lane.CaptureSource().Emissions.Where(e=>e.FiredAtNeuralMs+2.0625>_receiver.CompleteThroughBodyMs)
                .Select(e=>new{emission=e,arrivalAtBodyMs=e.FiredAtNeuralMs+2.0625}).ToArray();
            Save(Path.Combine(_folder,"final.json"),new{source=Lane.CaptureSource(),inputBatches=_sequence,outputBatches=_outputBatches,
                retries=_retries,backpressureChecks=_blocked,received=Received,tail,pendingBeyondHorizon=pending,
                receiverCompleteThroughMs=_receiver.CompleteThroughBodyMs,receiverPending=_receiver.PendingEvents,receiverDelivered=_receiver.DeliveredEvents});
            Require(_receiver.PendingEvents==pending.Length&&Received+pending.Length==Lane.CaptureSource().Emissions.Count,
                "Lost or duplicated motor events at the accounted horizon.");
            // File.OpenRead also specifies its own sharing mode. Flushing an
            // open writer does not release that handle on Windows; finalize all
            // output (including gzip trailers) before hashing completed files.
            Dispose();_finished=true;
        }
        public void Dispose(){_trajectory.Dispose();_transport.Dispose();_admissions.Dispose();}
    }
}
