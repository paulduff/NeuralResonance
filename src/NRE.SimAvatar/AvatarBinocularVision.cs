namespace NRE.SimAvatar;

/// <summary>
/// Physical placement of the avatar's two eyes. It supplies camera geometry
/// only; binocular correspondence, disparity, fusion, and depth are neuronal.
/// </summary>
public static class AvatarBinocularVision
{
    public const double InterocularDistanceMeters = 0.064;

    public static AvatarBinocularEyePose ComputeEyePose(
        double bodyX,
        double bodyZ,
        double headingDegrees)
    {
        if (!double.IsFinite(bodyX) ||
            !double.IsFinite(bodyZ) ||
            !double.IsFinite(headingDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(bodyX), "Eye pose inputs must be finite.");
        }

        var headingRadians = AvatarKinematics.DegreesToRadians(headingDegrees);
        var rightX = Math.Cos(headingRadians);
        var rightZ = -Math.Sin(headingRadians);
        var halfSeparation = InterocularDistanceMeters * 0.5;
        return new AvatarBinocularEyePose(
            new AvatarEyePose(
                bodyX - (rightX * halfSeparation),
                bodyZ - (rightZ * halfSeparation),
                headingDegrees),
            new AvatarEyePose(
                bodyX + (rightX * halfSeparation),
                bodyZ + (rightZ * halfSeparation),
                headingDegrees));
    }
}

public readonly record struct AvatarEyePose(
    double X,
    double Z,
    double HeadingDegrees);

public readonly record struct AvatarBinocularEyePose(
    AvatarEyePose Left,
    AvatarEyePose Right);
