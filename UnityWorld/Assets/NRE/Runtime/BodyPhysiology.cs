using System;

namespace Nre.UnityWorld
{
    public sealed class BodyPhysiology
    {
        public AvatarPhysiologyState State { get; private set; } =
            AvatarWorldDynamics.CreateRespawnState(AvatarWorldDefaults.Options);
        public double ContactDamage { get; private set; }
        public double MotorCapacity => AvatarWorldDynamics.AssessVitalState(State,
            AvatarWorldDefaults.Options).MotorCapacity;

        public void Advance(double seconds)
        {
            State = AvatarWorldDynamics.AdvancePhysiology(State, AvatarWorldDefaults.Options,
                seconds, 1.0, sleeping: false, inShelter: false);
        }

        public void Contact(string region, double force, double impulse, double area,
            double continuousSeconds, double sampleSeconds, double impactImpulse)
        {
            var assessment = AvatarWorldDynamics.ApplyPhysicalContact(State,
                new AvatarPhysicalContactExposure(region, force, impulse, area,
                    continuousSeconds, sampleSeconds, impactImpulse));
            State = assessment.State;
            ContactDamage += assessment.DamageFraction;
        }

        // A contact impulse contains support as well as impact. Charge an onset
        // only with appreciable pre-solve approach speed, after conservatively
        // removing one whole-body gravity impulse. This estimate needs calibration.
        public static double EstimateImpact(bool onset, double approachSpeed,
            double solverImpulse, double bodyMass, double seconds)
        {
            if (!onset || approachSpeed <= 0.2) return 0;
            return Math.Max(0, solverImpulse - bodyMass * 9.81 * seconds);
        }
    }
}
