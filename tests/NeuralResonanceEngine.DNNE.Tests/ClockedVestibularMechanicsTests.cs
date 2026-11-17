using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Mechanical arithmetic and physical-clock ownership only. No sensory neuron,
// calibrated hair cell, body-to-skull transform or balance qualification.
public sealed class ClockedVestibularMechanicsTests
{
    private static PhysicalSampleClock Clock(double stepMs=.125,long initial=0)=>new(
        Guid.Parse("f23b3c4b-5d10-42b0-af21-1e19e62a944a"),"vestibular-body","vestibular-skull-fixture",initial,stepMs,500,1000);
    private static AvatarVestibularKinematics Input(PhysicalSampleClock clock,long step,double yaw=0,double pitch=0,double roll=0,
        double right=0,double down=9.80665,double forward=0)=>new(step,PhysicalSampleProtocol.Identity(clock,step),new string('A',64),
            pitch,yaw,roll,right,down,forward);
    private static double[] Channels(AvatarVestibularCanals c)=>[c.LeftHorizontal,c.RightHorizontal,c.LeftAnterior,c.RightAnterior,c.LeftPosterior,c.RightPosterior];

    [Fact]
    public void Initial_Observation_Does_Not_Advance_An_Invented_Interval()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);
        var receipt=organ.Consume(Input(clock,0,yaw:2,pitch:1,right:3));
        Assert.Equal(0,receipt.State.IntegratedIntervals);Assert.Equal(500,receipt.State.PhysicalTimeMs);
        Assert.All(Channels(receipt.State.Cupula),x=>Assert.Equal(0,x));
        Assert.All(Channels(receipt.State.Adaptation),x=>Assert.Equal(0,x));
        Assert.Equal(3,receipt.State.Otolith.Right);
    }

    [Fact]
    public void Newly_Observed_Endpoint_Cannot_Alter_The_Completed_Past()
    {
        var clock=Clock();var a=new AvatarClockedVestibularMechanics(clock);var b=new AvatarClockedVestibularMechanics(clock);
        a.Consume(Input(clock,0,yaw:1));b.Consume(Input(clock,0,yaw:1));
        var first=a.Consume(Input(clock,1,yaw:8,right:5));var second=b.Consume(Input(clock,1,yaw:-3,right:-7));
        Assert.Equal(first.State,second.State);Assert.NotEqual(first.InputSha256,second.InputSha256);
        a.Consume(Input(clock,2));b.Consume(Input(clock,2));
        Assert.NotEqual(a.Capture().Cupula,b.Capture().Cupula);Assert.NotEqual(a.Capture().Otolith,b.Capture().Otolith);
    }

    [Fact]
    public void Exact_Last_Input_Replay_Is_Inert()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);var input=Input(clock,0,yaw:1);
        var receipt=organ.Consume(input);var state=organ.Capture();
        Assert.Same(receipt,organ.Consume(input with{}));Assert.Same(state,organ.Capture());Assert.Same(input,organ.HeldInput());
    }

    [Fact]
    public void Conflicting_Replay_Cannot_Reset_Or_Replace_The_Organ()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);organ.Consume(Input(clock,0,yaw:1));var state=organ.Capture();
        Assert.Throws<ArgumentException>(()=>organ.Consume(Input(clock,0,yaw:2)));
        Assert.Same(state,organ.Capture());Assert.Equal(1,organ.HeldInput()!.YawVelocityRadiansPerSecond);
    }

    [Fact]
    public void Missing_Physical_Sample_Blocks_Advancement_Before_Mutation()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);organ.Consume(Input(clock,0));var state=organ.Capture();
        Assert.Throws<ArgumentException>(()=>organ.Consume(Input(clock,2)));Assert.Same(state,organ.Capture());
        organ.Consume(Input(clock,1));Assert.Equal(1,organ.Capture().IntegratedIntervals);
    }

    [Fact]
    public void Earlier_Sample_Is_Not_A_Reset_Request()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);
        organ.Consume(Input(clock,0));organ.Consume(Input(clock,1));var state=organ.Capture();
        Assert.Throws<ArgumentException>(()=>organ.Consume(Input(clock,0)));Assert.Same(state,organ.Capture());
    }

    [Fact]
    public void Sample_From_A_Different_Epoch_Is_Rejected()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);var state=organ.Capture();
        Assert.Throws<ArgumentException>(()=>organ.Consume(Input(clock with{SessionId=Guid.NewGuid()},0)));
        Assert.Same(state,organ.Capture());Assert.Null(organ.HeldInput());
    }

    [Fact]
    public void Nonfinite_Or_Unidentified_Input_Cannot_Change_State()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);var state=organ.Capture();
        Assert.Throws<ArgumentException>(()=>organ.Consume(Input(clock,0,yaw:double.NaN)));
        Assert.Throws<ArgumentException>(()=>organ.Consume(Input(clock,0) with{PhysicalObservationSha256="missing"}));
        Assert.Same(state,organ.Capture());
    }

    [Fact]
    public void Source_State_Has_One_Physical_Owner()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);Exception? failure=null;
        var thread=new Thread(()=>{try{organ.Consume(Input(clock,0));}catch(Exception error){failure=error;}});
        thread.Start();thread.Join();Assert.IsType<InvalidOperationException>(failure);Assert.Null(organ.Capture().CompletedThroughStep);
    }

    [Fact]
    public void Nonzero_Epoch_And_Fractional_Physics_Time_Are_Preserved()
    {
        var clock=Clock(initial:500);var organ=new AvatarClockedVestibularMechanics(clock);
        organ.Consume(Input(clock,500,yaw:1));var receipt=organ.Consume(Input(clock,501));
        Assert.Equal(500.125,receipt.State.PhysicalTimeMs);Assert.Equal(1000.125,receipt.State.NeuralTimeMs);
        Assert.Equal(501,receipt.State.CompletedThroughStep);Assert.Equal(1,receipt.State.IntegratedIntervals);
        Assert.InRange(receipt.State.Cupula.LeftHorizontal,0.0027,0.0028);
    }

    [Fact]
    public void Held_Input_Transition_Is_Invariant_To_Physical_Subdivision()
    {
        var fineClock=Clock();var coarseClock=Clock(40);var fine=new AvatarClockedVestibularMechanics(fineClock);
        var coarse=new AvatarClockedVestibularMechanics(coarseClock);
        for(int i=0;i<=320;i++)fine.Consume(Input(fineClock,i,yaw:1,pitch:.5,roll:-.2));
        coarse.Consume(Input(coarseClock,0,yaw:1,pitch:.5,roll:-.2));coarse.Consume(Input(coarseClock,1));
        foreach(var pair in Channels(fine.Capture().Cupula).Zip(Channels(coarse.Capture().Cupula)))
            Assert.InRange(Math.Abs(pair.First-pair.Second),0,1e-12);
        foreach(var pair in Channels(fine.Capture().Adaptation).Zip(Channels(coarse.Capture().Adaptation)))
            Assert.InRange(Math.Abs(pair.First-pair.Second),0,1e-12);
    }

    [Fact]
    public void Physical_Long_Interval_Is_Not_Silently_Clamped()
    {
        var clock=Clock(10000);var organ=new AvatarClockedVestibularMechanics(clock);
        organ.Consume(Input(clock,0,yaw:1));organ.Consume(Input(clock,1));var turn=organ.Capture();
        Assert.InRange(turn.Adaptation.LeftHorizontal,.8646,.8647);Assert.True(turn.Cupula.LeftHorizontal>0);
        organ.Consume(Input(clock,2));Assert.True(organ.Capture().Cupula.LeftHorizontal<0);
        Assert.Equal(20500,organ.Capture().PhysicalTimeMs);
    }

    [Fact]
    public void Bilateral_Canal_Pairs_Retain_Direction()
    {
        var clock=Clock(10);var organ=new AvatarClockedVestibularMechanics(clock);
        organ.Consume(Input(clock,0,yaw:1,pitch:2,roll:.3));organ.Consume(Input(clock,1));var c=organ.Capture().Cupula;
        Assert.True(c.LeftHorizontal>0&&c.LeftAnterior>0&&c.RightAnterior>0);
        Assert.Equal(c.LeftHorizontal,-c.RightHorizontal);Assert.Equal(c.LeftAnterior,-c.RightPosterior);
        Assert.Equal(c.RightAnterior,-c.LeftPosterior);
    }

    [Fact]
    public void Static_Gravity_Does_Not_Create_Canal_Rotation()
    {
        var clock=Clock(10);var organ=new AvatarClockedVestibularMechanics(clock);
        var initial=organ.Consume(Input(clock,0,right:3,down:8,forward:4));
        organ.Consume(Input(clock,1,right:3,down:8,forward:4));
        Assert.Equal(initial.State.Otolith,organ.Capture().Otolith);
        Assert.All(Channels(organ.Capture().Cupula),x=>Assert.Equal(0,x));
    }

    [Fact]
    public void Raw_Mechanics_Are_Not_Normalized_Into_Population_Activations()
    {
        var clock=Clock(100);var organ=new AvatarClockedVestibularMechanics(clock);
        organ.Consume(Input(clock,0,yaw:100,right:200));organ.Consume(Input(clock,1));
        Assert.True(organ.Capture().Cupula.LeftHorizontal>1);Assert.True(organ.Capture().Cupula.RightHorizontal< -1);
        Assert.Equal(200,organ.Capture().Otolith.Right);
    }

    [Fact]
    public void Unrepresentable_Transition_Faults_Without_Resetting_Last_Completed_State()
    {
        var clock=Clock();var organ=new AvatarClockedVestibularMechanics(clock);
        var input=Input(clock,0,pitch:double.MaxValue,roll:-double.MaxValue);organ.Consume(input);var before=organ.Capture();
        Assert.Throws<ArithmeticException>(()=>organ.Consume(Input(clock,1)));
        var after=organ.Capture();Assert.True(after.Faulted);Assert.Equal(before.CompletedThroughStep,after.CompletedThroughStep);
        Assert.Equal(before.Cupula,after.Cupula);Assert.Same(input,organ.HeldInput());
        Assert.Throws<InvalidOperationException>(()=>organ.Consume(Input(clock,1)));
    }
}
