using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Counter/observation fixtures check ownership, not a Unity physics solve or a
// neuronal receptor. A separate case preserves an actual recorded Unity frame.
public sealed class PhysicalSampleProducerTests
{
    private static readonly PhysicalSampleClock Clock = new(Guid.Parse("7830ce1a-baa2-40cc-8a19-60298a78e3dc"),
        "avatar-body-1", "avatar_unity_body", 0, (double).01f * 1000, 200, 700);

    private sealed class Body : IAvatarPhysicalSampleBody
    {
        public long CompletedPhysicsStep { get; set; }
        public double PhysicsStepMs { get; set; } = Clock.PhysicsStepMs;
        public int Advances, Captures;
        public long WallMs = 1000;
        public Action? AdvanceHook, CaptureHook;
        public Func<PhysicalBodyFrameRequest>? FrameFactory;
        public void AdvanceOneStep() { ++Advances; ++CompletedPhysicsStep; AdvanceHook?.Invoke(); }
        public PhysicalBodyFrameRequest CaptureObservation()
        {
            ++Captures; CaptureHook?.Invoke();
            return FrameFactory?.Invoke() ?? new(CompletedPhysicsStep, WallMs, 0, 0, 0, 0, 0, 0,
                6_000_000, .99f, 37, .98f, .75f, Clock.SourceId, PhysicalArticulationFrame.Neutral with
                { Musculoskeletal = MusculoskeletalStateFrame.Neutral with
                    { Muscles = new[] { new PhysicalMuscleMeasurement("fixture", "L", 0, 0,
                        CompletedPhysicsStep % 2 == 0 ? .9f : 1.1f, CompletedPhysicsStep % 2 == 0 ? -.2f : .2f, 0) } } });
        }
    }

    [Fact]
    public void StartsPausedAndRequiresUnevolvedInitialObservation()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        Assert.Equal(PhysicalSampleAdvance.Paused, producer.TryAdvance());
        Assert.Throws<InvalidOperationException>(producer.Resume);
        Assert.Equal(0, body.Advances); Assert.Equal(0, body.Captures); Assert.False(producer.State.Faulted);
        var initial = producer.CaptureInitial();
        Assert.Equal(0, initial.PhysicsStep); Assert.Equal(Clock.NeuralEpochMs, initial.NeuralTimeMs);
        Assert.False(producer.State.Running); Assert.Equal(1, body.Captures);
        Assert.Throws<InvalidOperationException>(() => producer.CaptureInitial());
        producer.Resume(); Assert.Equal(PhysicalSampleAdvance.Advanced, producer.TryAdvance());
        Assert.Equal(1, body.Advances); Assert.Equal(2, body.Captures);
    }

    [Fact]
    public void FullBufferHoldsPhysicsUntilAnExactReceiptReleasesCapacity()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body, maximumSamples: 2);
        producer.CaptureInitial(); producer.Resume(); producer.TryAdvance();
        var full = producer.State;
        for (int i = 0; i < 20; ++i) Assert.Equal(PhysicalSampleAdvance.Backpressured, producer.TryAdvance());
        Assert.Equal(full, producer.State); Assert.Equal(1, body.Advances); Assert.Equal(2, body.Captures);
        var packet = producer.Publish();
        Assert.Equal(PhysicalSampleAdvance.Backpressured, producer.TryAdvance());
        var receiver = new AvatarPhysicalSampleReceiver(Clock); var receipt = receiver.Accept(packet);
        Assert.Throws<ArgumentException>(() => producer.Acknowledge(receipt with { BatchSha256 = "changed" }));
        Assert.Equal(PhysicalSampleAdvance.Backpressured, producer.TryAdvance());
        producer.Acknowledge(receipt);
        Assert.Equal(PhysicalSampleAdvance.Advanced, producer.TryAdvance());
        Assert.Equal(2, producer.State.CapturedThroughStep);
        Assert.Equal(new long[] { 0, 1 }, receiver.ConsumeThroughStep(1).Select(s => s.PhysicsStep));
    }

    [Fact]
    public void ByteCapacityIsReservedBeforeInvokingPhysics()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body,
            maximumObservationBytes: PhysicalSampleProtocol.MaximumObservationBytes);
        producer.CaptureInitial(); producer.Resume();
        Assert.Equal(PhysicalSampleAdvance.Backpressured, producer.TryAdvance()); Assert.Equal(0, body.Advances);
        var receiver = new AvatarPhysicalSampleReceiver(Clock);
        producer.Acknowledge(receiver.Accept(producer.Publish()));
        Assert.Equal(PhysicalSampleAdvance.Advanced, producer.TryAdvance());
    }

    [Fact]
    public void PauseAndTransportDelaysDoNotInventPhysicalSamplesOrAdvanceClock()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        producer.CaptureInitial(); producer.Resume(); producer.TryAdvance(); producer.Pause();
        var before = producer.State; body.WallMs = 9_000_000;
        for (int i = 0; i < 50; ++i) Assert.Equal(PhysicalSampleAdvance.Paused, producer.TryAdvance());
        Assert.Equal(before, producer.State); Assert.Equal(1, body.Advances); Assert.Equal(2, body.Captures);
        producer.Resume(); producer.TryAdvance();
        var sample = producer.Publish().Samples[^1];
        Assert.Equal(Clock.AtStep(2).NeuralMs, sample.NeuralTimeMs);
        Assert.Equal(body.WallMs, PhysicalSampleProtocol.Decode(Clock, sample).TimestampMs);
    }

    [Fact]
    public void PacketSizesAndRetriesPreserveAllProducerSamplesAndSigns()
    {
        PhysicalBodySample[] Run(int packetSize, bool retry)
        {
            var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
            var receiver = new AvatarPhysicalSampleReceiver(Clock); var consumed = new List<PhysicalBodySample>();
            producer.CaptureInitial(); producer.Resume();
            for (int step = 0; step <= 60; ++step)
            {
                if (step > 0) Assert.Equal(PhysicalSampleAdvance.Advanced, producer.TryAdvance());
                if (producer.State.UnsentSamples < packetSize && step < 60) continue;
                var packet = producer.Publish(packetSize); var receipt = receiver.Accept(packet);
                if (retry) Assert.Equal(receipt, receiver.Accept(Assert.Single(producer.Unacknowledged())));
                producer.Acknowledge(receipt);
                if (retry) producer.Acknowledge(receipt);
                consumed.AddRange(receiver.ConsumeThroughStep(packet.CompleteThroughStep));
            }
            Assert.Equal(60, body.Advances); Assert.Equal(61, body.Captures);
            return consumed.ToArray();
        }
        var expected = Run(1, false);
        Assert.Equal(expected, Run(7, true)); Assert.Equal(expected, Run(16, false));
        var velocities = expected.Select(s => PhysicalSampleProtocol.Decode(Clock, s).Articulation!
            .Musculoskeletal!.Muscles.Single().VelocityPerSecond).ToArray();
        Assert.Contains(-.2f, velocities); Assert.Contains(.2f, velocities);
    }

    [Fact]
    public void ActualRecordedUnityBodyIsPreservedWithoutSensorReinterpretation()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NeuralResonanceEngine.DNNE.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var request = JsonSerializer.Deserialize<PhysicalBodyFrameRequest>(File.ReadAllText(Path.Combine(directory.FullName,
            "UnityWorld", "Qualification", "corrected-body-fallen-example.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var clock = Clock with { SourceId = request.InputSource! };
        var body = new Body { FrameFactory = () => request };
        var producer = new AvatarPhysicalSampleProducer(clock, body);
        var captured = producer.CaptureInitial(); var expected = PhysicalSampleProtocol.Capture(clock, 0, request);
        Assert.Equal(expected, captured);
        var decoded = PhysicalSampleProtocol.Decode(clock, captured);
        Assert.Equal(38, decoded.Articulation!.Musculoskeletal!.Muscles.Count);
        Assert.Equal(request.NeuralSubstrates, decoded.NeuralSubstrates);
        Assert.Equal(request.Articulation!.LeftFootPressure, decoded.Articulation.LeftFootPressure);
        Assert.Equal(request.Articulation.Musculoskeletal!.Balance, decoded.Articulation.Musculoskeletal.Balance);
    }

    [Fact]
    public void CaptureFailureAfterMovementFaultsEpochAndRetainsEarlierEvidence()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        var initial = producer.CaptureInitial(); producer.Resume();
        body.CaptureHook = () => throw new InvalidOperationException("sensor failed");
        Assert.Throws<InvalidOperationException>(() => producer.TryAdvance());
        Assert.True(producer.State.Faulted); Assert.False(producer.State.Running);
        Assert.Equal(1, producer.State.ObservedPhysicsStep); Assert.Equal(0, producer.State.CapturedThroughStep);
        Assert.Contains("Completed-step capture", producer.State.FaultReason);
        body.CaptureHook = null;
        Assert.Throws<InvalidOperationException>(producer.Resume);
        Assert.Throws<InvalidOperationException>(() => producer.TryAdvance());
        Assert.Equal(1, body.Advances); Assert.Equal(initial, Assert.Single(producer.Publish().Samples));
    }

    [Fact]
    public void ExceptionAfterPartialPhysicsCannotBeRetriedAsAnotherStep()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        producer.CaptureInitial(); producer.Resume(); body.AdvanceHook = () => throw new InvalidOperationException("partial physics");
        Assert.Throws<InvalidOperationException>(() => producer.TryAdvance());
        Assert.True(producer.State.Faulted); Assert.Equal(1, body.CompletedPhysicsStep);
        Assert.Equal(0, producer.State.CapturedThroughStep); Assert.Equal(1, body.Captures);
        Assert.Throws<InvalidOperationException>(() => producer.TryAdvance()); Assert.Equal(1, body.Advances);
    }

    [Fact]
    public void MissingOrDoublePhysicalStepsNeverAcquireAFalseCompletenessStamp()
    {
        foreach (long step in new long[] { 0, 2 })
        {
            var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
            producer.CaptureInitial(); producer.Resume(); body.AdvanceHook = () => body.CompletedPhysicsStep = step;
            Assert.Throws<InvalidOperationException>(() => producer.TryAdvance());
            Assert.True(producer.State.Faulted); Assert.Equal(1, body.Captures);
            Assert.Equal(0, producer.Publish().CompleteThroughStep);
        }
    }

    [Fact]
    public void ExternalMovementWhilePausedPreventsResume()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        producer.CaptureInitial(); body.CompletedPhysicsStep = 1;
        Assert.Throws<InvalidOperationException>(producer.Resume);
        Assert.True(producer.State.Faulted); Assert.Equal(0, body.Advances); Assert.Equal(1, body.Captures);
    }

    [Fact]
    public void ChangedPhysicalDurationIsRejectedBeforeMovement()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        producer.CaptureInitial(); producer.Resume(); body.PhysicsStepMs = 10; // Not the float Unity duration.
        Assert.Throws<InvalidOperationException>(() => producer.TryAdvance());
        Assert.True(producer.State.Faulted); Assert.Equal(0, body.Advances);
    }

    [Fact]
    public void CaptureCannotChangeTheStepCounterDuringSnapshot()
    {
        var body = new Body { CaptureHook = null }; var producer = new AvatarPhysicalSampleProducer(Clock, body);
        body.CaptureHook = () => ++body.CompletedPhysicsStep;
        Assert.Throws<InvalidOperationException>(() => producer.CaptureInitial());
        Assert.True(producer.State.Faulted); Assert.Null(producer.State.CapturedThroughStep);
    }

    [Fact]
    public void CallbackReentryFaultsInsteadOfAdvancingTwice()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        producer.CaptureInitial(); producer.Resume(); body.AdvanceHook = () => producer.TryAdvance();
        Assert.Throws<InvalidOperationException>(() => producer.TryAdvance());
        Assert.True(producer.State.Faulted); Assert.Equal(1, body.Advances); Assert.Equal(1, body.Captures);
    }

    [Fact]
    public void AnotherThreadCannotPublishOrAdvanceTheOwnedBody()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        producer.CaptureInitial(); producer.Resume(); Exception? stepError = null, publishError = null;
        var other = new Thread(() =>
        {
            stepError = Record.Exception(() => producer.TryAdvance());
            publishError = Record.Exception(() => producer.Publish());
        });
        other.Start(); other.Join();
        Assert.IsType<InvalidOperationException>(stepError); Assert.IsType<InvalidOperationException>(publishError);
        Assert.False(producer.State.Faulted); Assert.Equal(0, body.Advances);
        Assert.Equal(PhysicalSampleAdvance.Advanced, producer.TryAdvance());
    }

    [Fact]
    public void ExhaustedMappedClockStopsBeforePhysicsInsteadOfPretendingBackpressure()
    {
        var clock = Clock with { InitialPhysicsStep = PhysicalSampleClock.MaximumExactStep - 1 };
        var body = new Body { CompletedPhysicsStep = clock.InitialPhysicsStep };
        var producer = new AvatarPhysicalSampleProducer(clock, body); producer.CaptureInitial(); producer.Resume();
        Assert.Equal(PhysicalSampleAdvance.Advanced, producer.TryAdvance());
        Assert.Throws<ArgumentOutOfRangeException>(() => producer.TryAdvance());
        Assert.True(producer.State.Faulted); Assert.Equal(1, body.Advances);
        Assert.Equal(PhysicalSampleClock.MaximumExactStep, producer.State.CapturedThroughStep);
    }

    [Fact]
    public void InvalidCompletedObservationCannotBeSkippedAndRecapturedLater()
    {
        var body = new Body(); var producer = new AvatarPhysicalSampleProducer(Clock, body);
        producer.CaptureInitial(); producer.Resume();
        body.FrameFactory = () => new(1, 1, float.NaN, 0, 0, 0, 0, 0, 6_000_000, 1, 37, .98f, .75f, Clock.SourceId);
        Assert.Throws<ArgumentException>(() => producer.TryAdvance());
        Assert.True(producer.State.Faulted); Assert.Equal(0, producer.State.CapturedThroughStep);
        body.FrameFactory = null;
        Assert.Throws<InvalidOperationException>(() => producer.TryAdvance()); Assert.Equal(1, body.Advances);
    }
}
