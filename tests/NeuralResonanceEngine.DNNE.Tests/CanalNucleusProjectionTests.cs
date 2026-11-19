using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class CanalNucleusProjectionTests
{
    private const string Instance=VestibularProjectionOutboxTests.Instance;
    private sealed class Fixture : IDisposable
    {
        internal readonly PhysicalSampleClock Clock;
        internal readonly AvatarCanalAfferentInput[] Sources;
        internal readonly CanalConvergenceConnection Source;
        internal Fixture()
        {
            Guid session=Guid.NewGuid();Clock=new(session,"fixture-body","fixture-source",50,.125,0,1000);
            AvatarCanalAfferentInput Build(int index)
            {
                Guid id=StableNeuralIdentity.Create("projection-passive-cell",index);var clock=new AvatarCanalAfferentClock(Clock,32,16);
                var template=AfferentEventObserverTests.Model(clock.SourceStepMs,session:session);
                var cell=new AvatarVestibularAfferentDynamics(session,id,template.Kernel,template.Settings,0,0,
                    [new("p",0,0,5,140)],[new("p",new(.2,.3,.4),new(.2,.3,.4,.5))],
                    [new("b",-45,new(.2,.3,.4,.5,.6,.7)),new("a",-65,new(.2,.3,.4,.5,.6,.7))],clock.SourceEpochMs);
                var p=AvatarCoupledCanalParameters.PublishedRightLabyrinth1();
                var field=AvatarCupulaMechanicalBinding.EquivalentCircularField(p,"control-"+index,AvatarCupulaCanal.Horizontal,"declared control geometry",p.SourceSha256,.37,.5);
                var attachment=new AvatarCupulaBundleAttachment(id,field.OrganId,field.Canal,"declared control attachment",0,40e-6,1);
                var mechanics=new AvatarCanalBundleInput(Clock,"head","sensor-"+index,new(0,0,0),AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1(),p,
                    new(default,default),new AvatarCanalStepSchedule("control",32,new[]{4,4,8,16}),field,attachment);
                mechanics.Consume(Head(index,50));mechanics.Consume(Head(index,51));
                return new(mechanics,cell,new("isolated-passive-"+index,"fibre-"+index,"a",100),clock,32);
            }
            Sources=[Build(0),Build(1)];
            Source=new(Sources.Select(s=>new CanalConvergenceFibre(s,[new(StableNeuralIdentity.Create("projection-control-contact"),Instance,17,.25,1,8)])),
                Instance,Path.Combine(Path.GetTempPath(),"nre-projection-owner",Guid.NewGuid().ToString("N")),.0625,NeuralSubstrateSupply.ReferenceBath);
        }
        private AvatarSkullObservation Head(int index,long step)=>new(step,PhysicalSampleProtocol.Identity(Clock,step),"head","sensor-"+index,
            new(0,0,0),new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1),new(0,0,0),new(0,0,0),new(0,0,0),new(0,-9.80665,0));
        internal CanalNucleusProjection Connect(double dt=.0625)
        {var routes=VestibularProjectionOutboxTests.Routes();return new(Source,Instance,"L",dt,routes,VestibularProjectionOutboxTests.Recipients(routes));}
        internal void Advance()
        {
            Source.ConsumeHeads(Sources.Select((s,i)=>(s,i)).ToDictionary(p=>p.s.Source.CellId,p=>Head(p.i,52)));
            foreach(var source in Sources)for(int i=0;i<16;i++)Source.AdvanceNext(source.Source.CellId,i);
        }
        public void Dispose()=>Source.Dispose();
    }

    [Fact]
    public void ActualSharedOwnerRetainsEverySourceUntilAllOutgoingRecipientsAcknowledge()
    {
        using var f=new Fixture();var projection=f.Connect();f.Advance();var frame=projection.PublishCompleted();
        var original=f.Source.Capture().Pending!;Assert.Equal(original.Sha256,frame.SourceFrameSha256);Assert.Equal(7,frame.Packets.Count);
        Assert.Same(frame,projection.PublishCompleted());Assert.All(f.Sources,s=>Assert.Equal(0,s.Capture().AcknowledgedSteps));
        Assert.Throws<InvalidOperationException>(()=>projection.ReleaseSource(frame.Sha256));
        foreach(var packet in frame.Packets.Take(6))projection.Accept(VestibularProjectionOutboxTests.Receipt(packet));
        Assert.Throws<InvalidOperationException>(()=>projection.ReleaseSource(frame.Sha256));Assert.Same(original,f.Source.Capture().Pending);
        projection.Accept(VestibularProjectionOutboxTests.Receipt(frame.Packets[6]));projection.ReleaseSource(frame.Sha256);projection.ReleaseSource(frame.Sha256);
        Assert.Null(f.Source.Capture().Pending);Assert.All(f.Sources,s=>Assert.Equal(16,s.Capture().AcknowledgedSteps));
        Assert.Equal(5,projection.Capture().Outgoing.ReleasedTick);Assert.Null(projection.Capture().Failure);
    }

    [Fact]
    public void ExternalSourceAcknowledgementCannotBeHiddenByOutgoingReceipts()
    {
        using var f=new Fixture();var projection=f.Connect();f.Advance();var frame=projection.PublishCompleted();
        f.Source.Acknowledge(f.Source.Capture().Pending!.Sha256);
        Assert.Throws<InvalidOperationException>(()=>projection.Accept(VestibularProjectionOutboxTests.Receipt(frame.Packets[0])));
        Assert.Throws<InvalidOperationException>(()=>projection.ReleaseSource(frame.Sha256));
        Assert.Same(frame,projection.Capture().Outgoing.Pending);Assert.Empty(projection.Capture().Outgoing.Receipts);
    }

    [Fact]
    public void BadDeclaredReceivingClockRetainsActualSourceOutputAndFaultsWithoutReplay()
    {
        using var f=new Fixture();var projection=f.Connect(.125);f.Advance();
        Assert.Throws<ArgumentException>(()=>projection.PublishCompleted());var snapshot=projection.Capture();
        Assert.NotNull(snapshot.Failure);Assert.NotNull(snapshot.AttemptedOriginalFrameJson);Assert.NotNull(f.Source.Capture().Pending);
        Assert.Throws<InvalidOperationException>(()=>projection.PublishCompleted());
        Assert.Equal(JsonSerializer.Serialize(snapshot),JsonSerializer.Serialize(projection.Capture()));
        Assert.All(f.Sources,s=>Assert.Equal(0,s.Capture().AcknowledgedSteps));Assert.Throws<ArgumentException>(()=>f.Connect());
    }
}
