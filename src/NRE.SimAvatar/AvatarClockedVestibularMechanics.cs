using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

// The body-to-skull transformation and the provenance of these measured values
// require separate qualification. An observation hash is identity, not proof of
// that transformation or authentication. No normalized activity or spikes here.
public sealed record AvatarVestibularKinematics(long PhysicsStep, Guid PhysicalSampleId,
    string PhysicalObservationSha256, double PitchVelocityRadiansPerSecond,
    double YawVelocityRadiansPerSecond, double RollVelocityRadiansPerSecond,
    double GravitoInertialRightMetersPerSecondSquared, double GravitoInertialDownMetersPerSecondSquared,
    double GravitoInertialForwardMetersPerSecondSquared);

// All six entries have angular-velocity units (rad/s), including Cupula.
// Cupula is the inherited reduced filter coordinate, not physical displacement.
public sealed record AvatarVestibularCanals(double LeftHorizontal, double RightHorizontal,
    double LeftAnterior, double RightAnterior, double LeftPosterior, double RightPosterior);
public sealed record AvatarVestibularVector(double Right, double Down, double Forward);
public sealed record AvatarVestibularMechanicalState(string Version, string Fingerprint,
    long? CompletedThroughStep, double? PhysicalTimeMs, double? NeuralTimeMs, long IntegratedIntervals,
    AvatarVestibularCanals Cupula, AvatarVestibularCanals Adaptation, AvatarVestibularVector Otolith,
    bool Faulted, string? FaultReason);
public sealed record AvatarVestibularMechanicalReceipt(Guid SessionId, long PhysicsStep, Guid PhysicalSampleId,
    string InputSha256, AvatarVestibularMechanicalState State);

// Isolated mechanics candidate. Preserves the existing labyrinth's reduced
// time constants and canal axes, but solves its coupled linear filters exactly
// for a held input. These parameters are NOT a calibrated human labyrinth.
// Nothing in live Avatar, Unity or ControlProgram currently selects this type.
public sealed class AvatarClockedVestibularMechanics
{
    public const string Version = "isolated-clocked-vestibular-mechanics-v1";
    public const double CupulaSeconds = .045, AdaptationSeconds = 5, OtolithSeconds = .080;
    private readonly PhysicalSampleClock _clock;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly double _seconds;
    private readonly long _firstObservationStep;
    private AvatarVestibularKinematics? _held;
    private AvatarVestibularMechanicalReceipt? _receipt;
    private AvatarVestibularMechanicalState _state;
    public string Fingerprint { get; }

    public AvatarClockedVestibularMechanics(PhysicalSampleClock clock, long? firstObservationStep = null)
    {
        ArgumentNullException.ThrowIfNull(clock);clock.Validate();
        _seconds=clock.PhysicsStepMs/1000;
        if (!double.IsFinite(_seconds)||_seconds<=0)
            throw new ArgumentException("Physical step cannot be represented in seconds.");
        _clock=clock;
        _firstObservationStep=firstObservationStep??clock.InitialPhysicsStep;
        _=clock.AtStep(_firstObservationStep);
        Fingerprint=Hash(JsonSerializer.Serialize(new{Version,Clock=clock.Fingerprint,CupulaSeconds,
            AdaptationSeconds,OtolithSeconds,InputConvention="previous endpoint held over next physical interval",
            Initialization="zero canal state; otolith explicitly initialized to first observed vector; no elapsed time",
            Axes="pitch/yaw/roll; LARP=(pitch-roll)/sqrt(2); RALP=(pitch+roll)/sqrt(2)"}));
        string version=Version;
        if(_firstObservationStep!=clock.InitialPhysicsStep)
        {
            version+="+declared-later-initial-observation-v1";
            Fingerprint=Hash(JsonSerializer.Serialize(new{Base=Fingerprint,version,FirstObservationStep=_firstObservationStep}));
        }
        var zero=new AvatarVestibularCanals(0,0,0,0,0,0);
        _state=new(version,Fingerprint,null,null,null,0,zero,zero,new(0,0,0),false,null);
    }

    public AvatarVestibularMechanicalState Capture() { RequireOwner();return _state; }

    public AvatarVestibularMechanicalReceipt Consume(AvatarVestibularKinematics input)
    {
        RequireOwner();ArgumentNullException.ThrowIfNull(input);
        if (_state.Faulted) throw new InvalidOperationException("Faulted vestibular epoch cannot resume or reset.");
        ValidateInput(input);
        string digest=Hash(JsonSerializer.Serialize(input));
        if (_receipt is not null && input.PhysicsStep==_receipt.PhysicsStep)
        {
            if (_receipt.InputSha256!=digest) throw new ArgumentException("Conflicting physical input replay.");
            return _receipt;
        }
        long next=_state.CompletedThroughStep is { } previous ? checked(previous+1) : _firstObservationStep;
        if(input.PhysicsStep!=next) throw new ArgumentException("Require the original next physical sample; missing/reversed steps cannot reset the organ.");
        var time=_clock.AtStep(input.PhysicsStep);
        AvatarVestibularMechanicalState updated;
        try
        {
            if (_held is null)
            {
                // Explicit initial condition, not an update of invented duration.
                updated=_state with{CompletedThroughStep=input.PhysicsStep,PhysicalTimeMs=time.PhysicalMs,
                    NeuralTimeMs=time.NeuralMs,Otolith=Vector(input)};
            }
            else
            {
                // Current endpoint values become available only NOW. Advancing
                // with them would apply future information to this past interval.
                // This is a declared zero-order hold approximation to motion.
                var drives=Directions(_held);
                double ea=Math.Exp(-_seconds/AdaptationSeconds),ec=Math.Exp(-_seconds/CupulaSeconds);
                double coupling=AdaptationSeconds/(AdaptationSeconds-CupulaSeconds)*ea*
                    OneMinusExpNegative(_seconds*(1/CupulaSeconds-1/AdaptationSeconds));
                var a=Values(_state.Adaptation);var c=Values(_state.Cupula);
                for(int i=0;i<6;i++)
                {
                    double difference=drives[i]-a[i];
                    c[i]=c[i]*ec+difference*coupling;
                    a[i]+=difference*OneMinusExpNegative(_seconds/AdaptationSeconds);
                }
                var v=Vector(_held);var old=_state.Otolith;double alpha=OneMinusExpNegative(_seconds/OtolithSeconds);
                var otolith=new AvatarVestibularVector(old.Right+(v.Right-old.Right)*alpha,
                    old.Down+(v.Down-old.Down)*alpha,old.Forward+(v.Forward-old.Forward)*alpha);
                if(!a.All(double.IsFinite)||!c.All(double.IsFinite)||!double.IsFinite(otolith.Right)||
                    !double.IsFinite(otolith.Down)||!double.IsFinite(otolith.Forward))
                    throw new ArithmeticException("Nonfinite vestibular transition; preserve the prior state/input without clipping.");
                updated=_state with{CompletedThroughStep=input.PhysicsStep,PhysicalTimeMs=time.PhysicalMs,
                    NeuralTimeMs=time.NeuralMs,IntegratedIntervals=checked(_state.IntegratedIntervals+1),
                    Cupula=Canals(c),Adaptation=Canals(a),Otolith=otolith};
            }
            var receipt=new AvatarVestibularMechanicalReceipt(_clock.SessionId,input.PhysicsStep,input.PhysicalSampleId,digest,updated);
            _state=updated;_held=input;_receipt=receipt;return receipt;
        }
        catch(Exception error)
        {
            _state=_state with{Faulted=true,FaultReason=error.ToString()};throw;
        }
    }

    // Available for a failed-epoch diagnostic. Records are immutable snapshots.
    public AvatarVestibularKinematics? HeldInput() { RequireOwner();return _held; }

    private void ValidateInput(AvatarVestibularKinematics input)
    {
        _=_clock.AtStep(input.PhysicsStep);
        if(input.PhysicalSampleId!=PhysicalSampleProtocol.Identity(_clock,input.PhysicsStep)||
            input.PhysicalObservationSha256 is null||input.PhysicalObservationSha256.Length!=64||
            input.PhysicalObservationSha256.Any(c=>!(c is >= '0' and <= '9' or >= 'A' and <= 'F'))||
            !double.IsFinite(input.PitchVelocityRadiansPerSecond)||!double.IsFinite(input.YawVelocityRadiansPerSecond)||
            !double.IsFinite(input.RollVelocityRadiansPerSecond)||!double.IsFinite(input.GravitoInertialRightMetersPerSecondSquared)||
            !double.IsFinite(input.GravitoInertialDownMetersPerSecondSquared)||!double.IsFinite(input.GravitoInertialForwardMetersPerSecondSquared))
            throw new ArgumentException("Require identified, finite skull measurements on the declared physical clock.");
    }

    private static AvatarVestibularVector Vector(AvatarVestibularKinematics input)=>new(
        input.GravitoInertialRightMetersPerSecondSquared,input.GravitoInertialDownMetersPerSecondSquared,
        input.GravitoInertialForwardMetersPerSecondSquared);
    private static double[] Directions(AvatarVestibularKinematics input)
    {
        double left=(input.PitchVelocityRadiansPerSecond-input.RollVelocityRadiansPerSecond)/Math.Sqrt(2);
        double right=(input.PitchVelocityRadiansPerSecond+input.RollVelocityRadiansPerSecond)/Math.Sqrt(2);
        return [input.YawVelocityRadiansPerSecond,-input.YawVelocityRadiansPerSecond,left,right,-right,-left];
    }
    private static double[] Values(AvatarVestibularCanals c)=>[c.LeftHorizontal,c.RightHorizontal,c.LeftAnterior,c.RightAnterior,c.LeftPosterior,c.RightPosterior];
    private static AvatarVestibularCanals Canals(double[] values)=>new(values[0],values[1],values[2],values[3],values[4],values[5]);
    private static double OneMinusExpNegative(double x)=>x<1e-5 ? x*(1-x*(.5-x*(1d/6-x*(1d/24-x/120)))) : 1-Math.Exp(-x);
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private void RequireOwner()
    {
        if(Environment.CurrentManagedThreadId!=_owner) throw new InvalidOperationException("One physical owner advances the vestibular mechanics.");
    }
}
