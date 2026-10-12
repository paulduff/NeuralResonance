// Generated from AvatarMuscle.cs and AvatarArticulatedBody.cs; do not edit.
// Source SHA256: 60efbd73e532d6a9a3192fb633d78e2e260917ea925d40ccb07199114d87ea5c
using System;

namespace Nre.UnityWorld {

internal sealed class AvatarMuscle
{
    private const double ActivationRiseSeconds = 0.045;
    private const double ActivationFallSeconds = 0.090;
    private const double FatigueRatePerSecond = 0.020;
    private const double RecoveryRatePerSecond = 0.032;
    private const double RestingRecoveryActivation = 0.10;
    private const double ActiveRecoveryScale = 0.04;
    private const double RelaxedActivationEpsilon = 0.0001;

    private double previousLength = 1.0;

    public AvatarMuscle(string name, string side, double maximumIsometricForceNewtons)
    {
        Name = name;
        Side = side;
        MaximumIsometricForceNewtons = maximumIsometricForceNewtons;
    }

    public string Name { get; }
    public string Side { get; }
    public double MaximumIsometricForceNewtons { get; }
    public double Activation { get; private set; }
    public double ForceNewtons { get; private set; }
    public double LengthFraction { get; private set; } = 1.0;
    public double VelocityPerSecond { get; private set; }
    public double FatigueFraction { get; private set; }
    public double FatigueCapacityFraction => Math.Pow(1.0 - FatigueFraction, 2.2);

    public MotorUnitPool DiagnosticMotorUnits { get; } = new MotorUnitPool();
    public double DiagnosticRequestedActivation { get; private set; }

    public void Advance(double excitation, double lengthFraction, double dt,
        bool suppressDiagnosticFatigue = false, MotorUnitFatigueParameters? diagnosticPoolParameters = null)
    {
        var target = Math.Clamp(excitation, 0.0, 1.0);
        var recruitmentState = diagnosticPoolParameters.HasValue ? DiagnosticRequestedActivation : Activation;
        var timeConstant = target > recruitmentState ? ActivationRiseSeconds : ActivationFallSeconds;
        var alpha = 1.0 - Math.Exp(-dt / timeConstant);
        if (diagnosticPoolParameters.HasValue)
        {
            DiagnosticRequestedActivation += (target - DiagnosticRequestedActivation) * alpha;
            if (target <= 0 && DiagnosticRequestedActivation < RelaxedActivationEpsilon)
                DiagnosticRequestedActivation = 0;
            DiagnosticMotorUnits.Advance(DiagnosticRequestedActivation, dt, diagnosticPoolParameters.Value);
            Activation = DiagnosticMotorUnits.ActiveFraction;
        }
        else Activation += (target - Activation) * alpha;

        LengthFraction = Math.Clamp(lengthFraction, 0.55, 1.45);
        VelocityPerSecond = (LengthFraction - previousLength) / Math.Max(0.001, dt);
        previousLength = LengthFraction;

        // Recovery is strongest at rest. Once a muscle is carrying a sustained
        // isometric load it may still recover a little through motor-unit
        // rotation, but that recovery cannot erase the cost of holding posture.
        var recoveryScale = Activation <= RestingRecoveryActivation
            ? 1.0
            : ActiveRecoveryScale;
        FatigueFraction = Math.Clamp(
            FatigueFraction +
            (Activation * FatigueRatePerSecond * dt) -
            ((1.0 - Activation) * RecoveryRatePerSecond * recoveryScale * dt),
            0.0,
            1.0);

        if (diagnosticPoolParameters.HasValue) FatigueFraction = DiagnosticMotorUnits.FatiguedFraction;
        else if (suppressDiagnosticFatigue) FatigueFraction = 0.0;

        if (!diagnosticPoolParameters.HasValue && target <= 0.0 && Activation < RelaxedActivationEpsilon)
        {
            Activation = 0.0;
        }

        var lengthDeparture = (LengthFraction - 1.0) / 0.42;
        var forceLength = Math.Exp(-(lengthDeparture * lengthDeparture));
        var shorteningPenalty = Math.Clamp(1.0 - Math.Max(0.0, -VelocityPerSecond) * 0.10, 0.45, 1.0);
        // Exhaustion is a property of the muscle plant. A maximally fatigued
        // muscle cannot preserve a hidden reserve of holding force; removing
        // excitation also remains a true zero-force, recovering state.
        var fatigueCapacity = diagnosticPoolParameters.HasValue ? 1.0 : FatigueCapacityFraction;
        ForceNewtons = MaximumIsometricForceNewtons * Activation *
            (0.35 + (forceLength * 0.65)) * shorteningPenalty * fatigueCapacity;
        if (Activation <= 0.0 || fatigueCapacity < 0.0001)
        {
            ForceNewtons = 0.0;
        }
    }

    public PhysicalMuscleMeasurement Capture()
        => new(
            Name,
            Side,
            (float)Activation,
            (float)ForceNewtons,
            (float)LengthFraction,
            (float)VelocityPerSecond,
            (float)FatigueFraction);

    public void ReconcileLength(double lengthFraction)
    {
        LengthFraction = Math.Clamp(lengthFraction, 0.55, 1.45);
        previousLength = LengthFraction;
        VelocityPerSecond = 0.0;
    }

    public void Reset()
    {
        DiagnosticMotorUnits.Reset();
        DiagnosticRequestedActivation = 0;
        previousLength = 1.0;
        Activation = 0.0;
        ForceNewtons = 0.0;
        LengthFraction = 1.0;
        VelocityPerSecond = 0.0;
        FatigueFraction = 0.0;
    }
}

internal sealed class AvatarMuscleJoint
{
    private readonly double minimum;
    private readonly double maximum;
    private readonly double restAngle;
    private readonly double momentArm;
    private readonly double passiveStiffness;
    private readonly double damping;

    public AvatarMuscleJoint(
        AvatarMuscle flexor,
        AvatarMuscle extensor,
        double minimum,
        double maximum,
        double restAngle,
        double inertia,
        double momentArm,
        double passiveStiffness,
        double damping)
    {
        Flexor = flexor;
        Extensor = extensor;
        this.minimum = minimum;
        this.maximum = maximum;
        this.restAngle = restAngle;
        this.momentArm = momentArm;
        this.passiveStiffness = passiveStiffness;
        this.damping = damping;
        Angle = restAngle;
    }

    public double PassiveStiffness => passiveStiffness;
    public double PassiveDamping => damping;
    public double RestAngle => restAngle;
    public AvatarMuscle Flexor { get; }
    public AvatarMuscle Extensor { get; }
    public double Angle { get; private set; }
    public double AngularVelocity { get; private set; }

    // PhysX supplies position/velocity and performs the only pose integration.
    // Return active muscle torque; native implicit drives supply passive terms.
    public double AdvanceMeasured(double targetAngle, double descendingGain,
        double tone, double measuredAngle, double measuredVelocity, double dt,
        bool preserveActiveCapacity = false, bool preserveVelocityCapacity = true,
        bool suppressDiagnosticFatigue = false, MotorUnitFatigueParameters? diagnosticPoolParameters = null)
    {
        Angle = measuredAngle;
        AngularVelocity = measuredVelocity;
        var boundedTarget = Math.Clamp(targetAngle, minimum, maximum);
        var error = boundedTarget - Angle;
        var reflex = Math.Clamp(Math.Abs(error) * 2.6, 0.0, 1.0) * Math.Clamp(descendingGain, 0.0, 1.0);
        var velocityOpposition = Math.Clamp(Math.Abs(AngularVelocity) * 0.08, 0.0, 0.22);
        var balancedForce = Math.Min(
            Flexor.MaximumIsometricForceNewtons,
            Extensor.MaximumIsometricForceNewtons);
        var flexorExcitation =
            (tone + (error > 0.0 ? reflex : 0.0) + (AngularVelocity < 0.0 ? velocityOpposition : 0.0)) *
            (balancedForce / Flexor.MaximumIsometricForceNewtons);
        var extensorExcitation =
            (tone + (error < 0.0 ? reflex : 0.0) + (AngularVelocity > 0.0 ? velocityOpposition : 0.0)) *
            (balancedForce / Extensor.MaximumIsometricForceNewtons);

        if (preserveActiveCapacity)
        {
            flexorExcitation = tone * (balancedForce / Flexor.MaximumIsometricForceNewtons) +
                (error > 0.0 ? reflex : 0.0) + (AngularVelocity < 0.0 ? velocityOpposition : 0.0) *
                (preserveVelocityCapacity ? 1.0 : balancedForce / Flexor.MaximumIsometricForceNewtons);
            extensorExcitation = tone * (balancedForce / Extensor.MaximumIsometricForceNewtons) +
                (error < 0.0 ? reflex : 0.0) + (AngularVelocity > 0.0 ? velocityOpposition : 0.0) *
                (preserveVelocityCapacity ? 1.0 : balancedForce / Extensor.MaximumIsometricForceNewtons);
        }

        var span = Math.Max(0.1, maximum - minimum);
        var normalized = (Angle - restAngle) / span;
        Flexor.Advance(flexorExcitation, 1.0 - (normalized * 0.34), dt, suppressDiagnosticFatigue, diagnosticPoolParameters);
        Extensor.Advance(extensorExcitation, 1.0 + (normalized * 0.34), dt, suppressDiagnosticFatigue, diagnosticPoolParameters);

        return (Flexor.ForceNewtons - Extensor.ForceNewtons) * momentArm;
    }

    public void Reconcile(double previousAcceptedAngle, double resolvedAngle, double dt)
    {
        var previous = Math.Clamp(previousAcceptedAngle, minimum, maximum);
        Angle = Math.Clamp(resolvedAngle, minimum, maximum);
        AngularVelocity = Math.Clamp(
            (Angle - previous) / Math.Max(0.001, dt),
            -5.5,
            5.5);

        var span = Math.Max(0.1, maximum - minimum);
        var normalized = (Angle - restAngle) / span;
        Flexor.ReconcileLength(1.0 - (normalized * 0.34));
        Extensor.ReconcileLength(1.0 + (normalized * 0.34));
    }

    public void Reset()
    {
        Angle = restAngle;
        AngularVelocity = 0.0;
        Flexor.Reset();
        Extensor.Reset();
    }
}


internal readonly struct PhysicalMuscleMeasurement {
    public readonly string Name, Side;
    public readonly float Activation, ForceNewtons, LengthFraction, VelocityPerSecond, FatigueFraction;
    public PhysicalMuscleMeasurement(string name, string side, float activation, float force,
        float length, float velocity, float fatigue) {
        Name=name; Side=side; Activation=activation; ForceNewtons=force;
        LengthFraction=length; VelocityPerSecond=velocity; FatigueFraction=fatigue;
    }
}
public static class MuscleProfiles {
    public static readonly MuscleProfile[] All = {
            new MuscleProfile("leftHip", "left_thigh", 0, "Iliopsoas", "GluteusMaximus", "L", 1_550, 2_350, -0.35, 2.09, 0.0, 2.6, 0.050, 42.0, 18.0),
            new MuscleProfile("rightHip", "right_thigh", 0, "Iliopsoas", "GluteusMaximus", "R", 1_550, 2_350, -0.35, 2.09, 0.0, 2.6, 0.050, 42.0, 18.0),
            new MuscleProfile("leftHipAbduction", "left_thigh", 1, "GluteusMedius", "AdductorGroup", "L", 1_650, 2_100, -0.45, 0.78, 0.0, 1.8, 0.048, 48.0, 15.0),
            new MuscleProfile("rightHipAbduction", "right_thigh", 1, "GluteusMedius", "AdductorGroup", "R", 1_650, 2_100, -0.45, 0.78, 0.0, 1.8, 0.048, 48.0, 15.0),
            new MuscleProfile("leftKnee", "left_shin", 0, "Hamstrings", "Quadriceps", "L", 1_750, 3_200, 0.0, 2.45, 0.06, 1.7, 0.042, 34.0, 6.8),
            new MuscleProfile("rightKnee", "right_shin", 0, "Hamstrings", "Quadriceps", "R", 1_750, 3_200, 0.0, 2.45, 0.06, 1.7, 0.042, 34.0, 6.8),
            new MuscleProfile("leftAnkle", "left_foot", 0, "TibialisAnterior", "GastrocnemiusSoleus", "L", 720, 2_900, -0.78, 0.52, 0.0, 0.55, 0.038, 22.0, 3.8),
            new MuscleProfile("rightAnkle", "right_foot", 0, "TibialisAnterior", "GastrocnemiusSoleus", "R", 720, 2_900, -0.78, 0.52, 0.0, 0.55, 0.038, 22.0, 3.8),
            new MuscleProfile("leftAnkleRoll", "left_foot", 1, "TibialisPosterior", "FibularisLongusBrevis", "L", 1_050, 1_350, -0.26, 0.52, 0.0, 0.42, 0.032, 18.0, 3.2),
            new MuscleProfile("rightAnkleRoll", "right_foot", 1, "TibialisPosterior", "FibularisLongusBrevis", "R", 1_050, 1_350, -0.26, 0.52, 0.0, 0.42, 0.032, 18.0, 3.2),
            new MuscleProfile("leftShoulder", "left_arm", 0, "AnteriorDeltoid", "LatissimusDorsi", "L", 1_050, 1_250, -0.70, 2.62, 0.0, 0.52, 0.032, 9.0, 2.8),
            new MuscleProfile("rightShoulder", "right_arm", 0, "AnteriorDeltoid", "LatissimusDorsi", "R", 1_050, 1_250, -0.70, 2.62, 0.0, 0.52, 0.032, 9.0, 2.8),
            new MuscleProfile("leftShoulderAbduction", "left_arm", 1, "MiddleDeltoid", "PectoralisMajor", "L", 1_150, 1_420, -0.18, 2.45, 0.0, 0.46, 0.035, 11.0, 3.2),
            new MuscleProfile("rightShoulderAbduction", "right_arm", 1, "MiddleDeltoid", "PectoralisMajor", "R", 1_150, 1_420, -0.18, 2.45, 0.0, 0.46, 0.035, 11.0, 3.2),
            new MuscleProfile("leftElbow", "left_forearm", 0, "BicepsBrachii", "TricepsBrachii", "L", 780, 1_050, 0.0, 2.62, 0.04, 0.22, 0.030, 7.0, 1.8),
            new MuscleProfile("rightElbow", "right_forearm", 0, "BicepsBrachii", "TricepsBrachii", "R", 780, 1_050, 0.0, 2.62, 0.04, 0.22, 0.030, 7.0, 1.8),
            new MuscleProfile("neckYaw", "head", 1, "LeftSpleniusCapitisRightSternocleidomastoid", "RightSpleniusCapitisLeftSternocleidomastoid", "M", 540, 540, -1.35, 1.35, 0.0, 0.18, 0.021, 8.0, 2.4),
            new MuscleProfile("neckPitch", "head", 0, "SpleniusCapitisUpperTrapezius", "LongusColliSternocleidomastoid", "M", 680, 620, -0.78, 0.95, 0.0, 0.20, 0.020, 9.0, 2.6),
            new MuscleProfile("trunkYaw", "chest", 0, "RightExternalObliqueLeftInternalOblique", "LeftExternalObliqueRightInternalOblique", "M", 1_180, 1_180, -0.61, 0.61, 0.0, 3.4, 0.045, 26.0, 6.2),
    };
}
public static class MuscleWorkingRanges {
    public const double WalkingHipFlexionRadians = 0.36;
    public const double WalkingHipExtensionRadians = 0.21;
    public const double WalkingKneeFlexionRadians = 0.62;
}
}
