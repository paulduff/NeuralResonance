using System.Text.Json;

namespace NRE.SimAvatar;

public sealed record AvatarNeuronalMotorState(
    bool Active,
    long Tick,
    long Sequence,
    double LeftDrive,
    double RightDrive,
    double ManipulatorDrive,
    double LeftShoulderSagittalDrive,
    double RightShoulderSagittalDrive,
    double LeftShoulderCoronalDrive,
    double RightShoulderCoronalDrive,
    double LeftElbowDrive,
    double RightElbowDrive,
    double HeadYawDrive,
    double HeadPitchDrive,
    double StandDrive,
    double CrouchDrive,
    double SitDrive,
    double LieDrive,
    double Confidence,
    double MinimumOutputConfidence,
    int MaxPopulationEventsPerSide)
{
    public static AvatarNeuronalMotorState UnavailableDefault { get; } = new(
        Active: false,
        Tick: 0,
        Sequence: 0,
        LeftDrive: 0.0,
        RightDrive: 0.0,
        ManipulatorDrive: 0.0,
        LeftShoulderSagittalDrive: 0.0,
        RightShoulderSagittalDrive: 0.0,
        LeftShoulderCoronalDrive: 0.0,
        RightShoulderCoronalDrive: 0.0,
        LeftElbowDrive: 0.0,
        RightElbowDrive: 0.0,
        HeadYawDrive: 0.0,
        HeadPitchDrive: 0.0,
        StandDrive: 0.0,
        CrouchDrive: 0.0,
        SitDrive: 0.0,
        LieDrive: 0.0,
        Confidence: 0.0,
        MinimumOutputConfidence: 1.0,
        MaxPopulationEventsPerSide: 12);
}

/// <summary>
/// Converts read-only neuronal motor telemetry into a body population code.
/// Goal names and symbolic directives are never read while creating output.
/// </summary>
public static class AvatarNeuronalMotorBridge
{
    public static List<AvatarDispatchSpike> Compose(
        JsonElement stateElement,
        IReadOnlyList<AvatarDispatchSpike> originalDispatches,
        long lastNeuronalTick,
        out long nextNeuronalTick,
        out AvatarNeuronalMotorState neuronalState)
    {
        ArgumentNullException.ThrowIfNull(originalDispatches);
        neuronalState = Parse(stateElement);
        nextNeuronalTick = lastNeuronalTick;

        var result = FilterNonNeuronalMotorTraffic(originalDispatches);

        if (neuronalState.Tick <= lastNeuronalTick ||
            !neuronalState.Active ||
            neuronalState.Confidence < neuronalState.MinimumOutputConfidence)
        {
            return result;
        }

        nextNeuronalTick = neuronalState.Tick;
        AppendPopulationEvents(result, neuronalState, "L", neuronalState.LeftDrive);
        AppendPopulationEvents(result, neuronalState, "R", neuronalState.RightDrive);
        AppendManipulatorEvents(result, neuronalState);
        AppendSignedEffectorEvents(result, neuronalState, "arm:left:shoulder:sagittal",
            neuronalState.LeftShoulderSagittalDrive, "L");
        AppendSignedEffectorEvents(result, neuronalState, "arm:right:shoulder:sagittal",
            neuronalState.RightShoulderSagittalDrive, "R");
        AppendSignedEffectorEvents(result, neuronalState, "arm:left:shoulder:coronal",
            neuronalState.LeftShoulderCoronalDrive, "L");
        AppendSignedEffectorEvents(result, neuronalState, "arm:right:shoulder:coronal",
            neuronalState.RightShoulderCoronalDrive, "R");
        AppendSignedEffectorEvents(result, neuronalState, "arm:left:elbow",
            neuronalState.LeftElbowDrive, "L");
        AppendSignedEffectorEvents(result, neuronalState, "arm:right:elbow",
            neuronalState.RightElbowDrive, "R");
        AppendSignedEffectorEvents(result, neuronalState, "orient:yaw", neuronalState.HeadYawDrive);
        AppendSignedEffectorEvents(result, neuronalState, "orient:pitch", neuronalState.HeadPitchDrive);
        AppendPostureEvents(result, neuronalState, "stand", neuronalState.StandDrive);
        AppendPostureEvents(result, neuronalState, "crouch", neuronalState.CrouchDrive);
        AppendPostureEvents(result, neuronalState, "sit", neuronalState.SitDrive);
        AppendPostureEvents(result, neuronalState, "lie", neuronalState.LieDrive);
        return result;
    }

    public static AvatarNeuronalMotorState Parse(JsonElement stateElement)
    {
        if (!AvatarJson.TryGetProperty(stateElement, "neuronalMotor", out var motor) ||
            motor.ValueKind != JsonValueKind.Object)
        {
            return AvatarNeuronalMotorState.UnavailableDefault;
        }

        return new AvatarNeuronalMotorState(
            Active: AvatarJson.GetBool(motor, "active"),
            Tick: Math.Max(0, AvatarJson.GetLong(motor, "tick")),
            Sequence: Math.Max(0, AvatarJson.GetLong(motor, "sequence")),
            LeftDrive: Math.Clamp(AvatarJson.GetDouble(motor, "leftDrive"), -1.0, 1.0),
            RightDrive: Math.Clamp(AvatarJson.GetDouble(motor, "rightDrive"), -1.0, 1.0),
            ManipulatorDrive: Math.Clamp(AvatarJson.GetDouble(motor, "manipulatorDrive"), 0.0, 1.0),
            LeftShoulderSagittalDrive: Math.Clamp(
                AvatarJson.GetDouble(motor, "leftShoulderSagittalDrive"), -1.0, 1.0),
            RightShoulderSagittalDrive: Math.Clamp(
                AvatarJson.GetDouble(motor, "rightShoulderSagittalDrive"), -1.0, 1.0),
            LeftShoulderCoronalDrive: Math.Clamp(
                AvatarJson.GetDouble(motor, "leftShoulderCoronalDrive"), -1.0, 1.0),
            RightShoulderCoronalDrive: Math.Clamp(
                AvatarJson.GetDouble(motor, "rightShoulderCoronalDrive"), -1.0, 1.0),
            LeftElbowDrive: Math.Clamp(AvatarJson.GetDouble(motor, "leftElbowDrive"), -1.0, 1.0),
            RightElbowDrive: Math.Clamp(AvatarJson.GetDouble(motor, "rightElbowDrive"), -1.0, 1.0),
            HeadYawDrive: Math.Clamp(AvatarJson.GetDouble(motor, "headYawDrive"), -1.0, 1.0),
            HeadPitchDrive: Math.Clamp(AvatarJson.GetDouble(motor, "headPitchDrive"), -1.0, 1.0),
            StandDrive: Math.Clamp(AvatarJson.GetDouble(motor, "standDrive"), 0.0, 1.0),
            CrouchDrive: Math.Clamp(AvatarJson.GetDouble(motor, "crouchDrive"), 0.0, 1.0),
            SitDrive: Math.Clamp(AvatarJson.GetDouble(motor, "sitDrive"), 0.0, 1.0),
            LieDrive: Math.Clamp(AvatarJson.GetDouble(motor, "lieDrive"), 0.0, 1.0),
            Confidence: Math.Clamp(AvatarJson.GetDouble(motor, "confidence"), 0.0, 1.0),
            MinimumOutputConfidence: Math.Clamp(AvatarJson.GetDouble(motor, "minimumOutputConfidence"), 0.0, 1.0),
            MaxPopulationEventsPerSide: Math.Clamp(AvatarJson.GetInt(motor, "maxPopulationEventsPerSide"), 1, 64));
    }

    private static List<AvatarDispatchSpike> FilterNonNeuronalMotorTraffic(
        IReadOnlyList<AvatarDispatchSpike> originalDispatches)
    {
        var result = new List<AvatarDispatchSpike>(originalDispatches.Count);
        for (var i = 0; i < originalDispatches.Count; i++)
        {
            var dispatch = originalDispatches[i];
            if (!AvatarMotorCatalog.IsMotorStructure(dispatch.SourceStructure))
            {
                result.Add(dispatch);
            }
        }

        return result;
    }

    private static void AppendPopulationEvents(
        List<AvatarDispatchSpike> output,
        AvatarNeuronalMotorState state,
        string hemisphere,
        double drive)
    {
        var magnitude = Math.Clamp(Math.Abs(drive), 0.0, 1.0);
        if (magnitude < 0.01)
        {
            return;
        }

        var eventCount = Math.Clamp(
            (int)Math.Round(magnitude * state.MaxPopulationEventsPerSide, MidpointRounding.AwayFromZero),
            1,
            state.MaxPopulationEventsPerSide);
        var polarity = drive >= 0.0 ? "excitatory" : "inhibitory";
        var wallClockMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < eventCount; i++)
        {
            output.Add(new AvatarDispatchSpike(
                SourceStructure: "SpinalCordMotor",
                SourceHemisphere: hemisphere,
                WallClockUnixMs: wallClockMs,
                SourceNeuronId: $"population:{hemisphere.ToLowerInvariant()}:{polarity}:{state.Tick}:{i}"));
        }
    }

    private static void AppendManipulatorEvents(
        List<AvatarDispatchSpike> output,
        AvatarNeuronalMotorState state)
    {
        var magnitude = Math.Clamp(state.ManipulatorDrive, 0.0, 1.0);
        if (magnitude < 0.01)
        {
            return;
        }

        var eventCount = Math.Clamp(
            (int)Math.Round(magnitude * state.MaxPopulationEventsPerSide, MidpointRounding.AwayFromZero),
            1,
            state.MaxPopulationEventsPerSide);
        var wallClockMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < eventCount; i++)
        {
            output.Add(new AvatarDispatchSpike(
                SourceStructure: "SpinalCordMotor",
                SourceHemisphere: "M",
                WallClockUnixMs: wallClockMs,
                SourceNeuronId: $"effector:manipulator:excitatory:{state.Tick}:{i}:n{state.MaxPopulationEventsPerSide}"));
        }
    }

    private static void AppendPostureEvents(
        List<AvatarDispatchSpike> output,
        AvatarNeuronalMotorState state,
        string posture,
        double drive)
    {
        var magnitude = Math.Clamp(drive, 0.0, 1.0);
        if (magnitude < 0.01)
        {
            return;
        }

        var eventCount = Math.Clamp(
            (int)Math.Round(magnitude * state.MaxPopulationEventsPerSide, MidpointRounding.AwayFromZero),
            1,
            state.MaxPopulationEventsPerSide);
        var wallClockMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < eventCount; i++)
        {
            output.Add(new AvatarDispatchSpike(
                SourceStructure: "SpinalCordMotor",
                SourceHemisphere: "M",
                WallClockUnixMs: wallClockMs,
                SourceNeuronId: $"effector:posture:{posture}:excitatory:{state.Tick}:{i}:n{state.MaxPopulationEventsPerSide}"));
        }
    }

    private static void AppendSignedEffectorEvents(
        List<AvatarDispatchSpike> output,
        AvatarNeuronalMotorState state,
        string effector,
        double drive,
        string hemisphere = "M")
    {
        var magnitude = Math.Clamp(Math.Abs(drive), 0.0, 1.0);
        if (magnitude < 0.01)
        {
            return;
        }

        var eventCount = Math.Clamp(
            (int)Math.Round(magnitude * state.MaxPopulationEventsPerSide, MidpointRounding.AwayFromZero),
            1,
            state.MaxPopulationEventsPerSide);
        var polarity = drive >= 0.0 ? "excitatory" : "inhibitory";
        var wallClockMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < eventCount; i++)
        {
            output.Add(new AvatarDispatchSpike(
                SourceStructure: "SpinalCordMotor",
                SourceHemisphere: hemisphere,
                WallClockUnixMs: wallClockMs,
                SourceNeuronId: $"effector:{effector}:{polarity}:{state.Tick}:{i}:n{state.MaxPopulationEventsPerSide}"));
        }
    }
}
