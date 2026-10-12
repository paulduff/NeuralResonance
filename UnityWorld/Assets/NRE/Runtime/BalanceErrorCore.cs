// Generated from AvatarBalanceDynamics.cs; do not edit.
// Encoding SHA256: 0bf15ba50e9d40da0732298f054a69e39434acaab726940e3e5d3bec13e7d6e2
using System;
namespace Nre.UnityWorld {
public enum AvatarBalancePhase
{
    Stable,
    Marginal,
    Unstable,
    Falling,
    Fallen,
    Righting,
    Airborne,
    BroadSupport,
    Dynamic
}
public static class AvatarBalanceSensor {
    private const double FallCommitSeconds = 0.20;
    private const double AcceptablePosturalTiltRadians = 0.075;
    private const double FallenAngleRadians = 1.30;
    public static double ResolveBalanceError(
        double dynamicMargin,
        double acceptableControlMargin,
        double instabilitySeconds,
        double tilt,
        AvatarBalancePhase phase)
    {
        var marginError = dynamicMargin >= acceptableControlMargin
            ? 0.0
            : Math.Clamp((acceptableControlMargin - dynamicMargin) / 0.22, 0.0, 1.0);
        var timeError = Math.Clamp(instabilitySeconds / FallCommitSeconds, 0.0, 1.0);
        var tiltError = tilt <= AcceptablePosturalTiltRadians
            ? 0.0
            : Math.Clamp(
                (tilt - AcceptablePosturalTiltRadians) /
                (FallenAngleRadians - AcceptablePosturalTiltRadians),
                0.0,
                1.0);
        var phaseFloor = phase switch
        {
            AvatarBalancePhase.Falling => 0.68,
            AvatarBalancePhase.Fallen => 1.0,
            AvatarBalancePhase.Righting => Math.Max(0.40, Math.Sin(Math.Min(Math.PI * 0.5, tilt))),
            AvatarBalancePhase.Unstable => 0.45,
            AvatarBalancePhase.Airborne => 0.25,
            _ => 0.0
        };
        return Math.Clamp(Math.Max(phaseFloor, Math.Max(marginError, Math.Max(timeError * 0.72, tiltError))), 0.0, 1.0);
    }
}
}
