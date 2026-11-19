#nullable enable
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record VestibularProjectionInput(string SourceInstance,string OutboxFingerprint,double EpochMs,double StepMs,
    IReadOnlyList<SynapticConnection> Projections,StructureId SourceStructure=StructureId.VestibularNuclei);
internal sealed record VestibularRecipientFrame(long Sequence,long CompletedTicks,double CompletedThroughMs,string CanonicalJson,string Sha256);
internal sealed record VestibularRecipientProgress(string SourceInstance,long Sequence,long CompletedTicks,double CompletedThroughMs,VestibularProjectionReceipt? LastReceipt);
internal sealed record VestibularRecipientStatus(long CompletedTicks,double CompletedThroughMs,double KnownSourceThroughMs,double PermittedThroughMs,
    IReadOnlyList<VestibularRecipientProgress> Sources,int WaitingEvents,VestibularRecipientFrame? Pending,string? Failure,
    string RetainedEvidenceJson,ProjectionReceiverEngineObservation Actual);

// Owns actual reticular/spinal neurons receiving the declared neuronal fibres.
// No live selector, source-rate substitution, new learning rule or motor command.
internal sealed class VestibularProjectionReceiver : IDisposable
{
    internal const string Version="owned-nucleus-projection-recipient-v2";
    private const int MaximumTicksPerFrame=4096,MaximumHeldPackets=4096;
    private static readonly object ConstructionGate=new();
    private sealed class Input(VestibularProjectionInput definition)
    {
        internal readonly VestibularProjectionInput Definition=definition;
        internal long Sequence,Ticks;
        internal VestibularProjectionPacket? LastPacket;
        internal VestibularProjectionReceipt? Receipt;
        internal int MinimumDelayMs;
        internal double Through=>Definition.EpochMs+Ticks*Definition.StepMs;
    }
    private sealed class Held(VestibularProjectionPacket packet,int remaining)
    {internal readonly VestibularProjectionPacket Packet=packet;internal int Remaining=remaining;}
    private sealed record Waiting(Held Owner,string MessageJson,Guid Id,double Arrival);
    private readonly Input[] _inputs;
    private readonly Dictionary<string,Input> _byFingerprint;
    private readonly StructureEngine _engine;
    private readonly string _instance;
    private readonly StructureId _structure;
    private readonly double _epoch,_step;
    private readonly int _capacity,_owner=Environment.CurrentManagedThreadId;
    private readonly NeuralSubstrateSupply _supply;
    private readonly bool _conductionLookahead;
    private readonly IsolatedDescendingProjection? _declaredOutput;
    private readonly List<Held> _held=[];
    private readonly List<Waiting> _waiting=[];
    private readonly List<string> _steps=[];
    private readonly List<RubralInputDelivery> _deliveries=[];
    private Waiting[] _dispatching=[];
    private long _ticks,_sequence,_admissions,_releasedAdmissions;
    private string? _failure,_lastReleased;
    private bool _disposed;
    private VestibularRecipientFrame? _pending;
    internal string Fingerprint { get; }
    internal StructureId Structure=>_structure;
    internal string Instance=>_instance;
    internal double EpochMs=>_epoch;
    internal double StepMs=>_step;
    internal bool CanAdvance { get { Healthy();return Time(checked(_ticks+1))<=PermittedThrough(); } }

    internal VestibularProjectionReceiver(StructureId structure,string instance,string stateDirectory,double receiverStepMs,
        IEnumerable<VestibularProjectionInput> inputs,NeuralSubstrateSupply supply,int capacity=4096,bool conductionLookahead=false,
        IsolatedDescendingProjection? declaredOutput=null)
    {
        ArgumentNullException.ThrowIfNull(inputs);ArgumentNullException.ThrowIfNull(supply);
        var definitions=inputs.ToArray();
        if(structure is not (StructureId.ReticularFormation or StructureId.SpinalCordMotor)||string.IsNullOrWhiteSpace(instance)||
            !instance.StartsWith("isolated-",StringComparison.Ordinal)||!Path.IsPathFullyQualified(stateDirectory)||
            !double.IsFinite(receiverStepMs)||receiverStepMs<=0||receiverStepMs>4||!supply.IsValid||capacity is <1 or >65536||
            definitions.Length is <1 or >256||definitions.Any(d=>d is null||
                d.SourceStructure is not (StructureId.VestibularNuclei or StructureId.ReticularFormation or StructureId.SpinalCordMotor)||
                string.IsNullOrWhiteSpace(d.SourceInstance)||
                !d.SourceInstance.StartsWith("isolated-",StringComparison.Ordinal)||!Digest(d.OutboxFingerprint)||
                !double.IsFinite(d.EpochMs)||d.EpochMs<0||!double.IsFinite(d.StepMs)||d.StepMs<=0||d.StepMs>4||
                d.Projections is null||d.Projections.Count is <1 or >64||d.Projections.Any(p=>p is null||p.Target!=structure||p.SynapseId==Guid.Empty||!Enum.IsDefined(p.Neurotransmitter)))||
            definitions.Select(d=>d.SourceInstance).Distinct(StringComparer.Ordinal).Count()!=definitions.Length||
            definitions.Select(d=>d.OutboxFingerprint).Distinct(StringComparer.Ordinal).Count()!=definitions.Length)
            throw new ArgumentException("Declare fresh actual recipients, explicit source identities, clocks, substrate supply and bounded queues.");
        _epoch=definitions[0].EpochMs;
        if(definitions.Any(d=>d.EpochMs!=_epoch||d.EpochMs+d.StepMs<=d.EpochMs)||!double.IsFinite(_epoch+receiverStepMs)||_epoch+receiverStepMs<=_epoch)
            throw new ArgumentException("All declared sources must share one representable original epoch.");
        _inputs=definitions.OrderBy(d=>d.SourceInstance,StringComparer.Ordinal).Select(d=>new Input(d with
        {Projections=Array.AsReadOnly(d.Projections.OrderBy(p=>p.SynapseId).ToArray())})).ToArray();
        foreach(var input in _inputs)
            if(input.Definition.Projections.Select(p=>p.SynapseId).Distinct().Count()!=input.Definition.Projections.Count)
                throw new ArgumentException("A source projection must have one declared identity.");
        _byFingerprint=_inputs.ToDictionary(i=>i.Definition.OutboxFingerprint,StringComparer.Ordinal);
        _instance=instance;_structure=structure;_step=receiverStepMs;_supply=supply;_capacity=capacity;_conductionLookahead=conductionLookahead;
        _declaredOutput=declaredOutput;
        lock(ConstructionGate)
        {
            if(File.Exists(stateDirectory)||Directory.Exists(stateDirectory)&&Directory.EnumerateFileSystemEntries(stateDirectory).Any())
                throw new InvalidOperationException("Preserve previous state; this recipient requires a fresh isolated directory.");
            string? directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),previous=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
            try
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",stateDirectory);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",instance);
                _engine=new(new(structure,"Izhikevich",structure==StructureId.ReticularFormation?"HomeostaticGain":"STDP",
                    "Isolated execution of the existing reticular/spinal receiving profile; physiology unqualified.",
                    structure==StructureId.ReticularFormation?new(2,10):new(1,4),capacity),null,
                    diagnosticDescendingProjection:declaredOutput);
            }
            finally{Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",directory);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",previous);}
        }
        _engine.RubralInputObserver=_deliveries.Add;
        foreach(var input in _inputs)input.MinimumDelayMs=_engine.MinimumIsolatedProjectionDelayMs(input.Definition.SourceStructure);
        Fingerprint=Hash(JsonSerializer.Serialize(new{Version,structure,instance,receiverStepMs,supply,capacity,conductionLookahead,
            Inputs=_inputs.Select(i=>new{i.Definition,i.MinimumDelayMs}),MaximumTicksPerFrame,MaximumHeldPackets}));
        if(declaredOutput is not null)Fingerprint=Hash(JsonSerializer.Serialize(new
            {Version="owned-nucleus-declared-output-v1",OriginalOwner=Fingerprint,OutputAnatomy=declaredOutput.Fingerprint}));
    }

    internal IReadOnlyList<DescendingPrimaryOutput> DescribePrimaryMotorOutputs()
    {
        Healthy();
        if(_declaredOutput is null)throw new InvalidOperationException("This receiving population has no declared motor output anatomy.");
        return _engine.DescribeDeclaredDescendingOutputs();
    }

    internal VestibularProjectionReceipt Admit(VestibularProjectionPacket packet)
    {
        Healthy();ArgumentNullException.ThrowIfNull(packet);
        if(packet.TargetInstance!=_instance||packet.SourceSequence<1||!Digest(packet.SourceFrameSha256)||
            packet.CanonicalJson is null||Hash(packet.CanonicalJson)!=packet.Sha256||packet.EventCount<0)
            throw new ArgumentException("Require an exact immutable packet for this receiving owner.");
        using var doc=JsonDocument.Parse(packet.CanonicalJson);var root=doc.RootElement;
        if(root.GetProperty("Version").GetString()!=VestibularProjectionOutbox.Version||
            !_byFingerprint.TryGetValue(root.GetProperty("OwnerFingerprint").GetString()!,out var input)||
            root.GetProperty("SourceStructure").GetInt32()!=(int)input.Definition.SourceStructure||
            root.GetProperty("SourceInstance").GetString()!=input.Definition.SourceInstance||root.GetProperty("TargetInstance").GetString()!=_instance||
            root.GetProperty("SourceSequence").GetInt64()!=packet.SourceSequence||root.GetProperty("SourceFrameSha256").GetString()!=packet.SourceFrameSha256)
            throw new ArgumentException("Unknown or inconsistent original source owner.");
        if(input.LastPacket==packet)return input.Receipt!;
        if(_pending is not null)throw new InvalidOperationException("Retain and acknowledge receiving output before admitting more source work.");
        long start=root.GetProperty("StartingTick").GetInt64(),end=root.GetProperty("CompletedTick").GetInt64();
        double through=root.GetProperty("CompletedThroughMs").GetDouble();var events=root.GetProperty("Events");
        if(packet.SourceSequence!=checked(input.Sequence+1)||start!=input.Ticks||end<start||end-start>4096||
            !double.IsFinite(through)||through!=input.Definition.EpochMs+end*input.Definition.StepMs||events.GetArrayLength()!=packet.EventCount)
            throw new ArgumentException("Source sequence, original clock or event accounting differs.");
        if(_held.Count>=MaximumHeldPackets||packet.EventCount>_capacity-_waiting.Count)
            throw new InvalidOperationException("Recipient ownership capacity exceeded; no packet was consumed.");
        var held=new Held(packet,packet.EventCount);var candidate=new List<Waiting>();var ids=new HashSet<Guid>();
        foreach(var row in events.EnumerateArray())
        {
            string originalJson=row.GetProperty("OriginalEventJson").GetString()!,routedJson=row.GetProperty("RoutedEventJson").GetString()!;
            var original=JsonSerializer.Deserialize<SpikeMessage>(originalJson)??throw new ArgumentException("Missing original event.");
            var routed=JsonSerializer.Deserialize<SpikeMessage>(routedJson)??throw new ArgumentException("Missing routed event.");
            if(!SpikeProtocol.validate_spike(original,out _)||JsonSerializer.Serialize(original)!=originalJson||
                !SpikeProtocol.validate_spike(routed,out _)||JsonSerializer.Serialize(routed)!=routedJson||
                original.SourceStructure!=input.Definition.SourceStructure||original.SourceInstanceKey!=input.Definition.SourceInstance||
                original.ProjectionId==Guid.Empty||original.IsSyntheticBackground||original.TimestampMs<=input.Through||original.TimestampMs>through)
                throw new ArgumentException("Original spike changed, was synthesized or lies outside its completed source interval.");
            Guid projection=row.GetProperty("ProjectionId").GetGuid();
            var route=input.Definition.Projections.SingleOrDefault(p=>p.SynapseId==projection)??throw new ArgumentException("Undeclared recipient projection.");
            var expected=TickCoordinator.RewriteSpikeForRoute(original,route);
            if(expected.Neurotransmitter!=original.Neurotransmitter||expected.VesicleQuanta!=original.VesicleQuanta||
                expected.ReuptakeRate!=original.ReuptakeRate||expected.TargetNeuronId!=original.TargetNeuronId)
                throw new ArgumentException("The declared route would alter original chemistry or identity.");
            expected.MessageId=StableNeuralIdentity.Create(VestibularProjectionOutbox.Version,original.MessageId,projection,_instance);
            if(JsonSerializer.Serialize(expected)!=routedJson||!ids.Add(routed.MessageId)||_waiting.Any(w=>w.Id==routed.MessageId))
                throw new ArgumentException("Routed event differs or duplicates an owned event.");
            double arrival=_engine.DescribeIsolatedProjectionArrival(routed);
            if(arrival<routed.TimestampMs+input.MinimumDelayMs)throw new InvalidOperationException("Existing conduction violates its declared lower bound.");
            if(arrival<=Time(_ticks))throw new ArgumentException("A completed recipient cannot accept an event in its elapsed past.");
            candidate.Add(new(held,routedJson,routed.MessageId,arrival));
        }
        var receipt=new VestibularProjectionReceipt(_instance,packet.SourceSequence,packet.SourceFrameSha256,packet.Sha256,packet.EventCount);
        _held.Add(held);_waiting.AddRange(candidate);input.Sequence=packet.SourceSequence;input.Ticks=end;
        input.LastPacket=packet;input.Receipt=receipt;_admissions++;
        return receipt;
    }

    internal VestibularRecipientFrame PublishCompleted()
    {
        Healthy();if(_pending is not null)return _pending;
        double horizon=PermittedThrough();
        if(_admissions==_releasedAdmissions&&Time(checked(_ticks+1))>horizon)
            throw new InvalidOperationException("No new source ownership or known neuronal interval to publish.");
        try
        {
            int count=0;
            while(count++<MaximumTicksPerFrame)
            {
                long tick=checked(_ticks+1);double through=Time(tick),start=Time(_ticks);
                if(!double.IsFinite(through)||through<=start)throw new InvalidOperationException("Recipient clock is no longer representable.");
                if(through>horizon)break;
                // No unknown source event can arrive in this interval. Admit
                // only due arrivals in a fixed order, so packet partitions do
                // not change the engine heap or equal-time contact order.
                _dispatching=_waiting.Where(w=>w.Arrival<=through).OrderBy(w=>w.Arrival).ThenBy(w=>w.MessageJson,StringComparer.Ordinal).ToArray();
                int deliveryStart=_deliveries.Count;
                if(_dispatching.Length>0)_engine.EnqueueSpikeBatchAsync(_dispatching.Select(w=>JsonSerializer.Deserialize<SpikeMessage>(w.MessageJson)!).ToArray()).GetAwaiter().GetResult();
                var result=_engine.ProcessStepAsync(new(tick,through,through-start,new(),new Dictionary<BrainRhythm,double>(),0,NeuralSubstrates:_supply),0).GetAwaiter().GetResult();
                _steps.Add(JsonSerializer.Serialize(result));
                var delivered=_deliveries.Skip(deliveryStart).ToArray();
                if(result.Ack.StructureId!=_structure||result.Ack.Tick!=tick||delivered.Length!=_dispatching.Length)
                    throw new InvalidOperationException("Actual receiving step or contact delivery is incomplete.");
                foreach(var d in delivered)
                {
                    var w=_dispatching.SingleOrDefault(w=>w.Id==d.OriginalMessageId);
                    if(w is null||d.OriginalJson!=w.MessageJson||d.ArrivalAtMs!=w.Arrival||d.AppliedAtMs!=w.Arrival||
                        d.ReceivingContactId==Guid.Empty||d.ActualTargetIndex<0||string.IsNullOrWhiteSpace(d.ActualTarget))
                        throw new InvalidOperationException("Original route did not reach its actual receiving contact at its conduction boundary.");
                }
                if(delivered.Select(d=>d.OriginalMessageId).Distinct().Count()!=delivered.Length)
                    throw new InvalidOperationException("An original contact event was integrated twice.");
                foreach(var w in _dispatching){_waiting.Remove(w);w.Owner.Remaining--;}
                _dispatching=[];_ticks=tick;
            }
            string json=EvidenceJson();return _pending=new(checked(++_sequence),_ticks,Time(_ticks),json,Hash(json));
        }
        catch(Exception e){_failure=e.ToString();throw;} // Actual engine prefix and remaining packets stay owned; no rollback.
    }
    internal void Acknowledge(string frameSha256)
    {
        Healthy();if(_pending is null&&_lastReleased is not null&&_lastReleased==frameSha256)return;
        if(_pending is null||frameSha256!=_pending.Sha256)throw new ArgumentException("Acknowledge the exact retained neuronal output.");
        _lastReleased=frameSha256;_pending=null;_releasedAdmissions=_admissions;_steps.Clear();_deliveries.Clear();
        _held.RemoveAll(h=>h.Remaining==0);
    }
    internal VestibularRecipientStatus Capture()
    {Owner();return new(_ticks,Time(_ticks),_inputs.Min(i=>i.Through),PermittedThrough(),Progress(),_waiting.Count,_pending,_failure,EvidenceJson(),_engine.ObserveIsolatedProjectionRecipient());}
    private IReadOnlyList<VestibularRecipientProgress> Progress()=>Array.AsReadOnly(_inputs.Select(i=>
        new VestibularRecipientProgress(i.Definition.SourceInstance,i.Sequence,i.Ticks,i.Through,i.Receipt)).ToArray());
    private string EvidenceJson()=>JsonSerializer.Serialize(new{Version,Fingerprint,SourceStructure=_structure,SourceInstance=_instance,
        CompletedTicks=_ticks,CompletedThroughMs=Time(_ticks),PermittedThroughMs=PermittedThrough(),ConductionLookahead=_conductionLookahead,
        Sources=Progress(),OwnedPackets=_held.Select(h=>new{h.Packet,h.Remaining}),
        WaitingEvents=_waiting.Select(w=>new{PacketSha256=w.Owner.Packet.Sha256,w.MessageJson,w.Arrival}),
        DispatchingEvents=_dispatching.Select(w=>new{PacketSha256=w.Owner.Packet.Sha256,w.MessageJson,w.Arrival}),
        ReceiverStepJson=_steps,ActualDeliveries=_deliveries,Failure=_failure});
    private double Time(long tick)=>_epoch+tick*_step;
    private double PermittedThrough()
    {
        // A future event is strictly later than its source's completed time.
        // Addition may round two adjacent source times to the same arrival at
        // a binary exponent boundary. Stay below the earliest representable
        // unknown arrival, rather than assuming strictness survives addition.
        double result=_inputs.Min(i=>_conductionLookahead?
            Math.BitDecrement(Math.BitIncrement(i.Through)+i.MinimumDelayMs):i.Through);
        if(!double.IsFinite(result))throw new InvalidOperationException("Known arrival horizon is not representable.");
        return result;
    }
    private static bool Digest(string? value)=>value is {Length:64}&&value.All(c=>char.IsAsciiHexDigit(c));
    private static string Hash(string text)=>VestibularProjectionOutbox.Hash(text);
    private void Owner(){if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns the receiving circuit.");if(_disposed)throw new ObjectDisposedException(nameof(VestibularProjectionReceiver));}
    private void Healthy(){Owner();if(_failure is not null)throw new InvalidOperationException("Preserve the failed receiving circuit; no replay or automatic reset.");}
    public void Dispose(){if(_disposed)return;Owner();_engine.Dispose();_disposed=true;}
}
