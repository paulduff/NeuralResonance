namespace NRE.SimAvatar;

public static class AvatarPhysicalInteraction
{
    public static bool IsWithinEffectorCone(
        double bodyX,
        double bodyZ,
        double headingDeg,
        double targetX,
        double targetZ,
        double maximumDistance,
        double halfAngleDeg)
    {
        var dx = targetX - bodyX;
        var dz = targetZ - bodyZ;
        var distanceSquared = (dx * dx) + (dz * dz);
        if (distanceSquared > maximumDistance * maximumDistance)
        {
            return false;
        }

        if (distanceSquared < 0.000001)
        {
            return true;
        }

        var targetHeading = Math.Atan2(dx, dz) * (180.0 / Math.PI);
        var offset = NormalizeSignedDegrees(targetHeading - headingDeg);
        return Math.Abs(offset) <= Math.Clamp(halfAngleDeg, 0.0, 180.0);
    }

    public static double NormalizeSignedDegrees(double angle)
    {
        var wrapped = ((angle + 540.0) % 360.0) - 180.0;
        return wrapped == -180.0 ? 180.0 : wrapped;
    }
}
