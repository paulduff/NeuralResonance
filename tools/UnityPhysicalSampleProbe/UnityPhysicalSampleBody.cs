using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace UnityPhysicalSampleProbe;

public sealed record BridgeReply(int Version, string SessionId, string Operation, string Unity,
    string BodyJson, string[] ContactsJson, long Sequence, long PhysicsStep, double PhysicsStepMs,
    bool Paused, bool Isolated, bool Connected, bool Finite, bool MusclesEnabled, float PelvisHeightMeters,
    int Links, long BodyFramesAccepted, long RetinalFramesAccepted, long ContactFramesAccepted,
    bool DirectStepRejected, bool WrongOwnerRejected);

// An isolated physical adapter, not a production transport. The Unity main
// thread owns Physics.Simulate; immutable files retain every command/reply.
// Once a command outcome becomes uncertain, the adapter never retries a step.
public sealed class UnityPhysicalSampleBody : IAvatarPhysicalSampleBody
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true };
    private readonly string _directory, _session;
    private readonly Process _process;
    private long _sequence;
    private BridgeReply _lastStep;
    public bool ProtocolFaulted { get; private set; }
    public bool InjectNextCaptureFailure { get; set; }
    public int StepRequests { get; private set; }
    public List<(long Step, string ReplyFile)> StepSources { get; } = new();
    public long CompletedPhysicsStep => _lastStep.PhysicsStep;
    public double PhysicsStepMs => _lastStep.PhysicsStepMs;
    public BridgeReply LastStep => _lastStep;

    public UnityPhysicalSampleBody(string output, Guid session, Process process)
    {
        _directory = Path.Combine(output, "bridge"); _session = session.ToString("N"); _process = process;
        _lastStep = WaitForReply("hello.json", 180);
        Validate(_lastStep, "hello", 0, 0);
        StepSources.Add((0, "bridge/hello.json"));
    }

    public void AdvanceOneStep()
    {
        ++StepRequests;
        _lastStep = Command("step", checked(CompletedPhysicsStep + 1));
        StepSources.Add((CompletedPhysicsStep, "bridge/reply-" + _sequence.ToString("D6") + ".json"));
    }

    public PhysicalBodyFrameRequest CaptureObservation()
    {
        if (InjectNextCaptureFailure)
        {
            InjectNextCaptureFailure = false;
            throw new InvalidOperationException("Injected post-movement capture failure");
        }
        return DecodeBody(_lastStep);
    }

    public BridgeReply Inspect() => Command("inspect", CompletedPhysicsStep);
    public void Stop() => Command("stop", CompletedPhysicsStep);
    public static PhysicalBodyFrameRequest DecodeBody(BridgeReply reply) =>
        JsonSerializer.Deserialize<PhysicalBodyFrameRequest>(reply.BodyJson, Json)
        ?? throw new InvalidOperationException("Missing Unity body observation.");

    public static string BodyWithoutWallStamp(BridgeReply reply) =>
        JsonSerializer.Serialize(DecodeBody(reply) with { TimestampMs = 0 }, Json);

    private BridgeReply Command(string operation, long expectedResultStep)
    {
        if (ProtocolFaulted) throw new InvalidOperationException("Uncertain bridge cannot issue another command.");
        try
        {
            long sequence = checked(_sequence + 1);
            var request = new { version = 1, sessionId = _session, operation, sequence, expectedStep = CompletedPhysicsStep };
            WriteNew(Path.Combine(_directory, "request-" + sequence.ToString("D6") + ".json"), request);
            var reply = WaitForReply("reply-" + sequence.ToString("D6") + ".json", 30);
            Validate(reply, operation, sequence, expectedResultStep);
            _sequence = sequence;
            return reply;
        }
        catch { ProtocolFaulted = true; throw; }
    }

    private BridgeReply WaitForReply(string name, int seconds)
    {
        var elapsed = Stopwatch.StartNew(); string path = Path.Combine(_directory, name);
        while (!File.Exists(path))
        {
            string failure = Path.Combine(_directory, "failure.json");
            if (File.Exists(failure)) throw new InvalidOperationException("Unity bridge failed: " + File.ReadAllText(failure));
            if (_process.HasExited) throw new InvalidOperationException("Unity exited before its reply, code " + _process.ExitCode);
            if (elapsed.Elapsed.TotalSeconds > seconds) throw new TimeoutException("Uncertain Unity request: " + name);
            Thread.Sleep(10);
        }
        return JsonSerializer.Deserialize<BridgeReply>(File.ReadAllText(path), Json)
            ?? throw new InvalidOperationException("Missing Unity bridge reply.");
    }

    private void Validate(BridgeReply reply, string operation, long sequence, long step)
    {
        if (reply.Version != 1 || reply.SessionId != _session || reply.Sequence != sequence || reply.Operation != operation ||
            reply.PhysicsStep != step || reply.PhysicsStepMs != (double).01f * 1000 ||
            !reply.Paused || !reply.Isolated || reply.Connected || !reply.Finite || !reply.MusclesEnabled ||
            reply.BodyFramesAccepted != 0 || reply.RetinalFramesAccepted != 0 || reply.ContactFramesAccepted != 0 ||
            !reply.DirectStepRejected || !reply.WrongOwnerRejected || reply.Links != 15 || !float.IsFinite(reply.PelvisHeightMeters))
            throw new InvalidOperationException("Unity source identity, clock, ownership, state or isolation differs.");
        var body = DecodeBody(reply);
        if (body.Sequence != step || body.InputSource != "avatar_unity_body" || body.Articulation?.Musculoskeletal?.Muscles.Count != 38)
            throw new InvalidOperationException("Incomplete or misidentified actual body observation.");
        // Strict typed parsing rejects unknown wire fields; serializer rejects
        // nonfinite nested numbers. No legacy descriptor normalization is used.
        _ = JsonSerializer.Serialize(body, Json);
        if (reply.ContactsJson is null || reply.ContactsJson.Length != reply.Links)
            throw new InvalidOperationException("Missing per-link contact observations.");
        var contacts = reply.ContactsJson.Select(s => JsonSerializer.Deserialize<SomaticContactFrameRequest>(s, Json)
            ?? throw new InvalidOperationException("Missing contact.")).ToArray();
        if (contacts.Any(c => c.Sequence != step) || contacts.Select(c => c.InputSource).Distinct().Count() != reply.Links)
            throw new InvalidOperationException("Contact grid identity differs.");
        _ = JsonSerializer.Serialize(contacts, Json);
    }

    public static void WriteNew<T>(string path, T value)
    {
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(stream, value, Json);
        File.Move(temporary, path); // Atomic completed file; never overwrite evidence.
    }
}
