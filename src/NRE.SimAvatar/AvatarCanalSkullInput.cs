using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public sealed record AvatarCanalAngularReceipt(string OwnerFingerprint,AvatarSkullKinematicReceipt Skull,
    long? DifferenceFromStep,AvatarSkullVector? AverageAngularAccelerationWorldRadiansPerSecondSquared,
    AvatarSkullVector? AverageAngularAccelerationSkullRadiansPerSecondSquared,
    AvatarCanalBasisProjection? Projection);
public sealed record AvatarCanalAngularFailure(AvatarSkullKinematicReceipt AcceptedSkull,string Reason);
public sealed record AvatarCanalAngularState(string Fingerprint,AvatarSkullKinematicReceipt? LastSkull,
    AvatarCanalAngularReceipt? LastReceipt,bool Faulted,AvatarCanalAngularFailure? Failure);

// Input adapter only. It never advances organ mechanics or a neuron. The world
// angular-velocity difference is available at the FINAL endpoint, then expressed
// in that endpoint's skull frame. It must not drive the already elapsed interval.
// Measured float endpoints remain float-accuracy evidence, even in doubles.
public sealed class AvatarCanalSkullInput
{
    public const string Version="causal-skull-to-printed-prime-acceleration-v1";
    private readonly AvatarSkullKinematics _skull;
    private readonly AvatarCanalPrimeBasis _basis;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly double _seconds;
    private AvatarSkullObservation? _previous;
    private AvatarCanalAngularReceipt? _receipt;
    private AvatarCanalAngularFailure? _failure;
    public string Fingerprint { get; }
    public bool Faulted=>_failure is not null||_skull.Faulted;

    public AvatarCanalSkullInput(PhysicalSampleClock clock,string headBodyId,string sensorId,
        AvatarSkullVector localPointMeters,AvatarCanalPrimeBasis basis)
    {
        ArgumentNullException.ThrowIfNull(basis);_basis=basis;
        _skull=new(clock,headBodyId,sensorId,localPointMeters);_seconds=clock.PhysicsStepMs/1000;
        Fingerprint=CleftNumbers.Fingerprint(new {Version,Skull=_skull.Fingerprint,Basis=basis.Fingerprint,
            Difference="(omega_world_current-omega_world_previous)/physical_seconds, projected in current skull frame",
            AngularMap="posterior=+roll, right=-pitch, superior=-yaw; axial determinant included",
            Availability="final endpoint only; no retroactive drive",MechanicalIntegration=false,LiveSelectionChanged=false});
    }

    public AvatarCanalAngularState Capture()
    { RequireOwner();return new(Fingerprint,_skull.Capture(),_receipt,Faulted,_failure); }

    public AvatarCanalAngularReceipt Consume(AvatarSkullObservation observation)
    {
        RequireOwner();if(Faulted)throw new InvalidOperationException("Faulted angular epoch cannot resume or reset.");
        // The existing owner validates identity, complete endpoints, rigid basis,
        // fixed material point and replay bytes before accepting anything.
        var skull=_skull.Consume(observation);
        if(_receipt is not null&&ReferenceEquals(skull,_receipt.Skull))return _receipt;
        try
        {
            AvatarSkullVector? world=null,local=null;AvatarCanalBasisProjection? projection=null;
            if(_previous is not null)
            {
                var delta=AvatarSkullVector.Subtract(observation.AngularVelocityWorldRadiansPerSecond,_previous.AngularVelocityWorldRadiansPerSecond);
                world=new(delta.X/_seconds,delta.Y/_seconds,delta.Z/_seconds);
                if(!world.Finite)throw new ArithmeticException("Nonfinite angular velocity difference.");
                local=new(AvatarSkullVector.Dot(world,observation.RightWorld),AvatarSkullVector.Dot(world,observation.UpWorld),
                    AvatarSkullVector.Dot(world,observation.ForwardWorld));
                projection=_basis.Resolve(AvatarCanalPrimeBasis.AxialSkullToSource(local));
            }
            var result=new AvatarCanalAngularReceipt(Fingerprint,skull,_previous?.PhysicsStep,world,local,projection);
            _previous=observation;return _receipt=result;
        }
        catch(Exception error) { _failure=new(skull,error.ToString());throw; }
    }

    private void RequireOwner()
    { if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns this angular observation epoch."); }
}
