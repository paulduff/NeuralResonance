using System.Text.Json;

namespace NRE.SimAvatar;

public sealed record AvatarNeuronalMotorState(
    bool Active,
    long Tick,
    long Sequence,
    double LeftDrive,
    double RightDrive,
    double ManipulatorDrive,
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
                SourceNeuronId: $"effector:manipulator:excitatory:{state.Tick}:{i}"));
        }
    }
}
