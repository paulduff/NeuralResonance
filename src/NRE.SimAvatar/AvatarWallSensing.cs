namespace NRE.SimAvatar;

/// <summary>
/// Shared, geometry-derived wall sensing for embodied simulators. The helper
/// converts physical ray distances into normalized tactile proximity only.
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
}
