using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Source completeness is prescribed here; the receiving motor cells actually
// evolve. These controls establish shared ownership, not neural recruitment,
// muscle response, physiological calibration or standing.
public sealed class SharedMotorAnatomyTests
{
    private static readonly Guid Session=Guid.Parse("6e219e36-3d50-481d-8ccb-9d055e85f214");
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private sealed class Pool
    {
        internal readonly DescendingMotorPool Owner;
        internal readonly DescendingMotorCell[] Cells;
        internal readonly VestibularProjectionOutbox Input;
        internal readonly MotorAxonContact[] Contacts;
        internal Pool(string side,int count=1)
        {
            string source="isolated-shared-descending-"+side,instance="isolated-shared-motor-pool-"+side;
            var route=new SynapticConnection(StructureId.SpinalCordMotor,StableNeuralIdentity.Create("shared-motor-input",side),NTEnum.GLUTAMATE,"prescribed-ownership-control");
            string hemisphere=side.ToUpperInvariant();
            Input=new(source,hemisphere,0,.125,[route],[new(StructureId.SpinalCordMotor,instance,hemisphere,new Uri("http://127.0.0.1:1"))],StructureId.ReticularFormation);
            Cells=Enumerable.Range(1,count).Select(i=>
            {
                string key=side+"-"+i.ToString("D3");
                var identity=new ReceptorMotorIdentity("isolated-shared-motor-"+key,"alpha-"+key,
                    StableNeuralIdentity.Create("shared-motor-axon",key),StableNeuralIdentity.Create("shared-motor-synapse",key));
                var receptor=new MotorReceptorDefinition(source,StructureId.ReticularFormation,"descending-"+key,
                    StableNeuralIdentity.Create("shared-motor-receptor",key),route.SynapseId,identity.Instance,identity.Neuron,i,
                    "declared-dendritic-contact",1,MotorReceptorKind.AmpaReference,50,.125);
                return new DescendingMotorCell(identity,MotoneuronReferenceParameters.CatLowThreshold,[receptor],SampleMs:.125);
            }).ToArray();
            Owner=new(Session,instance,Cells,[new(source,Input.Fingerprint,0,.125,[route],StructureId.ReticularFormation)]);
            Contacts=Cells.Select((c,i)=>MotorAxonContact.Create(c.Identity.Instance,c.Identity.Neuron,c.Identity.Projection,
                "tibant_"+side+"/unit-"+(i+1).ToString("D3"),2.0625)).ToArray();
        }
        internal DescendingMotorFrame QuietFrame()
        {
            var state=Input.Capture();long tick=state.ReleasedTick+1;
            string step=Json(new StructureStepResult(new(StructureId.ReticularFormation,tick,0,0,0,0,0,256,default,new()),[],[]));
            string evidence=Json(new{PrescribedQuietInput=true,Tick=tick,Step=step});
            var staged=Input.Stage(state.ReleasedSequence+1,evidence,VestibularProjectionOutbox.Hash(evidence),tick,tick*.125,[step]);
            var receipt=Owner.Admit(Assert.Single(staged.Packets));Input.Accept(receipt);Input.Release(staged.Sha256);
            return Owner.PublishCompleted();
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(147)]
    public void SeparatePoolsUseOneBodyAnatomyAndEveryOriginalCellCompletes(int cells)
    {
        var left=new Pool("l",cells);var right=new Pool("r",cells);
        var anatomy=new MotorAxonAnatomy(left.Contacts.Concat(right.Contacts));var clock=new MotorAxonClock(Session,0,50);
        string beforeLeft=Json(left.Owner.Capture().Cells),beforeRight=Json(right.Owner.Capture().Cells);
        left.Owner.AttachMotorOutput(anatomy,clock);right.Owner.AttachMotorOutput(anatomy,clock);
        Assert.Equal(beforeLeft,Json(left.Owner.Capture().Cells));Assert.Equal(beforeRight,Json(right.Owner.Capture().Cells));
        var receiver=new AvatarMotorAxonReceiver(anatomy,clock);var a=left.QuietFrame();var b=right.QuietFrame();
        Assert.Equal(cells,a.MotorBatches.Count);Assert.Equal(cells,b.MotorBatches.Count);
        var all=a.MotorBatches.Concat(b.MotorBatches).ToArray();Assert.Equal(cells*2,all.Select(x=>x.SourceInstanceKey).Distinct().Count());
        Assert.All(all,x=>{Assert.Equal(anatomy.Fingerprint,x.AnatomyFingerprint);Assert.Empty(x.Spikes);Assert.Equal(.125,x.CompleteThroughNeuralMs);});
        foreach(var batch in a.MotorBatches)left.Owner.AcceptBodyReceipt(receiver.AcceptWithAcknowledgement(batch));
        left.Owner.Acknowledge(a.Sha256);Assert.Equal(50,receiver.CompleteThroughBodyMs);
        Assert.Throws<ArgumentOutOfRangeException>(()=>receiver.AdvanceTo(50.125));
        foreach(var batch in b.MotorBatches.Take(cells-1))right.Owner.AcceptBodyReceipt(receiver.AcceptWithAcknowledgement(batch));
        Assert.Equal(50,receiver.CompleteThroughBodyMs);Assert.Throws<InvalidOperationException>(()=>right.Owner.Acknowledge(b.Sha256));
        var final=receiver.AcceptWithAcknowledgement(b.MotorBatches[^1]);right.Owner.AcceptBodyReceipt(final);
        right.Owner.AcceptBodyReceipt(receiver.AcceptWithAcknowledgement(b.MotorBatches[^1]));
        Assert.Equal(50.125,receiver.CompleteThroughBodyMs);Assert.Empty(receiver.AdvanceTo(50.125));right.Owner.Acknowledge(b.Sha256);
        Assert.All(left.Owner.Capture().Cells.Concat(right.Owner.Capture().Cells),s=>{Assert.Equal(.125,s.Cell.TimeMs);Assert.Empty(s.Emissions);});
        Assert.Equal(0,receiver.AcceptedEvents);Assert.Equal(0,receiver.DeliveredEvents);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra-owned-axon")]
    [InlineData("wrong-neuron")]
    [InlineData("wrong-projection")]
    public void SharedAnatomyCannotChangeOneOwnedAxon(string change)
    {
        var left=new Pool("l");var right=new Pool("r");var own=left.Contacts[0];var contacts=new List<MotorAxonContact>{own,right.Contacts[0]};
        if(change=="missing")contacts.RemoveAt(0);
        if(change=="extra-owned-axon")contacts.Add(MotorAxonContact.Create(own.SourceInstanceKey,"other-cell",Guid.NewGuid(),"extra-unit",own.ConductionDelayMs));
        if(change=="wrong-neuron")contacts[0]=MotorAxonContact.Create(own.SourceInstanceKey,"wrong-cell",own.ProjectionId,own.MotorUnitId,own.ConductionDelayMs);
        if(change=="wrong-projection")contacts[0]=MotorAxonContact.Create(own.SourceInstanceKey,own.SourceNeuronId,Guid.NewGuid(),own.MotorUnitId,own.ConductionDelayMs);
        string before=Json(left.Owner.Capture());
        Assert.Throws<ArgumentException>(()=>left.Owner.AttachMotorOutput(new(contacts),new(Session,0,0)));
        Assert.Equal(before,Json(left.Owner.Capture()));
        left.Owner.AttachMotorOutput(new(left.Contacts.Concat(right.Contacts)),new(Session,0,0));
    }

    [Fact]
    public void ForeignPoolReceiptCannotReleasePendingMotorOutput()
    {
        var left=new Pool("l");var right=new Pool("r");var anatomy=new MotorAxonAnatomy(left.Contacts.Concat(right.Contacts));
        var clock=new MotorAxonClock(Session,0,0);left.Owner.AttachMotorOutput(anatomy,clock);right.Owner.AttachMotorOutput(anatomy,clock);
        var a=left.QuietFrame();var b=right.QuietFrame();var receiver=new AvatarMotorAxonReceiver(anatomy,clock);
        var wrong=receiver.AcceptWithAcknowledgement(Assert.Single(b.MotorBatches));string before=Json(left.Owner.Capture());
        Assert.Throws<ArgumentException>(()=>left.Owner.AcceptBodyReceipt(wrong));Assert.Equal(before,Json(left.Owner.Capture()));
        Assert.Throws<InvalidOperationException>(()=>left.Owner.Acknowledge(a.Sha256));Assert.Equal(0,receiver.CompleteThroughBodyMs);
    }

    [Fact]
    public void UnattachedDeclaredPoolContinuesToBlockBodyTime()
    {
        var left=new Pool("l");var right=new Pool("r");var anatomy=new MotorAxonAnatomy(left.Contacts.Concat(right.Contacts));
        var clock=new MotorAxonClock(Session,0,0);left.Owner.AttachMotorOutput(anatomy,clock);var receiver=new AvatarMotorAxonReceiver(anatomy,clock);
        for(int i=0;i<3;i++)
        {
            var frame=left.QuietFrame();left.Owner.AcceptBodyReceipt(receiver.AcceptWithAcknowledgement(Assert.Single(frame.MotorBatches)));
            left.Owner.Acknowledge(frame.Sha256);Assert.Equal(0,receiver.CompleteThroughBodyMs);
            Assert.Throws<ArgumentOutOfRangeException>(()=>receiver.AdvanceTo(.125));
        }
        Assert.Equal(0,Assert.Single(right.Owner.Capture().Cells).Cell.TimeMs);
    }

    [Fact]
    public void PerPoolAnatomyCannotMasqueradeAsWholeBodyAnatomy()
    {
        var left=new Pool("l");var right=new Pool("r");var whole=new MotorAxonAnatomy(left.Contacts.Concat(right.Contacts));
        var clock=new MotorAxonClock(Session,0,0);left.Owner.AttachMotorOutput(new(left.Contacts),clock);
        var receiver=new AvatarMotorAxonReceiver(whole,clock);var frame=left.QuietFrame();
        Assert.Throws<ArgumentException>(()=>receiver.AcceptWithAcknowledgement(Assert.Single(frame.MotorBatches)));
        Assert.Equal(0,receiver.CompleteThroughBodyMs);Assert.Throws<InvalidOperationException>(()=>left.Owner.Acknowledge(frame.Sha256));
    }
}
