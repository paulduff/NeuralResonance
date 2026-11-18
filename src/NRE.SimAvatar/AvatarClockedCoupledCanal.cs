using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public sealed record AvatarCanalTimedTransition(long ClockSubstepIndex,double PhysicalTimeMs,double NeuralTimeMs,
    AvatarCanalMechanicalTransition Transition);
public sealed record AvatarClockedCanalMechanics(string Version,string Fingerprint,long? CompletedThroughStep,
    double? PhysicalTimeMs,double? NeuralTimeMs,long IntegratedIntervals,long IntegratedSubsteps,
    AvatarCanalMechanicalPoint Point,double InitialEnergyJoules,double EnergyJoules,
    double InputWorkJoules,double PhysicalDampingJoules,double NumericalDampingJoules,
    double WorkAdditionResidual,double PhysicalDampingAdditionResidual,double NumericalDampingAdditionResidual,
    double EnergyIdentityResidualJoules);
public sealed record AvatarCanalCompletedInterval(string Fingerprint,AvatarCanalAngularReceipt HeldInput,
    AvatarCanalAngularReceipt ClosingInput,AvatarClockedCanalMechanics Before,AvatarClockedCanalMechanics After,
    IReadOnlyList<AvatarCanalTimedTransition> Substeps);
public sealed record AvatarClockedCanalReceipt(AvatarCanalAngularReceipt Input,AvatarClockedCanalMechanics Mechanics,
    AvatarCanalCompletedInterval? CompletedInterval);
public sealed record AvatarClockedCanalFailure(AvatarCanalAngularState AcceptedInput,
    IReadOnlyList<AvatarCanalTimedTransition> AttemptedIntervalPrefix,string Reason);
public sealed record AvatarClockedCanalState(AvatarClockedCanalMechanics Mechanics,AvatarCanalAngularState Input,
    AvatarClockedCanalReceipt? LastReceipt,AvatarCanalCompletedInterval? PendingInterval,
    bool Faulted,AvatarClockedCanalFailure? Failure);

// Isolated causal owner; there is no live selector. The first angular sample
// initializes an explicitly supplied mechanical state at its availability time.
// Later physical intervals use ONLY the preceding angular sample. The measured
// difference over the just-finished interval cannot act retroactively on it.
// This delayed zero-order hold is an explicit sampled-input approximation.
public sealed class AvatarClockedCoupledCanal
{
    public const string Version="physical-clock-coupled-canal-held-input-v1";
    public const int MaximumSubstepsPerInterval=65536;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly PhysicalSampleClock _clock;
    private readonly AvatarCanalSkullInput _input;
    private readonly AvatarCoupledCanalDynamics _dynamics;
    private readonly double _substepMs;
    private AvatarClockedCanalMechanics _mechanics;
    private AvatarClockedCanalReceipt? _receipt;
    private AvatarCanalCompletedInterval? _pending;
    private AvatarClockedCanalFailure? _failure;
    private string? _acknowledged;
    public int SubstepsPerInterval { get; }
    public long FirstAngularStep { get; }
    public string Fingerprint { get; }

    public AvatarClockedCoupledCanal(PhysicalSampleClock clock,string headBodyId,string sensorId,
        AvatarSkullVector localPointMeters,AvatarCanalPrimeBasis basis,AvatarCoupledCanalParameters parameters,
        AvatarCanalMechanicalPoint initialPoint,int substepsPerInterval)
    {
        ArgumentNullException.ThrowIfNull(clock);clock.Validate();ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(parameters);ArgumentNullException.ThrowIfNull(initialPoint);
        initialPoint.VolumeMeters3.Validate();initialPoint.FlowMeters3PerSecond.Validate();
        if(basis.SourceSha256!=parameters.SourceSha256)throw new ArgumentException("The basis and mechanics require a common declared source.");
        if(substepsPerInterval<1||substepsPerInterval>MaximumSubstepsPerInterval||(substepsPerInterval&(substepsPerInterval-1))!=0)
            throw new ArgumentException("Declare a bounded power-of-two subdivision; no implicit timestep or clamping.");
        _clock=clock;SubstepsPerInterval=substepsPerInterval;FirstAngularStep=checked(clock.InitialPhysicsStep+1);
        _=clock.AtStep(FirstAngularStep);_substepMs=clock.PhysicsStepMs/substepsPerInterval;
        double seconds=(clock.PhysicsStepMs/1000)/substepsPerInterval;
        if(!double.IsNormal(_substepMs)||!double.IsNormal(seconds)||seconds*substepsPerInterval!=clock.PhysicsStepMs/1000)
            throw new ArgumentException("Substep duration must retain representable physical units.");
        _=TimeAtIndex(1);_input=new(clock,headBodyId,sensorId,localPointMeters,basis);
        _dynamics=new(parameters,seconds);
        double energy=parameters.Energy(initialPoint.VolumeMeters3.ToArray(),initialPoint.FlowMeters3PerSecond.ToArray());
        Fingerprint=CleftNumbers.Fingerprint(new {Version,Clock=clock.Fingerprint,Input=_input.Fingerprint,
            Dynamics=_dynamics.Fingerprint,FirstAngularStep,SubstepsPerInterval,InitialPoint=initialPoint,
            Availability="current angular sample drives following interval only",
            Initialization="explicit state at first available angular sample; preceding interval unintegrated",
            Handoff="one complete interval pending acknowledgement; exact latest input replay is inert",
            Commit="whole physical interval; retain attempted substep prefix on fault",LiveSelectionChanged=false});
        _mechanics=new(Version,Fingerprint,null,null,null,0,0,initialPoint,energy,energy,0,0,0,0,0,0,0);
    }

    public AvatarClockedCanalState Capture()
    { RequireOwner();return new(_mechanics,_input.Capture(),_receipt,_pending,Faulted,_failure); }
    public bool Faulted=>_failure is not null||_input.Faulted;

    public AvatarClockedCanalReceipt Consume(AvatarSkullObservation observation)
    {
        RequireOwner();ArgumentNullException.ThrowIfNull(observation);
        if(Faulted)throw new InvalidOperationException("Faulted coupled-canal epoch cannot resume or reset.");
        if(_pending is not null&&observation.PhysicsStep!=_receipt!.Input.Skull.PhysicsStep)
            throw new InvalidOperationException("Acknowledge the pending mechanical interval before another head endpoint.");
        // Reject an unrepresentable closing time before either owner advances.
        _=TimeAtIndex(Index(observation.PhysicsStep,0));
        var beforeInput=_input.Capture();var stages=new List<AvatarCanalTimedTransition>();
        try
        {
            var incoming=_input.Consume(observation);
            if(_receipt is not null&&ReferenceEquals(incoming,_receipt.Input))return _receipt;
            AvatarClockedCanalMechanics updated=_mechanics;AvatarCanalCompletedInterval? interval=null;
            if(incoming.Projection is null)
            {
                if(observation.PhysicsStep!=_clock.InitialPhysicsStep||_mechanics.CompletedThroughStep is not null)
                    throw new InvalidOperationException("Missing angular input after the declared first raw endpoint.");
            }
            else if(_mechanics.CompletedThroughStep is null)
            {
                if(observation.PhysicsStep!=FirstAngularStep)throw new InvalidOperationException("First angular availability changed.");
                updated=_mechanics with{CompletedThroughStep=observation.PhysicsStep,PhysicalTimeMs=incoming.Skull.PhysicalTimeMs,
                    NeuralTimeMs=incoming.Skull.NeuralTimeMs};
            }
            else
            {
                var held=_receipt!.Input;
                if(held.Projection is null||held.Skull.PhysicsStep!=_mechanics.CompletedThroughStep||
                    observation.PhysicsStep!=checked(held.Skull.PhysicsStep+1))
                    throw new InvalidOperationException("Mechanical interval does not follow its available held input.");
                var point=_mechanics.Point;
                double work=_mechanics.InputWorkJoules,physical=_mechanics.PhysicalDampingJoules,numerical=_mechanics.NumericalDampingJoules;
                double workError=_mechanics.WorkAdditionResidual,physicalError=_mechanics.PhysicalDampingAdditionResidual,numericalError=_mechanics.NumericalDampingAdditionResidual;
                for(int i=1;i<=SubstepsPerInterval;i++)
                {
                    long index=Index(held.Skull.PhysicsStep,i);var time=TimeAtIndex(index);
                    var transition=_dynamics.AdvanceHeld(point,held.Projection.Components);
                    stages.Add(new(index,time.PhysicalMs,time.NeuralMs,transition));point=transition.After;
                    Add(ref work,ref workError,transition.InputWorkJoules);
                    Add(ref physical,ref physicalError,transition.PhysicalDampingJoules);
                    Add(ref numerical,ref numericalError,transition.NumericalDampingJoules);
                }
                var last=stages[^1];
                if(last.PhysicalTimeMs!=incoming.Skull.PhysicalTimeMs||last.NeuralTimeMs!=incoming.Skull.NeuralTimeMs)
                    throw new ArithmeticException("Substep grid does not end at the original physical endpoint.");
                double energy=last.Transition.EnergyAfterJoules,residual=_mechanics.InitialEnergyJoules+work-physical-numerical-energy;
                CleftNumbers.Finite(residual);
                updated=_mechanics with{CompletedThroughStep=observation.PhysicsStep,PhysicalTimeMs=last.PhysicalTimeMs,
                    NeuralTimeMs=last.NeuralTimeMs,IntegratedIntervals=checked(_mechanics.IntegratedIntervals+1),
                    IntegratedSubsteps=checked(_mechanics.IntegratedSubsteps+SubstepsPerInterval),Point=point,EnergyJoules=energy,
                    InputWorkJoules=work,PhysicalDampingJoules=physical,NumericalDampingJoules=numerical,
                    WorkAdditionResidual=workError,PhysicalDampingAdditionResidual=physicalError,NumericalDampingAdditionResidual=numericalError,
                    EnergyIdentityResidualJoules=residual};
                var retained=Array.AsReadOnly(stages.ToArray());
                string digest=CleftNumbers.Fingerprint(new {Version,Fingerprint,Held=held,Closing=incoming,Before=_mechanics,After=updated,Substeps=retained});
                interval=new(digest,held,incoming,_mechanics,updated,retained);
            }
            var receipt=new AvatarClockedCanalReceipt(incoming,updated,interval);
            _mechanics=updated;_pending=interval;return _receipt=receipt;
        }
        catch(Exception error)
        {
            var afterInput=_input.Capture();
            if(afterInput.Faulted||afterInput.LastSkull!=beforeInput.LastSkull)
                _failure=new(afterInput,Array.AsReadOnly(stages.ToArray()),error.ToString());
            throw;
        }
    }

    public void AcknowledgeInterval(string fingerprint)
    {
        RequireOwner();
        if(_pending is null)
        {
            if(_acknowledged is not null&&_acknowledged==fingerprint)return;
            throw new ArgumentException("No matching completed mechanical interval.");
        }
        if(_pending.Fingerprint!=fingerprint)throw new ArgumentException("Acknowledgement belongs to another interval.");
        _acknowledged=fingerprint;_pending=null;
    }

    private long Index(long physicalStep,int substep)
    {
        _=_clock.AtStep(physicalStep);
        if(substep<0||substep>SubstepsPerInterval)throw new ArgumentOutOfRangeException(nameof(substep));
        long index=checked((physicalStep-_clock.InitialPhysicsStep)*SubstepsPerInterval+substep);
        if(index>PhysicalSampleClock.MaximumExactStep)throw new ArgumentOutOfRangeException(nameof(physicalStep),"Exact substep index exhausted.");
        return index;
    }
    private (double PhysicalMs,double NeuralMs) TimeAtIndex(long index)
    {
        if(index<0||index>PhysicalSampleClock.MaximumExactStep)throw new ArgumentOutOfRangeException(nameof(index));
        double elapsed=index*_substepMs,physical=_clock.PhysicalEpochMs+elapsed,neural=_clock.NeuralEpochMs+elapsed;
        if(!double.IsFinite(physical)||!double.IsFinite(neural)||(index>0&&
            (physical<=_clock.PhysicalEpochMs+(index-1)*_substepMs||neural<=_clock.NeuralEpochMs+(index-1)*_substepMs)))
            throw new ArithmeticException("Mechanical substep timestamps lost resolution; no merging or epoch reset.");
        return (physical,neural);
    }
    private static void Add(ref double sum,ref double remainder,double increment)
    {
        double y=increment-remainder,next=sum+y,error=(next-sum)-y;
        CleftNumbers.Finite(next);CleftNumbers.Finite(error);sum=next;remainder=error;
    }
    private void RequireOwner()
    { if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns the coupled-canal clock."); }
}
