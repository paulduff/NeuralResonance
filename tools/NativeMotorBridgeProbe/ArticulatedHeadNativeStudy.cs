#nullable enable
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static Nre.UnityWorld.MuscleHingeWire;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;
using Path = System.IO.Path;

// Actual full-body file bridge -> original Avatar skull adapter. Forces are
// explicitly prescribed fixtures; there is no neuronal or muscle-cell source.
internal static class ArticulatedHeadNativeStudy
{
    internal const string Version = "native-articulated-head-study-v1";
    private const int FinalStep = 8;
    private sealed record Condition(string Name, string Muscle, int InspectionsPerEndpoint);
    private static readonly Condition[] Cases = [new("quiet", "", 0), new("hip", "leftHip/flexor", 0),
        new("shoulder", "leftShoulderAbduction/flexor", 0), new("quiet-inspected", "", 2),
        new("hip-inspected", "leftHip/flexor", 2), new("shoulder-inspected", "leftShoulderAbduction/flexor", 2)];

    internal static void Run(string player, string originalDirectory, string output)
    {
        Require(new[] { player, originalDirectory, output }.All(Path.IsPathFullyQualified) &&
            File.Exists(player) && Directory.Exists(originalDirectory) && !Directory.Exists(output),
            "Require an existing isolated player/original evidence and a new absolute output directory.");
        string reportText = File.ReadAllText(Path.Combine(originalDirectory, "result.json"));
        using var original = JsonDocument.Parse(reportText);
        var report = original.RootElement;
        Require(report.GetProperty("completed").GetBoolean() && report.GetProperty("passed").GetBoolean() &&
            report.GetProperty("condition").GetString() == "quiet" && report.GetProperty("finalStep").GetInt64() == FinalStep &&
            report.GetProperty("paths").GetInt32() == 38 && Decode(report.GetProperty("stepMsBits").GetString()) == (double).005f * 1000,
            "Use the completed original quiet geometry and exact native rate.");
        using var geometry = JsonDocument.Parse(report.GetProperty("geometryJson").GetString()!);
        string declarationsText = geometry.RootElement.GetProperty("declarationsJson").GetString()!;
        using var declarations = JsonDocument.Parse(declarationsText);
        Directory.CreateDirectory(output);
        Write(Path.Combine(output, "protocol.json"), new { Version, Cases, FinalStep,
            OriginalReportSha256 = Wire.Digest(reportText), OriginalDeclarationsJson = declarationsText,
            PrescribedTensionNewtons = 40, PassivePreludeSteps = 1, OriginalSettingsRetained = true,
            NativePointAndDerivativeLimits = "native_skull_gate.py: original bounds",
            NeuronalSourceExecuted = false, MuscleCellExecuted = false, PhysiologyQualified = false, AvatarBalanceQualified = false });
        foreach (var condition in Cases) RunCase(player, output, declarations.RootElement, condition);
        Write(Path.Combine(output, "result.json"), new { Version, Completed = true, NativeCases = Cases.Length,
            ActualPhysicsSteps = Cases.Length * FinalStep, PhysicalEndpoints = Cases.Length * (FinalStep + 1),
            HeadReceipts = Cases.Length * (FinalStep + 1) * 3, ExtraInspections = Cases.Sum(c => c.InspectionsPerEndpoint) * (FinalStep + 1),
            IndependentSavedComparisonRequired = true, NeuronalSourceExecuted = false, MuscleCellExecuted = false,
            PhysiologyQualified = false, AvatarBalanceQualified = false });
    }

    private static void RunCase(string player, string output, JsonElement originalPaths, Condition condition)
    {
        Console.WriteLine("ACTUAL FULL AVATAR HEAD INPUT " + condition.Name);
        string directory = Path.Combine(output, condition.Name); Directory.CreateDirectory(directory);
        Guid session = Guid.NewGuid();
        var config = new Wire.Configuration { protocol = Wire.Protocol, sessionId = session.ToString("N"),
            stepSeconds = (double).005f, passivePreludeSteps = 1,
            declarations = originalPaths.GetProperty("paths").EnumerateArray().Select(p => new Wire.Declaration {
                muscleId = p.GetProperty("id").GetString()!, provenance = p.GetProperty("provenance").GetString()!,
                anchors = p.GetProperty("anchors").EnumerateArray().Select(a => new Wire.Anchor {
                    id = a.GetProperty("id").GetString()!, bodyId = a.GetProperty("bodyId").GetString()!,
                    x = Decode(a.GetProperty("xBits").GetString()), y = Decode(a.GetProperty("yBits").GetString()),
                    z = Decode(a.GetProperty("zBits").GetString()) }).ToArray() }).ToArray() };
        Wire.Validate(config);
        Require(condition.Muscle == "" || config.declarations.Any(p => p.muscleId == condition.Muscle), "Missing declared path.");
        string configPath = Path.Combine(directory, "input.json"); Write(configPath, config);
        string configurationJson = File.ReadAllText(configPath);
        var start = new ProcessStartInfo(player) { UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(player) };
        foreach (string argument in new[] { "-batchmode", "-nographics", "-logFile", Path.Combine(directory, "player.log"),
            "--neuronal-muscle-qualify", "1", "--tendon-bridge", session.ToString("N"), "--tendon-config", configPath,
            "--output", directory }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Full-body player did not start.");
        NativeArticulatedBody? body = null; NativeArticulatedHeadInput? head = null;
        int endpoints = 0, inspections = 0;
        using var acknowledged = Lines(Path.Combine(directory, "acknowledged.jsonl"));
        using var heads = Lines(Path.Combine(directory, "heads.jsonl"));
        using var inspected = Lines(Path.Combine(directory, "inspections.jsonl"));
        try
        {
            body = new NativeArticulatedBody(configurationJson,
                new FileArticulatedTendonExchange(Path.Combine(directory, "tendon-bridge"), process));
            var clock = new PhysicalSampleClock(session, "full-avatar-native", "avatar_unity_body", 0, body.PhysicsStepMs, 0, 1000);
            head = new NativeArticulatedHeadInput(body, clock);
            Write(Path.Combine(directory, "clock.json"), new { Clock = clock, clock.Fingerprint, HeadInputFingerprint = head.Fingerprint });
            Record();
            var attachment = body.InitializePassiveInterval(clock);
            Write(Path.Combine(directory, "attachment.json"), new { Attachment = attachment, OriginalEvidenceJson = body.InitializationEvidenceJson });
            Record();
            string plant = Wire.Digest(JsonSerializer.Serialize(new { Version, Condition = condition, Clock = clock.Fingerprint,
                attachment.InitializationEvidenceSha256, Kind = "prescribed-tension-fixture; not a motor neuron or muscle cell" }));
            body.Bind(plant);
            Require(ReferenceEquals(head.Last, head.ReadAcknowledgedEndpoint()), "Binding invented another head sample.");
            for (long step = 1; step < FinalStep; ++step)
            {
                var effort = new MotorMechanicalEffort(Guid.NewGuid(), session, body.Fingerprint, plant, step,
                    clock.AtStep(step).PhysicalMs, clock.AtStep(step + 1).PhysicalMs,
                    Array.AsReadOnly(config.declarations.Select(d => new TendonTension(d.muscleId, d.muscleId == condition.Muscle ? 40 : 0)).ToArray()));
                body.AdvanceOneStep(effort); Record();
            }
            body.Stop(); Require(ReferenceEquals(head.Last, head.ReadAcknowledgedEndpoint()), "Stopping invented another head sample.");
            Require(process.WaitForExit(30000) && process.ExitCode == 0, "Native player did not stop cleanly.");
            Require(endpoints == 9 && inspections == condition.InspectionsPerEndpoint * 9 && body.CompletedPhysicsStep == FinalStep,
                "Incomplete native sequence.");
            Write(Path.Combine(directory, "result.json"), new { Version, Completed = true, Condition = condition, Endpoints = endpoints,
                Inspections = inspections, PhysicsSteps = body.CompletedPhysicsStep, FinalSequence = body.Last.sequence,
                HeadReceipts = endpoints * 3, InitialAccelerationUnavailable = true, PrescribedForceOnly = true,
                IndependentSavedComparisonRequired = true, PhysiologicalFiringQualified = false, AvatarBalanceQualified = false });

            void Record()
            {
                // Preserve the complete acknowledged endpoint BEFORE attempting
                // sensory admission, including if the third sensor later fails.
                var reply = body.Last;
                acknowledged.WriteLine(JsonSerializer.Serialize(reply, NativeArticulatedBody.Json));
                var frame = head.ReadAcknowledgedEndpoint();
                heads.WriteLine(JsonSerializer.Serialize(frame, NativeArticulatedBody.Json)); endpoints++;
                Require(frame.PhysicsStep == endpoints - 1 && frame.Samples.Count == 3 &&
                    frame.Samples.All(s => (frame.PhysicsStep == 0) == (s.Kinematics.VestibularInput == null)),
                    "Initial acceleration or sensory endpoint continuity changed.");
                for (int i = 0; i < condition.InspectionsPerEndpoint; ++i)
                {
                    body.CaptureGeometry();
                    inspected.WriteLine(JsonSerializer.Serialize(body.Last, NativeArticulatedBody.Json)); inspections++;
                    Require(ReferenceEquals(frame, head.ReadAcknowledgedEndpoint()), "Inspection invented another head receipt.");
                }
            }
        }
        catch (Exception error)
        {
            Write(Path.Combine(directory, "failure.json"), new { Error = error.ToString(), LastAcknowledged = body?.Last,
                LastCompleteHeadFrame = head?.Last, head?.AttemptedReplyJson, head?.PartialSamples, HeadFailure = head?.Failure,
                Endpoints = endpoints, Inspections = inspections, CommandResent = false });
            throw;
        }
        finally
        {
            // Only this freshly spawned isolated child is ours to terminate.
            // Preserve its last files; never resume or retry an uncertain step.
            if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        }
    }

    private static StreamWriter Lines(string path) => new(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
        new UTF8Encoding(false, true)) { AutoFlush = true };
    private static void Write<T>(string path, T value) => NativeHingeBody.Write(path, value);
}
