using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class CanalNucleusConnectionTests
{
    private const string Instance="isolated-canal-nucleus-control";
    private static readonly Guid Session=Guid.Parse("113f4096-5399-4c20-9ef4-8a4367c2a8c8");
    private static readonly AvatarSkullVector Zero=new(0,0,0);
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static ObservedAfferentRoute Route(int target=17)=>new(StableNeuralIdentity.Create("canal-nucleus-control",target),Instance,target,.25,1,8);
    private static string StateDirectory()=>Path.Combine(Path.GetTempPath(),"nre-canal-nucleus",Guid.NewGuid().ToString("N"));
    private sealed record Fixture(AvatarCanalAfferentInput Source,AvatarVestibularAfferentDynamics Cell,PhysicalSampleClock Physical);
    private static AvatarSkullObservation Head(Fixture f,long step=52)=>new(step,PhysicalSampleProtocol.Identity(f.Physical,step),
        "head","centre",Zero,Zero,new(1,0,0),new(0,1,0),new(0,0,1),Zero,Zero,Zero,new(0,-9.80665,0));
    private static Fixture Create(int packet=16,double threshold=-60)
    {
        var physical=new PhysicalSampleClock(Session,"fixture-body","fixture-source",50,.125,0,1000);
        var clock=new AvatarCanalAfferentClock(physical,32,16);
        // Existing analytic passive two-capacitor control, at the explicitly
        // declared physical epoch. This crossing is not physiological firing.
        var template=AfferentEventObserverTests.Model(clock.SourceStepMs,session:Session);var s=template.Capture();
        var cell=new AvatarVestibularAfferentDynamics(Session,s.CellId,template.Kernel,s.Settings,0,0,
            [new("p",0,0,5,140)],[new("p",new(.2,.3,.4),new(.2,.3,.4,.5))],
            [new("b",-45,new(.2,.3,.4,.5,.6,.7)),new("a",-65,new(.2,.3,.4,.5,.6,.7))],clock.SourceEpochMs);
        var p=AvatarCoupledCanalParameters.PublishedRightLabyrinth1();
        var field=AvatarCupulaMechanicalBinding.EquivalentCircularField(p,"control-organ",AvatarCupulaCanal.Horizontal,
            "declared control geometry",p.SourceSha256,.37,.5);
        var attachment=new AvatarCupulaBundleAttachment(s.CellId,field.OrganId,field.Canal,"control attachment",0,40e-6,1);
        var mechanics=new AvatarCanalBundleInput(physical,"head","centre",Zero,AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1(),p,
            new(default,default),new AvatarCanalStepSchedule("control",32,new[]{4,4,8,16}),field,attachment);
        var placeholder=new Fixture(null!,cell,physical);
        mechanics.Consume(Head(placeholder,50));mechanics.Consume(Head(placeholder,51));
        return new(new(mechanics,cell,new("isolated-canal-source","actual-passive-cell","a",threshold),clock,packet),cell,physical);
    }
    private static CanalAfferentNucleusConnection Connect(Fixture f,IEnumerable<ObservedAfferentRoute>? routes=null,int capacity=4096)
        =>new(f.Source,Instance,StateDirectory(),routes??[Route()],.125,NeuralSubstrateSupply.ReferenceBath,capacity);
    private static void Advance(CanalAfferentNucleusConnection connection,int start=0,int count=16)
    {for(int i=start;i<start+count;i++)connection.AdvanceNext(i);}

    [Fact]
    public void AttachmentPreservesActualSourceStateAndRestoresProcessEnvironment()
    {
        var f=Create();string before=Json(f.Source.Capture());
        string? directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),instance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        using var connection=Connect(f);
        Assert.Equal(before,Json(f.Source.Capture()));Assert.Equal(0,connection.Capture().ReceiverTicks);
        Assert.Equal(f.Source.Source.EpochMs,connection.Capture().ReceiverThroughMs);
        Assert.Equal(directory,Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"));Assert.Equal(instance,Environment.GetEnvironmentVariable("SERVICE_INSTANCE"));
    }

    [Fact]
    public void ExistingStateAndAlreadyAdvancedSourceCannotBeSilentlyReused()
    {
        var f=Create();string directory=StateDirectory();Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"preserved.txt"),"original");
        Assert.Throws<InvalidOperationException>(()=>new CanalAfferentNucleusConnection(f.Source,Instance,directory,[Route()],.125,NeuralSubstrateSupply.ReferenceBath));
        Assert.Equal("original",File.ReadAllText(Path.Combine(directory,"preserved.txt")));
        f.Source.ConsumeHead(Head(f));f.Source.AdvanceNext(0);
        Assert.Throws<ArgumentException>(()=>Connect(f));Assert.Equal(1,f.Cell.Capture().CompletedSteps);
    }

    [Fact]
    public void OriginalPassiveCrossingReachesActualContactBeforeSourceAcknowledgement()
    {
        var f=Create();using var c=Connect(f);c.ConsumeHead(Head(f));Advance(c);
        var frame=c.PublishCompleted();using var doc=JsonDocument.Parse(frame.CanonicalJson);var root=doc.RootElement;
        var e=Assert.Single(root.GetProperty("Packet").GetProperty("Events").EnumerateArray());
        var delivery=Assert.Single(root.GetProperty("ActualDeliveries").EnumerateArray());
        Assert.Equal(17,delivery.GetProperty("ActualTargetIndex").GetInt32());
        Assert.Equal(e.GetProperty("CrossingTimeMs").GetDouble()+.25,delivery.GetProperty("AppliedAtMs").GetDouble());
        var evidence=Assert.Single(root.GetProperty("Admission").GetProperty("Deliveries").EnumerateArray());
        Assert.Equal(e.GetProperty("MessageId").GetGuid(),evidence.GetProperty("OriginalEventId").GetGuid());
        Assert.Equal(evidence.GetProperty("ContactId").GetGuid(),delivery.GetProperty("ReceivingContactId").GetGuid());
        Assert.Equal(0,f.Source.Capture().AcknowledgedSteps);Assert.NotNull(f.Source.Capture().Mechanical.PendingTrace);
        Assert.Equal(3,c.Capture().ReceiverTicks);Assert.Equal(1000.5,c.Capture().ReceiverThroughMs);
        c.Acknowledge(frame.Sha256);Assert.Equal(16,f.Source.Capture().AcknowledgedSteps);Assert.Null(f.Source.Capture().Mechanical.PendingTrace);
    }

    [Fact]
    public void PendingFrameStopsBothSourceAndBodyAndExactRetriesDoNotIntegrateTwice()
    {
        var f=Create();using var c=Connect(f);c.ConsumeHead(Head(f));Advance(c);var frame=c.PublishCompleted();
        string source=Json(f.Source.Capture()),receiver=Json(c.Capture());
        Assert.Same(frame,c.PublishCompleted());Assert.Throws<InvalidOperationException>(()=>c.AdvanceNext(16));
        Assert.Throws<InvalidOperationException>(()=>c.ConsumeHead(Head(f,53)));
        Assert.Throws<ArgumentException>(()=>c.Acknowledge("wrong"));
        Assert.Equal(source,Json(f.Source.Capture()));Assert.Equal(receiver,Json(c.Capture()));
        c.Acknowledge(frame.Sha256);c.Acknowledge(frame.Sha256);c.ConsumeHead(Head(f,53));c.AdvanceNext(16);
    }

    [Fact]
    public void QuietPacketsAdvanceOnlyTheKnownConductionHorizon()
    {
        var f=Create(packet:1,threshold:100);using var c=Connect(f);c.ConsumeHead(Head(f));c.AdvanceNext(0);
        var frame=c.PublishCompleted();using var doc=JsonDocument.Parse(frame.CanonicalJson);var root=doc.RootElement;
        Assert.Empty(root.GetProperty("Packet").GetProperty("Events").EnumerateArray());
        Assert.Empty(root.GetProperty("ActualDeliveries").EnumerateArray());
        double horizon=root.GetProperty("Admission").GetProperty("TargetSafeThroughMs").GetDouble();
        var status=c.Capture();Assert.True(status.ReceiverThroughMs<=horizon);Assert.True(status.ReceiverThroughMs+.125>horizon);
        c.Acknowledge(frame.Sha256);Assert.Throws<InvalidOperationException>(()=>c.PublishCompleted());
        Assert.Equal(status.ReceiverThroughMs,c.Capture().ReceiverThroughMs);
    }

    [Fact]
    public void PacketPartitionDoesNotMoveReceivingTicksOrOriginalDeliveries()
    {
        (string Source,long Ticks,double Through,string Deliveries,string Output) Run(int partition)
        {
            var f=Create(packet:partition);using var c=Connect(f);c.ConsumeHead(Head(f));
            var deliveries=new List<string>();var output=new List<string>();
            for(int i=0;i<16;i++)
            {
                c.AdvanceNext(i);if((i+1)%partition!=0)continue;
                var frame=c.PublishCompleted();using var doc=JsonDocument.Parse(frame.CanonicalJson);
                deliveries.AddRange(doc.RootElement.GetProperty("ActualDeliveries").EnumerateArray().Select(x=>x.GetRawText()));
                foreach(var step in doc.RootElement.GetProperty("ReceiverStepJson").EnumerateArray())
                {
                    using var result=JsonDocument.Parse(step.GetString()!);
                    output.AddRange(result.RootElement.GetProperty("OutboundSpikes").EnumerateArray().Select(x=>x.GetRawText()));
                }
                c.Acknowledge(frame.Sha256);
            }
            var status=c.Capture();return(Json(f.Cell.Capture()),status.ReceiverTicks,status.ReceiverThroughMs,Json(deliveries),Json(output));
        }
        Assert.Equal(Run(16),Run(1));
    }

    [Fact]
    public void OverloadedReceiverRetainsPacketAndDoesNotAcknowledgeSource()
    {
        var f=Create();using var c=Connect(f,[Route(),Route(18)],1);c.ConsumeHead(Head(f));Advance(c);
        Assert.Throws<StructureIngressOverloadException>(()=>c.PublishCompleted());
        Assert.NotNull(c.Capture().Failure);Assert.Equal(0,c.Capture().ReceiverTicks);
        Assert.Equal(0,f.Source.Capture().AcknowledgedSteps);Assert.Single(f.Source.Capture().PendingPacket!.Events);
        Assert.Throws<InvalidOperationException>(()=>c.PublishCompleted());Assert.Throws<InvalidOperationException>(()=>c.Acknowledge("anything"));
    }

    [Fact]
    public void ExternalSensoryAdvancementCannotEnterTheReceivingClock()
    {
        var f=Create();using var c=Connect(f);c.ConsumeHead(Head(f));f.Source.AdvanceNext(0);
        Assert.Throws<InvalidOperationException>(()=>c.PublishCompleted());Assert.Equal(0,c.Capture().ReceiverTicks);
        Assert.Equal(0,f.Source.Capture().AcknowledgedSteps);
    }

    [Fact]
    public void AStaleAcknowledgementCannotReleaseANewerFrame()
    {
        var f=Create(packet:1,threshold:100);using var c=Connect(f);c.ConsumeHead(Head(f));c.AdvanceNext(0);
        var first=c.PublishCompleted();c.Acknowledge(first.Sha256);c.AdvanceNext(1);var second=c.PublishCompleted();
        Assert.Throws<ArgumentException>(()=>c.Acknowledge(first.Sha256));Assert.Same(second,c.Capture().Pending);
        Assert.Equal(1,f.Source.Capture().AcknowledgedSteps);c.Acknowledge(second.Sha256);
    }

    [Fact]
    public void RejectedHeadGapPreservesSourceAndDoesNotFaultTheConnection()
    {
        var f=Create();using var c=Connect(f);string before=Json(f.Source.Capture());
        Assert.Throws<ArgumentException>(()=>c.ConsumeHead(Head(f,53)));Assert.Equal(before,Json(f.Source.Capture()));
        Assert.Null(c.Capture().Failure);c.ConsumeHead(Head(f));c.AdvanceNext(0);
    }
}
