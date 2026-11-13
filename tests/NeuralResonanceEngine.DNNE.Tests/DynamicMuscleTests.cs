using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class DynamicMuscleTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;
    public DynamicMuscleTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;
    private void Record(string name, DynamicMuscleSnapshot state) => _output.WriteLine(name + ": " + Encode(state));
    private static readonly Guid Session = Guid.Parse("66c886ef-5695-4e23-9715-2686565530b8");
    private static readonly Guid Projection = Guid.Parse("97257344-4cb4-48c4-80a9-fbe494c5e9bb");
    private static MotorAxonClock Clock => new(Session, 0, 0);
    private static DynamicMuscleGeometry Geometry => DynamicMuscleGeometry.NagamoriTibialisAnterior;
    private static MotorAxonContact Contact(string name = "unit-1", double delay = 0) =>
        MotorAxonContact.Create("spinal-L", name, Projection, name, delay);
    private static SlowMotorUnitDefinition Definition(string name = "unit-1") => SlowMotorUnitDefinition.NagamoriSlowUnit1(Contact(name));
    private static AvatarMotorAxonArrival Arrival(double time, long ordinal = 1, MotorAxonContact? contact = null)
    {
        contact ??= Contact();
        return new(Session, contact.SourceInstanceKey, StableNeuralIdentity.Create("dynamic-test", contact.MotorUnitId, ordinal),
            ordinal, contact.SourceNeuronId, contact.ProjectionId, StableNeuralIdentity.Create("dynamic-synapse", contact.MotorUnitId),
            contact.ContactId, contact.MotorUnitId, time - contact.ConductionDelayMs, time);
    }

    // Closed-form resting balance from the passive/tendon constitutive curves.
    private static double RestLength(DynamicMuscleGeometry g, double normalizedFiber = 1)
    {
        double passive = 23 * .046 * Math.Log(1 + Math.Exp((normalizedFiber / g.PassiveLengthScale - 1.17) / .046));
        double tendon = .964 + .0047 * Math.Log(Math.Exp(passive * Math.Cos(g.PennationRadians) / (27.8 * .0047)) - 1);
        return g.OptimalFiberLengthMeters * normalizedFiber * Math.Cos(g.PennationRadians) + g.ReferenceTendonLengthMeters * tendon;
    }
    private static AvatarDynamicMuscle Muscle(FiberBoundary boundary = FiberBoundary.Free, double normalizedFiber = 1,
        DynamicMuscleGeometry? geometry = null, SlowMotorUnitDefinition[]? units = null)
    {
        var g = geometry ?? Geometry;
        return new(g, units ?? [Definition()], Clock, g.OptimalFiberLengthMeters * normalizedFiber,
            RestLength(g, normalizedFiber), boundary: boundary);
    }
    private static void Advance(AvatarDynamicMuscle muscle, double until, double step = .0625, Func<double, double>? length = null)
    {
        while (muscle.TimeMs < until)
        {
            var before = muscle.Capture(); double end = Math.Min(until, before.TimeMs + step);
            muscle.AdvancePrescribed(new(before.TimeMs, end, before.MusculotendonLengthMeters,
                length?.Invoke(end) ?? before.MusculotendonLengthMeters));
        }
    }
    private static string Encode(DynamicMuscleSnapshot state) => JsonSerializer.Serialize(state);
    private static void Close(double expected, double actual, double tolerance = 1e-11) => Assert.InRange(Math.Abs(actual - expected), 0, tolerance);

    [Fact]
    public void OptimalFixedFiberRecoversOriginalCalciumCalculation()
    {
        var muscle = Muscle(FiberBoundary.FixedLengthDiagnostic);
        var old = new AvatarMotorUnitCalcium(MotorUnitCalciumParameters.NagamoriSlowUnit1, Contact(), Clock);
        double[] times = [10, 25.125, 80];
        for (int eventIndex = 0; eventIndex <= times.Length; eventIndex++)
        {
            double end = eventIndex == times.Length ? 1000 : times[eventIndex];
            while (old.TimeMs < end)
            {
                double dt = Math.Min(.0625, end - old.TimeMs); old.Advance(dt);
                Advance(muscle, old.TimeMs); var state = Assert.Single(muscle.Capture().Units);
                Close(old.FreeCalcium, state.FreeCalcium); Close(old.BoundCalcium, state.BoundCalcium);
                Close(old.Activation, state.Activation); Close(old.ReleaseDrive, state.ReleaseDrive);
                Close(old.ReleasedCalcium, state.ReleasedCalcium); Close(old.RecoveredCalcium, state.RecoveredCalcium);
                Close(old.IsometricForceNewtons, state.Activation * MotorUnitCalciumParameters.NagamoriSlowUnit1.PeakTetanicForceNewtons);
            }
            if (eventIndex < times.Length) { var arrival = Arrival(end, eventIndex + 1); old.Receive(arrival); muscle.Receive(arrival); }
        }
        Record("fixed-optimal", muscle.Capture());
        Assert.Equal(1, muscle.Capture().Yield); Assert.Equal(3, Assert.Single(muscle.Capture().Units).Arrivals);
        // Actual dynamic active force also includes the reference passive-2
        // correction. Only the original activation-equivalent force is equal.
    }

    [Fact]
    public void SilentFreeFiberPreservesAnalyticRestWithoutInventedRecruitment()
    {
        var muscle = Muscle(); double initial = muscle.Capture().FiberLengthMeters;
        Advance(muscle, 1000); var state = muscle.Capture(); var unit = Assert.Single(state.Units);
        Record("silent-free", state);
        Close(initial, state.FiberLengthMeters); Close(0, state.FiberVelocityMetersPerSecond);
        Assert.Equal(0, unit.Activation); Assert.Equal(0, unit.Arrivals); Assert.Equal(0, unit.FreeCalcium);
        Assert.Equal(0, state.Forces.ActiveFiberForceNewtons);
    }

    [Fact]
    public void LengthChangesCalciumActivationBeforeForceScaling()
    {
        var shortFiber = Muscle(FiberBoundary.FixedLengthDiagnostic, .9);
        var longFiber = Muscle(FiberBoundary.FixedLengthDiagnostic, 1.1);
        shortFiber.Receive(Arrival(0)); longFiber.Receive(Arrival(0));
        Advance(shortFiber, 50); Advance(longFiber, 50);
        Record("short-fixed", shortFiber.Capture()); Record("long-fixed", longFiber.Capture());
        var shorter = Assert.Single(shortFiber.Capture().Units); var longer = Assert.Single(longFiber.Capture().Units);
        Assert.True(Math.Abs(longer.BoundCalcium - shorter.BoundCalcium) > 1e-6);
        Assert.True(Math.Abs(longer.Activation - shorter.Activation) > 1e-6);
        Assert.Equal(1, shortFiber.Capture().Yield); Assert.Equal(1, longFiber.Capture().Yield);
    }

    [Fact]
    public void ActualArrivalChangesFreeFiberTendonAndYield()
    {
        var free = Muscle(); var held = Muscle(FiberBoundary.FixedLengthDiagnostic);
        free.Receive(Arrival(0)); held.Receive(Arrival(0)); Advance(free, 100); Advance(held, 100);
        var moving = free.Capture(); var clamp = held.Capture();
        Record("moving", moving); Record("clamped", clamp);
        Assert.True(Math.Abs(moving.FiberLengthMeters - clamp.FiberLengthMeters) > 1e-8);
        Assert.True(moving.Yield < 1); Assert.Equal(1, clamp.Yield);
        Assert.True(Math.Abs(moving.Forces.TendonAxialForceNewtons - clamp.Forces.TendonAxialForceNewtons) > 1e-6);
        Assert.InRange(moving.MaximumMomentumLedgerError, 0, 1e-12);
        Assert.InRange(moving.MaximumKineticLedgerError, 0, 1e-9);
        Assert.InRange(Assert.Single(moving.Units).MaximumCalciumLedgerError, 0, 1e-10);
    }

    [Fact]
    public void ExternalStretchMovesPassiveFiberWithoutInventingMotorEvents()
    {
        var muscle = Muscle(); double initial = muscle.Capture().MusculotendonLengthMeters;
        Advance(muscle, 100, length: time => initial + .001 * time / 100);
        var state = muscle.Capture(); var unit = Assert.Single(state.Units);
        Record("passive-stretch", state);
        Assert.True(state.FiberLengthMeters > Geometry.OptimalFiberLengthMeters);
        Assert.True(state.Yield < 1); Assert.Equal(0, unit.Activation); Assert.Equal(0, unit.Arrivals);
        Close(initial + .001, state.MusculotendonLengthMeters);
    }

    [Fact]
    public void SharedMechanicsDoesNotRecruitUnaddressedUnit()
    {
        var muscle = Muscle(units: [Definition(), Definition("unit-2")]);
        muscle.Receive(Arrival(0)); Advance(muscle, 100); var state = muscle.Capture();
        Record("selective-recruitment", state);
        Assert.True(state.Units[0].Activation > 0); Assert.Equal(1, state.Units[0].Arrivals);
        Assert.Equal(0, state.Units[1].Activation); Assert.Equal(0, state.Units[1].FreeCalcium);
        Assert.Equal(0, state.Units[1].Arrivals); Assert.True(state.Yield < 1);
    }

    [Fact]
    public void DimensionalMassAndForceScalingPreservesTrajectory()
    {
        var one = Muscle(); var g = Geometry;
        var twice = Muscle(geometry: g with { FiberMassKg = 2 * g.FiberMassKg, ReferenceForceNewtons = 2 * g.ReferenceForceNewtons },
            units: [Definition() with { Calcium = Definition().Calcium with { PeakTetanicForceNewtons = 2 * Definition().Calcium.PeakTetanicForceNewtons } }]);
        one.Receive(Arrival(0)); twice.Receive(Arrival(0)); Advance(one, 200); Advance(twice, 200);
        var a = one.Capture(); var b = twice.Capture();
        Record("original-scale", a); Record("double-scale", b);
        Close(a.FiberLengthMeters, b.FiberLengthMeters); Close(a.FiberVelocityMetersPerSecond, b.FiberVelocityMetersPerSecond);
        Close(a.Yield, b.Yield); Close(a.Units[0].Activation, b.Units[0].Activation);
        Close(2 * a.Forces.TendonAxialForceNewtons, b.Forces.TendonAxialForceNewtons);
    }

    [Fact]
    public void ReceiverDelayIsAppliedOnlyOnceAndIdentitySurvives()
    {
        var contact = Contact(delay: 2.125); var anatomy = new MotorAxonAnatomy([contact]);
        var publisher = new MotorAxonPublisher(anatomy, Clock, contact.SourceInstanceKey);
        var receiver = new AvatarMotorAxonReceiver(anatomy, Clock);
        var muscle = Muscle(units: [SlowMotorUnitDefinition.NagamoriSlowUnit1(contact)]);
        var spike = new SpikeMessage { MessageId = Guid.NewGuid(), SourceStructure = StructureId.SpinalCordMotor,
            SourceInstanceKey = contact.SourceInstanceKey, SourceNeuronId = contact.SourceNeuronId, TimestampMs = 10,
            ProjectionId = Projection, SynapseId = Guid.NewGuid(), Neurotransmitter = NTEnum.ACETYLCHOLINE };
        receiver.Accept(publisher.Publish([spike], 20)); Assert.Empty(receiver.AdvanceTo(12));
        var arrival = Assert.Single(receiver.AdvanceTo(12.125)); Advance(muscle, 12.125); muscle.Receive(arrival);
        Assert.Equal(spike.MessageId, Assert.Single(muscle.Capture().Units).LastMessageId);
        Advance(muscle, 12.1875); Assert.True(Assert.Single(muscle.Capture().Units).ReleaseDrive > 0);
    }

    [Fact]
    public void WrongIdentityOrTimeCannotChangeRecruitment()
    {
        var muscle = Muscle(); var original = Arrival(0); string before = Encode(muscle.Capture());
        AvatarMotorAxonArrival[] wrong = [original with { MotorUnitId = "missing" }, original with { SessionId = Guid.NewGuid() },
            original with { ContactId = Guid.NewGuid() }, original with { SourceNeuronId = "another-cell" },
            original with { ProjectionId = Guid.NewGuid() }, original with { SourceInstanceKey = "another-source" },
            original with { MessageId = Guid.Empty }, original with { SourceSynapseId = Guid.Empty },
            original with { Ordinal = 0 }, original with { ArrivalAtBodyMs = 1, FiredAtNeuralMs = 1 }];
        foreach (var arrival in wrong) { Assert.Throws<ArgumentException>(() => muscle.Receive(arrival)); Assert.Equal(before, Encode(muscle.Capture())); }
        muscle.Receive(original); Assert.Equal(1, Assert.Single(muscle.Capture().Units).Arrivals);
    }

    [Fact]
    public void DuplicateAndLateArrivalsAreRejected()
    {
        var muscle = Muscle(); var first = Arrival(0); muscle.Receive(first);
        Assert.Throws<ArgumentException>(() => muscle.Receive(first)); Advance(muscle, 1);
        Assert.Throws<ArgumentException>(() => muscle.Receive(Arrival(0, 2)));
        Assert.Equal(1, Assert.Single(muscle.Capture().Units).Arrivals);
    }

    [Fact]
    public void InvalidPrescribedIntervalLeavesOperationalStateUntouched()
    {
        var muscle = Muscle(); var state = muscle.Capture(); string before = Encode(state); double length = state.MusculotendonLengthMeters;
        PrescribedMusculotendonInterval[] wrong = [new(1, 1.0625, length, length), new(0, 1, length, length),
            new(0, .0625, length + .001, length), new(0, double.NaN, length, length), new(0, .0625, length, double.NaN), new(0, 0, length, length)];
        foreach (var interval in wrong) { Assert.Throws<ArgumentException>(() => muscle.AdvancePrescribed(interval)); Assert.Equal(before, Encode(muscle.Capture())); }
        Advance(muscle, .0625); Assert.False(muscle.Faulted);
    }

    [Fact]
    public void NumericalDomainFailureDoesNotCommitPartialStateOrResume()
    {
        var muscle = Muscle(); var before = muscle.Capture();
        Assert.Throws<InvalidOperationException>(() => muscle.AdvancePrescribed(new(0, .0625, before.MusculotendonLengthMeters, .001)));
        var after = muscle.Capture(); Assert.True(after.Faulted);
        Assert.Equal(Encode(before), Encode(after with { Faulted = false }));
        Assert.Throws<InvalidOperationException>(() => muscle.Receive(Arrival(0)));
        Assert.Throws<InvalidOperationException>(() => Advance(muscle, .0625));
    }

    [Fact]
    public void SnapshotDoesNotChangeWhenPlantAdvances()
    {
        var muscle = Muscle(); var snapshot = muscle.Capture(); string original = Encode(snapshot);
        muscle.Receive(Arrival(0)); Advance(muscle, 50);
        Assert.Equal(original, Encode(snapshot)); Assert.NotEqual(original, Encode(muscle.Capture()));
        Assert.Throws<NotSupportedException>(() => ((IList<DynamicMotorUnitState>)snapshot.Units)[0] = snapshot.Units[0]);
    }

    [Fact]
    public void UnitDeclarationOrderCannotAlterCoupledState()
    {
        var a = Muscle(units: [Definition(), Definition("unit-2")]); var b = Muscle(units: [Definition("unit-2"), Definition()]);
        a.Receive(Arrival(0)); b.Receive(Arrival(0)); Advance(a, 50); Advance(b, 50);
        Assert.Equal(Encode(a.Capture()), Encode(b.Capture()));
    }

    [Fact]
    public void GeometryAndParameterDomainErrorsAreExplicit()
    {
        Assert.Throws<ArgumentException>(() => Muscle(geometry: Geometry with { FiberMassKg = 0 }));
        Assert.Throws<ArgumentException>(() => Muscle(geometry: Geometry with { PennationRadians = Math.PI / 2 }));
        Assert.Throws<ArgumentException>(() => Muscle(units: [Definition() with { LengthGainAboveOptimal = double.NaN }]));
        Assert.Throws<ArgumentException>(() => Muscle(units: [Definition() with { Calcium = Definition().Calcium with { PeakTetanicForceNewtons = 1000 } }]));
        Assert.Throws<InvalidOperationException>(() => Muscle(normalizedFiber: 2));
        Assert.Throws<InvalidOperationException>(() => new AvatarDynamicMuscle(Geometry, [Definition()], Clock,
            Geometry.OptimalFiberLengthMeters, RestLength(Geometry), -8 * Geometry.OptimalFiberLengthMeters));
    }

    [Fact]
    public void AxialTendonForceIsDistinctFromItsFiberProjection()
    {
        var state = Muscle().Capture(); double cosine = Math.Cos(Geometry.PennationRadians);
        Assert.True(state.Forces.TendonAxialForceNewtons > state.Forces.ProjectedTendonForceNewtons);
        Close(state.Forces.TendonAxialForceNewtons * cosine, state.Forces.ProjectedTendonForceNewtons);
        Close(state.Forces.PassiveFiberForceNewtons * cosine * cosine, state.Forces.ProjectedFiberForceNewtons);
        Close(0, state.Forces.AccelerationMetersPerSecondSquared);
    }
}
