using System.Text.Json;
using Xunit.Abstractions;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class GolgiSdirk3Tests(ITestOutputHelper output)
{
    [Fact]
    public void TableauSatisfiesThirdOrderConditionsAndStiffAccuracy()
    {
        double g=GolgiSdirkCell.ThirdGamma;
        double[,] a={{g,0,0},{GolgiSdirkCell.ThirdA21,g,0},{GolgiSdirkCell.ThirdB1,GolgiSdirkCell.ThirdB2,g}};
        double[] b={a[2,0],a[2,1],a[2,2]}, c={g,(1+g)/2,1};
        Assert.InRange(Math.Abs(6*g*g*g-18*g*g+9*g-1),0,2e-15);
        for(int i=0;i<3;++i)Assert.InRange(Math.Abs(Enumerable.Range(0,3).Sum(j=>a[i,j])-c[i]),0,2e-15);
        Assert.InRange(Math.Abs(b.Sum()-1),0,2e-15);
        Assert.InRange(Math.Abs(Enumerable.Range(0,3).Sum(i=>b[i]*c[i])-.5),0,2e-15);
        Assert.InRange(Math.Abs(Enumerable.Range(0,3).Sum(i=>b[i]*c[i]*c[i])-1d/3),0,2e-15);
        Assert.InRange(Math.Abs(Enumerable.Range(0,3).Sum(i=>b[i]*Enumerable.Range(0,3).Sum(j=>a[i,j]*c[j]))-1d/6),0,2e-15);
        Assert.True(b[1]<0); // Negative quadrature weight must not be discarded or clipped.
    }

    [Fact]
    public void ExplicitThirdOrderRetainsInitialBiologyAndSecondOrderRemainsDefault()
    {
        var initial=new GolgiReferenceCell();var candidate=new GolgiSdirkCell(order:GolgiSdirkOrder.Third);
        Assert.Equal(initial.CopyVoltages(),candidate.CopyVoltages());Assert.Equal(initial.CopyGates(),candidate.CopyGates());
        Assert.Equal(initial.CopySkOccupancy(),candidate.CopySkOccupancy());Assert.Equal(initial.HighCalciumMm,candidate.HighCalciumMm);
        Assert.Equal(initial.LowCalciumMm,candidate.LowCalciumMm);Assert.Equal(GolgiSdirkCell.ThirdOrderVersion,candidate.SolverVersion);
        var ordinary=new GolgiSdirkCell();var explicitSecond=new GolgiSdirkCell(order:GolgiSdirkOrder.Second);
        Assert.Equal(GolgiSdirkOrder.Second,ordinary.Order);Assert.Equal(GolgiSdirkCell.Version,ordinary.SolverVersion);
        for(int i=0;i<64;++i){ordinary.Advance(.00390625,50);explicitSecond.Advance(.00390625,50);}
        Assert.Equal(Trace(ordinary),Trace(explicitSecond));Assert.Equal(ordinary.NetMembraneChargeFc,explicitSecond.NetMembraneChargeFc);
        Assert.Equal(ordinary.CopySomaticCrossings(),explicitSecond.CopySomaticCrossings());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThreePassiveStagesMatchIndependentDenseTableauEvaluation(bool electrode)
    {
        var nodes=new GolgiCable().CopyNodes();int n=nodes.Length;
        var stiffness=new double[n,n];
        for(int i=0;i<n;++i)stiffness[i,i]=nodes[i].LeakConductanceNs;
        for(int i=1;i<n;++i){int p=nodes[i].Parent;double g=nodes[i].AxialConductanceNs;
            stiffness[i,i]+=g;stiffness[p,p]+=g;stiffness[i,p]-=g;stiffness[p,i]-=g;}
        // Independently expressed tableau, applied as dense M*dV/dt=-K*V+I.
        double gamma=1-Math.Sqrt(2)*Math.Cos(Math.Atan(Math.Sqrt(2)/4)/3)/2+
            Math.Sqrt(6)*Math.Sin(Math.Atan(Math.Sqrt(2)/4)/3)/2;
        double[,] a={{gamma,0,0},{.5*(1-gamma),gamma,0},
            {.25*(-6*gamma*gamma+16*gamma-1),.25*(6*gamma*gamma-20*gamma+5),gamma}};
        const double h=.015625;var initial=Enumerable.Repeat(-5d,n).ToArray();var increments=new double[3][];
        var drive=new double[n];drive[electrode?n-1:0]=25;double[]? expected=null;
        for(int stage=0;stage<3;++stage)
        {
            var basis=(double[])initial.Clone();
            for(int i=0;i<n;++i)for(int j=0;j<stage;++j)basis[i]+=a[stage,j]*increments[j][i];
            var matrix=(double[,])stiffness.Clone();var rhs=(double[])drive.Clone();
            for(int i=0;i<n;++i){double mass=nodes[i].CapacitancePf/(gamma*h);matrix[i,i]+=mass;rhs[i]+=mass*basis[i];}
            expected=Dense(matrix,rhs);increments[stage]=expected.Select((v,i)=>(v-basis[i])/gamma).ToArray();
        }
        var candidate=new GolgiSdirkCell(block:GolgiDiagnosticBlock.Passive,order:GolgiSdirkOrder.Third);
        candidate.Advance(h,electrode?0:25,electrode?25:0);
        var actual=candidate.CopyVoltages();
        for(int i=0;i<n;++i)Assert.InRange(Math.Abs(actual[i]-(expected![i]-55)),0,2e-8);
        Assert.InRange(candidate.MaximumKirchhoffResidualPa,0,1e-5);
        Assert.InRange(candidate.MaximumChargeBalanceErrorFc,0,1e-7);
    }

    [Fact]
    public void PassiveEquilibriumRetainsCalciumAndSkMassWithoutClipping()
    {
        var cell=new GolgiSdirkCell(initialVoltageMv:-55,block:GolgiDiagnosticBlock.Passive,order:GolgiSdirkOrder.Third);
        for(int i=0;i<32;++i)cell.Advance(.015625);
        Assert.All(cell.CopyVoltages(),v=>Assert.InRange(Math.Abs(v+55),0,1e-10));
        Assert.InRange(Math.Abs(cell.HighCalciumMm-.00005),0,1e-16);Assert.InRange(Math.Abs(cell.LowCalciumMm-.00005),0,1e-16);
        Assert.InRange(cell.MaximumSkMassError,0,1e-12);Assert.Empty(cell.CopySomaticCrossings());
    }

    [Theory]
    [InlineData("None")]
    [InlineData("WithoutPersistentSodium")]
    [InlineData("WithoutHcn")]
    [InlineData("WithoutSk")]
    public void ActiveStateRefinesAndRetainsIndependentStageAccounting(string blockName)
    {
        var block=Enum.Parse<GolgiDiagnosticBlock>(blockName);
        double[] steps={.015625,.0078125,.00390625,.00048828125};var traces=new List<double[]>();
        foreach(double step in steps)
        {
            var cell=new GolgiSdirkCell(block:block,order:GolgiSdirkOrder.Third);
            for(int tick=0;tick<(int)(4/step);++tick)cell.Advance(step,50);
            Assert.Equal(4,cell.TimeMs);Assert.False(cell.Faulted);traces.Add(Trace(cell));
            Assert.InRange(cell.MaximumScaledNonlinearResidual,0,1);
            Assert.InRange(cell.MaximumKirchhoffResidualPa,0,1e-5);
            Assert.InRange(cell.MaximumGateStageResidualPerMs,0,1e-7);
            Assert.InRange(cell.MaximumCalciumStageResidualMmPerMs,0,1e-10);
            Assert.InRange(cell.MaximumChargeBalanceErrorFc,0,1e-4);
            Assert.InRange(cell.MaximumCalciumBalanceErrorMm,0,1e-9);
            Assert.InRange(cell.MaximumSkMassError,0,1e-8);
            Assert.True(cell.HighCalciumEnteredMm>0 && cell.LowCalciumEnteredMm>0);
        }
        // Short self-refinement, not independent-reference or long-spike timing
        // qualification. The 0.2 factor distinguishes expected cubic reduction
        // from second order where errors are well above declared numerical floors.
        foreach(var group in new[]{("voltageMv",0,135,2e-7),("gates",135,18,2e-9),("sk",153,6,2e-9),("calciumUm",159,2,2e-7)})
        {
            var errors=traces.Take(3).Select(t=>Enumerable.Range(group.Item2,group.Item3).Max(i=>Math.Abs(t[i]-traces[3][i]))).ToArray();
            output.WriteLine(JsonSerializer.Serialize(new{block=block.ToString(),group=group.Item1,steps,errors,refinementFactor=.2,roundoffFloor=group.Item4}));
            for(int i=1;i<errors.Length;++i)Assert.True(errors[i]<=.2*errors[i-1]+group.Item4,
                $"{block}/{group.Item1}: h={steps[i]:R}, error={errors[i]:R}, previous={errors[i-1]:R}, floor={group.Item4:R}");
        }
    }

    [Fact]
    public void RejectedArgumentsPreserveThirdOrderStateAndClock()
    {
        Assert.Throws<ArgumentException>(()=>new GolgiSdirkCell(order:(GolgiSdirkOrder)4));
        var cell=new GolgiSdirkCell(order:GolgiSdirkOrder.Third);var before=Trace(cell);
        Assert.Throws<ArgumentOutOfRangeException>(()=>cell.Advance(.1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>cell.Advance(.01,electrodeCurrentPa:double.NaN));
        Assert.Equal(before,Trace(cell));Assert.Equal(0,cell.TimeMs);Assert.False(cell.Faulted);
    }

    [Fact]
    public void FailedThirdOrderStepDoesNotCommitPhysicalStateLedgersOrEvents()
    {
        var cell=new GolgiSdirkCell(order:GolgiSdirkOrder.Third);var before=Trace(cell);
        Assert.ThrowsAny<Exception>(()=>cell.Advance(.015625,1e100));
        Assert.Equal(before,Trace(cell));Assert.Equal(0,cell.TimeMs);Assert.True(cell.Faulted);
        Assert.Equal(0,cell.NetMembraneChargeFc);Assert.Equal(0,cell.HighCalciumEnteredMm);
        Assert.Equal(0,cell.HighCalciumRemovedMm);Assert.Equal(0,cell.LowCalciumEnteredMm);Assert.Equal(0,cell.LowCalciumRemovedMm);
        Assert.Empty(cell.CopySomaticCrossings());Assert.Throws<InvalidOperationException>(()=>cell.Advance(.001));
    }

    private static double[] Trace(GolgiSdirkCell cell){var values=new double[161];cell.CopyPhysicalTrace(values);return values;}

    private static double[] Dense(double[,] a,double[] b)
    {
        int n=b.Length;
        for(int k=0;k<n;++k)
        {
            int pivot=k;for(int i=k+1;i<n;++i)if(Math.Abs(a[i,k])>Math.Abs(a[pivot,k]))pivot=i;
            Assert.True(Math.Abs(a[pivot,k])>1e-16);
            for(int j=0;j<n;++j)(a[k,j],a[pivot,j])=(a[pivot,j],a[k,j]);(b[k],b[pivot])=(b[pivot],b[k]);
            for(int i=k+1;i<n;++i){double factor=a[i,k]/a[k,k];for(int j=k;j<n;++j)a[i,j]-=factor*a[k,j];b[i]-=factor*b[k];}
        }
        var x=new double[n];for(int i=n-1;i>=0;--i){double sum=0;for(int j=i+1;j<n;++j)sum+=a[i,j]*x[j];x[i]=(b[i]-sum)/a[i,i];}return x;
    }
}
