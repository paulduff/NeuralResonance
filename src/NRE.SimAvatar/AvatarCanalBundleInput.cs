using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public sealed record AvatarCupulaStiffnessMatch(string MechanicalParametersFingerprint,string FieldFingerprint,
    double MechanicalStiffnessPascalPerCubicMeter,double FieldStiffnessPascalPerCubicMeter,double RelativeDifference);

public static class AvatarCupulaMechanicalBinding
{
    public const string Version="declared-cupula-stiffness-binding-v1";
    public const double MaximumRelativeDifference=1e-12;

    public static AvatarCupulaStiffnessMatch Match(AvatarCoupledCanalParameters mechanics,AvatarCupulaShearField field)
    {
        ArgumentNullException.ThrowIfNull(mechanics);ArgumentNullException.ThrowIfNull(field);
        int i=3+(int)field.Canal;double expected=mechanics.StiffnessPascalPerMeter3[6*i+i];
        double difference=Math.Abs(field.StiffnessPascalPerCubicMeter/expected-1);
        if(!double.IsFinite(difference)||difference>MaximumRelativeDifference)
            throw new ArgumentException("Declared spatial stiffness does not match the organ; no automatic rescaling of geometry or mechanics.");
        return new(mechanics.Fingerprint,field.Fingerprint,expected,field.StiffnessPascalPerCubicMeter,difference);
    }

    // An explicit inverse geometry construction, not measured human anatomy.
    // Callers must supply material, aspect ratio, identity and provenance.
    public static AvatarCupulaShearField EquivalentCircularField(AvatarCoupledCanalParameters mechanics,
        string organId,AvatarCupulaCanal canal,string geometrySource,string sourceSha256,
        double shearModulusPascal,double thicknessOverRadius)
    {
        ArgumentNullException.ThrowIfNull(mechanics);if(!Enum.IsDefined(canal))throw new ArgumentException("Unknown canal.");
        CleftNumbers.Positive(shearModulusPascal);CleftNumbers.Positive(thicknessOverRadius);
        int i=3+(int)canal;double stiffness=mechanics.StiffnessPascalPerMeter3[6*i+i];
        double radius=Math.Cbrt((8/Math.PI)*(shearModulusPascal/stiffness)*thicknessOverRadius);
        var field=new AvatarCupulaShearField(organId,canal,geometrySource,sourceSha256,radius,thicknessOverRadius*radius,shearModulusPascal);
        _=Match(mechanics,field);return field;
    }
}

public sealed record AvatarTimedCupulaBundle(long ClockTick,double PhysicalTimeMs,double NeuralTimeMs,
    AvatarCanalMechanicalPoint Mechanics,AvatarCupulaBundleSample Bundle);

// Released only after the complete mechanical interval is available. Its
// trajectory was generated using the earlier held angular input; the closing
// observation never supplies acceleration retroactively. All accepted knots
// remain available, even when a sensory clock later samples between them.
public sealed class AvatarCanalBundleTrace
{
    private readonly PhysicalSampleClock _clock;
    private readonly AvatarCupulaShearField _field;
    private readonly AvatarCupulaBundleAttachment _attachment;
    private readonly AvatarCanalCompletedInterval _mechanical;
    private readonly double _tickMs;
    public long FirstTick { get; }
    public long LastTick { get; }
    public int TicksPerPhysicalInterval { get; }
    public string Fingerprint { get; }
    public string MechanicalIntervalFingerprint=>_mechanical.Fingerprint;
    public AvatarCanalAngularReceipt HeldInput=>_mechanical.HeldInput;
    public AvatarCanalAngularReceipt ClosingInput=>_mechanical.ClosingInput;
    public IReadOnlyList<AvatarTimedCupulaBundle> Knots { get; }

    internal AvatarCanalBundleTrace(PhysicalSampleClock clock,int ticksPerInterval,AvatarCupulaShearField field,
        AvatarCupulaBundleAttachment attachment,AvatarCanalCompletedInterval mechanical)
    {
        _clock=clock;_field=field;_attachment=attachment;_mechanical=mechanical;
        TicksPerPhysicalInterval=ticksPerInterval;_tickMs=clock.PhysicsStepMs/ticksPerInterval;
        FirstTick=checked((mechanical.HeldInput.Skull.PhysicsStep-clock.InitialPhysicsStep)*ticksPerInterval);
        LastTick=checked(FirstTick+ticksPerInterval);
        var points=new List<AvatarTimedCupulaBundle>{new(FirstTick,mechanical.Before.PhysicalTimeMs!.Value,
            mechanical.Before.NeuralTimeMs!.Value,mechanical.Before.Point,field.Project(mechanical.Before.Point,attachment))};
        var before=mechanical.Before.Point;long previous=FirstTick;
        foreach(var step in mechanical.Substeps)
        {
            if(step.ClockSubstepIndex<=previous||step.ClockSubstepIndex>LastTick||step.Transition.Before!=before)
                throw new ArgumentException("Mechanical history is discontinuous.");
            points.Add(new(step.ClockSubstepIndex,step.PhysicalTimeMs,step.NeuralTimeMs,step.Transition.After,field.Project(step.Transition.After,attachment)));
            previous=step.ClockSubstepIndex;before=step.Transition.After;
        }
        if(previous!=LastTick||before!=mechanical.After.Point)throw new ArgumentException("Mechanical coverage is incomplete.");
        Knots=Array.AsReadOnly(points.ToArray());
        Fingerprint=CleftNumbers.Fingerprint(new {AvatarCanalBundleInput.Version,Clock=clock.Fingerprint,TicksPerPhysicalInterval,
            Mechanical=mechanical.Fingerprint,Field=field.Fingerprint,Attachment=attachment.Fingerprint,
            Dense=AvatarCanalDensePoint.Version,Knots});
    }

    public AvatarTimedCupulaBundle AtTick(long tick)
    {
        if(tick<FirstTick||tick>LastTick)throw new ArgumentOutOfRangeException(nameof(tick),"No extrapolation outside the released interval.");
        int lo=0,hi=Knots.Count-1;
        while(lo<=hi)
        {
            int mid=lo+(hi-lo)/2;var knot=Knots[mid];
            if(knot.ClockTick==tick)return knot;
            if(knot.ClockTick<tick)lo=mid+1;else hi=mid-1;
        }
        var start=Knots[hi];var end=Knots[lo];double fraction=(double)(tick-start.ClockTick)/(end.ClockTick-start.ClockTick);
        var point=AvatarCanalDensePoint.At(_mechanical.Substeps[hi].Transition,fraction);
        double physical=_clock.PhysicalEpochMs+tick*_tickMs,neural=_clock.NeuralEpochMs+tick*_tickMs;
        if(!double.IsFinite(physical)||!double.IsFinite(neural)||physical<=_clock.PhysicalEpochMs+(tick-1)*_tickMs||
            neural<=_clock.NeuralEpochMs+(tick-1)*_tickMs)throw new ArithmeticException("Dense sample timestamp lost resolution.");
        return new(tick,physical,neural,point,_field.Project(point,_attachment));
    }
}

public sealed record AvatarCanalBundleReceipt(AvatarClockedCanalReceipt Mechanics,AvatarTimedCupulaBundle? AvailableSample,AvatarCanalBundleTrace? CompletedTrace);
public sealed record AvatarCanalBundleFailure(long AttemptedPhysicalStep,string Reason);
public sealed record AvatarCanalBundleState(string Fingerprint,AvatarCupulaStiffnessMatch Stiffness,AvatarClockedCanalState Mechanics,
    AvatarCanalBundleReceipt? LastReceipt,AvatarCanalBundleTrace? PendingTrace,AvatarCanalBundleFailure? Failure);

// One input/acknowledgement owner. Projection failure preserves the complete
// accepted mechanical prefix and faults this bridge, without a rewind/reset.
// This publishes physical bundle trajectories; it does not advance a neuron.
public sealed class AvatarCanalBundleInput
{
    public const string Version="graded-canal-spatial-bundle-owner-v1";
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly PhysicalSampleClock _clock;
    private readonly AvatarClockedGradedCanal _mechanics;
    private readonly AvatarCupulaShearField _field;
    private readonly AvatarCupulaBundleAttachment _attachment;
    private AvatarCanalBundleReceipt? _receipt;
    private AvatarCanalBundleTrace? _pending;
    private AvatarCanalBundleFailure? _failure;
    private string? _acknowledged;
    public string Fingerprint { get; }
    public AvatarCupulaStiffnessMatch Stiffness { get; }

    public AvatarCanalBundleInput(PhysicalSampleClock clock,string headBodyId,string sensorId,AvatarSkullVector localPointMeters,
        AvatarCanalPrimeBasis basis,AvatarCoupledCanalParameters parameters,AvatarCanalMechanicalPoint initialPoint,
        AvatarCanalStepSchedule schedule,AvatarCupulaShearField field,AvatarCupulaBundleAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);attachment.Validate();Stiffness=AvatarCupulaMechanicalBinding.Match(parameters,field);
        // Validate the complete attachment before accepting any physical input.
        _=field.Project(initialPoint,attachment);_clock=clock;_field=field;_attachment=attachment;
        _mechanics=new(clock,headBodyId,sensorId,localPointMeters,basis,parameters,initialPoint,schedule);
        Fingerprint=CleftNumbers.Fingerprint(new {Version,Mechanics=_mechanics.Fingerprint,Stiffness,Attachment=attachment.Fingerprint,
            Binding=AvatarCupulaMechanicalBinding.Version,AvatarCupulaMechanicalBinding.MaximumRelativeDifference,
            Dense=AvatarCanalDensePoint.Version,Initialization="bundle state at first angular availability; no earlier sensory history",
            Handoff="every accepted mechanical knot; one completed trace pending exact acknowledgement",LiveSelectionChanged=false});
    }

    public AvatarCanalBundleState Capture()
    { RequireOwner();return new(Fingerprint,Stiffness,_mechanics.Capture(),_receipt,_pending,_failure); }

    public AvatarCanalBundleReceipt Consume(AvatarSkullObservation observation)
    {
        RequireOwner();ArgumentNullException.ThrowIfNull(observation);
        if(_failure is not null)throw new InvalidOperationException("Faulted bundle bridge cannot reset or resume.");
        if(_pending is not null&&observation.PhysicsStep!=_receipt!.Mechanics.Input.Skull.PhysicsStep)
            throw new InvalidOperationException("Acknowledge the exact pending bundle trace before advancing mechanics.");
        var before=_mechanics.Capture();
        try
        {
            var mechanical=_mechanics.Consume(observation);
            if(_receipt is not null&&ReferenceEquals(mechanical,_receipt.Mechanics))return _receipt;
            AvatarTimedCupulaBundle? available=null;AvatarCanalBundleTrace? trace=null;
            if(mechanical.Mechanics.CompletedThroughStep is not null)
            {
                long tick=checked((observation.PhysicsStep-_clock.InitialPhysicsStep)*_mechanics.TicksPerInterval);
                available=new(tick,mechanical.Mechanics.PhysicalTimeMs!.Value,mechanical.Mechanics.NeuralTimeMs!.Value,
                    mechanical.Mechanics.Point,_field.Project(mechanical.Mechanics.Point,_attachment));
            }
            if(mechanical.CompletedInterval is { } interval)trace=new(_clock,_mechanics.TicksPerInterval,_field,_attachment,interval);
            _pending=trace;return _receipt=new(mechanical,available,trace);
        }
        catch(Exception error)
        {
            var after=_mechanics.Capture();
            if(after.Faulted||after.Input.LastSkull!=before.Input.LastSkull)_failure=new(observation.PhysicsStep,error.ToString());
            throw;
        }
    }

    public void AcknowledgeTrace(string fingerprint)
    {
        RequireOwner();
        if(_pending is null)
        {
            if(_acknowledged is not null&&_acknowledged==fingerprint)return;
            throw new ArgumentException("No matching completed bundle trace.");
        }
        if(_pending.Fingerprint!=fingerprint)throw new ArgumentException("Acknowledgement belongs to another trace.");
        _mechanics.AcknowledgeInterval(_pending.MechanicalIntervalFingerprint);_acknowledged=fingerprint;_pending=null;
    }

    private void RequireOwner()
    { if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns the canal-to-bundle bridge."); }
}
