#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

internal sealed record CanalNucleusFrame(long Sequence,string PacketFingerprint,string CanonicalJson,string Sha256);
internal sealed record CanalNucleusStatus(long SourceSteps,long AcknowledgedSourceSteps,long ReceiverTicks,
    double ReceiverThroughMs,CanalNucleusFrame? Pending,string? Failure,string AttemptJson);

// Isolated in-process connection of the existing owners. No live selector,
// injected spikes, new afferent, receptor rule or calibrated anatomy is added.
internal sealed class CanalAfferentNucleusConnection : IDisposable
{
    internal const string Version="owned-canal-afferent-nucleus-v1";
    private const int MaximumTicksPerFrame=4096;
    private static readonly object ConstructionGate=new();
    private readonly int _thread=Environment.CurrentManagedThreadId;
    private readonly AvatarCanalAfferentInput _source;
    private readonly StructureEngine _receiver;
    private readonly IsolatedAfferentIngress _ingress;
    private readonly double _receiverStepMs;
    private readonly NeuralSubstrateSupply _supply;
    private readonly List<RubralInputDelivery> _deliveries=[];
    private readonly List<string> _steps=[];
    private long _sourceSteps,_acknowledged,_ticks,_sequence;
    private double _through;
    private bool _disposed;
    private string? _failure,_lastAcknowledged,_undrainedFailureOutput;
    private ObservedAfferentPacket? _packet;
    private AfferentIngressReceipt? _admission;
    private AvatarCanalBundleReceipt? _head;
    private CanalNucleusFrame? _pending;
    internal string Fingerprint { get; }

    internal CanalAfferentNucleusConnection(AvatarCanalAfferentInput source,string instance,string stateDirectory,
        IEnumerable<ObservedAfferentRoute> routes,double receiverStepMs,NeuralSubstrateSupply supply,int queueCapacity=4096)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(supply);
        var state=source.Capture();
        if(state.CompletedSteps!=0||state.AcknowledgedSteps!=0||state.Failure is not null||state.PendingPacket is not null||
            state.Mechanical.PendingTrace is not null||state.Neuronal.CompletedSteps!=0)
            throw new ArgumentException("Attach the actual fresh sensory owner before the first mechanical interval; do not reset or replay it.");
        if(!double.IsFinite(receiverStepMs)||receiverStepMs<=0||receiverStepMs>4||
            source.Source.EpochMs+receiverStepMs<=source.Source.EpochMs||
            string.IsNullOrWhiteSpace(instance)||!instance.StartsWith("isolated-",StringComparison.Ordinal)||
            !Path.IsPathFullyQualified(stateDirectory)||!supply.IsValid||queueCapacity<1||queueCapacity>65536)
            throw new ArgumentException("Explicit isolated recipient, fresh absolute save directory, representable receiver clock and bounded queue required.");
        _ingress=new(source.Source,routes);
        _source=source;_receiverStepMs=receiverStepMs;_supply=supply;_through=source.Source.EpochMs;_head=state.Mechanical.LastReceipt;
        // The serial diagnostic process owns this brief environment binding.
        // Tests use the existing process-environment collection.
        lock(ConstructionGate)
        {
            string? priorDirectory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
            string? priorInstance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
            try
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",stateDirectory);
                Environment.SetEnvironmentVariable("SERVICE_INSTANCE",instance);
                _receiver=new(new(StructureId.VestibularNuclei,"LIF","STDP",
                    "Isolated connection to the existing vestibular nucleus; physiology unqualified.",new(1,5),queueCapacity),
                    null,diagnosticAfferentIngress:_ingress);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",priorDirectory);
                Environment.SetEnvironmentVariable("SERVICE_INSTANCE",priorInstance);
            }
        }
        _receiver.RubralInputObserver=_deliveries.Add;
        Fingerprint=Hash(JsonSerializer.Serialize(new {Version,Source=source.Fingerprint,Ingress=_ingress.Fingerprint,
            receiverStepMs,supply,queueCapacity,MaximumTicksPerFrame}));
    }

    internal AvatarCanalBundleReceipt ConsumeHead(AvatarSkullObservation observation)
    {
        RequireAvailable();EnsureSource();
        try{return _head=_source.ConsumeHead(observation);}
        catch(Exception e){if(_source.Capture().Failure is not null)_failure=e.ToString();throw;}
    }

    internal AvatarCanalAfferentReceipt AdvanceNext(long startingStep)
    {
        RequireAvailable();EnsureSource();
        try
        {
            var receipt=_source.AdvanceNext(startingStep);_sourceSteps=receipt.Observation.CompletedStep;return receipt;
        }
        catch(Exception e){if(_source.Capture().Failure is not null)_failure=e.ToString();throw;}
    }

    internal CanalNucleusFrame PublishCompleted()
    {
        RequireHealthy();EnsureSource();
        if(_pending is not null)return _pending;
        if(_sourceSteps==_acknowledged)throw new InvalidOperationException("No new completed sensory progress to publish.");
        _packet=_source.Seal();
        double horizon=_packet.EndMs+_ingress.Routes.Min(r=>r.RemainingDelayMs);
        var times=new List<double>();
        // Fixed receiver grid, independent of packet partitions. Never force a
        // partial integration step merely because a packet happened to end.
        for(long next=checked(_ticks+1);;next=checked(next+1))
        {
            double time=_source.Source.EpochMs+next*_receiverStepMs;
            double previous=times.Count==0?_through:times[^1];
            if(!double.IsFinite(time)||time<=previous)throw new InvalidOperationException("Receiver clock is no longer representable.");
            if(time>horizon)break;
            if(times.Count==MaximumTicksPerFrame)throw new InvalidOperationException("Bounded receiver frame exceeded; source packet remains sealed.");
            times.Add(time);
        }
        try
        {
            _admission=_receiver.EnqueueObservedAfferentPacketAsync(_packet).GetAwaiter().GetResult();
            if(_admission.PacketFingerprint!=_packet.Fingerprint||_admission.TargetSafeThroughMs!=horizon)
                throw new InvalidOperationException("Receiver acknowledged another packet or conduction horizon.");
            foreach(double time in times)
            {
                long tick=checked(_ticks+1);
                var result=_receiver.ProcessStepAsync(new(tick,time,time-_through,new(),new Dictionary<BrainRhythm,double>(),0,
                    NeuralSubstrates:_supply),0).GetAwaiter().GetResult();
                _steps.Add(JsonSerializer.Serialize(result));_ticks=tick;_through=time;
            }
            string json=AttemptJson();
            return _pending=new(checked(++_sequence),_packet.Fingerprint,json,Hash(json));
        }
        catch(Exception e)
        {
            _failure=e.ToString();
            // A failed receiving step can have a valid prefix. Preserve its
            // remaining original output as well as earlier drained step frames.
            _undrainedFailureOutput=JsonSerializer.Serialize(_receiver.DrainOutboundSpikesAsync().GetAwaiter().GetResult());
            throw;
        }
    }

    internal void Acknowledge(string frameSha256)
    {
        RequireHealthy();EnsureSource();
        if(_pending is null)
        {
            if(_lastAcknowledged is not null&&frameSha256==_lastAcknowledged)return;
            throw new ArgumentException("No matching nucleus frame awaits acknowledgement.");
        }
        if(frameSha256!=_pending.Sha256)throw new ArgumentException("Acknowledgement belongs to another nucleus frame.");
        try{_source.AcknowledgePacket(_pending.PacketFingerprint);}
        catch(Exception e){_failure=e.ToString();throw;}
        _acknowledged=_sourceSteps;_lastAcknowledged=frameSha256;
        _packet=null;_admission=null;_pending=null;_steps.Clear();_deliveries.Clear();
    }

    internal CanalNucleusStatus Capture()
    {RequireOwner();return new(_sourceSteps,_acknowledged,_ticks,_through,_pending,_failure,AttemptJson());}
    private string AttemptJson()=>JsonSerializer.Serialize(new {Version,Fingerprint,Packet=_packet,Admission=_admission,
        ReceiverTicks=_ticks,ReceiverThroughMs=_through,ReceiverStepJson=_steps,ActualDeliveries=_deliveries,
        UndrainedFailureOutputJson=_undrainedFailureOutput,Failure=_failure});
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private void EnsureSource()
    {
        var s=_source.Capture();
        if(s.CompletedSteps!=_sourceSteps||s.AcknowledgedSteps!=_acknowledged||s.Neuronal.CompletedSteps!=_sourceSteps||
            s.Observer.ObservedThroughStep!=_sourceSteps||s.Failure is not null||!ReferenceEquals(s.Mechanical.LastReceipt,_head))
            throw new InvalidOperationException("Sensory source advanced or was acknowledged outside its connected owner.");
    }
    private void RequireOwner()
    {
        if(Environment.CurrentManagedThreadId!=_thread)throw new InvalidOperationException("One thread owns this source and nucleus.");
        if(_disposed)throw new ObjectDisposedException(nameof(CanalAfferentNucleusConnection));
    }
    private void RequireHealthy(){RequireOwner();if(_failure is not null)throw new InvalidOperationException("Preserve the failed connection; no silent retry of partial receiving biology.");}
    private void RequireAvailable(){RequireHealthy();if(_pending is not null)throw new InvalidOperationException("Acknowledge the complete nucleus frame before advancing its sensory source.");}
    public void Dispose(){if(_disposed)return;RequireOwner();_receiver.Dispose();_disposed=true;}
}
