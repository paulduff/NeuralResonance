using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Clock/handoff controls use declared synthetic mechanics, not human anatomy.
public sealed class ClockedCoupledCanalTests
{
    private static readonly AvatarSkullVector Zero=new(0,0,0),X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    private static readonly AvatarCanalMechanicalPoint Rest=new(default,default);
    private static PhysicalSampleClock Clock(double stepMs=1)=>new(Guid.NewGuid(),"body","head",500,stepMs,32,1000);
    private static AvatarCanalPrimeBasis Basis()=>new("synthetic Cartesian control",new string('A',64),X,Y,Z);
    private static AvatarCoupledCanalParameters Parameters()
    {
        var m=new double[36];var c=new double[36];var k=new double[36];var f=new double[18];
        for(int i=0;i<6;i++){m[6*i+i]=1;c[6*i+i]=2;if(i>=3)k[6*i+i]=1;}
        f[0]=1;f[9]=1;return new("synthetic clock mechanics",new string('A',64),m,c,k,f);
    }
    private static AvatarClockedCoupledCanal Owner(PhysicalSampleClock c,int n=4,AvatarCanalMechanicalPoint? initial=null)=>
        new(c,"head","centre",Zero,Basis(),Parameters(),initial??Rest,n);
    private static AvatarSkullObservation Input(PhysicalSampleClock c,long step,double roll=0)=>new(step,PhysicalSampleProtocol.Identity(c,step),
        "head","centre",Zero,Zero,X,Y,Z,Zero,Zero,new(0,0,roll),new(0,-9.80665,0));
    private static void Initialize(AvatarClockedCoupledCanal owner,PhysicalSampleClock c,double roll=1)
    { owner.Consume(Input(c,500));owner.Consume(Input(c,501,roll)); }

    [Fact]
    public void FirstTwoObservationsDoNotInventAnElapsedMechanicalInterval()
    {
        var c=Clock();var point=new AvatarCanalMechanicalPoint(new(0,0,0,.01,0,0),default);var owner=Owner(c,initial:point);
        var first=owner.Consume(Input(c,500));Assert.Null(first.Input.Projection);Assert.Null(first.Mechanics.CompletedThroughStep);
        Assert.Null(first.CompletedInterval);Assert.Equal(point,first.Mechanics.Point);
        var second=owner.Consume(Input(c,501,1));Assert.NotNull(second.Input.Projection);Assert.Equal(501,second.Mechanics.CompletedThroughStep);
        Assert.Equal(c.AtStep(501).NeuralMs,second.Mechanics.NeuralTimeMs);Assert.Equal(point,second.Mechanics.Point);
        Assert.Equal(0,second.Mechanics.IntegratedIntervals);Assert.Equal(0,second.Mechanics.IntegratedSubsteps);Assert.Null(second.CompletedInterval);
    }

    [Fact]
    public void NewlyMeasuredAccelerationCannotChangeTheIntervalJustCompleted()
    {
        var c=Clock();var a=Owner(c);var b=Owner(c);Initialize(a,c);Initialize(b,c);
        var left=a.Consume(Input(c,502,20));var right=b.Consume(Input(c,502,-10));
        Assert.Equal(left.Mechanics,right.Mechanics);Assert.NotEqual(left.Input.Projection,right.Input.Projection);
        Assert.NotEqual(left.CompletedInterval!.Fingerprint,right.CompletedInterval!.Fingerprint);
        a.AcknowledgeInterval(left.CompletedInterval.Fingerprint);b.AcknowledgeInterval(right.CompletedInterval.Fingerprint);
        a.Consume(Input(c,503));b.Consume(Input(c,503));Assert.NotEqual(a.Capture().Mechanics.Point,b.Capture().Mechanics.Point);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void WrapperUsesExactlyTheDeclaredQualifiedCoreStepsAndHeldInput(int n)
    {
        var c=Clock();var owner=Owner(c,n);Initialize(owner,c);var receipt=owner.Consume(Input(c,502,-7));var interval=receipt.CompletedInterval!;
        Assert.Equal(n,interval.Substeps.Count);Assert.Equal(501,interval.HeldInput.Skull.PhysicsStep);Assert.Equal(502,interval.ClosingInput.Skull.PhysicsStep);
        var core=new AvatarCoupledCanalDynamics(Parameters(),c.PhysicsStepMs/1000/n);var point=interval.Before.Point;
        foreach(var saved in interval.Substeps)
        {
            var expected=core.AdvanceHeld(point,interval.HeldInput.Projection!.Components);
            Assert.Equal(expected,saved.Transition);point=expected.After;
        }
        Assert.Equal(point,receipt.Mechanics.Point);Assert.Equal(1,receipt.Mechanics.IntegratedIntervals);Assert.Equal(n,receipt.Mechanics.IntegratedSubsteps);
    }

    [Fact]
    public void NonzeroEpochAndEveryFractionalSubstepRetainTheOriginalClock()
    {
        var c=Clock(.125);var owner=Owner(c,8);Initialize(owner,c);var receipt=owner.Consume(Input(c,502));var steps=receipt.CompletedInterval!.Substeps;
        for(int i=0;i<8;i++)
        {
            Assert.Equal(9+i,steps[i].ClockSubstepIndex);
            Assert.Equal(c.PhysicalEpochMs+(9+i)*.125/8,steps[i].PhysicalTimeMs);
            Assert.Equal(c.NeuralEpochMs+(9+i)*.125/8,steps[i].NeuralTimeMs);
        }
        Assert.Equal(c.AtStep(502).PhysicalMs,receipt.Mechanics.PhysicalTimeMs);Assert.Equal(c.AtStep(502).NeuralMs,receipt.Mechanics.NeuralTimeMs);
    }

    [Fact]
    public void PendingIntervalBlocksNewInputBeforeEitherOwnerChanges()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);owner.Consume(Input(c,502));var before=owner.Capture();
        Assert.Throws<InvalidOperationException>(()=>owner.Consume(Input(c,503,4)));Assert.Equal(before,owner.Capture());
        Assert.NotNull(owner.Capture().PendingInterval);Assert.Equal(502,owner.Capture().Input.LastSkull!.PhysicsStep);
    }

    [Fact]
    public void ExactLatestReplayIsInertIncludingWhileAcknowledgementIsPending()
    {
        var c=Clock();var owner=Owner(c);var first=Input(c,500);var r=owner.Consume(first);Assert.Same(r,owner.Consume(first with{}));
        owner.Consume(Input(c,501,1));var last=Input(c,502);var done=owner.Consume(last);var before=owner.Capture();
        Assert.Same(done,owner.Consume(last with{}));Assert.Equal(before,owner.Capture());
    }

    [Fact]
    public void AcknowledgementMustNameThePendingIntervalAndItsReplayIsInert()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var interval=owner.Consume(Input(c,502)).CompletedInterval!;var before=owner.Capture();
        Assert.Throws<ArgumentException>(()=>owner.AcknowledgeInterval(new string('B',64)));Assert.Equal(before,owner.Capture());
        owner.AcknowledgeInterval(interval.Fingerprint);Assert.Null(owner.Capture().PendingInterval);var acknowledged=owner.Capture();
        owner.AcknowledgeInterval(interval.Fingerprint);Assert.Equal(acknowledged,owner.Capture());
        Assert.Equal(before.Mechanics,acknowledged.Mechanics);owner.Consume(Input(c,503));Assert.Equal(2,owner.Capture().Mechanics.IntegratedIntervals);
    }

    [Fact]
    public void ConflictingReplayGapsAndForeignIdentityPreserveBothOwners()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var before=owner.Capture();
        Assert.Throws<ArgumentException>(()=>owner.Consume(Input(c,501,3)));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Input(c,503)));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Input(c,502) with{SampleId=Guid.NewGuid()}));
        Assert.Equal(before,owner.Capture());Assert.False(owner.Faulted);
    }

    [Fact]
    public void EarlierHeadInputCannotResetTheMechanicalState()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var r=owner.Consume(Input(c,502));owner.AcknowledgeInterval(r.CompletedInterval!.Fingerprint);
        var before=owner.Capture();Assert.Throws<ArgumentException>(()=>owner.Consume(Input(c,500)));Assert.Equal(before,owner.Capture());
    }

    [Fact]
    public void ReturnedSubstepCollectionCannotRewriteRetainedEvidence()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var r=owner.Consume(Input(c,502));var interval=r.CompletedInterval!;
        var list=Assert.IsAssignableFrom<IList<AvatarCanalTimedTransition>>(interval.Substeps);var first=list[0];
        Assert.Throws<NotSupportedException>(()=>list[0]=first with{ClockSubstepIndex=999});
        Assert.Equal(first,owner.Capture().PendingInterval!.Substeps[0]);
    }

    [Fact]
    public void PhysicalAndNumericalDissipationRemainSeparateAcrossIntervals()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);double work=0,physical=0,numerical=0;
        for(int i=502;i<=505;i++)
        {
            var r=owner.Consume(Input(c,i,i-500));foreach(var s in r.CompletedInterval!.Substeps)
            { work+=s.Transition.InputWorkJoules;physical+=s.Transition.PhysicalDampingJoules;numerical+=s.Transition.NumericalDampingJoules; }
            owner.AcknowledgeInterval(r.CompletedInterval.Fingerprint);
        }
        var m=owner.Capture().Mechanics;Assert.True(physical>0);Assert.True(numerical>0);
        Assert.InRange(Math.Abs(work-m.InputWorkJoules),0,1e-12);Assert.InRange(Math.Abs(physical-m.PhysicalDampingJoules),0,1e-12);
        Assert.InRange(Math.Abs(numerical-m.NumericalDampingJoules),0,1e-12);
        Assert.InRange(Math.Abs(m.EnergyIdentityResidualJoules),0,1e-12*(1+Math.Abs(work)+physical+numerical+m.EnergyJoules));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(131072)]
    public void InvalidSubdivisionCannotBeReplacedWithAnImplicitDefault(int n)
    { Assert.Throws<ArgumentException>(()=>Owner(Clock(),n)); }

    [Fact]
    public void InitializationSubdivisionAndInputSourceArePartOfOwnerIdentity()
    {
        var c=Clock();var first=Owner(c);
        Assert.NotEqual(first.Fingerprint,Owner(c,8).Fingerprint);
        Assert.NotEqual(first.Fingerprint,Owner(c,initial:new(new(0,0,0,.01,0,0),default)).Fingerprint);
        var p=Parameters();var other=new AvatarCoupledCanalParameters(p.Source,new string('B',64),p.MassKgPerMeter4,p.DampingPascalSecondsPerMeter3,p.StiffnessPascalPerMeter3,p.PressurePascalPerRadianPerSecondSquared);
        Assert.Throws<ArgumentException>(()=>new AvatarClockedCoupledCanal(c,"head","centre",Zero,Basis(),other,Rest,4));
    }

    [Fact]
    public void SubstepClockFailureRetainsTheAttemptedPrefixWithoutPartialPhysicalCommit()
    {
        var c=Clock(4) with{PhysicalEpochMs=0,NeuralEpochMs=9007199254740986d};var owner=Owner(c);Initialize(owner,c,0);var before=owner.Capture();
        Assert.Throws<ArithmeticException>(()=>owner.Consume(Input(c,502)));
        var failed=owner.Capture();Assert.True(failed.Faulted);Assert.Equal(before.Mechanics,failed.Mechanics);
        Assert.Equal(502,failed.Input.LastSkull!.PhysicsStep);Assert.Equal(2,failed.Failure!.AttemptedIntervalPrefix.Count);
        Assert.Equal(new long[]{5,6},failed.Failure.AttemptedIntervalPrefix.Select(s=>s.ClockSubstepIndex));
        Assert.Null(failed.PendingInterval);Assert.Throws<InvalidOperationException>(()=>owner.Consume(Input(c,502)));
    }

    [Fact]
    public void ExactSubstepIndexExhaustionRejectsBeforeAnObservationIsAccepted()
    {
        var c=Clock();var owner=Owner(c,8);var before=owner.Capture();long future=c.InitialPhysicsStep+PhysicalSampleClock.MaximumExactStep/8+1;
        Assert.Throws<ArgumentOutOfRangeException>(()=>owner.Consume(Input(c,future)));Assert.Equal(before,owner.Capture());
    }

    [Fact]
    public void OneThreadOwnsTheMechanicalStateAndAcknowledgements()
    {
        var c=Clock();var owner=Owner(c);Exception? failure=null;
        var thread=new Thread(()=>{try{owner.Consume(Input(c,500));}catch(Exception e){failure=e;}});thread.Start();thread.Join();
        Assert.IsType<InvalidOperationException>(failure);Assert.Null(owner.Capture().Input.LastSkull);
    }
}
