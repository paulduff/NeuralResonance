using System.Security.Cryptography;
using System.Text.Json;

namespace NeuralResonanceEngine.Protocol;

// Isolated observed-source contract. Not yet a live service/RPC selector or a
// claim that a numerical voltage crossing is a physiological action potential.
public sealed record ObservedAfferentSource(Guid SessionId,Guid CellId,string InstanceKey,
    string NeuronId,string CableNodeId,string ObservationFingerprint,double EpochMs,double StepMs)
{
    public const string ObserverVersion="isolated-afferent-completed-step-observer-v1";
    public const long MaximumExactStep=9_007_199_254_740_991;
    public void Validate()
    {
        if(SessionId==Guid.Empty||CellId==Guid.Empty||string.IsNullOrWhiteSpace(InstanceKey)||
            string.IsNullOrWhiteSpace(NeuronId)||string.IsNullOrWhiteSpace(CableNodeId)||
            ObservationFingerprint is null||ObservationFingerprint.Length!=64||!ObservationFingerprint.All(Uri.IsHexDigit)||
            !double.IsFinite(EpochMs)||EpochMs<0||!double.IsFinite(StepMs)||StepMs<=0||
            !double.IsFinite(EpochMs+StepMs)||EpochMs+StepMs<=EpochMs)
            throw new ArgumentException("Explicit source epoch, cell, observation criterion and representable clock required.");
    }
    public double TimeAt(long step)
    {
        Validate();
        if(step<0||step>MaximumExactStep)throw new ArgumentOutOfRangeException(nameof(step));
        double time=EpochMs+step*StepMs;
        if(!double.IsFinite(time))throw new ArgumentException("Unrepresentable sensory clock.");
        return time;
    }
    public Guid EventId(long ordinal)=>StableNeuralIdentity.Create(ObserverVersion,SessionId,CellId,
        InstanceKey,NeuronId,CableNodeId,ObservationFingerprint,ordinal);
}

public sealed record ObservedAfferentEvent(Guid MessageId,long Ordinal,long CompletedStep,double CrossingTimeMs);

public sealed class ObservedAfferentPacket
{
    public const string Version="observed-afferent-contiguous-packet-v1";
    public const int MaximumSteps=1_048_576,MaximumEvents=4096;
    public ObservedAfferentSource Source { get; }
    public long StartingStep { get; }
    public long CompletedStep { get; }
    public IReadOnlyList<ObservedAfferentEvent> Events { get; }
    public double StartMs=>Source.TimeAt(StartingStep);
    public double EndMs=>Source.TimeAt(CompletedStep);
    public string Fingerprint { get; }

    public ObservedAfferentPacket(ObservedAfferentSource source,long startingStep,long completedStep,IReadOnlyList<ObservedAfferentEvent> events)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(events);source.Validate();
        if(startingStep<0||completedStep<=startingStep||completedStep>ObservedAfferentSource.MaximumExactStep||
            completedStep-startingStep>MaximumSteps||events.Count>MaximumEvents||events.Count>completedStep-startingStep)
            throw new ArgumentException("Bounded, nonempty completed source interval required; quiet intervals have an empty event list.");
        var copied=events.ToArray();long priorStep=startingStep,priorOrdinal=0;double priorTime=source.TimeAt(startingStep);
        if(source.TimeAt(completedStep)<=priorTime)throw new ArgumentException("Source interval is not representable.");
        foreach(var e in copied)
        {
            if(e is null||e.Ordinal<=0||e.CompletedStep<=priorStep||e.CompletedStep>completedStep||
                (priorOrdinal!=0&&e.Ordinal!=checked(priorOrdinal+1))||e.MessageId!=source.EventId(e.Ordinal)||
                !double.IsFinite(e.CrossingTimeMs)||e.CrossingTimeMs<priorTime||
                e.CrossingTimeMs<source.TimeAt(e.CompletedStep-1)||e.CrossingTimeMs>source.TimeAt(e.CompletedStep)||
                source.TimeAt(e.CompletedStep)<=source.TimeAt(e.CompletedStep-1))
                throw new ArgumentException("Original ordered source events and their completed crossing intervals required.");
            priorStep=e.CompletedStep;priorOrdinal=e.Ordinal;priorTime=e.CrossingTimeMs;
        }
        Source=source;StartingStep=startingStep;CompletedStep=completedStep;Events=Array.AsReadOnly(copied);
        Fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Version,source,startingStep,completedStep,events=copied })));
    }
}

// Delay is ONLY the path remaining beyond the measured axonal site. Release
// parameters are explicit uncalibrated inputs to the existing receiver model.
public sealed record ObservedAfferentRoute(Guid ProjectionId,string TargetInstanceKey,int TargetCell,
    double RemainingDelayMs,float ReleaseQuanta,float ReuptakeRate)
{
    public void Validate(int neuronCount)
    {
        if(ProjectionId==Guid.Empty||string.IsNullOrWhiteSpace(TargetInstanceKey)||TargetCell<0||TargetCell>=neuronCount||
            !double.IsFinite(RemainingDelayMs)||RemainingDelayMs<0||!float.IsFinite(ReleaseQuanta)||ReleaseQuanta<.05f||ReleaseQuanta>5||
            !float.IsFinite(ReuptakeRate)||ReuptakeRate<=0)
            throw new ArgumentException("Explicit recipient, finite nonnegative remaining delay and valid release parameters required.");
    }
    public SpikeMessage Message(ObservedAfferentSource source,ObservedAfferentEvent e)
    {
        source.Validate();Validate(int.MaxValue);
        if(e.MessageId!=source.EventId(e.Ordinal))throw new ArgumentException("Event belongs to another source epoch.");
        var message=new SpikeMessage { MessageId=StableNeuralIdentity.Create("observed-afferent-delivery-v1",e.MessageId,ProjectionId,TargetInstanceKey,TargetCell),
            TimestampMs=e.CrossingTimeMs,SourceStructure=StructureId.VestibularAfferents,TargetStructure=StructureId.VestibularNuclei,
            SourceInstanceKey=source.InstanceKey,SourceNeuronId=source.NeuronId,TargetNeuronId="observed-afferent-target-"+TargetCell.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ProjectionId=ProjectionId,Neurotransmitter=NTEnum.GLUTAMATE,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL,
            VesicleQuanta=ReleaseQuanta,ReuptakeRate=ReuptakeRate };
        message.SynapseId=StableNeuralIdentity.Contact(message,TargetInstanceKey,TargetCell);return message;
    }
}
