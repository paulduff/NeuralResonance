#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nre.UnityWorld
{
    public enum MuscleFatiguePolicy { Normal, DiagnosticAnklesSuppressed, DiagnosticAllSuppressed }

    public sealed class MuscleProfile
    {
        public readonly string Variable, Joint, Flexor, Extensor, Side;
        public readonly int Axis;
        public readonly double FlexorForce, ExtensorForce, Minimum, Maximum, Rest, Inertia, MomentArm, Stiffness, Damping;
        public MuscleProfile(string variable, string joint, int axis, string flexor, string extensor, string side,
            double flexorForce, double extensorForce, double minimum, double maximum, double rest,
            double inertia, double momentArm, double stiffness, double damping)
        {
            Variable=variable; Joint=joint; Axis=axis; Flexor=flexor; Extensor=extensor; Side=side;
            FlexorForce=flexorForce; ExtensorForce=extensorForce; Minimum=minimum; Maximum=maximum;
            Rest=rest; Inertia=inertia; MomentArm=momentArm; Stiffness=stiffness; Damping=damping;
        }
    }

    public readonly struct MuscleMeasurement
    {
        public readonly string Name, Side;
        public readonly double Activation, ForceNewtons, LengthFraction, VelocityPerSecond, FatigueFraction;
        internal MuscleMeasurement(PhysicalMuscleMeasurement value, double capacity)
        { Name=value.Name; Side=value.Side; Activation=value.Activation; ForceNewtons=value.ForceNewtons*capacity;
          LengthFraction=value.LengthFraction; VelocityPerSecond=value.VelocityPerSecond; FatigueFraction=value.FatigueFraction; }
    }

    public sealed class MusclePlant
    {
        // Opt-in isolated comparison policy. Canonical recruitment remains the default.
        public bool PreserveActiveCapacity { get; internal set; }
        public bool PreserveVelocityCapacity { get; internal set; } = true;
        public MuscleFatiguePolicy DiagnosticFatiguePolicy { get; internal set; }
        public double DiagnosticHipSagittalOffsetRadians { get; internal set; }
        public double DiagnosticStrengthScale { get; }

        public MusclePlant(double diagnosticStrengthScale=1)
        {
            if(!Finite(diagnosticStrengthScale)||diagnosticStrengthScale<=0||MuscleProfiles.All.Any(p=>
                !Finite(p.FlexorForce*diagnosticStrengthScale)||!Finite(p.ExtensorForce*diagnosticStrengthScale)))
                throw new ArgumentException("Invalid muscle strength scale.");
            DiagnosticStrengthScale=diagnosticStrengthScale;
            joints=MuscleProfiles.All.ToDictionary(p=>p,
                p=>new AvatarMuscleJoint(new AvatarMuscle(p.Flexor,p.Side,p.FlexorForce*diagnosticStrengthScale),
                    new AvatarMuscle(p.Extensor,p.Side,p.ExtensorForce*diagnosticStrengthScale),
                    p.Minimum,p.Maximum,p.Rest,p.Inertia,p.MomentArm,p.Stiffness,p.Damping));
        }

        public void RequireLivePolicy()
        {
            if(DiagnosticFatiguePolicy!=MuscleFatiguePolicy.Normal||DiagnosticHipSagittalOffsetRadians!=0||
                PreserveActiveCapacity||!PreserveVelocityCapacity||DiagnosticStrengthScale!=1)
                throw new InvalidOperationException("Diagnostic muscle policies cannot enter live operation.");
        }
        internal readonly Dictionary<MuscleProfile,AvatarMuscleJoint> joints;
        double capacity=1;

        // The neuronal command chooses targets/recruitment. Measured lengths and
        // velocities drive canonical activation, antagonism and fatigue. No root
        // translation or joint pose is calculated or written here.
        public IReadOnlyList<JointActuation> Advance(MotorFrame command, AvatarSpinalLocomotorState spinal,
            Func<string,int,double> angle, Func<string,int,double> velocity, double motorCapacity, double dt,
            Func<string,int,double,double>? boundTarget=null)
        {
            if(!command.IsFinite() || !Finite(motorCapacity) || !Finite(dt) || dt<=0||
                !Finite(DiagnosticHipSagittalOffsetRadians))throw new ArgumentException("Invalid muscle inputs.");
            capacity=Math.Clamp(motorCapacity,0,1);
            var output=new Dictionary<string,(double first,double second)>();
            foreach(var pair in joints)
            {
                var p=pair.Key;double drive=Drive(p,command,spinal);
                bool leg=p.Joint.EndsWith("thigh")||p.Joint.EndsWith("shin")||p.Joint.EndsWith("foot");
                double gain=Math.Clamp(Math.Abs(drive)+(leg?command.standDrive:0),0,1);
                double tone=leg?Math.Max(0,command.standDrive)*.1:0;
                double target=drive==0?p.Rest:drive>0?drive*p.Maximum:-drive*p.Minimum;
                if(p.Variable=="leftHip"||p.Variable=="rightHip")target=DiagnosticHipSagittalOffsetRadians+drive*(drive>=0?
                    MuscleWorkingRanges.WalkingHipFlexionRadians:MuscleWorkingRanges.WalkingHipExtensionRadians);
                if(p.Variable=="leftKnee"||p.Variable=="rightKnee")target=p.Rest+drive*MuscleWorkingRanges.WalkingKneeFlexionRadians;
                if(boundTarget!=null)target=boundTarget(p.Joint,p.Axis,target);
                double measured=angle(p.Joint,p.Axis),speed=velocity(p.Joint,p.Axis);
                if(!Finite(measured)||!Finite(speed))throw new ArgumentException("Invalid measured muscle state.");
                bool suppressFatigue=DiagnosticFatiguePolicy==MuscleFatiguePolicy.DiagnosticAllSuppressed||
                    (DiagnosticFatiguePolicy==MuscleFatiguePolicy.DiagnosticAnklesSuppressed&&p.Joint.EndsWith("foot"));
                double torque=pair.Value.AdvanceMeasured(target,gain,tone,measured,speed,dt,PreserveActiveCapacity,PreserveVelocityCapacity,suppressFatigue)*capacity;
                output.TryGetValue(p.Joint,out var effort);
                output[p.Joint]=p.Axis==0?(torque,effort.second):(effort.first,torque);
            }
            return output.Select(p=>new JointActuation(p.Key,(float)p.Value.first,(float)p.Value.second)).ToArray();
        }

        public MuscleMeasurement[] Capture() => joints.Values.SelectMany(j=>new[]{j.Flexor,j.Extensor})
            .Select(m=>new MuscleMeasurement(m.Capture(),capacity)).ToArray();

        static double Drive(MuscleProfile profile, MotorFrame c, AvatarSpinalLocomotorState s)
        {
            switch(profile.Variable)
            {
                case "leftHip":return s.LeftCycle*c.leftDrive;
                case "rightHip":return s.RightCycle*c.rightDrive;
                case "leftKnee":return s.LeftFlexorRecruitment*Math.Abs(c.leftDrive);
                case "rightKnee":return s.RightFlexorRecruitment*Math.Abs(c.rightDrive);
                case "leftHipAbduction":return c.leftHipCoronalDrive;
                case "rightHipAbduction":return c.rightHipCoronalDrive;
                case "leftAnkle":return c.leftAnkleSagittalDrive;
                case "rightAnkle":return c.rightAnkleSagittalDrive;
                case "leftAnkleRoll":return c.leftAnkleCoronalDrive;
                case "rightAnkleRoll":return c.rightAnkleCoronalDrive;
                case "leftShoulder":return c.leftShoulderSagittalDrive;
                case "rightShoulder":return c.rightShoulderSagittalDrive;
                case "leftShoulderAbduction":return c.leftShoulderCoronalDrive;
                case "rightShoulderAbduction":return c.rightShoulderCoronalDrive;
                case "leftElbow":return c.leftElbowDrive;
                case "rightElbow":return c.rightElbowDrive;
                case "neckYaw":return c.headYawDrive;
                case "neckPitch":return c.headPitchDrive;
                case "trunkYaw":return c.trunkYawDrive;
                default:throw new InvalidOperationException("Unmapped muscle joint.");
            }
        }
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
