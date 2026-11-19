using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using System.Text.Json;

namespace NRE.SimAvatar;

public sealed record AvatarCanalAfferentReceipt(string TraceFingerprint,AvatarCanalAfferentTime Start,AvatarCanalAfferentTime End,
    AvatarTimedCupulaBundle HeldBundle,AvatarAfferentObservationReceipt Observation);
public sealed record AvatarCanalAfferentFailure(long StartingStep,long AcceptedNeuronalSteps,string Reason);
public sealed record AvatarCanalAfferentState(string Fingerprint,string ClockFingerprint,long CompletedSteps,long AcknowledgedSteps,
    int UnacknowledgedEvents,ObservedAfferentPacket? PendingPacket,AvatarCanalAfferentReceipt? LastReceipt,
    AvatarCanalBundleState Mechanical,AvatarAfferentDynamicsState Neuronal,AvatarAfferentObserverState Observer,
    AvatarCanalAfferentFailure? Failure);

// Owns the actual bundle -> joint hair/cleft/calyx/cable -> observed event path.
// Input is sampled at each source-step start and held for that complete step.
// This numerical forcing approximation needs coupled reference qualification.
// There is no resettling, firing-rate conversion, injected axonal current or
// reset. Quiet packets carry elapsed neuronal progress just as firing ones do.
public sealed class AvatarCanalAfferentInput
{
    public const string Version="canal-to-observed-afferent-owner-v1";
    private readonly int _owner=Environment.CurrentManagedThreadId,_maximumPacketSteps;
    private readonly AvatarCanalBundleInput _mechanics;
    private readonly AvatarVestibularAfferentDynamics _model;
    private readonly AvatarAfferentEventObserver _observer;
    private readonly AvatarAfferentPacketSource _packets;
    private readonly string _mechanicalFingerprint,_modelFingerprint;
    private readonly Guid _cell;
    private AvatarCanalBundleReceipt _head;
    private AvatarCanalBundleTrace? _trace;
    private AvatarCanalAfferentReceipt? _receipt;
    private AvatarCanalAfferentFailure? _failure;
    private ObservedAfferentPacket? _sealed;
    private string? _acknowledgedPacket;
    private long _step,_acknowledged;
    private int _unacknowledgedEvents;
    public string Fingerprint { get; }
    public AvatarCanalAfferentClock Clock { get; }
    public ObservedAfferentSource Source=>_packets.Source;

    public AvatarCanalAfferentInput(AvatarCanalBundleInput mechanics,AvatarVestibularAfferentDynamics model,
        AvatarAfferentObservationSite observationSite,AvatarCanalAfferentClock clock,int maximumPacketSteps)
    {
        ArgumentNullException.ThrowIfNull(mechanics);ArgumentNullException.ThrowIfNull(model);ArgumentNullException.ThrowIfNull(clock);
        if(maximumPacketSteps<1||maximumPacketSteps>ObservedAfferentPacket.MaximumSteps)throw new ArgumentOutOfRangeException(nameof(maximumPacketSteps));
        var m=mechanics.Capture();var n=model.Capture();var head=m.LastReceipt;var first=clock.At(0);
        var original=head is null?null:JsonSerializer.Deserialize<AvatarSkullObservation>(head.Mechanics.Input.Skull.RawObservationJson,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if(m.Failure is not null||m.Mechanics.Faulted||m.PendingTrace is not null||head?.AvailableSample is not { } initial||
            m.Mechanics.Mechanics.CompletedThroughStep!=clock.FirstAngularStep||m.Mechanics.Mechanics.IntegratedIntervals!=0||
            original?.SampleId!=PhysicalSampleProtocol.Identity(clock.PhysicalClock,clock.FirstAngularStep)||
            initial.ClockTick!=first.MechanicalTick||initial.PhysicalTimeMs!=first.PhysicalMs||initial.NeuralTimeMs!=first.MechanicalNeuralMs)
            throw new ArgumentException("Attach to the original first angular sample before any mechanical interval is consumed.");
        if(n.CompletedSteps!=0||n.Failure is not null||n.SessionId!=clock.PhysicalClock.SessionId||n.CellId!=initial.Bundle.HairCellId||
            n.EpochMs!=clock.SourceEpochMs||n.Settings.StepMs!=clock.SourceStepMs)
            throw new ArgumentException("Declare the attached cell's complete initial state, session, epoch and matched source step; no restart or resampling.");
        _mechanics=mechanics;_model=model;Clock=clock;_maximumPacketSteps=maximumPacketSteps;_head=head;
        _mechanicalFingerprint=m.Fingerprint;_modelFingerprint=n.Fingerprint;_cell=n.CellId;
        _observer=new(model,observationSite);_packets=new(_observer,maximumPacketSteps);clock.ValidateSource(_packets.Source);
        Fingerprint=CleftNumbers.Fingerprint(new {Version,Clock=clock.Fingerprint,Mechanics=m.Fingerprint,Source=_packets.Source,maximumPacketSteps,
            Forcing="dense bundle sampled at source-step start, held for that whole neuronal step; no gain or offset",
            Acknowledgement="keep physical trace until every source step and its final quiet/firing packet are acknowledged",
            State="original joint sensory state, compensation, flux ledgers and crossing times; no reset or automatic settling"});
        // Attachment evaluates neither the sensory kernel nor an elapsed step.
    }

    public AvatarCanalAfferentState Capture()
    {
        RequireOwner();return new(Fingerprint,Clock.Fingerprint,_step,_acknowledged,_unacknowledgedEvents,_sealed,_receipt,
            _mechanics.Capture(),_model.Capture(),_observer.Capture(),_failure);
    }

    public AvatarCanalBundleReceipt ConsumeHead(AvatarSkullObservation observation)
    {
        RequireHealthy();EnsureOwnership();ArgumentNullException.ThrowIfNull(observation);
        if(_trace is not null&&observation.PhysicsStep!=_head.Mechanics.Input.Skull.PhysicsStep)
            throw new InvalidOperationException("Consume and acknowledge the current sensory interval before advancing the head input.");
        if(_sealed is not null&&observation.PhysicsStep!=_head.Mechanics.Input.Skull.PhysicsStep)
            throw new InvalidOperationException("A sealed sensory packet is still pending.");
        bool accepted=false;
        try
        {
            var next=_mechanics.Consume(observation);
            if(ReferenceEquals(next,_head))return _head;
            accepted=true;
            _head=next;_trace=next.CompletedTrace;
            if(_trace is null||_trace.FirstTick!=Clock.At(_step).MechanicalTick||_trace.TicksPerPhysicalInterval!=Clock.MechanicalTicksPerInterval||
                _trace.Knots[0].Bundle.HairCellId!=_cell)
                throw new InvalidOperationException("Mechanical history does not continue the attached sensory cell's clock.");
            return next;
        }
        catch(Exception error)
        {
            var m=_mechanics.Capture();
            if(accepted||m.Failure is not null||m.Mechanics.Faulted||!ReferenceEquals(m.LastReceipt,_head)||m.PendingTrace!=_trace)Fail(error);
            throw;
        }
    }

    public AvatarCanalAfferentReceipt AdvanceNext(long expectedStartingStep)
    {
        RequireHealthy();EnsureOwnership();
        if(_sealed is not null)throw new InvalidOperationException("Acknowledge the sealed sensory packet before further neuronal advancement.");
        if(_receipt is not null&&expectedStartingStep==_receipt.Observation.StartingStep&&_step>_acknowledged)return _receipt;
        if(expectedStartingStep!=_step)throw new ArgumentException("Contiguous unacknowledged source steps required; no stale replay or skipped time.");
        if(_trace is null)throw new InvalidOperationException("No completed mechanical interval is available; do not fill future input.");
        if(_step-_acknowledged>=_maximumPacketSteps||_unacknowledgedEvents>=ObservedAfferentPacket.MaximumEvents)
            throw new InvalidOperationException("Sensory outbox is full; seal and acknowledge before advancement.");
        var start=Clock.At(_step);var end=Clock.At(checked(_step+1));
        if(start.MechanicalTick<_trace.FirstTick||end.MechanicalTick>_trace.LastTick)
            throw new InvalidOperationException("The entire neuronal step must be inside the released mechanical interval.");
        try
        {
            var sample=_trace.AtTick(start.MechanicalTick);var closing=_trace.AtTick(end.MechanicalTick);
            ValidateSample(sample,start);ValidateSample(closing,end);
            var observed=_packets.AdvanceNext(_step,sample.Bundle.BundleDisplacementNanometers);
            if(observed.Interval.StartMs!=start.SourceNeuralMs||observed.Interval.EndMs!=end.SourceNeuralMs)
                throw new InvalidOperationException("Original neuronal timestamps changed during the source step.");
            _step=observed.CompletedStep;if(observed.Event is not null)_unacknowledgedEvents++;
            return _receipt=new(_trace.Fingerprint,start,end,sample,observed);
        }
        catch(Exception error){Fail(error);throw;}
    }

    public ObservedAfferentPacket Seal()
    {
        RequireOwner();
        // Valid observed prefixes remain deliverable after a subsequent failure.
        if(_sealed is not null)return _sealed;
        if(_step==_acknowledged)throw new InvalidOperationException("No new completed sensory progress to seal.");
        return _sealed=_packets.Seal();
    }

    public void AcknowledgePacket(string fingerprint)
    {
        RequireOwner();
        if(_sealed is null)
        {
            if(_acknowledgedPacket is not null&&fingerprint==_acknowledgedPacket)return;
            throw new ArgumentException("No matching sensory packet is awaiting acknowledgement.");
        }
        if(fingerprint!=_sealed.Fingerprint)throw new ArgumentException("Acknowledgement belongs to another sensory packet.");
        if(_failure is null)EnsureOwnership();
        _packets.Acknowledge(fingerprint);_acknowledged=_sealed.CompletedStep;_acknowledgedPacket=fingerprint;
        _sealed=null;_unacknowledgedEvents=0;
        if(_failure is null&&_trace is not null&&Clock.At(_step).MechanicalTick==_trace.LastTick)
        {
            try{_mechanics.AcknowledgeTrace(_trace.Fingerprint);_trace=null;}
            catch(Exception error){Fail(error);throw;}
        }
    }

    private void ValidateSample(AvatarTimedCupulaBundle sample,AvatarCanalAfferentTime time)
    {
        if(sample.ClockTick!=time.MechanicalTick||sample.PhysicalTimeMs!=time.PhysicalMs||sample.NeuralTimeMs!=time.MechanicalNeuralMs||sample.Bundle.HairCellId!=_cell)
            throw new InvalidOperationException("Mechanical cell identity or its original timestamp differs from the declared integer clock.");
    }
    private void EnsureOwnership()
    {
        var m=_mechanics.Capture();var n=_model.Capture();var o=_observer.Capture();
        if(m.Fingerprint!=_mechanicalFingerprint||!ReferenceEquals(m.LastReceipt,_head)||!ReferenceEquals(m.PendingTrace,_trace)||
            m.Failure is not null||m.Mechanics.Faulted||n.Fingerprint!=_modelFingerprint||n.CompletedSteps!=_step||n.Failure is not null||
            o.ObservedThroughStep!=_step||o.Failure is not null)
            throw new InvalidOperationException("Mechanical or neuronal state advanced outside its composed owner.");
    }
    private void Fail(Exception error)=>_failure=new(_step,_model.Capture().CompletedSteps,error.ToString());
    private void RequireHealthy()
    { RequireOwner();if(_failure is not null)throw new InvalidOperationException("Faulted connected sensory epoch cannot reset or resume."); }
    private void RequireOwner()
    { if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns this mechanical and sensory handoff."); }
}
