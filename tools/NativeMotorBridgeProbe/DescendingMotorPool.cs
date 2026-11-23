#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record DescendingMotorCell(ReceptorMotorIdentity Identity,MotoneuronReferenceParameters Parameters,
    IReadOnlyList<MotorReceptorDefinition> Contacts,double InitialVoltageMv=-65,double SampleMs=.1,double MaximumStepMs=.003125);
internal sealed record DescendingMotorAdmission(VestibularProjectionPacket Original,IReadOnlyList<MotorReceptorBatchReceipt> Contacts);
internal sealed record DescendingMotorSample(string Instance,ReceptorMotorSourceState State);
internal sealed record DescendingMotorFrame(long Sequence,string CanonicalJson,string Sha256,IReadOnlyList<MotorAxonBatch> MotorBatches,bool MoreKnownWork);
internal sealed record DescendingMotorStatus(double KnownSourceThroughMs,IReadOnlyList<ReceptorMotorSourceState> Cells,
    DescendingMotorFrame? Pending,IReadOnlyList<MotorAxonAcknowledgement> BodyReceipts,string? Failure,string RetainedEvidenceJson);

// Owns detailed motor cells and their existing motor lanes. Input is still an
// explicitly declared projection; this owner cannot prove presynaptic firing or
// supply missing target anatomy. No generic nucleus event becomes a motor spike.
internal sealed class DescendingMotorPool
{
    internal const string Version="owned-descending-motor-pool-v1";
    private const int MaximumSamplesPerFrame=512,MaximumHeldPackets=4096,MaximumHeldEvents=65536;
    private static readonly JsonSerializerOptions EvidenceOptions=new(){NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
    private sealed class Cell(DescendingMotorCell definition,ReceptorMotorSource source)
    {internal readonly DescendingMotorCell Definition=definition;internal readonly ReceptorMotorSource Source=source;internal ReceptorMotorLane? Lane;}
    private sealed class Contact(Cell cell,MotorReceptorDefinition definition,Guid session,double epoch)
    {
        internal readonly Cell Cell=cell;internal readonly MotorReceptorDefinition Definition=definition;
        internal readonly MotorReceptorContact Validator=new(definition,session,epoch);internal long Admitted;
    }
    private sealed class Input(VestibularProjectionInput definition,Contact[] contacts)
    {
        internal readonly VestibularProjectionInput Definition=definition;internal readonly Contact[] Contacts=contacts;
        internal long Sequence,Ticks;internal VestibularProjectionPacket? Last;internal VestibularProjectionReceipt? Receipt;
        internal double Through=>Definition.EpochMs+Ticks*Definition.StepMs;
    }
    private sealed class Held(VestibularProjectionPacket packet)
    {internal readonly VestibularProjectionPacket Packet=packet;internal readonly List<(Contact Contact,long Through)> Ends=[];}
    private readonly Guid _session;
    private readonly double _epoch;
    private readonly string _instance;
    private readonly Cell[] _cells;
    private readonly Input[] _inputs;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly List<Held> _held=[];
    private readonly List<DescendingMotorAdmission> _admissions=[];
    private readonly List<DescendingMotorSample> _samples=[];
    private readonly List<MotorAxonBatch> _batches=[];
    private readonly List<MotorAxonAcknowledgement> _bodyReceipts=[];
    private bool _outputAttached;
    private long _sequence;
    private string? _failure,_lastReleased;
    private DescendingMotorFrame? _pending;
    internal string Fingerprint { get; }

    internal DescendingMotorPool(Guid session,string instance,IReadOnlyList<DescendingMotorCell> cells,
        IReadOnlyList<VestibularProjectionInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(cells);ArgumentNullException.ThrowIfNull(inputs);
        static bool Key(string? value)=>value is not null&&value.StartsWith("isolated-",StringComparison.Ordinal)&&value==value.Trim();
        if(session==Guid.Empty||!Key(instance)||cells.Count is <1 or >256||inputs.Count is <1 or >256||
            cells.Any(c=>c is null||c.Identity is null||!Key(c.Identity.Instance)||c.Contacts is null||c.Contacts.Count is <1 or >4096||c.Contacts.Any(d=>d is null))||
            cells.Select(c=>c.Identity.Instance).Distinct(StringComparer.Ordinal).Count()!=cells.Count||
            cells.Select(c=>c.Identity.Neuron).Distinct(StringComparer.Ordinal).Count()!=cells.Count||
            cells.Any(c=>c.Identity.Instance==instance)||inputs.Any(d=>d is null||!Key(d.SourceInstance)||d.SourceInstance==instance||
                d.SourceStructure is not (StructureId.VestibularNuclei or StructureId.ReticularFormation or StructureId.SpinalCordMotor)||
                !double.IsFinite(d.EpochMs)||d.EpochMs<0||!double.IsFinite(d.StepMs)||d.StepMs<=0||d.StepMs>4||!Digest(d.OutboxFingerprint)||
                d.Projections is null||d.Projections.Count is <1 or >64||d.Projections.Any(r=>r is null||r.Target!=StructureId.SpinalCordMotor||
                    r.SynapseId==Guid.Empty||!Enum.IsDefined(r.Neurotransmitter))||d.Projections.Select(r=>r.SynapseId).Distinct().Count()!=d.Projections.Count)||
            inputs.Select(d=>d.SourceInstance).Distinct(StringComparer.Ordinal).Count()!=inputs.Count||
            inputs.Select(d=>d.OutboxFingerprint).Distinct(StringComparer.Ordinal).Count()!=inputs.Count||
            cells.Any(c=>inputs.Any(i=>i.SourceInstance==c.Identity.Instance)))
            throw new ArgumentException("Declare distinct isolated source, pool and fresh motor-cell identities.");
        _epoch=inputs[0].EpochMs;
        if(inputs.Any(d=>d.EpochMs!=_epoch||!double.IsFinite(_epoch+d.StepMs)||_epoch+d.StepMs<=_epoch))
            throw new ArgumentException("All sources and freshly created motor cells require one representable original epoch.");
        _session=session;_instance=instance;
        var definitions=cells.OrderBy(c=>c.Identity.Instance,StringComparer.Ordinal).Select(c=>c with
            {Contacts=Array.AsReadOnly(c.Contacts.OrderBy(d=>d.ContactId).ToArray())}).ToArray();
        var contactDefinitions=definitions.SelectMany(c=>c.Contacts).ToArray();
        if(contactDefinitions.Length>4096||contactDefinitions.Select(c=>c.ContactId).Distinct().Count()!=contactDefinitions.Length)
            throw new ArgumentException("Each physical receptor needs a distinct declared identity; limit this pool to 4,096 contacts.");
        foreach(var contact in contactDefinitions)
        {
            contact.Validate();var input=inputs.SingleOrDefault(i=>i.SourceInstance==contact.SourceInstance&&i.SourceStructure==contact.SourceStructure);
            if(input is null||!input.Projections.Any(r=>r.SynapseId==contact.Projection&&r.Neurotransmitter==contact.Transmitter))
                throw new ArgumentException("Every receptor must belong to a declared source and original spinal projection.");
        }
        _cells=definitions.Select(d=>new Cell(d,new(d.Parameters,d.Contacts,session,d.Identity,d.MaximumStepMs,d.InitialVoltageMv,
            sampleMs:d.SampleMs,crossingInterpolation:MotoneuronCrossingInterpolation.CubicHermite,initialNeuralTimeMs:_epoch))).ToArray();
        var contacts=_cells.SelectMany(c=>c.Definition.Contacts.Select(d=>new Contact(c,d,session,_epoch))).ToArray();
        _inputs=inputs.OrderBy(d=>d.SourceInstance,StringComparer.Ordinal).Select(d=>new Input(d with
            {Projections=Array.AsReadOnly(d.Projections.OrderBy(r=>r.SynapseId).ToArray())},contacts.Where(c=>c.Definition.SourceInstance==d.SourceInstance).ToArray())).ToArray();
        if(_inputs.Any(i=>i.Contacts.Length==0))throw new ArgumentException("Do not declare a source whose output has no receiving contact.");
        Fingerprint=Hash(JsonSerializer.Serialize(new{Version,session,instance,Cells=_cells.Select(c=>new{c.Definition,c.Source.ConfigurationFingerprint}),
            Inputs=_inputs.Select(i=>i.Definition),MaximumSamplesPerFrame,MaximumHeldPackets,MaximumHeldEvents}));
    }

    internal VestibularProjectionReceipt Admit(VestibularProjectionPacket packet)
    {
        Healthy();ArgumentNullException.ThrowIfNull(packet);
        if(packet.TargetInstance!=_instance||packet.SourceSequence<=0||!Digest(packet.SourceFrameSha256)||packet.CanonicalJson is null||
            Hash(packet.CanonicalJson)!=packet.Sha256||packet.EventCount<0)
            throw new ArgumentException("Require the exact retained projection packet for this motor pool.");
        using var doc=JsonDocument.Parse(packet.CanonicalJson);var root=doc.RootElement;
        var input=_inputs.SingleOrDefault(i=>i.Definition.OutboxFingerprint==root.GetProperty("OwnerFingerprint").GetString());
        if(input is null||root.GetProperty("Version").GetString()!=VestibularProjectionOutbox.Version||
            root.GetProperty("SourceStructure").GetInt32()!=(int)input.Definition.SourceStructure||
            root.GetProperty("SourceInstance").GetString()!=input.Definition.SourceInstance||root.GetProperty("TargetInstance").GetString()!=_instance||
            root.GetProperty("SourceSequence").GetInt64()!=packet.SourceSequence||root.GetProperty("SourceFrameSha256").GetString()!=packet.SourceFrameSha256)
            throw new ArgumentException("Unknown or changed presynaptic source owner.");
        if(input.Last==packet)return input.Receipt!;
        if(_pending is not null)throw new InvalidOperationException("Retain and acknowledge motor output before new source work.");
        long start=root.GetProperty("StartingTick").GetInt64(),end=root.GetProperty("CompletedTick").GetInt64();
        double through=root.GetProperty("CompletedThroughMs").GetDouble();var events=root.GetProperty("Events");
        if(packet.SourceSequence!=checked(input.Sequence+1)||start!=input.Ticks||end<start||end-start>4096||
            !double.IsFinite(through)||through!=input.Definition.EpochMs+end*input.Definition.StepMs||events.GetArrayLength()!=packet.EventCount)
            throw new ArgumentException("Incomplete source sequence, clock or event accounting.");
        if(_held.Count>=MaximumHeldPackets||packet.EventCount>MaximumHeldEvents-_held.Sum(h=>h.Packet.EventCount))
            throw new InvalidOperationException("Motor input ownership capacity exceeded; no source packet consumed.");
        var selected=input.Contacts.ToDictionary(c=>c,_=>new List<SpikeMessage>());var ids=new HashSet<Guid>();
        foreach(var row in events.EnumerateArray())
        {
            string originalJson=row.GetProperty("OriginalEventJson").GetString()!,routedJson=row.GetProperty("RoutedEventJson").GetString()!;
            var original=JsonSerializer.Deserialize<SpikeMessage>(originalJson)??throw new ArgumentException("Missing original event.");
            var routed=JsonSerializer.Deserialize<SpikeMessage>(routedJson)??throw new ArgumentException("Missing routed event.");
            if(!SpikeProtocol.validate_spike(original,out _)||JsonSerializer.Serialize(original)!=originalJson||
                !SpikeProtocol.validate_spike(routed,out _)||JsonSerializer.Serialize(routed)!=routedJson||
                original.SourceStructure!=input.Definition.SourceStructure||original.SourceInstanceKey!=input.Definition.SourceInstance||
                original.IsSyntheticBackground||original.MessageId==Guid.Empty||original.SynapseId==Guid.Empty||
                original.ProjectionId==Guid.Empty||original.TimestampMs<=input.Through||original.TimestampMs>through)
                throw new ArgumentException("Original presynaptic event changed or is outside its newly completed interval.");
            Guid projection=row.GetProperty("ProjectionId").GetGuid();var route=input.Definition.Projections.SingleOrDefault(r=>r.SynapseId==projection)
                ??throw new ArgumentException("Undeclared spinal projection.");
            var expected=TickCoordinator.RewriteSpikeForRoute(original,route);
            if(expected.Neurotransmitter!=original.Neurotransmitter||expected.VesicleQuanta!=original.VesicleQuanta||
                expected.ReuptakeRate!=original.ReuptakeRate||expected.TargetNeuronId!=original.TargetNeuronId)
                throw new ArgumentException("Declare source anatomy; do not rewrite cellular target or chemistry during delivery.");
            expected.MessageId=StableNeuralIdentity.Create(VestibularProjectionOutbox.Version,original.MessageId,projection,_instance);
            if(JsonSerializer.Serialize(expected)!=routedJson||!ids.Add(routed.MessageId))throw new ArgumentException("Changed or duplicated original routed event.");
            var contacts=input.Contacts.Where(c=>c.Definition.SourceNeuron==routed.SourceNeuronId&&c.Definition.SourceSynapse==routed.SynapseId&&
                c.Definition.Projection==routed.ProjectionId&&c.Definition.TargetNeuron==routed.TargetNeuronId&&
                c.Definition.Transmitter==routed.Neurotransmitter&&c.Definition.IsFeedback==routed.IsFeedback).ToArray();
            if(contacts.Length==0)throw new ArgumentException("An original event has no declared motor receptor; retain the entire packet.");
            foreach(var contact in contacts){contact.Validator.DescribeOriginal(_session,routed);selected[contact].Add(routed);}
        }
        var held=new Held(packet);_held.Add(held);int admissionIndex=_admissions.Count;
        _admissions.Add(new(packet,Array.AsReadOnly(Array.Empty<MotorReceptorBatchReceipt>())));var receipts=new List<MotorReceptorBatchReceipt>();
        try
        {
            foreach(var contact in input.Contacts)
            {
                var originals=selected[contact].OrderBy(e=>e.TimestampMs).ThenBy(e=>e.MessageId).ToArray();
                var cell=contact.Cell;
                var receipt=cell.Lane is null?cell.Source.Admit(_session,contact.Definition.ContactId,packet.SourceSequence,through,originals):
                    cell.Lane.Admit(_session,contact.Definition.ContactId,packet.SourceSequence,through,originals);
                string canonical=JsonSerializer.Serialize(new{SessionId=_session,ContactId=contact.Definition.ContactId,Sequence=packet.SourceSequence,
                    CompleteThroughSourceMs=through,OriginalEventHashes=originals.Select(e=>Hash(JsonSerializer.Serialize(e))).ToArray()});
                receipts.Add(receipt);_admissions[admissionIndex]=new(packet,Array.AsReadOnly(receipts.ToArray()));
                contact.Admitted=checked(contact.Admitted+originals.Length);held.Ends.Add((contact,contact.Admitted));
                if(receipt.Sha256!=Hash(canonical)||receipt.Events!=originals.Length)throw new InvalidOperationException("Actual receptor ownership differs from its original input.");
            }
            input.Sequence=packet.SourceSequence;input.Ticks=end;input.Last=packet;
            return input.Receipt=new(_instance,packet.SourceSequence,packet.SourceFrameSha256,packet.Sha256,packet.EventCount);
        }
        catch(Exception e){_failure=e.ToString();throw;} // Retain actual partial admissions; never roll back or replay.
    }

    internal void AttachMotorOutput(MotorAxonAnatomy anatomy,MotorAxonClock clock)
    {
        Healthy();ArgumentNullException.ThrowIfNull(anatomy);ArgumentNullException.ThrowIfNull(clock);clock.Validate();
        if(_outputAttached||_pending is not null||clock.SessionId!=_session||anatomy.Contacts.Count!=_cells.Length)
            throw new ArgumentException("Attach each original motor cell once, outside pending output, on its actual neural epoch.");
        foreach(var cell in _cells)
        {
            var state=cell.Source.Capture();var identity=cell.Definition.Identity;
            if(state.Faulted||state.Cell.TimeMs!=clock.NeuralEpochMs||state.Cell.Crossings.Length!=0||state.Emissions.Count!=0||
                anatomy.Contacts.Count(c=>c.SourceInstanceKey==identity.Instance)!=1)
                throw new ArgumentException("Keep actual quiet prehistory; do not reset or relabel a motor cell at body attachment.");
            anatomy.Resolve(identity.Instance,identity.Neuron,identity.Projection);
        }
        try{foreach(var cell in _cells)cell.Lane=new(cell.Source,anatomy,clock,maximumBatches:1);_outputAttached=true;}
        catch(Exception e){_failure=e.ToString();throw;}
    }
    internal DescendingMotorFrame PublishCompleted()
    {
        Healthy();if(_pending is not null)return _pending;
        if(_admissions.Count==0&&!CanAdvance())throw new InvalidOperationException("No new owned input or completed motor interval.");
        try
        {
            int count=0;double through=KnownThrough();
            while(count<MaximumSamplesPerFrame)
            {
                bool advanced=false;
                foreach(var cell in _cells)
                {
                    var state=cell.Source.Capture();double next=_epoch+checked(state.CompletedSamples+1)*state.SampleMs;
                    if(next>through)continue;
                    var sample=cell.Lane is null?cell.Source.AdvanceOneSample():cell.Lane.AdvanceOneSample();
                    _samples.Add(new(cell.Definition.Identity.Instance,sample));count++;advanced=true;
                    if(count==MaximumSamplesPerFrame)break;
                }
                if(!advanced)break;
            }
            if(_outputAttached)foreach(var cell in _cells)
                if(cell.Source.Capture().Cell.TimeMs>cell.Lane!.PublishedThroughNeuralMs)_batches.Add(cell.Lane.PublishCompleted());
            bool more=CanAdvance();string json=EvidenceJson();
            return _pending=new(checked(++_sequence),json,Hash(json),Array.AsReadOnly(_batches.ToArray()),more);
        }
        catch(Exception e){_failure=e.ToString();throw;}
    }
    internal void AcceptBodyReceipt(MotorAxonAcknowledgement receipt)
    {
        Healthy();ArgumentNullException.ThrowIfNull(receipt);
        if(_pending is null)throw new InvalidOperationException("No retained motor output awaits body admission.");
        var batch=_batches.SingleOrDefault(b=>b.SourceInstanceKey==receipt.SourceInstanceKey);
        if(batch is null||MotorAxonBatchIntegrity.Receipt(batch,MotorAxonBatchIntegrity.Digest(batch))!=receipt)
            throw new ArgumentException("Acknowledge the exact output of the original motor neuron.");
        if(_bodyReceipts.Contains(receipt))return;
        _cells.Single(c=>c.Definition.Identity.Instance==receipt.SourceInstanceKey).Lane!.Acknowledge(receipt);_bodyReceipts.Add(receipt);
    }
    internal void Acknowledge(string frameSha256)
    {
        Healthy();if(_pending is null&&_lastReleased is not null&&frameSha256==_lastReleased)return;
        if(_pending is null||_pending.Sha256!=frameSha256)throw new ArgumentException("Acknowledge the exact retained motor-pool frame.");
        if(_bodyReceipts.Count!=_batches.Count)throw new InvalidOperationException("Every actual motor output needs its downstream body receipt.");
        _lastReleased=frameSha256;_pending=null;_samples.Clear();_admissions.Clear();_batches.Clear();_bodyReceipts.Clear();
        _held.RemoveAll(h=>h.Ends.All(end=>end.Contact.Cell.Source.Capture().Cell.InputLanes.Single(l=>l.ContactId==end.Contact.Definition.ContactId).DeliveredEvents>=end.Through));
    }
    internal DescendingMotorStatus Capture(){Owner();return new(KnownThrough(),Array.AsReadOnly(_cells.Select(c=>c.Source.Capture()).ToArray()),
        _pending,Array.AsReadOnly(_bodyReceipts.ToArray()),_failure,EvidenceJson());}
    private bool CanAdvance()=>_inputs.All(i=>i.Sequence>0)&&_cells.Any(c=>
        _epoch+checked(c.Source.Capture().CompletedSamples+1)*c.Definition.SampleMs<=KnownThrough());
    private double KnownThrough()=>_inputs.Any(i=>i.Sequence==0)?_epoch:_inputs.Min(i=>i.Through);
    private string EvidenceJson()=>JsonSerializer.Serialize(new{Version,Fingerprint,Session=_session,KnownSourceThroughMs=KnownThrough(),
        Sources=_inputs.Select(i=>new{i.Definition.SourceInstance,i.Sequence,i.Ticks,CompleteThroughMs=i.Through,i.Receipt}),
        RetainedPackets=_held.Select(h=>h.Packet),Admissions=_admissions,MotorSamples=_samples,MotorBatches=_batches,BodyReceipts=_bodyReceipts,
        Cells=_cells.Select(c=>c.Source.Capture()),OutputAttached=_outputAttached,Failure=_failure},EvidenceOptions);
    private static string Hash(string text)=>VestibularProjectionOutbox.Hash(text);
    private static bool Digest(string? text)=>text is {Length:64}&&text.All(char.IsAsciiHexDigit);
    private void Owner(){if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns the complete motor pool.");}
    private void Healthy(){Owner();if(_failure is not null)throw new InvalidOperationException("Preserve the failed motor pool; no reset or replay.");}
}
