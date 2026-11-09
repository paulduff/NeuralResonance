using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Interface fixtures, not motor-neuron or physiological qualification.
public sealed class MotorAxonReplayBufferTests
{
    static readonly Guid Projection=Guid.Parse("f0e26a14-530e-41b6-b511-5c7cd20fb428");
    static readonly MotorAxonClock Clock=new(Guid.Parse("caf3d052-224b-4e2b-adfe-bb60f584e3e0"),100,1000);
    static MotorAxonAnatomy Anatomy=>new([MotorAxonContact.Create("source","cell",Projection,"unit",2.0625)]);
    static SpikeMessage Spike(double time,int id=1)=>new()
    {
        MessageId=StableNeuralIdentity.Create("replay-buffer-fixture",id),TimestampMs=time,
        SourceStructure=StructureId.SpinalCordMotor,SourceInstanceKey="source",SourceNeuronId="cell",
        ProjectionId=Projection,SynapseId=StableNeuralIdentity.Create("replay-buffer-outbound","cell"),Neurotransmitter=NTEnum.ACETYLCHOLINE
    };

    [Fact]
    public void LostAcknowledgementRetriesOriginalEventsWithoutDoubleDelivery()
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source");var receiver=new AvatarMotorAxonReceiver(anatomy,Clock);
        var original=Spike(101.03125);var batch=sender.Publish([original],110);
        var lost=receiver.AcceptWithAcknowledgement(batch);var delivered=Assert.Single(receiver.AdvanceTo(1010));
        var replay=Assert.Single(sender.Unacknowledged());Assert.Same(batch,replay);
        var receipt=receiver.AcceptWithAcknowledgement(JsonSerializer.Deserialize<MotorAxonBatch>(JsonSerializer.Serialize(replay))!);
        Assert.Equal(lost,receipt);Assert.Empty(receiver.AdvanceTo(1010));Assert.Equal(1,receiver.DeliveredEvents);
        Assert.Equal(original.MessageId,delivered.MessageId);Assert.Equal(101.03125,delivered.FiredAtNeuralMs);Assert.Equal(1003.09375,delivered.ArrivalAtBodyMs);
        sender.Acknowledge(JsonSerializer.Deserialize<MotorAxonAcknowledgement>(JsonSerializer.Serialize(receipt))!);
        Assert.Equal(0,sender.PendingBatches);Assert.Equal(0,sender.PendingEvents);Assert.Equal(1,sender.AcknowledgedThroughSequence);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BackpressureDoesNotConsumeSourceSequenceOrdinalIdentityOrWatermark(bool byBatch)
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source",maximumBatches:byBatch?1:8,maximumEvents:byBatch?8:1);
        var receiver=new AvatarMotorAxonReceiver(anatomy,Clock);var first=sender.Publish([Spike(101)],110);
        Assert.Throws<InvalidOperationException>(()=>sender.Publish([Spike(111,2)],120));
        Assert.Equal(1,sender.PendingBatches);Assert.Equal(1,sender.PendingEvents);Assert.False(sender.Faulted);
        sender.Acknowledge(receiver.AcceptWithAcknowledgement(first));
        var retry=sender.Publish([Spike(111,2)],120);Assert.Equal(2,retry.Sequence);Assert.Equal(2,Assert.Single(retry.Spikes).Ordinal);
        receiver.AcceptWithAcknowledgement(retry);Assert.Equal(2,receiver.AdvanceTo(1020).Count);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("source")]
    [InlineData("anatomy")]
    [InlineData("version")]
    [InlineData("sequence")]
    [InlineData("digest")]
    public void UnmatchedAcknowledgementCannotReleaseRetainedEvents(string defect)
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source");var receiver=new AvatarMotorAxonReceiver(anatomy,Clock);
        var batch=sender.Publish([Spike(101)],110);var valid=receiver.AcceptWithAcknowledgement(batch);
        var invalid=defect switch
        {
            "session"=>valid with{SessionId=Guid.NewGuid()},"source"=>valid with{SourceInstanceKey="other"},
            "anatomy"=>valid with{AnatomyFingerprint="other"},"version"=>valid with{Version=99},
            "sequence"=>valid with{Sequence=2},"digest"=>valid with{BatchSha256=new string('0',64)},_=>throw new Exception()
        };
        Assert.Throws<ArgumentException>(()=>sender.Acknowledge(invalid));Assert.Same(batch,Assert.Single(sender.Unacknowledged()));
        Assert.Equal(0,sender.AcknowledgedThroughSequence);Assert.Equal(1,sender.PendingEvents);
        sender.Acknowledge(valid);Assert.Empty(sender.Unacknowledged());
    }

    [Fact]
    public void CumulativeAcceptanceDoesNotClaimBodyDeliveryOrMuscleForce()
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source");var receiver=new AvatarMotorAxonReceiver(anatomy,Clock);
        var first=sender.Publish([Spike(101)],110);var second=sender.Publish([Spike(111,2)],120);
        var early=receiver.AcceptWithAcknowledgement(first);var latest=receiver.AcceptWithAcknowledgement(second);
        sender.Acknowledge(latest);sender.Acknowledge(early); // Delayed earlier acknowledgement is still exact.
        Assert.Equal(2,sender.AcknowledgedThroughSequence);Assert.Equal(0,sender.PendingEvents);
        Assert.Equal(2,receiver.PendingEvents);Assert.Equal(0,receiver.DeliveredEvents);Assert.Equal(1000,receiver.BodyTimeMs);
        Assert.Equal(2,receiver.AdvanceTo(1020).Count);
    }

    [Fact]
    public void DuplicateReceiptIsIdempotentAndChangedReceiptIsRejected()
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source");var receiver=new AvatarMotorAxonReceiver(anatomy,Clock);
        var receipt=receiver.AcceptWithAcknowledgement(sender.Publish([Spike(101)],110));
        sender.Acknowledge(receipt);sender.Acknowledge(receipt);
        Assert.Throws<ArgumentException>(()=>sender.Acknowledge(receipt with{BatchSha256="changed"}));
        Assert.Equal(1,sender.AcknowledgedThroughSequence);Assert.Empty(sender.Unacknowledged());
    }

    [Fact]
    public void ExpiredReceiptCannotResetSequenceOrEraseNewWork()
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source",retainedAcknowledgements:1);
        var receiver=new AvatarMotorAxonReceiver(anatomy,Clock);var old=receiver.AcceptWithAcknowledgement(sender.Publish([],110));sender.Acknowledge(old);
        sender.Acknowledge(receiver.AcceptWithAcknowledgement(sender.Publish([],120)));var pending=sender.Publish([Spike(121)],130);
        Assert.Throws<ArgumentException>(()=>sender.Acknowledge(old));Assert.Same(pending,Assert.Single(sender.Unacknowledged()));Assert.Equal(2,sender.AcknowledgedThroughSequence);
    }

    [Fact]
    public void CallerMutationCannotRewriteTheRetainedBatchOrReceipt()
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source");var receiver=new AvatarMotorAxonReceiver(anatomy,Clock);
        var original=Spike(101.125);var supplied=new List<SpikeMessage>{original};var batch=sender.Publish(supplied,110);
        supplied.Clear();original.TimestampMs=900;original.SourceNeuronId="changed";
        var receipt=receiver.AcceptWithAcknowledgement(Assert.Single(sender.Unacknowledged()));
        Assert.Equal(MotorAxonBatchIntegrity.Digest(batch),receipt.BatchSha256);sender.Acknowledge(receipt);
        Assert.Equal(101.125,Assert.Single(receiver.AdvanceTo(1010)).FiredAtNeuralMs);
    }

    [Fact]
    public void RejectedSourceInputDoesNotPoisonTheBuffer()
    {
        var sender=new MotorAxonReplayBuffer(Anatomy,Clock,"source");var invalid=Spike(101);invalid.Neurotransmitter=NTEnum.GABA;
        Assert.Throws<ArgumentException>(()=>sender.Publish([invalid],110));Assert.False(sender.Faulted);Assert.Empty(sender.Unacknowledged());
        var valid=sender.Publish([Spike(101)],110);Assert.Equal(1,valid.Sequence);Assert.Equal(1,Assert.Single(valid.Spikes).Ordinal);
    }

    [Fact]
    public void EmptyCompletenessBatchesAreAlsoBoundedAndRetriedExactly()
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source",maximumBatches:1);var receiver=new AvatarMotorAxonReceiver(anatomy,Clock);
        var first=sender.Publish([],110);Assert.Throws<InvalidOperationException>(()=>sender.Publish([],120));
        Assert.Equal(1000,receiver.CompleteThroughBodyMs);sender.Acknowledge(receiver.AcceptWithAcknowledgement(Assert.Single(sender.Unacknowledged())));
        var second=sender.Publish([],120);Assert.Equal(2,second.Sequence);Assert.Equal(110,first.CompleteThroughNeuralMs);Assert.Empty(second.Spikes);
    }

    [Fact]
    public void PlantAcknowledgesAcceptanceAndReplayWithoutApplyingContractionTwice()
    {
        var anatomy=Anatomy;var sender=new MotorAxonReplayBuffer(anatomy,Clock,"source");
        var plant=new AvatarIsometricMotorUnitPlant(anatomy,Clock,new Dictionary<string,MotorUnitCalciumParameters>{{"unit",MotorUnitCalciumParameters.NagamoriSlowUnit1}});
        var batch=sender.Publish([Spike(101.03125)],110);var first=plant.AcceptWithAcknowledgement(batch);
        Assert.Equal(0,plant.Capture().DeliveredEvents);Assert.Equal(0,Assert.Single(plant.Capture().Units).ForceNewtons);
        var delivered=plant.AdvanceToTick(80);Assert.Single(delivered.Arrivals);var state=Assert.Single(delivered.Frame.Units);
        Assert.Equal(first,plant.AcceptWithAcknowledgement(batch));sender.Acknowledge(first);
        var repeated=plant.AdvanceToTick(80);Assert.Empty(repeated.Arrivals);Assert.Equal(state,Assert.Single(repeated.Frame.Units));
    }
}
