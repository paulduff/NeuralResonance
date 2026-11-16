using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ObservedMotorSourceTests
{
    private static readonly ObservedMotorIdentity Identity = new("observed-source", "observed-cell",
        Guid.Parse("8b31608c-b4c2-46db-888f-7b5ecb8cf87a"), Guid.Parse("4d964aa5-0077-4b8e-bcd9-6250a72df890"));
    private static MotorAxonAnatomy Anatomy(ObservedMotorIdentity identity) => new([
        MotorAxonContact.Create(identity.Instance, identity.Neuron, identity.Projection, "slow-unit", 2.0625)]);
    private static string Bytes(ObservedMotorSnapshot value) => JsonSerializer.Serialize(value);

    [Fact]
    public void Publication_Requires_A_New_Completed_Source_Interval()
    {
        var lane = new ObservedMotorLane(Guid.NewGuid(), Identity, false, Anatomy(Identity), new(Guid.NewGuid(), 0, 0));
        var initial = Bytes(lane.CaptureSource());
        Assert.Throws<InvalidOperationException>(() => lane.PublishCompleted());
        Assert.Equal(initial, Bytes(lane.CaptureSource()));
        lane.AdvanceOneSample(); var batch = lane.PublishCompleted();
        Assert.Equal(.1, batch.CompleteThroughNeuralMs); Assert.Empty(batch.Spikes);
        Assert.Throws<InvalidOperationException>(() => lane.PublishCompleted());
        Assert.Same(batch, Assert.Single(lane.Unacknowledged()));
    }

    [Fact]
    public void Backpressure_And_Exact_Retry_Do_Not_Reexecute_The_Neuron()
    {
        var anatomy = Anatomy(Identity); var clock = new MotorAxonClock(Guid.NewGuid(), 0, 0);
        var lane = new ObservedMotorLane(Guid.NewGuid(), Identity, false, anatomy, clock, maximumBatches: 1);
        var receiver = new AvatarMotorAxonReceiver(anatomy, clock);
        lane.AdvanceOneSample(); var batch = lane.PublishCompleted(); string before = Bytes(lane.CaptureSource());
        Assert.Throws<InvalidOperationException>(() => lane.AdvanceOneSample());
        Assert.Equal(before, Bytes(lane.CaptureSource()));
        var receipt = receiver.AcceptWithAcknowledgement(batch);
        Assert.Equal(receipt, receiver.AcceptWithAcknowledgement(Assert.Single(lane.Unacknowledged())));
        Assert.Equal(before, Bytes(lane.CaptureSource()));
        lane.Acknowledge(receipt); lane.Acknowledge(receipt);
        Assert.Empty(lane.Unacknowledged()); Assert.Equal(before, Bytes(lane.CaptureSource()));
        Assert.Equal(.2, lane.AdvanceOneSample().TimeMs);
    }

    [Fact]
    public void Packet_Cadence_Does_Not_Change_Source_State_Or_Integrated_Duration()
    {
        var evidence = Guid.NewGuid(); var anatomy = Anatomy(Identity); var clock = new MotorAxonClock(Guid.NewGuid(), 0, 0);
        var frequent = new ObservedMotorLane(evidence, Identity, false, anatomy, clock);
        var sparse = new ObservedMotorLane(evidence, Identity, false, anatomy, clock);
        var first = new AvatarMotorAxonReceiver(anatomy, clock); var second = new AvatarMotorAxonReceiver(anatomy, clock);
        for (int i = 1; i <= 20; ++i)
        {
            Assert.Equal(Bytes(frequent.AdvanceOneSample()), Bytes(sparse.AdvanceOneSample()));
            frequent.Acknowledge(first.AcceptWithAcknowledgement(frequent.PublishCompleted()));
            if (i % 5 == 0) sparse.Acknowledge(second.AcceptWithAcknowledgement(sparse.PublishCompleted()));
        }
        Assert.Equal(2, frequent.CaptureSource().TimeMs);
        Assert.Equal(Bytes(frequent.CaptureSource()), Bytes(sparse.CaptureSource()));
    }

    [Fact]
    public void A_Single_Cell_Cannot_Declare_Completeness_For_Another_Route_Or_Population()
    {
        var clock = new MotorAxonClock(Guid.NewGuid(), 0, 0);
        Assert.Throws<ArgumentException>(() => new ObservedMotorLane(Guid.NewGuid(), Identity with { Neuron = "another" }, false, Anatomy(Identity), clock));
        var population = new MotorAxonAnatomy([
            MotorAxonContact.Create(Identity.Instance, Identity.Neuron, Identity.Projection, "slow-unit", 2.0625),
            MotorAxonContact.Create(Identity.Instance, "second-cell", Guid.NewGuid(), "second-unit", 2.0625)]);
        Assert.Throws<ArgumentException>(() => new ObservedMotorLane(Guid.NewGuid(), Identity, false, population, clock));
        Assert.Throws<ArgumentException>(() => new ObservedMotorLane(Guid.NewGuid(), Identity, false, Anatomy(Identity), clock with { NeuralEpochMs = 1 }));
    }

    [Fact]
    public void Captured_State_Is_Detached_From_Later_Biological_Advancement()
    {
        var source = new ObservedMotorSource(Guid.NewGuid(), Identity, false);
        var initial = source.Capture(); var bytes = Bytes(initial);
        source.AdvanceOneSample(); source.AdvanceOneSample();
        Assert.Equal(bytes, Bytes(initial)); Assert.Equal(0, initial.CompletedSamples);
        Assert.Equal(2, source.Capture().CompletedSamples);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)initial.State)[0] = 0);
    }

    [Fact]
    public void A_Different_Thread_Cannot_Advance_Or_Publish_Source_Time()
    {
        var lane = new ObservedMotorLane(Guid.NewGuid(), Identity, false, Anatomy(Identity), new(Guid.NewGuid(), 0, 0));
        string before = Bytes(lane.CaptureSource()); Exception? advance = null, publish = null;
        var thread = new Thread(() =>
        {
            try { lane.AdvanceOneSample(); } catch (Exception error) { advance = error; }
            try { lane.PublishCompleted(); } catch (Exception error) { publish = error; }
        });
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(advance); Assert.IsType<InvalidOperationException>(publish);
        Assert.Equal(before, Bytes(lane.CaptureSource()));
    }
}
