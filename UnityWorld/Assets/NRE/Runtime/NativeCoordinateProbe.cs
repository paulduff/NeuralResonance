using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nre.UnityWorld
{
    // A two-link fixture checks the engine boundary independently of our avatar
    // adapter. Quaternion rotation verifies which physical axis a native slot
    // affects, rather than using the adapter to validate itself.
    public static class NativeCoordinateProbe
    {
        [Serializable]
        public sealed class Sample
        {
            public string axes, operation;
            public int slot, direction, nativeCount;
            public float[] position, velocity, force;
            public Vector3 rotationRadians;
        }

        [Serializable]
        public sealed class Report
        {
            public string unity;
            public bool passed;
            public List<Sample> samples = new List<Sample>();
        }

        public static Report Run()
        {
            var report = new Report { unity = Application.unityVersion };
            foreach (bool secondIsY in new[] { true, false })
                foreach (string operation in new[] { "force", "position", "velocity", "drive" })
                    for (int slot = 0; slot < (operation == "drive" ? 1 : 3); slot++)
                        foreach (int direction in new[] { 1, -1 })
                            report.samples.Add(Measure(secondIsY, operation, slot, direction));
            report.passed = report.samples.Count == 40 && report.samples.TrueForAll(Valid);
            return report;
        }

        static bool Valid(Sample sample)
        {
            if (sample.nativeCount != 3 || !Finite(sample.position) ||
                !Finite(sample.velocity) || !Finite(sample.force)) return false;
            var rotation = sample.rotationRadians;
            if (!Finite(new[] { rotation.x, rotation.y, rotation.z })) return false;
            if (sample.slot == 2) return rotation.magnitude < .002f;
            int physicalAxis = sample.slot == 0 ? 0 : sample.axes == "XY" ? 1 : 2;
            int storageSlot = sample.slot < 0 ? 1 : sample.slot;
            float minimum = sample.operation == "velocity" ? .001f :
                sample.operation == "position" ? .15f : .05f;
            if (sample.direction * rotation[physicalAxis] < minimum ||
                Mathf.Abs(sample.position[storageSlot] - rotation[physicalAxis]) > .002f) return false;
            for (int axis = 0; axis < 3; axis++)
                if (axis != physicalAxis && Mathf.Abs(rotation[axis]) > .002f) return false;
            return true;
        }

        static bool Finite(float[] values) => Array.TrueForAll(values, v => !float.IsNaN(v) && !float.IsInfinity(v));

        static Sample Measure(bool secondIsY, string operation, int slot, int direction)
        {
            var rig = new GameObject("Isolated native coordinate fixture");
            rig.SetActive(false);
            rig.transform.position = new Vector3(40, 4, 0);
            var root = rig.AddComponent<ArticulationBody>();
            root.immovable = true;
            root.useGravity = false;
            rig.AddComponent<BoxCollider>().size = new Vector3(.2f, .2f, .2f);
            var moving = new GameObject("Moving link");
            moving.transform.SetParent(rig.transform, false);
            var collider = moving.AddComponent<BoxCollider>();
            collider.size = new Vector3(.2f, .4f, .3f);
            collider.center = new Vector3(0, -.2f, 0);
            var joint = moving.AddComponent<ArticulationBody>();
            joint.mass = 1;
            joint.useGravity = false;
            joint.linearDamping = joint.angularDamping = 0;
            joint.sleepThreshold = 0;
            joint.jointType = ArticulationJointType.SphericalJoint;
            joint.matchAnchors = false;
            joint.anchorPosition = joint.parentAnchorPosition = Vector3.zero;
            joint.anchorRotation = joint.parentAnchorRotation = Quaternion.identity;
            joint.twistLock = ArticulationDofLock.LimitedMotion;
            joint.swingYLock = secondIsY ? ArticulationDofLock.LimitedMotion : ArticulationDofLock.LockedMotion;
            joint.swingZLock = secondIsY ? ArticulationDofLock.LockedMotion : ArticulationDofLock.LimitedMotion;
            var drive = new ArticulationDrive { lowerLimit = -45, upperLimit = 45, forceLimit = 10 };
            joint.xDrive = joint.yDrive = joint.zDrive = drive;
            Physics.IgnoreCollision(rig.GetComponent<Collider>(), collider);
            rig.SetActive(true);
            Physics.SyncTransforms();
            try
            {
                Physics.Simulate(WorldController.StepSeconds);
                int count = joint.dofCount;
                var values = new float[3];
                values[slot] = direction * (operation == "force" ? .25f : .2f);
                var native = new ArticulationReducedSpace(values[0], values[1], values[2]);
                joint.jointPosition = new ArticulationReducedSpace(0, 0, 0);
                joint.jointVelocity = new ArticulationReducedSpace(0, 0, 0);
                joint.jointForce = new ArticulationReducedSpace(0, 0, 0);
                if (operation == "position") joint.jointPosition = native;
                if (operation == "velocity") joint.jointVelocity = native;
                if (operation == "drive")
                {
                    drive.stiffness = 10;
                    drive.damping = 1;
                    drive.target = direction * .2f * Mathf.Rad2Deg;
                    if (secondIsY) joint.yDrive = drive;
                    else joint.zDrive = drive;
                }
                int steps = operation == "position" || operation == "velocity" ? 1 : 25;
                for (int i = 0; i < steps; i++)
                {
                    if (operation == "force") joint.jointForce = native;
                    Physics.Simulate(WorldController.StepSeconds);
                }
                var rotation = Quaternion.Inverse(root.transform.rotation) * joint.transform.rotation;
                rotation.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180) angle -= 360;
                return new Sample { axes = secondIsY ? "XY" : "XZ", operation = operation,
                    slot = operation == "drive" ? -1 : slot, direction = direction, nativeCount = count,
                    position = Values(joint.jointPosition), velocity = Values(joint.jointVelocity),
                    force = Values(joint.jointForce), rotationRadians = axis * (angle * Mathf.Deg2Rad) };
            }
            finally
            {
                rig.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(rig);
                else UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        static float[] Values(ArticulationReducedSpace value)
        {
            var result = new float[value.dofCount];
            for (int i = 0; i < result.Length; i++) result[i] = value[i];
            return result;
        }
    }
}
