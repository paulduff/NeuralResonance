using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using static Nre.UnityWorld.ArticulatedTendonWire;

namespace Nre.UnityWorld
{
    // One isolated full Avatar, owned by the host's existing causal muscle plant.
    // Every file is immutable; an uncertain physical command is never retried.
    internal sealed class ArticulatedTendonBridge
    {
        readonly WorldController world;
        ArticulatedMusclePathOwner owner;
        TendonMechanicsDiagnostic mechanics;
        Configuration configuration;
        string directory, configurationHash, observationsJson;
        Reply last;
        bool ownsDirectory, advanceAttempted;

        ArticulatedTendonBridge(WorldController world) { this.world = world; }
        internal static IEnumerator Run(WorldController world) => new ArticulatedTendonBridge(world).Run();
        IEnumerator Run()
        {
            yield return null; // Register bodies with physics paused at step zero.
            bool failed = false;
            try
            {
                string session = WorldController.Argument("--tendon-bridge");
                MuscleHingeWire.Require(MuscleHingeWire.GuidN(session) && world.IsolatedQualification && !world.Connected && !world.Running && world.physicsSteps == 0,
                    "Full-body bridge requires a fresh, paused, isolated session.");
                string text = DiagnosticBridgeFiles.ReadPublished(WorldController.Argument("--tendon-config"), 30000);
                configuration = JsonUtility.FromJson<Configuration>(text); float stepSeconds = Validate(configuration);
                MuscleHingeWire.Require(configuration.sessionId == session, "Attachment session differs.");
                configurationHash = Digest(text);
                directory = Path.Combine(world.outputDirectory, "tendon-bridge");
                MuscleHingeWire.Require(!Directory.Exists(directory), "Do not reuse a physical bridge directory.");
                Directory.CreateDirectory(directory); ownsDirectory = true;
                Write("configuration.json", text);
                mechanics = TendonMechanicsDiagnostic.Prepare(world, configuration.mechanicsMode);
                owner = new ArticulatedMusclePathOwner(world, configuration.declarations.Select(d => new ArticulatedMusclePathOwner.Declaration(
                    d.muscleId, d.provenance, d.anchors.Select(a => new MusclePathAnchor(a.id, a.bodyId, new MusclePathVector(a.x, a.y, a.z))))), stepSeconds,
                    allowPassivePrelude: configuration.passivePreludeSteps == 1);
                mechanics.Bound();
                var initial = Capture("hello", 0, true); Validate(initial, configuration, configurationHash);
                Write("hello.json", JsonUtility.ToJson(initial)); last = initial;
            }
            catch (Exception error) { Fail(error); failed = true; }
            if (failed) { Application.Quit(1); yield break; }

            var waiting = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                advanceAttempted = false;
                BridgeReadOutcome outcome = BridgeReadOutcome.Missing; string text = null;
                try { outcome = DiagnosticBridgeFiles.TryRead(Path.Combine(directory, "request-" + checked(last.sequence + 1).ToString("D6") + ".json"), out text); }
                catch (Exception error) { Fail(error); failed = true; }
                if (failed) { Application.Quit(1); yield break; }
                if (outcome != BridgeReadOutcome.Ready)
                {
                    if (waiting.Elapsed.TotalMinutes >= 5)
                    { Fail(new TimeoutException("Same request unavailable; no physical step or resend.")); Application.Quit(1); yield break; }
                    yield return null; continue;
                }
                bool stop = false;
                try
                {
                    // Capture() verifies the actual mechanics and pose before admission.
                    var current = Capture(last.operation, last.sequence, false);
                    MuscleHingeWire.Require(current.stateHash == last.stateHash, "Body moved outside its owner.");
                    var request = JsonUtility.FromJson<Request>(text); Validate(request, current, configuration, configurationHash);
                    if (request.operation == "bind") owner.Bind(request.plantFingerprint);
                    if (request.operation == "prelude")
                    {
                        advanceAttempted = true;
                        owner.AdvancePassivePrelude(request.expectedStep, request.stateHash);
                    }
                    if (request.operation == "step")
                    {
                        advanceAttempted = true;
                        owner.Advance(request.expectedStep, request.stateHash, request.plantFingerprint, request.effortId,
                            request.tensions.ToDictionary(t => t.muscleId, t => (double)t.axialNewtons, StringComparer.Ordinal));
                    }
                    var after = Capture(request.operation, request.sequence, request.operation == "step" || request.operation == "prelude");
                    ValidateTransition(current, request, after, configuration, configurationHash);
                    Write("reply-" + request.sequence.ToString("D6") + ".json", JsonUtility.ToJson(after));
                    last = after; waiting.Restart(); stop = request.operation == "stop";
                }
                catch (Exception error) { Fail(error); failed = true; }
                if (failed || stop) { Application.Quit(failed ? 1 : 0); yield break; }
            }
        }
        Reply Capture(string operation, long sequence, bool captureObservations)
        {
            mechanics.CheckUnchanged();
            var s = owner.Capture();
            if (captureObservations)
            {
                var body = world.Avatar;
                // These are the original encoded body/contact observations, including
                // their existing limitations. Muscle activation is owned by the host;
                // legacy body telemetry must not stand in for that dynamic state.
                observationsJson = JsonUtility.ToJson(new Observations {
                    physicsStep = s.PhysicsStep, bodyJson = JsonUtility.ToJson(BodyFrame.Capture(body, s.PhysicsStep)),
                    contactsJson = body.links.Values.OrderBy(l => l.name, StringComparer.Ordinal).Select(l => JsonUtility.ToJson(ContactFrame.Capture(body, l, s.PhysicsStep))).ToArray(),
                    skullJson = new[] { JsonUtility.ToJson(SkullMotionCapture.Capture(body, s.PhysicsStep, "centre", Vector3.zero)),
                        JsonUtility.ToJson(SkullMotionCapture.Capture(body, s.PhysicsStep, "left", new Vector3(-.0625f, 0, 0))),
                        JsonUtility.ToJson(SkullMotionCapture.Capture(body, s.PhysicsStep, "right", new Vector3(.0625f, 0, 0))) }
                });
            }
            // Inspections replay the original endpoint observation, including its
            // wall receipt timestamp. They do not manufacture another sample.
            return new Reply { protocol = Protocol, sessionId = configuration.sessionId, configurationHash = configurationHash,
                operation = operation, sequence = sequence, unity = Application.unityVersion, isolated = world.IsolatedQualification,
                paused = !world.Running, connected = world.Connected, physicsStep = s.PhysicsStep, physicsStepMs = s.PhysicsStepMs,
                physicalTimeMs = s.PhysicalTimeMs, bodyFingerprint = s.BodyFingerprint, plantFingerprint = s.PlantFingerprint,
                geometryJson = owner.GeometryJson, stateJson = s.StateJson, stateHash = s.StateHash, lastAppliedEffortId = s.LastEffortId,
                observationsJson = observationsJson, observationsHash = Digest(observationsJson), applicationsJson = owner.LastApplicationsJson,
                mechanicsDiagnosticJson = mechanics.Json(),
                paths = s.Paths.Select(p => new MuscleHingeWire.Path { muscleId = p.MuscleId, pathFingerprint = p.PathFingerprint,
                    lengthMeters = p.MusculotendonLengthMeters, velocityMetersPerSecond = p.MusculotendonVelocityMetersPerSecond }).ToArray() };
        }
        void Write(string name, string text)
        {
            string path = Path.Combine(directory, name), temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true))) writer.Write(text);
            File.Move(temporary, path);
        }
        void Fail(Exception error)
        {
            Debug.LogException(error);
            if (ownsDirectory)
            {
                try { Write("failure.json", JsonUtility.ToJson(new Failure { sessionId = configuration?.sessionId,
                    error = error.ToString(), lastAcknowledgedSequence = last?.sequence ?? 0, observedPhysicsStep = world.physicsSteps,
                    advanceAttempted = advanceAttempted, lastAcknowledged = last })); }
                catch (Exception preservationError) { Debug.LogException(preservationError); }
            }
        }
    }
}
