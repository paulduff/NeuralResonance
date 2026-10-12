using System;

namespace Nre.UnityWorld
{
    public readonly struct MotorUnitFatigueParameters
    {
        public readonly double FatigueRate, RecoveryRate, RestMultiplier;
        public MotorUnitFatigueParameters(double fatigueRate, double recoveryRate, double restMultiplier=1)
        {
            if(!Finite(fatigueRate)||!Finite(recoveryRate)||!Finite(restMultiplier)||
                fatigueRate<0||recoveryRate<0||restMultiplier<1||!Finite(recoveryRate*restMultiplier))
                throw new ArgumentException("Invalid motor-unit rates.");
            FatigueRate=fatigueRate;RecoveryRate=recoveryRate;RestMultiplier=restMultiplier;
        }

        // Frey-Law et al. 2012, Table 1 (1/s), with the 2018 3CC-r rest multiplier.
        // Joint-level fits are provisional when applied to individual dynamic fibers.
        public static MotorUnitFatigueParameters PublishedForJoint(string joint)
        {
            if(joint.EndsWith("foot"))return new MotorUnitFatigueParameters(.00589,.00058,15);
            if(joint.EndsWith("shin"))return new MotorUnitFatigueParameters(.01500,.00149,15);
            if(joint.EndsWith("forearm"))return new MotorUnitFatigueParameters(.00912,.00094,15);
            if(joint.EndsWith("arm"))return new MotorUnitFatigueParameters(.01820,.00168,15);
            if(joint=="chest")return new MotorUnitFatigueParameters(.00755,.00075,15);
            // No hip/neck fit in that table: explicitly use the general fit.
            return new MotorUnitFatigueParameters(.00970,.00091,15);
        }
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }

    public sealed class MotorUnitPool
    {
        public const double RecruitmentRate=10, DeactivationRate=10;
        public double RestingFraction { get; private set; }=1;
        public double ActiveFraction { get; private set; }
        public double FatiguedFraction { get; private set; }

        // 3CC/3CC-r: dMR=-C+R*MF, dMA=C-F*MA, dMF=F*MA-R*MF.
        // Conservative simultaneous transfers, substepped for positivity. A unit
        // can occupy exactly one pool; no force floor or second fatigue penalty.
        public void Advance(double targetLoad, double dt, MotorUnitFatigueParameters rates)
        {
            if(!Finite(targetLoad)||targetLoad<0||targetLoad>1||!Finite(dt)||dt<=0||dt>1)
                throw new ArgumentException("Invalid motor-unit input or timestep.");
            double recovery=rates.RecoveryRate*(targetLoad==0?rates.RestMultiplier:1);
            double maximumStep=Math.Min(.001,.5/(RecruitmentRate+rates.FatigueRate+recovery));
            if(!Finite(maximumStep)||maximumStep<=0||dt/maximumStep>1000000)
                throw new ArgumentException("Motor-unit rates exceed the integration budget.");
            int steps=(int)Math.Ceiling(dt/maximumStep);
            double h=dt/steps;
            for(int step=0;step<steps;step++)
            {
                double control=ActiveFraction<targetLoad?
                    RecruitmentRate*Math.Min(targetLoad-ActiveFraction,RestingFraction):
                    DeactivationRate*(targetLoad-ActiveFraction);
                double recruit=control*h;
                double fatigue=rates.FatigueRate*ActiveFraction*h;
                double recovered=recovery*FatiguedFraction*h;
                RestingFraction+=recovered-recruit;
                ActiveFraction+=recruit-fatigue;
                FatiguedFraction+=fatigue-recovered;
            }
        }
        public void Reset(){RestingFraction=1;ActiveFraction=0;FatiguedFraction=0;}
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }

    [Serializable] public sealed class MusclePoolState
    {
        public string name,side;
        public float requestedActivation,restingFraction,activeFraction,fatiguedFraction;
        internal MusclePoolState(AvatarMuscle muscle)
        {
            name=muscle.Name;side=muscle.Side;
            requestedActivation=(float)muscle.DiagnosticRequestedActivation;
            restingFraction=(float)muscle.DiagnosticMotorUnits.RestingFraction;
            activeFraction=(float)muscle.DiagnosticMotorUnits.ActiveFraction;
            fatiguedFraction=(float)muscle.DiagnosticMotorUnits.FatiguedFraction;
        }
    }
}
