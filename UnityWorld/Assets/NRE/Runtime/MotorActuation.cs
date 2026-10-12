using System;
using System.Collections.Generic;

namespace Nre.UnityWorld
{
    public readonly struct JointActuation
    {
        public readonly string Joint;
        public readonly float First, Second, Stiffness;
        public JointActuation(string joint, float first, float second = 0, float stiffness = 0)
        { Joint = joint; First = first; Second = second; Stiffness = stiffness; }
    }

    public static class MotorActuation
    {
        public static IReadOnlyList<JointActuation> Build(MotorFrame command,
            AvatarSpinalLocomotorState population, double capacity)
        {
            if (!command.IsFinite()) throw new ArgumentException("Motor measurements must be bounded and finite.");
            if (double.IsNaN(capacity) || double.IsInfinity(capacity)) throw new ArgumentException("Invalid capacity.");
            float scale = (float)Math.Clamp(capacity, 0, 1);
            var output = new List<JointActuation>();
            void Add(string name, float first, float second = 0, float stiffness = 0) =>
                output.Add(new JointActuation(name, first * scale, second * scale, stiffness * scale));
            void Leg(string side, float drive, double cycle, double flexor, float coronal, float ankle, float roll)
            {
                // Each leg retains its own descending sign and recruitment.
                // A shared spinal population does not merge opposing commands.
                float recruitment = Math.Abs(drive);
                Add(side + "_thigh", (float)cycle * 70 * drive, coronal * 70, command.standDrive * 80);
                Add(side + "_shin", (float)flexor * 35 * recruitment, stiffness: command.standDrive * 120);
                Add(side + "_foot", ankle * 65 + (float)cycle * 10 * recruitment, roll * 35);
            }
            Leg("left", command.leftDrive, population.LeftCycle, population.LeftFlexorRecruitment,
                command.leftHipCoronalDrive, command.leftAnkleSagittalDrive, command.leftAnkleCoronalDrive);
            Leg("right", command.rightDrive, population.RightCycle, population.RightFlexorRecruitment,
                command.rightHipCoronalDrive, command.rightAnkleSagittalDrive, command.rightAnkleCoronalDrive);
            Add("left_arm", command.leftShoulderSagittalDrive * 40, command.leftShoulderCoronalDrive * 40);
            Add("right_arm", command.rightShoulderSagittalDrive * 40, command.rightShoulderCoronalDrive * 40);
            Add("left_forearm", command.leftElbowDrive * 25);
            Add("right_forearm", command.rightElbowDrive * 25);
            Add("chest", command.trunkYawDrive * 50);
            Add("head", command.headPitchDrive * 8, command.headYawDrive * 8);
            return output;
        }
    }
}
