#nullable enable
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

internal enum PrescribedHeadAxis { Pitch, Yaw, Roll }
internal sealed record PrescribedHeadTurnSample(AvatarSkullObservation Observation,double AngleRadians,
    double AngularSpeedRadiansPerSecond,double AngularAccelerationRadiansPerSecondSquared,
    AvatarSkullVector PointAccelerationWorldMetersPerSecondSquared);

// Analytic diagnostic input, never a brain command or measured Unity motion.
// A C2 quintic turn starts and ends at rest, followed by a held orientation.
// Existing skull mechanics still derives its own causal sampled acceleration.
internal sealed class PrescribedHeadTurn
{
    internal const string Version="prescribed-quintic-head-turn-v1";
    private static readonly AvatarSkullVector Zero=new(0,0,0);
    private readonly AvatarSkullObservation _rest;
    private readonly AvatarSkullVector _axis;
    private readonly double _angle,_seconds;
    private readonly int _turnSteps;
    internal PhysicalSampleClock Clock { get; }
    internal long StartStep { get; }
    internal long EndStep { get; }
    internal string Fingerprint { get; }

    internal PrescribedHeadTurn(PhysicalSampleClock clock,AvatarSkullObservation rest,
        PrescribedHeadAxis axis,double signedAngleRadians,int startOffsetSteps,int turnSteps)
    {
        ArgumentNullException.ThrowIfNull(clock);ArgumentNullException.ThrowIfNull(rest);clock.Validate();
        if(!Enum.IsDefined(axis)||!double.IsFinite(signedAngleRadians)||Math.Abs(signedAngleRadians)>Math.PI||
            startOffsetSteps<2||turnSteps<2||rest.PhysicsStep!=clock.InitialPhysicsStep||
            rest.PointVelocityWorldMetersPerSecond!=Zero||rest.AngularVelocityWorldRadiansPerSecond!=Zero)
            throw new ArgumentException("Declare a stationary initial head, axis, bounded signed excursion and at least two quiet/turn steps.");
        // Reuse the actual adapter's geometry/identity checks; no normalization,
        // anatomical repair or substitute pelvis frame is permitted.
        new AvatarSkullKinematics(clock,rest.HeadBodyId,rest.SensorId,rest.LocalPointMeters).Consume(rest);
        StartStep=checked(clock.InitialPhysicsStep+startOffsetSteps);EndStep=checked(StartStep+turnSteps);
        _=clock.AtStep(checked(EndStep+1));
        _seconds=turnSteps*(clock.PhysicsStepMs/1000);
        if(!double.IsFinite(_seconds)||_seconds<=0||!double.IsFinite(signedAngleRadians/_seconds)||
            !double.IsFinite(signedAngleRadians/_seconds/_seconds))
            throw new ArgumentException("Prescribed angular derivatives must be representable.");
        _axis=axis switch {PrescribedHeadAxis.Pitch=>rest.RightWorld,PrescribedHeadAxis.Yaw=>rest.UpWorld,_=>rest.ForwardWorld};
        _rest=rest;Clock=clock;_angle=signedAngleRadians;_turnSteps=turnSteps;
        Fingerprint=PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(new {Version,Clock=clock.Fingerprint,rest,axis,
            signedAngleRadians,startOffsetSteps,turnSteps,Units="metres, seconds and radians; fixed origin and world gravity",
            Scope="prescribed diagnostic movement; neither native motion nor neuronal authority"}));
    }

    internal PrescribedHeadTurnSample AtStep(long step)
    {
        _=Clock.AtStep(step);
        double angle=0,speed=0,acceleration=0;
        if(step>=EndStep)angle=_angle;
        else if(step>StartStep)
        {
            double u=(double)(step-StartStep)/_turnSteps,v=1-u;
            angle=_angle*u*u*u*(10+u*(-15+6*u));
            speed=(_angle/_seconds)*30*u*u*v*v;
            acceleration=(_angle/_seconds/_seconds)*60*u*v*(1-2*u);
        }
        var right=Rotate(_rest.RightWorld,angle);var up=Rotate(_rest.UpWorld,angle);var forward=Rotate(_rest.ForwardWorld,angle);
        var local=_rest.LocalPointMeters;
        var offset=Add(Scale(right,local.X),Add(Scale(up,local.Y),Scale(forward,local.Z)));
        var omega=speed==0?Zero:Scale(_axis,speed);var alpha=acceleration==0?Zero:Scale(_axis,acceleration);
        var velocity=speed==0?Zero:Cross(omega,offset);
        var pointAcceleration=speed==0&&acceleration==0?Zero:Add(Cross(alpha,offset),Cross(omega,Cross(omega,offset)));
        var observation=_rest with {PhysicsStep=step,SampleId=PhysicalSampleProtocol.Identity(Clock,step),
            RightWorld=right,UpWorld=up,ForwardWorld=forward,
            PointWorldMeters=angle==0?_rest.PointWorldMeters:Add(_rest.OriginWorldMeters,offset),
            PointVelocityWorldMetersPerSecond=velocity,AngularVelocityWorldRadiansPerSecond=omega};
        if(!Finite(right)||!Finite(up)||!Finite(forward)||!Finite(observation.PointWorldMeters)||
            !Finite(velocity)||!Finite(omega)||!Finite(pointAcceleration)||!double.IsFinite(angle)||
            !double.IsFinite(speed)||!double.IsFinite(acceleration))
            throw new ArithmeticException("Unrepresentable prescribed head sample; no clipped substitute.");
        return new(observation,angle,speed,acceleration,pointAcceleration);
    }

    private AvatarSkullVector Rotate(AvatarSkullVector vector,double angle)
    {
        if(angle==0)return vector;
        double c=Math.Cos(angle),s=Math.Sin(angle);
        return Add(Scale(vector,c),Add(Scale(Cross(_axis,vector),s),Scale(_axis,Dot(_axis,vector)*(1-c))));
    }
    private static bool Finite(AvatarSkullVector v)=>double.IsFinite(v.X)&&double.IsFinite(v.Y)&&double.IsFinite(v.Z);
    private static double Dot(AvatarSkullVector a,AvatarSkullVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    private static AvatarSkullVector Scale(AvatarSkullVector a,double s)=>new(a.X*s,a.Y*s,a.Z*s);
    private static AvatarSkullVector Add(AvatarSkullVector a,AvatarSkullVector b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    private static AvatarSkullVector Cross(AvatarSkullVector a,AvatarSkullVector b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
}
