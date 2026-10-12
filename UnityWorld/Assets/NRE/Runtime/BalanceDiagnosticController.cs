using System;

namespace Nre.UnityWorld
{
    // An explicit isolated capability fixture, never part of live DNNE control.
    // It commands only existing muscle targets; it cannot move a root or joint.
    public static class BalanceDiagnosticController
    {
        public static MotorFrame Command(double positionGain, double velocityGain,
            double errorX, double errorZ, double velocityX, double velocityZ,
            double leftSagittal, double rightSagittal, double leftCoronal, double rightCoronal,
            bool supportedUpright,double coronalHipShare=0,double leftHipCoronal=0,double rightHipCoronal=0,double coronalGainRatio=1)
        {
            if(!Array.TrueForAll(new[]{positionGain,velocityGain,errorX,errorZ,velocityX,velocityZ,
                leftSagittal,rightSagittal,leftCoronal,rightCoronal,coronalHipShare,leftHipCoronal,rightHipCoronal,coronalGainRatio},Finite)||
                positionGain<0||velocityGain<0||coronalHipShare<0||coronalHipShare>1||coronalGainRatio<0)
                throw new ArgumentException("Invalid diagnostic balance measurements/gains.");
            var result=new MotorFrame{standDrive=1};
            if(!supportedUpright)return result;
            double sagittal=-positionGain*errorZ-velocityGain*velocityZ;
            double coronal=coronalGainRatio*(positionGain*errorX+velocityGain*velocityX);
            result.leftAnkleSagittalDrive=SignedTarget(leftSagittal+sagittal,"leftAnkle");
            result.rightAnkleSagittalDrive=SignedTarget(rightSagittal+sagittal,"rightAnkle");
            // The bilateral coronal anchors are mirrored in the physical avatar.
            result.leftAnkleCoronalDrive=SignedTarget(leftCoronal+coronal*(1-coronalHipShare),"leftAnkleRoll");
            result.rightAnkleCoronalDrive=SignedTarget(rightCoronal-coronal*(1-coronalHipShare),"rightAnkleRoll");
            // Existing Avatar hips have independent abductor/adductor muscles.
            // Compare their contribution explicitly rather than forcing ankle-only control.
            if(coronalHipShare>0){
                result.leftHipCoronalDrive=SignedTarget(leftHipCoronal+coronal*coronalHipShare,"leftHipAbduction");
                result.rightHipCoronalDrive=SignedTarget(rightHipCoronal-coronal*coronalHipShare,"rightHipAbduction");
            }
            return result;
        }

        public static void AddCoronalOffsets(MotorFrame command,double leftHip,double rightHip,double leftAnkle,double rightAnkle)
        {
            if(command==null||command.active||!command.IsFinite()||
                !Array.TrueForAll(new[]{leftHip,rightHip,leftAnkle,rightAnkle},Finite))
                throw new ArgumentException("Coronal pulses require an inactive diagnostic frame and finite offsets.");
            command.leftHipCoronalDrive=Offset(command.leftHipCoronalDrive,leftHip,"leftHipAbduction");
            command.rightHipCoronalDrive=Offset(command.rightHipCoronalDrive,rightHip,"rightHipAbduction");
            command.leftAnkleCoronalDrive=Offset(command.leftAnkleCoronalDrive,leftAnkle,"leftAnkleRoll");
            command.rightAnkleCoronalDrive=Offset(command.rightAnkleCoronalDrive,rightAnkle,"rightAnkleRoll");
        }

        static float Offset(float drive,double offset,string variable)
        {
            var profile=Array.Find(MuscleProfiles.All,p=>p.Variable==variable)??
                throw new InvalidOperationException("Diagnostic muscle profile is missing: "+variable);
            double radians=drive*(drive>=0?profile.Maximum:-profile.Minimum);
            return SignedTarget(radians+offset,variable);
        }

        static float SignedTarget(double radians,string variable)
        {
            var profile=Array.Find(MuscleProfiles.All,p=>p.Variable==variable)??
                throw new InvalidOperationException("Diagnostic muscle profile is missing: "+variable);
            return (float)Math.Clamp(radians/(radians>=0?profile.Maximum:-profile.Minimum),-1,1);
        }
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
