namespace NRE.SimAvatar;

/// <summary>
/// Tunable parameters for shared avatar kinematics. Each sim owns its own
/// instance with its own limits, so identical machinery can drive a maze avatar
/// (clamped forward, no reverse, tighter turn cap) and an open-world avatar
/// (allows reverse, faster sprint cap, wider turn cap).
/// </summary>
public sealed record AvatarKinematicsOptions(
    double MaxMotorDrive = 240.0,
    double ForwardSpeedCoefficient = 0.0125,
    double TurnSpeedCoefficient = 3.2,
    double MinForwardSpeed = 0.0,
    double MaxForwardSpeed = double.PositiveInfinity,
    double MaxTurnRateDeg = double.PositiveInfinity,
    bool AllowSignedMotorDrive = false,
    bool InPlaceTurnCancelsForwardDrive = false);

/// <summary>
/// Static helpers for the parts of avatar kinematics that are identical between
/// the maze and world simulators: motor-drive accumulation from spike traces,
/// drive decay, brain-driven (forward, turn-rate) translation, heading advance,
/// and forward-direction unit vector. Each sim still owns its position state
/// and applies its own collision rules.
/// </summary>
public static class AvatarKinematics
{
    /// <summary>
    /// Multiplicatively decay motor drives by <paramref name="smoothing"/> (typically 0.85–0.95).
    /// Pass the decayed values back to the caller's storage.
    /// </summary>
    public static void ApplyDriveDecay(ref double leftMotorDrive, ref double rightMotorDrive, double smoothing)
    {
        leftMotorDrive *= smoothing;
        rightMotorDrive *= smoothing;
    }

    /// <summary>
    /// Convert a batch of dispatch spikes into per-side motor input via
    /// <see cref="AvatarMotorCatalog.SummarizeMotorDrive"/> and add the contributions
    /// to <paramref name="leftMotorDrive"/> / <paramref name="rightMotorDrive"/>,
    /// clamped to <see cref="AvatarKinematicsOptions.MaxMotorDrive"/>. Returns the
    /// summary so callers can branch on motor-event count, raw inputs, or anything else.
    /// </summary>
    public static AvatarMotorDriveSummary IntegrateMotorSpikes(
        IReadOnlyList<AvatarDispatchSpike> dispatches,
        ref double leftMotorDrive,
        ref double rightMotorDrive,
        AvatarKinematicsOptions options)
    {
        var summary = AvatarMotorCatalog.SummarizeMotorDrive(dispatches);
        if (summary.MotorEvents > 0)
        {
            if (summary.InPlaceTurnEvents > 0 && options.InPlaceTurnCancelsForwardDrive)
            {
                var commonForwardDrive = (leftMotorDrive + rightMotorDrive) * 0.5;
                leftMotorDrive -= commonForwardDrive;
                rightMotorDrive -= commonForwardDrive;
            }

            var minimumDrive = options.AllowSignedMotorDrive ? -options.MaxMotorDrive : 0.0;
            leftMotorDrive = Math.Clamp(leftMotorDrive + summary.LeftInput, minimumDrive, options.MaxMotorDrive);
            rightMotorDrive = Math.Clamp(rightMotorDrive + summary.RightInput, minimumDrive, options.MaxMotorDrive);
        }

        return summary;
    }

    /// <summary>
    /// Translate the current L/R motor drives into a (forwardSpeed, turnRateDeg) pair using the
    /// configured coefficients. Both axes are clamped to the configured min/max.
    /// </summary>
    /// <param name="forwardGain">Multiplier on forward speed before clamping (defaults to 1.0).</param>
    /// <param name="turnGain">Multiplier on turn rate before clamping (defaults to 1.0).</param>
    public static (double ForwardSpeed, double TurnRateDeg) ComputeBrainMotorOutput(
        double leftMotorDrive,
        double rightMotorDrive,
        AvatarKinematicsOptions options,
        double forwardGain = 1.0,
        double turnGain = 1.0)
    {
        var rawForward = (leftMotorDrive + rightMotorDrive) * options.ForwardSpeedCoefficient * forwardGain;
        var rawTurn = (rightMotorDrive - leftMotorDrive) * options.TurnSpeedCoefficient * turnGain;

        var forward = Math.Clamp(rawForward, options.MinForwardSpeed, options.MaxForwardSpeed);
        var turn = Math.Clamp(rawTurn, -options.MaxTurnRateDeg, options.MaxTurnRateDeg);
        return (forward, turn);
    }

    /// <summary>Advance heading by turnRateDeg * dt and normalize to [0, 360).</summary>
    public static double AdvanceHeading(double headingDeg, double turnRateDeg, double dt)
        => NormalizeDegrees(headingDeg + (turnRateDeg * dt));

    /// <summary>Forward direction unit vector (dx, dz) for the supplied heading in degrees.</summary>
    public static (double DirX, double DirZ) ForwardDirection(double headingDeg)
    {
        var rad = DegreesToRadians(headingDeg);
        return (Math.Sin(rad), Math.Cos(rad));
    }

    public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    public static double NormalizeDegrees(double angle)
    {
        var wrapped = angle % 360.0;
        if (wrapped < 0)
        {
            wrapped += 360.0;
        }

        return wrapped;
    }
}
