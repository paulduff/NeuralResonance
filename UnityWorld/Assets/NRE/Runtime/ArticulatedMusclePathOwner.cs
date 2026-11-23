using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Nre.UnityWorld
{
    // Full Avatar mechanics boundary for the direct motor/muscle candidate.
    // This owner does not manufacture a neural command, choose attachment sites,
    // integrate muscle activation, or establish anatomical/neuronal authority.
    internal sealed class ArticulatedMusclePathOwner
    {
        internal sealed class Declaration
        {
            internal readonly string MuscleId, Provenance;
            internal readonly MusclePathAnchor[] Anchors;
            internal Declaration(string muscleId, string provenance, IEnumerable<MusclePathAnchor> anchors)
            { MuscleId = muscleId; Provenance = provenance; Anchors = anchors == null ? null : anchors.ToArray(); }
        }

        internal sealed class Snapshot
        {
            internal readonly long PhysicsStep;
            internal readonly double PhysicalTimeMs, PhysicsStepMs;
            internal readonly string BodyFingerprint, PlantFingerprint, StateJson, StateHash, LastEffortId;
            internal readonly IReadOnlyList<MusclePathSnapshot> Paths;
            internal Snapshot(long step, double stepMs, string body, string plant, string state, string effort, MusclePathSnapshot[] paths)
            {
                PhysicsStep = step; PhysicsStepMs = stepMs; PhysicalTimeMs = step * stepMs;
                BodyFingerprint = body; PlantFingerprint = plant; StateJson = state; StateHash = Hash(state);
                LastEffortId = effort; Paths = Array.AsReadOnly(paths);
            }
        }

        [Serializable] internal sealed class AppliedPoint
        {
            public string muscleId, attachmentId, bodyId, requestedXBits, requestedYBits, requestedZBits;
            public Vector3 worldPoint, appliedForceNewtons;
            public string[] pointBits, appliedBits;
        }
        [Serializable] sealed class Geometry
        {
            public string version = "declared-full-avatar-tendon-paths-v2", unity, declarationsJson, sceneMechanicsJson;
            public float stepSeconds;
            public Vector3 gravity;
            public GeometryLink[] links;
        }
        [Serializable] sealed class GeometryLink
        {
            public string id, parent;
            public float mass, linearDamping, angularDamping, jointFriction;
            public Vector3 inertia, centerOfMass, anchorPosition, parentAnchorPosition;
            public Quaternion inertiaRotation, anchorRotation, parentAnchorRotation;
            public ArticulationJointType jointType;
            public Drive[] activeDrives;
            public int[] activeNativeIndices;
            public bool gravityEnabled, immovable;
            public int dofCount, solverIterations, solverVelocityIterations;
            public ArticulationDofLock twistLock, swingYLock, swingZLock;
        }
        [Serializable] sealed class Drive
        {
            public string lowerBits, upperBits, stiffnessBits, dampingBits, forceLimitBits, targetBits, targetVelocityBits;
        }
        [Serializable] sealed class Endpoint
        {
            public long physicsStep;
            public EndpointLink[] links;
        }
        [Serializable] sealed class EndpointLink
        {
            public string id;
            public Vector3 position, velocity, angularVelocity;
            public Quaternion rotation;
            public float[] jointPosition, jointVelocity, jointForce;
            public string[] positionBits, rotationBits, velocityBits, angularVelocityBits, jointPositionBits, jointVelocityBits, jointForceBits;
        }
        [Serializable] sealed class DeclaredPaths { public DeclaredPath[] paths; }
        [Serializable] sealed class DeclaredPath { public string id, provenance; public DeclaredAnchor[] anchors; }
        [Serializable] sealed class DeclaredAnchor { public string id, bodyId, xBits, yBits, zBits; }
        [Serializable] sealed class Applications { public AppliedPoint[] points; }

        readonly WorldController world;
        readonly ArticulatedAvatar body;
        readonly AvatarMusclePath[] paths;
        readonly object stepOwner = new object();
        readonly int thread = Thread.CurrentThread.ManagedThreadId;
        readonly HashSet<string> effortIds = new HashSet<string>(StringComparer.Ordinal);
        readonly string declarationsJson;
        readonly float physicalStepSeconds;
        Snapshot last;
        bool faulted;
        string plantFingerprint = "", lastEffort = "";
        internal string BodyFingerprint { get; }
        internal string GeometryJson { get; }
        internal double PhysicsStepMs => (double)physicalStepSeconds * 1000;
        internal string Failure { get; private set; }
        internal string LastApplicationsJson { get; private set; } = "";

        internal ArticulatedMusclePathOwner(WorldController world, IEnumerable<Declaration> declarations, float stepSeconds = WorldController.StepSeconds)
        {
            if (world == null || declarations == null) throw new ArgumentNullException();
            if (!MusclePathVector.Finite(stepSeconds) || stepSeconds <= 0 || stepSeconds > WorldController.StepSeconds)
                throw new ArgumentException("Declare a finite physical step no larger than the existing world step.");
            physicalStepSeconds = stepSeconds;
            this.world = world; body = world.Avatar;
            var declared = declarations.ToArray();
            var expected = MuscleProfiles.All.SelectMany(p => new[] { p.Variable + "/flexor", p.Variable + "/extensor" }).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (declared.Any(d => d == null || d.Anchors == null || d.Anchors.Any(a => a == null)) ||
                !declared.Select(d => d.MuscleId).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(expected))
                throw new ArgumentException("Declare every existing Avatar muscle group exactly once, including side and joint axis.");
            declared = declared.OrderBy(d => d.MuscleId, StringComparer.Ordinal).ToArray();
            foreach (var declaration in declared)
            {
                // Validate the existing canonical path rules before claiming the body.
                var validation = new AvatarMusclePath(declaration.MuscleId, "validation", declaration.Provenance, declaration.Anchors);
                foreach (var anchor in declaration.Anchors)
                {
                    if (!body.links.ContainsKey(anchor.BodyId) || !Finite(F(anchor.LocalPositionMeters)))
                        throw new ArgumentException("Attachment names an absent body or unrepresentable local point.");
                }
                var profile = MuscleProfiles.All.Single(p => declaration.MuscleId.StartsWith(p.Variable + "/", StringComparison.Ordinal));
                if (!declaration.Anchors.Any(a => a.BodyId == profile.Joint) || declaration.Anchors.Select(a => a.BodyId).Distinct().Count() < 2)
                    throw new ArgumentException("A muscle must include its declared moving link and another body.");
                validation.Measure("validation", declaration.Anchors.Select(a => {
                    var native = body.links[a.BodyId].body; var point = native.transform.TransformPoint(F(a.LocalPositionMeters));
                    return new MusclePathPoint(a.AttachmentId, a.BodyId, D(point), D(native.GetPointVelocity(point)));
                }).ToArray());
            }
            declarationsJson = JsonUtility.ToJson(new DeclaredPaths { paths = declared.Select(d => new DeclaredPath {
                id = d.MuscleId, provenance = d.Provenance, anchors = d.Anchors.Select(a => new DeclaredAnchor {
                    id = a.AttachmentId, bodyId = a.BodyId, xBits = MuscleHingeWire.Encode(a.LocalPositionMeters.X),
                    yBits = MuscleHingeWire.Encode(a.LocalPositionMeters.Y), zBits = MuscleHingeWire.Encode(a.LocalPositionMeters.Z) }).ToArray() }).ToArray() });
            ValidateBodyScale();
            world.ClaimMusclePathOwnership(stepOwner, physicalStepSeconds);
            try
            {
                GeometryJson = CaptureGeometryJson(); BodyFingerprint = Hash(GeometryJson);
                paths = declared.Select(d => new AvatarMusclePath(d.MuscleId, BodyFingerprint, d.Provenance, d.Anchors)).ToArray();
                last = CaptureCore();
            }
            catch (Exception e) { Fail(e); throw; }
        }

        internal void Bind(string fingerprint)
        {
            Healthy();
            if (!MuscleHingeWire.Hash(fingerprint) || plantFingerprint.Length != 0 || world.physicsSteps != 0)
                throw new ArgumentException("Bind the actual muscle plant once before physical advancement.");
            try { CheckUnchanged(); plantFingerprint = fingerprint; last = CaptureCore(); }
            catch (Exception e) { Fail(e); throw; }
        }

        internal Snapshot Capture()
        {
            Healthy();
            try { CheckUnchanged(); return last; }
            catch (Exception e) { Fail(e); throw; }
        }

        internal Snapshot Advance(long expectedStep, string expectedStateHash, string plant, string effortId,
            IReadOnlyDictionary<string, double> tensions)
        {
            Healthy();
            try
            {
                if (expectedStep != last.PhysicsStep || expectedStateHash != last.StateHash || plant != plantFingerprint || !MuscleHingeWire.Hash(plant) ||
                    !MuscleHingeWire.GuidN(effortId) || effortIds.Contains(effortId) || effortIds.Count >= 1000000 || tensions == null ||
                    !tensions.Keys.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(paths.Select(p => p.MuscleId)))
                    throw new ArgumentException("Require this exact endpoint, bound plant, new effort and every muscle tension.");
                CheckUnchanged();
                // Prepare ALL paths/forces before queuing any native force. No
                // second recruitment, fatigue, strength gain or torque shortcut.
                var applications = last.Paths.SelectMany(p => p.ApplyTension(tensions[p.MuscleId]).PointForces.Select(f => new AppliedPoint {
                    muscleId = p.MuscleId, attachmentId = f.Point.AttachmentId, bodyId = f.Point.BodyId,
                    worldPoint = F(f.Point.PositionMeters), appliedForceNewtons = F(f.ForceNewtons),
                    pointBits = Bits(F(f.Point.PositionMeters)), appliedBits = Bits(F(f.ForceNewtons)),
                    requestedXBits = MuscleHingeWire.Encode(f.ForceNewtons.X), requestedYBits = MuscleHingeWire.Encode(f.ForceNewtons.Y),
                    requestedZBits = MuscleHingeWire.Encode(f.ForceNewtons.Z) })).ToArray();
                if (applications.Any(a => !Finite(a.worldPoint) || !Finite(a.appliedForceNewtons)))
                    throw new ArgumentException("Native point force is not finite at the declared float boundary.");
                effortIds.Add(effortId); LastApplicationsJson = JsonUtility.ToJson(new Applications { points = applications });
                world.StepForMusclePaths(stepOwner, () => {
                    foreach (var a in applications) body.links[a.bodyId].body.AddForceAtPosition(a.appliedForceNewtons, a.worldPoint, ForceMode.Force);
                });
                lastEffort = effortId; last = CaptureCore();
                if (last.PhysicsStep != checked(expectedStep + 1)) throw new InvalidOperationException("Native body did not advance exactly once.");
                return last;
            }
            catch (Exception e) { Fail(e); throw; } // Never resend after partial force application/uncertain physics.
        }

        void Healthy()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread || faulted) throw new InvalidOperationException("One healthy main-thread owner is required.");
        }
        void Fail(Exception error) { faulted = true; Failure = error.ToString(); world.PauseTrial("Paused · direct muscle observation failed"); }
        void CheckUnchanged()
        {
            var observed = CaptureCore();
            if (observed.StateHash != last.StateHash || observed.PhysicsStep != last.PhysicsStep || CaptureGeometryJson() != GeometryJson)
                throw new InvalidOperationException("Physical state, mechanics or ownership changed outside the declared step.");
        }
        Snapshot CaptureCore()
        {
            ValidateBodyScale();
            if (!body.IsFinite() || world.Connected || world.Running || !body.DirectTendonForcesEnabled || body.PhysicalStepSeconds != physicalStepSeconds)
                throw new InvalidOperationException("The isolated physical body is not finite, paused and exclusively owned.");
            string state = JsonUtility.ToJson(new Endpoint { physicsStep = world.physicsSteps, links = body.links.Values.OrderBy(l => l.name, StringComparer.Ordinal)
                .Select(l => new EndpointLink { id = l.name, position = l.body.transform.position, rotation = l.body.transform.rotation,
                    velocity = l.body.linearVelocity, angularVelocity = l.body.angularVelocity,
                    jointPosition = Reduced(l.body.jointPosition), jointVelocity = Reduced(l.body.jointVelocity), jointForce = Reduced(l.body.jointForce),
                    positionBits = Bits(l.body.transform.position), rotationBits = Bits(l.body.transform.rotation),
                    velocityBits = Bits(l.body.linearVelocity), angularVelocityBits = Bits(l.body.angularVelocity),
                    jointPositionBits = Bits(Reduced(l.body.jointPosition)), jointVelocityBits = Bits(Reduced(l.body.jointVelocity)),
                    jointForceBits = Bits(Reduced(l.body.jointForce)) }).ToArray() });
            var measured = paths.Select(p => p.Measure(BodyFingerprint, p.Anchors.Select(a => {
                var native = body.links[a.BodyId].body; var point = native.transform.TransformPoint(F(a.LocalPositionMeters));
                return new MusclePathPoint(a.AttachmentId, a.BodyId, D(point), D(native.GetPointVelocity(point)));
            }).ToArray())).ToArray();
            return new Snapshot(world.physicsSteps, PhysicsStepMs, BodyFingerprint, plantFingerprint, state, lastEffort, measured);
        }
        string CaptureGeometryJson() => JsonUtility.ToJson(new Geometry {
            unity = Application.unityVersion, declarationsJson = declarationsJson, sceneMechanicsJson = ArticulatedMechanicsIdentity.Capture(body),
            stepSeconds = physicalStepSeconds, gravity = Physics.gravity,
            links = body.links.Values.OrderBy(l => l.name, StringComparer.Ordinal).Select(l => new GeometryLink {
                id = l.name, parent = l.body.transform.parent == null ? "" : l.body.transform.parent.name, mass = l.body.mass,
                inertia = l.body.inertiaTensor, inertiaRotation = l.body.inertiaTensorRotation, centerOfMass = l.body.centerOfMass,
                linearDamping = l.body.linearDamping, angularDamping = l.body.angularDamping, jointFriction = l.body.jointFriction,
                anchorPosition = l.body.anchorPosition, anchorRotation = l.body.anchorRotation, parentAnchorPosition = l.body.parentAnchorPosition,
                parentAnchorRotation = l.body.parentAnchorRotation, jointType = l.body.jointType,
                activeDrives = Enumerable.Range(0,l.dofs).Select(axis => CaptureDrive(ArticulatedAvatar.JointDrive(l,axis))).ToArray(),
                activeNativeIndices = Enumerable.Range(0,l.dofs).Select(axis => l.coordinates.NativeIndex(axis)).ToArray(),
                gravityEnabled = l.body.useGravity, immovable = l.body.immovable, dofCount = l.body.dofCount,
                solverIterations = l.body.solverIterations, solverVelocityIterations = l.body.solverVelocityIterations,
                twistLock = l.body.twistLock, swingYLock = l.body.swingYLock, swingZLock = l.body.swingZLock }).ToArray() });
        void ValidateBodyScale()
        {
            foreach (var link in body.links.Values)
            {
                // Check declared local scales, not the numerically reconstructed
                // lossyScale of a rotating hierarchy.
                for (var t = link.body.transform; t != null; t = t.parent)
                {
                    var scale = t.localScale;
                    if (scale.x != 1 || scale.y != 1 || scale.z != 1) throw new InvalidOperationException("Declared attachment coordinates require unit-scale physical links.");
                }
            }
        }
        static Drive CaptureDrive(ArticulationDrive d) => new Drive {
            lowerBits = MuscleHingeWire.Encode(d.lowerLimit), upperBits = MuscleHingeWire.Encode(d.upperLimit),
            stiffnessBits = MuscleHingeWire.Encode(d.stiffness), dampingBits = MuscleHingeWire.Encode(d.damping),
            forceLimitBits = MuscleHingeWire.Encode(d.forceLimit), targetBits = MuscleHingeWire.Encode(d.target),
            targetVelocityBits = MuscleHingeWire.Encode(d.targetVelocity) };
        static string[] Bits(Vector3 v) => Bits(new[] { v.x, v.y, v.z });
        static string[] Bits(Quaternion q) => Bits(new[] { q.x, q.y, q.z, q.w });
        static string[] Bits(float[] values) => values.Select(v => MuscleHingeWire.Encode(v)).ToArray();
        static float[] Reduced(ArticulationReducedSpace state) => Enumerable.Range(0, state.dofCount).Select(i => state[i]).ToArray();
        static MusclePathVector D(Vector3 v) => new MusclePathVector(v.x, v.y, v.z);
        static Vector3 F(MusclePathVector v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static bool Finite(Vector3 v) => MusclePathVector.Finite(v.x) && MusclePathVector.Finite(v.y) && MusclePathVector.Finite(v.z);
        static string Hash(string text) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", ""); }
    }
}
