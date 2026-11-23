using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    // Opt-in native observation only. Never sets forces, poses, clocks or limits.
    // GetJointExternalForces reports COUNTERACTING reduced forces, not applied
    // joint torques. Preserve that distinction when analysing this evidence.
    internal sealed class NativeTendonForceTrace
    {
        [Serializable] sealed class Body
        {
            public string id;
            public int nativeIndex, dofCount, dofStart;
            public bool sleeping;
            public string[] centerBits, accumulatedForceBits, accumulatedTorqueBits;
            public string[] jointPositionBits, jointVelocityBits;
        }
        [Serializable] sealed class Observation
        {
            public string phase;
            public int applicationCount, nativeDofCount;
            public Body[] bodies;
            public string[] reducedCounteractingForceBits;
        }
        [Serializable] sealed class Evidence
        {
            public string version = "native-tendon-force-observation-v1", physicsStepSecondsBits;
            public long startStep;
            public bool observationOnly = true;
            public Observation[] observations;
        }
        readonly ArticulatedAvatar avatar;
        readonly float stepSeconds;
        readonly long startStep;
        readonly List<Observation> observations = new List<Observation>();
        internal NativeTendonForceTrace(ArticulatedAvatar avatar, float stepSeconds, long startStep)
        {
            this.avatar = avatar ?? throw new ArgumentNullException(nameof(avatar));
            if (!avatar.DirectTendonForcesEnabled || avatar.PhysicalStepSeconds != stepSeconds || startStep < 0)
                throw new InvalidOperationException("Require the actual isolated owner clock.");
            this.stepSeconds = stepSeconds; this.startStep = startStep;
        }
        static string[] Bits(Vector3 value) => new[] { MuscleHingeWire.Encode(value.x), MuscleHingeWire.Encode(value.y), MuscleHingeWire.Encode(value.z) };
        static string[] Bits(ArticulationReducedSpace value) => Enumerable.Range(0, value.dofCount).Select(i => MuscleHingeWire.Encode(value[i])).ToArray();
        internal void Record(string phase, int applicationCount)
        {
            var starts = new List<int>(); avatar.pelvis.GetDofStartIndices(starts);
            var counteracting = new List<float>();
            int count = avatar.pelvis.GetJointExternalForces(counteracting, stepSeconds);
            if (count != counteracting.Count || applicationCount < 0)
                throw new InvalidOperationException("Native force observation is incomplete.");
            var bodies = avatar.links.Values.OrderBy(l => l.name, StringComparer.Ordinal).Select(l => {
                var b = l.body;
                if (b.index < 0 || b.index >= starts.Count || starts[b.index] < 0 || starts[b.index] + b.dofCount > count)
                    throw new InvalidOperationException("Native joint observation has no declared coordinate range.");
                return new Body { id = l.name, nativeIndex = b.index, dofCount = b.dofCount, dofStart = starts[b.index],
                    sleeping = b.IsSleeping(), centerBits = Bits(b.worldCenterOfMass),
                    accumulatedForceBits = Bits(b.GetAccumulatedForce(stepSeconds)), accumulatedTorqueBits = Bits(b.GetAccumulatedTorque(stepSeconds)),
                    jointPositionBits = Bits(b.jointPosition), jointVelocityBits = Bits(b.jointVelocity) };
            }).ToArray();
            observations.Add(new Observation { phase = phase, applicationCount = applicationCount, nativeDofCount = count,
                bodies = bodies, reducedCounteractingForceBits = counteracting.Select(x => MuscleHingeWire.Encode(x)).ToArray() });
        }
        internal string Json() => JsonUtility.ToJson(new Evidence { startStep = startStep,
            physicsStepSecondsBits = MuscleHingeWire.Encode(stepSeconds), observations = observations.ToArray() });
    }
}
