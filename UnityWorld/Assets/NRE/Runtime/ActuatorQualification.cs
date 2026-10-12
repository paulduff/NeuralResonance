using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    public static class ActuatorQualification
    {
        [Serializable]
        public sealed class AxisResult
        {
            public string joint, nativeLocks;
            public int axis, physicalAxis, nativeCoordinate, nativeCount, unusedCoordinate;
            public float lowerRadians, upperRadians, pulseNewtonMeters;
            public float positiveDisplacement, negativeDisplacement;
            public float positiveRotationRadians, negativeRotationRadians;
            public float maximumLimitViolation, maximumUnusedCoordinate;
            public bool finite = true, passed;
        }

        public static IEnumerator Run(WorldController world, List<AxisResult> results)
        {
            if (world.Connected || !world.DiagnosticMode)
                throw new InvalidOperationException("Actuator calibration requires an isolated diagnostic world.");
            var body = world.Avatar;
            foreach (var link in body.links.Values.Where(l => l.dofs > 0).OrderBy(l => l.name))
            {
                link.coordinates.ValidateNativeCount(link.body.dofCount);
                Debug.Log($"NATIVE MAPPING {link.name}: {link.dofs} active axes, " +
                    $"{link.body.dofCount} native coordinates, second slot={link.coordinates.SecondIndex}, " +
                    $"locks={link.body.twistLock}/{link.body.swingYLock}/{link.body.swingZLock}");
                for (int axis = 0; axis < link.dofs; axis++)
                {
                    var drive = Drive(link, axis);
                    var result = new AxisResult { joint = link.name, axis = axis,
                        physicalAxis = axis == 0 ? 0 : link.coordinates.SecondPhysicalAxis,
                        nativeCoordinate = link.coordinates.NativeIndex(axis), nativeCount = link.body.dofCount,
                        unusedCoordinate = link.coordinates.UnusedIndex,
                        nativeLocks = $"{link.body.twistLock}/{link.body.swingYLock}/{link.body.swingZLock}",
                        lowerRadians = drive.lowerLimit * Mathf.Deg2Rad,
                        upperRadians = drive.upperLimit * Mathf.Deg2Rad,
                        pulseNewtonMeters = link.torqueLimit * .35f };
                    results.Add(result);
                    foreach (int direction in new[] { 1, -1 })
                    {
                        ResetFixture(body);
                        for (int i = 0; i < 5; i++) Simulate(body);
                        float start = body.Angle(link.name, axis);
                        Quaternion startRotation = JointRotation(link);
                        for (int i = 0; i < 25; i++)
                        {
                            body.Torques(link.name, axis == 0 ? direction * result.pulseNewtonMeters : 0,
                                axis == 1 ? direction * result.pulseNewtonMeters : 0);
                            Simulate(body);
                            result.finite &= body.IsFinite();
                            result.maximumLimitViolation = Mathf.Max(result.maximumLimitViolation, LimitViolation(body));
                            result.maximumUnusedCoordinate = Mathf.Max(result.maximumUnusedCoordinate, UnusedCoordinate(body));
                        }
                        float displacement = body.Angle(link.name, axis) - start;
                        var rotation = JointRotation(link) * Quaternion.Inverse(startRotation);
                        rotation.ToAngleAxis(out float angle, out Vector3 rotationAxis);
                        if (angle > 180) angle -= 360;
                        float physicalRotation = rotationAxis[result.physicalAxis] * angle * Mathf.Deg2Rad;
                        if (direction > 0) { result.positiveDisplacement = displacement; result.positiveRotationRadians = physicalRotation; }
                        else { result.negativeDisplacement = displacement; result.negativeRotationRadians = physicalRotation; }
                    }
                    result.passed = result.finite && result.positiveDisplacement > .01f &&
                        result.negativeDisplacement < -.01f && result.maximumLimitViolation < .05f &&
                        result.positiveRotationRadians > .01f && result.negativeRotationRadians < -.01f;
                    Debug.Log($"ACTUATOR {link.name}[{axis}] +{result.positiveDisplacement:F5} " +
                        $"{result.negativeDisplacement:F5} rad, violation {result.maximumLimitViolation:F5}, pass={result.passed}");
                    yield return null;
                }
            }
            body.ZeroActuation();
        }

        static void Simulate(ArticulatedAvatar body)
        {
            body.BeginStep();
            Physics.Simulate(WorldController.StepSeconds);
        }

        static ArticulationDrive Drive(ArticulatedAvatar.Link link, int axis) =>
            axis == 0 ? link.body.xDrive : link.coordinates.SecondPhysicalAxis == 1 ? link.body.yDrive : link.body.zDrive;

        static Quaternion JointRotation(ArticulatedAvatar.Link link) =>
            Quaternion.Inverse(link.body.transform.parent.rotation * link.body.parentAnchorRotation) *
                (link.body.transform.rotation * link.body.anchorRotation);

        static void CenterDrive(ArticulatedAvatar.Link link, int axis)
        {
            var drive = Drive(link, axis);
            drive.stiffness = 8;
            drive.target = (drive.lowerLimit + drive.upperLimit) * .5f;
            drive.targetVelocity = 0;
            if (axis == 0) link.body.xDrive = drive;
            else if (link.coordinates.SecondPhysicalAxis == 1) link.body.yDrive = drive;
            else link.body.zDrive = drive;
        }

        static void ResetFixture(ArticulatedAvatar body)
        {
            // Anchoring, gravity removal and resetting to midpoint poses are
            // calibration-only. No production root or joint is reset this way.
            body.pelvis.immovable = true;
            body.pelvis.TeleportRoot(new Vector3(0, 4, 0), Quaternion.identity);
            body.pelvis.linearVelocity = Vector3.zero;
            body.pelvis.angularVelocity = Vector3.zero;
            foreach (var link in body.links.Values)
            {
                link.body.useGravity = false;
                if (link.dofs == 0) continue;
                for (int axis = 0; axis < link.dofs; axis++) CenterDrive(link, axis);
                float first = Drive(link, 0).target * Mathf.Deg2Rad;
                float second = link.dofs == 2 ? Drive(link, 1).target * Mathf.Deg2Rad : 0;
                body.SetCalibrationPose(link, first, second);
            }
            Physics.SyncTransforms();
        }

        static float LimitViolation(ArticulatedAvatar body)
        {
            float violation = 0;
            foreach (var link in body.links.Values)
                for (int axis = 0; axis < link.dofs; axis++)
                {
                    var drive = Drive(link, axis);
                    float angle = body.Angle(link.name, axis);
                    violation = Mathf.Max(violation, drive.lowerLimit * Mathf.Deg2Rad - angle,
                        angle - drive.upperLimit * Mathf.Deg2Rad);
                }
            return violation;
        }

        static float UnusedCoordinate(ArticulatedAvatar body)
        {
            float maximum = 0;
            foreach (var link in body.links.Values.Where(l => l.dofs == 2))
                maximum = Mathf.Max(maximum, Mathf.Abs(link.body.jointPosition[link.coordinates.UnusedIndex]));
            return maximum;
        }
    }
}
