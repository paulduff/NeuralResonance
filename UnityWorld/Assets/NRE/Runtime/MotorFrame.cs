using System;
using System.Linq;

namespace Nre.UnityWorld
{
    [Serializable]
    public sealed class MotorFrame
    {
        public bool active;
        public long tick, sequence;
        public float confidence, minimumOutputConfidence;
        public float leftDrive, rightDrive, leftHipCoronalDrive, rightHipCoronalDrive,
            leftAnkleSagittalDrive, rightAnkleSagittalDrive, leftAnkleCoronalDrive,
            rightAnkleCoronalDrive, leftShoulderSagittalDrive, rightShoulderSagittalDrive,
            leftShoulderCoronalDrive, rightShoulderCoronalDrive, leftElbowDrive, rightElbowDrive,
            headYawDrive, headPitchDrive, trunkYawDrive, standDrive;

        public bool IsAuthorized() => active && IsFinite() &&
            minimumOutputConfidence >= 0 && confidence >= minimumOutputConfidence;
        public bool IsFinite() => new[] { confidence, minimumOutputConfidence,
            leftDrive, rightDrive, leftHipCoronalDrive, rightHipCoronalDrive,
            leftAnkleSagittalDrive, rightAnkleSagittalDrive, leftAnkleCoronalDrive,
            rightAnkleCoronalDrive, leftShoulderSagittalDrive, rightShoulderSagittalDrive,
            leftShoulderCoronalDrive, rightShoulderCoronalDrive, leftElbowDrive, rightElbowDrive,
            headYawDrive, headPitchDrive, trunkYawDrive, standDrive }
            .All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 1);
    }
}
