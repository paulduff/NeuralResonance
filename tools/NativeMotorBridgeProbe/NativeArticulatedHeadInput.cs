#nullable enable
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;

internal sealed record ArticulatedHeadSample(string SensorId, string OriginalNativeJson, string NativeSha256,
    AvatarSkullObservation Observation, AvatarSkullKinematicReceipt Kinematics);
internal sealed record ArticulatedHeadFrame(long PhysicsStep, double PhysicalMs, double NeuralMs,
    string BodyStateSha256, string OriginalObservationsJson, string ObservationsSha256, IReadOnlyList<ArticulatedHeadSample> Samples);

// Read only the native host's acknowledged endpoint. Does not request another
// physical sample, step a body, infer silence, or manufacture an afferent event.
internal sealed class NativeArticulatedHeadInput
{
    internal const string Version = "full-avatar-original-head-input-v1";
    private static readonly string[] Sensors = ["centre", "left", "right"];
    private static readonly string[] SnapshotFields = ["version", "headBodyId", "sensorId", "physicsStep", "localPointMeters",
        "originWorldMeters", "rightWorld", "upWorld", "forwardWorld", "pointWorldMeters", "pointVelocityWorldMetersPerSecond",
        "angularVelocityWorldRadiansPerSecond", "gravityWorldMetersPerSecondSquared", "centerOfMassWorldMeters",
        "centerOfMassVelocityWorldMetersPerSecond", "rotation", "scale", "hierarchy", "legacyScaleGuardPassed"];
    private readonly NativeArticulatedBody _body;
    private readonly PhysicalSampleClock _clock;
    private readonly AvatarSkullKinematics[] _adapters;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private ArticulatedHeadFrame? _last;
    private readonly List<ArticulatedHeadSample> _partial = [];
    internal string Fingerprint { get; }
    internal string? Failure { get; private set; }
    internal string? AttemptedReplyJson { get; private set; }
    internal IReadOnlyList<ArticulatedHeadSample> PartialSamples => Array.AsReadOnly(_partial.ToArray());

    internal NativeArticulatedHeadInput(NativeArticulatedBody body, PhysicalSampleClock clock)
    {
        ArgumentNullException.ThrowIfNull(body); ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        var current = body.ReadAcknowledgedForSensory();
        if (body.Faulted || body.CompletedPhysicsStep != 0 || clock.InitialPhysicsStep != 0 || clock.PhysicalEpochMs != 0 ||
            clock.SessionId.ToString("N") != current.sessionId || clock.PhysicsStepMs != body.PhysicsStepMs)
            throw new ArgumentException("Attach to the fresh actual body clock before its passive interval; no omitted head baseline.");
        _body = body; _clock = clock;
        _adapters = Sensors.Select((id, i) => new AvatarSkullKinematics(clock, "head", id, new(LocalX(i), 0, 0))).ToArray();
        Fingerprint = Wire.Digest(JsonSerializer.Serialize(new { Version, Clock = clock.Fingerprint, Body = body.Fingerprint,
            Adapters = _adapters.Select(a => a.Fingerprint).ToArray() }));
    }
    private static double LocalX(int sensor) => sensor == 0 ? 0 : sensor == 1 ? -.0625 : .0625;
    internal ArticulatedHeadFrame? Last { get { Owner(); return _last; } }
    internal ArticulatedHeadFrame ReadAcknowledgedEndpoint()
    {
        Owner();
        if (Failure is not null) throw new InvalidOperationException("Preserve the failed head-input epoch; no reset or replay.");
        var reply = _body.ReadAcknowledgedForSensory(); AttemptedReplyJson = JsonSerializer.Serialize(reply, NativeArticulatedBody.Json);
        try
        {
            if (_body.Faulted) throw new InvalidOperationException("Cannot advance sensory input after an uncertain native command.");
            if (_last is not null && reply.physicsStep == _last.PhysicsStep)
            {
                if (reply.stateHash != _last.BodyStateSha256 || reply.observationsJson != _last.OriginalObservationsJson || reply.observationsHash != _last.ObservationsSha256)
                    throw new InvalidOperationException("Conflicting observation at the same physical endpoint.");
                return _last;
            }
            long next = _last is null ? 0 : checked(_last.PhysicsStep + 1);
            if (reply.physicsStep != next) throw new InvalidOperationException("Every original physical endpoint is required; no sensory gap filling.");
            var time = _clock.AtStep(next);
            if ((double)reply.physicalTimeMs != time.PhysicalMs) throw new InvalidOperationException("Native observation clock changed.");
            var observations = JsonSerializer.Deserialize<Wire.Observations>(reply.observationsJson, NativeArticulatedBody.Json)!;
            _partial.Clear();
            for (int i = 0; i < Sensors.Length; ++i)
            {
                string raw = observations.skullJson[i]; NativeArticulatedBody.RejectDuplicateKeys(raw);
                using var document = JsonDocument.Parse(raw); var s = document.RootElement;
                if (!s.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(SnapshotFields) ||
                    s.GetProperty("version").GetString() != "native-head-material-point-v2" || s.GetProperty("headBodyId").GetString() != "head" ||
                    s.GetProperty("sensorId").GetString() != Sensors[i] || s.GetProperty("physicsStep").GetInt64() != next)
                    throw new ArgumentException("Incomplete or foreign native material-point observation.");
                var hierarchy = s.GetProperty("hierarchy").EnumerateArray().ToArray();
                if (!hierarchy.Select(n => n.GetProperty("name").GetString()).SequenceEqual(new[] { "head", "chest", "pelvis" }) ||
                    hierarchy.Any(n => Vector(n.GetProperty("localScale")) != new AvatarSkullVector(1, 1, 1)))
                    throw new ArgumentException("Preserve the actual unit-scale head hierarchy.");
                // Redundant native COM/rotation/scale evidence stays in raw bytes.
                // The existing skull adapter owns basis, point and derivative checks.
                _ = Vector(s.GetProperty("centerOfMassWorldMeters")); _ = Vector(s.GetProperty("centerOfMassVelocityWorldMetersPerSecond"));
                _ = Vector(s.GetProperty("scale")); _ = s.GetProperty("legacyScaleGuardPassed").GetBoolean();
                foreach (string key in new[] { "x", "y", "z", "w" }) Finite(s.GetProperty("rotation").GetProperty(key).GetDouble());
                CheckNativeState(s, reply.stateJson);
                var observation = new AvatarSkullObservation(next, PhysicalSampleProtocol.Identity(_clock, next), "head", Sensors[i],
                    Vector(s.GetProperty("localPointMeters")), Vector(s.GetProperty("originWorldMeters")), Vector(s.GetProperty("rightWorld")),
                    Vector(s.GetProperty("upWorld")), Vector(s.GetProperty("forwardWorld")), Vector(s.GetProperty("pointWorldMeters")),
                    Vector(s.GetProperty("pointVelocityWorldMetersPerSecond")), Vector(s.GetProperty("angularVelocityWorldRadiansPerSecond")),
                    Vector(s.GetProperty("gravityWorldMetersPerSecondSquared")));
                var receipt = _adapters[i].Consume(observation);
                _partial.Add(new(Sensors[i], raw, Wire.Digest(raw), observation, receipt));
            }
            var frame = new ArticulatedHeadFrame(next, time.PhysicalMs, time.NeuralMs, reply.stateHash,
                reply.observationsJson, reply.observationsHash, Array.AsReadOnly(_partial.ToArray()));
            _last = frame; _partial.Clear(); return frame;
        }
        catch (Exception error) { Failure = error.ToString(); throw; }
    }
    private static AvatarSkullVector Vector(JsonElement value) => new(Finite(value.GetProperty("x").GetDouble()),
        Finite(value.GetProperty("y").GetDouble()), Finite(value.GetProperty("z").GetDouble()));
    private static void CheckNativeState(JsonElement skull, string stateJson)
    {
        using var state = JsonDocument.Parse(stateJson);
        var head = state.RootElement.GetProperty("links").EnumerateArray().Single(b => b.GetProperty("id").GetString() == "head");
        foreach (var pair in new[] { ("originWorldMeters", "positionBits"), ("rotation", "rotationBits"),
            ("angularVelocityWorldRadiansPerSecond", "angularVelocityBits"), ("centerOfMassVelocityWorldMetersPerSecond", "velocityBits") })
        {
            var exact = head.GetProperty(pair.Item2).EnumerateArray().ToArray(); var names = new[] { "x", "y", "z", "w" };
            for (int i = 0; i < exact.Length; ++i)
                if ((double)skull.GetProperty(pair.Item1).GetProperty(names[i]).GetSingle() != Nre.UnityWorld.MuscleHingeWire.Decode(exact[i].GetString()))
                    throw new ArgumentException("Head observation does not match the acknowledged native body state.");
        }
        var point = Vector(skull.GetProperty("pointWorldMeters")); var centre = Vector(skull.GetProperty("centerOfMassWorldMeters"));
        var omega = Vector(skull.GetProperty("angularVelocityWorldRadiansPerSecond")); var centreVelocity = Vector(skull.GetProperty("centerOfMassVelocityWorldMetersPerSecond"));
        var velocity = Vector(skull.GetProperty("pointVelocityWorldMetersPerSecond"));
        double x = point.X - centre.X, y = point.Y - centre.Y, z = point.Z - centre.Z;
        double[] expected = [centreVelocity.X + omega.Y*z - omega.Z*y, centreVelocity.Y + omega.Z*x - omega.X*z, centreVelocity.Z + omega.X*y - omega.Y*x];
        double[] actual = [velocity.X, velocity.Y, velocity.Z];
        // Same prospective float-observation bounds as native_skull_gate.py.
        double allowance = 1e-5 + 1e-6*expected.Max(Math.Abs);
        if (actual.Zip(expected, (a, e) => Math.Abs(a-e)).Max() > allowance)
            throw new ArgumentException("Native material-point velocity disagrees with its actual centre/rotation velocity.");
    }
    private static double Finite(double value) => double.IsFinite(value) ? value : throw new ArgumentException("Nonfinite native measurement.");
    private void Owner() { if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("Use the original physical owner's thread."); }
}
