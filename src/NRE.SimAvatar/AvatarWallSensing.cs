namespace NRE.SimAvatar;

/// <summary>
/// Shared, geometry-derived wall sensing for embodied simulators. The helper
/// converts ray distances into tactile proximity and keeps collision orienting
/// on one escape side for the duration of a contact episode.
/// </summary>
public static class AvatarWallSensing
{
    public static double ProximityFromRay(
        bool hitWall,
        double distance,
        double bodyRadius,
        double probeRange)
    {
        if (!hitWall || !double.IsFinite(distance) || !double.IsFinite(bodyRadius) ||
            !double.IsFinite(probeRange) || probeRange <= 0.0)
        {
            return 0.0;
        }

        var clearance = Math.Max(0.0, distance - Math.Max(0.0, bodyRadius));
        return Math.Clamp(1.0 - (clearance / probeRange), 0.0, 1.0);
    }

    public static string ResolveEscapeHemisphere(
        string? latchedHemisphere,
        double leftProximity,
        double rightProximity,
        double currentTurnRateDeg,
        double sideDeadband = 0.04)
    {
        if (string.Equals(latchedHemisphere, "L", StringComparison.OrdinalIgnoreCase))
        {
            return "L";
        }

        if (string.Equals(latchedHemisphere, "R", StringComparison.OrdinalIgnoreCase))
        {
            return "R";
        }

        var left = Math.Clamp(double.IsFinite(leftProximity) ? leftProximity : 0.0, 0.0, 1.0);
        var right = Math.Clamp(double.IsFinite(rightProximity) ? rightProximity : 0.0, 0.0, 1.0);
        var deadband = Math.Clamp(double.IsFinite(sideDeadband) ? sideDeadband : 0.04, 0.0, 1.0);

        // Stimulate the escape side: a left-side contact should sustain a
        // rightward orienting response, and vice versa.
        if (left > right + deadband)
        {
            return "R";
        }

        if (right > left + deadband)
        {
            return "L";
        }

        return currentTurnRateDeg < 0.0 ? "L" : "R";
    }
}
