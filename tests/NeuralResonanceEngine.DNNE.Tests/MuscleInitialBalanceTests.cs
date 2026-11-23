using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MuscleInitialBalanceTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;
    public MuscleInitialBalanceTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;
    private static readonly Guid Session = Guid.Parse("f5c936e8-b50c-4ca6-814e-fa6882067d77");
    private static readonly Guid Projection = Guid.Parse("2df355b0-81fe-4aab-8a01-3369245873b7");
    private static MotorAxonClock Clock => new(Session, 1000, 25);
    private static DynamicMuscleGeometry Geometry => DynamicMuscleGeometry.NagamoriTibialisAnterior;
    private static SlowMotorUnitDefinition Unit(string name = "unit-1") =>
        SlowMotorUnitDefinition.NagamoriSlowUnit1(MotorAxonContact.Create("spinal-L", name, Projection, name, 0));
    private static double RestLength(DynamicMuscleGeometry g, double normalized)
    {
        // Independent algebraic inversion, with cancellation-safe library
        // functions. The candidate continues to use its original force kernel.
        double passive = 23 * .046 * double.LogP1(Math.Exp((normalized / g.PassiveLengthScale - 1.17) / .046));
        double tendon = .964 + .0047 * Math.Log(double.ExpM1(passive * Math.Cos(g.PennationRadians) / (27.8 * .0047)));
        return g.OptimalFiberLengthMeters * normalized * Math.Cos(g.PennationRadians) + g.ReferenceTendonLengthMeters * tendon;
    }
    private static MuscleInitialBalanceResult Solve(double length, DynamicMuscleGeometry? geometry = null,
        SlowMotorUnitDefinition[]? units = null, MuscleInitialBalanceLimits? limits = null)
    {
        var g = geometry ?? Geometry;
        return AvatarMuscleInitialBalance.Solve("declared-path", g, units ?? [Unit()], Clock, length,
            .4 * g.OptimalFiberLengthMeters, 1.45 * g.OptimalFiberLengthMeters, limits ?? MuscleInitialBalanceLimits.Diagnostic);
    }
    private static void Balanced(MuscleInitialBalanceResult r)
    {
        Assert.True(r.Bracketed); Assert.True(r.Balanced); Assert.NotNull(r.Definition);
        Assert.InRange(r.Candidate.FiberLengthMeters, r.FinalLowerFiberMeters, r.FinalUpperFiberMeters);
        Assert.InRange(r.FinalUpperFiberMeters-r.FinalLowerFiberMeters, 0, r.Limits.FiberLengthToleranceMeters);
        Assert.InRange(Math.Abs(r.ForceImbalanceNewtons), 0, r.ForceAllowanceNewtons);
        Assert.InRange(r.Evaluations, 2, r.Limits.MaximumIterations+2);
        Assert.Equal(FiberBoundary.Free, r.Candidate.Boundary);
        Assert.Equal(0, r.Candidate.FiberVelocityMetersPerSecond); Assert.Equal(1, r.Candidate.Yield);
        Assert.Equal(0, r.Candidate.Forces.ActiveFiberForceNewtons);
        Assert.All(r.Candidate.Units, u => { Assert.Equal(0, u.Arrivals); Assert.Equal(0, u.Activation); Assert.Equal(0, u.FreeCalcium); });
    }
    private void Record(string key, DynamicMuscleGeometry geometry, MuscleInitialBalanceResult result) =>
        _output.WriteLine("INITIAL_BALANCE_JSON " + JsonSerializer.Serialize(new { Key = key, Geometry = geometry, Result = result }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Theory]
    [InlineData(.5)]
    [InlineData(.75)]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.4)]
    public void RecoversAlgebraicPassiveBalance(double normalized)
    {
        var g = Geometry; var r = Solve(RestLength(g, normalized)); Record(FormattableString.Invariant($"length-{normalized}"), g, r); Balanced(r);
        // Solver bracket precision and constitutive floating-point accuracy are
        // separate. The external Decimal comparison uses this same 1e-11 m bound.
        Assert.InRange(Math.Abs(r.Candidate.FiberLengthMeters-normalized*g.OptimalFiberLengthMeters), 0, 1e-11);
    }

    [Theory]
    [InlineData(.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void DimensionallyScaledGeometryRetainsDeclaredBalance(double scale)
    {
        var g = Geometry with { OptimalFiberLengthMeters = Geometry.OptimalFiberLengthMeters*scale,
            ReferenceTendonLengthMeters = Geometry.ReferenceTendonLengthMeters*scale,
            ReferenceForceNewtons = Geometry.ReferenceForceNewtons*scale*scale,
            FiberMassKg = Geometry.FiberMassKg*scale*scale*scale };
        var r = Solve(RestLength(g, 1.1), g); Record(FormattableString.Invariant($"scale-{scale}"), g, r); Balanced(r);
        Assert.InRange(Math.Abs(r.Candidate.FiberLengthMeters-1.1*g.OptimalFiberLengthMeters), 0, 1e-11);
    }

    [Fact]
    public void FreshEpochAndLedgersRemainUntouched()
    {
        var r = Solve(RestLength(Geometry, 1)); Balanced(r);
        Assert.Equal(Clock.BodyEpochMs, r.Candidate.TimeMs);
        Assert.Equal(0, r.Candidate.IntegratedNetImpulseNewtonSeconds); Assert.Equal(0, r.Candidate.IntegratedNetFiberWorkJoules);
        Assert.All(r.Candidate.Units, u => { Assert.Equal(Guid.Empty, u.LastMessageId); Assert.Equal(0, u.ReleasedCalcium); Assert.Equal(0, u.RecoveredCalcium); });
    }

    [Fact]
    public void UnitIdentityIsPreservedAndCallerArrayIsCopied()
    {
        var unit = Unit(); var array = new[] { unit }; var r = Solve(RestLength(Geometry, 1), units: array); Balanced(r);
        array[0] = Unit("replacement");
        Assert.Same(unit, Assert.Single(r.Definition!.Units)); Assert.Equal(unit.Contact.MotorUnitId, Assert.Single(r.Candidate.Units).MotorUnitId);
        Assert.Equal("declared-path", r.Definition.MuscleId); Assert.Same(Geometry, r.Definition.Geometry);
    }

    [Fact]
    public void IncompatibleShortPathProducesNoMuscleDefinition()
    {
        var r = Solve(.2);
        Assert.False(r.Bracketed); Assert.False(r.Balanced); Assert.Null(r.Definition);
        Assert.Equal("NoRootInDeclaredBracket", r.StopReason); Assert.Equal(.2, r.MeasuredPathLengthMeters);
        Assert.Equal(.4*Geometry.OptimalFiberLengthMeters, r.FinalLowerFiberMeters);
        Assert.Equal(1.45*Geometry.OptimalFiberLengthMeters, r.FinalUpperFiberMeters);
    }

    [Fact]
    public void ExhaustedIterationBudgetReturnsCandidateInsideFinalBracket()
    {
        var r = Solve(RestLength(Geometry, 1), limits: MuscleInitialBalanceLimits.Diagnostic with { MaximumIterations = 1 });
        Assert.True(r.Bracketed); Assert.False(r.Balanced); Assert.Null(r.Definition); Assert.Equal("IterationLimit", r.StopReason);
        Assert.InRange(r.Candidate.FiberLengthMeters, r.FinalLowerFiberMeters, r.FinalUpperFiberMeters);
        Assert.Equal(3, r.Evaluations);
    }

    [Theory]
    [InlineData(0, .1)]
    [InlineData(.1, .05)]
    [InlineData(.05, .05)]
    [InlineData(double.NaN, .1)]
    public void InvalidDeclaredBracketRejects(double lo, double hi) =>
        Assert.Throws<ArgumentException>(() => AvatarMuscleInitialBalance.Solve("path", Geometry, [Unit()], Clock,
            RestLength(Geometry, 1), lo, hi, MuscleInitialBalanceLimits.Diagnostic));

    [Fact]
    public void OriginalLengthDomainRemainsEnforced() =>
        Assert.Throws<InvalidOperationException>(() => AvatarMuscleInitialBalance.Solve("path", Geometry, [Unit()], Clock,
            RestLength(Geometry, 1), .01*Geometry.OptimalFiberLengthMeters, Geometry.OptimalFiberLengthMeters, MuscleInitialBalanceLimits.Diagnostic));

    [Fact]
    public void MissingMotorUnitAnatomyRejects() => Assert.Throws<ArgumentException>(() => Solve(RestLength(Geometry, 1), units: []));

    [Fact]
    public void AddingSilentUnitsDoesNotInventRestingActivation()
    {
        var a = Solve(RestLength(Geometry, 1)); var b = Solve(RestLength(Geometry, 1), units: [Unit(), Unit("unit-2")]);
        Balanced(a); Balanced(b); Assert.Equal(a.Candidate.FiberLengthMeters, b.Candidate.FiberLengthMeters);
        Assert.Equal(a.Candidate.Forces, b.Candidate.Forces); Assert.Equal(2, b.Candidate.Units.Count);
    }

    [Fact]
    public void InitializedMuscleEvolvesAfterAnArrivalWithoutReinitialization()
    {
        var r = Solve(RestLength(Geometry, 1)); Balanced(r); var d = r.Definition!;
        var muscle = new AvatarDynamicMuscle(d.Geometry, d.Units, Clock, d.InitialFiberLengthMeters, r.MeasuredPathLengthMeters);
        var contact = Assert.Single(d.Units).Contact; Guid message = Guid.NewGuid();
        muscle.Receive(new(Session, contact.SourceInstanceKey, message, 1, contact.SourceNeuronId, contact.ProjectionId,
            Guid.NewGuid(), contact.ContactId, contact.MotorUnitId, Clock.NeuralEpochMs, Clock.BodyEpochMs));
        for (int i = 0; i < 1600; i++)
        {
            double start = Clock.BodyEpochMs+i*.0625;
            muscle.AdvancePrescribed(new(start, start+.0625, r.MeasuredPathLengthMeters, r.MeasuredPathLengthMeters));
        }
        var after = muscle.Capture(); var unit = Assert.Single(after.Units);
        Assert.Equal(Clock.BodyEpochMs+100, after.TimeMs); Assert.Equal(1, unit.Arrivals); Assert.Equal(message, unit.LastMessageId);
        Assert.True(unit.Activation > 0); Assert.True(Math.Abs(after.FiberLengthMeters-r.Candidate.FiberLengthMeters) > 1e-8);
        Assert.Equal(Clock.BodyEpochMs, r.Candidate.TimeMs); Assert.Equal(0, Assert.Single(r.Candidate.Units).Arrivals);
    }
}
