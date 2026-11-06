using System.Reflection;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class ConnectedTimingTests
{
    static Dictionary<Guid,SynapseState> Contacts(StructureEngine e)=>(Dictionary<Guid,SynapseState>)typeof(StructureEngine).GetField("_inboundSynapses",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(e)!;
    sealed class Fixture : IDisposable
    {
        readonly string? oldDir=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),oldInstance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        internal readonly StructureEngine Engine;
        internal Fixture(double step=.125,double[]? boundaries=null,bool cellLocal=false,int cells=1)
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Path.Combine(Path.GetTempPath(),"nre-timing",Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE","isolated-timing");
            try {Engine=new(new(StructureId.PurkinjeCellLayer,"HH",PurkinjeCalciumPlasticity.Rule,"timing test",new DelayWindow(1,1)),cells,step,boundaries,cellLocal);}
            catch {Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",oldDir);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",oldInstance);throw;}
        }
        public void Dispose(){Engine.Dispose();Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",oldDir);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",oldInstance);}
    }
    static SpikeMessage Input(double time,bool feedback=false)=>new()
    {
        MessageId=Guid.NewGuid(),TimestampMs=time,SourceInstanceKey="isolated-source",SourceNeuronId="n-000",TargetNeuronId="auto-000",
        ProjectionId=StableNeuralIdentity.Create("timing-test",feedback),SynapseId=StableNeuralIdentity.Create("timing-test",feedback),
        SourceStructure=StructureId.CerebellarGranule,TargetStructure=StructureId.PurkinjeCellLayer,
        Neurotransmitter=NTEnum.GLUTAMATE,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL,VesicleQuanta=1,ReuptakeRate=8,IsFeedback=feedback
    };
    static TickSignal Tick(long n,double time,double dt)=>new(n,time,dt,new NeuromodState(),new Dictionary<BrainRhythm,double>(),0){NeuralSubstrates=NeuralSubstrateSupply.ReferenceBath};
    static int Delay(StructureEngine e,SpikeMessage m)=>(int)typeof(StructureEngine).GetMethod("ComputeConductionDelayMs",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,[m])!;

    // Inspect scalar state of the cell and its electrical/support mechanisms.
    // This is an equality diagnostic, not a continuation/checkpoint serializer.
    static string CellState(ModelNeuron cell)
    {
        var state=new SortedDictionary<string,object?>();
        void Scalars(object value,string prefix)
        {
            foreach(var f in value.GetType().GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))
                if(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string))state[prefix+f.Name]=f.GetValue(value);
        }
        Scalars(cell,"cell.");
        foreach(var name in new[]{"_hh","_purkinjeClimbing","_microtubules","_energy","_localNeuromodState"})
            if(typeof(ModelNeuron).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(cell) is {} value)Scalars(value,name+".");
        return System.Text.Json.JsonSerializer.Serialize(state);
    }

    static SpikeMessage InputForCell(StructureEngine engine,int target,double time,bool feedback=false)
    {
        var method=typeof(StructureEngine).GetMethod("ResolveInboundNeuronIndex",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var message=Input(time,feedback);
        for(int i=0;i<4096;i++)
        {
            message.SourceNeuronId=$"n-{i:000}";
            if((int)method.Invoke(engine,[message])! ==target)return message;
        }
        throw new InvalidOperationException("Could not address diagnostic cell.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CellLocalArrivalsLeaveTheOtherCellUnchanged(bool feedback)
    {
        async Task<(string[] States,double[][] Spikes,int Releases)> Run(bool connected)
        {
            using var f=new Fixture(cellLocal:true,cells:2);
            int releases=0;f.Engine.InboundIntegrationObserver=_=>releases++;
            foreach(var time in new[]{.03125,10.71875,20.0625,30.03125})
            {
                if(connected)await f.Engine.EnqueueSpikeAsync(InputForCell(f.Engine,0,time,feedback));
                await f.Engine.EnqueueSpikeAsync(InputForCell(f.Engine,1,Math.Floor(time)));
            }
            var spikes=new[]{new List<double>(),new List<double>()};
            for(int n=1;n<=100;n++)
            {
                await f.Engine.ProcessTickAsync(Tick(n,n,1));
                foreach(var m in await f.Engine.DrainOutboundSpikesAsync())spikes[int.Parse(m.SourceNeuronId[2..])].Add(m.TimestampMs);
            }
            return(BiologicalContactLearningTests.Cells(f.Engine).Select(CellState).ToArray(),spikes.Select(x=>x.ToArray()).ToArray(),releases);
        }
        var a=await Run(true);var b=await Run(false);
        Assert.Equal(8,a.Releases);Assert.Equal(4,b.Releases);
        Assert.Equal(a.States[1],b.States[1]);Assert.Equal(a.Spikes[1],b.Spikes[1]);
        Assert.NotEqual(a.States[0],b.States[0]); // A real input must not be silently discarded.
    }

    [Fact]
    public async Task CellLocalNoOpMarkersChangeNeitherCellNorContactChemistry()
    {
        async Task<(string Cells,string Contacts,(string,double)[] Spikes,int Markers)> Run(bool markers)
        {
            using var f=new Fixture(cellLocal:true,cells:2,boundaries:markers?[0,.03125,1.0625,5.71875,20.03125,100]:null);
            int seen=0;f.Engine.DiagnosticBoundaryObserver=(a,b)=>{Assert.Equal(a,b);seen++;};
            foreach(int cell in new[]{0,1})foreach(var t in new[]{0d,10.0625,20.71875})await f.Engine.EnqueueSpikeAsync(InputForCell(f.Engine,cell,t));
            var spikes=new List<(string,double)>();
            for(int n=1;n<=100;n++)
            {
                await f.Engine.ProcessTickAsync(Tick(n,n,1));
                spikes.AddRange((await f.Engine.DrainOutboundSpikesAsync()).Select(m=>(m.SourceNeuronId,m.TimestampMs)));
            }
            return(string.Join('|',BiologicalContactLearningTests.Cells(f.Engine).Select(CellState)),
                System.Text.Json.JsonSerializer.Serialize(Contacts(f.Engine).Values.OrderBy(x=>x.SynapseId).Select(ContactLearningSnapshot.From)),spikes.ToArray(),seen);
        }
        var a=await Run(true);var b=await Run(false);
        Assert.Equal(6,a.Markers);Assert.Equal(0,b.Markers);
        Assert.Equal(a.Cells,b.Cells);Assert.Equal(a.Contacts,b.Contacts);Assert.Equal(a.Spikes,b.Spikes);
    }

    [Fact]
    public void CellLocalCandidateCannotBeEnabledForAServiceHostAndHasADistinctFingerprint()
    {
        Assert.Throws<ArgumentException>(()=>new StructureEngine(new(StructureId.PurkinjeCellLayer,"HH",PurkinjeCalciumPlasticity.Rule,"service",new DelayWindow(1,1)),null,diagnosticCellLocalIntegration:true));
        string Fingerprint(bool local)
        {
            using var f=new Fixture(cellLocal:local);
            var store=typeof(StructureEngine).GetField("_synapseStore",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Engine)!;
            return (string)store.GetType().GetField("_modelFingerprint",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(store)!;
        }
        Assert.NotEqual(Fingerprint(false),Fingerprint(true));
    }

    [Fact]
    public async Task DiagnosticBoundariesOnlySplitTimeAndCopyTheirSchedule()
    {
        double[] boundaries=[0,.03125,.5,1];using var f=new Fixture(boundaries:boundaries);
        boundaries[1]=.75;
        var seen=new List<(double Scheduled,double Processed)>();int releases=0;
        f.Engine.DiagnosticBoundaryObserver=(a,b)=>seen.Add((a,b));
        f.Engine.InboundIntegrationObserver=_=>releases++;
        await f.Engine.ProcessTickAsync(Tick(1,1,1));
        await f.Engine.ProcessTickAsync(Tick(2,2,1));
        Assert.Equal(new[]{(0d,0d),(.03125,.03125),(.5,.5),(1d,1d)},seen);
        Assert.Equal(0,releases);Assert.Empty(Contacts(f.Engine));
    }

    [Fact]
    public void DiagnosticBoundariesRejectMalformedSchedulesAndNonisolatedHosts()
    {
        foreach(var values in new[]{new[]{double.NaN},new[]{double.PositiveInfinity},new[]{-.1},new[]{1d,.5},new[]{1d,1d}})
            Assert.Throws<ArgumentException>(()=>new Fixture(boundaries:values));
        Assert.Throws<ArgumentException>(()=>new StructureEngine(new(StructureId.PurkinjeCellLayer,"HH",PurkinjeCalciumPlasticity.Rule,"not isolated",new DelayWindow(1,1)),null,null,[.03125]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundaryReleaseCannotTeachDuringTheIntervalBeforeItArrives(bool cellLocal)
    {
        using var f=new Fixture(cellLocal:cellLocal);var message=Input(0);double at=Delay(f.Engine,message);
        await f.Engine.EnqueueSpikeAsync(message);
        await f.Engine.ProcessTickAsync(Tick(1,at,at));
        var contact=Contacts(f.Engine).Single().Value;
        Assert.Equal(at,contact.LastUpdateTimestampMs);
        Assert.Equal(.05,contact.PurkinjeCalcium!.CalciumUm);
        Assert.Equal(0,contact.PurkinjeCalcium.EntryUm);
        Assert.Equal(0,contact.PurkinjeCalcium.InducedDepression);
        Assert.Equal(1,contact.VesicleQuanta);
        await f.Engine.ProcessTickAsync(Tick(2,at+.125,.125));
        Assert.True(contact.PurkinjeCalcium.EntryUm>0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FractionalFeedForwardAndFeedbackArrivalsAreProcessedExactlyOnceAtTheirBoundary(bool cellLocal)
    {
        using var f=new Fixture(cellLocal:cellLocal);var observations=new List<InboundSynapticObservation>();f.Engine.InboundIntegrationObserver=observations.Add;
        foreach(var m in new[]{Input(.03125),Input(.0625,true),Input(3.71875),Input(3.125,true)})await f.Engine.EnqueueSpikeAsync(m);
        await f.Engine.ProcessTickAsync(Tick(1,20,20));
        Assert.Equal(4,observations.Count);
        foreach(var o in observations)Assert.Equal(o.ScheduledDeliveryTimestampMs,o.ProcessingTimestampMs);
        Assert.Equal(observations.OrderBy(o=>o.ProcessingTimestampMs),observations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateTransportIsReportedAtProcessingTimeWithoutRewindingChemistry(bool cellLocal)
    {
        using var f=new Fixture(cellLocal:cellLocal);var observations=new List<InboundSynapticObservation>();f.Engine.InboundIntegrationObserver=observations.Add;
        await f.Engine.ProcessTickAsync(Tick(1,20,20));await f.Engine.EnqueueSpikeAsync(Input(0));
        await f.Engine.ProcessTickAsync(Tick(2,21,1));
        var o=Assert.Single(observations);Assert.Equal(20,o.ProcessingTimestampMs);Assert.True(o.ScheduledDeliveryTimestampMs<o.ProcessingTimestampMs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SamePhysicalGridAndEventsAreIndependentOfHostPacketDuration(bool cellLocal)
    {
        async Task<(double Voltage,double Calcium,double Weight,double[] Spikes)> Run(double packet)
        {
            using var f=new Fixture(cellLocal:cellLocal);var spikes=new List<double>();
            foreach(var t in new[]{.03125,10.71875,20.125,30.0625})await f.Engine.EnqueueSpikeAsync(Input(t));
            for(int n=1;n<=100/packet;n++)
            {
                await f.Engine.ProcessTickAsync(Tick(n,n*packet,packet));
                spikes.AddRange((await f.Engine.DrainOutboundSpikesAsync()).Select(m=>m.TimestampMs));
            }
            var ca=Contacts(f.Engine).Single().Value.PurkinjeCalcium!;
            return(BiologicalContactLearningTests.Cells(f.Engine)[0].LearningDendriteVoltageMv,ca.CalciumUm,ca.Weight,spikes.ToArray());
        }
        var a=await Run(1);var b=await Run(.125);
        Assert.Equal(a.Voltage,b.Voltage);Assert.Equal(a.Calcium,b.Calcium);Assert.Equal(a.Weight,b.Weight);Assert.Equal(a.Spikes,b.Spikes);
    }

    [Fact]
    public void ChemicalVoltageRampUsesTheWholeIntervalAndComposesAcrossPartitions()
    {
        var a=new PurkinjeCalciumPlasticity(1,-68);var b=new PurkinjeCalciumPlasticity(1,-68);var held=new PurkinjeCalciumPlasticity(1,-68);
        a.Advance(1,-68,-20);held.Advance(1,-20);
        for(int i=0;i<8;i++)b.Advance(.125,-68+48*i/8d,-68+48*(i+1)/8d);
        Assert.Equal(a.EntryUm,b.EntryUm);Assert.Equal(a.CalciumUm,b.CalciumUm);
        Assert.True(a.EntryUm<held.EntryUm);
    }
}
