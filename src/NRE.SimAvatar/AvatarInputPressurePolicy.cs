namespace NRE.SimAvatar;

public enum AvatarInputPriority
{
    Optional,
    Critical
}

public readonly record struct AvatarInputPressureDecision(
    bool ShouldPause,
    bool ShouldThrottle,
    long MinimumIntervalMs,
    string Reason)
{
    public static AvatarInputPressureDecision Allow(long minimumIntervalMs)
        => new(false, false, Math.Max(1L, minimumIntervalMs), string.Empty);
}

public static class AvatarInputPressurePolicy
{
    public const double OptionalPausePressure = 0.35;
    public const double OptionalThrottlePressure = 0.24;
    public const double CriticalThrottlePressure = 0.35;
    public const double CriticalLifelinePressure = 0.65;

    public static AvatarInputPressureDecision Evaluate(
        double engineInputPressure,
        int telemetryFailureStreak,
        bool channelGatePaused,
        string channelGateReason,
        AvatarInputPriority priority,
        long normalIntervalMs)
    {
        normalIntervalMs = Math.Max(1L, normalIntervalMs);
        if (channelGatePaused)
        {
            return new AvatarInputPressureDecision(
                true,
                false,
                normalIntervalMs,
                string.IsNullOrWhiteSpace(channelGateReason)
                    ? "channel retry backoff"
                    : channelGateReason);
        }

        var pressure = Math.Clamp(engineInputPressure, 0.0, 1.0);
        if (priority == AvatarInputPriority.Critical)
        {
            if (pressure >= CriticalLifelinePressure)
            {
                return new AvatarInputPressureDecision(
                    false,
                    true,
                    Math.Max(normalIntervalMs * 6L, 1_500L),
                    $"engine input pressure {pressure:0.00}; critical sensory lifeline");
            }

            if (pressure >= CriticalThrottlePressure || telemetryFailureStreak >= 4)
            {
                var reason = pressure >= CriticalThrottlePressure
                    ? $"engine input pressure {pressure:0.00}; critical sensory throttle"
                    : $"telemetry delayed (failures {telemetryFailureStreak}); critical sensory throttle";
                return new AvatarInputPressureDecision(
                    false,
                    true,
                    Math.Max(normalIntervalMs * 3L, 750L),
                    reason);
            }

            return AvatarInputPressureDecision.Allow(normalIntervalMs);
        }

        if (telemetryFailureStreak >= 2)
        {
            return new AvatarInputPressureDecision(
                true,
                false,
                normalIntervalMs,
                $"telemetry delayed (failures {telemetryFailureStreak})");
        }

        if (pressure >= OptionalPausePressure)
        {
            return new AvatarInputPressureDecision(
                true,
                false,
                normalIntervalMs,
                $"engine input pressure {pressure:0.00}");
        }

        if (pressure >= OptionalThrottlePressure)
        {
            return new AvatarInputPressureDecision(
                false,
                true,
                Math.Max(normalIntervalMs * 2L, normalIntervalMs + 500L),
                $"engine input pressure {pressure:0.00}; optional input throttle");
        }

        return AvatarInputPressureDecision.Allow(normalIntervalMs);
    }
}
