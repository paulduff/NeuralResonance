using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class SkullKinematicsTests
{
    private static readonly AvatarSkullVector Zero=new(0,0,0), Right=new(1,0,0), Up=new(0,1,0), Forward=new(0,0,1);
    private static PhysicalSampleClock Clock(double dt=.125)=>new(Guid.NewGuid(),"avatar","head-physical",500,dt,1234.567,4321.234);
    private static AvatarSkullKinematics Owner(PhysicalSampleClock c,AvatarSkullVector? point=null)=>new(c,"head","labyrinth",point??Zero);
    private static AvatarSkullObservation Observation(PhysicalSampleClock c,long step)=>new(step,PhysicalSampleProtocol.Identity(c,step),
        "head","labyrinth",Zero,Zero,Right,Up,Forward,Zero,Zero,Zero,new(0,-9.80665,0));
    private static void Near(double a,double b)=>Assert.InRange(Math.Abs(a-b),0,1e-11);

    [Fact]
    public void First_Head_Observation_Does_Not_Invent_Acceleration_Or_Elapsed_Mechanics()
    {
        var clock=Clock();var owner=Owner(clock);var first=owner.Consume(Observation(clock,500));
        Assert.Null(first.VestibularInput);Assert.Null(first.AverageAccelerationWorldMetersPerSecondSquared);
        Assert.Null(first.PreviousObservationSha256);Assert.Null(first.AccelerationFromStep);
        var organ=new AvatarClockedVestibularMechanics(clock,owner.FirstMechanicsStep);
        Assert.Null(organ.Capture().CompletedThroughStep);Assert.Equal(0,organ.Capture().IntegratedIntervals);
    }

    [Fact]
    public void Stationary_Head_Retains_Gravity_And_Both_Original_Endpoints()
    {
        var clock=Clock();var owner=Owner(clock);var first=owner.Consume(Observation(clock,500));
        var second=owner.Consume(Observation(clock,501));var input=second.VestibularInput!;
        Assert.Equal(first.RawObservationSha256,second.PreviousObservationSha256);Assert.Equal(500,second.AccelerationFromStep);
        Assert.Equal(Zero,second.AverageAccelerationWorldMetersPerSecondSquared);
        Near(9.80665,input.GravitoInertialDownMetersPerSecondSquared);Near(0,input.GravitoInertialRightMetersPerSecondSquared);
        Assert.Equal(PhysicalSampleProtocol.Identity(clock,501),input.PhysicalSampleId);
    }

    [Fact]
    public void Free_Fall_Has_Zero_GravitoInertial_Acceleration()
    {
        var clock=Clock();var owner=Owner(clock);owner.Consume(Observation(clock,500));
        var next=Observation(clock,501) with{PointVelocityWorldMetersPerSecond=new(0,-9.80665*clock.PhysicsStepMs/1000,0)};
        var input=owner.Consume(next).VestibularInput!;
        Near(0,input.GravitoInertialDownMetersPerSecondSquared);Near(0,input.GravitoInertialForwardMetersPerSecondSquared);
    }

    [Fact]
    public void Gravity_Is_Rotated_Into_The_Actual_Skull_Basis()
    {
        var clock=Clock();var owner=Owner(clock);
        var first=Observation(clock,500) with{UpWorld=Forward,ForwardWorld=new(0,-1,0)};
        owner.Consume(first);var next=first with{PhysicsStep=501,SampleId=PhysicalSampleProtocol.Identity(clock,501)};
        var input=owner.Consume(next).VestibularInput!;
        Near(0,input.GravitoInertialDownMetersPerSecondSquared);Near(9.80665,input.GravitoInertialForwardMetersPerSecondSquared);
    }

    [Fact]
    public void Turning_Does_Not_Create_Acceleration_From_Constant_World_Velocity()
    {
        var clock=Clock();var owner=Owner(clock);var velocity=new AvatarSkullVector(2,0,0);
        owner.Consume(Observation(clock,500) with{PointVelocityWorldMetersPerSecond=velocity});
        var receipt=owner.Consume(Observation(clock,501) with{RightWorld=new(0,0,-1),ForwardWorld=Right,
            PointVelocityWorldMetersPerSecond=velocity,AngularVelocityWorldRadiansPerSecond=new(1,2,3)});
        Assert.Equal(Zero,receipt.AverageAccelerationWorldMetersPerSecondSquared);
        var input=receipt.VestibularInput!;Near(-3,input.PitchVelocityRadiansPerSecond);Near(2,input.YawVelocityRadiansPerSecond);Near(1,input.RollVelocityRadiansPerSecond);
        Near(9.80665,input.GravitoInertialDownMetersPerSecondSquared);
    }

    [Fact]
    public void Fixed_OffCentre_Point_Retains_Rotational_Acceleration()
    {
        var clock=Clock(1000);var local=new AvatarSkullVector(.2,0,0);var owner=Owner(clock,local);double omega=Math.PI/2;
        owner.Consume(Observation(clock,500) with{LocalPointMeters=local,PointWorldMeters=local,
            PointVelocityWorldMetersPerSecond=new(0,0,-.2*omega),AngularVelocityWorldRadiansPerSecond=new(0,omega,0)});
        var receipt=owner.Consume(Observation(clock,501) with{LocalPointMeters=local,RightWorld=new(0,0,-1),ForwardWorld=Right,
            PointWorldMeters=new(0,0,-.2),PointVelocityWorldMetersPerSecond=new(-.2*omega,0,0),AngularVelocityWorldRadiansPerSecond=new(0,omega,0)});
        var a=receipt.AverageAccelerationWorldMetersPerSecondSquared!;
        Near(-.2*omega,a.X);Near(.2*omega,a.Z); // Interval average, not the final instantaneous centripetal acceleration.
    }

    [Fact]
    public void Delayed_Mechanical_Initialization_Keeps_The_Original_Physical_Clock()
    {
        var clock=Clock(.000001);var owner=Owner(clock);var organ=new AvatarClockedVestibularMechanics(clock,owner.FirstMechanicsStep);
        owner.Consume(Observation(clock,500));var first=owner.Consume(Observation(clock,501));var initialized=organ.Consume(first.VestibularInput!);
        Assert.Equal(clock.AtStep(501).PhysicalMs,initialized.State.PhysicalTimeMs);Assert.Equal(0,initialized.State.IntegratedIntervals);
        var next=organ.Consume(owner.Consume(Observation(clock,502)).VestibularInput!);
        Assert.Equal(clock.AtStep(502).NeuralMs,next.State.NeuralTimeMs);Assert.Equal(1,next.State.IntegratedIntervals);
        Assert.NotEqual(new AvatarClockedVestibularMechanics(clock).Fingerprint,organ.Fingerprint);
    }

    [Fact]
    public void New_Endpoint_Acceleration_Cannot_Act_On_The_Completed_Past_Interval()
    {
        var clock=Clock();var a=Owner(clock);var b=Owner(clock);
        var x=new AvatarClockedVestibularMechanics(clock,a.FirstMechanicsStep);var y=new AvatarClockedVestibularMechanics(clock,b.FirstMechanicsStep);
        a.Consume(Observation(clock,500));b.Consume(Observation(clock,500));
        x.Consume(a.Consume(Observation(clock,501)).VestibularInput!);y.Consume(b.Consume(Observation(clock,501)).VestibularInput!);
        x.Consume(a.Consume(Observation(clock,502)).VestibularInput!);
        y.Consume(b.Consume(Observation(clock,502) with{PointVelocityWorldMetersPerSecond=new(10,20,30)}).VestibularInput!);
        Assert.Equal(x.Capture(),y.Capture());
    }

    [Fact]
    public void Exact_Retry_Is_Inert_And_Conflicting_Retry_Gaps_And_Reversal_Are_Rejected()
    {
        var clock=Clock();var owner=Owner(clock);var raw=Observation(clock,500);var first=owner.Consume(raw);
        Assert.Same(first,owner.Consume(raw with{}));
        Assert.Throws<ArgumentException>(()=>owner.Consume(raw with{PointVelocityWorldMetersPerSecond=Right}));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Observation(clock,502)));Assert.Same(first,owner.Capture());
        var next=owner.Consume(Observation(clock,501));Assert.Throws<ArgumentException>(()=>owner.Consume(raw));Assert.Same(next,owner.Capture());
    }

    [Fact]
    public void Foreign_Head_Point_Session_And_Reflected_Or_Nonunit_Bases_Are_Rejected_Before_Mutation()
    {
        var clock=Clock();var owner=Owner(clock);var raw=Observation(clock,500);
        foreach(var bad in new[]{raw with{HeadBodyId="pelvis"},raw with{SensorId="other"},raw with{SampleId=Guid.NewGuid()},
            raw with{RightWorld=new(-1,0,0)},raw with{UpWorld=new(0,2,0)},raw with{PointWorldMeters=Right},
            raw with{LocalPointMeters=Right},raw with{PointVelocityWorldMetersPerSecond=new(double.NaN,0,0)}})
            Assert.Throws<ArgumentException>(()=>owner.Consume(bad));
        Assert.Null(owner.Capture());Assert.False(owner.Faulted);
    }

    [Fact]
    public void Derivative_Overflow_Preserves_The_Last_Endpoint_And_Faults_The_Epoch()
    {
        var clock=Clock();var owner=Owner(clock);var first=owner.Consume(Observation(clock,500) with{PointVelocityWorldMetersPerSecond=new(double.MaxValue/4,0,0)});
        Assert.Throws<ArithmeticException>(()=>owner.Consume(Observation(clock,501) with{PointVelocityWorldMetersPerSecond=new(-double.MaxValue/4,0,0)}));
        Assert.True(owner.Faulted);Assert.Same(first,owner.Capture());Assert.Throws<InvalidOperationException>(()=>owner.Consume(Observation(clock,501)));
    }

    [Fact]
    public void A_Different_Thread_Cannot_Consume_The_Head_History()
    {
        var clock=Clock();var owner=Owner(clock);Exception? error=null;
        var thread=new Thread(()=>{try{owner.Consume(Observation(clock,500));}catch(Exception e){error=e;}});thread.Start();thread.Join();
        Assert.IsType<InvalidOperationException>(error);Assert.Null(owner.Capture());
    }
}
