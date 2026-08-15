namespace NRE.SimAvatar;

public static class AvatarEffectorCatalog
{
    private const int LegacyPopulationSize = 12;
    private const string ManipulatorPrefix = "effector:manipulator:";
    private const string ArmPrefix = "effector:arm:";
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

            drive += PopulationContribution(neuronId);
            events++;
        }

        return (drive, events);
    }

    public static AvatarArmDrive SummarizeArmDrive(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        ArgumentNullException.ThrowIfNull(dispatches);
        var leftShoulderSagittal = 0.0;
        var rightShoulderSagittal = 0.0;
        var leftShoulderCoronal = 0.0;
        var rightShoulderCoronal = 0.0;
        var leftElbow = 0.0;
        var rightElbow = 0.0;
        var events = 0;
        for (var i = 0; i < dispatches.Count; i++)
        {
            var neuronId = dispatches[i].SourceNeuronId;
            if (string.IsNullOrWhiteSpace(neuronId) ||
                !neuronId.StartsWith(ArmPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var contribution = PopulationContribution(neuronId);
            if (neuronId.StartsWith($"{ArmPrefix}left:shoulder:sagittal:", StringComparison.OrdinalIgnoreCase))
            {
                leftShoulderSagittal += contribution;
            }
            else if (neuronId.StartsWith($"{ArmPrefix}right:shoulder:sagittal:", StringComparison.OrdinalIgnoreCase))
            {
                rightShoulderSagittal += contribution;
            }
            else if (neuronId.StartsWith($"{ArmPrefix}left:shoulder:coronal:", StringComparison.OrdinalIgnoreCase))
            {
                leftShoulderCoronal += contribution;
            }
            else if (neuronId.StartsWith($"{ArmPrefix}right:shoulder:coronal:", StringComparison.OrdinalIgnoreCase))
            {
                rightShoulderCoronal += contribution;
            }
            else if (neuronId.StartsWith($"{ArmPrefix}left:elbow:", StringComparison.OrdinalIgnoreCase))
            {
                leftElbow += contribution;
            }
            else if (neuronId.StartsWith($"{ArmPrefix}right:elbow:", StringComparison.OrdinalIgnoreCase))
            {
                rightElbow += contribution;
            }
            else
            {
                continue;
            }

            events++;
        }

        return new AvatarArmDrive(
            leftShoulderSagittal,
            rightShoulderSagittal,
            leftShoulderCoronal,
            rightShoulderCoronal,
            leftElbow,
            rightElbow,
            events);
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

            var contribution = PopulationContribution(neuronId);
            if (neuronId.StartsWith($"{PosturePrefix}stand:", StringComparison.OrdinalIgnoreCase))
            {
                stand += contribution;
            }
            else if (neuronId.StartsWith($"{PosturePrefix}crouch:", StringComparison.OrdinalIgnoreCase))
            {
                crouch += contribution;
            }
            else if (neuronId.StartsWith($"{PosturePrefix}sit:", StringComparison.OrdinalIgnoreCase))
            {
                sit += contribution;
            }
            else if (neuronId.StartsWith($"{PosturePrefix}lie:", StringComparison.OrdinalIgnoreCase))
            {
                lie += contribution;
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

            var contribution = PopulationContribution(neuronId);
            if (neuronId.StartsWith(OrientYawPrefix, StringComparison.OrdinalIgnoreCase))
            {
                yaw += contribution;
                events++;
            }
            else if (neuronId.StartsWith(OrientPitchPrefix, StringComparison.OrdinalIgnoreCase))
            {
                pitch += contribution;
                events++;
            }
        }

        return new AvatarOrientingDrive(yaw, pitch, events);
    }

    private static double PopulationContribution(string neuronId)
    {
        var populationSize = LegacyPopulationSize;
        var finalSeparator = neuronId.LastIndexOf(':');
        if (finalSeparator >= 0 && finalSeparator + 2 < neuronId.Length &&
            neuronId[finalSeparator + 1] is 'n' or 'N' &&
            int.TryParse(neuronId.AsSpan(finalSeparator + 2), out var encodedPopulationSize))
        {
            populationSize = Math.Clamp(encodedPopulationSize, 1, 64);
        }

        var sign = neuronId.Contains(":inhibitory:", StringComparison.OrdinalIgnoreCase) ? -1.0 : 1.0;
        return sign / populationSize;
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

public readonly record struct AvatarArmDrive(
    double LeftShoulderSagittalDelta,
    double RightShoulderSagittalDelta,
    double LeftShoulderCoronalDelta,
    double RightShoulderCoronalDelta,
    double LeftElbowDelta,
    double RightElbowDelta,
    int Events);
