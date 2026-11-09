using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Physical fixtures only. Passing these checks does not qualify transduction,
// neuronal event generation, Unity integration or physical recovery.
public sealed class PhysicalSampleTransportTests
{
    private static readonly PhysicalSampleClock Clock = new(Guid.Parse("2296d9ee-8376-4f0b-9c9a-8f82c9bc3eb9"),
        "avatar-body-1", "physical-source-1", 0, (double).01f * 1000, 500, 1000);
    private static PhysicalBodyFrameRequest Frame(long sequence = 1, long wallMs = 1_000_000, string? source = null) =>
        new(sequence, wallMs, .1f, .2f, .3f, .01f, .02f, .03f, 6_000_000, .99f, 37, .98f, .75f,
            source ?? Clock.SourceId, PhysicalArticulationFrame.Neutral);
    private static PhysicalSampleBatch Packet(long firstStep = 0, int count = 1, long sequence = 1, PhysicalSampleClock? clock = null)
    {
        clock ??= Clock;
        var samples = Enumerable.Range(0, count).Select(i => PhysicalSampleProtocol.Capture(clock, firstStep + i,
            Frame(firstStep + i, source: clock.SourceId))).ToArray();
        return new(PhysicalSampleProtocol.Version, clock.SessionId, clock.SourceId, clock.Fingerprint,
            sequence, firstStep + count - 1, samples);
    }
    private static T Wire<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static object ReceiverState(AvatarPhysicalSampleReceiver receiver) => new
    {
        receiver.PendingSamples, receiver.PendingObservationBytes, receiver.AcceptedSamples, receiver.ConsumedSamples,
        receiver.SampleCompleteThroughStep, receiver.ConsumedThroughStep, receiver.Faulted
    };

    [Fact]
    public void PhysicsStepsMapThroughDeclaredEpochsWithoutWallTime()
    {
        var a = PhysicalSampleProtocol.Capture(Clock, 7, Frame(wallMs: 100));
        var b = PhysicalSampleProtocol.Capture(Clock, 7, Frame(wallMs: 9_000_000));
        Assert.Equal(500 + 7 * (double).01f * 1000, a.PhysicalTimeMs);
        Assert.Equal(1000 + 7 * (double).01f * 1000, a.NeuralTimeMs);
        Assert.Equal(a.PhysicalTimeMs, b.PhysicalTimeMs); Assert.Equal(a.NeuralTimeMs, b.NeuralTimeMs);
        Assert.Equal(a.SampleId, b.SampleId); Assert.NotEqual(a.ObservationSha256, b.ObservationSha256);
        Assert.Equal(100, PhysicalSampleProtocol.Decode(Clock, a).TimestampMs);
        Assert.Equal(9_000_000, PhysicalSampleProtocol.Decode(Clock, b).TimestampMs);
    }

    [Fact]
    public void NonzeroInitialStepIsAnExplicitFreshEpoch()
    {
        var clock = Clock with { InitialPhysicsStep = 500 };
        var sender = new AvatarPhysicalSampleOutbox(clock); var receiver = new AvatarPhysicalSampleReceiver(clock);
        sender.Capture(500, Frame()); receiver.Accept(Wire(sender.Publish()));
        var first = Assert.Single(receiver.ConsumeThroughStep(500));
        Assert.Equal(clock.PhysicalEpochMs, first.PhysicalTimeMs); Assert.Equal(clock.NeuralEpochMs, first.NeuralTimeMs);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AtStep(499));
    }

    [Fact]
    public void NestedMuscleMeasurementsCannotMutateCapturedOrAcceptedSamples()
    {
        var muscles = new List<PhysicalMuscleMeasurement> { new("soleus", "L", .3f, 20, 1.1f, -.25f, .2f) };
        var observation = Frame() with { Articulation = PhysicalArticulationFrame.Neutral with
        { Musculoskeletal = MusculoskeletalStateFrame.Neutral with { Muscles = muscles } } };
        var sender = new AvatarPhysicalSampleOutbox(Clock); var receiver = new AvatarPhysicalSampleReceiver(Clock);
        var sample = sender.Capture(0, observation); muscles.Clear();
        var mutable = sender.Publish().Samples.ToArray(); var packet = Packet() with { Samples = mutable };
        receiver.Accept(packet); mutable[0] = sample with { ObservationJson = "changed" };
        var accepted = Assert.Single(receiver.ConsumeThroughStep(0));
        Assert.Equal(sample, accepted);
        var decoded = PhysicalSampleProtocol.Decode(Clock, accepted);
        Assert.Equal(-.25f, Assert.Single(decoded.Articulation!.Musculoskeletal!.Muscles).VelocityPerSecond);
        var again = PhysicalSampleProtocol.Decode(Clock, accepted);
        Assert.NotSame(decoded.Articulation.Musculoskeletal.Muscles, again.Articulation!.Musculoskeletal!.Muscles);
    }

    [Fact]
    public void PacketSizeAndReceiptRetriesPreserveEveryFixedGridSample()
    {
        PhysicalBodySample[] Run(int batchSize, bool retry)
        {
            var sender = new AvatarPhysicalSampleOutbox(Clock); var receiver = new AvatarPhysicalSampleReceiver(Clock);
            var consumed = new List<PhysicalBodySample>();
            for (int step = 0; step < 61; ++step)
            {
                Assert.True(sender.CanCaptureMaximumObservation);
                sender.Capture(step, Frame(step, 1_000_000 + step * 17));
                if (sender.UnsentSamples < batchSize && step != 60) continue;
                var batch = sender.Publish(batchSize); var receipt = receiver.Accept(Wire(batch));
                consumed.AddRange(receiver.ConsumeThroughStep(batch.CompleteThroughStep));
                if (retry)
                {
                    Assert.Equal(receipt, receiver.Accept(Wire(Assert.Single(sender.Unacknowledged()))));
                    Assert.Empty(receiver.ConsumeThroughStep(batch.CompleteThroughStep));
                }
                sender.Acknowledge(Wire(receipt));
            }
            Assert.Equal(61, receiver.AcceptedSamples); Assert.Equal(61, receiver.ConsumedSamples);
            Assert.Equal(0, sender.RetainedSamples); Assert.Equal(0, sender.RetainedObservationBytes);
            Assert.Equal(0, receiver.PendingObservationBytes);
            return consumed.ToArray();
        }
        var reference = Run(1, false);
        Assert.Equal(reference, Run(7, false)); Assert.Equal(reference, Run(16, true));
    }

    [Fact]
    public void MissingTrafficCannotAdvanceReceiverOrInventAnInitialState()
    {
        var receiver = new AvatarPhysicalSampleReceiver(Clock);
        Assert.Null(receiver.SampleCompleteThroughStep);
        Assert.Throws<ArgumentOutOfRangeException>(() => receiver.ConsumeThroughStep(0));
        receiver.Accept(Packet()); var before = ReceiverState(receiver);
        Assert.Throws<ArgumentOutOfRangeException>(() => receiver.ConsumeThroughStep(1));
        Assert.Equal(before, ReceiverState(receiver));
        Assert.Single(receiver.ConsumeThroughStep(0));
    }

    [Fact]
    public void RejectedSourceGapDoesNotConsumeStepOrIdentity()
    {
        var sender = new AvatarPhysicalSampleOutbox(Clock);
        Assert.Throws<ArgumentException>(() => sender.Capture(1, Frame()));
        Assert.Null(sender.CapturedThroughStep); Assert.Equal(0, sender.RetainedSamples);
        var first = sender.Capture(0, Frame());
        Assert.Throws<ArgumentException>(() => sender.Capture(0, Frame()));
        Assert.Throws<ArgumentException>(() => sender.Capture(2, Frame()));
        sender.Capture(1, Frame()); Assert.Equal(first, sender.Publish().Samples[0]);
    }

    [Fact]
    public void ReceiverRejectsPacketGapThenAcceptsOriginalPacketsInOrder()
    {
        var receiver = new AvatarPhysicalSampleReceiver(Clock);
        Assert.Throws<ArgumentException>(() => receiver.Accept(Packet(1, sequence: 2)));
        Assert.Equal(0, receiver.AcceptedSamples);
        receiver.Accept(Packet()); receiver.Accept(Packet(1, sequence: 2));
        Assert.Equal(new long[] { 0, 1 }, receiver.ConsumeThroughStep(1).Select(s => s.PhysicsStep));
    }

    [Fact]
    public void InvalidLastSampleRejectsEntirePacketBeforeCommit()
    {
        var receiver = new AvatarPhysicalSampleReceiver(Clock); var packet = Packet(count: 2);
        var invalid = packet with { Samples = new[] { packet.Samples[0], packet.Samples[1] with { ObservationSha256 = "wrong" } } };
        Assert.Throws<ArgumentException>(() => receiver.Accept(invalid));
        Assert.Equal(0, receiver.AcceptedSamples); Assert.Null(receiver.SampleCompleteThroughStep); Assert.False(receiver.Faulted);
        receiver.Accept(packet); Assert.Equal(2, receiver.ConsumeThroughStep(1).Count);
    }

    [Fact]
    public void EmptyPacketsAndFalseWatermarksCannotDeclareSilence()
    {
        var receiver = new AvatarPhysicalSampleReceiver(Clock); var packet = Packet();
        Assert.Throws<ArgumentException>(() => receiver.Accept(packet with { Samples = Array.Empty<PhysicalBodySample>(), CompleteThroughStep = 100 }));
        Assert.Throws<ArgumentException>(() => receiver.Accept(packet with { CompleteThroughStep = 1 }));
        Assert.Equal(0, receiver.AcceptedSamples); receiver.Accept(packet);
    }

    [Fact]
    public void DifferentSessionSourceBodyOrClockCannotAliasTheOriginalStream()
    {
        foreach (var clock in new[] { Clock with { SessionId = Guid.NewGuid() }, Clock with { SourceId = "source-2" },
            Clock with { BodyId = "body-2" }, Clock with { PhysicsStepMs = .1 }, Clock with { NeuralEpochMs = 2000 } })
        {
            var receiver = new AvatarPhysicalSampleReceiver(Clock);
            Assert.NotEqual(Clock.Fingerprint, clock.Fingerprint);
            Assert.NotEqual(PhysicalSampleProtocol.Identity(Clock, 0), PhysicalSampleProtocol.Identity(clock, 0));
            Assert.Throws<ArgumentException>(() => receiver.Accept(Packet(clock: clock)));
            Assert.Null(receiver.SampleCompleteThroughStep);
        }
    }

    [Fact]
    public void MappedTimeAndSampleIdentityCannotBeRetimed()
    {
        var packet = Packet(); var sample = packet.Samples[0];
        foreach (var changed in new[] { sample with { NeuralTimeMs = 1001 }, sample with { PhysicalTimeMs = 501 },
            sample with { SampleId = Guid.NewGuid() }, sample with { PhysicsStep = 1 } })
        {
            var receiver = new AvatarPhysicalSampleReceiver(Clock);
            Assert.Throws<ArgumentException>(() => receiver.Accept(packet with { Samples = new[] { changed } }));
            Assert.Equal(0, receiver.PendingSamples);
        }
    }

    [Fact]
    public void SourceBackpressureHoldsPhysicsUntilExactReceiptReleasesCapacity()
    {
        var sender = new AvatarPhysicalSampleOutbox(Clock, maximumSamples: 1); var receiver = new AvatarPhysicalSampleReceiver(Clock);
        sender.Capture(0, Frame()); var batch = sender.Publish(); Assert.False(sender.CanCaptureMaximumObservation);
        Assert.Throws<InvalidOperationException>(() => sender.Capture(1, Frame()));
        var receipt = receiver.Accept(batch); Assert.False(sender.CanCaptureMaximumObservation);
        sender.Acknowledge(receipt); Assert.True(sender.CanCaptureMaximumObservation);
        sender.Capture(1, Frame()); Assert.Equal(1, Assert.Single(sender.Publish().Samples).PhysicsStep);
        Assert.Equal(0, receiver.ConsumedSamples); // Acceptance is not receptor consumption.
    }

    [Fact]
    public void ReceiverBackpressureRetainsUnacknowledgedPacketWithoutTruncation()
    {
        var receiver = new AvatarPhysicalSampleReceiver(Clock, maximumSamples: 1);
        receiver.Accept(Packet()); var second = Packet(1, sequence: 2);
        Assert.Throws<InvalidOperationException>(() => receiver.Accept(second));
        Assert.Equal(1, receiver.AcceptedSamples); Assert.Equal(0, receiver.SampleCompleteThroughStep);
        receiver.ConsumeThroughStep(0); receiver.Accept(second);
        Assert.Equal(1, Assert.Single(receiver.ConsumeThroughStep(1)).PhysicsStep);
    }

    [Fact]
    public void BatchCapacityPreservesUnsentSamplesAndSequence()
    {
        var sender = new AvatarPhysicalSampleOutbox(Clock, maximumBatches: 1); var receiver = new AvatarPhysicalSampleReceiver(Clock);
        sender.Capture(0, Frame()); sender.Capture(1, Frame()); var first = sender.Publish(1);
        Assert.Throws<InvalidOperationException>(() => sender.Publish(1)); Assert.Equal(1, sender.UnsentSamples);
        sender.Acknowledge(receiver.Accept(first)); var second = sender.Publish(1);
        Assert.Equal(2, second.Sequence); Assert.Equal(1, Assert.Single(second.Samples).PhysicsStep);
    }

    [Fact]
    public void AlteredReplayCannotChangeAcceptedOrConsumedPhysicalState()
    {
        var receiver = new AvatarPhysicalSampleReceiver(Clock); var packet = Packet(); var receipt = receiver.Accept(packet);
        receiver.ConsumeThroughStep(0); var before = ReceiverState(receiver);
        var altered = PhysicalSampleProtocol.Capture(Clock, 0, Frame() with { LinearVelocityXMetersPerSecond = 9 });
        Assert.Throws<ArgumentException>(() => receiver.Accept(packet with { Samples = new[] { altered } }));
        Assert.Equal(before, ReceiverState(receiver)); Assert.Equal(receipt, receiver.Accept(packet));
    }

    [Fact]
    public void UnmatchedAndExpiredReceiptsCannotReleaseSourceState()
    {
        var sender = new AvatarPhysicalSampleOutbox(Clock, retainedReceipts: 1); var receiver = new AvatarPhysicalSampleReceiver(Clock);
        sender.Capture(0, Frame()); var first = receiver.Accept(sender.Publish());
        foreach (var bad in new[] { first with { BatchSha256 = "wrong" }, first with { CompleteThroughStep = 8 },
            first with { SessionId = Guid.NewGuid() }, first with { SourceId = "another" }, first with { Sequence = 2 } })
            Assert.Throws<ArgumentException>(() => sender.Acknowledge(bad));
        Assert.Equal(1, sender.RetainedSamples); sender.Acknowledge(first); sender.Acknowledge(first);
        sender.Capture(1, Frame()); sender.Acknowledge(receiver.Accept(sender.Publish()));
        Assert.Throws<ArgumentException>(() => sender.Acknowledge(first)); Assert.Equal(0, sender.RetainedSamples);
    }

    [Fact]
    public void ReceiverReplayHistoryExpiresExplicitlyWithoutEpochReset()
    {
        var receiver = new AvatarPhysicalSampleReceiver(Clock, retainedBatches: 1);
        var first = Packet(); receiver.Accept(first); receiver.Accept(Packet(1, sequence: 2));
        Assert.Throws<ArgumentException>(() => receiver.Accept(first));
        Assert.Equal(2, receiver.AcceptedSamples); Assert.Equal(2, receiver.ConsumeThroughStep(1).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => receiver.ConsumeThroughStep(0));
    }

    [Fact]
    public void NonfiniteNestedMeasurementsAndWrongOriginsLeaveSourceUnchanged()
    {
        var sender = new AvatarPhysicalSampleOutbox(Clock);
        Assert.Throws<ArgumentException>(() => sender.Capture(0, Frame(source: "wrong-source")));
        Assert.Throws<ArgumentException>(() => sender.Capture(0, Frame() with { LinearVelocityXMetersPerSecond = float.NaN }));
        var nested = Frame() with { Articulation = PhysicalArticulationFrame.Neutral with { Musculoskeletal =
            MusculoskeletalStateFrame.Neutral with { Muscles = new[] { new PhysicalMuscleMeasurement("soleus", "L", 0, 0, 1, float.PositiveInfinity, 0) } } } };
        Assert.Throws<ArgumentException>(() => sender.Capture(0, nested));
        Assert.Equal(0, sender.RetainedSamples); Assert.Null(sender.CapturedThroughStep);
        sender.Capture(0, Frame()); Assert.Single(sender.Publish().Samples);
    }

    [Fact]
    public void UnrepresentableOrInvalidClockCannotStart()
    {
        foreach (var clock in new[] { Clock with { SessionId = Guid.Empty }, Clock with { SourceId = " " },
            Clock with { BodyId = "body " }, Clock with { PhysicsStepMs = 0 }, Clock with { PhysicsStepMs = double.NaN },
            Clock with { InitialPhysicsStep = -1 }, Clock with { NeuralEpochMs = 1e30 }, Clock with { PhysicalEpochMs = double.PositiveInfinity } })
            Assert.Throws<ArgumentException>(() => new AvatarPhysicalSampleOutbox(clock));
        Assert.Throws<ArgumentOutOfRangeException>(() => Clock.AtStep(PhysicalSampleClock.MaximumExactStep + 1));
    }

    [Fact]
    public void ObservationByteLimitRejectsBeforeCaptureAndByteBudgetHoldsPhysics()
    {
        var sender = new AvatarPhysicalSampleOutbox(Clock, maximumObservationBytes: PhysicalSampleProtocol.MaximumObservationBytes);
        var huge = Frame() with { Articulation = PhysicalArticulationFrame.Neutral with { Musculoskeletal =
            MusculoskeletalStateFrame.Neutral with { Posture = new string('x', PhysicalSampleProtocol.MaximumObservationBytes) } } };
        Assert.Throws<ArgumentException>(() => sender.Capture(0, huge)); Assert.Equal(0, sender.RetainedSamples);
        sender.Capture(0, Frame()); Assert.False(sender.CanCaptureMaximumObservation);
        var receiver = new AvatarPhysicalSampleReceiver(Clock); sender.Acknowledge(receiver.Accept(sender.Publish()));
        Assert.True(sender.CanCaptureMaximumObservation); Assert.Equal(0, sender.RetainedObservationBytes);
    }

    [Fact]
    public void CumulativeReceiptDoesNotClaimReceptorConsumption()
    {
        var sender = new AvatarPhysicalSampleOutbox(Clock); var receiver = new AvatarPhysicalSampleReceiver(Clock);
        sender.Capture(0, Frame()); receiver.Accept(sender.Publish());
        sender.Capture(1, Frame()); var latest = receiver.Accept(sender.Publish()); sender.Acknowledge(latest);
        Assert.Equal(0, sender.PendingBatches); Assert.Equal(0, sender.RetainedSamples);
        Assert.Equal(2, receiver.PendingSamples); Assert.Equal(0, receiver.ConsumedSamples);
        Assert.Null(receiver.ConsumedThroughStep); Assert.Equal(1, receiver.SampleCompleteThroughStep);
    }

    [Fact]
    public void ReceiverByteCapacityRejectsAnEntirePacketAndCanResumeAfterConsumption()
    {
        // Payload-size fixture, not a physiologically meaningful posture label.
        var large = Frame() with { Articulation = PhysicalArticulationFrame.Neutral with { Musculoskeletal =
            MusculoskeletalStateFrame.Neutral with { Posture = new string('x', 140_000) } } };
        var sender = new AvatarPhysicalSampleOutbox(Clock);
        sender.Capture(0, large); sender.Capture(1, large);
        var batch = sender.Publish(); var receiver = new AvatarPhysicalSampleReceiver(Clock,
            maximumObservationBytes: PhysicalSampleProtocol.MaximumObservationBytes);
        Assert.Throws<InvalidOperationException>(() => receiver.Accept(batch));
        Assert.Equal(0, receiver.AcceptedSamples); Assert.Equal(0, receiver.PendingObservationBytes);
        // The original two-sample packet remains retained at its source; a real
        // receiver with an insufficient absolute capacity needs a compatible peer,
        // not a same-sequence packet rewritten after uncertain acceptance.
        Assert.Same(batch, Assert.Single(sender.Unacknowledged()));
        var compatible = new AvatarPhysicalSampleReceiver(Clock); sender.Acknowledge(compatible.Accept(batch));
        Assert.Equal(2, compatible.ConsumeThroughStep(1).Count);
        var separateSource = new AvatarPhysicalSampleOutbox(Clock);
        separateSource.Capture(0, large); separateSource.Capture(1, large);
        var first = separateSource.Publish(1); var second = separateSource.Publish(1);
        receiver.Accept(first); int pendingBytes = receiver.PendingObservationBytes;
        Assert.Throws<InvalidOperationException>(() => receiver.Accept(second));
        Assert.Equal(1, receiver.AcceptedSamples); Assert.Equal(pendingBytes, receiver.PendingObservationBytes);
        receiver.ConsumeThroughStep(0); Assert.Equal(0, receiver.PendingObservationBytes);
        receiver.Accept(second); Assert.Equal(pendingBytes, receiver.PendingObservationBytes);
    }
}
