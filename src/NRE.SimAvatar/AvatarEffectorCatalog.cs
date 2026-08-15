namespace NRE.SimAvatar;

public static class AvatarEffectorCatalog
{
    private const string ManipulatorPrefix = "effector:manipulator:";
    private const string PosturePrefix = "effector:posture:";
    private const string OrientYawPrefix = "effector:orient:yaw:";
    private const string OrientPitchPrefix = "effector:orient:pitch:";

    public static bool IsManipulatorEvent(AvatarDispatchSpike dispatch)
        => !string.IsNullOrWhiteSpace(dispatch.SourceNeuronId) &&
           dispatch.SourceNeuronId.StartsWith(ManipulatorPrefix, StringComparison.OrdinalIgnoreCase);

    public static (double DriveDelta, int Events) SummarizeManipulatorDrive(
        IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        ArgumentNullException.ThrowIfNull(dispatches);

        var drive = 0.0;
        var events = 0;
        for (var i = 0; i < dispatches.Count; i++)
        {
            var neuronId = dispatches[i].SourceNeuronId;
            if (!IsManipulatorEvent(dispatches[i]))
            {
                continue;
            }

            drive += neuronId.Contains(":inhibitory:", StringComparison.OrdinalIgnoreCase)
                ? -1.0
                : 1.0;
            events++;
        }

        return (drive, events);
    }

    public static AvatarPostureDrive SummarizePostureDrive(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        ArgumentNullException.ThrowIfNull(dispatches);
        var stand = 0.0;
        var crouch = 0.0;
        var sit = 0.0;
        var lie = 0.0;
        var events = 0;
        for (var i = 0; i < dispatches.Count; i++)
        {
            var neuronId = dispatches[i].SourceNeuronId;
            if (string.IsNullOrWhiteSpace(neuronId) ||
                !neuronId.StartsWith(PosturePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sign = neuronId.Contains(":inhibitory:", StringComparison.OrdinalIgnoreCase) ? -1.0 : 1.0;
            if (neuronId.StartsWith($"{PosturePrefix}stand:", StringComparison.OrdinalIgnoreCase))
            {
                stand += sign;
            }
            else if (neuronId.StartsWith($"{PosturePrefix}crouch:", StringComparison.OrdinalIgnoreCase))
            {
                crouch += sign;
            }
            else if (neuronId.StartsWith($"{PosturePrefix}sit:", StringComparison.OrdinalIgnoreCase))
            {
                sit += sign;
            }
            else if (neuronId.StartsWith($"{PosturePrefix}lie:", StringComparison.OrdinalIgnoreCase))
            {
                lie += sign;
            }
            events++;
        }

        return new AvatarPostureDrive(stand, crouch, sit, lie, events);
    }

    public static AvatarOrientingDrive SummarizeOrientingDrive(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        ArgumentNullException.ThrowIfNull(dispatches);
        var yaw = 0.0;
        var pitch = 0.0;
        var events = 0;
        for (var i = 0; i < dispatches.Count; i++)
        {
            var neuronId = dispatches[i].SourceNeuronId;
            if (string.IsNullOrWhiteSpace(neuronId))
            {
                continue;
            }

            var sign = neuronId.Contains(":inhibitory:", StringComparison.OrdinalIgnoreCase) ? -1.0 : 1.0;
            if (neuronId.StartsWith(OrientYawPrefix, StringComparison.OrdinalIgnoreCase))
            {
                yaw += sign;
                events++;
            }
            else if (neuronId.StartsWith(OrientPitchPrefix, StringComparison.OrdinalIgnoreCase))
            {
                pitch += sign;
                events++;
            }
        }

        return new AvatarOrientingDrive(yaw, pitch, events);
    }
}

public readonly record struct AvatarPostureDrive(
    double StandDelta,
    double CrouchDelta,
    double SitDelta,
    double LieDelta,
    int Events);

public readonly record struct AvatarOrientingDrive(
    double YawDelta,
    double PitchDelta,
    int Events);
