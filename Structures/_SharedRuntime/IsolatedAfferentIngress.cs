using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record AfferentDeliveryEvidence(Guid OriginalEventId,Guid DeliveryId,Guid ContactId,
    Guid ProjectionId,int TargetCell,double SourceTimeMs,double ScheduledArrivalMs);
internal sealed record AfferentIngressReceipt(string PacketFingerprint,long SourceCompletedStep,double SourceCompletedMs,
    double TargetSafeThroughMs,IReadOnlyList<AfferentDeliveryEvidence> Deliveries);

// Explicit, fresh isolated vestibular nucleus only. This is an in-process
// coupling boundary, not a service configuration switch or calibrated anatomy.
internal sealed class IsolatedAfferentIngress
{
    internal const string Version="isolated-observed-afferent-ingress-v1";
    internal ObservedAfferentSource Source { get; }
    internal IReadOnlyList<ObservedAfferentRoute> Routes { get; }
    internal string Fingerprint { get; }
    internal long CompletedStep { get; private set; }
    internal long LastOrdinal { get; private set; }
    internal AfferentIngressReceipt? LastReceipt { get; private set; }
    internal double SafeThroughMs=>Source.TimeAt(CompletedStep)+Routes.Min(r=>r.RemainingDelayMs);
    private bool _bound;

    internal IsolatedAfferentIngress(ObservedAfferentSource source,IEnumerable<ObservedAfferentRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(routes);source.Validate();
        var copied=routes.ToArray();
        foreach(var r in copied){ArgumentNullException.ThrowIfNull(r);r.Validate(int.MaxValue);}
        copied=copied.OrderBy(r=>r.ProjectionId).ThenBy(r=>r.TargetCell).ToArray();
        if(copied.Length==0||copied.Length>256||copied.Select(r=>(r.ProjectionId,r.TargetCell)).Distinct().Count()!=copied.Length)
            throw new ArgumentException("Declare a bounded, distinct set of receiving synaptic contacts.");
        Source=source;Routes=Array.AsReadOnly(copied);
        if(!double.IsFinite(SafeThroughMs))throw new ArgumentException("Initial conduction horizon is unrepresentable.");
        Fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Version,source,routes=copied })));
    }
    internal void Bind(StructureProfile profile,string instance,int neuronCount)
    {
        string? path=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        if(_bound||profile.StructureId!=StructureId.VestibularNuclei||!instance.StartsWith("isolated-",StringComparison.Ordinal)||
            string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||File.Exists(path)||
            (Directory.Exists(path)&&Directory.EnumerateFileSystemEntries(path).Any())||Routes.Any(r=>r.TargetInstanceKey!=instance))
            throw new InvalidOperationException("Observed sensory ingress requires its own fresh, explicitly addressed isolated vestibular nucleus.");
        foreach(var r in Routes)r.Validate(neuronCount);_bound=true;
    }
    internal bool IsRetry(ObservedAfferentPacket packet)=>LastReceipt?.PacketFingerprint==packet.Fingerprint;
    internal void ValidateNext(ObservedAfferentPacket packet)
    {
        if(packet.Source!=Source||packet.StartingStep!=CompletedStep)
            throw new ArgumentException("Wrong source/epoch or a skipped, stale or changed source interval.");
        long ordinal=LastOrdinal;
        foreach(var e in packet.Events)if(e.Ordinal!=checked(++ordinal))throw new ArgumentException("Missing or repeated source event ordinal.");
        if(!double.IsFinite(packet.EndMs+Routes.Min(r=>r.RemainingDelayMs)))throw new ArgumentException("Conduction horizon overflow.");
    }
    internal void Commit(ObservedAfferentPacket packet,AfferentIngressReceipt receipt)
    {
        CompletedStep=packet.CompletedStep;if(packet.Events.Count>0)LastOrdinal=packet.Events[^1].Ordinal;LastReceipt=receipt;
    }
    internal int Resolve(SpikeMessage message)
    {
        if(message.SourceStructure!=StructureId.VestibularAfferents||message.TargetStructure!=StructureId.VestibularNuclei||
            message.SourceInstanceKey!=Source.InstanceKey||message.SourceNeuronId!=Source.NeuronId||
            message.Neurotransmitter!=NTEnum.GLUTAMATE||message.IsFeedback||message.IsSyntheticBackground)
            throw new InvalidOperationException("Undeclared source reached the isolated sensory queue.");
        foreach(var r in Routes)
            if(message.ProjectionId==r.ProjectionId&&message.TargetNeuronId=="observed-afferent-target-"+r.TargetCell.ToString(System.Globalization.CultureInfo.InvariantCulture)&&
                message.SynapseId==StableNeuralIdentity.Contact(message,r.TargetInstanceKey,r.TargetCell))return r.TargetCell;
        throw new InvalidOperationException("Undeclared receiving contact; no topographic remapping is permitted.");
    }
    internal void ValidateTick(TickSignal tick,double lastTime)
    {
        double start=double.IsFinite(lastTime)?lastTime:Source.EpochMs;
        if(!double.IsFinite(tick.TimestampMs)||tick.TimestampMs<=start||tick.TimestampMs>SafeThroughMs||
            tick.TickDurationMs!=tick.TimestampMs-start||tick.TickDurationMs>100)
            throw new InvalidOperationException("Target must advance contiguously within the completed sensory horizon plus declared delay.");
    }
}

public sealed partial class StructureEngine
{
    private readonly IsolatedAfferentIngress? _diagnosticAfferentIngress;

    internal ValueTask<AfferentIngressReceipt> EnqueueObservedAfferentPacketAsync(ObservedAfferentPacket packet,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(packet);cancellationToken.ThrowIfCancellationRequested();
        // Same lock order as ProcessTick: state, then queue. No biological
        // advancement is performed here. Admission and progress commit together.
        lock(_stateGate)
        lock(_inboundQueueGate)
        {
            var ingress=_diagnosticAfferentIngress??throw new InvalidOperationException("This engine has no isolated observed sensory owner.");
            if(ingress.IsRetry(packet))return ValueTask.FromResult(ingress.LastReceipt!);
            ingress.ValidateNext(packet);
            int count=checked(packet.Events.Count*ingress.Routes.Count);
            int capacity=Math.Clamp(_profile.MaxInboundQueueDepth,1,65_536);
            if(count>StructureTransportLimits.MaxSpikeBatchCount||_feedForward.Count+_feedback.Count+count>capacity)
                throw new StructureIngressOverloadException(_profile.StructureId,capacity);
            var pending=new List<SpikeEnvelope>(count);var evidence=new List<AfferentDeliveryEvidence>(count);
            foreach(var e in packet.Events)
            foreach(var route in ingress.Routes)
            {
                var message=route.Message(ingress.Source,e);ValidateInboundSpike(message);
                double arrival=e.CrossingTimeMs+route.RemainingDelayMs;
                if(!double.IsFinite(arrival)||arrival<e.CrossingTimeMs||(route.RemainingDelayMs>0&&arrival==e.CrossingTimeMs)||
                    (double.IsFinite(_lastBiologicalTimestampMs)&&arrival<_lastBiologicalTimestampMs))
                    throw new InvalidOperationException("Unrepresentable or late sensory arrival; target cannot rewind.");
                if(_recentMessageIds.Contains(message.MessageId))throw new InvalidOperationException("New source interval reused a delivered event.");
                if(ingress.Resolve(message)!=route.TargetCell)throw new InvalidOperationException("Recipient identity changed.");
                pending.Add(new(message,arrival));evidence.Add(new(e.MessageId,message.MessageId,message.SynapseId,
                    message.ProjectionId,route.TargetCell,e.CrossingTimeMs,arrival));
            }
            var receipt=new AfferentIngressReceipt(packet.Fingerprint,packet.CompletedStep,packet.EndMs,
                packet.EndMs+ingress.Routes.Min(r=>r.RemainingDelayMs),Array.AsReadOnly(evidence.ToArray()));
            foreach(var item in pending)
            {
                _feedForward.Enqueue(item,item.DeliverAtTimestampMs);RememberMessageId(item.Message.MessageId);
            }
            Interlocked.Add(ref _feedForwardDepth,pending.Count);Interlocked.Add(ref _spikeInCount,pending.Count);
            ingress.Commit(packet,receipt);return ValueTask.FromResult(receipt);
        }
    }
}
