using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static NeuralResonanceEngine.DNNE.Tests.ArticulatedTendonBridgeTests;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;
using Scalar = Nre.UnityWorld.MuscleHingeWire;

namespace NeuralResonanceEngine.DNNE.Tests;

// Prescribed protocol peer, not native physics or afferent firing. Actual
// native-body transport is a separate frozen Unity qualification.
public sealed class NativeArticulatedHeadInputTests
{
    static string Json<T>(T value) => JsonSerializer.Serialize(value, NativeArticulatedBody.Json);
    static PhysicalSampleClock Clock(NativeArticulatedBody body) => new(Session, "full-body-peer", "avatar_unity_body", 0, body.PhysicsStepMs, 0, 1000);
    static object V(float x, float y, float z) => new { x, y, z };
    static Peer Prepared()
    {
        var configuration = Config(); configuration.passivePreludeSteps = 1; var peer = new Peer(configuration);
        Install(peer.Current); peer.Corrupt = Install; return peer;
    }
    static void Install(Wire.Reply reply)
    {
        float seconds = (float)(reply.physicsStep * (double)reply.physicsStepMs / 1000), position = seconds*seconds, velocity = 2*seconds;
        var observations = JsonSerializer.Deserialize<Wire.Observations>(reply.observationsJson, NativeArticulatedBody.Json)!;
        observations.skullJson = new[] { "centre", "left", "right" }.Select((sensor, i) => {
            float local = i == 0 ? 0 : i == 1 ? -.0625f : .0625f;
            return Json(new { version = "native-head-material-point-v2", headBodyId = "head", sensorId = sensor, physicsStep = reply.physicsStep,
                localPointMeters = V(local,0,0), originWorldMeters = V(position,1,0), rightWorld = V(1,0,0), upWorld = V(0,1,0), forwardWorld = V(0,0,1),
                pointWorldMeters = V(position+local,1,0), pointVelocityWorldMetersPerSecond = V(velocity,0,0),
                angularVelocityWorldRadiansPerSecond = V(0,0,0), gravityWorldMetersPerSecondSquared = V(0,-9.81f,0),
                centerOfMassWorldMeters = V(position,1,0), centerOfMassVelocityWorldMetersPerSecond = V(velocity,0,0),
                rotation = new { x = 0f, y = 0f, z = 0f, w = 1f }, scale = V(1,1,1),
                hierarchy = new[] { "head", "chest", "pelvis" }.Select(name => new { name, localScale = V(1,1,1) }), legacyScaleGuardPassed = true });
        }).ToArray();
        reply.observationsJson = Json(observations); reply.observationsHash = Wire.Digest(reply.observationsJson);
        var state = JsonNode.Parse(reply.stateJson)!; var head = state["links"]!.AsArray().Single(n => n!["id"]!.GetValue<string>() == "head")!;
        head["positionBits"] = new JsonArray(Scalar.Encode(position),Scalar.Encode(1),Scalar.Encode(0));
        head["velocityBits"] = new JsonArray(Scalar.Encode(velocity),Scalar.Encode(0),Scalar.Encode(0));
        reply.stateJson = state.ToJsonString(); reply.stateHash = Wire.Digest(reply.stateJson);
    }
    static void Step(NativeArticulatedBody body)
    {
        if (body.Last.plantFingerprint == "") body.Bind(new string('A',64));
        body.AdvanceOneStep(new(Guid.NewGuid(),Session,body.Fingerprint,body.Last.plantFingerprint,body.CompletedPhysicsStep,
            body.CompletedPhysicsStep*body.PhysicsStepMs,(body.CompletedPhysicsStep+1)*body.PhysicsStepMs,
            Array.AsReadOnly(Names.Select(n => new TendonTension(n,0)).ToArray())));
    }
    [Fact]
    public void OriginalHeadBaselineAndActualPassiveEndpointReachExistingKinematics()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var input = new NativeArticulatedHeadInput(body,clock);
        var first = input.ReadAcknowledgedEndpoint(); Assert.Empty(peer.Commands);
        Assert.All(first.Samples, s => { Assert.Null(s.Kinematics.AccelerationFromStep); Assert.Null(s.Kinematics.VestibularInput); });
        var attachment = body.InitializePassiveInterval(clock); var second = input.ReadAcknowledgedEndpoint();
        Assert.Equal(1,second.PhysicsStep); Assert.Equal(attachment.NeuralEpochMs,second.NeuralMs);
        Assert.Equal(body.Last.observationsJson,second.OriginalObservationsJson); Assert.Equal(body.Last.stateHash,second.BodyStateSha256);
        Assert.All(second.Samples, s => {
            Assert.Equal(PhysicalSampleProtocol.Identity(clock,1),s.Observation.SampleId); Assert.Equal(0,s.Kinematics.AccelerationFromStep);
            Assert.InRange(s.Kinematics.AverageAccelerationWorldMetersPerSecondSquared!.X,1.99999,2.00001);
            Assert.Equal(Wire.Digest(s.OriginalNativeJson),s.NativeSha256);
        });
        Assert.Single(peer.Commands); Assert.Equal(1,peer.PhysicalSteps); Assert.Null(input.Failure);
    }
    [Fact]
    public void InspectionReusesOriginalSensoryReceiptWithoutRecaptureOrNewDerivative()
    {
        var peer = Prepared(); var body = peer.Host(); var input = new NativeArticulatedHeadInput(body,Clock(body));
        var original = input.ReadAcknowledgedEndpoint(); body.CaptureGeometry();
        Assert.Same(original,input.ReadAcknowledgedEndpoint()); Assert.Same(original,input.ReadAcknowledgedEndpoint());
        Assert.Equal("inspect",Assert.Single(peer.Commands).operation); Assert.Equal(0,peer.PhysicalSteps);
    }
    [Theory]
    [InlineData("session")][InlineData("epoch")][InlineData("step")][InlineData("rate")]
    public void ForeignClockCannotRelabelNativeHeadHistory(string kind)
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body);
        clock = kind switch { "session" => clock with { SessionId = Guid.NewGuid() }, "epoch" => clock with { PhysicalEpochMs = 1 },
            "step" => clock with { InitialPhysicsStep = 1 }, _ => clock with { PhysicsStepMs = 1 } };
        Assert.Throws<ArgumentException>(() => new NativeArticulatedHeadInput(body,clock)); Assert.Empty(peer.Commands);
    }
    [Fact]
    public void AttachingAfterTheBaselineWasLostCannotManufactureAnInitialAcceleration()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); body.InitializePassiveInterval(clock);
        Assert.Throws<ArgumentException>(() => new NativeArticulatedHeadInput(body,clock)); Assert.Single(peer.Commands);
    }
    [Fact]
    public void SkippedEndpointFaultsWithoutInterpolationOrAnotherPhysicalRequest()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var input = new NativeArticulatedHeadInput(body,clock);
        var first = input.ReadAcknowledgedEndpoint(); body.InitializePassiveInterval(clock); Step(body); int count = peer.Commands.Count;
        Assert.Throws<InvalidOperationException>(() => input.ReadAcknowledgedEndpoint()); Assert.Same(first,input.Last);
        Assert.NotNull(input.Failure); Assert.Empty(input.PartialSamples); Assert.Equal(count,peer.Commands.Count);
    }
    [Theory]
    [InlineData("scale")][InlineData("missing")][InlineData("body-pose")][InlineData("point-velocity")][InlineData("basis")]
    public void InvalidSecondSensorRetainsCompletedPrefixAndStopsTheSensoryEpoch(string kind)
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var input = new NativeArticulatedHeadInput(body,clock);
        var original = input.ReadAcknowledgedEndpoint();
        peer.Corrupt = reply => {
            Install(reply); var samples = JsonSerializer.Deserialize<Wire.Observations>(reply.observationsJson,NativeArticulatedBody.Json)!;
            var skull = JsonNode.Parse(samples.skullJson[1])!;
            switch (kind) {
                case "scale": skull["hierarchy"]![0]!["localScale"]!["x"] = 2; break;
                case "missing": skull.AsObject().Remove("rightWorld"); break;
                case "body-pose": skull["originWorldMeters"]!["x"] = 2; break;
                case "point-velocity": skull["pointVelocityWorldMetersPerSecond"]!["x"] = 2; break;
                case "basis": skull["rightWorld"]!["x"] = -1; break;
            }
            samples.skullJson[1] = skull.ToJsonString(); reply.observationsJson = Json(samples); reply.observationsHash = Wire.Digest(reply.observationsJson);
        };
        body.InitializePassiveInterval(clock); int commands = peer.Commands.Count;
        Assert.ThrowsAny<Exception>(() => input.ReadAcknowledgedEndpoint()); Assert.Same(original,input.Last);
        Assert.Equal("centre",Assert.Single(input.PartialSamples).SensorId); Assert.NotNull(input.AttemptedReplyJson); Assert.NotNull(input.Failure);
        Assert.Throws<InvalidOperationException>(() => input.ReadAcknowledgedEndpoint()); Assert.Equal(commands,peer.Commands.Count);
    }
    [Fact]
    public void ReturnedCollectionsCannotChangeTheRetainedMeasurement()
    {
        var peer = Prepared(); var body = peer.Host(); var input = new NativeArticulatedHeadInput(body,Clock(body)); var frame = input.ReadAcknowledgedEndpoint();
        Assert.Throws<NotSupportedException>(() => ((IList<ArticulatedHeadSample>)frame.Samples).Clear());
        var changed = frame.Samples[0].Observation with { PointWorldMeters = new(9,9,9) };
        Assert.NotEqual(changed,input.Last!.Samples[0].Observation); Assert.Equal(3,input.Last.Samples.Count);
    }
    [Fact]
    public void AnUncertainPhysicalCommandCannotReturnStaleDataAsFreshSensoryProgress()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body); var input = new NativeArticulatedHeadInput(body,clock); var first = input.ReadAcknowledgedEndpoint();
        peer.LoseReply = true; Assert.Throws<TimeoutException>(() => body.InitializePassiveInterval(clock));
        Assert.Throws<InvalidOperationException>(() => input.ReadAcknowledgedEndpoint()); Assert.Same(first,input.Last); Assert.Single(peer.Commands);
    }
    [Fact]
    public async Task AnotherThreadCannotAdoptTheNativeOwnersSensoryEndpoint()
    {
        var peer = Prepared(); var body = peer.Host(); var clock = Clock(body);
        var completion = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        new Thread(() => completion.SetResult(Record.Exception(() => new NativeArticulatedHeadInput(body,clock)))) { IsBackground = true }.Start();
        Assert.IsType<InvalidOperationException>(await completion.Task);
        Assert.Empty(peer.Commands); Assert.False(body.Faulted);
    }
}
