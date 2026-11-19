using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record ConvergentAfferentInput(ObservedAfferentSource Source,IReadOnlyList<ObservedAfferentRoute> Routes);
internal sealed record ConvergentAfferentProgress(ObservedAfferentSource Source,long CompletedStep,long LastOrdinal,
    double SafeThroughMs,AfferentIngressReceipt? LastReceipt);

// Several original observed fibres converge on one actual vestibular population.
// Every fibre owns its event sequence and contact state. The slowest known
// arrival horizon bounds the recipient; silence is explicit progress, not a rate.
internal sealed class ConvergentAfferentIngress
{
    internal const string Version="isolated-convergent-afferent-ingress-v1";
    private readonly IsolatedAfferentIngress[] _inputs;
    private readonly Dictionary<(string Instance,string Neuron),IsolatedAfferentIngress> _byCell;
    private bool _bound;
    internal IReadOnlyList<ConvergentAfferentInput> Inputs { get; }
    internal double EpochMs { get; }
    internal double SafeThroughMs=>_inputs.Min(i=>i.SafeThroughMs);
    internal string Fingerprint { get; }

    internal ConvergentAfferentIngress(IEnumerable<ConvergentAfferentInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var copied=inputs.ToArray();
        if(copied.Length is <2 or >256||copied.Any(i=>i is null||i.Source is null||i.Routes is null))
            throw new ArgumentException("Declare two to 256 distinct observed sensory fibres.");
        _inputs=copied.Select(i=>new IsolatedAfferentIngress(i.Source,i.Routes))
            .OrderBy(i=>i.Source.InstanceKey,StringComparer.Ordinal).ThenBy(i=>i.Source.NeuronId,StringComparer.Ordinal).ToArray();
        var first=_inputs[0].Source;EpochMs=first.EpochMs;
        if(_inputs.Any(i=>i.Source.SessionId!=first.SessionId||i.Source.EpochMs!=EpochMs)||
            _inputs.Select(i=>i.Source.CellId).Distinct().Count()!=_inputs.Length||
            _inputs.Select(i=>(i.Source.InstanceKey,i.Source.NeuronId)).Distinct().Count()!=_inputs.Length||
            _inputs.Sum(i=>i.Routes.Count)>4096)
            throw new ArgumentException("Distinct physical cells and neuronal identities, one session/epoch and bounded contacts required.");
        _byCell=_inputs.ToDictionary(i=>(i.Source.InstanceKey,i.Source.NeuronId));
        Inputs=Array.AsReadOnly(_inputs.Select(i=>new ConvergentAfferentInput(i.Source,i.Routes)).ToArray());
        Fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{Version,Inputs})));
    }

    internal void Bind(StructureProfile profile,string instance,int neuronCount)
    {
        if(_bound)throw new InvalidOperationException("This convergence owner is already attached.");
        // Each original ingress enforces the same fresh full nucleus, explicit
        // instance and receiving-cell bounds before any state store is created.
        foreach(var input in _inputs)input.Bind(profile,instance,neuronCount);
        _bound=true;
    }
    internal IReadOnlyList<ConvergentAfferentProgress> Capture()=>Array.AsReadOnly(_inputs.Select(i=>
        new ConvergentAfferentProgress(i.Source,i.CompletedStep,i.LastOrdinal,i.SafeThroughMs,i.LastReceipt)).ToArray());
    internal IsolatedAfferentIngress ForPacket(ObservedAfferentPacket packet)
    {
        if(!_byCell.TryGetValue((packet.Source.InstanceKey,packet.Source.NeuronId),out var input)||input.Source!=packet.Source)
            throw new ArgumentException("Unknown or changed sensory source; no implicit attachment or epoch substitution.");
        return input;
    }
    internal double HorizonAfter(IsolatedAfferentIngress input,ObservedAfferentPacket packet)=>
        _inputs.Min(i=>ReferenceEquals(i,input)?packet.EndMs+i.Routes.Min(r=>r.RemainingDelayMs):i.SafeThroughMs);
    internal int Resolve(SpikeMessage message)
    {
        if(!_byCell.TryGetValue((message.SourceInstanceKey,message.SourceNeuronId),out var input))
            throw new InvalidOperationException("Undeclared source reached the convergent sensory queue.");
        return input.Resolve(message);
    }
    internal void ValidateTick(TickSignal tick,double lastTime)
    {
        double start=double.IsFinite(lastTime)?lastTime:EpochMs;
        // A later packet may report a crossing exactly at its starting endpoint.
        // Do not finish a recipient tick at that still-unknown arrival boundary.
        if(!double.IsFinite(tick.TimestampMs)||tick.TimestampMs<=start||tick.TimestampMs>=SafeThroughMs||
            tick.TickDurationMs!=tick.TimestampMs-start||tick.TickDurationMs>100)
            throw new InvalidOperationException("Recipient must stay within EVERY declared fibre's completed arrival horizon.");
    }
}

public sealed partial class StructureEngine
{
    private readonly ConvergentAfferentIngress? _diagnosticAfferentConvergence;

    private static void OrderConvergentInputs(List<SpikeEnvelope> due)=>due.Sort((a,b)=>
    {
        int c=a.DeliverAtTimestampMs.CompareTo(b.DeliverAtTimestampMs);if(c!=0)return c;
        c=StringComparer.Ordinal.Compare(a.Message.SourceInstanceKey,b.Message.SourceInstanceKey);if(c!=0)return c;
        c=StringComparer.Ordinal.Compare(a.Message.SourceNeuronId,b.Message.SourceNeuronId);if(c!=0)return c;
        c=a.Message.ProjectionId.CompareTo(b.Message.ProjectionId);if(c!=0)return c;
        c=a.Message.SynapseId.CompareTo(b.Message.SynapseId);if(c!=0)return c;
        return a.Message.MessageId.CompareTo(b.Message.MessageId);
    });

    internal ValueTask<AfferentIngressReceipt> EnqueueConvergentAfferentPacketAsync(ObservedAfferentPacket packet,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(packet);cancellationToken.ThrowIfCancellationRequested();
        lock(_stateGate)
        lock(_inboundQueueGate)
        {
            var convergence=_diagnosticAfferentConvergence??throw new InvalidOperationException("No convergent observed sensory owner is attached.");
            var input=convergence.ForPacket(packet);
            if(input.IsRetry(packet))return ValueTask.FromResult(input.LastReceipt!);
            input.ValidateNext(packet);
            int count=checked(packet.Events.Count*input.Routes.Count);
            int capacity=Math.Clamp(_profile.MaxInboundQueueDepth,1,65_536);
            if(count>StructureTransportLimits.MaxSpikeBatchCount||_feedForward.Count+_feedback.Count+count>capacity)
                throw new StructureIngressOverloadException(_profile.StructureId,capacity);
            var pending=new List<SpikeEnvelope>(count);var evidence=new List<AfferentDeliveryEvidence>(count);
            foreach(var e in packet.Events)
            foreach(var route in input.Routes)
            {
                var message=route.Message(input.Source,e);ValidateInboundSpike(message);
                double arrival=e.CrossingTimeMs+route.RemainingDelayMs;
                if(!double.IsFinite(arrival)||arrival<e.CrossingTimeMs||(route.RemainingDelayMs>0&&arrival==e.CrossingTimeMs)||
                    (double.IsFinite(_lastBiologicalTimestampMs)&&arrival<_lastBiologicalTimestampMs))
                    throw new InvalidOperationException("Unrepresentable or late sensory arrival; target cannot rewind.");
                if(_recentMessageIds.Contains(message.MessageId))throw new InvalidOperationException("Source interval reused a delivered event.");
                if(convergence.Resolve(message)!=route.TargetCell)throw new InvalidOperationException("Convergent receiving contact changed.");
                pending.Add(new(message,arrival));evidence.Add(new(e.MessageId,message.MessageId,message.SynapseId,
                    message.ProjectionId,route.TargetCell,e.CrossingTimeMs,arrival));
            }
            // This is the global horizon AT admission. Exact retry returns the
            // original receipt even after other fibres advance; Capture has now.
            var receipt=new AfferentIngressReceipt(packet.Fingerprint,packet.CompletedStep,packet.EndMs,
                convergence.HorizonAfter(input,packet),Array.AsReadOnly(evidence.ToArray()));
            foreach(var item in pending)
            {
                _feedForward.Enqueue(item,item.DeliverAtTimestampMs);RememberMessageId(item.Message.MessageId);
            }
            Interlocked.Add(ref _feedForwardDepth,pending.Count);Interlocked.Add(ref _spikeInCount,pending.Count);
            input.Commit(packet,receipt);return ValueTask.FromResult(receipt);
        }
    }
}
