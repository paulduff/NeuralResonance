#nullable enable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using NRE.SimAvatar;
using NeuralResonanceEngine.Shared.Contracts;
using Nre.UnityWorld;
using static Nre.UnityWorld.ArticulatedTendonWire;
using static Nre.UnityWorld.MuscleHingeWire;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;
using Request = Nre.UnityWorld.ArticulatedTendonWire.Request;
using Reply = Nre.UnityWorld.ArticulatedTendonWire.Reply;
using Path = System.IO.Path;

internal interface IArticulatedTendonExchange
{
    Reply ReadHello();
    void Publish(Request request);
    Reply ReadReply(long sequence);
}

// The real physical boundary consumed directly by AvatarCausalMotorPlant.
// It never drives neurons, computes a replacement force, or retries a command.
internal sealed class NativeArticulatedBody : IAvatarMotorMechanicalBody
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { IncludeFields = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static readonly string[] Bodies = ["chest", "head", "left_arm", "left_foot", "left_forearm", "left_hand", "left_shin", "left_thigh",
        "pelvis", "right_arm", "right_foot", "right_forearm", "right_hand", "right_shin", "right_thigh"];
    private readonly Configuration _configuration;
    private readonly string _configurationHash;
    private readonly IArticulatedTendonExchange _exchange;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly HashSet<Guid> _efforts = [];
    private Reply _last;
    private bool _faulted, _stopped;
    public string Fingerprint => _last.bodyFingerprint;
    public long CompletedPhysicsStep => _last.physicsStep;
    public double PhysicsStepMs { get; }
    internal bool Faulted => _faulted;
    internal Reply Last => Copy(_last);
    internal Reply ReadAcknowledgedForSensory()
    {
        Require(Environment.CurrentManagedThreadId == _owner, "The physical owner must hand off its own acknowledged observation.");
        return Copy(_last);
    }
    internal string OriginalObservationJson => _last.observationsJson;
    internal string? InitializationEvidenceJson { get; private set; }

    internal CausalMotorAttachment InitializePassiveInterval(PhysicalSampleClock originalClock)
    {
        CausalMotorAttachment? attachment = null;
        Execute(() =>
        {
            ArgumentNullException.ThrowIfNull(originalClock); originalClock.Validate();
            Require(_configuration.passivePreludeSteps == 1 && _last.physicsStep == 0 && _last.plantFingerprint == "" &&
                originalClock.SessionId.ToString("N") == _configuration.sessionId && originalClock.InitialPhysicsStep == 0 &&
                originalClock.PhysicalEpochMs == 0 && originalClock.PhysicsStepMs == PhysicsStepMs,
                "Passive initialization requires the original physical clock and a fresh unbound body.");
            var before = _last;
            var request = NewRequest("prelude"); request.endTimeMs = PhysicsStepMs;
            var after = Send(request);
            var frame = Frame(after);
            string evidence = JsonSerializer.Serialize(new
                { Protocol, OriginalClock = originalClock, ClockFingerprint = originalClock.Fingerprint, Before = before, Request = request, After = after }, Json);
            attachment = new(originalClock, frame, Wire.Digest(evidence));
            InitializationEvidenceJson = evidence;
        });
        return attachment ?? throw new InvalidOperationException("Missing passive attachment receipt.");
    }

    internal NativeArticulatedBody(string configurationJson, IArticulatedTendonExchange exchange)
    {
        RejectDuplicateKeys(configurationJson);
        _configuration = JsonSerializer.Deserialize<Configuration>(configurationJson, Json) ?? throw new ArgumentException("Missing declarations.");
        PhysicsStepMs = StepMs(_configuration); _configurationHash = Wire.Digest(configurationJson);
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _last = Copy(exchange.ReadHello());
        ValidateReceipt(_last);
        Require(_last.operation == "hello" && _last.sequence == 0 && _last.physicsStep == 0 && _last.plantFingerprint == "" &&
            _last.lastAppliedEffortId == "" && _last.applicationsJson == "", "Require a fresh, unevolved physical body.");
    }
    internal void Bind(string plantFingerprint) => Execute(() => { var r = NewRequest("bind"); r.plantFingerprint = plantFingerprint; Send(r); });
    internal void Stop() => Execute(() => { Send(NewRequest("stop")); _stopped = true; });
    public MotorMechanicalFrame CaptureGeometry()
    {
        MotorMechanicalFrame? result = null;
        Execute(() =>
        {
            var r = Send(NewRequest("inspect"));
            result = Frame(r);
        });
        return result ?? throw new InvalidOperationException("Missing physical receipt.");
    }
    private static MotorMechanicalFrame Frame(Reply r) => new(Guid.ParseExact(r.sessionId, "N"), r.bodyFingerprint, r.physicsStep, r.physicalTimeMs,
        r.lastAppliedEffortId == "" ? Guid.Empty : Guid.ParseExact(r.lastAppliedEffortId, "N"),
        Array.AsReadOnly(r.paths.Select(p => new MusculotendonPathMeasurement(p.muscleId, p.lengthMeters, p.velocityMetersPerSecond)).ToArray()));
    public void AdvanceOneStep(MotorMechanicalEffort effort) => Execute(() =>
    {
        Require(effort != null && effort.SessionId.ToString("N") == _configuration.sessionId && effort.BodyFingerprint == Fingerprint &&
            effort.PlantFingerprint == _last.plantFingerprint && effort.StartStep == CompletedPhysicsStep && effort.StartTimeMs == _last.physicalTimeMs &&
            effort.EndTimeMs == checked(CompletedPhysicsStep + 1) * PhysicsStepMs && effort.EffortId != Guid.Empty && !_efforts.Contains(effort.EffortId) &&
            effort.Tensions != null && effort.Tensions.All(t => t != null), "Effort must belong to the current body, plant and exact physical interval.");
        var r = NewRequest("step"); r.endTimeMs = effort.EndTimeMs; r.effortId = effort.EffortId.ToString("N");
        r.tensions = effort.Tensions.Select(t => new Tension { muscleId = t.MuscleId, axialNewtons = t.AxialNewtons }).ToArray();
        Wire.Validate(r, _last, _configuration, _configurationHash);
        _efforts.Add(effort.EffortId); // Includes a potentially executed but unacknowledged command.
        Send(r);
    });
    private void Execute(Action operation)
    {
        Require(!_faulted && !_stopped && Environment.CurrentManagedThreadId == _owner, "One owner; stopped or uncertain physical epochs cannot resume.");
        try { operation(); } catch { _faulted = true; throw; }
    }
    private Request NewRequest(string operation) => new() { protocol = Protocol, sessionId = _configuration.sessionId,
        configurationHash = _configurationHash, operation = operation, sequence = checked(_last.sequence + 1), expectedStep = _last.physicsStep,
        bodyFingerprint = _last.bodyFingerprint, plantFingerprint = _last.plantFingerprint, stateHash = _last.stateHash,
        effortId = "", startTimeMs = _last.physicalTimeMs, endTimeMs = _last.physicalTimeMs, tensions = [] };
    private Reply Send(Request request)
    {
        Wire.Validate(request, _last, _configuration, _configurationHash);
        _exchange.Publish(request);
        var reply = Copy(_exchange.ReadReply(request.sequence));
        ValidateReceipt(reply);
        ValidateTransition(_last, request, reply, _configuration, _configurationHash);
        _last = reply; return reply;
    }
    private void ValidateReceipt(Reply r)
    {
        Wire.Validate(r, _configuration, _configurationHash);
        using var geometry = Parse(r.geometryJson);
        Require(geometry.RootElement.GetProperty("links").EnumerateArray().Select(l => l.GetProperty("id").GetString()).SequenceEqual(Bodies),
            "Full physical geometry must retain all fifteen canonical links.");
        float expectedFriction = _configuration.mechanicsMode == "original" ? .05f : 0f;
        Require(geometry.RootElement.GetProperty("links").EnumerateArray().All(l => l.GetProperty("jointFriction").GetSingle() == expectedFriction),
            "Actual joint friction differs from the explicit reviewed body setting.");
        using var mechanics = Parse(r.mechanicsDiagnosticJson);
        Require(mechanics.RootElement.GetProperty("mode").GetString() == _configuration.mechanicsMode &&
            !mechanics.RootElement.GetProperty("liveConfigurationChanged").GetBoolean() &&
            !mechanics.RootElement.GetProperty("physiologicalCalibrationQualified").GetBoolean(), "Missing isolated mechanical provenance.");
        using var state = Parse(r.stateJson);
        Require(state.RootElement.GetProperty("physicsStep").GetInt64() == r.physicsStep &&
            state.RootElement.GetProperty("links").EnumerateArray().Select(l => l.GetProperty("id").GetString()).SequenceEqual(Bodies), "Wrong native body endpoint.");
        foreach (var link in state.RootElement.GetProperty("links").EnumerateArray())
        {
            foreach (var field in new[] { ("positionBits", 3), ("rotationBits", 4), ("velocityBits", 3), ("angularVelocityBits", 3) })
                Require(Bits(link.GetProperty(field.Item1)) == field.Item2, "Incomplete exact native pose.");
            int dofs = Bits(link.GetProperty("jointPositionBits"));
            Require(dofs <= 3 && Bits(link.GetProperty("jointVelocityBits")) == dofs && Bits(link.GetProperty("jointForceBits")) == dofs, "Incomplete native joint state.");
        }
        RejectDuplicateKeys(r.observationsJson);
        var observations = JsonSerializer.Deserialize<Observations>(r.observationsJson, Json);
        Require(observations != null && observations.physicsStep == r.physicsStep && observations.contactsJson?.Length == Bodies.Length &&
            observations.skullJson?.Length == 3, "Incomplete body/contact/head observation.");
        using var body = Parse(observations.bodyJson);
        Require(body.RootElement.GetProperty("sequence").GetInt64() == r.physicsStep &&
            body.RootElement.GetProperty("inputSource").GetString() == "avatar_unity_body", "Body measurement belongs to another physical step.");
        for (int i = 0; i < Bodies.Length; i++)
        {
            using var contact = Parse(observations.contactsJson[i]);
            Require(contact.RootElement.GetProperty("sequence").GetInt64() == r.physicsStep &&
                contact.RootElement.GetProperty("inputSource").GetString() == "avatar_unity_" + Bodies[i] + "_contact", "Missing/reordered physical contact.");
        }
        string[] sensors = ["centre", "left", "right"];
        for (int i = 0; i < sensors.Length; i++)
        {
            using var skull = Parse(observations.skullJson[i]);
            Require(skull.RootElement.GetProperty("physicsStep").GetInt64() == r.physicsStep &&
                skull.RootElement.GetProperty("sensorId").GetString() == sensors[i] && skull.RootElement.GetProperty("headBodyId").GetString() == "head",
                "Skull measurement belongs to another sensor or endpoint.");
        }
        if (r.physicsStep > _configuration.passivePreludeSteps) Require(!string.IsNullOrEmpty(r.applicationsJson), "Missing actual point-force applications.");
    }
    private static int Bits(JsonElement array)
    { foreach (var v in array.EnumerateArray()) _ = Decode(v.GetString()); return array.GetArrayLength(); }
    private static JsonDocument Parse(string json)
    { RejectDuplicateKeys(json); return JsonDocument.Parse(json); }
    internal static void RejectDuplicateKeys(string json)
    {
        using var document = JsonDocument.Parse(json); Walk(document.RootElement);
        static void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in e.EnumerateObject()) { Require(names.Add(p.Name), "Duplicate JSON property."); Walk(p.Value); }
            }
            else if (e.ValueKind == JsonValueKind.Array) foreach (var child in e.EnumerateArray()) Walk(child);
            else if (e.ValueKind == JsonValueKind.Number) Require(double.IsFinite(e.GetDouble()), "Nonfinite physical evidence.");
        }
    }
    private static Reply Copy(Reply r) => JsonSerializer.Deserialize<Reply>(JsonSerializer.Serialize(r, Json), Json)
        ?? throw new InvalidOperationException("Missing physical receipt.");
    private static void Require([DoesNotReturnIf(false)] bool condition, string message) => MuscleHingeWire.Require(condition, message);
}

internal sealed class FileArticulatedTendonExchange : IArticulatedTendonExchange
{
    readonly string _directory;
    readonly Process _process;
    internal FileArticulatedTendonExchange(string directory, Process process) { _directory = directory; _process = process; }
    public Reply ReadHello() => Read("hello.json", 180);
    public Reply ReadReply(long sequence) => Read("reply-" + sequence.ToString("D6") + ".json", 30);
    public void Publish(Request request)
    {
        string path = Path.Combine(_directory, "request-" + request.sequence.ToString("D6") + ".json"), temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(stream, request, NativeArticulatedBody.Json);
        File.Move(temporary, path);
    }
    private Reply Read(string name, int seconds)
    {
        var timer = Stopwatch.StartNew(); string path = Path.Combine(_directory, name);
        while (true)
        {
            var outcome = DiagnosticBridgeFiles.TryRead(path, out string text);
            if (outcome == BridgeReadOutcome.Ready)
            {
                NativeArticulatedBody.RejectDuplicateKeys(text);
                return JsonSerializer.Deserialize<Reply>(text, NativeArticulatedBody.Json) ?? throw new InvalidOperationException("Empty native receipt.");
            }
            var failure = DiagnosticBridgeFiles.TryRead(Path.Combine(_directory, "failure.json"), out string error);
            if (failure == BridgeReadOutcome.Ready) throw new InvalidOperationException("Native full-body bridge failed: " + error);
            if (_process.HasExited && outcome == BridgeReadOutcome.Missing && failure == BridgeReadOutcome.Missing)
                throw new InvalidOperationException("Native player exited without the expected receipt: " + _process.ExitCode);
            if (timer.Elapsed.TotalSeconds >= seconds) throw new TimeoutException("Uncertain native receipt; command was not resent: " + name);
            Thread.Sleep(2); // Read the SAME immutable file, including after a sharing violation.
        }
    }
}
