using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static NeuralResonanceEngine.DNNE.Tests.ArticulatedTendonBridgeTests;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;

namespace NeuralResonanceEngine.DNNE.Tests;

// Protocol/clock initialization controls. The peer does not run native physics;
// quiet packets below are prescribed fixtures, not observed neuronal silence.
public sealed class PassiveMotorAttachmentTests
{
    static Peer Prepared(float step = .000125f) { var c = Config(step); c.passivePreludeSteps = 1; return new(c); }
    static PhysicalSampleClock Clock(NativeArticulatedBody body) => new(Session, "full-body-peer", "avatar_unity_body", 0, body.PhysicsStepMs, 0, 1000);
    static CausalMuscleDefinition[] Definitions()
    {
        var g = DynamicMuscleGeometry.NagamoriTibialisAnterior;
        return Names.Select(n => new CausalMuscleDefinition(n, g, [SlowMotorUnitDefinition.NagamoriSlowUnit1(
            MotorAxonContact.Create("source-" + n, "cell-" + n, Guid.Parse("81f5f1dc-7bfd-4213-8751-2a277798f4bc"), "unit-" + n, .015625))], g.OptimalFiberLengthMeters)).ToArray();
    }
    static AvatarCausalMotorPlant Plant(NativeArticulatedBody body, PhysicalSampleClock clock, CausalMotorAttachment attachment) =>
        new(clock, body, Definitions(), Math.Max(2, (int)Math.Ceiling(body.PhysicsStepMs / AvatarDynamicMuscle.MaximumStepMs)),
            schedule: CausalMotorSchedule.BodyThenMotor, attachment: attachment);

    [Theory]
    [InlineData(.01f)][InlineData(.000125f)]
    public void FirstTwoPhysicalSamplesAndOriginalClockRemainPreserved(float step)
    {
        var peer = Prepared(step); var body = peer.Host(); var clock = Clock(body); string originalClock = JsonSerializer.Serialize(clock);
        var attachment = body.InitializePassiveInterval(clock);
        Assert.Equal(1, attachment.Frame.PhysicsStep); Assert.Equal(body.PhysicsStepMs, attachment.Frame.PhysicalTimeMs);
        Assert.Equal(clock.AtStep(1).NeuralMs, attachment.NeuralEpochMs); Assert.Equal(clock.Fingerprint, attachment.PhysicalClockFingerprint);
        Assert.Equal(new AvatarCanalAfferentClock(clock, 262144, 16384).SourceEpochMs, attachment.NeuralEpochMs);
        Assert.Equal(originalClock, JsonSerializer.Serialize(clock)); Assert.Equal(Guid.Empty, attachment.Frame.LastAppliedEffortId);
        Assert.Equal(Wire.Digest(body.InitializationEvidenceJson!), attachment.InitializationEvidenceSha256);
        using var evidence = JsonDocument.Parse(body.InitializationEvidenceJson!);
        Assert.Equal(0, evidence.RootElement.GetProperty("before").GetProperty("physicsStep").GetInt64());
        Assert.Equal(1, evidence.RootElement.GetProperty("after").GetProperty("physicsStep").GetInt64());
        var command = Assert.Single(peer.Commands); Assert.Equal("prelude", command.operation); Assert.Empty(command.tensions); Assert.Empty(command.effortId);
        Assert.Empty(body.Last.plantFingerprint); Assert.Empty(body.Last.applicationsJson);
    }
    [Theory]
    [InlineData("undeclared")][InlineData("bind-before-prelude")][InlineData("repeat-prelude")][InlineData("prelude-after-bind")]
    public void InitializationOrderCannotBeChangedOrReplayed(string condition)
    {
        var peer = condition == "undeclared" ? new Peer() : Prepared(); var body = peer.Host(); var clock = Clock(body);
        if (condition is "repeat-prelude" or "prelude-after-bind") body.InitializePassiveInterval(clock);
        if (condition == "prelude-after-bind") body.Bind(new string('A', 64));
        int commands = peer.Commands.Count, steps = peer.PhysicalSteps;
        if (condition == "bind-before-prelude") Assert.Throws<InvalidOperationException>(() => body.Bind(new string('A', 64)));
        else Assert.Throws<InvalidOperationException>(() => body.InitializePassiveInterval(clock));
        Assert.True(body.Faulted); Assert.Equal(commands, peer.Commands.Count); Assert.Equal(steps, peer.PhysicalSteps);
    }
    [Theory]
    [InlineData("session")][InlineData("step")][InlineData("epoch")][InlineData("rate")]
    public void ForeignPhysicalClockIsRejectedBeforeInitialization(string condition)
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body);
        clock = condition switch { "session" => clock with { SessionId = Guid.NewGuid() }, "step" => clock with { InitialPhysicsStep = 1 },
            "epoch" => clock with { PhysicalEpochMs = 1 }, "rate" => clock with { PhysicsStepMs = .125 }, _ => clock };
        Assert.Throws<InvalidOperationException>(() => body.InitializePassiveInterval(clock)); Assert.Empty(peer.Commands); Assert.Equal(0, peer.PhysicalSteps);
    }
    [Fact]
    public void LostPreludeReceiptCannotCreateAnAttachmentOrRepeatPhysics()
    {
        var peer = Prepared(); var body = peer.Host(); peer.LoseReply = true;
        Assert.Throws<TimeoutException>(() => body.InitializePassiveInterval(Clock(body)));
        Assert.True(body.Faulted); Assert.Equal(1, peer.PhysicalSteps); Assert.Equal(0, body.CompletedPhysicsStep); Assert.Null(body.InitializationEvidenceJson);
        Assert.Throws<InvalidOperationException>(() => body.InitializePassiveInterval(Clock(body))); Assert.Single(peer.Commands);
    }
    [Fact]
    public void FreshMuscleStateBeginsAtTheObservedEndpointAndDoesNotClaimEarlierMotorActivity()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var attachment = body.InitializePassiveInterval(clock);
        var plant = Plant(body, clock, attachment); body.Bind(plant.Fingerprint); plant.CaptureInitial();
        Assert.Equal(1, plant.State.CompletedPhysicsStep); Assert.Equal(clock.AtStep(1).PhysicalMs, plant.State.BodyTimeMs);
        Assert.Equal(clock.AtStep(1).PhysicalMs, plant.State.CompleteThroughBodyMs); Assert.Equal(0, plant.State.AcceptedEvents); Assert.Equal(0, plant.State.DeliveredEvents);
        Assert.All(plant.CaptureMuscles().Values, state => Assert.Equal(clock.AtStep(1).PhysicalMs, state.TimeMs));
        Assert.Equal(CausalBodyAdvance.Paused, plant.TryAdvanceBody().Outcome); Assert.Equal(1, peer.PhysicalSteps);
    }
    [Fact]
    public void FirstCoupledIntervalUsesTheOriginalPhysicalIndexAndEveryActualSourceWatermark()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var attachment = body.InitializePassiveInterval(clock);
        var plant = Plant(body, clock, attachment); body.Bind(plant.Fingerprint); plant.CaptureInitial(); plant.Resume();
        var begun = plant.TryAdvanceBody(); Assert.Equal(CausalBodyAdvance.Advanced, begun.Outcome);
        Assert.Equal(1, begun.Step!.Before.PhysicsStep); Assert.Equal(2, begun.Step.After.PhysicsStep);
        Assert.Equal(clock.AtStep(2).PhysicalMs, begun.Step.After.PhysicalTimeMs);
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness, plant.TryCompleteMotorInterval().Outcome);
        var anatomy = new MotorAxonAnatomy(Definitions().SelectMany(d => d.Units).Select(u => u.Contact));
        var motorClock = new MotorAxonClock(Session, attachment.NeuralEpochMs, attachment.Frame.PhysicalTimeMs);
        foreach (var name in Names[..^1]) plant.Accept(new MotorAxonPublisher(anatomy, motorClock, "source-" + name).Publish([], clock.AtStep(2).NeuralMs));
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness, plant.TryCompleteMotorInterval().Outcome);
        plant.Accept(new MotorAxonPublisher(anatomy, motorClock, "source-" + Names[^1]).Publish([], clock.AtStep(2).NeuralMs));
        Assert.Equal(CausalMotorAdvance.Advanced, plant.TryCompleteMotorInterval().Outcome);
        Assert.Equal(2, peer.PhysicalSteps); Assert.Equal(2, plant.State.CompletedPhysicsStep);
        Assert.Equal(clock.AtStep(2).PhysicalMs, plant.State.BodyTimeMs);
    }
    [Fact]
    public void MotorEventsBeforeTheFreshAttachmentDomainRemainRejected()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var attachment = body.InitializePassiveInterval(clock);
        var plant = Plant(body, clock, attachment); body.Bind(plant.Fingerprint); plant.CaptureInitial();
        var anatomy = new MotorAxonAnatomy(Definitions().SelectMany(d => d.Units).Select(u => u.Contact)); var contact = anatomy.Contacts[0];
        var batch = new MotorAxonBatch(1, Session, contact.SourceInstanceKey, anatomy.Fingerprint, 1, attachment.NeuralEpochMs,
            [new(Guid.NewGuid(), 1, contact.SourceNeuronId, contact.ProjectionId, Guid.NewGuid(), 1000 + body.PhysicsStepMs / 2,
                NTEnum.ACETYLCHOLINE, false, false)]);
        Assert.Throws<ArgumentException>(() => plant.Accept(batch)); Assert.Equal(0, plant.State.AcceptedEvents); Assert.Equal(1, peer.PhysicalSteps);
    }
    [Theory]
    [InlineData("active-effort")][InlineData("wrong-step")][InlineData("wrong-time")][InlineData("evidence")]
    public void AnActiveOrUnrelatedEndpointCannotBeRelabelledPassive(string condition)
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var a = body.InitializePassiveInterval(clock); var frame = a.Frame;
        frame = condition switch { "active-effort" => frame with { LastAppliedEffortId = Guid.NewGuid() }, "wrong-step" => frame with { PhysicsStep = 2 },
            "wrong-time" => frame with { PhysicalTimeMs = 0 }, _ => frame };
        Assert.Throws<ArgumentException>(() => new CausalMotorAttachment(clock, frame, condition == "evidence" ? "" : a.InitializationEvidenceSha256));
    }
    [Fact]
    public void RebasedClockCannotReplaceTheOriginalAttachmentClock()
    {
        var peer = Prepared(); var body = peer.Host(); var original = Clock(body); var a = body.InitializePassiveInterval(original);
        var rebased = original with { InitialPhysicsStep = 1, PhysicalEpochMs = a.Frame.PhysicalTimeMs, NeuralEpochMs = a.NeuralEpochMs };
        Assert.Throws<ArgumentException>(() => Plant(body, rebased, a));
    }
    [Fact]
    public void ChangedAttachmentPathsAreRejectedBeforeFreshMuscleInitialization()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var a = body.InitializePassiveInterval(clock);
        var paths = a.Frame.Paths.ToArray(); paths[0] = paths[0] with { LengthMeters = paths[0].LengthMeters + .01 };
        var changed = new CausalMotorAttachment(clock, a.Frame with { Paths = paths }, a.InitializationEvidenceSha256);
        var plant = Plant(body, clock, changed); body.Bind(plant.Fingerprint);
        Assert.Throws<InvalidOperationException>(() => plant.CaptureInitial()); Assert.True(plant.State.Faulted); Assert.Equal(1, peer.PhysicalSteps);
    }
}
