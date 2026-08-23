using NeuralResonanceEngine.Protocol;

internal static class WithdrawalReflexTopology
{
    internal const float SpinalNociceptiveRelayGain = 2.75f;
    internal const float MaximumRecurrentInhibition = 0.90f;
    internal const float MaximumAfferentSilenceMilliseconds = 500f;

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

    public static bool TryProjectInbound(
        SpikeMessage message,
        int targetNeuronCount,
        out int targetNeuronIndex)
    {
        targetNeuronIndex = 0;
        if (targetNeuronCount <= 0 || !TryResolveSourceRoute(message, out var route))
        {
            return false;
        }

        var fiber = ResolveFiber(message.SourceNeuronId, message.SynapseId);
        targetNeuronIndex = ActionChannelTopology.ProjectToChannel(
            route.ChannelIndex,
            targetNeuronCount,
            StructureId.SpinalCordMotor,
            fiber / Math.Max(1, route.ChannelCount));
        return true;
    }

    internal static bool TryResolveSourceRoute(
        SpikeMessage message,
        out WithdrawalSourceRoute route)
    {
        route = default;
        if (!IsPrimaryWithdrawalAfferent(message))
        {
            return false;
        }

        var sourceNeuronId = message.SourceNeuronId;
        var region = ResolveRegion(sourceNeuronId);
        var channels = ResolveWithdrawalChannels(sourceNeuronId, region);
        if (channels.Length == 0)
        {
            return false;
        }

        var fiber = ResolveFiber(sourceNeuronId, message.SynapseId);
        var channel = channels[Math.Abs(fiber % channels.Length)];
        var bodySide = sourceNeuronId.StartsWith("R:", StringComparison.OrdinalIgnoreCase)
            ? "left"
            : "right";
        var normalSector = ResolveContactNormalSector(sourceNeuronId);
        route = new WithdrawalSourceRoute(
            SourceKey: $"{bodySide}:{region}:{normalSector}:channel_{channel}",
            BodySide: bodySide,
            Region: region,
            ContactNormalSector: normalSector,
            ChannelIndex: channel,
            ChannelCount: channels.Length,
            MotorProjection: ResolveMotorProjection(channel));
        return true;
    }

    internal static bool IsEvokedWithdrawalInput(SpikeMessage message)
        => IsPrimaryWithdrawalAfferent(message);

    internal static float ApplySpinalRelayEfficacy(SpikeMessage message, float effectiveQuanta)
    {
        if (!float.IsFinite(effectiveQuanta) || !IsPrimaryWithdrawalAfferent(message))
        {
            return effectiveQuanta;
        }

        return Math.Clamp(effectiveQuanta * SpinalNociceptiveRelayGain, 0.05f, 5f);
    }

    internal static float ApplyRecurrentInhibition(
        SpikeMessage message,
        float effectiveQuanta,
        float inhibitoryTrace)
    {
        if (!float.IsFinite(effectiveQuanta) || !IsPrimaryWithdrawalAfferent(message))
        {
            return effectiveQuanta;
        }

        var inhibition = Math.Clamp(inhibitoryTrace, 0f, 1f);
        if (inhibition <= 0f)
        {
            return effectiveQuanta;
        }

        // A local inhibitory interneuron suppresses unchanged convergent input.
        // Larger nociceptive volleys retain an escape path so a worsening load
        // can recruit a fresh protective response during the recovery period.
        var threat = Math.Clamp((message.VesicleQuanta - 0.75f) / 4.25f, 0f, 1f);
        var acuteEscape = Math.Clamp((threat - 0.25f) / 0.75f, 0f, 1f);
        var inhibitoryGain = MaximumRecurrentInhibition - (acuteEscape * 0.72f);
        return Math.Clamp(
            effectiveQuanta * (1f - (inhibition * inhibitoryGain)),
            0.05f,
            5f);
    }

    internal static bool IsWithdrawalChannel(int channel)
        => channel is
            ActionChannelTopology.LeftShoulderExtensionChannel or
            ActionChannelTopology.RightShoulderExtensionChannel or
            ActionChannelTopology.LeftShoulderAbductionChannel or
            ActionChannelTopology.RightShoulderAbductionChannel or
            ActionChannelTopology.LeftElbowFlexionChannel or
            ActionChannelTopology.RightElbowFlexionChannel or
            ActionChannelTopology.LeftHipAbductionChannel or
            ActionChannelTopology.RightHipAbductionChannel or
            ActionChannelTopology.LeftAnkleDorsiflexionChannel or
            ActionChannelTopology.RightAnkleDorsiflexionChannel or
            ActionChannelTopology.ForwardChannel or
            ActionChannelTopology.ReverseChannel or
            ActionChannelTopology.LeftTurnChannel or
            ActionChannelTopology.RightTurnChannel or
            ActionChannelTopology.TrunkRotateLeftChannel or
            ActionChannelTopology.TrunkRotateRightChannel;

    internal static string ResolveMotorProjection(int channel)
        => channel switch
        {
            ActionChannelTopology.ForwardChannel => "forward",
            ActionChannelTopology.LeftTurnChannel => "turn_left",
            ActionChannelTopology.RightTurnChannel => "turn_right",
            ActionChannelTopology.ReverseChannel => "reverse",
            ActionChannelTopology.LeftShoulderExtensionChannel => "left_shoulder_extension",
            ActionChannelTopology.RightShoulderExtensionChannel => "right_shoulder_extension",
            ActionChannelTopology.LeftShoulderAbductionChannel => "left_shoulder_abduction",
            ActionChannelTopology.RightShoulderAbductionChannel => "right_shoulder_abduction",
            ActionChannelTopology.LeftElbowFlexionChannel => "left_elbow_flexion",
            ActionChannelTopology.RightElbowFlexionChannel => "right_elbow_flexion",
            ActionChannelTopology.LeftHipAbductionChannel => "left_hip_abduction",
            ActionChannelTopology.RightHipAbductionChannel => "right_hip_abduction",
            ActionChannelTopology.LeftAnkleDorsiflexionChannel => "left_ankle_dorsiflexion",
            ActionChannelTopology.RightAnkleDorsiflexionChannel => "right_ankle_dorsiflexion",
            ActionChannelTopology.TrunkRotateLeftChannel => "trunk_rotate_left",
            ActionChannelTopology.TrunkRotateRightChannel => "trunk_rotate_right",
            _ => $"channel_{channel}"
        };

    private static bool IsPrimaryWithdrawalAfferent(SpikeMessage message)
        => message.SourceStructure == StructureId.SomaticAfferents &&
           message.TargetStructure == StructureId.SpinalCordMotor &&
           message.Neurotransmitter == NTEnum.GLUTAMATE &&
           message.VesicleQuanta >= 0.75f &&
           message.SourceNeuronId.Contains(
               "free_nerve_ending_mechanonociceptor",
               StringComparison.OrdinalIgnoreCase) &&
           SupportsAnatomicalRegion(ResolveRegion(message.SourceNeuronId));

    private static int[] ResolveWithdrawalChannels(string sourceNeuronId, string region)
    {
        var rightBody = sourceNeuronId.StartsWith("L:", StringComparison.OrdinalIgnoreCase);
        var leftBody = sourceNeuronId.StartsWith("R:", StringComparison.OrdinalIgnoreCase);
        if (!rightBody && !leftBody)
        {
            return [];
        }

        if (region is "hand" or "forearm")
        {
            int[] limbChannels = leftBody
                ? [ActionChannelTopology.LeftShoulderExtensionChannel, ActionChannelTopology.LeftElbowFlexionChannel]
                : [ActionChannelTopology.RightShoulderExtensionChannel, ActionChannelTopology.RightElbowFlexionChannel];
            return limbChannels.Concat(ResolveAxialWithdrawalChannels(sourceNeuronId)).ToArray();
        }

        if (region == "arm")
        {
            int[] limbChannels = leftBody
                ? [ActionChannelTopology.LeftShoulderExtensionChannel, ActionChannelTopology.LeftShoulderAbductionChannel]
                : [ActionChannelTopology.RightShoulderExtensionChannel, ActionChannelTopology.RightShoulderAbductionChannel];
            return limbChannels.Concat(ResolveAxialWithdrawalChannels(sourceNeuronId)).ToArray();
        }

        if (region is "chest" or "pelvis")
        {
            return ResolveAxialWithdrawalChannels(sourceNeuronId);
        }

        return leftBody
            ? [ActionChannelTopology.LeftAnkleDorsiflexionChannel, ActionChannelTopology.LeftHipAbductionChannel]
            : [ActionChannelTopology.RightAnkleDorsiflexionChannel, ActionChannelTopology.RightHipAbductionChannel];
    }

    private static int[] ResolveAxialWithdrawalChannels(string sourceNeuronId)
    {
        if (sourceNeuronId.Contains("normal_z_pos", StringComparison.OrdinalIgnoreCase))
        {
            return [ActionChannelTopology.ForwardChannel];
        }

        if (sourceNeuronId.Contains("normal_z_neg", StringComparison.OrdinalIgnoreCase))
        {
            return [ActionChannelTopology.ReverseChannel];
        }

        if (sourceNeuronId.Contains("normal_x_pos", StringComparison.OrdinalIgnoreCase))
        {
            return [ActionChannelTopology.RightTurnChannel, ActionChannelTopology.TrunkRotateRightChannel];
        }

        if (sourceNeuronId.Contains("normal_x_neg", StringComparison.OrdinalIgnoreCase))
        {
            return [ActionChannelTopology.LeftTurnChannel, ActionChannelTopology.TrunkRotateLeftChannel];
        }

        // Vertical broad support is painful when sustained, but it is not a
        // directional collision from which the spinal cord can infer a safe
        // planar escape. Supraspinal somatic pathways retain that signal.
        return [];
    }

    private static string ResolveRegion(string sourceNeuronId)
    {
        if (string.IsNullOrWhiteSpace(sourceNeuronId))
        {
            return string.Empty;
        }

        var parts = sourceNeuronId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? parts[1].ToLowerInvariant() : string.Empty;
    }

    private static string ResolveContactNormalSector(string sourceNeuronId)
    {
        foreach (var sector in new[]
                 {
                     "normal_x_pos",
                     "normal_x_neg",
                     "normal_y_pos",
                     "normal_y_neg",
                     "normal_z_pos",
                     "normal_z_neg"
                 })
        {
            if (sourceNeuronId.Contains(sector, StringComparison.OrdinalIgnoreCase))
            {
                return sector;
            }
        }

        return "unspecified";
    }

    private static int ResolveFiber(string sourceNeuronId, Guid synapseId)
    {
        var marker = sourceNeuronId.LastIndexOf("fiber_", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0 && int.TryParse(sourceNeuronId[(marker + 6)..], out var fiber))
        {
            return Math.Max(0, fiber);
        }

        var bytes = synapseId.ToByteArray();
        return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
    }
}

internal readonly record struct WithdrawalSourceRoute(
    string SourceKey,
    string BodySide,
    string Region,
    string ContactNormalSector,
    int ChannelIndex,
    int ChannelCount,
    string MotorProjection);
