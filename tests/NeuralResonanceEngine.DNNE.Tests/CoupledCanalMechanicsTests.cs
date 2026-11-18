using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Synthetic diagonal controls test mechanics, not human physiological values.
// The published specimen is separately source-labelled, never a live preset.
public sealed class CoupledCanalMechanicsTests
{
    private static double[] Diagonal(double value, int first = 0)
    {
        var a = new double[36];
        for (int i = first; i < 6; i++) a[6 * i + i] = value;
        return a;
    }
    private static AvatarCoupledCanalParameters Synthetic(double damping = 2) =>
        new("analytic control only", new string('A', 64), Diagonal(1), Diagonal(damping), Diagonal(1, 3), new double[18]);
    private static AvatarCanalVector V(params double[] values) =>
        new(values[0], values[1], values[2], values[3], values[4], values[5]);
    private static AvatarCanalMechanicalPoint Zero() => new(default, default);
    private static void Close(double expected, double actual, double tolerance) =>
        Assert.InRange(Math.Abs(actual - expected), 0, tolerance);

    [Fact]
    public void PublishedCatalogUsesExplicitSIUnitsAndSourceIdentity()
    {
        var p = AvatarCoupledCanalParameters.PublishedRightLabyrinth1();
        Assert.Contains("right human labyrinth 1", p.Source);
        Assert.Equal("07724245A89D5BF4959DE50181830D932F70AD15BA402C6FFAef00DC2F0DB1E2".ToUpperInvariant(), p.SourceSha256);
        Assert.Equal(36, p.MassKgPerMeter4.Count);
        Assert.Equal(18, p.PressurePascalPerRadianPerSecondSquared.Count);
        Assert.Equal(1134e5, p.MassKgPerMeter4[0]);
        Assert.Equal(18.6e5, p.MassKgPerMeter4[1]);
        Assert.Equal(-2e11, p.DampingPascalSecondsPerMeter3[3]);
        Assert.Equal(17016e5, p.StiffnessPascalPerMeter3[21]);
        Assert.Equal(-.06107, p.PressurePascalPerRadianPerSecondSquared[0]);
        Assert.Equal(.06399, p.PressurePascalPerRadianPerSecondSquared[4]);
        Assert.Equal(p.Fingerprint, AvatarCoupledCanalParameters.PublishedRightLabyrinth1().Fingerprint);
    }

    [Fact]
    public void ParametersCopyTheirInputsAndExposeReadOnlyMatrices()
    {
        var m = Diagonal(1); var c = Diagonal(2); var k = Diagonal(1, 3); var f = new double[18];
        var p = new AvatarCoupledCanalParameters("fixture", new string('A', 64), m, c, k, f);
        string fingerprint = p.Fingerprint;
        m[0] = 9; c[0] = 9; k[21] = 9; f[0] = 9;
        Assert.Equal(1, p.MassKgPerMeter4[0]);
        Assert.Equal(2, p.DampingPascalSecondsPerMeter3[0]);
        Assert.Equal(1, p.StiffnessPascalPerMeter3[21]);
        Assert.Equal(0, p.PressurePascalPerRadianPerSecondSquared[0]);
        Assert.Equal(fingerprint, p.Fingerprint);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)p.MassKgPerMeter4)[0] = 5);
    }

    [Fact]
    public void SourceCoefficientsAndSolverStepParticipateInIdentity()
    {
        var p = Synthetic();
        var otherSource = new AvatarCoupledCanalParameters("another source", p.SourceSha256,
            p.MassKgPerMeter4, p.DampingPascalSecondsPerMeter3, p.StiffnessPascalPerMeter3, p.PressurePascalPerRadianPerSecondSquared);
        Assert.NotEqual(p.Fingerprint, otherSource.Fingerprint);
        Assert.NotEqual(p.Fingerprint, Synthetic(3).Fingerprint);
        Assert.NotEqual(new AvatarCoupledCanalDynamics(p, .01).Fingerprint, new AvatarCoupledCanalDynamics(p, .005).Fingerprint);
    }

    [Fact]
    public void SourceIdentityDimensionsAndFiniteValuesAreRequired()
    {
        var m = Diagonal(1); var c = Diagonal(2); var k = Diagonal(1, 3); var f = new double[18];
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalParameters(" ", new string('A', 64), m, c, k, f));
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalParameters("fixture", "invalid", m, c, k, f));
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalParameters("fixture", new string('A', 64), m[..35], c, k, f));
        f[0] = double.NaN;
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalParameters("fixture", new string('A', 64), m, c, k, f));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NegativeAndAsymmetricMechanicalMatricesAreRejected(bool damping)
    {
        var m = Diagonal(1); var c = Diagonal(2); var a = damping ? c : m;
        a[0] = -1;
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalParameters("fixture", new string('A', 64), m, c, Diagonal(1, 3), new double[18]));
        a[0] = 1; a[1] = .1;
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalParameters("fixture", new string('A', 64), m, c, Diagonal(1, 3), new double[18]));
    }

    [Fact]
    public void CupulaStiffnessTopologyIsExplicit()
    {
        var k = Diagonal(1, 3); k[21] = 0;
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalParameters("fixture", new string('A', 64), Diagonal(1), Diagonal(2), k, new double[18]));
        k[21] = 1; k[0] = 1;
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalParameters("fixture", new string('A', 64), Diagonal(1), Diagonal(2), k, new double[18]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SolverRequiresPositiveFiniteSeconds(double step)
    {
        Assert.Throws<ArgumentException>(() => new AvatarCoupledCanalDynamics(Synthetic(), step));
    }

    [Fact]
    public void UndrivenEmptyStateRemainsExactlyEmpty()
    {
        var solver = new AvatarCoupledCanalDynamics(AvatarCoupledCanalParameters.PublishedRightLabyrinth1(), .001);
        var result = solver.AdvanceHeld(Zero(), default);
        Assert.Equal(Zero(), result.After);
        Assert.Equal(0, result.EnergyAfterJoules);
        Assert.Equal(0, result.PhysicalDampingJoules);
        Assert.Equal(0, result.NumericalDampingJoules);
        Assert.Equal(0, result.MaximumStageBackwardError);
    }

    [Fact]
    public void DerivativeSatisfiesTheDimensionalCoupledForceEquation()
    {
        var p = AvatarCoupledCanalParameters.PublishedRightLabyrinth1();
        var point = new AvatarCanalMechanicalPoint(V(1e-13, -2e-13, 3e-13, 4e-13, -5e-13, 6e-13), V(1e-12, 2e-12, -3e-12, 4e-12, 5e-12, -6e-12));
        var d = new AvatarCoupledCanalDynamics(p, .001).Evaluate(point, new(1, -.5, .25));
        var q = point.VolumeMeters3.ToArray(); var v = point.FlowMeters3PerSecond.ToArray(); var a = d.AccelerationMeters3PerSecond2.ToArray();
        var f = d.PressurePascal.ToArray();
        for (int i = 0; i < 6; i++)
        {
            double lhs = 0;
            for (int j = 0; j < 6; j++) lhs += p.MassKgPerMeter4[6 * i + j] * a[j] + p.DampingPascalSecondsPerMeter3[6 * i + j] * v[j] + p.StiffnessPascalPerMeter3[6 * i + j] * q[j];
            Close(f[i], lhs, 1e-12);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ASourcePrimeInputIsNotSilentlyReducedToIndependentFilters(int component)
    {
        double[] input = new double[3]; input[component] = 1;
        var solver = new AvatarCoupledCanalDynamics(AvatarCoupledCanalParameters.PublishedRightLabyrinth1(), .001);
        var d = solver.Evaluate(Zero(), new(input[0], input[1], input[2]));
        Assert.All(d.AccelerationMeters3PerSecond2.ToArray(), x => Assert.NotEqual(0, x));
    }

    [Fact]
    public void ReversingInputReversesMotionWithoutChangingDissipation()
    {
        var solver = new AvatarCoupledCanalDynamics(AvatarCoupledCanalParameters.PublishedRightLabyrinth1(), .001);
        var plus = solver.AdvanceHeld(Zero(), new(1, -.5, .25));
        var minus = solver.AdvanceHeld(Zero(), new(-1, .5, -.25));
        Assert.Equal(plus.After.VolumeMeters3.ToArray().Select(x => -x), minus.After.VolumeMeters3.ToArray());
        Assert.Equal(plus.After.FlowMeters3PerSecond.ToArray().Select(x => -x), minus.After.FlowMeters3PerSecond.ToArray());
        Assert.Equal(plus.PhysicalDampingJoules, minus.PhysicalDampingJoules);
        Assert.Equal(plus.NumericalDampingJoules, minus.NumericalDampingJoules);
    }

    [Fact]
    public void PhysicalAndNumericalEnergyLossAreAccountedSeparately()
    {
        var solver = new AvatarCoupledCanalDynamics(Synthetic(), .1);
        var state = new AvatarCanalMechanicalPoint(V(0, 0, 0, 1, -.5, .25), V(1, -.5, .25, .2, -.1, .05));
        for (int i = 0; i < 20; i++)
        {
            var step = solver.AdvanceHeld(state, default);
            Assert.InRange(step.EnergyAfterJoules, 0, step.EnergyBeforeJoules);
            Assert.True(step.PhysicalDampingJoules > 0);
            Assert.True(step.NumericalDampingJoules > 0);
            Close(0, step.EnergyIdentityResidualJoules, 5e-14 * step.EnergyBeforeJoules);
            Assert.InRange(step.MaximumStageBackwardError, 0, 1e-12);
            state = step.After;
        }
    }

    [Fact]
    public void WorkBalancesStoredEnergyAndBothLossesForThePublishedMatrices()
    {
        var solver = new AvatarCoupledCanalDynamics(AvatarCoupledCanalParameters.PublishedRightLabyrinth1(), .001);
        var result = solver.AdvanceHeld(Zero(), new(1, -.5, .25));
        Assert.True(result.InputWorkJoules > 0);
        Assert.True(result.PhysicalDampingJoules > 0);
        Assert.True(result.NumericalDampingJoules > 0);
        Close(0, result.EnergyIdentityResidualJoules, 1e-10 * result.InputWorkJoules);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefinementMatchesIndependentClosedFormMechanics(bool spring)
    {
        double previous = double.PositiveInfinity;
        foreach (int count in new[] { 100, 200, 400 })
        {
            var solver = new AvatarCoupledCanalDynamics(Synthetic(), 1d / count);
            var state = spring ? new AvatarCanalMechanicalPoint(V(0, 0, 0, 1, 0, 0), default)
                : new AvatarCanalMechanicalPoint(default, V(1, 0, 0, 0, 0, 0));
            for (int i = 0; i < count; i++) state = solver.AdvanceHeld(state, default).After;
            double q = spring ? state.VolumeMeters3.CupulaHC : state.VolumeMeters3.EndolymphHC;
            double v = spring ? state.FlowMeters3PerSecond.CupulaHC : state.FlowMeters3PerSecond.EndolymphHC;
            double expectedQ = spring ? 2 * Math.Exp(-1) : .5 * (1 - Math.Exp(-2));
            double expectedV = spring ? -Math.Exp(-1) : Math.Exp(-2);
            double error = Math.Max(Math.Abs(q - expectedQ), Math.Abs(v - expectedV));
            Assert.InRange(error, 0, 2e-6);
            Assert.True(error < previous * .2, $"error {error:R}, previous {previous:R}");
            previous = error;
        }
    }

    [Fact]
    public void UnresolvedStiffModeDampsWithoutCreatingEnergy()
    {
        var solver = new AvatarCoupledCanalDynamics(Synthetic(1e6), .001);
        var before = new AvatarCanalMechanicalPoint(default, V(1, 0, 0, 0, 0, 0));
        var step = solver.AdvanceHeld(before, default);
        Assert.InRange(Math.Abs(step.After.FlowMeters3PerSecond.EndolymphHC), 0, .003);
        Assert.InRange(step.EnergyAfterJoules, 0, step.EnergyBeforeJoules);
        Assert.True(step.NumericalDampingJoules > 0);
        Close(0, step.EnergyIdentityResidualJoules, 1e-12);
    }

    [Fact]
    public void RejectedInputCannotMutateAnAcceptedMechanicalPoint()
    {
        var solver = new AvatarCoupledCanalDynamics(Synthetic(), .01);
        var state = new AvatarCanalMechanicalPoint(default, V(1, 0, 0, 0, 0, 0));
        var accepted = solver.AdvanceHeld(state, default);
        Assert.Throws<ArgumentException>(() => solver.AdvanceHeld(state, new(double.NaN, 0, 0)));
        Assert.Equal(accepted, solver.AdvanceHeld(state, default));
        Assert.Equal(1, state.FlowMeters3PerSecond.EndolymphHC);
    }
}
