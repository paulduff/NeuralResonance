using System.Reflection;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class PurkinjeCalciumTests
{
    static PurkinjeCalciumPlasticity Run(bool entry=true,bool release=true,double dt=1,int order=-100)
    {
        var c=new PurkinjeCalciumPlasticity(1,-68){BlockEntry=!entry,BlockRelease=!release};
        for(int step=0;step<12000/dt;step++)
        {
            double t=step*dt;
            for(int trial=0;trial<10;trial++)for(int burst=0;burst<5;burst++)
                if(t==1000+trial*1000+order+burst*10)c.ParallelRelease(1);
            bool depolarized=t>=1000&&t<11000&&(t-1000)%1000<20;
            c.Advance(dt,depolarized?-20:-68);
        }
        return c;
    }

    [Fact]
    public void CalciumDependsOnVoltageAndStoreReleaseAndConservesMass()
    {
        var paired=Run();var noEntry=Run(entry:false);var noRelease=Run(release:false);
        Assert.True(paired.PeakCalciumUm>noEntry.PeakCalciumUm);
        Assert.True(paired.PeakCalciumUm>noRelease.PeakCalciumUm);
        Assert.True(paired.Weight<noEntry.Weight);
        Assert.True(paired.Weight<noRelease.Weight);
        Assert.InRange(paired.MaximumMassError,0,1e-9);
        Assert.InRange(paired.Weight,.628,1);
    }

    [Fact]
    public void ChemistryRefinementDoesNotMultiplyLearningOrRelease()
    {
        var coarse=Run(dt:1);var fine=Run(dt:.125);
        Assert.Equal(coarse.Weight,fine.Weight,10);
        Assert.Equal(coarse.PeakCalciumUm,fine.PeakCalciumUm,10);
        Assert.Equal(coarse.EntryUm,fine.EntryUm,10);
    }

    [Fact]
    public void NoPresynapticMarkerCanDirectlyChangeTheCalciumWeight()
    {
        var contact=new SynapseState(Guid.NewGuid(),NTEnum.GLUTAMATE,1){SourceStructure=StructureId.CerebellarGranule};
        ContactPlasticity.Spike(contact,PurkinjeCalciumPlasticity.Rule,true,10,0);
        ContactPlasticity.Spike(contact,PurkinjeCalciumPlasticity.Rule,false,20,1);
        Assert.Equal(1,contact.VesicleQuanta);
        var calcium=new PurkinjeCalciumPlasticity(1,-68);calcium.ParallelRelease(1);
        Assert.Equal(1,calcium.Weight);
        Assert.Equal(PurkinjeCalciumPlasticity.RestCalciumUm,calcium.CalciumUm);
    }

    [Fact]
    public void ContactChemistryIsLocalAndWarmStartDoesNotPretendToResumeIt()
    {
        var trained=Run();var untouched=new PurkinjeCalciumPlasticity(1,-68);
        Assert.Equal(0,untouched.Receptor);Assert.Equal(1,untouched.Weight);
        var contact=new SynapseState(Guid.NewGuid(),NTEnum.GLUTAMATE,(float)trained.Weight){PurkinjeCalcium=trained};
        var learned=contact.VesicleQuanta;contact.PrepareWarmStart();
        Assert.Null(contact.PurkinjeCalcium);Assert.Equal(learned,contact.VesicleQuanta);
    }

    [Fact]
    public void TimingChangesCalciumRatherThanUsingABinaryCoincidenceFlag()
    {
        var ahead=Run(order:-100);var reversed=Run(order:100);var farAhead=Run(order:-500);
        Assert.True(Math.Abs(ahead.PeakCalciumUm-reversed.PeakCalciumUm)>.001);
        Assert.True(Math.Abs(ahead.PeakCalciumUm-farAhead.PeakCalciumUm)>.001);
    }

    [Fact]
    public void ClimbingInputUsesConductanceAndRelaxesWithoutAForcedVoltageReset()
    {
        var dendrite=new PurkinjeClimbingCompartment(-68);
        var message=new SpikeMessage{SourceStructure=StructureId.InferiorOlive,TargetStructure=StructureId.PurkinjeCellLayer,
            SourceInstanceKey="isolated-source",SourceNeuronId="n-000",VesicleQuanta=1,Neurotransmitter=NTEnum.GLUTAMATE};
        dendrite.Release(message);Assert.Equal(-68,dendrite.VoltageMv);
        double peak=-68;
        for(int i=0;i<400;i++){dendrite.Advance(.125,-68);peak=Math.Max(peak,dendrite.VoltageMv);}
        Assert.InRange(peak,-40,-1);
        Assert.InRange(Math.Abs(dendrite.VoltageMv+68),0,.05);
    }

    [Fact]
    public void ClimbingCompartmentRefinementPreservesVoltageAndIonicLoad()
    {
        (double Voltage,double Load) Run(double dt)
        {
            var c=new PurkinjeClimbingCompartment(-68);double load=0;
            var message=new SpikeMessage{SourceStructure=StructureId.InferiorOlive,TargetStructure=StructureId.PurkinjeCellLayer,
                SourceNeuronId="n-000",VesicleQuanta=1,Neurotransmitter=NTEnum.GLUTAMATE};
            for(int i=0;i<100/dt;i++)
            {
                if(i*dt is 10 or 13 or 17)c.Release(message);
                c.Advance(dt,-68);load+=c.MeanAbsoluteCurrent*dt;
            }
            return(c.VoltageMv,load);
        }
        var a=Run(1);var b=Run(.125);Assert.Equal(a.Voltage,b.Voltage,10);Assert.Equal(a.Load,b.Load,8);
    }

    [Fact]
    public void PersistedMinimumDoesNotFightTheChemicalEfficacyState()
    {
        var c=new PurkinjeCalciumPlasticity(.05,-68);
        for(int t=0;t<2000;t++){if(t%10==0)c.ParallelRelease(1);c.Advance(1,-20);}
        Assert.True(c.InducedDepression>0);Assert.Equal(.05,c.Weight);
    }

    [Fact]
    public void InvalidChemicalInputsFailExplicitly()
    {
        var c=new PurkinjeCalciumPlasticity(1,-68);
        Assert.Throws<ArgumentOutOfRangeException>(()=>c.ParallelRelease(double.NaN));
        Assert.Throws<InvalidOperationException>(()=>c.Advance(0,-68));
        Assert.Throws<InvalidOperationException>(()=>c.Advance(1,double.NaN));
    }

    [Fact]
    public void DiagnosticPopulationCannotLoadOldStateAndPublicPopulationIsUnchanged()
    {
        string root=Path.Combine(Path.GetTempPath(),"nre-purkinje-tests",Guid.NewGuid().ToString("N"));
        string? oldState=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),oldInstance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        try
        {
            Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",root);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE","isolated-test");
            var profile=new StructureProfile(StructureId.PurkinjeCellLayer,"HH",PurkinjeCalciumPlasticity.Rule,"test",new DelayWindow(10,15));
            using(var small=new StructureEngine(profile,2))Assert.Equal(2,BiologicalContactLearningTests.Cells(small).Length);
            var file=Directory.GetFiles(root,"*.json").Single();string original=File.ReadAllText(file);
            Assert.Throws<InvalidOperationException>(()=>new StructureEngine(profile,2));
            Assert.Throws<InvalidOperationException>(()=>new StructureEngine(profile));
            Assert.Equal(original,File.ReadAllText(file));
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Path.Combine(root,"full"));
            using(var full=new StructureEngine(profile))Assert.Equal(192,BiologicalContactLearningTests.Cells(full).Length);
        }
        finally{Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",oldState);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",oldInstance);}
    }
}
