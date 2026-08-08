namespace NRE.SimAvatar;

public readonly record struct AvatarEffectorAssessment(
    double Distance,
    double HeadingOffsetDegrees,
    bool WithinReach,
    bool WithinCone)
{
    public bool CanContact => WithinReach && WithinCone;
}

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
        return AssessEffectorCone(
            bodyX,
            bodyZ,
            headingDeg,
            targetX,
            targetZ,
            maximumDistance,
            halfAngleDeg).CanContact;
    }

    public static AvatarEffectorAssessment AssessEffectorCone(
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
        var distance = Math.Sqrt(distanceSquared);
        var withinReach = distance <= Math.Max(0.0, maximumDistance);
        if (distanceSquared < 0.000001)
        {
            return new AvatarEffectorAssessment(distance, 0.0, withinReach, WithinCone: true);
        }

        var targetHeading = Math.Atan2(dx, dz) * (180.0 / Math.PI);
        var offset = NormalizeSignedDegrees(targetHeading - headingDeg);
        var withinCone = Math.Abs(offset) <= Math.Clamp(halfAngleDeg, 0.0, 180.0);
        return new AvatarEffectorAssessment(distance, offset, withinReach, withinCone);
    }

    public static double NormalizeSignedDegrees(double angle)
    {
        var wrapped = ((angle + 540.0) % 360.0) - 180.0;
        return wrapped == -180.0 ? 180.0 : wrapped;
    }
}
