internal static class NeuronalWithdrawalReflex
{
    private static readonly HashSet<string> SupportedRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        "hand",
        "forearm",
        "arm",
        "foot",
        "shin",
        "knee",
        "thigh",
        "chest",
        "pelvis"
    };

    public static bool SupportsAnatomicalRegion(string? region)
        => !string.IsNullOrWhiteSpace(region) && SupportedRegions.Contains(region);

    public static bool IsWithdrawalChannel(int channel)
        => channel is
            NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel or
            NeuronalActionSelectionDecoder.RightShoulderExtensionChannel or
            NeuronalActionSelectionDecoder.LeftShoulderAbductionChannel or
            NeuronalActionSelectionDecoder.RightShoulderAbductionChannel or
            NeuronalActionSelectionDecoder.LeftElbowFlexionChannel or
            NeuronalActionSelectionDecoder.RightElbowFlexionChannel or
            NeuronalActionSelectionDecoder.LeftHipAbductionChannel or
            NeuronalActionSelectionDecoder.RightHipAbductionChannel or
            NeuronalActionSelectionDecoder.LeftAnkleDorsiflexionChannel or
            NeuronalActionSelectionDecoder.RightAnkleDorsiflexionChannel or
            NeuronalActionSelectionDecoder.ForwardChannel or
            NeuronalActionSelectionDecoder.ReverseChannel or
            NeuronalActionSelectionDecoder.LeftTurnChannel or
            NeuronalActionSelectionDecoder.RightTurnChannel or
            NeuronalActionSelectionDecoder.TrunkRotateLeftChannel or
            NeuronalActionSelectionDecoder.TrunkRotateRightChannel;
}
