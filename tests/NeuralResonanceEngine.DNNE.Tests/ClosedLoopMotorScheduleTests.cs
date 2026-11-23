using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Scheduling controls with prescribed motor packets and an analytic body.
// These do not establish active afferent firing or complete brain/body accuracy.
public sealed class ClosedLoopMotorScheduleTests
{
    static readonly Guid Session = Guid.Parse("ad38ad52-4028-4da3-8d29-5be843a76961");
    static readonly Guid Projection = Guid.Parse("ec0981dc-0eec-4357-91e4-304716d8f5ea");
    static string Json(object value) => JsonSerializer.Serialize(value);
    static DynamicMuscleGeometry Geometry => DynamicMuscleGeometry.NagamoriTibialisAnterior;
    static MotorAxonContact Contact(string name) => MotorAxonContact.Create("source-"+name, "cell-"+name, Projection, "unit-"+name, .015625);
    static CausalMuscleDefinition Definition(string name) => new(name, Geometry,
        [SlowMotorUnitDefinition.NagamoriSlowUnit1(Contact(name))], Geometry.OptimalFiberLengthMeters);
    static double RestLength()
    {
        var g=Geometry;
        double passive=23*.046*Math.Log(1+Math.Exp((1/g.PassiveLengthScale-1.17)/.046));
        double tendon=.964+.0047*Math.Log(Math.Exp(passive*Math.Cos(g.PennationRadians)/(27.8*.0047))-1);
        return g.OptimalFiberLengthMeters*Math.Cos(g.PennationRadians)+g.ReferenceTendonLengthMeters*tendon;
    }
    static SpikeMessage Spike(double time, int ordinal=1, string name="a") => new()
    {
        MessageId=StableNeuralIdentity.Create("split-body-fixture", name, ordinal), TimestampMs=time,
        SourceStructure=StructureId.SpinalCordMotor, SourceInstanceKey="source-"+name, SourceNeuronId="cell-"+name,
        ProjectionId=Projection, SynapseId=StableNeuralIdentity.Create("split-body-synapse", name), Neurotransmitter=NTEnum.ACETYLCHOLINE
    };
    sealed class Body(PhysicalSampleClock clock, string[] names) : IAvatarMotorMechanicalBody
    {
        public string Fingerprint => "analytic-split-clock-body-v1:"+string.Join(",",names);
        public long CompletedPhysicsStep { get; private set; } = clock.InitialPhysicsStep;
        public double PhysicsStepMs => clock.PhysicsStepMs;
        public double Angle, Speed;
        public int Advances;
        public bool LoseReceipt;
        Guid lastEffort;
        public MotorMechanicalFrame CaptureGeometry() => new(clock.SessionId,Fingerprint,CompletedPhysicsStep,
            clock.AtStep(CompletedPhysicsStep).PhysicalMs,lastEffort,
            names.Select(n=>new MusculotendonPathMeasurement(n,RestLength()-.02*Angle,-.02*Speed)).ToArray());
        public void AdvanceOneStep(MotorMechanicalEffort effort)
        {
            Assert.Equal(CompletedPhysicsStep,effort.StartStep);
            Assert.Equal(clock.AtStep(CompletedPhysicsStep).PhysicalMs,effort.StartTimeMs);
            Assert.Equal(clock.AtStep(CompletedPhysicsStep+1).PhysicalMs,effort.EndTimeMs);
            double seconds=PhysicsStepMs/1000, torque=effort.Tensions.Sum(t=>.02*t.AxialNewtons);
            // Exact constant-torque step, inertia 1 kg m². No hidden controller.
            Angle+=Speed*seconds+.5*torque*seconds*seconds;Speed+=torque*seconds;
            Advances++;CompletedPhysicsStep++;lastEffort=effort.EffortId;
            if(LoseReceipt)throw new IOException("Physical step happened; reply unavailable.");
        }
    }
    sealed class Rig
    {
        public readonly PhysicalSampleClock Clock = new(Session,"split-body","paths",7,.125,10,1010);
        public readonly Body Body;
        public readonly AvatarCausalMotorPlant Plant;
        readonly Dictionary<string,MotorAxonPublisher> publishers;
        public Rig(CausalMotorSchedule schedule=CausalMotorSchedule.BodyThenMotor,bool pair=false,int substeps=1)
        {
            string[] names=pair?["a","b"]:["a"];
            Body=new(Clock,names);
            var definitions=names.Select(Definition).ToArray();
            var anatomy=new MotorAxonAnatomy(definitions.SelectMany(d=>d.Units).Select(u=>u.Contact));
            var motorClock=new MotorAxonClock(Session,Clock.NeuralEpochMs,Clock.PhysicalEpochMs);
            publishers=names.ToDictionary(n=>n,n=>new MotorAxonPublisher(anatomy,motorClock,"source-"+n));
            Plant=new(Clock,Body,definitions,substeps,schedule:schedule);
        }
        public void Start(){Plant.CaptureInitial();Plant.Resume();}
        public MotorAxonBatch Feed(double through,SpikeMessage[] events,string name="a")
        {var b=publishers[name].Publish(events,through);Plant.Accept(b);return b;}
        public CausalMotorPendingStep Begin()
        {var r=Plant.TryAdvanceBody();Assert.Equal(CausalBodyAdvance.Advanced,r.Outcome);return Assert.IsType<CausalMotorPendingStep>(r.Step);}
        public CausalMotorStep Close()
        {var r=Plant.TryCompleteMotorInterval();Assert.Equal(CausalMotorAdvance.Advanced,r.Outcome);return Assert.IsType<CausalMotorStep>(r.Step);}
    }

    [Fact]
    public void ExistingScheduleStillRequiresCompleteMotorInputBeforePhysics()
    {
        var rig=new Rig(CausalMotorSchedule.MotorBeforeBody);rig.Start();
        Assert.Throws<InvalidOperationException>(()=>rig.Plant.TryAdvanceBody());
        Assert.Throws<InvalidOperationException>(()=>rig.Plant.TryCompleteMotorInterval());
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness,rig.Plant.TryAdvance().Outcome);
        Assert.Equal(0,rig.Body.Advances);Assert.False(rig.Plant.State.Faulted);
        Assert.NotEqual(rig.Plant.Fingerprint,new Rig().Plant.Fingerprint);
    }

    [Fact]
    public void SplitOwnerRejectsAtomicSchedulingWithoutChangingItsState()
    {
        var rig=new Rig();rig.Start();var before=rig.Plant.State;
        Assert.Throws<InvalidOperationException>(()=>rig.Plant.TryAdvance());
        Assert.Equal(before,rig.Plant.State);Assert.Equal(0,rig.Body.Advances);
    }

    [Fact]
    public void PhysicalEndpointIsAvailableBeforeMotorCompletionWithoutInventedSilence()
    {
        var rig=new Rig();rig.Start();string muscles=Json(rig.Plant.CaptureMuscles());
        var step=rig.Begin();
        Assert.Equal(8,step.After.PhysicsStep);Assert.Equal(10.125,step.After.PhysicalTimeMs);
        Assert.Equal(7,rig.Plant.State.CompletedPhysicsStep);Assert.Equal(8,rig.Plant.State.ObservedPhysicsStep);
        Assert.Equal(10,rig.Plant.State.BodyTimeMs);Assert.Equal(10,rig.Plant.State.CompleteThroughBodyMs);
        Assert.Equal(0,rig.Plant.State.DeliveredEvents);Assert.Equal(muscles,Json(rig.Plant.CaptureMuscles()));
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness,rig.Plant.TryCompleteMotorInterval().Outcome);
        Assert.Equal(muscles,Json(rig.Plant.CaptureMuscles()));
    }

    [Fact]
    public void OnePendingPhysicalStepCannotRunAheadOrReplayItsEffort()
    {
        var rig=new Rig();rig.Start();var first=rig.Begin();
        var repeat=rig.Plant.TryAdvanceBody();Assert.Equal(CausalBodyAdvance.AlreadyPending,repeat.Outcome);
        Assert.Same(first,repeat.Step);Assert.Equal(1,rig.Body.Advances);
    }

    [Fact]
    public void ClosureRequiresEveryActualSourceAndConsumesNoPartialInput()
    {
        var rig=new Rig(pair:true);rig.Start();rig.Begin();
        var packet=rig.Feed(1010.125,[Spike(1010.03125)]);rig.Plant.Accept(packet);
        var before=rig.Plant.State;string muscles=Json(rig.Plant.CaptureMuscles());
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness,rig.Plant.TryCompleteMotorInterval().Outcome);
        Assert.Equal(before,rig.Plant.State);Assert.Equal(muscles,Json(rig.Plant.CaptureMuscles()));
        rig.Feed(1010.125,[],"b");var closed=rig.Close();
        Assert.Single(closed.Arrivals);Assert.Equal(1,rig.Plant.State.DeliveredEvents);Assert.Null(rig.Plant.PendingStep);
        Assert.Equal(8,rig.Plant.State.CompletedPhysicsStep);Assert.Equal(1,rig.Body.Advances);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void DelayedClosureMatchesOriginalPartitionPhysicalAndMuscleStatesExactly(int substeps)
    {
        var original=new Rig(CausalMotorSchedule.MotorBeforeBody,substeps:substeps);
        var split=new Rig(substeps:substeps);original.Start();split.Start();
        original.Feed(1110,[Spike(1010.03125),Spike(1040.03125,2)]);
        for(int i=0;i<800;i++)
        {
            var pending=split.Begin();
            // Source completeness arrives only AFTER the physical endpoint.
            double end=1010+(i+1)*.125;
            SpikeMessage[] events=i==0?[Spike(1010.03125)]:i==240?[Spike(1040.03125,2)]:[];
            split.Feed(end,events);var actual=split.Close();
            var old=original.Plant.TryAdvance();Assert.Equal(CausalMotorAdvance.Advanced,old.Outcome);
            var expected=Assert.IsType<CausalMotorStep>(old.Step);
            Assert.Equal(Json(expected.Before.Paths),Json(actual.Before.Paths));
            Assert.Equal(Json(expected.After.Paths),Json(actual.After.Paths));
            Assert.Equal(Json(expected.Effort.Tensions),Json(actual.Effort.Tensions));
            Assert.Equal(Json(expected.Arrivals),Json(actual.Arrivals));Assert.Equal(Json(expected.Muscles),Json(actual.Muscles));
            Assert.Equal(expected.HeldTensionWorkOnBodyJoules,actual.HeldTensionWorkOnBodyJoules);
            Assert.Same(pending.After,actual.After);Assert.Equal(original.Body.Angle,split.Body.Angle);Assert.Equal(original.Body.Speed,split.Body.Speed);
        }
        Assert.Equal(2,split.Plant.State.DeliveredEvents);Assert.True(split.Plant.CaptureMuscles()["a"].Units[0].Activation>0);
    }

    [Fact]
    public void PostBodyPrescribedSensoryFixtureCanCloseWithoutCircularWaiting()
    {
        var rig=new Rig();rig.Body.Speed=.001;rig.Start();var observed=rig.Begin();
        Assert.True(observed.After.Paths[0].VelocityMetersPerSecond<0);
        // This is deliberately a fixture packet produced from a real completed
        // analytic body observation, not a claim of neuronal sensory firing.
        var original=Spike(1010.0625);string bytes=Json(original);
        rig.Feed(1010.125,[original]);var closed=rig.Close();
        Assert.Equal(bytes,Json(original));Assert.Equal(original.MessageId,Assert.Single(closed.Arrivals).MessageId);
        Assert.Equal(Json(observed.After),Json(closed.After));Assert.Equal(1,rig.Body.Advances);
        Assert.True(closed.Muscles["a"].Units[0].FreeCalcium>0);
    }

    [Fact]
    public void EndpointEventCannotAlterTheAlreadyCompletedBodyOrEarlierMuscleState()
    {
        var rig=new Rig();rig.Start();var pending=rig.Begin();string after=Json(pending.After);
        rig.Feed(1010.125,[Spike(1010.109375)]);var closed=rig.Close();
        Assert.Equal(10.125,Assert.Single(closed.Arrivals).ArrivalAtBodyMs);Assert.Equal(after,Json(closed.After));
        Assert.Equal(0,closed.Muscles["a"].Units[0].FreeCalcium);Assert.Equal(1,closed.Muscles["a"].Units[0].Arrivals);
        rig.Begin();rig.Feed(1010.25,[]);Assert.True(rig.Close().Muscles["a"].Units[0].FreeCalcium>0);
    }

    [Fact]
    public void PendingBodyCanPauseAndResumeWithoutResetOrAnotherPhysicalStep()
    {
        var rig=new Rig();rig.Start();var pending=rig.Begin();rig.Plant.Pause();
        Assert.Equal(CausalBodyAdvance.Paused,rig.Plant.TryAdvanceBody().Outcome);
        Assert.Equal(CausalMotorAdvance.Paused,rig.Plant.TryCompleteMotorInterval().Outcome);
        rig.Feed(1010.125,[]);rig.Plant.Resume();Assert.Same(pending,rig.Plant.PendingStep);
        rig.Close();Assert.Equal(1,rig.Body.Advances);Assert.Null(rig.Plant.PendingStep);
    }

    [Fact]
    public void OutOfBandMotionDuringPendingClosureFaultsWithoutConsumingEvents()
    {
        var rig=new Rig();rig.Start();var pending=rig.Begin();rig.Feed(1010.125,[Spike(1010.03125)]);
        string muscles=Json(rig.Plant.CaptureMuscles());rig.Body.Angle+=.01;
        Assert.Throws<InvalidOperationException>(()=>rig.Plant.TryCompleteMotorInterval());
        Assert.True(rig.Plant.State.Faulted);Assert.Equal(0,rig.Plant.State.DeliveredEvents);
        Assert.Same(pending,rig.Plant.PendingStep);Assert.Equal(muscles,Json(rig.Plant.CaptureMuscles()));
        Assert.Throws<InvalidOperationException>(()=>rig.Plant.Resume());
    }

    [Fact]
    public void UncertainNativeReceiptCannotBeRetriedOrInvented()
    {
        var rig=new Rig();rig.Start();rig.Body.LoseReceipt=true;
        Assert.Throws<IOException>(()=>rig.Plant.TryAdvanceBody());Assert.Equal(1,rig.Body.Advances);
        Assert.True(rig.Plant.State.Faulted);Assert.Null(rig.Plant.PendingStep);
        Assert.Throws<InvalidOperationException>(()=>rig.Plant.TryAdvanceBody());
        Assert.Throws<InvalidOperationException>(()=>rig.Plant.TryCompleteMotorInterval());Assert.Equal(1,rig.Body.Advances);
    }

    [Fact]
    public void ClosureWithoutBodyReceiptRejectsWithoutPoisoningFreshOwner()
    {
        var rig=new Rig();rig.Start();
        Assert.Throws<InvalidOperationException>(()=>rig.Plant.TryCompleteMotorInterval());
        Assert.False(rig.Plant.State.Faulted);rig.Begin();Assert.Equal(1,rig.Body.Advances);
    }

    [Fact]
    public void PendingEffortAndPathCollectionsCannotBeModifiedByTheirReader()
    {
        var rig=new Rig();rig.Start();var p=rig.Begin();
        Assert.Throws<NotSupportedException>(()=>((IList<TendonTension>)p.Effort.Tensions)[0]=new("a",1000));
        Assert.Throws<NotSupportedException>(()=>((IList<MusculotendonPathMeasurement>)p.After.Paths)[0]=new("a",1,0));
        rig.Feed(1010.125,[]);rig.Close();Assert.Equal(1,rig.Body.Advances);
    }
}
