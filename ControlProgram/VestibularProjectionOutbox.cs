using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record VestibularProjectionPacket(string TargetInstance,long SourceSequence,
    string SourceFrameSha256,string CanonicalJson,string Sha256,int EventCount);
internal sealed record VestibularProjectionFrame(long Sequence,string SourceFrameSha256,
    long CompletedTick,double CompletedThroughMs,IReadOnlyList<VestibularProjectionPacket> Packets,string Sha256);
internal sealed record VestibularProjectionReceipt(string TargetInstance,long SourceSequence,
    string SourceFrameSha256,string PacketSha256,int AcceptedEvents);
internal sealed record VestibularProjectionStatus(long ReleasedSequence,long ReleasedTick,double ReleasedThroughMs,
    VestibularProjectionFrame? Pending,IReadOnlyList<VestibularProjectionReceipt> Receipts,string? LastReleasedSha256);

// An isolated hand-off of completed neuronal output, not a firing-rate decoder.
// The producer adapter must establish that these are its actual completed steps.
// A packet receipt claims durable/owned admission, not target integration or firing.
internal sealed class VestibularProjectionOutbox
{
    internal const string Version="observed-nucleus-projection-outbox-v2";
    private const int MaximumSteps=4096,MaximumEvents=65536;
    private readonly string _instance;
    private readonly StructureId _structure;
    private readonly double _epoch,_step;
    private readonly (SynapticConnection Route,ServiceInstance[] Targets)[] _routes;
    private readonly string[] _targets;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly Dictionary<string,VestibularProjectionReceipt> _receipts=new(StringComparer.Ordinal);
    private long _releasedSequence,_releasedTick;
    private VestibularProjectionFrame? _pending;
    private string? _lastReleased,_inputJson;
    internal string Fingerprint { get; }

    internal VestibularProjectionOutbox(string sourceInstance,string hemisphere,double epochMs,double stepMs,
        IReadOnlyList<SynapticConnection> routes,IReadOnlyList<ServiceInstance> recipients,
        StructureId sourceStructure=StructureId.VestibularNuclei)
    {
        ArgumentNullException.ThrowIfNull(routes);ArgumentNullException.ThrowIfNull(recipients);
        if(sourceStructure is not (StructureId.VestibularNuclei or StructureId.ReticularFormation or StructureId.SpinalCordMotor)||
            sourceStructure==StructureId.SpinalCordMotor&&routes.Any(r=>r is not null&&r.Target!=StructureId.ReticularFormation)||
            string.IsNullOrWhiteSpace(sourceInstance)||!sourceInstance.StartsWith("isolated-",StringComparison.Ordinal)||
            hemisphere is not ("L" or "R" or "M")||!double.IsFinite(epochMs)||epochMs<0||!double.IsFinite(stepMs)||
            stepMs<=0||stepMs>4||!double.IsFinite(epochMs+stepMs)||epochMs+stepMs<=epochMs||routes.Count is <1 or >64||
            routes.Any(r=>r is null||r.SynapseId==Guid.Empty||!Enum.IsDefined(r.Target)||!Enum.IsDefined(r.Neurotransmitter)||string.IsNullOrWhiteSpace(r.ProjectionType))||
            routes.Select(r=>r.SynapseId).Distinct().Count()!=routes.Count||recipients.Count is <1 or >256||
            recipients.Any(r=>r is null||!Enum.IsDefined(r.StructureId)||string.IsNullOrWhiteSpace(r.InstanceKey)||
                !r.InstanceKey.StartsWith("isolated-",StringComparison.Ordinal)||r.Hemisphere is not ("L" or "R" or "M"))||
            recipients.Select(r=>r.InstanceKey).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=recipients.Count)
            throw new ArgumentException("Declare isolated neuronal identity, one representable clock, distinct projections and explicit recipients.");
        _instance=sourceInstance;_structure=sourceStructure;_epoch=epochMs;_step=stepMs;
        // Canonical ordering also makes the coordinator's final fallback stable.
        var instances=recipients.OrderBy(r=>r.InstanceKey,StringComparer.Ordinal).GroupBy(r=>r.StructureId)
            .ToDictionary(g=>g.Key,g=>g.ToList());
        _routes=routes.OrderBy(r=>r.Target).ThenBy(r=>r.SynapseId).Select(r=>(Route:r,
            Targets:TickCoordinator.ResolveTargetInstances(r.Target,hemisphere,instances,sourceStructure,r.ProjectionType)
                .OrderBy(t=>t.InstanceKey,StringComparer.Ordinal).ToArray())).ToArray();
        if(_routes.Any(r=>r.Targets.Length==0))throw new ArgumentException("Every declared outgoing pathway needs a recipient; no silent route loss.");
        _targets=_routes.SelectMany(r=>r.Targets).Select(r=>r.InstanceKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Fingerprint=Hash(JsonSerializer.Serialize(new{Version,sourceStructure,sourceInstance,hemisphere,epochMs,stepMs,
            Routes=_routes.Select(r=>new{r.Route,Targets=r.Targets.Select(t=>new{t.StructureId,t.InstanceKey,t.Hemisphere})}),MaximumSteps,MaximumEvents}));
    }

    internal VestibularProjectionFrame Stage(long sequence,string originalFrameJson,string originalFrameSha256,
        long completedTick,double completedThroughMs,IReadOnlyList<string> originalStepJson)
    {
        RequireOwner();ArgumentNullException.ThrowIfNull(originalStepJson);
        if(originalFrameJson is null||Hash(originalFrameJson)!=originalFrameSha256)
            throw new ArgumentException("Retain the exact original source frame and its digest.");
        string input=JsonSerializer.Serialize(new{sequence,originalFrameJson,originalFrameSha256,completedTick,completedThroughMs,originalStepJson});
        if(_pending is not null)
        {
            if(input==_inputJson)return _pending;
            throw new InvalidOperationException("All recipients must acknowledge the pending frame before another frame can be staged.");
        }
        if(sequence!=checked(_releasedSequence+1)||completedTick<_releasedTick||completedTick-_releasedTick>MaximumSteps||
            originalStepJson.Count!=completedTick-_releasedTick||!double.IsFinite(completedThroughMs)||completedThroughMs!=Time(completedTick))
            throw new ArgumentException("Require contiguous original completed ticks on the declared neuronal grid.");
        var byTarget=_targets.ToDictionary(t=>t,_=>new List<object>(),StringComparer.Ordinal);
        var seen=new HashSet<Guid>();int routedCount=0;
        for(int i=0;i<originalStepJson.Count;i++)
        {
            long tick=checked(_releasedTick+i+1);double start=Time(tick-1),end=Time(tick);
            if(end<=start)throw new InvalidOperationException("Neuronal clock is no longer representable.");
            var step=JsonSerializer.Deserialize<StructureStepResult>(originalStepJson[i])??throw new ArgumentException("Missing original receiving step.");
            if(step.Ack is null||step.Ack.StructureId!=_structure||step.Ack.Tick!=tick||step.OutboundSpikes is null)
                throw new ArgumentException("Missing, reordered or foreign completed receiving step.");
            foreach(var spike in step.OutboundSpikes)
            {
                if(spike is null)throw new ArgumentException("Missing original neuronal event.");
                string original=JsonSerializer.Serialize(spike);
                if(!SpikeProtocol.validate_spike(spike,out _)||JsonSerializer.Serialize(spike)!=original||spike.MessageId==Guid.Empty||
                    spike.SynapseId==Guid.Empty||spike.ProjectionId==Guid.Empty||spike.SourceStructure!=_structure||
                    spike.SourceInstanceKey!=_instance||string.IsNullOrWhiteSpace(spike.SourceNeuronId)||spike.IsSyntheticBackground||
                    spike.TimestampMs<=start||spike.TimestampMs>end||!seen.Add(spike.MessageId))
                    throw new ArgumentException("Require unique original neuronal events within their completed integration step.");
                // The source follows every declared collateral in the
                // existing connectome. This owner adds no efference gain or rate.
                foreach(var route in TickCoordinator.ResolveRoutes(_routes.Select(r=>r.Route).ToArray(),spike))
                {
                    var binding=_routes.Single(r=>r.Route==route);
                    foreach(var target in binding.Targets)
                    {
                        if(++routedCount>MaximumEvents)throw new InvalidOperationException("Projection frame exceeds its declared bound; original source remains held.");
                        var routed=TickCoordinator.RewriteSpikeForRoute(spike,route);
                        if(routed.Neurotransmitter!=spike.Neurotransmitter||routed.VesicleQuanta!=spike.VesicleQuanta||
                            routed.ReuptakeRate!=spike.ReuptakeRate||routed.SourceNeuronId!=spike.SourceNeuronId||routed.TargetNeuronId!=spike.TargetNeuronId)
                            throw new InvalidOperationException("This route would alter original cellular chemistry or identity; review anatomy rather than coercing it.");
                        // Different collaterals need different delivery identities
                        // even when they converge on the same receiving instance.
                        // The original neuronal event remains separately retained.
                        routed.MessageId=StableNeuralIdentity.Create(Version,spike.MessageId,route.SynapseId,target.InstanceKey);
                        byTarget[target.InstanceKey].Add(new{OriginalEventJson=original,ProjectionId=route.SynapseId,
                            RoutedEventJson=JsonSerializer.Serialize(routed)});
                    }
                }
            }
        }
        var packets=_targets.Select(target=>
        {
            string json=JsonSerializer.Serialize(new{Version,OwnerFingerprint=Fingerprint,SourceStructure=_structure,SourceInstance=_instance,
                TargetInstance=target,SourceSequence=sequence,SourceFrameSha256=originalFrameSha256,
                StartingTick=_releasedTick,CompletedTick=completedTick,CompletedThroughMs=completedThroughMs,
                Events=byTarget[target]});
            return new VestibularProjectionPacket(target,sequence,originalFrameSha256,json,Hash(json),byTarget[target].Count);
        }).ToArray();
        string hash=Hash(JsonSerializer.Serialize(new{Version,Fingerprint,sequence,originalFrameSha256,
            completedTick,completedThroughMs,Packets=packets}));
        _inputJson=input;
        return _pending=new(sequence,originalFrameSha256,completedTick,completedThroughMs,Array.AsReadOnly(packets),hash);
    }

    internal void Accept(VestibularProjectionReceipt receipt)
    {
        RequireOwner();ArgumentNullException.ThrowIfNull(receipt);
        var frame=_pending??throw new InvalidOperationException("No outgoing frame awaits admission.");
        var packet=frame.Packets.SingleOrDefault(p=>p.TargetInstance==receipt.TargetInstance);
        if(packet is null||receipt.SourceSequence!=frame.Sequence||receipt.SourceFrameSha256!=frame.SourceFrameSha256||
            receipt.PacketSha256!=packet.Sha256||receipt.AcceptedEvents!=packet.EventCount)
            throw new ArgumentException("A recipient must acknowledge its entire exact packet, including a quiet horizon.");
        if(_receipts.TryGetValue(receipt.TargetInstance,out var previous))
        {if(previous!=receipt)throw new ArgumentException("Conflicting recipient receipt.");return;}
        _receipts.Add(receipt.TargetInstance,receipt);
    }
    internal void RequireAllReceipts(string frameSha256)
    {
        RequireOwner();
        if(_pending is null||_pending.Sha256!=frameSha256||_receipts.Count!=_pending.Packets.Count)
            throw new InvalidOperationException("Every declared recipient must own its exact output before releasing the source.");
    }
    internal void Release(string frameSha256)
    {
        RequireOwner();
        if(_pending is null&&_lastReleased is not null&&_lastReleased==frameSha256)return;
        RequireAllReceipts(frameSha256);var frame=_pending!;
        _releasedSequence=frame.Sequence;_releasedTick=frame.CompletedTick;_lastReleased=frame.Sha256;
        _pending=null;_inputJson=null;_receipts.Clear();
    }
    internal VestibularProjectionStatus Capture()
    {RequireOwner();return new(_releasedSequence,_releasedTick,Time(_releasedTick),_pending,
        Array.AsReadOnly(_receipts.Values.OrderBy(r=>r.TargetInstance,StringComparer.Ordinal).ToArray()),_lastReleased);}
    internal static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private double Time(long tick)=>_epoch+tick*_step;
    private void RequireOwner()
    {if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns the original source output and its receipts.");}
}
