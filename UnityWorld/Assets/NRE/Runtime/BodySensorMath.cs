using System;
using System.Linq;
using System.Numerics;

namespace Nre.UnityWorld
{
    public static class BodySensorMath
    {
        // AvatarBalanceDynamics.ResolveBalanceError normalizes its support-margin
        // component over 0.22 m. Keep raw distance in balance.supportMarginMeters.
        public static float NormalizedSupportError(float marginMeters)=>Math.Clamp(-marginMeters/.22f,0,1);
        // A body lying inside its floor-contact hull is supported but still fallen.
        // Preserve Avatar's phase and tilt components, alongside the raw margin.
        // The Unity phase classifier has no instability timer/control allowance yet.
        public static float PhysicalBalanceError(float marginMeters,float pitch,float roll,string phase)
        {
            if(new[]{marginMeters,pitch,roll}.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))
                throw new ArgumentException("Invalid physical balance measurement.");
            if(!Enum.TryParse((phase??"").Replace("_",""),true,out AvatarBalancePhase parsed)||
                !Enum.IsDefined(typeof(AvatarBalancePhase),parsed))
                throw new ArgumentException("Unknown physical balance phase.");
            return (float)AvatarBalanceSensor.ResolveBalanceError(marginMeters,0,0,
                Math.Max(Math.Abs(pitch),Math.Abs(roll)),parsed);
        }
        // Body receptor range is 0..5000 N; contact events/physics keep full load.
        public static float HandReceptorLoad(float measuredNewtons)=>Math.Clamp(measuredNewtons,0,5000);
        // Whole-body tilt is already present in the balance fields. The trunk
        // spindle must report chest motion relative to the pelvis, not repeat it.
        public static (float pitch,float roll) RelativeTrunkTilt(Quaternion pelvis,Quaternion chest)
        {
            var relative=Quaternion.Normalize(Quaternion.Inverse(pelvis)*chest);
            var up=Vector3.Transform(Vector3.UnitY,relative);
            return (MathF.Asin(Math.Clamp(up.Z,-1,1)),MathF.Asin(Math.Clamp(-up.X,-1,1)));
        }
    }
}
