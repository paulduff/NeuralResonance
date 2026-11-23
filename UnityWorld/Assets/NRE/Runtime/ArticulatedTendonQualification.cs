using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    // Prescribed mechanical fixtures ONLY. No neural source, recruitment,
    // calibrated anatomy or standing controller is exercised by this study.
    internal static class ArticulatedTendonQualification
    {
        [Serializable] sealed class Report
        {
            public string version = "full-avatar-tendon-mechanics-v1", unity, condition, muscle, error, rejectedError;
            public string geometryJson, beforeRejectedState, afterRejectedState, stepMsBits, lastNativeForcesJson;
            public bool completed, passed, rejectedWithoutStep, faultLatchPassed;
            public bool nativeForceObservationEnabled;
            public string mechanicsDiagnosticJson;
            public bool neuronalSourceExecuted = false, anatomicalCalibrationQualified = false, avatarBalanceQualified = false;
            public long finalStep;
            public int links, paths;
            public float initialPelvisHeight, finalPelvisHeight, peakFootForce;
            public Fixture[] fixtures;
        }
        [Serializable] sealed class Fixture
        {
            public string muscleId, parent, child, provenance;
            public int axis, sign;
            public string[] worldAxisBits, pivotBits;
            public string radiusBits, halfSpanBits;
        }
        [Serializable] sealed class PathState { public string muscleId, lengthBits, velocityBits; public Point[] points; }
        [Serializable] sealed class Point { public string attachmentId, bodyId; public string[] positionBits, velocityBits; }
        [Serializable] sealed class Endpoint
        {
            public long step;
            public string physicalTimeMsBits, stateHash, stateJson, applicationsJson, nativeForcesJson;
            public PathState[] paths;
            public float leftFootForce, rightFootForce;
        }
        static string Arg(string name) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
        static string[] Bits(Vector3 v) => new[] { MuscleHingeWire.Encode(v.x), MuscleHingeWire.Encode(v.y), MuscleHingeWire.Encode(v.z) };
        static MusclePathVector D(Vector3 p) => new MusclePathVector(p.x, p.y, p.z);
        static string[] Bits(MusclePathVector p) => new[] { MuscleHingeWire.Encode(p.X), MuscleHingeWire.Encode(p.Y), MuscleHingeWire.Encode(p.Z) };
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        static ArticulatedMusclePathOwner.Declaration[] Fixtures(ArticulatedAvatar avatar, out Fixture[] evidence)
        {
            var paths = new List<ArticulatedMusclePathOwner.Declaration>(); var descriptions = new List<Fixture>();
            foreach (var profile in MuscleProfiles.All)
            {
                var child = avatar.links[profile.Joint].body;
                var parent = child.transform.parent.GetComponent<ArticulationBody>();
                Require(parent != null, "Fixture requires the real joint parent.");
                var rotation = child.transform.rotation * child.anchorRotation;
                int physicalAxis = profile.Axis == 0 ? 0 : avatar.links[profile.Joint].coordinates.SecondPhysicalAxis;
                var axis = rotation * (physicalAxis == 0 ? Vector3.right : physicalAxis == 1 ? Vector3.up : Vector3.forward);
                var u = rotation * (physicalAxis == 0 ? Vector3.up : Vector3.right);
                var v = Vector3.Cross(axis, u).normalized;
                var pivot = child.transform.TransformPoint(child.anchorPosition);
                float radius = (float)profile.MomentArm, halfSpan = .1f;
                Require(radius > 0 && radius < .2f, "Fixture moment arm must come from the existing profile.");
                foreach (int sign in new[] { -1, 1 })
                {
                    string id = profile.Variable + (sign == -1 ? "/flexor" : "/extensor");
                    string provenance = "diagnostic-joint-frame-opposing-lines-v1; existing-profile-radius; not-anatomical-attachment";
                    var origin = pivot + u * (sign * radius) - v * halfSpan;
                    var insertion = pivot + u * (sign * radius) + v * halfSpan;
                    paths.Add(new ArticulatedMusclePathOwner.Declaration(id, provenance, new[] {
                        new MusclePathAnchor("origin", parent.name, D(parent.transform.InverseTransformPoint(origin))),
                        new MusclePathAnchor("insertion", child.name, D(child.transform.InverseTransformPoint(insertion))) }));
                    descriptions.Add(new Fixture { muscleId = id, parent = parent.name, child = child.name, axis = profile.Axis, sign = sign,
                        provenance = provenance, worldAxisBits = Bits(axis), pivotBits = Bits(pivot), radiusBits = MuscleHingeWire.Encode(radius), halfSpanBits = MuscleHingeWire.Encode(halfSpan) });
                }
            }
            evidence = descriptions.ToArray(); return paths.ToArray();
        }
        static void Record(StreamWriter log, ArticulatedMusclePathOwner.Snapshot s, ArticulatedMusclePathOwner owner, ArticulatedAvatar body)
        {
            log.WriteLine(JsonUtility.ToJson(new Endpoint { step = s.PhysicsStep, physicalTimeMsBits = MuscleHingeWire.Encode(s.PhysicalTimeMs),
                stateHash = s.StateHash, stateJson = s.StateJson, applicationsJson = owner.LastApplicationsJson, nativeForcesJson = owner.LastNativeForcesJson,
                leftFootForce = body.LeftLoad, rightFootForce = body.RightLoad,
                paths = s.Paths.Select(p => new PathState { muscleId = p.MuscleId, lengthBits = MuscleHingeWire.Encode(p.MusculotendonLengthMeters),
                    velocityBits = MuscleHingeWire.Encode(p.MusculotendonVelocityMetersPerSecond), points = p.Points.Select(q => new Point {
                        attachmentId = q.AttachmentId, bodyId = q.BodyId, positionBits = Bits(q.PositionMeters), velocityBits = Bits(q.VelocityMetersPerSecond) }).ToArray() }).ToArray() }));
        }
        static string RawState(ArticulatedAvatar body) => string.Join("|", body.links.Values.OrderBy(l => l.name, StringComparer.Ordinal).Select(l => {
            var b = l.body; var q = b.transform.rotation;
            var parts = Bits(b.transform.position).Concat(new[] { MuscleHingeWire.Encode(q.x), MuscleHingeWire.Encode(q.y), MuscleHingeWire.Encode(q.z), MuscleHingeWire.Encode(q.w) })
                .Concat(Bits(b.linearVelocity)).Concat(Bits(b.angularVelocity));
            for (int i = 0; i < b.jointPosition.dofCount; i++) parts = parts.Concat(new[] { MuscleHingeWire.Encode(b.jointPosition[i]), MuscleHingeWire.Encode(b.jointVelocity[i]), MuscleHingeWire.Encode(b.jointForce[i]) });
            return l.name + ":" + string.Join(",", parts);
        }));
        internal static IEnumerator Run(WorldController world)
        {
            // Allow component registration with physics still paused.
            yield return null;
            var result = new Report { unity = Application.unityVersion, condition = Arg("--tendon-case"), muscle = Arg("--tendon-muscle") ?? "" };
            ArticulatedMusclePathOwner owner = null;
            TendonMechanicsDiagnostic diagnostic = null;
            int exit = 1;
            try
            {
                Require(world.IsolatedQualification && !world.Connected && !world.Running && world.physicsSteps == 0, "Fresh isolated paused world required.");
                string observe = Arg("--tendon-observe-native");
                Require(observe == null || observe == "1", "Unknown native observation setting.");
                result.nativeForceObservationEnabled = observe == "1";
                var declarations = Fixtures(world.Avatar, out result.fixtures);
                string mechanics = Arg("--tendon-mechanics");
                if (mechanics != null)
                {
                    Require(result.nativeForceObservationEnabled && (result.condition == "quiet" || result.condition == "pulse"), "Mechanical diagnosis requires observed quiet/pulse cases.");
                    diagnostic = TendonMechanicsDiagnostic.Prepare(world, mechanics);
                }
                owner = new ArticulatedMusclePathOwner(world, declarations, .005f, result.nativeForceObservationEnabled);
                string plant = new string('A', 64); owner.Bind(plant);
                diagnostic?.Bound();
                var state = owner.Capture(); result.geometryJson = owner.GeometryJson; result.stepMsBits = MuscleHingeWire.Encode(owner.PhysicsStepMs);
                result.links = world.Avatar.links.Count; result.paths = state.Paths.Count;
                Require(result.links == 15 && result.paths == 38, "Incomplete Avatar mechanics.");
                var tensions = state.Paths.ToDictionary(p => p.MuscleId, p => 0.0, StringComparer.Ordinal);
                result.initialPelvisHeight = world.Avatar.pelvis.transform.position.y;
                using (var log = new StreamWriter(Path.Combine(world.outputDirectory, "endpoints.jsonl"), false))
                {
                    log.AutoFlush = true; Record(log, state, owner, world.Avatar);
                    bool guard = result.condition != null && result.condition.StartsWith("reject-", StringComparison.Ordinal);
                    if (!guard)
                    {
                        Require(result.condition == "quiet" || result.condition == "pulse" || result.condition == "freefall", "Unknown native condition.");
                        if (result.condition == "pulse") { Require(tensions.ContainsKey(result.muscle), "Unknown muscle."); tensions[result.muscle] = 40; }
                        int steps = result.condition == "freefall" ? 100 : 8;
                        for (int i = 0; i < steps; i++)
                        {
                            diagnostic?.CheckUnchanged();
                            state = owner.Advance(state.PhysicsStep, state.StateHash, plant, Guid.NewGuid().ToString("N"), tensions);
                            Record(log, state, owner, world.Avatar);
                            diagnostic?.CheckUnchanged();
                            result.peakFootForce = Mathf.Max(result.peakFootForce, world.Avatar.LeftLoad + world.Avatar.RightLoad);
                            Require(!world.Connected && !world.Running && world.Avatar.IsFinite(), "Body became nonfinite or lost isolation.");
                        }
                    }
                    else
                    {
                        string effort = Guid.NewGuid().ToString("N"), stateHash = state.StateHash, suppliedPlant = plant; long suppliedStep = state.PhysicsStep;
                        if (result.condition == "reject-repeat")
                        {
                            state = owner.Advance(state.PhysicsStep, state.StateHash, plant, effort, tensions); Record(log, state, owner, world.Avatar);
                            suppliedStep = state.PhysicsStep; stateHash = state.StateHash;
                        }
                        switch (result.condition)
                        {
                            case "reject-step": suppliedStep++; break;
                            case "reject-state": stateHash = new string('B', 64); break;
                            case "reject-plant": suppliedPlant = new string('B', 64); break;
                            case "reject-repeat": break;
                            case "reject-missing": tensions.Remove(tensions.Keys.First()); break;
                            case "reject-negative": tensions[tensions.Keys.First()] = -1; break;
                            case "reject-nonfinite": tensions[tensions.Keys.First()] = double.NaN; break;
                            case "reject-shape": world.Avatar.pelvis.GetComponentInChildren<BoxCollider>().size += Vector3.one * .01f; break;
                            case "reject-gravity": Physics.gravity += Vector3.right; break;
                            case "reject-material": world.Avatar.pelvis.GetComponentInChildren<Collider>().sharedMaterial.staticFriction += .1f; break;
                            case "reject-filter":
                                Physics.IgnoreCollision(world.Avatar.links["left_foot"].body.GetComponentInChildren<Collider>(), world.Avatar.links["right_foot"].body.GetComponentInChildren<Collider>(), false); break;
                            default: throw new InvalidOperationException("Unknown rejection control.");
                        }
                        long beforeStep = world.physicsSteps; result.beforeRejectedState = RawState(world.Avatar);
                        bool rejected = false;
                        try { owner.Advance(suppliedStep, stateHash, suppliedPlant, effort, tensions); }
                        catch (Exception e) { rejected = true; result.rejectedError = e.ToString(); }
                        result.afterRejectedState = RawState(world.Avatar);
                        result.rejectedWithoutStep = rejected && beforeStep == world.physicsSteps && result.beforeRejectedState == result.afterRejectedState;
                        try { owner.Capture(); } catch (InvalidOperationException) { result.faultLatchPassed = true; }
                        Require(result.rejectedWithoutStep && result.faultLatchPassed, "Rejected command advanced native physics or did not latch its fault.");
                    }
                }
                result.finalStep = world.physicsSteps; result.finalPelvisHeight = world.Avatar.pelvis.transform.position.y;
                if (result.condition == "freefall") Require(result.finalPelvisHeight < result.initialPelvisHeight - .02f && result.peakFootForce > 0, "Unforced body did not fall/contact the floor.");
                result.completed = result.passed = true; exit = 0;
            }
            catch (Exception e) { result.error = e.ToString(); Debug.LogException(e); }
            result.finalStep = world.physicsSteps;
            if (owner != null) result.lastNativeForcesJson = owner.LastNativeForcesJson;
            if (diagnostic != null) result.mechanicsDiagnosticJson = diagnostic.Json();
            File.WriteAllText(Path.Combine(world.outputDirectory, "result.json"), JsonUtility.ToJson(result, true));
            Application.Quit(exit);
        }
    }
}
