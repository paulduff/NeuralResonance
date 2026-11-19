using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

// Isolated causal owner; there is no live selector. The first angular sample
// initializes an explicitly supplied mechanical state at its availability time.
// Later physical intervals use ONLY the preceding angular sample. The measured
// difference over the just-finished interval cannot act retroactively on it.
// This delayed zero-order hold is an explicit sampled-input approximation.
// The declared dyadic schedule resolves boundary transients without changing the core equations.
public sealed class AvatarClockedGradedCanal
{
    public const string Version="physical-clock-graded-canal-held-input-v1";
    public const int MaximumSubstepsPerInterval=AvatarCanalStepSchedule.MaximumSubstepsPerInterval;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly PhysicalSampleClock _clock;
    private readonly AvatarCanalSkullInput _input;
    private readonly Dictionary<int,AvatarCoupledCanalDynamics> _dynamics;
    private readonly AvatarCanalStepSchedule _schedule;
    private readonly double _substepMs;
    private AvatarClockedCanalMechanics _mechanics;
    private AvatarClockedCanalReceipt? _receipt;
    private AvatarCanalCompletedInterval? _pending;
    private AvatarClockedCanalFailure? _failure;
    private string? _acknowledged;
    public int SubstepsPerInterval=>_schedule.Count;
    public int TicksPerInterval=>_schedule.TicksPerInterval;
    public long FirstAngularStep { get; }
    public string Fingerprint { get; }

    public AvatarClockedGradedCanal(PhysicalSampleClock clock,string headBodyId,string sensorId,
        AvatarSkullVector localPointMeters,AvatarCanalPrimeBasis basis,AvatarCoupledCanalParameters parameters,
        AvatarCanalMechanicalPoint initialPoint,AvatarCanalStepSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(clock);clock.Validate();ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(parameters);ArgumentNullException.ThrowIfNull(initialPoint);
        initialPoint.VolumeMeters3.Validate();initialPoint.FlowMeters3PerSecond.Validate();
        if(basis.SourceSha256!=parameters.SourceSha256)throw new ArgumentException("The basis and mechanics require a common declared source.");
        ArgumentNullException.ThrowIfNull(schedule);_schedule=schedule;
        _clock=clock;FirstAngularStep=checked(clock.InitialPhysicsStep+1);
        _=clock.AtStep(FirstAngularStep);_substepMs=clock.PhysicsStepMs/TicksPerInterval;
        double seconds=(clock.PhysicsStepMs/1000)/TicksPerInterval;
        if(!double.IsNormal(_substepMs)||!double.IsNormal(seconds)||seconds*TicksPerInterval!=clock.PhysicsStepMs/1000)
            throw new ArgumentException("Substep duration must retain representable physical units.");
        _=TimeAtIndex(1);_input=new(clock,headBodyId,sensorId,localPointMeters,basis);
        _dynamics=schedule.StepWidthsTicks.Distinct().ToDictionary(width=>width,width=>new AvatarCoupledCanalDynamics(parameters,seconds*width));
        double energy=parameters.Energy(initialPoint.VolumeMeters3.ToArray(),initialPoint.FlowMeters3PerSecond.ToArray());
        Fingerprint=CleftNumbers.Fingerprint(new {Version,Clock=clock.Fingerprint,Input=_input.Fingerprint,
            Dynamics=_dynamics.OrderBy(p=>p.Key).Select(p=>new {WidthTicks=p.Key,p.Value.Fingerprint}),
            Schedule=schedule.Fingerprint,FirstAngularStep,TicksPerInterval,SubstepsPerInterval,InitialPoint=initialPoint,
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
                int elapsedTicks=0;
                foreach(int width in _schedule.StepWidthsTicks)
                {
                    elapsedTicks=checked(elapsedTicks+width);
                    long index=Index(held.Skull.PhysicsStep,elapsedTicks);var time=TimeAtIndex(index);
                    var transition=_dynamics[width].AdvanceHeld(point,held.Projection.Components);
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
        if(substep<0||substep>TicksPerInterval)throw new ArgumentOutOfRangeException(nameof(substep));
        long index=checked((physicalStep-_clock.InitialPhysicsStep)*TicksPerInterval+substep);
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
