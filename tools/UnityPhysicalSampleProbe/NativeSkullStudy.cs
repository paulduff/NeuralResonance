using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace UnityPhysicalSampleProbe;

// Only called by the isolated visible-window probe. The native sidecar is
// evidence, not a live transport. Every immutable original remains on disk.
internal sealed class NativeSkullStudy
{
    internal sealed record Quaternion(double X, double Y, double Z, double W);
    internal sealed record ScaleNode(string Name, AvatarSkullVector LocalScale);
    internal sealed record Snapshot(string Version, string HeadBodyId, string SensorId, long PhysicsStep,
        AvatarSkullVector LocalPointMeters, AvatarSkullVector OriginWorldMeters,
        AvatarSkullVector RightWorld, AvatarSkullVector UpWorld, AvatarSkullVector ForwardWorld,
        AvatarSkullVector PointWorldMeters, AvatarSkullVector PointVelocityWorldMetersPerSecond,
        AvatarSkullVector AngularVelocityWorldRadiansPerSecond, AvatarSkullVector GravityWorldMetersPerSecondSquared,
        AvatarSkullVector CenterOfMassWorldMeters, AvatarSkullVector CenterOfMassVelocityWorldMetersPerSecond,
        Quaternion Rotation, AvatarSkullVector Scale, ScaleNode[] Hierarchy, bool LegacyScaleGuardPassed);
    internal sealed record Sidecar(string Version, string SessionId, string Operation, long Sequence, long PhysicsStep, Snapshot[] Samples);
    private readonly string _output;
    private readonly PhysicalSampleClock _clock;
    private readonly Dictionary<string, AvatarSkullKinematics> _adapters = new();
    private readonly List<object> _receipts = new();
    public int Endpoints { get; private set; }

    public NativeSkullStudy(string output, PhysicalSampleClock clock)
    {
        _output = output; _clock = clock;
        foreach (var (name, x) in new[] { ("centre", 0.0), ("left", -.0625), ("right", .0625) })
            _adapters.Add(name, new AvatarSkullKinematics(clock, "head", name, new(x, 0, 0)));
    }

    public void Capture(BridgeReply body)
    {
        string relative = "bridge/skull-" + body.Sequence.ToString("D6") + ".json";
        string raw = File.ReadAllText(Path.Combine(_output, relative));
        var source = JsonSerializer.Deserialize<Sidecar>(raw, UnityPhysicalSampleBody.Json)
            ?? throw new InvalidOperationException("Missing actual head sample.");
        if (source.Version != "native-skull-sidecar-v1" || source.SessionId != _clock.SessionId.ToString("N") ||
            source.Sequence != body.Sequence || source.Operation != body.Operation || source.PhysicsStep != body.PhysicsStep ||
            source.PhysicsStep != Endpoints || source.Samples is null || source.Samples.Length != 3 ||
            !source.Samples.Select(s => s.SensorId).SequenceEqual(new[] { "centre", "left", "right" }))
            throw new InvalidOperationException("Native head sidecar identity or endpoint differs.");
        foreach (var point in source.Samples)
        {
            if (point.Version != "native-head-material-point-v2" || point.PhysicsStep != body.PhysicsStep ||
                point.Hierarchy is null || !point.Hierarchy.Select(n => n.Name).SequenceEqual(new[] { "head", "chest", "pelvis" }) ||
                point.Hierarchy.Any(n => n.LocalScale != new AvatarSkullVector(1, 1, 1)))
                throw new InvalidOperationException("Unexpected native material point.");
            var observation = new AvatarSkullObservation(point.PhysicsStep, PhysicalSampleProtocol.Identity(_clock, point.PhysicsStep),
                point.HeadBodyId, point.SensorId, point.LocalPointMeters, point.OriginWorldMeters,
                point.RightWorld, point.UpWorld, point.ForwardWorld, point.PointWorldMeters,
                point.PointVelocityWorldMetersPerSecond, point.AngularVelocityWorldRadiansPerSecond, point.GravityWorldMetersPerSecondSquared);
            var adapter = _adapters[point.SensorId]; var receipt = adapter.Consume(observation);
            if (!ReferenceEquals(adapter.Consume(observation), receipt)) throw new InvalidOperationException("Head retry mutated the receipt.");
            // Retain both the native bytes' hash and the typed adapter receipt.
            // Native coordinates/point-velocity are checked independently in Python.
            _receipts.Add(new { sourceFile = relative, sourceSha256 = PhysicalSampleProtocol.Digest(raw),
                sensorId = point.SensorId, adapterFingerprint = adapter.Fingerprint, receipt });
        }
        ++Endpoints;
        // Persist each endpoint before any next step, including on later failure.
        UnityPhysicalSampleBody.WriteNew(Path.Combine(_output, "skull-receipts-" + body.PhysicsStep.ToString("D6") + ".json"),
            _receipts.TakeLast(3).ToArray());
    }

    public void Complete()
    {
        if (Endpoints != 201 || _receipts.Count != 603 || _adapters.Values.Any(a => a.Faulted))
            throw new InvalidOperationException("Incomplete native head capture.");
    }
}
