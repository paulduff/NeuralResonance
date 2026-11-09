namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class GolgiSdirkTests
{
    [Fact]
    public void StartsFromTheSameBiologicalStateAsThePreservedControl()
    {
        var control = new GolgiReferenceCell(); var candidate = new GolgiSdirkCell();
        Assert.Equal(control.CopyVoltages(), candidate.CopyVoltages());
        Assert.Equal(control.CopyGates(), candidate.CopyGates());
        Assert.Equal(control.CopySkOccupancy(), candidate.CopySkOccupancy());
        Assert.Equal(control.HighCalciumMm, candidate.HighCalciumMm);
        Assert.Equal(control.LowCalciumMm, candidate.LowCalciumMm);
        Assert.Empty(candidate.CopySomaticCrossings());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PassiveStagesMatchIndependentDenseRungeKuttaSystem(bool electrode)
    {
        var nodes = new GolgiCable().CopyNodes(); int n = nodes.Length;
        var stiffness = new double[n, n];
        for (int i = 0; i < n; ++i) stiffness[i, i] = nodes[i].LeakConductanceNs;
        for (int i = 1; i < n; ++i)
        {
            int parent = nodes[i].Parent; double g = nodes[i].AxialConductanceNs;
            stiffness[i, i] += g; stiffness[parent, parent] += g; stiffness[i, parent] -= g; stiffness[parent, i] -= g;
        }
        const double step = .015625; double gamma = 1 - Math.Sqrt(.5);
        var drive = new double[n]; drive[electrode ? n - 1 : 0] = 25;
        double[] initial = Enumerable.Repeat(-5d, n).ToArray();
        double[] Stage(double[] basis)
        {
            var matrix = (double[,])stiffness.Clone(); var rhs = (double[])drive.Clone();
            for (int i = 0; i < n; ++i)
            { double a = nodes[i].CapacitancePf / (gamma * step); matrix[i, i] += a; rhs[i] += a * basis[i]; }
            return DenseReference(matrix, rhs);
        }
        var first = Stage(initial);
        var basis = initial.Select((v, i) => v + (1-gamma)/gamma*(first[i]-v)).ToArray();
        var expected = Stage(basis);
        var candidate = new GolgiSdirkCell(block: GolgiDiagnosticBlock.Passive);
        candidate.Advance(step, electrode ? 0 : 25, electrode ? 25 : 0);
        var actual = candidate.CopyVoltages();
        for (int i = 0; i < n; ++i) Assert.InRange(Math.Abs(actual[i] - (expected[i]-55)), 0, 2e-8);
        Assert.InRange(candidate.MaximumKirchhoffResidualPa, 0, 1e-5);
        Assert.InRange(candidate.MaximumChargeBalanceErrorFc, 0, 1e-7);
    }

    [Fact]
    public void PassiveEquilibriumAndCalciumDoNotRequireClamping()
    {
        var cell = new GolgiSdirkCell(initialVoltageMv: -55, block: GolgiDiagnosticBlock.Passive);
        for (int i = 0; i < 32; ++i) cell.Advance(.015625);
        Assert.All(cell.CopyVoltages(), v => Assert.InRange(Math.Abs(v+55), 0, 1e-10));
        Assert.InRange(Math.Abs(cell.HighCalciumMm-.00005), 0, 1e-16);
        Assert.InRange(Math.Abs(cell.LowCalciumMm-.00005), 0, 1e-16);
        Assert.Empty(cell.CopySomaticCrossings());
        Assert.InRange(cell.MaximumSkMassError, 0, 1e-12);
    }

    [Fact]
    public void ActiveStagesPreserveAccountingAndExposeTheirResiduals()
    {
        var cell = new GolgiSdirkCell();
        for (int i = 0; i < 256; ++i) cell.Advance(.00390625, 50);
        Assert.True(cell.HighCalciumEnteredMm > 0 && cell.LowCalciumEnteredMm > 0);
        Assert.InRange(cell.MaximumScaledNonlinearResidual, 0, 1);
        Assert.InRange(cell.MaximumKirchhoffResidualPa, 0, 1e-5);
        Assert.InRange(cell.MaximumGateStageResidualPerMs, 0, 1e-7);
        Assert.InRange(cell.MaximumCalciumStageResidualMmPerMs, 0, 1e-10);
        Assert.InRange(cell.MaximumChargeBalanceErrorFc, 0, 1e-4);
        Assert.InRange(cell.MaximumCalciumBalanceErrorMm, 0, 1e-9);
        Assert.InRange(cell.MaximumSkMassError, 0, 1e-8);
        Assert.True(cell.NonlinearEvaluations > 0);
        var trace = new double[161]; cell.CopyPhysicalTrace(trace);
        var volts = cell.CopyVoltages();
        for (int i = 0; i < 135; ++i) Assert.Equal(volts[i], trace[i]-55);
        Assert.Equal(cell.CopyGates(), trace[135..153]); Assert.Equal(cell.CopySkOccupancy(), trace[153..159]);
        Assert.Equal(cell.HighCalciumMm*1000, trace[159]); Assert.Equal(cell.LowCalciumMm*1000, trace[160]);
        Assert.Throws<ArgumentException>(() => cell.CopyPhysicalTrace(new double[160]));
    }

    [Fact]
    public void RejectedArgumentsDoNotAdvanceOrFaultTheCell()
    {
        var cell = new GolgiSdirkCell(); var state = cell.CopyVoltages();
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.Advance(.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.Advance(.01, double.NaN));
        Assert.Equal(state, cell.CopyVoltages()); Assert.Equal(0, cell.TimeMs); Assert.False(cell.Faulted);
    }

    [Fact]
    public void NumericalFailureDoesNotCommitAPartialStepOrInventEvents()
    {
        var cell = new GolgiSdirkCell(); var state = cell.CopyVoltages(); var gates = cell.CopyGates();
        Assert.ThrowsAny<Exception>(() => cell.Advance(.015625, 1e100));
        Assert.True(cell.Faulted); Assert.Equal(0, cell.TimeMs); Assert.Equal(state, cell.CopyVoltages());
        Assert.Equal(gates, cell.CopyGates()); Assert.Empty(cell.CopySomaticCrossings());
        Assert.Equal(0, cell.NetMembraneChargeFc);
        Assert.Throws<InvalidOperationException>(() => cell.Advance(.001));
    }

    // Deliberately dense elimination, separate from the Hines/tree and compact
    // Newton solvers. Geometry itself is checked against the pinned deposit.
    private static double[] DenseReference(double[,] a, double[] b)
    {
        int n = b.Length;
        for (int k = 0; k < n; ++k)
        {
            int pivot = k;
            for (int i = k+1; i < n; ++i) if (Math.Abs(a[i,k]) > Math.Abs(a[pivot,k])) pivot = i;
            Assert.True(Math.Abs(a[pivot,k]) > 1e-16);
            for (int j = 0; j < n; ++j) (a[k,j],a[pivot,j]) = (a[pivot,j],a[k,j]);
            (b[k],b[pivot]) = (b[pivot],b[k]);
            for (int i = k+1; i < n; ++i)
            {
                double factor = a[i,k]/a[k,k];
                for (int j = k; j < n; ++j) a[i,j] -= factor*a[k,j];
                b[i] -= factor*b[k];
            }
        }
        var x = new double[n];
        for (int i = n-1; i >= 0; --i)
        { double sum = 0; for (int j = i+1; j < n; ++j) sum += a[i,j]*x[j]; x[i] = (b[i]-sum)/a[i,i]; }
        return x;
    }
}
