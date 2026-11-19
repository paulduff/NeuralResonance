#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

internal sealed record CanalConvergenceFibre(AvatarCanalAfferentInput Source,IReadOnlyList<ObservedAfferentRoute> Routes);
internal sealed record CanalConvergenceFrame(long Sequence,string CanonicalJson,string Sha256);
internal sealed record CanalConvergenceSourceStatus(Guid CellId,long CompletedSteps,long AcknowledgedSteps,AvatarCanalAfferentState Actual);
internal sealed record CanalConvergenceStatus(long ReceiverTicks,double ReceiverThroughMs,
    IReadOnlyList<CanalConvergenceSourceStatus> Sources,CanalConvergenceFrame? Pending,string? Failure,string AttemptJson);

// Owns actual peripheral sources and one actual receiving nucleus. No source is
// replayed, reset or turned into a rate. This remains an isolated serial owner.
internal sealed class CanalConvergenceConnection : IDisposable
{
    internal const string Version="owned-convergent-canal-nucleus-v1";
    internal const double RigidVelocityToleranceMetersPerSecond=1e-5;
    private const int MaximumTicksPerFrame=4096;
    private static readonly object ConstructionGate=new();
    private sealed class Fibre(AvatarCanalAfferentInput source,AvatarCanalBundleReceipt head,AvatarSkullObservation observation)
    {
        internal readonly AvatarCanalAfferentInput Source=source;
        internal AvatarCanalBundleReceipt Head=head;
        internal AvatarSkullObservation Observation=observation;
        internal long Steps,Acknowledged;
    }
    private readonly Fibre[] _fibres;
    private readonly Dictionary<Guid,Fibre> _byCell;
    private readonly StructureEngine _receiver;
    private readonly ConvergentAfferentIngress _ingress;
    private readonly PhysicalSampleClock _physical;
    private readonly NeuralSubstrateSupply _supply;
    private readonly double _receiverStepMs;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly List<(Fibre Fibre,ObservedAfferentPacket Packet)> _packets=[];
    private readonly List<AfferentIngressReceipt> _admissions=[];
    private readonly List<RubralInputDelivery> _deliveries=[];
    private readonly List<string> _receiverSteps=[];
    private long _ticks,_sequence;
    private double _through;
    private string _lastHeadDigest,_attemptHeads;
    private string? _failure,_lastAcknowledged,_undrainedFailureOutput;
    private bool _disposed;
    private CanalConvergenceFrame? _pending;
    internal string Fingerprint { get; }

    internal CanalConvergenceConnection(IEnumerable<CanalConvergenceFibre> fibres,string instance,string stateDirectory,
        double receiverStepMs,NeuralSubstrateSupply supply,int queueCapacity=4096)
    {
        ArgumentNullException.ThrowIfNull(fibres);ArgumentNullException.ThrowIfNull(supply);
        var declared=fibres.ToArray();
        if(declared.Length is <2 or >256||declared.Any(f=>f is null||f.Source is null||f.Routes is null)||
            !double.IsFinite(receiverStepMs)||receiverStepMs<=0||receiverStepMs>4||!supply.IsValid||
            string.IsNullOrWhiteSpace(instance)||!instance.StartsWith("isolated-",StringComparison.Ordinal)||
            !Path.IsPathFullyQualified(stateDirectory)||queueCapacity is <1 or >65536)
            throw new ArgumentException("Declare actual fresh fibres, a bounded clock/queue, substrate supply and isolated recipient.");
        _ingress=new(declared.Select(f=>new ConvergentAfferentInput(f.Source.Source,f.Routes)));
        _physical=declared[0].Source.Clock.PhysicalClock;_through=_ingress.EpochMs;
        if(_through+receiverStepMs<=_through||!double.IsFinite(_through+receiverStepMs))
            throw new ArgumentException("Receiving clock is not representable at the source epoch.");
        _fibres=declared.OrderBy(f=>f.Source.Source.InstanceKey,StringComparer.Ordinal)
            .ThenBy(f=>f.Source.Source.NeuronId,StringComparer.Ordinal).Select(f=>
        {
            var state=f.Source.Capture();var head=state.Mechanical.LastReceipt;
            if(f.Source.Clock.PhysicalClock.Fingerprint!=_physical.Fingerprint||state.CompletedSteps!=0||state.AcknowledgedSteps!=0||
                state.Neuronal.CompletedSteps!=0||state.Failure is not null||state.Neuronal.Failure is not null||state.Observer.Failure is not null||
                state.Mechanical.Failure is not null||state.Mechanical.Mechanics.Faulted||state.PendingPacket is not null||
                state.Mechanical.PendingTrace is not null||head is null)
                throw new ArgumentException("Attach fresh actual owners on one physical clock before their first mechanical interval.");
            var observation=JsonSerializer.Deserialize<AvatarSkullObservation>(head.Mechanics.Input.Skull.RawObservationJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))??throw new ArgumentException("Original head observation missing.");
            return new Fibre(f.Source,head,observation);
        }).ToArray();
        _byCell=_fibres.ToDictionary(f=>f.Source.Source.CellId);
        ValidateHeads(_fibres.Select(f=>f.Observation).ToArray(),_fibres[0].Source.Clock.FirstAngularStep);
        _attemptHeads=HeadJson(_fibres.Select(f=>f.Observation).ToArray());_lastHeadDigest=Hash(_attemptHeads);
        _receiverStepMs=receiverStepMs;_supply=supply;
        lock(ConstructionGate)
        {
            string? directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),priorInstance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
            try
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",stateDirectory);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",instance);
                _receiver=new(new(StructureId.VestibularNuclei,"LIF","STDP","Isolated convergent original sensory owners; physiology unqualified.",new(1,5),queueCapacity),
                    null,diagnosticAfferentConvergence:_ingress);
            }
            finally{Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",directory);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",priorInstance);}
        }
        _receiver.RubralInputObserver=_deliveries.Add;
        Fingerprint=Hash(JsonSerializer.Serialize(new{Version,Sources=_fibres.Select(f=>f.Source.Fingerprint).ToArray(),
            Ingress=_ingress.Fingerprint,receiverStepMs,supply,queueCapacity,MaximumTicksPerFrame,RigidVelocityToleranceMetersPerSecond}));
    }

    internal IReadOnlyList<AvatarCanalBundleReceipt> ConsumeHeads(IReadOnlyDictionary<Guid,AvatarSkullObservation> observations)
    {
        RequireAvailable();EnsureSources();ArgumentNullException.ThrowIfNull(observations);
        if(observations.Count!=_fibres.Length||observations.Keys.Any(k=>!_byCell.ContainsKey(k)))
            throw new ArgumentException("Supply every declared cell's head observation, and no unknown cell.");
        var ordered=_fibres.Select(f=>observations[f.Source.Source.CellId]).ToArray();
        string json=HeadJson(ordered),digest=Hash(json);
        if(digest==_lastHeadDigest)return Array.AsReadOnly(_fibres.Select(f=>f.Head).ToArray());
        ValidateHeads(ordered,checked(_fibres[0].Observation.PhysicsStep+1));
        if(_fibres.Any(f=>f.Steps!=f.Acknowledged||f.Source.Capture().Mechanical.PendingTrace is not null))
            throw new InvalidOperationException("Finish and acknowledge every sensory interval before advancing the common head.");
        _attemptHeads=json;
        try
        {
            for(int i=0;i<_fibres.Length;i++)
            {
                var f=_fibres[i];f.Head=f.Source.ConsumeHead(ordered[i]);f.Observation=ordered[i];
            }
            _lastHeadDigest=digest;return Array.AsReadOnly(_fibres.Select(f=>f.Head).ToArray());
        }
        catch(Exception e){_failure=e.ToString();throw;} // Retain partial mechanical advancement; no rollback/replay.
    }

    internal AvatarCanalAfferentReceipt AdvanceNext(Guid cellId,long startingStep)
    {
        RequireAvailable();EnsureSources();
        if(!_byCell.TryGetValue(cellId,out var f))throw new ArgumentException("Unknown actual sensory owner.");
        try{var receipt=f.Source.AdvanceNext(startingStep);f.Steps=receipt.Observation.CompletedStep;return receipt;}
        catch(Exception e){if(f.Source.Capture().Failure is not null)_failure=e.ToString();throw;}
    }

    internal CanalConvergenceFrame PublishCompleted()
    {
        RequireHealthy();EnsureSources();if(_pending is not null)return _pending;
        if(_fibres.All(f=>f.Steps==f.Acknowledged))throw new InvalidOperationException("No original sensory progress to publish.");
        try
        {
            foreach(var f in _fibres.Where(f=>f.Steps>f.Acknowledged))_packets.Add((f,f.Source.Seal()));
            foreach(var p in _packets)
            {
                var receipt=_receiver.EnqueueConvergentAfferentPacketAsync(p.Packet).GetAwaiter().GetResult();_admissions.Add(receipt);
                if(receipt.PacketFingerprint!=p.Packet.Fingerprint||receipt.SourceCompletedStep!=p.Packet.CompletedStep||
                    receipt.SourceCompletedMs!=p.Packet.EndMs||receipt.TargetSafeThroughMs!=_ingress.SafeThroughMs)
                    throw new InvalidOperationException("Shared receiver acknowledged another packet or causal horizon.");
            }
            double horizon=_ingress.SafeThroughMs;int count=0;
            while(true)
            {
                long tick=checked(_ticks+1);double time=_ingress.EpochMs+tick*_receiverStepMs;
                if(!double.IsFinite(time)||time<=_through)throw new InvalidOperationException("Receiving grid is no longer representable.");
                if(time>=horizon)break; // The next packet may contain an exact-boundary event.
                if(count++==MaximumTicksPerFrame)throw new InvalidOperationException("Receiving frame capacity exceeded; preserve its completed prefix.");
                var result=_receiver.ProcessStepAsync(new(tick,time,time-_through,new(),new Dictionary<BrainRhythm,double>(),0,NeuralSubstrates:_supply),0).GetAwaiter().GetResult();
                _receiverSteps.Add(JsonSerializer.Serialize(result));_ticks=tick;_through=time;
            }
            string json=AttemptJson();return _pending=new(checked(++_sequence),json,Hash(json));
        }
        catch(Exception e)
        {
            _failure=e.ToString();_undrainedFailureOutput=JsonSerializer.Serialize(_receiver.DrainOutboundSpikesAsync().GetAwaiter().GetResult());throw;
        }
    }

    internal void Acknowledge(string sha256)
    {
        RequireHealthy();EnsureSources();
        if(_pending is null)
        {
            if(_lastAcknowledged is not null&&sha256==_lastAcknowledged)return;
            throw new ArgumentException("No matching shared nucleus output awaits acknowledgement.");
        }
        if(sha256!=_pending.Sha256)throw new ArgumentException("Acknowledge the exact retained shared output.");
        try
        {
            foreach(var p in _packets){p.Fibre.Source.AcknowledgePacket(p.Packet.Fingerprint);p.Fibre.Acknowledged=p.Packet.CompletedStep;}
        }
        catch(Exception e){_failure=e.ToString();throw;}
        _lastAcknowledged=sha256;_pending=null;_packets.Clear();_admissions.Clear();_deliveries.Clear();_receiverSteps.Clear();
    }

    internal CanalConvergenceStatus Capture()
    {
        RequireOwner();return new(_ticks,_through,Array.AsReadOnly(_fibres.Select(f=>
            new CanalConvergenceSourceStatus(f.Source.Source.CellId,f.Steps,f.Acknowledged,f.Source.Capture())).ToArray()),_pending,_failure,AttemptJson());
    }
    private string AttemptJson()=>JsonSerializer.Serialize(new{Version,Fingerprint,HeadInputJson=_attemptHeads,
        Packets=_packets.Select(p=>p.Packet).ToArray(),Admissions=_admissions,Progress=_ingress.Capture(),
        ReceiverTicks=_ticks,ReceiverThroughMs=_through,ReceiverStepJson=_receiverSteps,ActualDeliveries=_deliveries,
        UndrainedFailureOutputJson=_undrainedFailureOutput,Failure=_failure});
    private string HeadJson(AvatarSkullObservation[] observations)=>JsonSerializer.Serialize(_fibres.Select((f,i)=>new{f.Source.Source.CellId,Observation=observations[i]}));
    private void ValidateHeads(AvatarSkullObservation[] observations,long step)
    {
        var first=observations[0];if(first is null)throw new ArgumentException("Original head observations required.");
        for(int i=0;i<observations.Length;i++)
        {
            var s=observations[i];var prior=_fibres[i].Observation;
            if(s is null||s.PhysicsStep!=step||s.SampleId!=PhysicalSampleProtocol.Identity(_physical,step)||
                s.HeadBodyId!=prior.HeadBodyId||s.SensorId!=prior.SensorId||s.LocalPointMeters!=prior.LocalPointMeters||
                s.HeadBodyId!=first.HeadBodyId||s.OriginWorldMeters!=first.OriginWorldMeters||s.RightWorld!=first.RightWorld||
                s.UpWorld!=first.UpWorld||s.ForwardWorld!=first.ForwardWorld||s.AngularVelocityWorldRadiansPerSecond!=first.AngularVelocityWorldRadiansPerSecond||
                s.GravityWorldMetersPerSecondSquared!=first.GravityWorldMetersPerSecondSquared||!Close(OriginVelocity(s),OriginVelocity(first)))
                throw new ArgumentException("All fibres must observe one original head pose/rigid velocity at the same physical endpoint, retaining their own sensing points.");
        }
    }
    private static AvatarSkullVector OriginVelocity(AvatarSkullObservation s)
    {
        if(s.OriginWorldMeters is null||s.PointWorldMeters is null||s.PointVelocityWorldMetersPerSecond is null||s.AngularVelocityWorldRadiansPerSecond is null)
            throw new ArgumentException("Complete rigid head kinematics required.");
        var r=new AvatarSkullVector(s.PointWorldMeters.X-s.OriginWorldMeters.X,s.PointWorldMeters.Y-s.OriginWorldMeters.Y,s.PointWorldMeters.Z-s.OriginWorldMeters.Z);
        var w=s.AngularVelocityWorldRadiansPerSecond;var v=s.PointVelocityWorldMetersPerSecond;
        return new(v.X-(w.Y*r.Z-w.Z*r.Y),v.Y-(w.Z*r.X-w.X*r.Z),v.Z-(w.X*r.Y-w.Y*r.X));
    }
    private static bool Close(AvatarSkullVector a,AvatarSkullVector b)=>
        Math.Abs(a.X-b.X)<=RigidVelocityToleranceMetersPerSecond&&Math.Abs(a.Y-b.Y)<=RigidVelocityToleranceMetersPerSecond&&Math.Abs(a.Z-b.Z)<=RigidVelocityToleranceMetersPerSecond;
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private void EnsureSources()
    {
        foreach(var f in _fibres)
        {
            var s=f.Source.Capture();
            if(s.CompletedSteps!=f.Steps||s.AcknowledgedSteps!=f.Acknowledged||s.Neuronal.CompletedSteps!=f.Steps||
                s.Observer.ObservedThroughStep!=f.Steps||s.Failure is not null||s.Neuronal.Failure is not null||s.Observer.Failure is not null||
                s.Mechanical.Failure is not null||s.Mechanical.Mechanics.Faulted||!ReferenceEquals(s.Mechanical.LastReceipt,f.Head))
                throw new InvalidOperationException("A sensory owner advanced, failed or was acknowledged outside the shared connection.");
        }
    }
    private void RequireOwner()
    {if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns the complete convergent circuit.");if(_disposed)throw new ObjectDisposedException(nameof(CanalConvergenceConnection));}
    private void RequireHealthy(){RequireOwner();if(_failure is not null)throw new InvalidOperationException("Preserve the failed shared circuit; no partial replay or automatic reset.");}
    private void RequireAvailable(){RequireHealthy();if(_pending is not null)throw new InvalidOperationException("Acknowledge the shared receiving output before advancing any attached source or head.");}
    public void Dispose(){if(_disposed)return;RequireOwner();_receiver.Dispose();_disposed=true;}
}
