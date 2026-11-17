using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public sealed record AvatarSkullVector(double X, double Y, double Z)
{
    internal bool Finite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
    internal static AvatarSkullVector Add(AvatarSkullVector a, AvatarSkullVector b) => new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    internal static AvatarSkullVector Subtract(AvatarSkullVector a, AvatarSkullVector b) => new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    internal static AvatarSkullVector Scale(AvatarSkullVector a, double s) => new(a.X*s,a.Y*s,a.Z*s);
    internal static double Dot(AvatarSkullVector a, AvatarSkullVector b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    internal static AvatarSkullVector Cross(AvatarSkullVector a, AvatarSkullVector b) =>
        new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    internal double MaximumComponent => Math.Max(Math.Abs(X),Math.Max(Math.Abs(Y),Math.Abs(Z)));
}

// A fixed material point on the actual head, measured in world coordinates at
// one physical endpoint. These values are not reconstructed from pelvis angles.
// Float native measurements retain float accuracy when represented as doubles.
public sealed record AvatarSkullObservation(long PhysicsStep, Guid SampleId, string HeadBodyId,
    string SensorId, AvatarSkullVector LocalPointMeters, AvatarSkullVector OriginWorldMeters,
    AvatarSkullVector RightWorld, AvatarSkullVector UpWorld, AvatarSkullVector ForwardWorld,
    AvatarSkullVector PointWorldMeters, AvatarSkullVector PointVelocityWorldMetersPerSecond,
    AvatarSkullVector AngularVelocityWorldRadiansPerSecond, AvatarSkullVector GravityWorldMetersPerSecondSquared);

public sealed record AvatarSkullKinematicReceipt(long PhysicsStep, double PhysicalTimeMs, double NeuralTimeMs,
    string RawObservationJson, string RawObservationSha256, string? PreviousObservationSha256,
    long? AccelerationFromStep, AvatarSkullVector? AverageAccelerationWorldMetersPerSecondSquared,
    AvatarVestibularKinematics? VestibularInput);

// Isolated adapter: no live caller, physiological transduction or implicit
// initial acceleration. The first measurement has no derived acceleration.
// Later backward differences are available only at their final endpoint and
// are estimates of interval-average acceleration, not instantaneous values.
public sealed class AvatarSkullKinematics
{
    public const string Version = "isolated-head-world-kinematics-v1";
    public const double BasisTolerance = 1e-5, PointGeometryToleranceMeters = 1e-5;
    private readonly PhysicalSampleClock _clock;
    private readonly string _head, _sensor;
    private readonly AvatarSkullVector _local;
    private readonly double _seconds;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private AvatarSkullObservation? _previous;
    private AvatarSkullKinematicReceipt? _receipt;
    public bool Faulted { get; private set; }
    public string? FaultReason { get; private set; }
    public string Fingerprint { get; }
    public PhysicalSampleClock Clock => _clock;
    public long FirstMechanicsStep { get; }

    public AvatarSkullKinematics(PhysicalSampleClock clock, string headBodyId, string sensorId,
        AvatarSkullVector localPointMeters)
    {
        ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        if (!ValidKey(headBodyId) || !ValidKey(sensorId) || localPointMeters is null || !localPointMeters.Finite)
            throw new ArgumentException("Declare the head and fixed material point.");
        _seconds = clock.PhysicsStepMs/1000;
        if (!double.IsFinite(_seconds) || _seconds <= 0) throw new ArgumentException("Unrepresentable physical step.");
        FirstMechanicsStep = checked(clock.InitialPhysicsStep+1);
        _=clock.AtStep(FirstMechanicsStep);
        _clock=clock; _head=headBodyId; _sensor=sensorId; _local=localPointMeters;
        Fingerprint=PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(new{Version,RawClock=clock.Fingerprint,
            FirstMechanicsStep,Head=headBodyId,Sensor=sensorId,Local=localPointMeters,
            BasisTolerance,PointGeometryToleranceMeters,
            Acceleration="backward world point-velocity difference; available at end of interval",
            Axes="right/up/forward world basis; pitch=X yaw=Y roll=Z; gravito-inertial g-a, down=-localY"}));
    }

    public AvatarSkullKinematicReceipt? Capture() { RequireOwner(); return _receipt; }

    public AvatarSkullKinematicReceipt Consume(AvatarSkullObservation observation)
    {
        RequireOwner(); ArgumentNullException.ThrowIfNull(observation);
        if (Faulted) throw new InvalidOperationException("Faulted skull epoch cannot resume or reset.");
        Validate(observation);
        string original=JsonSerializer.Serialize(observation), digest=PhysicalSampleProtocol.Digest(original);
        if (_receipt is not null && observation.PhysicsStep==_receipt.PhysicsStep)
        {
            if (digest!=_receipt.RawObservationSha256) throw new ArgumentException("Conflicting head observation replay.");
            return _receipt;
        }
        long next=_previous is null?_clock.InitialPhysicsStep:checked(_previous.PhysicsStep+1);
        if (observation.PhysicsStep!=next) throw new ArgumentException("Require every original head endpoint; no gap or reset.");
        var time=_clock.AtStep(observation.PhysicsStep);
        try
        {
            AvatarSkullVector? acceleration=null; AvatarVestibularKinematics? input=null;
            if (_previous is not null)
            {
                var delta=AvatarSkullVector.Subtract(observation.PointVelocityWorldMetersPerSecond,_previous.PointVelocityWorldMetersPerSecond);
                acceleration=new(delta.X/_seconds,delta.Y/_seconds,delta.Z/_seconds);
                var gravitoInertial=AvatarSkullVector.Subtract(observation.GravityWorldMetersPerSecondSquared,acceleration);
                var omega=observation.AngularVelocityWorldRadiansPerSecond;
                // The identity still denotes the actual current physical sample.
                // Provenance names BOTH raw endpoints used for acceleration.
                string provenance=PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(new{Version,Fingerprint,
                    Previous=_receipt!.RawObservationSha256,Current=digest}));
                input=new(observation.PhysicsStep,observation.SampleId,provenance,
                    AvatarSkullVector.Dot(omega,observation.RightWorld),AvatarSkullVector.Dot(omega,observation.UpWorld),
                    AvatarSkullVector.Dot(omega,observation.ForwardWorld),
                    AvatarSkullVector.Dot(gravitoInertial,observation.RightWorld),-AvatarSkullVector.Dot(gravitoInertial,observation.UpWorld),
                    AvatarSkullVector.Dot(gravitoInertial,observation.ForwardWorld));
                if (!acceleration.Finite || !gravitoInertial.Finite ||
                    !new[]{input.PitchVelocityRadiansPerSecond,input.YawVelocityRadiansPerSecond,input.RollVelocityRadiansPerSecond,
                        input.GravitoInertialRightMetersPerSecondSquared,input.GravitoInertialDownMetersPerSecondSquared,
                        input.GravitoInertialForwardMetersPerSecondSquared}.All(double.IsFinite))
                    throw new ArithmeticException("Nonfinite head derivative/projection; preserve the prior endpoint.");
            }
            var receipt=new AvatarSkullKinematicReceipt(observation.PhysicsStep,time.PhysicalMs,time.NeuralMs,
                original,digest,_receipt?.RawObservationSha256,_previous?.PhysicsStep,acceleration,input);
            _previous=observation; _receipt=receipt; return receipt;
        }
        catch(Exception error) { Faulted=true; FaultReason=error.ToString(); throw; }
    }

    private void Validate(AvatarSkullObservation s)
    {
        if(s.HeadBodyId!=_head || s.SensorId!=_sensor || s.LocalPointMeters!=_local ||
            s.SampleId!=PhysicalSampleProtocol.Identity(_clock,s.PhysicsStep))
            throw new ArgumentException("Head, material point or original physical identity differs.");
        _=_clock.AtStep(s.PhysicsStep);
        var vectors=new[]{s.LocalPointMeters,s.OriginWorldMeters,s.RightWorld,s.UpWorld,s.ForwardWorld,
            s.PointWorldMeters,s.PointVelocityWorldMetersPerSecond,s.AngularVelocityWorldRadiansPerSecond,s.GravityWorldMetersPerSecondSquared};
        if(vectors.Any(v=>v is null||!v.Finite)) throw new ArgumentException("Every head measurement must be present and finite.");
        var axes=new[]{s.RightWorld,s.UpWorld,s.ForwardWorld};
        if(axes.Any(v=>Math.Abs(AvatarSkullVector.Dot(v,v)-1)>BasisTolerance)||
            Math.Abs(AvatarSkullVector.Dot(axes[0],axes[1]))>BasisTolerance||
            Math.Abs(AvatarSkullVector.Dot(axes[0],axes[2]))>BasisTolerance||
            Math.Abs(AvatarSkullVector.Dot(axes[1],axes[2]))>BasisTolerance||
            AvatarSkullVector.Subtract(AvatarSkullVector.Cross(axes[0],axes[1]),axes[2]).MaximumComponent>BasisTolerance)
            throw new ArgumentException("Require a finite unit right/up/forward rotation basis; no normalization or reflection repair.");
        var offset=AvatarSkullVector.Add(AvatarSkullVector.Scale(axes[0],_local.X),
            AvatarSkullVector.Add(AvatarSkullVector.Scale(axes[1],_local.Y),AvatarSkullVector.Scale(axes[2],_local.Z)));
        var expected=AvatarSkullVector.Add(s.OriginWorldMeters,offset);
        var residual=AvatarSkullVector.Subtract(expected,s.PointWorldMeters);
        if(!expected.Finite||!residual.Finite||residual.MaximumComponent>PointGeometryToleranceMeters)
            throw new ArgumentException("Measured point differs from the declared fixed head location.");
    }

    private static bool ValidKey(string? value)=>value is {Length:>0 and <=128}&&!string.IsNullOrWhiteSpace(value)&&
        value==value.Trim()&&!value.Any(char.IsControl);
    private void RequireOwner()
    {
        if(Environment.CurrentManagedThreadId!=_owner) throw new InvalidOperationException("One thread owns the head observation history.");
    }
}
