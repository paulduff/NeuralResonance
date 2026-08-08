namespace NRE.SimAvatar;

public static class AvatarEffectorCatalog
{
    private const string ManipulatorPrefix = "effector:manipulator:";

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
}
