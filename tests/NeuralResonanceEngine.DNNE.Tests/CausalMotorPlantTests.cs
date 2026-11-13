using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Synthetic axonal events and an analytic constant-moment-arm joint. These
// checks exercise causal mechanical coupling; they do not calibrate anatomy,
// qualify an independent continuous coupled solver, or demonstrate recovery.
public sealed class CausalMotorPlantTests
{
    private static readonly Guid Session = Guid.Parse("6e04cbcc-2c65-448c-8a49-2a495b1a8223");
    private static readonly Guid Projection = Guid.Parse("a238eb59-fdb7-4adf-89a4-2a366f9829c9");
    private static PhysicalSampleClock Clock(double step = .125, double epoch = 1000) => new(Session, "joint-fixture", "paths", 7, step, epoch, 100);
    private static DynamicMuscleGeometry Geometry => DynamicMuscleGeometry.NagamoriTibialisAnterior;
    private static MotorAxonContact Contact(string name = "a") => MotorAxonContact.Create("source-" + name, "cell-" + name, Projection, "unit-" + name, .015625);
    private static CausalMuscleDefinition Definition(string name = "a") => new(name, Geometry,
        [SlowMotorUnitDefinition.NagamoriSlowUnit1(Contact(name))], Geometry.OptimalFiberLengthMeters);
    private static double RestLength()
    {
        var g = Geometry;
        double passive = 23 * .046 * Math.Log(1 + Math.Exp((1 / g.PassiveLengthScale - 1.17) / .046));
        double tendon = .964 + .0047 * Math.Log(Math.Exp(passive * Math.Cos(g.PennationRadians) / (27.8 * .0047)) - 1);
        return g.OptimalFiberLengthMeters * Math.Cos(g.PennationRadians) + g.ReferenceTendonLengthMeters * tendon;
    }
    private static SpikeMessage Spike(double time, int id = 1, string name = "a") => new()
    {
        MessageId = StableNeuralIdentity.Create("causal-body-fixture", name, id), TimestampMs = time,
        SourceStructure = StructureId.SpinalCordMotor, SourceInstanceKey = "source-" + name,
        SourceNeuronId = "cell-" + name, ProjectionId = Projection,
        SynapseId = StableNeuralIdentity.Create("causal-body-fixture-contact", name), Neurotransmitter = NTEnum.ACETYLCHOLINE
    };
    private static string Encode(object value) => JsonSerializer.Serialize(value);

    private sealed class Joint : IAvatarMotorMechanicalBody
    {
        public readonly PhysicalSampleClock Clock;
        public readonly Dictionary<string, double> Arms;
        public bool Fixed;
        public double Angle, Speed;
        public const double Inertia = 1;
        public string Fingerprint { get; set; }
        public long CompletedPhysicsStep { get; set; }
        public double PhysicsStepMs => Clock.PhysicsStepMs;
        public Guid LastEffort;
        public int Advances;
        public Action? BeforeAdvance;
        public bool ThrowAfterAdvance;
        public Func<MotorMechanicalFrame, MotorMechanicalFrame>? AlterFrame;
        public readonly List<MotorMechanicalEffort> Efforts = [];
        public Joint(PhysicalSampleClock clock, bool fixedJoint, params (string Name, double Arm)[] paths)
        {
            Clock = clock; Fixed = fixedJoint; CompletedPhysicsStep = clock.InitialPhysicsStep;
            Arms = paths.ToDictionary(x => x.Name, x => x.Arm);
            Fingerprint = "analytic-one-dof-constant-arm-v1:" + Encode(new
            { fixedJoint, Paths = paths.Select(p => new { p.Name, p.Arm }).ToArray(), Inertia, Length = RestLength() });
        }
        public MotorMechanicalFrame CaptureGeometry()
        {
            var frame = new MotorMechanicalFrame(Clock.SessionId, Fingerprint, CompletedPhysicsStep,
                Clock.AtStep(CompletedPhysicsStep).PhysicalMs, LastEffort,
                Arms.Select(p => new MusculotendonPathMeasurement(p.Key, RestLength() - p.Value * Angle, -p.Value * Speed)).ToArray());
            return AlterFrame?.Invoke(frame) ?? frame;
        }
        public void AdvanceOneStep(MotorMechanicalEffort effort)
        {
            BeforeAdvance?.Invoke();
            Assert.Equal(CompletedPhysicsStep, effort.StartStep);
            Assert.Equal(Clock.AtStep(CompletedPhysicsStep).PhysicalMs, effort.StartTimeMs);
            Assert.Equal(Clock.AtStep(CompletedPhysicsStep + 1).PhysicalMs, effort.EndTimeMs);
            Assert.Equal(Fingerprint, effort.BodyFingerprint); Assert.Equal(Clock.SessionId, effort.SessionId);
            Assert.Equal(Arms.Keys.Order(), effort.Tensions.Select(t => t.MuscleId).Order());
            Assert.All(effort.Tensions, t => Assert.True(t.AxialNewtons >= 0 && double.IsFinite(t.AxialNewtons)));
            double seconds = (effort.EndTimeMs - effort.StartTimeMs) / 1000;
            double torque = effort.Tensions.Sum(t => Arms[t.MuscleId] * t.AxialNewtons);
            if (!Fixed)
            {
                // Exact solution for the constant torque applied over this interval.
                Angle += Speed * seconds + .5 * torque / Inertia * seconds * seconds;
                Speed += torque / Inertia * seconds;
            }
            Efforts.Add(effort); Advances++; CompletedPhysicsStep++; LastEffort = effort.EffortId;
            if (ThrowAfterAdvance) throw new InvalidOperationException("Fixture lost reply after physical step.");
        }
    }

    private sealed class Rig
    {
        public readonly PhysicalSampleClock Clock;
        public readonly Joint Body;
        public readonly AvatarCausalMotorPlant Plant;
        public readonly MotorAxonAnatomy Anatomy;
        public readonly MotorAxonClock MotorClock;
        public readonly Dictionary<string, MotorAxonPublisher> Publishers;
        public Rig(bool fixedJoint = true, bool pair = false, double epoch = 1000, int substeps = 1)
        {
            Clock = CausalMotorPlantTests.Clock(epoch: epoch);
            Body = new(Clock, fixedJoint, pair ? [("a", .02), ("b", -.02)] : [("a", .02)]);
            var definitions = pair ? new[] { Definition(), Definition("b") } : [Definition()];
            Anatomy = new(definitions.SelectMany(d => d.Units).Select(u => u.Contact));
            MotorClock = new(Clock.SessionId, Clock.NeuralEpochMs, Clock.PhysicalEpochMs);
            Publishers = Anatomy.Contacts.ToDictionary(c => c.SourceInstanceKey,
                c => new MotorAxonPublisher(Anatomy, MotorClock, c.SourceInstanceKey));
            Plant = new(Clock, Body, definitions, substeps);
        }
        public MotorAxonBatch Feed(SpikeMessage[] events, double through = 500, string name = "a")
        {
            var batch = Publishers["source-" + name].Publish(events, through); Plant.Accept(batch); return batch;
        }
        public void Start() { Plant.CaptureInitial(); Plant.Resume(); }
        public CausalMotorStep Advance(int count = 1)
        {
            CausalMotorStep? last = null;
            for (int i = 0; i < count; ++i)
            {
                var result = Plant.TryAdvance(); Assert.Equal(CausalMotorAdvance.Advanced, result.Outcome);
                last = Assert.IsType<CausalMotorStep>(result.Step);
            }
            return last!;
        }
    }

    [Fact]
    public void InitialCaptureDoesNotEvolveAndWorldStartsPaused()
    {
        var rig = new Rig(); rig.Feed([]);
        Assert.Throws<InvalidOperationException>(() => rig.Plant.Resume());
        var initial = rig.Plant.CaptureInitial();
        Assert.Equal(1000, initial.PhysicalTimeMs); Assert.Equal(7, initial.PhysicsStep);
        Assert.Equal(CausalMotorAdvance.Paused, rig.Plant.TryAdvance().Outcome); Assert.Equal(0, rig.Body.Advances);
        Assert.Equal(Geometry.OptimalFiberLengthMeters, rig.Plant.CaptureMuscles()["a"].FiberLengthMeters);
        rig.Plant.Resume(); rig.Advance(); rig.Plant.Pause();
        Assert.Equal(CausalMotorAdvance.Paused, rig.Plant.TryAdvance().Outcome); Assert.Equal(1, rig.Body.Advances);
    }

    [Fact]
    public void EveryNeuronalSourceMustBeCompleteBeforePhysicsOrQueueConsumption()
    {
        var rig = new Rig(pair: true); rig.Start(); rig.Feed([Spike(100.03125)]);
        var before = rig.Plant.State;
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness, rig.Plant.TryAdvance().Outcome);
        Assert.Equal(before, rig.Plant.State); Assert.Equal(0, rig.Body.Advances);
        rig.Feed([], name: "b"); var step = rig.Advance(); Assert.Single(step.Arrivals);
        Assert.Equal(1, rig.Plant.State.DeliveredEvents);
    }

    [Fact]
    public void FractionalArrivalRetainsIdentityAndDoesNotAffectEarlierEffort()
    {
        var active = new Rig(); var quiet = new Rig(); active.Start(); quiet.Start();
        var original = Spike(100.03125); active.Feed([original]); quiet.Feed([]);
        var a = active.Advance(); var q = quiet.Advance();
        Assert.Equal(Encode(q.Effort), Encode(a.Effort));
        var arrival = Assert.Single(a.Arrivals); Assert.Equal(1000.046875, arrival.ArrivalAtBodyMs);
        Assert.Equal(original.MessageId, arrival.MessageId); Assert.Equal(original.SynapseId, arrival.SourceSynapseId);
        Assert.Equal(Contact().ContactId, arrival.ContactId);
        Assert.True(a.Muscles["a"].Units[0].FreeCalcium > 0); Assert.Equal(0, q.Muscles["a"].Units[0].FreeCalcium);
        Assert.Equal(original.MessageId, a.Muscles["a"].Units[0].LastMessageId);
    }

    [Fact]
    public void NeuronalArrivalChangesSubsequentTensionAndFreeJointMotion()
    {
        var active = new Rig(fixedJoint: false); var quiet = new Rig(fixedJoint: false);
        active.Start(); quiet.Start(); active.Feed([Spike(100.03125), Spike(125, 2)]); quiet.Feed([]);
        var firstA = active.Advance(); var firstQ = quiet.Advance();
        Assert.Equal(Encode(firstQ.After), Encode(firstA.After));
        var lastA = active.Advance(999); var lastQ = quiet.Advance(999);
        Assert.True(lastA.Muscles["a"].Units[0].Activation > 0);
        Assert.True(active.Body.Angle > quiet.Body.Angle + 1e-9);
        Assert.True(Math.Abs(lastA.Effort.Tensions[0].AxialNewtons - lastQ.Effort.Tensions[0].AxialNewtons) > 1e-6);
    }

    [Fact]
    public void EndpointArrivalCannotRewriteTheCompletedPhysicalStep()
    {
        var rig = new Rig(); rig.Start(); rig.Feed([Spike(100.109375)]);
        var before = rig.Plant.CaptureMuscles()["a"]; var step = rig.Advance();
        Assert.Equal(1000.125, Assert.Single(step.Arrivals).ArrivalAtBodyMs);
        Assert.Equal(before.Forces.TendonAxialForceNewtons, step.Effort.Tensions[0].AxialNewtons);
        Assert.Equal(0, step.Muscles["a"].Units[0].FreeCalcium);
        Assert.Equal(1, step.Muscles["a"].Units[0].Arrivals);
        Assert.True(rig.Advance().Muscles["a"].Units[0].FreeCalcium > 0);
    }

    [Fact]
    public void FixedBodyMatchesQualifiedDirectMuscleWithIdenticalEventBoundaries()
    {
        var rig = new Rig(); rig.Start(); rig.Feed([Spike(100.03125), Spike(105.0625, 2), Spike(130, 3)]);
        var direct = new AvatarDynamicMuscle(Geometry, Definition().Units, rig.MotorClock, Geometry.OptimalFiberLengthMeters, RestLength());
        for (int i = 0; i < 1600; ++i)
        {
            var step = rig.Advance();
            foreach (var arrival in step.Arrivals)
            {
                if (arrival.ArrivalAtBodyMs > direct.TimeMs)
                    direct.AdvancePrescribed(new(direct.TimeMs, arrival.ArrivalAtBodyMs, RestLength(), RestLength()));
                direct.Receive(arrival);
            }
            if (step.After.PhysicalTimeMs > direct.TimeMs)
                direct.AdvancePrescribed(new(direct.TimeMs, step.After.PhysicalTimeMs, RestLength(), RestLength()));
            Assert.Equal(Encode(direct.Capture()), Encode(step.Muscles["a"]));
        }
    }

    [Fact]
    public void PacketSplittingAndReplayPreserveMechanicalAndMuscleStateExactly()
    {
        var whole = new Rig(fixedJoint: false); var split = new Rig(fixedJoint: false);
        whole.Start(); split.Start();
        var first = Spike(100.03125); var later = Spike(101.03125, 2);
        whole.Feed([first, later]); var packet = split.Feed([first], through: 101);
        split.Plant.Accept(packet); whole.Advance(8); split.Advance(8);
        split.Feed([later]); var w = whole.Advance(792); var s = split.Advance(792);
        Assert.Equal(Encode(w), Encode(s)); Assert.Equal(whole.Plant.State, split.Plant.State);
        Assert.Equal(Encode(whole.Body.Efforts), Encode(split.Body.Efforts));
    }

    [Fact]
    public void UnrelatedMuscleEventsDoNotPerturbAnotherMusclesNumericalPartition()
    {
        var quietB = new Rig(pair: true); var activeB = new Rig(pair: true); quietB.Start(); activeB.Start();
        quietB.Feed([Spike(100.03125)]); activeB.Feed([Spike(100.03125)]);
        quietB.Feed([], name: "b"); activeB.Feed([Spike(100.0625, name: "b"), Spike(105.03125, 2, "b")], name: "b");
        Assert.Equal(Encode(quietB.Advance(800).Muscles["a"]), Encode(activeB.Advance(800).Muscles["a"]));
    }

    [Fact]
    public void AxialTendonForceAndMeasuredPathHaveTheSameVirtualWork()
    {
        var rig = new Rig(fixedJoint: false); rig.Body.Speed = .001; rig.Start(); rig.Feed([Spike(100.03125)]);
        double cumulativeWork = 0, initialEnergy = .5 * Joint.Inertia * rig.Body.Speed * rig.Body.Speed;
        for (int i = 0; i < 800; ++i)
        {
            double initialAngle = rig.Body.Angle; var step = rig.Advance();
            double torque = .02 * step.Effort.Tensions[0].AxialNewtons;
            Assert.InRange(Math.Abs(step.HeldTensionWorkOnBodyJoules - torque * (rig.Body.Angle - initialAngle)), 0, 1e-14);
            cumulativeWork += step.HeldTensionWorkOnBodyJoules;
        }
        Assert.InRange(Math.Abs(cumulativeWork - (.5 * Joint.Inertia * rig.Body.Speed * rig.Body.Speed - initialEnergy)), 0, 1e-12);
    }

    [Fact]
    public void AntagonisticPathsProduceOppositeMotionWithTheSameUnitInput()
    {
        var left = new Rig(fixedJoint: false, pair: true); var right = new Rig(fixedJoint: false, pair: true);
        left.Start(); right.Start(); left.Feed([Spike(100.03125)]); left.Feed([], name: "b");
        right.Feed([]); right.Feed([Spike(100.03125, name: "b")], name: "b");
        left.Advance(800); right.Advance(800);
        Assert.True(left.Body.Angle > 1e-9); Assert.InRange(Math.Abs(left.Body.Angle + right.Body.Angle), 0, 1e-14);
        Assert.InRange(Math.Abs(left.Body.Speed + right.Body.Speed), 0, 1e-14);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("fingerprint")]
    [InlineData("step")]
    [InlineData("time")]
    [InlineData("effort")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("nonfinite")]
    public void InvalidPhysicalReplyFaultsWithoutRetryingAnAlreadyCompletedStep(string corruption)
    {
        var rig = new Rig(); rig.Start(); rig.Feed([]);
        rig.Body.AlterFrame = frame => frame.PhysicsStep == rig.Clock.InitialPhysicsStep ? frame : corruption switch
        {
            "session" => frame with { SessionId = Guid.Empty },
            "fingerprint" => frame with { BodyFingerprint = "different" },
            "step" => frame with { PhysicsStep = frame.PhysicsStep + 1 },
            "time" => frame with { PhysicalTimeMs = frame.PhysicalTimeMs + 1 },
            "effort" => frame with { LastAppliedEffortId = Guid.Empty },
            "missing" => frame with { Paths = [] },
            "duplicate" => frame with { Paths = [frame.Paths[0], frame.Paths[0]] },
            _ => frame with { Paths = [frame.Paths[0] with { LengthMeters = double.NaN }] }
        };
        Assert.Throws<InvalidOperationException>(() => rig.Plant.TryAdvance());
        Assert.True(rig.Plant.State.Faulted); Assert.Equal(7, rig.Plant.State.CompletedPhysicsStep);
        Assert.Equal(8, rig.Plant.State.ObservedPhysicsStep); Assert.Equal(1, rig.Body.Advances);
        Assert.Throws<InvalidOperationException>(() => rig.Plant.TryAdvance());
        Assert.Throws<InvalidOperationException>(() => rig.Plant.Resume()); Assert.Equal(1, rig.Body.Advances);
    }

    [Fact]
    public void UncertainPhysicalAdvanceCannotBeRetriedOrClaimedCompleted()
    {
        var rig = new Rig(); rig.Start(); rig.Feed([Spike(100.03125)]); rig.Body.ThrowAfterAdvance = true;
        Assert.Throws<InvalidOperationException>(() => rig.Plant.TryAdvance());
        Assert.True(rig.Plant.State.Faulted); Assert.False(rig.Plant.State.Running);
        Assert.Equal(7, rig.Plant.State.CompletedPhysicsStep); Assert.Equal(8, rig.Body.CompletedPhysicsStep);
        Assert.Equal(1, rig.Plant.State.DeliveredEvents); Assert.Equal(1, rig.Body.Advances);
        Assert.Throws<InvalidOperationException>(() => rig.Plant.TryAdvance()); Assert.Equal(1, rig.Body.Advances);
    }

    [Fact]
    public void ExternalPoseChangeFaultsBeforeAnyOwnedPhysicalStep()
    {
        var rig = new Rig(); rig.Start(); rig.Feed([]); rig.Body.Angle = .001;
        Assert.Throws<InvalidOperationException>(() => rig.Plant.TryAdvance()); Assert.True(rig.Plant.State.Faulted);
        Assert.Equal(0, rig.Body.Advances); Assert.Equal(0, rig.Plant.State.DeliveredEvents);
    }

    [Fact]
    public void ExternalPhysicsStepFaultsEvenWhileWaitingForNeuronalCompleteness()
    {
        var rig = new Rig(); rig.Start(); rig.Body.CompletedPhysicsStep++;
        Assert.Throws<InvalidOperationException>(() => rig.Plant.TryAdvance()); Assert.True(rig.Plant.State.Faulted);
        Assert.Equal(8, rig.Plant.State.ObservedPhysicsStep);
    }

    [Fact]
    public void CollapsedSubstepsAreRejectedBeforeDrainingEventsOrAdvancingPhysics()
    {
        var rig = new Rig(epoch: 1e12, substeps: 2048); rig.Start(); rig.Feed([Spike(100.03125)]);
        Assert.Throws<InvalidOperationException>(() => rig.Plant.TryAdvance());
        Assert.True(rig.Plant.State.Faulted); Assert.Equal(0, rig.Body.Advances);
        Assert.Equal(0, rig.Plant.State.DeliveredEvents); Assert.Equal(1, rig.Plant.State.PendingEvents);
    }

    [Fact]
    public void ExcessiveOrInsufficientSubstepDeclarationsAreRejected()
    {
        var clock = Clock(); var body = new Joint(clock, true, ("a", .02));
        Assert.Throws<ArgumentException>(() => new AvatarCausalMotorPlant(clock, body, [Definition()], 0));
        Assert.Throws<ArgumentException>(() => new AvatarCausalMotorPlant(clock, body, [Definition()], 16_385));
        Assert.Throws<ArgumentException>(() => new AvatarCausalMotorPlant(Clock(step: 10), body, [Definition()], 79));
    }

    [Fact]
    public void ReentrantPhysicalCallbackFaultsAndDoesNotDoubleStep()
    {
        var rig = new Rig(); rig.Start(); rig.Feed([]); rig.Body.BeforeAdvance = () => rig.Plant.TryAdvance();
        Assert.Throws<InvalidOperationException>(() => rig.Plant.TryAdvance()); Assert.True(rig.Plant.State.Faulted);
        Assert.Equal(0, rig.Body.Advances);
    }

    [Fact]
    public void DifferentThreadCannotOwnTheSamePhysicalPlant()
    {
        var rig = new Rig(); Exception? observed = null;
        var thread = new Thread(() => { try { rig.Plant.CaptureInitial(); } catch (Exception e) { observed = e; } });
        thread.Start(); thread.Join(); Assert.IsType<InvalidOperationException>(observed);
        Assert.False(rig.Plant.State.Initialized); Assert.False(rig.Plant.State.Faulted);
    }

    [Fact]
    public void SavedStepSnapshotsRemainImmutableAcrossFurtherAdvancement()
    {
        var rig = new Rig(fixedJoint: false); rig.Start(); rig.Feed([Spike(100.03125)]);
        var step = rig.Advance(); var saved = Encode(step); rig.Advance(799);
        Assert.Equal(saved, Encode(step));
        Assert.Throws<NotSupportedException>(() => ((IList<TendonTension>)step.Effort.Tensions)[0] = new("a", 0));
        Assert.Throws<NotSupportedException>(() => ((IList<MusculotendonPathMeasurement>)step.After.Paths)[0] = new("a", 1, 0));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, DynamicMuscleSnapshot>)step.Muscles).Clear());
    }

    [Fact]
    public void CompletedGeometryUpdatePreservesFiberCalciumAndAccountingAtTheSameTime()
    {
        var rig = new Rig(); var muscle = new AvatarDynamicMuscle(Geometry, Definition().Units, rig.MotorClock,
            Geometry.OptimalFiberLengthMeters, RestLength());
        muscle.AdvancePrescribed(new(1000, 1000.125, RestLength(), RestLength())); var before = muscle.Capture();
        muscle.ObserveCompletedBodyLength(before.TimeMs, RestLength() + 1e-6); var after = muscle.Capture();
        Assert.Equal(Encode(before with { MusculotendonLengthMeters = after.MusculotendonLengthMeters, Forces = after.Forces }), Encode(after));
        Assert.True(after.Forces.TendonAxialForceNewtons > before.Forces.TendonAxialForceNewtons);
        var saved = Encode(after);
        Assert.Throws<ArgumentException>(() => muscle.ObserveCompletedBodyLength(before.TimeMs + 1, RestLength()));
        Assert.Throws<ArgumentException>(() => muscle.ObserveCompletedBodyLength(before.TimeMs, double.NaN));
        Assert.Throws<InvalidOperationException>(() => muscle.ObserveCompletedBodyLength(before.TimeMs, .001));
        Assert.Equal(saved, Encode(muscle.Capture()));
    }
}
