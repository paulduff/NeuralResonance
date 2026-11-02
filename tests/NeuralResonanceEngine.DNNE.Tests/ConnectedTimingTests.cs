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
        internal Fixture(double step=.125)
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Path.Combine(Path.GetTempPath(),"nre-timing",Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE","isolated-timing");
            Engine=new(new(StructureId.PurkinjeCellLayer,"HH",PurkinjeCalciumPlasticity.Rule,"timing test",new DelayWindow(1,1)),1,step);
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

    [Fact]
    public async Task BoundaryReleaseCannotTeachDuringTheIntervalBeforeItArrives()
    {
        using var f=new Fixture();var message=Input(0);double at=Delay(f.Engine,message);
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

    [Fact]
    public async Task FractionalFeedForwardAndFeedbackArrivalsAreProcessedExactlyOnceAtTheirBoundary()
    {
        using var f=new Fixture();var observations=new List<InboundSynapticObservation>();f.Engine.InboundIntegrationObserver=observations.Add;
        foreach(var m in new[]{Input(.03125),Input(.0625,true),Input(3.71875),Input(3.125,true)})await f.Engine.EnqueueSpikeAsync(m);
        await f.Engine.ProcessTickAsync(Tick(1,20,20));
        Assert.Equal(4,observations.Count);
        foreach(var o in observations)Assert.Equal(o.ScheduledDeliveryTimestampMs,o.ProcessingTimestampMs);
        Assert.Equal(observations.OrderBy(o=>o.ProcessingTimestampMs),observations);
    }

    [Fact]
    public async Task LateTransportIsReportedAtProcessingTimeWithoutRewindingChemistry()
    {
        using var f=new Fixture();var observations=new List<InboundSynapticObservation>();f.Engine.InboundIntegrationObserver=observations.Add;
        await f.Engine.ProcessTickAsync(Tick(1,20,20));await f.Engine.EnqueueSpikeAsync(Input(0));
        await f.Engine.ProcessTickAsync(Tick(2,21,1));
        var o=Assert.Single(observations);Assert.Equal(20,o.ProcessingTimestampMs);Assert.True(o.ScheduledDeliveryTimestampMs<o.ProcessingTimestampMs);
    }

    [Fact]
    public async Task SamePhysicalGridAndEventsAreIndependentOfHostPacketDuration()
    {
        async Task<(double Voltage,double Calcium,double Weight,double[] Spikes)> Run(double packet)
        {
            using var f=new Fixture();var spikes=new List<double>();
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
