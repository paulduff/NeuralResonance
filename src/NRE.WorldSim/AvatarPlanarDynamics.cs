namespace NRE.WorldSim;

public readonly record struct AvatarPlanarMotionState(
    double ForwardVelocityMetersPerSecond,
    double TurnVelocityDegreesPerSecond);

/// <summary>
/// Integrates physical planar momentum after the neuronal body plant has produced
/// an achievable movement request. Posture changes alter propulsion and friction;
/// they do not erase existing momentum.
/// </summary>
public static class AvatarPlanarDynamics
{
    private const double MaximumForwardSpeed = 1.8;
    private const double MaximumReverseSpeed = 0.65;
    private const double MaximumTurnRate = 120.0;
    private const double PropulsiveAcceleration = 2.4;
    private const double ActiveBrakingAcceleration = 4.8;
    private const double AirborneLinearDrag = 0.08;
    private const double AirborneAngularDrag = 1.5;
    private const double RightingPropulsionScale = 0.34;
    private const double FallenPropulsionScale = 0.12;

    public static AvatarPlanarMotionState Advance(
        AvatarPlanarMotionState current,
        double requestedForwardSpeed,
        double requestedTurnRate,
        string posture,
        bool grounded,
        double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
        {
            return Sanitize(current);
        }

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.25);
        var state = Sanitize(current);
        var propulsionScale = posture switch
        {
            "standing" or "crouching" => 1.0,
            "righting" => RightingPropulsionScale,
            "falling" or "fallen" => FallenPropulsionScale,
            _ => 0.0
        };
        var forwardRequest = propulsionScale > 0.0 && double.IsFinite(requestedForwardSpeed)
            ? Math.Clamp(requestedForwardSpeed, -MaximumReverseSpeed, MaximumForwardSpeed) * propulsionScale
            : 0.0;
        var turnRequest = propulsionScale > 0.0 && double.IsFinite(requestedTurnRate)
            ? Math.Clamp(requestedTurnRate, -MaximumTurnRate, MaximumTurnRate) * propulsionScale
            : 0.0;

        if (!grounded)
        {
            return new AvatarPlanarMotionState(
                MoveTowards(state.ForwardVelocityMetersPerSecond, 0.0, AirborneLinearDrag * dt),
                MoveTowards(state.TurnVelocityDegreesPerSecond, 0.0, AirborneAngularDrag * dt));
        }

        var linearFriction = posture switch
        {
            "crouching" => 1.25,
            "sitting" => 1.85,
            "lying" => 2.40,
            "righting" => 1.35,
            "falling" or "fallen" => 1.80,
            _ => 0.90
        };
        var angularFriction = posture switch
        {
            "crouching" => 110.0,
            "sitting" => 145.0,
            "lying" => 180.0,
            "righting" => 125.0,
            "falling" or "fallen" => 155.0,
            _ => 80.0
        };

        return new AvatarPlanarMotionState(
            AdvanceAxis(
                state.ForwardVelocityMetersPerSecond,
                forwardRequest,
                PropulsiveAcceleration,
                ActiveBrakingAcceleration,
                linearFriction,
                dt),
            AdvanceAxis(
                state.TurnVelocityDegreesPerSecond,
                turnRequest,
                acceleration: 180.0,
                activeBraking: 260.0,
                passiveFriction: angularFriction,
                dt));
    }

    private static double AdvanceAxis(
        double current,
        double requested,
        double acceleration,
        double activeBraking,
        double passiveFriction,
        double dt)
    {
        if (Math.Abs(requested) < 0.0001)
        {
            return MoveTowards(current, 0.0, passiveFriction * dt);
        }

        var opposing = Math.Abs(current) > 0.0001 && Math.Sign(current) != Math.Sign(requested);
        var slowing = !opposing && Math.Abs(requested) < Math.Abs(current);
        var rate = opposing || slowing ? activeBraking : acceleration;
        return MoveTowards(current, requested, rate * dt);
    }

    private static AvatarPlanarMotionState Sanitize(AvatarPlanarMotionState state)
        => new(
            double.IsFinite(state.ForwardVelocityMetersPerSecond)
                ? Math.Clamp(state.ForwardVelocityMetersPerSecond, -MaximumReverseSpeed, MaximumForwardSpeed)
                : 0.0,
            double.IsFinite(state.TurnVelocityDegreesPerSecond)
                ? Math.Clamp(state.TurnVelocityDegreesPerSecond, -MaximumTurnRate, MaximumTurnRate)
                : 0.0);

    private static double MoveTowards(double current, double target, double maximumDelta)
    {
        var delta = target - current;
        if (Math.Abs(delta) <= maximumDelta)
        {
            return target;
        }

        return current + (Math.Sign(delta) * maximumDelta);
    }
}
