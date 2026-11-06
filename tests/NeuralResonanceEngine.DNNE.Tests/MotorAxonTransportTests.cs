using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MotorAxonTransportTests
{
    private static readonly Guid Projection = Guid.Parse("eae11ac8-f6f5-424c-9353-f178e1bb4cc2");
    private static readonly Guid Session = Guid.Parse("6097a7b7-720a-44b1-a240-ab45eaa96bd0");
    private static MotorAxonClock Clock => new(Session, 100, 1000);
    private static MotorAxonContact Contact(string source = "spinal-L", string neuron = "alpha-0", string unit = "left-soleus-0", double delay = 2) =>
        MotorAxonContact.Create(source, neuron, Projection, unit, delay);
    private static SpikeMessage Spike(double time, int id = 1, string source = "spinal-L", string neuron = "alpha-0") => new()
    {
        MessageId = StableNeuralIdentity.Create("motor-transport-fixture", source, id), TimestampMs = time,
        SourceStructure = StructureId.SpinalCordMotor, SourceInstanceKey = source, SourceNeuronId = neuron,
        ProjectionId = Projection, SynapseId = StableNeuralIdentity.Create("fixture-outbound", source, neuron),
        Neurotransmitter = NTEnum.ACETYLCHOLINE
    };
    private static (MotorAxonPublisher Publisher, AvatarMotorAxonReceiver Receiver) Pair(MotorAxonAnatomy? anatomy = null)
    {
        anatomy ??= new([Contact()]);
        return (new(anatomy, Clock, "spinal-L"), new(anatomy, Clock));
    }

    [Fact]
    public void OriginalIdentityFractionalFiringAndDeclaredDelaySurviveSerialization()
    {
        var (publisher, receiver) = Pair(); var original = Spike(101.125);
        var batch = publisher.Publish([original], 110);
        var restored = JsonSerializer.Deserialize<MotorAxonBatch>(JsonSerializer.Serialize(batch))!;
        Assert.Equal(MotorAxonBatchDisposition.Accepted, receiver.Accept(restored));
        original.TimestampMs = 999; original.SourceNeuronId = "changed-after-publish";
        Assert.Empty(receiver.AdvanceTo(1003));
        var arrival = Assert.Single(receiver.AdvanceTo(1003.125));
        Assert.Equal(Session, arrival.SessionId); Assert.Equal(original.MessageId, arrival.MessageId);
        Assert.Equal("spinal-L", arrival.SourceInstanceKey); Assert.Equal("alpha-0", arrival.SourceNeuronId);
        Assert.Equal(Projection, arrival.ProjectionId); Assert.Equal(original.SynapseId, arrival.SourceSynapseId);
        Assert.Equal(Contact().ContactId, arrival.ContactId); Assert.Equal("left-soleus-0", arrival.MotorUnitId);
        Assert.Equal(101.125, arrival.FiredAtNeuralMs); Assert.Equal(1003.125, arrival.ArrivalAtBodyMs);
        Assert.Equal(1, receiver.AcceptedEvents); Assert.Equal(1, receiver.DeliveredEvents);
    }

    [Fact]
    public void ExactReplayIsIdempotentButChangedReplayCannotChangeQueuedOrDeliveredEvents()
    {
        var (publisher, receiver) = Pair(); var batch = publisher.Publish([Spike(101)], 110);
        receiver.Accept(batch);
        Assert.Equal(MotorAxonBatchDisposition.ExactReplay, receiver.Accept(batch));
        var altered = batch with { Spikes = [batch.Spikes[0] with { FiredAtNeuralMs = 102 }] };
        Assert.Throws<ArgumentException>(() => receiver.Accept(altered));
        Assert.Single(receiver.AdvanceTo(1010));
        Assert.Equal(MotorAxonBatchDisposition.ExactReplay, receiver.Accept(batch));
        Assert.Empty(receiver.AdvanceTo(1010)); Assert.Equal(1, receiver.DeliveredEvents);
    }

    [Fact]
    public void MissingBatchHoldsTheBodyClockUntilTheExactGapIsRecovered()
    {
        var (publisher, receiver) = Pair();
        var first = publisher.Publish([Spike(101)], 110);
        var second = publisher.Publish([Spike(111, 2)], 120);
        Assert.Throws<ArgumentException>(() => receiver.Accept(second));
        Assert.Equal(1000, receiver.CompleteThroughBodyMs); Assert.Equal(0, receiver.PendingEvents);
        Assert.Throws<ArgumentOutOfRangeException>(() => receiver.AdvanceTo(1001));
        receiver.Accept(first); receiver.Accept(second);
        Assert.Equal(new[] { first.Spikes[0].MessageId, second.Spikes[0].MessageId },
            receiver.AdvanceTo(1020).Select(x => x.MessageId));
    }

    [Fact]
    public void SourceSilenceRequiresEmptyBatchesFromEveryDeclaredInstance()
    {
        var anatomy = new MotorAxonAnatomy([Contact(), Contact("spinal-R", unit: "right-soleus-0")]);
        var receiver = new AvatarMotorAxonReceiver(anatomy, Clock);
        var left = new MotorAxonPublisher(anatomy, Clock, "spinal-L");
        var right = new MotorAxonPublisher(anatomy, Clock, "spinal-R");
        receiver.Accept(left.Publish([Spike(101)], 110));
        Assert.Equal(1000, receiver.CompleteThroughBodyMs);
        Assert.Throws<ArgumentOutOfRangeException>(() => receiver.AdvanceTo(1003));
        receiver.Accept(right.Publish([], 105));
        Assert.Single(receiver.AdvanceTo(1005));
        Assert.Equal(1005, receiver.CompleteThroughBodyMs);
        Assert.Throws<ArgumentOutOfRangeException>(() => receiver.AdvanceTo(1006));
        receiver.Accept(right.Publish([], 110)); Assert.Empty(receiver.AdvanceTo(1010));
    }

    [Fact]
    public void BatchPartitionAndSourceArrivalOrderDoNotChangeMotorEventIdentityOrTiming()
    {
        var anatomy = new MotorAxonAnatomy([Contact(), Contact("spinal-R", unit: "right-soleus-0")]);
        var whole = new AvatarMotorAxonReceiver(anatomy, Clock);
        var split = new AvatarMotorAxonReceiver(anatomy, Clock);
        var left = new MotorAxonPublisher(anatomy, Clock, "spinal-L");
        var right = new MotorAxonPublisher(anatomy, Clock, "spinal-R");
        var l = new[] { Spike(101), Spike(105.125, 2), Spike(111, 3) };
        var r = new[] { Spike(101, source: "spinal-R"), Spike(109.25, 2, "spinal-R") };
        whole.Accept(right.Publish(r, 120)); whole.Accept(left.Publish(l, 120));
        left = new(anatomy, Clock, "spinal-L"); right = new(anatomy, Clock, "spinal-R");
        split.Accept(left.Publish(l[..2], 106)); split.Accept(right.Publish(r[..1], 106));
        var observed = split.AdvanceTo(1006).ToList();
        split.Accept(left.Publish(l[2..], 120)); split.Accept(right.Publish(r[1..], 120));
        observed.AddRange(split.AdvanceTo(1010)); observed.AddRange(split.AdvanceTo(1020));
        Assert.Equal(whole.AdvanceTo(1020), observed);
        Assert.Equal("spinal-L", observed[0].SourceInstanceKey);
        Assert.Equal("spinal-R", observed[1].SourceInstanceKey);
    }

    [Theory]
    [InlineData("transmitter")]
    [InlineData("background")]
    [InlineData("feedback")]
    [InlineData("source")]
    [InlineData("structure")]
    [InlineData("projection")]
    [InlineData("neuron")]
    [InlineData("identity")]
    [InlineData("synapse")]
    [InlineData("nonfinite")]
    public void InvalidSourceBatchIsRejectedAtomicallyWithoutInventingAReplacement(string defect)
    {
        var (publisher, receiver) = Pair(); var invalid = Spike(102, 2);
        switch (defect)
        {
            case "transmitter": invalid.Neurotransmitter = NTEnum.GABA; break;
            case "background": invalid.IsSyntheticBackground = true; break;
            case "feedback": invalid.IsFeedback = true; break;
            case "source": invalid.SourceInstanceKey = "other"; break;
            case "structure": invalid.SourceStructure = StructureId.V1; break;
            case "projection": invalid.ProjectionId = Guid.NewGuid(); break;
            case "neuron": invalid.SourceNeuronId = "unbound"; break;
            case "identity": invalid.MessageId = Guid.Empty; break;
            case "synapse": invalid.SynapseId = Guid.Empty; break;
            case "nonfinite": invalid.TimestampMs = double.NaN; break;
        }
        Assert.ThrowsAny<ArgumentException>(() => publisher.Publish([Spike(101), invalid], 110));
        var accepted = publisher.Publish([Spike(101)], 110);
        Assert.Equal(1, accepted.Sequence); Assert.Equal(1, accepted.Spikes[0].Ordinal);
        receiver.Accept(accepted); Assert.Single(receiver.AdvanceTo(1010));
    }

    [Theory]
    [InlineData("session")]
    [InlineData("version")]
    [InlineData("anatomy")]
    [InlineData("source")]
    [InlineData("ordinal")]
    [InlineData("transmitter")]
    [InlineData("background")]
    [InlineData("contact")]
    [InlineData("feedback")]
    [InlineData("repeat")]
    [InlineData("timestamp")]
    [InlineData("watermark")]
    public void ReceiverRejectsForeignOrCorruptPacketsWithoutConsumingTheirGoodPrefix(string defect)
    {
        var (publisher, receiver) = Pair(); var good = publisher.Publish([Spike(101), Spike(102, 2)], 110);
        var bad = defect switch
        {
            "session" => good with { SessionId = Guid.NewGuid() },
            "version" => good with { Version = 99 },
            "anatomy" => good with { AnatomyFingerprint = "other" },
            "source" => good with { SourceInstanceKey = "unknown" },
            "ordinal" => good with { Spikes = [good.Spikes[0], good.Spikes[1] with { Ordinal = 3 }] },
            "transmitter" => good with { Spikes = [good.Spikes[0], good.Spikes[1] with { Neurotransmitter = NTEnum.GABA }] },
            "background" => good with { Spikes = [good.Spikes[0], good.Spikes[1] with { IsSyntheticBackground = true }] },
            "feedback" => good with { Spikes = [good.Spikes[0], good.Spikes[1] with { IsFeedback = true }] },
            "repeat" => good with { Spikes = [good.Spikes[0], good.Spikes[1] with { MessageId = good.Spikes[0].MessageId }] },
            "contact" => good with { Spikes = [good.Spikes[0], good.Spikes[1] with { ProjectionId = Guid.NewGuid() }] },
            "timestamp" => good with { Spikes = [good.Spikes[0], good.Spikes[1] with { FiredAtNeuralMs = 99 }] },
            "watermark" => good with { CompleteThroughNeuralMs = 101 },
            _ => throw new ArgumentException()
        };
        Assert.ThrowsAny<ArgumentException>(() => receiver.Accept(bad));
        Assert.Equal(0, receiver.AcceptedEvents); Assert.Equal(0, receiver.PendingEvents);
        Assert.Equal(1000, receiver.CompleteThroughBodyMs);
        receiver.Accept(good); Assert.Equal(2, receiver.AdvanceTo(1010).Count);
    }

    [Fact]
    public void QueuePressureRejectsWholeBatchAndAllowsRetryAfterConsumption()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]);
        var publisher = new MotorAxonPublisher(anatomy, Clock, "spinal-L");
        var receiver = new AvatarMotorAxonReceiver(anatomy, Clock, maximumPending: 1);
        var first = publisher.Publish([Spike(101)], 110);
        var second = publisher.Publish([Spike(111, 2)], 120);
        receiver.Accept(first);
        Assert.Throws<InvalidOperationException>(() => receiver.Accept(second));
        Assert.Equal(1010, receiver.CompleteThroughBodyMs); Assert.Equal(1, receiver.AcceptedEvents);
        Assert.Single(receiver.AdvanceTo(1010)); receiver.Accept(second);
        Assert.Single(receiver.AdvanceTo(1020)); Assert.Equal(2, receiver.DeliveredEvents);
    }

    [Fact]
    public void CompletedIntervalCannotBeReopenedAndEventIdentityCannotBeReused()
    {
        var (publisher, receiver) = Pair(); var first = publisher.Publish([Spike(101)], 110);
        receiver.Accept(first); receiver.AdvanceTo(1010);
        Assert.Throws<ArgumentException>(() => publisher.Publish([Spike(109, 2)], 120));
        Assert.Throws<ArgumentException>(() => publisher.Publish([Spike(111)], 120));
        Assert.Throws<ArgumentException>(() => publisher.Publish([], 109));
        var second = publisher.Publish([Spike(111, 2)], 120);
        var reused = second with { Spikes = [second.Spikes[0] with { MessageId = first.Spikes[0].MessageId }] };
        var late = second with { Spikes = [second.Spikes[0] with { FiredAtNeuralMs = 110 }] };
        Assert.Throws<ArgumentException>(() => receiver.Accept(reused));
        Assert.Throws<ArgumentException>(() => receiver.Accept(late));
        receiver.Accept(second); Assert.Single(receiver.AdvanceTo(1020));
    }

    [Fact]
    public void DelayedArrivalsSurviveBodyStepsAndEmptyBatchesWithoutNewSpikes()
    {
        var (publisher, receiver) = Pair(new([Contact(delay: 25)]));
        receiver.Accept(publisher.Publish([Spike(101)], 110));
        Assert.Empty(receiver.AdvanceTo(1010)); Assert.Equal(1, receiver.PendingEvents);
        receiver.Accept(publisher.Publish([], 130));
        Assert.Empty(receiver.AdvanceTo(1025)); Assert.Single(receiver.AdvanceTo(1026));
        Assert.Empty(receiver.AdvanceTo(1030));
        Assert.Throws<ArgumentOutOfRangeException>(() => receiver.AdvanceTo(1029));
        Assert.Equal(1, receiver.AcceptedEvents);
    }

    [Fact]
    public void BoundedHistoryRejectsExpiredReplayAndEpochCapacityNeverSilentlyDropsEvents()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]);
        var publisher = new MotorAxonPublisher(anatomy, Clock, "spinal-L");
        var receiver = new AvatarMotorAxonReceiver(anatomy, Clock, maximumEpochEvents: 1, retainedBatches: 1);
        var first = publisher.Publish([Spike(101)], 110);
        receiver.Accept(first); receiver.AdvanceTo(1010);
        receiver.Accept(publisher.Publish([], 120));
        Assert.Throws<ArgumentException>(() => receiver.Accept(first));
        var more = publisher.Publish([Spike(121, 2)], 130);
        Assert.Throws<InvalidOperationException>(() => receiver.Accept(more));
        Assert.Equal(1020, receiver.CompleteThroughBodyMs); Assert.Equal(1, receiver.DeliveredEvents);
    }

    [Fact]
    public void AnatomyIsOrderIndependentButDelayChangesRequireAnExplicitNewFingerprint()
    {
        var a = Contact(); var b = Contact("spinal-R", unit: "right-soleus-0");
        var one = new MotorAxonAnatomy([a, b]); var two = new MotorAxonAnatomy([b, a]);
        Assert.Equal(one.Fingerprint, two.Fingerprint);
        Assert.NotEqual(one.Fingerprint, new MotorAxonAnatomy([a with { ConductionDelayMs = 3 }, b]).Fingerprint);
        Assert.Throws<ArgumentException>(() => new MotorAxonAnatomy([a, a]));
        Assert.Throws<ArgumentException>(() => new MotorAxonAnatomy([a, Contact("spinal-R", unit: a.MotorUnitId)]));
        Assert.Throws<ArgumentException>(() => new MotorAxonAnatomy([a with { ConductionDelayMs = -1 }]));
        Assert.Throws<ArgumentException>(() => new MotorAxonAnatomy([a with { ContactId = Guid.NewGuid() }]));
    }
}
