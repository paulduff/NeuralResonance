namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class OlivaryBatchTests
{
    static readonly OlivaryCellIdentity A=new("isolated-left-olive","N_0"),B=new("isolated-right-olive","N_0");
    static OlivaryCellSeed Seed(OlivaryCellIdentity id)=>OlivaryCellSeed.Capture(id,0,new());
    static void Same(OlivaryCellSeed expected,OlivaryCellSeed actual)
    {
        Assert.Equal(expected.Identity,actual.Identity);Assert.Equal(expected.Parameters,actual.Parameters);
        Assert.Equal(expected.SomaticSpikes,actual.SomaticSpikes);Assert.Equal(expected.AxonalSpikes,actual.AxonalSpikes);
        Assert.Equal(expected.State.Concat(new[]{expected.TimeMs,expected.AdditionalIonicLoad,expected.SynapticCurrent}).Select(BitConverter.DoubleToInt64Bits),
            actual.State.Concat(new[]{actual.TimeMs,actual.AdditionalIonicLoad,actual.SynapticCurrent}).Select(BitConverter.DoubleToInt64Bits));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void MixedIntervalsAndInputsMatchAllReferenceStateBits(int workers)
    {
        var ids=new[]{A,B,new OlivaryCellIdentity("isolated-left-olive","N_1")};
        var cells=new[]{new OlivaryMembrane(),new OlivaryMembrane(new(Hcn:0)),new OlivaryMembrane(new(LowCalcium:0))};
        var batch=new OlivaryCpuBatch(ids.Select((id,i)=>OlivaryCellSeed.Capture(id,0,cells[i])).ToArray());
        var times=new double[3];
        for(int frame=0;frame<512;frame++)
        {
            var steps=ids.Select((id,i)=>new OlivaryBatchStep(id,times[i],i==0?.125:.03125,
                new(DendriticAmpa:frame%8==i?.12:0,SomaticGaba:frame%13==i?.08:0,DendriticCurrent:frame>64&&frame<320?8+i:0,MetabolicPotassium:i*.02),
                i==1?.00625:.025)).ToArray();
            var results=batch.Advance(frame%2==0?steps:steps.Reverse().ToArray(),workers);
            for(int i=0;i<ids.Length;i++)
            {
                long soma=cells[i].SomaticSpikes,axon=cells[i].AxonalSpikes;
                bool fired=cells[i].Advance(steps[i].DurationMs,steps[i].Input,steps[i].MaximumInternalStepMs);times[i]+=steps[i].DurationMs;
                var result=results.Single(x=>x.Identity==ids[i]);
                Assert.Equal(fired,result.Metrics.Fired);Assert.Equal(cells[i].SomaticSpikes-soma,result.Metrics.SomaticSpikes);
                Assert.Equal(cells[i].AxonalSpikes-axon,result.Metrics.AxonalSpikes);
                Same(OlivaryCellSeed.Capture(ids[i],times[i],cells[i]),batch.Snapshot(ids[i]));
            }
        }
    }

    [Fact]
    public void OtherCellWorkDoesNotAdvanceUntouchedStateOrClock()
    {
        var batch=new OlivaryCpuBatch(new[]{Seed(A),Seed(B)});var untouched=batch.Snapshot(B);
        for(int frame=0;frame<128;frame++)batch.Advance(new[]{new OlivaryBatchStep(A,frame*.03125,.03125,new(DendriticCurrent:8))});
        Same(untouched,batch.Snapshot(B));Assert.Equal(4,batch.Snapshot(A).TimeMs);
    }

    [Fact]
    public void IdentitySurvivesLanePermutationAndIndependentInstanceNames()
    {
        var first=new OlivaryCpuBatch(new[]{Seed(A),Seed(B)});var second=new OlivaryCpuBatch(new[]{Seed(B),Seed(A)});
        for(int frame=0;frame<64;frame++)
        {
            var steps=new[]{new OlivaryBatchStep(A,frame*.125,.125,new(DendriticCurrent:8)),new OlivaryBatchStep(B,frame*.125,.125,new(SomaticGaba:.1))};
            Assert.Equal(first.Advance(steps,1),second.Advance(steps.Reverse().ToArray(),4));
        }
        Same(first.Snapshot(A),second.Snapshot(A));Same(first.Snapshot(B),second.Snapshot(B));
        Assert.NotEqual(first.Snapshot(A).State[0],first.Snapshot(B).State[0]);
    }

    [Fact]
    public void InvalidIntervalsAndIdentitiesRejectTheWholeRequest()
    {
        var batch=new OlivaryCpuBatch(new[]{Seed(A),Seed(B)});var a=batch.Snapshot(A);var b=batch.Snapshot(B);
        var good=new OlivaryBatchStep(A,0,.125,default);
        foreach(var bad in new[]{new OlivaryBatchStep(B,1,.125,default),new OlivaryBatchStep(B,-1,.125,default),
            new OlivaryBatchStep(B,0,double.NaN,default),new OlivaryBatchStep(B,0,.125,new(DendriticAmpa:-1)),
            new OlivaryBatchStep(new("missing","N_0"),0,.125,default),good})
        {
            Assert.ThrowsAny<ArgumentException>(()=>batch.Advance(new[]{good,bad}));Same(a,batch.Snapshot(A));Same(b,batch.Snapshot(B));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void NumericalFailureCannotPartiallyCommitOtherCells(int workers)
    {
        var batch=new OlivaryCpuBatch(new[]{Seed(A),Seed(B)});var a=batch.Snapshot(A);var b=batch.Snapshot(B);
        Assert.ThrowsAny<Exception>(()=>batch.Advance(new[]{new OlivaryBatchStep(A,0,.125,default),
            new OlivaryBatchStep(B,0,.125,new(DendriticCurrent:double.MaxValue))},workers));
        Same(a,batch.Snapshot(A));Same(b,batch.Snapshot(B));
        var clean=new OlivaryCpuBatch(new[]{a,b});var good=new[]{new OlivaryBatchStep(A,0,.125,default),new OlivaryBatchStep(B,0,.125,default)};
        Assert.Equal(clean.Advance(good),batch.Advance(good,workers));Same(clean.Snapshot(A),batch.Snapshot(A));Same(clean.Snapshot(B),batch.Snapshot(B));
    }

    [Fact]
    public void SeedsAndExportedArraysCannotMutateOwnedState()
    {
        var seed=Seed(A);var batch=new OlivaryCpuBatch(new[]{seed});var expected=batch.Snapshot(A);
        seed.State[0]=12;var exported=batch.Snapshot(A);exported.State[1]=12;
        Same(expected,batch.Snapshot(A));
        Assert.Throws<ArgumentException>(()=>new OlivaryCpuBatch(new[]{Seed(A),Seed(A)}));
        Assert.Throws<ArgumentException>(()=>new OlivaryCpuBatch(new[]{Seed(A) with{State=new double[13]}}));
        var invalid=Seed(A);invalid.State[4]=double.NaN;
        Assert.Throws<ArgumentException>(()=>new OlivaryCpuBatch(new[]{invalid}));
    }

    [Fact]
    public void MembraneSnapshotRestartsCountersLoadsAndSubFloatPrecision()
    {
        var cell=new OlivaryMembrane();bool crossingCompared=false;
        for(int i=0;i<16000;i++)
        {
            var before=crossingCompared?null:OlivaryCellSeed.Capture(A,i*.125,cell);
            var input=new OlivaryInput(DendriticCurrent:i>=8000&&i<8400?8:0);
            bool fired=cell.Advance(.125,input);
            if(fired&&!crossingCompared)
            {
                var transferred=new OlivaryCpuBatch(new[]{before!});
                var report=Assert.Single(transferred.Advance(new[]{new OlivaryBatchStep(A,i*.125,.125,input)}));
                Assert.True(report.Metrics.Fired);Assert.Equal(1,report.Metrics.AxonalSpikes);
                Same(OlivaryCellSeed.Capture(A,(i+1)*.125,cell),transferred.Snapshot(A));crossingCompared=true;
            }
        }
        Assert.True(crossingCompared);
        Assert.True(cell.AxonalSpikes>0);var seed=OlivaryCellSeed.Capture(A,2000,cell);
        var batch=new OlivaryCpuBatch(new[]{seed});Same(seed,batch.Snapshot(A));
        for(int i=0;i<64;i++)
        {
            batch.Advance(new[]{new OlivaryBatchStep(A,2000+i*.125,.125,default)});cell.Advance(.125);
            Same(OlivaryCellSeed.Capture(A,2000+(i+1)*.125,cell),batch.Snapshot(A));
        }
        var refined=batch.Snapshot(A);refined.State[3]+=Math.ScaleB(1d,-45);
        var restored=new OlivaryCpuBatch(new[]{refined});Same(refined,restored.Snapshot(A));
    }

    [Fact]
    public void ForeignThreadCannotReadOrMutateTheBatch()
    {
        var batch=new OlivaryCpuBatch(new[]{Seed(A)});Exception? error=null;
        var thread=new Thread(()=>{try{batch.Advance(new[]{new OlivaryBatchStep(A,0,.125,default)});}catch(Exception e){error=e;}});
        thread.Start();thread.Join();Assert.IsType<InvalidOperationException>(error);Same(Seed(A),batch.Snapshot(A));
    }
}
