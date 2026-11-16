using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NRE.SimAvatar;
using static Nre.UnityWorld.MuscleHingeWire;
using Path=System.IO.Path;

internal sealed class NativeHingeBody : IAvatarMotorMechanicalBody
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { IncludeFields = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly string _directory, _session;
    private readonly Process _process;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private Reply _last;
    private bool _faulted;
    private readonly string _geometry, _fingerprint;
    private readonly string[] _paths;
    internal Reply Last => Clone(_last);
    public string Fingerprint => _fingerprint;
    public long CompletedPhysicsStep => _last.physicsStep;
    public double PhysicsStepMs => StepMs;
    internal bool Faulted => _faulted;
    internal NativeHingeBody(string directory, Guid session, Process process)
    {
        _directory = directory; _session = session.ToString("N"); _process = process;
        _last = Wait("hello.json", 180);
        Check(_last, "hello", 0, 0, "", "");
        _geometry = _last.geometryJson; _fingerprint = _last.bodyFingerprint;
        _paths = _last.paths.Select(p => p.pathFingerprint).ToArray();
    }
    internal void Bind(string fingerprint)
    {
        var request = RequestFor("bind"); request.plantFingerprint = fingerprint; Send(request);
    }
    public MotorMechanicalFrame CaptureGeometry()
    {
        var prior = _last; var next = Send(RequestFor("inspect"));
        Require(prior.stateHash == next.stateHash && JsonSerializer.Serialize(prior.paths, Json) == JsonSerializer.Serialize(next.paths, Json),
            "Read-only capture changed native state.");
        return new(Guid.ParseExact(_session, "N"), Fingerprint, next.physicsStep, next.physicalTimeMs,
            string.IsNullOrEmpty(next.lastAppliedEffortId) ? Guid.Empty : Guid.ParseExact(next.lastAppliedEffortId, "N"),
            Array.AsReadOnly(next.paths.Select(p => new MusculotendonPathMeasurement(p.muscleId, p.lengthMeters, p.velocityMetersPerSecond)).ToArray()));
    }
    public void AdvanceOneStep(MotorMechanicalEffort effort)
    {
        Require(effort != null && effort.SessionId.ToString("N") == _session && effort.BodyFingerprint == Fingerprint &&
            effort.PlantFingerprint == _last.plantFingerprint && effort.StartStep == CompletedPhysicsStep &&
            effort.StartTimeMs == _last.physicalTimeMs && effort.EndTimeMs == (CompletedPhysicsStep + 1) * StepMs,
            "Effort belongs to a different body/clock/plant.");
        var request = RequestFor("step"); request.effortId = effort.EffortId.ToString("N"); request.endTimeMs = effort.EndTimeMs;
        request.tensions = effort.Tensions.Select(t => new Tension { muscleId = t.MuscleId, axialNewtons = t.AxialNewtons }).ToArray();
        Send(request);
    }
    internal void Stop() => Send(RequestFor("stop"));
    private Request RequestFor(string operation) => new()
    {
        version = Nre.UnityWorld.MuscleHingeWire.Version, sessionId = _session, operation = operation, sequence = checked(_last.sequence + 1),
        expectedStep = _last.physicsStep, bodyFingerprint = _last.bodyFingerprint, plantFingerprint = _last.plantFingerprint,
        stateHash = _last.stateHash, startTimeMs = _last.physicalTimeMs, endTimeMs = _last.physicalTimeMs,
        effortId = "", tensions = []
    };
    private Reply Send(Request request)
    {
        Require(!_faulted && Environment.CurrentManagedThreadId == _owner, "Bridge has one owner and never retries an uncertain command.");
        try
        {
            Validate(request, _last);
            Write(Path.Combine(_directory, "request-" + request.sequence.ToString("D6") + ".json"), request);
            var reply = Wait("reply-" + request.sequence.ToString("D6") + ".json", 30);
            string effort = request.operation == "step" ? request.effortId : _last.lastAppliedEffortId;
            long step = _last.physicsStep + (request.operation == "step" ? 1 : 0);
            Check(reply, request.operation, request.sequence, step, request.plantFingerprint, effort);
            Require(reply.bodyFingerprint == _fingerprint && reply.geometryJson == _geometry &&
                reply.paths.Select(p => p.pathFingerprint).SequenceEqual(_paths), "Native geometry identity changed.");
            if (request.operation != "step") Require(reply.stateHash == _last.stateHash, "Nonmoving command changed the native body.");
            _last = reply; return reply;
        }
        catch { _faulted = true; throw; }
    }
    // Deliberately bypass HOST validation to exercise the actual native rejection
    // boundary. This ends the isolated epoch and cannot be used to resume it.
    internal Failure RejectNative(string fault)
    {
        Require(!_faulted && Environment.CurrentManagedThreadId == _owner, "Invalid rejection control owner.");
        var request = RequestFor("step"); request.endTimeMs = StepMs; request.effortId = Guid.NewGuid().ToString("N");
        request.tensions = [new() { muscleId = "m0", axialNewtons = 1 }, new() { muscleId = "m1", axialNewtons = 0 }];
        switch (fault)
        {
            case "session": request.sessionId = Guid.NewGuid().ToString("N"); break;
            case "sequence": request.sequence += 1; break;
            case "clock": request.endTimeMs = .125; break;
            case "state": request.stateHash = new string('0', 64); break;
            case "plant": request.plantFingerprint = new string('0', 64); break;
            case "duplicate": request.tensions[1].muscleId = "m0"; break;
            case "negative": request.tensions[0].axialNewtons = -1; break;
            case "inspect-force": request.operation = "inspect"; break;
            default: throw new ArgumentException("Unknown negative control.");
        }
        _faulted = true;
        // Filename remains the next sequence, including the wrong-sequence payload.
        Write(Path.Combine(_directory, "request-" + (_last.sequence + 1).ToString("D6") + ".json"), request);
        if (!_process.WaitForExit(30000)) throw new TimeoutException("Native negative control did not terminate.");
        Require(_process.ExitCode == 1, "Malformed native command unexpectedly succeeded.");
        var failure = JsonSerializer.Deserialize<Failure>(File.ReadAllText(Path.Combine(_directory, "failure.json")), Json);
        Require(failure != null && failure.sessionId == _session && failure.sequence == _last.sequence &&
            failure.physicsStep == 0 && !failure.physicsAttempted && !string.IsNullOrWhiteSpace(failure.error) &&
            failure.observed != null && failure.observed.stateHash == _last.stateHash && failure.observed.physicsStep == 0,
            "Invalid command moved or failed to preserve the native state.");
        return failure;
    }
    private Reply Wait(string name, int seconds)
    {
        string path = Path.Combine(_directory, name); var timer = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            string failure = Path.Combine(_directory, "failure.json");
            if (File.Exists(failure)) throw new InvalidOperationException(File.ReadAllText(failure));
            if (_process.HasExited) throw new InvalidOperationException("Native player exited: " + _process.ExitCode);
            if (timer.Elapsed.TotalSeconds > seconds) throw new TimeoutException("Uncertain native reply: " + name);
            Thread.Sleep(2);
        }
        return JsonSerializer.Deserialize<Reply>(File.ReadAllText(path), Json) ?? throw new InvalidOperationException("Missing reply.");
    }
    private void Check(Reply r, string operation, long sequence, long step, string plant, string effort)
    {
        Require(r != null && r.version == Nre.UnityWorld.MuscleHingeWire.Version && r.sessionId == _session && r.unity == "6000.3.19f1" &&
            r.operation == operation && r.sequence == sequence && r.physicsStep == step && r.physicalTimeMs == step * StepMs &&
            r.physicsStepMs == StepMs && r.isolated && r.paused && r.plantFingerprint == plant && r.lastAppliedEffortId == effort &&
            !string.IsNullOrWhiteSpace(r.geometryJson) && !string.IsNullOrWhiteSpace(r.stateJson) &&
            r.bodyFingerprint == Digest(r.geometryJson) && r.stateHash == Digest(r.stateJson), "Invalid native state/clock/effort receipt.");
        Require(r.paths != null && r.paths.Length == 2 && r.paths[0]?.muscleId == "m0" && r.paths[1]?.muscleId == "m1" &&
            r.paths.All(p => Hash(p.pathFingerprint) && Finite(p.lengthMeters) && p.lengthMeters > 0 && Finite(p.velocityMetersPerSecond)),
            "Incomplete or invalid native paths.");
        using var state = JsonDocument.Parse(r.stateJson);
        Require(double.IsFinite(state.RootElement.GetProperty("angle").GetDouble()) &&
            double.IsFinite(state.RootElement.GetProperty("speed").GetDouble()), "Nonfinite native joint state.");
    }
    internal static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static Reply Clone(Reply value) => JsonSerializer.Deserialize<Reply>(JsonSerializer.Serialize(value, Json), Json);
    internal static void Write<T>(string path, T value)
    {
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) JsonSerializer.Serialize(stream, value, Json);
        File.Move(temporary, path);
    }
}
