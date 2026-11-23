using System;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    // Explicit isolated interventions. The isolated v3 bridge permits only the
    // reviewed original/no-joint-friction pair; the live body never selects this.
    // A changed response diagnoses a mechanical sensitivity, not anatomical validity.
    internal sealed class TendonMechanicsDiagnostic
    {
        [Serializable] internal sealed class Drive
        {
            public string type, lower, upper, stiffness, damping, forceLimit, target, targetVelocity;
        }
        [Serializable] internal sealed class Body
        {
            public string id, jointType, friction, linearDamping, angularDamping;
            public string maxJointVelocity, maxLinearVelocity, maxAngularVelocity, mass;
            public string[] inertia, inertiaRotation, center, anchorPosition, anchorRotation, parentAnchorPosition, parentAnchorRotation;
            public string[] locks;
            public int activeCount, secondPhysicalAxis, nativeCount, solverIterations, solverVelocityIterations;
            public bool gravity, immovable;
            public Drive[] drives;
        }
        [Serializable] internal sealed class Settings { public Body[] bodies; }
        [Serializable] sealed class Evidence
        {
            public string version = "isolated-tendon-mechanical-interventions-v1", mode;
            public Settings original, intervention, owned, changedDuringCapture;
            public bool liveConfigurationChanged = false, physiologicalCalibrationQualified = false;
        }
        static readonly string[] Modes = { "original", "no-joint-friction", "no-body-damping", "joint-speed-floor", "free-active-axes", "no-gravity" };
        readonly ArticulatedAvatar avatar;
        readonly Evidence evidence;
        string ownedJson;
        TendonMechanicsDiagnostic(ArticulatedAvatar avatar, string mode)
        { this.avatar = avatar; evidence = new Evidence { mode = mode, original = Capture(avatar) }; }
        static string B(float value) => MuscleHingeWire.Encode(value);
        static string[] V(Vector3 value) => new[] { B(value.x), B(value.y), B(value.z) };
        static string[] Q(Quaternion value) => new[] { B(value.x), B(value.y), B(value.z), B(value.w) };
        static Drive D(ArticulationDrive drive) => new Drive { type = drive.driveType.ToString(), lower = B(drive.lowerLimit), upper = B(drive.upperLimit),
            stiffness = B(drive.stiffness), damping = B(drive.damping), forceLimit = B(drive.forceLimit), target = B(drive.target), targetVelocity = B(drive.targetVelocity) };
        static Settings Capture(ArticulatedAvatar avatar) => new Settings { bodies = avatar.links.Values.OrderBy(l => l.name, StringComparer.Ordinal).Select(l => {
            var b = l.body;
            return new Body { id = l.name, jointType = b.jointType.ToString(), friction = B(b.jointFriction),
                linearDamping = B(b.linearDamping), angularDamping = B(b.angularDamping), maxJointVelocity = B(b.maxJointVelocity),
                maxLinearVelocity = B(b.maxLinearVelocity), maxAngularVelocity = B(b.maxAngularVelocity), mass = B(b.mass),
                inertia = V(b.inertiaTensor), inertiaRotation = Q(b.inertiaTensorRotation), center = V(b.centerOfMass),
                anchorPosition = V(b.anchorPosition), anchorRotation = Q(b.anchorRotation), parentAnchorPosition = V(b.parentAnchorPosition), parentAnchorRotation = Q(b.parentAnchorRotation),
                locks = new[] { b.twistLock.ToString(), b.swingYLock.ToString(), b.swingZLock.ToString() }, activeCount = l.dofs,
                secondPhysicalAxis = l.coordinates.SecondPhysicalAxis, nativeCount = b.dofCount,
                solverIterations = b.solverIterations, solverVelocityIterations = b.solverVelocityIterations,
                gravity = b.useGravity, immovable = b.immovable, drives = new[] { D(b.xDrive), D(b.yDrive), D(b.zDrive) } };
        }).ToArray() };
        internal static TendonMechanicsDiagnostic Prepare(WorldController world, string mode)
        {
            if (!Modes.Contains(mode) || !world.IsolatedQualification || world.Connected || world.Running || world.physicsSteps != 0 || world.Avatar.DirectTendonForcesEnabled)
                throw new InvalidOperationException("Mechanical intervention requires its declared fresh isolated world.");
            var result = new TendonMechanicsDiagnostic(world.Avatar, mode);
            foreach (var link in world.Avatar.links.Values)
            {
                var b = link.body;
                switch (mode)
                {
                    case "original": break;
                    case "no-joint-friction": b.jointFriction = 0; break;
                    case "no-body-damping": b.linearDamping = 0; b.angularDamping = 0; break;
                    case "joint-speed-floor": if (link.dofs > 0) b.maxJointVelocity = Mathf.Max(b.maxJointVelocity, 1000f); break;
                    case "free-active-axes":
                        if (link.dofs > 0) b.twistLock = ArticulationDofLock.FreeMotion;
                        if (link.dofs == 2)
                        {
                            if (link.coordinates.SecondPhysicalAxis == 1) b.swingYLock = ArticulationDofLock.FreeMotion;
                            else b.swingZLock = ArticulationDofLock.FreeMotion;
                        }
                        break;
                    case "no-gravity": b.useGravity = false; break;
                }
            }
            result.evidence.intervention = Capture(world.Avatar);
            return result;
        }
        internal void Bound()
        {
            if (!avatar.DirectTendonForcesEnabled || ownedJson != null) throw new InvalidOperationException("Require newly owned tendon mechanics.");
            evidence.owned = Capture(avatar); ownedJson = JsonUtility.ToJson(evidence.owned);
        }
        internal void CheckUnchanged()
        {
            var current = Capture(avatar);
            if (ownedJson == null || JsonUtility.ToJson(current) != ownedJson)
            {
                evidence.changedDuringCapture = current;
                throw new InvalidOperationException("Mechanical diagnostic settings changed during capture.");
            }
        }
        internal string Json() => JsonUtility.ToJson(evidence);
    }
}
