using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using Nre.UnityWorld;
using static Nre.UnityWorld.ArticulatedTendonWire;
using static Nre.UnityWorld.MuscleHingeWire;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;
using Request = Nre.UnityWorld.ArticulatedTendonWire.Request;
using Reply = Nre.UnityWorld.ArticulatedTendonWire.Reply;

namespace NeuralResonanceEngine.DNNE.Tests;

// Real host adapter/plant against a deterministic protocol peer, not Unity
// physics or neuronal authority. Native delivery needs its own visible study.
public sealed class ArticulatedTendonBridgeTests
{
    static readonly Guid Session = Guid.Parse("7cb80c66-6357-4a8b-b893-915e68d5d18a");
    static readonly string PlantHash = new('A', 64);
    static readonly string[] Names = MuscleProfiles.All.SelectMany(p => new[] { p.Variable + "/flexor", p.Variable + "/extensor" }).Order(StringComparer.Ordinal).ToArray();
    static readonly string[] Links = ["chest", "head", "left_arm", "left_foot", "left_forearm", "left_hand", "left_shin", "left_thigh",
        "pelvis", "right_arm", "right_foot", "right_forearm", "right_hand", "right_shin", "right_thigh"];
    static string Json<T>(T v) => JsonSerializer.Serialize(v, NativeArticulatedBody.Json);
    static T Copy<T>(T v) => JsonSerializer.Deserialize<T>(Json(v), NativeArticulatedBody.Json)!;
    static Configuration Config(float step = .000125f) => new() { protocol = Wire.Protocol, sessionId = Session.ToString("N"), stepSeconds = (double)step,
        declarations = Names.Select(n => new Declaration { muscleId = n, provenance = "protocol-peer-fixture-not-anatomy", anchors = [
            new Anchor { id = "origin", bodyId = "chest", x = 0d, y = -.1, z = 0d },
            new Anchor { id = "insertion", bodyId = "head", x = 0d, y = .1, z = 0d }] }).ToArray() };
    static double Length()
    {
        var g = DynamicMuscleGeometry.NagamoriTibialisAnterior;
        double passive = 23 * .046 * Math.Log(1 + Math.Exp((1 / g.PassiveLengthScale - 1.17) / .046));
        double tendon = .964 + .0047 * Math.Log(Math.Exp(passive * Math.Cos(g.PennationRadians) / (27.8 * .0047)) - 1);
        return g.OptimalFiberLengthMeters * Math.Cos(g.PennationRadians) + g.ReferenceTendonLengthMeters * tendon;
    }
    static string State(long step) => Json(new { physicsStep = step, links = Links.Select(id => new { id,
        positionBits = new[] { Encode(0), Encode(1), Encode(0) }, rotationBits = new[] { Encode(0), Encode(0), Encode(0), Encode(1) },
        velocityBits = new[] { Encode(0), Encode(0), Encode(0) }, angularVelocityBits = new[] { Encode(0), Encode(0), Encode(0) },
        jointPositionBits = id == "pelvis" ? Array.Empty<string>() : new[] { Encode(0) },
        jointVelocityBits = id == "pelvis" ? Array.Empty<string>() : new[] { Encode(0) },
        jointForceBits = id == "pelvis" ? Array.Empty<string>() : new[] { Encode(0) } }) });
    static Observations Samples(long step) => new() { physicsStep = step,
        bodyJson = Json(new { sequence = step, inputSource = "avatar_unity_body" }),
        contactsJson = Links.Select(id => Json(new { sequence = step, inputSource = "avatar_unity_" + id + "_contact" })).ToArray(),
        skullJson = new[] { "centre", "left", "right" }.Select(id => Json(new { physicsStep = step, headBodyId = "head", sensorId = id })).ToArray() };
    sealed class Peer : IArticulatedTendonExchange
    {
        public readonly string ConfigurationJson;
        public readonly Configuration Configuration;
        public Reply Current;
        public readonly List<Request> Commands = [];
        public bool LoseReply;
        public Action<Reply>? Corrupt;
        public int PhysicalSteps;
        public Peer(Configuration? configuration = null)
        {
            Configuration = configuration ?? Config(); ConfigurationJson = Json(Configuration);
            string geometry = Json(new { links = Links.Select(id => new { id }) }), state = State(0), observations = Json(Samples(0));
            Current = new() { protocol = Wire.Protocol, sessionId = Configuration.sessionId, configurationHash = Wire.Digest(ConfigurationJson),
                operation = "hello", unity = "6000.3.19f1", sequence = 0, physicsStep = 0, physicsStepMs = Wire.StepMs(Configuration), physicalTimeMs = 0d,
                bodyFingerprint = Wire.Digest(geometry), geometryJson = geometry, stateJson = state, stateHash = Wire.Digest(state),
                plantFingerprint = "", lastAppliedEffortId = "", observationsJson = observations, observationsHash = Wire.Digest(observations), applicationsJson = "",
                isolated = true, paused = true, connected = false, paths = Names.Select(n => new MuscleHingeWire.Path {
                    muscleId = n, pathFingerprint = Wire.Digest(n), lengthMeters = Length(), velocityMetersPerSecond = 0d }).ToArray() };
        }
        public Reply ReadHello() => Current;
        public void Publish(Request r)
        {
            Commands.Add(Copy(r)); var after = Copy(Current);
            after.operation = r.operation; after.sequence = r.sequence; after.plantFingerprint = r.plantFingerprint;
            if (r.operation == "step")
            {
                PhysicalSteps++; after.physicsStep++; after.physicalTimeMs = after.physicsStep * (double)after.physicsStepMs;
                after.stateJson = State(after.physicsStep); after.stateHash = Wire.Digest(after.stateJson);
                after.observationsJson = Json(Samples(after.physicsStep)); after.observationsHash = Wire.Digest(after.observationsJson);
                after.lastAppliedEffortId = r.effortId; after.applicationsJson = Json(new { fixture = true, tensions = r.tensions });
            }
            Current = after;
        }
        public Reply ReadReply(long sequence)
        {
            if (LoseReply) throw new TimeoutException("Prescribed lost acknowledgment after one physical step.");
            Corrupt?.Invoke(Current); return Current;
        }
        public NativeArticulatedBody Host() => new(ConfigurationJson, this);
    }
    static MotorMechanicalEffort Effort(NativeArticulatedBody body, Guid? id = null) => new(id ?? Guid.NewGuid(), Session, body.Fingerprint,
        body.Last.plantFingerprint, body.CompletedPhysicsStep, body.CompletedPhysicsStep * body.PhysicsStepMs, (body.CompletedPhysicsStep + 1) * body.PhysicsStepMs,
        Array.AsReadOnly(Names.Select((n, i) => new TendonTension(n, i * .125)).ToArray()));

    [Fact]
    public void All38ForcesRetainIdentityAndExactBitsAcrossOnePhysicalReceipt()
    {
        var peer = new Peer(); var body = peer.Host(); body.Bind(PlantHash); var initial = body.CaptureGeometry(); var effort = Effort(body);
        body.AdvanceOneStep(effort); var after = body.CaptureGeometry(); var command = Assert.Single(peer.Commands, c => c.operation == "step");
        Assert.Equal(38, after.Paths.Count); Assert.Equal(Names, after.Paths.Select(p => p.MuscleId));
        Assert.Equal(effort.Tensions.Select(t => Encode(t.AxialNewtons)), command.tensions.Select(t => t.axialNewtons.bits));
        Assert.Equal(0, initial.PhysicsStep); Assert.Equal(1, after.PhysicsStep); Assert.Equal(effort.EffortId, after.LastAppliedEffortId);
        Assert.Equal(body.PhysicsStepMs, after.PhysicalTimeMs); Assert.Equal(1, peer.PhysicalSteps);
    }
    [Fact]
    public void InspectionsBeforeBindingAndAfterStepRetainOneOriginalObservation()
    {
        var peer = new Peer(); var body = peer.Host(); string original = body.OriginalObservationJson;
        body.CaptureGeometry(); body.CaptureGeometry(); Assert.Equal(original, body.OriginalObservationJson); Assert.Equal(0, peer.PhysicalSteps);
        body.Bind(PlantHash); body.AdvanceOneStep(Effort(body)); string next = body.OriginalObservationJson;
        body.CaptureGeometry(); Assert.Equal(next, body.OriginalObservationJson); Assert.NotEqual(original, next);
    }
    [Theory]
    [InlineData("session")][InlineData("plant")][InlineData("body")][InlineData("step")][InlineData("clock")]
    [InlineData("missing")][InlineData("duplicate")][InlineData("reordered")][InlineData("negative")][InlineData("nan")]
    public void InvalidHostEffortFaultsBeforePublication(string bad)
    {
        var peer = new Peer(); var body = peer.Host(); body.Bind(PlantHash); var e = Effort(body); var tensions = e.Tensions.ToArray();
        e = bad switch { "session" => e with { SessionId = Guid.NewGuid() }, "plant" => e with { PlantFingerprint = new string('F', 64) },
            "body" => e with { BodyFingerprint = new string('F', 64) }, "step" => e with { StartStep = 1 }, "clock" => e with { EndTimeMs = .125 },
            "missing" => e with { Tensions = tensions[..^1] }, "reordered" => e with { Tensions = tensions.Reverse().ToArray() }, _ => e };
        if (bad == "duplicate") { tensions[1] = tensions[0]; e = e with { Tensions = tensions }; }
        if (bad is "negative" or "nan") { tensions[1] = tensions[1] with { AxialNewtons = bad == "negative" ? -1 : double.NaN }; e = e with { Tensions = tensions }; }
        Assert.Throws<InvalidOperationException>(() => body.AdvanceOneStep(e)); Assert.True(body.Faulted);
        Assert.Single(peer.Commands); Assert.Equal(0, peer.PhysicalSteps);
    }
    [Fact]
    public void MissingAcknowledgmentNeverResendsOrClaimsAnEndpoint()
    {
        var peer = new Peer(); var body = peer.Host(); body.Bind(PlantHash); peer.LoseReply = true;
        Assert.Throws<TimeoutException>(() => body.AdvanceOneStep(Effort(body)));
        Assert.Equal(1, peer.PhysicalSteps); Assert.Equal(0, body.CompletedPhysicsStep); Assert.True(body.Faulted);
        Assert.Throws<InvalidOperationException>(() => body.AdvanceOneStep(Effort(body)));
        Assert.Throws<InvalidOperationException>(() => body.CaptureGeometry()); Assert.Equal(2, peer.Commands.Count);
    }
    [Fact]
    public void ReusedOlderEffortCannotReenterAfterAnotherStep()
    {
        var peer = new Peer(); var body = peer.Host(); body.Bind(PlantHash); var first = Effort(body); body.AdvanceOneStep(first);
        body.AdvanceOneStep(Effort(body)); Assert.Throws<InvalidOperationException>(() => body.AdvanceOneStep(Effort(body, first.EffortId)));
        Assert.Equal(2, peer.PhysicalSteps); Assert.Equal(3, peer.Commands.Count);
    }
    [Theory]
    [InlineData("geometry")][InlineData("path")][InlineData("effort")][InlineData("step")][InlineData("observations")][InlineData("state")]
    public void WrongPhysicalReceiptPoisonsTheEpochWithoutAnotherCommand(string bad)
    {
        var peer = new Peer(); var body = peer.Host(); body.Bind(PlantHash);
        peer.Corrupt = r => { switch (bad) {
            case "geometry": r.geometryJson += " "; r.bodyFingerprint = Wire.Digest(r.geometryJson); break;
            case "path": r.paths[0].pathFingerprint = new string('F', 64); break;
            case "effort": r.lastAppliedEffortId = Guid.NewGuid().ToString("N"); break;
            case "step": r.physicsStep++; break;
            case "observations": r.observationsJson = Json(Samples(0)); r.observationsHash = Wire.Digest(r.observationsJson); break;
            case "state": r.stateJson = State(0); r.stateHash = Wire.Digest(r.stateJson); break;
        }};
        Assert.Throws<InvalidOperationException>(() => body.AdvanceOneStep(Effort(body))); Assert.True(body.Faulted);
        Assert.Equal(0, body.CompletedPhysicsStep); Assert.Equal(1, peer.PhysicalSteps);
        Assert.Throws<InvalidOperationException>(() => body.Stop()); Assert.Equal(2, peer.Commands.Count);
    }
    [Theory]
    [InlineData("contact-step")][InlineData("contact-order")][InlineData("head-step")][InlineData("head-sensor")][InlineData("body-step")]
    public void RehashedButForeignSensorySamplesAreRejected(string bad)
    {
        var peer = new Peer(); var samples = Samples(0);
        switch (bad) {
            case "contact-step": samples.contactsJson[0] = Json(new { sequence = 1, inputSource = "avatar_unity_chest_contact" }); break;
            case "contact-order": Array.Reverse(samples.contactsJson); break;
            case "head-step": samples.skullJson[0] = Json(new { physicsStep = 1, headBodyId = "head", sensorId = "centre" }); break;
            case "head-sensor": samples.skullJson[0] = samples.skullJson[1]; break;
            case "body-step": samples.bodyJson = Json(new { sequence = 1, inputSource = "avatar_unity_body" }); break;
        }
        peer.Current.observationsJson = Json(samples); peer.Current.observationsHash = Wire.Digest(peer.Current.observationsJson);
        Assert.Throws<InvalidOperationException>(() => peer.Host()); Assert.Empty(peer.Commands);
    }
    [Fact]
    public void BodyPauseAndStopCannotManufactureAForceOrExtraPhysicalStep()
    {
        var peer = new Peer(); var body = peer.Host(); body.Stop(); Assert.Equal(0, peer.PhysicalSteps);
        Assert.Throws<InvalidOperationException>(() => body.CaptureGeometry()); Assert.Single(peer.Commands);
    }
    [Fact]
    public void ReturnedReceiptsCannotMutateOwnedState()
    {
        var peer = new Peer(); var body = peer.Host(); var returned = body.Last; returned.paths[0].lengthMeters = -1d; returned.stateHash = "changed";
        peer.Current.paths[0].lengthMeters = 999d; // Peer also cannot mutate the retained hello after return.
        Assert.Equal(Length(), (double)body.Last.paths[0].lengthMeters); Assert.NotEqual("changed", body.Last.stateHash);
    }
    [Theory]
    [InlineData("missing")][InlineData("duplicate")][InlineData("unordered")][InlineData("one-body")][InlineData("decimal-clock")][InlineData("wrong-protocol")]
    public void RejectsIncompleteAttachmentOrClockDeclaration(string bad)
    {
        var c = Config();
        switch (bad) {
            case "missing": c.declarations = c.declarations[..^1]; break;
            case "duplicate": c.declarations[1] = c.declarations[0]; break;
            case "unordered": Array.Reverse(c.declarations); break;
            case "one-body": c.declarations[0].anchors[1].bodyId = c.declarations[0].anchors[0].bodyId; break;
            case "decimal-clock": c.stepSeconds = .000125d; break;
            case "wrong-protocol": c.protocol = "hinge"; break;
        }
        Assert.Throws<InvalidOperationException>(() => Wire.Validate(c));
    }
    [Fact]
    public void RealCausalPlantUsesAll38PathsAndWaitsForEverySourceAfterBodyReceipt()
    {
        var peer = new Peer(); var body = peer.Host(); var clock = new PhysicalSampleClock(Session, "full-body-peer", "paths", 0, body.PhysicsStepMs, 0, 1000);
        var g = DynamicMuscleGeometry.NagamoriTibialisAnterior;
        var definitions = Names.Select(n => new CausalMuscleDefinition(n, g, [SlowMotorUnitDefinition.NagamoriSlowUnit1(
            MotorAxonContact.Create("source-" + n, "cell-" + n, Guid.Parse("81f5f1dc-7bfd-4213-8751-2a277798f4bc"), "unit-" + n, .015625))], g.OptimalFiberLengthMeters)).ToArray();
        var plant = new AvatarCausalMotorPlant(clock, body, definitions, 2, schedule: CausalMotorSchedule.BodyThenMotor);
        var anatomy = new MotorAxonAnatomy(definitions.SelectMany(d => d.Units).Select(u => u.Contact));
        var motorClock = new MotorAxonClock(Session, 1000, 0);
        body.Bind(plant.Fingerprint); plant.CaptureInitial(); plant.Resume();
        Assert.Equal(CausalBodyAdvance.Advanced, plant.TryAdvanceBody().Outcome);
        Assert.Equal(1, peer.PhysicalSteps); Assert.Equal(0, plant.State.CompletedPhysicsStep); Assert.Equal(1, plant.State.ObservedPhysicsStep);
        Assert.Equal(38, Assert.Single(peer.Commands, c => c.operation == "step").tensions.Length);
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness, plant.TryCompleteMotorInterval().Outcome);
        foreach (var name in Names[..^1])
            plant.Accept(new MotorAxonPublisher(anatomy, motorClock, "source-" + name).Publish([], clock.AtStep(1).NeuralMs));
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness, plant.TryCompleteMotorInterval().Outcome);
        plant.Accept(new MotorAxonPublisher(anatomy, motorClock, "source-" + Names[^1]).Publish([], clock.AtStep(1).NeuralMs));
        Assert.Equal(CausalMotorAdvance.Advanced, plant.TryCompleteMotorInterval().Outcome);
        Assert.Equal(1, peer.PhysicalSteps); Assert.Equal(1, plant.State.CompletedPhysicsStep);
    }
}
