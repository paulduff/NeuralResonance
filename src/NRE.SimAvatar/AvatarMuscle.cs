using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

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

    public void Advance(double excitation, double lengthFraction, double dt)
    {
        var target = Math.Clamp(excitation, 0.0, 1.0);
        var timeConstant = target > Activation ? ActivationRiseSeconds : ActivationFallSeconds;
        var alpha = 1.0 - Math.Exp(-dt / timeConstant);
        Activation += (target - Activation) * alpha;

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

        if (target <= 0.0 && Activation < RelaxedActivationEpsilon)
        {
            Activation = 0.0;
        }

        var lengthDeparture = (LengthFraction - 1.0) / 0.42;
        var forceLength = Math.Exp(-(lengthDeparture * lengthDeparture));
        var shorteningPenalty = Math.Clamp(1.0 - Math.Max(0.0, -VelocityPerSecond) * 0.10, 0.45, 1.0);
        // Exhaustion is a property of the muscle plant. A maximally fatigued
        // muscle cannot preserve a hidden reserve of holding force; removing
        // excitation also remains a true zero-force, recovering state.
        var fatigueCapacity = FatigueCapacityFraction;
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
    private readonly double inertia;
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
        this.inertia = inertia;
        this.momentArm = momentArm;
        this.passiveStiffness = passiveStiffness;
        this.damping = damping;
        Angle = restAngle;
    }

    public AvatarMuscle Flexor { get; }
    public AvatarMuscle Extensor { get; }
    public double Angle { get; private set; }
    public double AngularVelocity { get; private set; }

    public void Advance(double targetAngle, double descendingGain, double tone, double dt)
    {
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

        var span = Math.Max(0.1, maximum - minimum);
        var normalized = (Angle - restAngle) / span;
        Flexor.Advance(flexorExcitation, 1.0 - (normalized * 0.34), dt);
        Extensor.Advance(extensorExcitation, 1.0 + (normalized * 0.34), dt);

        var activeTorque = (Flexor.ForceNewtons - Extensor.ForceNewtons) * momentArm;
        var passiveTorque = -((Angle - restAngle) * passiveStiffness) - (AngularVelocity * damping);
        var acceleration = Math.Clamp((activeTorque + passiveTorque) / inertia, -32.0, 32.0);
        AngularVelocity = Math.Clamp(AngularVelocity + (acceleration * dt), -5.5, 5.5);
        var next = Angle + (AngularVelocity * dt);
        if (next <= minimum)
        {
            Angle = minimum;
            if (AngularVelocity < 0.0)
            {
                AngularVelocity = 0.0;
            }
        }
        else if (next >= maximum)
        {
            Angle = maximum;
            if (AngularVelocity > 0.0)
            {
                AngularVelocity = 0.0;
            }
        }
        else
        {
            Angle = next;
        }
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

public readonly record struct AvatarMechanicalOutput(
    double ForwardSpeedMetersPerSecond,
    double TurnRateDegreesPerSecond,
    double SupportFraction,
    double UprightFraction,
    double BodyHeightMeters);

public readonly record struct AvatarCollisionProbe(
    string Region,
    double BodyX,
    double BodyY,
    double BodyZ,
    double RadiusMeters,
    bool LoadBearing);

public readonly record struct AvatarGroundContactProbe(
    string Region,
    double BodyX,
    double BodyY,
    double BodyZ,
    double LoadNewtons,
    double AreaSquareMillimeters);
