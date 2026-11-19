using System.Reflection;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class CanalConvergenceConnectionTests
{
    private const string Instance="isolated-owned-convergence-control";
    private static readonly Guid Session=Guid.Parse("d99a4e19-14a8-43dd-9fcb-33754a99f36b");
    private static readonly AvatarSkullVector Zero=new(0,0,0);
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static string DirectoryName()=>Path.Combine(Path.GetTempPath(),"nre-owned-convergence",Guid.NewGuid().ToString("N"));
    private sealed record Fibre(string Name,AvatarCanalAfferentInput Source,AvatarVestibularAfferentDynamics Cell,AvatarSkullVector Point);
    private sealed record Fixture(PhysicalSampleClock Clock,Fibre[] Fibres);
    private static AvatarSkullObservation Head(PhysicalSampleClock clock,string name,AvatarSkullVector local,long step)=>new(step,
        PhysicalSampleProtocol.Identity(clock,step),"head","sensor-"+name,local,Zero,new(1,0,0),new(0,1,0),new(0,0,1),local,Zero,Zero,new(0,-9.80665,0));
    private static Dictionary<Guid,AvatarSkullObservation> Heads(Fixture f,long step=52)=>f.Fibres.ToDictionary(x=>x.Source.Source.CellId,x=>Head(f.Clock,x.Name,x.Point,step));
    private static Fixture Create(int packet=32,bool quiet=false)
    {
        var physical=new PhysicalSampleClock(Session,"fixture-body","fixture-source",50,.125,0,1000);
        Fibre Build(string name,int sourceSteps,double x)
        {
            var clock=new AvatarCanalAfferentClock(physical,32,sourceSteps);var template=AfferentEventObserverTests.Model(clock.SourceStepMs,session:Session);
            Guid id=StableNeuralIdentity.Create("owned-convergence-control",name);var point=new AvatarSkullVector(x,0,0);
            var cell=new AvatarVestibularAfferentDynamics(Session,id,template.Kernel,template.Settings,0,0,
                [new("p",0,0,5,140)],[new("p",new(.2,.3,.4),new(.2,.3,.4,.5))],
                [new("b",-45,new(.2,.3,.4,.5,.6,.7)),new("a",-65,new(.2,.3,.4,.5,.6,.7))],clock.SourceEpochMs);
            var p=AvatarCoupledCanalParameters.PublishedRightLabyrinth1();
            var field=AvatarCupulaMechanicalBinding.EquivalentCircularField(p,"control-"+name,AvatarCupulaCanal.Horizontal,"declared control geometry",p.SourceSha256,.37,.5);
            var attachment=new AvatarCupulaBundleAttachment(id,field.OrganId,field.Canal,"declared control attachment",0,40e-6,1);
            var mechanics=new AvatarCanalBundleInput(physical,"head","sensor-"+name,point,AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1(),p,
                new(default,default),new AvatarCanalStepSchedule("control",32,new[]{4,4,8,16}),field,attachment);
            mechanics.Consume(Head(physical,name,point,50));mechanics.Consume(Head(physical,name,point,51));
            return new(name,new(mechanics,cell,new("isolated-canal-"+name,"passive-fibre-"+name,"a",quiet?100:-60),clock,packet),cell,point);
        }
        return new(physical,[Build("a",16,-.03),Build("b",32,.03)]);
    }
    private static CanalConvergenceFibre[] Inputs(Fixture f)=>f.Fibres.Select(x=>new CanalConvergenceFibre(x.Source,
        [new(StableNeuralIdentity.Create("owned-convergence-projection"),Instance,17,.25,1,8)])).ToArray();
    private static CanalConvergenceConnection Connect(Fixture f,int capacity=4096)=>new(Inputs(f),Instance,DirectoryName(),.0625,NeuralSubstrateSupply.ReferenceBath,capacity);
    private static void Advance(CanalConvergenceConnection c,Fibre fibre,int starting=0,int? count=null)
    {for(int i=starting;i<starting+(count??fibre.Source.Clock.SourceStepsPerInterval);i++)c.AdvanceNext(fibre.Source.Source.CellId,i);}
    private static string Sources(Fixture f)=>Json(f.Fibres.Select(x=>x.Cell.Capture()).ToArray());
    private static string Receiver(CanalConvergenceConnection c)
    {
        var engine=(StructureEngine)typeof(CanalConvergenceConnection).GetField("_receiver",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(c)!;
        var cells=(ModelNeuron[])typeof(StructureEngine).GetField("_neurons",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        return Json(cells.Select(n=>new{n.MembranePotentialMillivolts,n.NetSynapticCurrent,Receptors=n.CaptureCorticalReceptors()}));
    }

    [Fact]
    public void AttachmentPreservesBothActualCellsAndRestoresTheEnvironment()
    {
        var f=Create();string before=Sources(f);string? directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),instance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        using var c=Connect(f);Assert.Equal(before,Sources(f));Assert.Equal(0,c.Capture().ReceiverTicks);
        Assert.Equal(f.Fibres[0].Source.Source.EpochMs,c.Capture().ReceiverThroughMs);
        Assert.Equal(directory,Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"));Assert.Equal(instance,Environment.GetEnvironmentVariable("SERVICE_INSTANCE"));
    }

    [Fact]
    public void ActualPassiveFibresReachOneNucleusAndKeepTheirOriginalEvents()
    {
        var f=Create();using var c=Connect(f);c.ConsumeHeads(Heads(f));foreach(var fibre in f.Fibres)Advance(c,fibre);
        var frame=c.PublishCompleted();using var doc=JsonDocument.Parse(frame.CanonicalJson);var root=doc.RootElement;
        var packets=root.GetProperty("Packets").EnumerateArray().ToArray();Assert.Equal(2,packets.Length);
        var deliveries=root.GetProperty("ActualDeliveries").EnumerateArray().ToArray();Assert.Equal(2,deliveries.Length);
        foreach(var packet in packets)
        {
            var e=Assert.Single(packet.GetProperty("Events").EnumerateArray());string source=packet.GetProperty("Source").GetProperty("InstanceKey").GetString()!;
            var delivery=Assert.Single(deliveries,d=>JsonSerializer.Deserialize<SpikeMessage>(d.GetProperty("OriginalJson").GetString()!)!.SourceInstanceKey==source);
            Assert.Equal(e.GetProperty("CrossingTimeMs").GetDouble()+.25,delivery.GetProperty("AppliedAtMs").GetDouble());
            Assert.Equal(17,delivery.GetProperty("ActualTargetIndex").GetInt32());
        }
        Assert.Equal(2,deliveries.Select(d=>d.GetProperty("ReceivingContactId").GetGuid()).Distinct().Count());
        Assert.All(c.Capture().Sources,s=>{Assert.Equal(0,s.AcknowledgedSteps);Assert.NotNull(s.Actual.PendingPacket);Assert.NotNull(s.Actual.Mechanical.PendingTrace);});
        Assert.Equal(5,c.Capture().ReceiverTicks);c.Acknowledge(frame.Sha256);
        Assert.All(c.Capture().Sources,s=>{Assert.Equal(s.CompletedSteps,s.AcknowledgedSteps);Assert.Null(s.Actual.PendingPacket);Assert.Null(s.Actual.Mechanical.PendingTrace);});
    }

    [Fact]
    public void FasterSourcePublicationWaitsForTheOtherFibresKnownTime()
    {
        var f=Create();using var c=Connect(f);c.ConsumeHeads(Heads(f));Advance(c,f.Fibres[0]);
        var first=c.PublishCompleted();Assert.Equal(3,c.Capture().ReceiverTicks);Assert.Equal(1000.3125,c.Capture().ReceiverThroughMs);
        using(var doc=JsonDocument.Parse(first.CanonicalJson)){Assert.Empty(doc.RootElement.GetProperty("ActualDeliveries").EnumerateArray());Assert.Single(doc.RootElement.GetProperty("Packets").EnumerateArray());}
        c.Acknowledge(first.Sha256);Assert.Throws<InvalidOperationException>(()=>c.ConsumeHeads(Heads(f,53)));
        Advance(c,f.Fibres[1]);var second=c.PublishCompleted();Assert.Equal(5,c.Capture().ReceiverTicks);
        using(var doc=JsonDocument.Parse(second.CanonicalJson))Assert.Equal(2,doc.RootElement.GetProperty("ActualDeliveries").GetArrayLength());
        c.Acknowledge(second.Sha256);c.ConsumeHeads(Heads(f,53));
    }

    [Fact]
    public void PendingOutputStopsEverySourceAndExactRetryDoesNotIntegrateAgain()
    {
        var f=Create();using var c=Connect(f);c.ConsumeHeads(Heads(f));foreach(var fibre in f.Fibres)Advance(c,fibre);var frame=c.PublishCompleted();
        string before=Json(c.Capture());Assert.Same(frame,c.PublishCompleted());
        foreach(var fibre in f.Fibres)Assert.Throws<InvalidOperationException>(()=>c.AdvanceNext(fibre.Source.Source.CellId,fibre.Source.Clock.SourceStepsPerInterval));
        Assert.Throws<InvalidOperationException>(()=>c.ConsumeHeads(Heads(f,53)));Assert.Throws<ArgumentException>(()=>c.Acknowledge("wrong"));
        Assert.Equal(before,Json(c.Capture()));c.Acknowledge(frame.Sha256);c.Acknowledge(frame.Sha256);c.ConsumeHeads(Heads(f,53));
    }

    [Fact]
    public void QuietSourcesAdvanceTheSharedClockWithoutInventingSpikes()
    {
        var f=Create(quiet:true);using var c=Connect(f);c.ConsumeHeads(Heads(f));foreach(var fibre in f.Fibres)Advance(c,fibre);
        var frame=c.PublishCompleted();using var doc=JsonDocument.Parse(frame.CanonicalJson);
        Assert.All(doc.RootElement.GetProperty("Packets").EnumerateArray(),p=>Assert.Empty(p.GetProperty("Events").EnumerateArray()));
        Assert.Empty(doc.RootElement.GetProperty("ActualDeliveries").EnumerateArray());Assert.Equal(5,c.Capture().ReceiverTicks);
        c.Acknowledge(frame.Sha256);Assert.Throws<InvalidOperationException>(()=>c.PublishCompleted());
    }

    [Fact]
    public void ContradictoryHeadPosesAndRigidVelocitiesAreRejectedBeforeAnyMovement()
    {
        var f=Create();using var c=Connect(f);var id=f.Fibres[1].Source.Source.CellId;string before=Json(c.Capture());
        var wrong=Heads(f);wrong[id]=wrong[id] with{AngularVelocityWorldRadiansPerSecond=new(0,1,0)};
        Assert.Throws<ArgumentException>(()=>c.ConsumeHeads(wrong));Assert.Equal(before,Json(c.Capture()));
        wrong=Heads(f);wrong[id]=wrong[id] with{PointVelocityWorldMetersPerSecond=new(.1,0,0)};
        Assert.Throws<ArgumentException>(()=>c.ConsumeHeads(wrong));Assert.Equal(before,Json(c.Capture()));
        wrong=Heads(f);wrong.Remove(id);Assert.Throws<ArgumentException>(()=>c.ConsumeHeads(wrong));Assert.Equal(before,Json(c.Capture()));
        Assert.Throws<ArgumentException>(()=>c.ConsumeHeads(Heads(f,53)));Assert.Null(c.Capture().Failure);
    }

    [Fact]
    public void DifferentSensingPointsCanObserveOneRigidRotationAndRetryItsExactHeadBatch()
    {
        var f=Create();using var c=Connect(f);var heads=Heads(f);
        foreach(var id in heads.Keys.ToArray())
        {
            var s=heads[id];heads[id]=s with{AngularVelocityWorldRadiansPerSecond=new(0,1,0),PointVelocityWorldMetersPerSecond=new(0,0,-s.LocalPointMeters.X)};
        }
        var first=c.ConsumeHeads(heads);string before=Json(c.Capture());var retry=c.ConsumeHeads(heads);
        Assert.All(first.Zip(retry),pair=>Assert.Same(pair.First,pair.Second));Assert.Equal(before,Json(c.Capture()));
    }

    [Fact]
    public void PartialHeadFailureRetainsTheFirstMechanicalAdvanceAndStopsTheWholeOwner()
    {
        var f=Create();using var c=Connect(f);var heads=Heads(f);var id=f.Fibres[1].Source.Source.CellId;
        heads[id]=heads[id] with{PointWorldMeters=new(3,0,0)};
        Assert.Throws<ArgumentException>(()=>c.ConsumeHeads(heads));var status=c.Capture();Assert.NotNull(status.Failure);
        Assert.NotNull(status.Sources[0].Actual.Mechanical.PendingTrace);Assert.Null(status.Sources[1].Actual.Mechanical.PendingTrace);
        Assert.All(status.Sources,s=>Assert.Equal(0,s.CompletedSteps));Assert.Throws<InvalidOperationException>(()=>c.ConsumeHeads(Heads(f)));
        Assert.Throws<InvalidOperationException>(()=>c.PublishCompleted());
    }

    [Fact]
    public void PartialAdmissionFailureRetainsEveryPacketAndTheAcceptedPrefix()
    {
        var f=Create();using var c=Connect(f,capacity:1);c.ConsumeHeads(Heads(f));foreach(var fibre in f.Fibres)Advance(c,fibre);
        Assert.Throws<StructureIngressOverloadException>(()=>c.PublishCompleted());var status=c.Capture();Assert.NotNull(status.Failure);
        Assert.Equal(0,status.ReceiverTicks);Assert.All(status.Sources,s=>{Assert.NotNull(s.Actual.PendingPacket);Assert.Equal(0,s.AcknowledgedSteps);});
        using var doc=JsonDocument.Parse(status.AttemptJson);Assert.Equal(2,doc.RootElement.GetProperty("Packets").GetArrayLength());
        Assert.Single(doc.RootElement.GetProperty("Admissions").EnumerateArray());Assert.Throws<InvalidOperationException>(()=>c.Acknowledge("anything"));
    }

    [Fact]
    public void ExternalSourceAdvancementCannotBypassTheSharedOwner()
    {
        var f=Create();using var c=Connect(f);c.ConsumeHeads(Heads(f));f.Fibres[0].Source.AdvanceNext(0);
        Assert.Throws<InvalidOperationException>(()=>c.PublishCompleted());Assert.Equal(0,c.Capture().ReceiverTicks);
        Assert.Equal(0,f.Fibres[1].Cell.Capture().CompletedSteps);
    }

    [Fact]
    public void PacketPartitionAndSourceInterleavingPreserveActualCellAndReceivingResults()
    {
        (string Cells,string Received,string Delivered,long Ticks) Run(bool partition)
        {
            var f=Create(packet:partition?2:32);using var c=Connect(f);c.ConsumeHeads(Heads(f));var deliveries=new List<string>();
            void Publish(){var frame=c.PublishCompleted();using var doc=JsonDocument.Parse(frame.CanonicalJson);deliveries.AddRange(doc.RootElement.GetProperty("ActualDeliveries").EnumerateArray().Select(d=>d.GetRawText()));c.Acknowledge(frame.Sha256);}
            if(partition)
            {
                for(int i=0;i<16;i++){Advance(c,f.Fibres[1],2*i,2);Advance(c,f.Fibres[0],i,1);Publish();}
            }
            else{foreach(var fibre in f.Fibres)Advance(c,fibre);Publish();}
            return(Sources(f),Receiver(c),Json(deliveries),c.Capture().ReceiverTicks);
        }
        Assert.Equal(Run(false),Run(true));
    }

    [Fact]
    public void ExistingSavesAdvancedSourcesAndStaleAcknowledgementsCannotBeReused()
    {
        var f=Create();string folder=DirectoryName();Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"original.txt"),"original");
        Assert.Throws<InvalidOperationException>(()=>new CanalConvergenceConnection(Inputs(f),Instance,folder,.0625,NeuralSubstrateSupply.ReferenceBath));
        Assert.Equal("original",File.ReadAllText(Path.Combine(folder,"original.txt")));
        using var c=Connect(f);c.ConsumeHeads(Heads(f));Advance(c,f.Fibres[0]);var first=c.PublishCompleted();c.Acknowledge(first.Sha256);
        Assert.Throws<ArgumentException>(()=>Connect(f));Advance(c,f.Fibres[1]);var second=c.PublishCompleted();
        Assert.Throws<ArgumentException>(()=>c.Acknowledge(first.Sha256));Assert.Same(second,c.Capture().Pending);c.Acknowledge(second.Sha256);
    }
}
