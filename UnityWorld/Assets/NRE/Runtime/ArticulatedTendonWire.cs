#nullable disable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Nre.UnityWorld.MuscleHingeWire;

namespace Nre.UnityWorld
{
    // Shared verbatim with the host. No hinge clock, two-path assumption,
    // implicit attachment anatomy, or biological controller crosses this wire.
    public static class ArticulatedTendonWire
    {
        public const string Protocol = "full-avatar-tendon-wire-v3";
        public const string NamedMuscleProtocol = "named-avatar-tendon-wire-v1";
        public const string NamedGeometryVersion = "declared-named-avatar-tendon-paths-v1";
        // The original synthetic profile has exactly 38 paths. Named anatomy
        // instead binds the complete explicit configuration, without inventing
        // a flexor/extensor pair or duplicating a muscle for each crossed joint.
        public const int PathCount = 38;
        public const long MaximumSteps = 1000000;
        [Serializable] public sealed class Anchor { public string id, bodyId; public Real x, y, z; }
        [Serializable] public sealed class Declaration { public string muscleId, provenance; public Anchor[] anchors; }
        [Serializable] public sealed class Configuration
        {
            public string protocol, sessionId, mechanicsMode;
            public Real stepSeconds;
            public int passivePreludeSteps;
            public Declaration[] declarations;
        }
        [Serializable] public sealed class Request
        {
            public string protocol, sessionId, configurationHash, operation, bodyFingerprint, plantFingerprint, stateHash, effortId;
            public long sequence, expectedStep;
            public Real startTimeMs, endTimeMs;
            public Tension[] tensions;
        }
        [Serializable] public sealed class Reply
        {
            public string protocol, sessionId, configurationHash, operation, unity, bodyFingerprint, plantFingerprint;
            public string geometryJson, stateJson, stateHash, lastAppliedEffortId, observationsJson, observationsHash, applicationsJson, mechanicsDiagnosticJson;
            public long sequence, physicsStep;
            public Real physicsStepMs, physicalTimeMs;
            public bool isolated, paused, connected;
            public MuscleHingeWire.Path[] paths;
        }
        [Serializable] public sealed class Observations
        {
            public long physicsStep;
            public string bodyJson;
            public string[] contactsJson, skullJson;
        }
        [Serializable] public sealed class Failure
        {
            public string protocol = Protocol, sessionId, error;
            public long lastAcknowledgedSequence, observedPhysicsStep;
            public bool advanceAttempted;
            public Reply lastAcknowledged;
        }
        public static string Digest(string value)
        {
            Require(value != null, "Missing hashed payload.");
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 256 && !s.Any(char.IsControl);
        public static float Validate(Configuration c)
        {
            Require(c != null && (c.protocol == Protocol || c.protocol == NamedMuscleProtocol) && GuidN(c.sessionId), "Declare the full-body protocol and fresh session.");
            Require(c.mechanicsMode == "original" || c.mechanicsMode == "no-joint-friction",
                "Explicitly select the reviewed original or no-joint-friction mechanics; other interventions are not bridge settings.");
            Require(c.passivePreludeSteps == 0 || c.passivePreludeSteps == 1, "Declare zero or one passive initialization interval.");
            double seconds = c.stepSeconds;
            Require(Finite(seconds) && seconds > 0 && seconds <= (double).01f && (double)(float)seconds == seconds,
                "Physical seconds must be the exact positive native float, at most the existing world step.");
            Require(c.declarations != null && (c.protocol == Protocol ? c.declarations.Length == PathCount : c.declarations.Length > 0) &&
                c.declarations.All(d => d != null && Key(d.muscleId) && Key(d.provenance)),
                "Declare the complete selected muscle inventory and attachment provenance; the original protocol requires 38 paths.");
            var ids = c.declarations.Select(d => d.muscleId).ToArray();
            Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length && ids.SequenceEqual(ids.OrderBy(s => s, StringComparer.Ordinal)),
                "Muscle declarations must be distinct and canonically ordered.");
            foreach (var d in c.declarations)
            {
                Require(d.anchors != null && d.anchors.Length >= 2 && d.anchors.All(a => a != null && Key(a.id) && Key(a.bodyId)), "Incomplete attachment path.");
                Require(d.anchors.Select(a => a.id).Distinct(StringComparer.Ordinal).Count() == d.anchors.Length &&
                    d.anchors.Select(a => a.bodyId).Distinct(StringComparer.Ordinal).Count() >= 2, "Attachments need unique identities and at least two bodies.");
                foreach (var a in d.anchors)
                    Require(Finite(a.x) && Finite(a.y) && Finite(a.z) && Math.Abs((double)a.x) <= float.MaxValue &&
                        Math.Abs((double)a.y) <= float.MaxValue && Math.Abs((double)a.z) <= float.MaxValue, "Unrepresentable native attachment.");
            }
            return (float)seconds;
        }
        public static double StepMs(Configuration c) => (double)Validate(c) * 1000;
        public static void Validate(Reply r, Configuration c, string configurationHash)
        {
            double h = StepMs(c);
            Require(r != null && r.protocol == c.protocol && r.sessionId == c.sessionId && Hash(configurationHash) && r.configurationHash == configurationHash &&
                r.unity == "6000.3.19f1" && r.isolated && r.paused && !r.connected && r.sequence >= 0 && r.physicsStep >= 0 && r.physicsStep <= MaximumSteps &&
                r.physicsStepMs == h && r.physicalTimeMs == r.physicsStep * h, "Invalid native full-body identity or clock.");
            Require(!string.IsNullOrEmpty(r.geometryJson) && r.bodyFingerprint == Digest(r.geometryJson) &&
                !string.IsNullOrEmpty(r.stateJson) && r.stateHash == Digest(r.stateJson) &&
                !string.IsNullOrEmpty(r.observationsJson) && r.observationsHash == Digest(r.observationsJson) &&
                !string.IsNullOrEmpty(r.mechanicsDiagnosticJson), "Native evidence hash or declared mechanical evidence is missing.");
            Require((r.plantFingerprint == "" || Hash(r.plantFingerprint)) &&
                (r.physicsStep <= c.passivePreludeSteps ? r.lastAppliedEffortId == "" : GuidN(r.lastAppliedEffortId)), "Invalid plant/last effort identity.");
            Require(r.paths != null && r.paths.Length == c.declarations.Length && r.paths.All(p => p != null) &&
                r.paths.Select(p => p.muscleId).SequenceEqual(c.declarations.Select(d => d.muscleId)) &&
                r.paths.All(p => Hash(p.pathFingerprint) && Finite(p.lengthMeters) && p.lengthMeters > 0 && Finite(p.velocityMetersPerSecond)),
                "Missing, unordered or nonfinite muscle path measurements.");
        }
        public static void Validate(Request r, Reply current, Configuration c, string configurationHash)
        {
            Validate(current, c, configurationHash);
            Require(r != null && r.protocol == c.protocol && r.sessionId == current.sessionId && r.configurationHash == configurationHash &&
                r.sequence == checked(current.sequence + 1) && r.expectedStep == current.physicsStep &&
                r.bodyFingerprint == current.bodyFingerprint && r.stateHash == current.stateHash && r.startTimeMs == current.physicalTimeMs,
                "Command must name this exact session, sequence and body endpoint.");
            if (r.operation == "bind")
                Require(current.physicsStep == c.passivePreludeSteps && current.plantFingerprint == "" && Hash(r.plantFingerprint), "Bind once at the declared motor baseline.");
            else Require(r.plantFingerprint == current.plantFingerprint, "Plant binding differs.");
            if (r.operation == "step")
            {
                Require(Hash(current.plantFingerprint) && current.physicsStep < MaximumSteps && GuidN(r.effortId) && r.effortId != current.lastAppliedEffortId &&
                    r.endTimeMs == checked(current.physicsStep + 1) * (double)current.physicsStepMs, "Invalid effort or physical interval.");
                Require(r.tensions != null && r.tensions.Length == c.declarations.Length && r.tensions.All(t => t != null) &&
                    r.tensions.Select(t => t.muscleId).SequenceEqual(c.declarations.Select(d => d.muscleId)) &&
                    r.tensions.All(t => Finite(t.axialNewtons) && t.axialNewtons >= 0 && t.axialNewtons <= float.MaxValue),
                    "Require every ordered, finite, nonnegative tendon tension exactly once.");
            }
            else if (r.operation == "prelude")
                Require(c.passivePreludeSteps == 1 && current.physicsStep == 0 && current.plantFingerprint == "" &&
                    r.effortId == "" && r.tensions != null && r.tensions.Length == 0 && r.endTimeMs == (double)current.physicsStepMs,
                    "Passive initialization permits exactly one unbound interval without a motor effort.");
            else
                Require((r.operation == "bind" || r.operation == "inspect" || r.operation == "stop") && r.effortId == "" &&
                    r.tensions != null && r.tensions.Length == 0 && r.endTimeMs == r.startTimeMs, "Nonmoving command carries time or force.");
        }
        public static void ValidateTransition(Reply before, Request request, Reply after, Configuration c, string configurationHash)
        {
            Validate(request, before, c, configurationHash); Validate(after, c, configurationHash);
            bool moved = request.operation == "step" || request.operation == "prelude";
            Require(after.sequence == request.sequence && after.operation == request.operation &&
                after.physicsStep == before.physicsStep + (moved ? 1 : 0) && after.physicalTimeMs == request.endTimeMs &&
                after.plantFingerprint == request.plantFingerprint && after.lastAppliedEffortId == (request.operation == "step" ? request.effortId : before.lastAppliedEffortId) &&
                after.bodyFingerprint == before.bodyFingerprint && after.geometryJson == before.geometryJson && after.mechanicsDiagnosticJson == before.mechanicsDiagnosticJson &&
                after.paths.Select(p => p.pathFingerprint).SequenceEqual(before.paths.Select(p => p.pathFingerprint)), "Native step receipt or fixed geometry differs.");
            if (!moved)
                Require(after.stateJson == before.stateJson && after.observationsJson == before.observationsJson && after.applicationsJson == before.applicationsJson &&
                    after.paths.Zip(before.paths, (a, b) => a.lengthMeters == b.lengthMeters && a.velocityMetersPerSecond == b.velocityMetersPerSecond).All(v => v),
                    "Nonmoving command changed physical observations.");
        }
    }
}
